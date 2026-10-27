// The Mere's Hollow: a reliquary box of Mere-ivory holding the Saint's ear-bone. The first brothers found the
// bone on the strand, curled like a shell and humming, and named the island for it. The Choir sang into it
// every Hour and the tide came when it was called; when the Abbot wanted it for his pans, Hesper rowed it out
// past the reef and gave it back to the sea. Forty nights later the sea brings it up the Long Pier to Maren.
//
// It hears, and it answers. Sung a verse of the Choir's hymn, the Saint moves the water:
//   The Calling   Sing to the sea                          - the room's sea rises or falls to where Maren points;
//                                                            brimming, a held note makes the Saint breathe early
//   The Riptide   ...and the sea will take you home        - a rush of the sea throws back what stands before her
//   The Salting   Sing to the salt and the salt will rise  - salt crusts the drowned (the next blow shatters a
//                                                            limb), or rises from stone as a pillar
//   The Answer    Sing to the bone and the bone will answer - one of the drowned turns on its own kind, then
//                                                            falls apart for good
// Its voice is a bar of energy, resonance, and every verse spends a share of it. Breaking a knot lets out the brine
// that held it, and the Hollow drinks it in: that is how it fills. Held in the sea it drinks slowly, and only to
// halfway (the sea gives back what it hears, and it has heard her less than the drowned). A sung verse rings in the
// bone for a moment, so nothing is drunk from the sea until it stills, and each verse must ring itself out before it
// can be sung again. Singing is loud: everything near hears it, the swell rises, and the sea it calls stands the dead
// up like any tide. It is a last breath, not a weapon: Maren is still the thing being hunted.
// At the spring tide the Hollow can be grown into the Harpoon Gun at the Saint's Ear (Legend.cs): then it sings no
// verses at all, and its resonance feeds the Saint's Tine.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public static class Hollow
{
    // cost: resonance spent; ring: seconds before the same verse can be sung again
    public record Verse(string id, string name, string line, float cost, float ring);
    public static readonly Verse[] VERSES =
    {
        new("calling", "The Calling", "Sing to the sea", 30, 4),
        new("riptide", "The Riptide", "...and the sea will take you home", 35, 6),
        new("salting", "The Salting", "Sing to the salt and the salt will rise", 40, 7),
        new("answer", "The Answer", "Sing to the bone and the bone will answer", 70, 30),
    };
    public const float MAX = 100;
    public const float SEA_FILL = 50;      // the sea alone fills it this far
    const float KNOT_GAIN = 4, CROWN_GAIN = 8, SEA_RATE = 2.5f, STILL_T = 2.5f;
    const int TS = H.TILE;

    public static bool owned, held;
    public static List<string> verses = new();
    public static string verse = "calling";
    public static float resonance;
    public static float singCool, holdT, flash, verseShowT;
    public static float stillT;            // the bone still ringing after a verse: it drinks nothing from the sea
    public static readonly Dictionary<string, float> ringing = new();   // each verse ringing itself out
    public static float Ringing(string id) => ringing.TryGetValue(id, out var t) ? MathF.Max(0, t) : 0;
    static bool pressing, turnedThisPress;
    // The Calling: the room's sea moved toward where Maren pointed, in tiles (positive is higher)
    public static string surgeRoom;
    public static float surge, surgeTarget, surgeHold;
    public static float rush;              // tide still to rush through after the Saint was made to breathe
    // The Salting: salt raised from stone, to crumble again
    class Pillar { public RoomDef room; public int tx, ty; public char was; public float t; }
    static readonly List<Pillar> pillars = new();
    // the Riptide's spray and the Salting's bolt, for drawing
    class Wake { public Vector2 a, b; public float t, max; public string kind; }
    static readonly List<Wake> wakes = new();
    // the gift on the Long Pier
    static float giftT = -1, onPier, learnT;
    public static int Sung;                // tests

    public static Verse Def(string id) => VERSES.First(v => v.id == id);
    public static Verse Cur => Def(verse);
    public static bool Has(string id) => verses.Contains(id);

    // ---- state ----------------------------------------------------------------------------------------
    public static void Reset()
    {
        owned = held = false; verses = new(); verse = "calling"; resonance = 0;
        singCool = holdT = flash = verseShowT = stillT = 0; pressing = turnedThisPress = false; ringing.Clear();
        surge = surgeTarget = surgeHold = 0; surgeRoom = null; rush = 0;
        RevertPillars(); wakes.Clear(); giftT = -1; onPier = 0; learnT = 0; Sung = 0;
    }
    public static JsonObject ToJson() => new()
    {
        ["owned"] = owned, ["held"] = held, ["verse"] = verse, ["resonance"] = resonance,
        ["verses"] = new JsonArray(verses.Select(v => (JsonNode)v).ToArray()),
    };
    public static void Load(Props p)
    {
        Reset();
        if (p == null) return;
        owned = p.B("owned"); held = owned && p.B("held"); verse = p.S("verse") ?? "calling"; resonance = p.F("resonance");
        verses = p.Strings("verses");
        if (!Has(verse) && verses.Count > 0) verse = verses[0];
    }

    // ---- taking it up ----------------------------------------------------------------------------------
    public static void Take()
    {
        if (owned) return;
        owned = true; held = true;
        var f = World.flags; f.Set("hollowTaken", true); f.O.Remove("hollowX"); f.O.Remove("hollowY");
        resonance = MathF.Max(resonance, 40);
        Snd.Pickup(); Snd.Verse("calling"); Score.Sting("discover", 0.8f);
        var L = Data.Lore.P("hollow");
        UI.Inscription(L.S("name"), L.S("reveal"), () =>
        {
            Learn("calling", false);
            // whatever it would have heard while it lay in the sea, it heard
            if (f.B("glimpse")) Learn("riptide", false);
            if (G.narr.regions.Contains("saltworks")) Learn("salting", false);
            if (G.narr.regions.Contains("cloister")) Learn("answer", false);
        });
    }
    public static void Learn(string id, bool moment = true)
    {
        if (!owned || Has(id)) return;
        verses.Add(id);
        verse = id; held = true; verseShowT = 4;
        resonance = MathF.Max(resonance, Def(id).cost + 5);
        var v = Def(id);
        var L = Data.Lore.P("hollow").P("verses").P(id);
        if (moment) { Snd.Verse(id); G.Shake(2); Narr.Think("learn_" + id); Score.Sting("discover", 0.7f); }
        Hints.Did("verse_" + id);
        G.After(moment ? 1.6f : 0.05f, () => UI.Inscription(v.name, "“" + v.line + "”\n\n" + Glyphs.Plain(L.S("desc"), Glyphs.Current)));
    }
    public static void Hold(bool on)
    {
        if (!owned) return;
        if (on && Legend.merged) { if (!held) { Snd.Deny(); H.ToastMsg("The Hollow is grown into the Tine. It has no voice of its own now.", 2); } held = false; return; }
        if (on && !held) { held = true; Snd.Ui(); verseShowT = 2.5f; Hints.Did("hollow"); }
        else if (!on) held = false;
    }
    public static void NextVerse()
    {
        if (!owned || verses.Count == 0) return;
        int i = verses.IndexOf(verse);
        verse = verses[(i + 1) % verses.Count];
        verseShowT = 2.5f;
        Snd.Tine(Def(verse).id);
        Hints.Did("verse");
    }

    // ---- the gift ----------------------------------------------------------------------------------------
    // On the Long Pier, when the tide first turns, the sea heaves up over the boards and leaves the Hollow behind.
    static void UpdateGift(float dt)
    {
        var f = World.flags; var r = G.room; var p = G.player;
        if (r.Id != "pier") { onPier = 0; return; }
        onPier += dt;
        if (giftT < 0 && !f.B("hollowGift") && (Tide.PhaseId() == "rising" || onPier > 14))
        {
            f.Set("hollowGift", true); giftT = 0;
            Snd.SaintGroan(0); Snd.Rumble(); G.Shake(2.5f);
            Call(r, 2.4f, 1.0f);
        }
        if (giftT < 0) return;
        float before = giftT; giftT += dt;
        if (before < 2.8f && giftT >= 2.8f)
        {
            // the wave slides back down the boards, and leaves something on them
            float x = H.Clamp(p.Cx() + p.facing * 34, r.Px + 14 * TS, r.Px + 37 * TS);
            float y = r.Py + 17 * TS;
            f.Set("hollowX", x); f.Set("hollowY", y);
            SpawnRelic(r);
            Snd.Verse("calling");
        }
        if (before < 3.6f && giftT >= 3.6f) { Narr.Think("giftWave"); giftT = -1; }
    }
    static void SpawnRelic(RoomDef r)
    {
        var f = World.flags;
        if (owned || !f.Has("hollowX") || G.ents.Any(e => e is HollowRelic)) return;
        var s = Props.Empty(); s.Set("type", "hollow"); s.Set("x", f.F("hollowX")); s.Set("y", f.F("hollowY")); s.Set("key", "hollow_relic");
        G.ents.Add(new HollowRelic(s, r));
    }

    public static void OnRoomEnter(RoomDef r)
    {
        RevertPillars();
        surge = surgeTarget = surgeHold = 0; surgeRoom = null;
        wakes.Clear(); learnT = 0;
        if (r.Id == "pier") SpawnRelic(r);
    }

    // ---- resonance ------------------------------------------------------------------------------------
    // A knot breaks and the brine it held runs out; the Hollow drinks it in.
    public static void OnKnotBroken(Body b, string src)
    {
        if (!owned) return;
        var p = G.player;
        if (p == null || H.Dist(p.Cx(), p.Cy(), b.Cx(), b.Cy()) > 16 * TS) return;
        bool crown = b.knots.Any(k => k.type == "crown" && k.broken) && b.mode == "downed";
        // grown into the Tine it drinks deeper: the Saint wants what is in them
        float m = Legend.merged ? 1.75f : 1;
        Gain((crown ? CROWN_GAIN : KNOT_GAIN) * m);
    }
    public static void Gain(float n)
    {
        float was = resonance;
        resonance = MathF.Min(MAX, resonance + n);
        if (was < MAX && resonance >= MAX) { flash = 1; Snd.Tine("full"); }
    }

    // ---- singing ---------------------------------------------------------------------------------------
    // Called by the player every frame the Hollow is played: pressed this frame, still held down.
    public static void Play(Player p, bool down, bool pressed, float dt)
    {
        if (!owned || verses.Count == 0 || Legend.merged) return;
        if (pressed) { pressing = true; holdT = 0; turnedThisPress = false; }
        if (!pressing) return;
        bool brim = verse == "calling" && resonance >= MAX && CanCall(G.room);
        if (down)
        {
            holdT += dt;
            // Brimming, the Calling waits to see if the note is held.
            if (brim && !turnedThisPress && holdT >= 1.1f) { turnedThisPress = true; TurnTide(p); }
            if (!brim && pressed) Sing(p);
            return;
        }
        pressing = false;
        if (brim && !turnedThisPress) Sing(p);
    }

    public static bool Sing(Player p)
    {
        if (singCool > 0) return false;
        var v = Cur;
        if (p.inBellRoom) { Deny("The bell drowns the Hollow out. Nothing answers in here."); return false; }
        if (Legend.merged) { Deny("The Hollow is grown into the Tine. It has no voice of its own now."); return false; }
        if (Ringing(v.id) > 0) { Deny($"{v.name} is still ringing in the bone ({MathF.Ceiling(Ringing(v.id))} s)."); return false; }
        if (resonance < v.cost) { Deny(resonance < SEA_FILL ? "The Hollow is too quiet. Break their knots: it drinks the brine that held them." : "The Hollow is too quiet for that verse. Break more of their knots."); return false; }
        bool ok = verse switch
        {
            "calling" => SingCalling(p),
            "riptide" => SingRiptide(p),
            "salting" => SingSalting(p),
            "answer" => SingAnswer(p),
            _ => false,
        };
        if (!ok) return false;
        resonance -= v.cost;
        singCool = 0.6f; Sung++;
        ringing[v.id] = v.ring; stillT = STILL_T;
        // the sea hears it too
        G.AddSwell(1.2f);
        Snd.Verse(verse);
        Hints.Did("sing");
        G.Shake(1.5f); Pad.Rumble(0.3f, 0.2f, 0.25f);
        // the Hollow is loud: everything near hears it
        int asleep = G.ents.OfType<Body>().Count(b => b.mode == "dormant" && !b.corpse);
        Noise.Emit(p.Cx(), p.Cy(), 9, "hymn");
        if (G.ents.OfType<Body>().Count(b => b.mode == "dormant" && !b.corpse) < asleep) Narr.Think("singWake");
        Narr.Think("firstSing");
        return true;
    }
    static void Deny(string why) { Snd.Deny(); H.ToastMsg(why, 2.2f); singCool = 0.3f; }

    static Vector2 AimPoint(Player p)
    {
        if (Inp.padAim) return new Vector2(p.Cx() + MathF.Cos(p.aim) * 60, p.y + 9 + MathF.Sin(p.aim) * 60);
        return G.ScreenToWorld(Inp.mouse.x, Inp.mouse.y);
    }

    // ---- The Calling ----------------------------------------------------------------------------------
    // Only the tide answers: still pools, the brine shaft, the Choir's font and the deep water of the Gullet do not hear.
    public static bool CanCall(RoomDef r)
    {
        if (r == null || r.FixedWater != null || r.Pools != null || r.Id == "choir" || r.Deep) return false;
        float sea = Tide.SeaLevel() * TS;
        return sea - 6 * TS < r.Py + r.Ph;
    }
    static bool SingCalling(Player p)
    {
        var r = G.room;
        if (!CanCall(r)) { Deny(r.Deep || r.FixedWater != null || r.Pools != null || r.Id == "choir" ? "This water is still. It does not hear the Hollow." : "The sea is too far below to hear."); return false; }
        var at = AimPoint(p);
        float natural = Tide.SeaLevel() * TS;
        float want = (natural - at.Y) / TS;
        if (MathF.Abs(want) < 1.5f) want = MathF.Sin(p.aim) <= 0 ? 1.5f : -1.5f;
        Call(r, H.Clamp(want, -6, 6), 12);
        Hints.Did("calling");
        return true;
    }
    static void Call(RoomDef r, float tiles, float hold)
    {
        surgeRoom = r.Id; surgeTarget = tiles; surgeHold = hold;
        Snd.Surge(tiles > 0);
    }
    // How far the Calling has moved this room's sea, in pixels upward.
    public static float SurgePx(RoomDef r) => r != null && r.Id == surgeRoom ? surge * TS : 0;

    // Held long with the Hollow brimming: the Saint breathes early, and the tide turns.
    static void TurnTide(Player p)
    {
        if (Tide.paused) { Deny("Out here the tide will not turn for anyone."); return; }
        resonance = 0; rush = Tide.PhaseRemaining() + 0.05f; Sung++;
        Snd.Verse("turning"); Snd.Toll(); G.Shake(5); flash = 1;
        Noise.Emit(p.Cx(), p.Cy(), 30, "hymn");
        H.ToastMsg("You hold the note. Under the island, the Saint breathes.", 3.5f);
        Narr.Think("turnTide");
        Hints.Did("turning");
    }

    // ---- The Riptide ----------------------------------------------------------------------------------
    static bool SingRiptide(Player p)
    {
        var o = new Vector2(p.Cx(), p.y + 9); float a = p.aim;
        var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
        const float range = 130, cone = 0.42f;
        foreach (var e0 in G.ents.ToArray())
        {
            if (e0 is not Body b || b.gone || b.boss || b.type is "undertow" or "driftling") continue;
            var to = new Vector2(b.Cx(), b.Cy()) - o; float d = to.Length();
            if (d > range || (d > 12 && MathF.Abs(H.AngleDiff(a, MathF.Atan2(to.Y, to.X))) > cone) || !Phys.Los(o.X, o.Y, b.Cx(), b.Cy())) continue;
            float k = 1 - d / (range * 1.4f);
            if (p.grabbedBy == b) b.ReleaseGrab();
            b.vx = dir.X * 330 * k; b.vy = dir.Y * 210 * k - 150;
            b.flungT = 0.55f; b.stun = MathF.Max(b.stun, 0.9f);
            if (b.mode == "dormant") b.Wake(true);
            Voices.Hurt(b, false);
        }
        foreach (var d in G.ents.OfType<Driftling>()) if (H.Dist(o.X, o.Y, d.Cx(), d.Cy()) < range) { d.vx = dir.X * 300; d.vy = dir.Y * 300; }
        // it puts out what burns and drowns what glows
        foreach (var kv in Fire.cells.ToArray())
        {
            var c = new Vector2(kv.Key.Item1 * TS + 8, kv.Key.Item2 * TS + 8) - o;
            if (c.Length() < range && MathF.Abs(H.AngleDiff(a, MathF.Atan2(c.Y, c.X))) < cone) { Fire.cells.Remove(kv.Key); FX.Spawn(o.X + c.X, o.Y + c.Y, 4, new FXOpt { color = "#8a8a8a", grav = -40, lmin = 0.6f, lmax = 1.2f }); }
        }
        foreach (var pr in G.projectiles) if (pr.kind == "flare" && H.Dist(o.X, o.Y, pr.x, pr.y) < range) pr.dead = true;
        FX.Spawn(o.X + dir.X * 6, o.Y + dir.Y * 6, 46, new FXOpt { color = "#a8d0dc", ang = a, spread = cone * 0.7f, smin = 160, smax = 360, grav = 380, lmin = 0.25f, lmax = 0.6f, size = 1.6f });
        FX.Spawn(o.X + dir.X * 6, o.Y + dir.Y * 6, 14, new FXOpt { color = "#e4f4f8", ang = a, spread = cone * 0.5f, smin = 220, smax = 420, grav = 200, lmin = 0.15f, lmax = 0.35f, size = 1.1f, glow = true });
        wakes.Add(new Wake { a = o, b = o + dir * Phys.Ray(o.X, o.Y, a, range), t = 0.45f, max = 0.45f, kind = "riptide" });
        // under water, the sea carries her where she points; on land it shoves her back
        if (p.InWater()) { p.vx = dir.X * 430; p.vy = dir.Y * 430; p.invulnT = MathF.Max(p.invulnT, 0.25f); FX.Bubbles(p.Cx(), p.Cy(), 10); }
        else p.vx -= dir.X * 90;
        if (p.grabbedBy != null) { p.grabbedBy = null; p.state = "normal"; p.invulnT = 0.6f; }
        Noise.Emit(o.X + dir.X * 60, o.Y + dir.Y * 60, 6, "splash");
        Hints.Did("riptide");
        return true;
    }

    // ---- The Salting ----------------------------------------------------------------------------------
    static bool SingSalting(Player p)
    {
        var o = new Vector2(p.Cx(), p.y + 9); float a = p.aim;
        var hit = Combat.Hitscan(o.X, o.Y, a, 200);
        wakes.Add(new Wake { a = o, b = new Vector2(hit.x, hit.y), t = 0.3f, max = 0.3f, kind = "salt" });
        if ((hit.kind == "knot" || hit.kind == "body") && hit.target != null)
        {
            var b = hit.target;
            if (b.boss || b.type is "saltwright" or "undertow") { FX.Salt(hit.x, hit.y, 10); H.ToastMsg("The salt runs off it. It is salt already.", 2); return true; }
            Encrust(b);
            return true;
        }
        if (hit.kind == "wall" && RaisePillar(hit.x, hit.y, a)) return true;
        FX.Salt(hit.x, hit.y, 12);
        return true;
    }
    public static void Encrust(Body b)
    {
        // long enough for one blow, not for a walk round it
        b.saltT = 3.5f; b.stun = MathF.Max(b.stun, 3.5f); b.vx = 0;
        if (b.mode == "grab") b.ReleaseGrab();
        FX.Salt(b.Cx(), b.Cy(), 30);
        Snd.Crystal(b.Pan());
        Hints.Did("salting");
    }
    // Salt grows out of the stone that was struck: up from a floor, out from a wall, down from a ceiling.
    static bool RaisePillar(float hx, float hy, float a)
    {
        var r = G.room;
        if (r.BellRoom) return false;
        int tx = H.FloorI((hx - MathF.Cos(a) * 3) / TS), ty = H.FloorI((hy - MathF.Sin(a) * 3) / TS);
        var cells = new List<(int, int)>();
        if (World.Solid(tx, ty + 1) == 1) for (int i = 0; i < 3; i++) cells.Add((tx, ty - i));
        else if (World.Solid(tx, ty - 1) == 1) for (int i = 0; i < 2; i++) cells.Add((tx, ty + i));
        else
        {
            int side = World.Solid(tx + 1, ty) == 1 ? 1 : World.Solid(tx - 1, ty) == 1 ? -1 : 0;
            if (side == 0) return false;
            cells.Add((tx, ty)); cells.Add((tx - side, ty));
        }
        var p = G.player; int made = 0;
        foreach (var (cx, cy) in cells)
        {
            if (World.CharAt(cx, cy) != ' ' || World.RoomAtTile(cx, cy) != r) break;
            float px = cx * TS, py = cy * TS;
            if (H.RectsOverlap(px, py, TS, TS, p.x, p.y, p.w, p.h)) break;
            if (G.ents.Any(e => !e.gone && (e is Body || e is Obj) && H.RectsOverlap(px, py, TS, TS, e.x, e.y, e.w, e.h))) break;
            int i = (cy - r.Oy) * r.Tw + (cx - r.Ox);
            pillars.Add(new Pillar { room = r, tx = cx, ty = cy, was = r.Live[i], t = 15 + made * 0.2f });
            r.Live[i] = 'S';
            FX.Salt(px + 8, py + 8, 10);
            made++;
        }
        if (made == 0) return false;
        World.MarkDirty();
        Snd.Crystal(0);
        Hints.Did("pillar");
        return true;
    }
    static void UpdatePillars(float dt)
    {
        bool changed = false;
        for (int i = pillars.Count - 1; i >= 0; i--)
        {
            var pl = pillars[i];
            pl.t -= dt;
            if (pl.t > 0) continue;
            int k = (pl.ty - pl.room.Oy) * pl.room.Tw + (pl.tx - pl.room.Ox);
            if (pl.room.Live[k] == 'S') { pl.room.Live[k] = pl.was; FX.Salt(pl.tx * TS + 8, pl.ty * TS + 8, 8); changed = true; }
            pillars.RemoveAt(i);
        }
        if (changed) { World.MarkDirty(); Snd.Crack(); }
    }
    static void RevertPillars()
    {
        foreach (var pl in pillars) { int k = (pl.ty - pl.room.Oy) * pl.room.Tw + (pl.tx - pl.room.Ox); if (pl.room.Live[k] == 'S') pl.room.Live[k] = pl.was; }
        if (pillars.Count > 0) World.MarkDirty();
        pillars.Clear();
    }
    public static int Pillars => pillars.Count;   // tests

    // ---- The Answer -----------------------------------------------------------------------------------
    static bool SingAnswer(Player p)
    {
        var o = new Vector2(p.Cx(), p.y + 9); float a = p.aim;
        Body best = null; float bd = 1e9f;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body b || b.gone || !b.Humanoid || b.boss || b.corpse || b.knots.Count == 0 || b.thrallT > 0 || b.breach || b.hesper) continue;
            var to = new Vector2(b.Cx(), b.Cy()) - o; float d = to.Length();
            float diff = MathF.Abs(H.AngleDiff(a, MathF.Atan2(to.Y, to.X)));
            if (d > 180 || diff > 0.45f || !Phys.Los(o.X, o.Y, b.Cx(), b.Cy())) continue;
            float score = d * (1 + diff * 2);
            if (score < bd) { bd = score; best = b; }
        }
        if (best == null)
        {
            var corpse = G.ents.OfType<Corpse>().FirstOrDefault(c => !c.gone && H.Dist(o.X, o.Y, c.Cx(), c.Cy()) < 180);
            Deny(corpse != null ? "There is nothing left in that one to answer." : "No bone near enough to hear it.");
            return false;
        }
        if (best is Grafted g && g.passive) { Deny("The Choir sings back, louder. They will not answer you."); return false; }
        if (best.mode == "downed") best.StandUp();
        if (best.mode == "dormant") { best.mode = "hunt"; best.SetPose("stand"); }
        best.ReleaseGrab();
        best.thrallT = 10; best.stun = 0; best.mode = "hunt"; best.spotted = false;
        FX.Salt(best.Cx(), best.Cy(), 16);
        wakes.Add(new Wake { a = o, b = new Vector2(best.Cx(), best.y + 4), t = 0.5f, max = 0.5f, kind = "answer" });
        Hints.Did("answer");
        return true;
    }
    // While the verse lasts the body hunts its own kind, and sings while it does.
    public static void ThrallThink(Body b, float dt)
    {
        b.cool -= dt;
        Body target = null; float bd = 20 * TS;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e == b || !e.Alive() || e.corpse || e.thrallT > 0 || e.type is "driftling" or "undertow" || e.passive) continue;
            float d = H.Dist(b.Cx(), b.Cy(), e.Cx(), e.Cy());
            if (d < bd && (d < 3 * TS || Phys.Los(b.Cx(), b.y + 4, e.Cx(), e.Cy()))) { bd = d; target = e; }
        }
        var p = G.player;
        if (target == null)
        {
            // nothing to fight: it waits by her, swaying
            if (p != null && MathF.Abs(p.Cx() - b.Cx()) > 40) b.Pursue(dt, p.Cx(), 50); else b.vx *= 0.8f;
            return;
        }
        b.Pursue(dt, target.Cx(), b.pose == "crawl" ? 70 : 60);
        if (bd < 24 && b.cool <= 0)
        {
            b.cool = 0.9f;
            // it tears at one limb until it comes away, then the next
            var ks = target.knots.Where(k => !k.broken && !k.plated && !target.IsSealed(k)).OrderBy(k => k.hp).ToList();
            if (ks.Count > 0) target.DamageKnot(ks[0], 1.5f, "thrall");
            else target.DamageFlesh(1, target.Cx(), target.Cy(), "thrall");
            target.stun = MathF.Max(target.stun, 0.4f);
            int s = (int)H.Sign(target.Cx() - b.Cx()); if (s != 0) b.facing = s;
            Voices.Windup(b);
        }
    }
    // The verse runs out, and so does it: the knots let go all at once and it falls apart into salt and bone.
    public static void ThrallEnds(Body b)
    {
        foreach (var k in b.knots.Where(k => !k.broken && k.type != "crown").ToList()) { k.broken = true; Gore.OnKnotBroken(b, k, "answer"); }
        var crown = b.knots.FirstOrDefault(k => k.type == "crown");
        if (crown != null) crown.broken = true;
        b.mode = "downed"; b.SetPose("lying");
        Voices.Die(b);
        FX.Salt(b.Cx(), b.Cy(), 40);
        b.gone = true; b.mode = "gone";
    }

    // ---- every frame ------------------------------------------------------------------------------------
    public static void Update(float dt)
    {
        var p = G.player; var r = G.room;
        if (p == null || r == null) return;
        UpdateGift(dt);
        singCool -= dt; flash = MathF.Max(0, flash - dt * 1.5f); verseShowT -= dt; stillT -= dt;
        foreach (var k in ringing.Keys.ToArray()) { ringing[k] -= dt; if (ringing[k] <= 0) { ringing.Remove(k); if (owned && held && k == verse) Snd.Tine(k); } }
        // the sea it called rises toward the mark, holds, and settles back
        if (surgeRoom != null)
        {
            if (surgeRoom != r.Id) { surge = surgeTarget = 0; surgeRoom = null; }
            else
            {
                if (surgeHold > 0) surgeHold -= dt; else surgeTarget = 0;
                float before = surge;
                surge = H.Approach(surge, surgeTarget, dt * (surgeHold > 0 ? 3.2f : 1.3f));
                if (MathF.Abs(surge - before) > 0.0001f && H.Rand() < dt * 30)
                {
                    float x = G.cam.X + H.Rand() * H.VIEW_W, s = World.RoomSurface(r, x);
                    if (float.IsFinite(s)) FX.Splash(x, s, 3);
                }
                // the water it calls is the same water: it stands the dead up at High like any tide
                if (surge > 0.2f)
                    foreach (var e0 in G.ents)
                        if (e0 is Body b && b.mode == "downed" && !b.gone && !b.dragged && b.knots.Count > 0 && !b.reknot && World.IsWater(b.Cx(), b.Feet() - 3))
                        { b.reknot = true; Narr.Think("calledWater"); }
                if (surge == 0 && surgeTarget == 0) surgeRoom = null;
            }
        }
        // the Saint, made to breathe, rushes the tide through to its next turn
        if (rush > 0)
        {
            float step = MathF.Min(rush, dt * 24);
            Tide.Update(step); rush -= step;
            if (H.Rand() < dt * 8) G.Shake(1);
        }
        UpdatePillars(dt);
        foreach (var w in wakes) w.t -= dt;
        wakes.RemoveAll(w => w.t <= 0);
        if (!owned) return;
        // it drinks the sea it is held in, slowly, and only halfway; grown into the Tine, the sea fills it to the brim
        float fill = Legend.merged ? MAX : SEA_FILL;
        if (p.InWater() && !r.Brine && stillT <= 0 && resonance < fill) Gain(MathF.Min(fill - resonance, dt * SEA_RATE * (Legend.merged ? 1.6f : 1)));
        // and it hears what the island sings to it
        learnT += dt;
        if (learnT > 2.5f && !UI.IsOpen())
        {
            if (!Has("riptide") && World.flags.B("glimpse") && G.glimpseT <= 0) Learn("riptide");
            else if (!Has("salting") && r.Region == "saltworks") Learn("salting");
            else if (!Has("answer") && r.Id == "cloister") Learn("answer");
        }
    }

    // ---- drawing ------------------------------------------------------------------------------------------
    // Over the bodies: salt crusts, the sea-green eyes of the ones that answered, and the verses' wakes.
    public static void DrawWorld(Canvas c)
    {
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body b || b.gone) continue;
            if (b.saltT > 0)
            {
                var j = b.Skel();
                float a = MathF.Min(1, b.saltT / 0.5f);
                c.Fill = new Color(Gore.SALT.R, Gore.SALT.G, Gore.SALT.B, 0.72f * a);
                foreach (var kv in j)
                {
                    float hsh = TileArt.Hash((int)(kv.Value.X * 3), (int)(kv.Value.Y * 3));
                    c.Save(); c.Translate(kv.Value.X, kv.Value.Y); c.Rotate(hsh * 3);
                    c.Poly(new[] { new Vector2(-2.4f, 0), new Vector2(0, -2.8f - hsh), new Vector2(2.2f, 0.4f), new Vector2(0.3f, 2.4f) });
                    c.Restore();
                }
                c.Fill = new Color(1, 1, 1, 0.8f * a);
                foreach (var kv in j) c.FillRect(kv.Value.X - 0.4f, kv.Value.Y - 1.4f, 0.8f, 0.8f);
            }
            if (b.thrallT > 0 && b.Skel().TryGetValue("head", out var hd))
            {
                c.Fill = new Color(0.55f, 1f, 0.85f, 0.95f); c.FillCircle(hd.X + b.facing * 1.6f, hd.Y - 0.4f, 0.55f);
                c.Glow(b.Cx(), b.Cy(), 16, new Color(0.3f, 0.8f, 0.7f, 0.14f + 0.06f * MathF.Sin(G.Now * 4)));
            }
        }
        foreach (var w in wakes)
        {
            float k = w.t / w.max;
            switch (w.kind)
            {
                case "salt":
                    c.Stroke = new Color(0.92f, 0.96f, 1f, 0.8f * k); c.LineWidth = 1.2f; c.Line(w.a.X, w.a.Y, w.b.X, w.b.Y);
                    for (int i = 1; i < 8; i++) { var q = w.a.Lerp(w.b, i / 8f); c.Fill = new Color(1, 1, 1, 0.7f * k); c.FillRect(q.X - 0.6f, q.Y - 0.6f, 1.2f, 1.2f); }
                    break;
                case "answer":
                    c.Stroke = new Color(0.5f, 0.95f, 0.85f, 0.5f * k); c.LineWidth = 0.8f;
                    for (int i = 0; i < 3; i++) { var q = w.a.Lerp(w.b, (1 - k + i / 3f) % 1); c.BeginPath(); c.Arc(q.X, q.Y, 2 + i * 1.5f, 0, H.PI * 2); c.StrokePath(); }
                    break;
                case "riptide":
                {
                    // a rush of sea water driving out along the cone, streaked, with a foaming crest at its front
                    var d = (w.b - w.a).Normalized(); var n = new Vector2(-d.Y, d.X);
                    float reach = (1 - k * k) * (w.b - w.a).Length();
                    var front = w.a + d * reach;
                    float spread = 6 + reach * 0.42f;
                    c.FillPolyGrad(new[] { w.a + n * 3, front + n * spread, front - n * spread, w.a - n * 3 },
                        new[] { new Color(0.55f, 0.78f, 0.85f, 0.35f * k), new Color(0.6f, 0.85f, 0.9f, 0.12f * k), new Color(0.6f, 0.85f, 0.9f, 0.12f * k), new Color(0.55f, 0.78f, 0.85f, 0.35f * k) });
                    c.Stroke = new Color(0.85f, 0.95f, 1f, 0.55f * k); c.LineWidth = 0.8f;
                    for (int i = 0; i < 9; i++)
                    {
                        float off = (i / 8f - 0.5f) * 2, len = reach * (0.45f + TileArt.Hash(i, 41) * 0.5f);
                        var s0 = w.a + d * (reach - len) * 0.6f + n * off * spread * 0.5f;
                        var s1 = w.a + d * reach * (0.75f + TileArt.Hash(i, 43) * 0.25f) + n * off * spread * 0.9f;
                        c.Line(s0.X, s0.Y, s1.X, s1.Y);
                    }
                    c.Fill = new Color(0.92f, 0.98f, 1f, 0.7f * k);
                    for (int i = 0; i < 12; i++) { var q = front + n * ((i / 11f - 0.5f) * 2 * spread) - d * TileArt.Hash(i, 47) * 6; c.FillCircle(q.X, q.Y, 0.8f + TileArt.Hash(i, 49) * 1.1f); }
                    break;
                }
            }
        }
    }

    // The relic itself, as an icon: an ivory box with the curled bone glowing inside.
    public static void DrawIcon(Canvas c, float x, float y, float s, float glow)
    {
        float k = s / 12;
        c.Fill = H.Col("#2c2418"); c.FillRect(x - 5.4f * k, y - 2.6f * k, 10.8f * k, 6.4f * k);
        c.FillRectGradV(x - 5 * k, y - 2.2f * k, 10 * k, 5.6f * k, H.Col("#efe6cf"), H.Col("#b9ac8c"));
        // the lid, tipped open
        c.Fill = H.Col("#d8ccae"); c.Poly(new[] { new Vector2(x - 5.4f * k, y - 2.6f * k), new Vector2(x + 5.4f * k, y - 2.6f * k), new Vector2(x + 4.2f * k, y - 5.6f * k), new Vector2(x - 4.6f * k, y - 5.2f * k) });
        c.Stroke = H.Col("#8a7a54"); c.LineWidth = 0.5f * k; c.Line(x - 3.6f * k, y - 4.2f * k, x + 3.4f * k, y - 4.4f * k);
        // the bone inside, curled like a shell
        c.Fill = H.Col("#f7f1e2"); c.BeginPath(); c.Ellipse(x, y + 0.6f * k, 3.2f * k, 2 * k, 0, 0, H.PI * 2); c.FillPath();
        c.Stroke = H.Col("#9a8a64"); c.LineWidth = 0.45f * k; c.BeginPath(); c.Arc(x + 0.2f * k, y + 0.6f * k, 1.5f * k, 0.3f, H.PI * 1.7f); c.StrokePath();
        if (glow > 0) c.Glow(x, y, 9 * k, new Color(0.45f, 0.95f, 0.85f, 0.35f * glow));
    }

    // Under the lantern gauge: the Hollow's energy, sea rising in a glass. A mark shows how far the sea alone will
    // fill it; the share the verse in hand would spend is lit at the top of the water (or, too quiet, the reach of it
    // is drawn empty); while the bone still rings the water trembles. Grown into the Tine the glass goes dark as
    // brine, the marks are its throws, and under it a thin vein shows how far the sea has grown into her.
    public static void DrawHud(Canvas c, float y)
    {
        if (!owned) return;
        bool tine = Legend.merged;
        float f = resonance / MAX, pulse = 0.5f + 0.5f * MathF.Sin(G.Now * 3);
        float cost = tine ? Legend.TINE_COST : Cur.cost;
        bool can = resonance >= cost && (tine || Ringing(verse) <= 0);
        DrawIcon(c, 15, y, 11, f >= 1 ? 0.7f + 0.3f * pulse : f * 0.5f);
        float gx = 24, gw = 64, top = y - 2.3f, bh = 4.6f;
        var rim = tine ? H.Col("#3c4a40") : H.Col("#5a6a62");
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillRect(gx - 1, y - 3.2f, gw + 2, 6.4f);
        c.Fill = rim; c.FillRect(gx - 1, y - 3.2f, gw + 2, 0.7f); c.FillRect(gx - 1, y + 2.5f, gw + 2, 0.7f);
        c.FillRect(gx - 1.5f, y - 3.2f, 0.8f, 6.4f); c.FillRect(gx + gw + 0.7f, y - 3.2f, 0.8f, 6.4f);
        if (f > 0)
        {
            var hi = tine ? H.Col("#7fb8a0") : H.Col("#b8f2e0"); var lo = tine ? H.Col("#16372f") : H.Col("#2f8a7a");
            c.FillRectGradV(gx, top, gw * f, bh, hi, lo);
            // the bone still ringing: the water trembles and drinks nothing
            if (stillT > 0)
                for (int i = 0; i < 6; i++)
                {
                    float sx = gx + (G.Now * 40 + i * 11) % MathF.Max(1, gw * f);
                    c.Fill = new Color(1, 1, 1, 0.35f * MathF.Min(1, stillT)); c.FillRect(sx, top + 0.3f + MathF.Sin(G.Now * 20 + i) * 0.5f, 2.2f, 0.6f);
                }
            else { c.Fill = new Color(1, 1, 1, 0.3f); c.FillRect(gx, top + 0.2f, gw * f, 0.6f); }
            c.Glow(gx + gw * f, y, 5 + flash * 8, tine ? new Color(0.3f, 0.75f, 0.55f, 0.2f + flash * 0.4f) : new Color(0.5f, 1.1f, 0.95f, 0.25f + flash * 0.4f));
        }
        // what the verse (or the throw) in hand would spend
        if (can)
        {
            float x0 = gx + gw * (resonance - cost) / MAX;
            c.Fill = new Color(1, 1, 1, 0.22f + 0.12f * pulse); c.FillRect(x0, top, gw * cost / MAX, bh);
            c.Fill = new Color(0.9f, 1, 0.97f, 0.9f); c.FillRect(x0 - 0.4f, y - 3.6f, 0.8f, 7.2f);
        }
        else
        {
            c.Stroke = new Color(0.82f, 0.48f, 0.42f, 0.65f); c.LineWidth = 0.5f; c.StrokeRect(gx + 0.3f, top + 0.3f, gw * MathF.Min(1, cost / MAX) - 0.6f, bh - 0.6f);
        }
        // how far the sea alone fills it, or the Tine's throws
        if (!tine) { for (float dy = -2.6f; dy < 2.6f; dy += 1.4f) c.FillRect(gx + gw * SEA_FILL / MAX - 0.3f, y + dy, 0.6f, 0.7f, new Color(0.75f, 0.9f, 0.88f, 0.55f)); }
        else for (int i = 1; i < 4; i++) c.FillRect(gx + gw * i * Legend.TINE_COST / MAX - 0.25f, top, 0.5f, bh, new Color(0, 0, 0, 0.45f));
        if (tine || Legend.communion > 0) Legend.DrawVein(c, gx, y + 4.6f, gw);
        if (held || verseShowT > 0 || tine)
        {
            float a = held || tine ? 1 : MathF.Min(1, verseShowT);
            c.Font = "6px caps"; c.Fill = tine ? new Color(0.62f, 0.8f, 0.7f, 0.9f) : new Color(0.7f, 0.92f, 0.85f, 0.9f * a);
            string label = tine ? "the tine" : Cur.name.ToLowerInvariant();
            float rt = tine ? 0 : Ringing(verse);
            if (rt > 0) { c.Fill = new Color(0.55f, 0.6f, 0.58f, 0.8f * a); label += "  " + MathF.Ceiling(rt); }
            c.FillText(label, gx + gw + 6, y + 2);
        }
    }

    // The panel bottom right while the Hollow is in hand: what it is, the verse, and the verses known.
    public static void DrawPanel(Canvas c, float x, float y, float pw, float ph)
    {
        DrawIcon(c, x + 17, y + 18, 17, resonance >= Cur.cost ? 0.6f : 0.15f);
        c.Font = "8px caps"; c.Fill = H.Col("#eadfc4"); c.FillText(Data.Lore.P("hollow").S("name"), x + 33, y + 13);
        c.Font = "7px serif"; c.Fill = H.Col("#9fd8c8"); c.FillText(Cur.name, x + 33, y + 23);
        c.Font = "6px serif"; c.Fill = H.Col("#a89c80"); c.FillText("“" + Cur.line + "”", x + 33, y + 31);
        for (int i = 0; i < verses.Count; i++)
        {
            bool sel = verses[i] == verse;
            c.Save(); c.Translate(x + pw - 9 - (verses.Count - 1 - i) * 7, y + 10); c.Rotate(H.PI / 4);
            c.Fill = sel ? H.Col("#9fe8d4") : new Color(0.3f, 0.38f, 0.35f, 0.9f); c.FillRect(-1.8f, -1.8f, 3.6f, 3.6f); c.Restore();
        }
        float rt = Ringing(verse);
        c.Font = "7px sans"; c.Fill = rt > 0 ? H.Col("#8a948e") : resonance >= Cur.cost ? H.Col("#cfeee4") : H.Col("#d07a6a"); c.TextAlign = 2;
        c.FillText(rt > 0 ? "ringing " + MathF.Ceiling(rt) + " s" : (int)Cur.cost + " / " + (int)resonance, x + pw - 7, y + 40);
        c.TextAlign = 0;
    }
}

// The Hollow lying on the Long Pier's boards where the sea left it, humming, faintly green.
public class HollowRelic : Obj
{
    float hum;
    public HollowRelic(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 8; x = spec.F("x") - 6; y = spec.F("y") - 8; }
    public override string Prompt() => done || Hollow.owned ? null : "{interact} Take it up";
    public override bool Near(Player p) => NearR(p, 18);
    public override void Interact() { if (done || Hollow.owned) return; done = true; gone = true; Hollow.Take(); }
    public override void Update(float dt)
    {
        base.Update(dt);
        hum -= dt;
        if (hum <= 0) { hum = 3.2f; Snd.At("hollow:hum", 2.6f, s => { s.Voice(2.4f, 0.6f, 146.8f, 146.8f, (300, 870, 2240), (570, 840, 2410), 0.2f, 0, 0, 0.02f, 0, 0.6f); s.Bell(587.3f, 0.08f, 2); }, Cx(), Cy(), 0.35f, 1); }
    }
    public override LightSrc Light() => done ? null : new LightSrc { x = Cx(), y = Cy(), r = 46 + MathF.Sin(t * 2) * 4, i = 0.7f, color = "120,240,210" };
    public override void Draw(Canvas c)
    {
        if (done) return;
        Hollow.DrawIcon(c, Cx(), y + 4, 11, 0.6f + 0.25f * MathF.Sin(t * 2.2f));
        // water still running off it
        float ph = t * 0.8f % 1;
        c.Fill = new Color(0.6f, 0.8f, 0.85f, 0.6f * (1 - ph)); c.FillRect(Cx() + 3, y + 6 + ph * 3, 0.6f, 1);
    }
}
