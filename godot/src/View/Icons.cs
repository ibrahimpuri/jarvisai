// Small drawn icons for items, weapons, abilities and the HUD, so nothing is named by text alone.
// Each fits a square of side s centred on (cx, cy) and carries a dark outline to read on any ground.
using System;
using Godot;

namespace Hollowmere;

public static class Icons
{
    static readonly Color INK = new(0.05f, 0.04f, 0.035f, 0.9f);
    static Color C(string hex, float a = 1) { var c = H.Col(hex); c.A = a; return c; }

    static void Outline(Canvas c, float w = 1.1f) { c.Stroke = INK; c.LineWidth = w; c.StrokePath(); }
    static void Shape(Canvas c, Color fill, float ow = 1.1f) { c.Fill = fill; c.FillPath(); Outline(c, ow); }
    static void Disc(Canvas c, float x, float y, float r, Color fill, float ow = 1) { c.BeginPath(); c.Arc(x, y, r, 0, H.PI * 2); c.ClosePath(); Shape(c, fill, ow); }
    static void Box(Canvas c, float x, float y, float w, float h, Color fill, float ow = 1) { c.BeginPath(); c.Rect(x, y, w, h); Shape(c, fill, ow); }
    static void Shine(Canvas c, float x, float y, float w, float h, float a = 0.55f) => c.FillRect(x, y, w, h, new Color(1, 0.97f, 0.88f, a));

    public static void Draw(Canvas c, string id, float cx, float cy, float s, float alpha = 1)
    {
        c.Save();
        float a0 = c.GlobalAlpha; c.GlobalAlpha = a0 * alpha;
        c.Translate(cx, cy);
        float k = s / 16f;
        c.Scale(k, k);
        switch (id)
        {
            case "rivets":
                for (int i = 0; i < 3; i++)
                {
                    float x = -5 + i * 5, y = i == 1 ? -2 : 1;
                    Box(c, x - 1, y, 2, 6, C("#8a7448"), 0.8f);
                    c.BeginPath(); c.Ellipse(x, y, 3.2f, 1.8f, 0, H.PI, H.PI * 2); c.ClosePath(); Shape(c, C("#d7bd7c"), 0.8f);
                    Shine(c, x - 1.5f, y - 1.2f, 1.5f, 0.6f);
                }
                break;
            case "bolts":
                c.BeginPath(); c.MoveTo(-7, 5); c.LineTo(4, -6); c.LineTo(5, -5); c.LineTo(-6, 6); c.ClosePath(); Shape(c, C("#9fb0b8"), 0.8f);
                c.BeginPath(); c.MoveTo(7, -7); c.LineTo(2, -6); c.LineTo(6, -2); c.ClosePath(); Shape(c, C("#d0dde2"), 0.8f);
                c.BeginPath(); c.MoveTo(-7, 5); c.LineTo(-8, 1); c.LineTo(-5, 3); c.ClosePath(); Shape(c, C("#6a7a80"), 0.6f);
                break;
            case "flares":
                c.Save(); c.Rotate(-0.5f);
                Box(c, -2.5f, -4, 5, 10, C("#c8452f"));
                Box(c, -2.5f, -4, 5, 2, C("#e8d8b0"), 0.6f);
                c.Restore();
                c.Glow(3.5f, -6.5f, 4, new Color(1.4f, 0.8f, 0.3f, 0.8f));
                c.FillRect(2.5f, -7.5f, 2, 2, C("#fff0b0"));
                break;
            case "spools":
                Box(c, -6, -5, 12, 2, C("#7a5a3a"), 0.8f); Box(c, -6, 3, 12, 2, C("#7a5a3a"), 0.8f);
                Box(c, -4, -3, 8, 6, C("#c8ccd0"), 0.8f);
                for (int i = -2; i <= 2; i += 2) c.FillRect(-4, i - 0.3f, 8, 0.6f, C("#7f858a"));
                break;
            case "oil":
                c.BeginPath(); c.MoveTo(-2, -6); c.LineTo(2, -6); c.LineTo(2, -3); c.QuadraticCurveTo(6, -1, 6, 3); c.QuadraticCurveTo(6, 7, 0, 7); c.QuadraticCurveTo(-6, 7, -6, 3); c.QuadraticCurveTo(-6, -1, -2, -3); c.ClosePath();
                Shape(c, C("#3a4a4c", 0.95f));
                c.BeginPath(); c.MoveTo(-5.2f, 1.5f); c.QuadraticCurveTo(0, 0.5f, 5.2f, 1.5f); c.QuadraticCurveTo(5.4f, 6.2f, 0, 6.2f); c.QuadraticCurveTo(-5.4f, 6.2f, -5.2f, 1.5f); c.ClosePath(); c.Fill = C("#e3a83a"); c.FillPath();
                Box(c, -2.2f, -8, 4.4f, 2.5f, C("#9a7448"), 0.7f);
                Shine(c, -4, -1, 1, 5, 0.45f);
                break;
            case "poultice":
                c.BeginPath(); c.MoveTo(-6, -1); c.QuadraticCurveTo(-6, 6, 0, 6); c.QuadraticCurveTo(6, 6, 6, -1); c.ClosePath(); Shape(c, C("#5d8d4f"));
                c.BeginPath(); c.MoveTo(-6, -1); c.QuadraticCurveTo(0, -8, 6, -1); c.QuadraticCurveTo(0, -3, -6, -1); c.ClosePath(); Shape(c, C("#7fb06a"), 0.8f);
                c.Stroke = C("#d8c79a"); c.LineWidth = 1.2f; c.Line(-6, 1, 6, 1);
                c.BeginPath(); c.MoveTo(0, -5); c.QuadraticCurveTo(2.5f, -7.5f, 1, -8.5f); c.Stroke = C("#a0d080"); c.LineWidth = 1; c.StrokePath();
                break;
            case "stitchkit":
                c.BeginPath(); c.MoveTo(-7, -4); c.LineTo(7, -4); c.LineTo(7, 5); c.LineTo(-7, 5); c.ClosePath(); Shape(c, C("#6a4632"));
                c.FillRect(-7, -1, 14, 1.2f, C("#4a2e20"));
                c.FillRect(-1.2f, -3, 2.4f, 7, C("#d24b42")); c.FillRect(-3.5f, 0.3f, 7, 2.4f, C("#d24b42"));
                c.Stroke = C("#dfe6ea"); c.LineWidth = 0.8f; c.Line(4, -7, 8, -3);
                break;
            case "scrap":
                c.BeginPath(); c.MoveTo(-6, -3); c.LineTo(-2, -6); c.LineTo(4, -5); c.LineTo(7, 0); c.LineTo(3, 6); c.LineTo(-3, 5); c.LineTo(-7, 2); c.ClosePath(); Shape(c, C("#8e9092"));
                c.FillRect(-3, -2, 7, 1, C("#b9bbbd")); Disc(c, 1, 1.5f, 1.2f, C("#3a3a3a"), 0.5f);
                c.FillRect(-5, 3, 3, 1, C("#a0602a", 0.7f));
                break;
            case "cord":
                c.Stroke = INK; c.LineWidth = 3.4f; c.BeginPath(); c.Ellipse(0, 1, 6, 4, 0, 0, H.PI * 2); c.StrokePath(); c.BeginPath(); c.Ellipse(0, -1, 4, 2.6f, 0, 0, H.PI * 2); c.StrokePath();
                c.Stroke = C("#b89a6a"); c.LineWidth = 2; c.BeginPath(); c.Ellipse(0, 1, 6, 4, 0, 0, H.PI * 2); c.StrokePath(); c.BeginPath(); c.Ellipse(0, -1, 4, 2.6f, 0, 0, H.PI * 2); c.StrokePath();
                c.Stroke = C("#8a6e46"); c.LineWidth = 1; c.Line(5, 3, 7, 7);
                break;
            case "kelp":
                foreach (var (ox, ph, col) in new[] { (-2f, 0f, "#4f8a4f"), (2f, 1.4f, "#6aa860") })
                {
                    c.BeginPath(); c.MoveTo(ox, 7);
                    for (int i = 1; i <= 7; i++) c.LineTo(ox + MathF.Sin(i * 1.1f + ph) * 2.2f, 7 - i * 2);
                    c.Stroke = INK; c.LineWidth = 3.2f; c.StrokePath();
                    c.BeginPath(); c.MoveTo(ox, 7);
                    for (int i = 1; i <= 7; i++) c.LineTo(ox + MathF.Sin(i * 1.1f + ph) * 2.2f, 7 - i * 2);
                    c.Stroke = C(col); c.LineWidth = 2; c.StrokePath();
                }
                break;
            case "salt":
                c.BeginPath(); c.MoveTo(-7, 6); c.LineTo(-4, 1); c.LineTo(-2, 3); c.LineTo(0, -4); c.LineTo(2, 2); c.LineTo(4, -1); c.LineTo(7, 6); c.ClosePath(); Shape(c, C("#e8e8f0"));
                c.FillRect(-0.5f, -2, 1, 6, C("#ffffff", 0.8f));
                break;
            case "tallow":
                Box(c, -4, -3, 8, 9, C("#e8d8a0"));
                c.BeginPath(); c.MoveTo(-4, -3); c.QuadraticCurveTo(-2, -1, 0, -3); c.QuadraticCurveTo(2, -1, 4, -3); c.Stroke = C("#c8b880"); c.LineWidth = 0.8f; c.StrokePath();
                c.FillRect(-0.4f, -6, 0.8f, 3, C("#3a3028"));
                break;
            case "spirit":
                c.BeginPath(); c.MoveTo(-1.5f, -6); c.LineTo(1.5f, -6); c.LineTo(1.5f, -2); c.LineTo(4, 1); c.LineTo(4, 6); c.LineTo(-4, 6); c.LineTo(-4, 1); c.LineTo(-1.5f, -2); c.ClosePath(); Shape(c, C("#9cc4f0", 0.9f));
                c.FillRect(-3.4f, 2, 6.8f, 3.5f, C("#5d8fd0", 0.8f));
                Box(c, -2, -8, 4, 2, C("#7a6a5a"), 0.6f);
                Shine(c, -3, 0, 0.8f, 4, 0.6f);
                break;
            case "ivory":
                c.BeginPath(); c.MoveTo(-6, 6); c.QuadraticCurveTo(-7, -4, 4, -7); c.QuadraticCurveTo(7, -7, 6, -5); c.QuadraticCurveTo(-3, -2, -3, 6); c.ClosePath(); Shape(c, C("#f4ecd8"));
                c.Stroke = C("#c8b890"); c.LineWidth = 0.6f; c.Line(-4, 3, -4.5f, -1); c.Line(-2, -3, 1, -5);
                break;
            case "crank":
                c.Stroke = INK; c.LineWidth = 4; c.BeginPath(); c.MoveTo(-6, 6); c.LineTo(-6, -4); c.LineTo(5, -4); c.LineTo(5, 1); c.StrokePath();
                c.Stroke = C("#c08040"); c.LineWidth = 2.4f; c.BeginPath(); c.MoveTo(-6, 6); c.LineTo(-6, -4); c.LineTo(5, -4); c.LineTo(5, 1); c.StrokePath();
                Disc(c, 5, 3, 2, C("#6a4a2a"), 0.8f);
                break;
            case "tags":
                c.Stroke = C("#9a9a90"); c.LineWidth = 0.7f; c.BeginPath(); c.MoveTo(-3, -4); c.QuadraticCurveTo(0, -9, 3, -4); c.StrokePath();
                foreach (var (ox, oy) in new[] { (-3f, 0f), (2f, 1.5f) })
                {
                    c.BeginPath(); c.Ellipse(ox, oy, 3, 4.5f, 0, 0, H.PI * 2); c.ClosePath(); Shape(c, C("#d0c080"), 0.8f);
                    c.FillRect(ox - 1.5f, oy - 1, 3, 0.6f, C("#8a7a40")); c.FillRect(ox - 1.5f, oy + 0.6f, 2.4f, 0.6f, C("#8a7a40"));
                }
                break;
            case "clapper":
                c.Stroke = INK; c.LineWidth = 2.8f; c.Line(0, -7, 0, 2);
                c.Stroke = C("#8a7a50"); c.LineWidth = 1.4f; c.Line(0, -7, 0, 2);
                Disc(c, 0, 4, 3, C("#b0a070"));
                break;
            case "prism":
                c.BeginPath(); c.MoveTo(0, -7); c.LineTo(6, 0); c.LineTo(0, 7); c.LineTo(-6, 0); c.ClosePath(); Shape(c, C("#d0f0ff", 0.85f));
                c.BeginPath(); c.MoveTo(0, -7); c.LineTo(2, 0); c.LineTo(0, 7); c.LineTo(-6, 0); c.ClosePath(); c.Fill = C("#ffffff", 0.35f); c.FillPath();
                break;
            // ---- weapons
            case "driver":
                Box(c, -7, -3, 9, 5, C("#6f6a60"));
                Box(c, 2, -2, 6, 3, C("#8a857a"), 0.8f);
                c.BeginPath(); c.MoveTo(-5, 2); c.LineTo(-2, 2); c.LineTo(-3, 7); c.LineTo(-6, 7); c.ClosePath(); Shape(c, C("#5a3e2a"));
                Disc(c, -3, -1, 1.6f, C("#c9b37a"), 0.6f);
                Shine(c, -6, -2.5f, 7, 0.6f, 0.35f);
                break;
            case "harpoon":
                c.Stroke = INK; c.LineWidth = 3.4f; c.Line(-8, 3, 7, -3);
                c.Stroke = C("#7d8c92"); c.LineWidth = 2; c.Line(-8, 3, 7, -3);
                c.BeginPath(); c.MoveTo(8, -3.4f); c.LineTo(4, -5.5f); c.LineTo(4.6f, -1.6f); c.ClosePath(); Shape(c, C("#d0dde2"), 0.7f);
                c.BeginPath(); c.MoveTo(-6, 2); c.LineTo(-3, 2); c.LineTo(-4, 7); c.LineTo(-7, 7); c.ClosePath(); Shape(c, C("#5a3e2a"));
                break;
            case "sling":
                c.Stroke = INK; c.LineWidth = 3; c.BeginPath(); c.MoveTo(-6, 7); c.LineTo(-1, -1); c.LineTo(-5, -7); c.MoveTo(-1, -1); c.LineTo(4, -6); c.StrokePath();
                c.Stroke = C("#8a6a4a"); c.LineWidth = 1.8f; c.BeginPath(); c.MoveTo(-6, 7); c.LineTo(-1, -1); c.LineTo(-5, -7); c.MoveTo(-1, -1); c.LineTo(4, -6); c.StrokePath();
                c.Glow(5, -6, 4, new Color(1.4f, 0.7f, 0.3f, 0.7f));
                Disc(c, 5, -6, 1.6f, C("#ff9a4a"), 0.6f);
                break;
            case "sawline":
                Disc(c, -1, 1, 6, C("#5a4a3a"));
                Disc(c, -1, 1, 3.6f, C("#c8ccd0"), 0.7f);
                Disc(c, -1, 1, 1.2f, C("#3a3a3a"), 0.5f);
                c.Stroke = C("#dfe3e6"); c.LineWidth = 0.7f; c.Line(4.5f, 1, 8, -6);
                break;
            // ---- the legendary weapons
            case "tine":
                // the harpoon with the ear-bone grown through it, curled at the head like a shell
                c.Stroke = INK; c.LineWidth = 3.6f; c.Line(-8, 4, 6, -3);
                c.Stroke = C("#d8ccae"); c.LineWidth = 2.2f; c.Line(-8, 4, 6, -3);
                c.Stroke = C("#8a7c5c"); c.LineWidth = 0.6f; for (int i = 0; i < 4; i++) { float t = -5 + i * 2.6f; c.Line(t, 2.6f - i * 1.3f - 1.2f, t + 0.8f, 2.6f - i * 1.3f + 1.2f); }
                c.BeginPath(); c.MoveTo(6, -3); c.QuadraticCurveTo(10, -7, 8, -8); c.QuadraticCurveTo(5, -8, 6.5f, -5.5f); c.ClosePath(); Shape(c, C("#efe6cf"), 0.7f);
                c.BeginPath(); c.MoveTo(-6, 3); c.LineTo(-3, 3); c.LineTo(-4, 7.5f); c.LineTo(-7, 7.5f); c.ClosePath(); Shape(c, C("#5a3e2a"));
                c.Glow(7, -5, 5, new Color(0.4f, 1f, 0.85f, 0.45f));
                break;
            case "glass":
                // a brass lantern hood with the ivory lens in its mouth
                c.BeginPath(); c.MoveTo(-6, -4); c.LineTo(2, -4); c.LineTo(6, -6.5f); c.LineTo(6, 6.5f); c.LineTo(2, 4); c.LineTo(-6, 4); c.ClosePath(); Shape(c, C("#b08840"));
                Box(c, -6, -4, 8, 1.4f, C("#e0c070"), 0.5f);
                c.BeginPath(); c.Ellipse(6, 0, 1.8f, 6, 0, 0, H.PI * 2); c.ClosePath(); Shape(c, C("#f4ecd8"), 0.7f);
                c.Glow(7, 0, 7, new Color(1.4f, 1.1f, 0.7f, 0.55f));
                Shine(c, -5, -3, 6, 0.5f, 0.4f);
                break;
            // ---- abilities
            case "swing":
                c.Stroke = INK; c.LineWidth = 3.2f; c.BeginPath(); c.MoveTo(-6, 7); c.LineTo(2, -3); c.Arc(3.5f, -4.5f, 3, H.PI * 0.75f, H.PI * 2.1f); c.StrokePath();
                c.Stroke = C("#a6a8aa"); c.LineWidth = 1.8f; c.BeginPath(); c.MoveTo(-6, 7); c.LineTo(2, -3); c.Arc(3.5f, -4.5f, 3, H.PI * 0.75f, H.PI * 2.1f); c.StrokePath();
                break;
            case "boots":
                c.BeginPath(); c.MoveTo(-3, -7); c.LineTo(3, -7); c.LineTo(3, 2); c.LineTo(7, 3); c.LineTo(7, 6); c.LineTo(-4, 6); c.LineTo(-4, -1); c.ClosePath(); Shape(c, C("#6a5a40"));
                Box(c, -5, 5, 13, 2.4f, C("#b08840"), 0.8f);
                break;
            case "jet":
                Box(c, -3, -6, 6, 9, C("#8a7a5a"));
                Box(c, -1.2f, 3, 2.4f, 3, C("#5a5040"), 0.7f);
                for (int i = 0; i < 3; i++) { c.Stroke = C("#b8e0f0", 0.9f); c.LineWidth = 0.8f; c.BeginPath(); c.Arc(-4 + i * 4, 7 + (i % 2) * -1.5f, 1.3f, 0, H.PI * 2); c.StrokePath(); }
                break;
            case "tidekey":
                Disc(c, -3, -3, 3.6f, C("#c0a060"));
                Disc(c, -3, -3, 1.4f, C("#2a2418"), 0.5f);
                c.Stroke = INK; c.LineWidth = 2.8f; c.Line(-1, -1, 7, 7);
                c.Stroke = C("#c0a060"); c.LineWidth = 1.6f; c.Line(-1, -1, 7, 7);
                c.Stroke = C("#c0a060"); c.LineWidth = 1.2f; c.BeginPath(); c.MoveTo(4, 4); c.QuadraticCurveTo(6, 2, 7, 4); c.StrokePath();
                break;
            case "seal":
                c.Stroke = INK; c.LineWidth = 4.4f; c.BeginPath(); c.Arc(0, 0, 5, 0, H.PI * 2); c.StrokePath();
                c.Stroke = C("#b08840"); c.LineWidth = 3; c.BeginPath(); c.Arc(0, 0, 5, 0, H.PI * 2); c.StrokePath();
                for (int i = 0; i < 6; i++) { float a = i * H.PI / 3; c.FillRect(MathF.Cos(a) * 5 - 0.6f, MathF.Sin(a) * 5 - 0.6f, 1.2f, 1.2f, C("#f0d890")); }
                break;
            case "lens":
                Disc(c, -1, -1, 5, C("#a8e0f0", 0.7f));
                c.Stroke = C("#b08840"); c.LineWidth = 1.4f; c.BeginPath(); c.Arc(-1, -1, 5, 0, H.PI * 2); c.StrokePath();
                c.Stroke = INK; c.LineWidth = 3; c.Line(3, 3, 7, 7); c.Stroke = C("#6a5030"); c.LineWidth = 1.6f; c.Line(3, 3, 7, 7);
                Shine(c, -4, -4, 2, 1, 0.7f);
                break;
            // ---- HUD symbols
            case "lantern":
            case "lantern_dark":
                {
                    bool lit = id == "lantern";
                    c.Stroke = C("#6a5a3a"); c.LineWidth = 1; c.BeginPath(); c.Arc(0, -6.5f, 2.2f, H.PI, H.PI * 2); c.StrokePath();
                    Box(c, -4.5f, -5, 9, 2, C("#8a6a30"), 0.8f);
                    Box(c, -4, -3, 8, 8, lit ? C("#ffd27a") : C("#2c2820"), 0.9f);
                    if (lit) { c.Glow(0, 1, 6, new Color(1.3f, 0.9f, 0.4f, 0.35f)); c.FillRect(-1, -1, 2, 4, C("#fff6d8")); }
                    c.FillRect(-0.4f, -3, 0.8f, 8, C("#6a5020", 0.9f));
                    Box(c, -4.5f, 5, 9, 2, C("#8a6a30"), 0.8f);
                    break;
                }
            case "helmet":
                Disc(c, 0, 0, 6.5f, C("#b08840"));
                Disc(c, 1, 0, 3.2f, C("#2a3a40"), 0.8f);
                Shine(c, -4, -3.5f, 2, 1, 0.5f);
                break;
            default:
                Disc(c, 0, 0, 5, C("#9a9080"));
                break;
        }
        c.Restore();
        c.GlobalAlpha = a0;
    }

    // Draws an item glyph for an id, falling back on the item's colour.
    public static bool Has(string id) => id switch
    {
        "rivets" or "bolts" or "flares" or "spools" or "oil" or "poultice" or "stitchkit" or "scrap" or "cord" or "kelp" or "salt" or "tallow"
            or "spirit" or "ivory" or "crank" or "tags" or "clapper" or "prism" or "driver" or "harpoon" or "sling" or "sawline"
            or "swing" or "boots" or "jet" or "tidekey" or "seal" or "lens" or "lantern" or "lantern_dark" or "helmet" => true,
        _ => false,
    };
}
