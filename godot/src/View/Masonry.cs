// The back walls, painted the way a wall in a drowned abbey looks after four hundred years of sea and lamp smoke.
// Ashlar first: courses of different heights, every block its own stone with a face that catches the light at its
// top, chipped where the ruin has broken away. Then the weather, each mark the trace of something that happened:
// damp blooming through the stone; water that ran off every ledge and left streaks; soot above every lamp that
// burned; cracks; moss in the Cloister, salt bloom in the Saltworks, gilt in the Choir. In the rooms the tide
// reaches, the sea's own record: darker, greener stone below the high-water line, a white rind of salt along it,
// barnacles near the floor, and one faint line far higher up from the night the lighthouse went dark and the sea
// came higher than anyone remembered.
// Drawn once per room into the back-wall bake, in encoded value/accent/glow for the palette and world shaders.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Masonry
{
    const int TS = H.TILE;
    static Color V(float v, float a = 1) => Canvas.V(v, a);
    static Color VA(float v, float acc, float a = 1) => Canvas.VA(v, acc, a);
    static float R(Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // field(px, py, rough): distance from the rock (tiles, roughened) at room-local pixels; the wall stands where it
    // is below iso. rock(px, py): the same distance without the roughening, for contact shadow.
    public static void Paint(Canvas c, RoomDef r, Func<float, float, float, float> field, float iso, Func<float, float, float> rock,
        Func<int, int, bool> solid, IEnumerable<DecorItem> decor, bool works)
    {
        var rng = new Random(r.Id.Aggregate(41, (h, ch) => unchecked(h * 31 + ch)));
        bool Wall(float px, float py) => field(px, py, 1) < iso;
        int[] heights = works ? new[] { 8, 8, 9, 10 } : new[] { 10, 12, 12, 14, 16, 20 };
        float minW = works ? 13 : 18, maxW = works ? 22 : 44;

        // ---- the stones ----
        float y = 0;
        int course = 0;
        while (y < r.Ph)
        {
            float ch = heights[rng.Next(heights.Length)];
            float x = -R(rng, 0, maxW);
            while (x < r.Pw)
            {
                float bw = MathF.Round(R(rng, minW, maxW));
                float cx = x + bw / 2, cy = y + ch / 2;
                if (Wall(cx, cy))
                {
                    float h1 = TileArt.Hash(course * 131 + (int)x, (int)y + r.Ox * 7), h2 = TileArt.Hash((int)x * 3 + r.Oy, course * 17);
                    float low = Math.Clamp(y / MathF.Max(1, r.Ph), 0, 1);
                    // contact shadow where the wall meets the rock, a little dark toward the floor
                    float ao = 0.55f + 0.45f * MathF.Min(1, rock(cx, cy) / 3f);
                    float v = (0.1f + h1 * 0.06f - low * 0.02f) * ao;
                    float wx = r.Px + x, wy = r.Py + y;
                    c.Fill = V(0.035f); c.FillRect(wx, wy, bw, ch);
                    // which corners have fallen away: the ruin's edge chips the stones along it
                    bool tl = Wall(x + 2, y + 2), tr = Wall(x + bw - 2, y + 2), bl = Wall(x + 2, y + ch - 2), br = Wall(x + bw - 2, y + ch - 2);
                    float i0 = 0.8f;
                    var top = V(v + 0.03f); var bot = V(v - 0.025f);
                    if (tl && tr && bl && br) c.FillRectGradV(wx + i0, wy + i0, bw - i0 * 2, ch - i0 * 2, top, bot);
                    else
                    {
                        float k = MathF.Min(bw, ch) * 0.55f;
                        var pts = new List<Vector2>();
                        if (tl) pts.Add(new(wx + i0, wy + i0)); else { pts.Add(new(wx + i0, wy + k)); pts.Add(new(wx + k * 0.8f, wy + i0)); }
                        if (tr) pts.Add(new(wx + bw - i0, wy + i0)); else { pts.Add(new(wx + bw - k, wy + i0)); pts.Add(new(wx + bw - i0, wy + k * 0.7f)); }
                        if (br) pts.Add(new(wx + bw - i0, wy + ch - i0)); else { pts.Add(new(wx + bw - i0, wy + ch - k)); pts.Add(new(wx + bw - k * 0.9f, wy + ch - i0)); }
                        if (bl) pts.Add(new(wx + i0, wy + ch - i0)); else { pts.Add(new(wx + k, wy + ch - i0)); pts.Add(new(wx + i0, wy + ch - k * 0.8f)); }
                        c.Fill = V(v); c.Poly(pts.ToArray());
                    }
                    // the lit top of a stone nothing rests on, and the shadow under one that overhangs
                    if (!Wall(cx, y - 3)) { c.Fill = V(v + 0.14f, 0.85f); c.FillRect(wx + 1.5f, wy + i0, bw - 3, 0.8f); }
                    else { c.Fill = V(v + 0.05f, 0.5f); c.FillRect(wx + i0, wy + i0, bw - i0 * 2, 0.6f); }
                    // pitting and the odd hairline in the face
                    if (h2 > 0.55f) { c.Fill = V(v * 0.45f); c.FillRect(wx + 3 + h2 * (bw - 8), wy + 2 + h1 * (ch - 5), 1.4f, 1.1f); }
                    if (h2 < 0.12f) { c.Stroke = V(v * 0.35f); c.LineWidth = 0.5f; c.Line(wx + bw * 0.3f, wy + 1, wx + bw * 0.45f, wy + ch - 1); }
                    if (works && h1 > 0.82f) { c.Fill = V(v * 0.6f); c.FillRect(wx + bw / 2 - 1, wy + ch / 2 - 1, 2, 2); }   // the old bolt holes of the salt rakes
                }
                x += bw;
            }
            y += ch; course++;
        }

        // ---- the weather ----
        var tops = new List<(float x, float y)>();   // undersides of rock and ledges: where water runs from
        for (int ly = 0; ly < r.Th - 1; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
                if (solid(lx, ly) && !solid(lx, ly + 1)) tops.Add((lx * TS, (ly + 1) * TS));

        // damp blooming through the stone in broad soft patches
        int patches = (int)(r.Pw * r.Ph / 9000f);
        for (int i = 0; i < patches; i++)
        {
            float px = R(rng, 0, r.Pw), py = R(rng, 0, r.Ph);
            if (!Wall(px, py)) continue;
            float rad = R(rng, 18, 64);
            c.Glow(r.Px + px, r.Py + py, rad, rad * R(rng, 0.6f, 1.2f), V(0, R(rng, 0.12f, 0.26f)));
        }
        // streaks where water ran down from every ledge, for long enough to stain
        foreach (var (tx, ty) in tops)
        {
            if (rng.NextDouble() > (works ? 0.35 : 0.55)) continue;
            int n = 1 + rng.Next(3);
            for (int k = 0; k < n; k++)
            {
                float sx = tx + R(rng, 1, TS - 1), len = R(rng, 18, works ? 50 : 110), sw = R(rng, 0.8f, 2.6f);
                if (!Wall(sx, ty + 4)) continue;
                c.FillRectGradV(r.Px + sx, r.Py + ty, sw, len, V(0, R(rng, 0.22f, 0.45f)), V(0, 0));
                if (works && rng.NextDouble() < 0.4) c.FillRectGradV(r.Px + sx + sw, r.Py + ty, 0.8f, len * 0.6f, V(0.5f, 0.35f), V(0.5f, 0));   // salt left by the drip
            }
        }
        // soot above every lamp that has burned here
        if (decor != null)
            foreach (var d in decor)
            {
                if (d.kind is not ("sconce" or "lamp" or "candelabra" or "candles")) continue;
                float sx = d.kind == "sconce" ? d.x + 3 : d.x, sy = d.kind == "lamp" ? d.y + d.len : d.kind == "candles" ? d.y - 6 : d.y - 12;
                c.Glow(sx, sy - 16, 9, 26, V(0, 0.55f));
                c.Glow(sx, sy - 34, 14, 26, V(0, 0.3f));
            }
        // cracks running across the courses
        int cracks = 2 + (int)(r.Pw / 220f);
        for (int i = 0; i < cracks; i++)
        {
            float px = R(rng, 0, r.Pw), py = R(rng, 0, r.Ph * 0.8f);
            if (!Wall(px, py)) continue;
            c.Stroke = V(0.015f, 0.85f); c.LineWidth = 0.7f;
            float a = H.PI / 2 + R(rng, -0.6f, 0.6f);
            c.BeginPath(); c.MoveTo(r.Px + px, r.Py + py);
            for (int s = 0; s < 10; s++)
            {
                a += R(rng, -0.7f, 0.7f); float len = R(rng, 4, 11);
                px += MathF.Cos(a) * len; py += MathF.Sin(a) * len;
                if (!Wall(px, py)) break;
                c.LineTo(r.Px + px, r.Py + py);
            }
            c.StrokePath();
        }
        // what grows and blooms on the stone, by region
        int growth = (int)(r.Pw * r.Ph / 4000f);
        for (int i = 0; i < growth; i++)
        {
            float px = R(rng, 0, r.Pw), py = R(rng, 0, r.Ph);
            if (!Wall(px, py)) continue;
            float n = Decor.Fbm((r.Px + px) / 70f, (r.Py + py) / 70f);
            switch (r.Region)
            {
                case "cloister" when n > 0.5f:
                    c.Glow(r.Px + px, r.Py + py, R(rng, 6, 18), R(rng, 3, 8), VA(0.16f, 0.7f, 0.55f));
                    break;
                case "saltworks" when n > 0.52f && py > r.Ph * 0.45f:
                    c.Glow(r.Px + px, r.Py + py, R(rng, 5, 16), R(rng, 3, 7), V(0.55f, 0.35f));
                    break;
                case "choir" when n > 0.62f:
                    c.Fill = VA(0.3f, 0.8f, 0.4f); c.FillRect(r.Px + px, r.Py + py, R(rng, 4, 12), 0.7f);   // gilt, mostly flaked away
                    break;
                case "wrackline" when n > 0.55f && py > r.Ph * 0.5f:
                    c.Glow(r.Px + px, r.Py + py, R(rng, 6, 16), R(rng, 3, 7), VA(0.12f, 0.45f, 0.45f));
                    break;
            }
        }

        // ---- the sea's own record, in the rooms the tide reaches ----
        if (r.Tidal && r.FixedWater == null)
        {
            float hi = (Tide.SEA_HIGH - Tide.spring) * TS - r.Py;
            if (hi > 0 && hi < r.Ph)
            {
                // below the high-water mark the stone is darker and green with weed
                for (float px = 0; px < r.Pw; px += 8)
                {
                    float wob = (Decor.Noise((r.Px + px) / 23f, 3) - 0.5f) * 3;
                    if (!Wall(px + 4, hi + 6)) continue;
                    c.FillRectGradV(r.Px + px, r.Py + hi + wob, 8, r.Ph - hi, VA(0.0f, 0.3f, 0.3f), VA(0.0f, 0.2f, 0.42f));
                    // the white rind of salt the sea leaves at the top of its reach
                    float th = 0.8f + Decor.Noise((r.Px + px) / 9f, 11) * 1.4f;
                    c.Fill = V(0.52f, 0.75f); c.FillRect(r.Px + px, r.Py + hi + wob - th, 8, th);
                }
                // barnacles toward the floor
                for (int i = 0; i < r.Pw / 6; i++)
                {
                    float px = R(rng, 0, r.Pw), py = hi + R(rng, 8, MathF.Max(10, r.Ph - hi));
                    if (!Wall(px, py) || rock(px, py) > 2.5f) continue;
                    c.Fill = V(0.36f, 0.85f); c.FillCircle(r.Px + px, r.Py + py, R(rng, 0.6f, 1.3f));
                    c.Fill = V(0.03f); c.FillCircle(r.Px + px, r.Py + py, 0.35f);
                }
                // and far above it, faint, the line from the night the light went out
                float storm = hi - 3.5f * TS;
                if (storm > TS)
                    for (float px = 0; px < r.Pw; px += 8)
                    {
                        if (!Wall(px + 4, storm)) continue;
                        float wob = (Decor.Noise((r.Px + px) / 31f, 7) - 0.5f) * 4;
                        c.Fill = V(0.0f, 0.22f); c.FillRect(r.Px + px, r.Py + storm + wob, 8, 1.6f);
                        c.Fill = V(0.42f, 0.25f); c.FillRect(r.Px + px, r.Py + storm + wob - 0.8f, 8, 0.6f);
                    }
            }
        }
    }
}
