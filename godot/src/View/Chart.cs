// Maren's chart of the island: the logbook map, drawn as a Keeper's sea chart on old paper.
//
// Each room she has been in is drawn as its own shape, the open stone washed in its region's colour and the walls
// inked round it, so the chart is the island's real plan, not a grid of boxes. Rooms she has only seen the door of
// are dashed outlines with a question mark. Over that: the Bell Rooms (lit or dark), benches, the people she has met,
// Wrack Pools, the tide lines she has witnessed, her lost satchel, the Undertow's wake, and the current objective,
// a red-ink cross with the way there dotted in from where she stands. Region names, a compass rose and a legend.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Chart
{
    static readonly Color PAPER = H.Col("#c9b48a"), PAPER_LIGHT = H.Col("#dccaa2"), STAIN = H.Col("#8a6a42"),
        INK = H.Col("#2e2014"), INK_FADED = H.Col("#6a5236"), RED = H.Col("#9a2a1e"), SEA = H.Col("#3e5a64"), GOLD = H.Col("#c8962e");

    // the things marked on the chart, per room, from where the rooms place them (built once)
    record Mark(string kind, string id, float x, float y);
    static Dictionary<string, List<Mark>> marks;
    static void BuildMarks()
    {
        marks = new();
        foreach (var r in Data.Rooms)
        {
            var l = marks[r.Id] = new();
            foreach (var sp in G.RoomSpecs(r))
            {
                string t = sp.S("type");
                if (t is "bell" or "bench" or "npc" or "wrackpool" or "socket" or "crank" or "lift" or "sluice" or "font" or "lampwork")
                    l.Add(new Mark(t, sp.S("id"), sp.F("x"), sp.F("y") - 8));
                // a box she has not opened yet, so a room she has been through tells her what it still holds
                else if (t == "chest" && !sp.B("needsDrained")) l.Add(new Mark(t, sp.S("key"), sp.F("x"), sp.F("y") - 4));
            }
        }
    }

    static float ox, oy, k;
    static Vector2 M(float wx, float wy) => new(ox + wx / H.TILE * k, oy + wy / H.TILE * k);
    static float Hash(int a, int b) => TileArt.Hash(a, b);

    // How close the chart is drawn: 0 frames everything she knows of; each step in halves the view around her.
    public static int Zoom;
    public static Vector2 Pan;   // tiles the closer views are moved from Maren
    public const int MAX_ZOOM = 3;

    public static void Draw(Canvas c, float W, float Hh)
    {
        if (marks == null) BuildMarks();
        var visited = new HashSet<string>(Data.Rooms.Where(r => World.StateOrNull(r.Id)?.visited == true).Select(r => r.Id));
        // rooms she knows of but has not entered: their doors are open onto them
        var known = new HashSet<string>();
        foreach (var id in visited) foreach (var n in Guide.Links(id).Keys) if (!visited.Contains(n)) known.Add(n);
        View(W, Hh, visited, known);
        Paper(c, W, Hh);
        Sea(c, W, Hh, visited);
        foreach (var id in known) Unknown(c, Data.Room[id]);
        // the rooms: ink first, all of them, then the washes, so walls are inked where stone meets open
        var ordered = Data.Rooms.Where(r => visited.Contains(r.Id)).ToList();
        foreach (var r in ordered) Runs(c, r, true);
        foreach (var r in ordered) Runs(c, r, false);
        if (G.room != null && visited.Contains(G.room.Id))
        {
            var r = G.room; var a = M(r.Px, r.Py); var b = M(r.Px + r.Pw, r.Py + r.Ph);
            c.Stroke = new Color(GOLD.R, GOLD.G, GOLD.B, 0.55f + 0.25f * MathF.Sin(G.Now * 2.4f)); c.LineWidth = 0.8f; c.StrokeRect(a.X - 1, a.Y - 1, b.X - a.X + 2, b.Y - a.Y + 2);
        }
        foreach (var r in ordered) Tides(c, r);
        foreach (var r in ordered) Marks(c, r);
        RegionNames(c, ordered);
        Undertow(c, ordered);
        SaltEar(c);
        Satchel(c);
        Route(c);
        Maren(c);
        Title(c, W);
        Objective(c, W);
        Compass(c, W - 26, Hh - 24);
        Legend(c, Hh);
    }

    // The part of the island on the paper, in tiles. The island is far bigger than one sheet, so the chart frames what
    // she has found (and grows as she finds more), or, zoomed in, a closer view around her that can be panned.
    static void View(float W, float Hh, HashSet<string> visited, HashSet<string> known)
    {
        const float top = 30, bottom = 30, MIN_W = 9 * 40, MIN_H = 5 * 23, PAD = 20;
        var rooms = Data.Rooms.Where(r => visited.Contains(r.Id) || known.Contains(r.Id) || r == G.room).ToList();
        if (rooms.Count == 0) rooms = Data.Rooms;
        float x0 = rooms.Min(r => r.Ox) - PAD, y0 = rooms.Min(r => r.Oy) - PAD;
        float x1 = rooms.Max(r => r.Ox + r.Tw) + PAD, y1 = rooms.Max(r => r.Oy + r.Th) + PAD;
        float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, vw = MathF.Max(MIN_W, x1 - x0), vh = MathF.Max(MIN_H, y1 - y0);
        if (Zoom > 0 && G.player != null)
        {
            float f = MathF.Pow(2, Zoom);
            vw = MathF.Max(MIN_W * 0.75f, vw / f); vh = MathF.Max(MIN_H * 0.75f, vh / f);
            cx = G.player.Cx() / H.TILE + Pan.X; cy = G.player.Cy() / H.TILE + Pan.Y;
        }
        k = MathF.Min((W - 24) / vw, (Hh - top - bottom) / vh);
        ox = MathF.Floor(W / 2 - cx * k); oy = MathF.Floor(top + (Hh - top - bottom) / 2 - cy * k);
    }

    // ---- paper ------------------------------------------------------------------------------------------
    static void Paper(Canvas c, float W, float Hh)
    {
        c.FillRectGradV(0, 0, W, Hh, PAPER_LIGHT, PAPER);
        // water stains and foxing, always in the same places
        for (int i = 0; i < 26; i++)
        {
            float x = Hash(i, 3) * W, y = Hash(i, 7) * Hh, r = 14 + Hash(i, 11) * 46;
            c.Glow(x, y, r, r * (0.6f + Hash(i, 13) * 0.5f), new Color(STAIN.R, STAIN.G, STAIN.B, 0.06f + Hash(i, 17) * 0.08f));
        }
        for (int i = 0; i < 60; i++) { c.Fill = new Color(STAIN.R, STAIN.G, STAIN.B, 0.25f); c.FillRect(Hash(i, 19) * W, Hash(i, 23) * Hh, 0.8f, 0.8f); }
        // the edges darkened with handling
        c.Glow(0, 0, 90, 70, new Color(0.3f, 0.2f, 0.1f, 0.35f)); c.Glow(W, 0, 90, 70, new Color(0.3f, 0.2f, 0.1f, 0.35f));
        c.Glow(0, Hh, 90, 70, new Color(0.3f, 0.2f, 0.1f, 0.35f)); c.Glow(W, Hh, 90, 70, new Color(0.3f, 0.2f, 0.1f, 0.35f));
        c.Stroke = new Color(INK.R, INK.G, INK.B, 0.75f); c.LineWidth = 1; c.StrokeRect(4, 4, W - 8, Hh - 8);
        c.Stroke = new Color(INK.R, INK.G, INK.B, 0.4f); c.LineWidth = 0.5f; c.StrokeRect(7, 7, W - 14, Hh - 14);
    }

    // the sea: rows of little waves wherever no room is known
    static void Sea(Canvas c, float W, float Hh, HashSet<string> visited)
    {
        c.Stroke = new Color(SEA.R, SEA.G, SEA.B, 0.32f); c.LineWidth = 0.5f;
        for (float y = 16; y < Hh - 14; y += 9)
            for (float x = 14 + ((int)(y / 9) % 2) * 6; x < W - 14; x += 12)
            {
                // under a room she has seen, the paper is left for the room
                float wx = (x - ox) / k * H.TILE, wy = (y - oy) / k * H.TILE;
                var r = World.RoomAtTile(H.FloorI(wx / H.TILE), H.FloorI(wy / H.TILE));
                if (r != null && visited.Contains(r.Id)) continue;
                c.BeginPath(); c.MoveTo(x, y); c.QuadraticCurveTo(x + 2, y - 1.6f, x + 4, y); c.QuadraticCurveTo(x + 6, y + 1.6f, x + 8, y); c.StrokePath();
            }
    }

    static void Unknown(Canvas c, RoomDef r)
    {
        var a = M(r.Px, r.Py); var b = M(r.Px + r.Pw, r.Py + r.Ph);
        c.Fill = new Color(PAPER_LIGHT.R, PAPER_LIGHT.G, PAPER_LIGHT.B, 0.55f); c.FillRect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        c.Fill = new Color(INK_FADED.R, INK_FADED.G, INK_FADED.B, 0.6f);
        for (float x = a.X; x < b.X; x += 4) { c.FillRect(x, a.Y, 2, 0.6f); c.FillRect(x, b.Y - 0.6f, 2, 0.6f); }
        for (float y = a.Y; y < b.Y; y += 4) { c.FillRect(a.X, y, 0.6f, 2); c.FillRect(b.X - 0.6f, y, 0.6f, 2); }
        c.Font = "italic 9px Georgia, serif"; c.TextAlign = 1; c.Fill = new Color(INK_FADED.R, INK_FADED.G, INK_FADED.B, 0.7f);
        c.FillText("?", (a.X + b.X) / 2, (a.Y + b.Y) / 2 + 3); c.TextAlign = 0;
    }

    // the room's open stone, run by run: an inked pass a little larger, then the wash
    static void Runs(Canvas c, RoomDef r, bool ink)
    {
        var wash = PAPER_LIGHT.Lerp(H.Col(Data.Regions.P(r.Region).S("color")), 0.38f);
        if (r == G.room) wash = wash.Lerp(new Color(1, 0.95f, 0.8f), 0.25f);
        float o = ink ? 0.75f : 0;
        if (ink) c.Fill = new Color(INK.R, INK.G, INK.B, 0.9f); else c.Fill = wash;
        for (int ty = 0; ty < r.Th; ty++)
        {
            int run = -1;
            for (int tx = 0; tx <= r.Tw; tx++)
            {
                bool open = tx < r.Tw && World.Solidity(r.Live[ty * r.Tw + tx]) != 1;
                if (open && run < 0) run = tx;
                if (!open && run >= 0)
                {
                    float x = ox + (r.Ox + run) * k, y = oy + (r.Oy + ty) * k;
                    c.FillRect(x - o, y - o, (tx - run) * k + o * 2, k + o * 2);
                    run = -1;
                }
            }
        }
        if (ink) return;
        // a faint grain over the wash
        for (int i = 0; i < 14; i++)
        {
            float x = ox + (r.Ox + Hash(i, r.Col * 7 + r.Row) * r.Tw) * k, y = oy + (r.Oy + Hash(i + 40, r.Row * 5 + r.Col) * r.Th) * k;
            c.Fill = new Color(STAIN.R, STAIN.G, STAIN.B, 0.12f); c.FillRect(x, y, 2.5f, 0.6f);
        }
    }

    // the tide lines she has seen in the rooms the sea reaches
    static void Tides(Canvas c, RoomDef r)
    {
        if (!r.Tidal || r.FixedWater != null) return;
        var st = World.StateOrNull(r.Id); if (st == null) return;
        var a = M(r.Px, r.Py); var b = M(r.Px + r.Pw, r.Py + r.Ph);
        void Line(float sea, Color col)
        {
            float y = oy + sea * k;
            if (y < a.Y || y > b.Y) return;
            c.Stroke = col; c.LineWidth = 0.6f;
            for (float x = a.X + 1; x < b.X - 1; x += 5) c.Line(x, y + MathF.Sin(x * 0.7f) * 0.3f, x + 3, y + MathF.Sin(x * 0.7f + 2) * 0.3f);
        }
        if (st.seen.Contains("low")) Line(Tide.SEA_LOW, new Color(SEA.R, SEA.G, SEA.B, 0.6f));
        if (st.seen.Contains("high")) Line(Tide.SEA_HIGH, new Color(0.18f, 0.32f, 0.55f, 0.75f));
    }

    static void Marks(Canvas c, RoomDef r)
    {
        foreach (var m in marks[r.Id])
        {
            var p = M(m.x, m.y);
            switch (m.kind)
            {
                case "bell":
                {
                    bool lit = G.bellsLit.Contains(m.id);
                    if (lit) c.Glow(p.X, p.Y, 7, new Color(1.2f, 0.85f, 0.3f, 0.45f));
                    c.Fill = lit ? GOLD : new Color(INK.R, INK.G, INK.B, 0.85f);
                    c.Poly(new[] { new Vector2(p.X - 1.2f, p.Y - 3), new Vector2(p.X + 1.2f, p.Y - 3), new Vector2(p.X + 3, p.Y + 2), new Vector2(p.X - 3, p.Y + 2) });
                    c.FillRect(p.X - 0.6f, p.Y + 2, 1.2f, 1.2f);
                    break;
                }
                case "bench":
                    c.Fill = new Color(INK.R, INK.G, INK.B, 0.85f);
                    c.FillRect(p.X - 3, p.Y - 1, 6, 1.6f); c.FillRect(p.X - 1, p.Y + 0.6f, 2, 2); c.FillRect(p.X - 2.4f, p.Y + 2.4f, 4.8f, 1);
                    break;
                case "npc":
                {
                    if (!Story.NpcPresent(m.id, r.Id)) break;
                    c.Fill = new Color(RED.R, RED.G, RED.B, 0.85f); c.FillCircle(p.X, p.Y - 2, 1.4f); c.FillRect(p.X - 1.2f, p.Y - 0.6f, 2.4f, 3);
                    c.Font = "5px caps"; c.TextAlign = 1; c.Fill = new Color(INK.R, INK.G, INK.B, 0.85f); c.FillText(Story.NpcName(m.id).Split(' ')[^1], p.X, p.Y - 5); c.TextAlign = 0;
                    break;
                }
                case "chest":
                {
                    if (World.StateOrNull(r.Id)?.ents.TryGetValue(m.id, out var st) == true && st != null && st.B("done")) break;
                    if (r == G.room && G.ents.OfType<Chest>().Any(e => e.key == m.id && (e.done || e.gone))) break;   // opened just now
                    c.Fill = new Color(STAIN.R, STAIN.G, STAIN.B, 0.95f); c.FillRect(p.X - 2, p.Y - 1.4f, 4, 2.8f);
                    c.Fill = new Color(INK.R, INK.G, INK.B, 0.9f); c.FillRect(p.X - 2, p.Y - 0.2f, 4, 0.5f); c.FillRect(p.X - 0.4f, p.Y - 0.6f, 0.8f, 1);
                    break;
                }
                case "wrackpool":
                    c.Stroke = new Color(SEA.R, SEA.G, SEA.B, 0.9f); c.LineWidth = 0.6f;
                    c.BeginPath(); c.Arc(p.X, p.Y, 2.4f, 0, H.PI * 1.6f); c.StrokePath(); c.BeginPath(); c.Arc(p.X, p.Y, 1.1f, 1, H.PI * 2.2f); c.StrokePath();
                    break;
            }
        }
    }

    static void RegionNames(Canvas c, List<RoomDef> rooms)
    {
        c.Font = "italic 7px Georgia, serif"; c.TextAlign = 1;
        foreach (var g in rooms.GroupBy(r => r.Region))
        {
            float x = g.Average(r => r.Ox + r.Tw / 2f), y = g.Min(r => r.Oy) + 3;
            var p = new Vector2(ox + x * k, oy + y * k);
            string name = Data.Regions.P(g.Key).S("name");
            c.Fill = new Color(PAPER_LIGHT.R, PAPER_LIGHT.G, PAPER_LIGHT.B, 0.7f); c.FillText(name, p.X + 0.5f, p.Y + 0.5f);
            c.Fill = new Color(INK.R, INK.G, INK.B, 0.75f); c.FillText(name, p.X, p.Y);
        }
        c.TextAlign = 0;
    }

    static void Undertow(Canvas c, List<RoomDef> rooms)
    {
        if (G.undertow == null) return;
        foreach (var r in rooms)
        {
            if (!G.undertow.ripples.Contains(r.Id)) continue;
            var p = M(r.Px + r.Pw * 0.3f, r.Py + r.Ph * 0.75f);
            c.Stroke = new Color(SEA.R, SEA.G, SEA.B, 0.7f); c.LineWidth = 0.6f;
            for (int i = 1; i <= 3; i++) { c.BeginPath(); c.Arc(p.X, p.Y, i * 2.6f, 0, H.PI * 2); c.StrokePath(); }
        }
    }

    // the Salt Ear graft: every Brine body within two rooms, as a red pinprick
    static void SaltEar(Canvas c)
    {
        if (G.player == null || !G.player.grafts.Contains("ear") || G.room == null) return;
        foreach (var r in Data.Rooms)
        {
            if (Math.Abs(r.Col - G.room.Col) + Math.Abs(r.Row - G.room.Row) > 2) continue;
            var st = World.StateOrNull(r.Id);
            int n = st == null ? 0 : st.ents.Values.Count(e => e != null && e.A("knots") != null && e.A("knots").Count > 0 && !e.B("gone") && e.S("mode") != "downed");
            var p = M(r.Px + r.Pw / 2, r.Py + r.Ph / 2);
            for (int i = 0; i < Math.Min(n, 8); i++) { c.Fill = RED; c.FillCircle(p.X - 8 + i * 2.6f, p.Y + 4, 0.9f); }
        }
    }

    static void Satchel(Canvas c)
    {
        var sd = G.satchelDrop; if (sd == null) return;
        var p = M(sd.x, sd.y);
        c.Glow(p.X, p.Y, 6, new Color(1.2f, 0.6f, 0.2f, 0.45f));
        c.Fill = H.Col("#a0601e"); c.FillRect(p.X - 2.4f, p.Y - 1.6f, 4.8f, 3.6f);
        c.Fill = H.Col("#e0a040"); c.FillRect(p.X - 2.4f, p.Y - 1.6f, 4.8f, 1);
    }

    // the way to the objective, dotted in red ink, from where she stands through each door in turn
    static void Route(Canvas c)
    {
        var s = Guide.Current; var pl = G.player;
        if (s == null || pl == null || G.room == null) return;
        var pts = new List<Vector2> { M(pl.Cx(), pl.Cy()) };
        var route = Guide.Route(G.room.Id, s.room);
        if (route != null)
            for (int i = 0; i + 1 < route.Count; i++)
            {
                var seam = Guide.Links(route[i])[route[i + 1]];
                float sx = (float)seam.Average(t => t.tx) * H.TILE + 8, sy = (float)seam.Average(t => t.ty) * H.TILE + 8;
                pts.Add(M(sx, sy));
            }
        var tr = Data.Room[s.room];
        var tgt = Guide.ThingPos(s) ?? new Vector2(tr.Px + tr.Pw / 2, tr.Py + tr.Ph / 2);
        var end = M(tgt.X, tgt.Y);
        pts.Add(end);
        // marching dots
        float phase = G.Now * 6 % 4;
        c.Fill = new Color(RED.R, RED.G, RED.B, 0.85f);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[i + 1]; float len = (b - a).Length();
            for (float d = phase; d < len; d += 4) { var q = a.Lerp(b, d / len); c.FillCircle(q.X, q.Y, 0.65f); }
        }
        // the cross, in a ring that breathes
        float pulse = 0.5f + 0.5f * MathF.Sin(G.Now * 3);
        c.Stroke = new Color(RED.R, RED.G, RED.B, 0.5f + 0.4f * pulse); c.LineWidth = 0.7f;
        c.BeginPath(); c.Arc(end.X, end.Y, 4.5f + pulse * 1.5f, 0, H.PI * 2); c.StrokePath();
        c.Stroke = RED; c.LineWidth = 1.2f; c.Line(end.X - 2.6f, end.Y - 2.6f, end.X + 2.6f, end.Y + 2.6f); c.Line(end.X - 2.6f, end.Y + 2.6f, end.X + 2.6f, end.Y - 2.6f);
        // and the room it is in, named
        c.Font = "6px caps"; c.TextAlign = 1;
        c.Fill = new Color(RED.R, RED.G, RED.B, 0.95f); c.FillText(tr.Name, end.X, end.Y - 8);
        c.TextAlign = 0;
    }

    static void Maren(Canvas c)
    {
        var p = G.player; var r = G.room;
        if (p == null || r == null) return;
        var m = M(p.Cx(), p.Cy());
        c.Glow(m.X, m.Y, 9, new Color(1.3f, 0.95f, 0.45f, 0.55f));
        c.Fill = INK; c.FillCircle(m.X, m.Y, 2.4f);
        c.Fill = H.Col("#ffe6a0"); c.FillCircle(m.X, m.Y, 1.6f);
        c.Fill = INK; c.Poly(new[] { new Vector2(m.X + p.facing * 4.4f, m.Y), new Vector2(m.X + p.facing * 2.6f, m.Y - 1.4f), new Vector2(m.X + p.facing * 2.6f, m.Y + 1.4f) });
        c.Font = "6px caps"; c.TextAlign = 1; c.Fill = new Color(INK.R, INK.G, INK.B, 0.9f); c.FillText(r.Name, m.X, m.Y + 10); c.TextAlign = 0;
    }

    static void Title(Canvas c, float W)
    {
        c.Font = "12px Georgia, serif"; c.Fill = new Color(INK.R, INK.G, INK.B, 0.9f); c.FillText("Hollowmere", 14, 20);
        c.Font = "italic 6px Georgia, serif"; c.Fill = new Color(INK_FADED.R, INK_FADED.G, INK_FADED.B, 0.9f); c.FillText("the Keeper's chart, in Maren's hand", 14, 27);
    }

    static void Objective(Canvas c, float W)
    {
        var s = Guide.Current;
        c.TextAlign = 2;
        if (s == null) { c.Font = "7px caps"; c.Fill = INK; c.FillText("the night is over", W - 14, 18); c.TextAlign = 0; return; }
        c.Font = "7px caps"; c.Fill = RED; c.FillText(s.title, W - 14, 17);
        c.Font = "italic 6px Georgia, serif"; c.Fill = new Color(INK.R, INK.G, INK.B, 0.85f); c.FillText(Guide.WhereLine(s), W - 14, 25);
        c.TextAlign = 0;
    }

    static void Compass(Canvas c, float x, float y)
    {
        c.Stroke = new Color(INK.R, INK.G, INK.B, 0.6f); c.LineWidth = 0.5f;
        c.BeginPath(); c.Arc(x, y, 10, 0, H.PI * 2); c.StrokePath();
        c.BeginPath(); c.Arc(x, y, 7, 0, H.PI * 2); c.StrokePath();
        for (int i = 0; i < 8; i++)
        {
            float a = i * H.PI / 4 - H.PI / 2, l = i % 2 == 0 ? 12 : 7;
            var tip = new Vector2(x + MathF.Cos(a) * l, y + MathF.Sin(a) * l);
            var s1 = new Vector2(x + MathF.Cos(a + 0.5f) * 2, y + MathF.Sin(a + 0.5f) * 2); var s2 = new Vector2(x + MathF.Cos(a - 0.5f) * 2, y + MathF.Sin(a - 0.5f) * 2);
            c.Fill = i == 0 ? RED : new Color(INK.R, INK.G, INK.B, i % 2 == 0 ? 0.8f : 0.5f);
            c.Poly(new[] { tip, s1, new Vector2(x, y), s2 });
        }
        c.Font = "6px caps"; c.TextAlign = 1; c.Fill = INK; c.FillText("N", x, y - 13); c.TextAlign = 0;
    }

    static void Legend(Canvas c, float Hh)
    {
        float x = 14, y = Hh - 13;
        c.Font = "5px caps";
        void Item(Action<float> icon, string label)
        {
            icon(x + 3);
            c.Fill = new Color(INK.R, INK.G, INK.B, 0.85f); c.FillText(label, x + 9, y + 2);
            x += 12 + c.MeasureText(label) + 8;
        }
        Item(ix => { c.Stroke = RED; c.LineWidth = 1; c.Line(ix - 2, y - 2, ix + 2, y + 2); c.Line(ix - 2, y + 2, ix + 2, y - 2); }, "objective");
        Item(ix => { c.Fill = GOLD; c.Poly(new[] { new Vector2(ix - 1, y - 2.4f), new Vector2(ix + 1, y - 2.4f), new Vector2(ix + 2.4f, y + 1.6f), new Vector2(ix - 2.4f, y + 1.6f) }); }, "bell room");
        Item(ix => { c.Fill = INK; c.FillRect(ix - 2.6f, y - 1, 5.2f, 1.4f); c.FillRect(ix - 0.8f, y + 0.4f, 1.6f, 1.8f); }, "bench");
        Item(ix => { c.Fill = RED; c.FillCircle(ix, y - 1.4f, 1.2f); c.FillRect(ix - 1, y - 0.4f, 2, 2.4f); }, "people");
        Item(ix => { c.Stroke = SEA; c.LineWidth = 0.6f; c.BeginPath(); c.Arc(ix, y, 2.2f, 0, H.PI * 1.6f); c.StrokePath(); }, "wrack pool");
        Item(ix => { c.Fill = STAIN; c.FillRect(ix - 2, y - 1.4f, 4, 2.8f); c.Fill = INK; c.FillRect(ix - 2, y - 0.2f, 4, 0.5f); }, "unopened box");
        Item(ix => { c.Stroke = SEA; c.LineWidth = 0.6f; c.Line(ix - 3, y, ix + 3, y); }, "tide seen");
        Item(ix => { c.Fill = new Color(INK_FADED.R, INK_FADED.G, INK_FADED.B, 0.8f); c.FillRect(ix - 3, y - 2, 2, 0.6f); c.FillRect(ix + 1, y - 2, 2, 0.6f); c.FillRect(ix - 3, y + 1.6f, 2, 0.6f); c.FillRect(ix + 1, y + 1.6f, 2, 0.6f); }, "not yet entered");
        c.Fill = new Color(INK_FADED.R, INK_FADED.G, INK_FADED.B, 0.9f);
        c.FillText(Zoom > 0 ? "closer: wheel, triggers or + - to zoom; right stick or arrows to look about" : "wheel, triggers or + - to look closer", x + 4, y + 2);
    }
}
