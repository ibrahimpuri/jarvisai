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
                mode = "windup"; atkT = 0.8f; cool = boss ? 2.2f : 3;
                foreach (var k in knots) if (!k.broken) k.flare = 0.8f;
                Snd.Groan(Pan(), 0.4f);
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
        // the drowned bell-ringer, fused to a channel buoy
        c.FillStyle = "#1c2a2e";
        c.BeginPath(); c.Ellipse(x, y, 34, 14, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(60, 80, 84, 0.35f);
        c.BeginPath(); c.Ellipse(x - f * 4, y - 6, 26, 5, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#23353a";
        for (int i = 0; i < 5; i++) { c.BeginPath(); c.Arc(x - f * (24 - i * 11), y + 10 + MathF.Sin(anim * 3 + i) * 2, 5, 0, H.PI * 2); c.FillPath(); }
        // arms like bell ropes
        c.StrokeStyle = "#3a4a48"; c.LineWidth = 3;
        c.BeginPath(); c.MoveTo(x + f * 10, y); c.QuadraticCurveTo(x + f * 22, y + 14, j["ropeL"].X, j["ropeL"].Y + 8); c.StrokePath();
        c.BeginPath(); c.MoveTo(x - f * 12, y); c.QuadraticCurveTo(x - f * 26, y + 16, j["ropeR"].X, j["ropeR"].Y + 10); c.StrokePath();
        // head
        var hd = j["head"];
        c.FillStyle = "#56645e"; c.BeginPath(); c.Arc(hd.X, hd.Y, 7, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#0a0f10"; c.FillRect(hd.X + f * 2, hd.Y - 2, 3, 2);
        // the buoy cage on its back
        var bu = j["buoy"];
        c.FillStyle = K("buoy").broken ? "#4a2a20" : "#8a3a2a";
        c.BeginPath(); c.MoveTo(bu.X - 7, bu.Y + 8); c.LineTo(bu.X, bu.Y - 10); c.LineTo(bu.X + 7, bu.Y + 8); c.FillPath();
        c.FillStyle = "#c0a060"; c.BeginPath(); c.Arc(bu.X, bu.Y - 2, 3, 0, H.PI * 2); c.FillPath();
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
    static readonly string[] SEA = { "reef", "pier", "tithe", "cloister", "nave", "priors" };

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
    bool started;
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
    void Start() { started = true; mode = "hunt"; Snd.Toll(); G.bossBar = this; H.ToastMsg("The Abbot of Salt rises from his kilns."); }
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
                Snd.Groan(Pan(), 0.4f);
            }
            else
            {
                mode = "windup"; atkT = 1.2f; cool = 2.6f;
                slamX = H.Clamp(p.Cx(), room.Px + 40, room.Px + room.Pw - 40);
                foreach (var k in knots) if (!k.broken) k.flare = 1.2f;
                Snd.Groan(Pan(), 0.5f);
            }
        }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        float x = Cx(), gy = Feet(), f = facing;
        // robe
        c.FillStyle = "#4a3f36";
        c.BeginPath(); c.MoveTo(x - 18, gy); c.LineTo(x - 14, gy - 50); c.LineTo(x + 14, gy - 50); c.LineTo(x + 18, gy); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.2f);
        c.BeginPath(); c.MoveTo(x - 18 * f, gy); c.LineTo(x - 14 * f, gy - 50); c.LineTo(x - 6 * f, gy - 50); c.LineTo(x - 8 * f, gy); c.FillPath();
        // salt armour
        c.FillStyle = "#d8d0c0";
        c.FillRect(x - 15, gy - 52, 30, 8);
        c.FillRect(x - 10, gy - 30, 20, 4);
        c.Fill = H.Rgba(255, 255, 255, 0.3f); c.FillRect(x - 15, gy - 52, 30, 1.5f);
        // head + mitre
        var hd = j["head"];
        c.FillStyle = "#8a9088"; c.BeginPath(); c.Arc(hd.X, hd.Y, 7, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#e8e0d0"; c.BeginPath(); c.MoveTo(hd.X - 6, hd.Y - 5); c.LineTo(hd.X, hd.Y - 18); c.LineTo(hd.X + 6, hd.Y - 5); c.FillPath();
        // pick arm
        var arm = j["armL"]; var hand = j["hand"];
        c.StrokeStyle = "#3a322a"; c.LineWidth = 6;
        c.BeginPath(); c.MoveTo(arm.X, arm.Y); c.LineTo(hand.X, hand.Y); c.StrokePath();
        c.FillStyle = "#3a322a"; c.FillCircle(arm.X, arm.Y, 3); c.FillCircle(hand.X, hand.Y, 3);
        float ang = mode == "windup" ? -2.4f * f : mode == "recover" ? 1.3f * f : 0.4f * f;
        c.Save(); c.Translate(hand.X, hand.Y); c.Rotate(ang);
        c.StrokeStyle = "#6a5030"; c.LineWidth = 4; c.BeginPath(); c.MoveTo(0, 0); c.LineTo(0, -36); c.StrokePath();
        c.StrokeStyle = "#b0b8bc"; c.LineWidth = 5; c.BeginPath(); c.MoveTo(-14, -34); c.QuadraticCurveTo(0, -42, 16, -30); c.StrokePath();
        c.Restore();
        var ar = j["armR"];
        c.StrokeStyle = "#3a322a"; c.LineWidth = 5;
        c.BeginPath(); c.MoveTo(ar.X, ar.Y); c.LineTo(ar.X - f * 6, ar.Y + 18); c.StrokePath();
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
        c.FillStyle = broken ? "#2a2420" : "#5a4a3a";
        c.FillRect(x, y, w, h);
        if (!broken)
        {
            c.Fill = H.Rgba(0, 0, 0, 0.25f); c.FillRect(x, y + h - 4, w, 4);
            float fl = 0.85f + 0.15f * MathF.Sin(G.Now * 9 + x);
            c.Fill = new Color(1, 0.54f * fl, 0.19f * fl); c.FillRect(x + 7, y + 12, 10, 8);
            c.Fill = H.Rgba(255, 220, 140, 0.8f); c.FillRect(x + 9, y + 16, 6, 3);
            c.FillStyle = "#6a5a4a"; c.FillRect(x - 2, y - 3, w + 4, 4);
        }
        else { c.FillStyle = "#1a1410"; c.FillRect(x + 4, y, 16, 12); }
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
    void Start() { started = true; mode = "hunt"; G.bossBar = this; H.ToastMsg("Five bodies fused into one rolling mass. The Shoal."); Snd.Scream(); }
    public override bool DamageKnot(Knot k, float d, string s) { if (!started) Start(); return base.DamageKnot(k, d, s); }
    public override void OnKnotBroken(Knot k, string src)
    {
        // Each broken knot sheds a smaller body.
        var spec = Props.Empty();
        spec.Set("x", Cx()); spec.Set("y", Feet()); spec.Set("key", "shed" + H.Uid()); spec.Set("pose", "standing");
        var sp = new Shucker(spec, room);
        sp.K("kneeL").broken = true; sp.K("kneeR").broken = true; sp.SetPose("crawl"); sp.mode = "hunt";
        foreach (var q in sp.knots) { q.max = 2; if (!q.broken) q.hp = 2; }
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
        c.FillStyle = "#3a3a34";
        c.BeginPath(); c.Arc(x, y, 20, 0, H.PI * 2); c.FillPath();
        c.StrokeStyle = "#7f8b80"; c.LineWidth = 2.5f;
        for (int i = 0; i < 10; i++)
        {
            float a = rot * 1.0f + i * 0.63f;
            c.BeginPath(); c.MoveTo(x + MathF.Cos(a) * 10, y + MathF.Sin(a) * 10); c.LineTo(x + MathF.Cos(a + 0.3f) * 23, y + MathF.Sin(a + 0.3f) * 22); c.StrokePath();
        }
        c.FillStyle = "#7f8b80";
        for (int i = 0; i < 5; i++) { float a = rot + i * 1.25f; c.BeginPath(); c.Arc(x + MathF.Cos(a) * 14, y + MathF.Sin(a) * 12, 3.2f, 0, H.PI * 2); c.FillPath(); }
    }
    public override Props Serialize() => null;
}
