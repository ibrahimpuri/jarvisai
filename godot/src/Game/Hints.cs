// Hints in the manner of Rain World: nothing is explained up front. A faint glyph and a word or two
// rise over Maren only when something is relevant and the player has stalled for a while, and a
// hint is retired for good the first time its action is done, whether or not it was ever shown.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Hints
{
    // id, the action that retires it, seconds of hesitation before it shows, when it is relevant
    record Def(string id, string doneBy, float delay, Func<bool> relevant);
    static Player P => G.player;
    static readonly HashSet<string> zones = new();         // zones Maren stands in this frame
    static readonly Dictionary<string, float> waited = new();
    static string shown; static float alpha, shownFor;
    static float walked, sneakT, focusT, sinceTurn = -1, sinceWeapon = -1, sinceBoots = -1, sinceJet = -1;
    static int weaponsSeen = 1;
    static bool hadBoots, hadJet;
    public static bool Enabled => Settings.Hints;
    public static string Shown => alpha > 0.05f ? shown : null;   // tests

    static IEnumerable<Body> Bodies(float r) => G.ents.OfType<Body>().Where(b => !b.gone && H.Dist(b.Cx(), b.Cy(), P.Cx(), P.Cy()) < r);
    static bool Hostile(Body b) => b.Alive() && !b.corpse && !b.passive && b.mode != "dormant" && b.knots.Any(k => !k.broken);
    static bool RingAbove()
    {
        int tx = H.FloorI(P.Cx() / H.TILE), ty = H.FloorI(P.y / H.TILE);
        for (int dy = 0; dy <= 5; dy++) for (int dx = -4; dx <= 4; dx++) if (World.CharAt(tx + dx, ty - dy) == 'o') return true;
        return false;
    }
    static bool CrownBare() => Bodies(40).Any(b => b.Alive() && b.knots.Any(k => k.type == "crown" && !k.broken && !b.IsSealed(k)));
    static WeaponState Cur => P.weapons[P.cur];
    static string Ammo => Data.Weapons.P(P.cur).S("ammo");

    static readonly Def[] DEFS =
    {
        new("move", "move", 5, () => true),
        new("jump", "jump", 2.5f, () => zones.Contains("jump")),
        new("swim", "swimout", 3, () => P.InWater()),
        new("ladder", "climb", 2.5f, () => P.state != "ladder" && P.onGround && (P.OnLadderTile() || P.LadderBelow())),
        new("walljump", "walljump", 5, () => zones.Contains("walljump")),
        new("drop", "drop", 3, () => zones.Contains("drop") && P.OnOneWay()),
        new("shoot", "barrel", 7, () => zones.Contains("shoot") && G.ents.OfType<Barrel>().Any(b => !b.done)),
        new("dash", "dash", 3.5f, () => zones.Contains("dash")),
        new("gaff", "gaffknot", 1.5f, CrownBare),
        new("fight", "knot", 3, () => Bodies(220).Any(Hostile)),
        new("reload", "reload", 1.5f, () => Cur.mag <= 0 && P.bag.Count(Ammo) > 0 && P.reloadT <= 0),
        new("craft", "craft", 2, () => P.cur == "driver" && Cur.mag <= 0 && P.bag.Count("rivets") <= 0 && P.bag.Count("scrap") > 0),
        new("heal", "poultice", 4, () => P.hp < P.maxHp && P.bag.Count("poultice") > 0 && P.healT <= 0 && !Bodies(240).Any(Hostile)),
        new("swing", "swing", 3, () => P.Has("swing") && P.state != "swing" && RingAbove()),
        new("focus", "focus", 4, () => P.LanternLit() && Bodies(150).Any(b => b.corpse || b.sailor)),
        new("shutter", "shutter", 2, () => !P.shut && Bodies(200).Any(b => b.type == "bellmouth" && b.Alive())),
        new("sneak", "sneak", 2, () => !Inp.Held("sneak") && MathF.Abs(P.vx) > 100 && Bodies(130).Any(b => b.Alive() && b.mode == "dormant")),
        new("boots", "boots", 0, () => sinceBoots >= 0 && sinceBoots < 10),
        new("jet", "jet", 0, () => sinceJet >= 0 && sinceJet < 10 && (P.InWater() || !P.onGround)),
        new("weapon", "weapon", 0, () => sinceWeapon >= 0 && sinceWeapon < 8),
        new("map", "map", 0, () => sinceTurn >= 0 && sinceTurn < 7),
        new("sing", "sing", 3, () => Hollow.owned && Hollow.held && Hollow.resonance >= Hollow.Cur.cost && !P.inBellRoom && !UI.IsOpen()),
        new("hollow", "hollow", 6, () => Hollow.owned && !Hollow.held && Hollow.resonance >= Hollow.MAX * 0.6f && Bodies(220).Any(Hostile)),
        new("verse", "verse", 6, () => Hollow.held && Hollow.verses.Count > 1),
    };

    static HashSet<string> Done => G.narr.hints;
    static HashSet<string> DidSet => G.narr.did;

    public static void Reset()
    {
        zones.Clear(); waited.Clear(); shown = null; alpha = 0; shownFor = 0;
        walked = sneakT = focusT = 0; sinceTurn = sinceWeapon = sinceBoots = sinceJet = -1;
        weaponsSeen = G.player?.weaponOrder.Count ?? 1;
        hadBoots = G.player?.Has("boots") ?? false; hadJet = G.player?.Has("jet") ?? false;
    }

    // Something Maren did; retires every hint that waits on it.
    public static void Did(string action)
    {
        if (G.narr == null) return;
        DidSet.Add(action);
        foreach (var d in DEFS) if (d.doneBy == action) Done.Add(d.id);
    }
    public static bool HasDone(string action) => G.narr != null && DidSet.Contains(action);
    public static void InZone(string id) => zones.Add(id);
    // Cut by salt crystals: the dash hint stops waiting.
    public static void Cut() { if (!Done.Contains("dash")) waited["dash"] = 99; }
    public static void TideTurned() { if (!Done.Contains("map")) sinceTurn = 0; }

    public static void Update(float dt)
    {
        var p = P;
        if (p == null || p.dead || G.narr == null) { zones.Clear(); return; }
        // things that are easier to notice here than at their source
        if (p.onGround && MathF.Abs(p.vx) > 20) { walked += MathF.Abs(p.vx) * dt; if (walked > 40) Did("move"); }
        if (Inp.Held("sneak") && MathF.Abs(p.vx) > 20) { sneakT += dt; if (sneakT > 1) Did("sneak"); }
        if (p.focusing) { focusT += dt; if (focusT > 0.5f) Did("focus"); }
        if (p.weaponOrder.Count > weaponsSeen) { weaponsSeen = p.weaponOrder.Count; sinceWeapon = 0; }
        if (!hadBoots && p.Has("boots")) { hadBoots = true; sinceBoots = 0; }
        if (!hadJet && p.Has("jet")) { hadJet = true; sinceJet = 0; }
        if (sinceTurn >= 0) sinceTurn += dt;
        if (sinceWeapon >= 0) sinceWeapon += dt;
        if (sinceBoots >= 0) sinceBoots += dt;
        if (sinceJet >= 0) sinceJet += dt;

        string want = null;
        if (Enabled && !UI.IsOpen())
        {
            foreach (var d in DEFS)
            {
                if (Done.Contains(d.id)) { waited.Remove(d.id); continue; }
                bool rel;
                try { rel = d.relevant(); } catch { rel = false; }
                float w = waited.TryGetValue(d.id, out var v) ? v : 0;
                w = rel ? w + dt : MathF.Max(0, w - dt * 2);
                waited[d.id] = w;
                if (rel && w >= d.delay && want == null) want = d.id;
            }
        }
        zones.Clear();
        // fade between hints; a hint that stops being relevant fades away
        if (want != shown)
        {
            alpha -= dt * 2.5f;
            if (alpha <= 0) { alpha = 0; shown = want; shownFor = 0; }
        }
        else if (shown != null) { alpha = MathF.Min(1, alpha + dt * 1.6f); shownFor += dt; }
    }

    public static void Draw(Canvas c)
    {
        if (shown == null || alpha <= 0.01f) return;
        string text = Data.Lore.P("hints")?.S(shown);
        if (text == null) return;
        var p = P;
        float x = p.Cx() - G.cam.X, y = p.y - G.cam.Y - 13 - MathF.Sin(G.Now * 2) * 1.2f;
        c.Font = "italic 7px serif";
        float w = Glyphs.Measure(c, text);
        x = Math.Clamp(x, w / 2 + 6, H.VIEW_W - w / 2 - 6);
        if (y < 70) y = p.y + p.h - G.cam.Y + 16;   // no room above her head: show it at her feet
        c.Glow(x, y - 3, w * 0.75f + 10, 13, new Color(0, 0, 0, 0.55f * alpha));
        c.TextAlign = 1;
        c.Fill = new Color(0.92f, 0.88f, 0.78f, 0.9f * alpha);
        Glyphs.DrawText(c, text, x, y);
        c.TextAlign = 0;
    }
}
