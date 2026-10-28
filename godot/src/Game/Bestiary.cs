// The newer kinds of the drowned, in the three tiers of the night. Every one is a body held up by salt knots, broken
// by the same rules as the rest; each changes the question of which knot to break first.
//
//   start (Act I, the Wrackline and the Saltworks): Saltgull, Netter, Kilnman
//   mid   (Act II, the Cloister, the Choir and the Ribvault): Bloater, Toller, Cantor, Ossuary, Kite
//   end   (Act III, the Gullet and the Skull): Drowned Diver, Deepwalker, Wick
//
// Earlier kinds keep turning up later; rooms can also bring later kinds into earlier places from a given act
// (`fromAct` on a spawn). See godot/docs/ENEMIES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

// One way of walking her down and striking, shared by the newer humanoid kinds.
public abstract class Drowned : Body
{
    protected Drowned(Props spec, RoomDef room) : base(spec, room) { }
    public override bool Humanoid => true;
    protected void FromPose(Props spec, string awake = "hunt")
    {
        string sp = spec.S("pose");
        if (sp == "seated") { SetPose("seated"); mode = "dormant"; }
        else if (sp == "lying") { SetPose("lying"); mode = "dormant"; }
        else mode = awake;
        awakeMode = awake;
    }
    // Rising from dormancy; true while it is still getting up.
    protected bool Rising(float dt)
    {
        if (mode == "dormant") return true;
        if (mode != "rise") return false;
        atkT -= dt;
        if (atkT <= 0) { SetPose(LegsOK() ? "stand" : "crawl"); mode = "hunt"; }
        return true;
    }
    // A blow already begun: the wind-up, the strike, a grab, the recovery. True while busy with it.
    protected bool Blow(float dt, float lunge, float reach, int dmg, bool grabs)
    {
        var p = G.player;
        if (mode == "windup")
        {
            vx *= 0.85f; atkT -= dt;
            if (atkT <= 0) { mode = "attack"; atkT = 0.3f; vx = facing * lunge; }
            return true;
        }
        if (mode == "attack")
        {
            atkT -= dt;
            var hb = HitBox();
            if (p != null && !p.Invuln() && H.RectsOverlap(hb.x - reach, hb.y, hb.w + reach * 2, hb.h, p.x, p.y, p.w, p.h))
            {
                if (grabs && CanAttack() && p.TryGrab(this)) { mode = "grab"; atkT = 0.4f; Snd.Grab(); return true; }
                p.Hurt(dmg, this); mode = "recover"; atkT = 0.7f; return true;
            }
            if (atkT <= 0) { mode = "recover"; atkT = 0.7f; }
            return true;
        }
        if (mode == "grab")
        {
            vx = 0;
            if (p == null || p.grabbedBy != this) { mode = "recover"; atkT = 0.6f; return true; }
            atkT -= dt;
            if (atkT <= 0) { atkT = Cool(1.0f); p.Hurt(1, this); }
            return true;
        }
        if (mode == "recover") { vx *= 0.8f; atkT -= dt; if (atkT <= 0) mode = "hunt"; return true; }
        return false;
    }
    // Where it is going: her, if it can see her; the last noise, for a while; or nowhere.
    protected Vector2? Seek(float dt)
    {
        var p = G.player;
        if (Perceives()) { target = new Vector2(p.Cx(), p.Cy()); alert = 3; }
        else if (lastHeard != null && alert > 0) { target = lastHeard; alert -= dt; }
        else target = null;
        return target;
    }
    protected void StartBlow(float windup, float cooldown)
    {
        var p = G.player;
        mode = "windup"; atkT = Wind(windup) + Legend.Hesitation; cool = Cool(cooldown);
        int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
        foreach (var k in knots) if (!k.broken) k.flare = 0.6f;
        Voices.Windup(this);
    }
    protected float DistToHer() { var p = G.player; return p == null ? 1e9f : H.Dist(Cx(), Cy(), p.Cx(), p.Cy()); }
}

// ======================================================================= the start
// ---------------------------------------------------------------- Saltgull
// Gulls that drowned in the Brine's water and came back crusted white. They roost on rock and rigging, lift together
// at a noise or a lamp, and come down on her in long swoops. One knot each, at the wing: anything that touches it
// brings the bird down. They are quick, and there are always several.
public class Saltgull : Body
{
    float phase, swoopT, swoopCool;
    Vector2 home;
    public Saltgull(Props spec, RoomDef room) : base(spec, room)
    {
        type = "saltgull"; voice = "gull";
        AddKnot("wing", "wing", "wing", 1, 3.5f);
        w = 9; h = 6; x = spec.F("x") - 4.5f; y = spec.F("y") - 6;
        home = new Vector2(Cx(), y);
        phase = H.Rand(0, 6.3f); swoopCool = H.Rand(1.5f, 3.5f);
        mode = spec.S("pose") == "flying" ? "fly" : "dormant"; awakeMode = "fly";
    }
    public override Dictionary<string, Vector2> Skeleton()
    {
        float flap = mode == "fly" ? MathF.Sin(anim * 14) * 3 : 0;
        return new() { ["wing"] = new(Cx() - facing * 1, y + 2 - flap * 0.3f), ["head"] = new(Cx() + facing * 4, y + 1), ["body"] = new(Cx(), Cy()) };
    }
    public override void Hear(float hx, float hy, float d, float r, string src)
    {
        if (mode == "dormant" && d < r) Wake(true);
    }
    // one lifts and the flock lifts with it
    public override void Wake(bool force = false)
    {
        if (mode != "dormant" || corpse) return;
        mode = "fly"; vy = -80; Voices.Alert(this);
        foreach (var e in G.ents.OfType<Saltgull>()) if (e != this && e.mode == "dormant" && H.Dist(e.Cx(), e.Cy(), Cx(), Cy()) < 10 * H.TILE) { e.mode = "fly"; e.vy = -80; }
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (mode == "dormant")
        {
            // a lamp pointed at the roost, or anyone walking under it, puts them up
            if (p != null && !p.inBellRoom && (H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 3.5f * H.TILE || (p.LanternLit() && p.focusing && p.BeamHits(Cx(), Cy())))) Wake(true);
            return;
        }
        if (mode != "fly" || p == null) return;
        swoopCool -= dt;
        Vector2 aim;
        if (swoopT > 0)
        {
            // the swoop: straight down at where she was, and away
            swoopT -= dt;
            aim = new Vector2(p.Cx(), p.Cy());
            var hb = HitBox();
            if (!p.Invuln() && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h)) { p.Hurt(1, this); swoopT = 0; swoopCool = H.Rand(2.5f, 4); vy = -160; }
            float sp = 230 * Menace;
            var dv = (aim - new Vector2(Cx(), Cy())).Normalized();
            vx = H.Approach(vx, dv.X * sp, 900 * dt); vy = H.Approach(vy, dv.Y * sp, 900 * dt);
        }
        else
        {
            // circling over her, each bird on its own slant
            aim = new Vector2(p.Cx() + MathF.Sin(anim * 1.1f + phase) * 70, p.Cy() - 60 + MathF.Cos(anim * 1.7f + phase) * 14);
            var dv = aim - new Vector2(Cx(), Cy());
            vx = H.Approach(vx, Math.Clamp(dv.X * 2, -140, 140), 400 * dt);
            vy = H.Approach(vy, Math.Clamp(dv.Y * 2, -120, 120), 400 * dt);
            if (swoopCool <= 0 && Phys.Los(Cx(), Cy(), p.Cx(), p.Cy()) && !p.inBellRoom) { swoopT = 0.9f; Voices.Windup(this); K("wing").flare = 0.6f; }
        }
        if (MathF.Abs(vx) > 5) facing = vx > 0 ? 1 : -1;
    }
    public override void Physics(float dt, bool pinned = false)
    {
        if (mode is "fly") { Phys.Move(this, dt); var r = room; x = Math.Clamp(x, r.Px + 2, r.Px + r.Pw - w - 2); y = Math.Clamp(y, r.Py + 2, r.Py + r.Ph - h - 2); return; }
        base.Physics(dt, pinned);
    }
    public override void OnKnotBroken(Knot k, string src) { corpse = true; Down(src); }
    public override bool Perceives() => mode == "fly";
    // a bird keeps a bird's size, flying or fallen
    public override void SetPose(string p) { pose = p; }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        float cx = Cx(), cy = Cy(), f = facing;
        bool down = mode == "downed";
        float flap = mode == "fly" ? MathF.Sin(anim * 14) : -0.6f;
        var white = burnT > 0 ? H.Col("#3a3028") : H.Col("#cfd2c8");
        // wings: long, crusted, the near one in front
        c.Fill = white.Darkened(0.35f);
        c.Poly(new[] { new Vector2(cx - f, cy - 1), new Vector2(cx - f * 9, cy - 1 - flap * 6), new Vector2(cx - f * 5, cy + 1) });
        c.Fill = down ? white.Darkened(0.2f) : white;
        c.BeginPath(); c.Ellipse(cx, cy, 4.2f, down ? 1.6f : 2.4f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = white.Lightened(0.1f);
        c.Poly(new[] { new Vector2(cx + f, cy - 1), new Vector2(cx + f * 6, cy - 2 - flap * 7), new Vector2(cx + f * 3, cy + 1) });
        // head, a yellowed beak, an eye gone white
        c.Fill = white; c.FillCircle(cx + f * 3.8f, cy - 1.3f, 1.7f);
        c.Fill = H.Col("#b89a48"); c.Poly(new[] { new Vector2(cx + f * 5, cy - 1.6f), new Vector2(cx + f * 7.6f, cy - 0.8f), new Vector2(cx + f * 5, cy - 0.6f) });
        c.Fill = H.Col("#eef4f0"); c.FillCircle(cx + f * 4.2f, cy - 1.8f, 0.5f);
        // salt in the feathers
        c.Fill = H.Rgba(240, 245, 250, 0.6f); c.FillRect(cx - 2, cy - 2, 1, 1); c.FillRect(cx + 1, cy - 1.5f, 1, 1);
    }
}

// ---------------------------------------------------------------- Netter
// A Hithe-wife drowned in her own nets. She throws them: a net that lands on Maren holds her until she tears it, cuts
// it with the gaff, or it burns. Then she comes in with the gutting knife. Break her throwing arm (the right elbow)
// and there are no more nets.
public class Netter : Drowned
{
    float netCool;
    public Netter(Props spec, RoomDef room) : base(spec, room)
    {
        type = "netter"; voice = "shucker";
        AddKnot("kneeL", "knee", "kneeL", 3);
        AddKnot("kneeR", "knee", "kneeR", 3);
        AddKnot("elbowL", "elbow", "elbowL", 3);
        AddKnot("elbowR", "elbow", "elbowR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        FromPose(spec);
        netCool = H.Rand(0.5f, 1.5f);
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (Rising(dt)) return;
        if (mode == "throw")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0 && p != null)
            {
                mode = "recover"; atkT = 0.6f;
                // a net, thrown high so it comes down over her
                float dx = p.Cx() - Cx(), t = Math.Clamp(MathF.Abs(dx) / 220, 0.35f, 0.9f), g = 500;
                G.enemyShots.Add(new Shot { x = Cx(), y = y + 6, vx = dx / t, vy = (p.Cy() - (y + 6)) / t - 0.5f * g * t, grav = g, life = 2, dmg = 0, src = this, kind = "net" });
                Snd.Gaff();
            }
            return;
        }
        if (Blow(dt, 220 * Menace, 2, 1, false)) return;
        cool -= dt; netCool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        float d = DistToHer();
        bool sees = Perceives();
        float spd = (K("kneeL").broken || K("kneeR").broken ? 30 : 52) * Menace;
        if (pose == "crawl") spd = 70 * Menace;
        // she likes to throw from a little way off, and only then come in
        if (sees && netCool <= 0 && ArmOK("R") && p.netT <= 0 && d > 3 * H.TILE && d < 10 * H.TILE)
        {
            mode = "throw"; atkT = Wind(0.8f); netCool = Cool(4.5f);
            int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
            K("elbowR").flare = 0.8f;
            Voices.Windup(this);
            return;
        }
        Pursue(dt, tgt.Value.X, spd);
        if (sees && d < 30 && cool <= 0 && CanAttack()) StartBlow(0.45f / Menace, 1.4f);
    }
    public override Palette Pal() => burnT > 0 ? new Palette { robe = "#1a1410", skin = "#3a2a20", cowl = "#141010" } : new Palette { robe = "#3e4a3a", skin = "#8a958a", cowl = "#4a4436", dark = "#1e241c" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        var j = Skel();
        // the net she drowned in, still knotted round her shoulders, trailing
        c.Stroke = H.Rgba(150, 132, 96, 0.7f); c.LineWidth = 0.45f;
        var sh = (j["shoulderL"] + j["shoulderR"]) / 2;
        for (int i = 0; i < 4; i++) c.Line(sh.X - 4 + i * 2.5f, sh.Y, sh.X - 5 + i * 2.6f - facing * 2, j["hip"].Y + 4 + MathF.Sin(anim + i) * 0.8f);
        for (int i = 0; i < 3; i++) { float yy = sh.Y + 3 + i * 3; c.Line(sh.X - 5, yy, sh.X + 4, yy + 0.5f); }
        // and a gutting knife
        if (ArmOK("L") && mode != "downed") { var hl = j["handL"]; c.Stroke = H.Col("#a8aca4"); c.LineWidth = 0.8f; c.Line(hl.X, hl.Y, hl.X + facing * 4, hl.Y - 1.5f); }
    }
}

// ---------------------------------------------------------------- Kilnman
// A stoker who kept the kilns burning, and keeps them burning still. Fire does not touch him: it is in him, and his
// shovel throws embers that catch on timber, rigging and spilled oil. Get him into water and the heat goes out of
// him with a scream of steam, and his knots crack like hot glass dropped in a bucket.
public class Kilnman : Drowned
{
    public bool doused;
    float emberT;
    public Kilnman(Props spec, RoomDef room) : base(spec, room)
    {
        type = "kilnman"; voice = "saltwright";
        AddKnot("kneeL", "knee", "kneeL", 4);
        AddKnot("kneeR", "knee", "kneeR", 4);
        AddKnot("shoulderL", "shoulder", "shoulderL", 4);
        AddKnot("shoulderR", "shoulder", "shoulderR", 4);
        AddKnot("crown", "crown", "sternum", 5, 5, 2);
        FromPose(spec);
    }
    public override void Burn() { if (doused) base.Burn(); else { FX.Embers(Cx(), Cy(), 10); Snd.Fire(Pan()); } }
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        if (src == "fire" && !doused) return false;
        // doused, every knot is brittle
        if (doused && src is "rivet" or "gaff" or "harpoon" or "bolt") dmg *= 2;
        return base.DamageKnot(k, dmg, src);
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (!doused && World.IsWater(Cx(), Cy()))
        {
            doused = true; stun = 1.2f;
            FX.Spawn(Cx(), Cy(), 30, new FXOpt { color = "#d8dcdc", grav = -80, smin = 10, smax = 60, lmin = 0.6f, lmax = 1.4f });
            Snd.At("kiln:steam", 1.4f, s => s.NoiseSweep(1.2f, 0.9f, "bandpass", 4000, 900, 1.4f, 0.01f), Cx(), Cy(), 0.9f, 2);
            Voices.Windup(this);
        }
        if (!doused && H.Rand() < dt * 10) FX.Embers(Cx() + H.Rand(-4, 4), Cy() - 4, 1);
        if (Rising(dt)) return;
        if (mode == "attack" && !doused)
        {
            // where the shovel comes down, embers catch
            emberT -= dt;
            if (emberT <= 0) { emberT = 0.15f; Fire.Ignite(H.FloorI((Cx() + facing * 16) / H.TILE), H.FloorI((Feet() - 4) / H.TILE), 4); }
        }
        if (Blow(dt, 160 * Menace, 6, 2, false)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        float spd = (doused ? 30 : 40) * Menace * (LegsOK() ? 1 : 0.6f);
        Pursue(dt, tgt.Value.X, spd);
        if (Perceives() && DistToHer() < 38 && cool <= 0 && CanAttack()) StartBlow(0.8f, 2.2f);
    }
    public override LightSrc Light() => doused || gone || mode == "downed" ? null : new LightSrc { x = Cx(), y = Cy() - 3, r = 46, i = 0.55f, color = "255,140,60" };
    public override Palette Pal() => doused ? new Palette { robe = "#2a2622", skin = "#6a6a64", cowl = null, dark = "#141210" } : new Palette { robe = "#4a3020", skin = "#9a7a62", cowl = null, dark = "#2a160c" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        var j = Skel();
        var st = j["sternum"];
        // the coals inside him, through the split leather apron
        if (!doused && mode != "downed") { c.Glow(st.X, st.Y + 2, 7, H.Rgba(255, 120, 40, 0.5f + 0.2f * MathF.Sin(anim * 5))); c.Fill = H.Col("#ffb060"); c.FillRect(st.X - 1, st.Y + 1, 2, 3); }
        // the shovel
        if (ArmOK("R") && mode != "downed") { var hr = j["handR"]; c.Stroke = H.Col("#5a4030"); c.LineWidth = 1; c.Line(hr.X, hr.Y, hr.X + facing * 8, hr.Y + 3); c.Fill = H.Col("#3a3a3a"); c.FillRect(hr.X + facing * 7 - 2, hr.Y + 2, 4, 3); }
    }
}

// ======================================================================= the middle
// ---------------------------------------------------------------- Bloater
// The drowned that floated. At High tide it lifts off the floor and drifts to her through the water, belly first;
// its crown is in the belly. Break it and the belly bursts, and a swarm of Driftlings with it. Burn it and nothing
// comes out. On a dry floor it can only drag itself.
public class Bloater : Drowned
{
    bool burst;
    public Bloater(Props spec, RoomDef room) : base(spec, room)
    {
        type = "bloater"; voice = "shucker";
        AddKnot("elbowL", "elbow", "elbowL", 3);
        AddKnot("elbowR", "elbow", "elbowR", 3);
        AddKnot("crown", "crown", "hip", 5, 6);
        FromPose(spec);
        if (mode == "dormant") SetPose("lying");
    }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown" && !burst && burnT <= 0 && src != "fire")
        {
            // the belly goes, and what was growing in it
            burst = true;
            Gore.Bleed(Cx(), Cy(), Cx() - facing);
            FX.Spawn(Cx(), Cy(), 30, new FXOpt { color = "#6a7a6a", smin = 30, smax = 140 });
            Snd.Crack(Pan()); G.Shake(2);
            for (int i = 0; i < 5; i++)
            {
                var s = Props.Empty(); s.Set("x", Cx() + H.Rand(-8, 8)); s.Set("y", Cy() + H.Rand(-6, 6)); s.Set("key", "burst" + H.Uid());
                G.ents.Add(new Driftling(s, room));
            }
        }
        base.OnKnotBroken(k, src);
    }
    public override void Think(float dt)
    {
        var p = G.player;
        bool wet = World.IsWater(Cx(), Cy());
        if (mode == "dormant")
        {
            // the water lifts it
            if (wet || (p != null && DistToHer() < 2.5f * H.TILE)) Wake(true);
            return;
        }
        if (mode == "rise") { atkT -= dt; if (atkT <= 0) { SetPose(wet ? "stand" : "crawl"); mode = "hunt"; } return; }
        if (Blow(dt, 90, 2, 1, true)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (wet)
        {
            // drifting, belly up, toward her
            if (pose != "stand") SetPose("stand");
            var aim = tgt ?? new Vector2(Cx(), y - 20);
            var dv = (aim - new Vector2(Cx(), Cy()));
            vx = H.Approach(vx, Math.Clamp(dv.X, -1, 1) * 45 * Menace, 120 * dt);
            vy = H.Approach(vy, Math.Clamp(dv.Y, -1, 1) * 35 * Menace - 8, 120 * dt);
            if (MathF.Abs(vx) > 5) facing = vx > 0 ? 1 : -1;
        }
        else
        {
            if (pose != "crawl") SetPose("crawl");
            if (tgt != null) Pursue(dt, tgt.Value.X, 22 * Menace); else vx *= 0.9f;
        }
        if (tgt != null && Perceives() && DistToHer() < 22 && cool <= 0 && CanAttack()) StartBlow(0.6f, 2);
    }
    public override void Physics(float dt, bool pinned = false)
    {
        if (World.IsWater(Cx(), Cy()) && mode is "hunt" or "windup" or "attack" or "recover" or "grab") { Phys.Move(this, dt); return; }
        base.Physics(dt, pinned);
    }
    public override Palette Pal() => burnT > 0 ? new Palette { robe = "#1a1410", skin = "#3a2a20" } : new Palette { robe = "#4a524a", skin = "#a8b4a4", cowl = null, dark = "#2a302a" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        if (burst || mode == "downed") return;
        var j = Skel();
        var b = j["hip"];
        // the belly, tight as a drum, veined, something moving inside it
        float r = 7 + MathF.Sin(anim * 1.4f) * 0.6f;
        c.Fill = H.Col(burnT > 0 ? "#2a2018" : "#b8c4b0");
        c.BeginPath(); c.Ellipse(b.X + facing * 1.5f, b.Y - 3, r, r * 0.9f, 0, 0, H.PI * 2); c.FillPath();
        c.Stroke = H.Rgba(90, 110, 100, 0.6f); c.LineWidth = 0.4f;
        c.BeginPath(); c.MoveTo(b.X - 4, b.Y - 6); c.QuadraticCurveTo(b.X, b.Y - 2, b.X + 5, b.Y - 4); c.StrokePath();
        float m = MathF.Sin(anim * 3.3f) * 2;
        c.Fill = H.Rgba(60, 80, 70, 0.4f); c.FillCircle(b.X + m, b.Y - 3 + m * 0.4f, 1.6f);
    }
}

// ---------------------------------------------------------------- Toller
// A bell-roper who still carries the handbell he rang the Hours with. When he sees her he rings it, and every body in
// the room that can still stand gets up and comes to the sound. Break the arm that holds the bell (the right elbow)
// and he is only a drowned man.
public class Toller : Drowned
{
    float ringCool;
    public Toller(Props spec, RoomDef room) : base(spec, room)
    {
        type = "toller"; voice = "bellmouth";
        AddKnot("kneeL", "knee", "kneeL", 3);
        AddKnot("kneeR", "knee", "kneeR", 3);
        AddKnot("elbowL", "elbow", "elbowL", 3);
        AddKnot("elbowR", "elbow", "elbowR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        FromPose(spec, "idle");
    }
    void Ring()
    {
        Snd.At("toller:ring", 2.2f, s => { for (int i = 0; i < 6; i++) s.Bell(H.Rand(1180, 1240), 0.25f, 1.2f, i * 0.28f); }, Cx(), Cy(), 1f, 2);
        G.Shake(1.5f); G.AddSwell(1.5f);
        var at = new Vector2(Cx(), Cy());
        foreach (var e0 in G.ents.ToArray())
            if (e0 is Body e && e != this && !e.corpse && e.Alive())
            {
                e.lastHeard = at; e.alert = 6;
                if (e.mode == "dormant") e.Wake(true);
            }
        Noise.Emit(Cx(), Cy(), 14, "bell");
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (Rising(dt)) return;
        ringCool -= dt;
        if (mode == "ring")
        {
            vx = 0; atkT -= dt;
            if (!ArmOK("R")) { mode = "hunt"; return; }
            if (atkT <= 0) { Ring(); mode = "hunt"; ringCool = Cool(9); }
            return;
        }
        if (Blow(dt, 200 * Menace, 2, 1, true)) return;
        cool -= dt;
        bool sees = Perceives();
        if (sees && ringCool <= 0 && ArmOK("R"))
        {
            mode = "ring"; atkT = Wind(1.1f);
            int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
            K("elbowR").flare = 1.1f;
            Voices.Windup(this);
            return;
        }
        if (mode == "idle") { if (sees) mode = "hunt"; else { vx *= 0.9f; return; } }
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        Pursue(dt, tgt.Value.X, (LegsOK() ? 44 : 30) * Menace);
        if (sees && DistToHer() < 30 && cool <= 0 && CanAttack()) StartBlow(0.55f, 1.6f);
    }
    public override Palette Pal() => burnT > 0 ? new Palette { robe = "#1a1410", skin = "#3a2a20", cowl = "#141010" } : new Palette { robe = "#3a3044", skin = "#8a9490", cowl = "#2a2234", dark = "#181420" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        var j = Skel();
        if (!ArmOK("R") || mode == "downed") return;
        // the handbell, green with salt, its clapper swinging
        var hr = j["handR"];
        float sw = mode == "ring" ? MathF.Sin(anim * 30) * 0.6f : MathF.Sin(anim * 2) * 0.1f;
        c.Save(); c.Translate(hr.X, hr.Y); c.Rotate(sw);
        c.Fill = H.Col("#6a7a5a"); c.Poly(new[] { new Vector2(-2, 1), new Vector2(2, 1), new Vector2(3, 5), new Vector2(-3, 5) });
        c.Fill = H.Col("#3a3a2a"); c.FillRect(-0.5f, -1.5f, 1, 2.5f);
        c.Restore();
    }
}

// ---------------------------------------------------------------- Cantor
// A singer of the Choir who never stopped. Its song finds the downed dead and grows their knots back: while it
// sings, nothing she puts down stays down. Break its jaw and the song stops. It keeps its distance, and it is not
// brave.
public class Cantor : Drowned
{
    Body singingTo;
    public Cantor(Props spec, RoomDef room) : base(spec, room)
    {
        type = "cantor"; voice = "grafted";
        AddKnot("jaw", "jaw", "jaw", 3);
        AddKnot("shoulderL", "shoulder", "shoulderL", 3);
        AddKnot("shoulderR", "shoulder", "shoulderR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        FromPose(spec, "idle");
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (Rising(dt)) return;
        if (mode == "sing")
        {
            vx *= 0.8f; atkT -= dt;
            if (K("jaw").broken || singingTo == null || singingTo.gone || singingTo.mode != "downed") { mode = "idle"; singingTo = null; return; }
            if (atkT <= 0)
            {
                singingTo.StandUp(); FX.Salt(singingTo.Cx(), singingTo.Cy(), 24);
                H.ToastMsg("The Cantor's song stands the dead back up. Break its jaw.", 2.5f);
                singingTo = null; mode = "idle"; cool = Cool(3);
            }
            return;
        }
        if (Blow(dt, 160, 2, 1, false)) return;
        cool -= dt;
        // a downed body within the song's reach, not burned and not hauled away
        if (!K("jaw").broken && cool <= 0)
        {
            var b = G.ents.OfType<Body>().Where(e => e != this && e.mode == "downed" && !e.gone && !e.corpse && !e.dragged && e.burnT <= 0 && e.knots.Count > 0 && e is not Saltgull && H.Dist(e.Cx(), e.Cy(), Cx(), Cy()) < 11 * H.TILE)
                .OrderBy(e => H.Dist(e.Cx(), e.Cy(), Cx(), Cy())).FirstOrDefault();
            if (b != null)
            {
                singingTo = b; mode = "sing"; atkT = Wind(3.2f);
                K("jaw").flare = 3.2f;
                float n = 196 * H.Pick(new[] { 1f, 1.125f, 1.25f, 1.5f });
                Snd.At("cantor:song" + n, 3.4f, s => { s.Voice(3.2f, 1, n, n * 1.01f, Voices.U, Voices.O, 0.1f, 0, 0, 0.03f, 0, 0.6f); s.Voice(3.0f, 0.4f, n * 1.5f, n * 1.5f, Voices.O, Voices.A, 0.12f, 0, 0, 0.03f, 0.2f, 0.6f); }, Cx(), Cy(), 0.8f, 2);
                return;
            }
        }
        if (p == null) return;
        float d = DistToHer();
        bool sees = Perceives();
        // it backs away from her; cornered, it shoves
        if (sees && d < 6 * H.TILE) Pursue(dt, Cx() - H.Sign(p.Cx() - Cx()) * 60, 50 * Menace);
        else vx *= 0.9f;
        if (sees) { int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s; }
        if (sees && d < 26 && cool <= 0 && CanAttack()) StartBlow(0.5f, 2);
    }
    public override Palette Pal() => burnT > 0 ? new Palette { robe = "#1a1410", skin = "#3a2a20" } : new Palette { robe = "#d0ccc0", skin = "#9aa6a8", cowl = null, dark = "#5a5a56" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        if (mode != "sing" || singingTo == null) return;
        // the song, drawn as a thread of pale light from its open mouth to the body it is raising
        var j = Skel(); var m = j["jaw"];
        c.Fill = H.Col("#101010"); c.FillCircle(m.X, m.Y, 1.4f);
        float a = 0.25f + 0.2f * MathF.Sin(anim * 6);
        c.Stroke = H.Rgba(190, 240, 230, a); c.LineWidth = 0.7f;
        c.BeginPath(); c.MoveTo(m.X, m.Y); c.QuadraticCurveTo((m.X + singingTo.Cx()) / 2, MathF.Min(m.Y, singingTo.Cy()) - 18, singingTo.Cx(), singingTo.Cy()); c.StrokePath();
        c.Glow(singingTo.Cx(), singingTo.Cy(), 10, H.Rgba(190, 240, 230, a));
    }
}

// ---------------------------------------------------------------- Ossuary
// Bones heaped in a crypt. When she comes near they stand up, one at a time, and knot themselves into something with
// arms too long for it. While it is still putting itself together its knots are bare and brittle; afterwards it
// reaches for her from twice as far as anything else.
public class Ossuary : Drowned
{
    public Ossuary(Props spec, RoomDef room) : base(spec, room)
    {
        type = "ossuary"; voice = "gullet";
        AddKnot("wristL", "wrist", "handL", 3);
        AddKnot("wristR", "wrist", "handR", 3);
        AddKnot("kneeL", "knee", "kneeL", 3);
        AddKnot("kneeR", "knee", "kneeR", 3);
        AddKnot("crown", "crown", "sternum", 5, 5, 2);
        mode = "pile"; awakeMode = "hunt";
        SetPose("lying");
    }
    // it reaches with its hands: break both wrists and it has nothing to take hold of her with
    public override bool CanAttack() => !K("wristL").broken || !K("wristR").broken;
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        if (mode == "pile") { Assemble(); return false; }
        if (mode == "assemble") dmg *= 2;
        return base.DamageKnot(k, dmg, src);
    }
    void Assemble()
    {
        if (mode != "pile") return;
        mode = "assemble"; atkT = 2.0f; SetPose("stand");
        foreach (var k in knots) k.flare = 2.0f;
        Snd.At("ossuary:rattle", 2.0f, s => s.Clicks(40, 1.9f, 0.7f, 2600, 1.4f), Cx(), Cy(), 0.8f, 2);
        Voices.Alert(this);
    }
    public override void Wake(bool force = false) { if (mode == "pile") Assemble(); }
    public override void Hear(float hx, float hy, float d, float r, string src) { if (mode == "pile") { if (d < 5 * H.TILE) Assemble(); return; } base.Hear(hx, hy, d, r, src); }
    public override void Think(float dt)
    {
        if (mode == "pile") { if (DistToHer() < 4.5f * H.TILE) Assemble(); return; }
        if (mode == "assemble") { vx = 0; atkT -= dt; if (atkT <= 0) mode = "hunt"; return; }
        if (Blow(dt, 140 * Menace, 22, 1, true)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        Pursue(dt, tgt.Value.X, (LegsOK() ? 50 : 26) * Menace);
        if (Perceives() && DistToHer() < 3.2f * H.TILE && cool <= 0 && CanAttack()) StartBlow(0.6f, 1.8f);
    }
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = base.Skeleton();
        if (pose == "stand")
        {
            // the arms hang to its knees
            j["elbowL"] += new Vector2(0, 3); j["elbowR"] += new Vector2(0, 3);
            j["handL"] += new Vector2(facing * 4, 8); j["handR"] += new Vector2(facing * 3, 8);
            if (mode == "assemble") { float u = Math.Clamp(1 - atkT / 2, 0, 1); foreach (var k in j.Keys.ToArray()) j[k] = new Vector2(j[k].X, H.Lerp(Feet() - 1, j[k].Y, u)); }
        }
        return j;
    }
    public override Palette Pal() => new() { robe = "#4a4438", skin = "#d8d0bc", cowl = null, dark = "#8a8270" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        if (mode == "pile")
        {
            // a heap of bones and skulls, salt-white, with knots like pearls in the joints
            float cx = Cx(), fy = Feet();
            c.Fill = H.Col("#bfb8a4"); for (int i = 0; i < 9; i++) c.FillRect(cx - 10 + TileArt.Hash(i, 3) * 18, fy - 2 - TileArt.Hash(i, 4) * 5, 4 + TileArt.Hash(i, 5) * 4, 1.4f);
            c.Fill = H.Col("#d8d0bc"); c.FillCircle(cx - 3, fy - 4, 2.4f); c.FillCircle(cx + 4, fy - 3, 2);
            c.Fill = H.Col("#2a2620"); c.FillCircle(cx - 3.6f, fy - 4.4f, 0.6f); c.FillCircle(cx - 2.2f, fy - 4.4f, 0.6f);
            return;
        }
        DrawHuman(c, Pal());
    }
}

// ---------------------------------------------------------------- Kite
// A drowned brother whose cloak the wind took. It rides the gusts through the ribs like a ragged kite and drops on her
// out of the dark. Break a shoulder and the cloak folds: it falls, and has to crawl.
public class Kite : Drowned
{
    float phase, diveT, diveCool;
    public Kite(Props spec, RoomDef room) : base(spec, room)
    {
        type = "kite"; voice = "shucker";
        AddKnot("shoulderL", "shoulder", "shoulderL", 3);
        AddKnot("shoulderR", "shoulder", "shoulderR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 1);
        mode = spec.S("pose") == "perched" ? "dormant" : "glide"; awakeMode = "glide";
        phase = H.Rand(0, 6.3f); diveCool = H.Rand(2, 4);
    }
    bool Flying => mode is "glide" or "dive" && K("shoulderL").broken == false && K("shoulderR").broken == false;
    public override void Wake(bool force = false) { if (mode == "dormant") { mode = "glide"; vy = -60; Voices.Alert(this); } }
    public override void OnKnotBroken(Knot k, string src)
    {
        base.OnKnotBroken(k, src);
        if (k.type == "shoulder" && mode is "glide" or "dive") { mode = "hunt"; SetPose("crawl"); H.ToastMsg("Its cloak folds. It falls.", 1.5f); }
    }
    float RoomWind()
    {
        if (room.Raw.A("windZones") is not System.Text.Json.Nodes.JsonArray zones) return 0;
        foreach (var zn in zones)
        {
            var z = new Props(zn.AsObject());
            float period = z.F("period"); int dir = H.FloorI(G.stats.time / period) % 2 == 1 ? -1 : 1;
            return dir * z.F("force") * 0.4f;
        }
        return 0;
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (mode == "dormant") { if (Perceives() || DistToHer() < 4 * H.TILE) Wake(); return; }
        if (Flying && p != null)
        {
            diveCool -= dt;
            if (mode == "dive")
            {
                diveT -= dt;
                var dv = (new Vector2(p.Cx(), p.Cy()) - new Vector2(Cx(), Cy())).Normalized();
                vx = H.Approach(vx, dv.X * 200 * Menace, 700 * dt); vy = H.Approach(vy, dv.Y * 200 * Menace, 700 * dt);
                var hb = HitBox();
                if (!p.Invuln() && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h)) { p.Hurt(1, this); p.vx += H.Sign(vx) * 160; diveT = 0; }
                if (diveT <= 0) { mode = "glide"; diveCool = H.Rand(3, 5); vy = -120; }
            }
            else
            {
                // drifting on the wind, high, above her
                var aim = new Vector2(p.Cx() + MathF.Sin(anim * 0.7f + phase) * 90 + RoomWind(), p.Cy() - 80);
                var dv = aim - new Vector2(Cx(), Cy());
                vx = H.Approach(vx, Math.Clamp(dv.X * 1.2f, -110, 110), 200 * dt);
                vy = H.Approach(vy, Math.Clamp(dv.Y * 1.2f, -70, 70) + MathF.Sin(anim * 2 + phase) * 15, 200 * dt);
                if (diveCool <= 0 && Phys.Los(Cx(), Cy(), p.Cx(), p.Cy()) && !p.inBellRoom) { mode = "dive"; diveT = 1.0f; Voices.Windup(this); foreach (var k in knots) k.flare = 0.6f; }
            }
            if (MathF.Abs(vx) > 5) facing = vx > 0 ? 1 : -1;
            return;
        }
        // folded: it crawls after her
        if (Blow(dt, 120 * Menace, 2, 1, false)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx *= 0.9f; return; }
        Pursue(dt, tgt.Value.X, 60 * Menace);
        if (Perceives() && DistToHer() < 24 && cool <= 0) StartBlow(0.5f, 1.6f);
    }
    public override void Physics(float dt, bool pinned = false)
    {
        if (Flying) { Phys.Move(this, dt); var r = room; x = Math.Clamp(x, r.Px + 2, r.Px + r.Pw - w - 2); y = Math.Clamp(y, r.Py + 2, r.Py + r.Ph - h - 2); return; }
        base.Physics(dt, pinned);
    }
    public override Palette Pal() => new() { robe = "#2a2a30", skin = "#8a908c", cowl = "#202026", dark = "#121216" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        if (Flying)
        {
            // the cloak spread like a sail, ragged at the edges, the body hanging under it
            var j = Skel(); var sh = (j["shoulderL"] + j["shoulderR"]) / 2;
            float flap = MathF.Sin(anim * 4 + phase) * 3;
            c.Fill = H.Col("#1c1c22");
            var pts = new List<Vector2> { sh + new Vector2(-14, -2 - flap), sh + new Vector2(0, -6), sh + new Vector2(14, -2 + flap) };
            for (int i = 6; i >= 0; i--) pts.Add(sh + new Vector2(-14 + i * 28 / 6f, 6 + (i % 2) * 3 + MathF.Sin(anim * 5 + i) * 1.2f));
            c.Poly(pts.ToArray());
        }
        DrawHuman(c, Pal());
    }
}

// ======================================================================= the end
// ---------------------------------------------------------------- Drowned Diver
// A salvage diver from the mainland, in brass and canvas like Maren's, come for the abbey's salt and kept by it. It
// still carries its harpoon gun, and it keeps its distance. The helmet covers its crown: break the latch at its neck
// and the helmet falls, and only then can the crown be reached.
public class Diver : Drowned
{
    public Diver(Props spec, RoomDef room) : base(spec, room)
    {
        type = "diver"; voice = "saltwright";
        AddKnot("kneeL", "knee", "kneeL", 4);
        AddKnot("kneeR", "knee", "kneeR", 4);
        AddKnot("elbowR", "elbow", "elbowR", 4);
        AddKnot("latch", "latch", "neck", 4, 3.5f);
        AddKnot("crown", "crown", "sternum", 5, 5);
        FromPose(spec);
    }
    public override bool IsSealed(Knot k) => k.type == "crown" ? !K("latch").broken : base.IsSealed(k);
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.id == "latch") { Snd.Crack(Pan()); FX.Spawn(Cx(), y + 2, 10, new FXOpt { color = "#c8973f", smin = 30, smax = 90 }); H.ToastMsg("The helmet falls. The crown is bare.", 1.6f); }
        base.OnKnotBroken(k, src);
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (Rising(dt)) return;
        if (mode == "aim")
        {
            vx *= 0.8f; atkT -= dt;
            if (!ArmOK("R")) { mode = "hunt"; return; }
            if (atkT <= 0 && p != null)
            {
                mode = "recover"; atkT = 0.9f;
                float a = MathF.Atan2(p.Cy() - (y + 10), p.Cx() - Cx());
                G.enemyShots.Add(new Shot { x = Cx() + facing * 6, y = y + 10, vx = MathF.Cos(a) * 380, vy = MathF.Sin(a) * 380, life = 2, dmg = 2, src = this });
                Snd.Harpoon(Pan());
            }
            return;
        }
        if (Blow(dt, 180 * Menace, 2, 1, false)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null || p == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        float d = DistToHer();
        bool sees = Perceives();
        // it holds off at harpoon range, backs away when she closes, and only fights hand to hand when it must
        float spd = (LegsOK() ? 48 : 28) * Menace;
        if (d > 9 * H.TILE) Pursue(dt, tgt.Value.X, spd);
        else if (d < 4 * H.TILE && LegsOK()) Pursue(dt, Cx() - H.Sign(p.Cx() - Cx()) * 60, spd);
        else vx *= 0.85f;
        if (sees) { int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s; }
        if (sees && cool <= 0 && ArmOK("R") && d > 3 * H.TILE && Phys.Los(Cx(), y + 10, p.Cx(), p.Cy()))
        {
            mode = "aim"; atkT = Wind(0.9f); cool = Cool(2.6f);
            K("elbowR").flare = 0.9f;
            Voices.Windup(this);
        }
        else if (sees && d < 28 && cool <= 0 && CanAttack()) StartBlow(0.5f, 1.6f);
    }
    // a diver does not drown
    public override void Physics(float dt, bool pinned = false)
    {
        if (World.IsWater(Cx(), Cy()) && mode is "hunt" or "aim" or "recover" && !pinned) { vy = H.Approach(vy, 0, 300 * dt); Phys.Move(this, dt); return; }
        base.Physics(dt, pinned);
    }
    public override Palette Pal() => new() { robe = "#5a5040", skin = "#7a8478", cowl = null, dark = "#2a2620" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        var j = Skel();
        if (!K("latch").broken && mode != "downed")
        {
            // the brass helmet, its glass dark, something moving behind it
            var hd = j["head"];
            c.Fill = H.Col("#9a7432"); c.FillCircle(hd.X, hd.Y, 4.6f);
            c.Fill = H.Col("#101418"); c.FillCircle(hd.X + facing * 1.4f, hd.Y, 2.2f);
            c.Fill = H.Rgba(120, 220, 200, 0.25f + 0.2f * MathF.Sin(anim * 2)); c.FillCircle(hd.X + facing * 1.6f, hd.Y - 0.3f, 0.8f);
        }
        if (ArmOK("R") && mode != "downed")
        {
            var hr = j["handR"];
            c.Stroke = H.Col("#4a4a44"); c.LineWidth = 1.6f; c.Line(hr.X, hr.Y, hr.X + facing * 7, hr.Y - 1);
            if (mode != "recover") { c.Stroke = H.Col("#b0b4ac"); c.LineWidth = 0.6f; c.Line(hr.X + facing * 7, hr.Y - 1, hr.X + facing * 10, hr.Y - 1.4f); }
        }
    }
}

// ---------------------------------------------------------------- Deepwalker
// Something that drowned so deep the weight of the water made it huge. It walks the floor of the Gullet and nowhere
// else, slowly, and its knots are dark: only the focused beam shows them, and only a knot that can be seen can be
// broken. Its hands close like a door.
public class Deepwalker : Drowned
{
    public Deepwalker(Props spec, RoomDef room) : base(spec, room)
    {
        type = "deepwalker"; voice = "saltwright";
        AddKnot("kneeL", "knee", "kneeL", 6, 6);
        AddKnot("kneeR", "knee", "kneeR", 6, 6);
        AddKnot("elbowL", "elbow", "elbowL", 6, 6);
        AddKnot("elbowR", "elbow", "elbowR", 6, 6);
        AddKnot("crown", "crown", "sternum", 8, 7, 2);
        w = 12 * SCALE; h = 28 * SCALE; x = spec.F("x") - w / 2; y = spec.F("y") - h;
        FromPose(spec);
    }
    const float SCALE = 1.7f;
    public override void SetPose(string p)
    {
        if (p == pose) return;
        float bottom = y + h, mid = Cx();
        base.SetPose(p);
        w *= SCALE; h *= SCALE; x = mid - w / 2; y = bottom - h;
    }
    bool Lit(Knot k)
    {
        var p = G.player; var kp = KnotPos(k);
        return p != null && kp != null && p.LanternLit() && p.focusing && p.BeamHits(kp.Value.X, kp.Value.Y);
    }
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        // a dark knot cannot be found to break
        if (!Lit(k) && src is "rivet" or "gaff" or "harpoon" or "bolt" or "saw") { Snd.Tink(Pan()); FX.Sparks(Cx(), Cy(), 2); return false; }
        return base.DamageKnot(k, dmg, src);
    }
    public override void DrawKnots(Canvas c)
    {
        // its knots only show in the beam
        var p = G.player;
        if (p == null || !p.focusing) return;
        base.DrawKnots(c);
    }
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = base.Skeleton();
        float cx = Cx(), fy = Feet();
        foreach (var k in j.Keys.ToArray()) j[k] = new Vector2(cx + (j[k].X - cx) * SCALE, fy + (j[k].Y - fy) * SCALE);
        return j;
    }
    public override void Think(float dt)
    {
        if (Rising(dt)) return;
        if (Blow(dt, 70, 10, 2, true)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 200 * dt); return; }
        Pursue(dt, tgt.Value.X, (LegsOK() ? 26 : 14) * Menace);
        if (Perceives() && DistToHer() < 44 && cool <= 0 && CanAttack()) StartBlow(1.2f, 2.6f);
    }
    public override bool Perceives()
    {
        var p = G.player;
        if (p == null || p.dead || p.inBellRoom) return false;
        // it feels her in the water at any distance in its own room, and sees nothing at all
        return World.IsWater(p.Cx(), p.Cy()) && DistToHer() < 16 * H.TILE;
    }
    public override Palette Pal() => new() { robe = "#1e2a2c", skin = "#7a8a88", cowl = "#141c1e", dark = "#0c1012" };
    public override void Draw(Canvas c) { if (!gone) DrawHuman(c, Pal()); }
}

// ---------------------------------------------------------------- Wick
// In the Skull the drowned learned to hate the light. They come for the lamp, not for her: near one, oil runs out of
// her lantern like water, and a flare dies in its hands. The focused beam burns them; they cannot stand in it.
public class Wick : Drowned
{
    float beamT;
    public Wick(Props spec, RoomDef room) : base(spec, room)
    {
        type = "wick"; voice = "gullet";
        AddKnot("elbowL", "elbow", "elbowL", 3);
        AddKnot("elbowR", "elbow", "elbowR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        FromPose(spec);
    }
    public override bool Perceives()
    {
        var p = G.player;
        if (p == null || p.dead || p.inBellRoom) return false;
        // it knows light, not her: a lit lantern across the room, a shuttered one only close by
        float d = DistToHer();
        return p.LanternLit() ? d < 14 * H.TILE : d < 2 * H.TILE;
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (Rising(dt)) return;
        if (p != null && !p.inBellRoom)
        {
            float d = DistToHer();
            // it drinks the light
            if (d < 3.5f * H.TILE && p.LanternLit()) { p.oil = MathF.Max(0, p.oil - dt * 7); if (H.Rand() < dt * 6) FX.Spawn(p.Cx(), p.y + 8, 1, new FXOpt { color = "#1a1612", grav = -20, smin = 5, smax = 20 }); }
            foreach (var pr in G.projectiles) if (pr.kind == "flare" && !pr.dead && H.Dist(pr.x, pr.y, Cx(), Cy()) < 2.5f * H.TILE) { pr.dead = true; FX.Spawn(pr.x, pr.y, 8, new FXOpt { color = "#2a2420", grav = -30 }); }
            // and the focused beam burns it
            if (p.LanternLit() && p.focusing && p.BeamHits(Cx(), Cy()) && mode != "downed")
            {
                beamT += dt; vx = H.Approach(vx, H.Sign(Cx() - p.Cx()) * 60, 600 * dt);
                if (H.Rand() < dt * 12) FX.Embers(Cx() + H.Rand(-4, 4), Cy(), 1);
                if (beamT > 0.8f) { beamT = 0; var k = knots.Where(q => !q.broken && !IsSealed(q)).OrderBy(q => H.Rand()).FirstOrDefault(); if (k != null) DamageKnot(k, 1, "lamp"); }
                return;
            }
        }
        if (Blow(dt, 200 * Menace, 2, 1, false)) return;
        cool -= dt;
        var tgt = Seek(dt);
        if (tgt == null) { vx = H.Approach(vx, 0, 300 * dt); return; }
        Pursue(dt, tgt.Value.X, (p != null && p.LanternLit() ? 68 : 30) * Menace);
        if (Perceives() && DistToHer() < 26 && cool <= 0 && CanAttack()) StartBlow(0.45f, 1.4f);
    }
    public override Palette Pal() => new() { robe = "#14120f", skin = "#4a4640", cowl = "#0c0a08", dark = "#060504" };
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        // it is darker where it stands: the light goes into it
        if (mode != "downed") c.Glow(Cx(), Cy(), 26, H.Rgba(0, 0, 0, 0.45f));
        DrawHuman(c, Pal());
        if (mode == "downed") return;
        var m = j["jaw"];
        c.Fill = H.Col("#000000"); c.FillCircle(m.X, m.Y, 1.6f);
        c.Stroke = H.Rgba(255, 200, 120, 0.25f); c.LineWidth = 0.4f;
        var p = G.player;
        if (p != null && p.LanternLit() && DistToHer() < 3.5f * H.TILE) { c.BeginPath(); c.MoveTo(p.Cx(), p.y + 8); c.QuadraticCurveTo((p.Cx() + m.X) / 2, m.Y - 6, m.X, m.Y); c.StrokePath(); }
    }
}
