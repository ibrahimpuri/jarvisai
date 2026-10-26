// A small Canvas 2D style drawing API over Godot's CanvasItem, so the entity art reads
// the same as it did in the browser build: paths, transforms, fills, strokes and text.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public class Canvas
{
    struct State { public Transform2D T; public Color Fill, Stroke; public float LineWidth, Alpha; public FontSpec Font; public int Align; }
    public struct FontSpec { public bool Mono, Italic, Bold; public int Size; }

    CanvasItem ci;
    Rid rid;
    State s;
    readonly Stack<State> stack = new();
    readonly List<List<Vector2>> path = new();
    List<Vector2> cur;
    bool transformDirty = true;

    public Canvas Begin(CanvasItem item)
    {
        ci = item; rid = item.GetCanvasItem();
        stack.Clear(); path.Clear(); cur = null;
        s = new State { T = Transform2D.Identity, Fill = Colors.White, Stroke = Colors.White, LineWidth = 1, Alpha = 1, Font = new FontSpec { Size = 10 }, Align = 0 };
        transformDirty = true;
        return this;
    }

    // ---- state ----------------------------------------------------------------------------
    public Color Fill { get => s.Fill; set => s.Fill = value; }
    public Color Stroke { get => s.Stroke; set => s.Stroke = value; }
    public string FillStyle { set => s.Fill = H.Col(value); }
    public string StrokeStyle { set => s.Stroke = H.Col(value); }
    public float LineWidth { get => s.LineWidth; set => s.LineWidth = value; }
    public float GlobalAlpha { get => s.Alpha; set => s.Alpha = value; }
    /// 0 left, 1 center, 2 right
    public int TextAlign { get => s.Align; set => s.Align = value; }
    public string Font { set => s.Font = ParseFont(value); }
    public FontSpec FontSpecValue { get => s.Font; set => s.Font = value; }
    public void Save() => stack.Push(s);
    public void Restore() { if (stack.Count > 0) { s = stack.Pop(); transformDirty = true; } }
    public void Translate(float x, float y) { s.T = s.T * new Transform2D(0, new Vector2(x, y)); transformDirty = true; }
    public void Rotate(float a) { s.T = s.T * new Transform2D(a, Vector2.Zero); transformDirty = true; }
    public void Scale(float x, float y) { s.T = s.T * new Transform2D(new Vector2(x, 0), new Vector2(0, y), Vector2.Zero); transformDirty = true; }
    public void SetTransform(Transform2D t) { s.T = t; transformDirty = true; }
    public Transform2D GetTransform() => s.T;
    void ApplyT() { if (transformDirty) { ci.DrawSetTransformMatrix(s.T); transformDirty = false; } }
    Color A(Color c) => new(c.R, c.G, c.B, c.A * s.Alpha);

    // ---- rects ---------------------------------------------------------------------------------
    public void FillRect(float x, float y, float w, float h)
    {
        if (w < 0) { x += w; w = -w; }
        if (h < 0) { y += h; h = -h; }
        if (w == 0 || h == 0) return;
        ApplyT(); ci.DrawRect(new Rect2(x, y, w, h), A(s.Fill));
    }
    public void FillRect(float x, float y, float w, float h, Color c) { var f = s.Fill; s.Fill = c; FillRect(x, y, w, h); s.Fill = f; }
    public void StrokeRect(float x, float y, float w, float h) { ApplyT(); ci.DrawRect(new Rect2(x, y, w, h), A(s.Stroke), false, s.LineWidth); }
    public void FillCircle(float x, float y, float r) { ApplyT(); ci.DrawCircle(new Vector2(x, y), r, A(s.Fill)); }

    // Vertical / horizontal gradient rectangles and soft radial glows.
    public void FillRectGradV(float x, float y, float w, float h, Color top, Color bottom)
    {
        ApplyT();
        ci.DrawPolygon(new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) }, new[] { A(top), A(top), A(bottom), A(bottom) });
    }
    public void FillRectGradH(float x, float y, float w, float h, Color left, Color right)
    {
        ApplyT();
        ci.DrawPolygon(new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) }, new[] { A(left), A(right), A(right), A(left) });
    }
    public void FillPolyGrad(Vector2[] pts, Color[] cols) { ApplyT(); var c = new Color[cols.Length]; for (int i = 0; i < c.Length; i++) c[i] = A(cols[i]); ci.DrawPolygon(pts, c); }
    static Texture2D radial;
    public static Texture2D Radial
    {
        get
        {
            if (radial != null) return radial;
            var g = new Gradient();
            g.SetColor(0, new Color(1, 1, 1, 1)); g.SetColor(1, new Color(1, 1, 1, 0));
            g.AddPoint(0.45f, new Color(1, 1, 1, 0.35f));
            radial = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1, 0.5f), Width = 128, Height = 128 };
            return radial;
        }
    }
    public void Glow(float x, float y, float r, Color col) { ApplyT(); ci.DrawTextureRect(Radial, new Rect2(x - r, y - r, r * 2, r * 2), false, A(col)); }
    public void Glow(float x, float y, float rx, float ry, Color col) { ApplyT(); ci.DrawTextureRect(Radial, new Rect2(x - rx, y - ry, rx * 2, ry * 2), false, A(col)); }
    public void Texture(Texture2D tex, float x, float y) { ApplyT(); ci.DrawTexture(tex, new Vector2(x, y), A(s.Fill)); }
    public void Poly(Vector2[] pts)
    {
        if (pts.Length < 3) return;
        BeginPath(); MoveTo(pts[0].X, pts[0].Y); for (int i = 1; i < pts.Length; i++) LineTo(pts[i].X, pts[i].Y);
        FillPath();
    }

    // ---- paths -------------------------------------------------------------------------------------
    public void BeginPath() { path.Clear(); cur = null; }
    public void MoveTo(float x, float y) { cur = new List<Vector2> { new(x, y) }; path.Add(cur); }
    public void LineTo(float x, float y) { if (cur == null) MoveTo(x, y); else cur.Add(new Vector2(x, y)); }
    public void ClosePath()
    {
        if (cur == null || cur.Count == 0) return;
        var first = cur[0];
        if (cur.Count > 1 && cur[^1] != first) cur.Add(first);
        MoveTo(first.X, first.Y);
    }
    public void Arc(float x, float y, float r, float a0, float a1, bool ccw = false) => Ellipse(x, y, r, r, 0, a0, a1, ccw);
    public void Ellipse(float x, float y, float rx, float ry, float rot, float a0, float a1, bool ccw = false)
    {
        float sweep = a1 - a0;
        const float TAU = MathF.PI * 2;
        if (!ccw) { if (sweep >= TAU) sweep = TAU; else { while (sweep < 0) sweep += TAU; } }
        else { if (sweep <= -TAU) sweep = -TAU; else { while (sweep > 0) sweep -= TAU; } }
        int n = Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweep) * MathF.Max(rx, ry) / 1.5f), 4, 64);
        float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
        for (int i = 0; i <= n; i++)
        {
            float a = a0 + sweep * i / n;
            float px = MathF.Cos(a) * rx, py = MathF.Sin(a) * ry;
            float wx = x + px * cr - py * sr, wy = y + px * sr + py * cr;
            if (cur == null) MoveTo(wx, wy); else cur.Add(new Vector2(wx, wy));
        }
    }
    public void QuadraticCurveTo(float cx, float cy, float x, float y)
    {
        if (cur == null) MoveTo(cx, cy);
        var p0 = cur[^1];
        for (int i = 1; i <= 8; i++)
        {
            float t = i / 8f, u = 1 - t;
            cur.Add(new Vector2(u * u * p0.X + 2 * u * t * cx + t * t * x, u * u * p0.Y + 2 * u * t * cy + t * t * y));
        }
    }
    public void Rect(float x, float y, float w, float h) { MoveTo(x, y); LineTo(x + w, y); LineTo(x + w, y + h); LineTo(x, y + h); ClosePath(); }

    public void FillPath()
    {
        ApplyT();
        var col = A(s.Fill);
        foreach (var sp in path)
        {
            int n = sp.Count;
            if (n > 1 && sp[^1] == sp[0]) n--;
            if (n < 3) continue;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = sp[i];
            var idx = Geometry2D.TriangulatePolygon(pts);
            if (idx.Length == 0)
            {
                // self-intersecting outline: fall back to a fan
                idx = new int[(n - 2) * 3];
                for (int i = 0; i < n - 2; i++) { idx[i * 3] = 0; idx[i * 3 + 1] = i + 1; idx[i * 3 + 2] = i + 2; }
            }
            var cols = new Color[n];
            Array.Fill(cols, col);
            RenderingServer.CanvasItemAddTriangleArray(rid, idx, pts, cols);
        }
    }
    public void StrokePath()
    {
        ApplyT();
        var col = A(s.Stroke);
        foreach (var sp in path)
        {
            if (sp.Count < 2) continue;
            ci.DrawPolyline(sp.ToArray(), col, s.LineWidth, true);
        }
    }
    public void Line(float x1, float y1, float x2, float y2) { ApplyT(); ci.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), A(s.Stroke), s.LineWidth, true); }

    // ---- text ------------------------------------------------------------------------------------
    static readonly Dictionary<(bool, bool, bool), Font> fonts = new();
    public static Font GetFont(bool mono, bool italic, bool bold)
    {
        var key = (mono, italic, bold);
        if (fonts.TryGetValue(key, out var f)) return f;
        var sf = new SystemFont
        {
            FontNames = mono ? new[] { "Consolas", "Cascadia Mono", "DejaVu Sans Mono", "Liberation Mono", "monospace" }
                             : new[] { "Georgia", "Times New Roman", "DejaVu Serif", "Liberation Serif", "serif" },
            FontItalic = italic,
            FontWeight = bold ? 700 : 400,
            Antialiasing = TextServer.FontAntialiasing.Gray,
            Hinting = TextServer.Hinting.Light,
            SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
        };
        fonts[key] = sf;
        return sf;
    }
    public static FontSpec ParseFont(string css)
    {
        var f = new FontSpec { Size = 10 };
        foreach (var part in css.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "italic") f.Italic = true;
            else if (part == "bold") f.Bold = true;
            else if (part.EndsWith("px") && float.TryParse(part[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px)) f.Size = (int)MathF.Round(px);
            else if (part.StartsWith("monospace")) f.Mono = true;
        }
        return f;
    }
    Font CurFont => GetFont(s.Font.Mono, s.Font.Italic, s.Font.Bold);
    public float MeasureText(string text) => CurFont.GetStringSize(text, HorizontalAlignment.Left, -1, s.Font.Size).X;
    public void FillText(string text, float x, float y)
    {
        if (string.IsNullOrEmpty(text)) return;
        ApplyT();
        var font = CurFont;
        if (s.Align != 0)
        {
            float w = font.GetStringSize(text, HorizontalAlignment.Left, -1, s.Font.Size).X;
            x -= s.Align == 1 ? w / 2 : w;
        }
        ci.DrawString(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1, s.Font.Size, A(s.Fill));
    }
}
