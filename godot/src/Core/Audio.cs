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

    // RBJ biquad, like WebAudio's BiquadFilterNode.
    class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;
        public Biquad(string type, float freq, float q)
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
    static readonly Dictionary<string, AudioStreamWav[]> cache = new();
    AudioStreamPlayer ambPlayer;
    AudioStreamGeneratorPlayback amb;
    // ambience state (smoothed toward targets like setTargetAtTime)
    float surfGain = 0.05f, surfFreq = 500, droneGain = 0, droneFreq = 180;
    float tSurfGain = 0.05f, tSurfFreq = 500, tDroneGain = 0.15f, tDroneFreq = 120;
    double phLfo, ph1, ph2, ph3;
    float lpSurf, lpDrone1, lpDrone2;
    public static float Volume = 0.7f;
    public static bool Muted;

    public override void _Ready()
    {
        inst = this;
        for (int i = 0; i < 24; i++) { var p = new AudioStreamPlayer { Bus = "Master" }; AddChild(p); voices.Add(p); }
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
            float v = (surf + drone) * 0.5f;
            buf[i] = new Vector2(v, v);
        }
        if (frames > 0) amb.PushBuffer(buf);
    }

    static void Play(string key, float seconds, Action<Synth> build, float vol = 1, float pan = 0, int variants = 2)
    {
        if (inst == null || Muted) return;
        int pq = (int)MathF.Round(Math.Clamp(pan, -1, 1) * 4);
        string k = key + "|" + pq;
        if (!cache.TryGetValue(k, out var arr))
        {
            arr = new AudioStreamWav[variants];
            for (int i = 0; i < variants; i++) { var s = new Synth(seconds, pq / 4f); build(s); arr[i] = s.ToStream(); }
            cache[k] = arr;
        }
        var v = inst.voices[inst.next++ % inst.voices.Count];
        v.Stream = arr[H.Rng.Next(arr.Length)];
        v.VolumeDb = vol <= 0.0001f ? -80 : Mathf.LinearToDb(vol);
        v.Play();
    }

    public static void Ambience(float waterLevel01, float tension01, bool underwater)
    {
        if (inst == null) return;
        inst.tSurfGain = 0.03f + waterLevel01 * 0.07f;
        inst.tSurfFreq = underwater ? 250 : 500 + waterLevel01 * 300;
        inst.tDroneGain = 0.15f + tension01 * 0.5f;
        inst.tDroneFreq = 120 + tension01 * 300;
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
    public static void Hurt() => Play("hurt", 0.35f, s => { s.Tone("sawtooth", 180, 0.25f, 0.25f, 70); s.Noise(0.15f, 0.2f, "lowpass", 800); });
    public static void Heal() => Play("heal", 0.65f, s => { s.Tone("sine", 520, 0.3f, 0.12f); s.Tone("sine", 780, 0.4f, 0.1f, 0, 0.15f); }, 1, 0, 1);
    public static void Pickup() => Play("pickup", 0.25f, s => { s.Tone("triangle", 880, 0.08f, 0.1f); s.Tone("triangle", 1320, 0.1f, 0.08f, 0, 0.07f); }, 1, 0, 1);
    public static void Craft() => Play("craft", 0.2f, s => { s.Noise(0.1f, 0.12f, "bandpass", 3000, 4); s.Tone("triangle", 660, 0.12f, 0.08f); }, 1, 0, 1);
    public static void Groan(float pan = 0, float vol = 0.18f) => Play("groan", 1.0f, s => s.Tone("sawtooth", H.Rand(70, 95), 0.9f, 1, H.Rand(50, 60)), vol, pan, 3);
    public static void Inhale(float pan = 0) => Play("inhale", 2.8f, s => s.Noise(1.5f, 0.25f, "bandpass", 600, 1.5f, 1.2f), 1, pan, 1);
    public static void Scream(float pan = 0) => Play("scream", 1.3f, s => { s.Tone("sawtooth", 420, 1.2f, 0.3f, 900); s.Tone("square", 610, 1.2f, 0.15f, 1300); s.Noise(1.2f, 0.3f, "bandpass", 2500, 1); }, 1, pan, 1);
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
}
