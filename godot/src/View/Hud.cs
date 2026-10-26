// HUD drawn over the world, and the logbook map.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Hud
{
    static readonly Dictionary<string, string> PH_COL = new() { ["low"] = "#38464e", ["rising"] = "#3f7a86", ["high"] = "#24456e", ["ebbing"] = "#5a7462" };

    static void Pips(Canvas c, Player p)
    {
        for (int i = 0; i < p.maxHp; i++)
        {
            float x = 14 + i * 13, y = 14;
            c.Save(); c.Translate(x, y); c.Rotate(H.PI / 4);
            c.Fill = i < p.hp ? H.Col("#d8c08a") : H.Rgba(80, 70, 60, 0.6f);
            c.FillRect(-4, -4, 8, 8);
            if (i < p.hp) { c.Fill = H.Rgba(255, 245, 210, 0.45f); c.FillRect(-4, -4, 8, 2); }
            c.Stroke = new Color(0, 0, 0, 0.6f); c.LineWidth = 1; c.StrokeRect(-4, -4, 8, 8);
            c.Restore();
        }
    }

    static void Breath(Canvas c, Player p)
    {
        float max = p.BreathMax();
        if (!float.IsFinite(max)) return;
        if (!p.HeadUnder() && p.breath >= max - 0.1f) return;
        // Shown as bubbles rising from the helmet rather than a bar.
        int n = (int)MathF.Ceiling(p.breath / 3);
        int total = (int)MathF.Ceiling(max / 3);
        for (int i = 0; i < total; i++)
        {
            c.Fill = i < n ? H.Rgba(180, 220, 255, 0.85f) : H.Rgba(80, 100, 120, 0.35f);
            c.BeginPath(); c.Arc(12 + i * 8, 31, 3, 0, H.PI * 2); c.FillPath();
            if (i < n) { c.Fill = H.Rgba(255, 255, 255, 0.7f); c.FillRect(11 + i * 8, 29.5f, 1, 1); }
        }
    }

    static void Lantern(Canvas c, Player p)
    {
        float y = p.breath < p.BreathMax() - 0.1f || p.HeadUnder() ? 42 : 30;
        c.FillStyle = p.shut ? "#555" : "#ffd27a";
        c.FillRect(10, y, 5, 7);
        c.Fill = H.Rgba(60, 50, 40, 0.7f); c.FillRect(18, y + 2, 50, 3);
        c.FillStyle = p.focusing ? "#fff0c0" : "#c9a040";
        c.FillRect(18, y + 2, 50 * (p.oil / p.OilMax()), 3);
        c.FillStyle = "#aaa"; c.Font = "7px monospace";
        c.FillText("x" + p.bag.Count("oil"), 72, y + 6);
        if (p.shut) { c.FillStyle = "#888"; c.FillText("SHUT", 88, y + 6); }
    }

    static void Dial(Canvas c)
    {
        float cx = H.VIEW_W - 30, cy = 30, r = 20;
        float total = Tide.Total();
        float a0 = -H.PI / 2;
        for (int i = 0; i < 4; i++)
        {
            float a1 = a0 + (Tide.Len(i) / total) * H.PI * 2;
            c.FillStyle = PH_COL[Tide.PHASES[i].Id];
            c.BeginPath(); c.MoveTo(cx, cy); c.Arc(cx, cy, r, a0, a1); c.ClosePath(); c.FillPath();
            a0 = a1;
        }
        c.Fill = H.Rgba(255, 255, 255, 0.06f); c.BeginPath(); c.Arc(cx - 3, cy - 4, r - 6, 0, H.PI * 2); c.FillPath();
        c.StrokeStyle = "#c0a060"; c.LineWidth = 1.5f; c.BeginPath(); c.Arc(cx, cy, r, 0, H.PI * 2); c.StrokePath();
        float a = -H.PI / 2 + (Tide.t / total) * H.PI * 2;
        c.StrokeStyle = "#f0e0b0"; c.LineWidth = 2;
        c.BeginPath(); c.MoveTo(cx, cy); c.LineTo(cx + MathF.Cos(a) * (r - 2), cy + MathF.Sin(a) * (r - 2)); c.StrokePath();
        c.FillStyle = "#c0a060"; c.BeginPath(); c.Arc(cx, cy, 2.5f, 0, H.PI * 2); c.FillPath();
        c.Font = "8px monospace"; c.TextAlign = 2;
        c.FillStyle = "#e0d4b0";
        c.FillText(Tide.PhaseName().ToUpperInvariant(), cx - r - 6, cy - 2);
        c.FillStyle = "#9a9080";
        c.FillText(H.FmtTime(Tide.PhaseRemaining()), cx - r - 6, cy + 8);
        if (Tide.spring != 0) { c.FillStyle = "#a06060"; c.FillText("SPRING TIDES", cx - r - 6, cy + 18); }
        c.TextAlign = 0;
    }

    static void Weapon(Canvas c, Player p)
    {
        var w = p.weapons[p.cur]; var def = Data.Weapons.P(p.cur);
        float x = H.VIEW_W - 130, y = H.VIEW_H - 30;
        c.Fill = H.Rgba(10, 12, 14, 0.55f); c.FillRect(x - 6, y - 12, 132, 38);
        c.Fill = H.Rgba(200, 170, 100, 0.35f); c.FillRect(x - 6, y - 12, 132, 1);
        c.Font = "8px monospace"; c.FillStyle = "#d8c8a0";
        c.FillText(def.S("name") + (w.tier > 0 ? " " + new string('I', w.tier + 1) : ""), x, y - 2);
        c.Font = "11px monospace"; c.FillStyle = "#f0e0c0";
        c.FillText(w.mag + " / " + p.bag.Count(def.S("ammo")), x, y + 11);
        c.Font = "7px monospace"; c.FillStyle = "#888";
        c.FillText(string.Concat(p.weaponOrder.Select((id, i) => id == p.cur ? "[" + (i + 1) + "]" : " " + (i + 1) + " ")), x + 64, y + 11);
        c.FillStyle = "#7fbf7f"; c.FillText("Poultice " + p.bag.Count("poultice") + "  Stitch " + p.bag.Count("stitchkit") + "  Scrap " + p.bag.Count("scrap"), x, y + 22);
    }

    static void PromptBox(Canvas c)
    {
        string text = G.promptText;
        if (text == null) return;
        c.Font = "8px monospace";
        float w = Glyphs.Measure(c, text) + 14;
        float x = (H.VIEW_W - w) / 2, y = H.VIEW_H - 48;
        c.Fill = H.Rgba(10, 12, 14, 0.7f); c.FillRect(x, y, w, 17);
        c.Fill = H.Rgba(200, 170, 100, 0.4f); c.FillRect(x, y, w, 1);
        c.FillStyle = "#e8dcb8"; Glyphs.DrawText(c, text, x + 7, y + 11.5f);
    }

    // Notifications: a wrapped column under the HUD, leaving the centre for titles and story text.
    static void Toasts(Canvas c)
    {
        c.Font = "7px monospace"; c.TextAlign = 0;
        const float maxW = 236;
        float y = 62;
        foreach (var t in H.Toasts)
        {
            float a = MathF.Min(1, MathF.Min(t.T, (t.Max - t.T) * 4 + 0.2f));
            var lines = Glyphs.Wrap(c, t.Text, maxW);
            bool rich = t.Text.Contains('{');
            float lh = rich ? 12 : 9, h = lines.Count * lh + 5;
            c.Fill = H.Rgba(8, 10, 12, 0.6f * a); c.FillRect(8, y - 8, maxW + 10, h);
            c.Fill = H.Rgba(200, 170, 100, 0.7f * a); c.FillRect(8, y - 8, 1.5f, h);
            c.Fill = H.Rgba(232, 220, 184, a);
            for (int i = 0; i < lines.Count; i++) Glyphs.DrawText(c, lines[i], 13, y + i * lh + (rich ? 1.5f : 0));
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
            c.Font = "15px Georgia, serif";
            c.Fill = new Color(0, 0, 0, 0.5f * a); c.FillText(G.room.Name, cx + 1, 97);
            c.Fill = H.Rgba(236, 224, 190, a); c.FillText(G.room.Name, cx, 96);
            float w = c.MeasureText(G.room.Name) / 2 + 18;
            c.Fill = H.Rgba(200, 170, 100, 0.5f * a);
            c.FillRect(cx - w - 40, 92, 40, 1); c.FillRect(cx + w, 92, 40, 1);
            c.Font = "7px monospace"; c.Fill = H.Rgba(170, 158, 135, a);
            string region = Data.Regions.P(G.room.Region).S("name").ToUpperInvariant();
            c.FillText(string.Join(" ", region.ToCharArray()) + (G.room.BellRoom ? "   ·   B E L L   R O O M" : ""), cx, 110);
            c.TextAlign = 0;
        }
        Narr.DrawEpigraph(c, dt, 126);
        Narr.DrawHUD(c, dt);
    }

    static void BossBar(Canvas c)
    {
        var b = G.bossBar;
        if (b == null || b.gone || b.dead) return;
        string name = b.type switch { "abbot" => "THE ABBOT OF SALT", "undertow" => "THE UNDERTOW", "shoal" => "THE SHOAL", "grafted" => "HESPER VANE, THE CANTOR", "shucker" => "TEODOR", _ => "" };
        int total = b.knots.Count, left = b.knots.Count(k => !k.broken);
        float x = 120, y = H.VIEW_H - 16, w = H.VIEW_W - 240;
        c.Font = "8px monospace"; c.TextAlign = 1; c.FillStyle = "#d8c8a0";
        c.FillText(name, H.VIEW_W / 2, y - 4);
        c.TextAlign = 0;
        for (int i = 0; i < total; i++)
        {
            c.Fill = i < left ? H.Rgba(200, 240, 255, 0.85f) : H.Rgba(60, 70, 80, 0.6f);
            c.FillRect(x + i * (w / total) + 1, y, w / total - 2, 4);
        }
    }

    static void Grabbed(Canvas c, Player p)
    {
        if (p.grabbedBy == null) return;
        c.Font = "12px monospace"; c.TextAlign = 1; c.FillStyle = "#ff9a7a";
        Glyphs.DrawText(c, "BREAK FREE  {mash}  x" + p.mash, H.VIEW_W / 2, H.VIEW_H / 2 + 50);
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
        c.Stroke = H.Rgba(240, 230, 200, 0.7f); c.LineWidth = 1;
        c.BeginPath(); c.MoveTo(mx - 4, my); c.LineTo(mx - 1.5f, my); c.MoveTo(mx + 1.5f, my); c.LineTo(mx + 4, my);
        c.MoveTo(mx, my - 4); c.LineTo(mx, my - 1.5f); c.MoveTo(mx, my + 1.5f); c.LineTo(mx, my + 4); c.StrokePath();
    }

    public static void Draw(Canvas c)
    {
        var p = G.player;
        c.LineWidth = 1; c.GlobalAlpha = 1; c.TextAlign = 0;
        Pips(c, p); Breath(c, p); Lantern(c, p);
        if (G.dialT > 0.01f) { c.GlobalAlpha = G.dialT; Dial(c); c.GlobalAlpha = 1; }
        Weapon(c, p); PromptBox(c); Toasts(c); RoomTitle(c); BossBar(c); Grabbed(c, p);
        Hints.Draw(c);
        if (Inp.sneakLatch && Pad.Using) { c.Font = "6px monospace"; c.FillStyle = "#9a9080"; c.FillText("creeping", 10, 56); }
        if (G.god) { c.FillStyle = "#f66"; c.Font = "7px monospace"; c.FillText("DEBUG", 6, H.VIEW_H - 4); }
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
