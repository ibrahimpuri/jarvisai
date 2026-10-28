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
    // How much faster and quicker to strike the drowned are than the old margins: the Brine grows stronger with each
    // act (the spring tide most of all), and the crossing chosen at the start scales it.
    public static float Menace => (1 + 0.08f * (G.act - 1)) * Diff.C.menace;
    // How long a blow is telegraphed, and the pause before the next, on this crossing.
    public static float Wind(float t) => t * Diff.C.windup;
    public static float Cool(float t) => t * Diff.C.strikeCool;
    public float? barbT, climbT;
    public Knot pinKnot;
    public List<Knot> knots = new();
    public string mode = "idle", pose = "stand", awakeMode;
    // which kind of the drowned it sounds like: the newer kinds borrow a voice, pitched by their own throats
    public string voice;
    public string Voice => voice ?? type;
    // the bestiary's name for it: the variants are kinds of their own
    public virtual string Kind => type;
    public bool reknot, dragged, tetherable, corpse, boss, breach, hesper, teodor, sailor, passive, shed;
    public Vector2? target, lastHeard;
    public Dictionary<string, float> crust = new();
    public HashSet<string> severed = new(), saltLimbs = new();   // limbs torn off; limbs the Brine grew back in salt
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
    // The robed, jointed dead: they come apart limb by limb.
    public virtual bool Humanoid => false;
    public virtual Palette Pal() => new() { robe = "#3a3530", skin = "#7f8b80", cowl = "#2c2824" };

    public Knot AddKnot(string id, string type, string joint, float hp, float r = 4.5f, int sealedNeeds = 0)
    {
        hp = Diff.Knot(hp, boss);
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
    public float hitFlash;          // white flash when struck
    public float voiceT = H.Rand(2, 9), footT, senseT, busyT, lostT;   // the voice: next murmur, stride, senses
    public bool spotted;            // has seen Maren (it cried out)
    public float saltT, thrallT, flungT;   // the Hollow's verses: crusted in salt, answering the song, thrown by the sea
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
        // crusted in salt, a joint is brittle as glass: one blow shatters it (a plate just flies off)
        if (saltT > 0 && src is "rivet" or "gaff" or "harpoon" or "stomp" or "thrall" or "tine")
        {
            if (k.plated) { k.plated = false; FX.Salt(kp.X, kp.Y, 10); }
            else if (!IsSealed(k)) { dmg = 99; FX.Salt(kp.X, kp.Y, 24); Snd.Crystal(pan); }
        }
        if (IsSealed(k)) { Snd.Tink(pan); FX.Sparks(kp.X, kp.Y, 3); Flinch(); return false; }
        if (k.plated)
        {
            if (src == "gaff" || src == "fire" || src == "pry" || src == "glass")
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
        hitFlash = 0.11f;
        if (src is "rivet" or "gaff" or "harpoon" or "saw" or "stomp" or "bolt" or "sling" or "tine") G.HitStop(0.025f);
        if (src is "rivet" or "gaff" or "harpoon" or "saw" or "stomp") Hints.Did(src == "gaff" ? "gaffknot" : "knot");
        if (src == "gaff") Hints.Did("knot");
        FX.Salt(kp.X, kp.Y, 5);
        Wake(true);
        if (k.hp <= 0)
        {
            k.broken = true; k.hp = 0;
            Snd.Crack(pan); G.HitStop(0.06f); G.Shake(1.5f);
            FX.Salt(kp.X, kp.Y, 16);
            G.stats.knots[k.type] = G.stats.knots.GetValueOrDefault(k.type) + 1;
            Narr.Think("firstKnot");
            if (!G.stats.byAct.TryGetValue(G.act, out var ba)) G.stats.byAct[G.act] = ba = new Dictionary<string, int>();
            ba[k.type] = ba.GetValueOrDefault(k.type) + 1;
            OnKnotBroken(k, src);
            Gore.OnKnotBroken(this, k, src);
            Hollow.OnKnotBroken(this, src);
        }
        else
        {
            Snd.Tink(pan);
            Voices.Hurt(this, false);
            Flinch();
        }
        G.AddSwell(0.3f);
        return true;
    }
    public virtual void DamageFlesh(float dmg, float hx, float hy, string src)
    {
        if (gone) return;
        // a blow anywhere on a salted body shatters the limb nearest it
        if (saltT > 0 && src is "rivet" or "gaff" or "harpoon" or "stomp" or "thrall" or "tine")
        {
            var limb = knots.Where(k => !k.broken && k.type != "crown" && KnotPos(k) != null).OrderBy(k => (KnotPos(k).Value - new Vector2(hx, hy)).LengthSquared()).FirstOrDefault();
            if (limb != null) { DamageKnot(limb, 99, src); return; }
        }
        Snd.Flesh(Pan());
        Gore.Wound(hx, hy);
        if (src == "rivet" && !corpse && knots.Any(k => !k.broken)) Narr.Think("torso");
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
            if (best.hp <= 0) { best.broken = true; Snd.KnotBreak(Pan()); OnKnotBroken(best, src); Gore.OnKnotBroken(this, best, src); Hollow.OnKnotBroken(this, src); }
            else if (H.Rand() < 0.4f) Voices.Hurt(this, false);
        }
        Flinch();
    }
    public virtual void Flinch() { if (mode == "hunt") vx *= 0.3f; }
    public virtual void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "crown") { Down(src); return; }
        var crown = knots.Find(c => c.type == "crown" && !c.broken);
        if (crown != null && crown.sealedNeeds > 0 && !IsSealed(crown)) Narr.Think("crown");
        if (mode == "grab" && (k.type == "elbow" || k.type == "shoulder" || k.type == "arm")) ReleaseGrab();
        if (!LegsOK() && pose == "stand" && mode != "downed") SetPose("crawl");
        if (mode == "windup" && !CanAttack()) mode = "hunt";
    }
    public virtual bool CanAttack() => ArmsOK();
    public virtual void Down(string src)
    {
        mode = "downed";
        saltT = 0; thrallT = 0; flungT = 0;
        ReleaseGrab();
        vx = 0;
        SetPose("lying");
        reknot = false;
        Voices.Die(this);
        G.OnBodyDowned(this, src);
    }
    // Re-knotting: the Brine grows new knots and the body stands again.
    public virtual void StandUp()
    {
        if (gone || corpse) return;
        reknot = false;
        foreach (var k in knots) { k.broken = false; k.hp = k.max; k.plated = k.basePlated; }
        // what was torn off grows back as salt
        if (severed.Count > 0) { saltLimbs.UnionWith(severed); severed.Clear(); Narr.Think("saltLimb"); }
        G.ApplyEncrustation(this);
        SetPose("stand");
        mode = "hunt";
        anim = 0;
        Voices.Rise(this);
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
        Voices.Rise(this);
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
        if (hitFlash > 0) hitFlash -= dt;
        foreach (var k in knots) if (k.flare > 0) k.flare -= dt;
        if (burnT > 0)
        {
            burnT -= dt;
            if (H.Rand() < 36 * dt) FX.Embers(Cx() + H.Rand(-8, 8), Cy(), 2);
            if (burnT <= 0) { gone = true; mode = "gone"; G.OnBodyBurned(this); FX.Spawn(Cx(), Cy(), 20, new FXOpt { color = "#333", grav = -30 }); }
            return;
        }
        // burned away, hauled into the sea: nothing left to act
        if (gone) return;
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
        if (saltT > 0) { saltT -= dt; if (saltT <= 0) { stun = MathF.Min(stun, 0.3f); FX.Salt(Cx(), Cy(), 20); Snd.Crack(Pan()); } }
        // thrown by the Riptide: it flies until it lands, and a wall it meets hard breaks something
        if (flungT > 0)
        {
            flungT -= dt;
            float speed = MathF.Abs(vx);
            Physics(dt);
            if (hitWall != 0 && speed > 140)
            {
                flungT = 0; vx = -hitWall * 30;
                var k = knots.Where(q => !q.broken && !q.plated && !IsSealed(q)).OrderBy(q => H.Rand()).FirstOrDefault();
                if (k != null) DamageKnot(k, 1.5f, "riptide");
                G.Shake(2); Snd.Thud(Pan());
            }
            if (onGround && flungT < 0.5f) vx *= 0.85f;
            return;
        }
        if (stun > 0 && thrallT <= 0) { stun -= dt; vx *= 0.8f; Physics(dt); return; }
        if (thrallT > 0)
        {
            if (stun > 0) stun -= dt;
            thrallT -= dt;
            Hollow.ThrallThink(this, dt);
            Physics(dt);
            Voices.Tick(this, dt);
            if (thrallT <= 0) Hollow.ThrallEnds(this);
            return;
        }
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
        Voices.Tick(this, dt);
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
    // The drowned: a sodden robe with a torn hem, a rope cinch, a deep hood over a bloated face, salt
    // crusted on the cloth and weed trailing from the shoulders. The robe follows the skeleton, so it
    // hangs when they stand, drags when they crawl and lies flat when they are down.
    static readonly Dictionary<(string, float), string> shades = new();
    protected static string Shade(string hex, float k)
    {
        if (!shades.TryGetValue((hex, k), out var s)) shades[(hex, k)] = s = "#" + H.Col(hex).Lerp(H.Col("#0e0f10"), k).ToHtml(false);
        return s;
    }
    protected void DrawHuman(Canvas c, Palette pal)
    {
        var j = Skel();
        int f = facing;
        // as Hollow Knight treats its crowds: dark bodies that keep a hint of their colour, pale masks
        string robe = Shade(pal.robe, 0.6f), skin = Shade(pal.skin, 0.55f), dark = Shade(pal.dark ?? "#1a1d1e", 0.4f);
        var robeC = H.Col(robe); var skinC = H.Col(skin);
        var maskC = H.Col(pal.skin).Lerp(H.Col("#e4ded0"), 0.62f);
        var sl = j["shoulderL"]; var sr = j["shoulderR"]; var hip = j["hip"]; var head = j["head"]; var neck = j["neck"];
        var sh = (sl + sr) / 2;
        float seed = (key?.Length ?? 3) * 0.37f + x * 0.013f;
        // back limbs, in shadow
        DrawLimb(c, j["hip"], j["kneeR"], 2.6f, dark); Limb(c, "shinR", j["kneeR"], j["footR"], 2.0f, dark);
        Arm(c, "R", dark, dark, 2.2f, 1.6f);
        DrawLimb(c, j["hip"], j["kneeL"], 2.8f, robe);

        // the robe
        Vector2[] body;
        if (pose == "stand")
        {
            float sway = Math.Clamp(-vx / 40f, -2, 2) + MathF.Sin(anim * 2 + seed) * 0.4f;
            float hemY = MathF.Max(j["kneeL"].Y, j["kneeR"].Y) + 3.5f;
            var pts = new List<Vector2> { new(sh.X - f * 3.6f, sh.Y - 0.6f), new(sh.X + f * 3.2f, sh.Y - 0.8f), new(hip.X + f * 3.8f, hip.Y), new(hip.X + f * 5f + sway * 0.5f, hemY) };
            // a torn hem, ragged differently on every body
            int teeth = 6;
            for (int i = 1; i < teeth; i++)
            {
                float k = i / (float)teeth;
                float hx = H.Lerp(hip.X + f * 5f + sway * 0.5f, hip.X - f * 5.6f + sway * 1.5f, k);
                float hy = hemY - ((i % 2 == 0) ? 0 : 1.6f + TileArt.Hash((int)(seed * 100) + i, 7) * 2.2f);
                pts.Add(new Vector2(hx, hy));
            }
            pts.Add(new Vector2(hip.X - f * 5.6f + sway * 1.5f, hemY - 0.6f));
            pts.Add(new Vector2(hip.X - f * 4.2f, hip.Y));
            body = pts.ToArray();
        }
        else
        {
            var axis = hip - sh; float len = axis.Length(); var d = len > 0.01f ? axis / len : new Vector2(1, 0);
            var n = new Vector2(-d.Y, d.X);
            var tail = hip + d * 6;
            body = new[] { sh + n * 3.2f - d, sh - n * 3.2f - d, hip - n * 3.8f, tail - n * 3.2f, tail - n * 1 + d * 1.5f, tail + n * 1.2f + d * 0.6f, tail + n * 3.4f, hip + n * 3.8f };
        }
        c.Fill = robeC; c.Poly(body);
        // folds: the back half in shadow, a sheen down the front, creases
        c.Fill = new Color(0, 0, 0, 0.26f);
        if (pose == "stand") c.Poly(new[] { body[0], new Vector2(sh.X - f * 1.2f, sh.Y), new Vector2(hip.X - f * 1.6f, hip.Y + 2), new Vector2(body[^2].X + f * 2, body[^2].Y), body[^2], body[^1] });
        c.Fill = new Color(1, 1, 1, 0.07f);
        if (pose == "stand") c.Poly(new[] { body[1], body[2], body[3], new Vector2(body[3].X - f * 1.6f, body[3].Y), new Vector2(sh.X + f * 1.4f, sh.Y) });
        c.Stroke = new Color(0, 0, 0, 0.3f); c.LineWidth = 0.5f;
        if (pose == "stand") { c.Line(hip.X + f * 1.2f, hip.Y + 1.5f, body[4].X, body[4].Y - 1); c.Line(hip.X - f * 1.8f, hip.Y + 1.5f, body[^3].X, body[^3].Y - 1); }
        // the rope cinch with its knot and a trailing end
        c.Stroke = H.Col("#8a7650"); c.LineWidth = 1.1f;
        var bl = pose == "stand" ? new Vector2(hip.X - f * 4, hip.Y - 0.5f) : hip + (sh - hip).Normalized().Orthogonal() * 3.6f;
        var br = pose == "stand" ? new Vector2(hip.X + f * 3.6f, hip.Y - 0.5f) : hip - (sh - hip).Normalized().Orthogonal() * 3.6f;
        c.Line(bl.X, bl.Y, br.X, br.Y);
        c.Fill = H.Col("#a08a5a"); c.FillCircle(H.Lerp(bl.X, br.X, 0.6f), H.Lerp(bl.Y, br.Y, 0.6f), 0.9f);
        if (pose == "stand")
        {
            float kx = H.Lerp(bl.X, br.X, 0.6f), ky = H.Lerp(bl.Y, br.Y, 0.6f), sw = MathF.Sin(anim * 3 + seed) * 0.8f - vx / 60f;
            c.Stroke = H.Col("#8a7650"); c.LineWidth = 0.7f; c.BeginPath(); c.MoveTo(kx, ky); c.QuadraticCurveTo(kx + sw, ky + 2.5f, kx + sw * 1.6f, ky + 5); c.StrokePath();
        }
        // salt crusted into the cloth
        c.Fill = new Color(0.9f, 0.94f, 0.96f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            float t = TileArt.Hash((int)(seed * 50) + i, 3), s2 = TileArt.Hash((int)(seed * 50) + i, 5);
            var p = sh.Lerp(hip, t) + new Vector2((s2 - 0.5f) * 5, s2 * 2);
            c.FillRect(p.X, p.Y, 0.9f, 0.9f);
        }
        // weed hanging off a shoulder
        c.Stroke = H.Col("#2f4a30"); c.LineWidth = 0.8f;
        for (int i = 0; i < 2; i++)
        {
            float wx = sh.X - f * (1.5f + i * 1.6f), wy = sh.Y + 0.5f, sw = MathF.Sin(anim * 2.2f + i + seed) * 1.2f - vx / 50f;
            c.BeginPath(); c.MoveTo(wx, wy); c.QuadraticCurveTo(wx + sw, wy + 4, wx + sw * 1.8f - f * 0.5f, wy + 7 + i * 2); c.StrokePath();
        }

        // front shin and bare foot under the hem, front arm in its sleeve
        if (Limb(c, "shinL", j["kneeL"], j["footL"], 2.1f, skin))
        {
            c.Fill = saltLimbs.Contains("shinL") ? Gore.SALT : skinC;
            c.BeginPath(); c.Ellipse(j["footL"].X + f * 1, j["footL"].Y - 0.6f, 2, 0.9f, 0, 0, H.PI * 2); c.FillPath();
        }
        if (Arm(c, "L", robe, skin, 2.7f, 1.8f))
        {
            var hand = j["handL"];
            bool saltHand = saltLimbs.Contains("armL") || saltLimbs.Contains("forearmL");
            c.Stroke = saltHand ? Gore.SALT : skinC; c.LineWidth = 0.6f;
            var fd = (j["handL"] - j["elbowL"]).Normalized();
            for (int i = -1; i <= 1; i++) { var tip = hand + fd.Rotated(i * 0.35f) * 2.4f; c.Line(hand.X, hand.Y, tip.X, tip.Y); }
            // sleeve cuff
            if (!saltLimbs.Contains("armL")) { c.Fill = robeC; c.FillCircle(j["elbowL"].X, j["elbowL"].Y, 1.6f); }
        }

        // the head: grey and swollen, eye hollows, the jaw slack
        c.Fill = maskC; c.FillCircle(head.X, head.Y, 3.3f);
        c.Fill = new Color(0, 0, 0, 0.18f); c.BeginPath(); c.Arc(head.X, head.Y, 3.3f, H.PI * 0.1f, H.PI * 0.9f); c.FillPath();
        c.Fill = new Color(0.03f, 0.03f, 0.04f, 0.92f); c.BeginPath(); c.Ellipse(head.X + f * 1.5f, head.Y - 0.4f, 1.1f, 1.4f, 0, 0, H.PI * 2); c.FillPath();
        c.BeginPath(); c.Ellipse(head.X - f * 0.4f, head.Y - 0.6f, 0.8f, 1.1f, 0, 0, H.PI * 2); c.FillPath();
        c.FillRect(head.X + f * 1.2f - 0.6f, head.Y + 1.6f, 1.6f, 0.6f);
        if (severed.Contains("jaw"))
        {
            // the jaw torn away: a wet gape under the face
            var jw = j["jaw"];
            c.Fill = Gore.BLOOD_DARK; c.BeginPath(); c.Ellipse(jw.X - f * 0.6f, jw.Y - 0.4f, 1.9f, 1.4f, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = Gore.RAW; c.FillCircle(jw.X - f * 0.4f, jw.Y - 0.6f, 0.8f);
            c.Stroke = Gore.BLOOD_DARK; c.LineWidth = 0.5f; c.Line(jw.X, jw.Y + 0.6f, jw.X + f * 0.3f, jw.Y + 2.6f + MathF.Sin(anim * 3) * 0.4f);
        }
        else if (saltLimbs.Contains("jaw")) { var jw = j["jaw"]; c.Fill = Gore.SALT; c.BeginPath(); c.Ellipse(jw.X - f * 0.5f, jw.Y - 0.6f, 1.8f, 1.1f, 0, 0, H.PI * 2); c.FillPath(); }
        bool hostile = mode is "hunt" or "windup" or "attack" or "grab" or "inhale";
        if (hostile && !corpse) { c.Fill = new Color(0.7f, 0.95f, 1f, 0.9f); c.FillCircle(head.X + f * 1.6f, head.Y - 0.4f, 0.45f); c.Glow(head.X + f * 1.6f, head.Y - 0.4f, 2.5f, new Color(0.6f, 0.9f, 1f, 0.5f)); }
        if (pal.cowl != null)
        {
            // a deep hood: over the crown, down the back and onto the shoulders, the face in its shadow
            c.Fill = H.Col(pal.cowl);
            c.BeginPath();
            c.MoveTo(head.X + f * 3.2f, head.Y - 1.2f);
            c.QuadraticCurveTo(head.X + f * 2.4f, head.Y - 4.8f, head.X - f * 0.6f, head.Y - 4.6f);
            c.QuadraticCurveTo(head.X - f * 4.8f, head.Y - 3.8f, head.X - f * 4.6f, head.Y + 1);
            c.QuadraticCurveTo(head.X - f * 4.4f, sh.Y - 1, sh.X - f * 3.8f, sh.Y + 0.6f);
            c.LineTo(sh.X + f * 1.6f, sh.Y);
            c.QuadraticCurveTo(head.X + f * 0.4f, head.Y + 2.4f, head.X + f * 0.6f, head.Y - 0.6f);
            c.QuadraticCurveTo(head.X + f * 1.4f, head.Y - 2.4f, head.X + f * 3.2f, head.Y - 1.2f);
            c.ClosePath(); c.FillPath();
            c.Fill = new Color(0, 0, 0, 0.35f); c.BeginPath(); c.Arc(head.X + f * 1.6f, head.Y - 0.2f, 2.4f, f > 0 ? -H.PI * 0.9f : -H.PI * 0.1f, f > 0 ? -H.PI * 0.3f : -H.PI * 0.7f, f < 0); c.LineTo(head.X + f * 1.6f, head.Y - 0.2f); c.FillPath();
            c.Fill = new Color(1, 1, 1, 0.08f); c.FillRect(head.X - f * 1.6f - 1, head.Y - 4.2f, 2.4f, 0.7f);
        }
        else
        {
            // matted hair clinging to the skull
            c.Fill = H.Col(dark);
            c.BeginPath(); c.Arc(head.X - f * 0.4f, head.Y - 0.6f, 3.4f, f > 0 ? H.PI * 0.8f : -H.PI * 0.2f, f > 0 ? H.PI * 2.05f : H.PI * 1.05f, f < 0); c.FillPath();
            c.Stroke = H.Col(dark); c.LineWidth = 0.6f;
            for (int i = 0; i < 3; i++) c.Line(head.X - f * (1.6f + i), head.Y + 1, head.X - f * (1.8f + i * 1.1f), head.Y + 3.6f + i * 0.5f);
        }
        // water still running off them
        if (!World.IsWater(Cx(), Cy()) && pose == "stand" && !corpse)
        {
            float ph = (anim * 0.9f + seed) % 1;
            c.Fill = new Color(0.6f, 0.75f, 0.85f, 0.6f * (1 - ph));
            c.FillRect(body[4].X, body[4].Y + ph * 6, 0.6f, 1.2f);
        }
        if (mode == "windup" || mode == "inhale")
        {
            var jaw = j["jaw"];
            c.Fill = new Color(1, 240 / 255f, 200 / 255f, 0.5f);
            c.BeginPath(); c.Arc(jaw.X, jaw.Y, 1.2f, 0, H.PI * 2); c.FillPath();
        }
    }
    // A limb segment: drawn whole, grown back in salt, or gone with a stump left behind. True if it is there.
    bool Limb(Canvas c, string chain, Vector2 a, Vector2 b, float w, string col)
    {
        if (severed.Contains(chain)) { var d = b - a; Gore.DrawStump(c, a, d.LengthSquared() > 0.01f ? d.Normalized() : new Vector2(0, 1)); return false; }
        if (saltLimbs.Contains(chain)) { Gore.DrawSaltLimb(c, a, b, w); return true; }
        DrawLimb(c, a, b, w, col);
        return true;
    }
    bool Arm(Canvas c, string side, string upper, string fore, float wu, float wf)
    {
        var j = Skel();
        Vector2 sh = j["shoulder" + side], el = j["elbow" + side], hd = j["hand" + side];
        if (!Limb(c, "arm" + side, sh, el, wu, upper)) return false;
        if (saltLimbs.Contains("arm" + side)) { Gore.DrawSaltLimb(c, el, hd, wf); return true; }
        return Limb(c, "forearm" + side, el, hd, wf, fore);
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
            ["severed"] = new JsonArray(severed.Select(v => (JsonNode)v).ToArray()), ["salt"] = new JsonArray(saltLimbs.Select(v => (JsonNode)v).ToArray()),
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
        severed = new HashSet<string>(s.Strings("severed")); saltLimbs = new HashSet<string>(s.Strings("salt"));
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
    // Once it has her, it does not walk: it lurches, a scrabbling burst nearly as fast as she can run, then has to
    // stop and gather itself, and lurches again.
    public float lurchT, lurchCool;
    public override string Kind => knots.Any(k => k.braced) ? "braced" : "shucker";
    public Shucker(Props spec, RoomDef room) : base(spec, room)
    {
        type = "shucker";
        AddKnot("kneeL", "knee", "kneeL", 4);
        AddKnot("kneeR", "knee", "kneeR", 4);
        AddKnot("elbowL", "elbow", "elbowL", 4);
        AddKnot("elbowR", "elbow", "elbowR", 4);
        AddKnot("crown", "crown", "sternum", 5, 5, 2);
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
        float menace = Menace;
        float spd = (crawling ? 85 : (K("kneeL").broken || K("kneeR").broken ? 34 : 55)) * menace;
        if (mode == "grab")
        {
            vx = 0;
            if (p == null || p.grabbedBy != this) { mode = "recover"; atkT = 0.6f; return; }
            atkT -= dt;
            if (atkT <= 0) { atkT = Cool(1.0f); p.Hurt(1, this); }
            return;
        }
        if (mode == "windup")
        {
            vx *= 0.85f; atkT -= dt;
            if (atkT <= 0) { mode = "attack"; atkT = 0.32f; vx = facing * (crawling ? 190 : 250) * menace; }
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
            float d = H.Dist(Cx(), Cy(), p.Cx(), p.Cy());
            bool sees = Perceives();
            lurchCool -= dt; lurchT -= dt;
            if (sees && lurchCool <= 0 && d > 36 && d < 8 * H.TILE)
            {
                lurchT = H.Rand(0.8f, 1.5f); lurchCool = lurchT + H.Rand(1.0f, 2.2f) / menace;
                if (H.Rand() < 0.5f) Voices.Alert(this);
            }
            Pursue(dt, target.Value.X, lurchT > 0 ? (crawling ? 135 : 118) * menace : spd);
            if (p != null && !p.dead && d < 32 && cool <= 0 && ArmsOK() && sees)
            {
                mode = "windup"; atkT = Wind(0.5f / menace) + Legend.Hesitation; cool = Cool(1.3f); lurchT = 0;
                int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s;
                foreach (var k in knots) if (!k.broken) k.flare = 0.6f;
                Voices.Windup(this);
            }
            // Armless Shuckers still body-check.
            var hb = HitBox();
            if (!ArmsOK() && p != null && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h) && cool <= 0) { p.Hurt(1, this); cool = 1.5f; }
        }
        else
        {
            vx = H.Approach(vx, 0, 300 * dt);
        }
    }
    public override bool Humanoid => true;
    public override Palette Pal()
    {
        var tint = teodor ? new Palette { robe = "#2f3b44", skin = "#8b968c", cowl = null, dark = "#1b2228" } : new Palette { robe = "#3a3530", skin = "#7f8b80", cowl = "#2c2824" };
        if (burnT > 0) { tint.robe = "#1a1410"; tint.skin = "#3a2a20"; }
        return tint;
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
        if (teodor) { c.FillStyle = "#c9a85a"; var n = Skel()["neck"]; c.FillRect(n.X - 1, n.Y, 2, 2); }
    }
}
