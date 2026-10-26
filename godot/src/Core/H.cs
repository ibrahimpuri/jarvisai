// Shared constants and helpers (the JS build's util.js).
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public static class H
{
    public const int TILE = 16;
    public const int SCREEN_W = 40;   // tiles per screen, horizontally
    public const int SCREEN_H = 23;   // tiles per screen, vertically
    public const int VIEW_W = 640;    // internal resolution
    public const int VIEW_H = 360;
    public const float PI = MathF.PI;

    public static readonly Random Rng = new();

    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Sign(float v) => v < 0 ? -1 : v > 0 ? 1 : 0;
    public static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
    public static float Rand() => (float)Rng.NextDouble();
    public static float Rand(float a, float b) => a + Rand() * (b - a);
    public static int RandI(int a, int b) => (int)MathF.Floor(Rand(a, b + 1));
    public static T Pick<T>(IList<T> arr) => arr[(int)MathF.Floor(Rand() * arr.Count) % Math.Max(1, arr.Count)];
    public static float Approach(float v, float target, float step) => v < target ? MathF.Min(v + step, target) : MathF.Max(v - step, target);
    public static float Floor(float v) => MathF.Floor(v);
    public static int FloorI(float v) => (int)MathF.Floor(v);
    public static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);

    public static float AngleDiff(float a, float b)
    {
        float d = b - a;
        while (d > PI) d -= PI * 2;
        while (d < -PI) d += PI * 2;
        return d;
    }

    public static string FmtTime(float s)
    {
        int n = (int)MathF.Max(0, MathF.Ceiling(s));
        return (n / 60) + ":" + (n % 60).ToString("00");
    }

    public static bool RectsOverlap(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
        => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    // Segment vs circle: returns the parametric t of first hit or -1.
    public static float SegCircle(float x1, float y1, float x2, float y2, float cx, float cy, float r)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float fx = x1 - cx, fy = y1 - cy;
        float a = dx * dx + dy * dy;
        if (a == 0) return -1;
        float b = 2 * (fx * dx + fy * dy);
        float c = fx * fx + fy * fy - r * r;
        float disc = b * b - 4 * a * c;
        if (disc < 0) return -1;
        disc = MathF.Sqrt(disc);
        float t1 = (-b - disc) / (2 * a);
        float t2 = (-b + disc) / (2 * a);
        if (t1 >= 0 && t1 <= 1) return t1;
        if (t2 >= 0 && t2 <= 1) return t2;
        if (c < 0) return 0;
        return -1;
    }

    static int uid = 1;
    public static int Uid() => uid++;

    // Tiny event log shown as toasts.
    public class Toast { public string Text; public float T, Max; }
    public static readonly List<Toast> Toasts = new();
    public static void ToastMsg(string text, float time = 3.5f)
    {
        Toasts.Add(new Toast { Text = text, T = time, Max = time });
        if (Toasts.Count > 5) Toasts.RemoveAt(0);
    }

    // CSS-style colour strings from the data ("#c9b37a", "rgba(1,2,3,0.5)").
    static readonly Dictionary<string, Color> colorCache = new();
    public static Color Col(string s)
    {
        if (s == null) return Colors.White;
        if (colorCache.TryGetValue(s, out var c)) return c;
        if (s.StartsWith("#")) c = Color.FromHtml(s);
        else if (s.StartsWith("rgb"))
        {
            int i = s.IndexOf('('), j = s.IndexOf(')');
            var parts = s.Substring(i + 1, j - i - 1).Split(',');
            float r = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) / 255f;
            float g = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) / 255f;
            float b = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) / 255f;
            float a = parts.Length > 3 ? float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : 1f;
            c = new Color(r, g, b, a);
        }
        else c = Colors.White;
        colorCache[s] = c;
        return c;
    }
    public static Color Rgba(int r, int g, int b, float a = 1) => new(r / 255f, g / 255f, b / 255f, a);
}
