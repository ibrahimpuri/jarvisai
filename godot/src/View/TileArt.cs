// Tile art for the pieces the terrain and depth layers do not cover: timber and bone platforms, ladders,
// planks, rings and crystals, and the dynamic tiles (doors, gates, salt walls). Drawn encoded with the
// rest of the room, except the dynamic layer, which is redrawn each frame.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public static class TileArt
{
    const int TS = H.TILE;
    // Stone and back-wall tones the room props are drawn in, and each region's colour grade for the post pass.
    public class Pal { public int[] stone, back; public Color grade; }
    public static readonly Dictionary<string, Pal> PAL = new()
    {
        ["wrackline"] = new Pal { stone = new[] { 66, 76, 82 }, back = new[] { 26, 32, 36 }, grade = H.Rgba(30, 60, 80) },
        ["saltworks"] = new Pal { stone = new[] { 98, 92, 84 }, back = new[] { 42, 38, 34 }, grade = H.Rgba(90, 70, 40) },
        ["cloister"] = new Pal { stone = new[] { 60, 74, 70 }, back = new[] { 24, 32, 31 }, grade = H.Rgba(30, 70, 55) },
        ["choir"] = new Pal { stone = new[] { 64, 70, 94 }, back = new[] { 24, 28, 44 }, grade = H.Rgba(40, 40, 100) },
        ["ribvault"] = new Pal { stone = new[] { 104, 94, 78 }, back = new[] { 38, 33, 27 }, grade = H.Rgba(90, 65, 35) },
        ["gullet"] = new Pal { stone = new[] { 46, 62, 68 }, back = new[] { 12, 20, 24 }, grade = H.Rgba(20, 60, 70) },
        ["skull"] = new Pal { stone = new[] { 106, 96, 78 }, back = new[] { 40, 34, 26 }, grade = H.Rgba(110, 75, 30) },
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
    public static bool IsSolidCh(char ch) => SOLIDS.Contains(ch) || ch is 'S' or 'C' or 'E' or 'L' or 'V' or 'K' or 'Z' or 'Y' or 'h';

    // ---- timber and bone ----------------------------------------------------------------------------
    public static void SolidTile(Canvas c, char ch, float x, float y, int tx, int ty, RoomDef room, Pal pal, Func<int, int, bool> solidAt, int lx, int ly)
    {
        float h = Hash(tx, ty);
        bool up = !solidAt(lx, ly - 1), down = !solidAt(lx, ly + 1), left = !solidAt(lx - 1, ly), right = !solidAt(lx + 1, ly);
        switch (ch)
        {
            case '_': return;
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
        }
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
