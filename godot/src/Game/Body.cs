// Brine bodies: puppets held up by salt knots at their joints.
// Hits on flesh deal 10% damage; hits on a knot deal full damage; broken knots change behaviour.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public class Knot
{
    public string id, type, joint;
    public float hp, max, r = 4.5f, flare, plateHp = 2;
    public bool broken, plated, basePlated, braced, sealedFlag, needsBuoy;
    public int sealedNeeds;
}

public class Palette { public string robe, skin, cowl, dark = "#1a1d1e"; }

public class Body : Ent
{
    public float anim, stun, pinT, burnT, atkT, cool, alert, speed = 45, hpScale = 1, fireT;
    public float? barbT, climbT;
    public Knot pinKnot;
    public List<Knot> knots = new();
    public string mode = "idle", pose = "stand", awakeMode;
    public bool reknot, dragged, tetherable, corpse, boss, breach, hesper, teodor, sailor, passive, shed;
    public Vector2? target, lastHeard;
    public Dictionary<string, float> crust = new();
    protected Dictionary<string, Vector2> _skel;

    public Body(Props spec, RoomDef room)
    {
        isBody = true;
        this.spec = spec; this.room = room;
        key = spec.S("key");
        facing = spec.Has("facing") ? spec.I("facing") : (H.Rand() < 0.5f ? -1 : 1);
        anim = H.Rand() * 10;
        stepUp = true;
        w = 12; h = 28;
        x = spec.F("x") - w / 2; y = spec.F("y") - h;
        boss = spec.B("boss");
    }
    public override bool Hears => true;
    public virtual (float x, float y, float w, float h) HitBox() => (x, y, w, h);

    public Knot AddKnot(string id, string type, string joint, float hp, float r = 4.5f, int sealedNeeds = 0)
    {
        var k = new Knot { id = id, type = type, joint = joint, hp = hp, max = hp, r = r, sealedNeeds = sealedNeeds };
        knots.Add(k);
        return k;
    }
    public Knot K(string id) => knots.Find(k => k.id == id);
    public int BrokenLimbs() => knots.Count(k => k.broken && k.type != "crown");
    public virtual bool IsSealed(Knot k) => k.sealedNeeds > 0 && BrokenLimbs() < k.sealedNeeds;
    public bool Alive() => !dead && !gone && mode != "downed" && mode != "captured";

    // ---- skeleton -----------------------------------------------------------
    static readonly Dictionary<string, (float, float)> DIMS = new()
    {
        ["stand"] = (12, 28), ["crawl"] = (24, 10), ["seated"] = (14, 20), ["lying"] = (26, 7), ["hang"] = (12, 28),
    };
    public virtual void SetPose(string p)
    {
        if (p == pose) return;
        float bottom = y + h, mid = Cx();
        pose = p;
        var d = DIMS.TryGetValue(p, out var dd) ? dd : (12, 28);
        w = d.Item1; h = d.Item2;
        x = mid - w / 2; y = bottom - h;
        // Make sure we do not end up inside a wall.
        for (int i = 0; i < 8 && Phys.BoxSolid(x, y, w, h); i++) x += (i % 2 == 1 ? -1 : 1) * (i + 1) * 2;
    }
    public Dictionary<string, Vector2> Skel() => _skel ??= Skeleton();
    static Vector2 V(float x, float y) => new(x, y);
    public virtual Dictionary<string, Vector2> Skeleton()
    {
        float f = facing, x = Cx(), gy = Feet();
        float t = anim;
        var j = new Dictionary<string, Vector2>();
        float walk = MathF.Abs(vx) > 5 ? MathF.Sin(t * 7) : 0;
        bool armL = ArmOK("L"), armR = ArmOK("R");
        if (pose == "stand")
        {
            float bob = MathF.Abs(walk) * 1;
            j["hip"] = V(x, gy - 13 + bob);
            j["sternum"] = V(x + f * 1, gy - 19 + bob);
            j["shoulderL"] = V(x + f * 1.5f, gy - 22 + bob);
            j["shoulderR"] = V(x - f * 0.5f, gy - 22 + bob);
            j["neck"] = V(x + f * 2, gy - 24 + bob);
            j["head"] = V(x + f * 3, gy - 26.5f + bob);
            j["jaw"] = V(x + f * 5, gy - 24.5f + bob);
            var lk = K("kneeL"); var rk = K("kneeR");
            bool limp = (lk != null && lk.broken) || (rk != null && rk.broken);
            j["kneeL"] = V(x + f * (walk * 3 + 1), gy - 7);
            j["footL"] = V(x + f * (walk * 5), gy);
            j["kneeR"] = V(x + f * (-walk * 3 + 1), gy - 7 + (limp ? 1 : 0));
            j["footR"] = V(x + f * (-walk * 5), gy);
            bool reach = mode == "hunt" || mode == "windup" || mode == "attack" || mode == "grab";
            float raise = mode == "windup" ? -4 : 0;
            foreach (var side in new[] { "L", "R" })
            {
                var sh = j["shoulder" + side];
                bool ok = side == "L" ? armL : armR;
                if (!ok)
                {
                    float sw = MathF.Sin(t * 3 + (side == "L" ? 0 : 1)) * 1.5f;
                    j["elbow" + side] = V(sh.X + sw, sh.Y + 6);
                    j["hand" + side] = V(sh.X + sw * 2, sh.Y + 12);
                }
                else if (reach)
                {
                    float off = side == "L" ? 0 : -1.5f;
                    j["elbow" + side] = V(sh.X + f * 6, sh.Y + 1 + raise + off);
                    j["hand" + side] = V(sh.X + f * 12, sh.Y + raise * 2 + off);
                }
                else
                {
                    j["elbow" + side] = V(sh.X + f * 2, sh.Y + 6);
                    j["hand" + side] = V(sh.X + f * 4, sh.Y + 11);
                }
            }
        }
        else if (pose == "crawl")
        {
            float c = MathF.Sin(t * 9) * 2;
            j["hip"] = V(x - f * 8, gy - 4);
            j["sternum"] = V(x + f * 2, gy - 6);
            j["shoulderL"] = V(x + f * 5, gy - 7);
            j["shoulderR"] = V(x + f * 4, gy - 7);
            j["neck"] = V(x + f * 7, gy - 7);
            j["head"] = V(x + f * 10, gy - 7);
            j["jaw"] = V(x + f * 11.5f, gy - 5);
            j["kneeL"] = V(x - f * 12, gy - 2);
            j["footL"] = V(x - f * 16, gy - 1);
            j["kneeR"] = V(x - f * 12.5f, gy - 1);
            j["footR"] = V(x - f * 15, gy);
            foreach (var side in new[] { "L", "R" })
            {
                var sh = j["shoulder" + side];
                bool ok = side == "L" ? armL : armR;
                float cc = side == "L" ? c : -c;
                if (ok) { j["elbow" + side] = V(sh.X + f * (4 + cc), gy - 3); j["hand" + side] = V(sh.X + f * (8 + cc), gy); }
                else { j["elbow" + side] = V(sh.X + f * 1, gy - 2); j["hand" + side] = V(sh.X - f * 2, gy); }
            }
        }
        else if (pose == "seated")
        {
            j["hip"] = V(x, gy - 8);
            j["kneeL"] = V(x + f * 6, gy - 8); j["footL"] = V(x + f * 7, gy);
            j["kneeR"] = V(x + f * 5, gy - 7.5f); j["footR"] = V(x + f * 5.5f, gy);
            j["sternum"] = V(x + f * 0.5f, gy - 13);
            j["shoulderL"] = V(x + f * 1.5f, gy - 16); j["shoulderR"] = V(x, gy - 16);
            j["neck"] = V(x + f * 2.5f, gy - 17);
            j["head"] = V(x + f * 4, gy - 18);
            j["jaw"] = V(x + f * 5.5f, gy - 16);
            j["elbowL"] = V(x + f * 3, gy - 11); j["handL"] = V(x + f * 6, gy - 9);
            j["elbowR"] = V(x + f * 1.5f, gy - 11); j["handR"] = V(x + f * 4.5f, gy - 9);
        }
        else
        { // lying / downed
            float y = gy - 2;
            j["head"] = V(x + f * 11, y - 1);
            j["jaw"] = V(x + f * 12.5f, y);
            j["neck"] = V(x + f * 8, y);
            j["shoulderL"] = V(x + f * 6, y - 1); j["shoulderR"] = V(x + f * 6, y + 0.5f);
            j["sternum"] = V(x + f * 3, y);
            j["hip"] = V(x - f * 4, y);
            j["kneeL"] = V(x - f * 9, y - 1); j["footL"] = V(x - f * 13, y);
            j["kneeR"] = V(x - f * 9, y + 1); j["footR"] = V(x - f * 13.5f, y + 1);
            j["elbowL"] = V(x + f * 4, y + 1); j["handL"] = V(x + f * 1, y + 1.5f);
            j["elbowR"] = V(x + f * 8, y + 1.5f); j["handR"] = V(x + f * 10, y + 1.5f);
        }
        return j;
    }
    public virtual Vector2? KnotPos(Knot k) => Skel().TryGetValue(k.joint, out var p) ? p : null;
    public bool ArmOK(string side)
    {
        var e = K("elbow" + side) ?? K("shoulder" + side) ?? K("arm" + side);
        return e == null || !e.broken;
    }
    public bool ArmsOK() => ArmOK("L") || ArmOK("R");
    public bool LegsOK()
    {
        var a = K("kneeL"); var b = K("kneeR");
        if (a == null && b == null) return true;
        return !((a != null && a.broken) && (b != null && b.broken));
    }

    // ---- damage --------------------------------------------------------------
    public virtual bool DamageKnot(Knot k, float dmg, string src)
    {
        if (k.broken || gone) return false;
        float pan = Pan();
        var kp = KnotPos(k) ?? new Vector2(Cx(), Cy());
        if (IsSealed(k)) { Snd.Tink(pan); FX.Sparks(kp.X, kp.Y, 3); Flinch(); return false; }
        if (k.plated)
        {
            if (src == "gaff" || src == "fire" || src == "pry")
            {
                k.plateHp -= src == "pry" ? 2 : 1;
                Snd.Crack(pan); FX.Salt(kp.X, kp.Y, 6);
                if (k.plateHp <= 0) { k.plated = false; H.ToastMsg("Plate pried off."); }
            }
            else { Snd.Tink(pan); FX.Sparks(kp.X, kp.Y, 3); }
            Flinch();
            return false;
        }
        // A knot flaring during a wind-up is brittle.
        if (k.flare > 0 && src == "rivet") dmg *= 2;
        k.hp -= dmg;
        FX.Salt(kp.X, kp.Y, 5);
        Wake(true);
        if (k.hp <= 0)
        {
            k.broken = true; k.hp = 0;
            Snd.Crack(pan);
            FX.Salt(kp.X, kp.Y, 16);
            G.stats.knots[k.type] = G.stats.knots.GetValueOrDefault(k.type) + 1;
            Narr.Think("firstKnot");
            if (!G.stats.byAct.TryGetValue(G.act, out var ba)) G.stats.byAct[G.act] = ba = new Dictionary<string, int>();
            ba[k.type] = ba.GetValueOrDefault(k.type) + 1;
            OnKnotBroken(k, src);
        }
        else
        {
            Snd.Tink(pan);
            Flinch();
        }
        G.AddSwell(0.3f);
        return true;
    }
    public virtual void DamageFlesh(float dmg, float hx, float hy, string src)
    {
        if (gone) return;
        Snd.Thud(Pan());
        FX.Blood(hx, hy, 3);
        Wake(true);
        // 10% of the hit reaches the nearest open knot.
        Knot best = null; float bd = 1e9f;
        foreach (var k in knots)
        {
            if (k.broken || k.plated || IsSealed(k)) continue;
            var p = KnotPos(k); if (p == null) continue;
            float d = H.Dist(hx, hy, p.Value.X, p.Value.Y);
            if (d < bd) { bd = d; best = k; }
        }
        if (best != null)
        {
            best.hp -= dmg * 0.1f;
            if (best.hp <= 0) { best.broken = true; Snd.Crack(Pan()); OnKnotBroken(best, src); }
        }
        Flinch();
    }
    public virtual void Flinch() { if (mode == "hunt") vx *= 0.3f; }
    public virtual void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown") { Down(src); return; }
        if (mode == "grab" && (k.type == "elbow" || k.type == "shoulder" || k.type == "arm")) ReleaseGrab();
        if (!LegsOK() && pose == "stand" && mode != "downed") SetPose("crawl");
        if (mode == "windup" && !CanAttack()) mode = "hunt";
    }
    public virtual bool CanAttack() => ArmsOK();
    public virtual void Down(string src)
    {
        mode = "downed";
        ReleaseGrab();
        vx = 0;
        SetPose("lying");
        reknot = false;
        Snd.Groan(Pan(), 0.1f);
        G.OnBodyDowned(this, src);
    }
    // Re-knotting: the Brine grows new knots and the body stands again.
    public virtual void StandUp()
    {
        if (gone || corpse) return;
        reknot = false;
        foreach (var k in knots) { k.broken = false; k.hp = k.max; k.plated = k.basePlated; }
        G.ApplyEncrustation(this);
        SetPose("stand");
        mode = "hunt";
        anim = 0;
        Snd.Groan(Pan(), 0.25f);
        FX.Salt(Cx(), Cy(), 20);
    }
    public virtual void Burn() { burnT = 3; Snd.Fire(Pan()); }
    public virtual void Pin(Knot k, float t, int tier)
    {
        if (k.type == "crown") return;
        pinT = t; pinKnot = k;
        if (tier >= 2) barbT = 2;
        if (tier >= 1) tetherable = true;
    }
    public virtual void ReleaseGrab()
    {
        var p = G.player;
        if (p != null && p.grabbedBy == this) { p.grabbedBy = null; p.state = "normal"; }
        if (mode == "grab") { mode = "recover"; atkT = 0.8f; }
    }
    public float Pan() { var p = G.player; return p != null ? H.Clamp((Cx() - p.Cx()) / 300, -1, 1) : 0; }

    // ---- perception ---------------------------------------------------------
    public override void Hear(float hx, float hy, float d, float r, string src)
    {
        if (gone || corpse || mode == "downed" || mode == "captured") return;
        if (mode == "dormant")
        {
            if (src == "scream" || G.CanWake()) { lastHeard = new Vector2(hx, hy); Wake(); }
            return;
        }
        lastHeard = new Vector2(hx, hy);
        alert = 3;
    }
    public virtual void Wake(bool force = false)
    {
        if (mode != "dormant" || corpse) return;
        if (!force && !G.CanWake()) return;
        mode = "rise"; atkT = 0.9f;
        Snd.Groan(Pan());
        G.AddSwell(1);
    }
    public virtual bool Perceives()
    {
        var p = G.player;
        if (p == null || p.dead || p.inBellRoom) return false;
        float d = H.Dist(Cx(), Cy(), p.Cx(), p.Cy());
        bool lit = p.LanternLit();
        float sight = lit ? 9 * H.TILE * (p.HasFitting("tallowr") ? 1.3f : 1) : 1.6f * H.TILE;
        if (d < 1.6f * H.TILE) return true;
        if (d < sight && Phys.Los(Cx(), y + 4, p.Cx(), p.Cy())) return true;
        return false;
    }

    // ---- update -----------------------------------------------------------------
    public override void Update(float dt)
    {
        _skel = null;
        anim += dt;
        foreach (var k in knots) if (k.flare > 0) k.flare -= dt;
        if (burnT > 0)
        {
            burnT -= dt;
            if (H.Rand() < 36 * dt) FX.Embers(Cx() + H.Rand(-8, 8), Cy(), 2);
            if (burnT <= 0) { gone = true; mode = "gone"; G.OnBodyBurned(this); FX.Spawn(Cx(), Cy(), 20, new FXOpt { color = "#333", grav = -30 }); }
            return;
        }
        // Fire hurts knots.
        if (Fire.BurnsAt(Cx(), Feet() - 4) && mode != "downed")
        {
            fireT -= dt;
            if (fireT <= 0)
            {
                fireT = 1;
                var ks = knots.Where(k => !k.broken && !IsSealed(k)).ToList();
                if (ks.Count > 0) DamageKnot(H.Pick(ks), 1, "fire");
            }
        }
        if (mode == "downed" || mode == "captured") { Physics(dt); return; }
        if (stun > 0) { stun -= dt; vx *= 0.8f; Physics(dt); return; }
        if (pinT > 0)
        {
            pinT -= dt; vx = 0;
            if (barbT != null)
            {
                barbT -= dt;
                if (barbT <= 0 && pinKnot != null && !pinKnot.broken) { barbT = null; DamageKnot(pinKnot, 99, "harpoon"); }
            }
            Physics(dt, true);
            return;
        }
        foreach (var id in crust.Keys.ToArray()) { crust[id] -= dt; if (crust[id] <= 0) crust.Remove(id); }
        Think(dt);
        Physics(dt);
    }
    public virtual void Think(float dt) { }
    public virtual void Physics(float dt, bool pinned = false)
    {
        bool inWater = World.IsWater(Cx(), Cy());
        if (dragged) return;
        if (inWater) { vy += 250 * dt; if (vy > 70) vy = 70; }
        else { vy += 1100 * dt; if (vy > 500) vy = 500; }
        if (pinned) vx = 0;
        // Limp bodies do not slide.
        if (mode == "downed" || mode == "dormant" || mode == "captured") vx *= onGround ? 0.7f : 0.98f;
        Phys.Move(this, dt);
        // Bodies stay in their own room.
        var r = room;
        if (x < r.Px + 2) { x = r.Px + 2; vx = 0; hitWall = -1; }
        if (x + w > r.Px + r.Pw - 2) { x = r.Px + r.Pw - 2 - w; vx = 0; hitWall = 1; }
        if (y > r.Py + r.Ph) { y = r.Py + r.Ph - h - 2; vy = 0; }
    }

    // Generic shambling pursuit used by humanoids.
    public void Pursue(float dt, float tx, float speed)
    {
        float dx = tx - Cx();
        if (MathF.Abs(dx) > 4) { facing = (int)H.Sign(dx); vx = H.Approach(vx, facing * speed, 600 * dt); }
        else vx = H.Approach(vx, 0, 600 * dt);
        if (hitWall != 0 && onGround && pose == "stand" && LegsOK()) vy = -260; // clamber
        else if (hitWall != 0 && pose == "stand" && LegsOK() && climbT == null) climbT = 0.6f;
        if (climbT != null) { climbT -= dt; vy = MathF.Min(vy, -60); if (climbT <= 0 || hitWall == 0) climbT = null; }
    }

    // ---- rendering --------------------------------------------------------------
    protected static void DrawLimb(Canvas c, Vector2 a, Vector2 b, float w, string col)
    {
        c.StrokeStyle = col; c.LineWidth = w;
        c.BeginPath(); c.MoveTo(a.X, a.Y); c.LineTo(b.X, b.Y); c.StrokePath();
        // round caps
        c.FillStyle = col; c.FillCircle(a.X, a.Y, w / 2); c.FillCircle(b.X, b.Y, w / 2);
    }
    protected void DrawHuman(Canvas c, Palette pal)
    {
        var j = Skel();
        string robe = pal.robe, skin = pal.skin, dark = pal.dark ?? "#1a1d1e";
        // back limbs
        DrawLimb(c, j["hip"], j["kneeR"], 2.6f, dark); DrawLimb(c, j["kneeR"], j["footR"], 2.2f, dark);
        DrawLimb(c, j["shoulderR"], j["elbowR"], 2.2f, dark); DrawLimb(c, j["elbowR"], j["handR"], 1.8f, dark);
        // robe body
        c.FillStyle = robe;
        var sl = j["shoulderL"]; var hip = j["hip"];
        c.BeginPath();
        c.MoveTo(sl.X - 3, sl.Y - 1);
        c.LineTo(sl.X + 3, sl.Y - 1);
        if (pose == "stand") { c.LineTo(hip.X + 4.5f, hip.Y + 4); c.LineTo(hip.X - 4.5f, hip.Y + 4); }
        else { c.LineTo(hip.X + 3, hip.Y + 2.5f); c.LineTo(hip.X - 3, hip.Y - 2.5f); }
        c.ClosePath(); c.FillPath();
        if (pose == "stand") c.FillRect(hip.X - 4.5f, hip.Y, 9, 6);
        // robe shading: a darker fold down the back edge
        c.Fill = new Color(0, 0, 0, 0.18f);
        c.BeginPath(); c.MoveTo(sl.X - 3 * facing, sl.Y - 1); c.LineTo(sl.X - 1 * facing, sl.Y - 1); c.LineTo(hip.X - 2.5f * facing, hip.Y + 4); c.LineTo(hip.X - 4.5f * facing, hip.Y + 4); c.ClosePath(); c.FillPath();
        // front limbs
        DrawLimb(c, j["hip"], j["kneeL"], 2.8f, robe); DrawLimb(c, j["kneeL"], j["footL"], 2.4f, skin);
        DrawLimb(c, j["shoulderL"], j["elbowL"], 2.6f, robe); DrawLimb(c, j["elbowL"], j["handL"], 2, skin);
        // head + cowl
        var head = j["head"];
        c.FillStyle = skin; c.BeginPath(); c.Arc(head.X, head.Y, 3.4f, 0, H.PI * 2); c.FillPath();
        if (pal.cowl != null) { c.FillStyle = pal.cowl; c.BeginPath(); c.Arc(head.X - facing * 0.8f, head.Y - 0.5f, 3.8f, H.PI * 0.9f, H.PI * 2.1f); c.FillPath(); }
        // salt crust speckles
        c.Fill = new Color(230 / 255f, 240 / 255f, 245 / 255f, 0.55f);
        for (int i = 0; i < 4; i++)
        {
            float t = (i * 0.37f + (key != null ? key.Length * 0.13f : 0)) % 1;
            c.FillRect(H.Lerp(sl.X, hip.X, t) + ((i * 7) % 5) - 2, H.Lerp(sl.Y, hip.Y, t), 1, 1);
        }
        if (mode == "windup" || mode == "inhale")
        {
            var jaw = j["jaw"];
            c.Fill = new Color(1, 240 / 255f, 200 / 255f, 0.5f);
            c.BeginPath(); c.Arc(jaw.X, jaw.Y, 1.2f, 0, H.PI * 2); c.FillPath();
        }
    }
    public override void DrawKnots(Canvas c)
    {
        foreach (var k in knots)
        {
            var pp = KnotPos(k); if (pp == null) continue;
            var p = pp.Value;
            float v = Render.KnotVisibility(p.X, p.Y, this);
            if (k.flare > 0) v = MathF.Max(v, 0.9f);
            if (mode == "downed" || mode == "dormant" && corpse) v *= 0.15f;
            if (k.broken)
            {
                if (v > 0.05f) { c.Fill = H.Rgba(60, 70, 75, 0.6f * v); c.FillRect(p.X - 1.5f, p.Y - 1.5f, 3, 3); }
                continue;
            }
            if (v < 0.04f) continue;
            bool sealedK = IsSealed(k);
            float s = k.r * (k.type == "crown" ? 0.95f : 0.8f);
            if (v > 0.5f) { c.Fill = H.Rgba(190, 240, 255, 0.18f * v); c.BeginPath(); c.Arc(p.X, p.Y, s + 4, 0, H.PI * 2); c.FillPath(); }
            c.Save(); c.Translate(p.X, p.Y); c.Rotate(H.PI / 4 + MathF.Sin(anim * 2 + p.X) * 0.1f);
            c.Fill = k.plated ? H.Rgba(200, 190, 170, v) : sealedK ? H.Rgba(120, 150, 170, v) : k.flare > 0 ? H.Rgba(255, 255, 240, v) : H.Rgba(200, 245, 255, v);
            c.FillRect(-s / 2, -s / 2, s, s);
            if (k.plated) { c.Stroke = H.Rgba(90, 80, 70, v); c.LineWidth = 1; c.StrokeRect(-s / 2 - 1, -s / 2 - 1, s + 2, s + 2); }
            if (k.hp < k.max && !k.plated) { c.Stroke = H.Rgba(40, 60, 70, v); c.LineWidth = 0.8f; c.BeginPath(); c.MoveTo(-s / 2, 0); c.LineTo(s / 2, -1); c.StrokePath(); }
            c.Restore();
        }
    }
    // How brightly knots glow this frame (used by the renderer for real lights on bright knots).
    public float KnotGlow(Knot k)
    {
        var p = KnotPos(k); if (p == null || k.broken) return 0;
        float v = Render.KnotVisibility(p.Value.X, p.Value.Y, this);
        if (k.flare > 0) v = MathF.Max(v, 0.9f);
        if (mode == "downed" || mode == "dormant" && corpse) v *= 0.15f;
        return v;
    }

    public override Props Serialize()
    {
        var o = new JsonObject
        {
            ["x"] = x, ["y"] = y, ["mode"] = mode == "rise" ? "hunt" : mode, ["pose"] = pose, ["facing"] = facing, ["gone"] = gone,
            ["reknot"] = reknot,
            ["knots"] = new JsonArray(knots.Select(k => (JsonNode)new JsonObject { ["hp"] = k.hp, ["broken"] = k.broken, ["plated"] = k.plated }).ToArray()),
        };
        return new Props(o);
    }
    static readonly HashSet<string> AWAKE = new() { "hunt", "windup", "attack", "recover", "grab", "inhale", "rise", "idle" };
    public override void Restore(Props s)
    {
        SetPose(s.S("pose") ?? pose);
        x = s.F("x"); y = s.F("y"); facing = s.I("facing", facing); gone = s.B("gone"); reknot = s.B("reknot");
        mode = s.S("mode") ?? mode;
        if (AWAKE.Contains(mode)) mode = awakeMode ?? "hunt";
        if (s.A("knots") is JsonArray ka)
            for (int i = 0; i < ka.Count && i < knots.Count; i++)
            {
                var ks = new Props(ka[i].AsObject());
                if (ks.Has("hp")) knots[i].hp = ks.F("hp");
                if (ks.Has("broken")) knots[i].broken = ks.B("broken");
                if (ks.Has("plated")) knots[i].plated = ks.B("plated");
            }
        if (gone) mode = "gone";
    }
}

// ---------------------------------------------------------------- Shucker
public class Shucker : Body
{
    public bool standOnWater;
    public int grabHits;
    public Shucker(Props spec, RoomDef room) : base(spec, room)
    {
        type = "shucker";
        AddKnot("kneeL", "knee", "kneeL", 3);
        AddKnot("kneeR", "knee", "kneeR", 3);
        AddKnot("elbowL", "elbow", "elbowL", 3);
        AddKnot("elbowR", "elbow", "elbowR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        teodor = spec.B("teodor");
        standOnWater = spec.B("standOnWater");
        string sp = spec.S("pose");
        if (sp == "seated") { SetPose("seated"); mode = "dormant"; }
        else if (sp == "lying") { SetPose("lying"); mode = "dormant"; }
        else mode = "hunt";
        awakeMode = "hunt";
    }
    public override void OnKnotBroken(Knot k, string src)
    {
        base.OnKnotBroken(k, src);
        if (teodor && mode != "downed")
        {
            bool limbs = new[] { "kneeL", "kneeR", "elbowL", "elbowR" }.All(id => K(id).broken);
            if (limbs) { mode = "captured"; SetPose("lying"); ReleaseGrab(); H.ToastMsg("Teodor lies helpless, his Crown still bright. E to bind him for Odile."); }
        }
    }
    public override void Down(string src)
    {
        base.Down(src);
        var q = G.Quest("teodor");
        if (teodor && !breach && !new[] { "killed", "tags", "bound", "captured", "saved", "dead" }.Contains(q))
        {
            G.SetQuest("teodor", "killed");
            G.SpawnPickup("tags", Cx(), y - 4);
            H.ToastMsg("Brass tags spill from Teodor's collar.");
        }
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (mode == "dormant")
        {
            if (standOnWater && World.IsWater(Cx(), Feet() - 3)) { standOnWater = false; Wake(true); }
            return;
        }
        if (mode == "rise")
        {
            atkT -= dt;
            if (atkT <= 0) { SetPose(LegsOK() ? "stand" : "crawl"); mode = "hunt"; }
            return;
        }
        bool crawling = pose == "crawl";
        float spd = crawling ? 80 : (K("kneeL").broken || K("kneeR").broken ? 30 : 45);
        if (mode == "grab")
        {
            vx = 0;
            if (p == null || p.grabbedBy != this) { mode = "recover"; atkT = 0.6f; return; }
            atkT -= dt;
            if (atkT <= 0) { atkT = 1.3f; p.Hurt(1, this); }
            return;
        }
        if (mode == "windup")
        {
            vx *= 0.85f; atkT -= dt;
            if (atkT <= 0) { mode = "attack"; atkT = 0.3f; vx = facing * (crawling ? 160 : 200); }
            return;
        }
        if (mode == "attack")
        {
            atkT -= dt;
            var hb = HitBox();
            if (p != null && !p.Invuln() && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h) && ArmsOK())
            {
                if (p.TryGrab(this)) { mode = "grab"; atkT = 0.4f; Snd.Grab(); return; }
            }
            if (atkT <= 0) { mode = "recover"; atkT = 0.7f; }
            return;
        }
        if (mode == "recover") { vx *= 0.8f; atkT -= dt; if (atkT <= 0) mode = "hunt"; return; }
        // hunt
        cool -= dt;
        if (Perceives()) { target = new Vector2(p.Cx(), p.Cy()); alert = 3; }
        else if (lastHeard != null && alert > 0) { target = lastHeard; alert -= dt; }
        else target = null;
        if (target != null)
        {
            Pursue(dt, target.Value.X, spd);
            float d = H.Dist(Cx(), Cy(), p.Cx(), p.Cy());
            if (p != null && !p.dead && d < 32 && cool <= 0 && ArmsOK() && Perceives())
            {
                mode = "windup"; atkT = 0.6f; cool = 1.4f;
                int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
                foreach (var k in knots) if (!k.broken) k.flare = 0.6f;
                Snd.Groan(Pan(), 0.25f);
            }
            // Armless Shuckers still body-check.
            var hb = HitBox();
            if (!ArmsOK() && p != null && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h) && cool <= 0) { p.Hurt(1, this); cool = 1.5f; }
        }
        else
        {
            vx = H.Approach(vx, 0, 300 * dt);
            if (H.Rand() < dt * 0.1f) Snd.Groan(Pan(), 0.05f);
        }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var tint = teodor ? new Palette { robe = "#2f3b44", skin = "#8b968c", cowl = null, dark = "#1b2228" } : new Palette { robe = "#3a3530", skin = "#7f8b80", cowl = "#2c2824" };
        if (burnT > 0) { tint.robe = "#1a1410"; tint.skin = "#3a2a20"; }
        DrawHuman(c, tint);
        if (teodor) { c.FillStyle = "#c9a85a"; var n = Skel()["neck"]; c.FillRect(n.X - 1, n.Y, 2, 2); }
    }
}
