// Gore: what a broken knot does to the flesh it was holding together. The drowned bleed old blood gone
// thick with salt. When a limb's knot breaks the limb comes away, the stump keeps weeping, and the spray
// stains whatever it lands on until the tide comes in and washes the stone clean. In water the blood
// hangs in slow clouds. Re-knotted bodies grow the lost limb back in salt.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

// A severed piece tumbling through the room.
public class Gib
{
    public string kind;                 // shin, forearm, arm, jaw, pick, graft, chunk
    public float x, y, vx, vy, ang, av, len, w = 2, bleed, age;
    public Color flesh, cloth;
    public bool rest, salt;
    public int seed;
}
// Blood dried onto stone. side: 0 floor, 1 a wall to the left, 2 a wall to the right, 3 a ceiling.
public class Stain { public float x, y, r, a = 1; public int side, seed; }

public static class Gore
{
    public static readonly Color BLOOD = new(0.42f, 0.07f, 0.07f), BLOOD_DARK = new(0.2f, 0.035f, 0.04f), RAW = new(0.62f, 0.12f, 0.11f), BONE = new(0.9f, 0.86f, 0.78f);
    public static readonly Color SALT = new(0.8f, 0.86f, 0.88f), SALT_SHADE = new(0.55f, 0.62f, 0.66f);

    class Drop { public float x, y, vx, vy, size, life; public bool salt; }
    class Cloud { public float x, y, r, a, grow; }

    public static readonly List<Gib> gibs = new();
    static readonly List<Drop> drops = new();
    static readonly List<Cloud> clouds = new();
    static readonly Dictionary<string, List<Stain>> stains = new();
    static float dripT;

    public static bool On => Settings.Gore != "off";
    public static bool Full => Settings.Gore == "full";
    public static int DropCount => drops.Count;   // tests
    public static List<Stain> Stains(string room) { if (!stains.TryGetValue(room, out var l)) stains[room] = l = new List<Stain>(); return l; }

    // A new room: pieces and spray belong to the room they fell in; stains stay for the rest of the session.
    public static void Clear() { gibs.Clear(); drops.Clear(); clouds.Clear(); }
    public static void Reset() { Clear(); stains.Clear(); }

    // Which part of a body a knot holds on. Only the robed, jointed dead come apart limb by limb.
    public static string Chain(Body b, Knot k)
    {
        if (!b.Humanoid) return null;
        return k.joint switch
        {
            "kneeL" => "shinL", "kneeR" => "shinR",
            "elbowL" => "forearmL", "elbowR" => "forearmR",
            "shoulderL" => "armL", "shoulderR" => "armR",
            "jaw" => "jaw",
            _ => null,
        };
    }

    // A knot broke: tear off what it held, and bleed.
    public static void OnKnotBroken(Body b, Knot k, string src)
    {
        if (!k.broken || b.gone) return;
        var kp = b.KnotPos(k) ?? new Vector2(b.Cx(), b.Cy());
        float ang = AwayFromMaren(kp);
        if (src is "fire" or "saw" or "stomp" or "gullet" or "driftling") ang = -H.PI / 2 + H.Rand(-0.6f, 0.6f);
        Voices.Hurt(b, true);
        if (!On) return;
        int n = Full ? 26 : 10;
        string chain = Chain(b, k);
        if (chain != null && !b.severed.Contains(chain) && !(chain.StartsWith("forearm") && b.severed.Contains("arm" + chain[^1])))
        {
            b.severed.Add(chain);
            SpawnLimb(b, chain, ang);
            Spray(kp.X, kp.Y, ang, 0.75f, n, 70, 230);
            Spray(kp.X, kp.Y, -H.PI / 2, 1.1f, n / 3, 30, 110);
            Snd.Sever(b.Pan());
            G.HitStop(0.09f);
            Narr.Think("firstSever");
        }
        else if (k.type == "crown")
        {
            // the chest knot goes and the brine in the body lets go all at once
            Spray(kp.X, kp.Y, ang, 1.3f, n + 12, 50, 200);
            Spray(kp.X, kp.Y, -H.PI / 2, 0.7f, n / 2, 80, 190);
            if (b.Humanoid) Throw(b, "chunk", kp, ang, 1.4f);
            Snd.Sever(b.Pan());
        }
        else if (k.type == "graft")
        {
            Throw(b, "graft", kp, ang, 1.6f);
            Spray(kp.X, kp.Y, ang, 0.9f, n / 2, 60, 180);
        }
        else if (b.type is "gullet" or "shoal" or "abbot" or "undertow")
        {
            Spray(kp.X, kp.Y, ang, 1.0f, n, 50, 200);
            Throw(b, "chunk", kp, ang, 1);
            if (b.type == "abbot") FX.Salt(kp.X, kp.Y, 24);
        }
        else Spray(kp.X, kp.Y, ang, 1.0f, n / 2, 40, 160);
    }

    // A hit that only reached flesh.
    public static void Wound(float x, float y, int n = 6)
    {
        if (!On) { FX.Blood(x, y, 3); return; }
        Spray(x, y, AwayFromMaren(new Vector2(x, y)), 0.6f, Full ? n : n / 2 + 1, 40, 150);
    }
    // Maren's own blood, when something reaches her.
    public static void Bleed(float x, float y, float fromX)
    {
        if (!On) { FX.Blood(x, y, 6); return; }
        float ang = x > fromX ? -0.5f : -H.PI + 0.5f;
        Spray(x, y, ang, 0.7f, Full ? 10 : 4, 40, 140);
    }

    static float AwayFromMaren(Vector2 at)
    {
        var p = G.player;
        if (p == null) return -H.PI / 2;
        var d = at - new Vector2(p.Cx(), p.y + 9);
        return d.LengthSquared() > 0.01f ? MathF.Atan2(d.Y, d.X) : -H.PI / 2;
    }

    public static void Spray(float x, float y, float ang, float spread, int n, float smin, float smax)
    {
        if (!On) return;
        for (int i = 0; i < n; i++)
        {
            float a = ang + H.Rand(-spread, spread), s = H.Rand(smin, smax);
            drops.Add(new Drop { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s - 30, size = H.Rand(0.7f, 1.9f), life = 2.5f, salt = H.Rand() < 0.12f });
        }
        if (drops.Count > 700) drops.RemoveRange(0, drops.Count - 700);
    }

    static Gib Make(Body b, string kind, Vector2 at, float ang, float speed)
    {
        var pal = b.Pal();
        var g = new Gib
        {
            kind = kind, x = at.X, y = at.Y, seed = H.Uid(),
            vx = MathF.Cos(ang) * speed * H.Rand(90, 170), vy = MathF.Sin(ang) * speed * H.Rand(60, 120) - H.Rand(110, 190),
            av = H.Rand(-14, 14), bleed = Full ? 2.5f : 0.8f,
            flesh = H.Col(pal.skin).Lerp(H.Col("#0e0f10"), 0.45f), cloth = H.Col(pal.robe).Lerp(H.Col("#0e0f10"), 0.55f),
        };
        gibs.Add(g);
        if (gibs.Count > 36) gibs.RemoveAt(0);
        return g;
    }
    static void Throw(Body b, string kind, Vector2 at, float ang, float speed) => Make(b, kind, at, ang, speed);

    static void SpawnLimb(Body b, string chain, float ang)
    {
        var j = b.Skel();
        string side = chain[^1].ToString();
        Vector2 a, z;
        switch (chain[..^1])
        {
            case "shin": a = j["knee" + side]; z = j["foot" + side]; break;
            case "forearm": a = j["elbow" + side]; z = j["hand" + side]; break;
            case "arm": a = j["shoulder" + side]; z = j["hand" + side]; break;
            default: a = j["jaw"]; z = a + new Vector2(b.facing * 2, 1); break;
        }
        var g = Make(b, chain == "jaw" ? "jaw" : chain[..^1], (a + z) / 2, ang, 1);
        g.len = MathF.Max(3, (z - a).Length()); g.ang = MathF.Atan2(z.Y - a.Y, z.X - a.X);
        g.salt = b.saltLimbs.Contains(chain) || (chain.StartsWith("forearm") && b.saltLimbs.Contains("arm" + side));
        // a Saltwright's pick goes with the hand that held it
        if (b.type == "saltwright" && side == "L" && chain != "shinL") { var pk = Make(b, "pick", j["handL"], ang + 0.4f, 1.2f); pk.len = 16; }
    }

    // ---- update ----------------------------------------------------------------------------------------
    public static void Update(float dt)
    {
        var room = G.room;
        if (room == null) return;
        var st = Stains(room.Id);
        // drops fly, land and stain; in water they bloom into clouds
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            d.life -= dt;
            float px = d.x, py = d.y;
            d.vy += 620 * dt;
            d.x += d.vx * dt; d.y += d.vy * dt;
            if (World.IsWater(d.x, d.y))
            {
                if (Full && clouds.Count < 90 && H.Rand() < 0.35f) clouds.Add(new Cloud { x = d.x, y = d.y, r = 1.5f, a = 0.55f, grow = H.Rand(1.5f, 3.5f) });
                drops.RemoveAt(i); continue;
            }
            if (World.SolidPx(d.x, d.y))
            {
                if (Full && !d.salt)
                {
                    // which face it struck: moving down onto a floor, up into a ceiling, or sideways into a wall
                    bool hitY = World.SolidPx(px, d.y), hitX = World.SolidPx(d.x, py);
                    int side = hitY || !hitX ? (d.vy > 0 ? 0 : 3) : (d.vx > 0 ? 2 : 1);
                    float tx = MathF.Floor(d.x / H.TILE) * H.TILE, ty = MathF.Floor(d.y / H.TILE) * H.TILE;
                    float sx = side == 2 ? tx : side == 1 ? tx + H.TILE : d.x;
                    float sy = side == 0 ? ty : side == 3 ? ty + H.TILE : d.y;
                    AddStain(st, sx, sy, d.size * H.Rand(0.9f, 1.8f), side);
                }
                drops.RemoveAt(i); continue;
            }
            if (d.life <= 0) drops.RemoveAt(i);
        }
        foreach (var c in clouds) { c.r += c.grow * dt; c.a -= dt * 0.07f; c.y -= dt * 1.5f; if (Tide.PhaseId() == "ebbing") c.x += room.Seaward * 8 * dt; }
        clouds.RemoveAll(c => c.a <= 0);
        // the tide washes the stone clean: stains under water fade
        for (int i = st.Count - 1; i >= 0; i--) if (World.IsWater(st[i].x, st[i].y - 2)) { st[i].a -= dt * 0.04f; if (st[i].a <= 0) st.RemoveAt(i); }
        UpdateGibs(dt, st);
        // stumps keep weeping
        if (!Full) return;
        dripT -= dt;
        if (dripT > 0) return;
        dripT = 0.12f;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body b || b.gone || b.severed.Count == 0 || b.burnT > 0 || H.Rand() > 0.35f) continue;
            var (at, dir) = Stump(b, H.Pick(b.severed.ToList()));
            drops.Add(new Drop { x = at.X + dir.X, y = at.Y + dir.Y, vx = dir.X * 20 + H.Rand(-6, 6), vy = dir.Y * 20, size = H.Rand(0.6f, 1.2f), life = 2 });
        }
    }

    static void AddStain(List<Stain> st, float x, float y, float r, int side)
    {
        // close spatter merges into a pool instead of piling up
        foreach (var s in st)
            if (s.side == side && MathF.Abs(s.x - x) < 2.5f && MathF.Abs(s.y - y) < 2.5f) { s.r = MathF.Min(6, s.r + r * 0.25f); s.a = 1; return; }
        st.Add(new Stain { x = x, y = y, r = r, side = side, seed = H.Uid() });
        if (st.Count > 260) st.RemoveAt(0);
    }

    static void UpdateGibs(float dt, List<Stain> st)
    {
        var p = G.player;
        foreach (var g in gibs)
        {
            g.age += dt;
            if (g.bleed > 0 && Full)
            {
                g.bleed -= dt;
                if (H.Rand() < dt * (g.rest ? 3 : 14)) drops.Add(new Drop { x = g.x + MathF.Cos(g.ang) * g.len / 2, y = g.y + MathF.Sin(g.ang) * g.len / 2, vx = g.vx * 0.3f + H.Rand(-10, 10), vy = g.vy * 0.3f, size = H.Rand(0.6f, 1.3f), life = 2 });
            }
            // Maren kicks what lies in her way
            if (g.rest && p != null && MathF.Abs(p.vx) > 50 && p.onGround && H.RectsOverlap(p.x, p.y + p.h - 6, p.w, 8, g.x - 3, g.y - 3, 6, 6))
            { g.rest = false; g.vx = p.vx * 0.9f + H.Sign(p.vx) * 30; g.vy = -90; g.av = H.Rand(-10, 10); Snd.Splat(Pan(g.x), 0.05f); }
            if (g.rest) continue;
            bool wet = World.IsWater(g.x, g.y);
            if (wet) { g.vy += 120 * dt; g.vx *= 1 - 2.5f * dt; g.vy *= 1 - 2.5f * dt; g.av *= 1 - 2 * dt; }
            else g.vy += 900 * dt;
            float r = g.kind == "pick" ? 3 : 2;
            float nx = g.x + g.vx * dt;
            if (World.SolidPx(nx + H.Sign(g.vx) * r, g.y)) { g.vx *= -0.35f; g.av *= 0.6f; }
            else g.x = nx;
            float ny = g.y + g.vy * dt;
            if (World.SolidPx(g.x, ny + (g.vy > 0 ? r : -r)))
            {
                if (g.vy > 140)
                {
                    Snd.Splat(Pan(g.x), MathF.Min(0.22f, g.vy / 1600));
                    if (Full && !g.salt && g.kind != "pick") AddStain(st, g.x, MathF.Floor((ny + r) / H.TILE) * H.TILE, 2.4f, 0);
                    if (Full && g.kind != "pick" && g.kind != "graft") Spray(g.x, g.y, -H.PI / 2, 1.2f, 3, 20, 70);
                }
                g.vy *= -0.28f; g.vx *= 0.65f; g.av *= 0.55f;
                if (MathF.Abs(g.vy) < 30 && MathF.Abs(g.vx) < 15)
                {
                    g.rest = true; g.vx = g.vy = 0; g.av = 0;
                    // lie flat
                    float flat = MathF.Round(g.ang / H.PI) * H.PI;
                    g.ang = flat + H.Rand(-0.15f, 0.15f);
                    g.y = MathF.Floor((ny + r) / H.TILE) * H.TILE - (g.kind == "pick" ? 1.5f : 1);
                }
            }
            else g.y = ny;
            g.ang += g.av * dt;
            // pieces stay in the room they fell in
            var rm = G.room;
            if (g.x < rm.Px || g.x > rm.Px + rm.Pw || g.y > rm.Py + rm.Ph) g.rest = true;
        }
    }
    static float Pan(float x) { var p = G.player; return p != null ? H.Clamp((x - p.Cx()) / 300, -1, 1) : 0; }

    // Where a stump is and which way it points.
    public static (Vector2 at, Vector2 dir) Stump(Body b, string chain)
    {
        var j = b.Skel();
        string side = chain[^1].ToString();
        (Vector2 a, Vector2 z) = chain[..^1] switch
        {
            "shin" => (j["knee" + side], j["foot" + side]),
            "forearm" => (j["elbow" + side], j["hand" + side]),
            "arm" => (j["shoulder" + side], j["elbow" + side]),
            _ => (j["jaw"], j["jaw"] + new Vector2(0, 2)),
        };
        var d = z - a; float l = d.Length();
        return (a, l > 0.01f ? d / l : new Vector2(0, 1));
    }

    // ---- drawing -----------------------------------------------------------------------------------------
    public static void DrawStains(Canvas c)
    {
        var r = G.room; if (r == null || !On) return;
        var cam = G.cam;
        foreach (var s in Stains(r.Id))
        {
            if (s.x < cam.X - 20 || s.x > cam.X + H.VIEW_W + 20 || s.y < cam.Y - 20 || s.y > cam.Y + H.VIEW_H + 20) continue;
            var col = new Color(BLOOD_DARK.R, BLOOD_DARK.G, BLOOD_DARK.B, 0.85f * s.a);
            c.Fill = col;
            float h1 = TileArt.Hash(s.seed, 3), h2 = TileArt.Hash(s.seed, 5);
            // all rectangles, so a floor soaked in a hundred stains stays one cheap batch
            switch (s.side)
            {
                case 0:
                    // a smear soaked into the top of the floor, with a few flecks beside it
                    float ry = MathF.Min(1.1f, s.r * 0.3f);
                    c.FillRect(s.x - s.r * 1.5f, s.y, s.r * 3, ry);
                    c.FillRect(s.x - s.r, s.y + ry * 0.5f, s.r * 2, ry);
                    c.FillRect(s.x + (h1 - 0.5f) * s.r * 4.5f, s.y, 0.8f + h2 * 0.6f, 0.6f);
                    c.FillRect(s.x - (h2 - 0.5f) * s.r * 3.5f, s.y + 0.2f, 0.6f, 0.5f);
                    c.FillRect(s.x - s.r * 0.7f, s.y + 0.1f, s.r * 0.9f, 0.45f, new Color(RAW.R, RAW.G, RAW.B, 0.3f * s.a));
                    break;
                case 3:
                    c.FillRect(s.x - s.r, s.y - 0.3f, s.r * 2, 0.6f);
                    c.FillRect(s.x - 0.3f, s.y, 0.6f, 1 + h1 * 2.5f);
                    break;
                default:
                    // a splash on the wall that runs down in a thin streak
                    float wx = s.side == 1 ? s.x + 0.2f : s.x - 0.2f;
                    c.FillRect(wx - s.r * 0.35f, s.y - s.r * 0.9f, s.r * 0.7f, s.r * 1.8f);
                    c.FillRect(wx - 0.3f, s.y, 0.6f, s.r * (1.5f + h1 * 3));
                    break;
            }
        }
    }

    public static void DrawPieces(Canvas c)
    {
        if (!On) return;
        foreach (var cl in clouds) c.Glow(cl.x, cl.y, cl.r, new Color(0.16f, 0.02f, 0.03f, cl.a * 0.6f));
        foreach (var g in gibs) DrawGib(c, g);
        foreach (var d in drops)
        {
            c.Fill = d.salt ? new Color(0.9f, 0.92f, 0.94f, 0.85f) : d.size > 1.3f ? BLOOD : RAW;
            float l = MathF.Min(2.5f, MathF.Abs(d.vy) / 140);
            c.FillRect(d.x - d.size / 2, d.y - d.size / 2 - l / 2, d.size, d.size + l);
        }
    }

    static void DrawGib(Canvas c, Gib g)
    {
        c.Save(); c.Translate(g.x, g.y); c.Rotate(g.ang);
        float h = g.len / 2;
        var flesh = g.salt ? SALT : g.flesh;
        switch (g.kind)
        {
            case "shin":
            case "forearm":
                c.Stroke = flesh; c.LineWidth = g.kind == "shin" ? 2.1f : 1.8f; c.Line(-h, 0, h, 0);
                c.Fill = flesh;
                if (g.kind == "shin") { c.BeginPath(); c.Ellipse(h + 0.6f, 0.6f, 1.8f, 0.9f, 0, 0, H.PI * 2); c.FillPath(); }
                else { c.Stroke = flesh; c.LineWidth = 0.6f; for (int i = -1; i <= 1; i++) c.Line(h, 0, h + 2.2f, i * 0.8f); }
                TornEnd(c, -h, g);
                break;
            case "arm":
                c.Stroke = g.salt ? SALT : g.cloth; c.LineWidth = 2.6f; c.Line(-h, 0, 0, 0.4f);
                c.Stroke = flesh; c.LineWidth = 1.8f; c.Line(0, 0.4f, h, 0);
                c.Fill = g.salt ? SALT : g.cloth; c.FillCircle(0, 0.4f, 1.5f);
                c.Stroke = flesh; c.LineWidth = 0.6f; for (int i = -1; i <= 1; i++) c.Line(h, 0, h + 2.2f, i * 0.8f);
                TornEnd(c, -h, g);
                break;
            case "jaw":
                c.Fill = BONE; c.BeginPath(); c.Arc(0, 0, 2.2f, 0, H.PI); c.FillPath();
                c.Fill = RAW; c.FillRect(-2, -0.4f, 4, 0.8f);
                c.Stroke = H.Col("#d8d4c4"); c.LineWidth = 0.35f; for (int i = -1; i <= 1; i++) c.Line(i * 0.9f, 0, i * 0.9f, -0.8f);
                break;
            case "pick":
                c.Stroke = H.Col("#6a5030"); c.LineWidth = 1.6f; c.Line(-8, 0, 7, 0);
                c.Stroke = H.Col("#9aa0a4"); c.LineWidth = 2.2f; c.BeginPath(); c.MoveTo(5, -5); c.QuadraticCurveTo(9, 0, 5, 5); c.StrokePath();
                break;
            case "graft":
                c.Fill = new Color(0.75f, 0.95f, 1f); c.FillRect(-1.5f, -1.5f, 3, 3);
                c.Glow(0, 0, 5, new Color(0.6f, 0.9f, 1f, 0.5f * MathF.Max(0, 1 - g.age / 20)));
                break;
            default:
                // a ragged lump of what was inside
                c.Fill = g.cloth; c.BeginPath(); c.Ellipse(0, 0, 2.6f, 1.6f, TileArt.Hash(g.seed, 1), 0, H.PI * 2); c.FillPath();
                c.Fill = BLOOD; c.BeginPath(); c.Ellipse(0.6f, 0.2f, 1.6f, 1.1f, 0, 0, H.PI * 2); c.FillPath();
                c.Fill = BONE; c.FillCircle(-0.8f, -0.4f, 0.5f);
                break;
        }
        c.Restore();
    }
    static void TornEnd(Canvas c, float x, Gib g)
    {
        if (g.salt) { c.Fill = new Color(0.95f, 0.98f, 1f); c.FillRect(x - 1, -1, 1.6f, 2); return; }
        c.Fill = RAW; c.FillCircle(x, 0, 1.25f);
        c.Fill = BONE; c.FillCircle(x - 0.9f, 0, 0.55f);
    }

    // The raw end where a limb used to be.
    public static void DrawStump(Canvas c, Vector2 at, Vector2 dir)
    {
        c.Fill = BLOOD_DARK; c.FillCircle(at.X + dir.X * 0.6f, at.Y + dir.Y * 0.6f, 1.35f);
        c.Fill = RAW; c.FillCircle(at.X + dir.X * 1.0f, at.Y + dir.Y * 1.0f, 0.95f);
        c.Fill = BONE; c.FillCircle(at.X + dir.X * 1.7f, at.Y + dir.Y * 1.7f, 0.5f);
    }
    // A limb the Brine grew back: salt crystal, faceted, faintly bright.
    public static void DrawSaltLimb(Canvas c, Vector2 a, Vector2 b, float w)
    {
        c.Stroke = SALT_SHADE; c.LineWidth = w + 0.5f; c.Line(a.X, a.Y, b.X, b.Y);
        c.Stroke = SALT; c.LineWidth = w * 0.7f; c.Line(a.X, a.Y, b.X, b.Y);
        var d = b - a; var n = new Vector2(-d.Y, d.X).Normalized();
        c.Fill = new Color(1, 1, 1, 0.85f);
        for (int i = 1; i <= 2; i++) { var p = a.Lerp(b, i / 3f) + n * (i % 2 == 0 ? 0.6f : -0.6f); c.FillRect(p.X - 0.35f, p.Y - 0.35f, 0.7f, 0.7f); }
    }
}
