// The Undertow (stalker and final hunter), the Abbot of Salt, and the Shoal.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

// ---------------------------------------------------------------- Undertow (live body)
public class Undertow : Body
{
    public bool leaving;
    float bellT, stunT, timeLeaving;
    int dir;
    public Undertow(Props spec, RoomDef room) : base(spec, room)
    {
        type = "undertow";
        boss = spec.B("boss");
        w = 64; h = 30;
        x = spec.F("x") - w / 2; y = spec.F("y") - h / 2;
        AddKnot("buoy", "buoy", "buoy", 24, 7);
        if (boss)
        {
            AddKnot("ropeL", "rope", "ropeL", 8, 5).needsBuoy = true;
            AddKnot("ropeR", "rope", "ropeR", 8, 5).needsBuoy = true;
            AddKnot("crown", "crown", "core", 12, 7, 3);
        }
        mode = "hunt"; awakeMode = "hunt";
        dir = spec.I("dir", 1);
        facing = dir;
    }
    public override bool IsSealed(Knot k)
    {
        if (k.needsBuoy) return !K("buoy").broken;
        return base.IsSealed(k);
    }
    static Vector2 V(float x, float y) => new(x, y);
    public override Dictionary<string, Vector2> Skeleton()
    {
        float f = facing, x = Cx(), y = Cy();
        float s = MathF.Sin(anim * 2) * 2;
        return new()
        {
            ["buoy"] = V(x - f * 6, y - 16 + s),
            ["ropeL"] = V(x + f * 18, y + 6), ["ropeR"] = V(x - f * 22, y + 8),
            ["core"] = V(x + f * 4, y + 2),
            ["head"] = V(x + f * 28, y),
        };
    }
    public override void Hear(float hx, float hy, float d, float r, string src) { }
    public override void Wake(bool force = false) { }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.id == "buoy")
        {
            if (!boss)
            {
                leaving = true;
                H.ToastMsg("The buoy knot shatters. The Undertow sinks away for the rest of this tide.");
                G.undertow.DriveOff();
                Snd.Buoy(0.6f, Pan());
            }
            else { stunT = 7; Snd.Buoy(0.7f, Pan()); H.ToastMsg("It reels. The bell-ropes are exposed!"); }
        }
        if (k.type == "crown") Die();
    }
    void Die()
    {
        gone = true; dead = true;
        FX.Salt(Cx(), Cy(), 60);
        Snd.Toll(); G.Shake(8);
        G.OnUndertowKilled();
    }
    public override void Update(float dt)
    {
        _skel = null;
        anim += dt;
        if (gone) return;
        foreach (var k in knots) if (k.flare > 0) k.flare -= dt;
        var p = G.player;
        bellT -= dt;
        if (bellT <= 0) { bellT = 2.2f; Snd.Buoy(0.5f, Pan()); }
        if (stunT > 0)
        {
            stunT -= dt; vx *= 0.95f; vy = 20;
            if (stunT <= 0 && boss)
            {
                // Regrows its buoy knot if the ropes still hold.
                if (!K("ropeL").broken || !K("ropeR").broken) { var b = K("buoy"); b.broken = false; b.hp = 12; H.ToastMsg("The buoy knot regrows."); }
            }
            SwimMove(dt);
            return;
        }
        if (leaving)
        {
            vx = dir * 140; vy = 30;
            SwimMove(dt);
            if (x > room.Px + room.Pw + 40 || x + w < room.Px - 40 || timeLeaving > 4) gone = true;
            timeLeaving += dt;
            return;
        }
        cool -= dt;
        if (p == null) return;
        bool pIn = World.IsWater(p.Cx(), p.Cy());
        float tx = p.Cx(), ty = p.Cy();
        if (!pIn) ty = MathF.Max(p.Cy() + 30, World.SurfaceAt(p.Cx(), p.Cy()) + 20);
        if (mode == "windup")
        {
            atkT -= dt; vx *= 0.9f; vy *= 0.9f;
            if (atkT <= 0)
            {
                mode = "attack"; atkT = 0.5f;
                float a = MathF.Atan2(ty - Cy(), tx - Cx());
                vx = MathF.Cos(a) * 300; vy = MathF.Sin(a) * 300;
            }
        }
        else if (mode == "attack")
        {
            atkT -= dt;
            if (atkT <= 0) mode = "hunt";
        }
        else
        {
            float a = MathF.Atan2(ty - Cy(), tx - Cx());
            float sp = boss ? 95 : 80;
            vx = H.Approach(vx, MathF.Cos(a) * sp, 200 * dt);
            vy = H.Approach(vy, MathF.Sin(a) * sp, 200 * dt);
            if (cool <= 0 && H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 110 && (pIn || boss))
            {
                mode = "windup"; atkT = Wind(0.8f); cool = Cool(boss ? 2.2f : 3);
                foreach (var k in knots) if (!k.broken) k.flare = 0.8f;
                Voices.Windup(this);
            }
        }
        if (MathF.Abs(vx) > 10) facing = (int)H.Sign(vx);
        SwimMove(dt);
        if (H.RectsOverlap(x, y, w, h, p.x, p.y, p.w, p.h) && !p.Invuln())
        {
            p.Hurt(2, this);
            p.vx += H.Sign(p.Cx() - Cx()) * 200; p.vy -= 120;
        }
    }
    void SwimMove(float dt)
    {
        // Stays in water at least waist-deep.
        float nx = x + vx * dt, ny = y + vy * dt;
        bool Ok(float px, float py) => World.IsWater(px + w / 2, py + 6) && !Phys.BoxSolid(px, py, w, h);
        if (Ok(nx, y)) x = nx; else vx *= -0.3f;
        if (Ok(x, ny)) y = ny; else vy *= -0.3f;
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        float f = facing, x = Cx(), y = Cy();
        // the drowned bell-ringer, fused to a channel buoy: a swollen, barnacled mass trailing weed and chain
        float sw = MathF.Sin(anim * 2) * 1.5f;
        for (int i = 0; i < 6; i++)
        {
            float wx = x - f * (18 - i * 7), wy = y + 9;
            c.Stroke = i % 2 == 0 ? H.Col("#22382e") : H.Col("#2e2a22"); c.LineWidth = 1.6f;
            c.BeginPath(); c.MoveTo(wx, wy); c.QuadraticCurveTo(wx - f * 6 + sw, wy + 10, wx - f * 12 + sw * 2, wy + 18 + i % 3 * 4); c.StrokePath();
        }
        c.Stroke = H.Col("#3a3632"); c.LineWidth = 1;
        for (int i = 0; i < 7; i++) { float cx2 = x - f * (6 + i * 3.6f), cy2 = y + 10 + i * 2.6f + sw * (i / 7f); c.BeginPath(); c.Ellipse(cx2, cy2, 1.4f, 0.9f, 0.6f, 0, H.PI * 2); c.StrokePath(); }
        c.FillStyle = "#1a272b";
        c.BeginPath(); c.Ellipse(x, y, 34, 14, 0, 0, H.PI * 2); c.FillPath();
        for (int i = 0; i < 6; i++) { float bx = x - f * (26 - i * 10), by = y + 8 + MathF.Sin(anim * 3 + i) * 2; c.FillCircle(bx, by, 6 + (i % 2)); }
        c.FillCircle(x + f * 18, y - 6, 9); c.FillCircle(x - f * 12, y - 9, 8);
        // a wet sheen over the hump, sickly skin showing through rents
        c.Fill = H.Rgba(90, 120, 124, 0.3f); c.BeginPath(); c.Ellipse(x - f * 4, y - 9, 24, 4, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(120, 140, 130, 0.35f);
        for (int i = 0; i < 4; i++) { c.BeginPath(); c.Ellipse(x - f * (16 - i * 9), y + 2 + (i % 2) * 3, 3, 1.4f, 0.3f, 0, H.PI * 2); c.FillPath(); }
        // barnacles crusting the back
        for (int i = 0; i < 18; i++)
        {
            float ang = H.PI * (1.05f + i * 0.05f), rr = 0.85f + TileArt.Hash(i, 5) * 0.1f;
            float bx = x + MathF.Cos(ang) * 34 * rr, by = y + MathF.Sin(ang) * 14 * rr;
            c.Fill = H.Col("#9a968a"); c.FillCircle(bx, by, 1.4f + TileArt.Hash(i, 9)); c.Fill = H.Col("#2a2824"); c.FillCircle(bx, by - 0.2f, 0.5f);
        }
        // arms like bell ropes, each ending in a clapper of a fist
        c.StrokeStyle = "#3a4a48"; c.LineWidth = 3.4f;
        c.BeginPath(); c.MoveTo(x + f * 10, y); c.QuadraticCurveTo(x + f * 22, y + 14, j["ropeL"].X, j["ropeL"].Y + 8); c.StrokePath();
        c.BeginPath(); c.MoveTo(x - f * 12, y); c.QuadraticCurveTo(x - f * 26, y + 16, j["ropeR"].X, j["ropeR"].Y + 10); c.StrokePath();
        c.Stroke = H.Rgba(150, 140, 110, 0.4f); c.LineWidth = 0.8f;
        c.BeginPath(); c.MoveTo(x + f * 10, y - 1); c.QuadraticCurveTo(x + f * 22, y + 13, j["ropeL"].X, j["ropeL"].Y + 7); c.StrokePath();
        foreach (var (rx, ry) in new[] { (j["ropeL"].X, j["ropeL"].Y + 8), (j["ropeR"].X, j["ropeR"].Y + 10) })
        { c.FillStyle = "#4a5852"; c.FillCircle(rx, ry + 1, 3.6f); c.FillStyle = "#6a6a5a"; c.FillCircle(rx - 1, ry, 1.2f); }
        // the head under its rotten hood
        var hd = j["head"];
        c.FillStyle = "#56645e"; c.BeginPath(); c.Arc(hd.X, hd.Y, 7, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.25f); c.BeginPath(); c.Arc(hd.X, hd.Y, 7, 0.2f, H.PI - 0.2f); c.FillPath();
        c.FillStyle = "#1c2624";
        c.BeginPath(); c.MoveTo(hd.X + f * 7, hd.Y - 2); c.QuadraticCurveTo(hd.X + f * 5, hd.Y - 10, hd.X - f * 2, hd.Y - 9); c.QuadraticCurveTo(hd.X - f * 10, hd.Y - 6, hd.X - f * 9, hd.Y + 6); c.LineTo(hd.X - f * 4, hd.Y + 8); c.QuadraticCurveTo(hd.X - f * 2, hd.Y - 4, hd.X + f * 7, hd.Y - 2); c.ClosePath(); c.FillPath();
        c.FillStyle = "#0a0f10"; c.FillRect(hd.X + f * 2 - (f < 0 ? 3 : 0), hd.Y - 2, 3, 2); c.FillRect(hd.X + f * 1.5f - (f < 0 ? 4 : 0), hd.Y + 3, 4, 1.2f);
        bool angry = mode is "charge" or "windup" or "attack" or "slam";
        c.Fill = angry ? H.Rgba(200, 240, 255, 0.95f) : H.Rgba(150, 190, 200, 0.6f); c.FillCircle(hd.X + f * 3.5f, hd.Y - 1, 0.8f);
        if (angry) c.Glow(hd.X + f * 3.5f, hd.Y - 1, 5, H.Rgba(160, 230, 255, 0.6f));
        // the buoy cage grown into its back, its light still blinking
        var bu = j["buoy"];
        bool bb = K("buoy").broken;
        c.FillStyle = bb ? "#4a2a20" : "#8a3a2a";
        c.BeginPath(); c.MoveTo(bu.X - 8, bu.Y + 8); c.LineTo(bu.X - 3, bu.Y - 9); c.LineTo(bu.X + 3, bu.Y - 9); c.LineTo(bu.X + 8, bu.Y + 8); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.Poly(new[] { new Vector2(bu.X + 1, bu.Y - 9), new Vector2(bu.X + 3, bu.Y - 9), new Vector2(bu.X + 8, bu.Y + 8), new Vector2(bu.X + 3, bu.Y + 8) });
        c.StrokeStyle = bb ? "#2a1a14" : "#3a2018"; c.LineWidth = 0.9f;
        for (int i = 0; i < 3; i++) c.Line(bu.X - 7 + i * 0.6f, bu.Y + 6 - i * 5, bu.X + 7 - i * 0.6f, bu.Y + 6 - i * 5);
        c.Fill = H.Rgba(255, 255, 255, 0.12f); c.FillRect(bu.X - 3, bu.Y - 8, 1, 15);
        c.FillStyle = "#2a2622"; c.FillRect(bu.X - 1.5f, bu.Y - 13, 3, 4);
        bool blinkOn = !bb && (anim % 2.4f) < 0.5f;
        c.FillStyle = bb ? "#3a3020" : blinkOn ? "#ffe0a0" : "#c0a060"; c.FillCircle(bu.X, bu.Y - 14, 2.4f);
        if (blinkOn) c.Glow(bu.X, bu.Y - 14, 14, H.Rgba(255, 210, 140, 0.5f));
    }
    public override Props Serialize() => null;
}

// ---------------------------------------------------------------- Undertow (roaming system)
public class UndertowSystem
{
    public string room;
    float moveT = 10, bellT;
    public bool driven, dead;
    public HashSet<string> ripples = new();
    int from = 1;
    public Undertow live;
    // The rooms it travels: the sea row's waist-deep water, marked `sea` in the room data.
    static string[] SEA => sea ??= Data.Rooms.Where(r => r.Raw.B("sea")).Select(r => r.Id).ToArray();
    static string[] sea;

    public bool Active()
    {
        if (dead || driven || G.act < 2) return false;
        string ph = Tide.PhaseId();
        return (ph == "rising" && Tide.PhaseProgress() > 0.55f) || ph == "high";
    }
    // The sea rooms either side along the sea row, however wide each room is.
    public List<string> Neighbours(string id)
    {
        var r = Data.Room[id];
        return SEA.Where(o => o != id && Data.Room[o].Tidal && (Data.Room[o].Col + Data.Room[o].W == r.Col || r.Col + r.W == Data.Room[o].Col)).ToList();
    }
    public void DriveOff() { driven = true; room = null; live = null; }
    public void OnPhase(string ph) { if (ph == "low") { driven = false; ripples.Clear(); } if (ph == "ebbing" || ph == "low") room = null; }
    public void Update(float dt)
    {
        var p = G.player;
        if (!Active()) { room = null; live = null; return; }
        if (room == null)
        {
            // Rises out of the sea at the outer reef.
            room = "reef"; moveT = 14; from = 1;
        }
        ripples.Add(room);
        var here = G.room;
        if (live != null && (live.gone || live.room != here))
        {
            if (live.gone && !driven) moveT = 0;
            live = null;
        }
        if (live == null)
        {
            moveT -= dt;
            if (moveT <= 0)
            {
                var nb = Neighbours(room);
                string next = H.Pick(nb);
                if (p != null && SEA.Contains(here.Id))
                {
                    // drift toward the player
                    var best = nb.OrderBy(a => Math.Abs(Data.Room[a].Col - here.Col)).First();
                    if (H.Rand() < 0.7f) next = best;
                }
                int s = Math.Sign(Data.Room[room].Col - Data.Room[next].Col);
                from = s != 0 ? s : 1;
                room = next; moveT = H.Rand(14, 22);
            }
            // Enter the player's room: never in a Bell Room, never where a boss still stands, never within 10 s of a load.
            if (room == here.Id && !here.BellRoom && G.sinceLoad > 10 && !p.dead && !G.ents.Any(e => e is Body b && b.boss && !b.gone)) SpawnLive(here);
        }
        // The bell is always audible before it arrives.
        bellT -= dt;
        if (bellT <= 0 && live == null)
        {
            var r = Data.Room[room];
            int dcol = r.Col - here.Col, drow = r.Row - here.Row;
            int dist = Math.Abs(dcol) + Math.Abs(drow);
            bellT = 2.6f;
            if (dist <= 3) Snd.Buoy(0.5f / (1 + dist * 1.3f), H.Clamp(dcol * 0.6f, -1, 1));
        }
    }
    void SpawnLive(RoomDef r)
    {
        // Enter from the side it came from, under the surface.
        float side = from > 0 ? r.Px + r.Pw - 40 : r.Px + 40;
        float surf = World.RoomSurface(r, side);
        if (!float.IsFinite(surf)) return;
        float floor = r.Py + r.Ph;
        if (floor - surf < 3 * H.TILE) return;
        float y = MathF.Min(surf + 2.2f * H.TILE, floor - 3 * H.TILE);
        var spec = Props.Empty();
        spec.Set("x", side); spec.Set("y", y); spec.Set("key", "undertow"); spec.Set("dir", -from);
        var b = new Undertow(spec, r);
        live = b;
        G.ents.Add(b);
        Snd.Buoy(0.8f, from > 0 ? 0.8f : -0.8f);
        H.ToastMsg("A buoy bell clangs close. The Undertow is in the water with you.", 3);
        G.AddSwell(3);
    }
}

// ---------------------------------------------------------------- Abbot of Salt
public class Abbot : Body
{
    public bool started;
    float slamX;
    public Abbot(Props spec, RoomDef room) : base(spec, room)
    {
        type = "abbot"; boss = true;
        w = 36; h = 66;
        x = spec.F("x") - w / 2; y = spec.F("y") - h;
        var a = AddKnot("armL", "arm", "armL", 6, 6); a.plated = a.basePlated = true;
        var b = AddKnot("armR", "arm", "armR", 6, 6); b.plated = b.basePlated = true;
        var c = AddKnot("crown", "crown", "crown", 8, 7, 2); c.plated = c.basePlated = true;
        mode = "idle"; awakeMode = "hunt";
        stepUp = false;
    }
    public override void SetPose(string p) { }
    static Vector2 V(float x, float y) => new(x, y);
    public override Dictionary<string, Vector2> Skeleton()
    {
        float f = facing, x = Cx(), gy = Feet();
        float raise = mode == "windup" ? -12 : mode == "recover" ? 10 : 0;
        return new()
        {
            ["crown"] = V(x + f * 2, gy - 44),
            ["armL"] = V(x + f * 16, gy - 46 + raise * 0.4f),
            ["armR"] = V(x - f * 14, gy - 44),
            ["head"] = V(x + f * 6, gy - 58),
            ["hand"] = V(x + f * 26, gy - 40 + raise),
        };
    }
    public override void Hear(float hx, float hy, float d, float r, string src) { }
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        if (!started) Start();
        // His plates fall only when he strikes the kilns.
        if (k.plated) { Snd.Tink(Pan()); var kp = KnotPos(k).Value; FX.Sparks(kp.X, kp.Y, 4); return false; }
        return base.DamageKnot(k, dmg, src);
    }
    public override void DamageFlesh(float d, float hx, float hy, string src) { if (!started) Start(); Snd.Tink(Pan()); }
    public void Start() { started = true; mode = "hunt"; Snd.Toll(); G.bossBar = this; H.ToastMsg("The Abbot of Salt rises from his kilns."); }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown")
        {
            gone = true; dead = true; mode = "gone";
            FX.Salt(Cx(), Cy(), 80); G.Shake(10); Snd.Toll();
            G.OnAbbotKilled(this);
        }
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (!started)
        {
            if (p != null && H.Dist(p.Cx(), p.Cy(), Cx(), Cy()) < 17 * H.TILE && Phys.Los(p.Cx(), p.Cy(), Cx(), y + 10)) Start();
            return;
        }
        cool -= dt;
        if (mode == "windup")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0)
            {
                mode = "recover"; atkT = 1.2f;
                float sx = slamX;
                Snd.Thud(); Snd.Rumble(); G.Shake(7);
                FX.Salt(sx, Feet(), 30);
                if (p != null && MathF.Abs(p.Cx() - sx) < 34 && p.y + p.h > Feet() - 40) p.Hurt(3, this);
                foreach (var e in G.ents.ToArray())
                {
                    if (e is Kiln kiln && !kiln.broken && MathF.Abs(kiln.Cx() - sx) < 34)
                    {
                        kiln.Shatter();
                        var k = knots.Find(k => k.plated);
                        if (k != null) { k.plated = false; H.ToastMsg("The kiln bursts and a salt plate falls from the Abbot!"); }
                    }
                }
            }
            return;
        }
        if (mode == "sweep")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0)
            {
                mode = "recover"; atkT = 0.8f;
                Snd.Gaff(); FX.Salt(Cx() + facing * 40, Feet() - 4, 12);
                if (p != null && MathF.Abs(p.Cx() - Cx()) < 64 && p.y + p.h > Feet() - 14) p.Hurt(2, this);
            }
            return;
        }
        if (mode == "recover") { atkT -= dt; if (atkT <= 0) mode = "hunt"; return; }
        if (p == null) return;
        float dx = p.Cx() - Cx();
        int s = (int)H.Sign(dx); facing = s != 0 ? s : 1;
        vx = MathF.Abs(dx) > 40 ? facing * 28 : 0;
        if (cool <= 0)
        {
            if (MathF.Abs(dx) < 52 && H.Rand() < 0.5f)
            {
                mode = "sweep"; atkT = 0.8f; cool = 2.0f;
                foreach (var k in knots) if (!k.broken) k.flare = 0.8f;
                Voices.Windup(this);
            }
            else
            {
                mode = "windup"; atkT = Wind(1.2f); cool = Cool(2.6f);
                slamX = H.Clamp(p.Cx(), room.Px + 40, room.Px + room.Pw - 40);
                foreach (var k in knots) if (!k.broken) k.flare = 1.2f;
                Voices.Windup(this);
            }
        }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        float x = Cx(), gy = Feet(), f = facing;
        // the Abbot: a vast robe gone stiff with salt, armour of crystal grown over the shoulders and chest
        float br = MathF.Sin(anim * 1.4f) * 0.6f;
        c.Fill = H.Rgba(0, 0, 0, 0.4f); c.BeginPath(); c.Ellipse(x, gy, 22, 2.4f, 0, 0, H.PI * 2); c.FillPath();
        var robe = new List<Vector2> { new(x - 14, gy - 50 + br), new(x + 14, gy - 50 + br), new(x + 19, gy) };
        for (int i = 1; i < 9; i++) robe.Add(new Vector2(x + 19 - i * 38f / 9, gy - (i % 2 == 0 ? 0 : 2.5f + TileArt.Hash(i, 3) * 2)));
        robe.Add(new Vector2(x - 19, gy));
        c.FillStyle = "#4a3f36"; c.Poly(robe.ToArray());
        c.Fill = H.Rgba(0, 0, 0, 0.25f);
        c.BeginPath(); c.MoveTo(x - 19 * f, gy); c.LineTo(x - 14 * f, gy - 50 + br); c.LineTo(x - 6 * f, gy - 50 + br); c.LineTo(x - 8 * f, gy); c.FillPath();
        c.Stroke = H.Rgba(0, 0, 0, 0.3f); c.LineWidth = 0.8f;
        for (int i = -2; i <= 2; i++) c.Line(x + i * 5, gy - 40, x + i * 6.5f, gy - 2);
        // salt creeping up from the hem
        c.Fill = H.Rgba(225, 222, 212, 0.55f);
        for (int i = 0; i < 12; i++) { float sx = x - 17 + TileArt.Hash(i, 21) * 34, sy = gy - 2 - TileArt.Hash(i, 22) * 14; c.FillCircle(sx, sy, 0.8f + TileArt.Hash(i, 23) * 1.4f); }
        // crystal armour: faceted plates on the shoulders and across the chest
        void Crystal(float cx, float cy, float w2, float h2, float lean)
        {
            c.FillStyle = "#d8d0c0"; c.Poly(new[] { new Vector2(cx - w2, cy), new Vector2(cx + lean, cy - h2), new Vector2(cx + w2, cy) });
            c.Fill = H.Rgba(255, 255, 255, 0.45f); c.Poly(new[] { new Vector2(cx - w2 * 0.5f, cy), new Vector2(cx + lean, cy - h2), new Vector2(cx, cy) });
            c.Fill = H.Rgba(120, 110, 100, 0.35f); c.Poly(new[] { new Vector2(cx + w2 * 0.4f, cy), new Vector2(cx + lean, cy - h2), new Vector2(cx + w2, cy) });
        }
        c.FillStyle = "#c8c0b0"; c.FillRect(x - 15, gy - 52 + br, 30, 8); c.FillRect(x - 10, gy - 31 + br, 20, 4);
        c.Fill = H.Rgba(255, 255, 255, 0.3f); c.FillRect(x - 15, gy - 52 + br, 30, 1.5f);
        Crystal(x - 13, gy - 51 + br, 3, 9, -1.5f); Crystal(x - 8, gy - 51 + br, 2.4f, 6, -0.5f); Crystal(x + 9, gy - 51 + br, 2.6f, 7, 0.8f); Crystal(x + 13.5f, gy - 51 + br, 3.2f, 11, 1.8f);
        Crystal(x - 6, gy - 30 + br, 2, 4, 0); Crystal(x + 4, gy - 30 + br, 2.4f, 5, 0.6f);
        float pulse = 0.5f + 0.5f * MathF.Sin(anim * 3);
        c.Glow(x + 13.5f, gy - 58 + br, 6, H.Rgba(220, 235, 255, 0.15f + 0.15f * pulse));
        // head, crusted white, under a tall mitre
        var hd = j["head"];
        c.FillStyle = "#8a9088"; c.BeginPath(); c.Arc(hd.X, hd.Y, 7, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(230, 228, 220, 0.7f); c.BeginPath(); c.Arc(hd.X - f * 1.5f, hd.Y + 1, 6, H.PI * 0.3f, H.PI * 1.1f); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.7f); c.FillRect(hd.X + f * 2.5f - 1, hd.Y - 1.5f, 2.4f, 1.6f); c.FillRect(hd.X + f * 1 - 2, hd.Y + 3, 4, 1);
        bool hunting = mode is "windup" or "attack" or "charge";
        c.Fill = hunting ? H.Rgba(255, 230, 190, 0.95f) : H.Rgba(200, 200, 190, 0.6f); c.FillCircle(hd.X + f * 3.2f, hd.Y - 0.8f, 0.8f);
        if (hunting) c.Glow(hd.X + f * 3.2f, hd.Y - 0.8f, 5, H.Rgba(255, 220, 160, 0.6f));
        c.FillStyle = "#e8e0d0"; c.BeginPath(); c.MoveTo(hd.X - 6.5f, hd.Y - 4.5f); c.QuadraticCurveTo(hd.X - 6, hd.Y - 14, hd.X, hd.Y - 19); c.QuadraticCurveTo(hd.X + 6, hd.Y - 14, hd.X + 6.5f, hd.Y - 4.5f); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.18f); c.Poly(new[] { new Vector2(hd.X, hd.Y - 19), new Vector2(hd.X + 6.5f, hd.Y - 4.5f), new Vector2(hd.X + 2, hd.Y - 4.5f) });
        c.FillStyle = "#c0a060"; c.FillRect(hd.X - 6.5f, hd.Y - 6, 13, 1.6f); c.FillRect(hd.X - 0.6f, hd.Y - 17, 1.2f, 11);
        c.FillRect(hd.X - 2.5f, hd.Y - 13, 5, 1.2f);
        // the pick arm
        var arm = j["armL"]; var hand = j["hand"];
        c.StrokeStyle = "#3a322a"; c.LineWidth = 6.4f;
        c.BeginPath(); c.MoveTo(arm.X, arm.Y); c.LineTo(hand.X, hand.Y); c.StrokePath();
        c.Stroke = H.Rgba(220, 218, 210, 0.45f); c.LineWidth = 1.2f; c.Line(arm.X, arm.Y - 2, H.Lerp(arm.X, hand.X, 0.6f), H.Lerp(arm.Y, hand.Y, 0.6f) - 2);
        c.FillStyle = "#3a322a"; c.FillCircle(arm.X, arm.Y, 3.4f);
        c.FillStyle = "#9a9a90"; c.FillCircle(hand.X, hand.Y, 3);
        float ang = mode == "windup" ? -2.4f * f : mode == "recover" ? 1.3f * f : 0.4f * f;
        c.Save(); c.Translate(hand.X, hand.Y); c.Rotate(ang);
        c.StrokeStyle = "#6a5030"; c.LineWidth = 4; c.BeginPath(); c.MoveTo(0, 4); c.LineTo(0, -36); c.StrokePath();
        c.Stroke = H.Rgba(255, 220, 170, 0.2f); c.LineWidth = 1; c.Line(-1, 2, -1, -34);
        c.FillStyle = "#4a4640"; c.FillRect(-3, -37, 6, 5);
        c.FillStyle = "#b0b8bc"; c.BeginPath(); c.MoveTo(-16, -31); c.QuadraticCurveTo(-6, -40, 0, -37); c.QuadraticCurveTo(8, -40, 18, -28); c.QuadraticCurveTo(8, -34, 0, -32); c.QuadraticCurveTo(-6, -34, -16, -31); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 255, 255, 0.4f); c.BeginPath(); c.MoveTo(-14, -32); c.QuadraticCurveTo(-6, -39, 0, -36.5f); c.LineTo(0, -35.5f); c.QuadraticCurveTo(-6, -37.5f, -14, -32); c.FillPath();
        c.Restore();
        var ar = j["armR"];
        c.StrokeStyle = "#3a322a"; c.LineWidth = 5.4f;
        c.BeginPath(); c.MoveTo(ar.X, ar.Y); c.LineTo(ar.X - f * 6, ar.Y + 18); c.StrokePath();
        c.FillStyle = "#9a9a90"; c.FillCircle(ar.X - f * 6, ar.Y + 18, 2.6f);
    }
    public override Props Serialize() => null;
}

// ---------------------------------------------------------------- The Almoner
// The brother who chose which one in ten went down to the Saint, and made sure it was never him. A bloated Shucker the
// size of three men, chained by the neck to the Refectory's long table; the doors west are chained to him too. Which
// knots break first decides the fight. Knees first, and he drags himself about, but when the tide is in he swims at
// her through the flooded hall. Elbows first, and he cannot grab or throw, but he heaves the table. The crown in his
// chest is sealed until two limbs are broken.
public class Almoner : Drowned
{
    const float SCALE = 2.8f;
    public bool started;
    readonly float anchorX, anchorY, chain, tableX0, tableX1;
    float noteT, slamT;
    public Almoner(Props spec, RoomDef room) : base(spec, room)
    {
        type = "almoner"; boss = true; voice = "shucker";
        AddKnot("kneeL", "knee", "kneeL", 7, 7);
        AddKnot("kneeR", "knee", "kneeR", 7, 7);
        AddKnot("elbowL", "elbow", "elbowL", 6, 7);
        AddKnot("elbowR", "elbow", "elbowR", 6, 7);
        AddKnot("crown", "crown", "sternum", 10, 9, 2);
        // his chain is made fast to the middle of the table
        anchorX = spec.F("x"); anchorY = room.Py + 37 * H.TILE;
        tableX0 = room.Px + 22 * H.TILE; tableX1 = room.Px + 58 * H.TILE;
        chain = 28 * H.TILE;
        pose = "";
        SetPose("seated");
        x = spec.F("x") - w / 2; y = spec.F("y") - h;
        facing = 1;
        mode = "dormant"; awakeMode = "hunt";
        stepUp = false;
    }
    public override void SetPose(string p)
    {
        if (p == pose) return;
        float bottom = y + h, mid = Cx();
        base.SetPose(p);
        w *= SCALE; h *= SCALE; x = mid - w / 2; y = bottom - h;
    }
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = base.Skeleton();
        float cx = Cx(), fy = Feet();
        foreach (var k in j.Keys.ToArray()) j[k] = new Vector2(cx + (j[k].X - cx) * SCALE, fy + (j[k].Y - fy) * SCALE);
        return j;
    }
    public void Start()
    {
        if (started) return;
        started = true; mode = "rise"; atkT = 1.4f;
        G.bossBar = this;
        Snd.Toll(); Snd.Groan(0, 0.5f); G.Shake(4);
        H.ToastMsg("The Almoner heaves himself up from the table. The chain at his neck pulls taut.", 4);
        Narr.Think("almoner");
    }
    public override void Wake(bool force = false) { if (mode == "dormant") Start(); }
    public override void Hear(float hx, float hy, float d, float r, string src) { if (mode == "dormant" && d < r) Start(); }
    public override bool Perceives() { var p = G.player; return started && p != null && !p.dead && G.room == room; }
    public override bool DamageKnot(Knot k, float dmg, string src) { Start(); return base.DamageKnot(k, dmg, src); }
    public override void DamageFlesh(float d, float hx, float hy, string src) { Start(); base.DamageFlesh(d, hx, hy, src); }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown")
        {
            ReleaseGrab();
            gone = true; dead = true; mode = "gone";
            FX.Salt(Cx(), Cy(), 90); G.Shake(10); Snd.Toll(); Snd.Rumble();
            G.OnAlmonerKilled(this);
            return;
        }
        base.OnKnotBroken(k, src);
        if (k.type == "knee" && !LegsOK()) H.ToastMsg(World.IsWater(Cx(), Feet() - 6) || Tide.PhaseId() == "high" ? "His legs give. He slides into the water, and swims." : "His legs give. He drags himself toward the water under the table.", 3);
        if (k.type == "elbow" && !ArmsOK()) H.ToastMsg("His arms hang. He gets his shoulder under the table instead.", 3);
    }
    // she is somewhere his hands can reach: on the floor or the table, within the chain
    bool Reachable(Player p) => MathF.Abs(p.Cx() - anchorX) < chain + 2 * H.TILE && p.Feet() > room.Py + 33 * H.TILE;
    bool OnTable(Player p) => p.onGround && p.Cx() > tableX0 && p.Cx() < tableX1 && MathF.Abs(p.Feet() - anchorY) < 6;
    public override void Think(float dt)
    {
        var p = G.player;
        if (p == null) return;
        // the doors west are chained to him
        noteT -= dt;
        if (noteT <= 0 && p.Cx() < room.Px + 3 * H.TILE)
        {
            noteT = 12;
            H.ToastMsg("The doors west are chained shut. The chains run back across the floor to the Almoner's collar.", 4);
        }
        if (mode == "dormant")
        {
            if (H.Dist(p.Cx(), p.Cy(), Cx(), Cy()) < 20 * H.TILE && p.Feet() > room.Py + 30 * H.TILE) Start();
            return;
        }
        if (mode == "rise") { vx = 0; atkT -= dt; if (atkT <= 0) { SetPose(LegsOK() ? "stand" : "crawl"); mode = "hunt"; } return; }
        if (mode == "heave" || mode == "slam" || mode == "throw") { Special(dt, p); return; }
        if (Blow(dt, 150, 14, 2, true)) return;
        cool -= dt;
        int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
        bool wet = World.IsWater(Cx(), Cy());
        if (!LegsOK() && wet && World.IsWater(p.Cx(), p.Cy()))
        {
            // he swims: heavy, fast, straight at her
            float dx = p.Cx() - Cx(), dy = p.Cy() - Cy(), dd = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            float sp = 78 * Menace;
            vx = H.Approach(vx, dx / dd * sp, 300 * dt); vy = H.Approach(vy, dy / dd * sp - 20, 300 * dt);
        }
        else if (Reachable(p)) Pursue(dt, p.Cx(), (LegsOK() ? 30 : 16) * Menace);
        else vx = H.Approach(vx, 0, 400 * dt);
        if (cool > 0) return;
        float d = DistToHer();
        if (!ArmsOK())
        {
            // no hands: he gets his shoulder under the table and throws it up
            if (d < 30 * H.TILE) { mode = "heave"; atkT = Wind(1.3f); cool = Cool(3.4f); foreach (var k in knots) if (!k.broken) k.flare = 1.3f; Voices.Windup(this); Snd.Groan(Pan(), 0.4f); }
            return;
        }
        if (OnTable(p) && d < 9 * H.TILE) { mode = "slam"; atkT = Wind(0.9f); cool = Cool(2.4f); foreach (var k in knots) if (!k.broken) k.flare = 0.9f; Voices.Windup(this); return; }
        if (!Reachable(p)) { mode = "throw"; atkT = Wind(0.8f); cool = Cool(2.6f); foreach (var k in knots) if (!k.broken) k.flare = 0.8f; Voices.Windup(this); return; }
        if (d < 4.5f * H.TILE * (LegsOK() ? 1 : 0.7f)) StartBlow(0.9f, 2.2f);
    }
    // the heave, the slam on the table, the thrown bowl
    void Special(float dt, Player p)
    {
        vx *= 0.8f; atkT -= dt;
        if (atkT > 0) return;
        string was = mode; mode = "recover"; atkT = 0.9f;
        if (was == "throw")
        {
            // an alms-bowl of pewter, thrown hard
            var hand = Skel()["handL"];
            float dx = p.Cx() - hand.X, t = H.Clamp(MathF.Abs(dx) / 260, 0.45f, 1.3f), g = 460;
            G.enemyShots.Add(new Shot { x = hand.X, y = hand.Y, vx = dx / t, vy = (p.Cy() - hand.Y) / t - 0.5f * g * t, grav = g, life = 2.4f, dmg = 1, src = this, kind = "bowl" });
            Snd.Gaff(Pan());
            return;
        }
        // the table jumps under his fists or his shoulder
        slamT = 0.5f;
        Snd.Thud(Pan()); Snd.Rumble(); G.Shake(was == "heave" ? 7 : 5);
        for (float tx = tableX0; tx < tableX1; tx += 24) FX.Salt(tx, anchorY, 3);
        bool onTable = OnTable(p);
        bool onFloor = p.onGround && p.Cx() > tableX0 - 3 * H.TILE && p.Cx() < tableX1 + 3 * H.TILE && p.Feet() > anchorY + 1;
        if (was == "slam" && onTable) { p.Hurt(2, this); p.vy = -280; }
        if (was == "heave")
        {
            if (onTable) { p.Hurt(1, this); p.vy = -380; }
            else if (onFloor) { p.Hurt(2, this); p.vy = -240; }
            // at High water the heave sends a wave down the hall
            if (World.IsWater(p.Cx(), p.Cy()) && MathF.Abs(p.Cx() - Cx()) < 26 * H.TILE) p.vx += H.Sign(p.Cx() - Cx()) * 320;
        }
    }
    public override void Physics(float dt, bool pinned = false)
    {
        base.Physics(dt, pinned);
        // the chain: he cannot go further from the table than it lets him
        float dx = Cx() - anchorX, dy = Cy() - anchorY, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > chain) { x -= dx * (1 - chain / d); y -= dy * (1 - chain / d); vx *= 0.3f; }
    }
    public override void Update(float dt) { base.Update(dt); if (slamT > 0) slamT -= dt; }
    public override Palette Pal() => new() { robe = "#211d22", skin = "#93a092", cowl = "#171419", dark = "#0e0c10" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        float cx = Cx(), fy = Feet();
        // the chain, from the ring in the table to his collar, slack or taut
        var neck = Skel()["neck"];
        float sag = MathF.Max(0, 40 - H.Dist(neck.X, neck.Y, anchorX, anchorY) * 0.12f);
        c.StrokeStyle = "#4a4038"; c.LineWidth = 2.2f;
        c.BeginPath(); c.MoveTo(anchorX, anchorY);
        for (int i = 1; i <= 12; i++) { float t = i / 12f; c.LineTo(H.Lerp(anchorX, neck.X, t), H.Lerp(anchorY, neck.Y, t) + MathF.Sin(t * H.PI) * sag); }
        c.StrokePath();
        c.Stroke = H.Rgba(160, 130, 100, 0.35f); c.LineWidth = 0.8f;
        for (int i = 1; i < 12; i += 2) { float t = i / 12f; float lx = H.Lerp(anchorX, neck.X, t), ly = H.Lerp(anchorY, neck.Y, t) + MathF.Sin(t * H.PI) * sag; c.Line(lx - 1.5f, ly - 1, lx + 1.5f, ly + 1); }
        c.FillStyle = "#5a4c40"; c.FillCircle(anchorX, anchorY - 1, 3);
        if (slamT > 0) { c.Fill = H.Rgba(220, 215, 200, slamT * 0.6f); c.FillRect(tableX0, anchorY - 6 - slamT * 8, tableX1 - tableX0, 3); }
        // the body: drawn at a man's size and scaled up to his
        var scaled = _skel;
        _skel = base.Skeleton();
        c.Save(); c.Translate(cx, fy); c.Scale(SCALE, SCALE); c.Translate(-cx, -fy);
        DrawHuman(c, Pal());
        var j = _skel;
        // the belly that the brothers' bread made
        var st = j["sternum"]; var hp = j["hip"];
        c.FillStyle = Shade(Pal().robe, 0.55f); c.BeginPath(); c.Ellipse((st.X + hp.X) / 2 + facing * 2.2f, (st.Y + hp.Y) / 2 + 1, 6.5f, 5.2f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(255, 255, 255, 0.05f); c.BeginPath(); c.Ellipse((st.X + hp.X) / 2 + facing * 3.4f, (st.Y + hp.Y) / 2 - 1, 3, 2.4f, 0, 0, H.PI * 2); c.FillPath();
        // the iron collar, and the alms-purse at his belt
        var nk = j["neck"];
        c.StrokeStyle = "#3a3430"; c.LineWidth = 1.6f; c.BeginPath(); c.Arc(nk.X, nk.Y + 0.5f, 2.4f, 0, H.PI * 2); c.StrokePath();
        c.FillStyle = "#4a3a28"; c.FillCircle(hp.X - facing * 3, hp.Y + 1.5f, 1.8f);
        c.Fill = H.Rgba(200, 170, 90, 0.5f); c.FillRect(hp.X - facing * 3 - 0.6f, hp.Y + 0.5f, 1.2f, 0.8f);
        c.Restore();
        _skel = scaled;
    }
    public override Props Serialize() => null;
}

// ---------------------------------------------------------------- The Precentor
// The Choir's master of song, a Bellmouth grown into the organ he played. He hangs from its case over the loft, out
// of reach, spits brine, and screams: the scream wakes the dead in the stalls and throws down anyone it finds in the
// open (the two piers are the only shelter). Break his jaw while he draws breath and the scream chokes in him; strike
// the loft's tuned bell at the pitch of the salt in his throat and the salt shatters, he hangs stunned, and the plates
// over the pipes that root him in the organ fall away until it grows back. Two broken limbs open the crown in his chest.
public class Precentor : Body
{
    public bool started, throatSalted = true;
    public int pitch;
    float saltT, screamCool = 4, spitCool = 2, stunT, hintT;
    int hints;
    static readonly string[] NOTE = { "high and thin", "a little lower", "low" };
    public Precentor(Props spec, RoomDef room) : base(spec, room)
    {
        type = "precentor"; boss = true; voice = "bellmouth";
        AddKnot("jaw", "jaw", "jaw", 5, 8);
        var a = AddKnot("pipeL", "pipe", "shoulderL", 6, 7); a.plated = a.basePlated = true;
        var b = AddKnot("pipeR", "pipe", "shoulderR", 6, 7); b.plated = b.basePlated = true;
        AddKnot("crown", "crown", "sternum", 9, 9, 2);
        // he hangs from the organ's case: spec y is his lowest point
        w = 56; h = 80; x = spec.F("x") - w / 2; y = spec.F("y") - h;
        pitch = (int)H.Rand(0, 3) % 3;
        mode = "dormant"; awakeMode = "idle";
        stepUp = false;
    }
    public override void SetPose(string p) { }
    public override bool Perceives() { var p = G.player; return started && p != null && !p.dead && G.room == room; }
    public override void Hear(float hx, float hy, float d, float r, string src) { if (mode == "dormant" && src != "scream" && d < r) Start(); }
    public override void Wake(bool force = false) { if (mode == "dormant") Start(); }
    public void Start()
    {
        if (started) return;
        started = true; mode = "idle"; screamCool = 3;
        G.bossBar = this;
        Snd.Scream(Pan()); G.Shake(4);
        H.ToastMsg("The Precentor lifts his head out of the organ, and the salt in his throat begins to ring.", 4);
        Narr.Think("precentor");
    }
    static Vector2 V(float x, float y) => new(x, y);
    public override Dictionary<string, Vector2> Skeleton()
    {
        float cx = Cx(), top = y;
        float sway = MathF.Sin(anim * 0.9f) * 2;
        float gape = mode == "inhale" ? H.Clamp(1.8f - atkT, 0, 1.8f) * 6 : mode == "stun" ? 8 : 0;
        return new()
        {
            ["shoulderL"] = V(cx - 22, top + 12), ["shoulderR"] = V(cx + 22, top + 12),
            ["sternum"] = V(cx + sway * 0.3f, top + 30),
            ["head"] = V(cx + sway, top + 50), ["jaw"] = V(cx + sway, top + 66 + gape),
            ["elbowL"] = V(cx - 32, top + 38 + sway), ["handL"] = V(cx - 27, top + 66),
            ["elbowR"] = V(cx + 32, top + 38 - sway), ["handR"] = V(cx + 27, top + 66),
            ["hip"] = V(cx, top + 40), ["neck"] = V(cx + sway * 0.6f, top + 40),
        };
    }
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        Start();
        // the jaw is only open while he draws breath, or hangs stunned
        if (k.id == "jaw" && mode != "inhale" && mode != "stun") { Snd.Tink(Pan()); var kp = KnotPos(k).Value; FX.Sparks(kp.X, kp.Y, 3); return false; }
        return base.DamageKnot(k, dmg, src);
    }
    public override void DamageFlesh(float d, float hx, float hy, string src) { Start(); base.DamageFlesh(d, hx, hy, src); }
    public override void Flinch() { }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown")
        {
            gone = true; dead = true; mode = "gone";
            FX.Salt(Cx(), Cy(), 90); G.Shake(10); Snd.Toll(); Snd.Rumble();
            G.OnPrecentorKilled(this);
            return;
        }
        if (k.id == "jaw")
        {
            if (mode == "inhale") { mode = "stun"; stunT = 3; H.ToastMsg("His jaw breaks in the middle of the breath. The scream chokes in him.", 3); }
            else H.ToastMsg("His jaw hangs broken. He cannot scream any more.", 3);
        }
        if (k.type == "pipe") H.ToastMsg("A pipe tears out of him with a shriek of air.", 2.5f);
        var crown = K("crown");
        if (!crown.broken && !IsSealed(crown)) Narr.Think("crown");
    }
    // the loft's tuned bell, rung anywhere in the room
    public void HearBell(int lvl)
    {
        if (!started) { Start(); return; }
        if (!throatSalted || gone) return;
        if (lvl != pitch) { if (hints < 4) { hints++; H.ToastMsg("Wrong note. The salt in his throat rings " + NOTE[pitch] + ".", 3); } return; }
        throatSalted = false; saltT = 16;
        mode = "stun"; stunT = 4.5f;
        foreach (var kn in knots) if (kn.type == "pipe" && !kn.broken) kn.plated = false;
        var jp = KnotPos(K("jaw")).Value;
        FX.Salt(jp.X, jp.Y - 8, 40); Snd.Crystal(Pan()); Snd.Crack(Pan()); G.Shake(5);
        H.ToastMsg("The salt in his throat sings and shatters! He hangs stunned, and the plates over his pipes fall away.", 4);
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (p == null) return;
        if (mode == "dormant")
        {
            if (p.Feet() > room.Py + 30 * H.TILE && H.Dist(p.Cx(), p.Cy(), Cx(), Cy()) < 30 * H.TILE) Start();
            return;
        }
        if (!throatSalted)
        {
            saltT -= dt;
            if (saltT <= 0)
            {
                throatSalted = true;
                pitch = (pitch + 1 + (H.Rand() < 0.5f ? 0 : 1)) % 3;
                foreach (var kn in knots) if (kn.type == "pipe" && !kn.broken) kn.plated = true;
                H.ToastMsg("Salt grows back in his throat. It rings a new note: " + NOTE[pitch] + ".", 4);
            }
        }
        if (mode == "stun") { stunT -= dt; if (stunT <= 0) { mode = "idle"; screamCool = 3; } return; }
        if (mode == "inhale")
        {
            atkT -= dt;
            K("jaw").flare = 0.3f;
            hintT -= dt;
            if (hintT <= 0) { hintT = 0.6f; Snd.Bell(new[] { 392, 330, 262 }[pitch], 0.12f); }
            if (atkT > 0) return;
            mode = "idle"; screamCool = Cool(7f);
            Scream(p);
            return;
        }
        screamCool -= dt; spitCool -= dt;
        if (!K("jaw").broken && screamCool <= 0)
        {
            mode = "inhale"; atkT = Wind(1.8f); hintT = 0;
            Snd.Inhale(Pan());
            if (hints < 3 && throatSalted) { hints++; H.ToastMsg("He draws breath. The salt in his throat rings " + NOTE[pitch] + ".", 2.5f); }
            return;
        }
        if (spitCool <= 0)
        {
            // a gout of brine at her, arcing down from the organ
            spitCool = Cool(K("jaw").broken ? 1.6f : 2.6f);
            var jp = KnotPos(K("jaw")).Value;
            float dx = p.Cx() - jp.X, t = H.Clamp(MathF.Abs(dx) / 240, 0.4f, 1.2f), g = 420;
            G.enemyShots.Add(new Shot { x = jp.X, y = jp.Y, vx = dx / t, vy = (p.Cy() - jp.Y) / t - 0.5f * g * t, grav = g, life = 2.4f, dmg = 1, src = this, kind = "brine" });
            Snd.Splash(0.2f, Pan());
        }
    }
    void Scream(Player p)
    {
        var jp = KnotPos(K("jaw")).Value;
        if (!throatSalted)
        {
            // with the salt gone from his throat, all that comes out is a croak
            Snd.Groan(Pan(), 0.4f);
            H.ToastMsg("He screams, and only a croak comes out.", 2);
            return;
        }
        Snd.Scream(Pan()); G.Shake(6); G.AddSwell(2);
        // the scream wakes two of the dead in the stalls, the nearest to her
        foreach (var e in G.ents.OfType<Body>().Where(b => b != this && b.mode == "dormant" && !b.corpse && !b.gone).OrderBy(b => H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy())).Take(2).ToArray())
        { e.lastHeard = new Vector2(p.Cx(), p.Cy()); e.Wake(true); }
        Noise.Emit(Cx(), Cy(), 20, "scream");
        // and it throws down anyone it finds in the open
        if (!p.inBellRoom && Phys.Los(jp.X, jp.Y, p.Cx(), p.Cy()))
        {
            p.Hurt(2, this);
            p.vx += H.Sign(p.Cx() - jp.X) * 260; p.vy = -180;
        }
    }
    public override void Physics(float dt, bool pinned = false) { vx = vy = 0; }
    public override Palette Pal() => new() { robe = "#d8d4c8", skin = "#8e9e98", cowl = null, dark = "#6a6a62" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        float cx = Cx(), top = y;
        // the organ's pipes, driven down into his shoulders
        foreach (var side in new[] { "L", "R" })
        {
            var sh = j["shoulder" + side];
            var kn = K("pipe" + side);
            float px = sh.X + (side == "L" ? -10 : 10);
            if (kn.broken) { c.StrokeStyle = "#4a463e"; c.LineWidth = 5; c.Line(px, top - 40, px + (side == "L" ? -3 : 3), top - 6); continue; }
            c.StrokeStyle = "#7a7260"; c.LineWidth = 6; c.Line(px, top - 48, sh.X, sh.Y);
            c.Stroke = H.Rgba(255, 240, 200, 0.25f); c.LineWidth = 1.2f; c.Line(px - 2, top - 48, sh.X - 2, sh.Y);
            if (kn.plated) { c.Fill = H.Rgba(225, 222, 212, 0.75f); for (int i = 0; i < 6; i++) c.FillCircle(sh.X + TileArt.Hash(i, 7) * 8 - 4, sh.Y - 4 - i * 2.5f, 1.6f + TileArt.Hash(i, 9) * 1.2f); }
        }
        // a robe gone stiff and white, hanging from the organ's case
        var st = j["sternum"];
        c.FillStyle = "#b8b2a4";
        c.Poly(new[] { V(cx - 26, top), V(cx + 26, top), V(cx + 20, top + 30), V(st.X + 13, top + 52), V(st.X - 13, top + 52), V(cx - 20, top + 30) });
        c.Fill = H.Rgba(0, 0, 0, 0.22f); c.Poly(new[] { V(cx + 6, top), V(cx + 26, top), V(cx + 20, top + 30), V(st.X + 13, top + 52), V(st.X + 4, top + 52) });
        c.Stroke = H.Rgba(60, 55, 48, 0.4f); c.LineWidth = 0.8f; for (int i = -2; i <= 2; i++) c.Line(cx + i * 7, top + 4, st.X + i * 4, top + 50);
        // long arms, hanging
        foreach (var side in new[] { "L", "R" }) { DrawLimb(c, j["shoulder" + side], j["elbow" + side], 3.4f, "#8a8478"); DrawLimb(c, j["elbow" + side], j["hand" + side], 2.6f, "#9a9488"); }
        // the head: a bell of bone, and its jaw hanging open
        var hd = j["head"]; var jw = j["jaw"];
        c.FillStyle = "#a8aea6"; c.BeginPath(); c.Ellipse(hd.X, hd.Y, 15, 13, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(255, 255, 255, 0.12f); c.BeginPath(); c.Ellipse(hd.X - 4, hd.Y - 4, 7, 5, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#1a1416"; c.BeginPath(); c.Ellipse(hd.X, (hd.Y + jw.Y) / 2 + 3, 9, (jw.Y - hd.Y) / 2 + 2, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#98a098"; c.BeginPath(); c.MoveTo(hd.X - 14, hd.Y + 4); c.LineTo(jw.X - 10, jw.Y); c.LineTo(jw.X + 10, jw.Y); c.LineTo(hd.X + 14, hd.Y + 4); c.LineTo(hd.X + 9, hd.Y + 8); c.LineTo(jw.X, jw.Y - 4); c.LineTo(hd.X - 9, hd.Y + 8); c.ClosePath(); c.FillPath();
        bool hunting = mode is "inhale" or "idle";
        c.Fill = hunting ? H.Rgba(255, 230, 190, 0.9f) : H.Rgba(180, 180, 170, 0.5f);
        c.FillCircle(hd.X - 6, hd.Y - 3, 1.2f); c.FillCircle(hd.X + 6, hd.Y - 3, 1.2f);
        // the salt in his throat, glowing the colour of its note
        if (throatSalted)
        {
            var col = pitch switch { 0 => H.Rgba(190, 225, 255, 0.9f), 1 => H.Rgba(200, 250, 210, 0.9f), _ => H.Rgba(255, 215, 150, 0.9f) };
            float glow = 0.35f + (mode == "inhale" ? 0.45f : 0.15f) * (0.5f + 0.5f * MathF.Sin(anim * 9));
            c.Glow(jw.X, jw.Y - 6, 14, new Color(col.R, col.G, col.B, glow * 0.5f));
            c.Fill = col;
            for (int i = 0; i < 5; i++) { float sx = jw.X - 6 + i * 3, sy = jw.Y - 8 + TileArt.Hash(i, 11) * 3; c.Poly(new[] { V(sx - 1.6f, sy + 2), V(sx, sy - 3 - TileArt.Hash(i, 12) * 3), V(sx + 1.6f, sy + 2) }); }
        }
    }
    public override Props Serialize() => null;
}

public class Kiln : Ent
{
    public bool broken;
    public Kiln(Props spec, RoomDef room)
    {
        this.spec = spec; this.room = room; type = "kiln"; key = spec.S("key");
        x = spec.F("x") - 12; y = spec.F("y") - 26; w = 24; h = 26;
    }
    public override float Cx() => x + 12;
    public override float Cy() => y + 13;
    public void Shatter()
    {
        broken = true;
        FX.Spawn(Cx(), Cy(), 30, new FXOpt { color = "#ff9040", glow = true, smin = 60, smax = 200 });
        Snd.Crack();
        Fire.Ignite(H.FloorI(Cx() / H.TILE), H.FloorI((y + 20) / H.TILE), 5);
    }
    public override LightSrc Light() => broken ? null : new LightSrc { x = Cx(), y = y + 14, r = 70, i = 0.7f, color = "255,120,40" };
    public override void Draw(Canvas c)
    {
        // a salt kiln: a brick dome on a plinth, a chimney, an iron door and the fire behind its grate
        float cx = x + w / 2, by = y + h;
        c.FillStyle = broken ? "#2a2420" : "#5a463a";
        c.BeginPath(); c.MoveTo(x, by); c.LineTo(x, y + 6); c.QuadraticCurveTo(cx, y - 6, x + w, y + 6); c.LineTo(x + w, by); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.3f);
        for (int r = 0; r < (int)(h / 4); r++) { float ry = y + 6 + r * 4; c.FillRect(x, ry, w, 0.6f); for (float bx = x + (r % 2) * 3; bx < x + w; bx += 6) c.FillRect(bx, ry, 0.6f, 4); }
        c.Fill = H.Rgba(255, 220, 180, 0.08f); c.FillRect(x, y + 6, 2, h - 6);
        c.FillStyle = "#3a2e26"; c.FillRect(x + w - 7, y - 10, 5, 12); c.FillRect(x + w - 8, y - 11, 7, 2);
        c.FillStyle = "#4a3e34"; c.FillRect(x - 2, by - 3, w + 4, 3);
        if (!broken)
        {
            float fl = 0.85f + 0.15f * MathF.Sin(G.Now * 9 + x);
            c.FillStyle = "#1a1210"; c.BeginPath(); c.MoveTo(x + 6, by - 3); c.LineTo(x + 6, y + 13); c.QuadraticCurveTo(cx, y + 9, x + w - 6, y + 13); c.LineTo(x + w - 6, by - 3); c.ClosePath(); c.FillPath();
            c.Fill = new Color(1, 0.5f * fl, 0.16f * fl); c.FillRect(x + 7, y + 13, w - 14, by - y - 16);
            c.Fill = new Color(1.4f, 1.0f * fl, 0.5f * fl, 0.9f); c.FillRect(x + 9, by - 9, w - 18, 4);
            c.FillStyle = "#2a2420"; for (float gx = x + 8; gx < x + w - 7; gx += 2.6f) c.FillRect(gx, y + 12, 0.9f, by - y - 15);
            c.Glow(cx, by - 8, 18, new Color(1, 0.5f, 0.2f, 0.25f * fl));
            for (int i = 0; i < 3; i++) { float ph = (G.Now * 0.5f + i / 3f + x * 0.01f) % 1; c.Glow(x + w - 4.5f + MathF.Sin(ph * 5 + i) * 2, y - 12 - ph * 18, 2 + ph * 4, H.Rgba(120, 110, 100, 0.25f * (1 - ph))); }
        }
        else
        {
            c.FillStyle = "#120e0c"; c.BeginPath(); c.MoveTo(x + 4, by - 3); c.LineTo(x + 5, y + 6); c.LineTo(x + 10, y + 2); c.LineTo(x + 14, y + 8); c.LineTo(x + 19, y + 3); c.LineTo(x + w - 4, by - 3); c.ClosePath(); c.FillPath();
            c.FillStyle = "#3a2e26"; c.FillRect(x + 2, by - 4, 4, 2); c.FillRect(x + w - 7, by - 3, 5, 2);
        }
    }
    public override Props Serialize() { var p = Props.Empty(); p.Set("broken", broken); return p; }
    public override void Restore(Props s) { broken = s.B("broken"); }
}

// ---------------------------------------------------------------- The Shoal
public class Shoal : Body
{
    float rot;
    bool started;
    public Shoal(Props spec, RoomDef room) : base(spec, room)
    {
        type = "shoal"; boss = true;
        w = 46; h = 40; x = spec.F("x") - 23; y = spec.F("y") - 40;
        for (int i = 0; i < 8; i++) AddKnot("k" + i, "shoal", "k" + i, 4, 5);
        mode = "idle"; awakeMode = "hunt";
        stepUp = false;
    }
    public override void SetPose(string p) { }
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = new Dictionary<string, Vector2>(); float x = Cx(), y = Cy();
        for (int i = 0; i < 8; i++) { float a = rot + i * H.PI / 4; j["k" + i] = new Vector2(x + MathF.Cos(a) * 17, y + MathF.Sin(a) * 15); }
        return j;
    }
    public override void Hear(float hx, float hy, float d, float r, string src) { if (!started) Start(); }
    public void Start() { started = true; mode = "hunt"; G.bossBar = this; H.ToastMsg("Five bodies fused into one rolling mass. The Shoal."); Snd.Scream(); }
    public override bool DamageKnot(Knot k, float d, string s) { if (!started) Start(); return base.DamageKnot(k, d, s); }
    public override void OnKnotBroken(Knot k, string src)
    {
        // Each broken knot sheds a smaller body.
        var spec = Props.Empty();
        spec.Set("x", Cx()); spec.Set("y", Feet()); spec.Set("key", "shed" + H.Uid()); spec.Set("pose", "standing");
        var sp = new Shucker(spec, room);
        sp.K("kneeL").broken = true; sp.K("kneeR").broken = true; sp.SetPose("crawl"); sp.mode = "hunt";
        float shedHp = Diff.Knot(2, false);
        foreach (var q in sp.knots) { q.max = shedHp; if (!q.broken) q.hp = shedHp; }
        sp.shed = true;
        G.ents.Add(sp);
        if (knots.All(q => q.broken))
        {
            gone = true; dead = true; FX.Salt(Cx(), Cy(), 50); G.Shake(6);
            G.OnShoalKilled();
        }
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (!started) { if (p != null && H.Dist(p.Cx(), p.Cy(), Cx(), Cy()) < 10 * H.TILE) Start(); return; }
        if (p == null) return;
        float dx = p.Cx() - Cx();
        int s = (int)H.Sign(dx); facing = s != 0 ? s : 1;
        float sp = 55 + knots.Count(k => k.broken) * 6;
        vx = H.Approach(vx, facing * sp, 120 * dt);
        rot += vx * dt / 17;
        if (hitWall != 0 && onGround) vy = -200;
        if (H.RectsOverlap(x, y, w, h, p.x, p.y, p.w, p.h) && !p.Invuln()) { p.Hurt(2, this); p.vx = facing * 220; p.vy = -160; }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        float x = Cx(), y = Cy();
        // the Shoal: a knot of drowned fish wheeling around a dark heart, never quite the same shape twice
        c.Glow(x, y, 34, H.Rgba(10, 20, 24, 0.6f));
        c.FillStyle = "#24282a";
        c.BeginPath(); for (int i = 0; i <= 24; i++) { float a = i / 24f * H.PI * 2, rr = 13 + MathF.Sin(a * 3 + rot * 2) * 2 + MathF.Sin(a * 5 - rot * 3) * 1.4f; float px = x + MathF.Cos(a) * rr, py = y + MathF.Sin(a) * rr; if (i == 0) c.MoveTo(px, py); else c.LineTo(px, py); } c.FillPath();
        for (int i = 0; i < 38; i++)
        {
            float ring = 0.45f + TileArt.Hash(i, 3) * 0.6f, spd = (i % 2 == 0 ? 1 : -0.7f) * (1.2f - ring * 0.5f);
            float a = rot * spd * 1.3f + i * 2.39f;
            float rx = 22 * ring, ry = 19 * ring + MathF.Sin(rot + i) * 2;
            float fx = x + MathF.Cos(a) * rx, fy = y + MathF.Sin(a) * ry;
            float heading = a + (spd > 0 ? H.PI / 2 : -H.PI / 2);
            c.Save(); c.Translate(fx, fy); c.Rotate(heading);
            float sz = 1.6f + TileArt.Hash(i, 7) * 1.4f, wag = MathF.Sin(rot * 9 + i) * 0.5f;
            c.Fill = i % 5 == 0 ? H.Col("#9aa8a4") : H.Col("#6c7a78");
            c.BeginPath(); c.Ellipse(0, 0, sz * 1.6f, sz * 0.7f, 0, 0, H.PI * 2); c.FillPath();
            c.Poly(new[] { new Vector2(-sz * 1.3f, 0), new Vector2(-sz * 2.4f, -sz * 0.7f + wag), new Vector2(-sz * 2.4f, sz * 0.7f + wag) });
            c.Fill = H.Rgba(230, 240, 240, 0.8f); c.FillCircle(sz * 0.9f, -sz * 0.15f, 0.35f);
            c.Restore();
        }
        // what holds it together: the pale shapes of limbs and a face, turning in the middle
        c.FillStyle = "#7f8b80";
        for (int i = 0; i < 5; i++) { float a = rot + i * 1.25f; c.BeginPath(); c.Arc(x + MathF.Cos(a) * 9, y + MathF.Sin(a) * 7, 2.4f, 0, H.PI * 2); c.FillPath(); }
        c.Fill = H.Rgba(180, 230, 240, 0.6f + 0.3f * MathF.Sin(rot * 2)); c.FillCircle(x, y, 1.6f);
        c.Glow(x, y, 8, H.Rgba(150, 220, 240, 0.25f));
    }
    public override Props Serialize() => null;
}
