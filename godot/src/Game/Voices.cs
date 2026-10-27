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
    static readonly (float, float, float) A = (730, 1090, 2440), O = (570, 840, 2410), U = (300, 870, 2240), E = (530, 1840, 2480), UH = (520, 1190, 2390), M = (250, 900, 2200);
    static readonly float[] HYMN = { 146.8f, 174.6f, 196f, 220f, 261.6f };   // the Choir sings in D
    public static float stingCool = 8;   // the sting never comes twice in quick succession
    public static int Stings;   // tests

    public static void Update(float dt) { if (stingCool > 0) stingCool -= dt; }
    public static void Reset() { stingCool = 8; }

    static void Say(Body b, string what, float seconds, Action<Synth> build, float vol, float busy = 0, int variants = 4)
    {
        // voices sit under the guns and the sea in the mix: heard, placed, never louder than a rivet
        Snd.At(b.type + ":" + what, seconds, build, b.Cx(), b.y + 6, vol * 0.5f, variants);
        b.busyT = MathF.Max(b.busyT, busy > 0 ? busy : seconds * 0.8f);
    }
    static float Dist(Body b) { var p = G.player; return p == null ? 9999 : H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy()); }

    // ---- what each of them sounds like ---------------------------------------------------------------------
    public static void Idle(Body b)
    {
        // the ones that answered the Hollow sing the hymn while they tear at their brothers
        if (b.thrallT > 0)
        {
            float n = H.Pick(HYMN) * 0.5f;
            Say(b, "answer" + n, 2.4f, s => s.Voice(2.2f, 1, n, n, U, O, 0.3f, 0.5f, 0.3f, 0.02f), 0.45f, 0, 2);
            return;
        }
        switch (b.type)
        {
            case "shucker":
                if (b.mode == "dormant") Say(b, "murmur", 1.4f, s => s.Voice(1.3f, 1, H.Rand(62, 74), H.Rand(52, 60), M, M, 0.6f, 0.9f, 0.5f), 0.22f);
                else Say(b, "moan", 2.0f, s => s.Voice(H.Rand(1.4f, 1.9f), 1, H.Rand(76, 90), H.Rand(58, 68), UH, U, 0.35f, 0.7f, 0.3f, 0.01f), 0.5f);
                break;
            case "bellmouth":
                // a bell-ringer humming the Hours to himself
                Say(b, "hum", 2.6f, s => { s.Voice(2.4f, 1, 98, H.Rand(96, 100), M, U, 0.15f, 0.3f, 0, 0.02f, 0, 0.3f); s.Bell(196, 0.12f, 2.2f, 0.3f); }, 0.45f);
                break;
            case "saltwright":
                // heavy breath, in and out, through salt in the lungs
                Say(b, "breath", 2.2f, s => { s.NoiseSweep(0.8f, 0.5f, "bandpass", 600, 1100, 2, 0.3f); s.NoiseSweep(1.0f, 0.4f, "bandpass", 1000, 450, 2, 0.1f, 1.0f); s.Voice(0.9f, 0.25f, 70, 62, UH, U, 0.6f, 0.2f, 0.6f, 0, 1.0f); }, 0.4f);
                break;
            case "gullet":
                Say(b, "click", 1.4f, s => { s.Clicks(H.RandI(6, 11), 1.1f, 0.6f, 3200, 1.6f); s.NoiseSweep(0.5f, 0.1f, "bandpass", 3000, 4200, 6, 0.1f, 0.6f); }, 0.4f);
                break;
            case "grafted":
                if (b.passive || b.mode == "idle" || H.Rand() < 0.6f)
                {
                    // a line of the hymn, sung under the breath
                    float n = H.Pick(HYMN);
                    Say(b, "sing" + n, 2.8f, s => { s.Voice(2.6f, 1, n, n * H.Rand(0.99f, 1.01f), U, O, 0.12f, 0, 0, 0.025f, 0, 0.5f); s.Voice(2.4f, 0.4f, n * 1.5f, n * 1.5f, O, A, 0.15f, 0, 0, 0.03f, 0.2f, 0.5f); }, 0.5f, 0, 2);
                }
                break;
            case "shoal":
                Say(b, "chorus", 2.2f, s => { for (int i = 0; i < 4; i++) s.Voice(H.Rand(1.2f, 1.8f), 0.5f, H.Rand(60, 140), H.Rand(50, 90), UH, U, 0.4f, 0.9f, 0.5f, 0, H.Rand(0, 0.4f)); }, 0.6f);
                break;
        }
    }

    // The moment a hunter first sees Maren.
    public static void Alert(Body b)
    {
        switch (b.type)
        {
            case "shucker":
                Say(b, "alert", 1.1f, s => { s.NoiseSweep(0.4f, 0.4f, "bandpass", 500, 2200, 3, 0.25f); s.Voice(0.85f, 1, H.Rand(150, 175), H.Rand(260, 320), UH, A, 0.3f, 0.3f, 0.55f, 0.02f, 0.25f); }, 0.8f);
                break;
            case "saltwright":
                Say(b, "alert", 1.0f, s => s.Voice(0.9f, 1, 95, 150, UH, A, 0.35f, 0.2f, 0.8f), 0.8f);
                break;
            case "gullet":
                Say(b, "hiss", 0.9f, s => { s.NoiseSweep(0.7f, 0.7f, "bandpass", 2600, 4200, 4, 0.05f); s.Clicks(14, 0.6f, 0.6f, 3500, 0.6f); }, 0.8f);
                break;
            case "grafted":
                // the hymn breaks into a shriek a semitone apart
                Say(b, "shriek", 1.4f, s => { s.Voice(1.2f, 1, 220, 233, A, A, 0.2f, 0, 0.3f, 0.03f); s.Voice(1.2f, 0.8f, 330, 311, A, E, 0.2f, 0, 0.3f, 0.03f, 0.05f); }, 0.8f, 0, 2);
                break;
        }
    }

    public static void Windup(Body b)
    {
        switch (b.type)
        {
            case "shucker": Say(b, "snarl", 0.7f, s => s.Voice(0.6f, 1, H.Rand(90, 105), H.Rand(130, 150), E, A, 0.5f, 0.3f, 0.85f), 0.8f); break;
            case "saltwright": Say(b, "heave", 0.6f, s => s.Voice(0.45f, 1, 105, 145, UH, A, 0.4f, 0.1f, 0.6f, 0, 0, 0.02f), 0.85f); break;
            case "gullet": Say(b, "rattle", 0.6f, s => s.Clicks(18, 0.5f, 0.8f, 2800, 0.5f), 0.7f); break;
            case "bellmouth": Say(b, "grunt", 0.6f, s => s.Voice(0.5f, 1, 85, 70, O, UH, 0.3f, 0.5f, 0.5f), 0.7f); break;
            case "undertow": Say(b, "moan", 1.8f, s => { s.Voice(1.6f, 1, 52, 38, O, U, 0.35f, 0.6f, 0.4f, 0.01f, 0, 0.2f, 0.75f); s.Bell(98, 0.25f, 1.6f, 0.1f); }, 1, 0, 2); break;
            case "abbot": Say(b, "roar", 1.2f, s => { s.Voice(1.0f, 1, 58, 82, A, O, 0.45f, 0.4f, 1, 0, 0, 0.1f, 0.85f); s.Tone("sine", 45, 0.9f, 0.4f, 35); }, 1f, 0, 2); break;
        }
    }

    // Pain: a knot struck (or broken, with a wail when something comes off).
    public static void Hurt(Body b, bool broke)
    {
        if (b.busyT > 0.3f && !broke) return;
        switch (b.type)
        {
            case "shucker":
                if (broke) Say(b, "wail", 1.2f, s => s.Voice(1.1f, 1, H.Rand(240, 280), H.Rand(120, 150), A, O, 0.25f, 0.5f, 0.2f, 0.03f), 0.75f);
                else Say(b, "grunt", 0.35f, s => s.Voice(0.28f, 1, H.Rand(160, 185), H.Rand(100, 120), UH, UH, 0.3f, 0.4f, 0.3f), 0.5f);
                break;
            case "bellmouth": Say(b, broke ? "bellow" : "oof", broke ? 1.0f : 0.4f, s => { s.Voice(broke ? 0.9f : 0.35f, 1, 130, 85, O, U, 0.3f, 0.6f, 0.3f); if (broke) s.Bell(147, 0.2f, 0.8f); }, 0.6f); break;
            case "saltwright": Say(b, broke ? "howl" : "grunt", broke ? 1.0f : 0.35f, s => s.Voice(broke ? 0.9f : 0.3f, 1, broke ? 190 : 125, broke ? 95 : 90, broke ? A : UH, UH, 0.35f, 0.2f, 0.6f), 0.6f); break;
            case "grafted": Say(b, broke ? "keen" : "ah", broke ? 1.2f : 0.45f, s => s.Voice(broke ? 1.1f : 0.4f, 1, broke ? 330 : 280, broke ? 196 : 240, A, broke ? O : A, 0.15f, 0, 0.1f, 0.04f), 0.6f); break;
            case "gullet": Say(b, "squeal", 0.5f, s => { s.NoiseSweep(0.35f, 0.6f, "bandpass", 3600, 1800, 5); s.Clicks(6, 0.3f, 0.5f, 3000); }, 0.6f); break;
            case "abbot": if (broke) Say(b, "agony", 1.6f, s => s.Voice(1.5f, 1, 95, 55, A, U, 0.4f, 0.5f, 0.9f, 0, 0, 0.1f, 0.85f), 1, 0, 2); break;
        }
    }

    public static void Die(Body b)
    {
        switch (b.type)
        {
            case "shucker": case "saltwright":
                // the air and water going out of it, rattling, with bubbles
                Say(b, "rattle", 2.3f, s => { s.Voice(2.1f, 1, H.Rand(72, 82), H.Rand(40, 48), UH, U, 0.7f, 1, 0.8f); s.NoiseSweep(1.6f, 0.15f, "lowpass", 900, 200, 0.7f, 0.05f, 0.3f); }, 0.7f, 2);
                break;
            case "bellmouth": Say(b, "toll", 2.6f, s => { s.Voice(2.2f, 1, 92, 50, O, U, 0.5f, 1, 0.6f); s.Bell(98, 0.4f, 2.4f, 0.2f); }, 0.7f, 2); break;
            case "grafted": Say(b, "fall", 2.4f, s => s.Voice(2.2f, 1, 220, 98, O, U, 0.25f, 0.2f, 0.2f, 0.04f), 0.65f, 2, 2); break;
            case "gullet": Say(b, "gargle", 1.4f, s => { s.NoiseSweep(1.2f, 0.5f, "bandpass", 1600, 300, 2, 0.02f); s.Clicks(8, 1.0f, 0.4f, 2600, 2.2f); }, 0.6f, 1); break;
        }
    }

    // The water stands it up: a gasp drawn in through brine, and salt crackling as the knots grow.
    public static void Rise(Body b)
    {
        if (!b.Humanoid) return;
        Say(b, "rise", 1.8f, s => { s.Clicks(16, 1.2f, 0.25f, 4200, 1.4f); s.NoiseSweep(0.7f, 0.35f, "bandpass", 400, 1500, 2, 0.4f, 0.3f); s.Voice(1.2f, 0.9f, H.Rand(58, 66), H.Rand(88, 100), U, UH, 0.55f, 0.8f, 0.6f, 0, 0.5f); }, 0.65f);
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
                if (stingCool <= 0 && G.bossBar == null) { stingCool = 45; Stings++; G.After(0.12f, Snd.Sting); }
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
