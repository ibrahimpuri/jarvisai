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
    List<string> Neighbours(string id)
    {
        var r = Data.Room[id];
        return SEA.Where(o => o != id && Data.Room[o].Row == r.Row && Math.Abs(Data.Room[o].Col - r.Col) == 1).ToList();
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
            // Enter the player's room: never in a Bell Room, never within 10 s of a load.
            if (room == here.Id && !here.BellRoom && G.sinceLoad > 10 && !p.dead) SpawnLive(here);
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
