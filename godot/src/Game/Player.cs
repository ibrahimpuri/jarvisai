// Maren Hult: a working diver, not a warrior.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public class WeaponState { public int tier, mag; }

public class Player : Mover
{
    const int TS = H.TILE;
    const float RUN = 150, SNEAK = 70, GRAV = 1300, JUMP = 400, COYOTE = 0.1f, BUFFER = 0.12f;

    public class SwingState { public float ax, ay, len, ang, av; }
    class RingMemo { public float x, y, t; }

    public int facing = 1;
    public int maxHp = 5, hp = 5;
    public float breath = 30, oil = 90;
    bool lowAir;   // she stayed under long enough to come up gasping
    public HashSet<string> abilities = new() { "swing" };
    public Dictionary<string, WeaponState> weapons = new() { ["driver"] = new WeaponState { tier = 0, mag = 6 } };
    public List<string> weaponOrder = new() { "driver" };
    public string cur = "driver";
    public Bag bag = new(8);
    public HashSet<string> fittingsOwned = new(), fittings = new(), grafts = new();
    public int pressure = 4;
    public bool shut, focusing;
    public string state = "normal";
    float coyote, jumpBuf;
    public float invulnT, dashT, dashCool, dashInv, dashVy;
    float hazardT;   // a dash carries her clean over crystals and through fire, though not past the drowned
    float wetT;      // just out of the water, bobbing at the surface: a jump still leaves the water
    bool airDashed, dashAir;
    float gaffCool, fireCool;
    public float reloadT, healT, craftT;
    public string healKind;
    float healTick; int healTotal, healLeft;
    Props craftRecipe;
    public Body grabbedBy;
    public int mash;
    public Body dragging;
    public SwingState swing;
    public bool boots;
    float drownT, pressureT, stepT;
    public float aim;
    float staggerT, gaffLooseT, gaffAnim, ladderCool, dropT;
    // Ledges: where her feet last left firm footing, and how high the way she left it could carry her. A lip she has
    // nearly cleared is caught only if that could have reached it, so a ledge grab never climbs a wall a jump cannot.
    public float takeoffY = 1e9f, takeoffCap;
    float mantleT; Vector2 mantleFrom, mantleTo; int mantleSide;
    const float CLIMB = 112, MANTLE_TIME = 0.26f;
    // Standing still and holding down (or up, away from a ladder) looks that way after a moment.
    public float lookT; public int lookDir;
    public int hanging;
    int lastHang; float hangGrace;
    public bool dead, inBellRoom;
    public List<Vector2> beamPath;
    RingMemo lastRing;
    bool? wasIn;

    public Player() { w = 10; h = 22; stepUp = true; bag.Add("rivets", 12); bag.Add("poultice", 1); }

    public bool Has(string a) => abilities.Contains(a);
    // A Netter's net over her: she can hardly move and cannot jump until she tears it (mash jump), cuts it with the
    // gaff, or it burns.
    public float netT;
    public void Entangle(float t)
    {
        if (dead || G.god) return;
        bool fresh = netT <= 0;
        netT = MathF.Max(netT, t);
        Snd.Grab(); FX.Spawn(Cx(), Cy(), 6, new FXOpt { color = "#8a7a5a", smin = 10, smax = 40 });
        if (fresh) H.ToastMsg("Netted! Mash {jump} to tear it, or {gaff} to cut it.", 2);
        Hints.Did("net");
    }
    public bool HasFitting(string f) => fittings.Contains(f);
    public bool Invuln() => invulnT > 0 || (dashT > 0 && dashInv > 0);
    public int WeaponTier(string wn) => weapons.TryGetValue(wn, out var ws) ? ws.tier : 0;
    public int MagSize(string wn) => wn == "driver" ? (HasFitting("feeder") ? 5 : 6) : Data.Weapons.P(wn).I("mag");
    public static bool Legendary(string wn) => wn is "tine" or "glass";
    public float BreathMax()
    {
        if (grafts.Contains("lung")) return float.PositiveInfinity;
        float b = Has("seal") ? 60 : 30;
        if (HasFitting("cork")) b *= 1.4f;
        return b;
    }
    public float OilMax() => HasFitting("tallowr") ? 135 : 90;
    public bool LanternLit() => !shut && oil > 0 && !dead;
    public Vector2 LanternPos() => new(Cx() + facing * 3, y + 9);
    public bool HeadUnder() => World.IsWater(Cx(), y + 3);
    public bool InWater() => World.IsWater(Cx(), Cy());
    public bool Locked() => reloadT > 0 || craftT > 0 || (healT > 0 && healKind == "stitch") || grabbedBy != null || staggerT > 0 || state == "mantle";

    // Beam hit test used by Bellmouths: is (x,y) inside the lantern cone?
    public bool BeamHits(float px, float py)
    {
        if (!LanternLit()) return false;
        var lp = LanternPos();
        float d = H.Dist(lp.X, lp.Y, px, py);
        float range = focusing ? 260 : 130;
        float spread = focusing ? 0.22f : 0.65f;
        if (d > range) return false;
        float a = MathF.Atan2(py - lp.Y, px - lp.X);
        return MathF.Abs(H.AngleDiff(aim, a)) < spread && Phys.Los(lp.X, lp.Y, px, py);
    }

    // ---- damage -------------------------------------------------------------
    public void Hurt(int n, Mover src = null)
    {
        if (dead || Invuln() || G.god) return;
        if (inBellRoom) return;
        if (HasFitting("lead") && n >= 2) n -= 1;
        // the crossing: on the gentlest, heavy blows land softer; on the hardest, some land harder
        var C = Diff.C;
        if (C.softHeavy && n >= 2) n -= 1;
        if (n >= 1 && C.extraHit > 0 && H.Rand() < C.extraHit) n += 1;
        hp -= n;
        invulnT = C.grace; regenT = 0;
        Snd.Hurt(); G.Shake(3); G.flash = 0.25f; G.HitStop(0.07f);
        Gore.Bleed(Cx(), Cy(), src?.Cx() ?? Cx() - facing);
        // A hit cancels the remainder of a poultice; interrupts a Stitch Kit.
        if (healT > 0) { if (healKind == "stitch") H.ToastMsg("Stitching interrupted. The kit is kept."); healT = 0; }
        craftT = 0; reloadT = 0;
        if (state == "mantle") state = "normal";
        if (src != null)
        {
            bool fromBehind = H.Sign(src.Cx() - Cx()) == -facing;
            if (grafts.Contains("ear") && fromBehind) { staggerT = 1; H.ToastMsg("The salt on your neck cracks. You stagger.", 1.5f); }
            if (grafts.Contains("arm") && n >= 2) { gaffLooseT = 2; H.ToastMsg("The crusted wrist jolts. The gaff slips loose!", 1.5f); }
            vx = H.Sign(Cx() - src.Cx()) * (n >= 2 ? 190 : 140); if (!InWater()) vy = -150;
            // a heavy blow knocks the wind out of her
            if (n >= 2 && !C.softHeavy) staggerT = MathF.Max(staggerT, 0.3f);
        }
        G.AddSwell(0.5f);
        if (hp <= 0) { hp = 0; G.OnPlayerDeath(); }
    }
    public bool TryGrab(Body enemy)
    {
        if (grabbedBy != null || Invuln() || dead || state == "swing" || G.god) return false;
        grabbedBy = enemy; state = "grabbed";
        bool cracked = enemy.knots.Any(k => (k.type == "elbow" || k.type == "arm") && k.hp < k.max);
        mash = cracked ? (Diff.C.mash + 1) / 2 : Diff.C.mash;
        healT = 0; reloadT = 0; craftT = 0;
        if (dragging != null) StopDrag();
        H.ToastMsg("Grabbed! Mash {mash} to break free.", 2);
        return true;
    }

    static int Dir() => (Inp.Held("right") ? 1 : 0) - (Inp.Held("left") ? 1 : 0);

    // ---- update ---------------------------------------------------------------
    public void Update(float dt)
    {
        Inp.OnLadder = state == "ladder" && !dead;
        if (dead) return;
        invulnT -= dt; dashCool -= dt; gaffCool -= dt; fireCool -= dt; hazardT -= dt; shutNoteT -= dt; wetT -= dt;
        staggerT -= dt; gaffLooseT -= dt;
        if (lastRing != null) lastRing.t -= dt;
        var r = G.room;
        inBellRoom = r.BellRoom && !(r.Id == "chapel" && G.Quest("tables") == "sold") && !G.BellRoomBreached(r.Id);

        // aim from the mouse, or from the pad
        if (Inp.padAim) aim = PadAim(dt);
        else
        {
            var mw = G.ScreenToWorld(Inp.mouse.x, Inp.mouse.y); float mx = mw.X, my = mw.Y;
            aim = MathF.Atan2(my - (y + 9), mx - Cx());
            padVec = new Vector2(MathF.Cos(aim), MathF.Sin(aim)) * 80;
        }
        if (state != "swing" && dragging == null && !Locked()) facing = MathF.Cos(aim) >= 0 ? 1 : -1;

        if (grabbedBy != null) { UpdateGrabbed(dt); return; }
        Recover(dt);
        UpdateLantern(dt);
        UpdateTimers(dt);

        if (state == "mantle") { UpdateMantle(dt); AfterMove(dt); return; }
        if (state == "swing") { UpdateSwing(dt); Takeoff(40); AfterMove(dt); return; }
        if (state == "ladder") { UpdateLadder(dt); Takeoff(19); AfterMove(dt); return; }
        if (InWater() && !(boots && !World.IsBrine(Cx(), Cy())) && !BootsBrine()) { UpdateSwim(dt); Takeoff(37); AfterMove(dt); return; }
        UpdateGround(dt);
        if (onGround) Takeoff(boots && InWater() ? 45 : 56);
        else if (hanging != 0) Takeoff(50);
        AfterMove(dt);
    }
    // Her feet are on something firm; `cap` is how far above them the way off it can lift her, less a margin.
    void Takeoff(float cap) { takeoffY = y + h; takeoffCap = cap; }

    // Up or down on the stick counts only when it is not mostly sideways, so a run with the stick held a little high
    // does not catch every ladder on the way past.
    static bool UpIntent() => Inp.Held("up") && (!Pad.Using || -Inp.stick.Y >= MathF.Abs(Inp.stick.X) * 0.6f);
    static bool DownIntent() => Inp.Held("down") && (!Pad.Using || Inp.stick.Y >= MathF.Abs(Inp.stick.X) * 0.6f);
    // A ladder carries on at or just above her head: there is something to climb.
    bool LadderUp() { int tx = H.FloorI(Cx() / TS); return World.IsLadder(tx, H.FloorI((y + 4) / TS)) || World.IsLadder(tx, H.FloorI((y - 6) / TS)); }
    void MountLadder()
    {
        state = "ladder"; Hints.Did("climb"); vx = 0; vy = 0; jumpBuf = 0;
        x = MathF.Floor(Cx() / TS) * TS + 8 - w / 2;
    }
    bool BootsBrine() => boots && World.IsBrine(Cx(), Cy());

    // On the gentlest crossing she gets her breath back: a pip at a time, while nothing is hunting her.
    float regenT;
    void Recover(float dt)
    {
        float every = Diff.C.regen;
        if (every <= 0 || hp >= maxHp || healT > 0) { regenT = 0; return; }
        if (G.ents.OfType<Body>().Any(b => b.Alive() && !b.corpse && !b.passive && b.mode is "hunt" or "windup" or "attack" or "grab" && H.Dist(b.Cx(), b.Cy(), Cx(), Cy()) < 320)) { regenT = 0; return; }
        regenT += dt;
        if (regenT >= every) { regenT = 0; hp++; Snd.Heal(); }
    }

    // ---- pad aim ----------------------------------------------------------------------
    // The right stick points the aim; the gyro nudges it like a mouse. With the stick left alone,
    // Maren aims where she walks, and a gentle pull settles the aim on a knot or a barrel.
    public Vector2 padVec = new(80, 0);
    float stickIdle;
    float PadAim(float dt)
    {
        var s = Inp.aimStick;
        bool manual = s.Length() > 0.3f;
        if (manual) { padVec = s.Normalized() * 80; stickIdle = 0; }
        else
        {
            stickIdle += dt;
            int d = Dir();
            if (stickIdle > 0.4f && d != 0 && MathF.Sign(padVec.X) != d && state != "swing") padVec = new Vector2(d * 80, 0);
        }
        padVec += Pad.GyroDelta;
        float len = padVec.Length();
        if (len < 20) padVec = len < 0.01f ? new Vector2(facing * 20, 0) : padVec / len * 20;
        else if (len > 160) padVec = padVec / len * 160;
        float a = MathF.Atan2(padVec.Y, padVec.X);
        return Settings.AimAssist ? Assist(a, (manual || Settings.Gyro ? 0.18f : 0.32f) * Diff.C.assist) : a;
    }
    public float Assist(float a, float cone)
    {
        var o = LanternPos();
        float best = cone, pick = float.NaN;
        void Try(float tx, float ty)
        {
            float dx = tx - o.X, dy = ty - o.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 10 || dist > 300) return;
            float ang = MathF.Atan2(dy, dx), diff = MathF.Abs(H.AngleDiff(a, ang)) + dist / 3000f;
            if (diff < best && Phys.Los(o.X, o.Y, tx, ty)) { best = diff; pick = ang; }
        }
        foreach (var e in G.ents)
        {
            if (e is Body b)
            {
                if (!b.Alive() || b.corpse || b.passive) continue;
                foreach (var k in b.knots)
                {
                    if (k.broken || b.IsSealed(k)) continue;
                    var kp = b.KnotPos(k);
                    if (kp != null) Try(kp.Value.X, kp.Value.Y);
                }
            }
            else if (e is Barrel br && !br.done) Try(br.Cx(), br.Cy());
        }
        return float.IsNaN(pick) ? a : a + H.AngleDiff(a, pick) * 0.7f;
    }

    void UpdateTimers(float dt)
    {
        // Reload locks Maren in place.
        if (reloadT > 0) { reloadT -= dt; if (reloadT <= 0) FinishReload(); }
        if (craftT > 0)
        {
            craftT -= dt;
            if (craftT <= 0) { var rc = craftRecipe; int made = Diff.Yield(rc); bag.Pay(rc.Counts("cost")); bag.Add(rc.S("makes"), made); Snd.Craft(); H.ToastMsg($"Crafted {made} {Data.ItemName(rc.S("makes"))}.", 2); }
        }
        if (healT > 0)
        {
            healT -= dt;
            if (healKind == "poultice")
            {
                healTick -= dt;
                if (healTick <= 0 && healLeft > 0) { healTick = 4f / healTotal; healLeft--; hp = Math.Min(maxHp, hp + 1); }
            }
            else if (healKind == "stitch" && healT <= 0) { bag.Remove("stitchkit", 1); hp = maxHp; Snd.Heal(); H.ToastMsg("Stitched. Full health."); }
        }
        if (Inp.Hit("reload")) { if (Hollow.held) Hollow.NextVerse(); else StartReload(); }
        if (Inp.Hit("verse")) Hollow.NextVerse();
        if (Inp.Hit("poultice")) UsePoultice();
        if (Inp.Hit("stitch")) UseStitch();
        if (Inp.Hit("quickcraft")) QuickCraft("rivets");
        if (Inp.Hit("shutter")) { shut = !shut; Snd.Ui(); Hints.Did("shutter"); H.ToastMsg(shut ? "Lantern shuttered. You are nearly invisible, and blind to weak points." : "Lantern open.", 2); }
        if (Inp.Hit("boots") && Has("boots")) { boots = !boots; Snd.Thud(); Hints.Did("boots"); H.ToastMsg(boots ? "Ballast Boots: on. You sink." : "Ballast Boots: off.", 1.5f); }
        for (int i = 1; i <= 4; i++) if (Inp.Hit("w" + i) && weaponOrder.Count >= i) { Hollow.Hold(false); SelectWeapon(weaponOrder[i - 1]); }
        if (Inp.Hit("w5")) { if (Legend.merged) SelectWeapon("tine"); else Hollow.Hold(true); }
        if (Inp.Hit("w6") && weaponOrder.Count >= 5) { Hollow.Hold(false); SelectWeapon(weaponOrder[4]); }
        int cycle = Inp.mouse.wheel + (Inp.Hit("wnext") ? 1 : 0) - (Inp.Hit("wprev") ? 1 : 0);
        if (cycle != 0)
        {
            // the Mere's Hollow sits after the weapons in the cycle
            var order = new List<string>(weaponOrder);
            if (Hollow.owned && !Legend.merged) order.Add("hollow");
            int n = order.Count, i = Hollow.held ? n - 1 : order.IndexOf(cur);
            if (n > 1)
            {
                string next = order[((i + cycle) % n + n) % n];
                if (next == "hollow") Hollow.Hold(true);
                else if (next == cur && Hollow.held) { Hollow.Hold(false); Snd.Ui(); Hints.Did("weapon"); }
                else { Hollow.Hold(false); SelectWeapon(next); }
            }
        }
        if (!Locked() && healKind != "stitch")
        {
            if (Hollow.held) Hollow.Play(this, Inp.mouse.l || Inp.Held("fire") || Inp.Held("sing"), Inp.mouse.lp || Inp.Hit("fire") || Inp.Hit("sing"), dt);
            else
            {
                if (cur == "glass") Legend.Beam(this, Inp.mouse.l || Inp.Held("fire"), Inp.mouse.lp || Inp.Hit("fire"), dt);
                else if (Inp.mouse.l || Inp.Held("fire")) Fire();
                if (Hollow.owned) Hollow.Play(this, Inp.Held("sing"), Inp.Hit("sing"), dt);
            }
            if (Inp.Hit("gaff") && netT <= 0) GaffStrike();
            if (Inp.Hit("burn")) BurnBody();
            if (Inp.Hit("interact")) G.Interact();
        }
    }

    void UpdateLantern(float dt)
    {
        focusing = (Inp.mouse.r || Inp.Held("focus")) && !shut && oil > 0;
        if (!shut && oil > 0 && !inBellRoom) oil -= dt * (focusing ? 1 : 0.5f) * (G.room.BellRoom ? 0 : 1) * Diff.C.oil;
        if (oil <= 0)
        {
            oil = 0;
            if (bag.Count("oil") > 0 && !shut) { bag.Remove("oil", 1); oil = 90; H.ToastMsg("You refill the lantern from an oil flask.", 2); }
        }
        beamPath = focusing ? TraceBeam(dt) : null;
    }
    // Focused beam path, bent by prisms.
    public List<Vector2> TraceBeam(float dt = 1 / 60f)
    {
        var lp = LanternPos();
        float x0 = lp.X, y0 = lp.Y, ang = aim;
        var path = new List<Vector2> { new(x0, y0) };
        float budget = 1100;
        var used = new HashSet<Prism>();
        var targets = G.ents.Where(e => e is Prism || e is Crystal).ToList();
        for (int seg = 0; seg < 6 && budget > 0; seg++)
        {
            float dx = MathF.Cos(ang), dy = MathF.Sin(ang);
            Ent hit = null;
            float d = 0;
            for (; d < budget; d += 3)
            {
                float px = x0 + dx * d, py = y0 + dy * d;
                if (World.SolidPx(px, py)) break;
                foreach (var e in targets)
                {
                    if (e is Prism pr && used.Contains(pr)) continue;
                    var c = e is Prism p2 ? p2.Center() : ((Crystal)e).Center();
                    if (MathF.Abs(px - c.X) < 6 && MathF.Abs(py - c.Y) < 6) { hit = e; break; }
                }
                if (hit != null) break;
            }
            path.Add(new Vector2(x0 + dx * d, y0 + dy * d));
            budget -= d;
            if (hit == null) break;
            if (hit is Crystal cr) { cr.HitByBeam(dt); break; }
            var prism = (Prism)hit;
            used.Add(prism); prism.lit = 1;
            var cc = prism.Center();
            var (ddx, ddy) = Prism.DIRS[prism.dir];
            x0 = cc.X; y0 = cc.Y; ang = MathF.Atan2(ddy, ddx);
            path.Add(new Vector2(x0, y0));
        }
        return path;
    }

    void UpdateGrabbed(float dt)
    {
        vx = 0; vy = 0;
        if (Inp.Hit("gaff") || Inp.Hit("interact") || Inp.Hit("jump"))
        {
            mash--; G.Shake(1); Snd.Gaff();
            if (mash <= 0)
            {
                var e = grabbedBy; grabbedBy = null; state = "normal";
                if (e != null) { e.mode = "recover"; e.atkT = 1.0f; e.stun = 0.6f; e.vx = -e.facing * 80; }
                invulnT = 0.6f; vx = -facing * 120;
                H.ToastMsg("Broke free!", 1.2f);
            }
        }
        // You can still shoot the grabbing arm, or sing the Riptide and let the sea tear it off you.
        bool sing = Inp.Hit("sing") || (Hollow.held && (Inp.mouse.lp || Inp.Hit("fire")));
        if (sing && Hollow.owned && Hollow.verse == "riptide") Hollow.Sing(this);
        else if ((Inp.mouse.lp || Inp.Hit("fire")) && fireCool <= 0 && !Hollow.held) Fire(true);
        if (grabbedBy != null && (grabbedBy.gone || grabbedBy.mode == "downed")) { grabbedBy = null; state = "normal"; }
    }

    void UpdateGround(float dt)
    {
        bool sneak = Inp.Held("sneak");
        // Sneaking quiets her steps; in the air there are none, so a light tilt of the stick does not shorten a jump.
        float speed = sneak && onGround ? SNEAK : RUN;
        if (dragging != null) speed = 44;
        if (healT > 0 && healKind == "poultice") speed *= 0.4f;
        if (Legend.beaming) speed = MathF.Min(speed, 55);
        if (boots && InWater()) speed = 85;
        if (netT > 0) speed *= 0.3f;
        if (Locked()) speed = 0;
        int dir = Dir();
        if (dashT > 0)
        {
            dashT -= dt; dashInv -= dt;
            if (dashT <= 0) vx *= 0.4f;
        }
        else
        {
            float accel = onGround ? 1600 : 900;
            vx = H.Approach(vx, dir * speed, accel * dt);
        }
        // Currents at Ebb drag at Maren unless she wears the boots.
        ApplyCurrent(dt);
        ApplyWind(dt);

        if (onGround) { coyote = COYOTE; airDashed = false; } else coyote -= dt;
        if (netT > 0)
        {
            // tangled: jump tears at the net instead of jumping; the gaff cuts it; fire burns it off
            netT -= dt;
            if (Inp.Hit("jump")) { netT -= 0.45f; FX.Spawn(Cx(), Cy(), 2, new FXOpt { color = "#8a7a5a", smin = 10, smax = 30 }); }
            if (Inp.Hit("gaff") || Hollowmere.Fire.BurnsAt(Cx(), Cy())) netT = 0;
            if (netT <= 0) { netT = 0; Snd.Gaff(); H.ToastMsg("Free of the net.", 1); }
            jumpBuf = 0;
        }
        else if (Inp.Hit("jump")) jumpBuf = BUFFER; else jumpBuf -= dt;

        // Ladders. Up climbs one; so does jump at a ladder with no direction held (cross on a PlayStation pad), and
        // jump held in the air catches one. Jump with a direction is still a jump, so a run past a ladder carries on.
        ladderCool -= dt;
        if (OnLadderTile() && dragging == null && !Locked())
        {
            bool jumpClimb = dir == 0 && LadderUp() && (Inp.Hit("jump") || (!onGround && Inp.Held("jump") && vy > -60 && ladderCool <= 0));
            if (UpIntent() || jumpClimb || (DownIntent() && !onGround && ladderCool <= 0)) { MountLadder(); return; }
        }
        if (DownIntent() && onGround && dragging == null && !LadderBelow()) OpenHatchBelow();
        if (onGround && dir == 0 && MathF.Abs(vx) < 10 && dragging == null && (DownIntent() || UpIntent())) { lookT += dt; lookDir = DownIntent() ? 1 : -1; }
        else lookT = 0;
        if (DownIntent() && onGround && LadderBelow() && dragging == null) { MountLadder(); y += 4; return; }

        // Wall-hang on timber (never wet stone).
        int wallDir = dir;
        bool hangTile = wallDir != 0 && WallAt(wallDir);
        if (!onGround && hangTile && vy > 0 && dragging == null)
        {
            vy = MathF.Min(vy, 20);
            hanging = wallDir;
            lastHang = wallDir; hangGrace = 0.15f;
        }
        else { hanging = 0; hangGrace -= dt; }
        if (onGround) hangGrace = 0;

        if (jumpBuf > 0 && !Locked())
        {
            if (Inp.Held("down") && onGround && OnOneWay()) { dropT = 0.25f; jumpBuf = 0; Hints.Did("drop"); }
            else if (coyote > 0 && dragging == null)
            {
                vy = boots && InWater() ? -300 : -JUMP; coyote = 0; jumpBuf = 0;
                Snd.Jump(); Noise.Emit(Cx(), y + h, 2, "jump"); Hints.Did("jump");
            }
            else if (wetT > 0 && dragging == null && World.IsWater(Cx(), y + h + 2))
            {
                // bobbing at the surface, half in and half out: the jump still carries her out of the water
                vy = -330; wetT = 0; jumpBuf = 0; Takeoff(37);
                float surf = World.SurfaceAt(Cx(), y + h + 2);
                Snd.Splash(0.2f); if (float.IsFinite(surf)) FX.Splash(Cx(), surf, 8);
            }
            else if (hanging != 0 || hangGrace > 0)
            {
                // A jump just after letting go of the wall (or while pushing away from it) still counts.
                int wd = hanging != 0 ? hanging : lastHang;
                vy = -380; vx = -wd * 190; facing = -wd; jumpBuf = 0; hanging = 0; hangGrace = 0; Snd.Jump(); Hints.Did("walljump");
            }
        }
        if (Inp.Up("jump") && vy < -120 && dashT <= 0) vy *= 0.5f;
        if (dropT > 0) dropT -= dt;

        // Dash: short, 0.2 s of invulnerability. Jet Valve adds air and water bursts.
        if (Inp.Hit("dash") && dashCool <= 0 && !Locked() && dragging == null) Dash();

        // Gaff swing
        if ((Inp.Hit("swing") || (Inp.Held("swing") && !onGround && (lastRing == null || lastRing.t <= 0.3f))) && Has("swing") && !Locked()) StartSwing(!Inp.Hit("swing"));

        // gravity
        float g = boots && InWater() ? 900 : GRAV;
        if (dashT <= 0 || !dashAir) vy += g * dt;
        float maxFall = boots && InWater() ? 220 : 520;
        if (vy > maxFall) vy = maxFall;
        if (dashT > 0 && dashAir) vy = dashVy;

        bool wasG = onGround;
        float vyBefore = vy;
        dropping = dropT > 0;
        Phys.Move(this, dt);
        if (platform != null) y += platform.dy;
        // a jump that clips a ceiling's corner slides past it rather than stopping dead
        if (hitCeil && vyBefore < -150) CornerSlip(vyBefore, dir);
        // a ledge she has nearly cleared: catch the lip and pull up onto it
        if (!onGround && dir != 0 && vy > -60 && dashT <= 0 && dragging == null && !Locked() && !DownIntent() && TryLedge(dir)) return;
        // landing
        if (onGround && !wasG && vyBefore > 250)
        {
            Noise.Emit(Cx(), y + h, NoiseScale() * 3, "land");
            Snd.Land(Surface(), vyBefore > 450 ? 0.32f : 0.22f);
            FX.Dust(Cx(), y + h, vyBefore > 450 ? 9 : 5);
            if (boots && !HasFitting("soles")) Stomp();
        }
        // footsteps
        if (onGround && MathF.Abs(vx) > 20)
        {
            stepT -= dt * MathF.Abs(vx) / 60;
            if (stepT <= 0)
            {
                stepT = 1;
                float m = (sneak ? 2 : 6) * NoiseScale();
                Noise.Emit(Cx(), y + h, m, "step");
                Snd.Footstep(Surface(), sneak ? 0.04f : 0.11f);
                if (!sneak && MathF.Abs(vx) > 140) FX.Dust(Cx() - facing * 2, y + h, 1, -facing);
            }
        }
    }
    float NoiseScale()
    {
        float s = 1;
        if (OnMetal()) s *= 1.5f;
        if (HasFitting("soles")) s *= 0.5f;
        return s;
    }
    // what is under her boots: the footsteps are of it
    public string Surface()
    {
        if (InWater() && !HeadUnder() || World.IsWater(Cx(), y + h - 2)) return "water";
        char ch = World.CharAt(H.FloorI(Cx() / TS), H.FloorI((y + h + 2) / TS));
        if (ch is 'M' or 'G') return "metal";
        if (ch is 'P' or '=' or 'T' or 'b') return "wood";
        if (ch is 'S' or 'x' or 'C') return "salt";
        if (ch == 'R') return "bone";
        return G.room?.Region switch { "gullet" => "flesh", "ribvault" or "skull" => "bone", "saltworks" => H.Rand() < 0.3f ? "salt" : "stone", _ => "stone" };
    }
    bool OnMetal()
    {
        char ch = World.CharAt(H.FloorI(Cx() / TS), H.FloorI((y + h + 2) / TS));
        return ch == 'M' || ch == 'P';
    }
    public bool OnOneWay() => World.Solid(H.FloorI(Cx() / TS), H.FloorI((y + h + 2) / TS)) == 2 || (platform != null && platform.oneWay);
    public bool OnLadderTile() => World.IsLadder(H.FloorI(Cx() / TS), H.FloorI((y + h - 4) / TS)) || World.IsLadder(H.FloorI(Cx() / TS), H.FloorI((y + 4) / TS));
    public bool LadderBelow() => World.IsLadder(H.FloorI(Cx() / TS), H.FloorI((y + h + 4) / TS));
    bool WallAt(int dir)
    {
        int tx = H.FloorI((dir > 0 ? x + w + 2 : x - 2) / TS);
        return World.IsWallHang(tx, H.FloorI((y + 6) / TS)) && World.IsWallHang(tx, H.FloorI((y + h - 6) / TS));
    }
    // Ribvault gusts: telegraphed by streaks of salt dust, they push Maren sideways.
    public float WindAt()
    {
        var r = G.room;
        if (r.Raw.A("windZones") is not JsonArray zones) return 0;
        int tx = H.FloorI(Cx() / TS) - r.Ox, ty = H.FloorI(Cy() / TS) - r.Oy;
        foreach (var zn in zones)
        {
            var z = new Props(zn.AsObject());
            if (tx < z.I("x0") || tx > z.I("x1") || ty < z.I("y0") || ty > z.I("y1")) continue;
            float period = z.F("period");
            float ph = (G.stats.time % period) / period;
            // calm for half the cycle, then a gust that alternates direction each cycle
            int dir = H.FloorI(G.stats.time / period) % 2 == 1 ? -1 : 1;
            return ph < 0.5f ? 0 : dir * z.F("force") * MathF.Sin((ph - 0.5f) * 2 * H.PI);
        }
        return 0;
    }
    void ApplyWind(float dt)
    {
        float wv = WindAt();
        if (wv == 0 || boots) return;
        vx += wv * dt * (onGround ? 0.5f : 1.6f);
    }
    void ApplyCurrent(float dt)
    {
        if (Tide.PhaseId() != "ebbing" || boots) return;
        var r = G.room;
        if (r.Seaward == 0 || !InWater()) return;
        vx += r.Seaward * 160 * dt;
    }

    float shutNoteT;
    static void ShutOverhead(char ch)
    {
        if (ch == 'K') { Snd.Thud(); H.ToastMsg("The seawall gate is shut fast above you. Its crank socket is at the foot of the tower.", 3.5f); Hints.Did("climb"); }
        else if (ch == 'Z') { Snd.Thud(); H.ToastMsg("The hatch into the Skull is shut fast above you.", 3); }
    }
    // Standing on a shut gate from its far side, she can lift its bar and let it fall open beneath her: a way back,
    // never a way through. (It also frees anyone who reached the far side some other way.)
    bool OpenHatchBelow()
    {
        int tx = H.FloorI(Cx() / TS), ty = H.FloorI((y + h + 4) / TS);
        char ch = World.CharAt(tx, ty);
        string flag = ch == 'K' ? "seawallOpen" : ch == 'Z' ? "skullOpen" : null;
        if (flag == null || World.flags.B(flag) || !World.IsLadder(tx, ty + 1)) return false;
        World.flags.Set(flag, true);
        World.MarkDirty();
        Snd.Rumble(); G.Shake(3);
        H.ToastMsg(ch == 'K' ? "From this side the gate's bar lifts easily. It swings open beneath you." : "From this side the hatch's bolt draws back. It falls open beneath you.", 4);
        return true;
    }

    void UpdateLadder(float dt)
    {
        int tx = H.FloorI(Cx() / TS);
        vx = 0;
        int dir = Dir();
        // up, or jump held with no direction, climbs; down climbs down
        bool down = DownIntent(), up = !down && (UpIntent() || (Inp.Held("jump") && dir == 0));
        int v = (down ? 1 : 0) - (up ? 1 : 0);
        vy = v * CLIMB;
        float ny = y + vy * dt;
        // stop at the top or bottom of the ladder
        int headTile = H.FloorI((ny + 2) / TS), footTile = H.FloorI((ny + h - 2) / TS);
        if (!World.IsLadder(tx, headTile) && !World.IsLadder(tx, footTile)) { state = "normal"; return; }
        // a shut gate or solid stone overhead stops the climb (a ladder never carries her through it)
        int over = H.FloorI(ny / TS);
        if (v < 0 && World.Solid(tx, over) == 1)
        {
            y = (over + 1) * TS; vy = 0;
            if (shutNoteT <= 0) { shutNoteT = 4; ShutOverhead(World.CharAt(tx, over)); }
            return;
        }
        if (v > 0 && World.Solid(tx, H.FloorI((ny + h) / TS)) == 1) { state = "normal"; y = H.FloorI((ny + h) / TS) * TS - h; vy = 0; onGround = true; ladderCool = 0.3f; return; }
        if (v < 0 && !World.IsLadder(tx, headTile) && !World.IsLadder(tx, footTile) && !World.IsLadder(tx, H.FloorI((ny + h - 10) / TS)))
        {
            // Step off the top rung onto it.
            state = "normal";
            int top = H.FloorI((ny + h + 2) / TS);
            if (World.Solid(tx, top) >= 1) { y = top * TS - h; vy = 0; onGround = true; } else vy = -120;
            return;
        }
        y = ny;
        if (MathF.Abs(vy) > 1) { stepT -= dt * 3.4f; if (stepT <= 0) { stepT = 1; Snd.Step(true, 0.05f); } }
        // jump with a direction leaps off to that side; a firm push sideways lets go
        if (Inp.Hit("jump") && dir != 0) { state = "normal"; vy = -250; vx = dir * 140; ladderCool = 0.3f; }
        else if (dir != 0 && !up && !down && Sideways()) { state = "normal"; ladderCool = 0.3f; }
        onGround = false;
    }
    void CornerSlip(float vyWas, int dir)
    {
        for (int d = 1; d <= 5; d++)
            foreach (int s0 in dir < 0 ? new[] { -1, 1 } : new[] { 1, -1 })
            {
                float nx = x + s0 * d;
                if (!Phys.BoxSolid(nx, y, w, h) && !Phys.BoxSolid(nx, y - 4, w, h)) { x = nx; vy = vyWas; hitCeil = false; return; }
            }
    }
    // A push that is mostly sideways, not a climb leaning off true.
    static bool Sideways() => !Pad.Using || Inp.stick == Vector2.Zero || (MathF.Abs(Inp.stick.X) > 0.5f && MathF.Abs(Inp.stick.X) > MathF.Abs(Inp.stick.Y) * 1.4f);

    // ---- ledges ---------------------------------------------------------------------------
    // Pushing at a wall whose lip is between her head and her feet, she catches it and pulls herself up: the jump
    // that clipped the corner still gets her there. Only lips her last takeoff could have reached count.
    bool TryLedge(int side)
    {
        float face = side > 0 ? x + w + 1.5f : x - 1.5f;
        int tx = H.FloorI(face / TS);
        if (Phys.BoxSolid(x + side * 2, y, w, h) == false) return false;   // no wall there
        int t0 = H.FloorI((y - 6) / TS), t1 = H.FloorI((y + h - 3) / TS);
        for (int ty = t0; ty <= t1; ty++)
        {
            if (World.Solid(tx, ty) != 1 || World.Solid(tx, ty - 1) == 1) continue;
            float lip = ty * TS;
            if (lip < y - 6 || lip > y + h - 3) continue;
            if (!Climbable(World.CharAt(tx, ty))) return false;
            if (lip < takeoffY - takeoffCap) return false;
            float sx = side > 0 ? tx * TS + 1 : tx * TS + TS - w - 1;
            // her own column clear up to the top, and room to stand on it
            if (Phys.BoxSolid(x, lip - h - 0.5f, w, y + h - (lip - h))) return false;
            if (Phys.BoxSolid(MathF.Min(x, sx), lip - h - 0.5f, MathF.Abs(sx - x) + w, h)) return false;
            state = "mantle"; mantleT = 0; mantleSide = side;
            mantleFrom = new Vector2(x, y); mantleTo = new Vector2(sx, lip - h - 0.01f);
            vx = vy = 0; facing = side; hanging = 0; jumpBuf = 0;
            Snd.Gaff(); Hints.Did("mantle");
            return true;
        }
        return false;
    }
    // Stone, timber, bone, metal and salt can be climbed; crystals, gates and the island's edge cannot.
    static bool Climbable(char ch) => ch is not ('x' or '_' or 'K' or 'Z' or 'h' or 'E' or 'L' or 'V' or 'Y' or 'C' or 'Q' or 'J' or 'U');
    void UpdateMantle(float dt)
    {
        mantleT += dt / MANTLE_TIME;
        float k = MathF.Min(1, mantleT);
        // up first, then over the lip
        float ky = Ease(MathF.Min(1, k / 0.65f)), kx = Ease(MathF.Max(0, (k - 0.4f) / 0.6f));
        x = H.Lerp(mantleFrom.X, mantleTo.X, kx);
        y = H.Lerp(mantleFrom.Y, mantleTo.Y, ky);
        vx = vy = 0; onGround = false;
        // a jump pressed while she pulls up is kept for the moment she stands
        if (Inp.Hit("jump")) jumpBuf = BUFFER;
        if (k >= 1)
        {
            state = "normal"; onGround = true; vx = mantleSide * 30; coyote = COYOTE;
            Snd.Footstep(Surface(), 0.1f); FX.Dust(Cx(), y + h, 3, mantleSide);
            Noise.Emit(Cx(), y + h, 3 * NoiseScale(), "step");
        }
        static float Ease(float u) => u * u * (3 - 2 * u);
    }

    void UpdateSwim(float dt)
    {
        bool brine = World.IsBrine(Cx(), Cy());
        int dx = Dir();
        int dy = (Inp.Held("down") ? 1 : 0) - (Inp.Held("up") ? 1 : 0);
        // a ladder out of the water: up, or jump with no direction, takes it
        if (OnLadderTile() && dragging == null && !Locked() && (UpIntent() || DownIntent() || (Inp.Hit("jump") && dx == 0 && LadderUp()))) { MountLadder(); return; }
        float sp = Locked() ? 0 : 105;
        if (dragging != null) sp = 40;
        // on a pad she swims where the stick points, at the speed it is pushed
        Vector2 want = Pad.Using && Inp.stick != Vector2.Zero ? Inp.stick : new Vector2(dx, dy);
        if (want.Length() > 1) want = want.Normalized();
        if (dashT > 0) { dashT -= dt; dashInv -= dt; }
        else
        {
            vx = H.Approach(vx, want.X * sp, 500 * dt);
            vy = H.Approach(vy, want.Y * sp, 500 * dt);
        }
        // buoyancy
        float buoy = 25;
        if (HasFitting("lead")) buoy = 160;
        // Brine floats you like a cork; the Gullet's pressure will not let an unsealed suit dive.
        bool crushed = World.IsDeep(Cx(), Cy()) && !Has("seal");
        if (brine || crushed) buoy = -520;
        if (dashT <= 0) vy += buoy * dt;
        if ((brine || crushed) && vy < -80) vy = -80;
        ApplyCurrent(dt);
        // surface: jump out
        float surf = World.SurfaceAt(Cx(), Cy());
        bool atSurface = y + 4 < surf + 4;
        if (Inp.Hit("jump") && atSurface && !Locked()) { vy = -330; wetT = 0; Snd.Splash(0.2f); FX.Splash(Cx(), surf, 8); }
        else if (atSurface) wetT = 0.25f;
        if (Inp.Hit("dash") && dashCool <= 0 && !Locked() && dragging == null) Dash(true);
        if (Inp.Hit("swing") && Has("swing")) StartSwing();
        coyote = 0;
        dropping = dropT > 0;
        Phys.Move(this, dt);
        if (H.Rand() < dt * 2 && HeadUnder()) FX.Bubbles(Cx() + facing * 3, y + 3);
        stepT -= dt;
        if (stepT <= 0 && (MathF.Abs(vx) > 30 || MathF.Abs(vy) > 30)) { stepT = 0.6f; Noise.Emit(Cx(), Cy(), 3 * (HasFitting("soles") ? 0.7f : 1), "swim"); }
    }

    void Dash(bool water = false)
    {
        bool jet = Has("jet");
        bool inAir = !onGround && !water;
        if ((inAir || water) && !jet)
        {
            if (water) { vx += facing * 60; dashCool = 0.6f; }
            return;
        }
        if (inAir && airDashed) return;
        float dx = Dir();
        float dy = (Inp.Held("down") ? 1 : 0) - (Inp.Held("up") ? 1 : 0);
        if (!jet || (!inAir && !water)) dy = 0;
        if (dx == 0 && dy == 0) dx = facing;
        float len = H.Hypot(dx, dy); dx /= len; dy /= len;
        float slow = HasFitting("lead") ? 0.75f : 1;
        float sp = (inAir || water ? 440 : 340) * slow;
        if (inAir || water)
        {
            if (!grafts.Contains("lung") && !(breath >= 3)) { H.ToastMsg("Not enough suit air for the Jet Valve.", 1.5f); return; }
            if (!grafts.Contains("lung")) breath -= 3;
            Snd.Burst(); FX.Bubbles(Cx(), Cy(), 8);
            airDashed = inAir;
            if (inAir) Takeoff(56);
        }
        vx = dx * sp; vy = dy * sp;
        Hints.Did(inAir || water ? "jet" : "dash");
        dashVy = dy * sp; dashAir = inAir || water;
        dashT = inAir || water ? 0.22f : 0.15f;
        // the suit is heavy: a short window, and a long breath before the next
        dashInv = Diff.C.dodge; hazardT = 0.3f;
        dashCool = Diff.C.dodgeCool;
        if (!inAir && !water) FX.Dust(Cx(), y + h, 6, -dx);
        Snd.Jump();
    }
    void Stomp()
    {
        foreach (var e0 in G.ents.ToArray())
        {
            if (e0 is not Body e || e.gone) continue;
            var hb = e.HitBox();
            if (H.RectsOverlap(x - 4, y + h - 4, w + 8, 12, hb.x, hb.y, hb.w, hb.h))
            {
                float fx = Cx(), fy = y + h;
                var k = e.knots.Where(k => !k.broken && !e.IsSealed(k) && e.KnotPos(k) != null)
                    .OrderBy(k => { var p = e.KnotPos(k).Value; return H.Dist(p.X, p.Y, fx, fy); }).FirstOrDefault();
                if (k != null) { e.DamageKnot(k, 3, "stomp"); G.Shake(3); H.ToastMsg("Stomp!", 1); }
                vy = -200;
                return;
            }
        }
    }

    // ---- gaff ------------------------------------------------------------------
    void StartSwing(bool ringOnly = false)
    {
        if (dragging != null) return;
        // Prefer a ring within reach and in sight.
        Vector2? best = null; float bd = 1e9f;
        int tx0 = H.FloorI((Cx() - 110) / TS), tx1 = H.FloorI((Cx() + 110) / TS);
        int ty0 = H.FloorI((Cy() - 110) / TS), ty1 = H.FloorI((Cy() + 40) / TS);
        for (int ty = ty0; ty <= ty1; ty++) for (int tx = tx0; tx <= tx1; tx++)
            {
                if (World.CharAt(tx, ty) != 'o') continue;
                float rx = tx * TS + 8, ry = ty * TS + 8;
                float d = H.Dist(Cx(), y + 4, rx, ry);
                // Chaining: skip the ring just released and favour rings ahead of the swing.
                if (lastRing != null && lastRing.t > 0 && lastRing.x == rx && lastRing.y == ry) continue;
                if (!onGround && MathF.Abs(vx) > 40 && H.Sign(rx - Cx()) == H.Sign(vx)) d *= 0.6f;
                if (d < 110 * (onGround ? 1 : 1.25f) && d < bd && ry < y + 8 && Phys.Los(Cx(), y + 4, rx, ry)) { bd = d; best = new Vector2(rx, ry); }
            }
        if (best != null)
        {
            var b = best.Value;
            // Keep the rope short enough that the bottom of the arc clears the floor.
            float floorD = Phys.Ray(b.X, b.Y, H.PI / 2, 200);
            float len = MathF.Max(24, MathF.Min(MathF.Min(H.Dist(Cx(), y + 4, b.X, b.Y), 78), floorD - 22));
            float ang = MathF.Atan2(Cx() - b.X, (y + 4) - b.Y);
            swing = new SwingState { ax = b.X, ay = b.Y, len = len, ang = ang, av = (vx * MathF.Cos(ang) - vy * MathF.Sin(ang)) / len };
            state = "swing"; Snd.Hook(); vy = 0; Hints.Did("swing");
            return;
        }
        if (ringOnly) return;
        // Otherwise a hook pull on a light enemy in front of you.
        foreach (var e0 in G.ents.ToArray())
        {
            if (e0 is not Body e || !e.Alive() || e.boss || e.type == "undertow") continue;
            if (e.type == "saltwright" && !(e.pinT > 0 && e.tetherable)) continue;
            if (e.pinT > 0 && e.tetherable && H.Dist(Cx(), Cy(), e.Cx(), e.Cy()) < 160)
            {
                e.pinT = 0; e.vx = H.Sign(Cx() - e.Cx()) * 260; e.vy = -120; e.stun = 1.2f;
                Snd.Hook(); H.ToastMsg("Tether: yanked off the wall!", 1);
                return;
            }
            float d = H.Dist(Cx(), Cy(), e.Cx(), e.Cy());
            float a = MathF.Atan2(e.Cy() - Cy(), e.Cx() - Cx());
            if (d < 90 && MathF.Abs(H.AngleDiff(aim, a)) < 0.5f && Phys.Los(Cx(), Cy(), e.Cx(), e.Cy()))
            {
                if (e is Gullet gl && gl.mode == "hang") gl.Drop(true);
                else { e.vx = -MathF.Cos(a) * 220; e.vy = -140; e.stun = 0.7f; }
                Snd.Hook(); H.ToastMsg("Gaff pull!", 0.8f);
                return;
            }
        }
    }
    void UpdateSwing(float dt)
    {
        var s = swing;
        int dir = Dir();
        const float g = 1100;
        // Pumping adds energy in the direction of motion, like a real swing.
        float pump = dir != 0 && (H.Sign(s.av) == dir || MathF.Abs(s.av) < 0.5f) ? dir * 7 : 0;
        s.av += (-g / s.len * MathF.Sin(s.ang) + pump) * dt;
        s.av *= 1 - 0.15f * dt;
        s.av = H.Clamp(s.av, -6, 6);
        // the rope shortens or pays out only for a push that is mostly up or down, not a pump that leans
        if (UpIntent()) s.len = MathF.Max(24, s.len - 60 * dt);
        if (DownIntent()) s.len = MathF.Min(MathF.Min(110, Phys.Ray(s.ax, s.ay, H.PI / 2, 200) - 22), s.len + 60 * dt);
        s.ang += s.av * dt;
        float nx = s.ax + MathF.Sin(s.ang) * s.len - w / 2;
        float ny = s.ay + MathF.Cos(s.ang) * s.len - 4;
        if (Phys.BoxSolid(nx, ny, w, h)) s.av *= -0.4f;
        else { x = nx; y = ny; }
        facing = s.av >= 0 ? 1 : -1;
        if (!Inp.Held("swing") || Inp.Hit("jump"))
        {
            // Keep momentum on release. A jump-release flings a little harder than simply letting go.
            float boost = Inp.Hit("jump") ? 1.2f : 1;
            vx = MathF.Cos(s.ang) * s.av * s.len * boost;
            vy = -MathF.Sin(s.ang) * s.av * s.len * boost - (Inp.Hit("jump") ? 160 : 0);
            lastRing = new RingMemo { x = s.ax, y = s.ay, t = 0.45f };
            state = "normal"; swing = null;
        }
    }
    void GaffStrike()
    {
        if (gaffCool > 0) return;
        if (gaffLooseT > 0) { Snd.Deny(); return; }
        gaffCool = 0.48f;
        gaffAnim = 0.18f;
        Snd.Gaff();
        const float reach = 26;
        float hx = Cx() + MathF.Cos(aim) * 16, hy = y + 9 + MathF.Sin(aim) * 16;
        float knotMul = HasFitting("splitter") ? 2 : 1, fleshMul = HasFitting("splitter") ? 0.5f : 1;
        bool hitSomething = false;
        // Knots in reach first.
        Body be = null; Knot bk = null; float bd = reach;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e.gone) continue;
            foreach (var k in e.knots)
            {
                if (k.broken) continue;
                var p = e.KnotPos(k); if (p == null) continue;
                float d = H.Dist(hx, hy, p.Value.X, p.Value.Y);
                if (d < bd) { bd = d; be = e; bk = k; }
            }
        }
        if (be != null)
        {
            be.DamageKnot(bk, 1 * knotMul, "gaff");
            if (grafts.Contains("arm") && !be.boss) { be.crust[bk.id] = 3; be.stun = MathF.Max(be.stun, be.type == "saltwright" ? 1.5f : 3); FX.Salt(be.Cx(), be.Cy(), 10); }
            hitSomething = true;
        }
        else
        {
            foreach (var e0 in G.ents.ToArray())
            {
                if (e0 is not Body e || e.gone) continue;
                var b = e.HitBox();
                if (hx > b.x - 10 && hx < b.x + b.w + 10 && hy > b.y - 10 && hy < b.y + b.h + 10)
                {
                    e.DamageFlesh(1 * fleshMul, hx, hy, "gaff");
                    // Gaff shove knocks light enemies back.
                    if (e.type == "shucker" || e.type == "driftling" || e.type == "grafted") { e.vx = H.Sign(e.Cx() - Cx()) * 120; e.stun = MathF.Max(e.stun, 0.25f); }
                    if (grafts.Contains("arm")) { var k = e.knots.Find(k => !k.broken); if (k != null) e.crust[k.id] = 3; e.stun = MathF.Max(e.stun, 1.5f); }
                    hitSomething = true; break;
                }
            }
        }
        foreach (var e in G.ents.ToArray())
        {
            if (H.Dist(hx, hy, e.Cx(), e.Cy()) >= reach + 8) continue;
            if (e is Barrel br) { br.OnGaff(); hitSomething = true; }
            else if (e is TunedBell tb) { tb.OnGaff(); hitSomething = true; }
        }
        Noise.Emit(hx, hy, hitSomething ? 4 : 2, "gaff");
    }

    // ---- ranged ------------------------------------------------------------------
    public void SelectWeapon(string wn) { if (wn != null && weapons.ContainsKey(wn) && wn != cur) { cur = wn; reloadT = 0; Snd.Ui(); Hints.Did("weapon"); } }
    void StartReload()
    {
        if (Legendary(cur)) return;
        var ws = weapons[cur]; var def = Data.Weapons.P(cur);
        if (reloadT > 0 || ws.mag >= MagSize(cur)) return;
        string ammo = def.S("ammo");
        if (bag.Count(ammo) <= 0) { H.ToastMsg("No " + Data.ItemName(ammo).ToLowerInvariant() + " in the satchel.", 1.5f); Snd.Empty(); return; }
        reloadT = def.F("reload") * (cur == "driver" && HasFitting("feeder") ? 0.6f : 1);
        Snd.Reload(); Hints.Did("reload");
    }
    void FinishReload()
    {
        var ws = weapons[cur]; var def = Data.Weapons.P(cur);
        int need = MagSize(cur) - ws.mag;
        ws.mag += bag.Remove(def.S("ammo"), need);
    }
    void Fire(bool grabbed = false)
    {
        if (fireCool > 0) return;
        var ws = weapons[cur]; var def = Data.Weapons.P(cur);
        if (cur == "glass") return;   // the Glass burns while held (Legend.Beam)
        if (cur == "tine")
        {
            fireCool = def.F("cooldown");
            Legend.FireTine(this, new Vector2(Cx() + MathF.Cos(aim) * 8, y + 9 + MathF.Sin(aim) * 8));
            return;
        }
        if (ws.mag <= 0)
        {
            if (Inp.mouse.lp || Inp.Hit("fire")) { Snd.Empty(); StartReload(); }
            fireCool = 0.2f;
            return;
        }
        fireCool = def.F("cooldown");
        Pad.Rumble(cur == "harpoon" ? 0.35f : 0.28f, cur == "harpoon" ? 0.45f : 0, cur == "harpoon" ? 0.16f : 0.06f);
        var lp = new Vector2(Cx() + MathF.Cos(aim) * 6, y + 9 + MathF.Sin(aim) * 6);
        if (cur == "driver")
        {
            int shots = ws.tier >= 1 && ws.mag >= 2 ? 2 : 1;
            ws.mag -= shots;
            float ang = aim;
            // Knot-seeker: snaps aim to a visible knot within 10 degrees.
            if (ws.tier >= 2)
            {
                float? best = null; float bd = 0.175f;
                foreach (var e0 in G.ents)
                {
                    if (e0 is not Body e || e.gone) continue;
                    foreach (var k in e.knots)
                    {
                        if (k.broken || e.IsSealed(k)) continue;
                        var p = e.KnotPos(k); if (p == null) continue;
                        if (Render.KnotVisibility(p.Value.X, p.Value.Y, e) < 0.3f) continue;
                        float a = MathF.Atan2(p.Value.Y - lp.Y, p.Value.X - lp.X);
                        float d = MathF.Abs(H.AngleDiff(aim, a));
                        if (d < bd && Phys.Los(lp.X, lp.Y, p.Value.X, p.Value.Y)) { bd = d; best = a; }
                    }
                }
                if (best != null) ang = best.Value;
            }
            for (int i = 0; i < shots; i++)
            {
                float a = ang + (i > 0 ? 0.03f : 0);
                var hit = ShootRay(lp.X, lp.Y, a, 340, out var obj);
                G.tracers.Add(new G.Tracer { x1 = lp.X, y1 = lp.Y, x2 = hit.x, y2 = hit.y, t = 0.08f });
                if (obj != null) obj.OnShot();
                else if (hit.kind == "knot") hit.target.DamageKnot(hit.knot, 2, "rivet");
                else if (hit.kind == "body") hit.target.DamageFlesh(2, hit.x, hit.y, "rivet");
                else if (hit.kind == "wall") { FX.Sparks(hit.x, hit.y, 3); Snd.Tink(); Ricochet(hit.x, hit.y); }
            }
            Snd.Rivet(0);
            Noise.Emit(Cx(), Cy(), 10, "rivet");
            G.Shake(1);
            G.muzzle = 0.06f;
        }
        else if (cur == "harpoon")
        {
            ws.mag--;
            G.projectiles.Add(new Projectile("bolt", lp.X, lp.Y, MathF.Cos(aim) * 520, MathF.Sin(aim) * 520, 3, 120));
            Snd.Harpoon(0);
            Noise.Emit(Cx(), Cy(), 6, "harpoon");
        }
        else if (cur == "sling")
        {
            ws.mag--;
            float life = ws.tier >= 2 ? 20 : 10;
            G.projectiles.Add(new Projectile("flare", lp.X, lp.Y, MathF.Cos(aim) * 300, MathF.Sin(aim) * 300 - 40, life, 500));
            Snd.Sling(0); Snd.Flare(0);
            Noise.Emit(Cx(), Cy(), 4, "sling");
        }
        else if (cur == "sawline") PlaceSawLine(ws);
        if (ws.mag <= 0) StartReload();
    }
    Hit ShootRay(float x0, float y0, float ang, float maxD, out Barrel obj)
    {
        obj = null;
        var hit = Combat.Hitscan(x0, y0, ang, maxD);
        // shootable objects (oil barrels)
        float ex = hit.x, ey = hit.y;
        foreach (var e in G.ents)
        {
            if (e is not Barrel b || b.done) continue;
            float t = Combat.RayRect(x0, y0, ex, ey, b.x, b.y, b.w, b.h);
            if (t >= 0 && t < (hit.t == 0 ? 1 : hit.t)) { obj = b; return new Hit { kind = "object", x = x0 + (ex - x0) * t, y = y0 + (ey - y0) * t, t = t }; }
        }
        return hit;
    }
    void Ricochet(float rx, float ry)
    {
        // A rivet against a bell or metal pulls attention there.
        char ch = World.CharAt(H.FloorI(rx / TS), H.FloorI(ry / TS));
        if (ch == 'M') Noise.Emit(rx, ry, 8, "clang");
    }
    void PlaceSawLine(WeaponState ws)
    {
        float ly = y + h - 6;
        var lines = G.sawlines.Where(s => !s.dead).ToList();
        int maxLines = ws.tier >= 1 ? 2 : 1;
        if (lines.Count >= maxLines) lines[0].dead = true;
        float x2 = Cx();
        for (int i = 0; i < 10 * TS; i += 4) { x2 = Cx() + facing * i; if (World.SolidPx(x2, ly)) break; }
        ws.mag--;
        G.sawlines.Add(new SawLine(Cx(), x2, ly, ws.tier >= 2));
        Snd.Hook(); H.ToastMsg("Saw-Line strung.", 1);
    }

    // ---- healing and crafting --------------------------------------------------------
    public void UsePoultice()
    {
        if (healT > 0 || Locked()) return;
        if (bag.Count("poultice") <= 0) { H.ToastMsg("No poultices.", 1.5f); Snd.Deny(); return; }
        if (hp >= maxHp) { H.ToastMsg("You are not hurt.", 1); return; }
        bag.Remove("poultice", 1);
        healKind = "poultice"; healT = 4;
        healTotal = healLeft = Diff.C.poultice + (HasFitting("bilge") ? 1 : 0);
        healTick = 4f / healTotal;
        Snd.Heal(); Hints.Did("poultice");
    }
    public void UseStitch()
    {
        if (healT > 0 || Locked()) return;
        if (bag.Count("stitchkit") <= 0) { H.ToastMsg("No Stitch Kits.", 1.5f); Snd.Deny(); return; }
        if (hp >= maxHp) { H.ToastMsg("You are not hurt.", 1); return; }
        healKind = "stitch"; healT = 4; vx = 0; Hints.Did("poultice");
        H.ToastMsg("Kneeling to stitch (4 s)...", 2);
    }
    public bool QuickCraft(string id)
    {
        var rc = Data.Recipes.Find(r => r.S("id") == id);
        if (rc == null || craftT > 0 || Locked()) return false;
        string makes = rc.S("makes");
        if (!bag.Has(rc.Counts("cost"))) { H.ToastMsg("Missing materials for " + Data.ItemName(makes) + ".", 1.5f); Snd.Deny(); return false; }
        if (bag.Room(makes) < Diff.Yield(rc)) { H.ToastMsg("No room in the satchel.", 1.5f); Snd.Deny(); return false; }
        if (rc.S("where") == "bench") return false;
        craftRecipe = rc; craftT = rc.F("time"); vx = 0; Hints.Did("craft");
        H.ToastMsg($"Crafting {Data.ItemName(makes)}... ({rc.F("time")} s, crouched)", rc.F("time"));
        return true;
    }

    // ---- bodies: drag and burn ----------------------------------------------------------
    public Body NearestDownedBody(float r = 22)
    {
        Body best = null; float bd = r;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e.gone || e.boss) continue;
            if (!(e.mode == "downed" || e.corpse || e.mode == "captured")) continue;
            float d = MathF.Abs(e.Cx() - Cx()) + MathF.Abs(e.Feet() - (y + h)) * 0.5f;
            if (d < bd + e.w / 2) { bd = d; best = e; }
        }
        return best;
    }
    public void StartDrag(Body e) { dragging = e; e.dragged = true; Snd.Hook(); }
    public void StopDrag()
    {
        if (dragging == null) return;
        var e = dragging; e.dragged = false; e.vy = 0; e.vx = 0;
        dragging = null;
        if (World.AboveTideLine(G.room, e.Cx(), e.Feet() - 2) && e.mode == "downed") H.ToastMsg("Hauled above the tide line. It will stay down.", 2.5f);
    }
    void BurnBody()
    {
        var e = NearestDownedBody(26);
        if (e == null) return;
        if (oil < 30) { H.ToastMsg("Not enough lantern oil to burn it (needs a third of a flask).", 2); Snd.Deny(); return; }
        oil -= 30;
        if (dragging == e) StopDrag();
        e.Burn();
        Noise.Emit(e.Cx(), e.Cy(), 6, "fire");
        H.ToastMsg("You douse it with oil and set it alight. It will not stand again.", 2.5f);
    }

    void AfterMove(float dt)
    {
        // Dragged body follows behind.
        if (dragging != null)
        {
            var e = dragging;
            if (e.gone || state == "ladder" || state == "swing") StopDrag();
            else
            {
                float tx = Cx() - facing * 16 - e.w / 2;
                e.vx = (tx - e.x) * 8; e.vy = InWater() ? (y + h - e.h - e.y) * 6 : e.vy + 1100 * dt;
                e.facing = -facing;
                bool save = e.dragged; e.dragged = false;
                Phys.Move(e, dt);
                e.dragged = save;
                if (MathF.Abs(e.Cx() - Cx()) > 60) StopDrag();
            }
        }
        // breath
        float max = BreathMax();
        bool vent = World.CharAt(H.FloorI(Cx() / TS), H.FloorI((y + 4) / TS)) == 'a';
        if (HeadUnder() && !vent && float.IsFinite(max))
        {
            breath -= dt;
            if (H.Rand() < dt * 3) FX.Bubbles(Cx() + facing * 3, y + 2);
            if (breath <= 0)
            {
                breath = 0; drownT -= dt;
                if (drownT <= 0) { drownT = 2; invulnT = 0; Snd.Choke(); Hurt(1); H.ToastMsg("Drowning!", 1); }
            }
            lowAir = breath < max * 0.3f;
        }
        else
        {
            if (lowAir) { lowAir = false; Snd.Surface(); }
            breath = MathF.Min(float.IsFinite(max) ? max : 30, breath + dt * (vent ? 12 : 8));
            drownT = 0;
        }
        // Gullet pressure without the seal.
        if (World.IsDeep(Cx(), Cy()) && !Has("seal"))
        {
            pressureT -= dt;
            G.pressureWarn = 1;
            if (pressureT <= 0) { pressureT = 1.5f; invulnT = 0; Hurt(1); H.ToastMsg("The pressure crushes your helmet. You need a Pressure Seal.", 2); }
        }
        else pressureT = 0.5f;
        // salt crystals and fire
        if (hazardT <= 0 && World.CharAt(H.FloorI(Cx() / TS), H.FloorI((y + h - 2) / TS)) == 'x') { Hurt(1); vy = -260; Hints.Cut(); }
        if (hazardT <= 0 && Hollowmere.Fire.BurnsAt(Cx(), y + h - 4)) Hurt(1);
        // splash in/out
        bool inW = InWater();
        if (inW != wasIn)
        {
            if (wasIn == true && !inW) Hints.Did("swimout");
            if (MathF.Abs(vy) > 60) { Snd.Splash(0.2f); FX.Splash(Cx(), World.SurfaceAt(Cx(), Cy()), 8); Noise.Emit(Cx(), Cy(), 4, "splash"); }
            wasIn = inW;
        }
        if (gaffAnim > 0) gaffAnim -= dt;
    }

    // ---- render -------------------------------------------------------------------------------
    // Maren is drawn as a small rig: legs solved from where the feet should be, a long oilskin coat that
    // trails behind her, a brass diving helmet and corselet, the air bottle on her back, and her working
    // arm following the aim. All of it is vector work, so it stays sharp at any window size.
    float animT, runPhase, coatSway, landT, lastVy, bob;
    bool wasOnGround = true;

    // The cloak hangs from her shoulder as a chain of points moved by verlet integration: it lags behind
    // when she runs, lifts when she falls and drifts in water.
    readonly Vector2[] cloak = new Vector2[6], cloakPrev = new Vector2[6];
    bool cloakReady;
    void UpdateCloak(Vector2 anchor, float dt, int f)
    {
        const float SEG = 2.4f;
        int n = cloak.Length;
        if (!cloakReady || (cloak[0] - anchor).Length() > 30)
        {
            for (int i = 0; i < n; i++) cloak[i] = cloakPrev[i] = anchor + new Vector2(-f * i * SEG * 0.6f, i * SEG * 0.8f);
            cloakReady = true;
        }
        cloak[0] = cloakPrev[0] = anchor;
        if (dt <= 0) return;
        bool wet = InWater();
        var grav = new Vector2(-f * (wet ? 10 : 30), wet ? 40 : 420);
        for (int i = 1; i < n; i++)
        {
            var v = (cloak[i] - cloakPrev[i]) * (wet ? 0.85f : 0.93f);
            cloakPrev[i] = cloak[i];
            cloak[i] += v + grav * dt * dt;
        }
        for (int it = 0; it < 3; it++)
            for (int i = 1; i < n; i++)
            {
                var d = cloak[i] - cloak[i - 1]; float len = d.Length();
                if (len > 0.001f) cloak[i] = cloak[i - 1] + d / len * SEG;
                // it hangs behind her, never through her
                if ((cloak[i].X - anchor.X) * f > 0.5f) cloak[i].X = anchor.X + f * 0.5f;
            }
    }

    static Vector2 Knee(Vector2 hip, Vector2 foot, float len, int dir)
    {
        var d = foot - hip; float dist = MathF.Min(d.Length(), len * 2 - 0.01f);
        if (dist < 0.01f) return hip + new Vector2(0, len);
        var mid = hip + d.Normalized() * dist / 2;
        float off = MathF.Sqrt(MathF.Max(0, len * len - dist * dist / 4));
        var n = new Vector2(-d.Y, d.X).Normalized() * dir;
        return mid + n * off;
    }

    public void Draw(Canvas c)
    {
        if (dead) return;
        float dt = Math.Clamp(G.Now - animT, 0, 0.05f); animT = G.Now;
        float x0 = Cx(), gy = y + h; int f = facing;
        bool blink = invulnT > 0 && H.FloorI(invulnT * 20) % 2 == 0;
        if (blink) c.GlobalAlpha = 0.5f;
        bool kneel = (healT > 0 && healKind == "stitch") || craftT > 0;
        bool swim = InWater() && !onGround && state == "normal";
        bool sneak = Inp.Held("sneak") && onGround && !kneel;
        float speed = MathF.Abs(vx);
        // timing: the stride follows distance covered, the coat lags behind the body
        if (onGround && speed > 20) runPhase += dt * speed / 9.5f;
        else if (swim) runPhase += dt * 6;
        else if (state == "ladder" && MathF.Abs(vy) > 10) runPhase += dt * 9;
        else if (state == "mantle") runPhase += dt * 14;
        coatSway = H.Lerp(coatSway, Math.Clamp(vx / 160f, -1.5f, 1.5f) * -f + (onGround ? 0 : Math.Clamp(-vy / 300f, -1, 1)), MathF.Min(1, dt * 8));
        if (onGround && !wasOnGround && lastVy > 200) landT = 0.14f;
        wasOnGround = onGround; lastVy = vy;
        if (landT > 0) landT -= dt;
        float run = onGround && speed > 20 ? MathF.Min(1, speed / 120f) : 0;
        bob = onGround && speed < 20 && !kneel ? MathF.Sin(G.Now * 2.2f) * 0.35f : run > 0 ? MathF.Abs(MathF.Sin(runPhase)) * -1.1f * run : 0;
        float crouch = kneel ? 6 : sneak ? 2.5f : landT > 0 ? landT / 0.14f * 2.5f : 0;

        // squash on landing, a slight lean into the run
        c.Save();
        c.Translate(x0, gy);
        float lean = state == "ladder" ? 0 : dashT > 0 ? 0.28f * f : run * 0.12f * MathF.Sign(vx) + (swim ? 0.5f * MathF.Sign(vx) * MathF.Min(1, speed / 80f) : 0);
        c.Rotate(lean);
        c.Translate(-x0, -gy);

        float hipY = gy - 9.5f + crouch + bob, shY = gy - 17 + crouch + bob, headY = gy - 21.5f + crouch * 0.9f + bob;
        var hip = new Vector2(x0, hipY);
        // ---- legs ----
        Vector2 footA, footB;
        if (kneel) { footA = new(x0 + f * 4, gy); footB = new(x0 - f * 3, gy); }
        else if (state == "ladder" || state == "mantle") { float s = MathF.Sin(runPhase); footA = new(x0 - 2, gy - 1.5f + s * 2); footB = new(x0 + 2, gy - 1.5f - s * 2); }
        else if (swim) { float s = MathF.Sin(runPhase); footA = new(x0 - f * 3 + s * 1.5f, gy + 1 + s * 2); footB = new(x0 - f * 4 - s * 1.5f, gy + 1 - s * 2); }
        else if (!onGround) { footA = vy < 0 ? new(x0 + f * 3.5f, gy - 3.5f) : new(x0 + f * 2.5f, gy - 0.5f); footB = vy < 0 ? new(x0 - f * 2, gy - 1.5f) : new(x0 - f * 3, gy - 1.5f); }
        else if (run > 0)
        {
            float pa = runPhase, pb = runPhase + H.PI;
            footA = new(x0 + f * MathF.Sin(pa) * 5 * run, gy - MathF.Max(0, MathF.Cos(pa)) * 3.2f * run);
            footB = new(x0 + f * MathF.Sin(pb) * 5 * run, gy - MathF.Max(0, MathF.Cos(pb)) * 3.2f * run);
        }
        else { footA = new(x0 + f * 2, gy); footB = new(x0 - f * 1.5f, gy); }
        void Leg(Vector2 foot, bool back)
        {
            var knee = Knee(hip, foot, 5.1f, -f);
            c.Stroke = back ? H.Col("#0e0f10") : H.Col("#17191b"); c.LineWidth = 2.6f;
            c.BeginPath(); c.MoveTo(hip.X, hip.Y); c.LineTo(knee.X, knee.Y); c.LineTo(foot.X, foot.Y - 1); c.StrokePath();
            // boot, brass toe cap
            c.Fill = back ? H.Col("#0c0a08") : H.Col("#16120e");
            c.BeginPath(); c.MoveTo(foot.X - f * 1.6f, foot.Y - 2.6f); c.LineTo(foot.X + f * 1.4f, foot.Y - 2.6f); c.QuadraticCurveTo(foot.X + f * 3.4f, foot.Y - 1.4f, foot.X + f * 3.2f, foot.Y); c.LineTo(foot.X - f * 1.8f, foot.Y); c.ClosePath(); c.FillPath();
            c.Fill = boots && !back ? H.Col("#d0a050") : H.Col("#8a6a30"); c.FillRect(Math.Min(foot.X + f * 1.8f, foot.X + f * 3.1f), foot.Y - 1.3f, 1.3f, 1.3f);
            if (boots && Has("boots")) { c.Fill = H.Rgba(255, 210, 120, 0.5f); c.FillRect(foot.X - 1.8f, foot.Y - 0.6f, 5, 0.6f); }
        }
        Leg(footB, true);

        // ---- air bottle and hose on her back ----
        float bx = x0 - f * 4.6f;
        c.FillRectGradH(bx - 1.7f, shY - 1, 3.4f, 8, H.Col("#8a6a34"), H.Col("#3a2810"));
        c.Fill = H.Col("#4a3418"); c.FillRect(bx - 1.9f, shY + 1.5f, 3.8f, 0.8f); c.FillRect(bx - 1.9f, shY + 5, 3.8f, 0.8f);
        c.Fill = H.Col("#7a7a72"); c.FillRect(bx - 0.6f, shY - 2.2f, 1.2f, 1.4f);
        c.Stroke = H.Col("#2a2a26"); c.LineWidth = 1.1f;
        c.BeginPath(); c.MoveTo(bx, shY - 2); c.QuadraticCurveTo(bx - f * 1.5f, headY - 4, x0 - f * 3.5f, headY - 1.5f); c.StrokePath();

        // ---- the oilskin coat ----
        float hem = gy - 4.5f + crouch * 0.4f, sway = coatSway * 2.2f + (run > 0 ? MathF.Sin(runPhase * 2) * 0.6f : 0);
        float flap = swim ? 2 : 0;
        var coat = new[]
        {
            new Vector2(x0 - f * 3.8f, shY), new Vector2(x0 + f * 3.6f, shY),
            new Vector2(x0 + f * 4.4f, hipY), new Vector2(x0 + f * 4.6f + sway * 0.3f, hem),
            new Vector2(x0 + f * 1.5f + sway * 0.6f, hem + 0.8f),
            new Vector2(x0 - f * 1.5f + sway, hem + 1.2f + flap),
            new Vector2(x0 - f * 5.6f + sway * 1.4f, hem + 0.4f + flap - MathF.Max(0, -vy / 120f)),
            new Vector2(x0 - f * 4.8f, hipY),
        };
        c.Fill = H.Col("#1a1814"); c.Poly(coat);
        // sheen down the front, shadow down the back, the seam and the belt
        c.Fill = H.Rgba(220, 200, 150, 0.07f); c.Poly(new[] { coat[1], coat[2], coat[3], new Vector2(x0 + f * 2.8f + sway * 0.4f, hem), new Vector2(x0 + f * 2.2f, shY + 1) });
        c.Fill = H.Rgba(0, 0, 0, 0.28f); c.Poly(new[] { coat[0], new Vector2(x0 - f * 2.2f, shY + 1), new Vector2(x0 - f * 2.6f + sway, hem + 1), coat[6], coat[7] });
        c.Stroke = H.Rgba(0, 0, 0, 0.35f); c.LineWidth = 0.5f; c.Line(x0 + f * 0.8f, hipY + 1, x0 + f * 0.8f + sway * 0.5f, hem);
        c.Fill = H.Col("#2a2016"); c.FillRect(x0 - 4.6f, hipY - 1.6f, 9.2f, 1.7f);
        c.Fill = H.Col("#c9a050"); c.FillRect(x0 + f * 0.6f - 0.8f, hipY - 1.7f, 1.6f, 1.9f);
        // satchel on the hip
        c.Fill = H.Col("#2a2016"); c.FillRect(x0 - f * 5.2f - 1.6f, hipY - 0.5f, 3.2f, 3.6f);
        c.Fill = H.Col("#1a140c"); c.FillRect(x0 - f * 5.2f - 1.6f, hipY - 0.5f, 3.2f, 1.2f);
        // the Mere's Hollow on her belt, its seam glowing sea-green while it has a verse in it
        if (Hollow.owned && !Hollow.held)
        {
            float bx2 = x0 + f * 3.2f - 1.3f;
            c.Fill = H.Col("#d8ccae"); c.FillRect(bx2, hipY + 0.2f, 2.6f, 2.2f);
            c.Fill = H.Col("#8a7a54"); c.FillRect(bx2, hipY + 0.9f, 2.6f, 0.4f);
            if (Hollow.resonance >= Hollow.Cur.cost) { c.Fill = H.Rgba(170, 255, 225, 0.9f); c.FillRect(bx2 + 0.2f, hipY + 0.9f, 2.2f, 0.4f); c.Glow(bx2 + 1.3f, hipY + 1.2f, 4, H.Rgba(120, 240, 210, 0.3f)); }
        }

        Leg(footA, false);

        // ---- a short cloak off the shoulders, trailing on its own little rope of points ----
        UpdateCloak(new Vector2(x0 - f * 2.4f, shY + 0.4f), dt, f);
        {
            int n = cloak.Length;
            var left = new Vector2[n]; var right = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var d = (cloak[Math.Min(n - 1, i + 1)] - cloak[Math.Max(0, i - 1)]).Normalized();
                var nn = new Vector2(-d.Y, d.X);
                float wdt = H.Lerp(3.4f, 0.9f, i / (float)(n - 1));
                left[i] = cloak[i] + nn * wdt; right[i] = cloak[i] - nn * wdt;
            }
            var poly = left.Concat(right.Reverse()).ToArray();
            c.Fill = H.Col("#100e0b"); c.Poly(poly);
            c.Stroke = H.Rgba(200, 180, 140, 0.12f); c.LineWidth = 0.5f;
            c.BeginPath(); c.MoveTo(left[0].X, left[0].Y); for (int i = 1; i < n; i++) c.LineTo(left[i].X, left[i].Y); c.StrokePath();
        }

        // ---- brass corselet over the shoulders ----
        c.Fill = H.Col("#7a5a28");
        c.BeginPath(); c.MoveTo(x0 - 5, shY + 0.5f); c.QuadraticCurveTo(x0, shY - 2.4f, x0 + 5, shY + 0.5f); c.LineTo(x0 + 3.8f, shY + 3.2f); c.QuadraticCurveTo(x0, shY + 4.2f, x0 - 3.8f, shY + 3.2f); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 230, 160, 0.35f); c.FillRect(x0 + f * 1 - 1.5f, shY - 0.8f, 3, 0.7f);
        c.Fill = H.Col("#5a4018"); for (int i = -2; i <= 2; i++) c.FillCircle(x0 + i * 1.8f, shY + 2.2f - MathF.Abs(i) * 0.5f, 0.35f);

        // ---- the lantern on her chest ----
        float lx = x0 + f * 3, ly = gy - 13 + crouch * 0.7f + bob;
        c.Fill = H.Col("#2a2420"); c.FillRect(lx - 1.8f, ly - 2.4f, 3.6f, 0.8f); c.FillRect(lx - 1.8f, ly + 1.6f, 3.6f, 0.8f);
        c.Fill = LanternLit() ? H.Col("#ffd27a") : H.Col("#3a3020"); c.FillRect(lx - 1.3f, ly - 1.6f, 2.6f, 3.2f);
        if (LanternLit()) { c.Fill = H.Rgba(255, 250, 225, 0.95f); c.FillRect(lx - 0.4f, ly - 0.6f, 0.8f, 1.4f); }
        c.Fill = H.Col("#2a2420"); c.FillRect(lx - 0.25f, ly - 1.6f, 0.5f, 3.2f);
        if (shut) { c.Fill = H.Col("#3a3226"); c.FillRect(lx - 1.5f, ly - 1.7f, 3, 3.4f); }

        // ---- the helmet ----
        float hr = 5.6f;
        c.Fill = H.Col("#c8973f"); c.FillCircle(x0, headY, hr);
        c.Fill = H.Rgba(50, 28, 6, 0.55f); c.BeginPath(); c.Arc(x0, headY, hr, H.PI * 0.05f, H.PI * 0.95f); c.QuadraticCurveTo(x0, headY + 1.5f, x0 + hr * 0.99f, headY + 0.3f); c.FillPath();
        c.Fill = H.Rgba(255, 240, 190, 0.6f); c.BeginPath(); c.Ellipse(x0 - f * 1.6f, headY - 2.6f, 2.6f, 1.4f, -0.5f * f, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(255, 250, 230, 0.8f); c.FillCircle(x0 - f * 2.2f, headY - 3, 0.6f);
        // top valve and the bonnet seam
        c.Fill = H.Col("#7a5a24"); c.FillRect(x0 - 0.9f, headY - hr - 1.3f, 1.8f, 1.6f);
        c.Stroke = H.Rgba(60, 40, 10, 0.6f); c.LineWidth = 0.5f; c.BeginPath(); c.Arc(x0, headY + 2.5f, hr * 0.95f, -H.PI * 0.95f, -H.PI * 0.05f); c.StrokePath();
        // side port at the back of the head
        c.Fill = H.Col("#3a3424"); c.FillCircle(x0 - f * 2.6f, headY + 0.4f, 1.2f);
        c.Stroke = H.Col("#6a4a1c"); c.LineWidth = 0.5f; c.BeginPath(); c.Arc(x0 - f * 2.6f, headY + 0.4f, 1.2f, 0, H.PI * 2); c.StrokePath();
        // the faceplate: glass, frame, two guard bars
        float px = x0 + f * 2.4f, py = headY + 0.2f, pr = 2.9f;
        bool under = HeadUnder();
        bool glowing = LanternLit() && !under;
        c.Fill = glowing ? H.Col("#ffd98e") : under ? H.Col("#4a7a88") : H.Col("#141c20"); c.FillCircle(px, py, pr);
        if (glowing) { c.Fill = H.Rgba(255, 250, 230, 0.95f); c.FillCircle(px + f * 0.4f, py - 0.3f, pr * 0.45f); }
        if (!glowing) { c.Fill = H.Rgba(160, 200, 210, under ? 0.4f : 0.22f); c.BeginPath(); c.Arc(px, py, pr * 0.8f, H.PI * 1.05f, H.PI * 1.55f); c.LineTo(px, py); c.FillPath(); }
        // the glass cracks as she takes hits
        if (hp <= 2)
        {
            c.Stroke = H.Rgba(235, 245, 250, hp <= 1 ? 0.9f : 0.6f); c.LineWidth = 0.35f;
            c.Line(px - f * 0.6f, py - 0.4f, px + f * 1.9f, py - 2.1f); c.Line(px - f * 0.6f, py - 0.4f, px - f * 1.8f, py + 1.6f);
            if (hp <= 1) { c.Line(px - f * 0.6f, py - 0.4f, px + f * 2.2f, py + 1.2f); c.Line(px + f * 0.8f, py - 1.3f, px + f * 0.4f, py - 2.6f); }
        }
        c.Stroke = H.Col("#d4a85a"); c.LineWidth = 0.9f; c.BeginPath(); c.Arc(px, py, pr, 0, H.PI * 2); c.StrokePath();
        c.Stroke = H.Col("#7a5a24"); c.LineWidth = 0.45f; c.Line(px - 1, py - pr + 0.4f, px - 1, py + pr - 0.4f); c.Line(px + 1, py - pr + 0.4f, px + 1, py + pr - 0.4f);
        if (glowing) c.Glow(px, py, 6, H.Rgba(255, 210, 140, 0.35f));
        // the collar where helmet meets corselet, studded
        c.Fill = H.Col("#7a5a24"); c.FillRect(x0 - 4.2f, headY + hr - 1.6f, 8.4f, 1.8f);
        c.Fill = H.Col("#d4b070"); for (int i = -1; i <= 1; i++) c.FillCircle(x0 + i * 2.8f, headY + hr - 0.7f, 0.4f);
        // bubbles from the valve under water
        if (under)
            for (int i = 0; i < 3; i++)
            {
                float ph = (G.Now * 0.8f + i / 3f) % 1;
                c.Stroke = H.Rgba(200, 230, 240, 0.6f * (1 - ph)); c.LineWidth = 0.4f;
                c.BeginPath(); c.Arc(x0 + MathF.Sin(ph * 9 + i) * 1.2f, headY - hr - 1 - ph * 12, 0.6f + ph * 0.8f, 0, H.PI * 2); c.StrokePath();
            }

        // ---- the free arm swings, the working arm follows the aim ----
        var sh = new Vector2(x0 + f * 1.2f, shY + 1.2f);
        if (state != "ladder")
        {
            float sw = run > 0 ? -MathF.Sin(runPhase) * 0.6f * run : swim ? MathF.Sin(runPhase) * 0.8f : 0.1f;
            var back = new Vector2(x0 - f * 1.4f, shY + 1.2f);
            var hand = back + new Vector2(-f * MathF.Sin(sw) * 6, MathF.Cos(sw) * 6);
            c.Stroke = H.Col("#121110"); c.LineWidth = 2.1f; c.Line(back.X, back.Y, hand.X, hand.Y);
            c.Fill = H.Col("#221a12"); c.FillCircle(hand.X, hand.Y, 1.1f);
        }
        float aimA = state == "swing" ? MathF.Atan2(swing.ay - sh.Y, swing.ax - sh.X) : state == "ladder" ? -H.PI / 2 : state == "mantle" ? -H.PI / 2 + mantleSide * 0.5f : aim;
        float reach = state == "ladder" ? 5 : 6.4f;
        var elbowBend = new Vector2(MathF.Cos(aimA + 0.5f * f), MathF.Sin(aimA + 0.5f * f)) * 3.4f;
        var hx2 = sh + new Vector2(MathF.Cos(aimA), MathF.Sin(aimA)) * reach;
        var elbow = sh + elbowBend;
        c.Stroke = H.Col("#18160f"); c.LineWidth = 2.3f;
        c.BeginPath(); c.MoveTo(sh.X, sh.Y); c.LineTo(elbow.X, elbow.Y); c.LineTo(hx2.X, hx2.Y); c.StrokePath();
        c.Fill = H.Col("#221a12"); c.FillCircle(hx2.X, hx2.Y, 1.2f);
        float hx = hx2.X, hy = hx2.Y;
        if (state == "swing")
        {
            c.Stroke = H.Col("#9a9a90"); c.LineWidth = 0.9f; c.Line(hx, hy, swing.ax, swing.ay);
            c.Fill = H.Col("#6a6a62"); c.FillCircle(swing.ax, swing.ay, 1.4f);
        }
        else if (gaffAnim > 0)
        {
            float a2 = aimA + (gaffAnim / 0.18f - 0.5f) * 1.6f * f;
            float ex = hx + MathF.Cos(a2) * 14, ey = hy + MathF.Sin(a2) * 14;
            c.Stroke = H.Col("#6a5034"); c.LineWidth = 1.4f; c.Line(hx, hy, ex, ey);
            c.Stroke = H.Col("#b0b0a8"); c.LineWidth = 1.2f;
            c.BeginPath(); c.Arc(ex, ey, 2.6f, a2, a2 + H.PI * 1.2f * f, f < 0); c.StrokePath();
            c.Stroke = H.Rgba(220, 230, 240, 0.35f); c.LineWidth = 3;
            c.BeginPath(); c.Arc(hx, hy, 13, aimA - 0.8f * f, aimA + 0.8f * f, f < 0); c.StrokePath();
        }
        else if (state != "ladder") DrawWeapon(c, hx, hy, aimA);

        c.Restore();
        // what she is busy with, as a thin bar over the helmet
        float prog = reloadT > 0 ? 1 - reloadT / Data.Weapons.P(cur).F("reload") : healT > 0 ? healT / (healKind == "stitch" ? 4 : 3) : craftT > 0 ? 1 - craftT / craftRecipe.F("time") : -1;
        if (prog >= 0)
        {
            var col = reloadT > 0 ? H.Col("#e0c070") : healT > 0 ? H.Col("#8fd08f") : H.Col("#d0d0d0");
            float by = headY - hr - 6;
            c.Fill = H.Rgba(0, 0, 0, 0.6f); c.FillRect(x0 - 7, by - 0.5f, 14, 2.4f);
            c.Fill = col; c.FillRect(x0 - 6.5f, by, 13 * Math.Clamp(prog, 0, 1), 1.4f);
        }
        // a net over her shoulders, knotted cord in a coarse mesh
        if (netT > 0)
        {
            c.Stroke = H.Rgba(150, 132, 96, 0.85f); c.LineWidth = 0.5f;
            float nx0 = x0 - 8, ny0 = headY - 6, nw = 16, nh = gy - ny0 + 1;
            for (int i = 0; i <= 5; i++) { float u = i / 5f; c.Line(nx0 + nw * u, ny0, nx0 + nw * (u * 0.8f + 0.1f), ny0 + nh); }
            for (int i = 0; i <= 6; i++) { float u = i / 6f; c.Line(nx0 + u * 1.5f, ny0 + nh * u, nx0 + nw - u * 1.5f, ny0 + nh * u + MathF.Sin(G.Now * 3 + i) * 0.6f); }
        }
        // graft knots grown into her, faintly alight
        if (grafts.Count > 0)
        {
            var gc = H.Rgba(200, 245, 255, 0.9f);
            if (grafts.Contains("ear")) { c.Fill = gc; c.FillCircle(x0 - f * 3.4f, headY + 1.5f, 0.9f); c.Glow(x0 - f * 3.4f, headY + 1.5f, 3, H.Rgba(160, 230, 255, 0.4f)); }
            if (grafts.Contains("arm")) { c.Fill = gc; c.FillCircle(hx, hy, 0.9f); c.Glow(hx, hy, 3, H.Rgba(160, 230, 255, 0.4f)); }
            if (grafts.Contains("lung")) { c.Fill = gc; c.FillCircle(x0, shY + 4, 0.9f); c.Glow(x0, shY + 4, 3, H.Rgba(160, 230, 255, 0.4f)); }
        }
        c.GlobalAlpha = 1;
    }

    // The weapon in her hand, drawn along the aim and kept upright whichever way she faces.
    void DrawWeapon(Canvas c, float hx, float hy, float a)
    {
        if (Hollow.held)
        {
            // the Hollow held out at arm's length, lid open toward what she sings to
            float g = Hollow.resonance >= Hollow.Cur.cost ? 0.7f + 0.3f * MathF.Sin(G.Now * 4) : 0.15f;
            Hollow.DrawIcon(c, hx + MathF.Cos(a) * 2.5f, hy + MathF.Sin(a) * 2.5f - 0.5f, 6, g + (Hollow.singCool > 0.3f ? 1 : 0));
            return;
        }
        c.Save();
        c.Translate(hx, hy); c.Rotate(a);
        if (MathF.Cos(a) < 0) c.Scale(1, -1);
        switch (cur)
        {
            case "driver":
                c.Fill = H.Col("#5a5a54"); c.FillRect(-1.5f, -1.8f, 8.5f, 3.4f);
                c.Fill = H.Col("#8a8a80"); c.FillRect(-1.5f, -1.8f, 8.5f, 0.8f);
                c.Fill = H.Col("#b08840"); c.FillRect(1.5f, -1.9f, 1.2f, 3.6f); c.FillRect(5, -1.9f, 0.8f, 3.6f);
                c.Fill = H.Col("#3a3a36"); c.FillRect(7, -1.1f, 2.6f, 2.2f);
                c.Fill = H.Col("#4a3626"); c.FillRect(-0.8f, 1.4f, 1.8f, 2.6f);
                c.Fill = H.Col("#c0c0b8"); c.FillRect(9.4f, -0.4f, 1, 0.8f);
                break;
            case "harpoon":
                c.Fill = H.Col("#4a3626"); c.FillRect(-3, -1, 6, 2.2f);
                c.Fill = H.Col("#6a7a80"); c.FillRect(2, -1.3f, 9, 2.4f);
                c.Fill = H.Col("#9aaab0"); c.FillRect(2, -1.3f, 9, 0.6f);
                c.Stroke = H.Col("#c8d0d4"); c.LineWidth = 0.8f; c.Line(10, 0, 15, 0);
                c.Fill = H.Col("#c8d0d4"); c.Poly(new[] { new Vector2(15, -1.4f), new Vector2(17.5f, 0), new Vector2(15, 1.4f) });
                c.Fill = H.Col("#4a3626"); c.FillRect(0, 1, 1.6f, 2.4f);
                break;
            case "tine":
            {
                // the harpoon gun with the bone grown through it: ribbed and pale, the prod become a long curled tine
                float g = Hollow.resonance >= Legend.TINE_COST ? 0.6f + 0.3f * MathF.Sin(G.Now * 3) : 0.15f;
                c.Fill = H.Col("#4a3626"); c.FillRect(-3, -1, 5, 2.2f);
                c.Fill = H.Col("#d8ccae"); c.FillRect(1, -1.6f, 9, 3);
                c.Fill = H.Col("#8a7c5c"); for (int i = 0; i < 4; i++) c.FillRect(2 + i * 2.2f, -1.6f, 0.6f, 3);
                c.Fill = H.Col("#efe6cf"); c.Poly(new[] { new Vector2(10, -1.4f), new Vector2(18, -0.6f), new Vector2(19.5f, 0.6f), new Vector2(10, 1.2f) });
                c.Stroke = H.Col("#9a8a64"); c.LineWidth = 0.4f; c.BeginPath(); c.Arc(4.5f, 0, 1.2f, 0.3f, H.PI * 1.7f); c.StrokePath();
                c.Glow(15, 0, 6, new Color(0.4f, 0.95f, 0.85f, 0.3f * g));
                break;
            }
            case "glass":
            {
                // the lantern's brass hood with the lens screwed in, glowing as it heats
                float hot = Legend.coolT > 0 ? 1 : Legend.heat / Legend.GLASS_HEAT;
                c.Fill = H.Col("#6a5226"); c.FillRect(-1.5f, -2.2f, 6, 4.4f);
                c.Fill = H.Col("#c09a50"); c.FillRect(-1.5f, -2.2f, 6, 0.8f);
                c.Fill = H.Col("#8a6a2a"); c.Poly(new[] { new Vector2(4.5f, -2.2f), new Vector2(8, -3.2f), new Vector2(8, 3.2f), new Vector2(4.5f, 2.2f) });
                c.Fill = new Color(1, 0.95f - hot * 0.5f, 0.8f - hot * 0.6f, 0.95f); c.FillRect(7.6f, -2.6f, 1.2f, 5.2f);
                if (hot > 0.05f) c.Glow(8, 0, 4 + hot * 3, new Color(1.2f, 0.5f, 0.2f, 0.3f * hot));
                break;
            }
            case "sling":
                c.Stroke = H.Col("#7a5a3a"); c.LineWidth = 1.3f;
                c.Line(0, 0, 4, 0); c.Line(4, 0, 7, -2.6f); c.Line(4, 0, 7, 2.6f);
                c.Stroke = H.Col("#c0a070"); c.LineWidth = 0.5f; c.BeginPath(); c.MoveTo(7, -2.6f); c.QuadraticCurveTo(3, 0, 7, 2.6f); c.StrokePath();
                break;
            default:
                c.Fill = H.Col("#5a5a54"); c.FillCircle(3, 0, 2.6f);
                c.Fill = H.Col("#9a9a90"); c.FillCircle(3, 0, 1.2f);
                c.Stroke = H.Col("#c0c0c0"); c.LineWidth = 0.5f; c.Line(5.5f, 0, 10, 0);
                break;
        }
        c.Restore();
    }

    public JsonObject ToJson()
    {
        JsonArray Arr(IEnumerable<string> s) => new(s.Select(v => (JsonNode)v).ToArray());
        return new JsonObject
        {
            ["maxHp"] = maxHp, ["hp"] = hp, ["oil"] = oil, ["abilities"] = Arr(abilities),
            ["weapons"] = new JsonObject(weapons.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, new JsonObject { ["tier"] = kv.Value.tier, ["mag"] = kv.Value.mag }))),
            ["weaponOrder"] = Arr(weaponOrder), ["cur"] = cur, ["bag"] = bag.ToJson(),
            ["fittingsOwned"] = Arr(fittingsOwned), ["fittings"] = Arr(fittings), ["pressure"] = pressure,
            ["grafts"] = Arr(grafts), ["shut"] = shut,
        };
    }
    public void Load(Props o)
    {
        maxHp = o.I("maxHp", 5); hp = o.I("hp", 5); oil = o.F("oil", 90); cur = o.S("cur") ?? "driver"; pressure = o.I("pressure", 4); shut = o.B("shut");
        weapons = new();
        if (o.P("weapons") is Props wp) foreach (var k in wp.Keys) weapons[k] = new WeaponState { tier = wp.P(k).I("tier"), mag = wp.P(k).I("mag") };
        weaponOrder = o.Strings("weaponOrder");
        abilities = new HashSet<string>(o.Strings("abilities")); bag = Bag.From(o.P("bag"));
        fittingsOwned = new HashSet<string>(o.Strings("fittingsOwned")); fittings = new HashSet<string>(o.Strings("fittings")); grafts = new HashSet<string>(o.Strings("grafts"));
        breath = BreathMax(); if (!float.IsFinite(breath)) breath = 30;
    }
}
