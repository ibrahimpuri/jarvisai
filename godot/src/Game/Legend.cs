// The last night's weapons (Act III). There are two, and each is the other's opposite: one is the sea, and one is
// the lamp.
//
//   The Saint's Tine   At the spring tide the Saint's Ear, a hollow in the bone of the Gullet's dome the exact shape of
//                      the reliquary, runs with brine. Lay the Hollow in it with the Harpoon Gun across it and the bone
//                      grows the two together. The Tine throws a lance of the sea itself that runs through up to three
//                      bodies and breaks a knot in each. It throws on the Hollow's resonance (a quarter a throw) and
//                      the knots it breaks feed it deeper than before; the sea fills it to the brim. Empty, it throws
//                      on Maren's blood. While it is grown the Hollow sings no verses, the bells bleed it dry and heal
//                      her by half, and every throw is loud enough to wake the dead. A bench prises it apart again:
//                      the harpoon comes back as it was, and the Hollow comes back empty.
//   The Burning Glass  Agathe's spare lamp lens, left for Maren in the Ribvault Bell Room. On her lantern it burns in a
//                      line like the great lamp: every knot the beam touches cracks, plates of salt split in the heat,
//                      and the fallen burn so they never stand. It drinks oil fast (fifteen a second, a lantern's worth
//                      in six), slows her to a walk, and after three seconds the lens must cool. Its light is light:
//                      everything that hunts by the lamp sees it.
//
// The sea's hold. Every second the Tine is grown together outside a Bell Room counts, and nothing takes it back except
// the lamp's own light: the Glass burns a little of it out of her while it burns (and burns her too). A quarter of the
// way, the Tine is warm in the morning. Halfway, it has rooted, and prising it out tears her. Three quarters, the
// drowned hesitate before her. All the way, she can hear the Saint under the island, dying and tired, and if she
// still has the Tine in her hand when she reaches the lamp, the sea is waiting at the window for her: the fourth
// ending, the Host of the Mere.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public static class Legend
{
    public const float TINE_COST = 25, HOST_SECONDS = 480;
    const float TINE_SPEED = 640, TINE_LIFE = 0.5f, TINE_DMG = 3, TINE_PIERCE = 3, TINE_BLOOD_HOLD = 20;
    public const float GLASS_OIL = 15, GLASS_HEAT = 3, GLASS_COOL = 2, GLASS_RANGE = 220, GLASS_TICK = 0.28f, GLASS_BURN = 1.2f;

    public static bool merged, glassOwned;
    public static float communion;          // seconds the Tine has been grown together, outside the bells
    public static int stage;                // quarters of the way to the host
    public static WeaponState harpoonWas;   // the harpoon as it went into the Ear
    public static bool beaming;
    public static float heat, coolT;
    public static Vector2 beamA, beamB;
    public static int Thrown;               // tests

    class Lance { public Vector2 p, v; public float t, fade = -1; public readonly HashSet<Body> hit = new(); public readonly List<Vector2> trail = new(); }
    static readonly List<Lance> lances = new();
    static readonly Dictionary<Body, float> burning = new();
    static float glassTick, hissT;
    static bool beamWas;

    public static bool SocketAwake => G.act >= 3;
    public static bool Rooted => stage >= 2;
    public static bool HostReady => merged && communion >= HOST_SECONDS;
    public static float Hold01 => Math.Clamp(communion / HOST_SECONDS, 0, 1);
    // the drowned, seeing what she is becoming, hold back a moment before they strike
    public static float Hesitation => merged && stage >= 3 ? 0.25f : 0;

    // ---- state ---------------------------------------------------------------------------------------
    public static void Reset()
    {
        merged = glassOwned = beaming = beamWas = false; communion = 0; stage = 0; harpoonWas = null;
        heat = coolT = glassTick = hissT = 0; lances.Clear(); burning.Clear(); Thrown = 0;
    }
    public static JsonObject ToJson() => new()
    {
        ["merged"] = merged, ["glass"] = glassOwned, ["communion"] = communion, ["stage"] = stage,
        ["harpoonTier"] = harpoonWas?.tier ?? 0, ["harpoonMag"] = harpoonWas?.mag ?? 1,
    };
    public static void Load(Props p)
    {
        Reset();
        if (p == null) return;
        merged = p.B("merged"); glassOwned = p.B("glass"); communion = p.F("communion"); stage = p.I("stage");
        if (merged) harpoonWas = new WeaponState { tier = p.I("harpoonTier"), mag = p.I("harpoonMag", 1) };
    }

    static Props L => Data.Lore.P("legend");

    // ---- the merge ---------------------------------------------------------------------------------------
    public static void Merge()
    {
        var p = G.player;
        if (merged || !Hollow.owned || !p.weapons.ContainsKey("harpoon")) return;
        harpoonWas = p.weapons["harpoon"];
        int i = p.weaponOrder.IndexOf("harpoon");
        p.weapons.Remove("harpoon");
        p.weapons["tine"] = new WeaponState { tier = 0, mag = 1 };
        if (i >= 0) p.weaponOrder[i] = "tine"; else p.weaponOrder.Add("tine");
        merged = true; Hollow.held = false; p.cur = "tine"; p.reloadT = 0;
        Hollow.resonance = MathF.Max(Hollow.resonance, 50);
        World.flags.Set("tineGrown", true);
        Snd.Merge(); G.Shake(5); G.flash = 0.35f; Pad.Rumble(0.6f, 0.8f, 1.2f);
        Score.Sting("discover", 0.9f);
        G.AddSwell(2);
        FX.Spawn(p.Cx(), p.Cy(), 40, new FXOpt { color = "#a8d0dc", grav = -30, smin = 20, smax = 80, lmin = 0.6f, lmax = 1.6f, size = 1.4f });
        UI.Inscription(L.P("tine").S("name"), L.P("socket").S("merged") + "\n\n" + Glyphs.Plain(L.P("tine").S("desc"), Glyphs.Current));
    }

    // At a bench: the harpoon comes back as it was, the Hollow comes back empty. Once it has rooted it tears her.
    public static void Unmerge()
    {
        var p = G.player;
        if (!merged) return;
        int i = p.weaponOrder.IndexOf("tine");
        p.weapons.Remove("tine");
        p.weapons["harpoon"] = harpoonWas ?? new WeaponState { tier = 0, mag = 1 };
        if (i >= 0) p.weaponOrder[i] = "harpoon"; else p.weaponOrder.Add("harpoon");
        if (p.cur == "tine") p.cur = "harpoon";
        bool torn = Rooted;
        merged = false; harpoonWas = null; lances.Clear();
        Hollow.resonance = 0; Hollow.stillT = 4;
        Snd.Unmerge();
        var U = L.P("unmerge");
        if (torn)
        {
            p.hp = Math.Max(1, p.hp - 2);
            Gore.Bleed(p.Cx(), p.Cy(), p.Cx() - p.facing); Gore.Bleed(p.Cx(), p.Cy() - 4, p.Cx() + p.facing);
            Snd.Hurt(); G.Shake(4); G.flash = 0.3f;
            H.ToastMsg(U.S("torn"), 4.5f);
        }
        else H.ToastMsg(U.S("done"), 3.5f);
    }

    public static void TakeGlass()
    {
        var p = G.player;
        glassOwned = true;
        if (!p.weapons.ContainsKey("glass")) { p.weapons["glass"] = new WeaponState { tier = 0, mag = 1 }; p.weaponOrder.Add("glass"); }
        Snd.Pickup(); Score.Sting("discover", 0.8f);
        var g = L.P("glass");
        UI.Inscription(g.S("name"), g.S("note") + "\n\n" + Glyphs.Plain(g.S("desc"), Glyphs.Current));
    }

    public static void OnActStart(int n)
    {
        if (n == 3) G.After(7, () => H.ToastMsg("Somewhere in the Ribvault a lantern is burning that is not yours. And far below, in the Gullet, something is running with brine.", 6));
    }

    // ---- the Saint's Tine -------------------------------------------------------------------------------
    public static void FireTine(Player p, Vector2 lp)
    {
        if (p.inBellRoom) { Snd.Deny(); H.ToastMsg("The bell drowns it out. The Tine will not throw in here.", 2); return; }
        if (Hollow.resonance >= TINE_COST) Hollow.resonance -= TINE_COST;
        else
        {
            // empty, it drinks from her
            if (p.hp <= 1) { Snd.Deny(); H.ToastMsg("The Tine is empty, and so are you.", 1.8f); return; }
            p.hp -= 1; communion += TINE_BLOOD_HOLD;
            Gore.Bleed(p.Cx(), p.Cy(), p.Cx() - p.facing);
            Snd.TineDrink(); G.flash = 0.2f; G.Shake(2);
            Narr.Think("tineDrinks");
            H.ToastMsg("Empty, it drinks from you.", 1.6f);
        }
        Hollow.stillT = 1.2f;
        var dir = new Vector2(MathF.Cos(p.aim), MathF.Sin(p.aim));
        lances.Add(new Lance { p = lp, v = dir * TINE_SPEED, t = TINE_LIFE });
        Thrown++;
        Snd.TineThrow();
        Noise.Emit(p.Cx(), p.Cy(), 12, "hymn");
        G.Shake(2.5f); Pad.Rumble(0.5f, 0.6f, 0.2f);
        G.AddSwell(0.6f);
        if (!p.InWater()) p.vx -= dir.X * 80;
        FX.Spawn(lp.X, lp.Y, 18, new FXOpt { color = "#a8d0dc", ang = p.aim, spread = 0.4f, smin = 80, smax = 240, grav = 300, lmin = 0.2f, lmax = 0.45f, size = 1.3f });
    }

    static void UpdateLances(float dt)
    {
        for (int i = lances.Count - 1; i >= 0; i--)
        {
            var l = lances[i];
            if (l.fade >= 0) { l.fade -= dt; if (l.fade < 0) lances.RemoveAt(i); continue; }
            var a = l.p; var step = l.v * dt; float len = step.Length();
            float ang = MathF.Atan2(step.Y, step.X);
            float wall = Phys.Ray(a.X, a.Y, ang, len);
            var b = a + step / len * wall;
            // everything in its way, nearest first: a knot it runs through, or the nearest open knot of a body it runs through
            var hits = new List<(float t, Body e, Knot k)>();
            foreach (var e0 in G.ents)
            {
                if (e0 is not Body e || e.gone || e.dead || l.hit.Contains(e)) continue;
                Knot best = null; float bt = 2;
                foreach (var k in e.knots)
                {
                    if (k.broken) continue;
                    var kp = e.KnotPos(k); if (kp == null) continue;
                    float t = H.SegCircle(a.X, a.Y, b.X, b.Y, kp.Value.X, kp.Value.Y, k.r + 2);
                    if (t >= 0 && t < bt) { bt = t; best = k; }
                }
                if (best == null)
                {
                    var hb = e.HitBox();
                    float tb = Combat.RayRect(a.X, a.Y, b.X, b.Y, hb.x, hb.y, hb.w, hb.h);
                    if (tb < 0) continue;
                    bt = tb;
                    var at = a.Lerp(b, tb);
                    best = e.knots.Where(k => !k.broken && !k.plated && !e.IsSealed(k) && e.KnotPos(k) != null)
                        .OrderBy(k => (e.KnotPos(k).Value - at).LengthSquared()).FirstOrDefault();
                }
                hits.Add((bt, e, best));
            }
            foreach (var (t, e, k) in hits.OrderBy(h => h.t))
            {
                l.hit.Add(e);
                var at = a.Lerp(b, t);
                if (k != null && !e.IsSealed(k)) e.DamageKnot(k, TINE_DMG, "tine");
                else e.DamageFlesh(TINE_DMG, at.X, at.Y, "tine");
                if (!e.boss) { e.stun = MathF.Max(e.stun, 0.5f); e.vx += l.v.X * 0.22f; }
                FX.Spawn(at.X, at.Y, 14, new FXOpt { color = "#b8dce4", ang = ang, spread = 0.9f, smin = 60, smax = 200, grav = 320, lmin = 0.2f, lmax = 0.5f, size = 1.2f });
                Snd.TineHit(e.Pan());
                if (l.hit.Count >= TINE_PIERCE) { l.t = 0; b = at; break; }
            }
            l.trail.Add(b); if (l.trail.Count > 12) l.trail.RemoveAt(0);
            l.p = b; l.t -= dt;
            if (wall < len - 0.01f)
            {
                FX.Spawn(b.X, b.Y, 22, new FXOpt { color = "#a8d0dc", ang = ang + H.PI, spread = 1.2f, smin = 60, smax = 220, grav = 380, lmin = 0.25f, lmax = 0.6f, size = 1.4f });
                Snd.Splash(0.3f); l.t = 0;
            }
            if (l.t <= 0) l.fade = 0.22f;
        }
    }

    // ---- the Burning Glass -------------------------------------------------------------------------------
    // Called by the player every frame the Glass is in hand.
    public static void Beam(Player p, bool down, bool pressed, float dt)
    {
        string why = coolT > 0 ? "The lens is still too hot to hold to." : p.oil <= 0 ? "No oil in the lantern to burn." : p.shut ? "The lantern is shuttered." : p.inBellRoom ? "The bell's light is enough in here." : null;
        if (pressed && why != null) { Snd.Deny(); H.ToastMsg(why, 1.5f); }
        bool want = down && why == null;
        if (want && !beaming)
        {
            if (merged)
            {
                // the light finds the salt in her hand
                if (p.hp <= 1) { if (pressed) { Snd.Deny(); H.ToastMsg("The light would burn out what is left of you.", 2); } return; }
                p.hp -= 1; Gore.Bleed(p.Cx(), p.Cy(), p.Cx() - p.facing); Snd.Hurt(); G.flash = 0.2f;
                Narr.Think("glassBurnsMe");
                H.ToastMsg("The light finds the salt in your hand.", 2);
            }
            Snd.GlassOn(); Narr.Think("glassFirst");
        }
        beaming = want;
        if (!beaming) return;
        p.oil = MathF.Max(0, p.oil - GLASS_OIL * dt);
        heat += dt;
        if (heat >= GLASS_HEAT)
        {
            coolT = GLASS_COOL; heat = 0; beaming = false;
            Snd.GlassCrack(); H.ToastMsg("The lens is glowing. Let it cool.", 1.5f);
            return;
        }
        // the lamp's light burns the sea out of her, a little
        if (merged) communion = MathF.Max(0, communion - dt * 4);
        var a = p.LanternPos(); float ang = p.aim;
        float d = Phys.Ray(a.X, a.Y, ang, GLASS_RANGE);
        var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
        var b = a + dir * d;
        beamA = a; beamB = b;
        glassTick -= dt; hissT -= dt;
        bool tick = glassTick <= 0;
        if (tick) glassTick = GLASS_TICK;
        foreach (var e0 in G.ents.ToArray())
        {
            if (e0 is not Body e || e.gone || e.dead) continue;
            var hb = e.HitBox();
            float t = Combat.RayRect(a.X, a.Y, b.X, b.Y, hb.x - 2, hb.y - 2, hb.w + 4, hb.h + 4);
            if (t < 0) continue;
            // the fallen burn, and do not stand again
            if ((e.mode == "downed" || e.corpse) && !e.boss)
            {
                if (e.burnT > 0) continue;
                burning[e] = burning.GetValueOrDefault(e) + dt;
                if (H.Rand() < dt * 20) FX.Embers(e.Cx() + H.Rand(-6, 6), e.Cy(), 1);
                if (burning[e] >= GLASS_BURN) { burning.Remove(e); e.Burn(); }
                continue;
            }
            if (!tick) continue;
            // the knot nearest the line burns (a crown still sealed under the others only glows)
            Knot best = null; float bd = 1e9f;
            foreach (var k in e.knots)
            {
                if (k.broken || e.IsSealed(k)) continue;
                var kp = e.KnotPos(k); if (kp == null) continue;
                float dd = DistToSeg(kp.Value, a, b);
                if (dd < bd) { bd = dd; best = k; }
            }
            if (best != null) { e.DamageKnot(best, 1, "glass"); var kp = e.KnotPos(best) ?? new Vector2(e.Cx(), e.Cy()); FX.Embers(kp.X, kp.Y, 3); }
            else e.DamageFlesh(1, e.Cx(), e.Cy(), "glass");
            if (hissT <= 0) { hissT = 0.4f; Snd.GlassHiss(e.Pan()); }
        }
        if (H.Rand() < dt * 30) FX.Embers(b.X + H.Rand(-2, 2), b.Y + H.Rand(-2, 2), 1);
    }
    static float DistToSeg(Vector2 q, Vector2 a, Vector2 b)
    {
        var ab = b - a; float l2 = ab.LengthSquared();
        float t = l2 < 0.001f ? 0 : Math.Clamp((q - a).Dot(ab) / l2, 0, 1);
        return (a + ab * t - q).Length();
    }

    // ---- every frame ---------------------------------------------------------------------------------------
    public static void Update(float dt)
    {
        var p = G.player;
        if (p == null) return;
        UpdateLances(dt);
        if (coolT > 0) coolT -= dt;
        if (p.cur != "glass" || p.dead || p.Locked() || Hollow.held) beaming = false;
        if (!beaming) heat = MathF.Max(0, heat - dt * 1.2f);
        if (beaming != beamWas) { beamWas = beaming; if (!beaming) burning.Clear(); }
        if (!merged || p.dead) return;
        // the bells hurt what carries the sea: in a Bell Room it bleeds the Hollow dry, and nothing counts
        if (p.inBellRoom) { Hollow.resonance = MathF.Max(0, Hollow.resonance - dt * 6); return; }
        if (G.mode != "play" || UI.IsOpen()) return;
        communion += dt;
        int s = Math.Min(4, (int)(communion / (HOST_SECONDS / 4)));
        if (s > stage) { stage = s; OnStage(s); }
    }
    static void OnStage(int s)
    {
        Narr.Think("host" + s);
        Snd.SaintGroan(0);
        if (s >= 2) Dread.Play("whisper");
        if (s == 4) { Score.Sting("turn", 0.8f); G.Shake(3); }
        // reaching the lamp holding it at the last is what opens the window
        if (s == 4 && G.room?.Id == "lamp") SpawnWindow(G.room);
    }

    public static void OnRoomEnter(RoomDef r)
    {
        lances.Clear(); burning.Clear(); beaming = false;
        if (r.Id == "lamp" && HostReady) SpawnWindow(r);
        if (r.Id == "lamp" && Story.VerseReady) SpawnMusicBox(r);
    }
    // On the Mere-Saint crossing, with all nine pages, Agathe's music box stands open by the lamp.
    public static void SpawnMusicBox(RoomDef r)
    {
        if (G.ents.Any(e => e is MusicBox)) return;
        var s = Props.Empty(); s.Set("type", "musicbox"); s.Set("x", r.Px + 20 * H.TILE + 8); s.Set("y", r.Py + 17 * H.TILE); s.Set("key", "music_box");
        G.ents.Add(new MusicBox(s, r));
    }
    static void SpawnWindow(RoomDef r)
    {
        if (G.ents.Any(e => e is SeaWindow)) return;
        var s = Props.Empty(); s.Set("type", "seawindow"); s.Set("x", r.Px + 35 * H.TILE + 8); s.Set("y", r.Py + 17 * H.TILE); s.Set("key", "sea_window");
        G.ents.Add(new SeaWindow(s, r));
    }

    // ---- light and drawing -------------------------------------------------------------------------------
    public static void AddLights(List<LightSrc> lights)
    {
        if (beaming)
        {
            lights.Add(new LightSrc { x = beamB.X, y = beamB.Y, r = 60, i = 1.1f, color = "255,236,196" });
            var m = beamA.Lerp(beamB, 0.5f);
            lights.Add(new LightSrc { x = m.X, y = m.Y, r = 70, i = 0.6f, color = "255,226,170" });
        }
        foreach (var l in lances) if (l.fade < 0) lights.Add(new LightSrc { x = l.p.X, y = l.p.Y, r = 40, i = 0.5f, color = "120,220,210" });
        var p = G.player;
        if (merged && p != null && stage >= 3 && !p.dead) lights.Add(new LightSrc { x = p.Cx(), y = p.y + 4, r = 18, i = 0.25f, color = "90,220,190" });
    }

    public static void DrawWorld(Canvas c)
    {
        foreach (var l in lances)
        {
            float k = l.fade >= 0 ? l.fade / 0.22f : 1;
            if (l.trail.Count == 0) continue;
            var head = l.trail[^1];
            var d = l.v.Normalized(); var n = new Vector2(-d.Y, d.X);
            // a rope of sea water, foam at its head, the body of it thinning back along the trail
            for (int i = 1; i < l.trail.Count; i++)
            {
                float f = i / (float)l.trail.Count;
                c.Stroke = new Color(0.55f, 0.82f, 0.86f, 0.55f * f * k); c.LineWidth = 0.6f + 2.4f * f;
                c.Line(l.trail[i - 1].X, l.trail[i - 1].Y, l.trail[i].X, l.trail[i].Y);
            }
            c.Stroke = new Color(0.92f, 0.98f, 1f, 0.8f * k); c.LineWidth = 0.8f;
            var tail = head - d * 14;
            c.Line(tail.X, tail.Y, head.X, head.Y);
            c.Fill = new Color(0.95f, 1, 1, 0.85f * k);
            for (int i = 0; i < 5; i++) { var q = head - d * TileArt.Hash(i, Thrown) * 5 + n * (TileArt.Hash(i, 7) - 0.5f) * 4; c.FillCircle(q.X, q.Y, 0.6f + TileArt.Hash(i, 9) * 0.8f); }
            c.Glow(head.X, head.Y, 10, new Color(0.4f, 0.9f, 0.85f, 0.25f * k));
        }
        if (beaming)
        {
            var a = beamA; var b = beamB; var d = (b - a).Normalized(); var n = new Vector2(-d.Y, d.X);
            float fl = 0.85f + 0.15f * MathF.Sin(G.Now * 50);
            c.FillPolyGrad(new[] { a + n * 1.2f, b + n * 4.5f, b - n * 4.5f, a - n * 1.2f },
                new[] { new Color(1, 0.95f, 0.8f, 0.5f * fl), new Color(1, 0.9f, 0.7f, 0.16f), new Color(1, 0.9f, 0.7f, 0.16f), new Color(1, 0.95f, 0.8f, 0.5f * fl) });
            c.Stroke = new Color(1, 0.98f, 0.92f, 0.9f * fl); c.LineWidth = 0.9f; c.Line(a.X, a.Y, b.X, b.Y);
            // motes burning in it
            for (int i = 0; i < 10; i++)
            {
                float f = (G.Now * 1.7f + TileArt.Hash(i, 3)) % 1;
                var q = a.Lerp(b, f) + n * (TileArt.Hash(i, 5) - 0.5f) * 5 * f;
                c.Fill = new Color(1, 0.85f, 0.55f, 0.8f * (1 - f)); c.FillRect(q.X - 0.4f, q.Y - 0.4f, 0.8f, 0.8f);
            }
            c.Glow(b.X, b.Y, 14, new Color(1.2f, 0.9f, 0.6f, 0.4f * fl));
        }
        // what it is doing to her: the bone along her glove, and later her eyes in the helmet
        var p = G.player;
        if (merged && p != null && !p.dead && stage >= 1)
        {
            float hx = p.Cx() + p.facing * 3, hy = p.y + 4;
            if (stage >= 3) { c.Fill = new Color(0.55f, 1f, 0.85f, 0.8f + 0.2f * MathF.Sin(G.Now * 2)); c.FillRect(hx - 0.5f + p.facing * 0.6f, hy - 0.4f, 1.1f, 0.6f); }
            if (stage >= 4 && H.Rand() < 0.04f) FX.Spawn(p.Cx() + p.facing * 4, p.y + 12, 1, new FXOpt { color = "#8ab8b0", grav = 300, smin = 0, smax = 10, lmin = 0.4f, lmax = 0.8f, size = 0.8f });
        }
    }

    // Under the Hollow's glass on the HUD: a thin vein of the sea's hold on her, quarter by quarter.
    public static void DrawVein(Canvas c, float x, float y, float w)
    {
        float f = Hold01;
        c.Fill = new Color(0, 0, 0, 0.45f); c.FillRect(x, y - 0.6f, w, 1.2f);
        if (f > 0)
        {
            float pulse = 0.6f + 0.4f * MathF.Sin(G.Now * (1.5f + stage));
            c.Fill = new Color(0.25f + 0.2f * f, 0.55f, 0.48f, (0.45f + 0.4f * f) * pulse); c.FillRect(x, y - 0.4f, w * f, 0.8f);
        }
        for (int i = 1; i < 4; i++) c.FillRect(x + w * i / 4f - 0.3f, y - 1, 0.6f, 2, i <= stage ? new Color(0.6f, 0.95f, 0.82f, 0.8f) : new Color(0.3f, 0.35f, 0.33f, 0.6f));
    }

    // The weapon panel while a legendary is in hand.
    public static void DrawPanel(Canvas c, Player p, float x, float y, float pw)
    {
        c.Font = "8px caps"; c.Fill = H.Col("#eadfc4");
        if (p.cur == "tine")
        {
            c.FillText(L.P("tine").S("name"), x + 33, y + 13);
            int throws = (int)(Hollow.resonance / TINE_COST);
            for (int i = 0; i < 4; i++)
            {
                float mx = x + 37 + i * 9, my = y + 23;
                c.Fill = i < throws ? H.Col("#9fd8c8") : new Color(0.2f, 0.25f, 0.23f, 0.85f);
                c.Poly(new[] { new Vector2(mx - 3, my + 1.4f), new Vector2(mx + 3.4f, my), new Vector2(mx - 3, my - 1.4f) });
            }
            c.Font = "6px serif"; c.Fill = throws == 0 ? H.Col("#d07a6a") : H.Col("#a89c80");
            c.FillText(throws == 0 ? "empty: it will drink from you" : "throws on the Hollow", x + 33, y + 33);
        }
        else
        {
            c.FillText(L.P("glass").S("name"), x + 33, y + 13);
            float gx = x + 36, gw = 70, f = Math.Clamp(heat / GLASS_HEAT, 0, 1);
            c.Fill = new Color(0, 0, 0, 0.55f); c.FillRect(gx, y + 20, gw, 4);
            if (coolT > 0) { c.Fill = new Color(0.9f, 0.3f + 0.2f * MathF.Sin(G.Now * 12), 0.15f, 0.9f); c.FillRect(gx, y + 20, gw * coolT / GLASS_COOL, 4); }
            else if (f > 0) c.FillRectGradV(gx, y + 20, gw * f, 4, H.Col("#fff0c0"), H.Col("#e08a30"));
            c.Font = "6px serif"; c.Fill = coolT > 0 ? H.Col("#d07a6a") : H.Col("#a89c80");
            c.FillText(coolT > 0 ? "cooling" : $"burns oil: {(int)(p.oil / GLASS_OIL)} s left", x + 33, y + 33);
        }
    }
}

// The Saint's Ear: a hollow in the roof of the Gullet's dome, curled like a shell. Before the spring tide it is dry,
// and the letters round it are only letters. At the spring tide it runs with brine and hums.
public class EarSocket : Obj
{
    float hum;
    public EarSocket(Props spec, RoomDef room) : base(spec, room) { w = 20; h = 14; x = spec.F("x") - 10; y = spec.F("y") - H.TILE; }
    public override bool Near(Player p) => H.Dist(p.Cx(), p.Cy(), Cx(), Cy()) < 36;
    public override string Prompt() => "{interact} The Saint's Ear";
    bool Wet => Legend.SocketAwake && !Legend.merged;
    public override void Interact()
    {
        var L = Data.Lore.P("legend").P("socket"); var p = G.player;
        if (!Legend.SocketAwake) { UI.Inscription(L.S("name"), L.S("dry")); Narr.Think("socketDry"); return; }
        if (Legend.merged) { UI.Say(null, L.S("already")); return; }
        if (!Hollow.owned) { UI.Inscription(L.S("name"), L.S("wet") + "\n\n" + L.S("noHollow")); return; }
        if (!p.weapons.ContainsKey("harpoon")) { UI.Inscription(L.S("name"), L.S("wet") + "\n\n" + L.S("noHarpoon")); return; }
        UI.Inscription(L.S("name"), L.S("wet"), () => G.After(0.05f, () => UI.Confirm(L.S("ask"), Legend.Merge)));
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        if (!Wet) return;
        hum -= dt;
        if (hum <= 0) { hum = 2.8f; Snd.At("ear:hum", 2.6f, s => { s.Voice(2.4f, 0.5f, 73.4f, 73.4f, (300, 870, 2240), (570, 840, 2410), 0.3f, 0.5f, 0.2f, 0.02f, 0, 0.8f, 0.8f, 0.4f, 3); s.Tine(293.7f, 0.06f, 2.2f, 0.3f); }, Cx(), Cy(), 0.4f, 1); }
        if (H.Rand() < dt * 3) FX.Spawn(Cx() + H.Rand(-6, 6), y + h, 1, new FXOpt { color = "#9ac8c4", grav = 120, smin = 0, smax = 10, lmin = 0.6f, lmax = 1.2f, size = 0.8f });
    }
    public override LightSrc Light() => Wet ? new LightSrc { x = Cx(), y = y + 10, r = 70 + MathF.Sin(t * 1.4f) * 6, i = 0.95f, color = "110,230,200" } : new LightSrc { x = Cx(), y = y + 8, r = 26, i = 0.25f, color = "200,190,160" };
    public override void Draw(Canvas c)
    {
        float cx = Cx(), cy = y + 6;
        // the boss of bone it is cut into, grown down out of the dome's roof on a stalk
        c.Fill = H.Col("#2a2620"); c.Poly(new[] { new Vector2(cx - 9, y - 18), new Vector2(cx + 9, y - 18), new Vector2(cx + 13, cy - 2), new Vector2(cx - 13, cy - 2) });
        c.FillRectGradV(cx - 7, y - 18, 14, 20, H.Col("#6a604c"), H.Col("#8a8068"));
        c.Fill = H.Col("#1a1814"); c.BeginPath(); c.Ellipse(cx, cy - 1, 17, 10, 0, 0, H.PI * 2); c.FillPath();
        c.FillRectGradV(cx - 15, cy - 7, 30, 10, H.Col("#a89c80"), H.Col("#5a5242"));
        // the lip, and the spiral hollow in it, curled like a shell
        c.Fill = H.Col("#f6efdc"); c.BeginPath(); c.Ellipse(cx, cy, 12, 7, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Col("#b8ac8c"); c.BeginPath(); c.Ellipse(cx, cy + 1, 10.4f, 5.8f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = Wet ? H.Col("#0f3a34") : H.Col("#16130f"); c.BeginPath(); c.Ellipse(cx + 0.8f, cy + 0.6f, 7.6f, 4.4f, 0, 0, H.PI * 2); c.FillPath();
        c.Stroke = H.Col("#d8ccae"); c.LineWidth = 0.8f;
        c.BeginPath(); c.Arc(cx + 1, cy + 0.6f, 4.4f, 0.4f, H.PI * 1.8f); c.StrokePath();
        c.BeginPath(); c.Arc(cx + 1.6f, cy + 0.6f, 2.2f, 0.8f, H.PI * 1.9f); c.StrokePath();
        // the letters round the lip
        c.Fill = new Color(0.3f, 0.26f, 0.18f, 0.95f);
        for (int i = 0; i < 18; i++) { float a = H.PI * 2 * i / 18; c.FillRect(cx + MathF.Cos(a) * 11.2f - 0.4f, cy + MathF.Sin(a) * 6.4f - 0.6f, 0.8f, 1.2f); }
        if (Wet)
        {
            float g = 0.55f + 0.35f * MathF.Sin(t * 1.4f);
            c.Glow(cx, cy + 1, 22, new Color(0.3f, 0.95f, 0.8f, 0.35f * g));
            // wet, the letters round the lip glow, and the curl of the hollow, so it shows through the deep water
            for (int i = 0; i < 18; i++)
            {
                float a = H.PI * 2 * i / 18, w = 0.5f + 0.5f * MathF.Sin(t * 2 - i * 0.7f);
                c.Glow(cx + MathF.Cos(a) * 11.2f, cy + MathF.Sin(a) * 6.4f, 2.4f, new Color(0.75f, 1f, 0.92f, (0.35f + 0.4f * w) * g));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = 0.4f + i * 0.5f, rr = 4.6f - i * 0.28f;
                c.Glow(cx + 1 + MathF.Cos(a) * rr, cy + 0.6f + MathF.Sin(a) * rr * 0.6f, 1.8f, new Color(0.6f, 1f, 0.9f, 0.45f * g));
            }
            c.Fill = new Color(0.45f, 1f, 0.85f, 0.35f * g); c.BeginPath(); c.Ellipse(cx + 0.8f, cy + 0.6f, 5, 2.6f, 0, 0, H.PI * 2); c.FillPath();
            // brine running out of it, though nothing drips into it
            for (int i = 0; i < 3; i++)
            {
                float ph = (t * 0.6f + i * 0.33f) % 1;
                c.Fill = new Color(0.6f, 0.9f, 0.88f, 0.75f * (1 - ph)); c.FillRect(cx - 5 + i * 4.5f + MathF.Sin(t + i) * 0.8f, cy + 6 + ph * 10, 0.8f, 1.8f);
            }
        }
    }
}

// Agathe's spare lamp lens in its brass hood, tied with her red cloth, on the floor of the Ribvault Bell Room.
public class GlassCase : Obj
{
    public GlassCase(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 9; x = spec.F("x") - 6; y = spec.F("y") - 9; }
    public override string Prompt() => done || Legend.glassOwned ? null : "{interact} A lantern hood, and a note";
    public override bool Near(Player p) => NearR(p, 18);
    public override void Interact() { if (done || Legend.glassOwned) return; done = true; gone = true; Legend.TakeGlass(); }
    public override LightSrc Light() => done ? null : new LightSrc { x = Cx(), y = Cy(), r = 34 + MathF.Sin(t * 3) * 2, i = 0.55f, color = "255,226,170" };
    public override void Draw(Canvas c)
    {
        if (done) return;
        float cx = Cx(), by = y + h;
        c.Fill = H.Col("#6a5226"); c.FillRect(cx - 5, by - 6, 10, 6);
        c.FillRectGradV(cx - 4.4f, by - 5.6f, 8.8f, 5, H.Col("#d8b060"), H.Col("#7a5a26"));
        // the lens, catching the bell's light
        c.Fill = H.Col("#f4ecd8"); c.BeginPath(); c.Ellipse(cx, by - 6.4f, 3.4f, 2.2f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = new Color(1, 1, 1, 0.6f + 0.4f * MathF.Sin(t * 2.4f)); c.FillRect(cx - 1.6f, by - 7.4f, 1.2f, 0.7f);
        // her red cloth, and the label
        c.Fill = H.Col("#a02a20"); c.FillRect(cx + 3.6f, by - 5, 1.4f, 4); c.FillRect(cx + 4.4f, by - 1.4f, 2.4f, 1);
        c.Fill = H.Col("#d8ccae"); c.FillRect(cx + 5.4f, by - 3.6f, 3, 2.2f);
        c.Glow(cx, by - 6, 8, new Color(1.2f, 0.95f, 0.6f, 0.18f));
    }
}

// The lamp room's seaward window, once the sea has its hold on her: the sea pressed up against the glass, impossibly
// high, two hundred and twelve steps above the reef. Faces in it.
public class SeaWindow : Obj
{
    public SeaWindow(Props spec, RoomDef room) : base(spec, room) { w = 22; h = 64; x = spec.F("x") - 11; y = spec.F("y") - 64; }
    public override string Prompt() => "{interact} The sea, at the window";
    public override bool Near(Player p) => NearR(p, 16);
    public override void Interact() => Story.Ending("host");
    public override Props Serialize() => null;
    public override LightSrc Light() => new() { x = Cx(), y = y + 24, r = 60, i = 0.5f, color = "70,180,170" };
    public override void Draw(Canvas c)
    {
        float x0 = x, y0 = y, ww = w, hh = h - 8;
        // a lancet window, its stone frame
        c.Fill = H.Col("#2a2620"); c.FillRect(x0 - 2, y0 + 6, ww + 4, hh);
        c.BeginPath(); c.Arc(x0 + ww / 2, y0 + 8, ww / 2 + 2, H.PI, H.PI * 2); c.FillPath();
        // the sea behind the glass, all the way up
        c.FillRectGradV(x0, y0 + 8, ww, hh - 2, H.Col("#0d2a2a"), H.Col("#04100f"));
        c.Fill = H.Col("#0d2a2a"); c.BeginPath(); c.Arc(x0 + ww / 2, y0 + 8, ww / 2, H.PI, H.PI * 2); c.FillPath();
        // light moving in it, and the faces pressed to the glass
        for (int i = 0; i < 4; i++)
        {
            float fx = x0 + 4 + TileArt.Hash(i, 11) * (ww - 8), fy = y0 + 14 + i * 10 + MathF.Sin(t * 0.6f + i) * 2;
            float a = 0.25f + 0.2f * MathF.Sin(t * 0.8f + i * 1.7f);
            c.Fill = new Color(0.55f, 0.7f, 0.66f, a); c.BeginPath(); c.Ellipse(fx, fy, 2.4f, 3, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = new Color(0.02f, 0.05f, 0.05f, a * 2); c.FillRect(fx - 1.2f, fy - 0.6f, 0.8f, 0.8f); c.FillRect(fx + 0.5f, fy - 0.6f, 0.8f, 0.8f); c.FillRect(fx - 0.6f, fy + 1.2f, 1.2f, 0.5f);
        }
        c.Stroke = new Color(0.6f, 0.85f, 0.8f, 0.2f + 0.1f * MathF.Sin(t * 2)); c.LineWidth = 0.5f;
        for (int i = 0; i < 3; i++) { float wy = y0 + 20 + i * 14 + MathF.Sin(t + i) * 3; c.Line(x0 + 1, wy, x0 + ww - 1, wy + 1); }
        // the leading
        c.Stroke = H.Col("#1a1612"); c.LineWidth = 0.8f; c.Line(x0 + ww / 2, y0 + 2, x0 + ww / 2, y0 + hh + 4);
        for (int i = 1; i < 4; i++) c.Line(x0, y0 + 8 + i * hh / 4, x0 + ww, y0 + 8 + i * hh / 4);
        // the sill, wet
        c.Fill = H.Col("#3a342a"); c.FillRect(x0 - 3, y0 + hh + 4, ww + 6, 3);
        c.Fill = new Color(0.5f, 0.75f, 0.75f, 0.5f); c.FillRect(x0 - 2, y0 + hh + 4, ww + 4, 0.7f);
        c.Glow(Cx(), y0 + 30, 30, new Color(0.2f, 0.6f, 0.55f, 0.12f));
    }
}

// Agathe's music box: Mere-ivory, its lid carved with the lamp and a little boat, the tune it plays the lullaby she sang
// Maren. On the Mere-Saint crossing, with all nine pages read, it is how the seventh verse is sung.
public class MusicBox : Obj
{
    public MusicBox(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 9; x = spec.F("x") - 6; y = spec.F("y") - 9; }
    public override string Prompt() => "{interact} " + Data.Lore.P("saint").S("box") + ": sing the seventh verse";
    public override bool Near(Player p) => NearR(p, 16);
    public override void Interact() => Story.Ending("verse");
    public override Props Serialize() => null;
    public override LightSrc Light() => new() { x = Cx(), y = y, r = 46, i = 0.45f, color = "120,220,200" };
    public override void Draw(Canvas c)
    {
        float t = G.Now, x0 = x, y0 = y;
        // the box, its lid open, a comb of brass and a turning drum
        c.Fill = H.Col("#d9ccae"); c.FillRect(x0, y0 + 3, w, h - 3);
        c.Fill = H.Col("#b8a888"); c.FillRect(x0, y0 + h - 2, w, 2);
        c.Save(); c.Translate(x0 + 1, y0 + 3); c.Rotate(-1.1f);
        c.Fill = H.Col("#e8dcc0"); c.FillRect(0, -1.2f, w - 1, 1.6f);
        c.Restore();
        c.Fill = H.Col("#8a6a3a"); c.FillRect(x0 + 2, y0 + 4, w - 4, 1);
        c.Fill = H.Col("#c0a060"); for (int i = 0; i < 5; i++) c.FillRect(x0 + 3 + i * 1.5f, y0 + 5, 0.6f, 2);
        // the tune, rising off it as little sea-green notes
        for (int i = 0; i < 4; i++)
        {
            float ph = (t * 0.4f + i / 4f) % 1;
            c.Fill = new Color(0.55f, 0.9f, 0.82f, 0.7f * (1 - ph));
            float nx = x0 + w / 2 + MathF.Sin(t * 1.5f + i * 2) * 6, ny = y0 - ph * 26;
            c.FillCircle(nx, ny, 1.1f); c.FillRect(nx + 0.8f, ny - 3.2f, 0.5f, 3.2f);
        }
        c.Glow(Cx(), y0 + 4, 14, new Color(0.5f, 0.9f, 0.8f, 0.2f + 0.08f * MathF.Sin(t * 2)));
    }
}
