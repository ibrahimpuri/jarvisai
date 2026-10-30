// HUD drawn over the world, and the logbook map.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Hud
{
    public static bool CleanShots;   // showcase screenshots: no debug tag
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

    // the air vents in the room she is in, found once per room
    static RoomDef ventRoom;
    static readonly List<Vector2> vents = new();
    static Vector2? NearestVent(Player p)
    {
        var r = G.room;
        if (r != ventRoom)
        {
            ventRoom = r; vents.Clear();
            for (int ty = 0; ty < r.Th; ty++) for (int tx = 0; tx < r.Tw; tx++)
                    if (World.CharAt(r.Ox + tx, r.Oy + ty) == 'a') vents.Add(new Vector2(r.Px + tx * 16 + 8, r.Py + ty * 16 + 8));
        }
        if (vents.Count == 0) return null;
        return vents.OrderBy(v => v.DistanceSquaredTo(new Vector2(p.Cx(), p.Cy()))).First();
    }
    // Running low under water, the nearest vent is marked: a ring of bubbles on it, or an arrow at the edge toward it.
    static void AirPointer(Canvas c, Player p, float max)
    {
        if (!p.HeadUnder() || p.breath > max * 0.45f) return;
        var v = NearestVent(p);
        if (v == null) return;
        var sp = G.WorldToScreen(v.Value.X, v.Value.Y);
        float pulse = 0.5f + 0.5f * MathF.Sin(G.Now * 6);
        var col = H.Rgba(190, 228, 255, 0.55f + 0.4f * pulse);
        if (sp.X > 8 && sp.X < H.VIEW_W - 8 && sp.Y > 8 && sp.Y < H.VIEW_H - 8)
        {
            c.Stroke = col; c.LineWidth = 1;
            c.BeginPath(); c.Arc(sp.X, sp.Y, 7 + pulse * 3, 0, H.PI * 2); c.StrokePath();
            c.Glow(sp.X, sp.Y, 14, H.Rgba(150, 210, 255, 0.18f));
            return;
        }
        var me = G.WorldToScreen(p.Cx(), p.Cy());
        var d = (sp - me).Normalized();
        float ex = Math.Clamp(me.X + d.X * 1000, 18, H.VIEW_W - 18), ey = Math.Clamp(me.Y + d.Y * 1000, 70, H.VIEW_H - 70);
        // pull the point back along the line so it sits on the clamped edge
        float k = MathF.Min(MathF.Abs(d.X) > 0.001f ? (ex - me.X) / d.X : 1e9f, MathF.Abs(d.Y) > 0.001f ? (ey - me.Y) / d.Y : 1e9f);
        var at = me + d * k;
        float ang = MathF.Atan2(d.Y, d.X);
        c.Save(); c.Translate(at.X, at.Y); c.Rotate(ang);
        c.Fill = col; c.Poly(new[] { new Vector2(6, 0), new Vector2(-4, -4), new Vector2(-2, 0), new Vector2(-4, 4) });
        c.Restore();
        c.Stroke = col; c.LineWidth = 0.8f; c.BeginPath(); c.Arc(at.X - d.X * 9, at.Y - d.Y * 9, 2.4f, 0, H.PI * 2); c.StrokePath();
    }

    // Breath shows only underwater, as bubbles rising from the helmet.
    static void Breath(Canvas c, Player p)
    {
        float max = p.BreathMax();
        if (!float.IsFinite(max)) return;
        AirPointer(c, p, max);
        if (!p.HeadUnder() && p.breath >= max - 0.1f) return;
        int n = (int)MathF.Ceiling(p.breath / 3), total = (int)MathF.Ceiling(max / 3);
        float by = Hollow.owned ? 62 : 50;
        Icons.Draw(c, "helmet", 15, by, 11);
        for (int i = 0; i < total; i++)
        {
            float x = 26 + i * 7.5f, y = by + MathF.Sin(G.Now * 3 + i) * 0.6f;
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
        float pw = 150, ph = Hollow.held ? 52 : 44, x = H.VIEW_W - pw - 8, y = H.VIEW_H - ph - 8;
        Plate(c, x, y, pw, ph);
        if (Hollow.held) { Hollow.DrawPanel(c, x, y, pw, ph); Consumables(c, p, x, y + ph - 8); return; }
        // icon in a recessed well
        c.Fill = new Color(0, 0, 0, 0.45f); c.FillRect(x + 6, y + 6, 22, 22);
        c.Stroke = new Color(GOLD.R, GOLD.G, GOLD.B, 0.35f); c.LineWidth = 0.6f; c.StrokeRect(x + 6, y + 6, 22, 22);
        Icons.Draw(c, p.cur, x + 17, y + 17, 19);
        if (Player.Legendary(p.cur))
        {
            Legend.DrawPanel(c, p, x, y, pw);
            WeaponPips(c, p, x, pw, y);
            Consumables(c, p, x, y + 36);
            return;
        }
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
        WeaponPips(c, p, x, pw, y);
        Consumables(c, p, x, y + 36);
    }
    // the other weapons carried, a diamond each; the legendary ones in their own colour
    static void WeaponPips(Canvas c, Player p, float x, float pw, float y)
    {
        if (p.weaponOrder.Count <= 1) return;
        for (int i = 0; i < p.weaponOrder.Count; i++)
        {
            string id = p.weaponOrder[i];
            bool sel = id == p.cur;
            var on = id == "tine" ? H.Col("#9fe8d4") : id == "glass" ? H.Col("#ffe0a0") : GOLD;
            c.Save(); c.Translate(x + pw - 9 - (p.weaponOrder.Count - 1 - i) * 7, y + 10); c.Rotate(H.PI / 4);
            c.Fill = sel ? on : Player.Legendary(id) ? new Color(on.R * 0.4f, on.G * 0.4f, on.B * 0.4f, 0.9f) : new Color(0.35f, 0.3f, 0.22f, 0.9f); c.FillRect(-1.8f, -1.8f, 3.6f, 3.6f); c.Restore();
        }
    }
    static void Consumables(Canvas c, Player p, float x, float cy)
    {
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
        float y = Hollow.owned ? 80 : 66;
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
        string name = b.type switch { "abbot" => "The Abbot of Salt", "almoner" => "The Almoner", "precentor" => "The Precentor", "mother" => "The Hanging Mother", "undertow" => "The Undertow", "shoal" => "The Shoal", "grafted" => "Hesper Vane, the Cantor", "shucker" => "Teodor", _ => "" };
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
            var ms = G.WorldToScreen(p.Cx() + MathF.Cos(p.aim) * 60, p.y + 9 + MathF.Sin(p.aim) * 60); mx = ms.X; my = ms.Y;
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
        Pips(c, p); Lantern(c, p); Hollow.DrawHud(c, 49); Breath(c, p);
        if (G.dialT > 0.01f) { c.GlobalAlpha = G.dialT; Dial(c); c.GlobalAlpha = 1; }
        Weapon(c, p); PromptBox(c); Toasts(c); RoomTitle(c); BossBar(c); Grabbed(c, p);
        Guide.DrawHud(c);
        Hints.Draw(c);
        if (Inp.sneakLatch && Pad.Using) { c.Font = "7px caps"; c.FillStyle = "#a89c80"; c.FillText("creeping", 12, 62); }
        if (G.god && !CleanShots) { c.FillStyle = "#f66"; c.Font = "7px sans"; c.FillText("DEBUG", 6, H.VIEW_H - 4); }
        Crosshair(c);
    }

    // ---- logbook map --------------------------------------------------------------------
    public static void DrawMap(Canvas c, float W, float Hh) => Chart.Draw(c, W, Hh);
}
