// The playable rock, drawn as Hollow Knight draws its ground: a dark silhouette with a lit lip, no grid in
// sight. Tiles are read on a dual grid (each cell's corners are tile centres), so every outer corner can
// be rounded and every inner corner filleted while the edges stay on the collision lines. The mass darkens
// away from its edges, the edges carry a rim that is brightest where it faces up, and the surfaces grow
// grass, moss, salt, roots and stalactites by region. Everything is drawn in encoded value/accent/glow
// for the palette shader.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public static class Terrain
{
    const int TS = H.TILE;
    const float RC = 4.5f, RV = 3f;   // outer corner radius, inner fillet radius

    public static bool IsMass(char ch) => ch is '#' or 'G' or 'M';

    // Chebyshev distance from each tile to the nearest tile where target holds, within the room and a
    // border of neighbouring tiles. Returns (Tw x Th), capped at max.
    public static int[] Distance(RoomDef r, Func<int, int, bool> target, int max)
    {
        int B = max + 1, W = r.Tw + 2 * B, Hh = r.Th + 2 * B;
        var d = new int[W * Hh];
        var q = new Queue<int>();
        for (int y = 0; y < Hh; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (target(x - B, y - B)) { d[i] = 0; q.Enqueue(i); } else d[i] = int.MaxValue;
            }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % W, y = i / W;
            if (d[i] >= max) continue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= Hh) continue;
                    int j = ny * W + nx;
                    if (d[j] > d[i] + 1) { d[j] = d[i] + 1; q.Enqueue(j); }
                }
        }
        var res = new int[r.Tw * r.Th];
        for (int y = 0; y < r.Th; y++) for (int x = 0; x < r.Tw; x++) res[y * r.Tw + x] = Math.Min(max, d[(y + B) * W + x + B]);
        return res;
    }

    static float ValueAt(int d) => d <= 0 ? 0.15f : d == 1 ? 0.15f : d == 2 ? 0.095f : d == 3 ? 0.06f : 0.035f;

    public static void Paint(Canvas c, RoomDef r, Func<int, int, char> at)
    {
        bool Mass(int x, int y) => IsMass(at(x, y));
        bool Solidish(int x, int y) { char ch = at(x, y); return ch != '_' && TileArt.IsSolidCh(ch); }
        var inner = Distance(r, (x, y) => !Mass(x, y), 5);
        float Val(int x, int y)
        {
            if (!Mass(x, y)) return 0.15f;
            if (x < 0 || y < 0 || x >= r.Tw || y >= r.Th) return 0.06f;
            return ValueAt(inner[y * r.Tw + x]);
        }
        var rims = new List<(Vector2 a, Vector2 b, Vector2 n)>();

        for (int cy = -1; cy < r.Th; cy++)
            for (int cx = -1; cx < r.Tw; cx++)
            {
                bool tl = Mass(cx, cy), tr = Mass(cx + 1, cy), bl = Mass(cx, cy + 1), br = Mass(cx + 1, cy + 1);
                if (!(tl || tr || bl || br)) continue;
                float ox = r.Px + cx * TS + 8, oy = r.Py + cy * TS + 8;
                float vtl = Val(cx, cy), vtr = Val(cx + 1, cy), vbl = Val(cx, cy + 1), vbr = Val(cx + 1, cy + 1);
                Color Col(Vector2 p)
                {
                    float u = Math.Clamp((p.X - ox) / TS, 0, 1), w = Math.Clamp((p.Y - oy) / TS, 0, 1);
                    return Canvas.V(H.Lerp(H.Lerp(vtl, vtr, u), H.Lerp(vbl, vbr, u), w));
                }
                void Fill(List<Vector2> pts)
                {
                    var arr = pts.ToArray(); var cols = new Color[arr.Length];
                    for (int i = 0; i < arr.Length; i++) cols[i] = Col(arr[i]);
                    c.FillPolyGrad(arr, cols);
                }
                if (tl && tr && bl && br)
                {
                    Fill(new List<Vector2> { new(ox, oy), new(ox + TS, oy), new(ox + TS, oy + TS), new(ox, oy + TS) });
                    continue;
                }
                // solidity for shaping includes timber and doors, so rock meets them without a gap
                bool[] mass = { tl, tr, bl, br };
                bool[] sol = { Solidish(cx, cy), Solidish(cx + 1, cy), Solidish(cx, cy + 1), Solidish(cx + 1, cy + 1) };
                var O = new Vector2(ox + 8, oy + 8);
                Vector2[] dq = { new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
                int[] hn = { 1, 0, 3, 2 }, vn = { 2, 3, 0, 1 };
                bool[] convex = new bool[4], fillet = new bool[4];
                for (int q = 0; q < 4; q++)
                {
                    convex[q] = mass[q] && !sol[hn[q]] && !sol[vn[q]];
                    fillet[q] = !sol[q] && mass[hn[q]] && mass[vn[q]];
                }
                for (int q = 0; q < 4; q++)
                {
                    var d = dq[q];
                    if (mass[q])
                    {
                        var A = O + new Vector2(d.X * 8, 0); var B = O + new Vector2(d.X * 8, d.Y * 8); var D = O + new Vector2(0, d.Y * 8);
                        var pts = new List<Vector2> { A, B, D };
                        if (convex[q]) pts.AddRange(Arc(O + d * RC, RC, new Vector2(-d.X, 0), new Vector2(0, -d.Y)));
                        else pts.Add(O);
                        Fill(pts);
                    }
                    else if (fillet[q])
                    {
                        var pts = new List<Vector2> { O };
                        pts.AddRange(Arc(O + d * RV, RV, new Vector2(-d.X, 0), new Vector2(0, -d.Y)));
                        Fill(pts);
                    }
                }
                // rims along the half-lines through O, trimmed where a corner was rounded or filleted
                void HalfLine(int a, int b, Vector2 dir)
                {
                    if (mass[a] == mass[b]) return;
                    int s = mass[a] ? a : b, o = mass[a] ? b : a;
                    if (sol[o]) return;   // rock against timber or a door: no lip
                    var n = (dq[o] - dq[s]) * 0.5f;   // from the solid quadrant toward the air one
                    float trim = convex[s] ? RC : fillet[o] ? RV : 0;
                    rims.Add((O + dir * trim, O + dir * 8, n));
                }
                HalfLine(0, 1, new Vector2(0, -1)); HalfLine(2, 3, new Vector2(0, 1));
                HalfLine(0, 2, new Vector2(-1, 0)); HalfLine(1, 3, new Vector2(1, 0));
                for (int q = 0; q < 4; q++)
                {
                    var d = dq[q];
                    if (convex[q]) { var arc = Arc(O + d * RC, RC, new Vector2(0, -d.Y), new Vector2(-d.X, 0)); for (int i = 0; i + 1 < arc.Count; i++) rims.Add((arc[i], arc[i + 1], (arc[i] + arc[i + 1]) * 0.5f - (O + d * RC))); }
                    if (fillet[q]) { var arc = Arc(O + d * RV, RV, new Vector2(-d.X, 0), new Vector2(0, -d.Y)); for (int i = 0; i + 1 < arc.Count; i++) rims.Add((arc[i], arc[i + 1], (O + d * RV) - (arc[i] + arc[i + 1]) * 0.5f)); }
                }
            }

        Texture(c, r, at, inner);
        Rims(c, rims);
        Growth(c, r, at, Mass);
    }

    // Points along a quarter circle from direction a to direction b (unit axis vectors) about centre.
    static List<Vector2> Arc(Vector2 centre, float rad, Vector2 a, Vector2 b)
    {
        float a0 = MathF.Atan2(a.Y, a.X), a1 = MathF.Atan2(b.Y, b.X);
        float da = a1 - a0; while (da > H.PI) da -= H.PI * 2; while (da < -H.PI) da += H.PI * 2;
        var pts = new List<Vector2>();
        for (int i = 0; i <= 5; i++) { float t = a0 + da * i / 5; pts.Add(centre + new Vector2(MathF.Cos(t), MathF.Sin(t)) * rad); }
        return pts;
    }

    // The lip: bright and doubled where a surface faces up, quieter on walls, faint underneath.
    static void Rims(Canvas c, List<(Vector2 a, Vector2 b, Vector2 n)> rims)
    {
        foreach (var (a, b, n0) in rims)
        {
            var n = n0.Length() > 0.001f ? n0.Normalized() : Vector2.Up;
            float up = Math.Clamp(-n.Y, 0, 1), down = Math.Clamp(n.Y, 0, 1);
            var inward = -n;
            if (up > 0.3f)
            {
                c.Stroke = Canvas.V(0.32f, 0.9f); c.LineWidth = 2.2f; c.Line(a.X + inward.X * 1.6f, a.Y + inward.Y * 1.6f, b.X + inward.X * 1.6f, b.Y + inward.Y * 1.6f);
                c.Stroke = Canvas.VA(0.78f * up + 0.3f * (1 - up), 0.35f * up); c.LineWidth = 1.2f; c.Line(a.X + inward.X * 0.5f, a.Y + inward.Y * 0.5f, b.X + inward.X * 0.5f, b.Y + inward.Y * 0.5f);
            }
            else if (down > 0.6f) { c.Stroke = Canvas.V(0.22f, 0.8f); c.LineWidth = 0.8f; c.Line(a.X + inward.X * 0.4f, a.Y + inward.Y * 0.4f, b.X + inward.X * 0.4f, b.Y + inward.Y * 0.4f); }
            else { c.Stroke = Canvas.VA(0.36f, 0.1f, 0.9f); c.LineWidth = 0.9f; c.Line(a.X + inward.X * 0.45f, a.Y + inward.Y * 0.45f, b.X + inward.X * 0.45f, b.Y + inward.Y * 0.45f); }
        }
    }

    // Quiet texture inside the mass near its faces: embedded stones, cracks, bedding lines, grates, brick.
    static void Texture(Canvas c, RoomDef r, Func<int, int, char> at, int[] inner)
    {
        bool layered = r.Outdoor || r.Region is "wrackline" or "ribvault" or "gullet";
        for (int ly = 0; ly < r.Th; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                char ch = at(lx, ly);
                if (!IsMass(ch)) continue;
                int d = inner[ly * r.Tw + lx];
                if (d > 3) continue;
                float x = r.Px + lx * TS, y = r.Py + ly * TS, h = TileArt.Hash(r.Ox + lx, r.Oy + ly);
                float fade = d == 1 ? 1 : d == 2 ? 0.6f : 0.3f;
                if (ch == 'G')
                {
                    c.Fill = Canvas.V(0.0f, 0.9f * fade); for (int i = 2; i < TS; i += 4) c.FillRect(x + i, y + 2, 1.6f, TS - 4);
                    c.Fill = Canvas.V(0.22f, 0.6f * fade); c.FillRect(x + 1, y + 1, TS - 2, 0.8f);
                    continue;
                }
                if (ch == 'M')
                {
                    c.Fill = Canvas.V(0.02f, 0.7f * fade);
                    for (int row = 0; row < 3; row++) { c.FillRect(x, y + 5 + row * 5, TS, 0.6f); c.FillRect(x + ((row + lx) % 2) * 8 + 3, y + row * 5, 0.6f, 5); }
                    if (h > 0.6f) { c.Fill = Canvas.VA(0.22f, 0.5f, 0.5f * fade); c.FillRect(x + h * 8, y + 7, 4, 3); }
                    continue;
                }
                if (layered)
                {
                    c.Stroke = Canvas.V(0.2f, 0.45f * fade); c.LineWidth = 0.6f;
                    float yy = y + 4 + ((r.Oy + ly) * 7 % 9);
                    c.BeginPath(); c.MoveTo(x, yy + MathF.Sin((r.Ox + lx) * 1.3f) * 1.2f); c.QuadraticCurveTo(x + 8, yy + MathF.Sin((r.Ox + lx) * 2.1f) * 2, x + TS, yy + MathF.Sin((r.Ox + lx + 1) * 1.3f) * 1.2f); c.StrokePath();
                }
                if (h > 0.62f)
                {
                    // an embedded stone, its top catching a little light
                    float sx = x + 3 + h * 8, sy = y + 4 + TileArt.Hash(lx, ly * 3) * 8, sw = 2.5f + h * 3, sh = 1.8f + h * 1.6f;
                    c.Fill = Canvas.V(0.2f, 0.7f * fade); c.BeginPath(); c.Ellipse(sx, sy, sw, sh, 0, 0, H.PI * 2); c.FillPath();
                    c.Fill = Canvas.V(0.05f, 0.8f * fade); c.BeginPath(); c.Ellipse(sx + 0.4f, sy + 0.5f, sw * 0.8f, sh * 0.7f, 0, 0, H.PI * 2); c.FillPath();
                }
                else if (h < 0.25f)
                {
                    c.Stroke = Canvas.V(0.0f, 0.8f * fade); c.LineWidth = 0.7f;
                    float sx = x + 4 + h * 30;
                    c.BeginPath(); c.MoveTo(sx, y + 2); c.LineTo(sx + 2, y + 7); c.LineTo(sx + 0.5f, y + 12); c.StrokePath();
                }
            }
    }

    // What grows on and hangs from the surfaces, by region.
    static void Growth(Canvas c, RoomDef r, Func<int, int, char> at, Func<int, int, bool> mass)
    {
        string reg = r.Outdoor ? "outdoor" : r.Region;
        bool tidal = r.Tidal && r.FixedWater == null;
        float hiLine = (Tide.SEA_HIGH - Tide.spring) * TS, loLine = (Tide.SEA_LOW - Tide.spring) * TS;
        bool Air(int x, int y) { char ch = at(x, y); return !TileArt.IsSolidCh(ch) || ch == '_'; }
        for (int ly = 0; ly < r.Th; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                if (!mass(lx, ly)) continue;
                float x = r.Px + lx * TS, y = r.Py + ly * TS; float h = TileArt.Hash((r.Ox + lx) * 7, (r.Oy + ly) * 3);
                bool up = Air(lx, ly - 1), down = Air(lx, ly + 1), left = Air(lx - 1, ly), right = Air(lx + 1, ly);
                bool wet = tidal && y + TS > hiLine;
                // the sea's band: darker, crusted with barnacles below the high-water mark
                if (wet && (up || down || left || right))
                {
                    c.Fill = Canvas.V(0, 0.22f); c.FillRect(x, MathF.Max(y, hiLine), TS, y + TS - MathF.Max(y, hiLine));
                    if (h > 0.4f) for (int i = 0; i < 3; i++)
                        {
                            float bx = x + 2 + TileArt.Hash(lx + i, ly) * 12, by = y + 2 + TileArt.Hash(lx, ly + i) * 12;
                            c.Fill = Canvas.V(0.42f, 0.8f); c.FillCircle(bx, by, 0.9f); c.Fill = Canvas.V(0.05f); c.FillCircle(bx, by, 0.35f);
                        }
                }
                if (up)
                {
                    if (reg is "outdoor" or "cloister" or "wrackline" && !(wet && y >= loLine))
                    {
                        // grass and weed: thin blades, a few taller
                        int n = reg == "cloister" ? 6 : 4;
                        for (int i = 0; i < n; i++)
                        {
                            float gx = x + 1 + i * (14f / n) + TileArt.Hash(lx * 5 + i, ly) * 2, gh = 1.5f + TileArt.Hash(lx + i * 3, ly * 7) * (reg == "outdoor" ? 4.5f : 3.5f);
                            float lean = (TileArt.Hash(lx + i, ly + 9) - 0.5f) * 2.2f;
                            c.Fill = Canvas.VA(0.24f, 0.45f); c.Poly(new[] { new Vector2(gx - 0.6f, y + 0.6f), new Vector2(gx + lean, y - gh), new Vector2(gx + 0.6f, y + 0.6f) });
                        }
                        if (reg == "cloister") { c.Fill = Canvas.VA(0.3f, 0.6f, 0.9f); c.FillRect(x, y - 0.6f, TS, 1.6f); }
                    }
                    else if (reg == "saltworks" && h > 0.3f)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float sx = x + 2 + TileArt.Hash(lx + i, ly) * 12, sh = 1.5f + TileArt.Hash(lx, ly + i) * 2.5f;
                            c.Fill = Canvas.VA(0.85f, 0.25f); c.Poly(new[] { new Vector2(sx - 1, y + 0.5f), new Vector2(sx, y - sh), new Vector2(sx + 1, y + 0.5f) });
                        }
                    }
                    else if (reg is "ribvault" or "skull" && h > 0.55f)
                    {
                        c.Fill = Canvas.VA(0.7f, 0.3f); c.BeginPath(); c.Ellipse(x + 4 + h * 8, y - 0.2f, 2.4f, 0.9f, 0.2f, 0, H.PI * 2); c.FillPath();
                    }
                    else if (reg == "gullet" && h > 0.5f)
                    {
                        c.Fill = Canvas.VA(0.4f, 0.8f); c.FillCircle(x + 3 + h * 10, y - 0.4f, 1.1f);
                        c.Fill = Canvas.Gl(0.6f, 0.8f); c.FillCircle(x + 3 + h * 10, y - 0.6f, 0.5f);
                    }
                    // pebbles sitting on the lip, part of the silhouette
                    if (h < 0.2f) { c.Fill = Canvas.V(0.15f); c.BeginPath(); c.Ellipse(x + 3 + h * 40, y - 0.3f, 1.8f, 1.1f, 0, H.PI, H.PI * 2); c.FillPath(); }
                }
                if (down)
                {
                    // stalactites, roots and dripping moss under every overhang
                    if (h > 0.55f && reg is not "gullet")
                    {
                        float sx = x + 3 + TileArt.Hash(lx, ly * 5) * 10, len = 3 + h * 7;
                        c.Fill = Canvas.V(0.12f); c.Poly(new[] { new Vector2(sx - 2, y + TS - 0.5f), new Vector2(sx + 0.3f, y + TS + len), new Vector2(sx + 2, y + TS - 0.5f) });
                        if (reg == "saltworks") { c.Fill = Canvas.VA(0.8f, 0.2f, 0.7f); c.Poly(new[] { new Vector2(sx - 1, y + TS), new Vector2(sx + 0.3f, y + TS + len * 0.8f), new Vector2(sx, y + TS) }); }
                    }
                    if (reg is "cloister" or "choir" or "outdoor" or "gullet" && h < 0.5f)
                    {
                        c.Stroke = reg == "gullet" ? Canvas.VA(0.2f, 0.4f) : Canvas.VA(0.18f, 0.4f); c.LineWidth = 0.7f;
                        for (int i = 0; i < 3; i++)
                        {
                            float rx = x + 2 + i * 5 + TileArt.Hash(lx + i, ly) * 3, rl = 3 + TileArt.Hash(lx, ly + i * 3) * 9;
                            c.BeginPath(); c.MoveTo(rx, y + TS); c.QuadraticCurveTo(rx + 1.5f, y + TS + rl * 0.5f, rx - 0.5f, y + TS + rl); c.StrokePath();
                        }
                    }
                }
                if ((left || right) && h > 0.72f)
                {
                    // a rock bulging from the face
                    float sx = left ? x : x + TS, sy = y + 3 + TileArt.Hash(lx * 3, ly) * 10, bw = 1.5f + h * 1.5f;
                    c.Fill = Canvas.V(0.15f); c.BeginPath(); c.Ellipse(sx, sy, bw, 2.5f + h * 2, 0, 0, H.PI * 2); c.FillPath();
                    c.Stroke = Canvas.V(0.34f, 0.8f); c.LineWidth = 0.7f; c.BeginPath(); c.Arc(sx, sy, MathF.Min(bw, 2.5f), left ? H.PI * 1.1f : -H.PI * 0.4f, left ? H.PI * 1.5f : -H.PI * 0.05f); c.StrokePath();
                }
            }
    }
}
