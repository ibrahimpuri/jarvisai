// Noise, particles, fire, hitscan and projectiles: the sandbox systems every room shares.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hollowmere;

public static class Noise
{
    // Every action has a noise radius in metres (tiles).
    public static void Emit(float x, float y, float metres, string src)
    {
        if (G.ents == null) return;
        float r = metres * H.TILE;
        G.noiseRings.Add(new G.Ring { x = x, y = y, r = r, t = 0.5f });
        foreach (var e in G.ents.ToArray())
            if (e.Hears && !e.dead)
            {
                float d = H.Dist(x, y, e.Cx(), e.Cy());
                if (d <= r) e.Hear(x, y, d, r, src);
            }
    }
}

public class Particle { public float x, y, vx, vy, life, max, size, grav; public Godot.Color color; public bool glow; }
public class FXOpt
{
    public float? ang; public float spread, smin = 20, smax = 90, lmin = 0.3f, lmax = 0.8f, size = 1.5f, grav = 200;
    public string color = "#ddd"; public bool glow;
}

public static class FX
{
    public static List<Particle> parts = new();
    public static void Spawn(float x, float y, int n, FXOpt o = null)
    {
        o ??= new FXOpt();
        var col = H.Col(o.color);
        for (int i = 0; i < n; i++)
        {
            float a = o.ang.HasValue ? o.ang.Value + H.Rand(-o.spread, o.spread) : H.Rand(0, H.PI * 2);
            float s = H.Rand(o.smin, o.smax);
            float life = H.Rand(o.lmin, o.lmax);
            parts.Add(new Particle { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, life = life, max = life, color = col, size = o.size, grav = o.grav, glow = o.glow });
        }
        if (parts.Count > 1500) parts.RemoveRange(0, parts.Count - 1500);
    }
    public static void Salt(float x, float y, int n = 8) => Spawn(x, y, n, new FXOpt { color = "#e8f4ff", size = 1.6f, smin = 40, smax = 140, glow = true });
    public static void Blood(float x, float y, int n = 5) => Spawn(x, y, n, new FXOpt { color = "#3a4a40", size = 1.6f });
    public static void Splash(float x, float y, int n = 10) => Spawn(x, y, n, new FXOpt { color = "#9fc4d0", ang = -H.PI / 2, spread = 1.0f, smin = 40, smax = 130, grav = 400 });
    public static void Sparks(float x, float y, int n = 6) => Spawn(x, y, n, new FXOpt { color = "#ffd080", size = 1.2f, smin = 60, smax = 160, glow = true, lmin = 0.1f, lmax = 0.3f });
    public static void Bubbles(float x, float y, int n = 1) => Spawn(x, y, n, new FXOpt { color = "#bfe0ff", ang = -H.PI / 2, spread = 0.4f, smin = 15, smax = 35, grav = -40, lmin = 0.6f, lmax = 1.4f });
    public static void Embers(float x, float y, int n = 2) => Spawn(x, y, n, new FXOpt { color = "#ff8a30", ang = -H.PI / 2, spread = 0.6f, smin = 10, smax = 50, grav = -30, glow = true });
    public static void Update(float dt)
    {
        foreach (var p in parts) { p.life -= dt; p.vy += p.grav * dt; p.x += p.vx * dt; p.y += p.vy * dt; }
        parts.RemoveAll(p => p.life <= 0);
    }
}

public static class Fire
{
    public class Cell { public int tx, ty; public float t, spread; }
    public static readonly Dictionary<(int, int), Cell> cells = new();
    public static void Clear() => cells.Clear();
    public static void Ignite(int tx, int ty, float t = 6)
    {
        if (World.Solid(tx, ty) == 1) return;
        float px = tx * H.TILE + 8, py = ty * H.TILE + 8;
        if (World.IsWater(px, py)) return;
        if (!cells.ContainsKey((tx, ty))) cells[(tx, ty)] = new Cell { tx = tx, ty = ty, t = t, spread = 0.7f };
    }
    public static void Spill(float x, float y, int radiusTiles)
    {
        int tx0 = H.FloorI(x / H.TILE), ty0 = H.FloorI(y / H.TILE);
        for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
            for (int dy = -2; dy <= 1; dy++)
            {
                int tx = tx0 + dx, ty = ty0 + dy;
                // Oil pools on top of solid ground.
                if (World.Solid(tx, ty) != 1 && World.Solid(tx, ty + 1) >= 1) Ignite(tx, ty, 6 + H.Rand() * 2);
            }
        Snd.Fire();
        Noise.Emit(x, y, 8, "fire");
    }
    public static bool BurnsAt(float x, float y) => cells.ContainsKey((H.FloorI(x / H.TILE), H.FloorI(y / H.TILE)));
    static readonly (int, int)[] NB = { (1, 0), (-1, 0), (0, -1), (0, 1), (1, 1), (-1, 1), (1, -1), (-1, -1) };
    public static void Update(float dt)
    {
        var add = new List<(int, int, float)>();
        foreach (var kv in cells.ToArray())
        {
            var c = kv.Value;
            c.t -= dt; c.spread -= dt;
            float px = c.tx * H.TILE + 8, py = c.ty * H.TILE + 8;
            if (World.IsWater(px, py)) c.t = 0;
            if (H.Rand() < dt * 6) FX.Embers(px + H.Rand(-6, 6), py + 6, 1);
            if (c.spread <= 0)
            {
                c.spread = 0.9f;
                foreach (var (dx, dy) in NB)
                {
                    int tx = c.tx + dx, ty = c.ty + dy;
                    char ch = World.CharAt(tx, ty);
                    // Fire spreads along timber and rigging, and cracks salt walls.
                    if (ch == 'b') { World.SetChar(tx, ty, ' '); add.Add((tx, ty, 4)); FX.Embers(tx * 16 + 8, ty * 16 + 8, 6); }
                    else if (ch == 'S') { World.SetChar(tx, ty, ' '); FX.Salt(tx * 16 + 8, ty * 16 + 8, 12); Snd.Crack(); }
                    else if ((ch == 'T' || ch == 'P' || ch == '=') && H.Rand() < 0.35f)
                    {
                        // burn the air cell beside the timber
                        foreach (var (ex, ey) in new[] { (0, -1), (1, 0), (-1, 0) })
                            if (World.Solid(tx + ex, ty + ey) == 0 && !cells.ContainsKey((tx + ex, ty + ey))) { add.Add((tx + ex, ty + ey, 3)); break; }
                    }
                }
            }
            if (c.t <= 0) cells.Remove(kv.Key);
        }
        foreach (var (tx, ty, t) in add) Ignite(tx, ty, t);
    }
}

public class Hit { public string kind; public float t, x, y; public Body target; public Knot knot; }

public static class Combat
{
    // Rivet: hitscan against knots first, then bodies, then walls.
    public static Hit Hitscan(float x, float y, float ang, float maxD, Body ignore = null)
    {
        float dx = MathF.Cos(ang), dy = MathF.Sin(ang);
        float wallD = Phys.Ray(x, y, ang, maxD);
        float ex = x + dx * wallD, ey = y + dy * wallD;
        var best = new Hit { kind = wallD < maxD ? "wall" : "none", t = 1, x = ex, y = ey };
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e.dead || e.gone || e == ignore) continue;
            foreach (var k in e.knots)
            {
                if (k.broken) continue;
                var kp = e.KnotPos(k);
                if (kp == null) continue;
                float t = H.SegCircle(x, y, ex, ey, kp.Value.X, kp.Value.Y, k.r);
                if (t >= 0 && t < best.t) best = new Hit { kind = "knot", t = t, target = e, knot = k, x = x + (ex - x) * t, y = y + (ey - y) * t };
            }
        }
        if (best.kind != "knot")
        {
            foreach (var e0 in G.ents)
            {
                if (e0 is not Body e || e.dead || e.gone || e == ignore) continue;
                var b = e.HitBox();
                float t = RayRect(x, y, ex, ey, b.x, b.y, b.w, b.h);
                if (t >= 0 && t < best.t) best = new Hit { kind = "body", t = t, target = e, x = x + (ex - x) * t, y = y + (ey - y) * t };
            }
        }
        return best;
    }

    public static float RayRect(float x1, float y1, float x2, float y2, float rx, float ry, float rw, float rh)
    {
        float t0 = 0, t1 = 1;
        float dx = x2 - x1, dy = y2 - y1;
        Span<float> p = stackalloc float[] { -dx, dx, -dy, dy };
        Span<float> q = stackalloc float[] { x1 - rx, rx + rw - x1, y1 - ry, ry + rh - y1 };
        for (int i = 0; i < 4; i++)
        {
            if (p[i] == 0) { if (q[i] < 0) return -1; }
            else
            {
                float t = q[i] / p[i];
                if (p[i] < 0) { if (t > t1) return -1; if (t > t0) t0 = t; }
                else { if (t < t0) return -1; if (t < t1) t1 = t; }
            }
        }
        return t0;
    }
}

// ---- projectiles -----------------------------------------------------------
public class Projectile
{
    public string kind;
    public float x, y, vx, vy, life, grav;
    public bool dead;
    class Stuck { public Body body; public float t, ox, oy; public bool wall; }
    Stuck stuck;
    public Body StuckBody => stuck?.body;

    public Projectile(string kind, float x, float y, float vx, float vy, float life = 3, float grav = 0)
    {
        this.kind = kind; this.x = x; this.y = y; this.vx = vx; this.vy = vy; this.life = life; this.grav = grav;
    }
    public void Update(float dt)
    {
        if (stuck != null) { UpdateStuck(dt); return; }
        float nx = x + vx * dt, ny = y + vy * dt;
        vy += grav * dt;
        if (World.IsWater(x, y)) { vx *= 1 - 2 * dt; vy *= 1 - 2 * dt; }
        // Hit bodies: knots first.
        float ang = MathF.Atan2(ny - y, nx - x);
        float d = H.Hypot(nx - x, ny - y);
        var hit = Combat.Hitscan(x, y, ang, d + 1);
        if (hit.kind == "knot" || hit.kind == "body") { OnHit(hit); return; }
        if (hit.kind == "wall") { x = hit.x - MathF.Cos(ang) * 2; y = hit.y - MathF.Sin(ang) * 2; OnWall(); return; }
        x = nx; y = ny;
        life -= dt;
        if (life <= 0) dead = true;
        var r = G.room;
        if (r != null && (x < r.Px - 50 || x > r.Px + r.Pw + 50 || y > r.Py + r.Ph + 50)) dead = true;
    }
    void OnHit(Hit hit)
    {
        var P = G.player;
        if (kind == "bolt")
        {
            Snd.Thud();
            if (hit.kind == "knot")
            {
                hit.target.DamageKnot(hit.knot, 1, "harpoon");
                if (!hit.target.dead) hit.target.Pin(hit.knot, 4, P.WeaponTier("harpoon"));
            }
            else hit.target.DamageFlesh(1, hit.x, hit.y, "harpoon");
            // The bolt rides the body; it drops when the pin ends.
            stuck = new Stuck { body = hit.target, t = 4 };
            x = hit.x; y = hit.y;
        }
        else if (kind == "flare")
        {
            if (P.WeaponTier("sling") >= 1) stuck = new Stuck { body = hit.target, ox = hit.x - hit.target.Cx(), oy = hit.y - hit.target.Cy() };
            else { vx *= -0.3f; vy = -60; }
        }
    }
    void OnWall()
    {
        if (kind == "bolt")
        {
            Snd.Thud(); FX.Sparks(x, y, 4);
            G.SpawnLoose("bolts", x, y);
            dead = true;
        }
        else if (kind == "flare")
        {
            vx *= 0.3f; vy = 0; grav = 0;
            stuck = new Stuck { wall = true };
        }
    }
    void UpdateStuck(float dt)
    {
        if (kind == "bolt")
        {
            var b = stuck.body;
            stuck.t -= dt;
            if (b != null && !b.dead) { x = b.Cx(); y = b.Cy(); }
            if (stuck.t <= 0 || b == null || b.dead || b.gone) { G.SpawnLoose("bolts", x, y); dead = true; }
            return;
        }
        if (kind == "flare")
        {
            var b = stuck.body;
            if (b != null && !b.dead && !b.gone) { x = b.Cx() + stuck.ox; y = b.Cy() + stuck.oy; }
            else if (stuck.body != null) stuck = new Stuck { wall = true };
            if (stuck.wall && !Phys.BoxSolid(x - 1, y + 1, 2, 2)) y += 60 * dt;
            life -= dt;
            if (life <= 0) dead = true;
        }
    }
    public LightSrc Light()
    {
        if (kind == "flare") return new LightSrc { x = x, y = y, r = 110 + MathF.Sin(G.Now * 1000 / 60) * 6, i = 1, color = "255,140,60" };
        return null;
    }
    public void Draw(Canvas c)
    {
        if (kind == "bolt")
        {
            float a = stuck != null ? 0 : MathF.Atan2(vy, vx);
            c.Save(); c.Translate(x, y); c.Rotate(a);
            c.StrokeStyle = "#b8c4cc"; c.LineWidth = 1.5f; c.BeginPath(); c.MoveTo(-9, 0); c.LineTo(5, 0); c.StrokePath();
            c.FillStyle = "#dfe8ee"; c.BeginPath(); c.MoveTo(5, -2); c.LineTo(9, 0); c.LineTo(5, 2); c.FillPath();
            c.Restore();
        }
        else if (kind == "flare")
        {
            c.FillStyle = "#ffd0a0"; c.FillRect(x - 1.5f, y - 1.5f, 3, 3);
            if (H.Rand() < 0.5f) FX.Embers(x, y, 1);
        }
    }
}

// A Saw-Line: a wire at knee height that cuts the knots of anything crossing it.
public class SawLine
{
    public float x1, x2, y; public int cuts = 3; public bool dead, rewinds;
    readonly Dictionary<Body, float> cooldown = new();
    public SawLine(float a, float b, float y, bool rewinds) { x1 = MathF.Min(a, b); x2 = MathF.Max(a, b); this.y = y; this.rewinds = rewinds; }
    public void Update(float dt)
    {
        foreach (var e0 in G.ents.ToArray())
        {
            if (e0 is not Body e || e.dead || e.gone || e.mode == "downed" || e.mode == "dormant") continue;
            var b = e.HitBox();
            if (b.x + b.w > x1 && b.x < x2 && b.y < y && b.y + b.h > y)
            {
                cooldown.TryGetValue(e, out var cd);
                if (cd > 0) continue;
                cooldown[e] = 1.0f;
                var k = e.knots.FirstOrDefault(k => !k.broken && !k.sealedFlag && (k.type == "knee" || k.type == "spine"))
                        ?? e.knots.FirstOrDefault(k => !k.broken && !k.sealedFlag && !k.plated);
                if (k != null) { e.DamageKnot(k, 2, "saw"); FX.Sparks(e.Cx(), y, 6); Snd.Crack(); }
                cuts--;
            }
        }
        foreach (var e in cooldown.Keys.ToArray()) cooldown[e] -= dt;
        if (cuts <= 0)
        {
            dead = true;
            if (rewinds) { G.player.bag.Add("spools", 1); H.ToastMsg("The Saw-Line rewinds itself."); }
        }
    }
    public void Draw(Canvas c)
    {
        c.Stroke = H.Rgba(220, 220, 230, 0.8f); c.LineWidth = 1;
        c.BeginPath(); c.MoveTo(x1, y); c.LineTo(x2, y); c.StrokePath();
        c.FillStyle = "#888"; c.FillRect(x1 - 1, y - 2, 3, 4); c.FillRect(x2 - 2, y - 2, 3, 4);
    }
}
