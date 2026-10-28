// The menus' look. Everything Maren opens has been in the sea: slate gone slick and green, driftwood bleached and
// split, brass gone to verdigris, paper foxed with salt. Kelp hangs from the top edges and sways as if the water were
// still moving, bladderwrack climbs the sides, barnacles crowd the corners, salt crusts the lower edge and water drips
// from the upper one. Every piece is drawn here, procedurally, from a seed, so each menu has its own growth and it is
// the same every time it opens.
using System;
using Godot;

namespace Hollowmere;

public static class MenuArt
{
    public static float Now => Time.GetTicksMsec() / 1000f;
    static float Hash(int a, int b) => TileArt.Hash(a, b);
    static Color A(Color c, float a) => new(c.R, c.G, c.B, c.A * a);

    public static readonly Color
        INK = H.Col("#050708"), SLATE = H.Col("#151b1d"), SLATE_DARK = H.Col("#090c0d"), SLATE_LIGHT = H.Col("#243034"),
        IRON = H.Col("#24282a"), RUST = H.Col("#6a3420"), BRASS = H.Col("#b8904c"), BRASS_LIGHT = H.Col("#e8c880"), BRASS_DARK = H.Col("#5a4424"),
        VERDIGRIS = H.Col("#4f8f7c"), BONE = H.Col("#e6dac0"), TEXT = H.Col("#e6dcc0"), MUTED = H.Col("#9a8f78"),
        KELP_DARK = H.Col("#0f1d14"), KELP = H.Col("#2a4626"), KELP_LIGHT = H.Col("#5f7c36"), KELP_TIP = H.Col("#93a24c"),
        SALT = H.Col("#dfe6e4"), AMBER = H.Col("#ffd27a"), SEAGLASS = H.Col("#7fd8c8"), BLOOD = H.Col("#b0402e"),
        WOOD = H.Col("#3b2f23"), WOOD_DARK = H.Col("#1e1812"), WOOD_LIGHT = H.Col("#6a5a44"),
        PAPER = H.Col("#cdb98e"), PAPER_LIGHT = H.Col("#ddcba4"), PAPER_INK = H.Col("#2e2014"), STAIN = H.Col("#8a6a42");

    // ---- materials -----------------------------------------------------------------------------------------
    // Wet slate: dark, a little green, mottled where the damp sits, streaked where water has run down it.
    public static void Slate(Canvas c, float x, float y, float w, float h, int seed, float alpha = 0.97f)
    {
        c.FillRectGradV(x, y, w, h, A(new Color(0.085f, 0.11f, 0.115f), alpha), A(new Color(0.03f, 0.042f, 0.045f), alpha));
        for (int i = 0; i < 9; i++)
        {
            float px = x + Hash(i, seed) * w, py = y + Hash(i, seed + 1) * h, r = 20 + Hash(i, seed + 2) * 50;
            c.Glow(px, py, r, r * 0.7f, i % 3 == 0 ? new Color(0.25f, 0.42f, 0.38f, 0.07f) : new Color(0, 0, 0, 0.18f));
        }
        // runs of damp down the face
        for (int i = 0; i < 10; i++)
        {
            float px = x + Hash(i, seed + 5) * w, len = h * (0.15f + Hash(i, seed + 6) * 0.5f), py = y + Hash(i, seed + 7) * h * 0.3f;
            c.FillRectGradV(px, py, 0.8f, len, new Color(0.4f, 0.55f, 0.55f, 0.07f), new Color(0.4f, 0.55f, 0.55f, 0));
        }
        // a sheen along the top, as if the light were coming down through water
        c.FillRectGradV(x, y, w, h * 0.35f, new Color(0.5f, 0.7f, 0.7f, 0.05f), new Color(0, 0, 0, 0));
    }
    // Driftwood: boards bleached by the sea, the grain raised, nail heads gone to rust.
    public static void Wood(Canvas c, float x, float y, float w, float h, int seed, float board = 14)
    {
        int n = Math.Max(1, (int)MathF.Ceiling(h / board));
        for (int i = 0; i < n; i++)
        {
            float by = y + i * board, bh = MathF.Min(board, y + h - by);
            float tone = 0.85f + Hash(i, seed) * 0.3f;
            var top = new Color(WOOD.R * tone + 0.04f, WOOD.G * tone + 0.03f, WOOD.B * tone + 0.02f);
            c.FillRectGradV(x, by, w, bh, top, new Color(WOOD_DARK.R * tone, WOOD_DARK.G * tone, WOOD_DARK.B * tone));
            for (int g = 0; g < 5; g++)
            {
                float gy = by + 2 + Hash(g, seed + i * 7) * (bh - 4), gx = x + Hash(g + 9, seed + i) * w * 0.4f, gl = w * (0.3f + Hash(g + 3, seed + i) * 0.6f);
                c.FillRect(gx, gy, MathF.Min(gl, x + w - gx), 0.5f, new Color(WOOD_LIGHT.R, WOOD_LIGHT.G, WOOD_LIGHT.B, 0.35f));
            }
            if (Hash(i, seed + 3) < 0.6f) { float kx = x + 6 + Hash(i, seed + 4) * (w - 12); c.Fill = new Color(0.08f, 0.06f, 0.04f, 0.7f); c.BeginPath(); c.Ellipse(kx, by + bh / 2, 2.2f, 1.1f, 0, 0, H.PI * 2); c.FillPath(); }
            c.FillRect(x, by, w, 0.6f, new Color(0.85f, 0.8f, 0.7f, 0.12f));
            c.FillRect(x, by + bh - 1, w, 1, new Color(0, 0, 0, 0.55f));
            Nail(c, x + 5, by + bh / 2); Nail(c, x + w - 5, by + bh / 2);
        }
        // the sea has bleached the lower boards
        c.FillRectGradV(x, y + h * 0.55f, w, h * 0.45f, new Color(0.8f, 0.85f, 0.85f, 0), new Color(0.8f, 0.85f, 0.85f, 0.07f));
    }
    public static void Nail(Canvas c, float x, float y)
    {
        c.Fill = new Color(0.05f, 0.04f, 0.03f, 0.8f); c.FillCircle(x, y, 1.3f);
        c.Fill = new Color(RUST.R, RUST.G, RUST.B, 0.9f); c.FillCircle(x - 0.2f, y - 0.2f, 0.9f);
        c.Glow(x, y + 2.5f, 2.5f, 4, new Color(RUST.R, RUST.G, RUST.B, 0.25f));
    }
    // Paper that has been wet and dried: foxed, stained, darker with handling at the edges.
    public static void Parchment(Canvas c, float x, float y, float w, float h, int seed)
    {
        c.FillRectGradV(x, y, w, h, PAPER_LIGHT, PAPER);
        for (int i = 0; i < 14; i++)
        {
            float px = x + Hash(i, seed) * w, py = y + Hash(i, seed + 1) * h, r = 10 + Hash(i, seed + 2) * 36;
            c.Glow(px, py, r, r * 0.7f, new Color(STAIN.R, STAIN.G, STAIN.B, 0.05f + Hash(i, seed + 3) * 0.07f));
        }
        for (int i = 0; i < 40; i++) c.FillRect(x + Hash(i, seed + 4) * w, y + Hash(i, seed + 5) * h, 0.7f, 0.7f, new Color(STAIN.R, STAIN.G, STAIN.B, 0.3f));
        foreach (var (cx, cy) in new[] { (x, y), (x + w, y), (x, y + h), (x + w, y + h) }) c.Glow(cx, cy, 40, 30, new Color(0.3f, 0.2f, 0.1f, 0.3f));
        // a tide line where it lay in water
        float ty = y + h * (0.6f + Hash(seed, 9) * 0.25f);
        c.FillRectGradV(x, ty, w, h - (ty - y), new Color(0.45f, 0.38f, 0.26f, 0.0f), new Color(0.45f, 0.38f, 0.26f, 0.12f));
        c.FillRect(x, ty, w, 0.6f, new Color(0.45f, 0.35f, 0.22f, 0.25f));
    }
    // A slab of the island's stone with a border chiselled into it, lichen in the cuts.
    public static void Stone(Canvas c, float x, float y, float w, float h, int seed)
    {
        c.FillRectGradV(x, y, w, h, H.Col("#2a3331"), H.Col("#161c1b"));
        for (int i = 0; i < 18; i++)
        {
            float px = x + Hash(i, seed) * w, py = y + Hash(i, seed + 1) * h, r = 6 + Hash(i, seed + 2) * 30;
            c.Glow(px, py, r, r * 0.8f, i % 4 == 0 ? new Color(0.35f, 0.45f, 0.3f, 0.1f) : new Color(0, 0, 0, 0.15f));
        }
        for (int i = 0; i < 60; i++) c.FillRect(x + Hash(i, seed + 6) * w, y + Hash(i, seed + 7) * h, 0.8f, 0.8f, new Color(0.7f, 0.75f, 0.7f, 0.08f));
        float m = 6;
        c.Stroke = new Color(0, 0, 0, 0.6f); c.LineWidth = 1.2f; c.StrokeRect(x + m, y + m, w - m * 2, h - m * 2);
        c.Stroke = new Color(0.7f, 0.78f, 0.75f, 0.14f); c.LineWidth = 0.6f; c.StrokeRect(x + m + 1, y + m + 1, w - m * 2, h - m * 2);
        for (int i = 0; i < 10; i++) { float t = Hash(i, seed + 8); float lx = x + m + t * (w - m * 2); c.Glow(lx, y + m, 3, 1.5f, new Color(0.5f, 0.65f, 0.35f, 0.4f)); }
    }
    // Brass: a plate with a bevel, rivets at its corners, verdigris where the sea sat on it.
    public static void Brass(Canvas c, float x, float y, float w, float h, int seed, float lit = 0, bool rivets = true)
    {
        var top = BRASS.Lerp(BRASS_LIGHT, 0.25f + lit * 0.35f); var bot = BRASS_DARK.Lerp(BRASS, lit * 0.3f);
        c.FillRect(x - 0.6f, y - 0.6f, w + 1.2f, h + 1.2f, new Color(0, 0, 0, 0.65f));
        c.FillRectGradV(x, y, w, h, top, bot);
        c.FillRect(x, y, w, 0.7f, new Color(1, 0.95f, 0.8f, 0.45f));
        c.FillRect(x, y + h - 0.7f, w, 0.7f, new Color(0, 0, 0, 0.4f));
        // verdigris where the sea sat on it, in the lower corners and along the foot
        for (int i = 0; i < 3; i++)
        {
            float px = i == 0 ? x + 2 : i == 1 ? x + w - 2 : x + 4 + Hash(i, seed) * (w - 8), py = y + h - 1.5f;
            float rx = MathF.Min(w * 0.12f, 2.5f + Hash(i, seed + 2) * 4), ry = MathF.Min(h * 0.35f, 1.5f + Hash(i, seed + 3) * 2);
            c.Glow(px, py, rx, ry, new Color(VERDIGRIS.R, VERDIGRIS.G, VERDIGRIS.B, 0.3f));
        }
        if (rivets && w > 14 && h > 7) { Rivet(c, x + 2.5f, y + h / 2); Rivet(c, x + w - 2.5f, y + h / 2); }
    }
    public static void Rivet(Canvas c, float x, float y, float r = 1.1f)
    {
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillCircle(x + 0.3f, y + 0.4f, r);
        c.Fill = BRASS_DARK; c.FillCircle(x, y, r);
        c.Fill = new Color(1, 0.92f, 0.7f, 0.7f); c.FillCircle(x - r * 0.3f, y - r * 0.3f, r * 0.4f);
    }

    // ---- the sea getting in --------------------------------------------------------------------------------
    // A strand of kelp from its holdfast: it sways, more toward the tip, with long blades and the odd float.
    public static void Kelp(Canvas c, Vector2 root, float ang, float len, float t, int seed, float width = 2.2f, float alpha = 1)
    {
        int n = Math.Max(5, (int)(len / 5));
        float seg = len / n, a = ang, speed = 0.7f + Hash(seed, 3) * 0.6f;
        Span<Vector2> pts = stackalloc Vector2[n + 1];
        pts[0] = root;
        for (int i = 1; i <= n; i++)
        {
            float k = i / (float)n;
            a += MathF.Sin(t * speed + i * 0.55f + seed) * 0.09f * k + (Hash(i, seed) - 0.5f) * 0.16f;
            pts[i] = pts[i - 1] + new Vector2(MathF.Cos(a), MathF.Sin(a)) * seg;
        }
        // the stipe, tapering
        for (int i = 0; i < n; i++)
        {
            var d = (pts[i + 1] - pts[i]).Normalized(); var nr = new Vector2(-d.Y, d.X);
            float w0 = width * (1 - i / (float)n * 0.75f), w1 = width * (1 - (i + 1) / (float)n * 0.75f);
            var col = KELP_DARK.Lerp(KELP, i / (float)n);
            c.Fill = A(col, alpha);
            c.Poly(new[] { pts[i] + nr * w0 / 2, pts[i + 1] + nr * w1 / 2, pts[i + 1] - nr * w1 / 2, pts[i] - nr * w0 / 2 });
        }
        // blades along it, alternating sides, and a few gas floats
        for (int i = 1; i < n; i++)
        {
            if (i % 2 == 1 || Hash(i, seed + 11) < 0.25f)
            {
                var d = (pts[i + 1] - pts[i]).Normalized();
                float side = (i % 4 < 2) ? 1 : -1;
                float la = MathF.Atan2(d.Y, d.X) + side * (0.5f + Hash(i, seed + 12) * 0.4f) + MathF.Sin(t * 1.3f + i + seed) * 0.15f;
                float bl = 4 + Hash(i, seed + 13) * 6 * (1 - i / (float)n * 0.4f), bw = 1.1f + Hash(i, seed + 14) * 1.2f;
                var mid = pts[i] + new Vector2(MathF.Cos(la), MathF.Sin(la)) * bl * 0.5f;
                c.Save(); c.Translate(mid.X, mid.Y); c.Rotate(la);
                c.Fill = A(KELP.Lerp(KELP_LIGHT, Hash(i, seed + 15) * 0.7f), alpha); c.BeginPath(); c.Ellipse(0, 0, bl * 0.5f, bw, 0, 0, H.PI * 2); c.FillPath();
                c.Fill = A(new Color(KELP_TIP.R, KELP_TIP.G, KELP_TIP.B, 0.35f), alpha); c.FillRect(-bl * 0.4f, -0.2f, bl * 0.8f, 0.4f);
                c.Restore();
            }
            if (Hash(i, seed + 21) < 0.18f)
            {
                c.Fill = A(KELP_LIGHT, alpha); c.FillCircle(pts[i].X, pts[i].Y, 1.3f);
                c.Fill = A(new Color(0.9f, 1, 0.7f, 0.35f), alpha); c.FillCircle(pts[i].X - 0.4f, pts[i].Y - 0.4f, 0.5f);
            }
        }
        // a lighter tip, where it catches the light
        c.Fill = A(new Color(KELP_TIP.R, KELP_TIP.G, KELP_TIP.B, 0.7f), alpha); c.FillCircle(pts[n].X, pts[n].Y, width * 0.3f);
    }
    // Bladderwrack climbing: a stem that forks, flat fronds, paired bladders.
    public static void Wrack(Canvas c, Vector2 root, float ang, float len, float t, int seed, int depth = 0)
    {
        int n = Math.Max(3, (int)(len / 6));
        float seg = len / n, a = ang;
        var p = root;
        for (int i = 0; i < n; i++)
        {
            a += MathF.Sin(t * 0.9f + i * 0.7f + seed) * 0.05f + (Hash(i, seed + depth * 31) - 0.5f) * 0.3f;
            var q = p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * seg;
            float w = 2.2f * (1 - i / (float)n * 0.6f) / (1 + depth * 0.5f);
            var d = (q - p).Normalized(); var nr = new Vector2(-d.Y, d.X);
            c.Fill = new Color(0.12f, 0.12f, 0.07f).Lerp(new Color(0.3f, 0.26f, 0.12f), i / (float)n);
            c.Poly(new[] { p + nr * w, q + nr * w * 0.85f, q - nr * w * 0.85f, p - nr * w });
            if (Hash(i, seed + 40 + depth) < 0.45f)
            {
                var m = p.Lerp(q, 0.5f);
                c.Fill = new Color(0.36f, 0.29f, 0.12f); c.FillCircle(m.X + nr.X * 1.6f, m.Y + nr.Y * 1.6f, 1.1f); c.FillCircle(m.X - nr.X * 1.6f, m.Y - nr.Y * 1.6f, 1.1f);
                c.Fill = new Color(1, 0.9f, 0.6f, 0.18f); c.FillCircle(m.X + nr.X * 1.4f - 0.3f, m.Y + nr.Y * 1.4f - 0.3f, 0.4f);
            }
            if (depth < 2 && i > 0 && Hash(i, seed + 50 + depth) < 0.3f) Wrack(c, q, a + (Hash(i, seed + 51) < 0.5f ? -0.7f : 0.7f), len * 0.45f, t, seed + i * 13, depth + 1);
            p = q;
        }
    }
    // A crowd of barnacles: chalky cones, each with its dark mouth.
    public static void Barnacles(Canvas c, float x, float y, float spread, int seed, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Hash(i, seed) * H.PI * 2, d = MathF.Sqrt(Hash(i, seed + 1)) * spread;
            float bx = x + MathF.Cos(a) * d, by = y + MathF.Sin(a) * d * 0.8f, r = 1.1f + Hash(i, seed + 2) * 2.2f * (1 - d / spread * 0.5f);
            c.Fill = new Color(0, 0, 0, 0.45f); c.FillCircle(bx + 0.4f, by + 0.6f, r);
            c.Fill = new Color(0.62f, 0.62f, 0.56f); c.FillCircle(bx, by, r);
            c.Fill = new Color(0.8f, 0.8f, 0.74f); c.FillCircle(bx - r * 0.2f, by - r * 0.25f, r * 0.7f);
            c.Fill = new Color(0.12f, 0.12f, 0.1f); c.FillCircle(bx, by, r * 0.38f);
            c.Fill = new Color(1, 1, 1, 0.5f); c.FillRect(bx - r * 0.55f, by - r * 0.6f, 0.6f, 0.6f);
        }
    }
    // A cockle shell, ribbed, the colour of old bone.
    public static void Shell(Canvas c, float x, float y, float s, float rot, int seed)
    {
        c.Save(); c.Translate(x, y); c.Rotate(rot);
        c.Fill = new Color(0, 0, 0, 0.4f); c.BeginPath(); c.Ellipse(0.5f, 0.8f, s, s * 0.8f, 0, H.PI, H.PI * 2); c.LineTo(-s * 0.2f + 0.5f, s * 0.35f + 0.8f); c.LineTo(s * 0.2f + 0.5f, s * 0.35f + 0.8f); c.ClosePath(); c.FillPath();
        c.Fill = new Color(0.86f, 0.78f, 0.66f); c.BeginPath(); c.Ellipse(0, 0, s, s * 0.8f, 0, H.PI, H.PI * 2); c.LineTo(-s * 0.2f, s * 0.35f); c.LineTo(s * 0.2f, s * 0.35f); c.ClosePath(); c.FillPath();
        c.Stroke = new Color(0.55f, 0.45f, 0.36f, 0.8f); c.LineWidth = 0.45f;
        for (int i = 0; i < 6; i++) { float a = H.PI + (i + 0.5f) / 6f * H.PI; c.Line(0, s * 0.3f, MathF.Cos(a) * s * 0.95f, MathF.Sin(a) * s * 0.75f); }
        c.Restore();
    }
    // Salt crust along an edge: crystals, white and grey.
    public static void Salt(Canvas c, float x, float y, float w, int seed, float depth = 4)
    {
        for (int i = 0; i < (int)(w / 2.2f); i++)
        {
            float px = x + Hash(i, seed) * w, py = y - Hash(i, seed + 1) * depth, s = 0.5f + Hash(i, seed + 2) * 1.3f;
            c.Fill = new Color(SALT.R, SALT.G, SALT.B, 0.35f + Hash(i, seed + 3) * 0.5f);
            c.FillRect(px, py, s, s);
        }
        c.FillRectGradV(x, y - depth, w, depth, new Color(1, 1, 1, 0), new Color(SALT.R, SALT.G, SALT.B, 0.12f));
    }
    // Water gathering on an edge, swelling, letting go.
    public static void Drips(Canvas c, float x0, float x1, float y, float t, int seed, int n = 3, float fall = 60)
    {
        for (int i = 0; i < n; i++)
        {
            float x = x0 + Hash(i, seed) * (x1 - x0), period = 2.4f + Hash(i, seed + 1) * 2.6f;
            float ph = (t + Hash(i, seed + 2) * period) % period / period;
            if (ph < 0.7f)
            {
                float r = 0.4f + ph / 0.7f * 0.9f;
                c.Fill = new Color(0.7f, 0.85f, 0.9f, 0.55f); c.FillCircle(x, y + r, r);
                c.Fill = new Color(1, 1, 1, 0.6f); c.FillRect(x - r * 0.4f, y + r * 0.6f, 0.5f, 0.5f);
            }
            else
            {
                float k = (ph - 0.7f) / 0.3f, dy = k * k * fall;
                c.Fill = new Color(0.7f, 0.85f, 0.9f, 0.6f * (1 - k)); c.FillRect(x - 0.5f, y + 1 + dy, 1, 2.2f);
            }
        }
    }

    // ---- frames ----------------------------------------------------------------------------------------
    public enum Look { Slate, Wood, Parchment, Stone }
    // The panel itself, behind its content.
    public static void Frame(Canvas c, float w, float h, Look look, int seed)
    {
        c.Fill = new Color(0, 0, 0, 0.5f); c.FillRect(4, 5, w, h);
        c.Glow(w / 2, h / 2, w * 0.7f, h * 0.7f, new Color(0, 0, 0, 0.35f));
        switch (look)
        {
            case Look.Wood: Wood(c, 0, 0, w, h, seed, 18); c.FillRect(5, 5, w - 10, h - 10, new Color(0.02f, 0.015f, 0.01f, 0.42f)); c.Glow(w / 2, h / 2, w * 0.5f, h * 0.5f, new Color(0, 0, 0, 0.2f)); break;
            case Look.Parchment: Parchment(c, 0, 0, w, h, seed); break;
            case Look.Stone: Stone(c, 0, 0, w, h, seed); break;
            default: Slate(c, 0, 0, w, h, seed); break;
        }
        if (look == Look.Parchment || look == Look.Stone) return;
        // an iron band round it, studded with brass
        c.Stroke = new Color(IRON.R, IRON.G, IRON.B, 0.95f); c.LineWidth = 3; c.StrokeRect(1.5f, 1.5f, w - 3, h - 3);
        c.Stroke = new Color(BRASS.R, BRASS.G, BRASS.B, 0.45f); c.LineWidth = 0.6f; c.StrokeRect(0.3f, 0.3f, w - 0.6f, h - 0.6f);
        c.Stroke = new Color(BRASS.R, BRASS.G, BRASS.B, 0.25f); c.StrokeRect(3.4f, 3.4f, w - 6.8f, h - 6.8f);
        for (float x = 14; x < w - 10; x += 26) { Rivet(c, x, 1.5f, 0.9f); Rivet(c, x, h - 1.5f, 0.9f); }
        for (float y = 14; y < h - 10; y += 26) { Rivet(c, 1.5f, y, 0.9f); Rivet(c, w - 1.5f, y, 0.9f); }
        // rust run from the rivets, verdigris on the band
        for (int i = 0; i < 6; i++) { float px = Hash(i, seed + 70) * w; c.Glow(px, 3, 3, 8, new Color(RUST.R, RUST.G, RUST.B, 0.3f)); }
        // brass corner caps
        foreach (var (cx, cy, dx, dy) in new[] { (0f, 0f, 1, 1), (w, 0f, -1, 1), (0f, h, 1, -1), (w, h, -1, -1) })
        {
            c.Fill = BRASS_DARK;
            c.FillRect(dx > 0 ? cx : cx - 12, dy > 0 ? cy : cy - 3, 12, 3); c.FillRect(dx > 0 ? cx : cx - 3, dy > 0 ? cy : cy - 12, 3, 12);
            c.Fill = new Color(BRASS.R, BRASS.G, BRASS.B, 0.9f);
            c.FillRect(dx > 0 ? cx : cx - 11, dy > 0 ? cy + 0.5f : cy - 2.5f, 11, 1.2f); c.FillRect(dx > 0 ? cx + 0.5f : cx - 2.5f, dy > 0 ? cy : cy - 11, 1.2f, 11);
            c.Glow(cx + dx * 5, cy + dy * 5, 6, new Color(VERDIGRIS.R, VERDIGRIS.G, VERDIGRIS.B, 0.35f));
            Rivet(c, cx + dx * 6, cy + dy * 6, 1.3f);
        }
    }
    // What the sea has grown over the panel, in front of its content: kelp hanging from the top, wrack climbing the
    // sides from the lower corners, barnacles crowding the corners, shells and salt along the foot, water dripping.
    public static void Overgrowth(Canvas c, float w, float h, int seed, float t, float amount = 1, bool drips = true)
    {
        if (amount <= 0) return;
        // wrack up the sides, outside the band
        Wrack(c, new Vector2(-2, h - 2), -H.PI / 2 - 0.2f, MathF.Min(h * 0.4f, 50) * amount, t, seed + 1);
        Wrack(c, new Vector2(w + 2, h - 2), -H.PI / 2 + 0.2f, MathF.Min(h * 0.3f, 38) * amount, t, seed + 2);
        // kelp from the top corners, leaning out, so the words stay clear
        int strands = 2 + (int)(Hash(seed, 1) * 2) + (w > 300 ? 2 : 0);
        for (int i = 0; i < strands; i++)
        {
            float u = Hash(i, seed + 4);
            bool left = i % 2 == 0;
            float x = left ? u * 8 : w - u * 8;
            if (i >= 4) x = w * (0.3f + u * 0.4f);
            float len = (i >= 4 ? 8 + Hash(i, seed + 5) * 6 : 14 + Hash(i, seed + 5) * 22) * amount;
            float lean = i >= 4 ? (Hash(i, seed + 6) - 0.5f) * 0.3f : (left ? 0.35f : -0.35f) + (Hash(i, seed + 6) - 0.5f) * 0.2f;
            Kelp(c, new Vector2(x, -2), H.PI / 2 + lean, len, t, seed + i * 7, 2 + Hash(i, seed + 7));
        }
        // and a long strand down one side, hanging along the band
        bool lside = Hash(seed, 8) < 0.5f;
        Kelp(c, new Vector2(lside ? -2 : w + 2, 4), H.PI / 2 + (lside ? 0.1f : -0.1f), MathF.Min(h * 0.6f, 90) * amount, t, seed + 99, 2.4f, 0.95f);
        // barnacles crowd the corners
        Barnacles(c, 4, 4, 7, seed + 10, 9);
        Barnacles(c, w - 4, h - 4, 8, seed + 11, 11);
        Barnacles(c, w - 3, 5, 4, seed + 12, 4);
        Barnacles(c, 5, h - 3, 5, seed + 13, 6);
        // a shell or two on the lower edge, salt along it
        Salt(c, 6, h, w - 12, seed + 14);
        Shell(c, w * (0.2f + Hash(seed, 15) * 0.2f), h - 1, 3.4f, -0.2f, seed);
        if (w > 200) Shell(c, w * (0.65f + Hash(seed, 16) * 0.2f), h, 2.6f, 0.3f, seed + 1);
        if (drips) Drips(c, 14, w - 14, h - 1, t, seed + 17, w > 300 ? 4 : 2, 30);
    }

    // ---- buttons -------------------------------------------------------------------------------------------
    public enum Btn { List, Chip, Tab, Primary, Danger, Title, Slot, Flat }
    // How a button looks under its words. `state`: 0 idle, 1 hover, 2 focus, 3 pressed; `on` for a toggle that is set.
    public static void Button(Canvas c, float w, float h, Btn style, int state, bool on, bool disabled, int seed, float t)
    {
        float lit = state switch { 1 => 0.6f, 2 => 1, 3 => 1.2f, _ => 0 } * (disabled ? 0.3f : 1);
        float flick = 0.85f + 0.15f * MathF.Sin(t * 9 + seed) * MathF.Sin(t * 5.3f);
        switch (style)
        {
            case Btn.List:
            case Btn.Danger:
            {
                var glow = style == Btn.Danger ? BLOOD : AMBER;
                if (lit > 0)
                {
                    c.Glow(w * 0.35f, h / 2, w * 0.6f, h * 0.9f, new Color(glow.R, glow.G, glow.B, 0.12f * lit * flick));
                    c.FillRectGradH(0, 0, w, h, new Color(1, 0.8f, 0.45f, 0.09f * lit), new Color(1, 0.8f, 0.45f, 0));
                    c.FillRect(0, h - 0.7f, w * 0.8f, 0.7f, new Color(BRASS.R, BRASS.G, BRASS.B, 0.6f * lit));
                    // a lantern bead at the left of the one in hand
                    c.Glow(4, h / 2, 6, new Color(glow.R, glow.G, glow.B, 0.6f * lit * flick));
                    c.Fill = new Color(1, 0.9f, 0.6f, 0.95f); c.Save(); c.Translate(4, h / 2); c.Rotate(H.PI / 4); c.FillRect(-1.3f, -1.3f, 2.6f, 2.6f); c.Restore();
                }
                else
                {
                    c.FillRect(6, h - 0.6f, w * 0.5f, 0.5f, new Color(BRASS.R, BRASS.G, BRASS.B, 0.18f));
                    c.Save(); c.Translate(4, h / 2); c.Rotate(H.PI / 4); c.FillRect(-0.9f, -0.9f, 1.8f, 1.8f, new Color(BRASS.R, BRASS.G, BRASS.B, disabled ? 0.15f : 0.4f)); c.Restore();
                }
                if (on) c.FillRect(0, 1, 1.5f, h - 2, AMBER);
                break;
            }
            case Btn.Chip:
            {
                // a little brass tag; lit when set
                if (on) Brass(c, 0, 0, w, h, seed, 0.4f + lit * 0.3f, false);
                else { c.FillRectGradV(0, 0, w, h, new Color(0.11f, 0.13f, 0.13f, 0.95f), new Color(0.05f, 0.06f, 0.06f, 0.95f)); c.Stroke = new Color(BRASS.R, BRASS.G, BRASS.B, disabled ? 0.12f : 0.35f); c.LineWidth = 0.6f; c.StrokeRect(0.3f, 0.3f, w - 0.6f, h - 0.6f); }
                if (lit > 0) { c.Stroke = new Color(AMBER.R, AMBER.G, AMBER.B, 0.8f * lit * flick); c.LineWidth = 0.9f; c.StrokeRect(-0.8f, -0.8f, w + 1.6f, h + 1.6f); c.Glow(w / 2, h / 2, w * 0.6f, h, new Color(1, 0.75f, 0.4f, 0.12f * lit)); }
                break;
            }
            case Btn.Tab:
            {
                // a leather bookmark hanging from the page's top, its end cut in a swallowtail
                float hh = on ? h + 3 : h;
                var leather = on ? H.Col("#6a3a22") : H.Col("#3a2418");
                c.Fill = new Color(0, 0, 0, 0.5f); c.Poly(new[] { new Vector2(1, 0), new Vector2(w + 1, 0), new Vector2(w + 1, hh + 1), new Vector2(w / 2 + 1, hh - 3 + 1), new Vector2(1, hh + 1) });
                c.FillPolyGrad(new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, hh), new Vector2(w / 2, hh - 3), new Vector2(0, hh) },
                    new[] { leather.Lightened(0.15f), leather.Lightened(0.15f), leather.Darkened(0.3f), leather, leather.Darkened(0.3f) });
                c.Stroke = new Color(0.9f, 0.75f, 0.5f, on ? 0.6f : 0.3f); c.LineWidth = 0.5f;
                for (float y = 2; y < hh - 4; y += 3) { c.Line(1.5f, y, 1.5f, y + 1.5f); c.Line(w - 1.5f, y, w - 1.5f, y + 1.5f); }
                if (lit > 0) c.Glow(w / 2, hh / 2, w * 0.7f, hh, new Color(1, 0.75f, 0.4f, 0.15f * lit * flick));
                break;
            }
            case Btn.Primary:
            {
                // a brass plaque, riveted; it warms when in hand
                Brass(c, 0, 0, w, h, seed, 0.15f + lit * 0.45f);
                c.Stroke = new Color(0.25f, 0.18f, 0.08f, 0.7f); c.LineWidth = 0.5f; c.StrokeRect(3, 2, w - 6, h - 4);
                if (lit > 0) c.Glow(w / 2, h / 2, w * 0.7f, h * 1.2f, new Color(1, 0.75f, 0.4f, 0.2f * lit * flick));
                break;
            }
            case Btn.Title:
            {
                // a board nailed to the signpost, its far end split and wet; the one in hand catches the lantern
                Wood(c, 0, 0, w, h, seed, h);
                c.Fill = new Color(0, 0, 0, 0.25f); c.Poly(new[] { new Vector2(w - 6, 0), new Vector2(w, 0), new Vector2(w + 4, h / 2), new Vector2(w, h), new Vector2(w - 6, h) });
                c.FillPolyGrad(new[] { new Vector2(w - 2, 0), new Vector2(w + 5, h / 2), new Vector2(w - 2, h) }, new[] { WOOD, WOOD_DARK, WOOD_DARK });
                if (lit > 0)
                {
                    c.Glow(w * 0.3f, h / 2, w * 0.75f, h * 1.6f, new Color(1, 0.72f, 0.35f, 0.22f * lit * flick));
                    c.FillRectGradV(0, 0, w, h, new Color(1, 0.8f, 0.5f, 0.12f * lit), new Color(1, 0.8f, 0.5f, 0));
                }
                else c.FillRect(0, 0, w, h, new Color(0, 0, 0, 0.3f));
                break;
            }
            case Btn.Flat: break;
        }
    }

    // ---- an inventory slot: a pocket stitched in oilcloth -------------------------------------------------
    public static void Slot(Canvas c, float w, float h, bool filled, int state, float t, int seed, bool key = false)
    {
        float lit = state switch { 1 => 0.6f, 2 => 1, 3 => 1.2f, _ => 0 };
        c.FillRectGradV(0, 0, w, h, filled ? new Color(0.13f, 0.12f, 0.09f, 0.96f) : new Color(0.07f, 0.07f, 0.06f, 0.85f), new Color(0.04f, 0.04f, 0.03f, 0.96f));
        c.Glow(w / 2, h * 0.3f, w * 0.6f, h * 0.4f, new Color(1, 1, 1, filled ? 0.03f : 0.01f));
        // the stitching
        c.Fill = new Color(0.7f, 0.6f, 0.42f, filled ? 0.45f : 0.22f);
        for (float x = 3; x < w - 3; x += 3) { c.FillRect(x, 1.5f, 1.5f, 0.5f); c.FillRect(x, h - 2, 1.5f, 0.5f); }
        for (float y = 3; y < h - 3; y += 3) { c.FillRect(1.5f, y, 0.5f, 1.5f); c.FillRect(w - 2, y, 0.5f, 1.5f); }
        if (key) { c.Fill = new Color(AMBER.R, AMBER.G, AMBER.B, 0.6f); c.Poly(new[] { new Vector2(w - 7, 0), new Vector2(w, 0), new Vector2(w, 7) }); }
        if (lit > 0)
        {
            float flick = 0.85f + 0.15f * MathF.Sin(t * 8 + seed);
            c.Glow(w / 2, h / 2, w * 0.75f, h * 0.75f, new Color(1, 0.75f, 0.4f, 0.16f * lit * flick));
            // brass brackets at the corners of the one in hand
            c.Fill = new Color(BRASS_LIGHT.R, BRASS_LIGHT.G, BRASS_LIGHT.B, 0.9f * MathF.Min(1, lit));
            foreach (var (cx, cy, dx, dy) in new[] { (-1f, -1f, 1, 1), (w + 1, -1f, -1, 1), (-1f, h + 1, 1, -1), (w + 1, h + 1, -1, -1) })
            { c.FillRect(dx > 0 ? cx : cx - 5, dy > 0 ? cy : cy - 1, 5, 1); c.FillRect(dx > 0 ? cx : cx - 1, dy > 0 ? cy : cy - 5, 1, 5); }
        }
    }

    // ---- a heartbeat, as the diving suit's gauge would show it --------------------------------------------
    // Like a survival-horror condition screen: steady and green-blue when whole, amber and faster when hurt,
    // red and stumbling at the last pip.
    public static void Heartbeat(Canvas c, float x, float y, float w, float h, float health, float t)
    {
        var col = health > 0.6f ? H.Col("#7fd8b8") : health > 0.3f ? H.Col("#f0b050") : H.Col("#e05040");
        float bpm = health > 0.6f ? 62 : health > 0.3f ? 96 : 128;
        c.FillRect(x, y, w, h, new Color(0, 0.03f, 0.03f, 0.85f));
        c.Stroke = new Color(col.R, col.G, col.B, 0.1f); c.LineWidth = 0.4f;
        for (float gx = x + 6; gx < x + w; gx += 6) c.Line(gx, y, gx, y + h);
        for (float gy = y + 4; gy < y + h; gy += 4) c.Line(x, gy, x + w, gy);
        float beat = 60 / bpm, mid = y + h * 0.6f;
        float Wave(float u)
        {
            float ph = (u % beat) / beat;
            float v = 0;
            if (ph < 0.08f) v = MathF.Sin(ph / 0.08f * H.PI) * 0.12f;                    // P
            else if (ph > 0.14f && ph < 0.17f) v = -(ph - 0.14f) / 0.03f * 0.2f;           // Q
            else if (ph >= 0.17f && ph < 0.21f) v = -0.2f + (ph - 0.17f) / 0.04f * 1.2f;   // R up
            else if (ph >= 0.21f && ph < 0.25f) v = 1f - (ph - 0.21f) / 0.04f * 1.35f;    // down to S
            else if (ph >= 0.25f && ph < 0.28f) v = -0.35f + (ph - 0.25f) / 0.03f * 0.35f;
            else if (ph > 0.38f && ph < 0.55f) v = MathF.Sin((ph - 0.38f) / 0.17f * H.PI) * 0.22f; // T
            if (health <= 0.3f) v *= 0.8f + 0.4f * MathF.Sin(u * 7.3f);               // stumbling
            return v;
        }
        float span = 1.6f, head = t % span / span;
        Vector2? prev = null;
        for (float i = 0; i <= w; i += 0.8f)
        {
            float u = (i / w) * span + MathF.Floor(t / span) * span;
            float age = (head - i / w + 1) % 1;
            var p = new Vector2(x + i, mid - Wave(u) * h * 0.5f);
            if (prev != null) { c.Stroke = new Color(col.R, col.G, col.B, MathF.Max(0.08f, 1 - age * 1.1f)); c.LineWidth = 0.9f; c.Line(prev.Value.X, prev.Value.Y, p.X, p.Y); }
            prev = p;
        }
        float hx = x + head * w;
        c.Glow(hx, mid - Wave(head * span + MathF.Floor(t / span) * span) * h * 0.5f, 5, new Color(col.R, col.G, col.B, 0.7f));
    }
    // ---- emblems ----------------------------------------------------------------------------------------------
    // A storm lantern: iron cap and ring, a glass chimney with the flame in it, a cage over the glass.
    public static void Lantern(Canvas c, float x, float y, float s, float t, float lit = 1, int seed = 0)
    {
        float flick = 0.82f + 0.18f * MathF.Sin(t * 11 + seed) * MathF.Sin(t * 6.1f + seed * 2);
        c.Glow(x, y + s * 0.2f, s * 3.2f, new Color(1, 0.7f, 0.32f, 0.22f * lit * flick));
        c.Stroke = H.Col("#151515"); c.LineWidth = s * 0.12f;
        c.BeginPath(); c.Arc(x, y - s * 0.95f, s * 0.22f, 0, H.PI * 2); c.StrokePath();
        c.Fill = H.Col("#2a2622"); c.Poly(new[] { new Vector2(x - s * 0.35f, y - s * 0.72f), new Vector2(x + s * 0.35f, y - s * 0.72f), new Vector2(x + s * 0.5f, y - s * 0.5f), new Vector2(x - s * 0.5f, y - s * 0.5f) });
        c.FillRectGradV(x - s * 0.36f, y - s * 0.5f, s * 0.72f, s * 0.95f, new Color(1, 0.86f, 0.5f, 0.55f + 0.35f * lit * flick), new Color(0.9f, 0.5f, 0.2f, 0.4f + 0.3f * lit));
        c.Glow(x, y, s * 0.4f, s * 0.55f, new Color(1.5f, 1.15f, 0.6f, 0.9f * lit * flick));
        c.Fill = new Color(1, 0.95f, 0.8f, lit); c.BeginPath(); c.Ellipse(x, y + s * 0.05f, s * 0.07f, s * 0.16f * flick, 0, 0, H.PI * 2); c.FillPath();
        c.Stroke = H.Col("#1c1a18"); c.LineWidth = s * 0.07f;
        c.Line(x - s * 0.36f, y - s * 0.5f, x - s * 0.36f, y + s * 0.45f); c.Line(x + s * 0.36f, y - s * 0.5f, x + s * 0.36f, y + s * 0.45f); c.Line(x, y - s * 0.5f, x, y + s * 0.45f);
        c.Fill = H.Col("#2a2622"); c.FillRect(x - s * 0.48f, y + s * 0.45f, s * 0.96f, s * 0.16f);
        c.Fill = new Color(BRASS.R, BRASS.G, BRASS.B, 0.6f); c.FillRect(x - s * 0.48f, y + s * 0.45f, s * 0.96f, s * 0.04f);
    }
    // A candle stub in a dish, wax run down it, the flame leaning in a draught.
    public static void Candle(Canvas c, float x, float y, float s, float t, float lit = 1)
    {
        float flick = 0.8f + 0.2f * MathF.Sin(t * 13) * MathF.Sin(t * 7.7f), lean = MathF.Sin(t * 1.7f) * 0.15f;
        c.Glow(x, y - s * 0.7f, s * 2.4f, new Color(1, 0.72f, 0.35f, 0.18f * lit * flick));
        c.Fill = H.Col("#3a2c1c"); c.BeginPath(); c.Ellipse(x, y + s * 0.55f, s * 0.75f, s * 0.18f, 0, 0, H.PI * 2); c.FillPath();
        c.FillRectGradH(x - s * 0.24f, y - s * 0.45f, s * 0.48f, s, H.Col("#d8ccae"), H.Col("#8a7e66"));
        c.Fill = H.Col("#efe6cc"); c.FillRect(x - s * 0.24f, y - s * 0.45f, s * 0.48f, s * 0.08f);
        c.Fill = H.Col("#e2d8bc"); c.FillRect(x - s * 0.22f, y - s * 0.4f, s * 0.08f, s * 0.6f); c.FillCircle(x - s * 0.18f, y + s * 0.22f, s * 0.06f);
        c.Stroke = H.Col("#151210"); c.LineWidth = 0.5f; c.Line(x, y - s * 0.45f, x, y - s * 0.6f);
        c.Save(); c.Translate(x, y - s * 0.62f); c.Rotate(lean);
        c.Fill = new Color(1, 0.6f, 0.2f, 0.8f * lit); c.BeginPath(); c.Ellipse(0, -s * 0.18f, s * 0.12f, s * 0.26f * flick, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = new Color(1, 0.95f, 0.75f, lit); c.BeginPath(); c.Ellipse(0, -s * 0.14f, s * 0.06f, s * 0.14f * flick, 0, 0, H.PI * 2); c.FillPath();
        c.Restore();
    }
    // A drowned skull: salt-white, the jaw gone, kelp through an eye, barnacles on the crown.
    public static void Skull(Canvas c, float x, float y, float s, float t, int seed = 0)
    {
        c.Glow(x, y, s * 1.8f, new Color(0.7f, 0.15f, 0.1f, 0.14f));
        c.Fill = new Color(0, 0, 0, 0.5f); c.BeginPath(); c.Ellipse(x + 0.6f, y + 0.8f, s * 0.62f, s * 0.58f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Col("#cfc8b4"); c.BeginPath(); c.Ellipse(x, y - s * 0.05f, s * 0.6f, s * 0.55f, 0, 0, H.PI * 2); c.FillPath();
        c.FillRect(x - s * 0.34f, y + s * 0.25f, s * 0.68f, s * 0.3f);
        c.Fill = H.Col("#efe9da"); c.BeginPath(); c.Ellipse(x - s * 0.15f, y - s * 0.25f, s * 0.3f, s * 0.2f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Col("#0c0a09");
        c.BeginPath(); c.Ellipse(x - s * 0.24f, y + s * 0.05f, s * 0.15f, s * 0.13f, 0, 0, H.PI * 2); c.FillPath();
        c.BeginPath(); c.Ellipse(x + s * 0.24f, y + s * 0.05f, s * 0.15f, s * 0.13f, 0, 0, H.PI * 2); c.FillPath();
        c.Poly(new[] { new Vector2(x, y + s * 0.2f), new Vector2(x - s * 0.07f, y + s * 0.34f), new Vector2(x + s * 0.07f, y + s * 0.34f) });
        for (int i = 0; i < 4; i++) { c.Fill = H.Col("#e0d8c2"); c.FillRect(x - s * 0.26f + i * s * 0.14f, y + s * 0.5f, s * 0.1f, s * 0.12f); }
        c.Glow(x + s * 0.24f, y + s * 0.05f, s * 0.12f, new Color(1, 0.3f, 0.2f, 0.5f + 0.3f * MathF.Sin(t * 2)));
        Kelp(c, new Vector2(x - s * 0.24f, y + s * 0.05f), H.PI / 2 + 0.3f, s * 1.3f, t, seed + 5, 1.6f);
        Barnacles(c, x + s * 0.25f, y - s * 0.4f, s * 0.2f, seed + 3, 5);
        Salt(c, x - s * 0.4f, y - s * 0.42f, s * 0.5f, seed + 7, 1.5f);
    }
    // A bronze bell on its yoke, swinging; when it rings, rings of sound go out from its lip.
    public static void Bell(Canvas c, float x, float y, float s, float t, float swing = 0.12f, int seed = 0)
    {
        float a = MathF.Sin(t * 1.6f) * swing;
        c.Fill = H.Col("#1c1712"); c.FillRect(x - s * 1.1f, y - s * 0.15f, s * 2.2f, s * 0.22f);
        c.Fill = new Color(BRASS.R, BRASS.G, BRASS.B, 0.3f); c.FillRect(x - s * 1.1f, y - s * 0.15f, s * 2.2f, s * 0.05f);
        Rivet(c, x - s * 0.9f, y - s * 0.04f); Rivet(c, x + s * 0.9f, y - s * 0.04f);
        c.Save(); c.Translate(x, y); c.Rotate(a);
        var body = new[] { new Vector2(-s * 0.2f, s * 0.08f), new Vector2(s * 0.2f, s * 0.08f), new Vector2(s * 0.45f, s * 0.35f), new Vector2(s * 0.52f, s * 0.85f), new Vector2(s * 0.7f, s * 1.1f), new Vector2(-s * 0.7f, s * 1.1f), new Vector2(-s * 0.52f, s * 0.85f), new Vector2(-s * 0.45f, s * 0.35f) };
        c.FillPolyGrad(body, new[] { BRASS_LIGHT, BRASS, BRASS_DARK, BRASS_DARK, BRASS_DARK, BRASS, BRASS, BRASS_LIGHT });
        c.Fill = new Color(VERDIGRIS.R, VERDIGRIS.G, VERDIGRIS.B, 0.45f); c.Poly(new[] { new Vector2(s * 0.3f, s * 0.4f), new Vector2(s * 0.48f, s * 0.6f), new Vector2(s * 0.55f, s * 1.0f), new Vector2(s * 0.35f, s * 0.95f) });
        c.Fill = new Color(1, 0.95f, 0.75f, 0.35f); c.FillRect(-s * 0.32f, s * 0.35f, s * 0.08f, s * 0.6f);
        c.Fill = H.Col("#3a2a14"); c.FillRect(-s * 0.7f, s * 1.04f, s * 1.4f, s * 0.08f);
        c.Fill = H.Col("#151210"); c.FillCircle(MathF.Sin(t * 1.6f - 0.6f) * s * 0.25f, s * 1.12f, s * 0.12f);
        Barnacles(c, -s * 0.45f, s * 0.9f, s * 0.14f, seed + 9, 3);
        c.Restore();
        // the sound, faint, in rings from the lip
        for (int i = 0; i < 3; i++)
        {
            float ph = (t * 0.35f + i / 3f) % 1;
            c.Stroke = new Color(AMBER.R, AMBER.G, AMBER.B, 0.18f * (1 - ph)); c.LineWidth = 0.6f;
            c.BeginPath(); c.Ellipse(x, y + s * 1.1f, s * (0.8f + ph * 2.2f), s * (0.25f + ph * 0.6f), 0, 0, H.PI * 2); c.StrokePath();
        }
    }
    // An iron hook with a short chain, for something to hang from.
    public static void Hook(Canvas c, float x, float y, float len)
    {
        c.Stroke = H.Col("#2a2826"); c.LineWidth = 0.8f;
        for (float d = 0; d < len; d += 2.4f) { c.BeginPath(); c.Ellipse(x, y + d + 1.2f, d % 4.8f < 2.4f ? 0.7f : 0.35f, 1.2f, 0, 0, H.PI * 2); c.StrokePath(); }
    }
    // The Mere-Saint's eye under the water: a great dark eye ringed in too many ribs, sea-green light moving in it.
    // Asleep (locked) it is shut and crusted over with salt.
    public static void SaintEye(Canvas c, float x, float y, float s, float t, bool awake)
    {
        var sea = SEAGLASS;
        c.Glow(x, y, s * 2.4f, s * 1.6f, new Color(sea.R, sea.G, sea.B, awake ? 0.2f + 0.06f * MathF.Sin(t * 1.3f) : 0.05f));
        // the ribs, curving round it like a cage
        var bone = awake ? new Color(0.86f, 0.83f, 0.74f, 0.9f) : new Color(0.6f, 0.62f, 0.6f, 0.6f);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 5; i++)
            {
                float k = i / 4f, rx = s * (0.75f + k * 0.35f), ry = s * (0.55f + k * 0.28f);
                c.Stroke = new Color(0, 0, 0, 0.5f); c.LineWidth = s * 0.1f;
                c.BeginPath(); c.Ellipse(x + side * s * 0.02f, y + 0.6f, rx, ry, 0, side < 0 ? H.PI * 0.62f : -H.PI * 0.38f, side < 0 ? H.PI * 1.38f : H.PI * 0.38f); c.StrokePath();
                c.Stroke = bone; c.LineWidth = s * 0.07f;
                c.BeginPath(); c.Ellipse(x + side * s * 0.02f, y, rx, ry, 0, side < 0 ? H.PI * 0.62f : -H.PI * 0.38f, side < 0 ? H.PI * 1.38f : H.PI * 0.38f); c.StrokePath();
            }
        if (awake)
        {
            float blink = (t % 6.5f) < 0.18f ? 0.15f : 1;
            float look = MathF.Sin(t * 0.7f) * s * 0.12f;
            c.Fill = H.Col("#0a1a1a"); c.BeginPath(); c.Ellipse(x, y, s * 0.6f, s * 0.42f * blink, 0, 0, H.PI * 2); c.FillPath();
            c.Glow(x + look, y, s * 0.5f, s * 0.4f * blink, new Color(sea.R, sea.G, sea.B, 0.5f));
            c.Fill = new Color(sea.R, sea.G, sea.B, 0.95f); c.BeginPath(); c.Ellipse(x + look, y, s * 0.3f, s * 0.3f * blink, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = H.Col("#021010"); c.BeginPath(); c.Ellipse(x + look, y, s * 0.07f, s * 0.26f * blink, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = new Color(1, 1, 1, 0.7f * blink); c.FillCircle(x + look - s * 0.1f, y - s * 0.1f, s * 0.05f);
            c.Stroke = new Color(sea.R, sea.G, sea.B, 0.6f); c.LineWidth = s * 0.04f;
            c.BeginPath(); c.Ellipse(x, y, s * 0.6f, s * 0.42f * blink, 0, 0, H.PI * 2); c.StrokePath();
        }
        else
        {
            // shut, and salted over
            c.Stroke = new Color(0.5f, 0.55f, 0.55f, 0.85f); c.LineWidth = s * 0.07f;
            c.BeginPath(); c.MoveTo(x - s * 0.6f, y); c.QuadraticCurveTo(x, y + s * 0.3f, x + s * 0.6f, y); c.StrokePath();
            Salt(c, x - s * 0.7f, y + s * 0.05f, s * 1.4f, 7, s * 0.3f);
            Barnacles(c, x + s * 0.45f, y - s * 0.2f, s * 0.2f, 9, 5);
        }
    }
}
