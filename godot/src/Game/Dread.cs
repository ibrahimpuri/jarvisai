// The dread director. The island is never silent, and never silent the same way twice: when nothing is hunting her,
// the dark makes sounds. Three knocks on the far side of a wall. Nails on stone. Her name, whispered. Wet footsteps
// that pass behind the wall and stop. Something heavy dragged somewhere below. A scream a long way off. In the
// worst places, a child humming the lullaby her mother sang her. And once in a long while, when she has been
// alone in the dark for too long, something slams right beside her.
// It paces itself like a horror film: nothing during a fight or in a Bell Room (fear needs quiet to work in), more
// often as the tide comes up, when her lamp is shuttered, and the longer it has been calm. The sounds come from
// out of view, from inside the walls and below the floor, placed in the room and sent through its reverb.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Dread
{
    static float calm, nextT = 30, bigT = 150;
    public static int Played;
    public static string Last;
    public static bool Enabled = true;
    static readonly (float, float, float) A = (730, 1090, 2440), UH = (520, 1190, 2390), M = (250, 900, 2200), O = (570, 840, 2410), E = (530, 1840, 2480);
    // the lullaby, for the child who hums it
    static readonly float[] LULLABY = { 440, 349.2f, 392, 440, 587.3f, 523.3f, 440, 392, 349.2f };

    public static void Reset() { calm = 0; nextT = 30; bigT = 150; }

    static bool Fight()
    {
        var p = G.player;
        if (G.bossBar != null || G.undertow?.live != null) return true;
        foreach (var e in G.ents)
            if (e is Body b && !b.gone && b.Alive() && !b.corpse && !b.passive && b.mode != "dormant" && b.type != "driftling"
                && (b.spotted || H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy()) < 9 * H.TILE)) return true;
        return p.invulnT > 0;
    }

    public static void Update(float dt)
    {
        var p = G.player; var r = G.room;
        if (!Enabled || G.mode != "play" || p == null || p.dead || r == null || UI.IsOpen()) return;
        if (Fight() || p.inBellRoom) { calm = 0; nextT = MathF.Max(nextT, 10); return; }
        calm += dt; nextT -= dt; bigT -= dt;
        if (nextT > 0) return;
        float pressure = 0.35f + Tide.Level01() * 0.45f + (p.LanternLit() ? 0 : 0.45f) + MathF.Min(calm / 150, 0.5f) + (G.act - 1) * 0.15f
            + (Legend.merged ? 0.15f + Legend.stage * 0.1f : 0);   // the sea in her hand: the dark talks to her more
        nextT = H.Rand(20, 48) / pressure;
        if (bigT <= 0 && calm > 70 && !r.Outdoor && (!p.LanternLit() || H.Rand() < 0.35f)) { Play("slam"); bigT = H.Rand(240, 420); return; }
        Play(Pick(r));
    }

    static string Pick(RoomDef r)
    {
        var w = new List<(string, float)> { ("knock", 3), ("scratch", 2.5f), ("whisper", 1.6f), ("steps", 2), ("drag", 1.5f), ("scream", 1.2f), ("breath", 1) };
        switch (r.Region)
        {
            case "saltworks": w.Add(("chain", 3)); break;
            case "cloister": case "choir": w.Add(("child", 1.2f)); w.Add(("whisper", 1)); break;
            case "skull": w.Add(("chain", 1.5f)); w.Add(("child", 0.8f)); break;
            case "gullet": w.Add(("laugh", 3)); w.RemoveAll(x => x.Item1 is "steps" or "knock"); break;
        }
        if (r.Outdoor) w.RemoveAll(x => x.Item1 is "knock" or "scratch" or "steps" or "drag" or "child");
        if (w.Count == 0) return "scream";
        float total = w.Sum(x => x.Item2), k = H.Rand() * total;
        foreach (var (id, wt) in w) { if ((k -= wt) <= 0) return id; }
        return w[^1].Item1;
    }

    // somewhere out of view: inside the wall beside her, below the floor, or off the edge of the screen
    static Vector2 Spot(float near)
    {
        var p = G.player;
        float side = H.Rand() < 0.5f ? -1 : 1;
        float dx = near > 0 ? H.Rand(near * 0.6f, near) : H.Rand(H.VIEW_W * 0.45f, H.VIEW_W * 0.8f);
        return new Vector2(p.Cx() + side * dx, p.Cy() + H.Rand(-30, 40));
    }

    public static void Play(string id)
    {
        Played++; Last = id;
        var p = G.player;
        switch (id)
        {
            case "knock":
            {
                // three, a pause, and then one more
                var at = Spot(0);
                int n = H.RandI(2, 4);
                Snd.At("dread:knock" + n, 2.6f, s => { for (int i = 0; i < n; i++) Knock(s, i * 0.38f); Knock(s, n * 0.38f + 1.1f, 0.8f); }, at.X, at.Y, 0.7f, 2);
                break;
            }
            case "scratch":
            {
                var at = Spot(70);
                Snd.At("dread:scratch", 2.0f, s => { for (int i = 0; i < 4; i++) s.NoiseSweep(H.Rand(0.25f, 0.45f), 0.5f, "bandpass", H.Rand(2200, 3200), H.Rand(3600, 5200), 6, 0.02f, i * 0.42f); }, at.X, at.Y, 0.35f, 3);
                break;
            }
            case "whisper":
            {
                // her name, close enough to be inside the helmet with her
                var at = Spot(50);
                Snd.At("dread:whisper", 2.0f, s =>
                {
                    if (Legend.merged && Legend.stage >= 2 && H.Rand() < 0.5f) { s.Whisper(1, 0.05f, ("k", 0.06f), ("uh", 0.2f), ("m", 0.3f)); s.Whisper(0.8f, 0.7f, ("d", 0.05f), ("a", 0.3f), ("n", 0.3f)); }
                    else if (H.Rand() < 0.65f) s.Whisper(1, 0.05f, ("m", 0.16f), ("a", 0.3f), ("r", 0.1f), ("e", 0.24f), ("n", 0.3f));
                    else s.Whisper(1, 0.05f, ("m", 0.14f), ("a", 0.28f), ("m", 0.14f), ("a", 0.4f));
                    s.Whisper(0.5f, 1.1f, ("h", 0.2f), ("e", 0.15f), ("l", 0.08f), ("p", 0.05f));
                }, at.X, at.Y, 0.5f, 3);
                break;
            }
            case "steps":
            {
                var at = Spot(0);
                Snd.At("dread:steps", 4.2f, s => { for (int i = 0; i < 6; i++) { float t = i * 0.6f + H.Rand(0, 0.05f); s.Noise(0.06f, 0.8f * (1 - i * 0.1f), "lowpass", 480, 0.7f, 0.003f, t); s.Noise(0.05f, 0.35f, "bandpass", 1600, 3, 0.002f, t + 0.02f); } }, at.X, at.Y, 0.5f, 2);
                break;
            }
            case "drag":
            {
                var at = Spot(0);
                Snd.At("dread:drag", 3.6f, s => { s.NoiseSweep(3.2f, 0.6f, "lowpass", 220, 420, 1, 0.4f); for (int i = 0; i < 14; i++) s.Noise(0.05f, 0.3f, "bandpass", H.Rand(300, 700), 3, 0.005f, 0.2f + i * 0.21f); }, at.X, at.Y + 30, 0.55f, 2);
                break;
            }
            case "scream":
            {
                // a long way off, in another room, and it does not stop until it does
                var at = Spot(0);
                Snd.At("dread:scream", 3.2f, s => { s.Voice(1.8f, 1, 380, 520, A, E, 0.3f, 0.3f, 0.4f, 0.04f, 0.1f, 0.05f, 1, 0.3f, 2, 0.6f, 0.4f); s.Voice(1.0f, 0.6f, 500, 260, E, UH, 0.3f, 0.4f, 0.5f, 0.03f, 2.0f, 0.04f, 1, 0.4f, 2, 0.5f); }, at.X + MathF.Sign(at.X - p.Cx()) * 260, at.Y, 0.32f, 2);
                break;
            }
            case "breath":
            {
                // something breathing right behind her, slow and wet, and then it stops
                var at = new Vector2(p.Cx() - p.facing * 26, p.Cy() - 4);
                Snd.At("dread:breath", 3.4f, s => { s.NoiseSweep(1.1f, 0.45f, "bandpass", 500, 900, 2, 0.4f); s.Voice(0.9f, 0.18f, 64, 58, UH, U(), 0.8f, 0.8f, 0.5f, 0, 0.3f, 0.2f, 1, 0.5f); s.NoiseSweep(1.4f, 0.4f, "bandpass", 900, 380, 2, 0.1f, 1.5f); }, at.X, at.Y, 0.4f, 2);
                break;
            }
            case "chain":
            {
                var at = Spot(0);
                Snd.At("dread:chain", 2.4f, s => { for (int i = 0; i < 26; i++) { float t = i * 0.07f + H.Rand(0, 0.03f); s.Noise(0.02f, 0.5f, "bandpass", H.Rand(2200, 4200), 5, 0.001f, t); s.Tone("triangle", H.Rand(1800, 3200), 0.04f, 0.08f, 0, t); } }, at.X, at.Y - 40, 0.45f, 2);
                break;
            }
            case "child":
            {
                // a child humming the lullaby, somewhere in the dark, slightly wrong
                var at = Spot(0);
                Snd.At("dread:child", 7.5f, s => { float t = 0; foreach (var f in LULLABY) { s.Voice(0.62f, 0.8f, f * 0.99f, f * 0.985f, M, M, 0.25f, 0, 0, 0.02f, t, 0.08f, 1.25f); t += 0.7f + H.Rand(0, 0.12f); } }, at.X, at.Y, 0.32f, 1);
                break;
            }
            case "laugh":
            {
                var at = Spot(0);
                Snd.At("dread:laugh", 1.8f, s => { for (int i = 0; i < 7; i++) s.Voice(0.11f, 0.8f * (1 - i * 0.08f), 160 - i * 6, 140 - i * 6, UH, A, 0.5f, 0.8f, 0.4f, 0, i * 0.15f, 0.01f, 1, 0.6f, 2, 0.3f); }, at.X, at.Y, 0.5f, 2);
                break;
            }
            case "slam":
            {
                // the big one: something hits the wall right beside her, and the strings scream
                var at = new Vector2(p.Cx() + (H.Rand() < 0.5f ? -1 : 1) * 40, p.Cy());
                Snd.At("dread:slam", 2.6f, s => { s.Noise(0.4f, 1, "lowpass", 1600, 0.7f, 0.001f); s.Tone("sine", 70, 0.9f, 1, 30); s.Bell(140, 0.5f, 1.4f); s.Noise(0.6f, 0.3f, "bandpass", 900, 1, 0.01f, 0.05f); }, at.X, at.Y, 1, 2);
                Score.Sting("scare", 1);
                G.Shake(5); G.After(0.15f, Snd.Gasp);
                break;
            }
        }
    }

    static (float, float, float) U() => (300, 870, 2240);
    static void Knock(Synth s, float at, float v = 1)
    {
        s.Noise(0.09f, 0.9f * v, "lowpass", 380, 0.8f, 0.002f, at);
        s.Tone("sine", 110, 0.12f, 0.6f * v, 75, at);
        s.Noise(0.03f, 0.3f * v, "bandpass", 900, 4, 0.001f, at);
    }
}
