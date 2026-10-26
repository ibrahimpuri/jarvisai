// Tile art: back walls with ambient occlusion, masonry with region accents, decor and dynamic tiles.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public static class TileArt
{
    const int TS = H.TILE;
    public class Pal { public int[] stone, back; public string accent; public Color grade, bg0, bg1, ambient; }
    public static readonly Dictionary<string, Pal> PAL = new()
    {
        ["wrackline"] = new Pal { stone = new[] { 66, 76, 82 }, back = new[] { 26, 32, 36 }, accent = "barnacle", grade = H.Rgba(30, 60, 80), bg0 = H.Col("#0e1216"), bg1 = H.Col("#1a2228"), ambient = new Color(0.36f, 0.40f, 0.46f) },
        ["saltworks"] = new Pal { stone = new[] { 98, 92, 84 }, back = new[] { 42, 38, 34 }, accent = "salt", grade = H.Rgba(90, 70, 40), bg0 = H.Col("#1a1713"), bg1 = H.Col("#2a251f"), ambient = new Color(0.42f, 0.39f, 0.35f) },
        ["cloister"] = new Pal { stone = new[] { 60, 74, 70 }, back = new[] { 24, 32, 31 }, accent = "moss", grade = H.Rgba(30, 70, 55), bg0 = H.Col("#0c1212"), bg1 = H.Col("#182222"), ambient = new Color(0.33f, 0.39f, 0.37f) },
        ["choir"] = new Pal { stone = new[] { 64, 70, 94 }, back = new[] { 24, 28, 44 }, accent = "mosaic", grade = H.Rgba(40, 40, 100), bg0 = H.Col("#0c0e16"), bg1 = H.Col("#181c2e"), ambient = new Color(0.33f, 0.35f, 0.46f) },
        ["ribvault"] = new Pal { stone = new[] { 104, 94, 78 }, back = new[] { 38, 33, 27 }, accent = "bone", grade = H.Rgba(90, 65, 35), bg0 = H.Col("#16130e"), bg1 = H.Col("#262118"), ambient = new Color(0.40f, 0.37f, 0.32f) },
        ["gullet"] = new Pal { stone = new[] { 46, 62, 68 }, back = new[] { 12, 20, 24 }, accent = "flesh", grade = H.Rgba(20, 60, 70), bg0 = H.Col("#050c0f"), bg1 = H.Col("#0c1a20"), ambient = new Color(0.12f, 0.16f, 0.19f) },
        ["skull"] = new Pal { stone = new[] { 106, 96, 78 }, back = new[] { 40, 34, 26 }, accent = "bone", grade = H.Rgba(110, 75, 30), bg0 = H.Col("#18130c"), bg1 = H.Col("#2a1f12"), ambient = new Color(0.42f, 0.37f, 0.30f) },
    };

    public static float Hash(int x, int y)
    {
        int h = unchecked(x * 374761393 + y * 668265263);
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        return (uint)(h ^ (h >> 16)) / 4294967295f;
    }
    static Color Rgb(int[] c, float k, float add = 0) => new(Math.Clamp((c[0] * k + add) / 255f, 0, 1), Math.Clamp((c[1] * k + add) / 255f, 0, 1), Math.Clamp((c[2] * k + add) / 255f, 0, 1));

    public static readonly HashSet<char> SOLIDS = new("#_TRMPG");
    public static readonly HashSet<char> DECOR = new("=|wHoxa");
    public static readonly HashSet<char> DYNAMIC = new("ELVKZYhyCSb");
    public static readonly HashSet<char> COVERS = new("#GTPM");
    public static bool IsSolidCh(char ch) => SOLIDS.Contains(ch) || ch is 'S' or 'C' or 'E' or 'L' or 'V' or 'K' or 'Z' or 'Y' or 'h';

    // ---- back wall -----------------------------------------------------------------------------------
    public static void BackTile(Canvas c, float x, float y, int tx, int ty, Pal pal, RoomDef room, int d)
    {
        float ao = 0.5f + 0.125f * Math.Min(d, 4);
        float h = Hash(tx, ty);
        string reg = room.Region;
        // big shapes in the back wall: pilasters, ribs, folds
        float shape = 1;
        if (reg is "cloister" or "choir" or "wrackline") { int m = ((tx % 10) + 10) % 10; if (m == 0) shape = 1.35f; else if (m == 1) shape = 0.8f; }
        if (reg is "ribvault" or "skull") shape = 1 + 0.25f * MathF.Sin(tx * 0.9f + MathF.Sin(ty * 0.35f) * 1.5f);
        if (reg == "gullet") shape = 1 + 0.3f * MathF.Sin(ty * 0.6f + tx * 0.25f);
        float k = ao * shape;
        c.Fill = Rgb(pal.back, k * (0.94f + h * 0.12f));
        c.FillRect(x, y, TS, TS);
        // brick courses (bigger and fainter than the solid masonry)
        c.Fill = new Color(0, 0, 0, 0.22f * ao);
        c.FillRect(x, y + 7, TS, 1); c.FillRect(x, y + 15, TS, 1);
        int off = (((ty % 2) + 2) % 2) * 8;
        c.FillRect(x + ((off + 4) % 16), y, 1, 7); c.FillRect(x + ((off + 12) % 16), y + 8, 1, 7);
        if (reg == "saltworks" && h > 0.72f) { c.Fill = H.Rgba(230, 232, 236, 0.10f); c.FillRect(x + h * 12, y + 3, 2, 1); }
        if (reg == "cloister" && h > 0.85f) { c.Fill = H.Rgba(70, 110, 70, 0.12f); c.FillRect(x + 2, y + h * 10, 3, 2); }
        if (reg == "choir" && h > 0.9f) { c.Fill = ((tx + ty) & 1) == 0 ? H.Rgba(80, 90, 170, 0.12f) : H.Rgba(150, 120, 60, 0.12f); c.FillRect(x + 4, y + 4, 3, 3); }
    }
    public static void WindowHole(Canvas c, float x, float y) => c.FillRectGradV(x, y, TS, TS, H.Col("#24384a"), H.Col("#16222e"));

    // ---- solid masonry -------------------------------------------------------------------------------
    public static void SolidTile(Canvas c, char ch, float x, float y, int tx, int ty, RoomDef room, Pal pal, Func<int, int, bool> solidAt, int lx, int ly)
    {
        float h = Hash(tx, ty);
        bool up = !solidAt(lx, ly - 1), down = !solidAt(lx, ly + 1), left = !solidAt(lx - 1, ly), right = !solidAt(lx + 1, ly);
        bool exposed = up || down || left || right;
        switch (ch)
        {
            case '_': return;
            case '#':
            case 'G':
                {
                    if (!exposed)
                    {
                        // inside a rock mass: dark and quiet
                        c.Fill = Rgb(pal.stone, 0.42f + h * 0.05f); c.FillRect(x, y, TS, TS);
                        if (h > 0.8f) { c.Fill = new Color(0, 0, 0, 0.18f); c.FillRect(x + h * 10, y + 4, 4, 3); }
                        break;
                    }
                    // masonry: two courses of bricks, offset by row
                    for (int r = 0; r < 2; r++)
                    {
                        int row = ty * 2 + r, off = (((row % 2) + 2) % 2) * 8;
                        for (int b = -1; b < 2; b++)
                        {
                            float bx = x + off + b * 16 - 8;
                            float v = Hash(tx * 3 + b, row) - 0.5f;
                            c.Fill = Rgb(pal.stone, 0.88f + v * 0.18f);
                            float x0 = MathF.Max(x, bx), x1 = MathF.Min(x + TS, bx + 16);
                            if (x1 > x0) c.FillRect(x0, y + r * 8, x1 - x0, 8);
                        }
                        c.Fill = new Color(0, 0, 0, 0.32f);
                        c.FillRect(x, y + r * 8 + 7, TS, 1);
                        c.FillRect(x + ((off + 8) % 16), y + r * 8, 1, 7);
                    }
                    c.Fill = new Color(1, 1, 1, 0.04f); c.FillRect(x, y, TS, 1);
                    if (up)
                    {
                        c.Fill = H.Rgba(170, 200, 210, 0.24f); c.FillRect(x, y, TS, 2);
                        AccentTop(c, x, y, tx, ty, h, pal.accent, room);
                    }
                    if (down) { c.Fill = new Color(0, 0, 0, 0.38f); c.FillRect(x, y + TS - 3, TS, 3); }
                    if (left) { c.Fill = H.Rgba(170, 200, 210, 0.08f); c.FillRect(x, y, 1, TS); }
                    if (right) { c.Fill = new Color(0, 0, 0, 0.22f); c.FillRect(x + TS - 2, y, 2, TS); }
                    if (ch == 'G') { c.FillStyle = "#121212"; for (int i = 2; i < TS; i += 4) c.FillRect(x + i, y + 1, 2, 6); }
                    break;
                }
            case 'T':
            case 'P':
                {
                    int[] bs = ch == 'P' ? new[] { 76, 58, 42 } : new[] { 80, 60, 40 };
                    c.Fill = Rgb(bs, 0.9f + h * 0.15f); c.FillRect(x, y, TS, TS);
                    c.Fill = new Color(0, 0, 0, 0.28f); for (int i = 0; i < TS; i += 5) c.FillRect(x, y + i, TS, 1);
                    c.Fill = H.Rgba(255, 220, 160, 0.07f); c.FillRect(x + 2 + h * 6, y + 1, 1, TS - 2);
                    c.Fill = H.Rgba(30, 20, 10, 0.5f); c.FillRect(x + 3, y + 2, 1, 1); c.FillRect(x + 12, y + 7, 1, 1);
                    if (up) { c.Fill = H.Rgba(210, 180, 130, 0.28f); c.FillRect(x, y, TS, 1); }
                    if (down) { c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(x, y + TS - 2, TS, 2); }
                    break;
                }
            case 'R':
                {
                    c.FillRectGradV(x, y + 2, TS, (TS - 4) / 2f, H.Col("#dccfb2"), H.Col("#b4a68a"));
                    c.FillRectGradV(x, y + 2 + (TS - 4) / 2f, TS, (TS - 4) / 2f, H.Col("#b4a68a"), H.Col("#8a7c62"));
                    c.Fill = H.Rgba(80, 60, 40, 0.35f); c.FillRect(x + h * 12, y + 5, 2, 6);
                    c.Fill = H.Rgba(255, 250, 230, 0.25f); c.FillRect(x, y + 3, TS, 1);
                    break;
                }
            case 'M':
                {
                    c.FillStyle = "#4c4642"; c.FillRect(x, y, TS, TS);
                    c.FillStyle = "#6c5c4c"; c.FillRect(x, y, TS, 2);
                    c.Fill = new Color(0, 0, 0, 0.35f); c.FillRect(x + 7, y + 2, 1, TS - 2);
                    c.FillStyle = "#8a7a64"; c.FillRect(x + 2, y + 4, 1.5f, 1.5f); c.FillRect(x + TS - 4, y + 4, 1.5f, 1.5f); c.FillRect(x + 2, y + 12, 1.5f, 1.5f);
                    c.Fill = H.Rgba(150, 70, 30, 0.18f + h * 0.2f); c.FillRect(x + h * 8, y + 6, 4, 8);
                    break;
                }
        }
    }
    static void AccentTop(Canvas c, float x, float y, int tx, int ty, float h, string accent, RoomDef room)
    {
        if (accent == "salt" || h > 0.78f)
        {
            c.Fill = H.Rgba(236, 240, 245, 0.55f);
            for (int i = 0; i < 4; i++) { float px = x + ((h * 97 + i * 5) % 15); c.FillRect(px, y - 1, 1.5f, 2 + ((i + tx) % 2 + 2) % 2); }
        }
        if (accent == "barnacle" && room.Row == 4)
        {
            c.Fill = H.Rgba(30, 40, 40, 0.6f); c.FillRect(x + h * 12, y + 2, 2, 2); c.FillRect(x + ((h * 31) % 13), y + 3, 1, 1);
            c.Fill = H.Rgba(60, 110, 70, 0.5f); c.FillRect(x + ((h * 53) % 12), y + 1, 3, 1);
        }
        if (accent == "moss") { c.Fill = H.Rgba(70, 120, 70, 0.55f); c.FillRect(x + h * 8, y, 6, 2); c.FillRect(x + ((h * 41) % 13), y + 2, 2, 1); }
        if (accent == "mosaic" && h > 0.6f) { c.Fill = H.Col(new[] { "#5a64b0", "#b09050", "#4a8a7a" }[(int)(h * 3) % 3]); c.FillRect(x + 6, y + 4, 2, 2); }
        if (accent == "flesh") { c.Fill = H.Rgba(120, 50, 50, 0.35f); c.FillRect(x, y + 2, TS, 1); }
        if (accent == "bone" && h > 0.5f) { c.Fill = H.Rgba(230, 220, 195, 0.3f); c.FillRect(x + 2, y + 1, 10, 1); }
    }

    // ---- decor ---------------------------------------------------------------------------------------
    public static void Decor(Canvas c, char ch, float x, float y, int tx, int ty)
    {
        float h = Hash(tx, ty);
        switch (ch)
        {
            case '=':
                c.FillStyle = "#5c4632"; c.FillRect(x, y, TS, 4);
                c.FillStyle = "#7e6244"; c.FillRect(x, y, TS, 1);
                c.Fill = new Color(0, 0, 0, 0.35f); c.FillRect(x, y + 3, TS, 1);
                c.FillStyle = "#3a2a1a"; if (tx % 3 == 0) c.FillRect(x + 6, y + 4, 2, 5);
                break;
            case '|':
                c.FillRectGradH(x + 2, y, (TS - 4) * 0.45f, TS, H.Rgba(22, 26, 30, 0.95f), H.Rgba(58, 66, 70, 0.95f));
                c.FillRectGradH(x + 2 + (TS - 4) * 0.45f, y, (TS - 4) * 0.55f, TS, H.Rgba(58, 66, 70, 0.95f), H.Rgba(16, 18, 22, 0.95f));
                c.Fill = new Color(0, 0, 0, 0.25f); c.FillRect(x + 2, y + 15, TS - 4, 1);
                break;
            case 'w':
                {
                    var cols = new[] { H.Rgba(150, 50, 50, 0.45f), H.Rgba(50, 80, 150, 0.45f), H.Rgba(170, 140, 50, 0.4f), H.Rgba(50, 120, 95, 0.4f) };
                    c.Fill = cols[(int)(h * 4) % 4]; c.FillRect(x + 3, y + 1, TS - 6, TS - 2);
                    c.Fill = new Color(1, 1, 1, 0.08f); c.FillRect(x + 4, y + 2, 2, 5);
                    c.Fill = H.Rgba(10, 10, 12, 0.9f); c.FillRect(x + 7, y, 1.5f, TS); c.FillRect(x + 2, y + 7, TS - 4, 1.5f);
                    c.FillRect(x + 2, y, 1.5f, TS); c.FillRect(x + TS - 3.5f, y, 1.5f, TS);
                    break;
                }
            case 'H':
                c.FillStyle = "#5c4632"; c.FillRect(x + 3, y, 2, TS); c.FillRect(x + TS - 5, y, 2, TS);
                c.FillStyle = "#6e5640"; c.FillRect(x + 3, y + 3, TS - 6, 2); c.FillRect(x + 3, y + 11, TS - 6, 2);
                c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(x + 3, y + 5, TS - 6, 1); c.FillRect(x + 3, y + 13, TS - 6, 1);
                break;
            case 'o':
                c.StrokeStyle = "#9a9a9a"; c.LineWidth = 1.5f; c.BeginPath(); c.Arc(x + 8, y + 8, 4, 0, H.PI * 2); c.StrokePath();
                c.Stroke = new Color(1, 1, 1, 0.25f); c.LineWidth = 0.8f; c.BeginPath(); c.Arc(x + 8, y + 8, 4, H.PI * 1.1f, H.PI * 1.6f); c.StrokePath();
                c.FillStyle = "#5a5a5a"; c.FillRect(x + 7, y, 2, 4);
                break;
            case 'x':
                for (int i = 0; i < 4; i++)
                {
                    float bx = x + 1 + i * 4, tip = y + 5 + Hash(tx + i, ty) * 5;
                    c.Fill = H.Rgba(225, 240, 250, 0.95f); c.BeginPath(); c.MoveTo(bx, y + TS); c.LineTo(bx + 1.5f, tip); c.LineTo(bx + 3, y + TS); c.FillPath();
                    c.Fill = H.Rgba(150, 190, 210, 0.6f); c.BeginPath(); c.MoveTo(bx + 1.5f, tip); c.LineTo(bx + 3, y + TS); c.LineTo(bx + 1.8f, y + TS); c.FillPath();
                }
                break;
            case 'a':
                c.Fill = H.Rgba(180, 220, 255, 0.22f); c.BeginPath(); c.Arc(x + 8, y + 8, 6, 0, H.PI * 2); c.FillPath();
                c.Stroke = H.Rgba(200, 235, 255, 0.35f); c.LineWidth = 1; c.BeginPath(); c.Arc(x + 8, y + 8, 4, 0, H.PI * 2); c.StrokePath();
                break;
        }
    }

    // ---- dynamic tiles (doors, gates, salt walls) ------------------------------------------------
    public static void Dynamic(Canvas c, char ch, float x, float y, int tx, int ty, float now)
    {
        int s = World.Solidity(ch);
        switch (ch)
        {
            case 'E': case 'L': case 'V': case 'K': case 'Z': case 'Y': case 'h':
                {
                    string col = ch switch { 'E' => "#3a5a6a", 'L' => "#4a3a2a", 'V' => "#5a4a6a", 'K' => "#5a5048", 'Z' => "#6a5a3a", 'Y' => "#3a4a5a", _ => "#4a4a4a" };
                    if (s == 1)
                    {
                        c.FillStyle = col; c.FillRect(x, y, TS, TS);
                        c.Fill = new Color(0, 0, 0, 0.35f); c.FillRect(x + 2, y + 2, TS - 4, TS - 4);
                        c.Fill = new Color(1, 1, 1, 0.12f); c.FillRect(x + 7, y + 2, 2, TS - 4);
                        c.Fill = H.Rgba(20, 20, 20, 0.6f); c.FillRect(x + 1, y + 4, 2, 1); c.FillRect(x + TS - 3, y + 4, 2, 1);
                        if (ch == 'V') { c.FillStyle = "#c0a060"; c.FillRect(x + 6, y + 6, 4, 4); }
                    }
                    else if (ch == 'K' || ch == 'Z') Decor(c, 'H', x, y, tx, ty);
                    else { c.Fill = new Color(0, 0, 0, 0.25f); c.FillRect(x, y, 2, TS); c.FillRect(x + TS - 2, y, 2, TS); }
                    break;
                }
            case 'y': if (s != 0) Decor(c, '=', x, y, tx, ty); else { c.Fill = H.Rgba(80, 70, 60, 0.25f); c.FillRect(x, y, TS, 3); } break;
            case 'C':
                {
                    float t = now / 0.6f + tx;
                    c.Fill = H.Rgba(190, 230, 250, 0.55f); c.FillRect(x + 2, y, TS - 4, TS);
                    c.Fill = new Color(1, 1, 1, 0.35f + MathF.Sin(t) * 0.15f); c.FillRect(x + 4, y + 2, 2, TS - 4);
                    break;
                }
            case 'S':
                c.FillStyle = "#ccc4b4"; c.FillRect(x, y, TS, TS);
                c.Fill = new Color(1, 1, 1, 0.35f); c.FillRect(x, y, TS, 1);
                c.Stroke = H.Rgba(90, 80, 70, 0.6f); c.LineWidth = 1;
                c.BeginPath(); c.MoveTo(x + 3, y + 2); c.LineTo(x + 8, y + 9); c.LineTo(x + 6, y + 14); c.MoveTo(x + 8, y + 9); c.LineTo(x + 13, y + 7); c.StrokePath();
                break;
            case 'b':
                c.FillStyle = "#5a4a30"; c.FillRect(x, y, TS, 3);
                c.StrokeStyle = "#7a6a50"; c.LineWidth = 1; c.Line(x, y + 1, x + TS, y + 1);
                break;
        }
    }
}
