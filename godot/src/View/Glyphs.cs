// Button prompts drawn for whatever the player is holding: keycaps and mouse buttons, or the face
// buttons, shoulders, sticks and d-pad of an Xbox, PlayStation or Switch controller.
// Text can carry {action} tokens ("{interact} Open the locker"); they become glyphs when drawn.
using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Hollowmere;

public static class Glyphs
{
    public enum Style { Keyboard, Xbox, PlayStation, Switch }
    public static Style? Force;   // tests and screenshots
    public static Style PadStyle => Settings.Prompts switch
    {
        "xbox" => Style.Xbox, "playstation" => Style.PlayStation, "switch" => Style.Switch,
        _ => Pad.Kind switch { PadKind.PlayStation => Style.PlayStation, PadKind.Switch => Style.Switch, _ => Style.Xbox },
    };
    public static Style Current => Force ?? (Pad.Using ? PadStyle : Style.Keyboard);

    // ---- which controls perform an action -----------------------------------------------------------
    static readonly Dictionary<string, string[]> KEYS = new()
    {
        ["move"] = new[] { "K:A", "K:D" }, ["left"] = new[] { "K:A" }, ["right"] = new[] { "K:D" }, ["up"] = new[] { "K:W" }, ["down"] = new[] { "K:S" },
        ["jump"] = new[] { "K:Space" }, ["dash"] = new[] { "K:C" }, ["gaff"] = new[] { "K:F" }, ["swing"] = new[] { "K:Q:hold" },
        ["interact"] = new[] { "K:E" }, ["burn"] = new[] { "K:X" }, ["reload"] = new[] { "K:R" }, ["shutter"] = new[] { "K:T" },
        ["poultice"] = new[] { "K:Z" }, ["stitch"] = new[] { "K:G" }, ["quickcraft"] = new[] { "K:V" }, ["boots"] = new[] { "K:B" },
        ["map"] = new[] { "K:Tab" }, ["satchel"] = new[] { "K:I" }, ["journal"] = new[] { "K:I" }, ["pause"] = new[] { "K:Esc" },
        ["sneak"] = new[] { "K:Shift:hold" }, ["fire"] = new[] { "M:L" }, ["focus"] = new[] { "M:R:hold" }, ["aim"] = new[] { "M:move" },
        ["weapon"] = new[] { "M:wheel" }, ["drop"] = new[] { "K:S", "+", "K:Space" }, ["walljump"] = new[] { "K:A", "/", "K:D", "+", "K:Space" },
        ["confirm"] = new[] { "K:E" }, ["cancel"] = new[] { "K:Esc" }, ["tabs"] = new string[0], ["skip"] = new[] { "K:Esc" },
        ["mash"] = new[] { "K:F", "/", "K:E", "/", "K:Space" },
        ["hollow"] = new[] { "K:5" }, ["sing"] = new[] { "K:H" }, ["verse"] = new[] { "K:Y" }, ["singhold"] = new[] { "K:H:hold" },
    };
    static string[] PadControls(string action) => action switch
    {
        "move" or "left" or "right" => new[] { "LS" }, "up" => new[] { "LS:up" }, "down" => new[] { "LS:down" },
        "jump" => new[] { "A" }, "dash" => new[] { "B" }, "gaff" => new[] { "X" }, "interact" => new[] { "Y" }, "burn" => new[] { "Y:hold" },
        "swing" => new[] { "LB:hold" }, "reload" => new[] { "RB" }, "quickcraft" => new[] { "RB:hold" },
        "poultice" => new[] { "DU" }, "stitch" => new[] { "DU:hold" }, "boots" => new[] { "DD" },
        "weapon" => new[] { "DLR" }, "wprev" => new[] { "DL" }, "wnext" => new[] { "DR" },
        "shutter" => new[] { "R3" }, "sneak" => new[] { "L3" }, "fire" => new[] { "RT" }, "focus" => new[] { "LT:hold" }, "aim" => new[] { "RS" },
        "map" => new[] { "BACK" }, "satchel" or "journal" => new[] { "BACK" }, "pause" => new[] { "START" },
        "drop" => new[] { "LS:down", "+", "A" }, "walljump" => new[] { "LS", "+", "A" },
        "confirm" => new[] { Pad.ConfirmButton == JoyButton.A ? "A" : "B" }, "cancel" => new[] { Pad.CancelButton == JoyButton.A ? "A" : "B" },
        "skip" => new[] { "START" }, "tabs" => new[] { "LB", "/", "RB" }, "mash" => new[] { "X", "/", "Y", "/", "A" },
        "hollow" => new[] { "DLR" }, "sing" => new[] { "RT" }, "verse" => new[] { "RB" }, "singhold" => new[] { "RT:hold" },
        _ => new string[0],
    };
    public static string[] Controls(string action, Style st) =>
        st == Style.Keyboard ? (KEYS.TryGetValue(action, out var k) ? k : new string[0]) : PadControls(action);
    public static bool Known(string action) => KEYS.ContainsKey(action) || PadControls(action).Length > 0;

    // ---- palettes --------------------------------------------------------------------------------
    static readonly Color BODY = H.Col("#202326"), EDGE = H.Col("#8d8370"), LABEL = H.Col("#efe6cf");
    static Color Face(Style st, string b) => st switch
    {
        Style.Xbox => b switch { "A" => H.Col("#7dc055"), "B" => H.Col("#e05a48"), "X" => H.Col("#4b90d8"), _ => H.Col("#e9c24c") },
        Style.PlayStation => b switch { "A" => H.Col("#93b4ea"), "B" => H.Col("#ea736c"), "X" => H.Col("#da92cb"), _ => H.Col("#5dcaa9") },
        _ => LABEL,
    };
    // Labels by position: A = bottom, B = right, X = left, Y = top.
    static string FaceLabel(Style st, string b) => st == Style.Switch ? b switch { "A" => "B", "B" => "A", "X" => "Y", _ => "X" } : b;
    static string ShoulderLabel(Style st, string id) => st switch
    {
        Style.PlayStation => id switch { "LB" => "L1", "RB" => "R1", "LT" => "L2", _ => "R2" },
        Style.Switch => id switch { "LB" => "L", "RB" => "R", "LT" => "ZL", _ => "ZR" },
        _ => id,
    };
    static string KeyLabel(string k) => k switch { "Escape" => "Esc", _ => k };

    static Color Al(Color c, float a) => new(c.R, c.G, c.B, c.A * a);

    // ---- one control ------------------------------------------------------------------------------
    static (string id, bool hold) Split(string token)
    {
        bool hold = token.EndsWith(":hold");
        return (hold ? token[..^5] : token, hold);
    }
    public static float Width(Canvas c, string token, Style st, float s)
    {
        var (id, hold) = Split(token);
        float w = BaseWidth(c, id, st, s);
        if (hold) { var f = c.FontSpecValue; c.Font = Px(s * 0.5f) + "px monospace"; w += 2 + c.MeasureText("hold"); c.FontSpecValue = f; }
        return w;
    }
    static string Px(float v) => MathF.Max(4, MathF.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    static float BaseWidth(Canvas c, string id, Style st, float s)
    {
        if (id == "+" || id == "/") return s * 0.55f;
        if (id.StartsWith("K:"))
        {
            var f = c.FontSpecValue; c.Font = Px(s * 0.58f) + "px monospace";
            float w = MathF.Max(s, c.MeasureText(KeyLabel(id[2..])) + s * 0.5f);
            c.FontSpecValue = f; return w;
        }
        if (id.StartsWith("M:")) return s * 0.72f;
        return id switch
        {
            "LB" or "RB" => s * 1.45f, "LT" or "RT" => s * 1.3f, "TOUCH" => s * 1.7f,
            "BACK" or "START" => st == Style.Switch ? s * 0.8f : s * 1.15f,
            _ => s,
        };
    }

    // Draws a control centred on yMid starting at x; returns its width.
    public static float Draw(Canvas c, string token, Style st, float x, float yMid, float s, float alpha = 1)
    {
        var (id, hold) = Split(token);
        float w = BaseWidth(c, id, st, s);
        var saveFont = c.FontSpecValue; int saveAlign = c.TextAlign; float saveLw = c.LineWidth;
        c.TextAlign = 1;
        float cx = x + w / 2, top = yMid - s / 2;
        if (id == "+" || id == "/")
        {
            c.Font = Px(s * 0.6f) + "px monospace"; c.Fill = Al(LABEL, 0.7f * alpha);
            c.FillText(id, cx, yMid + s * 0.2f);
        }
        else if (id.StartsWith("K:")) KeyCap(c, KeyLabel(id[2..]), x, top, w, s, alpha);
        else if (id.StartsWith("M:")) MouseGlyph(c, id[2..], cx, yMid, s, alpha);
        else if (id is "A" or "B" or "X" or "Y") FaceButton(c, st, id, cx, yMid, s, alpha);
        else if (id is "LB" or "RB" or "LT" or "RT") Shoulder(c, st, id, x, top, w, s, alpha);
        else if (id.StartsWith("LS") || id.StartsWith("RS") || id is "L3" or "R3") Stick(c, id, cx, yMid, s, alpha);
        else if (id.StartsWith("D")) DPad(c, id, cx, yMid, s, alpha);
        else if (id is "BACK" or "START") MenuButton(c, st, id, cx, yMid, w, s, alpha);
        else if (id == "TOUCH") { Pill(c, x, top + s * 0.2f, w, s * 0.6f, alpha); }
        if (hold)
        {
            c.TextAlign = 0; c.Font = Px(s * 0.5f) + "px monospace"; c.Fill = Al(LABEL, 0.65f * alpha);
            c.FillText("hold", x + w + 2, yMid + s * 0.18f);
            w += 2 + c.MeasureText("hold");
        }
        c.FontSpecValue = saveFont; c.TextAlign = saveAlign; c.LineWidth = saveLw;
        return w;
    }

    static void RoundRect(Canvas c, float x, float y, float w, float h, float r)
    {
        r = MathF.Min(r, MathF.Min(w, h) / 2);
        c.BeginPath();
        c.Arc(x + w - r, y + r, r, -H.PI / 2, 0);
        c.Arc(x + w - r, y + h - r, r, 0, H.PI / 2);
        c.Arc(x + r, y + h - r, r, H.PI / 2, H.PI);
        c.Arc(x + r, y + r, r, H.PI, H.PI * 1.5f);
        c.ClosePath();
    }
    static void Pill(Canvas c, float x, float y, float w, float h, float a)
    {
        RoundRect(c, x, y, w, h, h / 2);
        c.Fill = Al(BODY, 0.92f * a); c.FillPath();
        c.Stroke = Al(EDGE, a); c.LineWidth = 1; c.StrokePath();
    }
    static void KeyCap(Canvas c, string label, float x, float y, float w, float s, float a)
    {
        RoundRect(c, x, y + s * 0.08f, w, s * 0.92f, s * 0.2f);
        c.Fill = Al(H.Col("#141210"), 0.9f * a); c.FillPath();
        RoundRect(c, x, y, w, s * 0.84f, s * 0.2f);
        c.Fill = Al(H.Col("#2c2822"), 0.95f * a); c.FillPath();
        c.Stroke = Al(H.Col("#b7a682"), a); c.LineWidth = 1; c.StrokePath();
        c.Font = Px(s * 0.58f) + "px monospace"; c.Fill = Al(LABEL, a);
        c.FillText(label, x + w / 2, y + s * 0.62f);
    }
    static void MouseGlyph(Canvas c, string which, float cx, float cy, float s, float a)
    {
        float w = s * 0.66f, h = s, x = cx - w / 2, y = cy - h / 2;
        RoundRect(c, x, y, w, h, w * 0.45f);
        c.Fill = Al(BODY, 0.92f * a); c.FillPath();
        c.Stroke = Al(EDGE, a); c.LineWidth = 1; c.StrokePath();
        var lit = Al(H.Col("#f0d890"), a);
        if (which == "L" || which == "R")
        {
            c.BeginPath();
            if (which == "L") { c.MoveTo(cx, y + 1); c.LineTo(cx, y + h * 0.42f); c.LineTo(x + 1, y + h * 0.42f); c.Arc(x + w * 0.45f, y + w * 0.45f, w * 0.45f - 1, H.PI, H.PI * 1.5f); }
            else { c.MoveTo(cx, y + 1); c.Arc(x + w - w * 0.45f, y + w * 0.45f, w * 0.45f - 1, -H.PI / 2, 0); c.LineTo(x + w - 1, y + h * 0.42f); c.LineTo(cx, y + h * 0.42f); }
            c.ClosePath(); c.Fill = lit; c.FillPath();
        }
        c.Stroke = Al(EDGE, a); c.Line(cx, y + 1, cx, y + h * 0.42f); c.Line(x + 1, y + h * 0.42f, x + w - 1, y + h * 0.42f);
        if (which == "wheel") c.FillRect(cx - 1, y + h * 0.14f, 2, h * 0.22f, lit);
        if (which == "move")
        {
            c.Stroke = Al(LABEL, 0.75f * a);
            c.Line(x - 3, cy, x - 1, cy - 2); c.Line(x - 3, cy, x - 1, cy + 2);
            c.Line(x + w + 3, cy, x + w + 1, cy - 2); c.Line(x + w + 3, cy, x + w + 1, cy + 2);
        }
    }
    static void FaceButton(Canvas c, Style st, string b, float cx, float cy, float s, float a)
    {
        float r = s / 2;
        c.Fill = Al(BODY, 0.95f * a); c.FillCircle(cx, cy, r);
        var col = Face(st, b);
        c.Stroke = Al(st == Style.Switch ? EDGE : col.Lerp(BODY, 0.35f), a); c.LineWidth = 1;
        c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
        if (st == Style.PlayStation)
        {
            c.Stroke = Al(col, a); c.LineWidth = MathF.Max(1, s * 0.1f);
            float q = s * 0.21f;
            switch (b)
            {
                case "A": c.Line(cx - q, cy - q, cx + q, cy + q); c.Line(cx - q, cy + q, cx + q, cy - q); break;
                case "B": c.BeginPath(); c.Arc(cx, cy, q * 1.05f, 0, H.PI * 2); c.StrokePath(); break;
                case "X": c.StrokeRect(cx - q * 0.9f, cy - q * 0.9f, q * 1.8f, q * 1.8f); break;
                default: c.BeginPath(); c.MoveTo(cx, cy - q * 1.15f); c.LineTo(cx + q * 1.05f, cy + q * 0.75f); c.LineTo(cx - q * 1.05f, cy + q * 0.75f); c.ClosePath(); c.StrokePath(); break;
            }
            return;
        }
        c.Font = "bold " + Px(s * 0.64f) + "px monospace"; c.Fill = Al(col, a);
        c.FillText(FaceLabel(st, b), cx, cy + s * 0.23f);
    }
    static void Shoulder(Canvas c, Style st, string id, float x, float y, float w, float s, float a)
    {
        bool trigger = id is "LT" or "RT";
        if (trigger)
        {
            // a trigger: rounded on top, flat underneath
            float r = s * 0.42f;
            c.BeginPath(); c.Arc(x + r, y + r, r, H.PI, H.PI * 1.5f); c.Arc(x + w - r, y + r, r, -H.PI / 2, 0);
            c.LineTo(x + w, y + s); c.LineTo(x, y + s); c.ClosePath();
        }
        else RoundRect(c, x, y + s * 0.18f, w, s * 0.64f, s * 0.28f);
        c.Fill = Al(BODY, 0.95f * a); c.FillPath();
        c.Stroke = Al(EDGE, a); c.LineWidth = 1; c.StrokePath();
        c.Font = Px(s * (trigger ? 0.52f : 0.5f)) + "px monospace"; c.Fill = Al(LABEL, a);
        c.FillText(ShoulderLabel(st, id), x + w / 2, y + s * (trigger ? 0.68f : 0.66f));
    }
    static void Stick(Canvas c, string id, float cx, float cy, float s, float a)
    {
        float r = s / 2;
        c.Fill = Al(BODY, 0.95f * a); c.FillCircle(cx, cy, r);
        c.Stroke = Al(EDGE, a); c.LineWidth = 1;
        c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
        c.BeginPath(); c.Arc(cx, cy, r * 0.55f, 0, H.PI * 2); c.StrokePath();
        bool press = id is "L3" or "R3";
        if (press) { c.Fill = Al(H.Col("#f0d890"), a); c.FillCircle(cx, cy, r * 0.3f); }
        c.Font = Px(s * 0.42f) + "px monospace"; c.Fill = Al(LABEL, a);
        if (!press) c.FillText(id[0].ToString(), cx, cy + s * 0.15f);
        if (id.EndsWith(":up") || id.EndsWith(":down"))
        {
            bool up = id.EndsWith(":up"); float ty = up ? cy - r - 1 : cy + r + 1, d = up ? -1 : 1;
            c.Fill = Al(H.Col("#f0d890"), a);
            c.Poly(new[] { new Vector2(cx - s * 0.2f, ty), new Vector2(cx + s * 0.2f, ty), new Vector2(cx, ty + d * s * 0.22f) });
        }
    }
    static void DPad(Canvas c, string id, float cx, float cy, float s, float a)
    {
        float arm = s * 0.34f, half = s / 2;
        var dark = Al(BODY, 0.95f * a); var lit = Al(H.Col("#f0d890"), a);
        bool U = id == "DU", Dn = id == "DD", L = id is "DL" or "DLR", R = id is "DR" or "DLR";
        c.FillRect(cx - arm / 2, cy - half, arm, s, dark);
        c.FillRect(cx - half, cy - arm / 2, s, arm, dark);
        if (U) c.FillRect(cx - arm / 2 + 0.5f, cy - half + 0.5f, arm - 1, half - arm / 2, lit);
        if (Dn) c.FillRect(cx - arm / 2 + 0.5f, cy + arm / 2, arm - 1, half - arm / 2 - 0.5f, lit);
        if (L) c.FillRect(cx - half + 0.5f, cy - arm / 2 + 0.5f, half - arm / 2, arm - 1, lit);
        if (R) c.FillRect(cx + arm / 2, cy - arm / 2 + 0.5f, half - arm / 2 - 0.5f, arm - 1, lit);
        c.Stroke = Al(EDGE, a); c.LineWidth = 1;
        c.BeginPath();
        c.MoveTo(cx - arm / 2, cy - half); c.LineTo(cx + arm / 2, cy - half); c.LineTo(cx + arm / 2, cy - arm / 2); c.LineTo(cx + half, cy - arm / 2);
        c.LineTo(cx + half, cy + arm / 2); c.LineTo(cx + arm / 2, cy + arm / 2); c.LineTo(cx + arm / 2, cy + half); c.LineTo(cx - arm / 2, cy + half);
        c.LineTo(cx - arm / 2, cy + arm / 2); c.LineTo(cx - half, cy + arm / 2); c.LineTo(cx - half, cy - arm / 2); c.LineTo(cx - arm / 2, cy - arm / 2); c.ClosePath();
        c.StrokePath();
    }
    static void MenuButton(Canvas c, Style st, string id, float cx, float cy, float w, float s, float a)
    {
        bool start = id == "START";
        var lab = Al(LABEL, a);
        if (st == Style.Switch)
        {
            float r = s * 0.4f;
            c.Fill = Al(BODY, 0.95f * a); c.FillCircle(cx, cy, r);
            c.Stroke = Al(EDGE, a); c.LineWidth = 1; c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
            c.FillRect(cx - r * 0.5f, cy - 0.6f, r, 1.2f, lab);
            if (start) c.FillRect(cx - 0.6f, cy - r * 0.5f, 1.2f, r, lab);
            return;
        }
        Pill(c, cx - w / 2, cy - s * 0.32f, w, s * 0.64f, a);
        if (st == Style.Xbox && !start)
        {
            // View: two overlapping windows
            c.Stroke = lab; c.LineWidth = 1;
            c.StrokeRect(cx - s * 0.2f, cy - s * 0.14f, s * 0.26f, s * 0.2f);
            c.StrokeRect(cx - s * 0.06f, cy - s * 0.04f, s * 0.26f, s * 0.2f);
        }
        else if (st == Style.PlayStation && !start)
        {
            // Create: three short upright strokes
            for (int i = -1; i <= 1; i++) c.FillRect(cx + i * s * 0.14f - 0.5f, cy - s * 0.14f, 1, s * 0.28f, lab);
        }
        else for (int i = -1; i <= 1; i++) c.FillRect(cx - s * 0.2f, cy + i * s * 0.11f - 0.5f, s * 0.4f, 1, lab);   // Menu / Options
    }

    // ---- an action, and text with {action} tokens -------------------------------------------------
    public static float ActionWidth(Canvas c, string action, Style st, float s)
    {
        var ctl = Controls(action, st); float w = 0;
        for (int i = 0; i < ctl.Length; i++) w += Width(c, ctl[i], st, s) + (i > 0 ? 1 : 0);
        return w;
    }
    public static float DrawAction(Canvas c, string action, Style st, float x, float yMid, float s, float alpha = 1)
    {
        var ctl = Controls(action, st); float w = 0;
        for (int i = 0; i < ctl.Length; i++) { if (i > 0) w += 1; w += Draw(c, ctl[i], st, x + w, yMid, s, alpha); }
        return w;
    }

    // Splits "{interact} Open it" into text runs and action tokens.
    public static List<(bool glyph, string s)> Parse(string text)
    {
        var parts = new List<(bool, string)>();
        int i = 0;
        while (i < text.Length)
        {
            int a = text.IndexOf('{', i);
            int b = a >= 0 ? text.IndexOf('}', a) : -1;
            if (a < 0 || b < 0) { parts.Add((false, text[i..])); break; }
            if (a > i) parts.Add((false, text[i..a]));
            parts.Add((true, text[(a + 1)..b]));
            i = b + 1;
        }
        return parts;
    }
    // Glyph size that sits well beside text of the current font size.
    static float GlyphSize(Canvas c) => MathF.Round(c.FontSpecValue.Size * 1.35f + 1);
    public static float Measure(Canvas c, string text, Style? style = null)
    {
        var st = style ?? Current; float s = GlyphSize(c), w = 0;
        foreach (var (g, t) in Parse(text)) w += g ? ActionWidth(c, t, st, s) + 2 : c.MeasureText(t);
        return w;
    }
    // Draws rich text with its baseline at y, honouring TextAlign; returns the width.
    public static float DrawText(Canvas c, string text, float x, float y, float alpha = 1, Style? style = null)
    {
        var st = style ?? Current;
        float total = Measure(c, text, st), s = GlyphSize(c);
        int align = c.TextAlign;
        float px = align == 1 ? x - total / 2 : align == 2 ? x - total : x;
        float mid = y - c.FontSpecValue.Size * 0.33f;
        c.TextAlign = 0;
        var fill = c.Fill;
        foreach (var (g, t) in Parse(text))
        {
            if (g) { px += 1; px += DrawAction(c, t, st, px, mid, s, alpha * fill.A) + 1; }
            else { c.Fill = fill; c.FillText(t, px, y); px += c.MeasureText(t); }
        }
        c.Fill = fill; c.TextAlign = align;
        return total;
    }
    // Word wrap that keeps glyph tokens whole.
    public static List<string> Wrap(Canvas c, string text, float maxW)
    {
        var lines = new List<string>(); var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            string trial = line.Length > 0 ? line + " " + word : word;
            if (line.Length > 0 && Measure(c, trial) > maxW) { lines.Add(line.ToString()); line.Clear(); line.Append(word); }
            else { line.Clear(); line.Append(trial); }
        }
        if (line.Length > 0) lines.Add(line.ToString());
        return lines;
    }
    // Plain-text version for places that cannot draw glyphs: dialogue boxes, tests.
    public static string Plain(string text, Style st = Style.Keyboard)
    {
        var sb = new StringBuilder();
        foreach (var (g, t) in Parse(text))
        {
            if (!g) { sb.Append(t); continue; }
            var ctl = Controls(t, st);
            for (int i = 0; i < ctl.Length; i++)
            {
                var (id, hold) = Split(ctl[i]);
                sb.Append(id is "+" or "/" ? " " + id + " " : Name(id, st)).Append(hold ? " (hold)" : "");
            }
        }
        return sb.ToString();
    }
    public static string Name(string id, Style st)
    {
        if (id.StartsWith("K:")) return KeyLabel(id[2..]);
        if (id.StartsWith("M:")) return id[2..] switch { "L" => "left click", "R" => "right click", "wheel" => "the mouse wheel", _ => "the mouse" };
        if (id is "A" or "B" or "X" or "Y")
            return st == Style.PlayStation ? id switch { "A" => "Cross", "B" => "Circle", "X" => "Square", _ => "Triangle" } : FaceLabel(st, id);
        if (id is "LB" or "RB" or "LT" or "RT") return ShoulderLabel(st, id);
        string stickName(char side) => side == 'L' ? "the left stick" : "the right stick";
        if (id.StartsWith("LS") || id.StartsWith("RS")) return stickName(id[0]) + (id.EndsWith(":up") ? " up" : id.EndsWith(":down") ? " down" : "");
        if (id is "L3" or "R3") return st == Style.Xbox ? (id == "L3" ? "LS click" : "RS click") : id;
        return id switch
        {
            "DU" => "d-pad up", "DD" => "d-pad down", "DL" => "d-pad left", "DR" => "d-pad right", "DLR" => "d-pad left / right",
            "BACK" => st switch { Style.PlayStation => "Create", Style.Switch => "-", _ => "View" },
            "START" => st switch { Style.PlayStation => "Options", Style.Switch => "+", _ => "Menu" },
            "TOUCH" => "the touchpad",
            _ => id,
        };
    }
}
