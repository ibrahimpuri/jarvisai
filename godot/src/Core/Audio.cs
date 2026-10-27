// Procedural sound: every effect is synthesised (the same recipes as the browser build),
// rendered once into a cached stereo sample, and played from a small voice pool.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

// Offline synthesis into a stereo float buffer.
public class Synth
{
    public const int RATE = 44100;
    public float[] L, R;
    readonly float panL, panR;
    static readonly Random rng = new();

    public Synth(float seconds, float pan)
    {
        int n = (int)(seconds * RATE) + 1;
        L = new float[n]; R = new float[n];
        float a = (Math.Clamp(pan, -1, 1) + 1) * MathF.PI / 4;
        panL = MathF.Cos(a); panR = MathF.Sin(a);
    }
    void Add(int i, float v) { if (i >= 0 && i < L.Length) { L[i] += v * panL; R[i] += v * panR; } }

    // exponential attack to peak, then exponential decay, as in the WebAudio ramps
    static float Env(float t, float a, float peak, float d)
    {
        const float floor = 0.0001f;
        if (t < 0) return 0;
        if (t < a) return floor * MathF.Pow(peak / floor, t / a);
        if (t < a + d) return peak * MathF.Pow(floor / peak, (t - a) / d);
        return 0;
    }
    static float Osc(string type, float ph)
    {
        float p = ph - MathF.Floor(ph);
        return type switch
        {
            "square" => p < 0.5f ? 1 : -1,
            "triangle" => 1 - 4 * MathF.Abs(p - 0.5f),
            "sawtooth" => 2 * p - 1,
            _ => MathF.Sin(p * MathF.PI * 2),
        };
    }

    public void Tone(string type, float freq, float dur, float vol, float glide = 0, float at = 0)
    {
        int start = (int)(at * RATE), n = (int)((dur + 0.05f) * RATE);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE;
            float f = glide > 0 ? freq * MathF.Pow(MathF.Max(20, glide) / freq, MathF.Min(1, t / dur)) : freq;
            ph += f / RATE;
            // band-limit the harsh waves a little
            float s = Osc(type, (float)ph);
            Add(start + i, s * Env(t, 0.005f, vol, dur) * (type == "sine" || type == "triangle" ? 1 : 0.6f));
        }
    }

    // RBJ biquad, like WebAudio's BiquadFilterNode. Retuning keeps its state, so a filter can sweep.
    class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;
        public Biquad(string type, float freq, float q) => Set(type, freq, q);
        public void Set(string type, float freq, float q)
        {
            float w0 = 2 * MathF.PI * Math.Clamp(freq, 10, RATE / 2 - 100) / RATE, cs = MathF.Cos(w0), sn = MathF.Sin(w0);
            float Q = type == "bandpass" ? MathF.Max(0.05f, q) : MathF.Max(0.1f, MathF.Pow(10, q / 20) * 0.7071f);
            float alpha = sn / (2 * Q), a0;
            switch (type)
            {
                case "highpass": b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; break;
                case "bandpass": b0 = alpha; b1 = 0; b2 = -alpha; break;
                default: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; break;
            }
            a0 = 1 + alpha; a1 = -2 * cs; a2 = 1 - alpha;
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        }
        public float Run(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }
    public void Noise(float dur, float vol, string filter = "lowpass", float freq = 1200, float q = 0.7f, float attack = 0.005f, float at = 0)
    {
        var bq = new Biquad(filter, freq * (0.8f + (float)rng.NextDouble() * 0.4f), q);
        int start = (int)(at * RATE), n = (int)((dur + attack + 0.05f) * RATE);
        float gain = filter == "bandpass" ? 1.6f : 1;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE;
            float s = bq.Run((float)rng.NextDouble() * 2 - 1);
            Add(start + i, s * gain * Env(t, attack, vol, dur));
        }
    }
    // A bell is a stack of inharmonic partials with a long decay.
    static readonly (float m, float a)[] PARTIALS = { (0.5f, 0.6f), (1, 1), (1.19f, 0.5f), (1.56f, 0.35f), (2, 0.4f), (2.74f, 0.2f), (3.76f, 0.12f) };
    public void Bell(float freq, float vol, float decay, float at = 0)
    {
        int start = (int)(at * RATE);
        foreach (var (m, a) in PARTIALS)
        {
            float d = decay / (0.6f + m * 0.4f);
            int n = (int)((decay + 0.1f) * RATE);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / RATE;
                Add(start + i, MathF.Sin(2 * MathF.PI * freq * m * t) * Env(t, 0.003f, a * 0.4f * vol, d));
            }
        }
    }

    // Filtered noise whose band sweeps from f0 to f1: a wet tear, a squelch, a rush of water.
    public void NoiseSweep(float dur, float vol, string filter, float f0, float f1, float q = 1, float attack = 0.005f, float at = 0)
    {
        var bq = new Biquad(filter, f0, q);
        int start = (int)(at * RATE), n = (int)((dur + attack + 0.05f) * RATE);
        float gain = filter == "bandpass" ? 1.6f : 1;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE;
            if ((i & 63) == 0) bq.Set(filter, f0 * MathF.Pow(f1 / f0, MathF.Min(1, t / MathF.Max(0.001f, dur))), q);
            Add(start + i, bq.Run((float)rng.NextDouble() * 2 - 1) * gain * Env(t, attack, vol, dur));
        }
    }

    // A voice: a pulse at a pitch that glides from f0a to f0b, shaped by three formant bands that morph
    // from one vowel to another, with breath, a wet gurgle (water in the throat) and vocal fry (the voice
    // catching and rattling). Every drowned thing in the abbey speaks through this.
    public void Voice(float dur, float vol, float f0a, float f0b, (float, float, float) va, (float, float, float) vb,
        float breath = 0.2f, float gurgle = 0, float fry = 0, float vib = 0, float at = 0, float attack = 0.06f, float shift = 1)
    {
        int start = (int)(at * RATE), n = (int)(dur * RATE);
        var b1 = new Biquad("bandpass", va.Item1 * shift, 5); var b2 = new Biquad("bandpass", va.Item2 * shift, 8); var b3 = new Biquad("bandpass", va.Item3 * shift, 10);
        // the rattle and the gurgle are amplitude games that leave a rumble under the voice: cut it, and dull the top
        var hp = new Biquad("highpass", 110, 0); var hp2 = new Biquad("highpass", 110, 0); var lp = new Biquad("lowpass", 3200, 0);
        double ph = 0, fryPh = 0;
        float bub = 0, bubT = 0, jit = 0, rel = MathF.Min(0.35f, dur * 0.45f), fryHz = 30;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE, u = t / dur;
            if ((i & 127) == 0)
            {
                float k = u * u * (3 - 2 * u);
                b1.Set("bandpass", H.Lerp(va.Item1, vb.Item1, k) * shift, 5);
                b2.Set("bandpass", H.Lerp(va.Item2, vb.Item2, k) * shift, 8);
                b3.Set("bandpass", H.Lerp(va.Item3, vb.Item3, k) * shift, 10);
                jit = ((float)rng.NextDouble() * 2 - 1) * 0.012f;
                fryHz = 24 + (float)rng.NextDouble() * 18;
            }
            if (gurgle > 0)
            {
                bubT -= 1f / RATE;
                if (bubT <= 0) { bubT = 0.02f + (float)rng.NextDouble() * 0.08f; bub = 1; }
                bub *= 0.9992f;
            }
            float f0 = f0a * MathF.Pow(f0b / f0a, u) * (1 + vib * MathF.Sin(2 * MathF.PI * 5.2f * t) + jit + gurgle * 0.05f * bub);
            ph += f0 / RATE;
            float p = (float)(ph - Math.Floor(ph));
            float exc = (1 - breath) * (1 - 2 * p) + breath * ((float)rng.NextDouble() * 2 - 1) * 0.9f;
            if (fry > 0) { fryPh += fryHz / RATE; float fp = (float)(fryPh - Math.Floor(fryPh)); exc *= 1 - fry + fry * (fp < 0.3f ? 1.7f : 0.15f); }
            if (gurgle > 0) exc *= 1 - gurgle * 0.55f + gurgle * 0.9f * bub;
            float y = lp.Run(hp2.Run(hp.Run(b1.Run(exc) + b2.Run(exc) * 0.7f + b3.Run(exc) * 0.3f)));
            float env = MathF.Min(1, t / attack) * MathF.Min(1, (dur - t) / rel);
            Add(start + i, y * env * vol);
        }
    }

    // Sharp clicks, spaced to speed up or slow down: chitin, teeth, a tongue against bone.
    public void Clicks(int count, float dur, float vol, float freq, float accel = 1, float at = 0)
    {
        for (int k = 0; k < count; k++) Noise(0.006f, vol * (0.6f + (float)rng.NextDouble() * 0.4f), "highpass", freq, 2, 0.001f, at + dur * MathF.Pow(k / (float)count, accel));
    }

    // A sustained tone that swells in and dies away, darkened by a one-pole filter: strings, a choir, a hum.
    public void Swell(string type, float freq, float dur, float vol, float attack, float vib = 0, float glide = 0, float at = 0, float cutoff = 3000)
    {
        int start = (int)(at * RATE), n = (int)(dur * RATE);
        double ph = 0; float lp = 0, a = 1 - MathF.Exp(-2 * MathF.PI * cutoff / RATE);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE, u = t / dur;
            float f = (glide > 0 ? freq * MathF.Pow(glide / freq, u) : freq) * (1 + vib * MathF.Sin(2 * MathF.PI * 5.5f * t));
            ph += f / RATE;
            lp += (Osc(type, (float)ph) - lp) * a;
            float env = t < attack ? MathF.Sin(t / attack * MathF.PI / 2) : MathF.Pow(1 - (t - attack) / MathF.Max(0.001f, dur - attack), 1.6f);
            Add(start + i, lp * env * vol);
        }
    }

    // A struck tine, like the comb of a music box: the note, the bar's high partials, dying away.
    public void Tine(float freq, float vol, float decay, float at = 0)
    {
        int start = (int)(at * RATE), n = (int)((decay + 0.05f) * RATE);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / RATE, e = MathF.Exp(-t * 4.5f / decay) * MathF.Min(1, t / 0.002f);
            float v = MathF.Sin(2 * MathF.PI * freq * t) + 0.25f * MathF.Sin(2 * MathF.PI * freq * 5.4f * t) * MathF.Exp(-t * 18) + 0.12f * MathF.Sin(2 * MathF.PI * freq * 2.76f * t) * MathF.Exp(-t * 8);
            Add(start + i, v * e * vol);
        }
    }

    // Scale the whole sound so its loudest moment sits at the given peak.
    public void Normalize(float peak)
    {
        float m = 0.0001f;
        for (int i = 0; i < L.Length; i++) m = MathF.Max(m, MathF.Max(MathF.Abs(L[i]), MathF.Abs(R[i])));
        float k = peak / m;
        for (int i = 0; i < L.Length; i++) { L[i] *= k; R[i] *= k; }
    }

    public AudioStreamWav ToStream()
    {
        int n = L.Length;
        var bytes = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            short l = (short)(Math.Clamp(L[i], -1, 1) * 32000), r = (short)(Math.Clamp(R[i], -1, 1) * 32000);
            bytes[i * 4] = (byte)(l & 0xff); bytes[i * 4 + 1] = (byte)((l >> 8) & 0xff);
            bytes[i * 4 + 2] = (byte)(r & 0xff); bytes[i * 4 + 3] = (byte)((r >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, Stereo = true, MixRate = RATE, Data = bytes };
    }
}

public partial class Snd : Node
{
    static Snd inst;
    readonly List<AudioStreamPlayer> voices = new();
    int next;
    // Sounds that happen somewhere in the room: panned and quietened by distance from the view, and sent
    // through the room's reverb, so a moan in the nave carries and a step in a side passage is close.
    readonly List<AudioStreamPlayer2D> spots = new();
    int nextSpot;
    static int worldBus = -1;
    static AudioEffectReverb reverb;
    static AudioEffectLowPassFilter muffle;   // the world heard through water, from inside a brass helmet
    float under, tUnder;
    readonly List<AudioStreamPlayer> dry = new();   // menu sounds: not in the room, so no reverb and no water
    static readonly Dictionary<string, AudioStreamWav[]> cache = new();
    AudioStreamPlayer ambPlayer;
    AudioStreamGeneratorPlayback amb;
    // ambience state (smoothed toward targets like setTargetAtTime)
    float surfGain = 0.05f, surfFreq = 500, droneGain = 0, droneFreq = 180, safeGain, tSafeGain;
    float tSurfGain = 0.05f, tSurfFreq = 500, tDroneGain = 0.15f, tDroneFreq = 120;
    double phLfo, ph1, ph2, ph3, phS1, phS2, phS3, phS4;
    float lpSurf, lpDrone1, lpDrone2;
    public static float Volume = 0.7f;
    public static bool Muted;

    public override void _Ready()
    {
        inst = this;
        if (worldBus < 0)
        {
            worldBus = AudioServer.BusCount;
            AudioServer.AddBus(worldBus);
            AudioServer.SetBusName(worldBus, "World");
            AudioServer.SetBusSend(worldBus, "Master");
            reverb = new AudioEffectReverb { RoomSize = 0.6f, Damping = 0.5f, Spread = 0.9f, Hipass = 0.15f, Dry = 1, Wet = 0.15f, PredelayMsec = 35, PredelayFeedback = 0.25f };
            AudioServer.AddBusEffect(worldBus, reverb);
            muffle = new AudioEffectLowPassFilter { CutoffHz = 20000, Resonance = 0.6f };
            AudioServer.AddBusEffect(worldBus, muffle);
        }
        for (int i = 0; i < 4; i++) { var p = new AudioStreamPlayer { Bus = "Master" }; AddChild(p); dry.Add(p); }
        for (int i = 0; i < 24; i++) { var p = new AudioStreamPlayer { Bus = "World" }; AddChild(p); voices.Add(p); }
        for (int i = 0; i < 20; i++) { var p = new AudioStreamPlayer2D { Bus = "World", MaxDistance = 640, Attenuation = 1.3f }; AddChild(p); spots.Add(p); }
        var gen = new AudioStreamGenerator { MixRate = 22050, BufferLength = 0.25f };
        ambPlayer = new AudioStreamPlayer { Stream = gen, VolumeDb = -2 };
        AddChild(ambPlayer);
        ambPlayer.Play();
        amb = ambPlayer.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        SetVolume(Volume);
    }
    public static void SetVolume(float v)
    {
        Volume = v;
        AudioServer.SetBusVolumeDb(0, v <= 0.001f ? -80 : Mathf.LinearToDb(v));
    }

    public override void _Process(double delta)
    {
        // Surf: filtered noise whose level follows the tide; drone: detuned saws through a dark filter.
        float k1 = 1 - MathF.Exp(-(float)delta / 0.5f), k2 = 1 - MathF.Exp(-(float)delta / 1.0f);
        surfGain += (tSurfGain - surfGain) * k1; surfFreq += (tSurfFreq - surfFreq) * k1;
        droneGain += (tDroneGain - droneGain) * k2; droneFreq += (tDroneFreq - droneFreq) * k2;
        safeGain += (tSafeGain - safeGain) * k2;
        under += (tUnder - under) * (1 - MathF.Exp(-(float)delta / 0.25f));
        if (muffle != null) muffle.CutoffHz = 20000 * MathF.Pow(700f / 20000, under);
        if (amb == null) return;
        int frames = amb.GetFramesAvailable();
        const float rate = 22050;
        float aS = 1 - MathF.Exp(-2 * MathF.PI * surfFreq / rate), aD = 1 - MathF.Exp(-2 * MathF.PI * droneFreq / rate);
        var buf = new Vector2[frames];
        var rng = H.Rng;
        for (int i = 0; i < frames; i++)
        {
            phLfo += 0.12 / rate; ph1 += 55 / rate; ph2 += 55.6 / rate; ph3 += 82.4 / rate;
            float lfo = MathF.Sin((float)(phLfo * Math.PI * 2));
            lpSurf += ((float)rng.NextDouble() * 2 - 1 - lpSurf) * aS;
            float surf = lpSurf * MathF.Max(0, surfGain + lfo * 0.03f) * 2.2f;
            float saw = ((float)(ph1 % 1) * 2 - 1 + (float)(ph2 % 1) * 2 - 1 + (float)(ph3 % 1) * 2 - 1) * 0.05f;
            lpDrone1 += (saw - lpDrone1) * aD; lpDrone2 += (lpDrone1 - lpDrone2) * aD;
            float drone = lpDrone2 * droneGain * 1.6f;
            // inside a Bell Room the bell's long resonance hangs in the air: a soft open chord, breathing
            float safe = 0;
            if (safeGain > 0.001f)
            {
                phS1 += 196 / rate; phS2 += 293.7 / rate; phS3 += 392 / rate; phS4 += 587.3 / rate;
                float breathe = 0.6f + 0.4f * MathF.Sin((float)(phLfo * Math.PI * 2 * 2.5));
                safe = (MathF.Sin((float)(phS1 * Math.PI * 2)) + 0.7f * MathF.Sin((float)(phS2 * Math.PI * 2)) + 0.45f * MathF.Sin((float)(phS3 * Math.PI * 2)) + 0.2f * MathF.Sin((float)(phS4 * Math.PI * 2))) * 0.05f * safeGain * breathe;
            }
            float v = (surf + drone * (1 - safeGain * 0.8f) + safe) * 0.5f;
            buf[i] = new Vector2(v, v);
        }
        if (frames > 0) amb.PushBuffer(buf);
    }

    static AudioStreamWav[] Bake(string key, float seconds, Action<Synth> build, float pan, int variants, float normalize = 0)
    {
        if (!cache.TryGetValue(key, out var arr))
        {
            arr = new AudioStreamWav[variants];
            for (int i = 0; i < variants; i++) { var s = new Synth(seconds, pan); build(s); if (normalize > 0) s.Normalize(normalize); arr[i] = s.ToStream(); }
            cache[key] = arr;
        }
        return arr;
    }
    // Tests and tools: write every sound made so far to WAV files, one per effect.
    public static int DumpCache(string dir)
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        int n = 0;
        foreach (var kv in cache)
        {
            string name = kv.Key.Replace("|at", "").Replace(":", "_").Replace("|", "_p").Replace(" ", "");
            kv.Value[0].SaveToWav(dir.TrimEnd('/') + "/" + name + ".wav"); n++;
        }
        return n;
    }
    public static string Last;   // tests: the last effect asked for
    public static readonly List<string> Heard = new();   // tests: recent effects
    static void Log(string key) { Last = key; Heard.Add(key); if (Heard.Count > 300) Heard.RemoveRange(0, 100); }
    static void Play(string key, float seconds, Action<Synth> build, float vol = 1, float pan = 0, int variants = 2)
    {
        Log(key);
        if (inst == null || Muted) return;
        int pq = (int)MathF.Round(Math.Clamp(pan, -1, 1) * 4);
        var arr = Bake(key + "|" + pq, seconds, build, pq / 4f, variants);
        var v = key is "ui" or "deny" ? inst.dry[inst.next % inst.dry.Count] : inst.voices[inst.next % inst.voices.Count];
        inst.next++;
        v.Stream = arr[H.Rng.Next(arr.Length)];
        v.VolumeDb = vol <= 0.0001f ? -80 : Mathf.LinearToDb(vol);
        v.Play();
    }
    // A sound from a place in the world. Godot pans and quietens it by distance from the view.
    public static void At(string key, float seconds, Action<Synth> build, float x, float y, float vol = 1, int variants = 3, float normalize = 0.9f)
    {
        Log(key);
        if (inst == null || Muted) return;
        var arr = Bake(key + "|at", seconds, build, 0, variants, normalize);
        var v = inst.spots[inst.nextSpot++ % inst.spots.Count];
        v.Stream = arr[H.Rng.Next(arr.Length)];
        v.GlobalPosition = new Vector2(x, y);
        v.VolumeDb = vol <= 0.0001f ? -80 : Mathf.LinearToDb(vol);
        v.Play();
    }

    public static void Ambience(float waterLevel01, float tension01, bool underwater, bool safe = false)
    {
        if (inst == null) return;
        inst.tSurfGain = 0.03f + waterLevel01 * 0.07f;
        inst.tSurfFreq = underwater ? 250 : 500 + waterLevel01 * 300;
        inst.tDroneGain = 0.15f + tension01 * 0.5f;
        inst.tDroneFreq = 120 + tension01 * 300;
        inst.tSafeGain = safe ? 1 : 0;
        inst.tUnder = underwater ? 1 : 0;
    }

    // How a room sounds: the open shore is dry, the nave and the sunken choir ring for seconds.
    public static void Acoustics(RoomDef r)
    {
        if (reverb == null || r == null) return;
        (float size, float wet, float damp) a = r.Outdoor ? (0.3f, 0.03f, 0.6f) : r.Id switch
        {
            "nave" or "abbot" or "choir" => (0.92f, 0.36f, 0.35f),
            "lair" or "gulletmouth" => (0.85f, 0.3f, 0.2f),
            "tithe" or "tithe_gallery" or "cloister" or "choirsteps" or "ribshaft" or "prisms" => (0.78f, 0.26f, 0.45f),
            _ when r.BellRoom => (0.6f, 0.2f, 0.5f),
            _ => (0.65f, 0.18f, 0.5f),
        };
        reverb.RoomSize = a.size; reverb.Wet = a.wet; reverb.Damping = a.damp;
    }

    // ---- the effects ------------------------------------------------------------------------
    public static void Step(bool metal, float vol = 0.12f, float pan = 0) =>
        Play("step" + metal, 0.15f, s => { s.Noise(0.05f, 1, "bandpass", metal ? 2600 : 700, metal ? 6 : 1.5f); if (metal) s.Tone("triangle", 900 + H.Rand() * 200, 0.08f, 0.4f); }, vol, pan, 3);
    public static void Splash(float vol = 0.25f, float pan = 0) => Play("splash", 0.45f, s => s.Noise(0.35f, 1, "lowpass", 1400, 0.5f, 0.01f), vol, pan);
    public static void Jump(float pan = 0) => Play("jump", 0.15f, s => s.Noise(0.08f, 0.08f, "bandpass", 500, 1), 1, pan);
    public static void Rivet(float pan = 0) => Play("rivet", 0.15f, s => { s.Noise(0.06f, 0.4f, "highpass", 2000, 1); s.Tone("square", 220, 0.07f, 0.15f, 80); }, 1, pan);
    public static void Harpoon(float pan = 0) => Play("harpoon", 0.3f, s => { s.Noise(0.15f, 0.3f, "bandpass", 900, 2); s.Tone("sawtooth", 160, 0.2f, 0.15f, 60); }, 1, pan);
    public static void Sling(float pan = 0) => Play("sling", 0.3f, s => s.Noise(0.2f, 0.15f, "bandpass", 1500, 3, 0.04f), 1, pan);
    public static void Reload() => Play("reload", 0.25f, s => { s.Tone("square", 600, 0.04f, 0.08f); s.Tone("square", 900, 0.04f, 0.08f, 0, 0.12f); }, 1, 0, 1);
    public static void Empty() => Play("empty", 0.1f, s => s.Tone("square", 1200, 0.03f, 0.06f), 1, 0, 1);
    public static void Tink(float pan = 0) => Play("tink", 0.15f, s => s.Tone("triangle", 2400, 0.08f, 0.12f, 1800), 1, pan, 1);
    public static void Thud(float pan = 0) => Play("thud", 0.2f, s => s.Noise(0.12f, 0.25f, "lowpass", 300), 1, pan);
    public static void Crack(float pan = 0) => Play("crack", 0.35f, s => { s.Noise(0.18f, 0.5f, "highpass", 1500, 0.8f); s.Tone("triangle", 1700, 0.25f, 0.2f, 500); }, 1, pan);
    public static void Gaff(float pan = 0) => Play("gaff", 0.2f, s => s.Noise(0.12f, 0.15f, "bandpass", 2000, 2, 0.02f), 1, pan);
    public static void Hook(float pan = 0) => Play("hook", 0.15f, s => s.Tone("triangle", 1400, 0.1f, 0.12f, 900), 1, pan, 1);
    // Maren, hit: the clang of the blow on brass and her grunt inside the helmet
    public static void Hurt() => Play("hurt", 0.4f, s => { s.Tone("sawtooth", 180, 0.25f, 0.2f, 70); s.Noise(0.15f, 0.2f, "lowpass", 800); s.Voice(0.3f, 0.35f, H.Rand(230, 260), H.Rand(180, 200), (520, 1190, 2390), (570, 840, 2410), 0.5f, 0, 0.2f, 0, 0.02f, 0.02f, 0.85f); }, 1, 0, 3);
    public static void Heal() => Play("heal", 0.65f, s => { s.Tone("sine", 520, 0.3f, 0.12f); s.Tone("sine", 780, 0.4f, 0.1f, 0, 0.15f); }, 1, 0, 1);
    public static void Pickup() => Play("pickup", 0.25f, s => { s.Tone("triangle", 880, 0.08f, 0.1f); s.Tone("triangle", 1320, 0.1f, 0.08f, 0, 0.07f); }, 1, 0, 1);
    public static void Craft() => Play("craft", 0.2f, s => { s.Noise(0.1f, 0.12f, "bandpass", 3000, 4); s.Tone("triangle", 660, 0.12f, 0.08f); }, 1, 0, 1);
    public static void Groan(float pan = 0, float vol = 0.18f) => Play("groan", 1.0f, s => s.Tone("sawtooth", H.Rand(70, 95), 0.9f, 1, H.Rand(50, 60)), vol, pan, 3);
    public static void Inhale(float pan = 0) => Play("inhale", 2.8f, s => s.Noise(1.5f, 0.25f, "bandpass", 600, 1.5f, 1.2f), 1, pan, 1);
    // The Bellmouth's scream: a cracked bell of a voice, many throats in it, loud enough to wake the room.
    public static void Scream(float pan = 0) => Play("scream", 1.5f, s =>
    {
        s.Tone("sawtooth", 420, 1.2f, 0.25f, 900); s.Tone("square", 610, 1.2f, 0.12f, 1300); s.Noise(1.2f, 0.3f, "bandpass", 2500, 1);
        s.Voice(1.35f, 0.5f, 380, 640, (730, 1090, 2440), (530, 1840, 2480), 0.3f, 0.2f, 0.4f, 0.03f);
        s.Voice(1.35f, 0.4f, 405, 690, (730, 1090, 2440), (730, 1090, 2440), 0.3f, 0, 0.5f, 0.04f, 0.04f);
        s.Bell(311, 0.25f, 1.3f);
    }, 1, pan, 1);
    public static void Grab() => Play("grab", 0.35f, s => { s.Noise(0.2f, 0.3f, "lowpass", 600); s.Tone("sawtooth", 110, 0.3f, 0.2f, 70); }, 1, 0, 1);
    public static void Fire(float pan = 0) => Play("fire", 0.8f, s => s.Noise(0.6f, 0.2f, "bandpass", 900, 0.5f, 0.1f), 1, pan);
    public static void Burst(float pan = 0) => Play("burst", 0.35f, s => s.Noise(0.25f, 0.25f, "bandpass", 400, 1, 0.01f), 1, pan);
    public static void Bubble() => Play("bubble", 0.1f, s => s.Tone("sine", H.Rand(500, 900), 0.06f, 0.05f, 1400), 1, 0, 3);
    public static void Bell(float freq, float vol = 0.4f, float pan = 0, float decay = 3.5f) => Play("bell" + freq + ":" + decay, decay + 0.2f, s => s.Bell(freq, 1, decay), vol, pan, 1);
    public static void Buoy(float vol, float pan) => Bell(98, vol, pan, 4);
    public static void TideBell() => Bell(330, 0.25f, 0, 3);
    public static void RoomBell() => Play("roombell", 5.4f, s => { s.Bell(392, 0.35f, 5); s.Bell(392 * 1.5f, 0.15f, 4, 0.3f); }, 1, 0, 1);
    public static void Toll() => Bell(110, 0.6f, 0, 6);
    public static void Ui() => Play("ui", 0.1f, s => s.Tone("triangle", 700, 0.05f, 0.06f), 1, 0, 1);
    public static void Deny() => Play("deny", 0.2f, s => s.Tone("square", 160, 0.12f, 0.1f), 1, 0, 1);
    public static void Rumble() => Play("rumble", 1.9f, s => s.Noise(1.5f, 0.4f, "lowpass", 120, 0.5f, 0.2f), 1, 0, 2);
    public static void Flare(float pan = 0) => Play("flare", 0.6f, s => s.Noise(0.5f, 0.2f, "highpass", 3000, 0.5f, 0.02f), 1, pan);

    // ---- flesh ------------------------------------------------------------------------------------------
    // A limb tearing away: the wet rip, the crack of the joint, the weight of it, then spatter.
    public static void Sever(float pan = 0) => Play("sever", 0.75f, s =>
    {
        s.NoiseSweep(0.24f, 0.6f, "bandpass", 1900, 240, 2.5f);
        s.Noise(0.05f, 0.5f, "highpass", 2200, 1, 0.001f, 0.015f);
        s.Tone("sine", 95, 0.2f, 0.45f, 50);
        for (int i = 0; i < 5; i++) s.Noise(0.03f, 0.16f, "bandpass", 700 + i * 260, 3, 0.002f, 0.14f + i * 0.07f + H.Rand(0, 0.04f));
    }, 0.95f, pan, 3);
    public static void Splat(float pan = 0, float vol = 0.15f) => Play("splat", 0.25f, s => { s.Noise(0.08f, 0.6f, "lowpass", 420, 0.7f); s.NoiseSweep(0.1f, 0.3f, "bandpass", 1300, 500, 3); }, vol, pan, 3);
    public static void Drip(float x, float y) => At("drip", 0.2f, s => { s.Tone("sine", H.Rand(900, 1500), 0.05f, 0.5f, H.Rand(1800, 2400)); s.Noise(0.03f, 0.2f, "highpass", 3000); }, x, y, 0.07f, 4);

    // Maren breathing hard inside the helmet when she is badly hurt: in through the teeth, out ragged.
    public static void Breath(bool worse) => Play("breath" + worse, 1.4f, s =>
    {
        s.NoiseSweep(0.45f, 0.5f, "bandpass", 700, 1300, 2.5f, 0.2f);
        s.NoiseSweep(0.6f, 0.45f, "bandpass", 1200, 500, 2, 0.05f, 0.6f);
        if (worse) s.Voice(0.4f, 0.12f, 210, 180, (520, 1190, 2390), (520, 1190, 2390), 0.8f, 0, 0.4f, 0, 0.62f, 0.02f, 0.9f);
    }, worse ? 0.5f : 0.32f, 0, 3);

    // ---- dread ------------------------------------------------------------------------------------------
    // The moment something sees you: a scrape of strings and a low swell, never more than once in a while.
    public static void Sting() => Play("sting", 2.4f, s =>
    {
        s.Swell("sawtooth", 110, 2.2f, 0.12f, 0.35f, 0.004f, 0, 0, 900);
        s.Swell("sawtooth", 116.5f, 2.2f, 0.11f, 0.4f, 0.005f, 0, 0, 900);
        s.Swell("sawtooth", 123.5f, 2.0f, 0.1f, 0.45f, 0.006f, 0, 0, 900);
        s.Swell("sawtooth", 1480, 1.3f, 0.13f, 0.04f, 0.012f, 1390, 0.02f, 4200);
        s.Swell("sawtooth", 1568, 1.1f, 0.08f, 0.06f, 0.015f, 1480, 0.05f, 4200);
        s.NoiseSweep(0.9f, 0.18f, "bandpass", 400, 2800, 4, 0.5f);
        s.Tone("sine", 55, 1.6f, 0.35f, 40);
    }, 0.8f, 0, 2);
    // The island itself: the Saint's ribs settling under the abbey, a long groan of bone through stone.
    public static void SaintGroan(float pan) => Play("saintgroan", 5.2f, s =>
    {
        s.Swell("sine", 46, 4.8f, 0.45f, 1.6f, 0.02f, 34);
        s.Swell("sine", 69, 4.2f, 0.15f, 1.8f, 0.03f, 52);
        s.NoiseSweep(3.6f, 0.08f, "bandpass", 260, 170, 6, 1.2f, 0.6f);
        for (int i = 0; i < 6; i++) s.Noise(0.08f, 0.05f, "bandpass", 220 + i * 30, 5, 0.01f, 1.2f + i * 0.45f + H.Rand(0, 0.2f));
    }, 0.55f, pan, 2);
    // ---- the Mere's Hollow ---------------------------------------------------------------------------
    // Every verse is a phrase of the Choir's hymn (in D), played on the Hollow's comb over the sea's answer.
    static readonly Dictionary<string, float[]> PHRASES = new()
    {
        ["calling"] = new[] { 440f, 392f, 349.2f, 293.7f },
        ["riptide"] = new[] { 293.7f, 349.2f, 440f, 587.3f },
        ["salting"] = new[] { 392f, 440f, 523.3f, 587.3f },
        ["answer"] = new[] { 293.7f, 440f, 392f, 293.7f },
        ["turning"] = new[] { 587.3f, 523.3f, 440f, 392f, 349.2f, 293.7f, 220f, 146.8f },
    };
    public static void Verse(string id) => Play("verse:" + id, 3.4f, s =>
    {
        var notes = PHRASES[id]; float step = id == "turning" ? 0.2f : 0.13f;
        for (int i = 0; i < notes.Length; i++) s.Tine(notes[i], 0.3f, 1.4f, i * step);
        switch (id)
        {
            case "calling": s.NoiseSweep(2.0f, 0.22f, "lowpass", 180, 700, 0.7f, 0.6f, 0.1f); s.Swell("sine", 73.4f, 2.4f, 0.25f, 0.8f); break;
            case "riptide": s.NoiseSweep(0.7f, 0.45f, "bandpass", 300, 1900, 1.2f, 0.05f); s.Noise(0.5f, 0.25f, "lowpass", 500, 0.7f, 0.02f, 0.1f); break;
            case "salting": s.Clicks(22, 0.8f, 0.3f, 5200, 0.7f, 0.1f); break;
            case "answer": s.Voice(2.2f, 0.4f, 146.8f, 146.8f, (300, 870, 2240), (570, 840, 2410), 0.2f, 0, 0, 0.02f, 0.1f, 0.4f); break;
            case "turning": s.Swell("sine", 36.7f, 3f, 0.4f, 1.2f); s.NoiseSweep(2.6f, 0.3f, "lowpass", 120, 900, 0.7f, 1.2f, 0.2f); break;
        }
    }, 0.75f, 0, 1);
    public static void Tine(string id) => Play("tine:" + id, 1.4f, s => s.Tine(id == "full" ? 880 : PHRASES.TryGetValue(id, out var n) ? n[0] : 440, 0.3f, 1.2f), 0.5f, 0, 1);
    // The sea answering the Calling: a long heave up, or a drawing-back.
    public static void Surge(bool up) => Play("surge" + up, 3f, s => { s.NoiseSweep(2.4f, 0.4f, "lowpass", up ? 150 : 600, up ? 600 : 150, 0.7f, 0.5f); s.Swell("sine", up ? 49 : 65, 2.6f, 0.3f, 0.7f, 0, up ? 65 : 41); }, 0.7f, 0, 1);
    public static void Crystal(float pan = 0) => Play("crystal", 0.8f, s => { s.Clicks(26, 0.6f, 0.35f, 4800, 0.6f); s.NoiseSweep(0.5f, 0.15f, "highpass", 2500, 6000, 1); }, 0.6f, pan, 2);

    // A bell somewhere far off in the abbey: Ansel keeping the hour, or something that thinks it is.
    public static void FarBell(float pan) => Bell(H.Pick(new[] { 98f, 110f, 130.8f }), 0.06f, pan, 5);
}
