// The voices of the drowned. Every Brine body speaks, and each kind in its own way: a murmur while it
// waits (only the knotted dead breathe, so a silent body is a true corpse), a cry the moment it sees Maren,
// a snarl before it strikes, pain, a wail when a limb tears away, a long wet rattle when it goes down and a
// gasp when the water stands it up again. Their wet footsteps carry, so you hear where they are before
// you see them. Everything is placed in the room and sent through its reverb.
using System;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Voices
{
    // Formants (F1, F2, F3) for the vowels the dead still have.
    static readonly (float, float, float) A = (730, 1090, 2440), O = (570, 840, 2410), U = (300, 870, 2240), E = (530, 1840, 2480), UH = (520, 1190, 2390), M = (250, 900, 2200), I = (300, 2250, 3000);
    static readonly float[] HYMN = { 146.8f, 174.6f, 196f, 220f, 261.6f };   // the Choir sings in D
    public static float stingCool = 8;   // the sting never comes twice in quick succession
    public static int Stings;   // tests

    public static void Update(float dt) { if (stingCool > 0) stingCool -= dt; }
    public static void Reset() { stingCool = 8; }

    // Every body has its own throat: one of three pitches, fixed by who it was, so a room of them is many voices.
    static float Throat(Body b) { int h = 0; foreach (char ch in b.key ?? b.type) h = unchecked(h * 31 + ch); return new[] { 0.86f, 1f, 1.14f }[Math.Abs(h) % 3]; }
    static int Bucket(Body b) => Array.IndexOf(new[] { 0.86f, 1f, 1.14f }, Throat(b));
    static void Say(Body b, string what, float seconds, Action<Synth, float> build, float vol, float busy = 0, int variants = 4)
    {
        // voices sit under the guns and the sea in the mix: heard, placed, never louder than a rivet... except a scream
        float k = Throat(b);
        Snd.At(b.type + ":" + what, seconds, s => build(s, k), b.Cx(), b.y + 6, vol * 0.5f, variants, 0.9f, "#" + Bucket(b));
        b.busyT = MathF.Max(b.busyT, busy > 0 ? busy : seconds * 0.8f);
    }
    static float Dist(Body b) { var p = G.player; return p == null ? 9999 : H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy()); }
    // a sob: the voice catching, breaking into short gasps as it falls
    static void Sob(Synth s, float at, float f0, float vol, int n)
    {
        for (int i = 0; i < n; i++) s.Voice(0.16f, vol * (1 - i * 0.12f), f0 * (1 - i * 0.05f), f0 * (0.9f - i * 0.05f), A, UH, 0.5f, 0.3f, 0.5f, 0, at + i * 0.22f, 0.02f, 1, 0.3f, 1, 0.25f);
    }
    static void Bubbles(Synth s, float at, float dur, int n)
    {
        for (int i = 0; i < n; i++) s.Tone("sine", H.Rand(300, 700), 0.05f, 0.08f, H.Rand(900, 1600), at + H.Rand(0, dur));
    }

    // ---- what each of them sounds like ---------------------------------------------------------------------
    public static void Idle(Body b)
    {
        // the ones that answered the Hollow sing the hymn while they tear at their brothers
        if (b.thrallT > 0)
        {
            float n = H.Pick(HYMN) * 0.5f;
            Say(b, "answer" + n, 2.4f, (s, k) => s.Voice(2.2f, 1, n, n, U, O, 0.3f, 0.5f, 0.3f, 0.02f, 0, 0.06f, 1, 0.2f, 2), 0.45f, 0, 2);
            return;
        }
        switch (b.type)
        {
            case "shucker":
                if (b.mode == "dormant")
                {
                    // the dormant dead mutter in their sleep; close enough, it is her name
                    if (Dist(b) < 5 * H.TILE && H.Rand() < 0.35f)
                        Say(b, "murmur", 1.6f, (s, k) => { s.Whisper(0.9f, 0.05f, ("m", 0.14f), ("a", 0.22f), ("r", 0.09f), ("e", 0.18f), ("n", 0.2f)); s.Voice(1.3f, 0.35f, 60 * k, 50 * k, M, M, 0.6f, 0.9f, 0.5f, 0, 0.1f, 0.06f, 1, 0.4f); }, 0.3f);
                    else
                        Say(b, "murmur", 1.4f, (s, k) => { s.Voice(1.3f, 1, H.Rand(62, 74) * k, H.Rand(52, 60) * k, M, M, 0.6f, 0.9f, 0.5f, 0, 0, 0.06f, 1, 0.45f); s.Whisper(0.35f, 0.3f, ("uh", 0.12f), ("w", 0.08f), ("o", 0.16f), ("n", 0.1f)); }, 0.22f);
                }
                else Say(b, "moan", 2.2f, (s, k) => { s.Voice(H.Rand(1.5f, 2.0f), 1, H.Rand(74, 90) * k, H.Rand(52, 64) * k, UH, U, 0.35f, 0.8f, 0.4f, 0.01f, 0, 0.08f, k, 0.4f, 2); Bubbles(s, 0.4f, 1.2f, 3); }, 0.55f);
                break;
            case "bellmouth":
                // a bell-ringer humming the Hours to himself, a second voice in his throat a tone below
                Say(b, "hum", 2.6f, (s, k) => { s.Voice(2.4f, 1, 98, H.Rand(96, 100), M, U, 0.15f, 0.3f, 0, 0.02f, 0, 0.3f, 1, 0.15f, 2); s.Bell(196, 0.12f, 2.2f, 0.3f); }, 0.45f);
                break;
            case "saltwright":
                // heavy breath through salt in the lungs, crackling as it goes in
                Say(b, "breath", 2.4f, (s, k) => { s.NoiseSweep(0.8f, 0.5f, "bandpass", 600, 1100, 2, 0.3f); s.Clicks(14, 0.8f, 0.18f, 4200, 1.2f, 0.1f); s.NoiseSweep(1.0f, 0.4f, "bandpass", 1000, 450, 2, 0.1f, 1.0f); s.Voice(0.9f, 0.3f, 70 * k, 60 * k, UH, U, 0.6f, 0.2f, 0.6f, 0, 1.0f, 0.06f, 1, 0.6f); }, 0.42f);
                break;
            case "gullet":
                // clicks, then something like laughter, wet, in a throat that is not a throat
                Say(b, "click", 1.6f, (s, k) => { s.Clicks(H.RandI(6, 11), 1.1f, 0.6f, 3200, 1.6f); s.NoiseSweep(0.5f, 0.1f, "bandpass", 3000, 4200, 6, 0.1f, 0.6f); if (H.Rand() < 0.5f) for (int i = 0; i < 5; i++) s.Voice(0.09f, 0.4f, 340, 300, E, UH, 0.6f, 0.4f, 0.3f, 0, 0.8f + i * 0.12f, 0.01f, 1.3f, 0.2f, 1, 0.4f); }, 0.42f);
                break;
            case "grafted":
                if (b.passive || b.mode == "idle" || H.Rand() < 0.6f)
                {
                    // a line of the hymn, sung under the breath, and one of the voices in it is singing the wrong note
                    float n = H.Pick(HYMN);
                    Say(b, "sing" + n, 2.8f, (s, k) => { s.Voice(2.6f, 1, n, n * H.Rand(0.99f, 1.01f), U, O, 0.12f, 0, 0, 0.025f, 0, 0.5f); s.Voice(2.4f, 0.4f, n * 1.5f, n * 1.5f, O, A, 0.15f, 0, 0, 0.03f, 0.2f, 0.5f); s.Voice(2.2f, 0.22f, n * 1.06f, n * 0.98f, U, U, 0.2f, 0, 0.2f, 0.04f, 0.5f, 0.5f); }, 0.5f, 0, 2);
                }
                break;
            case "shoal":
                Say(b, "chorus", 2.4f, (s, k) => { for (int i = 0; i < 5; i++) s.Voice(H.Rand(1.2f, 1.9f), 0.5f, H.Rand(60, 140), H.Rand(45, 90), UH, U, 0.4f, 0.9f, 0.5f, 0, H.Rand(0, 0.4f), 0.06f, 1, 0.5f); }, 0.65f);
                break;
        }
    }

    // The moment a hunter first sees Maren: a gasp drawn in, then the cry torn out of it.
    public static void Alert(Body b)
    {
        switch (b.type)
        {
            case "shucker":
                Say(b, "alert", 1.5f, (s, k) => { s.Voice(0.95f, 1, H.Rand(170, 200) * k, H.Rand(380, 460) * k, UH, A, 0.3f, 0.3f, 0.5f, 0.02f, 0, 0.03f, k, 0.25f, 2, 0.55f, 0.35f); Bubbles(s, 0.6f, 0.6f, 3); }, 0.95f);
                break;
            case "saltwright":
                Say(b, "alert", 1.3f, (s, k) => { s.Voice(1.0f, 1, 85 * k, 150 * k, UH, A, 0.35f, 0.2f, 0.8f, 0, 0, 0.05f, 0.9f, 0.85f, 2, 0.45f, 0.3f); s.Clicks(20, 0.9f, 0.2f, 4400, 0.8f, 0.3f); }, 0.95f);
                break;
            case "gullet":
                Say(b, "hiss", 1.0f, (s, k) => { s.NoiseSweep(0.7f, 0.7f, "bandpass", 2600, 4200, 4, 0.05f); s.Clicks(14, 0.6f, 0.6f, 3500, 0.6f); s.Voice(0.6f, 0.5f, 520, 700, E, I, 0.5f, 0.2f, 0.4f, 0.03f, 0.15f, 0.02f, 1.3f, 0.2f, 2, 0.6f); }, 0.85f);
                break;
            case "grafted":
                // the hymn breaks into a shriek, several throats a semitone apart
                Say(b, "shriek", 1.5f, (s, k) => { s.Voice(1.2f, 1, 220, 233, A, A, 0.2f, 0, 0.3f, 0.03f, 0, 0.06f, 1, 0, 3, 0.4f); s.Voice(1.2f, 0.8f, 330, 311, A, E, 0.2f, 0, 0.3f, 0.03f, 0.05f, 0.06f, 1, 0, 2, 0.4f); }, 0.9f, 0, 2);
                break;
        }
    }

    public static void Windup(Body b)
    {
        switch (b.type)
        {
            case "shucker": Say(b, "snarl", 0.8f, (s, k) => s.Voice(0.65f, 1, H.Rand(90, 105) * k, H.Rand(140, 160) * k, E, A, 0.5f, 0.3f, 0.85f, 0, 0, 0.04f, k, 0.8f, 2, 0.55f), 0.85f); break;
            case "saltwright": Say(b, "heave", 0.7f, (s, k) => s.Voice(0.5f, 1, 105 * k, 150 * k, UH, A, 0.4f, 0.1f, 0.6f, 0, 0, 0.02f, 0.9f, 0.6f, 2, 0.4f), 0.85f); break;
            case "gullet": Say(b, "rattle", 0.6f, (s, k) => s.Clicks(18, 0.5f, 0.8f, 2800, 0.5f), 0.7f); break;
            case "bellmouth": Say(b, "grunt", 0.6f, (s, k) => s.Voice(0.5f, 1, 85, 70, O, UH, 0.3f, 0.5f, 0.5f, 0, 0, 0.06f, 1, 0.6f, 2, 0.3f), 0.75f); break;
            case "undertow": Say(b, "moan", 2.0f, (s, k) => { s.Voice(1.8f, 1, 52, 36, O, U, 0.35f, 0.6f, 0.4f, 0.01f, 0, 0.2f, 0.75f, 0.7f, 3, 0.2f); s.Bell(98, 0.25f, 1.6f, 0.1f); s.Tone("sine", 41, 1.6f, 0.4f, 30); }, 1, 0, 2); break;
            case "abbot": Say(b, "roar", 1.4f, (s, k) => { s.Voice(1.1f, 1, 58, 84, A, O, 0.45f, 0.4f, 1, 0, 0, 0.1f, 0.85f, 0.9f, 3, 0.55f, 0.3f); s.Tone("sine", 45, 1.0f, 0.45f, 32); }, 1f, 0, 2); break;
        }
    }

    // Pain: a knot struck (or broken, an agonised wail and then sobbing when something comes off).
    public static void Hurt(Body b, bool broke)
    {
        if (b.busyT > 0.3f && !broke) return;
        switch (b.type)
        {
            case "shucker":
                if (broke) Say(b, "wail", 2.0f, (s, k) => { s.Voice(0.9f, 1, H.Rand(260, 300) * k, H.Rand(340, 380) * k, A, A, 0.25f, 0.5f, 0.2f, 0.035f, 0, 0.02f, k, 0.3f, 2, 0.65f, 0.2f); s.Voice(0.7f, 0.8f, 330 * k, 150 * k, A, O, 0.3f, 0.6f, 0.4f, 0.03f, 1.0f, 0.03f, k, 0.4f, 2, 0.5f); Sob(s, 1.55f, 160 * k, 0.5f, 3); }, 0.9f, 2.2f);
                else Say(b, "grunt", 0.4f, (s, k) => s.Voice(0.3f, 1, H.Rand(160, 185) * k, H.Rand(100, 120) * k, UH, UH, 0.3f, 0.4f, 0.3f, 0, 0, 0.02f, k, 0.4f, 1, 0.35f), 0.55f);
                break;
            case "bellmouth": Say(b, broke ? "bellow" : "oof", broke ? 1.2f : 0.4f, (s, k) => { s.Voice(broke ? 1.0f : 0.35f, 1, broke ? 150 : 130, 85, O, U, 0.3f, 0.6f, 0.3f, 0, 0, 0.04f, 1, 0.5f, 2, broke ? 0.5f : 0.2f); if (broke) s.Bell(147, 0.2f, 0.8f); }, 0.7f); break;
            case "saltwright": Say(b, broke ? "howl" : "grunt", broke ? 1.4f : 0.35f, (s, k) => { s.Voice(broke ? 1.1f : 0.3f, 1, broke ? 200 * k : 125 * k, broke ? 90 * k : 90 * k, broke ? A : UH, UH, 0.35f, 0.2f, 0.6f, 0.02f, 0, 0.03f, 0.9f, 0.6f, 2, broke ? 0.6f : 0.25f); if (broke) s.Clicks(30, 1.0f, 0.25f, 4600, 1, 0.2f); }, 0.75f); break;
            case "grafted": Say(b, broke ? "keen" : "ah", broke ? 1.4f : 0.45f, (s, k) => s.Voice(broke ? 1.2f : 0.4f, 1, broke ? 340 : 280, broke ? 190 : 240, A, broke ? O : A, 0.15f, 0, 0.1f, 0.045f, 0, 0.04f, 1, 0, broke ? 3 : 1, broke ? 0.45f : 0.1f), 0.7f); break;
            case "gullet": Say(b, "squeal", 0.6f, (s, k) => { s.NoiseSweep(0.35f, 0.6f, "bandpass", 3600, 1800, 5); s.Clicks(6, 0.3f, 0.5f, 3000); s.Voice(0.4f, 0.5f, 700, 400, I, E, 0.4f, 0.3f, 0.3f, 0.04f, 0, 0.02f, 1.2f, 0, 2, 0.6f); }, 0.7f); break;
            case "abbot": if (broke) Say(b, "agony", 1.8f, (s, k) => s.Voice(1.6f, 1, 100, 52, A, U, 0.4f, 0.5f, 0.9f, 0.02f, 0, 0.1f, 0.85f, 0.8f, 3, 0.6f, 0.3f), 1, 0, 2); break;
        }
    }

    public static void Die(Body b)
    {
        switch (b.type)
        {
            case "shucker": case "saltwright":
                // the air and water going out of it, rattling, choking, bubbling; then one last breath going out
                Say(b, "rattle", 3.2f, (s, k) => { s.Voice(2.4f, 1, H.Rand(72, 82) * k, H.Rand(36, 44) * k, UH, U, 0.7f, 1, 0.9f, 0, 0, 0.06f, k, 0.6f, 2, 0.2f); s.NoiseSweep(1.6f, 0.15f, "lowpass", 900, 200, 0.7f, 0.05f, 0.3f); Bubbles(s, 0.5f, 2.0f, 6); s.NoiseSweep(0.9f, 0.25f, "bandpass", 900, 300, 2, 0.1f, 2.3f); }, 0.75f, 2);
                break;
            case "bellmouth": Say(b, "toll", 2.6f, (s, k) => { s.Voice(2.2f, 1, 92, 50, O, U, 0.5f, 1, 0.6f, 0, 0, 0.06f, 1, 0.5f, 2); s.Bell(98, 0.4f, 2.4f, 0.2f); }, 0.7f, 2); break;
            case "grafted": Say(b, "fall", 2.6f, (s, k) => s.Voice(2.4f, 1, 220, 92, O, U, 0.25f, 0.2f, 0.2f, 0.04f, 0, 0.06f, 1, 0.2f, 3), 0.7f, 2, 2); break;
            case "gullet": Say(b, "gargle", 1.6f, (s, k) => { s.NoiseSweep(1.2f, 0.5f, "bandpass", 1600, 300, 2, 0.02f); s.Clicks(8, 1.0f, 0.4f, 2600, 2.2f); Bubbles(s, 0.2f, 1.0f, 5); }, 0.65f, 1); break;
        }
    }

    // The water stands it up: brine coughed out of its lungs, a gasp drawn in, salt crackling as the knots grow.
    public static void Rise(Body b)
    {
        if (!b.Humanoid) return;
        Say(b, "rise", 2.2f, (s, k) => { s.Clicks(16, 1.2f, 0.25f, 4200, 1.4f); for (int i = 0; i < 3; i++) s.Voice(0.14f, 0.7f, 140 * k, 110 * k, UH, A, 0.7f, 0.9f, 0.6f, 0, 0.1f + i * 0.2f, 0.01f, k, 0.4f, 1, 0.5f); s.NoiseSweep(0.7f, 0.35f, "bandpass", 400, 1500, 2, 0.4f, 0.8f); s.Voice(1.2f, 0.9f, H.Rand(58, 66) * k, H.Rand(88, 100) * k, U, UH, 0.55f, 0.8f, 0.6f, 0, 1.0f, 0.06f, k, 0.4f, 2); }, 0.7f);
    }

    static void Step(Body b)
    {
        bool wet = World.IsWater(b.Cx(), b.Feet() - 2) || b.room.Region is "wrackline" or "gullet" or "choir" or "cloister";
        if (b.type == "saltwright") Snd.At("step:salt", 0.25f, s => { s.Noise(0.05f, 0.7f, "highpass", 2400, 1); s.Noise(0.12f, 0.8f, "lowpass", 180, 0.7f); }, b.Cx(), b.Feet(), 0.3f, 3);
        else if (b.pose == "crawl") Snd.At("step:drag", 0.4f, s => s.NoiseSweep(0.3f, 0.6f, "bandpass", 500, 280, 1.5f, 0.05f), b.Cx(), b.Feet(), 0.18f, 3);
        else if (wet) Snd.At("step:wet", 0.2f, s => { s.Noise(0.05f, 0.8f, "lowpass", 520, 0.7f); s.Noise(0.05f, 0.4f, "bandpass", 1700, 3, 0.002f, 0.015f); }, b.Cx(), b.Feet(), 0.2f, 4);
        else Snd.At("step:bare", 0.15f, s => s.Noise(0.05f, 0.8f, "lowpass", 380, 0.7f), b.Cx(), b.Feet(), 0.14f, 3);
    }

    // Every frame for every living body: footsteps, the moment it notices Maren, and idle murmurs.
    public static void Tick(Body b, float dt)
    {
        if (b.gone || b.corpse || b.type is "driftling" or "undertow") return;
        b.busyT -= dt;
        var p = G.player;
        if (p == null || p.dead) return;
        float d = Dist(b);
        if (d > 30 * H.TILE) return;
        if ((b.Humanoid || b.type == "abbot") && b.onGround && MathF.Abs(b.vx) > 10 && b.mode != "downed" && b.mode != "dormant")
        {
            b.footT -= dt * MathF.Abs(b.vx) / (b.type == "abbot" ? 30 : 20);
            if (b.footT <= 0) { b.footT = 1; if (b.type == "abbot") Snd.At("step:abbot", 0.4f, s => { s.Noise(0.15f, 1, "lowpass", 140, 0.7f); s.Noise(0.06f, 0.4f, "highpass", 2000, 1); }, b.Cx(), b.Feet(), 0.55f, 2); else Step(b); }
        }
        if (b.mode is "downed" or "captured" or "gone") { b.spotted = false; return; }
        bool hunter = b.thrallT <= 0 && !b.passive && b.mode != "dormant" && b.mode != "hang" && b.type is "shucker" or "saltwright" or "gullet" or "grafted";
        b.senseT -= dt;
        if (hunter && b.senseT <= 0)
        {
            b.senseT = 0.3f;
            bool sees = b.Perceives();
            if (sees) b.lostT = 0; else b.lostT += 0.3f;
            if (sees && !b.spotted)
            {
                b.spotted = true;
                Alert(b);
                if (stingCool <= 0 && G.bossBar == null) { stingCool = 45; Stings++; G.After(0.12f, () => { if (Score.Active) Score.Sting("seen", 0.9f); else Snd.Sting(); }); }
            }
            if (b.spotted && b.lostT > 8) b.spotted = false;
        }
        b.voiceT -= dt;
        if (b.voiceT <= 0)
        {
            bool asleep = b.mode == "dormant" || (b.type == "gullet" && b.mode == "hang");
            b.voiceT = asleep ? H.Rand(5, 11) : H.Rand(3.5f, 8);
            // the sleeping dead only murmur when you are close enough to hear them breathe
            if (b.busyT <= 0 && (asleep ? d < 7 * H.TILE : d < 22 * H.TILE)) Idle(b);
        }
    }
}
