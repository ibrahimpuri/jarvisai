// HUD drawn over the world, and the logbook map.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Hud
{
    static readonly Dictionary<string, string> PH_COL = new() { ["low"] = "#3d4b52", ["rising"] = "#3f7a86", ["high"] = "#244a73", ["ebbing"] = "#587a64" };
    static readonly Color GOLD = H.Col("#c9a14a"), GOLD_DIM = H.Col("#7a6230"), IRON = H.Rgba(14, 13, 12, 0.72f), TEXT = H.Col("#eadfc4"), MUTED = H.Col("#a89c80");

    // A dark iron plate with a gold rule along the top and rivets at the corners.
    public static void Plate(Canvas c, float x, float y, float w, float h, float a = 1)
    {
        c.Fill = new Color(0, 0, 0, 0.35f * a); c.FillRect(x + 1.5f, y + 1.5f, w, h);
        c.FillRectGradV(x, y, w, h, new Color(0.09f, 0.085f, 0.075f, 0.82f * a), new Color(0.045f, 0.04f, 0.035f, 0.82f * a));
        c.Fill = new Color(GOLD.R, GOLD.G, GOLD.B, 0.55f * a); c.FillRect(x, y, w, 0.8f);
        c.Fill = new Color(GOLD_DIM.R, GOLD_DIM.G, GOLD_DIM.B, 0.45f * a); c.FillRect(x, y + h - 0.8f, w, 0.8f);
        foreach (var (rx, ry) in new[] { (x + 2.5f, y + 2.5f), (x + w - 2.5f, y + 2.5f), (x + 2.5f, y + h - 2.5f), (x + w - 2.5f, y + h - 2.5f) })
        {
            c.Fill = new Color(0.55f, 0.45f, 0.25f, 0.8f * a); c.FillCircle(rx, ry, 1);
            c.Fill = new Color(1, 0.9f, 0.6f, 0.5f * a); c.FillRect(rx - 0.5f, ry - 0.6f, 0.6f, 0.5f);
        }
    }
    // A thin gold rule with a diamond at its centre, for titles.
    public static void Flourish(Canvas c, float cx, float y, float half, float a)
    {
        var g = new Color(GOLD.R, GOLD.G, GOLD.B, 0.6f * a);
        c.FillRectGradH(cx - half, y, half - 5, 0.8f, new Color(g.R, g.G, g.B, 0), g);
        c.FillRectGradH(cx + 5, y, half - 5, 0.8f, g, new Color(g.R, g.G, g.B, 0));
        c.Save(); c.Translate(cx, y + 0.4f); c.Rotate(H.PI / 4); c.Fill = g; c.FillRect(-2, -2, 4, 4); c.Restore();
    }

    // Health: brass-rimmed glass lamps, lit amber while full.
    static void Pips(Canvas c, Player p)
    {
        float pulse = p.hp <= 1 ? 0.5f + 0.5f * MathF.Sin(G.Now * 7) : 0;
        for (int i = 0; i < p.maxHp; i++)
        {
            float x = 17 + i * 14, y = 17;
            bool full = i < p.hp;
            c.Save(); c.Translate(x, y); c.Rotate(H.PI / 4);
            c.Fill = new Color(0, 0, 0, 0.45f); c.FillRect(-5.2f, -4.2f, 10.4f, 10.4f);
            c.Fill = GOLD_DIM; c.FillRect(-5.2f, -5.2f, 10.4f, 10.4f);
            c.Fill = GOLD; c.FillRect(-4.6f, -4.6f, 9.2f, 9.2f);
            if (full)
            {
                c.FillRectGradV(-3.6f, -3.6f, 7.2f, 7.2f, H.Col("#ffe3a0"), H.Col("#c8641e").Lerp(H.Col("#e03020"), pulse));
                c.Fill = new Color(1, 1, 0.9f, 0.7f); c.FillRect(-3, -3, 2.2f, 2.2f);
            }
            else
            {
                c.Fill = H.Col("#1c1915"); c.FillRect(-3.6f, -3.6f, 7.2f, 7.2f);
                c.Stroke = new Color(0.5f, 0.45f, 0.38f, 0.5f); c.LineWidth = 0.5f; c.Line(-2.5f, 1.5f, 0.5f, -0.5f); c.Line(0.5f, -0.5f, 2, 1);
            }
            c.Restore();
            if (full) c.Glow(x, y, 9, new Color(1.2f, 0.6f, 0.2f, 0.12f + pulse * 0.25f));
        }
    }

    // Breath shows only underwater, as bubbles rising from the helmet.
    static void Breath(Canvas c, Player p)
    {
        float max = p.BreathMax();
        if (!float.IsFinite(max)) return;
        if (!p.HeadUnder() && p.breath >= max - 0.1f) return;
        int n = (int)MathF.Ceiling(p.breath / 3), total = (int)MathF.Ceiling(max / 3);
        Icons.Draw(c, "helmet", 15, 50, 11);
        for (int i = 0; i < total; i++)
        {
            float x = 26 + i * 7.5f, y = 50 + MathF.Sin(G.Now * 3 + i) * 0.6f;
            bool on = i < n;
            c.Stroke = on ? H.Rgba(190, 228, 255, 0.95f) : H.Rgba(90, 110, 130, 0.45f); c.LineWidth = 0.9f;
            c.BeginPath(); c.Arc(x, y, 2.6f, 0, H.PI * 2); c.StrokePath();
            if (on) { c.Fill = H.Rgba(150, 200, 240, 0.25f); c.FillCircle(x, y, 2.2f); c.Fill = new Color(1, 1, 1, 0.8f); c.FillRect(x - 1.3f, y - 1.4f, 0.9f, 0.9f); }
        }
    }

    // The lantern and its oil: a glass gauge, and the spare flasks beside it.
    static void Lantern(Canvas c, Player p)
    {
        float y = 34;
        Icons.Draw(c, p.LanternLit() ? "lantern" : "lantern_dark", 15, y, 13);
        float gx = 24, gw = 56, f = Math.Clamp(p.oil / p.OilMax(), 0, 1);
        c.Fill = new Color(0, 0, 0, 0.55f); c.FillRect(gx - 1, y - 3, gw + 2, 6);
        c.Fill = GOLD_DIM; c.FillRect(gx - 1, y - 3, gw + 2, 0.7f); c.FillRect(gx - 1, y + 2.3f, gw + 2, 0.7f);
        c.FillRect(gx - 1.5f, y - 3, 0.8f, 6); c.FillRect(gx + gw + 0.7f, y - 3, 0.8f, 6);
        if (f > 0)
        {
            var top = p.focusing ? H.Col("#fff2c8") : H.Col("#f3c35a"); var bot = p.focusing ? H.Col("#e9b65a") : H.Col("#a8661c");
            c.FillRectGradV(gx, y - 1.8f, gw * f, 3.6f, top, bot);
            c.Fill = new Color(1, 1, 0.9f, 0.45f); c.FillRect(gx, y - 1.6f, gw * f, 0.7f);
            if (p.LanternLit()) c.Glow(gx + gw * f, y, 5, new Color(1.2f, 0.8f, 0.3f, 0.25f));
        }
        for (int i = 1; i < 4; i++) c.FillRect(gx + gw * i / 4f, y - 1.8f, 0.5f, 3.6f, new Color(0, 0, 0, 0.35f));
        int flasks = p.bag.Count("oil");
        float fx = gx + gw + 9;
        for (int i = 0; i < Math.Min(flasks, 3); i++) Icons.Draw(c, "oil", fx + i * 8, y, 9);
        if (flasks > 3) { c.Font = "7px sans"; c.Fill = MUTED; c.FillText("×" + flasks, fx + 22, y + 2.5f); }
        if (p.shut) { c.Font = "6px caps"; c.Fill = MUTED; c.FillText("shuttered", gx, y + 10); }
    }

    // The tide dial: a brass bezel, the four phases, a hand, and the phase named beside it.
    static void Dial(Canvas c)
    {
        float cx = H.VIEW_W - 34, cy = 34, r = 21;
        float total = Tide.Total();
        c.Glow(cx, cy, r + 10, new Color(0, 0, 0, 0.5f));
        c.Fill = H.Col("#0d1216"); c.FillCircle(cx, cy, r);
        float a0 = -H.PI / 2;
        for (int i = 0; i < 4; i++)
        {
            float a1 = a0 + (Tide.Len(i) / total) * H.PI * 2;
            var col = H.Col(PH_COL[Tide.PHASES[i].Id]); col.A = Tide.Index() == i ? 0.95f : 0.55f;
            c.Fill = col; c.BeginPath(); c.MoveTo(cx, cy); c.Arc(cx, cy, r - 3, a0, a1); c.ClosePath(); c.FillPath();
            c.Stroke = new Color(0, 0, 0, 0.6f); c.LineWidth = 0.6f; c.Line(cx, cy, cx + MathF.Cos(a0) * (r - 3), cy + MathF.Sin(a0) * (r - 3));
            a0 = a1;
        }
        // waves engraved across the high-water quarter, a crescent for low water
        c.Fill = new Color(1, 1, 1, 0.07f); c.BeginPath(); c.Arc(cx - 3, cy - 5, r - 8, 0, H.PI * 2); c.FillPath();
        // bezel
        c.Stroke = GOLD_DIM; c.LineWidth = 3.2f; c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
        c.Stroke = GOLD; c.LineWidth = 1.2f; c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
        c.Stroke = new Color(1, 0.95f, 0.75f, 0.4f); c.LineWidth = 0.6f; c.BeginPath(); c.Arc(cx, cy, r + 0.6f, H.PI * 1.1f, H.PI * 1.6f); c.StrokePath();
        for (int i = 0; i < 24; i++)
        {
            float a = i * H.PI / 12; bool big = i % 6 == 0;
            c.Stroke = big ? GOLD : new Color(GOLD.R, GOLD.G, GOLD.B, 0.5f); c.LineWidth = big ? 1 : 0.5f;
            c.Line(cx + MathF.Cos(a) * (r - 3.5f), cy + MathF.Sin(a) * (r - 3.5f), cx + MathF.Cos(a) * (r - (big ? 6.5f : 5)), cy + MathF.Sin(a) * (r - (big ? 6.5f : 5)));
        }
        // the hand
        float ha = -H.PI / 2 + (Tide.t / total) * H.PI * 2;
        float hx = cx + MathF.Cos(ha) * (r - 5), hy = cy + MathF.Sin(ha) * (r - 5);
        c.Stroke = new Color(0, 0, 0, 0.6f); c.LineWidth = 2.2f; c.Line(cx + 0.6f, cy + 0.8f, hx + 0.6f, hy + 0.8f);
        c.Stroke = H.Col("#f4e6c0"); c.LineWidth = 1.3f; c.Line(cx, cy, hx, hy);
        c.Save(); c.Translate(hx, hy); c.Rotate(ha + H.PI / 4); c.Fill = H.Col("#f4e6c0"); c.FillRect(-1.6f, -1.6f, 3.2f, 3.2f); c.Restore();
        c.Fill = GOLD; c.FillCircle(cx, cy, 2.4f); c.Fill = H.Col("#3a2a10"); c.FillCircle(cx, cy, 0.9f);
        // the phase, named
        c.TextAlign = 2;
        c.Font = "10px caps"; c.Fill = new Color(0, 0, 0, 0.6f); c.FillText(Tide.PhaseName(), cx - r - 7 + 0.7f, cy - 0.3f);
        c.Fill = TEXT; c.FillText(Tide.PhaseName(), cx - r - 7, cy - 1);
        c.Font = "8px sans"; c.Fill = MUTED; c.FillText(H.FmtTime(Tide.PhaseRemaining()), cx - r - 7, cy + 9);
        if (Tide.spring != 0) { c.Font = "7px caps"; c.Fill = H.Col("#d0806a"); c.FillText("spring tides", cx - r - 7, cy + 18); }
        c.TextAlign = 0;
    }

    // The weapon in hand, its magazine as rivet heads, and the satchel's healing and scrap.
    static void Weapon(Canvas c, Player p)
    {
        var w = p.weapons[p.cur]; var def = Data.Weapons.P(p.cur);
        float pw = 150, ph = 44, x = H.VIEW_W - pw - 8, y = H.VIEW_H - ph - 8;
        Plate(c, x, y, pw, ph);
        // icon in a recessed well
        c.Fill = new Color(0, 0, 0, 0.45f); c.FillRect(x + 6, y + 6, 22, 22);
        c.Stroke = new Color(GOLD.R, GOLD.G, GOLD.B, 0.35f); c.LineWidth = 0.6f; c.StrokeRect(x + 6, y + 6, 22, 22);
        Icons.Draw(c, p.cur, x + 17, y + 17, 19);
        c.Font = "8px caps"; c.Fill = TEXT;
        c.FillText(def.S("name") + (w.tier > 0 ? "  " + new string('I', w.tier + 1) : ""), x + 33, y + 13);
        // magazine
        int mag = p.MagSize(p.cur);
        for (int i = 0; i < mag; i++)
        {
            float mx = x + 36 + i * 8, my = y + 22;
            bool on = i < w.mag;
            c.Fill = on ? H.Col("#d7bd7c") : new Color(0.25f, 0.23f, 0.2f, 0.8f);
            c.BeginPath(); c.Ellipse(mx, my, 2.8f, 1.6f, 0, H.PI, H.PI * 2); c.ClosePath(); c.FillPath();
            c.FillRect(mx - 0.9f, my, 1.8f, 3.6f, on ? H.Col("#8a7448") : new Color(0.2f, 0.18f, 0.16f, 0.8f));
        }
        c.Font = "8px sans"; c.Fill = MUTED; c.TextAlign = 2;
        c.FillText("+" + p.bag.Count(def.S("ammo")), x + pw - 7, y + 25);
        c.TextAlign = 0;
        // other weapons carried
        if (p.weaponOrder.Count > 1)
            for (int i = 0; i < p.weaponOrder.Count; i++)
            {
                bool sel = p.weaponOrder[i] == p.cur;
                c.Save(); c.Translate(x + pw - 9 - (p.weaponOrder.Count - 1 - i) * 7, y + 10); c.Rotate(H.PI / 4);
                c.Fill = sel ? GOLD : new Color(0.35f, 0.3f, 0.22f, 0.9f); c.FillRect(-1.8f, -1.8f, 3.6f, 3.6f); c.Restore();
            }
        // consumables
        float cy = y + 36;
        int k = 0;
        foreach (var item in new[] { "poultice", "stitchkit", "scrap" })
        {
            int n = p.bag.Count(item); float ix = x + 12 + k * 44;
            Icons.Draw(c, item, ix, cy, 10, n > 0 ? 1 : 0.35f);
            c.Font = "8px sans"; c.Fill = n > 0 ? TEXT : MUTED; c.FillText("×" + n, ix + 7, cy + 3);
            k++;
        }
    }

    static void PromptBox(Canvas c)
    {
        string text = G.promptText;
        if (text == null || UI.IsOpen()) return;
        c.Font = "9px serif";
        float w = Glyphs.Measure(c, text) + 22;
        float x = (H.VIEW_W - w) / 2, y = H.VIEW_H - 52;
        c.FillRectGradH(x - 20, y, 20, 18, new Color(0.04f, 0.035f, 0.03f, 0), new Color(0.04f, 0.035f, 0.03f, 0.78f));
        c.Fill = new Color(0.04f, 0.035f, 0.03f, 0.78f); c.FillRect(x, y, w, 18);
        c.FillRectGradH(x + w, y, 20, 18, new Color(0.04f, 0.035f, 0.03f, 0.78f), new Color(0.04f, 0.035f, 0.03f, 0));
        Flourish(c, H.VIEW_W / 2, y, w / 2 + 16, 0.9f);
        c.Fill = new Color(GOLD_DIM.R, GOLD_DIM.G, GOLD_DIM.B, 0.5f); c.FillRect(x, y + 18, w, 0.6f);
        c.Fill = TEXT; Glyphs.DrawText(c, text, x + 11, y + 12.5f);
    }

    // Notifications: a wrapped column under the vitals, leaving the centre for titles and story text.
    static void Toasts(Canvas c)
    {
        c.Font = "8px serif"; c.TextAlign = 0;
        const float maxW = 230;
        float y = 66;
        foreach (var t in H.Toasts)
        {
            float a = MathF.Min(1, MathF.Min(t.T, (t.Max - t.T) * 4 + 0.2f));
            var lines = Glyphs.Wrap(c, t.Text, maxW);
            bool rich = t.Text.Contains('{');
            float lh = rich ? 12 : 10, h = lines.Count * lh + 6;
            c.FillRectGradH(8, y - 9, maxW + 14, h, new Color(0.04f, 0.035f, 0.03f, 0.72f * a), new Color(0.04f, 0.035f, 0.03f, 0));
            c.Fill = new Color(GOLD.R, GOLD.G, GOLD.B, 0.75f * a); c.FillRect(8, y - 9, 1.2f, h);
            c.Fill = new Color(TEXT.R, TEXT.G, TEXT.B, a);
            for (int i = 0; i < lines.Count; i++) Glyphs.DrawText(c, lines[i], 14, y + i * lh + (rich ? 1.5f : 0));
            y += h + 3;
        }
    }

    static void RoomTitle(Canvas c)
    {
        float dt = G.frameDt;
        if (G.titleT > 0)
        {
            float a = MathF.Min(1, MathF.Min(G.titleT, (6 - G.titleT) * 2 + 0.3f));
            float cx = H.VIEW_W / 2;
            c.TextAlign = 1;
            c.Font = "20px caps";
            c.Fill = new Color(0, 0, 0, 0.55f * a); c.FillText(G.room.Name, cx + 1, 99);
            c.Fill = new Color(0.95f, 0.9f, 0.78f, a); c.FillText(G.room.Name, cx, 98);
            float w = c.MeasureText(G.room.Name) / 2 + 14;
            Flourish(c, cx, 104, w + 34, a);
            c.Font = "8px caps"; c.Fill = new Color(0.72f, 0.66f, 0.54f, a);
            string region = Data.Regions.P(G.room.Region).S("name");
            c.FillText(string.Join(" ", region.ToCharArray()) + (G.room.BellRoom ? "   ·   B e l l   R o o m" : ""), cx, 116);
            c.TextAlign = 0;
        }
        Narr.DrawEpigraph(c, dt, 132);
        Narr.DrawHUD(c, dt);
    }

    static void BossBar(Canvas c)
    {
        var b = G.bossBar;
        if (b == null || b.gone || b.dead) return;
        string name = b.type switch { "abbot" => "The Abbot of Salt", "undertow" => "The Undertow", "shoal" => "The Shoal", "grafted" => "Hesper Vane, the Cantor", "shucker" => "Teodor", _ => "" };
        int total = b.knots.Count, left = b.knots.Count(k => !k.broken);
        // across the top of the screen, clear of the weapon plate and the prompts
        float w = 300, x = (H.VIEW_W - 300) / 2f, y = 30;
        c.Font = "10px caps"; c.TextAlign = 1;
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillText(name, H.VIEW_W / 2 + 0.7f, y - 7.3f);
        c.Fill = TEXT; c.FillText(name, H.VIEW_W / 2, y - 8);
        c.TextAlign = 0;
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillRect(x - 3, y - 2, w + 6, 6);
        c.Fill = GOLD_DIM; c.FillRect(x - 3, y - 2, w + 6, 0.7f); c.FillRect(x - 3, y + 3.3f, w + 6, 0.7f);
        for (int i = 0; i < total; i++)
        {
            float kx = x + (i + 0.5f) * (w / total);
            bool on = i < left;
            c.Save(); c.Translate(kx, y + 1); c.Rotate(H.PI / 4);
            c.Fill = on ? H.Rgba(200, 240, 255, 0.95f) : H.Rgba(55, 65, 72, 0.9f); c.FillRect(-2.4f, -2.4f, 4.8f, 4.8f);
            c.Restore();
            if (on) c.Glow(kx, y + 1, 6, new Color(0.7f, 1.1f, 1.4f, 0.2f));
        }
    }

    static void Grabbed(Canvas c, Player p)
    {
        if (p.grabbedBy == null) return;
        float sh = MathF.Sin(G.Now * 40) * 0.8f;
        c.Font = "14px caps"; c.TextAlign = 1; c.FillStyle = "#ff9a7a";
        Glyphs.DrawText(c, "Break free  {mash}  ×" + p.mash, H.VIEW_W / 2 + sh, H.VIEW_H / 2 + 50);
        c.TextAlign = 0;
    }

    static void Crosshair(Canvas c)
    {
        float mx = Inp.mouse.x, my = Inp.mouse.y;
        if (Inp.padAim)
        {
            var p = G.player;
            mx = p.Cx() + MathF.Cos(p.aim) * 60 - G.cam.X; my = p.y + 9 + MathF.Sin(p.aim) * 60 - G.cam.Y;
        }
        c.Stroke = new Color(0, 0, 0, 0.5f); c.LineWidth = 2;
        c.BeginPath(); c.MoveTo(mx - 5, my); c.LineTo(mx - 2, my); c.MoveTo(mx + 2, my); c.LineTo(mx + 5, my);
        c.MoveTo(mx, my - 5); c.LineTo(mx, my - 2); c.MoveTo(mx, my + 2); c.LineTo(mx, my + 5); c.StrokePath();
        c.Stroke = H.Rgba(245, 232, 200, 0.85f); c.LineWidth = 0.9f;
        c.BeginPath(); c.MoveTo(mx - 5, my); c.LineTo(mx - 2, my); c.MoveTo(mx + 2, my); c.LineTo(mx + 5, my);
        c.MoveTo(mx, my - 5); c.LineTo(mx, my - 2); c.MoveTo(mx, my + 2); c.LineTo(mx, my + 5); c.StrokePath();
        c.Fill = H.Rgba(255, 220, 150, 0.9f); c.FillRect(mx - 0.5f, my - 0.5f, 1, 1);
    }

    public static void Draw(Canvas c)
    {
        var p = G.player;
        c.LineWidth = 1; c.GlobalAlpha = 1; c.TextAlign = 0;
        // a soft shade behind the vitals so they read over bright rooms
        c.Glow(48, 26, 70, 34, new Color(0, 0, 0, 0.35f));
        Pips(c, p); Lantern(c, p); Breath(c, p);
        if (G.dialT > 0.01f) { c.GlobalAlpha = G.dialT; Dial(c); c.GlobalAlpha = 1; }
        Weapon(c, p); PromptBox(c); Toasts(c); RoomTitle(c); BossBar(c); Grabbed(c, p);
        Hints.Draw(c);
        if (Inp.sneakLatch && Pad.Using) { c.Font = "7px caps"; c.FillStyle = "#a89c80"; c.FillText("creeping", 12, 62); }
        if (G.god) { c.FillStyle = "#f66"; c.Font = "7px sans"; c.FillText("DEBUG", 6, H.VIEW_H - 4); }
        Crosshair(c);
    }

    // ---- logbook map --------------------------------------------------------------------
    public static void DrawMap(Canvas c, float W, float Hh)
    {
        c.FillStyle = "#1a1712"; c.FillRect(0, 0, W, Hh);
        c.FillStyle = "#2a251c"; c.FillRect(8, 8, W - 16, Hh - 16);
        // the island's extent, from the rooms themselves
        int cols = Data.Rooms.Max(r => r.Col + r.W), rows = Data.Rooms.Max(r => r.Row + r.H);
        float sw = MathF.Floor((W - 40) / cols), sh = MathF.Floor((Hh - 60) / rows);
        float ox = 20, oy = 36;
        c.Font = "11px Georgia, serif"; c.FillStyle = "#d8c8a0";
        c.FillText("Maren's logbook — the island", 20, 24);
        foreach (var r in Data.Rooms)
        {
            var st = World.StateOrNull(r.Id);
            if (st == null || !st.visited) continue;
            float x = ox + r.Col * sw, y = oy + r.Row * sh, w = r.W * sw - 3, h = r.H * sh - 3;
            var col = H.Col(Data.Regions.P(r.Region).S("color")); col.A = 0.55f;
            c.Fill = col; c.FillRect(x, y, w, h);
            c.StrokeStyle = r == G.room ? "#ffe0a0" : "#5a5040"; c.LineWidth = r == G.room ? 2 : 1; c.StrokeRect(x, y, w, h);
            // water lines only for tide phases Maren has actually seen here
            float LineFor(float sea) => y + ((sea - r.Oy) / r.Th) * h;
            if (r.Row == 4 && r.FixedWater == null)
            {
                if (st.seen.Contains("low")) { c.StrokeStyle = "#7ab0c0"; c.LineWidth = 1; c.Line(x, LineFor(Tide.SEA_LOW), x + w, LineFor(Tide.SEA_LOW)); }
                if (st.seen.Contains("high")) { c.StrokeStyle = "#3a70b0"; c.LineWidth = 1; c.Line(x, LineFor(Tide.SEA_HIGH), x + w, LineFor(Tide.SEA_HIGH)); }
                if (st.seen.Contains("ebbing")) { c.FillStyle = "#9ac0a0"; c.Font = "8px monospace"; c.FillText("E", x + w - 9, y + 10); }
                bool sounded = st.seen.Contains("low") && st.seen.Contains("high") && st.seen.Contains("ebbing");
                if (sounded) { c.FillStyle = "#d8c8a0"; c.Font = "8px monospace"; c.FillText("✓", x + 3, y + 10); }
            }
            if (r.BellRoom) { c.FillStyle = G.bellsLit.Contains(G.BellIdFor(r.Id) ?? "") ? "#ffd27a" : "#6a5a3a"; c.BeginPath(); c.Arc(x + w / 2, y + h / 2, 4, 0, H.PI * 2); c.FillPath(); }
            if (G.undertow != null && G.undertow.ripples.Contains(r.Id))
            {
                c.Stroke = H.Rgba(160, 200, 230, 0.6f); c.LineWidth = 1;
                for (int i = 1; i <= 2; i++) { c.BeginPath(); c.Arc(x + w * 0.3f, y + h * 0.75f, i * 4, 0, H.PI * 2); c.StrokePath(); }
            }
            if (r.Ents.Values.Any(e => e.S("type") == "wrackpool")) { c.FillStyle = "#5a8aa0"; c.Font = "8px monospace"; c.FillText("W", x + 3, y + h - 4); }
        }
        // Salt Ear: every Brine body within two rooms
        if (G.player.grafts.Contains("ear"))
        {
            foreach (var r in Data.Rooms)
            {
                if (Math.Abs(r.Col - G.room.Col) + Math.Abs(r.Row - G.room.Row) > 2) continue;
                var st = World.StateOrNull(r.Id);
                int n = st == null ? 0 : st.ents.Values.Count(e => e != null && e.A("knots") != null && e.A("knots").Count > 0 && !e.B("gone") && e.S("mode") != "downed");
                float x = ox + r.Col * sw, y = oy + r.Row * sh;
                for (int i = 0; i < Math.Min(n, 8); i++) { c.FillStyle = "#ff8080"; c.FillRect(x + 6 + i * 5, y + sh / 2, 3, 3); }
            }
        }
        if (G.satchelDrop != null)
        {
            var r = Data.Room[G.satchelDrop.room];
            float x = ox + r.Col * sw + ((G.satchelDrop.x - r.Px) / r.Pw) * r.W * sw, y = oy + r.Row * sh + ((G.satchelDrop.y - r.Py) / r.Ph) * r.H * sh;
            c.FillStyle = "#e0a040"; c.FillRect(x - 3, y - 3, 6, 6);
        }
        // Maren
        var p = G.player; var rr = G.room;
        float px = ox + rr.Col * sw + ((p.Cx() - rr.Px) / rr.Pw) * rr.W * sw, py = oy + rr.Row * sh + ((p.Cy() - rr.Py) / rr.Ph) * rr.H * sh;
        c.FillStyle = "#fff0c0"; c.BeginPath(); c.Arc(px, py, 3, 0, H.PI * 2); c.FillPath();
        c.Font = "9px monospace"; c.FillStyle = "#9a9080";
        c.FillText("Water lines appear for tide phases you have witnessed. ✓ = fully sounded. W = Wrack Pool. Ripples = the Undertow passed.", 20, Hh - 14);
    }
}
