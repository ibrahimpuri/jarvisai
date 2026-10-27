// The depth behind and in front of the playfield, layered the way Rain World builds its rooms and
// Hollow Knight its parallax: a far vista of the region's architecture sunk in fog, closer structures
// in the mid distance, a ruined back wall that clings to the rock and breaks open onto the depths, and
// an out-of-focus foreground of dark shapes in front of everything. All of it is generated from the room
// and drawn in encoded value/accent/glow, to be coloured by the palette shader.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Strata
{
    const int TS = H.TILE;
    static Color V(float v, float a = 1) => Canvas.V(v, a);
    static Color VA(float v, float acc, float a = 1) => Canvas.VA(v, acc, a);
    static Color Gl(float g, float a = 1) => Canvas.Gl(g, a);
    static int Seed(RoomDef r) => r.Id.Aggregate(23, (h, ch) => unchecked(h * 31 + ch));
    static float R(Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    public static string Style(RoomDef r) => r.Outdoor ? "shore" : r.Region switch
    {
        "saltworks" => "works",
        "ribvault" or "skull" => "bone",
        "gullet" => "flesh",
        _ => "gothic",
    };

    // The world rectangle a layer with parallax factor f must cover so that no edge shows wherever the
    // camera goes in this room. f = 1 moves with the world; less is further away.
    public static Rect2 Area(RoomDef r, float f)
    {
        const int VW = H.VIEW_W, VH = H.VIEW_H;
        var camC = CamCentre(r);
        float mx = MathF.Max(0, (r.Pw - VW) / 2) * MathF.Abs(1 - f) + 24, my = MathF.Max(0, (r.Ph - VH) / 2) * MathF.Abs(1 - f) + 24;
        float x0 = MathF.Min(r.Px, camC.X) - mx, y0 = MathF.Min(r.Py, camC.Y) - my;
        float x1 = MathF.Max(r.Px + r.Pw, camC.X + VW) + mx, y1 = MathF.Max(r.Py + r.Ph, camC.Y + VH) + my;
        return new Rect2(x0, y0, x1 - x0, y1 - y0);
    }
    public static Vector2 CamCentre(RoomDef r) => new(r.Px + r.Pw / 2 - H.VIEW_W / 2f, r.Py + r.Ph / 2 - H.VIEW_H / 2f);

    // ---- far: the region's great architecture, mostly fog ------------------------------------------------
    public static void PaintFar(Canvas c, RoomDef r, Rect2 a)
    {
        if (r.Outdoor) return;
        // a painted vista for the region, if there is one (art/vistas); the drawn architecture below otherwise
        var vista = Vista(r);
        if (vista != null) { PaintVista(c, r, a, vista); return; }
        var rng = new Random(Seed(r) + 1);
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        c.FillRectGradV(x0, y0, w, h, V(0.55f), V(0.28f));
        // light falling from openings high above
        int shafts = 2 + rng.Next(3);
        for (int i = 0; i < shafts; i++)
        {
            float sx = x0 + R(rng, 0.1f, 0.9f) * w, sw = R(rng, 18, 46), lean = R(rng, 0.15f, 0.45f) * h;
            c.FillPolyGrad(new[] { new Vector2(sx, y0), new Vector2(sx + sw, y0), new Vector2(sx + sw + lean, y0 + h), new Vector2(sx + lean - sw * 0.5f, y0 + h) },
                new[] { Gl(0.22f, 0.5f), Gl(0.22f, 0.5f), Gl(0, 0), Gl(0, 0) });
        }
        switch (Style(r))
        {
            case "gothic": FarGothic(c, r, a, rng); break;
            case "works": FarWorks(c, a, rng); break;
            case "bone": FarBone(c, r, a, rng); break;
            case "flesh": FarFlesh(c, a, rng); break;
        }
    }

    public static Texture2D Vista(RoomDef r) => r.Outdoor ? null : Art.Tex("vistas/" + r.Region);
    // The vista covers the far plane; each room of the region looks at a different part of the same great space.
    static void PaintVista(Canvas c, RoomDef r, Rect2 a, Texture2D t)
    {
        float tw = t.GetWidth(), th = t.GetHeight();
        float s = MathF.Max(a.Size.X / tw, a.Size.Y / th);
        float w = tw * s, h = th * s;
        float ox = (w - a.Size.X) * (Math.Abs(Seed(r)) % 1000 / 1000f), oy = (h - a.Size.Y) * 0.5f;
        bool enc = c.Encode; c.Encode = false;   // the vista is already in the palette encoding
        c.Fill = Colors.White;
        c.Texture(t, a.Position.X - ox, a.Position.Y - oy, w, h);
        c.Encode = enc;
    }

    static void PointedArch(Canvas c, float xl, float xr, float spring, float apex, float thick, Color col)
    {
        float mid = (xl + xr) / 2;
        c.Stroke = col; c.LineWidth = thick;
        c.BeginPath(); c.MoveTo(xl, spring + 40); c.LineTo(xl, spring); c.QuadraticCurveTo(xl, apex + (spring - apex) * 0.25f, mid, apex); c.QuadraticCurveTo(xr, apex + (spring - apex) * 0.25f, xr, spring); c.LineTo(xr, spring + 40); c.StrokePath();
    }
    static void Lancet(Canvas c, float cx, float top, float wdt, float hgt, float glow)
    {
        var pts = new List<Vector2> { new(cx - wdt / 2, top + hgt), new(cx - wdt / 2, top + wdt * 0.8f) };
        for (int i = 0; i <= 6; i++) { float t = i / 6f; pts.Add(new Vector2(cx - wdt / 2 + wdt * t, top + wdt * 0.8f - MathF.Sin(t * H.PI) * wdt * 0.8f - (t > 0.35f && t < 0.65f ? wdt * 0.15f : 0))); }
        pts.Add(new Vector2(cx + wdt / 2, top + hgt));
        c.Fill = Gl(glow, 0.9f); c.Poly(pts.ToArray());
        c.Fill = VA(0.75f, 0.5f, 0.35f); c.Poly(pts.ToArray());
        c.Fill = V(0.1f); c.FillRect(cx - 0.8f, top + 2, 1.6f, hgt - 2); c.FillRect(cx - wdt / 2, top + hgt * 0.55f, wdt, 1.4f);
    }
    static void FarGothic(Canvas c, RoomDef r, Rect2 a, Random rng)
    {
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        bool choir = r.Region == "choir", shore = r.Region == "wrackline";
        float bay = R(rng, 100, 130), spring = y0 + h * 0.42f, apex = y0 + h * 0.16f;
        // the wall at the back of the nave with its lancets, then the arcade in front of it
        for (float x = x0 - R(rng, 0, bay); x < x0 + w + bay; x += bay)
        {
            float mid = x + bay / 2;
            if (rng.NextDouble() < 0.7) Lancet(c, mid, spring - 8, R(rng, 12, 18), R(rng, 50, 80), R(rng, 0.15f, 0.35f));
            c.Fill = V(0.17f); c.FillRect(x - 8, apex - 30, 16, h);
            c.Fill = V(0.24f); c.FillRect(x - 8, apex - 30, 3, h);
            c.Fill = V(0.15f); c.FillRect(x - 11, spring - 4, 22, 6); c.FillRect(x - 12, y0 + h - 30, 24, 30);
            bool broken = shore && rng.NextDouble() < 0.35;
            if (!broken) PointedArch(c, x + 8, x + bay - 8, spring, apex, 7, V(0.17f));
            // the triforium: small arches over the main arcade
            for (int k = 0; k < 3; k++)
            {
                float tx = x + 10 + k * (bay - 20) / 3, tw = (bay - 20) / 3;
                PointedArch(c, tx + 2, tx + tw - 2, apex - 6, apex - 20, 2.5f, V(0.2f, 0.8f));
            }
        }
        // the vault overhead
        c.Fill = V(0.13f); c.FillRect(x0, y0, w, apex - 34 - y0 + 4);
        for (float x = x0; x < x0 + w; x += bay / 2) { c.Stroke = V(0.16f); c.LineWidth = 4; c.BeginPath(); c.MoveTo(x, apex - 30); c.QuadraticCurveTo(x + bay / 4, y0 + 4, x + bay / 2, apex - 30); c.StrokePath(); }
        if (choir)
        {
            // a rose window high in the far wall, and the great organ under it
            float rx = x0 + w / 2 + R(rng, -60, 60), ry = y0 + h * 0.3f, rr = MathF.Min(46, h * 0.16f);
            c.Fill = V(0.12f); c.FillCircle(rx, ry, rr + 5);
            c.Fill = Gl(0.45f, 0.9f); c.FillCircle(rx, ry, rr);
            c.Fill = VA(0.8f, 0.6f, 0.4f); c.FillCircle(rx, ry, rr);
            c.Stroke = V(0.1f); c.LineWidth = 1.6f;
            for (int i = 0; i < 12; i++) { float an = i * H.PI / 6; c.Line(rx, ry, rx + MathF.Cos(an) * rr, ry + MathF.Sin(an) * rr); }
            c.BeginPath(); c.Arc(rx, ry, rr * 0.45f, 0, H.PI * 2); c.StrokePath();
            float ox = rx - 70;
            for (int i = 0; i < 15; i++)
            {
                float px = ox + i * 10, ph = 40 + MathF.Sin(i / 14f * H.PI) * 70;
                c.Fill = V(0.2f); c.FillRect(px, y0 + h - 30 - ph, 7, ph);
                c.Fill = V(0.32f); c.FillRect(px, y0 + h - 30 - ph, 1.5f, ph);
                c.Fill = V(0.05f); c.BeginPath(); c.Ellipse(px + 3.5f, y0 + h - 30 - ph * 0.3f, 2, 3, 0, 0, H.PI * 2); c.FillPath();
            }
        }
        if (shore)
        {
            // nets hung to dry between the columns, long since rotted
            for (int k = 0; k < 3; k++)
            {
                float nx = x0 + R(rng, 0.1f, 0.8f) * w, ny = spring - R(rng, 10, 40), nw = R(rng, 60, 110);
                c.Stroke = V(0.14f, 0.7f); c.LineWidth = 0.8f;
                for (int i = 0; i <= 8; i++) { float t = i / 8f; c.BeginPath(); c.MoveTo(nx + nw * t, ny); c.QuadraticCurveTo(nx + nw * t + 4, ny + 30, nx + nw * t - 2, ny + 50 + MathF.Sin(i) * 6); c.StrokePath(); }
                for (int j = 0; j <= 5; j++) { float t = j / 5f; c.BeginPath(); c.MoveTo(nx, ny + 50 * t); c.QuadraticCurveTo(nx + nw / 2, ny + 50 * t + 14, nx + nw, ny + 50 * t); c.StrokePath(); }
            }
        }
    }
    static void FarWorks(Canvas c, Rect2 a, Random rng)
    {
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        // salt haze lit from above
        for (int i = 0; i < 5; i++) c.Glow(x0 + R(rng, 0, 1) * w, y0 + R(rng, 0.05f, 0.4f) * h, R(rng, 60, 140), R(rng, 20, 50), V(0.7f, 0.35f));
        // scaffold towers
        for (float x = x0 + R(rng, 0, 80); x < x0 + w; x += R(rng, 110, 170))
        {
            float tw = R(rng, 26, 40), top = y0 + R(rng, 0.05f, 0.35f) * h;
            c.Fill = V(0.16f); c.FillRect(x, top, 3, h); c.FillRect(x + tw, top, 3, h);
            c.Stroke = V(0.16f); c.LineWidth = 1.4f;
            for (float yy = top; yy < y0 + h; yy += 28) { c.Line(x, yy, x + tw + 3, yy); c.Line(x + 1, yy, x + tw + 2, yy + 28); c.Line(x + tw + 2, yy, x + 1, yy + 28); }
            if (rng.NextDouble() < 0.5) { c.Fill = V(0.16f); c.FillRect(x - 10, top - 6, tw + 23, 6); }
        }
        // gear wheels and the belts between them
        int gears = 1 + rng.Next(2);
        for (int i = 0; i < gears; i++)
        {
            float gx = x0 + R(rng, 0.15f, 0.85f) * w, gy = y0 + R(rng, 0.25f, 0.6f) * h, gr = R(rng, 34, 64);
            c.Stroke = V(0.15f); c.LineWidth = 7; c.BeginPath(); c.Arc(gx, gy, gr, 0, H.PI * 2); c.StrokePath();
            c.Fill = V(0.15f);
            int teeth = (int)(gr / 4);
            for (int k = 0; k < teeth; k++) { float an = k * H.PI * 2 / teeth; c.Save(); c.Translate(gx + MathF.Cos(an) * (gr + 4), gy + MathF.Sin(an) * (gr + 4)); c.Rotate(an); c.FillRect(-3, -2.5f, 6, 5); c.Restore(); }
            c.Stroke = V(0.15f); c.LineWidth = 3;
            for (int k = 0; k < 6; k++) { float an = k * H.PI / 3 + 0.3f; c.Line(gx, gy, gx + MathF.Cos(an) * gr, gy + MathF.Sin(an) * gr); }
            c.Fill = V(0.15f); c.FillCircle(gx, gy, gr * 0.18f);
        }
        // pipes running the length of the hall
        for (int i = 0; i < 3; i++)
        {
            float py = y0 + R(rng, 0.15f, 0.75f) * h, pt = R(rng, 5, 9);
            c.Fill = V(0.17f); c.FillRect(x0, py, w, pt);
            c.Fill = V(0.26f); c.FillRect(x0, py, w, 1.2f);
            for (float fx = x0 + R(rng, 0, 40); fx < x0 + w; fx += 60) { c.Fill = V(0.13f); c.FillRect(fx, py - 2, 4, pt + 4); }
        }
        // a chain of buckets
        float bx = x0 + R(rng, 0.2f, 0.8f) * w;
        c.Stroke = V(0.14f); c.LineWidth = 1.2f; c.Line(bx, y0, bx, y0 + h * 0.8f); c.Line(bx + 18, y0, bx + 18, y0 + h * 0.8f);
        for (float yy = y0 + 20; yy < y0 + h * 0.8f; yy += 26) { c.Fill = V(0.14f); c.Poly(new[] { new Vector2(bx - 4, yy), new Vector2(bx + 4, yy), new Vector2(bx + 3, yy + 7), new Vector2(bx - 3, yy + 7) }); }
    }
    static void FarBone(Canvas c, RoomDef r, Rect2 a, Random rng)
    {
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        // the giant's ribs, curving overhead
        for (float x = x0 - 60 + R(rng, 0, 60); x < x0 + w + 60; x += R(rng, 90, 130))
        {
            float bot = y0 + h + 10, top = y0 + R(rng, 0.0f, 0.15f) * h, bend = R(rng, 50, 90);
            c.Stroke = V(0.18f); c.LineWidth = R(rng, 16, 24);
            c.BeginPath(); c.MoveTo(x, bot); c.QuadraticCurveTo(x - bend, (bot + top) / 2, x + 70, top); c.StrokePath();
            c.Stroke = V(0.3f, 0.8f); c.LineWidth = 3;
            c.BeginPath(); c.MoveTo(x - 6, bot); c.QuadraticCurveTo(x - bend - 6, (bot + top) / 2, x + 64, top - 4); c.StrokePath();
        }
        // the spine along the roof
        for (float x = x0; x < x0 + w; x += 26)
        {
            c.Fill = V(0.16f); c.BeginPath(); c.Ellipse(x, y0 + 14, 11, 9, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = V(0.12f); c.FillRect(x + 9, y0 + 11, 8, 6);
        }
        if (r.Region == "skull")
        {
            // the eye sockets of the skull, open to the night, with the moon's light coming through
            for (int i = 0; i < 2; i++)
            {
                float ex = x0 + w * (0.3f + i * 0.4f), ey = y0 + h * 0.3f;
                c.Fill = Gl(0.35f, 0.8f); c.BeginPath(); c.Ellipse(ex, ey, 46, 30, i == 0 ? 0.15f : -0.15f, 0, H.PI * 2); c.FillPath();
                c.Fill = V(0.6f, 0.5f); c.BeginPath(); c.Ellipse(ex, ey, 46, 30, i == 0 ? 0.15f : -0.15f, 0, H.PI * 2); c.FillPath();
                c.FillPolyGrad(new[] { new Vector2(ex - 40, ey), new Vector2(ex + 40, ey), new Vector2(ex + 90, y0 + h), new Vector2(ex - 10, y0 + h) }, new[] { Gl(0.18f, 0.4f), Gl(0.18f, 0.4f), Gl(0, 0), Gl(0, 0) });
            }
        }
    }
    static void FarFlesh(Canvas c, Rect2 a, Random rng)
    {
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        float cx = x0 + w / 2, cy = y0 + h * 0.45f;
        // looking down the throat: ridged rings closing into the dark
        for (int i = 9; i >= 1; i--)
        {
            float rx = w * 0.09f * i, ry = h * 0.11f * i;
            c.Stroke = V(i % 2 == 0 ? 0.12f : 0.2f); c.LineWidth = 10 + i;
            c.BeginPath(); c.Ellipse(cx, cy, rx, ry, 0, 0, H.PI * 2); c.StrokePath();
        }
        c.Fill = V(0.05f); c.BeginPath(); c.Ellipse(cx, cy, w * 0.07f, h * 0.08f, 0, 0, H.PI * 2); c.FillPath();
        for (int i = 0; i < 26; i++)
        {
            float gx = x0 + R(rng, 0, 1) * w, gy = y0 + R(rng, 0, 1) * h;
            c.Fill = Gl(0.5f, 0.8f); c.FillCircle(gx, gy, R(rng, 0.8f, 2));
            c.Glow(gx, gy, R(rng, 5, 10), VA(0.6f, 0.9f, 0.25f));
        }
        for (int i = 0; i < 14; i++)
        {
            float tx = x0 + R(rng, 0, 1) * w, len = R(rng, 40, 140);
            c.Stroke = V(0.12f); c.LineWidth = R(rng, 2, 5);
            c.BeginPath(); c.MoveTo(tx, y0); c.QuadraticCurveTo(tx + R(rng, -20, 20), y0 + len / 2, tx + R(rng, -10, 10), y0 + len); c.StrokePath();
        }
    }

    // ---- mid: closer columns, chains, scaffolds, ribs and cliffs -------------------------------------------
    public static void PaintMid(Canvas c, RoomDef r, Rect2 a)
    {
        var rng = new Random(Seed(r) + 2);
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y, bot = y0 + h;
        switch (Style(r))
        {
            case "gothic":
                for (float x = x0 + R(rng, 20, 120); x < x0 + w; x += R(rng, 170, 260))
                {
                    // a great column, some broken off where the vault fell
                    float cw = R(rng, 22, 30), top = rng.NextDouble() < 0.35 ? y0 + R(rng, 0.3f, 0.6f) * h : y0 - 10;
                    c.Fill = V(0.1f); c.FillRect(x, top, cw, bot - top);
                    c.Fill = V(0.2f); c.FillRect(x, top, 3, bot - top);
                    c.Fill = V(0.06f); c.FillRect(x + cw - 5, top, 5, bot - top);
                    c.Fill = V(0.1f); c.FillRect(x - 4, bot - 26, cw + 8, 26);
                    if (top > y0) { c.Fill = V(0.1f); c.Poly(new[] { new Vector2(x, top), new Vector2(x + cw * 0.3f, top - 9), new Vector2(x + cw * 0.55f, top - 3), new Vector2(x + cw * 0.8f, top - 12), new Vector2(x + cw, top) }); }
                    else { c.Fill = V(0.1f); c.FillRect(x - 6, y0 + h * 0.3f, cw + 12, 8); }
                }
                Chains(c, a, rng, 0.08f);
                break;
            case "works":
                for (float x = x0 + R(rng, 0, 100); x < x0 + w; x += R(rng, 150, 230))
                {
                    // a timber gantry with a walkway and a ladder
                    float gw = R(rng, 60, 110), gy = y0 + R(rng, 0.3f, 0.6f) * h;
                    c.Fill = V(0.1f); c.FillRect(x, gy, 5, bot - gy); c.FillRect(x + gw, gy, 5, bot - gy);
                    c.FillRect(x - 8, gy, gw + 21, 5);
                    c.Fill = V(0.2f); c.FillRect(x - 8, gy, gw + 21, 1.2f);
                    c.Stroke = V(0.1f); c.LineWidth = 2; c.Line(x + 5, gy + 5, x + gw, bot); c.Line(x + gw, gy + 5, x + 5, bot);
                    float lx = x + gw * 0.5f;
                    c.Stroke = V(0.12f); c.LineWidth = 1.2f; c.Line(lx, gy, lx, bot); c.Line(lx + 7, gy, lx + 7, bot);
                    for (float yy = gy + 6; yy < bot; yy += 7) c.Line(lx, yy, lx + 7, yy);
                }
                for (int i = 0; i < 2; i++)
                {
                    float px = x0 + R(rng, 0.1f, 0.9f) * w;
                    c.Fill = V(0.12f); c.FillRect(px, y0, 8, h);
                    c.Fill = V(0.24f); c.FillRect(px + 1, y0, 1.5f, h);
                    for (float yy = y0 + R(rng, 10, 50); yy < bot; yy += 70) { c.Fill = V(0.08f); c.FillRect(px - 3, yy, 14, 5); }
                }
                Chains(c, a, rng, 0.09f);
                break;
            case "bone":
                for (float x = x0 + R(rng, 0, 80); x < x0 + w + 40; x += R(rng, 130, 190))
                {
                    float top = y0 + R(rng, 0.0f, 0.2f) * h, bend = R(rng, 40, 70);
                    c.Stroke = V(0.1f); c.LineWidth = 11;
                    c.BeginPath(); c.MoveTo(x, bot + 5); c.QuadraticCurveTo(x - bend, (bot + top) / 2, x + 46, top); c.StrokePath();
                    c.Stroke = V(0.26f, 0.9f); c.LineWidth = 2.4f;
                    c.BeginPath(); c.MoveTo(x - 4, bot + 5); c.QuadraticCurveTo(x - bend - 4, (bot + top) / 2, x + 42, top - 3); c.StrokePath();
                }
                break;
            case "flesh":
                for (float x = x0 + R(rng, 0, 60); x < x0 + w; x += R(rng, 60, 110))
                {
                    c.Stroke = V(0.07f); c.LineWidth = R(rng, 8, 16);
                    c.BeginPath(); c.MoveTo(x, y0); for (float yy = 0; yy <= h; yy += 20) c.LineTo(x + MathF.Sin(yy * 0.03f + x) * 9, y0 + yy); c.StrokePath();
                }
                for (int i = 0; i < 18; i++)
                {
                    float gx = x0 + R(rng, 0, 1) * w, gy = y0 + R(rng, 0, 1) * h;
                    c.Fill = V(0.08f); c.FillCircle(gx, gy, 3.5f);
                    c.Fill = Gl(0.7f); c.FillCircle(gx, gy, 1.4f);
                }
                break;
            case "shore":
                // sea stacks and the bones of old wrecks between the shore and the far island
                for (float x = x0 + R(rng, -40, 60); x < x0 + w; x += R(rng, 140, 240))
                {
                    // a crag: broad at the foot, broken and stepped toward the top, bedded in layers
                    float sw = R(rng, 50, 100), top = y0 + R(rng, 0.38f, 0.62f) * h, ht = bot - top;
                    var pts = new List<Vector2> { new(x - sw * 0.7f, bot) };
                    int n = 12;
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n, peak = 1 - MathF.Pow(MathF.Abs(t - 0.45f) * 2, 1.6f);
                        float yy = bot - ht * MathF.Max(0.15f, peak) * (0.75f + Decor.Noise(x * 0.1f + i * 1.3f, 7) * 0.35f);
                        pts.Add(new Vector2(x - sw / 2 + sw * t + (Decor.Noise(i * 2.1f, x) - 0.5f) * 6, yy));
                    }
                    pts.Add(new Vector2(x + sw * 0.7f, bot));
                    c.Fill = V(0.1f); c.Poly(pts.ToArray());
                    c.Stroke = V(0.16f, 0.6f); c.LineWidth = 0.8f;
                    for (float yy = top + 12; yy < bot; yy += R(rng, 7, 12)) c.Line(x - sw * 0.3f + R(rng, -6, 6), yy, x + sw * 0.3f + R(rng, -6, 6), yy + R(rng, -2, 2));
                }
                for (int i = 0; i < 2; i++)
                {
                    float wx = x0 + R(rng, 0.2f, 0.8f) * w, wy = bot - R(rng, 30, 60);
                    c.Stroke = V(0.09f); c.LineWidth = 3.5f;
                    for (int k = 0; k < 5; k++) { float rx = wx + k * 12; c.BeginPath(); c.MoveTo(rx, bot); c.QuadraticCurveTo(rx - 10, wy + 10, rx + 4, wy - 10 + k * 3); c.StrokePath(); }
                    c.Line(wx + 20, bot, wx + 34, wy - 60);
                }
                break;
        }
    }
    // ---- near: the ruin's own bones, dark against the fog ------------------------------------------------------
    // Between the fog and the back wall: what is left of the structure that stood here, in near-black silhouette,
    // seen only where the wall has fallen away. The broken ribs of the vault and the piers that held them, the
    // Saltworks' pipes and hoists, the Saint's vertebrae, the Gullet's hanging tendrils. This is the layer that
    // gives a Hollow Knight room its depth: something dark and close in front of something pale and far.
    public static void PaintNear(Canvas c, RoomDef r, Rect2 a)
    {
        if (r.Outdoor) return;
        var rng = new Random(Seed(r) + 9);
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y, bot = y0 + h;
        var ink = V(0.045f); var edge = V(0.12f);
        switch (Style(r))
        {
            case "gothic":
                for (float x = x0 + R(rng, -80, 40); x < x0 + w; x += R(rng, 230, 330))
                {
                    float span = R(rng, 150, 230), spring = y0 + h * R(rng, 0.2f, 0.32f), apex = y0 - R(rng, 10, 40);
                    // the piers, one or both broken off below the springing
                    for (int side = 0; side < 2; side++)
                    {
                        float px = x + side * span - 10, top = rng.NextDouble() < 0.4 ? spring + R(rng, 0.2f, 0.5f) * (bot - spring) : spring;
                        c.Fill = ink; c.FillRect(px, top, 20, bot - top + 4);
                        c.Fill = edge; c.FillRect(px, top, 2, bot - top + 4);
                        if (top > spring) { c.Fill = ink; c.Poly(new[] { new Vector2(px, top), new Vector2(px + 5, top - R(rng, 6, 14)), new Vector2(px + 11, top - 3), new Vector2(px + 16, top - R(rng, 8, 18)), new Vector2(px + 20, top) }); }
                        else { c.Fill = ink; c.FillRect(px - 5, spring - 4, 30, 8); }
                    }
                    // the rib of the vault, a pointed arch; half the time one half has fallen
                    bool brokenL = rng.NextDouble() < 0.3, brokenR = !brokenL && rng.NextDouble() < 0.4;
                    float mx = x + span / 2;
                    void Rib(float ax, float bx, bool broken)
                    {
                        float ex = broken ? H.Lerp(ax, bx, R(rng, 0.35f, 0.6f)) : bx;
                        int n = 16;
                        var outer = new List<Vector2>(); var inner = new List<Vector2>();
                        for (int i = 0; i <= n; i++)
                        {
                            float t = i / (float)n, px = H.Lerp(ax, ex, t);
                            float u = MathF.Abs(px - mx) / (span / 2);              // 1 at the springing, 0 at the apex
                            float py = apex + (spring - apex) * (1 - MathF.Sqrt(MathF.Max(0, 1 - u)));   // pointed at the apex, upright at the springing
                            outer.Add(new Vector2(px, py)); inner.Add(new Vector2(px, py + 11));
                        }
                        inner.Reverse();
                        var poly = outer.Concat(inner).ToArray();
                        c.Fill = ink; c.Poly(poly);
                        c.Stroke = edge; c.LineWidth = 1.2f;
                        c.BeginPath(); c.MoveTo(outer[0].X, outer[0].Y); foreach (var q in outer) c.LineTo(q.X, q.Y); c.StrokePath();
                        // the voussoirs
                        c.Stroke = V(0.08f); c.LineWidth = 0.8f;
                        for (int i = 2; i < outer.Count - 1; i += 2) c.Line(outer[i].X, outer[i].Y, inner[outer.Count - 1 - i].X, inner[outer.Count - 1 - i].Y);
                        if (broken) { var e = outer[^1]; c.Fill = ink; c.Poly(new[] { e, new Vector2(e.X + 4, e.Y + 6), new Vector2(e.X - 2, e.Y + 9), new Vector2(e.X + 1, e.Y + 13), new Vector2(e.X - 5, e.Y + 11) }); }
                    }
                    if (!brokenL) Rib(x, mx, false); else Rib(x, mx, true);
                    if (!brokenR) Rib(x + span, mx, false); else Rib(x + span, mx, true);
                    // a corona of candles still hanging from the keystone on its chains
                    if (rng.NextDouble() < 0.6)
                    {
                        float cy = apex + R(rng, 0.35f, 0.55f) * (bot - apex), rx = R(rng, 14, 22);
                        c.Stroke = V(0.07f); c.LineWidth = 1;
                        c.Line(mx, apex + 8, mx - rx, cy); c.Line(mx, apex + 8, mx + rx, cy); c.Line(mx, apex + 8, mx, cy - 3);
                        c.Stroke = ink; c.LineWidth = 3; c.BeginPath(); c.Ellipse(mx, cy, rx, 3.5f, 0, 0, H.PI * 2); c.StrokePath();
                        for (int k = 0; k < 6; k++) { float cx = mx - rx + k * rx * 0.4f; c.Fill = ink; c.FillRect(cx - 1, cy - 6, 2, 5); }
                    }
                }
                break;
            case "works":
            {
                // the salt mains: great pipes across the hall with flanged joints, a hoist and its hook
                int pipes = 1 + rng.Next(2);
                for (int i = 0; i < pipes; i++)
                {
                    float py = y0 + h * R(rng, 0.12f, 0.38f) + i * 40, pr = R(rng, 6, 9);
                    c.FillRectGradV(x0, py - pr, w, pr * 2, V(0.1f), V(0.03f));
                    c.Fill = edge; c.FillRect(x0, py - pr, w, 1.2f);
                    for (float fx = x0 + R(rng, 0, 60); fx < x0 + w; fx += R(rng, 60, 110)) { c.Fill = ink; c.FillRect(fx, py - pr - 3, 7, pr * 2 + 6); c.Fill = V(0.1f); c.FillRect(fx, py - pr - 3, 7, 1); }
                    // a branch dropping to the floor
                    float dx = x0 + R(rng, 0.2f, 0.8f) * w;
                    c.Fill = ink; c.FillRect(dx, py, pr * 1.4f, bot - py); c.Fill = edge; c.FillRect(dx, py, 1.2f, bot - py);
                    c.Fill = ink; c.FillCircle(dx + pr * 0.7f, py + R(rng, 40, 90), pr * 1.4f);
                }
                for (float x = x0 + R(rng, 40, 160); x < x0 + w; x += R(rng, 200, 320))
                {
                    // a gantry crane's beam, and the hook on its chain
                    float by = y0 + h * R(rng, 0.05f, 0.15f), bl = R(rng, 90, 150);
                    c.Fill = ink; c.FillRect(x, by, bl, 9); c.Fill = edge; c.FillRect(x, by, bl, 1);
                    c.Stroke = ink; c.LineWidth = 1.4f;
                    for (float k = x + 6; k < x + bl - 6; k += 12) { c.Line(k, by + 9, k + 6, by); c.Line(k + 6, by, k + 12, by + 9); }
                    float hx = x + bl * R(rng, 0.4f, 0.8f), hl = R(rng, 60, 140);
                    c.Stroke = V(0.06f); c.LineWidth = 1.6f; c.Line(hx, by + 9, hx, by + 9 + hl);
                    for (float yy = by + 12; yy < by + 9 + hl; yy += 5) { c.Fill = V(0.06f); c.FillRect(hx - 1.5f, yy, 3, 3); }
                    c.Stroke = ink; c.LineWidth = 3; c.BeginPath(); c.Arc(hx + 3, by + 13 + hl, 5, -H.PI * 0.5f, H.PI * 0.95f); c.StrokePath();
                }
                break;
            }
            case "bone":
                // the Saint's spine, arching overhead, and ribs coming down out of the dark
                {
                    float sy = y0 + h * R(rng, 0.06f, 0.16f);
                    for (float x = x0 - 10; x < x0 + w + 20; x += 26)
                    {
                        float yy = sy + MathF.Sin((x - x0) / w * H.PI * 1.3f + 0.4f) * 22;
                        c.Fill = ink; c.BeginPath(); c.Ellipse(x, yy, 11, 9, 0, 0, H.PI * 2); c.FillPath();
                        c.Fill = edge; c.BeginPath(); c.Ellipse(x - 2, yy - 4, 7, 2.5f, 0, 0, H.PI * 2); c.FillPath();
                        c.Fill = ink; c.Poly(new[] { new Vector2(x - 3, yy + 6), new Vector2(x + 3, yy + 6), new Vector2(x + 1, yy + 26), new Vector2(x - 1, yy + 26) });
                    }
                    for (float x = x0 + R(rng, -40, 60); x < x0 + w; x += R(rng, 160, 240))
                    {
                        float len = R(rng, 0.5f, 0.85f) * h, bend = R(rng, 40, 80);
                        c.Stroke = ink; c.LineWidth = 13;
                        c.BeginPath(); c.MoveTo(x, sy); c.QuadraticCurveTo(x + bend, sy + len * 0.5f, x + bend * 0.4f, sy + len); c.StrokePath();
                        c.Stroke = edge; c.LineWidth = 2;
                        c.BeginPath(); c.MoveTo(x - 5, sy); c.QuadraticCurveTo(x + bend - 5, sy + len * 0.5f, x + bend * 0.4f - 5, sy + len); c.StrokePath();
                    }
                }
                break;
            case "flesh":
                // tendrils hanging from the roof of the throat, a few still lit at the tip
                for (float x = x0 + R(rng, 0, 30); x < x0 + w; x += R(rng, 24, 60))
                {
                    float len = R(rng, 0.2f, 0.7f) * h, sway = R(rng, -14, 14), wid = R(rng, 4, 10);
                    var pts = new List<Vector2>();
                    int n = 10;
                    for (int i = 0; i <= n; i++) { float t = i / (float)n; pts.Add(new Vector2(x - wid * (1 - t) + MathF.Sin(t * 3 + x) * sway * t, y0 + len * t)); }
                    for (int i = n; i >= 0; i--) { float t = i / (float)n; pts.Add(new Vector2(x + wid * (1 - t) + MathF.Sin(t * 3 + x) * sway * t, y0 + len * t)); }
                    c.Fill = ink; c.Poly(pts.ToArray());
                    if (rng.NextDouble() < 0.35) { var tip = pts[n]; c.Fill = Gl(0.6f); c.FillCircle(tip.X, tip.Y, 1.6f); }
                }
                break;
        }
    }

    static void Chains(Canvas c, Rect2 a, Random rng, float v)
    {
        float x0 = a.Position.X, y0 = a.Position.Y, w = a.Size.X, h = a.Size.Y;
        int n = 2 + rng.Next(3);
        for (int i = 0; i < n; i++)
        {
            float cx = x0 + R(rng, 0.05f, 0.95f) * w, len = R(rng, 0.25f, 0.6f) * h;
            c.Stroke = V(v); c.LineWidth = 1.6f; c.Line(cx, y0, cx, y0 + len);
            c.Fill = V(v); for (float yy = y0; yy < y0 + len; yy += 6) c.FillRect(cx - 1.6f, yy, 3.2f, 3);
            if (rng.NextDouble() < 0.5) { c.FillRect(cx - 5, y0 + len, 10, 2); c.Poly(new[] { new Vector2(cx - 5, y0 + len + 2), new Vector2(cx + 5, y0 + len + 2), new Vector2(cx, y0 + len + 12) }); }
            else { c.Fill = V(v + 0.04f); c.FillRect(cx - 3.5f, y0 + len, 7, 9); c.Fill = Gl(0.5f, 0.8f); c.FillRect(cx - 2, y0 + len + 2, 4, 5); }
        }
    }

    // ---- the ruined back wall ---------------------------------------------------------------------------
    // Fills where field < iso on a grid of samples (marching squares with interpolated edges), colouring
    // each vertex with col; returns the crossing segments so the broken edge can be picked out.
    static List<(Vector2, Vector2)> IsoFill(Canvas c, float[,] f, float iso, Vector2 origin, float s, Func<Vector2, Color> col)
    {
        var edges = new List<(Vector2, Vector2)>();
        int nx = f.GetLength(0), ny = f.GetLength(1);
        var crossings = new List<Vector2>(4);
        for (int j = 0; j < ny - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                float[] v = { f[i, j], f[i + 1, j], f[i + 1, j + 1], f[i, j + 1] };
                Vector2[] p = { origin + new Vector2(i * s, j * s), origin + new Vector2((i + 1) * s, j * s), origin + new Vector2((i + 1) * s, (j + 1) * s), origin + new Vector2(i * s, (j + 1) * s) };
                int inside = 0; for (int k = 0; k < 4; k++) if (v[k] < iso) inside++;
                if (inside == 0) continue;
                var pts = new List<Vector2>(8); crossings.Clear();
                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) % 4;
                    bool ina = v[k] < iso, inb = v[k2] < iso;
                    if (ina) pts.Add(p[k]);
                    if (ina != inb)
                    {
                        float t = (iso - v[k]) / (v[k2] - v[k]);
                        var q = p[k].Lerp(p[k2], Math.Clamp(t, 0, 1));
                        pts.Add(q); crossings.Add(q);
                    }
                }
                if (crossings.Count == 2) edges.Add((crossings[0], crossings[1]));
                else if (crossings.Count == 4) { edges.Add((crossings[0], crossings[1])); edges.Add((crossings[2], crossings[3])); }
                var cols = pts.Select(col).ToArray();
                c.FillPolyGrad(pts.ToArray(), cols);
            }
        return edges;
    }

    // The wall behind the playfield survives only near the rock; elsewhere it has fallen and the depths
    // show through. Masonry falls brick by brick, so its broken edge steps; bone and flesh are organic.
    public static void PaintBackWall(Canvas c, RoomDef r, Func<int, int, bool> solid, IEnumerable<DecorItem> decor = null)
    {
        string style = Style(r);
        if (style == "shore") return;   // outside, the island itself is the backdrop
        var dist = Terrain.Distance(r, solid, 8);
        float D(int x, int y) => x < 0 || y < 0 || x >= r.Tw || y >= r.Th ? 0 : dist[y * r.Tw + x];
        // distance to the rock in tiles, smoothly interpolated, with noise to rough up the ruin
        float Field(float px, float py, float rough)
        {
            float u = px / TS - 0.5f, w = py / TS - 0.5f; int ix = (int)MathF.Floor(u), iy = (int)MathF.Floor(w); float fu = u - ix, fw = w - iy;
            float d = H.Lerp(H.Lerp(D(ix, iy), D(ix + 1, iy), fu), H.Lerp(D(ix, iy + 1), D(ix + 1, iy + 1), fu), fw);
            float wx = r.Px + px, wy = r.Py + py;
            return d + (Decor.Fbm(wx / 46f, wy / 38f) - 0.5f) * 2.6f * rough + (Decor.Noise(wx / 9f, wy / 9f) - 0.5f) * 0.8f * rough;
        }
        if (style == "bone") { Ossuary(c, r, Field); return; }
        if (style is "gothic" or "works")
        {
            // ashlar and its four hundred years of weather (Masonry), standing where the rock still holds it up
            float Rock(float px, float py)
            {
                float u = px / TS - 0.5f, w = py / TS - 0.5f; int ix = (int)MathF.Floor(u), iy = (int)MathF.Floor(w); float fu = u - ix, fw = w - iy;
                return H.Lerp(H.Lerp(D(ix, iy), D(ix + 1, iy), fu), H.Lerp(D(ix, iy + 1), D(ix + 1, iy + 1), fu), fw);
            }
            Masonry.Paint(c, r, Field, 2.4f, Rock, solid, decor, style == "works");
            return;
        }
        // bone and flesh: organic shapes from the same field
        const float S = 8;
        int nx = r.Tw * 2 + 1, ny = r.Th * 2 + 1;
        var field = new float[nx, ny];
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) field[i, j] = Field(i * S, j * S, 1);
        float iso = style == "flesh" ? 2.6f : 2.2f;
        Color Wall(Vector2 p)
        {
            float n = Decor.Fbm(p.X / 30f, p.Y / 30f);
            return style == "flesh" ? VA(0.12f + n * 0.08f, 0.15f) : V(0.14f + n * 0.08f);
        }
        var edges = IsoFill(c, field, iso, new Vector2(r.Px, r.Py), S, Wall);
        for (int ly = 0; ly < r.Th; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                if (solid(lx, ly) || field[lx * 2 + 1, ly * 2 + 1] > iso - 0.5f) continue;
                float x = r.Px + lx * TS, y = r.Py + ly * TS; float h = TileArt.Hash(r.Ox + lx, r.Oy + ly);
                if (style == "bone" && h > 0.5f) { c.Stroke = V(0.26f, 0.7f); c.LineWidth = 0.8f; c.BeginPath(); c.Arc(x + 8, y + 8 + h * 4, 5 + h * 3, H.PI * 1.1f, H.PI * 1.9f); c.StrokePath(); }
                if (style == "flesh" && h > 0.6f) { c.Stroke = VA(0.2f, 0.35f, 0.6f); c.LineWidth = 0.7f; c.BeginPath(); c.MoveTo(x, y + 8 * h); c.QuadraticCurveTo(x + 8, y + 16 * h, x + 16, y + 4 + 8 * h); c.StrokePath(); }
            }
        c.Stroke = V(0.3f, 0.7f); c.LineWidth = 0.9f;
        c.BeginPath(); foreach (var (p0, p1) in edges) { c.MoveTo(p0.X, p0.Y); c.LineTo(p1.X, p1.Y); } c.StrokePath();
    }

    // The bone halls are walled with the dead: skulls and the ends of long bones stacked in courses.
    static void Ossuary(Canvas c, RoomDef r, Func<float, float, float, float> field)
    {
        const float BW = 12, BH = 10, ISO = 1.9f;
        int cols = (int)(r.Pw / BW) + 2, rows = (int)(r.Ph / BH) + 1;
        bool Has(int bx, int by) => by >= 0 && by < rows && field(bx * BW + (by % 2) * BW / 2, by * BH + BH / 2, 1) < ISO;
        for (int by = 0; by < rows; by++)
            for (int bx = -1; bx < cols; bx++)
            {
                if (!Has(bx, by)) continue;
                float x = r.Px + bx * BW + (by % 2) * BW / 2 - BW / 2, y = r.Py + by * BH, cx = x + BW / 2, cy = y + BH / 2;
                float h = TileArt.Hash(bx * 5 + r.Ox, by * 11 + r.Oy);
                c.Fill = V(0.04f); c.FillRect(x, y, BW, BH);
                if (h < 0.42f)
                {
                    // a skull, facing out
                    c.Fill = V(0.13f + h * 0.06f); c.FillCircle(cx, cy - 0.4f, 4.4f);
                    c.Fill = V(0.03f); c.FillCircle(cx - 1.8f, cy - 0.4f, 1.3f); c.FillCircle(cx + 1.8f, cy - 0.4f, 1.3f);
                }
                else
                {
                    // the knuckled ends of long bones, laid crosswise
                    c.Fill = V(0.11f + h * 0.05f); c.FillRect(cx - 4.4f, y + 2, 8.8f, 6);
                    c.Fill = V(0.05f); c.FillRect(cx - 0.4f, y + 2, 0.8f, 6); c.FillRect(cx - 4.4f, y + 4.6f, 8.8f, 0.8f);
                }
                if (!Has(bx, by - 1)) { c.Fill = V(0.24f, 0.7f); c.FillRect(x + 1, y, BW - 2, 0.7f); }
            }
    }

    // ---- foreground: dark, out of focus, in front of everything ------------------------------------------
    // Shapes stand on the rock at the room's edges and hang from its ceilings, so they frame the view
    // without hiding the ground Maren walks on.
    public static void PaintFore(Canvas c, RoomDef r, Rect2 a, Func<int, int, bool> mass)
    {
        var rng = new Random(Seed(r) + 4);
        string style = Style(r);
        var ink = V(0.0f);
        c.Fill = ink; c.Stroke = ink;
        for (int lx = 0; lx < r.Tw; lx++)
        {
            bool edgeCol = lx < 4 || lx > r.Tw - 5;
            for (int ly = 1; ly < r.Th; ly++)
            {
                if (!mass(lx, ly) || mass(lx, ly - 1)) continue;   // a surface: rock with air above
                int thick = 0; while (thick < 6 && mass(lx, ly + thick)) thick++;
                float sx = r.Px + lx * TS + 8, sy = r.Py + ly * TS;
                bool low = ly > r.Th - 5;
                if (!(edgeCol || low) || thick < 2 || rng.NextDouble() > (edgeCol ? 0.35 : 0.07)) continue;
                float bottom = MathF.Max(sy + thick * TS, a.End.Y);
                // away from the edges nothing may stand above the ground Maren walks on
                if (!edgeCol) sy += 5;
                switch (style)
                {
                    case "gothic":
                        if (rng.NextDouble() < 0.4)
                        {
                            // a broken column stump
                            float cw = R(rng, 14, 22), top = sy - (edgeCol ? R(rng, 4, 26) : 0);
                            c.Poly(new[] { new Vector2(sx - cw / 2, bottom), new Vector2(sx - cw / 2, top + 4), new Vector2(sx - cw * 0.1f, top), new Vector2(sx + cw * 0.15f, top + 5), new Vector2(sx + cw / 2, top + 1), new Vector2(sx + cw / 2, bottom) });
                            c.FillRect(sx - cw / 2 - 3, sy - 2, cw + 6, 4);
                        }
                        else Rubble(c, rng, sx, sy + 2, bottom, R(rng, 18, 34));
                        break;
                    case "works":
                        if (rng.NextDouble() < 0.5 && edgeCol) { float bw = R(rng, 12, 20), bh = R(rng, 10, 16); c.FillRect(sx - bw / 2, sy - bh + 2, bw, bh + bottom - sy); c.FillRect(sx - bw / 2 + 3, sy - bh - R(rng, 6, 12), bw * 0.7f, 12); }
                        else Rubble(c, rng, sx, sy + 2, bottom, R(rng, 16, 28));
                        break;
                    case "bone":
                        Rubble(c, rng, sx, sy + 3, bottom, R(rng, 16, 26));
                        for (int k = 0; k < 2; k++)
                        {
                            float bx = sx + R(rng, -10, 10), byy = sy - R(rng, 0, 4), len = R(rng, 12, 22), an = R(rng, -0.5f, 0.5f);
                            c.Save(); c.Translate(bx, byy); c.Rotate(an);
                            c.FillRect(-len / 2, -1.6f, len, 3.2f); c.FillCircle(-len / 2, -1.4f, 2.4f); c.FillCircle(-len / 2, 1.4f, 2.4f); c.FillCircle(len / 2, -1.4f, 2.4f); c.FillCircle(len / 2, 1.4f, 2.4f);
                            c.Restore();
                        }
                        break;
                    case "flesh":
                        for (int k = 0; k < 3; k++) c.FillCircle(sx + R(rng, -10, 10), sy + R(rng, 2, 8), R(rng, 6, 12));
                        break;
                    default:
                        Rubble(c, rng, sx, sy + 2, bottom, R(rng, 18, 34));
                        break;
                }
                // reeds, weed and grass come up in front of the lip in the green and shore regions
                if (edgeCol && (style == "shore" || r.Region is "cloister" or "wrackline"))
                    for (int k = 0; k < 7; k++)
                    {
                        float gx = sx + R(rng, -14, 14), gh = R(rng, 6, style == "shore" ? 22 : 13), lean = R(rng, -4, 4);
                        c.Poly(new[] { new Vector2(gx - 1.3f, sy + 3), new Vector2(gx + lean, sy - gh), new Vector2(gx + 1.3f, sy + 3) });
                    }
                break;
            }
        }
        // things hanging from the ceilings
        for (int ly = 0; ly < r.Th - 1; ly++)
            for (int lx = 0; lx < r.Tw; lx++)
            {
                if (!(mass(lx, ly) && !mass(lx, ly + 1) && ly < r.Th / 2) || rng.NextDouble() > 0.1) continue;
                float x = r.Px + lx * TS + R(rng, 2, 14), y = r.Py + (ly + 1) * TS, len = R(rng, 16, 46);
                c.LineWidth = R(rng, 1.4f, 2.8f);
                if (style is "works" or "gothic" && rng.NextDouble() < 0.5)
                {
                    c.Line(x, y - 4, x, y + len);
                    for (float yy = y; yy < y + len; yy += 5) c.FillRect(x - 1.8f, yy, 3.6f, 2.8f);
                    if (style == "works") { c.Stroke = ink; c.LineWidth = 2; c.BeginPath(); c.Arc(x, y + len + 3, 3, -H.PI / 2, H.PI * 0.9f); c.StrokePath(); }
                }
                else
                {
                    float sw = R(rng, -6, 6);
                    c.BeginPath(); c.MoveTo(x, y - 4); c.QuadraticCurveTo(x + sw, y + len * 0.5f, x - sw * 0.5f, y + len); c.StrokePath();
                    if (style != "flesh") for (float k = 0.3f; k < 1; k += 0.25f) { float lx2 = H.Lerp(x, x - sw * 0.5f, k) + sw * 0.3f, ly2 = y + len * k; c.BeginPath(); c.Ellipse(lx2 + 2, ly2, 2.6f, 1.2f, 0.5f, 0, H.PI * 2); c.FillPath(); }
                }
            }
    }
    // A heap of fallen stone: angular blocks piled on a base.
    static void Rubble(Canvas c, Random rng, float cx, float top, float bottom, float w)
    {
        var pts = new List<Vector2> { new(cx - w / 2 - 4, bottom) };
        int n = 6;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            pts.Add(new Vector2(cx - w / 2 + w * t, top + MathF.Sin(t * H.PI) * -R(rng, 2, 7) + R(rng, 0, 3)));
        }
        pts.Add(new Vector2(cx + w / 2 + 4, bottom));
        c.Poly(pts.ToArray());
        for (int k = 0; k < 2; k++) { float bx = cx + R(rng, -w / 3, w / 3), bs = R(rng, 4, 7); c.Save(); c.Translate(bx, top - bs * 0.4f); c.Rotate(R(rng, -0.6f, 0.6f)); c.FillRect(-bs / 2, -bs / 2, bs, bs); c.Restore(); }
    }
}
