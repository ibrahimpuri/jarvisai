// The title screen: the island at night seen from the water, the abbey's last lit windows, a boat with
// one lantern rowing in, rain and the odd flash of lightning, and the name of the place over it all.
using System;
using Godot;

namespace Hollowmere;

public partial class TitleVista : Control
{
    readonly Canvas cv = new();
    public bool ShowName = true;
    readonly float t0 = Time.GetTicksMsec() / 1000f;
    static readonly (float x, float y, float l, float v)[] rain = new (float, float, float, float)[90];

    public TitleVista()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        for (int i = 0; i < rain.Length; i++) rain[i] = (TileArt.Hash(i, 61) * 700, TileArt.Hash(i, 62) * 400, 6 + TileArt.Hash(i, 63) * 6, 260 + TileArt.Hash(i, 64) * 120);
    }
    public override void _Process(double d) => QueueRedraw();

    public override void _Draw()
    {
        var c = cv.Begin(this);
        float t = Time.GetTicksMsec() / 1000f - t0 + 40;
        const float W = H.VIEW_W, Hh = H.VIEW_H;
        // lightning: rare, double-struck
        float ph = t % 23f, flash = ph < 0.12f ? 1 - ph / 0.12f : ph > 0.25f && ph < 0.35f ? 0.6f * (1 - (ph - 0.25f) / 0.1f) : 0;
        var cam = new Vector2(7 * 640 + MathF.Sin(t * 0.05f) * 30, 4 * 360 - 20);
        Backdrop.Outdoor(c, cam, t, flash, 235);

        // the boat, rocking on the swell, its lantern doubled in the water
        float bx = 470 + MathF.Sin(t * 0.07f) * 14, by = 300 + MathF.Sin(t * 1.1f) * 1.2f, rock = MathF.Sin(t * 1.1f + 0.6f) * 0.06f;
        c.FillRectGradV(bx - 1.5f, by + 2, 3, 40, new Color(1, 0.75f, 0.4f, 0.35f), new Color(1, 0.75f, 0.4f, 0));
        for (int i = 0; i < 8; i++) { float yy = by + 4 + i * 4.5f, ww = 4 + i * 1.6f + MathF.Sin(t * 3 + i) * 2; c.Fill = new Color(1, 0.8f, 0.5f, 0.3f - i * 0.03f); c.FillRect(bx - ww / 2 + MathF.Sin(t * 2 + i * 1.3f) * 2, yy, ww, 0.8f); }
        c.Save(); c.Translate(bx, by); c.Rotate(rock);
        c.Fill = H.Col("#0b0c0d");
        c.BeginPath(); c.MoveTo(-22, -3); c.QuadraticCurveTo(-18, 5, -6, 5); c.LineTo(14, 5); c.QuadraticCurveTo(22, 3, 25, -5); c.LineTo(20, -3); c.LineTo(-18, -3); c.ClosePath(); c.FillPath();
        c.Fill = new Color(0.4f, 0.48f, 0.55f, 0.5f); c.FillRect(-18, -3.4f, 38, 0.7f);
        // Maren at the oars: helmet, shoulders, the oar dipping
        float stroke = MathF.Sin(t * 1.4f);
        c.Fill = H.Col("#0b0c0d"); c.FillRect(-4, -11, 8, 8);
        c.FillCircle(0, -14.5f, 4.4f);
        c.Stroke = new Color(0.45f, 0.4f, 0.3f, 0.6f); c.LineWidth = 0.8f; c.BeginPath(); c.Arc(0, -14.5f, 4.4f, -H.PI * 0.9f, -H.PI * 0.4f); c.StrokePath();
        c.Fill = new Color(0.5f, 0.42f, 0.28f, 0.8f); c.FillCircle(2.2f, -14.3f, 1.6f);
        c.Stroke = H.Col("#0b0c0d"); c.LineWidth = 1.4f; c.Line(-2, -7, -16 - stroke * 6, 4 + stroke * 2);
        // the lantern on its pole at the bow
        c.Line(18, -3, 21, -17);
        c.Fill = H.Col("#ffd27a"); c.FillRect(19.6f, -16.5f, 3, 4);
        c.Restore();
        float lx = bx + 21 * MathF.Cos(rock) + 15 * MathF.Sin(rock), ly = by - 14.5f;
        c.Glow(lx, ly, 26, new Color(1, 0.7f, 0.35f, 0.32f));
        c.Glow(lx, ly, 6, new Color(1.6f, 1.2f, 0.6f, 0.8f));

        // nearer swell and a dark spit of rock under the menu
        for (int i = 0; i < 5; i++)
        {
            float y = 318 + i * 9, off = (t * (10 + i * 6) + i * 70) % 120;
            c.Fill = new Color(0.45f, 0.55f, 0.65f, 0.08f);
            for (float x = -120 + off; x < W; x += 120) c.FillRect(x, y, 60 + i * 10, 1);
        }
        c.Fill = H.Col("#050709");
        c.Poly(new[] { new Vector2(0, Hh), new Vector2(0, 268), new Vector2(40, 262), new Vector2(90, 276), new Vector2(150, 290), new Vector2(210, 312), new Vector2(260, 334), new Vector2(300, Hh) });
        c.Fill = new Color(0.3f, 0.38f, 0.46f, 0.45f);
        c.Poly(new[] { new Vector2(0, 268), new Vector2(40, 262), new Vector2(90, 276), new Vector2(90, 277), new Vector2(40, 263.2f), new Vector2(0, 269.2f) });
        for (int i = 0; i < 6; i++) { float fx = 200 + i * 14 + MathF.Sin(t * 1.3f + i) * 3; c.Fill = new Color(0.8f, 0.86f, 0.9f, 0.25f + 0.15f * MathF.Sin(t * 2 + i)); c.FillRect(fx, 308 + i * 4.5f, 10, 0.8f); }

        // rain
        c.Stroke = new Color(0.6f, 0.7f, 0.8f, 0.22f); c.LineWidth = 0.6f;
        foreach (var r in rain)
        {
            float y = (r.y + t * r.v) % 400 - 20, x = (r.x - t * r.v * 0.18f) % 700; if (x < -30) x += 700;
            c.Line(x, y, x - r.l * 0.18f, y + r.l);
        }
        // keep the left side quiet for the title and the menu
        c.FillRectGradH(0, 0, 340, Hh, new Color(0, 0, 0, 0.62f), new Color(0, 0, 0, 0));
        if (flash > 0) c.FillRect(0, 0, W, Hh, new Color(0.75f, 0.82f, 0.9f, flash * 0.08f));

        if (!ShowName) return;
        // the name
        float tx = 46, ty = 98;
        c.Font = "34px caps";
        c.Glow(tx + 120, ty - 10, 160, 34, new Color(0.9f, 0.7f, 0.35f, 0.07f));
        c.Fill = new Color(0, 0, 0, 0.7f); c.FillText("Hollowmere", tx + 1.2f, ty + 1.2f);
        c.Fill = H.Col("#d9b36a"); c.FillText("Hollowmere", tx, ty);
        float tw = c.MeasureText("Hollowmere");
        Hud.Flourish(c, tx + tw / 2, ty + 9, tw / 2 + 6, 1);
        c.Font = "italic 8px serif"; c.TextAlign = 1;
        c.Fill = H.Col("#b8ab8c"); c.FillText("A tide-bound descent through a drowned island", tx + tw / 2, ty + 22);
        c.TextAlign = 0;
    }
}
