// Set dressing, generated from each room's tiles so every room is furnished without hand placement:
// architecture on the back walls, props on the floors, things hanging from ceilings, moss, kelp,
// barnacles, salt and the wet band the tide leaves on the stone, and the candles and torches that
// light it all. Static parts are baked with the room; swaying and burning parts are drawn each frame.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public class DecorItem
{
    public string kind;
    public float x, y;          // anchor in world px: bottom centre for floor props, top centre for hanging ones
    public float w, h, len;     // size, and length for hanging things
    public float seed;
    public bool lit;            // gives off light
}

public static class Decor
{
    const int TS = H.TILE;

    // ---- noise ---------------------------------------------------------------------------------------
    static float Smooth(float t) => t * t * (3 - 2 * t);
    public static float Noise(float x, float y)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = Smooth(x - ix), fy = Smooth(y - iy);
        float a = TileArt.Hash(ix, iy), b = TileArt.Hash(ix + 1, iy), c = TileArt.Hash(ix, iy + 1), d = TileArt.Hash(ix + 1, iy + 1);
        return H.Lerp(H.Lerp(a, b, fx), H.Lerp(c, d, fx), fy);
    }
    public static float Fbm(float x, float y) => Noise(x, y) * 0.55f + Noise(x * 2.1f + 17, y * 2.1f + 3) * 0.3f + Noise(x * 4.3f + 5, y * 4.3f + 29) * 0.15f;

    static Color Mul(int[] c, float k, float a = 1) => new(Math.Clamp(c[0] * k / 255f, 0, 1), Math.Clamp(c[1] * k / 255f, 0, 1), Math.Clamp(c[2] * k / 255f, 0, 1), a);
    static Color C(string hex, float a = 1) { var c = H.Col(hex); c.A = a; return c; }

    // ---- what each region is furnished with ------------------------------------------------------------
    class Theme
    {
        public (string kind, float w)[] floor, wall, hang, under;
        public float floorRate, wallRate, hangRate;
        public string arch;                     // arcade, timber, ribs, flesh, none
        public string banner;
    }
    static readonly Dictionary<string, Theme> THEMES = new()
    {
        ["wrackline"] = new Theme
        {
            floor = new[] { ("rope", 2f), ("crate", 2f), ("barrel", 2f), ("candles", 1.5f), ("sack", 1f), ("pot", 1.5f), ("bucket", 1f), ("anchor", 0.6f), ("net", 1f) },
            wall = new[] { ("sconce", 3f), ("net_wall", 1.5f), ("chainring", 1.5f), ("plaque", 0.8f) },
            hang = new[] { ("chain", 2f), ("lamp", 1.5f), ("rope_hang", 1f), ("cobweb", 1f) },
            under = new[] { ("kelp_tuft", 3f), ("rock", 2f), ("shell", 1f) },
            floorRate = 0.32f, wallRate = 1, hangRate = 0.12f, arch = "arcade", banner = "#5a2a24",
        },
        ["saltworks"] = new Theme
        {
            floor = new[] { ("saltpile", 3f), ("sack", 3f), ("barrel", 2f), ("crate", 2f), ("bucket", 1f), ("rake", 1f), ("crystals", 1.2f) },
            wall = new[] { ("sconce", 2f), ("pipe", 3f), ("gauge", 1f), ("shelf", 1f) },
            hang = new[] { ("chain", 2f), ("hook", 1.5f), ("stalactite", 3f), ("lamp", 1f) },
            under = new[] { ("crystals", 2f), ("rock", 1f) },
            floorRate = 0.38f, wallRate = 1, hangRate = 0.22f, arch = "timber", banner = null,
        },
        ["cloister"] = new Theme
        {
            floor = new[] { ("candles", 3f), ("statue", 1.6f), ("urn", 2f), ("column", 1.2f), ("books", 1f), ("candelabra", 1.4f), ("bones", 0.6f) },
            wall = new[] { ("sconce", 2.5f), ("niche", 2f), ("ivy", 2f), ("plaque", 1f) },
            hang = new[] { ("censer", 1.5f), ("chain", 1f), ("ivy_hang", 3f), ("cobweb", 1.5f), ("lamp", 1f) },
            under = new[] { ("kelp_tuft", 2f), ("rock", 1f), ("urn", 0.5f) },
            floorRate = 0.34f, wallRate = 1, hangRate = 0.2f, arch = "arcade", banner = "#2f4a3c",
        },
        ["choir"] = new Theme
        {
            floor = new[] { ("candelabra", 3f), ("candles", 3f), ("statue", 1.6f), ("urn", 1f), ("books", 1f) },
            wall = new[] { ("sconce", 2f), ("niche", 2f), ("organ", 1.4f), ("plaque", 0.8f) },
            hang = new[] { ("censer", 2f), ("lamp", 2f), ("chain", 1f), ("kelp", 2f) },
            under = new[] { ("kelp_tuft", 3f), ("rock", 1f), ("candles_out", 1f) },
            floorRate = 0.34f, wallRate = 1, hangRate = 0.2f, arch = "arcade", banner = "#2c3466",
        },
        ["ribvault"] = new Theme
        {
            floor = new[] { ("bones", 3f), ("crystals", 2f), ("rock", 2f), ("candles", 1f), ("rope", 1f) },
            wall = new[] { ("sconce", 2.5f), ("plaque", 0.6f) },
            hang = new[] { ("rope_hang", 2f), ("stalactite", 2f), ("chain", 1f), ("cobweb", 1f) },
            under = new[] { ("bones", 1f), ("rock", 1f) },
            floorRate = 0.3f, wallRate = 0.8f, hangRate = 0.18f, arch = "ribs", banner = null,
        },
        ["gullet"] = new Theme
        {
            floor = new[] { ("nodules", 3f), ("bones", 2f), ("rock", 2f), ("kelp_tuft", 2f) },
            wall = new[] { ("nodules_wall", 3f) },
            hang = new[] { ("kelp", 3f), ("roots", 2f) },
            under = new[] { ("nodules", 2f), ("kelp_tuft", 3f), ("bones", 1f) },
            floorRate = 0.32f, wallRate = 0.7f, hangRate = 0.3f, arch = "flesh", banner = null,
        },
        ["skull"] = new Theme
        {
            floor = new[] { ("candles", 3f), ("bones", 1.5f), ("crate", 1f), ("books", 1f), ("candelabra", 1f) },
            wall = new[] { ("sconce", 3f), ("plaque", 1f), ("pipe", 1f) },
            hang = new[] { ("chain", 2f), ("lamp", 2f), ("cobweb", 1f) },
            under = new[] { ("bones", 1f) },
            floorRate = 0.3f, wallRate = 1, hangRate = 0.16f, arch = "ribs", banner = "#5a3a1a",
        },
    };
    static readonly Theme OUTDOOR = new()
    {
        floor = new[] { ("rock", 3f), ("pebbles", 3f), ("wrack", 2f), ("shell", 1.5f), ("driftwood", 1.2f), ("pot", 0.6f), ("rope", 0.6f), ("anchor", 0.3f) },
        wall = Array.Empty<(string, float)>(), hang = new[] { ("kelp", 3f), ("roots", 1f) },
        under = new[] { ("kelp_tuft", 3f), ("rock", 2f), ("shell", 1f) },
        floorRate = 0.42f, wallRate = 0, hangRate = 0.3f, arch = "none",
    };
    static Theme ThemeFor(RoomDef r) => r.Outdoor ? OUTDOOR : THEMES.TryGetValue(r.Region, out var t) ? t : THEMES["wrackline"];

    static string Pick((string kind, float w)[] list, Random rng)
    {
        float total = list.Sum(e => e.w), v = (float)rng.NextDouble() * total;
        foreach (var (k, w) in list) { v -= w; if (v <= 0) return k; }
        return list[^1].kind;
    }
    static readonly Dictionary<string, (float w, float h)> SIZE = new()
    {
        ["rock"] = (14, 8), ["pebbles"] = (12, 3), ["wrack"] = (18, 4), ["shell"] = (5, 3), ["driftwood"] = (28, 5), ["pot"] = (14, 10),
        ["rope"] = (12, 5), ["anchor"] = (14, 18), ["crate"] = (14, 13), ["barrel"] = (11, 14), ["sack"] = (11, 11), ["saltpile"] = (18, 8),
        ["candles"] = (12, 10), ["candles_out"] = (10, 8), ["candelabra"] = (12, 24), ["urn"] = (9, 14), ["statue"] = (14, 30), ["column"] = (28, 9),
        ["books"] = (10, 7), ["bones"] = (16, 5), ["crystals"] = (12, 11), ["bucket"] = (8, 8), ["rake"] = (6, 22), ["net"] = (20, 6),
        ["nodules"] = (12, 6), ["kelp_tuft"] = (10, 12),
    };
    static readonly HashSet<string> TALL = new() { "candelabra", "statue", "anchor", "rake" };
    static readonly HashSet<string> LIT = new() { "candles", "candelabra", "sconce", "lamp", "nodules", "nodules_wall", "brazier", "crystals" };

    // ---- placement --------------------------------------------------------------------------------------
    public static List<DecorItem> Build(RoomDef r, Func<int, int, bool> solidAt, IEnumerable<Ent> ents)
    {
        var items = new List<DecorItem>();
        var th = ThemeFor(r);
        var rng = new Random(r.Id.Aggregate(17, (h, ch) => unchecked(h * 31 + ch)));
        char At(int lx, int ly) => (lx < 0 || ly < 0 || lx >= r.Tw || ly >= r.Th) ? '#' : r.Live[ly * r.Tw + lx];
        bool Empty(int lx, int ly) => !solidAt(lx, ly) && At(lx, ly) is not ('H' or '=' or 'x' or 'b' or 'y' or 'w');
        bool Ground(int lx, int ly) => solidAt(lx, ly + 1) && At(lx, ly + 1) is not ('=' or 'b' or 'S' or 'C' or '_');
        // keep clear of anything the player handles
        var reserved = new HashSet<(int, int)>();
        foreach (var e in ents)
        {
            if (e is Zone) continue;
            int x0 = H.FloorI((e.x - r.Px) / TS) - 1, x1 = H.FloorI((e.x + e.w - r.Px) / TS) + 1;
            int y0 = H.FloorI((e.y - r.Py) / TS) - 1, y1 = H.FloorI((e.y + e.h - r.Py) / TS);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) reserved.Add((x, y));
        }
        bool Free(int lx, int ly) => !reserved.Contains((lx, ly));
        float lowLine = r.Row == 4 && r.FixedWater == null ? (Tide.SEA_LOW - r.Oy) : float.PositiveInfinity;
        float World(int l, bool xAxis) => xAxis ? r.Px + l * TS : r.Py + l * TS;

        // floor props, walked along each run of ground
        for (int ly = 1; ly < r.Th - 1; ly++)
        {
            int lx = 1;
            while (lx < r.Tw - 1)
            {
                if (!(Empty(lx, ly) && Ground(lx, ly))) { lx++; continue; }
                bool under = ly >= lowLine;
                var list = under ? th.under : th.floor;
                if (rng.NextDouble() < th.floorRate && Free(lx, ly) && list.Length > 0)
                {
                    string k = Pick(list, rng);
                    bool tall = TALL.Contains(k);
                    if (tall && !(Empty(lx, ly - 1) && Empty(lx, ly - 2))) k = "candles";
                    if (k == "statue" && !(Empty(lx, ly - 1) && Empty(lx, ly - 2) && Ground(lx + 1, ly) && Empty(lx + 1, ly))) k = "urn";
                    var sz = SIZE.TryGetValue(k, out var s0) ? s0 : (10, 8);
                    float jitter = (float)rng.NextDouble() * 6 - 3;
                    items.Add(new DecorItem { kind = k, x = World(lx, true) + 8 + jitter, y = World(ly + 1, false), w = sz.w, h = sz.h, seed = (float)rng.NextDouble(), lit = LIT.Contains(k) });
                    lx += 2 + rng.Next(3);
                }
                else lx++;
            }
        }
        // wall props on indoor back walls: a little above each floor run
        if (th.wallRate > 0 && !r.Outdoor)
        {
            for (int ly = 2; ly < r.Th - 2; ly++)
                for (int lx = 2; lx < r.Tw - 2; lx++)
                {
                    if (!(Empty(lx, ly) && Ground(lx, ly))) continue;
                    // the wall spot sits 2-3 tiles over the floor, with open air around it
                    int wy = ly - 2 - ((lx * 7 + ly) % 2);
                    if (wy < 1 || !Empty(lx, wy) || !Empty(lx, wy - 1) || !Empty(lx - 1, wy) || !Empty(lx + 1, wy)) continue;
                    if (((lx * 13 + ly * 7) % 9) != 0 || rng.NextDouble() > 0.75f * th.wallRate) continue;
                    if (!Free(lx, wy) || items.Any(d => MathF.Abs(d.x - World(lx, true) - 8) < 5 * TS && MathF.Abs(d.y - World(wy, false)) < 3 * TS && d.kind is "sconce" or "niche" or "organ" or "banner")) continue;
                    string k = Pick(th.wall, rng);
                    if (k == "organ" && !(Empty(lx, wy - 2) && Empty(lx, wy - 3))) k = "sconce";
                    items.Add(new DecorItem { kind = k, x = World(lx, true) + 8, y = World(wy, false) + 8, seed = (float)rng.NextDouble(), lit = LIT.Contains(k) });
                    // banners hang higher up in tall halls
                    if (th.banner != null && Empty(lx + 3, wy - 3) && Empty(lx + 3, wy - 6) && Empty(lx + 3, wy - 1) && rng.NextDouble() < 0.5)
                        items.Add(new DecorItem { kind = "banner", x = World(lx + 3, true) + 8, y = World(wy - 6, false), len = 4 * TS + rng.Next(2) * TS, seed = (float)rng.NextDouble() });
                }
        }
        // things hanging from ceilings, cobwebs in the corners
        for (int ly = 1; ly < r.Th - 1; ly++)
            for (int lx = 1; lx < r.Tw - 1; lx++)
            {
                if (!Empty(lx, ly) || !solidAt(lx, ly - 1) || At(lx, ly - 1) is '_' or '=') continue;
                int open = 0; while (open < 12 && Empty(lx, ly + open)) open++;
                bool corner = solidAt(lx - 1, ly) || solidAt(lx + 1, ly);
                if (corner && !r.Outdoor && rng.NextDouble() < 0.35)
                {
                    items.Add(new DecorItem { kind = "cobweb", x = World(lx, true) + (solidAt(lx - 1, ly) ? 0 : TS), y = World(ly, false), w = solidAt(lx - 1, ly) ? 1 : -1, seed = (float)rng.NextDouble() });
                    continue;
                }
                if (open < 4 || rng.NextDouble() > th.hangRate || !Free(lx, ly) || !Free(lx, ly + 1)) continue;
                string k = Pick(th.hang, rng);
                if (k == "cobweb") continue;
                float len = k switch
                {
                    "stalactite" => 6 + (float)rng.NextDouble() * 10,
                    "kelp" or "roots" or "ivy_hang" => 10 + (float)rng.NextDouble() * Math.Min(40, (open - 2) * TS),
                    _ => 14 + (float)rng.NextDouble() * Math.Min(44, (open - 3) * TS),
                };
                items.Add(new DecorItem { kind = k, x = World(lx, true) + 4 + (float)rng.NextDouble() * 8, y = World(ly, false), len = len, seed = (float)rng.NextDouble(), lit = LIT.Contains(k) });
            }
        // kelp swaying up from the sea floor and along the low-water line outdoors
        if (r.Outdoor || r.Row == 4)
            for (int lx = 1; lx < r.Tw - 1; lx++)
                for (int ly = 2; ly < r.Th - 1; ly++)
                    if (ly >= lowLine && Empty(lx, ly) && Ground(lx, ly) && rng.NextDouble() < 0.35 && Free(lx, ly))
                        items.Add(new DecorItem { kind = "kelp_up", x = World(lx, true) + 3 + (float)rng.NextDouble() * 10, y = World(ly + 1, false), len = 12 + (float)rng.NextDouble() * 26, seed = (float)rng.NextDouble() });
        return items;
    }

    // ---- static props, baked with the back wall ---------------------------------------------------------
    public static void PaintBackProps(Canvas c, RoomDef r, List<DecorItem> items)
    {
        var pal = TileArt.PAL[r.Region];
        foreach (var d in items)
        {
            switch (d.kind)
            {
                case "banner": Banner(c, d, ThemeFor(r).banner); break;
                case "sconce": Sconce(c, d.x, d.y); break;
                case "niche": Niche(c, d, pal); break;
                case "plaque": Plaque(c, d.x, d.y, d.seed); break;
                case "ivy": Ivy(c, d.x, d.y, d.seed); break;
                case "net_wall": NetWall(c, d.x, d.y, d.seed); break;
                case "chainring": ChainRing(c, d.x, d.y); break;
                case "pipe": Pipe(c, d.x, d.y, d.seed, pal); break;
                case "gauge": Gauge(c, d.x, d.y); break;
                case "shelf": Shelf(c, d.x, d.y, d.seed); break;
                case "organ": Organ(c, d.x, d.y, d.seed); break;
                case "nodules_wall": NodulesWall(c, d.x, d.y, d.seed); break;
                case "cobweb": Cobweb(c, d.x, d.y, d.w, d.seed); break;
                case "stalactite": Stalactite(c, d.x, d.y, d.len, r.Region == "saltworks", pal); break;
                default:
                    if (SIZE.ContainsKey(d.kind)) FloorProp(c, d, pal, r);
                    break;
            }
        }
    }

    static void FloorProp(Canvas c, DecorItem d, TileArt.Pal pal, RoomDef r)
    {
        float x = d.x, y = d.y, s = d.seed;
        switch (d.kind)
        {
            case "rock":
                {
                    float w = 8 + s * 10, hh = 4 + s * 5;
                    var col = Mul(pal.stone, r.Outdoor ? 0.95f : 0.8f);
                    c.Fill = col; c.BeginPath(); c.MoveTo(x - w / 2, y); c.QuadraticCurveTo(x - w / 2, y - hh, x - w * 0.1f, y - hh); c.QuadraticCurveTo(x + w / 2, y - hh * 0.9f, x + w / 2, y); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(1, 1, 1, 0.12f); c.BeginPath(); c.MoveTo(x - w * 0.4f, y - hh * 0.55f); c.QuadraticCurveTo(x - w * 0.2f, y - hh * 0.95f, x + w * 0.1f, y - hh * 0.9f); c.LineTo(x - w * 0.1f, y - hh * 0.65f); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(x - w / 2, y - 1.5f, w, 1.5f);
                    if (s > 0.55f) { c.Fill = r.Outdoor ? C("#4f6a3a", 0.8f) : C("#3e5a40", 0.7f); c.FillRect(x - w * 0.25f, y - hh * 0.95f, w * 0.35f, 1.4f); }
                    break;
                }
            case "pebbles":
                for (int i = 0; i < 5; i++)
                {
                    float px = x - 6 + Hsh(s, i) * 12, pr = 1 + Hsh(s, i + 9) * 1.6f;
                    c.Fill = Mul(pal.stone, 0.7f + Hsh(s, i + 3) * 0.4f); c.BeginPath(); c.Ellipse(px, y - pr * 0.6f, pr * 1.3f, pr * 0.8f, 0, 0, H.PI * 2); c.FillPath();
                }
                break;
            case "wrack":
                for (int i = 0; i < 6; i++)
                {
                    float px = x - 9 + Hsh(s, i) * 18;
                    c.Stroke = i % 2 == 0 ? C("#3d4a26", 0.95f) : C("#5a4a22", 0.9f); c.LineWidth = 1.3f;
                    c.BeginPath(); c.MoveTo(px - 4, y - 0.5f); c.QuadraticCurveTo(px, y - 4 - Hsh(s, i + 5) * 2, px + 5, y - 0.8f); c.StrokePath();
                }
                break;
            case "shell":
                c.Fill = C("#d8cbb0"); c.BeginPath(); c.MoveTo(x - 3, y); c.QuadraticCurveTo(x, y - 5, x + 3, y); c.ClosePath(); c.FillPath();
                c.Stroke = C("#9a8a70", 0.8f); c.LineWidth = 0.4f; for (int i = -2; i <= 2; i++) c.Line(x, y - 0.3f, x + i * 1.2f, y - 3.2f + MathF.Abs(i) * 0.6f);
                break;
            case "driftwood":
                c.Fill = C("#6e5d4a"); c.BeginPath(); c.MoveTo(x - 14, y); c.LineTo(x - 13, y - 4); c.LineTo(x + 12, y - 5); c.LineTo(x + 14, y - 1); c.LineTo(x + 13, y); c.ClosePath(); c.FillPath();
                c.Fill = C("#9a8a74", 0.6f); c.FillRect(x - 12, y - 4, 22, 0.8f);
                c.Fill = C("#3a2e22", 0.8f); c.FillCircle(x - 3, y - 2.5f, 1.1f); c.FillRect(x + 5, y - 3, 4, 0.6f);
                break;
            case "pot":
                c.Fill = C("#4a3a28"); c.FillRect(x - 7, y - 2, 14, 2);
                c.Stroke = C("#7a6040"); c.LineWidth = 1;
                for (int i = 0; i < 5; i++) { float a = H.PI + i * H.PI / 4; c.Line(x + MathF.Cos(a) * 7, y - 1, x + MathF.Cos(a) * 3, y - 10 + MathF.Abs(MathF.Sin(a)) * 2); }
                c.BeginPath(); c.Arc(x, y - 1, 7, H.PI, H.PI * 2); c.StrokePath();
                c.Stroke = C("#9a8a6a", 0.5f); c.LineWidth = 0.4f; for (int i = 0; i < 3; i++) { c.BeginPath(); c.Arc(x, y - 1, 3 + i * 1.5f, H.PI * 1.1f, H.PI * 1.9f); c.StrokePath(); }
                break;
            case "rope":
                for (int i = 0; i < 3; i++)
                {
                    c.Stroke = C("#3a2e1e"); c.LineWidth = 2.2f; c.BeginPath(); c.Ellipse(x, y - 1.5f - i * 1.4f, 6 - i, 2.2f - i * 0.3f, 0, 0, H.PI * 2); c.StrokePath();
                    c.Stroke = C("#a08458"); c.LineWidth = 1.2f; c.BeginPath(); c.Ellipse(x, y - 1.5f - i * 1.4f, 6 - i, 2.2f - i * 0.3f, 0, 0, H.PI * 2); c.StrokePath();
                }
                break;
            case "anchor":
                {
                    c.Save(); c.Translate(x, y); c.Rotate(-0.25f);
                    c.Stroke = C("#2c2a28"); c.LineWidth = 2.6f; c.Line(0, -17, 0, -2);
                    c.BeginPath(); c.Arc(0, -6, 6, H.PI * 0.15f, H.PI * 0.85f); c.StrokePath();
                    c.Line(-4, -14, 4, -14);
                    c.Stroke = C("#5a4a3a", 0.8f); c.LineWidth = 1.2f; c.BeginPath(); c.Arc(0, -19, 2, 0, H.PI * 2); c.StrokePath();
                    c.Fill = C("#7a3a20", 0.5f); c.FillRect(-1, -12, 2, 4);
                    c.Restore();
                    break;
                }
            case "crate":
                {
                    float w = 12 + s * 4, hh = 11 + s * 3;
                    Crate(c, x, y, w, hh);
                    if (s > 0.7f) Crate(c, x + 2, y - hh, w * 0.75f, hh * 0.75f);
                    break;
                }
            case "barrel":
                {
                    float w = 10, hh = 13;
                    c.Fill = C("#5a4026"); c.BeginPath(); c.MoveTo(x - w / 2 + 1, y); c.QuadraticCurveTo(x - w / 2 - 1, y - hh / 2, x - w / 2 + 1, y - hh); c.LineTo(x + w / 2 - 1, y - hh); c.QuadraticCurveTo(x + w / 2 + 1, y - hh / 2, x + w / 2 - 1, y); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(1, 0.9f, 0.7f, 0.1f); c.FillRect(x - 2, y - hh + 1, 2, hh - 2);
                    c.Fill = C("#2e2a26"); c.FillRect(x - w / 2, y - hh + 2, w, 1.2f); c.FillRect(x - w / 2 - 0.5f, y - hh / 2, w + 1, 1.2f); c.FillRect(x - w / 2, y - 3, w, 1.2f);
                    c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(x + w / 2 - 3, y - hh + 1, 2, hh - 1);
                    break;
                }
            case "sack":
                {
                    var col = r.Region == "saltworks" ? C("#b8a888") : C("#8a7658");
                    c.Fill = col; c.BeginPath(); c.MoveTo(x - 5, y); c.QuadraticCurveTo(x - 7, y - 6, x - 2, y - 9); c.LineTo(x + 2, y - 9); c.QuadraticCurveTo(x + 7, y - 6, x + 5, y); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(0, 0, 0, 0.2f); c.BeginPath(); c.MoveTo(x + 1, y); c.QuadraticCurveTo(x + 6, y - 5, x + 2, y - 9); c.LineTo(x + 5, y); c.ClosePath(); c.FillPath();
                    c.Fill = C("#5a4a34"); c.FillRect(x - 2.5f, y - 10, 5, 1.2f);
                    if (r.Region == "saltworks") { c.Fill = C("#ecedf0", 0.85f); c.BeginPath(); c.MoveTo(x + 3, y); c.QuadraticCurveTo(x + 7, y - 2, x + 10, y); c.ClosePath(); c.FillPath(); }
                    break;
                }
            case "saltpile":
                {
                    float w = 12 + s * 8, hh = 5 + s * 5;
                    c.Fill = C("#d8d6d0"); c.BeginPath(); c.MoveTo(x - w / 2, y); c.QuadraticCurveTo(x - w * 0.1f, y - hh * 1.4f, x + w / 2, y); c.ClosePath(); c.FillPath();
                    c.Fill = C("#ffffff", 0.5f); c.BeginPath(); c.MoveTo(x - w * 0.3f, y - hh * 0.4f); c.QuadraticCurveTo(x - w * 0.1f, y - hh * 1.1f, x + w * 0.05f, y - hh * 0.6f); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(0.4f, 0.38f, 0.34f, 0.35f); c.FillRect(x - w / 2, y - 1, w, 1);
                    for (int i = 0; i < 4; i++) c.FillRect(x - w / 2 + Hsh(s, i) * w, y - Hsh(s, i + 4) * hh, 0.8f, 0.8f, C("#ffffff", 0.9f));
                    break;
                }
            case "candles":
            case "candles_out":
                {
                    int n = 2 + (int)(s * 3);
                    for (int i = 0; i < n; i++)
                    {
                        float cx = x - 5 + i * (10f / Math.Max(1, n - 1)), hh = 4 + Hsh(s, i) * 6;
                        c.Fill = d.kind == "candles" ? C("#e8dcc0") : C("#8a8a78"); c.FillRect(cx - 1.1f, y - hh, 2.2f, hh);
                        c.Fill = new Color(1, 1, 1, 0.25f); c.FillRect(cx - 1.1f, y - hh, 0.7f, hh);
                        c.Fill = C("#d8ccb0"); c.FillRect(cx - 1.6f, y - 1.2f, 3.2f, 1.2f);
                        c.Fill = C("#2a2420"); c.FillRect(cx - 0.2f, y - hh - 1, 0.4f, 1);
                    }
                    c.Fill = new Color(0.85f, 0.8f, 0.7f, 0.6f); c.BeginPath(); c.Ellipse(x, y - 0.4f, 7, 1, 0, 0, H.PI * 2); c.FillPath();
                    break;
                }
            case "candelabra":
                {
                    c.Fill = C("#2a2622"); c.FillRect(x - 0.8f, y - 20, 1.6f, 20); c.FillRect(x - 4, y - 1.5f, 8, 1.5f);
                    c.BeginPath(); c.MoveTo(x - 3, y - 1.5f); c.LineTo(x, y - 5); c.LineTo(x + 3, y - 1.5f); c.ClosePath(); c.FillPath();
                    c.Stroke = C("#2a2622"); c.LineWidth = 1.2f; c.BeginPath(); c.MoveTo(x - 5, y - 21); c.QuadraticCurveTo(x - 5, y - 17, x, y - 17); c.QuadraticCurveTo(x + 5, y - 17, x + 5, y - 21); c.StrokePath();
                    foreach (float ox in new[] { -5f, 0f, 5f }) { c.Fill = C("#e8dcc0"); c.FillRect(x + ox - 0.9f, y - 25 + (ox == 0 ? -2 : 0), 1.8f, 4); c.Fill = C("#2a2622"); c.FillRect(x + ox - 1.6f, y - 21.5f + (ox == 0 ? -2 : 0), 3.2f, 1); }
                    break;
                }
            case "urn":
                c.Fill = Mul(pal.stone, 1.1f); c.BeginPath(); c.MoveTo(x - 2.5f, y - 13); c.LineTo(x + 2.5f, y - 13); c.QuadraticCurveTo(x + 2, y - 11, x + 4, y - 8); c.QuadraticCurveTo(x + 6, y - 3, x + 3, y - 1); c.LineTo(x + 3.5f, y); c.LineTo(x - 3.5f, y); c.LineTo(x - 3, y - 1); c.QuadraticCurveTo(x - 6, y - 3, x - 4, y - 8); c.QuadraticCurveTo(x - 2, y - 11, x - 2.5f, y - 13); c.ClosePath(); c.FillPath();
                c.Fill = new Color(1, 1, 1, 0.12f); c.FillRect(x - 3, y - 9, 1.2f, 6);
                c.Fill = new Color(0, 0, 0, 0.25f); c.FillRect(x + 1.5f, y - 9, 2, 7);
                c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(x - 4.5f, y - 7, 9, 0.8f);
                break;
            case "statue":
                {
                    // a hooded monk in stone, hands folded, on a plinth
                    var st = Mul(pal.stone, 1.15f); var dk = Mul(pal.stone, 0.75f);
                    c.Fill = dk; c.FillRect(x - 7, y - 5, 14, 5); c.Fill = st; c.FillRect(x - 8, y - 6, 16, 1.5f);
                    c.Fill = st; c.BeginPath(); c.MoveTo(x - 5, y - 6); c.QuadraticCurveTo(x - 6, y - 16, x - 4, y - 22); c.QuadraticCurveTo(x, y - 30, x + 4, y - 22); c.QuadraticCurveTo(x + 6, y - 16, x + 5, y - 6); c.ClosePath(); c.FillPath();
                    c.Fill = new Color(0, 0, 0, 0.55f); c.BeginPath(); c.Ellipse(x + 0.5f, y - 22.5f, 2.2f, 2.8f, 0, 0, H.PI * 2); c.FillPath();
                    c.Fill = new Color(0, 0, 0, 0.25f); c.BeginPath(); c.MoveTo(x + 1, y - 6); c.QuadraticCurveTo(x + 5, y - 14, x + 3, y - 21); c.QuadraticCurveTo(x + 6, y - 16, x + 5, y - 6); c.ClosePath(); c.FillPath();
                    c.Fill = Mul(pal.stone, 1.3f); c.FillRect(x - 2.5f, y - 15, 5, 2.5f);
                    c.Fill = new Color(1, 1, 1, 0.1f); c.FillRect(x - 4, y - 20, 1, 12);
                    if (d.seed > 0.5f) { c.Fill = C("#3e5a40", 0.6f); c.FillRect(x - 5, y - 8, 4, 1.4f); }
                    break;
                }
            case "column":
                {
                    var st = Mul(pal.stone, 1.05f);
                    c.Fill = st; c.FillRect(x - 14, y - 8, 22, 8);
                    c.Fill = new Color(0, 0, 0, 0.25f); for (int i = 0; i < 4; i++) c.FillRect(x - 12 + i * 5, y - 8, 0.8f, 8);
                    c.Fill = Mul(pal.stone, 1.25f); c.FillRect(x - 14, y - 8, 22, 1);
                    c.Fill = st; c.BeginPath(); c.Ellipse(x + 8, y - 4, 2.5f, 4, 0, 0, H.PI * 2); c.FillPath();
                    c.Fill = new Color(0, 0, 0, 0.3f); c.BeginPath(); c.Ellipse(x + 8, y - 4, 1.6f, 3, 0, 0, H.PI * 2); c.FillPath();
                    c.Fill = Mul(pal.stone, 0.8f); c.FillRect(x + 11, y - 3, 3, 3);
                    break;
                }
            case "books":
                for (int i = 0; i < 3; i++)
                {
                    float bw = 8 - i * 1.2f, by = y - 2.2f * (i + 1);
                    c.Fill = C(new[] { "#5a2a24", "#2c3a4a", "#4a4024" }[(i + (int)(s * 3)) % 3]); c.FillRect(x - bw / 2 + (i % 2) * 0.8f, by, bw, 2.2f);
                    c.Fill = C("#d8ccb0", 0.8f); c.FillRect(x - bw / 2 + 0.6f + (i % 2) * 0.8f, by + 0.5f, bw - 1.2f, 0.6f);
                }
                break;
            case "bones":
                c.Fill = C("#d8ccb0"); c.Stroke = C("#d8ccb0"); c.LineWidth = 1.2f;
                c.Line(x - 7, y - 1, x + 2, y - 2); c.FillCircle(x - 7, y - 1, 1); c.FillCircle(x + 2, y - 2, 1);
                c.Line(x + 1, y - 0.6f, x + 7, y - 0.8f);
                if (s > 0.4f) { c.BeginPath(); c.Arc(x - 1, y - 3, 2.6f, H.PI, H.PI * 2); c.LineTo(x + 1.6f, y - 1); c.LineTo(x - 3.6f, y - 1); c.ClosePath(); c.FillPath(); c.Fill = C("#1a1612"); c.FillRect(x - 2.2f, y - 3.6f, 1, 1); c.FillRect(x - 0.4f, y - 3.6f, 1, 1); }
                break;
            case "crystals":
                for (int i = 0; i < 4; i++)
                {
                    float bx = x - 5 + i * 3.3f, hh = 4 + Hsh(s, i) * 7, lean = (Hsh(s, i + 7) - 0.5f) * 3;
                    c.Fill = C("#cfe4ee", 0.9f); c.BeginPath(); c.MoveTo(bx - 1.6f, y); c.LineTo(bx + lean, y - hh); c.LineTo(bx + 1.6f, y); c.ClosePath(); c.FillPath();
                    c.Fill = C("#ffffff", 0.6f); c.BeginPath(); c.MoveTo(bx - 0.6f, y); c.LineTo(bx + lean, y - hh); c.LineTo(bx, y); c.ClosePath(); c.FillPath();
                }
                break;
            case "bucket":
                c.Fill = C("#5a4430"); c.BeginPath(); c.MoveTo(x - 4, y - 7); c.LineTo(x + 4, y - 7); c.LineTo(x + 3, y); c.LineTo(x - 3, y); c.ClosePath(); c.FillPath();
                c.Fill = C("#2e2a26"); c.FillRect(x - 4, y - 6, 8, 0.9f); c.FillRect(x - 3.5f, y - 2, 7, 0.9f);
                c.Stroke = C("#3a3530"); c.LineWidth = 0.7f; c.BeginPath(); c.Arc(x, y - 7, 4, H.PI, H.PI * 2); c.StrokePath();
                break;
            case "rake":
                c.Save(); c.Translate(x, y); c.Rotate(-0.2f);
                c.Fill = C("#6a5038"); c.FillRect(-0.8f, -22, 1.6f, 22);
                c.Fill = C("#3a3632"); c.FillRect(-4, -23, 8, 1.6f); for (int i = 0; i < 4; i++) c.FillRect(-3.5f + i * 2.3f, -23, 0.8f, 3);
                c.Restore();
                break;
            case "net":
                c.Fill = C("#4a4030", 0.9f); c.BeginPath(); c.MoveTo(x - 10, y); c.QuadraticCurveTo(x - 6, y - 6, x + 2, y - 5); c.QuadraticCurveTo(x + 9, y - 4, x + 10, y); c.ClosePath(); c.FillPath();
                c.Stroke = C("#8a7a5a", 0.6f); c.LineWidth = 0.4f;
                for (int i = 0; i < 6; i++) c.Line(x - 9 + i * 3.4f, y, x - 6 + i * 3, y - 4.5f);
                for (int i = 0; i < 6; i++) c.Line(x - 6 + i * 3, y, x - 9 + i * 3.4f, y - 4);
                c.Fill = C("#c08040"); c.FillCircle(x + 6, y - 3, 1.2f);
                break;
            case "nodules":
                for (int i = 0; i < 4; i++) { float bx = x - 5 + Hsh(s, i) * 10, br = 1.2f + Hsh(s, i + 3) * 1.8f; c.Fill = C("#244a4a"); c.FillCircle(bx, y - br, br); c.Fill = C("#7af0d8", 0.85f); c.FillCircle(bx, y - br, br * 0.5f); }
                break;
            case "kelp_tuft":
                for (int i = 0; i < 4; i++)
                {
                    float bx = x - 4 + i * 2.6f, hh = 6 + Hsh(s, i) * 6;
                    c.Stroke = i % 2 == 0 ? C("#2f5a3a", 0.9f) : C("#4a6a30", 0.9f); c.LineWidth = 1.3f;
                    c.BeginPath(); c.MoveTo(bx, y); c.QuadraticCurveTo(bx + 2, y - hh / 2, bx + (Hsh(s, i + 5) - 0.5f) * 5, y - hh); c.StrokePath();
                }
                break;
        }
    }
    static float Hsh(float s, int i) => TileArt.Hash((int)(s * 9973) + i * 31, i * 7 + 3);

    static void Crate(Canvas c, float x, float y, float w, float hh)
    {
        c.Fill = C("#6a5032"); c.FillRect(x - w / 2, y - hh, w, hh);
        c.Fill = new Color(0, 0, 0, 0.3f); for (int i = 1; i < 3; i++) c.FillRect(x - w / 2, y - hh + i * hh / 3, w, 0.7f);
        c.Fill = C("#4a3622"); c.FillRect(x - w / 2, y - hh, w, 1.4f); c.FillRect(x - w / 2, y - 1.4f, w, 1.4f); c.FillRect(x - w / 2, y - hh, 1.4f, hh); c.FillRect(x + w / 2 - 1.4f, y - hh, 1.4f, hh);
        c.Stroke = C("#4a3622"); c.LineWidth = 1.2f; c.Line(x - w / 2 + 1, y - 1, x + w / 2 - 1, y - hh + 1);
        c.Fill = new Color(1, 0.9f, 0.7f, 0.1f); c.FillRect(x - w / 2 + 1.4f, y - hh + 1.4f, w - 2.8f, 0.8f);
        c.Fill = C("#9a9080"); c.FillRect(x - w / 2 + 0.4f, y - hh + 0.4f, 0.7f, 0.7f); c.FillRect(x + w / 2 - 1.1f, y - hh + 0.4f, 0.7f, 0.7f);
    }

    // ---- wall furniture -----------------------------------------------------------------------------------
    static void Sconce(Canvas c, float x, float y)
    {
        c.Fill = C("#1e1c1a"); c.FillRect(x - 1.5f, y - 1, 3, 7);
        c.Stroke = C("#2a2622"); c.LineWidth = 1.2f; c.BeginPath(); c.MoveTo(x, y + 4); c.QuadraticCurveTo(x + 4, y + 3, x + 3, y - 2); c.StrokePath();
        c.Fill = C("#4a3826"); c.FillRect(x + 1.8f, y - 6, 2.4f, 5);
        c.Fill = C("#2a2622"); c.FillRect(x + 1, y - 2, 4, 1.2f);
        // soot above the flame
        c.Glow(x + 3, y - 14, 4, 9, new Color(0, 0, 0, 0.35f));
    }
    static void Banner(Canvas c, DecorItem d, string col)
    {
        float x = d.x, y = d.y, len = d.len, w = 12;
        var cloth = C(col ?? "#4a3020");
        c.Fill = C("#2a2420"); c.FillRect(x - w / 2 - 2, y - 1, w + 4, 1.6f);
        c.Fill = C("#b08840"); c.FillCircle(x - w / 2 - 2, y - 0.2f, 1); c.FillCircle(x + w / 2 + 2, y - 0.2f, 1);
        c.Fill = cloth;
        c.BeginPath(); c.MoveTo(x - w / 2, y); c.LineTo(x + w / 2, y);
        int teeth = 3;
        for (int i = 0; i <= teeth * 2; i++)
        {
            float tx = x + w / 2 - i * (w / (teeth * 2));
            float ty = y + len - (i % 2 == 0 ? 0 : 4) - Hsh(d.seed, i) * 3;
            c.LineTo(tx, ty);
        }
        c.ClosePath(); c.FillPath();
        c.Fill = new Color(0, 0, 0, 0.25f); c.FillRect(x + w / 2 - 3, y, 3, len - 4);
        c.Fill = new Color(1, 1, 1, 0.06f); c.FillRect(x - w / 2 + 1, y, 1.5f, len - 4);
        // a faded emblem: a bell, the abbey's mark
        float ey = y + len * 0.38f;
        c.Fill = C("#c9a14a", 0.55f);
        c.BeginPath(); c.MoveTo(x - 2.5f, ey + 3); c.QuadraticCurveTo(x - 2.5f, ey - 3, x, ey - 3.5f); c.QuadraticCurveTo(x + 2.5f, ey - 3, x + 2.5f, ey + 3); c.ClosePath(); c.FillPath();
        c.FillRect(x - 3.5f, ey + 3, 7, 0.9f); c.FillCircle(x, ey + 4.5f, 0.8f);
    }
    static void Niche(Canvas c, DecorItem d, TileArt.Pal pal)
    {
        float x = d.x, y = d.y + 6;
        c.Fill = Mul(pal.back, 0.45f); c.BeginPath(); c.MoveTo(x - 6, y + 4); c.LineTo(x - 6, y - 8); c.QuadraticCurveTo(x - 6, y - 15, x, y - 17); c.QuadraticCurveTo(x + 6, y - 15, x + 6, y - 8); c.LineTo(x + 6, y + 4); c.ClosePath(); c.FillPath();
        c.Stroke = Mul(pal.back, 1.6f); c.LineWidth = 1.2f; c.BeginPath(); c.MoveTo(x - 6.5f, y + 4); c.LineTo(x - 6.5f, y - 8); c.QuadraticCurveTo(x - 6.5f, y - 15.5f, x, y - 17.5f); c.QuadraticCurveTo(x + 6.5f, y - 15.5f, x + 6.5f, y - 8); c.LineTo(x + 6.5f, y + 4); c.StrokePath();
        c.Fill = Mul(pal.back, 1.5f); c.FillRect(x - 7.5f, y + 4, 15, 1.5f);
        if (d.seed > 0.5f)
        {
            // a small saint in the niche
            c.Fill = Mul(pal.stone, 1.05f); c.BeginPath(); c.MoveTo(x - 3, y + 4); c.QuadraticCurveTo(x - 3.5f, y - 4, x - 2, y - 7); c.QuadraticCurveTo(x, y - 11, x + 2, y - 7); c.QuadraticCurveTo(x + 3.5f, y - 4, x + 3, y + 4); c.ClosePath(); c.FillPath();
            c.Fill = new Color(0, 0, 0, 0.5f); c.FillCircle(x + 0.3f, y - 7.5f, 1.2f);
        }
        else { c.Fill = C("#e8dcc0"); c.FillRect(x - 1, y - 1, 2, 5); c.Fill = C("#2a2420"); c.FillRect(x - 0.2f, y - 2, 0.4f, 1); }
    }
    static void Plaque(Canvas c, float x, float y, float s)
    {
        bool brass = s > 0.5f;
        c.Fill = brass ? C("#6a5420") : C("#4a4a44"); c.FillRect(x - 7, y - 4, 14, 8);
        c.Fill = brass ? C("#b08a40") : C("#6a6a60"); c.FillRect(x - 6.2f, y - 3.2f, 12.4f, 6.4f);
        c.Fill = new Color(0, 0, 0, 0.35f); for (int i = 0; i < 3; i++) c.FillRect(x - 4.5f, y - 1.8f + i * 1.6f, 9 - (i == 2 ? 3 : 0), 0.6f);
        c.Fill = new Color(1, 1, 1, 0.2f); c.FillRect(x - 6.2f, y - 3.2f, 12.4f, 0.6f);
    }
    static void Ivy(Canvas c, float x, float y, float s)
    {
        for (int k = 0; k < 4; k++)
        {
            float bx = x - 8 + k * 5, len = 10 + Hsh(s, k) * 22;
            c.Stroke = C("#2a3a22", 0.9f); c.LineWidth = 0.8f;
            c.BeginPath(); c.MoveTo(bx, y - 10);
            for (int i = 1; i <= 6; i++) c.LineTo(bx + MathF.Sin(i * 1.3f + s * 9 + k) * 1.5f, y - 10 + len * i / 6);
            c.StrokePath();
            for (int i = 0; i < 6; i++)
            {
                float lx = bx + MathF.Sin(i * 1.3f + s * 9 + k) * 1.5f, ly = y - 10 + len * i / 6;
                c.Fill = (i + k) % 2 == 0 ? C("#3a5a30", 0.9f) : C("#4f7040", 0.9f);
                c.BeginPath(); c.Ellipse(lx + (i % 2 == 0 ? 1.4f : -1.4f), ly, 1.6f, 1.1f, 0.5f, 0, H.PI * 2); c.FillPath();
            }
        }
    }
    static void NetWall(Canvas c, float x, float y, float s)
    {
        c.Stroke = C("#7a6a4a", 0.65f); c.LineWidth = 0.5f;
        float w = 22, hh = 18;
        for (int i = 0; i <= 6; i++) { float t = i / 6f; c.BeginPath(); c.MoveTo(x - w / 2 + w * t, y - hh / 2); c.QuadraticCurveTo(x - w / 2 + w * t + 2, y, x - w / 2 + w * t - 1, y + hh / 2 + MathF.Sin(i) * 2); c.StrokePath(); }
        for (int j = 0; j <= 5; j++) { float t = j / 5f; c.BeginPath(); c.MoveTo(x - w / 2, y - hh / 2 + hh * t); c.QuadraticCurveTo(x, y - hh / 2 + hh * t + 3, x + w / 2, y - hh / 2 + hh * t); c.StrokePath(); }
        c.Fill = C("#3a3028"); c.FillCircle(x - w / 2, y - hh / 2, 1.2f); c.FillCircle(x + w / 2, y - hh / 2, 1.2f);
        c.Fill = C("#c08040", 0.9f); c.FillCircle(x - 4, y + 4, 1.4f); c.FillCircle(x + 6, y - 1, 1.2f);
    }
    static void ChainRing(Canvas c, float x, float y)
    {
        c.Fill = C("#2a2826"); c.FillRect(x - 2, y - 2, 4, 4);
        c.Stroke = C("#4a4642"); c.LineWidth = 1.2f; c.BeginPath(); c.Arc(x, y + 4, 3, 0, H.PI * 2); c.StrokePath();
        c.Stroke = new Color(1, 1, 1, 0.15f); c.LineWidth = 0.5f; c.BeginPath(); c.Arc(x, y + 4, 3, H.PI * 1.1f, H.PI * 1.6f); c.StrokePath();
        c.Fill = C("#7a3a20", 0.4f); c.FillRect(x - 1, y + 2, 2, 8);
    }
    static void Pipe(Canvas c, float x, float y, float s, TileArt.Pal pal)
    {
        bool vertical = s > 0.5f;
        var col = C("#4a4038"); var hi = new Color(1, 0.9f, 0.75f, 0.12f);
        if (vertical)
        {
            c.Fill = col; c.FillRect(x - 2.5f, y - 30, 5, 44); c.Fill = hi; c.FillRect(x - 1.5f, y - 30, 1, 44);
            for (int i = 0; i < 3; i++) { c.Fill = C("#3a322a"); c.FillRect(x - 3.5f, y - 26 + i * 14, 7, 2); }
            c.Fill = C("#7a3a20", 0.35f); c.FillRect(x - 2.5f, y - 10, 5, 6);
        }
        else
        {
            c.Fill = col; c.FillRect(x - 24, y - 2.5f, 48, 5); c.Fill = hi; c.FillRect(x - 24, y - 1.5f, 48, 1);
            for (int i = 0; i < 4; i++) { c.Fill = C("#3a322a"); c.FillRect(x - 22 + i * 14, y - 3.5f, 2, 7); }
            c.Fill = C("#8a2a1a"); c.FillCircle(x + 4, y - 5, 2.4f); c.Fill = C("#3a322a"); c.FillRect(x + 3.5f, y - 3, 1, 1);
        }
    }
    static void Gauge(Canvas c, float x, float y)
    {
        c.Fill = C("#6a5420"); c.FillCircle(x, y, 4.5f);
        c.Fill = C("#d8d0b8"); c.FillCircle(x, y, 3.5f);
        c.Stroke = C("#2a2420"); c.LineWidth = 0.6f; c.Line(x, y, x + 2.2f, y - 1.6f);
        c.Fill = C("#4a4038"); c.FillRect(x - 1, y + 4, 2, 8);
    }
    static void Shelf(Canvas c, float x, float y, float s)
    {
        c.Fill = C("#4a3622"); c.FillRect(x - 10, y + 2, 20, 2);
        c.Fill = C("#2a2420"); c.FillRect(x - 8, y + 4, 1.2f, 3); c.FillRect(x + 7, y + 4, 1.2f, 3);
        for (int i = 0; i < 4; i++)
        {
            float jx = x - 7 + i * 4.5f, jh = 3 + Hsh(s, i) * 3;
            c.Fill = C(new[] { "#5a6a4a", "#7a5a3a", "#4a5a6a", "#8a7a5a" }[i]); c.FillRect(jx - 1.6f, y + 2 - jh, 3.2f, jh);
            c.Fill = new Color(1, 1, 1, 0.2f); c.FillRect(jx - 1.2f, y + 2 - jh, 0.6f, jh);
        }
    }
    static void Organ(Canvas c, float x, float y, float s)
    {
        for (int i = 0; i < 7; i++)
        {
            float px = x - 12 + i * 4, hh = 20 + MathF.Abs(3 - i) * -3 + 10;
            c.FillRectGradH(px - 1.5f, y + 8 - hh, 3, hh, C("#8a7040"), C("#4a3a20"));
            c.Fill = C("#1a1612"); c.BeginPath(); c.Ellipse(px, y + 8 - hh * 0.25f, 1, 1.6f, 0, 0, H.PI * 2); c.FillPath();
        }
        c.Fill = C("#3a2a1a"); c.FillRect(x - 15, y + 8, 30, 4);
        c.Fill = C("#b08840", 0.6f); c.FillRect(x - 15, y + 8, 30, 0.8f);
    }
    static void NodulesWall(Canvas c, float x, float y, float s)
    {
        for (int i = 0; i < 6; i++)
        {
            float bx = x - 8 + Hsh(s, i) * 16, by = y - 8 + Hsh(s, i + 6) * 16, br = 1 + Hsh(s, i + 12) * 1.6f;
            c.Fill = C("#18363a"); c.FillCircle(bx, by, br + 0.6f); c.Fill = C("#6ae0d0", 0.8f); c.FillCircle(bx, by, br * 0.6f);
        }
    }
    static void Cobweb(Canvas c, float x, float y, float dir, float s)
    {
        // a fan of threads tucked into the corner where wall meets ceiling
        float size = 8 + s * 6;
        c.Stroke = new Color(0.85f, 0.85f, 0.8f, 0.22f); c.LineWidth = 0.4f;
        for (int i = 0; i <= 4; i++) { float a = i * H.PI / 8; c.Line(x, y, x + dir * MathF.Cos(a) * size, y + MathF.Sin(a) * size); }
        for (int k = 1; k <= 3; k++)
        {
            float rr = size * k / 3.5f;
            c.BeginPath();
            for (int i = 0; i <= 4; i++) { float a = i * H.PI / 8; float px = x + dir * MathF.Cos(a) * rr, py = y + MathF.Sin(a) * rr; if (i == 0) c.MoveTo(px, py); else c.QuadraticCurveTo(x + dir * MathF.Cos(a - H.PI / 16) * rr * 0.85f, y + MathF.Sin(a - H.PI / 16) * rr * 0.85f, px, py); }
            c.StrokePath();
        }
    }
    static void Stalactite(Canvas c, float x, float y, float len, bool salt, TileArt.Pal pal)
    {
        var col = salt ? C("#dcdcd8", 0.95f) : Mul(pal.stone, 0.85f);
        c.Fill = col; c.BeginPath(); c.MoveTo(x - 2.5f, y); c.LineTo(x + 0.3f, y + len); c.LineTo(x + 2.5f, y); c.ClosePath(); c.FillPath();
        c.Fill = salt ? C("#ffffff", 0.6f) : new Color(1, 1, 1, 0.12f); c.BeginPath(); c.MoveTo(x - 1.5f, y); c.LineTo(x + 0.3f, y + len * 0.8f); c.LineTo(x - 0.4f, y); c.ClosePath(); c.FillPath();
        if (len > 10) { c.Fill = col; c.BeginPath(); c.MoveTo(x + 2, y); c.LineTo(x + 3.5f, y + len * 0.45f); c.LineTo(x + 4.5f, y); c.ClosePath(); c.FillPath(); }
    }

    // ---- architecture in the back walls --------------------------------------------------------------
    public static void PaintArchitecture(Canvas c, RoomDef r, Func<int, int, bool> solidAt)
    {
        if (r.Outdoor) return;
        var th = ThemeFor(r); var pal = TileArt.PAL[r.Region];
        char At(int lx, int ly) => (lx < 0 || ly < 0 || lx >= r.Tw || ly >= r.Th) ? '#' : r.Live[ly * r.Tw + lx];
        bool Open(int lx, int ly) => !solidAt(lx, ly);   // windows sit inside the arches, so they count as open
        switch (th.arch)
        {
            case "arcade": Arcade(c, r, pal, solidAt, Open); break;
            case "timber": Timber(c, r, pal, solidAt, Open); break;
            case "ribs": Ribs(c, r, pal, Open); break;
            case "flesh": Flesh(c, r, pal, Open); break;
        }
    }
    // Each long floor run gets a row of pointed arches with columns between them, as tall as the hall allows.
    static void Arcade(Canvas c, RoomDef r, TileArt.Pal pal, Func<int, int, bool> solidAt, Func<int, int, bool> open)
    {
        var dark = Mul(pal.back, 0.5f); var light = Mul(pal.stone, 0.95f); var mid = Mul(pal.stone, 0.75f);
        for (int ly = 2; ly < r.Th - 1; ly++)
        {
            int lx = 1;
            while (lx < r.Tw - 1)
            {
                if (!(open(lx, ly) && solidAt(lx, ly + 1))) { lx++; continue; }
                int start = lx; while (lx < r.Tw - 1 && open(lx, ly) && solidAt(lx, ly + 1)) lx++;
                int len = lx - start;
                if (len < 7) continue;
                int bays = Math.Max(1, len / 7); float bw = len * TS / (float)bays;
                for (int b = 0; b < bays; b++)
                {
                    float x0 = r.Px + start * TS + b * bw, x1 = x0 + bw, fy = r.Py + (ly + 1) * TS;
                    // headroom over this bay
                    int hmin = 99;
                    for (int tx = start + (int)(b * bw / TS); tx < start + (int)((b + 1) * bw / TS); tx++)
                    { int hgt = 0; while (hgt < 12 && open(tx, ly - hgt)) hgt++; hmin = Math.Min(hmin, hgt); }
                    if (hmin < 5) continue;
                    float archH = Math.Min(hmin - 1, 9) * TS, top = fy - archH;
                    float inset = 5, cx = (x0 + x1) / 2, spring = top + (x1 - x0) * 0.42f;
                    // recess
                    c.Fill = new Color(dark.R, dark.G, dark.B, 0.55f);
                    c.BeginPath(); c.MoveTo(x0 + inset, fy); c.LineTo(x0 + inset, spring); c.QuadraticCurveTo(x0 + inset, top + 4, cx, top); c.QuadraticCurveTo(x1 - inset, top + 4, x1 - inset, spring); c.LineTo(x1 - inset, fy); c.ClosePath(); c.FillPath();
                    // moulding around the arch
                    c.Stroke = light; c.LineWidth = 1.6f;
                    c.BeginPath(); c.MoveTo(x0 + inset - 1, spring); c.QuadraticCurveTo(x0 + inset - 1, top + 3, cx, top - 1.5f); c.QuadraticCurveTo(x1 - inset + 1, top + 3, x1 - inset + 1, spring); c.StrokePath();
                    c.Stroke = new Color(0, 0, 0, 0.4f); c.LineWidth = 1;
                    c.BeginPath(); c.MoveTo(x0 + inset + 1, spring); c.QuadraticCurveTo(x0 + inset + 1, top + 5, cx, top + 1.5f); c.QuadraticCurveTo(x1 - inset - 1, top + 5, x1 - inset - 1, spring); c.StrokePath();
                    // keystone
                    c.Fill = light; c.FillRect(cx - 2, top - 3, 4, 4);
                    // a slit window in tall bays, a moonlit lancet
                    if (archH >= 7 * TS)
                    {
                        float wy = top + 14, wh = archH * 0.35f;
                        c.FillRectGradV(cx - 2.5f, wy, 5, wh, new Color(0.25f, 0.32f, 0.42f, 0.8f), new Color(0.12f, 0.16f, 0.22f, 0.8f));
                        c.Fill = new Color(0.25f, 0.32f, 0.42f, 0.8f); c.BeginPath(); c.Arc(cx, wy, 2.5f, H.PI, H.PI * 2); c.FillPath();
                        c.Fill = new Color(0, 0, 0, 0.6f); c.FillRect(cx - 0.4f, wy - 2, 0.8f, wh + 2);
                    }
                    // columns at the bay edges
                    foreach (float colx in new[] { x0, x1 })
                    {
                        if (colx <= r.Px + TS || colx >= r.Px + r.Pw - TS) continue;
                        c.FillRectGradH(colx - 3.5f, top + 6, 7, fy - top - 6, light, Mul(pal.stone, 0.55f));
                        c.Fill = new Color(0, 0, 0, 0.18f); c.FillRect(colx + 1, top + 6, 2.5f, fy - top - 6);
                        c.Fill = light; c.FillRect(colx - 5, top + 4, 10, 2.5f); c.FillRect(colx - 5, fy - 3, 10, 3);
                        c.Fill = mid; c.FillRect(colx - 4, top + 6.5f, 8, 1.2f);
                    }
                }
            }
        }
    }
    // Damp and age on the back walls: broad stains, streaks running down from every ceiling and ledge,
    // a few patches of brighter stone where the facing has fallen away.
    public static void PaintWallWear(Canvas c, RoomDef r, Func<int, int, bool> solidAt)
    {
        if (r.Outdoor) return;
        var pal = TileArt.PAL[r.Region];
        for (int i = 0; i < r.Tw * r.Th / 14; i++)
        {
            float x = r.Px + TileArt.Hash(i, 41) * r.Pw, y = r.Py + TileArt.Hash(i, 43) * r.Ph, s = 20 + TileArt.Hash(i, 47) * 60;
            if (TileArt.Hash(i, 53) < 0.7f) c.Glow(x, y, s, s * (0.6f + TileArt.Hash(i, 59) * 0.8f), new Color(0, 0, 0, 0.22f));
            else c.Glow(x, y, s * 0.6f, s * 0.4f, Mul(pal.back, 1.9f, 0.18f));
        }
        for (int ly = 1; ly < r.Th; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                if (solidAt(lx, ly) || !solidAt(lx, ly - 1)) continue;
                float h = TileArt.Hash(r.Ox + lx, r.Oy + ly * 7);
                if (h < 0.45f) continue;
                float x = r.Px + lx * TS + h * 12, y = r.Py + ly * TS, len = 14 + h * 50;
                c.FillRectGradV(x, y, 2 + h * 3, len, new Color(0, 0, 0, 0.3f), new Color(0, 0, 0, 0));
                if (r.Region == "saltworks") c.FillRectGradV(x + 1, y, 1, len * 0.6f, new Color(0.9f, 0.9f, 0.92f, 0.12f), new Color(0.9f, 0.9f, 0.92f, 0));
                if (r.Region is "cloister" or "choir") c.FillRectGradV(x + 1, y, 1.5f, len * 0.4f, new Color(0.2f, 0.35f, 0.2f, 0.25f), new Color(0.2f, 0.35f, 0.2f, 0));
            }
    }

    // The Saltworks: posts, cross braces and beams, salt-stained.
    static void Timber(Canvas c, RoomDef r, TileArt.Pal pal, Func<int, int, bool> solidAt, Func<int, int, bool> open)
    {
        var wood = C("#3a2c1e"); var hi = C("#5a4430");
        for (int ly = 2; ly < r.Th - 1; ly++)
        {
            int lx = 1;
            while (lx < r.Tw - 1)
            {
                if (!(open(lx, ly) && solidAt(lx, ly + 1))) { lx++; continue; }
                int start = lx; while (lx < r.Tw - 1 && open(lx, ly) && solidAt(lx, ly + 1)) lx++;
                int len = lx - start;
                if (len < 6) continue;
                int bays = Math.Max(1, len / 6); float bw = len * TS / (float)bays;
                for (int b = 0; b <= bays; b++)
                {
                    float px = r.Px + start * TS + b * bw, fy = r.Py + (ly + 1) * TS;
                    int tx = Math.Clamp((int)((px - r.Px) / TS), 0, r.Tw - 1); int hgt = 0; while (hgt < 10 && open(tx, ly - hgt)) hgt++;
                    if (hgt < 4) continue;
                    float top = fy - Math.Min(hgt, 7) * TS;
                    c.Fill = wood; c.FillRect(px - 2.5f, top, 5, fy - top); c.Fill = hi; c.FillRect(px - 2.5f, top, 1, fy - top);
                    if (b < bays)
                    {
                        float nx = px + bw;
                        c.Fill = wood; c.FillRect(px, top, bw, 4); c.Fill = hi; c.FillRect(px, top, bw, 0.8f);
                        c.Stroke = wood; c.LineWidth = 3; c.Line(px + 2, top + 4, nx - 2, MathF.Min(fy - 2, top + bw * 0.7f));
                        c.Fill = C("#e8e8f0", 0.25f); c.FillRect(px + 4, top + 4, bw * 0.3f, 0.8f);
                    }
                    c.Fill = C("#8a8478"); c.FillCircle(px, top + 2, 0.9f);
                }
            }
        }
    }
    // The Ribvault and the Skull: the giant's bones curve through the back wall.
    static void Ribs(Canvas c, RoomDef r, TileArt.Pal pal, Func<int, int, bool> open)
    {
        var bone = C("#5e5444", 0.85f); var boneHi = C("#8a7c64", 0.7f);
        for (float x = r.Px + 40; x < r.Px + r.Pw - 20; x += 110)
        {
            float h = MathF.Min(r.Ph - 30, 300), y0 = r.Py + r.Ph - 10, y1 = y0 - h, bend = 46;
            c.Stroke = new Color(0, 0, 0, 0.35f); c.LineWidth = 12;
            c.BeginPath(); c.MoveTo(x + 3, y0); c.QuadraticCurveTo(x - bend + 3, (y0 + y1) / 2, x + 40, y1); c.StrokePath();
            c.Stroke = bone; c.LineWidth = 9;
            c.BeginPath(); c.MoveTo(x, y0); c.QuadraticCurveTo(x - bend, (y0 + y1) / 2, x + 37, y1); c.StrokePath();
            c.Stroke = boneHi; c.LineWidth = 2.5f;
            c.BeginPath(); c.MoveTo(x - 3, y0); c.QuadraticCurveTo(x - bend - 3, (y0 + y1) / 2, x + 34, y1 - 2); c.StrokePath();
        }
    }
    // The Gullet: ridged, wet, faintly alive.
    static void Flesh(Canvas c, RoomDef r, TileArt.Pal pal, Func<int, int, bool> open)
    {
        for (float x = r.Px + 10; x < r.Px + r.Pw; x += 34)
        {
            c.Stroke = C("#2a1a1e", 0.6f); c.LineWidth = 7;
            c.BeginPath(); c.MoveTo(x, r.Py); for (float y = 0; y <= r.Ph; y += 24) c.LineTo(x + MathF.Sin(y * 0.03f + x) * 8, r.Py + y); c.StrokePath();
            c.Stroke = C("#4a2a2e", 0.35f); c.LineWidth = 2;
            c.BeginPath(); c.MoveTo(x - 2, r.Py); for (float y = 0; y <= r.Ph; y += 24) c.LineTo(x - 2 + MathF.Sin(y * 0.03f + x) * 8, r.Py + y); c.StrokePath();
        }
        c.Stroke = C("#5a2a30", 0.3f); c.LineWidth = 0.8f;
        for (int i = 0; i < 24; i++)
        {
            float vx = r.Px + TileArt.Hash(i, 3) * r.Pw, vy = r.Py + TileArt.Hash(i, 9) * r.Ph;
            c.BeginPath(); c.MoveTo(vx, vy); c.QuadraticCurveTo(vx + 10, vy + 6, vx + 18, vy + 2); c.QuadraticCurveTo(vx + 24, vy, vx + 30, vy + 8); c.StrokePath();
        }
    }

    // ---- details on the masonry itself: tide band, moss, grass, salt, cracks -------------------------------
    public static void PaintSolidDetail(Canvas c, RoomDef r, Func<int, int, bool> solidAt)
    {
        char At(int lx, int ly) => (lx < 0 || ly < 0 || lx >= r.Tw || ly >= r.Th) ? '#' : r.Live[ly * r.Tw + lx];
        bool tidal = r.Row == 4 && r.FixedWater == null;
        float hiLine = (Tide.SEA_HIGH - Tide.spring) * TS, loLine = (Tide.SEA_LOW - Tide.spring) * TS;
        string reg = r.Region;
        for (int ly = 0; ly < r.Th; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                char ch = At(lx, ly);
                if (ch is not ('#' or 'G' or 'M')) continue;
                bool up = !solidAt(lx, ly - 1), down = !solidAt(lx, ly + 1), left = !solidAt(lx - 1, ly), right = !solidAt(lx + 1, ly);
                if (!(up || down || left || right)) continue;
                float x = r.Px + lx * TS, y = r.Py + ly * TS; int tx = r.Ox + lx, ty = r.Oy + ly;
                float h = TileArt.Hash(tx * 3, ty * 5);
                // the tide band: everything the sea covers at high water is darker, greener, crusted
                if (tidal && y + TS > hiLine)
                {
                    float wet = Math.Clamp((y + TS - hiLine) / TS, 0, 1);
                    c.Fill = new Color(0.02f, 0.06f, 0.06f, 0.32f * wet); c.FillRect(x, y, TS, TS);
                    if (y < hiLine + 1 && y + TS > hiLine) { c.Fill = new Color(0.85f, 0.88f, 0.86f, 0.22f); c.FillRect(x, hiLine - 0.5f, TS, 1); }
                    if (h > 0.45f && (left || right || up))
                        for (int i = 0; i < 3; i++)
                        {
                            float bx = x + Hsh(h, i) * 14 + 1, by = y + Hsh(h, i + 5) * 14 + 1;
                            c.Fill = C("#a8a49a", 0.85f); c.FillCircle(bx, by, 1.2f); c.Fill = C("#3a3832", 0.9f); c.FillCircle(bx, by, 0.45f);
                        }
                    if (y + TS > loLine && h > 0.3f) { c.Fill = C("#2e5a3a", 0.55f); c.FillRect(x + h * 8, y + 2, 5, 1.2f); c.FillRect(x + Hsh(h, 2) * 12, y + 9, 3, 1); }
                }
                // tops
                if (up)
                {
                    if (r.Outdoor && !(tidal && y >= loLine))
                    {
                        // sea grass and thrift on the rocks
                        for (int i = 0; i < 5; i++)
                        {
                            float gx = x + 1 + i * 3 + Hsh(h, i) * 2, gh = 2 + Hsh(h, i + 4) * 4;
                            c.Stroke = i % 2 == 0 ? C("#4a6a3a", 0.95f) : C("#6a7a44", 0.9f); c.LineWidth = 0.8f;
                            c.Line(gx, y + 0.5f, gx + (Hsh(h, i + 9) - 0.5f) * 2, y - gh);
                        }
                        if (h > 0.82f) { c.Fill = C("#d890a8"); c.FillCircle(x + 6 + h * 4, y - 3.5f, 1); c.FillCircle(x + 8 + h * 4, y - 2.8f, 0.8f); }
                    }
                    else if (reg == "cloister" || reg == "gullet") { c.Fill = reg == "gullet" ? C("#3a2a2e", 0.7f) : C("#3e5a36", 0.75f); c.FillRect(x, y - 0.5f, TS, 1.6f); if (h > 0.5f) { c.FillRect(x + h * 10, y + 1, 4, 1.4f); } }
                    else if (reg == "saltworks" && h > 0.35f) { c.Fill = C("#e6e6ea", 0.6f); c.FillRect(x + h * 6, y - 1, 6, 1.4f); c.FillRect(x + h * 6 + 2, y - 2, 2, 1); }
                    else if (h > 0.6f && !r.Outdoor) { c.Fill = Mul(TileArt.PAL[reg].stone, 0.9f); c.FillRect(x + h * 12, y - 1.5f, 2, 1.5f); c.FillRect(x + h * 12 + 3, y - 1, 1.2f, 1); }
                }
                // undersides drip moss in the damp regions
                if (down && (reg is "cloister" or "choir" or "gullet" || (r.Outdoor && h > 0.4f)))
                {
                    c.Fill = reg == "gullet" ? C("#3a222a", 0.7f) : C("#2e4a30", 0.7f);
                    for (int i = 0; i < 3; i++) { float mx = x + 2 + i * 5 + Hsh(h, i) * 2, ml = 1.5f + Hsh(h, i + 3) * 3.5f; c.FillRect(mx, y + TS, 1.2f, ml); }
                }
                // cracks and missing stones on exposed faces
                if ((left || right) && h > 0.86f)
                {
                    c.Stroke = new Color(0, 0, 0, 0.45f); c.LineWidth = 0.6f;
                    float sx = left ? x + 1 : x + TS - 1;
                    c.BeginPath(); c.MoveTo(sx, y + 3); c.LineTo(sx + (left ? 4 : -4), y + 7); c.LineTo(sx + (left ? 3 : -3), y + 12); c.StrokePath();
                }
                if (up && h < 0.08f) { c.Fill = new Color(0, 0, 0, 0.45f); c.FillRect(x + 4, y, 6, 3); }
            }
    }

    // ---- moving and burning things, drawn each frame ------------------------------------------------------
    public static void PaintDynamic(Canvas c, List<DecorItem> items, float t, Rect2 view)
    {
        foreach (var d in items)
        {
            if (d.x < view.Position.X - 40 || d.x > view.End.X + 40 || d.y < view.Position.Y - 80 || d.y > view.End.Y + 80) continue;
            switch (d.kind)
            {
                case "kelp":
                case "roots":
                case "ivy_hang":
                    {
                        bool wet = World.IsWater(d.x, d.y + d.len * 0.5f);
                        float sway = MathF.Sin(t * (wet ? 1.1f : 0.7f) + d.seed * 10) * (wet ? 3.5f : 1.5f);
                        var col = d.kind == "kelp" ? C("#2f5a3a", 0.9f) : d.kind == "roots" ? C("#3a2e22", 0.9f) : C("#3a5a30", 0.9f);
                        int segs = 6;
                        c.Stroke = col; c.LineWidth = d.kind == "kelp" ? 1.6f : 1;
                        c.BeginPath(); c.MoveTo(d.x, d.y);
                        for (int i = 1; i <= segs; i++) { float k = i / (float)segs; c.LineTo(d.x + sway * k * k + MathF.Sin(k * 6 + d.seed * 7) * 1.2f, d.y + d.len * k); }
                        c.StrokePath();
                        if (d.kind != "roots")
                            for (int i = 2; i <= segs; i += 2)
                            {
                                float k = i / (float)segs, lx = d.x + sway * k * k + MathF.Sin(k * 6 + d.seed * 7) * 1.2f, ly = d.y + d.len * k;
                                c.Fill = col; c.BeginPath(); c.Ellipse(lx + (i % 4 == 0 ? 1.5f : -1.5f), ly, 1.8f, 0.9f, i % 4 == 0 ? 0.6f : -0.6f, 0, H.PI * 2); c.FillPath();
                            }
                        break;
                    }
                case "kelp_up":
                    {
                        bool wet = World.IsWater(d.x, d.y - 4);
                        float sway = MathF.Sin(t * (wet ? 1.3f : 0.5f) + d.seed * 10) * (wet ? 4 : 0.8f);
                        var col = d.seed > 0.5f ? C("#2f5a3a", 0.92f) : C("#4a5a26", 0.9f);
                        c.Stroke = col; c.LineWidth = 1.5f;
                        c.BeginPath(); c.MoveTo(d.x, d.y);
                        for (int i = 1; i <= 6; i++) { float k = i / 6f; c.LineTo(d.x + sway * k * k + MathF.Sin(k * 5 + d.seed * 9) * 1.5f, d.y - d.len * k); }
                        c.StrokePath();
                        float tx = d.x + sway, ty = d.y - d.len;
                        c.Fill = col; c.BeginPath(); c.Ellipse(tx, ty, 1.4f, 2.6f, 0.3f, 0, H.PI * 2); c.FillPath();
                        break;
                    }
                case "chain":
                case "lamp":
                case "censer":
                case "hook":
                case "rope_hang":
                    {
                        float sway = MathF.Sin(t * 0.9f + d.seed * 10) * 0.05f;
                        float ex = d.x + MathF.Sin(sway) * d.len, ey = d.y + MathF.Cos(sway) * d.len;
                        if (d.kind == "rope_hang") { c.Stroke = C("#6a5438"); c.LineWidth = 1.2f; c.Line(d.x, d.y, ex, ey); c.Fill = C("#6a5438"); c.FillRect(ex - 1, ey - 1, 2, 3); break; }
                        int links = (int)(d.len / 3);
                        for (int i = 0; i < links; i++)
                        {
                            float k = (i + 0.5f) / links, lx = H.Lerp(d.x, ex, k), ly = H.Lerp(d.y, ey, k);
                            c.Stroke = C("#3a3632"); c.LineWidth = 0.8f;
                            if (i % 2 == 0) { c.BeginPath(); c.Ellipse(lx, ly, 0.9f, 1.6f, sway, 0, H.PI * 2); c.StrokePath(); }
                            else c.Line(lx - 0.2f, ly - 1.4f, lx - 0.2f, ly + 1.4f);
                        }
                        if (d.kind == "lamp")
                        {
                            c.Fill = C("#2a2622"); c.FillRect(ex - 3, ey, 6, 1.4f); c.FillRect(ex - 2.5f, ey + 7, 5, 1.4f);
                            c.Fill = C("#ffd27a", 0.9f); c.FillRect(ex - 2.2f, ey + 1.4f, 4.4f, 5.6f);
                            c.Fill = C("#2a2622"); c.FillRect(ex - 0.3f, ey + 1.4f, 0.6f, 5.6f);
                        }
                        else if (d.kind == "censer")
                        {
                            c.Fill = C("#6a5420"); c.BeginPath(); c.Arc(ex, ey + 3, 3, 0, H.PI); c.ClosePath(); c.FillPath();
                            c.Fill = C("#8a7030"); c.BeginPath(); c.Arc(ex, ey + 3, 3, H.PI, H.PI * 2); c.FillPath();
                            for (int i = 0; i < 3; i++) { float sp = (t * 0.4f + i / 3f + d.seed) % 1; c.Fill = new Color(0.7f, 0.7f, 0.68f, 0.18f * (1 - sp)); c.FillCircle(ex + MathF.Sin(sp * 6 + i) * 3, ey - sp * 22, 1.5f + sp * 3); }
                        }
                        else if (d.kind == "hook") { c.Stroke = C("#4a4642"); c.LineWidth = 1.1f; c.BeginPath(); c.Arc(ex, ey + 2, 2.2f, -H.PI / 2, H.PI * 0.9f); c.StrokePath(); }
                        break;
                    }
            }
        }
    }

    // Flames and glowing things: additive, drawn above the world.
    public static void PaintGlow(Canvas c, List<DecorItem> items, float t, Rect2 view)
    {
        foreach (var d in items)
        {
            if (!d.lit) continue;
            if (d.x < view.Position.X - 60 || d.x > view.End.X + 60 || d.y < view.Position.Y - 80 || d.y > view.End.Y + 80) continue;
            float fl = Flicker(t, d.seed);
            switch (d.kind)
            {
                case "candles":
                    {
                        int n = 2 + (int)(d.seed * 3);
                        for (int i = 0; i < n; i++)
                        {
                            float cx = d.x - 5 + i * (10f / Math.Max(1, n - 1)), hh = 4 + Hsh(d.seed, i) * 6;
                            Flame(c, cx, d.y - hh - 1, 1.6f, t, d.seed + i);
                        }
                        c.Glow(d.x, d.y - 8, 18 * fl, new Color(1f, 0.6f, 0.25f, 0.12f));
                        break;
                    }
                case "candelabra":
                    foreach (float ox in new[] { -5f, 0f, 5f }) Flame(c, d.x + ox, d.y - 26 + (ox == 0 ? -2 : 0), 1.7f, t, d.seed + ox);
                    c.Glow(d.x, d.y - 26, 20 * fl, new Color(1f, 0.6f, 0.25f, 0.14f));
                    break;
                case "sconce":
                    Flame(c, d.x + 3, d.y - 7, 3, t, d.seed);
                    c.Glow(d.x + 3, d.y - 9, 26 * fl, new Color(1f, 0.5f, 0.2f, 0.16f));
                    break;
                case "lamp":
                    {
                        float sway = MathF.Sin(t * 0.9f + d.seed * 10) * 0.05f;
                        float ex = d.x + MathF.Sin(sway) * d.len, ey = d.y + MathF.Cos(sway) * d.len;
                        c.Glow(ex, ey + 4, 6, new Color(1.5f, 1.1f, 0.6f, 0.8f));
                        c.Glow(ex, ey + 4, 22 * fl, new Color(1f, 0.65f, 0.3f, 0.12f));
                        break;
                    }
                case "nodules":
                case "nodules_wall":
                    {
                        float pulse = 0.6f + 0.4f * MathF.Sin(t * 1.3f + d.seed * 20);
                        c.Glow(d.x, d.y - 3, 14, new Color(0.3f, 1.1f, 1f, 0.16f * pulse));
                        for (int i = 0; i < 4; i++) c.Glow(d.x - 5 + Hsh(d.seed, i) * 10, d.y - 2, 2.5f, new Color(0.6f, 1.6f, 1.4f, 0.6f * pulse));
                        break;
                    }
                case "crystals":
                    {
                        float tw = MathF.Max(0, MathF.Sin(t * 1.7f + d.seed * 30));
                        c.Glow(d.x, d.y - 5, 9, new Color(0.7f, 0.95f, 1.2f, 0.08f + 0.1f * tw));
                        if (tw > 0.93f) c.Glow(d.x - 2 + d.seed * 4, d.y - 7, 2, new Color(1.6f, 1.7f, 1.8f, 0.9f));
                        break;
                    }
            }
        }
    }
    static float Flicker(float t, float seed) => 0.88f + 0.08f * MathF.Sin(t * 13 + seed * 50) + 0.05f * MathF.Sin(t * 29 + seed * 17);
    static void Flame(Canvas c, float x, float y, float s, float t, float seed)
    {
        float wob = MathF.Sin(t * 14 + seed * 40) * 0.35f * s, hgt = s * (2.4f + 0.5f * MathF.Sin(t * 19 + seed * 13));
        c.Fill = new Color(1.0f, 0.42f, 0.12f, 0.75f);
        c.BeginPath(); c.MoveTo(x - s * 0.7f, y); c.QuadraticCurveTo(x - s * 0.6f, y - hgt * 0.6f, x + wob, y - hgt); c.QuadraticCurveTo(x + s * 0.6f, y - hgt * 0.6f, x + s * 0.7f, y); c.ClosePath(); c.FillPath();
        c.Fill = new Color(1.6f, 1.2f, 0.6f, 0.9f);
        c.BeginPath(); c.MoveTo(x - s * 0.35f, y); c.QuadraticCurveTo(x - s * 0.3f, y - hgt * 0.45f, x + wob * 0.6f, y - hgt * 0.7f); c.QuadraticCurveTo(x + s * 0.3f, y - hgt * 0.45f, x + s * 0.35f, y); c.ClosePath(); c.FillPath();
        c.Glow(x, y - hgt * 0.4f, s * 2.4f, new Color(1.4f, 0.8f, 0.35f, 0.5f));
    }

    // Real lights for the brightest of them; the renderer adds these to its pool.
    public static IEnumerable<LightSrc> Lights(List<DecorItem> items, float t, Rect2 view)
    {
        foreach (var d in items)
        {
            if (!d.lit || d.x < view.Position.X - 120 || d.x > view.End.X + 120 || d.y < view.Position.Y - 120 || d.y > view.End.Y + 120) continue;
            float fl = Flicker(t, d.seed);
            switch (d.kind)
            {
                case "sconce": yield return new LightSrc { x = d.x + 3, y = d.y - 9, r = 92 * fl, i = 0.85f * fl, color = "255,150,70" }; break;
                case "candelabra": yield return new LightSrc { x = d.x, y = d.y - 26, r = 70 * fl, i = 0.7f * fl, color = "255,170,90" }; break;
                case "candles": yield return new LightSrc { x = d.x, y = d.y - 8, r = 48 * fl, i = 0.55f * fl, color = "255,175,95" }; break;
                case "lamp": yield return new LightSrc { x = d.x, y = d.y + d.len + 4, r = 80 * fl, i = 0.7f * fl, color = "255,185,110" }; break;
                case "nodules": case "nodules_wall": yield return new LightSrc { x = d.x, y = d.y - 3, r = 46, i = 0.55f + 0.2f * MathF.Sin(t * 1.3f + d.seed * 20), color = "90,230,210" }; break;
            }
        }
    }
}
