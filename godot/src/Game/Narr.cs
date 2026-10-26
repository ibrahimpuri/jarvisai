// Text-only storytelling: Maren's thoughts, whispers of the drowned, carvings, dreams, fates.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Hollowmere;

public class NarrState
{
    public HashSet<string> thoughts = new(), regions = new(), read = new(), heard = new();
    public int dreamIndex;
    public Dictionary<string, int> lore = new();
    public JsonObject ToJson()
    {
        JsonArray A(IEnumerable<string> s) => new(s.Select(v => (JsonNode)v).ToArray());
        return new JsonObject
        {
            ["thoughts"] = A(thoughts), ["regions"] = A(regions), ["read"] = A(read), ["heard"] = A(heard), ["dreamIndex"] = dreamIndex,
            ["lore"] = new JsonObject(lore.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value))),
        };
    }
    public static NarrState From(Props p)
    {
        var s = new NarrState();
        if (p == null) return s;
        s.thoughts = new HashSet<string>(p.Strings("thoughts")); s.regions = new HashSet<string>(p.Strings("regions"));
        s.read = new HashSet<string>(p.Strings("read")); s.heard = new HashSet<string>(p.Strings("heard"));
        s.dreamIndex = p.I("dreamIndex"); s.lore = p.Counts("lore");
        return s;
    }
}

public static class Narr
{
    static Props L => Data.Lore;
    static NarrState St => G.narr ??= new NarrState();

    // ---- thoughts: one quiet italic line, each shown once ----------------------------------
    class Timed { public string text; public float t, max, x, y; }
    static Timed thought, epigraph;
    public static void Think(string id)
    {
        var s = St;
        string text = L.P("thoughts")?.S(id);
        if (s.thoughts.Contains(id) || text == null) return;
        s.thoughts.Add(id);
        thought = new Timed { text = text, t = 5, max = 5 };
    }

    // ---- region epigraphs ----------------------------------------------------------------------
    public static bool EnterRegion(string region)
    {
        var s = St;
        if (s.regions.Contains(region)) return false;
        s.regions.Add(region);
        string ep = L.P("epigraphs")?.S(region);
        if (ep != null) epigraph = new Timed { text = ep, t = 6, max = 6 };
        return true;
    }

    // ---- whispers: hold the focused beam on a still body to hear it ----------------------------
    static readonly List<Timed> whispers = new();
    static Body focusBody;
    static float focusT;
    static readonly HashSet<Body> heardThisVisit = new();
    public static void OnRoomEnter() { heardThisVisit.Clear(); whispers.Clear(); focusBody = null; }
    static string WhisperFor(Body b)
    {
        var W = L.P("whispers");
        var named = W.P("named");
        if (b.key != null && named.Has(b.key)) return named.S(b.key);
        if (b.sailor && named.Has("sailor")) return named.S("sailor");
        if (b.teodor && named.Has("teodor")) return named.S("teodor");
        var pool = b.corpse ? W.Strings("corpse") : (W.Has(b.type) ? W.Strings(b.type) : W.Strings("shucker"));
        // Stable per body so the same corpse always says the same thing.
        int h = 0; foreach (char ch in b.key ?? "") h = unchecked(h * 31 + ch);
        return pool[Math.Abs(h) % pool.Count];
    }
    public static void UpdateWhispers(float dt)
    {
        var p = G.player;
        foreach (var w in whispers) { w.t -= dt; w.y -= dt * 4; }
        whispers.RemoveAll(w => w.t <= 0);
        if (p == null || !p.focusing) { focusBody = null; focusT = 0; return; }
        Body target = null;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e.gone || e.boss || e.type == "driftling" || e.type == "undertow") continue;
            bool still = e.corpse || e.mode == "dormant" || e.mode == "downed" || e.mode == "captured" || (e.type == "gullet" && e.mode == "hang");
            if (!still || heardThisVisit.Contains(e)) continue;
            if (H.Dist(p.Cx(), p.Cy(), e.Cx(), e.Cy()) > 200) continue;
            if (p.BeamHits(e.Cx(), e.Cy())) { target = e; break; }
        }
        if (target != focusBody) { focusBody = target; focusT = 0; }
        if (target == null) return;
        focusT += dt;
        if (focusT > 0.7f)
        {
            heardThisVisit.Add(target);
            St.heard.Add(target.key ?? "");
            whispers.Add(new Timed { x = target.Cx(), y = target.y - 6, text = WhisperFor(target), t = 5.5f, max = 5.5f });
            focusBody = null;
            G.Tip("whisper", "Hold the focused beam on the still and the drowned, and you can hear what they are thinking.", 5);
        }
    }

    // ---- carvings ---------------------------------------------------------------------------------
    // Place each carving on the nearest dry floor cell to its hint, clear of other entities.
    public static List<Props> CarvingSpecs(RoomDef room, HashSet<(int, int)> taken)
    {
        var outList = new List<Props>();
        if (L.P("carvings")?.A(room.Id) is not JsonArray list) return outList;
        for (int i = 0; i < list.Count; i++)
        {
            var c = new Props(list[i].AsObject());
            var spot = FindFloor(room, c.I("tx"), c.I("ty"), taken);
            if (spot == null) continue;
            taken.Add(spot.Value);
            var s = Props.Empty();
            s.Set("type", "carving"); s.Set("tx", spot.Value.Item1); s.Set("ty", spot.Value.Item2);
            s.Set("id", "carving_" + room.Id + "_" + i); s.Set("title", c.S("title")); s.Set("text", c.S("text"));
            outList.Add(s);
        }
        return outList;
    }
    static (int, int)? FindFloor(RoomDef room, int hx, int hy, HashSet<(int, int)> taken)
    {
        (int, int)? best = null; float bd = 1e9f;
        for (int dy = -6; dy <= 6; dy++) for (int dx = -8; dx <= 8; dx++)
            {
                int lx = hx + dx, ly = hy + dy;
                if (lx < 1 || ly < 1 || lx >= room.Tw - 1 || ly >= room.Th - 1) continue;
                int tx = room.Ox + lx, ty = room.Oy + ly;
                if (World.Solidity(World.CharAt(tx, ty)) != 0 || World.Solidity(World.CharAt(tx, ty - 1)) != 0) continue;
                if (World.IsLadder(tx, ty)) continue;
                if (World.Solidity(World.CharAt(tx, ty + 1)) != 1) continue;
                if (taken.Contains((lx, ly)) || taken.Contains((lx - 1, ly)) || taken.Contains((lx + 1, ly))) continue;
                // keep clear of the low-tide water line
                if (room.Row == 4 && room.FixedWater == null && (ty + 0.5f) > Tide.SEA_LOW) continue;
                float d = Math.Abs(dx) + Math.Abs(dy) * 1.5f;
                if (d < bd) { bd = d; best = (lx, ly); }
            }
        return best;
    }
    public static void MarkRead(string id) => St.read.Add(id);
    public static bool IsRead(string id) => St.read.Contains(id);

    // ---- dreams, deaths, lore, fates --------------------------------------------------------------
    public static string NextDream()
    {
        var s = St;
        var dreams = L.Strings("dreams");
        string d = dreams[s.dreamIndex % dreams.Count];
        s.dreamIndex++;
        return d;
    }
    public static (string title, string line) DeathText()
    {
        var D = L.P("death");
        return (H.Pick(D.Strings("titles")), H.Pick(D.Strings("lines")));
    }
    public static string NpcLore(string id)
    {
        var lines = L.P("npcLore")?.Strings(id);
        if (lines == null || lines.Count == 0) return null;
        var s = St;
        int i = s.lore.GetValueOrDefault(id);
        s.lore[id] = i + 1;
        return lines[i % lines.Count];
    }
    public static List<string> Fates(string kind)
    {
        var q = G.quests; var F = L.P("fates"); var flags = World.flags;
        var o = new List<string>();
        string tables = q.S("tables");
        if (kind == "relight") o.Add(tables == "sold" ? F.P("ansel").S("sold") : F.P("ansel").S("burned"));
        else if (kind == "hymn") o.Add(tables == "sold" ? F.P("ansel").S("sold") : F.P("ansel").S("sunk"));
        else o.Add(tables == "sold" ? F.P("ansel").S("sold") : F.P("ansel").S("alive"));
        o.Add(q.B("odileLost") ? F.P("odile").S("lost") : F.P("odile").S("alive"));
        string te = q.S("teodor");
        if (te == "saved") o.Add(F.P("teodor").S("saved"));
        else if (te == "killed" || te == "tags" || te == "dead") o.Add(F.P("teodor").S("dead"));
        if (q.B("hesperDead") || flags.B("choirDrained")) o.Add(F.P("hesper").S("dead"));
        else o.Add(kind == "relight" ? F.P("hesper").S("burned") : kind == "hymn" ? F.P("hesper").S("sunk") : F.P("hesper").S("alive"));
        o.Add(F.P("agathe").S(kind));
        o.Add(kind == "relight" && G.player.grafts.Count >= 2 ? F.P("maren").S("relightGrafted") : F.P("maren").S(kind));
        return o.Where(x => x != null).ToList();
    }

    // ---- drawing ------------------------------------------------------------------------------------
    public static List<string> Wrap(Canvas c, string text, float maxW)
    {
        var words = text.Split(' '); var lines = new List<string>(); string line = "";
        foreach (var w in words)
        {
            string t = line.Length > 0 ? line + " " + w : w;
            if (c.MeasureText(t) > maxW && line.Length > 0) { lines.Add(line); line = w; } else line = t;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }
    public static void DrawWorld(Canvas c)
    {
        c.Font = "italic 7px Georgia, serif";
        c.TextAlign = 1;
        foreach (var w in whispers)
        {
            float a = MathF.Min(1, MathF.Min((w.max - w.t) * 2, w.t / 1.5f));
            var lines = Wrap(c, w.text, 150);
            for (int i = 0; i < lines.Count; i++)
            {
                float ly = w.y - (lines.Count - 1 - i) * 9;
                c.Fill = new Godot.Color(0, 0, 0, 0.5f * a); c.FillText(lines[i], w.x + 0.6f, ly + 0.6f);
                c.Fill = H.Rgba(200, 225, 235, 0.85f * a); c.FillText(lines[i], w.x, ly);
            }
        }
        c.TextAlign = 0;
    }
    public static void DrawHUD(Canvas c, float dt)
    {
        if (thought == null) return;
        thought.t -= dt;
        float a = MathF.Min(1, MathF.Min((thought.max - thought.t) * 1.5f, thought.t));
        c.Font = "italic 9px Georgia, serif"; c.TextAlign = 1;
        c.Fill = new Godot.Color(0, 0, 0, 0.55f * a); c.FillText(thought.text, H.VIEW_W / 2 + 0.7f, H.VIEW_H - 63.3f);
        c.Fill = H.Rgba(236, 226, 200, 0.92f * a); c.FillText(thought.text, H.VIEW_W / 2, H.VIEW_H - 64);
        c.TextAlign = 0;
        if (thought.t <= 0) thought = null;
    }
    public static void DrawEpigraph(Canvas c, float dt, float y)
    {
        if (epigraph == null) return;
        epigraph.t -= dt;
        float a = MathF.Min(1, MathF.Min((epigraph.max - epigraph.t - 0.8f) * 1.2f, epigraph.t / 1.2f));
        if (a > 0)
        {
            c.Font = "italic 9px Georgia, serif"; c.TextAlign = 1;
            c.Fill = H.Rgba(200, 190, 165, a); c.FillText(epigraph.text, H.VIEW_W / 2, y);
            c.TextAlign = 0;
        }
        if (epigraph.t <= 0) epigraph = null;
    }
    public static bool EpigraphActive() => epigraph != null;
    public static string CurrentThought => thought?.text;
    public static void Reset() { thought = null; epigraph = null; whispers.Clear(); heardThisVisit.Clear(); }
}
