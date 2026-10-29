// Monuments: one large story prop in each room where something happened, standing against the back wall behind
// the play, so a player who stops to look learns what the place was without a word being said. The first Hult
// holding up a lamp with no flame in it; the little boats the chapel nailed up for every ship the reef took; the
// tithe scales, one pan for salt and one worn smooth by a kneeling man; the Abbot with salt growing out of his
// open palm; a brother keeping the Rule with his hands over his eyes; a bell-roper who never let go; the great
// bell fallen through the Choir floor; a child's mitten at the top of the Ribvault pegs; a ship half swallowed in
// the Gullet; the old lighthouse lens, cracked through.
// Painted once into the back-wall bake (encoded value/accent/glow, so the world shader lights them and gives them
// relief). StoryFocus finds them here and leans the camera, the focus and the light toward the one Maren is near.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public class Monument
{
    public string id, room, kind;
    public int hint, hintY = -1;          // preferred local tile (column; row, or -1 for the lowest floor)
    public int w, h;                      // clear air it needs, in tiles
    public Color tint;                    // the colour the light leans toward while Maren is near it
    public float x, floor;                // placed: bottom centre, world px
    public float Cy => floor - h * H.TILE * 0.5f;
    public Monument Copy() => (Monument)MemberwiseClone();
}

public static class Monuments
{
    const int TS = H.TILE;
    static Color V(float v, float a = 1) => Canvas.V(v, a);
    static Color VA(float v, float acc, float a = 1) => Canvas.VA(v, acc, a);
    static Color Gl(float g, float a = 1) => Canvas.Gl(g, a);

    public static readonly Monument[] DEFS =
    {
        new() { id = "keeper", room = "seawall", kind = "keeper", hint = 9, w = 3, h = 6, tint = new Color(0.86f, 0.94f, 1.14f) },
        new() { id = "votives", room = "chapel", kind = "votives", hint = 24, w = 5, h = 6, tint = new Color(1.12f, 1.0f, 0.86f) },
        new() { id = "scales", room = "tithe", kind = "scales", hint = 20, w = 6, h = 6, tint = new Color(1.06f, 1.04f, 0.98f) },
        new() { id = "abbot", room = "kilns", kind = "abbot", hint = 12, w = 4, h = 6, tint = new Color(1.18f, 0.98f, 0.8f) },
        new() { id = "weeper", room = "cloister", kind = "weeper", hint = 21, hintY = 16, w = 4, h = 5, tint = new Color(0.92f, 1.08f, 0.96f) },
        new() { id = "roper", room = "nave", kind = "roper", hint = 14, w = 3, h = 7, tint = new Color(0.9f, 0.96f, 1.12f) },
        new() { id = "bell", room = "choir", kind = "bell", hint = 30, w = 5, h = 4, tint = new Color(1.16f, 1.04f, 0.82f) },
        new() { id = "mitten", room = "ribshaft", kind = "mitten", hint = 24, hintY = 26, w = 3, h = 8, tint = new Color(1.14f, 0.96f, 0.92f) },
        new() { id = "swallowed", room = "gulletmouth", kind = "swallowed", hint = 18, hintY = 14, w = 6, h = 5, tint = new Color(0.86f, 1.1f, 1.06f) },
        new() { id = "lens", room = "lamp", kind = "lens", hint = 24, w = 4, h = 5, tint = new Color(1.1f, 1.06f, 0.96f) },
    };

    // ---- placement -------------------------------------------------------------------------------------------
    // Each stands on the nearest open stretch of floor to its hint, from the room's original map (so it never moves
    // when salt grows or a door opens). Ladders and platforms may cross in front of it: it is behind the play.
    static readonly Dictionary<string, Monument> placed = new();
    static bool Air(char ch) => World.Solidity(ch) == 0 || ch is 'E' or 'L' or 'V' or 'K' or 'Z' or 'Y' or 'h' or 'Q' or 'J';
    static bool Floor(char ch) => ch is '#' or '_' or 'T' or 'R' or 'M' or 'P' or 'S' or 'C' or 'G' or '=' or 'b';
    public static Monument For(RoomDef r)
    {
        if (r == null) return null;
        if (placed.TryGetValue(r.Id, out var m)) return m;
        var def = DEFS.FirstOrDefault(d => d.room == r.Id);
        if (def == null || r.Base == null) { placed[r.Id] = null; return null; }
        char At(int lx, int ly) => lx < 0 || ly < 0 || lx >= r.Tw || ly >= r.Th ? '#' : r.Base[ly * r.Tw + lx];
        int hw = def.w / 2;
        (int lx, int ly)? best = null; float bs = 1e9f;
        for (int ly = def.h; ly < r.Th - 1; ly++)
            for (int lx = hw + 2; lx < r.Tw - hw - 3; lx++)
            {
                int blocked = 0, floor = 0;
                for (int dx = -hw; dx <= hw; dx++)
                {
                    for (int dy = 0; dy < def.h; dy++) if (!Air(At(lx + dx, ly - dy))) blocked++;
                    if (Floor(At(lx + dx, ly + 1))) floor++;
                }
                if (blocked > 1 || !Floor(At(lx, ly + 1)) || floor * 2 < def.w) continue;
                float s = MathF.Abs(lx - def.hint) + (def.hintY >= 0 ? MathF.Abs(ly - def.hintY) * 0.7f : (r.Th - ly) * 0.15f);
                if (s < bs) { bs = s; best = (lx, ly); }
            }
        if (best == null) { placed[r.Id] = null; return null; }
        m = def.Copy();
        m.x = r.Px + best.Value.lx * TS + TS / 2f; m.floor = r.Py + (best.Value.ly + 1) * TS;
        placed[r.Id] = m;
        return m;
    }

    // ---- painting --------------------------------------------------------------------------------------------
    public static void Paint(Canvas c, RoomDef r)
    {
        var m = For(r);
        if (m == null) return;
        float g0 = c.EncGain; c.EncGain = 0.8f;   // a little under the walls' own props, so they sit back in the dark until lit
        var rng = new Random(m.id.Aggregate(7, (h, ch) => unchecked(h * 31 + ch)));
        float x = m.x, f = m.floor;
        // a little darkness on the wall behind it, so it stands off the stone rather than being printed on it
        c.Glow(x, f - m.h * TS * 0.45f, m.w * TS * 0.75f, m.h * TS * 0.6f, V(0, 0.55f));
        switch (m.kind)
        {
            case "keeper": Keeper(c, x, f, rng); break;
            case "votives": Votives(c, x, f, rng); break;
            case "scales": Scales(c, x, f, rng); break;
            case "abbot": Abbot(c, x, f, rng); break;
            case "weeper": Weeper(c, x, f, rng); break;
            case "roper": Roper(c, x, f, rng); break;
            case "bell": FallenBell(c, x, f, rng); break;
            case "mitten": Mitten(c, x, f, rng); break;
            case "swallowed": Swallowed(c, x, f, rng); break;
            case "lens": Lens(c, x, f, rng); break;
        }
        c.EncGain = g0;
    }

    // A filled shape lit from the upper left: each vertex a little brighter toward the light, so the relief in the
    // world shader turns the figure round.
    static void Shape(Canvas c, float ox, float oy, float v, float spread, float acc, params float[] xy)
    {
        int n = xy.Length / 2;
        var p = new Vector2[n]; var cols = new Color[n];
        float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
        for (int i = 0; i < n; i++) { p[i] = new Vector2(ox + xy[i * 2], oy + xy[i * 2 + 1]); x0 = MathF.Min(x0, p[i].X); x1 = MathF.Max(x1, p[i].X); y0 = MathF.Min(y0, p[i].Y); y1 = MathF.Max(y1, p[i].Y); }
        float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hw = MathF.Max(1, (x1 - x0) / 2), hh = MathF.Max(1, (y1 - y0) / 2);
        for (int i = 0; i < n; i++)
        {
            float vv = v + spread * (-0.6f * (p[i].X - cx) / hw - 0.4f * (p[i].Y - cy) / hh);
            cols[i] = acc > 0 ? VA(vv, acc) : V(vv);
        }
        c.FillPolyGrad(p, cols);
    }
    static void S(Canvas c, float ox, float oy, float v, params float[] xy) => Shape(c, ox, oy, v, 0.06f, 0, xy);
    static void Plinth(Canvas c, float x, float f, float w, float h, Random rng)
    {
        c.FillRectGradV(x - w / 2 - 3, f - 5, w + 6, 5, V(0.2f), V(0.12f));
        c.FillRectGradV(x - w / 2, f - h, w, h - 5, V(0.22f), V(0.14f));
        c.Fill = V(0.32f); c.FillRect(x - w / 2, f - h, w, 1.2f); c.FillRect(x - w / 2 - 3, f - 5, w + 6, 1);
        c.Fill = V(0.07f); c.FillRect(x + w / 2 - 2, f - h + 1, 2, h - 6);
        // the stone has been there long enough to crack
        c.Stroke = V(0.06f); c.LineWidth = 0.7f;
        float cx = x + (float)(rng.NextDouble() - 0.5) * w * 0.6f;
        c.BeginPath(); c.MoveTo(cx, f - h); c.LineTo(cx + 2, f - h + 4); c.LineTo(cx - 1, f - h + 7); c.LineTo(cx + 1.5f, f - 5); c.StrokePath();
    }
    static void Streaks(Canvas c, float x0, float x1, float y0, float y1, Random rng, int n, float v = 0.1f)
    {
        c.Stroke = V(v, 0.7f); c.LineWidth = 0.8f;
        for (int i = 0; i < n; i++)
        {
            float sx = x0 + (float)rng.NextDouble() * (x1 - x0), len = (y1 - y0) * (0.3f + 0.7f * (float)rng.NextDouble());
            c.Line(sx, y0, sx + (float)(rng.NextDouble() - 0.5) * 1.5f, y0 + len);
        }
    }

    // The first Hult, on the seawall she built, holding a lamp up toward the reef. There is no flame in it. Someone
    // has tied a strip of red cloth round her wrist.
    static void Keeper(Canvas c, float x, float f, Random rng)
    {
        Plinth(c, x, f, 26, 14, rng);
        float b = f - 14;
        S(c, x, b, 0.21f, -10, 0, -8, -16, -6.5f, -30, 6.5f, -30, 8.5f, -16, 11, 0, 6, 1, 1, -1, -4, 1);            // the long coat
        c.Stroke = V(0.12f); c.LineWidth = 0.8f; c.Line(x - 2, b - 28, x - 4, b - 1); c.Line(x + 3, b - 26, x + 5, b - 1);
        S(c, x, b, 0.24f, -6.5f, -30, -5.5f, -43, 5, -43, 6.5f, -30);                                                // the body
        S(c, x, b, 0.23f, -5.5f, -42, -8.5f, -30, -7, -29, -4, -39);                                                // the arm at her side
        S(c, x, b, 0.25f, 3.5f, -42, 8.5f, -54, 10.5f, -64, 8.5f, -65, 6.5f, -55, 2.5f, -39);                       // the arm held up
        c.Fill = V(0.26f); c.FillCircle(x - 0.5f, b - 47.5f, 4.4f);                                                   // her head, in a scarf
        S(c, x, b, 0.2f, -4.4f, -48, -7.5f, -39, -2.5f, -43);
        c.Fill = V(0.05f); c.FillCircle(x + 1.8f, b - 47, 1.3f);
        // the lamp: a cage with no glass lit in it
        float lx = x + 10, ly = b - 72;
        c.Stroke = V(0.3f); c.LineWidth = 1; c.BeginPath(); c.Arc(lx, ly - 5, 2.5f, H.PI, 0); c.StrokePath();
        c.Fill = V(0.04f); c.FillRect(lx - 3.5f, ly - 3, 7, 9);
        c.Fill = V(0.3f); c.FillRect(lx - 4, ly - 4, 8, 1.4f); c.FillRect(lx - 4, ly + 6, 8, 1.6f);
        c.FillRect(lx - 4, ly - 3, 1, 9); c.FillRect(lx + 3, ly - 3, 1, 9); c.FillRect(lx - 0.5f, ly - 3, 1, 9);
        c.Fill = Gl(0.06f, 0.5f); c.FillRect(lx - 2.6f, ly - 2, 0.8f, 3);                                             // moonlight on the glass, no more
        // the red cloth, and its tail lifting in the wind off the sea
        c.Fill = VA(0.3f, 1); c.FillRect(x + 8, b - 60, 4, 2.2f);
        Shape(c, x, b, 0.28f, 0.04f, 1, 11.5f, -60, 17, -61.5f, 20, -58, 15, -59, 11.5f, -58);
        Streaks(c, x - 7, x + 8, b - 42, b - 4, rng, 7);
        // lichen where the spray reaches
        for (int i = 0; i < 12; i++) { c.Fill = VA(0.19f, 0.35f, 0.8f); c.FillCircle(x + (float)(rng.NextDouble() - 0.5) * 24, f - (float)rng.NextDouble() * 18, 0.8f + (float)rng.NextDouble()); }
    }

    // The chapel's board of little boats, one carved and nailed up for every ship the reef took. The old ones are
    // dark with lamp smoke. Eleven are new.
    static void Votives(Canvas c, float x, float f, Random rng)
    {
        float top = f - 88, w = 64, hgt = 54;
        c.FillRectGradV(x - w / 2, top, w, hgt, V(0.17f), V(0.12f));
        c.Fill = V(0.09f); for (int i = 1; i < 5; i++) c.FillRect(x - w / 2, top + i * hgt / 5, w, 0.8f);
        c.Fill = V(0.24f); c.FillRect(x - w / 2 - 2, top - 3, w + 4, 3); c.FillRect(x - w / 2 - 1, top + hgt, w + 2, 2.5f);
        c.Fill = V(0.06f); c.FillRect(x + w / 2 - 1.5f, top, 1.5f, hgt);
        int k = 0;
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++, k++)
            {
                float bx = x - w / 2 + 7 + col * 12.5f + (row % 2) * 3, by = top + 14 + row * 16;
                bool fresh = k >= 4;   // the four oldest, then the eleven since the light went out
                float v = fresh ? 0.36f : 0.2f;
                S(c, bx, by, v, -5, 0, 5, 0, 3.5f, 2.6f, -3.5f, 2.6f);
                c.Fill = V(v - 0.04f); c.FillRect(bx - 0.4f, by - 8, 0.9f, 8);
                S(c, bx, by, v + 0.02f, 0.5f, -7.5f, 4.5f, -1.5f, 0.5f, -1.5f);
                c.Fill = V(0.5f); c.FillCircle(bx, by + 1.2f, 0.5f);                                     // the nail
                if (fresh) { c.Stroke = V(0.08f, 0.8f); c.LineWidth = 0.5f; c.Line(bx - 3, by + 5, bx + 3, by + 5); }   // a name scratched under it
            }
        // the shelf of candles under it, every one burned out
        c.Fill = V(0.22f); c.FillRect(x - 26, f - 30, 52, 3);
        c.Fill = V(0.1f); c.FillRect(x - 26, f - 27, 52, 1.5f);
        for (int i = 0; i < 9; i++)
        {
            float cx = x - 22 + i * 5.4f + (float)(rng.NextDouble() - 0.5) * 2, ch = 3 + (float)rng.NextDouble() * 6;
            c.FillRectGradV(cx - 1.3f, f - 30 - ch, 2.6f, ch, V(0.42f), V(0.3f));
            c.Fill = V(0.36f); c.FillRect(cx - 1.8f, f - 30.5f, 3.6f, 1.2f);
            c.Fill = V(0.05f); c.FillRect(cx - 0.2f, f - 31.5f - ch, 0.5f, 1.5f);
        }
        Streaks(c, x - 24, x + 24, f - 27, f - 6, rng, 9, 0.3f);   // wax run down the wall
    }

    // The tithe scales: one pan heaped with salt, the other empty and worn smooth in the shape of a man kneeling.
    // The man's side hangs lower.
    static void Scales(Canvas c, float x, float f, Random rng)
    {
        Plinth(c, x, f, 22, 10, rng);
        c.FillRectGradH(x - 2.5f, f - 86, 5, 76, V(0.28f), V(0.14f));
        c.Fill = V(0.3f); c.FillCircle(x, f - 89, 3.2f);
        float px = x, py = f - 84, ang = 0.1f, half = 38;
        float lx = px - MathF.Cos(ang) * half, ly = py - MathF.Sin(ang) * half, rx = px + MathF.Cos(ang) * half, ry = py + MathF.Sin(ang) * half;
        c.Stroke = V(0.26f); c.LineWidth = 3; c.Line(lx, ly, rx, ry);
        c.Stroke = V(0.36f); c.LineWidth = 0.8f; c.Line(lx, ly - 1.2f, rx, ry - 1.2f);
        c.Fill = V(0.32f); c.FillCircle(px, py, 2.6f);
        void Pan(float ex, float ey, float drop, bool salt)
        {
            float pyy = ey + drop;
            c.Stroke = V(0.22f); c.LineWidth = 0.8f;
            c.Line(ex, ey, ex - 11, pyy); c.Line(ex, ey, ex + 11, pyy); c.Line(ex, ey, ex, pyy);
            S(c, ex, pyy, 0.25f, -13, 0, 13, 0, 9, 5, -9, 5);
            c.Fill = V(0.34f); c.FillRect(ex - 13, pyy - 0.5f, 26, 1);
            if (salt)
            {
                // a heap of salt, bright and crystalline
                Shape(c, ex, pyy, 0.55f, 0.1f, 0, -11, 0, -6, -5, -1, -7.5f, 4, -6, 10, 0);
                for (int i = 0; i < 8; i++) { c.Fill = Gl(0.05f, 0.7f); c.FillRect(ex - 8 + (float)rng.NextDouble() * 16, pyy - 1 - (float)rng.NextDouble() * 5, 0.7f, 0.7f); }
            }
            else
            {
                // the pan polished where knees and hands have rested in it
                c.Fill = V(0.42f, 0.6f); c.FillCircle(ex - 4, pyy + 1.6f, 2.2f); c.FillCircle(ex + 4, pyy + 1.6f, 2.2f); c.FillRect(ex - 1, pyy + 2.2f, 2, 1);
            }
        }
        Pan(lx, ly, 34, true);
        Pan(rx, ry, 40, false);
        Streaks(c, x - 2, x + 2, f - 80, f - 12, rng, 3);
    }

    // The Abbot, in stone, holding out his open hand. The salt has grown out of his palm like a second hand
    // reaching back to him.
    static void Abbot(Canvas c, float x, float f, Random rng)
    {
        Plinth(c, x, f, 28, 12, rng);
        float b = f - 12;
        S(c, x, b, 0.22f, -12, 0, -10, -30, -8, -46, -6.5f, -52, 7, -52, 9, -44, 10, -26, 12, 0);                    // robe
        c.Stroke = V(0.13f); c.LineWidth = 0.9f;
        c.BeginPath(); c.MoveTo(x - 4, b - 48); c.QuadraticCurveTo(x - 7, b - 24, x - 6, b); c.StrokePath();
        c.BeginPath(); c.MoveTo(x + 1, b - 46); c.QuadraticCurveTo(x + 2, b - 22, x + 3, b); c.StrokePath();
        c.BeginPath(); c.MoveTo(x + 5, b - 40); c.QuadraticCurveTo(x + 8, b - 20, x + 8, b); c.StrokePath();
        c.Fill = V(0.18f); c.FillRect(x - 9, b - 34, 18, 2);                                                         // the rope belt
        S(c, x, b, 0.25f, -7, -52, -6.5f, -61, -2, -66.5f, 3.5f, -65, 7, -60, 7, -52, 3, -55, -3, -55);              // hood
        c.Fill = V(0.03f); c.BeginPath(); c.Ellipse(x + 1.5f, b - 58, 3.2f, 3.8f, 0, 0, H.PI * 2); c.FillPath();    // no face in it
        S(c, x, b, 0.24f, 5, -48, 19, -44, 21, -40, 6, -39);                                                         // the sleeve held out
        S(c, x, b, 0.28f, 19.5f, -43.5f, 26, -44.5f, 27, -43, 21, -40.5f);                                           // the open hand
        // the salt, growing up out of the palm and curling back toward him like fingers
        float hx = x + 24, hy = b - 44.5f;
        for (int i = 0; i < 6; i++)
        {
            float a = -1.9f + i * 0.28f, len = 9 + i * 2.2f + (float)rng.NextDouble() * 3;
            float tx = hx + MathF.Cos(a) * len, ty = hy + MathF.Sin(a) * len;
            float nx = -MathF.Sin(a) * 1.4f, ny = MathF.Cos(a) * 1.4f;
            var pts = new[] { new Vector2(hx + nx, hy + ny), new Vector2((hx + tx) / 2 + nx * 1.6f, (hy + ty) / 2 + ny * 1.6f), new Vector2(tx, ty), new Vector2((hx + tx) / 2 - nx * 1.2f, (hy + ty) / 2 - ny * 1.2f), new Vector2(hx - nx, hy - ny) };
            c.FillPolyGrad(pts, new[] { V(0.4f), V(0.6f), V(0.75f), V(0.5f), V(0.38f) });
            c.Fill = Gl(0.08f, 0.8f); c.FillRect(tx - 0.5f, ty - 0.5f, 1, 1);
        }
        // salt crusting his shoulders and the folds of the robe
        for (int i = 0; i < 26; i++) { c.Fill = V(0.5f + (float)rng.NextDouble() * 0.2f, 0.85f); c.FillRect(x - 8 + (float)rng.NextDouble() * 16, b - 52 + (float)rng.NextDouble() * 30, 1, 1); }
        Streaks(c, x - 8, x + 9, b - 30, b - 2, rng, 6);
        // the inscription on the plinth, worn shallow
        c.Fill = V(0.1f); for (int i = 0; i < 7; i++) c.FillRect(x - 9 + i * 2.7f, f - 8.5f, 1.6f, 1.2f);
    }

    // A brother of the Cloister keeping the Rule: Do not look into the water. He has his hands over his eyes, and
    // the moss has grown over his shoulders while he waited.
    static void Weeper(Canvas c, float x, float f, Random rng)
    {
        Plinth(c, x - 4, f, 22, 8, rng);
        float b = f - 8;
        S(c, x - 4, b, 0.21f, -10, 0, -9, -24, -7.5f, -38, 5, -40, 8, -28, 10, 0);                                  // robe
        S(c, x - 4, b, 0.24f, -7.5f, -38, -6, -48, -1, -52, 5, -50, 8.5f, -44, 6, -39);                              // hood, bowed toward the water
        S(c, x - 4, b, 0.23f, -4, -38, 3, -44, 5.5f, -42, -1, -35);                                                 // arms up to the face
        S(c, x - 4, b, 0.25f, 2, -39, 6, -45, 8, -43, 4, -37);
        c.Fill = V(0.3f); c.FillCircle(x + 3, b - 45, 2.2f); c.FillCircle(x + 1.6f, b - 43, 2);                       // the hands over the eyes
        c.Fill = V(0.06f); c.FillRect(x + 1, b - 47.5f, 3, 1.2f);
        // the stain where water has run down from his hands for a long time
        c.Fill = V(0.08f, 0.75f); c.FillRect(x + 1.5f, b - 41, 1.2f, 32); c.FillRect(x + 3, b - 38, 0.8f, 22);
        c.Fill = Gl(0.03f, 0.5f); c.FillRect(x + 1.8f, b - 30, 0.6f, 6);
        // moss on his shoulders and the top of the hood
        for (int i = 0; i < 30; i++)
        {
            float a = (float)rng.NextDouble();
            float mx = H.Lerp(x - 11, x + 5, a), my = b - 38 - MathF.Sin(a * H.PI) * 12 + (float)rng.NextDouble() * 4;
            c.Fill = VA(0.24f, 0.7f, 0.9f); c.FillCircle(mx, my, 0.9f + (float)rng.NextDouble() * 1.3f);
        }
        for (int i = 0; i < 10; i++) { c.Fill = VA(0.2f, 0.6f, 0.9f); c.FillCircle(x - 14 + (float)rng.NextDouble() * 22, f - (float)rng.NextDouble() * 3, 1 + (float)rng.NextDouble()); }
        // the basin he will not look into
        float bx = x + 20;
        S(c, bx, f, 0.22f, -9, 0, -10, -9, 10, -9, 9, 0);
        c.Fill = V(0.03f); c.FillRect(bx - 8.5f, f - 8.5f, 17, 2.5f);
        c.Fill = Gl(0.05f, 0.7f); c.FillRect(bx - 3, f - 8, 4, 0.6f);
    }

    // The last of the Bell-Ropers, who rang the warning a full tide too late. The rope comes down out of the dark;
    // he sits at the foot of it with his hand still closed round it.
    static void Roper(Canvas c, float x, float f, Random rng)
    {
        float rx = x + 3;
        // the rope, out of the dark above
        float top = f - 220;
        for (float y = top; y < f - 18; y += 3)
        {
            float k = (y - top) / (f - 18 - top);
            float sx = rx + MathF.Sin(y * 0.03f) * 1.5f * k;
            c.Fill = V(0.25f * MathF.Min(1, k * 2.5f), MathF.Min(1, 0.2f + k * 2));
            c.FillRect(sx - 1.6f, y, 3.2f, 3.2f);
            c.Fill = V(0.14f * MathF.Min(1, k * 2.5f), MathF.Min(1, 0.2f + k * 2)); c.FillRect(sx - 1.6f, y + 1.8f, 3.2f, 0.8f);
        }
        // its end, coiled on the floor
        for (int i = 0; i < 4; i++) { c.Fill = V(0.22f + i * 0.02f); c.BeginPath(); c.Ellipse(rx + 6, f - 2 - i * 2.4f, 12 - i * 1.8f, 2.6f, 0, 0, H.PI * 2); c.FillPath(); c.Fill = V(0.12f); c.FillRect(rx - 4 + i * 2, f - 2.5f - i * 2.4f, 20 - i * 3.6f, 0.6f); }
        // the ringer, slumped against it, bowed
        S(c, x, f, 0.17f, -16, 0, -17, -9, -13, -19, -7, -24, -1.5f, -20, 1, -10, 3, 0);
        c.Fill = V(0.33f); c.FillCircle(x - 7.5f, f - 25.5f, 3.4f);
        c.Fill = V(0.05f); c.FillCircle(x - 6, f - 25.5f, 1); c.FillRect(x - 8.5f, f - 23.5f, 2.5f, 0.7f);
        // the hand, bone now, closed round the rope
        c.Stroke = V(0.4f); c.LineWidth = 0.9f;
        c.Line(x - 2, f - 16, rx - 1.5f, f - 27); c.Line(x - 1, f - 15, rx - 1.5f, f - 25);
        for (int i = 0; i < 3; i++) { c.Line(rx - 2, f - 28 + i * 1.6f, rx + 2, f - 27.5f + i * 1.6f); }
        Streaks(c, x - 14, x - 2, f - 18, f - 2, rng, 4, 0.08f);
    }

    // The great bell of the Choir, fallen through the floor of its tower and lying on its side, cracked. It must
    // have rung once more when it landed, loud enough to hear on the mainland.
    static void FallenBell(Canvas c, float x, float f, Random rng)
    {
        float cy = f - 21;
        float Half(float u) => u < 0.25f ? 11 + u * 4 : u < 0.55f ? 12 + (u - 0.25f) * 6 : 13.8f + MathF.Pow((u - 0.55f) / 0.45f, 2) * 9.5f;
        int n = 14; float x0 = x - 26, x1 = x + 22;
        var pts = new List<Vector2>(); var cols = new List<Color>();
        for (int i = 0; i <= n; i++) { float u = i / (float)n; float px = H.Lerp(x0, x1, u); pts.Add(new Vector2(px, cy - Half(u))); cols.Add(VA(0.32f - u * 0.05f, 0.45f)); }
        for (int i = n; i >= 0; i--) { float u = i / (float)n; float px = H.Lerp(x0, x1, u); pts.Add(new Vector2(px, MathF.Min(f + 2, cy + Half(u)))); cols.Add(VA(0.16f - u * 0.03f, 0.35f)); }
        c.FillPolyGrad(pts.ToArray(), cols.ToArray());
        // the crown and its canons
        c.Fill = VA(0.26f, 0.4f); c.FillRect(x0 - 5, cy - 7, 6, 14);
        c.Stroke = VA(0.3f, 0.45f); c.LineWidth = 1.6f;
        c.BeginPath(); c.Arc(x0 - 6, cy - 3.5f, 3, H.PI * 0.5f, H.PI * 1.5f); c.StrokePath();
        c.BeginPath(); c.Arc(x0 - 6, cy + 3.5f, 3, H.PI * 0.5f, H.PI * 1.5f); c.StrokePath();
        // gilt bands following the waist and the sound bow
        foreach (float u in new[] { 0.2f, 0.62f, 0.86f })
        {
            float px = H.Lerp(x0, x1, u), hh = Half(u);
            c.Fill = VA(0.44f, 0.9f); c.FillRect(px - 0.9f, cy - hh, 1.8f, MathF.Min(f + 2, cy + hh) - (cy - hh));
            c.Fill = Gl(0.06f, 0.7f); c.FillRect(px - 0.4f, cy - hh + 2, 0.8f, 4);
        }
        // the mouth: the dark inside it, and the lip
        c.Fill = V(0.02f); c.BeginPath(); c.Ellipse(x1 + 0.5f, cy, 4.5f, 22, 0, 0, H.PI * 2); c.FillPath();
        c.Stroke = VA(0.38f, 0.6f); c.LineWidth = 1.4f; c.BeginPath(); c.Ellipse(x1 + 0.5f, cy, 4.5f, 22, 0, -H.PI * 0.5f, H.PI * 0.4f); c.StrokePath();
        // the crack
        c.Stroke = V(0.02f); c.LineWidth = 1.1f;
        c.BeginPath(); c.MoveTo(x1, cy - 19); c.LineTo(x1 - 6, cy - 15); c.LineTo(x1 - 9, cy - 16); c.LineTo(x1 - 15, cy - 10); c.LineTo(x1 - 18, cy - 11); c.StrokePath();
        // the clapper, thrown out onto the floor
        S(c, x1 + 14, f, 0.24f, -6, -1, -4, -4.5f, 2, -5, 7, -3.5f, 8, -1, 2, 0);
        // the floor it came through, in pieces around it
        for (int i = 0; i < 14; i++)
        {
            float sx = x0 - 8 + (float)rng.NextDouble() * (x1 - x0 + 30), sz = 1.5f + (float)rng.NextDouble() * 3.5f;
            S(c, sx, f, 0.2f + (float)rng.NextDouble() * 0.06f, -sz, 0, -sz * 0.6f, -sz, sz * 0.7f, -sz * 0.8f, sz, 0);
        }
    }

    // A line of iron pegs up the bone, worn bright by climbers. At the highest one a child could reach, a small red
    // mitten is still tied on. AGATHE, AGE NINE, ALL THE WAY TO THE TOP.
    static void Mitten(Canvas c, float x, float f, Random rng)
    {
        Vector2 Peg(int i) => new(x - 16 + i * 5f + MathF.Sin(i * 1.7f) * 3, f - 12 - i * 14f);
        const int N = 8;
        // the climbing line, run from peg to peg and worn pale where hands went
        c.Stroke = V(0.3f); c.LineWidth = 1.4f;
        c.BeginPath(); for (int i = 0; i < N - 1; i++) { var a = Peg(i); var b = Peg(i + 1); if (i == 0) c.MoveTo(a.X + 3, a.Y); c.QuadraticCurveTo((a.X + b.X) / 2 + 5, (a.Y + b.Y) / 2 + 3, b.X + 3, b.Y); } c.StrokePath();
        for (int i = 0; i < N; i++)
        {
            var p = Peg(i);
            c.Fill = V(0.03f, 0.8f); c.FillRect(p.X - 4, p.Y - 0.5f, 11, 4.5f);
            c.FillRectGradV(p.X - 5, p.Y - 2.5f, 11, 4, V(0.5f), V(0.24f));
            c.Fill = V(0.36f); c.FillCircle(p.X - 5, p.Y - 0.5f, 2.6f);
            c.Fill = V(0.6f); c.FillRect(p.X - 3, p.Y - 2.5f, 7, 0.9f);
        }
        // a frayed rope left on the third peg
        var r0 = Peg(2);
        c.Stroke = V(0.26f); c.LineWidth = 2f;
        c.BeginPath(); c.MoveTo(r0.X + 3, r0.Y + 1); c.QuadraticCurveTo(r0.X + 8, r0.Y + 16, r0.X + 4, r0.Y + 28); c.StrokePath();
        c.LineWidth = 0.7f; c.Line(r0.X + 4, r0.Y + 28, r0.X + 2, r0.Y + 32); c.Line(r0.X + 4, r0.Y + 28, r0.X + 6, r0.Y + 32);
        // the mitten, on the highest peg
        var top = Peg(N - 1);
        c.Stroke = VA(0.32f, 0.8f); c.LineWidth = 0.8f; c.Line(top.X + 1, top.Y + 1, top.X + 2, top.Y + 6);
        Shape(c, top.X + 2, top.Y + 6, 0.34f, 0.08f, 1, -3.6f, 0, 3.6f, 0, 4.2f, 6, 3, 10, -2.7f, 10, -3.9f, 6);
        Shape(c, top.X + 2, top.Y + 6, 0.36f, 0.05f, 1, 3.6f, 3.8f, 6.6f, 3, 6.9f, 5.4f, 4.2f, 6.6f);
        c.Fill = VA(0.48f, 0.4f); c.FillRect(top.X - 1, top.Y + 14.5f, 6.6f, 1.8f);
        // the climbers' count, notched beside the lowest pegs
        c.Fill = V(0.05f);
        for (int g = 0; g < 5; g++)
            for (int i = 0; i < 5; i++)
            {
                float tx = x + 10 + g * 8 + i * 1.5f, ty = f - 14 - (g % 2) * 9;
                if (i < 4) c.FillRect(tx, ty, 0.9f, 6); else { c.Save(); c.Translate(tx - 3, ty + 3); c.Rotate(-0.9f); c.FillRect(-4, -0.45f, 8, 0.9f); c.Restore(); }
            }
        Streaks(c, x - 14, x + 18, f - 100, f - 20, rng, 5, 0.09f);
    }

    // A ship's keel and ribs, standing out of the Gullet wall like a fishbone in a throat. The wall has closed
    // round its stern.
    static void Swallowed(Canvas c, float x, float f, Random rng)
    {
        float kx0 = x - 44, ky0 = f - 8, kx1 = x + 40, ky1 = f - 52;
        // the ribs, curving up off the keel
        for (int i = 0; i < 7; i++)
        {
            float u = 0.12f + i * 0.13f;
            float bx = H.Lerp(kx0, kx1, u), by = H.Lerp(ky0, ky1, u), len = 26 + MathF.Sin(u * H.PI) * 16;
            c.Stroke = V(0.2f); c.LineWidth = 3.2f;
            c.BeginPath(); c.MoveTo(bx, by); c.QuadraticCurveTo(bx - 14, by - len * 0.6f, bx - 4, by - len); c.StrokePath();
            c.Stroke = V(0.29f); c.LineWidth = 0.8f;
            c.BeginPath(); c.MoveTo(bx - 1, by - 1); c.QuadraticCurveTo(bx - 15, by - len * 0.6f, bx - 5, by - len); c.StrokePath();
        }
        c.Stroke = V(0.23f); c.LineWidth = 5; c.Line(kx0, ky0, kx1, ky1);
        c.Stroke = V(0.33f); c.LineWidth = 1; c.Line(kx0, ky0 - 2, kx1, ky1 - 2);
        // a broken mast, and the rag of a line still on it
        c.Stroke = V(0.22f); c.LineWidth = 2.4f; c.Line(x + 6, f - 30, x + 24, f - 74);
        c.Stroke = V(0.16f); c.LineWidth = 0.7f; c.BeginPath(); c.MoveTo(x + 22, f - 68); c.QuadraticCurveTo(x + 30, f - 56, x + 27, f - 44); c.StrokePath();
        // the flesh closed round the stern: folds, wet
        for (int i = 0; i < 9; i++)
        {
            float u = i / 8f, fx = kx0 - 6 + u * 26, fy = f - 6 - MathF.Sin(u * 2.4f) * 22 - (float)rng.NextDouble() * 6, r = 7 + (float)rng.NextDouble() * 6;
            c.Fill = V(0.13f + (float)rng.NextDouble() * 0.04f); c.BeginPath(); c.Ellipse(fx, fy, r, r * 0.7f, 0.5f, 0, H.PI * 2); c.FillPath();
            c.Fill = V(0.06f, 0.8f); c.BeginPath(); c.Ellipse(fx + 1.5f, fy + r * 0.45f, r * 0.8f, 1.1f, 0.4f, 0, H.PI * 2); c.FillPath();
            c.Fill = Gl(0.04f, 0.6f); c.FillRect(fx - r * 0.3f, fy - r * 0.45f, 2, 0.6f);
        }
    }

    // The old lighthouse lens, taken down when it cracked and leaned against the wall to wait for a new one that
    // never came. LUMEN CONTRA SALEM.
    static void Lens(Canvas c, float x, float f, Random rng)
    {
        float cx = x, cy = f - 38, R0 = 33;
        // the wedge it leans on
        S(c, x, f, 0.18f, -14, 0, -12, -6, 14, -6, 16, 0);
        // the brass frame
        c.Fill = VA(0.3f, 0.6f); c.BeginPath(); c.Ellipse(cx, cy, R0 * 0.86f, R0, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = V(0.1f); c.BeginPath(); c.Ellipse(cx, cy, R0 * 0.86f - 3, R0 - 3, 0, 0, H.PI * 2); c.FillPath();
        // the prism rings, each catching the light at its upper edge
        for (int i = 5; i >= 1; i--)
        {
            float rr = i * 5.4f;
            c.Fill = V(0.14f + i * 0.02f); c.BeginPath(); c.Ellipse(cx, cy, rr * 0.86f, rr, 0, 0, H.PI * 2); c.FillPath();
            c.Stroke = V(0.36f); c.LineWidth = 0.7f; c.BeginPath(); c.Ellipse(cx, cy, rr * 0.86f, rr, 0, -H.PI * 0.95f, -H.PI * 0.35f); c.StrokePath();
            c.Fill = Gl(0.08f, 0.6f); c.FillRect(cx - rr * 0.5f, cy - rr * 0.82f, 1, 1);
        }
        c.Fill = Gl(0.1f, 0.7f); c.BeginPath(); c.Ellipse(cx - 1, cy - 1, 2.2f, 2.6f, 0, 0, H.PI * 2); c.FillPath();
        // cracked clean through; one half has slipped a little
        c.Stroke = V(0.02f); c.LineWidth = 1.4f;
        c.BeginPath(); c.MoveTo(cx - 22, cy - 24); c.LineTo(cx - 8, cy - 9); c.LineTo(cx - 4, cy - 10); c.LineTo(cx + 7, cy + 6); c.LineTo(cx + 20, cy + 26); c.StrokePath();
        c.Stroke = V(0.4f, 0.7f); c.LineWidth = 0.5f;
        c.BeginPath(); c.MoveTo(cx - 21, cy - 25); c.LineTo(cx - 7, cy - 10.5f); c.LineTo(cx - 3, cy - 11.5f); c.StrokePath();
        // rivets round the frame
        for (int i = 0; i < 16; i++) { float a = i / 16f * H.PI * 2; c.Fill = V(0.42f); c.FillCircle(cx + MathF.Cos(a) * (R0 * 0.86f - 1.5f), cy + MathF.Sin(a) * (R0 - 1.5f), 0.8f); }
        // the salt has found it too
        for (int i = 0; i < 14; i++) { c.Fill = V(0.55f, 0.8f); c.FillRect(cx - 20 + (float)rng.NextDouble() * 40, cy + R0 - 6 + (float)rng.NextDouble() * 5, 1, 1); }
    }
}
