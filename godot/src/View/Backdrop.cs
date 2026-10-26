// The night beyond the shore rooms, in parallax layers: sky, moon and clouds; the island itself with the
// abbey on its plateau and the dark lighthouse on the skull; the open sea with its stacks, glitter and
// fog; gulls riding the wind. Everything is placed relative to the view from the wreck, so walking east
// along the shore slides the island away behind Maren.
using System;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Backdrop
{
    const int VW = H.VIEW_W, VH = H.VIEW_H;
    const float REFX = 7 * 640, REFY = 4 * 360;     // where the wreck sits on the map
    static readonly (float x, float y, float s, float p)[] stars = Enumerable.Range(0, 140).Select(i => (TileArt.Hash(i, 1), TileArt.Hash(i, 2), TileArt.Hash(i, 3), TileArt.Hash(i, 4) * 6)).ToArray();
    // each cloud is a cluster of soft lobes
    static readonly (float x, float y, float w, float speed, int lobes)[] clouds = Enumerable.Range(0, 14).Select(i => (TileArt.Hash(i, 11) * 1400, 24 + TileArt.Hash(i, 12) * 120, 60 + TileArt.Hash(i, 13) * 120, 2 + TileArt.Hash(i, 14) * 4, 4 + (int)(TileArt.Hash(i, 15) * 4))).ToArray();

    static Color C(string hex, float a = 1) { var c = H.Col(hex); c.A = a; return c; }

    public static void Outdoor(Canvas c, Vector2 cam, float t, float flash)
    {
        float dx = cam.X - REFX, dy = cam.Y - REFY;
        float horizon = 238 - dy * 0.08f;
        // sky, darkest overhead, hazy toward the sea
        c.FillRectGradV(0, -40, VW, horizon * 0.55f + 40, C("#020509"), C("#0a121b"));
        c.FillRectGradV(0, horizon * 0.55f, VW, horizon * 0.45f + 1, C("#0a121b"), C("#243648"));
        if (flash > 0) c.FillRect(0, 0, VW, horizon, new Color(0.6f, 0.7f, 0.85f, flash * 0.12f));

        // stars, thinning toward the horizon
        foreach (var s in stars)
        {
            float sy = s.y * horizon * 0.8f, fade = 1 - sy / (horizon * 0.8f);
            float sx = ((s.x * 1100 - dx * 0.01f) % 1100 + 1100) % 1100 - 200;
            if (sx < -2 || sx > VW + 2) continue;
            float tw = (0.3f + 0.4f * MathF.Sin(t * (1 + s.s) + s.p)) * fade;
            c.Fill = new Color(0.85f, 0.88f, 0.92f, tw);
            float sz = s.s > 0.9f ? 1.6f : s.s > 0.6f ? 1.1f : 0.8f;
            c.FillRect(sx, sy, sz, sz);
            if (s.s > 0.97f) { c.Glow(sx + 0.8f, sy + 0.8f, 4, new Color(0.8f, 0.85f, 1f, 0.25f * fade)); }
        }

        // the moon, haloed
        float mx = 505 - dx * 0.02f, my = 66 - dy * 0.03f;
        c.Glow(mx, my, 210, 170, new Color(0.45f, 0.55f, 0.65f, 0.12f));
        c.Glow(mx, my, 60, new Color(0.75f, 0.82f, 0.9f, 0.22f));
        c.Fill = C("#dfe6ea"); c.FillCircle(mx, my, 13);
        c.Fill = new Color(0.55f, 0.6f, 0.65f, 0.35f); c.FillCircle(mx + 4, my - 3, 3.4f); c.FillCircle(mx - 4, my + 4, 2.4f); c.FillCircle(mx - 2, my - 6, 1.6f); c.FillCircle(mx + 5, my + 5, 1.4f);
        c.Fill = new Color(0, 0, 0, 0.12f); c.BeginPath(); c.Arc(mx + 1.5f, my, 13, -H.PI / 2, H.PI / 2); c.FillPath();

        // clouds drifting east, lit along their tops where they face the moon
        foreach (var cl in clouds)
        {
            float x = ((cl.x + t * cl.speed - dx * 0.04f) % 1400 + 1400) % 1400 - 300, y = cl.y - dy * 0.04f;
            if (x + cl.w < -40 || x - cl.w > VW + 40) continue;
            float near = MathF.Max(0, 1 - H.Dist(x, y, mx, my) / 320);
            for (int k = 0; k < cl.lobes; k++)
            {
                float lx = x + (k - cl.lobes / 2f) * cl.w / cl.lobes, ly = y + MathF.Sin(k * 2.3f + cl.x) * 5, lr = cl.w / cl.lobes * (1.1f + TileArt.Hash(k, (int)cl.x) * 0.6f);
                c.Glow(lx, ly, lr * 1.4f, lr * 0.75f, new Color(0.08f, 0.11f, 0.14f, 0.85f));
                if (near > 0) c.Glow(lx + (mx - lx) * 0.03f, ly - lr * 0.35f, lr, lr * 0.3f, new Color(0.5f, 0.58f, 0.66f, 0.2f * near));
            }
        }

        // ---- the island, far off: skull, lighthouse, plateau and abbey ----
        float ix = -dx * 0.06f, iy = horizon - 238;
        var far = C("#0a1016"); var rim = new Color(0.32f, 0.4f, 0.48f, 0.55f + flash * 0.4f);
        Vector2 P(float x, float y) => new(x + ix, y + iy);
        var island = new[]
        {
            P(-220, 150), P(-120, 140), P(-40, 150), P(5, 132), P(20, 118), P(36, 100), P(56, 92), P(76, 92), P(98, 102),
            P(116, 122), P(126, 146), P(140, 152), P(330, 150), P(352, 164), P(380, 176), P(402, 196), P(428, 218), P(452, 236), P(470, 242), P(-220, 242),
        };
        c.Glow(200 + ix, 150 + iy, 300, 60, new Color(0.3f, 0.4f, 0.5f, 0.12f));
        Silhouette(c, island, far, rim);
        // the skull: sockets and the gap of the nose, darker than the rock around them
        c.Fill = C("#05080b"); c.BeginPath(); c.Ellipse(57 + ix, 114 + iy, 5, 4, 0.2f, 0, H.PI * 2); c.FillPath();
        c.BeginPath(); c.Ellipse(79 + ix, 114 + iy, 5, 4, -0.2f, 0, H.PI * 2); c.FillPath();
        // the lighthouse on its crown, unlit
        c.Fill = far;
        c.Poly(new[] { P(63, 94), P(66, 58), P(74, 58), P(77, 94) });
        c.FillRect(61 + ix, 55 + iy, 18, 3); c.FillRect(64 + ix, 46 + iy, 12, 9);
        c.Poly(new[] { P(62, 46), P(70, 38), P(78, 46) });
        c.Fill = rim; c.FillRect(76 + ix, 59 + iy, 1, 34); c.FillRect(75 + ix, 46 + iy, 1, 9);
        c.Fill = C("#05080b"); c.FillRect(66 + ix, 48 + iy, 8, 5);
        float glint = MathF.Max(0, MathF.Sin(t * 0.4f)) * 0.25f;
        c.Fill = new Color(0.6f, 0.65f, 0.7f, glint); c.FillRect(71 + ix, 49 + iy, 1.5f, 3);
        // the abbey: bell tower and spire, the long nave with its buttresses, a second tower
        c.Fill = far;
        c.FillRect(150 + ix, 84 + iy, 18, 68);
        c.Poly(new[] { P(147, 86), P(159, 50), P(171, 86) });
        c.FillRect(146 + ix, 84 + iy, 26, 4);
        c.Poly(new[] { P(168, 128), P(228, 104), P(290, 128), P(290, 152), P(168, 152) });
        for (int i = 0; i < 6; i++) c.Poly(new[] { P(178 + i * 19, 152), P(182 + i * 19, 126), P(186 + i * 19, 152) });
        c.FillRect(290 + ix, 112 + iy, 14, 40);
        c.Poly(new[] { P(288, 114), P(297, 94), P(306, 114) });
        c.FillRect(310 + ix, 132 + iy, 18, 20);
        c.Fill = rim;
        c.FillRect(167 + ix, 86 + iy, 1, 66); c.FillRect(303 + ix, 114 + iy, 1, 38);
        c.Stroke = rim; c.LineWidth = 0.8f;
        c.Line(159 + ix, 50 + iy, 171 + ix, 86 + iy); c.Line(228 + ix, 104 + iy, 290 + ix, 128 + iy); c.Line(297 + ix, 94 + iy, 306 + ix, 114 + iy);
        // a few windows still burning, somebody keeping the vigil
        foreach (var (wx, wy, ph) in new[] { (157f, 98f, 0f), (190f, 136f, 1.3f), (226f, 136f, 2.1f), (262f, 136f, 3.7f), (296f, 124f, 0.6f) })
        {
            float a = 0.45f + 0.25f * MathF.Sin(t * 1.7f + ph) * MathF.Sin(t * 0.37f + ph * 2);
            c.Fill = new Color(1f, 0.72f, 0.38f, a); c.FillRect(wx + ix, wy + iy, 2, 3);
            c.Glow(wx + 1 + ix, wy + 1.5f + iy, 6, new Color(1f, 0.6f, 0.3f, a * 0.25f));
        }

        // ---- the sea ----
        c.FillRectGradV(0, horizon, VW, VH - horizon + 2, C("#152331"), C("#04080c"));
        c.Glow(VW / 2f, horizon, VW * 0.8f, 16, new Color(0.35f, 0.45f, 0.55f, 0.18f));
        c.Fill = new Color(0.55f, 0.65f, 0.75f, 0.35f); c.FillRect(0, horizon, VW, 1);
        // swells, slowly rolling in
        for (int i = 0; i < 26; i++)
        {
            float k = i / 26f, y = horizon + 3 + k * k * (VH - horizon);
            float off = (t * (6 + k * 20) + TileArt.Hash(i, 9) * 600 - dx * (0.06f + k * 0.2f)) % 160;
            c.Fill = new Color(0.4f, 0.5f, 0.6f, 0.08f + 0.05f * (1 - k));
            for (float x = -160 + off; x < VW; x += 160) c.FillRect(x, y, 40 + k * 70, 1);
        }
        // the moon's path on the water
        for (int i = 0; i < 70; i++)
        {
            float y = horizon + 2 + i * 1.7f; if (y > VH) break;
            float w = 4 + i * 1.1f + TileArt.Hash(i, 7) * 10, wob = MathF.Sin(t * 1.4f + i * 0.8f) * (2 + i * 0.05f);
            float a = (0.32f - i * 0.004f) * (0.6f + 0.4f * MathF.Sin(t * 3 + i * 1.7f));
            c.Fill = new Color(0.8f, 0.86f, 0.92f, MathF.Max(0, a));
            c.FillRect(mx - w / 2 + (TileArt.Hash(i, 3) - 0.5f) * 10 + wob, y, w * (0.3f + TileArt.Hash(i, 5) * 0.5f), 1);
        }

        // ---- sea stacks, nearer ----
        float stx = -dx * 0.14f, sty = iy * 1.4f;
        foreach (var (bx, w, h, seed) in new[] { (430f, 34f, 40f, 1), (500f, 16f, 26f, 2), (650f, 46f, 58f, 3), (775f, 28f, 36f, 4) })
        {
            float x0 = bx + stx, top = horizon + 6 - h + sty, bot = horizon + 10 + sty;
            if (x0 + w < -10 || x0 - w > VW + 10) continue;
            var pts = new System.Collections.Generic.List<Vector2> { new(x0 - w / 2 - 6, bot) };
            int n = 7;
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n, px = x0 - w / 2 + w * k;
                float py = top + MathF.Abs(k - 0.45f) * h * 0.5f + (TileArt.Hash(seed, i) - 0.5f) * 8;
                if (i == 0 || i == n) py = top + h * 0.45f;
                pts.Add(new Vector2(px, py));
            }
            pts.Add(new Vector2(x0 + w / 2 + 6, bot));
            Silhouette(c, pts.ToArray(), C("#070c11"), new Color(0.3f, 0.38f, 0.46f, 0.5f + flash * 0.4f));
            // gulls nest on top; foam breaks around the foot
            float f = 0.5f + 0.5f * MathF.Sin(t * 1.6f + seed * 2);
            c.Fill = new Color(0.75f, 0.82f, 0.88f, 0.25f + 0.25f * f); c.FillRect(x0 - w / 2 - 6 - f * 3, bot - 1, w + 12 + f * 6, 1);
            c.Fill = new Color(0.75f, 0.82f, 0.88f, 0.15f * f); c.FillRect(x0 - w / 2 - 2, bot - 3, w * 0.4f, 1);
        }

        // fog banks low over the water
        for (int i = 0; i < 6; i++)
        {
            float x = ((TileArt.Hash(i, 21) * 1000 + t * (3 + i) - dx * 0.1f) % 1000 + 1000) % 1000 - 180;
            c.Glow(x, horizon + 6 + TileArt.Hash(i, 22) * 20, 160 + TileArt.Hash(i, 23) * 120, 16, new Color(0.45f, 0.52f, 0.6f, 0.1f));
        }

        // gulls, wheeling
        for (int i = 0; i < 5; i++)
        {
            float speed = 9 + TileArt.Hash(i, 31) * 10, span = VW + 240;
            float x = ((TileArt.Hash(i, 32) * span + t * speed * (i % 2 == 0 ? 1 : -1)) % span + span) % span - 120;
            float y = 60 + TileArt.Hash(i, 33) * 120 + MathF.Sin(t * 0.4f + i * 2) * 14;
            float flap = MathF.Sin(t * (5 + i) + i) * 0.5f + (MathF.Sin(t * 0.3f + i) > 0.6f ? 0 : 0.2f);
            float s = 2.5f + TileArt.Hash(i, 34) * 1.5f;
            c.Stroke = C("#0a0f14"); c.LineWidth = 0.9f;
            c.BeginPath(); c.MoveTo(x - s, y - flap * s); c.QuadraticCurveTo(x - s * 0.4f, y - s * 0.4f, x, y); c.QuadraticCurveTo(x + s * 0.4f, y - s * 0.4f, x + s, y - flap * s); c.StrokePath();
        }
    }

    // A dark shape with a thin moonlit edge on the side that faces the moon.
    static void Silhouette(Canvas c, Vector2[] pts, Color body, Color rim)
    {
        c.Fill = rim; c.Poly(pts.Select(p => p + new Vector2(0.8f, -0.8f)).ToArray());
        c.Fill = body; c.Poly(pts);
    }
}
