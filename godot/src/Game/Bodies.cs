// The other Brine bodies: Gullet, Bellmouth, Driftling, Saltwright, the Grafted choir, and corpses.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public class Shot { public float x, y, vx, vy, life, dmg, grav; public object src; public string kind; }

// ---------------------------------------------------------------- Gullet
public class Gullet : Body
{
    public float ax, ay, len;
    bool stunOnLand;
    // The Gullet Pair hangs from two tendrils, a little apart. Cut one and it swings from the other; only cutting
    // both brings it down.
    public bool pair;
    public override string Kind => pair ? "gulletpair" : "gullet";
    float swingT, swingCool;
    const float SPREAD = 9;
    public Gullet(Props spec, RoomDef room) : base(spec, room)
    {
        type = "gullet";
        ax = spec.F("x"); ay = spec.F("anchorY"); len = spec.F("len", 4) * H.TILE;
        pair = spec.B("pair");
        AddKnot("tendril", "tendril", "tendril", 2, 4);
        if (pair) AddKnot("tendril2", "tendril", "tendril2", 2, 4);
        AddKnot("spine1", "spine", "s1", 3);
        AddKnot("spine2", "spine", "s2", 3);
        AddKnot("spine3", "spine", "s3", 3);
        mode = "hang"; awakeMode = "ground";
        w = 22; h = 9;
        x = ax - w / 2; y = ay + len - h;
    }
    static Vector2 V(float x, float y) => new(x, y);
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = new Dictionary<string, Vector2>();
        float sway = mode == "hang" ? MathF.Sin(anim * 1.3f) * 2 : 0;
        if (Swinging) sway = MathF.Sin(swingT * 2.4f) * 22;
        if (mode == "hang")
        {
            float hx = HangX();
            j["tendril"] = V((pair ? ax - SPREAD : ax) + sway * 0.4f, ay + len * 0.45f);
            if (pair) j["tendril2"] = V(ax + SPREAD + sway * 0.4f, ay + len * 0.45f);
            float bx = hx + sway, by = ay + len;
            j["s1"] = V(bx, by - 10); j["s2"] = V(bx + 1, by - 5); j["s3"] = V(bx, by);
            j["head"] = V(bx, by + 3);
        }
        else
        {
            float f = facing, x = Cx(), y = Feet() - 4;
            float wv = MathF.Sin(anim * 10) * 1.5f;
            j["s1"] = V(x - f * 8, y + wv); j["s2"] = V(x, y - wv); j["s3"] = V(x + f * 7, y + wv);
            j["head"] = V(x + f * 12, y - 1);
            j["tendril"] = V(x - f * 14, y + 2);
            if (pair) j["tendril2"] = V(x - f * 12, y + 3);
        }
        return j;
    }
    public override (float x, float y, float w, float h) HitBox()
    {
        if (mode == "hang") return (Cx() - 5, ay + len - 14, 10, 18);
        return (x, y, w, h);
    }
    public override float Cx() => mode == "hang" ? HangX() + (Swinging ? MathF.Sin(swingT * 2.4f) * 22 : 0) : x + w / 2;
    // swinging from one tendril of a pair: it hangs under the one still holding
    bool Swinging => pair && mode == "hang" && (K("tendril").broken != K("tendril2").broken);
    float HangX() => !Swinging ? ax : K("tendril").broken ? ax + SPREAD : ax - SPREAD;
    public override float Cy() => mode == "hang" ? ay + len - 5 : y + h / 2;
    public override void Hear(float hx, float hy, float d, float r, string src)
    {
        if (mode == "hang")
        {
            // Drops on noise below it.
            if (MathF.Abs(hx - ax) < 3.5f * H.TILE && hy > ay + len - 8 && (src == "scream" || G.CanWake())) Drop();
            return;
        }
        base.Hear(hx, hy, d, r, src);
    }
    public void Drop(bool stunned = false)
    {
        if (mode != "hang") return;
        mode = "fall";
        x = ax - w / 2; y = ay + len - h;
        vy = 50; stunOnLand = stunned;
        x = Cx() - w / 2;
        K("tendril").broken = true;
        if (pair) K("tendril2").broken = true;
        Voices.Alert(this);
    }
    public override void OnKnotBroken(Knot k, string src)
    {
        if (k.type == "tendril" && mode == "hang")
        {
            // one tendril of a pair cut: it swings from the other, and swings hard
            if (pair && knots.Count(q => q.type == "tendril" && !q.broken) == 1) { swingT = 0; Snd.Thud(Pan()); Voices.Alert(this); return; }
            k.broken = false; Drop(true); return;
        }
        if (k.type == "spine" && knots.Count(q => q.type == "spine" && q.broken) >= 3) Down(src);
    }
    bool tendrilHit;
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        if (k.type == "spine" && mode == "hang" && src != "fire") dmg *= 0.5f;
        // a shot to a tendril does not shake it loose; cutting it does (OnKnotBroken), and it lands stunned
        tendrilHit = k.type == "tendril";
        try { return base.DamageKnot(k, dmg, src); } finally { tendrilHit = false; }
    }
    public override void Wake(bool force = false)
    {
        if (mode != "hang" || tendrilHit) return;
        // a pair swinging from one tendril holds on; only cutting that one brings it down
        if (pair && knots.Count(q => q.type == "tendril" && !q.broken) == 1) return;
        Drop();
    }
    public override void Think(float dt)
    {
        var p = G.player;
        if (mode == "hang")
        {
            vx = 0; vy = 0;
            if (Swinging)
            {
                // it swings through anyone under it
                swingT += dt; swingCool -= dt;
                var hb = HitBox();
                if (p != null && swingCool <= 0 && H.RectsOverlap(hb.x - 3, hb.y, hb.w + 6, hb.h + 4, p.x, p.y, p.w, p.h)) { p.Hurt(1, this); swingCool = 1.2f; }
            }
            return;
        }
        if (mode == "fall")
        {
            if (onGround)
            {
                // Crushes whatever it lands on.
                var hb = HitBox();
                foreach (var e0 in G.ents.ToArray())
                {
                    if (e0 is Body e && e != this && e.Alive() && e.K("kneeL") != null)
                    {
                        var eb = e.HitBox();
                        if (!H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, eb.x, eb.y, eb.w, eb.h)) continue;
                        e.DamageKnot(e.K("kneeL"), 9, "gullet"); e.DamageKnot(e.K("kneeR"), 9, "gullet"); e.stun = 2;
                    }
                }
                if (p != null && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h)) p.Hurt(2, this);
                Snd.Thud(Pan()); FX.Salt(Cx(), Feet(), 10);
                G.Shake(3);
                mode = "ground"; stun = stunOnLand ? 3 : 0.8f;
            }
            return;
        }
        if (mode == "windup")
        {
            vx *= 0.8f; atkT -= dt;
            if (atkT <= 0) { mode = "attack"; atkT = 0.25f; vx = facing * 220; }
            return;
        }
        if (mode == "attack")
        {
            atkT -= dt;
            var hb = HitBox();
            if (p != null && H.RectsOverlap(hb.x, hb.y, hb.w, hb.h, p.x, p.y, p.w, p.h)) { p.Hurt(2, this); mode = "recover"; atkT = 0.8f; }
            if (atkT <= 0) { mode = "recover"; atkT = 0.8f; }
            return;
        }
        if (mode == "recover") { vx *= 0.85f; atkT -= dt; if (atkT <= 0) mode = "ground"; return; }
        // ground crawl
        cool -= dt;
        bool sees = Perceives();
        if (sees || (lastHeard != null && alert > 0))
        {
            float tx = sees ? p.Cx() : lastHeard.Value.X;
            alert -= dt;
            float dx = tx - Cx();
            int s = (int)H.Sign(dx); if (s != 0) facing = s;
            vx = H.Approach(vx, facing * 70, 500 * dt);
            if (p != null && H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 40 && cool <= 0)
            {
                mode = "windup"; atkT = Wind(0.5f); cool = Cool(1.8f);
                foreach (var k in knots) if (!k.broken) k.flare = 0.5f;
                Voices.Windup(this);
            }
        }
        else vx *= 0.9f;
    }
    public override void Physics(float dt, bool pinned = false)
    {
        if (mode == "hang") return;
        base.Physics(dt, pinned);
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        c.StrokeStyle = "#9fb7c0"; c.LineWidth = 1.5f;
        if (mode == "hang")
        {
            foreach (var (kid, tx) in pair ? new[] { ("tendril", ax - SPREAD), ("tendril2", ax + SPREAD) } : new[] { ("tendril", ax) })
            {
                if (K(kid).broken) { c.BeginPath(); c.MoveTo(tx, ay); c.LineTo(tx + MathF.Sin(anim * 2) * 2, ay + 10); c.StrokePath(); continue; }
                c.BeginPath(); c.MoveTo(tx, ay); c.QuadraticCurveTo(tx + MathF.Sin(anim) * 3, ay + len / 2, j["s1"].X, j["s1"].Y); c.StrokePath();
                c.Fill = H.Rgba(220, 240, 250, 0.7f); c.FillRect(tx - 3, ay, 6, 2);
            }
        }
        // a smooth spine through the segments, a body tapering from tail to a heavy head
        var sp = new[] { j["s1"], j["s2"], j["s3"], j["head"] };
        var pts = new List<Vector2>();
        for (int seg = 0; seg < 3; seg++)
        {
            Vector2 p0 = sp[Math.Max(0, seg - 1)], p1 = sp[seg], p2 = sp[seg + 1], p3 = sp[Math.Min(3, seg + 2)];
            for (int q = 0; q < 6; q++)
            {
                float u = q / 6f, u2 = u * u, u3 = u2 * u;
                pts.Add(0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3));
            }
        }
        pts.Add(sp[3]);
        int n = pts.Count;
        var left = new Vector2[n]; var right = new Vector2[n];
        for (int q = 0; q < n; q++)
        {
            var d = (pts[Math.Min(n - 1, q + 1)] - pts[Math.Max(0, q - 1)]).Normalized();
            var nn = new Vector2(-d.Y, d.X);
            float k2 = q / (float)(n - 1);
            float wdt = 1.2f + 3.4f * MathF.Sin(MathF.Min(1, k2 * 1.15f) * H.PI * 0.55f) + (k2 > 0.85f ? (k2 - 0.85f) * 6 : 0);
            wdt *= 1 + 0.08f * MathF.Sin(anim * 6 - q * 0.8f);
            left[q] = pts[q] + nn * wdt; right[q] = pts[q] - nn * wdt * 0.9f;
        }
        bool burnt = burnT > 0;
        c.Fill = burnt ? H.Col("#2a1c10") : H.Col("#3e4a46");
        c.Poly(left.Concat(right.Reverse()).ToArray());
        // pale belly, darker mottled back, a ragged fin along the spine
        c.Fill = burnt ? H.Rgba(60, 40, 20, 0.5f) : H.Rgba(150, 172, 160, 0.45f);
        c.Poly(right.Concat(pts.AsEnumerable().Reverse().Select((v, q) => v.Lerp(right[n - 1 - q], 0.45f))).ToArray());
        c.Fill = H.Rgba(0, 0, 0, 0.25f);
        for (int q = 2; q < n - 3; q += 3) c.FillCircle(H.Lerp(left[q].X, pts[q].X, 0.4f), H.Lerp(left[q].Y, pts[q].Y, 0.4f), 1.1f);
        c.Fill = burnt ? H.Col("#1a120a") : H.Col("#2e3836");
        for (int q = 1; q < n - 3; q += 2)
        {
            var d = (pts[q + 1] - pts[q - 1]).Normalized(); var nn = new Vector2(-d.Y, d.X);
            float fl = 2 + MathF.Sin(anim * 8 + q) * 0.8f;
            c.Poly(new[] { left[q], left[q] + nn * fl + d * 1.2f, left[q + 1] });
        }
        // the head: a wide jaw, needle teeth, one pale eye
        var head = sp[3]; var hd = (sp[3] - sp[2]).Normalized(); var hn = new Vector2(-hd.Y, hd.X);
        bool open = mode == "windup" || mode == "attack";
        float gape = open ? 0.5f : 0.12f;
        var jawTip = head + hd * 5.5f;
        c.Fill = burnt ? H.Col("#2a1c10") : H.Col("#46524e");
        c.Poly(new[] { head + hn * 3.4f, jawTip + hn * 0.6f, head + hd * 2 });
        c.Poly(new[] { head - hn * 3, jawTip.Rotated(0) - hn * (0.6f + gape * 6) - hd * gape * 2, head + hd * 2 });
        c.Fill = H.Rgba(10, 6, 8, 0.9f); c.Poly(new[] { head + hd * 1.5f, jawTip + hn * 0.2f, jawTip - hn * (0.4f + gape * 6) - hd * gape * 2 });
        c.Stroke = H.Col("#d8d4c4"); c.LineWidth = 0.4f;
        for (int q = 1; q <= 3; q++) { var tp = head.Lerp(jawTip, q / 4f); c.Line(tp.X + hn.X * 0.4f, tp.Y + hn.Y * 0.4f, tp.X - hn.X * (0.4f + gape * 2), tp.Y - hn.Y * (0.4f + gape * 2)); }
        var eye = head + hn * 1.8f - hd * 0.4f;
        c.Fill = H.Col("#c0d0c8"); c.FillCircle(eye.X, eye.Y, 1.1f);
        c.Fill = H.Col("#101418"); c.FillCircle(eye.X + hd.X * 0.3f, eye.Y + hd.Y * 0.3f, 0.5f);
        if (mode == "windup") c.Glow(eye.X, eye.Y, 4, H.Rgba(220, 240, 255, 0.7f));
    }
    public override Props Serialize() { var s = base.Serialize(); s.Set("mode", mode == "fall" ? "ground" : mode); return s; }
    public override void Restore(Props s)
    {
        base.Restore(s);
        string sm = s.S("mode");
        if (sm == "hang") { mode = "hang"; x = ax - w / 2; y = ay + len - h; }
        else if (sm != "downed" && sm != "gone") { mode = "ground"; K("tendril").broken = true; if (pair) K("tendril2").broken = true; }
        w = 22; h = 9;
    }
    public override void SetPose(string p) { if (p == "lying") pose = "lying"; }
    public override void StandUp()
    {
        base.StandUp();
        w = 22; h = 9; mode = "ground"; K("tendril").broken = true;
        if (pair) K("tendril2").broken = true;
    }
}

// ---------------------------------------------------------------- Bellmouth
public class Bellmouth : Body
{
    float screamCool, home, belly;
    // The Hooded Bellmouth has a hymnal page tied over its eyes. The lantern means nothing to it; noise does, so
    // shuttering will not save her, and only quiet will.
    public bool hooded;
    public override string Kind => hooded ? "hooded" : "bellmouth";
    public Bellmouth(Props spec, RoomDef room) : base(spec, room)
    {
        type = "bellmouth";
        hooded = spec.B("hooded");
        AddKnot("jaw", "jaw", "jaw", 3);
        AddKnot("shoulderL", "shoulder", "shoulderL", 3);
        AddKnot("shoulderR", "shoulder", "shoulderR", 3);
        AddKnot("crown", "crown", "sternum", 4, 5, 2);
        w = 16; h = 30; x = spec.F("x") - 8; y = spec.F("y") - 30;
        mode = "idle"; awakeMode = "idle";
        home = spec.F("x");
    }
    public override void SetPose(string p) { if (p == "lying") base.SetPose("lying"); }
    public override bool Humanoid => true;
    public override Palette Pal() => new() { robe = "#d8d4c8", skin = "#8e9e98", cowl = null, dark = "#6a6a62" };
    bool SeesLight()
    {
        if (hooded) return false;
        var p = G.player;
        var lights = new List<(float x, float y, bool lantern)>();
        if (p != null && p.LanternLit() && !p.inBellRoom) lights.Add((p.Cx(), p.y + 8, true));
        foreach (var pr in G.projectiles) if (pr.kind == "flare") lights.Add((pr.x, pr.y, false));
        foreach (var l in lights)
        {
            float d = H.Dist(Cx(), y + 6, l.x, l.y);
            float range = l.lantern ? (p.focusing ? 13 : 8) * H.TILE * (p.HasFitting("tallowr") ? 1.3f : 1) : 7 * H.TILE;
            if (d > range) continue;
            bool facingOK = H.Sign(l.x - Cx()) == facing || MathF.Abs(l.x - Cx()) < 20 || (l.lantern && p.BeamHits(Cx(), Cy()));
            if (facingOK && Phys.Los(Cx(), y + 6, l.x, l.y)) return true;
        }
        return false;
    }
    public override void Hear(float hx, float hy, float d, float r, string src)
    {
        if (!hooded) { base.Hear(hx, hy, d, r, src); return; }
        if (src == "scream" || d > r || mode != "idle" || screamCool > 0 || K("jaw").broken) return;
        mode = "inhale"; atkT = 1.5f;
        int s = (int)H.Sign(hx - Cx()); if (s != 0) facing = s;
        Snd.Inhale(Pan());
    }
    public override void Think(float dt)
    {
        var p = G.player;
        screamCool -= dt; cool -= dt;
        if (mode == "inhale")
        {
            vx = 0; atkT -= dt;
            K("jaw").flare = 0.2f;
            if (K("jaw").broken) { mode = "idle"; screamCool = 4; H.ToastMsg("The Bellmouth chokes on its scream."); return; }
            if (atkT <= 0)
            {
                mode = "idle"; screamCool = 9;
                Snd.Scream(Pan());
                G.Shake(4);
                G.AddSwell(2);
                foreach (var e0 in G.ents.ToArray()) if (e0 is Body e && e != this && e.mode == "dormant" && !e.corpse) { e.lastHeard = new Vector2(Cx(), Cy()); e.Wake(true); }
                foreach (var e0 in G.ents.ToArray()) if (e0 is Gullet gl && gl.mode == "hang") gl.Drop();
                Noise.Emit(Cx(), Cy(), 20, "scream");
            }
            return;
        }
        if (mode == "windup")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0)
            {
                mode = "recover"; atkT = 0.6f;
                // the bell of its skull brought down on her: a heavy blow
                if (p != null && H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 34) p.Hurt(2, this);
                Snd.Thud(Pan());
            }
            return;
        }
        if (mode == "recover") { atkT -= dt; if (atkT <= 0) mode = "idle"; return; }
        // idle: sway, wander, look for light
        if (H.Rand() < dt * 0.3f) facing = -facing;
        float dx = home - Cx();
        vx = MathF.Abs(dx) > 24 ? H.Sign(dx) * 15 : vx * 0.9f;
        if (!K("jaw").broken && screamCool <= 0 && SeesLight())
        {
            mode = "inhale"; atkT = 1.5f;
            if (p != null) { int s = (int)H.Sign(p.Cx() - Cx()); if (s != 0) facing = s; }
            Snd.Inhale(Pan());
            return;
        }
        if (p != null && ArmsOK() && cool <= 0 && H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 30)
        {
            mode = "windup"; atkT = Wind(0.7f); cool = Cool(2);
            int s = (int)H.Sign(p.Cx() - Cx()); facing = s != 0 ? s : 1;
            foreach (var k in knots) if (!k.broken) k.flare = 0.7f;
            Voices.Windup(this);
        }
    }
    public override Dictionary<string, Vector2> Skeleton()
    {
        var j = base.Skeleton();
        belly = 0;
        if (pose == "stand")
        {
            float puff = mode == "inhale" ? (1.5f - atkT) * 3 : 0;
            j["jaw"] = new Vector2(j["jaw"].X, j["jaw"].Y + 1 + puff * 0.3f);
            belly = puff;
        }
        return j;
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        DrawHuman(c, Pal());
        if (pose == "stand")
        {
            float b = belly + 5;
            var st = j["sternum"];
            c.FillStyle = "#9aa8a2";
            c.BeginPath(); c.Ellipse(st.X + facing * 2, st.Y + 3, b, b * 1.1f, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = H.Rgba(255, 255, 255, 0.12f);
            c.BeginPath(); c.Ellipse(st.X + facing * 2 - 1.5f, st.Y + 1, b * 0.5f, b * 0.45f, 0, 0, H.PI * 2); c.FillPath();
            c.FillStyle = "#d8d4c8"; c.FillRect(st.X - 6, st.Y + 4, 12, 3);
            if (mode == "inhale") { c.FillStyle = "#101010"; c.BeginPath(); c.Arc(j["jaw"].X, j["jaw"].Y, 2, 0, H.PI * 2); c.FillPath(); }
        }
        if (hooded && j.TryGetValue("head", out var hd))
        {
            // a page of the hymnal, wet through, tied over its eyes with bell-rope
            c.Save(); c.Translate(hd.X, hd.Y - 1); c.Rotate(MathF.Sin(anim * 0.7f) * 0.08f);
            c.Fill = H.Col("#cfc6a8"); c.FillRect(-4.5f, -2.2f, 9, 4.4f);
            c.Fill = H.Rgba(60, 50, 40, 0.55f); for (int q = 0; q < 3; q++) c.FillRect(-3.5f, -1.4f + q * 1.3f, 7 - q, 0.4f);
            c.Fill = H.Col("#6a5236"); c.FillRect(-5, -0.4f, 10, 0.8f);
            c.Restore();
        }
    }
}

// ---------------------------------------------------------------- Driftling
public class Driftling : Body
{
    float nibble, flop;
    Projectile flareTarget;
    public string nest;
    public Driftling(Props spec, RoomDef room) : base(spec, room)
    {
        type = "driftling";
        w = 6; h = 4; x = spec.F("x"); y = spec.F("y");
        AddKnot("core", "core", "core", 1, 3);
        mode = "swim"; awakeMode = "swim";
        vx = H.Rand(-30, 30); vy = H.Rand(-20, 20);
        nest = spec.S("nest");
        key ??= "drift" + H.Uid();
    }
    public override Dictionary<string, Vector2> Skeleton() => new() { ["core"] = new Vector2(Cx(), Cy()) };
    public override void Hear(float hx, float hy, float d, float r, string src) { }
    public override void Wake(bool force = false) { }
    public override void OnKnotBroken(Knot k, string src) { gone = true; dead = true; FX.Salt(Cx(), Cy(), 4); Gore.Spray(Cx(), Cy(), -H.PI / 2, 1.6f, 6, 20, 90); }
    public override void DamageFlesh(float dmg, float hx, float hy, string src) => OnKnotBroken(knots[0], src);
    public override void Update(float dt)
    {
        _skel = null;
        anim += dt;
        if (gone) return;
        var p = G.player;
        bool inWater = World.IsWater(Cx(), Cy());
        if (!inWater)
        {
            vy += 600 * dt; flop += dt;
            if (flop > 3) { gone = true; dead = true; }
            Phys.Move(this, dt);
            return;
        }
        flop = 0;
        // Drawn to light: flares first, then an unshuttered lantern.
        float? tx = null, ty = null;
        float best = 1e9f;
        foreach (var pr in G.projectiles)
            if (pr.kind == "flare" && (World.IsWater(pr.x, pr.y + 2) || MathF.Abs(pr.y - World.SurfaceAt(pr.x, pr.y)) < 40))
            {
                float d = H.Dist(Cx(), Cy(), pr.x, pr.y);
                if (d < best && d < 260) { best = d; tx = pr.x; ty = pr.y; flareTarget = pr; }
            }
        if (tx == null && p != null && p.LanternLit() && !p.inBellRoom)
        {
            float d = H.Dist(Cx(), Cy(), p.Cx(), p.Cy());
            if (d < 200) { tx = p.Cx(); ty = p.Cy(); flareTarget = null; }
        }
        if (tx == null && p != null && H.Dist(Cx(), Cy(), p.Cx(), p.Cy()) < 28) { tx = p.Cx(); ty = p.Cy(); }
        if (tx != null)
        {
            float a = MathF.Atan2(ty.Value - Cy(), tx.Value - Cx()) + MathF.Sin(anim * 5 + key.Length) * 0.8f;
            vx = H.Approach(vx, MathF.Cos(a) * 110, 400 * dt);
            vy = H.Approach(vy, MathF.Sin(a) * 110, 400 * dt);
        }
        else
        {
            vx += H.Rand(-60, 60) * dt; vy += H.Rand(-60, 60) * dt;
            vx *= 0.98f; vy *= 0.98f;
        }
        // keep under the surface
        float surf = World.SurfaceAt(Cx(), Cy());
        if (Cy() < surf + 4) vy = MathF.Abs(vy) + 20;
        Phys.Move(this, dt);
        nibble -= dt;
        if (p != null && nibble <= 0 && H.RectsOverlap(x, y, w, h, p.x, p.y, p.w, p.h))
        {
            nibble = 1.0f;
            if (p.dashT > 0) { OnKnotBroken(knots[0], "dash"); return; }
            if (H.Rand() < 0.5f) p.Hurt(1, this);
        }
        // A flare stuck to a body: the swarm strips its knots for you.
        var sb = flareTarget?.StuckBody;
        if (sb != null && nibble <= 0)
        {
            if (H.Dist(Cx(), Cy(), sb.Cx(), sb.Cy()) < 20)
            {
                nibble = 0.8f;
                var ks = sb.knots.Where(k => !k.broken && !k.plated && !sb.IsSealed(k)).ToList();
                if (ks.Count > 0) sb.DamageKnot(H.Pick(ks), 1, "driftling");
            }
        }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        float a = MathF.Atan2(vy, vx);
        c.Save(); c.Translate(Cx(), Cy()); c.Rotate(a);
        if (MathF.Cos(a) < 0) c.Scale(1, -1);
        float wag = MathF.Sin(anim * 20) * 1.4f;
        // a lean grey fish with a forked tail, a ragged dorsal fin and one white eye
        c.FillStyle = "#4c5e66";
        c.Poly(new[] { new Vector2(-4, 0), new Vector2(-7.5f, -2.6f + wag), new Vector2(-6.4f, 0 + wag * 0.5f), new Vector2(-7.5f, 2.6f + wag) });
        c.Poly(new[] { new Vector2(-1.5f, -1.8f), new Vector2(0.5f, -3.6f), new Vector2(2, -1.8f) });
        c.FillStyle = "#5c7078"; c.BeginPath(); c.Ellipse(0, 0, 4.6f, 2, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(190, 210, 210, 0.45f); c.BeginPath(); c.Ellipse(0.4f, 0.8f, 3.6f, 0.9f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.FillRect(-2.5f, -1.4f, 4, 0.5f);
        c.Stroke = H.Rgba(0, 0, 0, 0.4f); c.LineWidth = 0.4f; c.Line(2.2f, -1.4f, 2.6f, 1.2f);
        c.Fill = H.Rgba(220, 240, 245, 0.95f); c.FillCircle(3, -0.4f, 0.65f);
        c.Fill = H.Col("#101418"); c.FillCircle(3.2f, -0.4f, 0.3f);
        c.Restore();
    }
    public override Props Serialize() => null;
}

// ---------------------------------------------------------------- Saltwright
public class Saltwright : Body
{
    float exposed;
    public Saltwright(Props spec, RoomDef room) : base(spec, room)
    {
        type = "saltwright";
        var a = AddKnot("armL", "arm", "elbowL", 4); a.plated = a.basePlated = true;
        var b = AddKnot("armR", "arm", "elbowR", 4); b.plated = b.basePlated = true;
        AddKnot("crown", "crown", "sternum", 5, 5, 2);
        w = 16; h = 30; x = spec.F("x") - 8; y = spec.F("y") - 30;
        mode = "idle"; awakeMode = "hunt";
    }
    public override void SetPose(string p) { if (p == "lying") base.SetPose("lying"); }
    public override bool Humanoid => true;
    public override Palette Pal() => new() { robe = "#5a4a3a", skin = "#8a8a7e", cowl = "#3a3028", dark = "#2a221a" };
    public override void Think(float dt)
    {
        var p = G.player;
        cool -= dt;
        if (mode == "windup")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0)
            {
                mode = "recover"; atkT = 1.5f; exposed = 1.5f;
                Snd.Thud(Pan()); G.Shake(2);
                float hx = Cx() + facing * 22, hy = y + h - 8;
                if (p != null && MathF.Abs(p.Cx() - hx) < 22 && MathF.Abs(p.Cy() - hy) < 24) p.Hurt(2, this);
                // The pick breaks salt walls and floors.
                for (int dy = -2; dy <= 1; dy++) for (int dx = 0; dx <= 1; dx++)
                    {
                        int tx = H.FloorI((hx + facing * dx * 16) / H.TILE), ty = H.FloorI(hy / H.TILE) + dy;
                        if (World.CharAt(tx, ty) == 'S') { World.SetChar(tx, ty, ' '); FX.Salt(tx * 16 + 8, ty * 16 + 8, 14); Snd.Crack(); }
                    }
                FX.Salt(hx, hy + 6, 8);
                Noise.Emit(hx, hy, 8, "pick");
            }
            return;
        }
        if (mode == "recover") { vx = 0; atkT -= dt; exposed = atkT; if (atkT <= 0) { mode = "hunt"; exposed = 0; } return; }
        if (mode == "idle")
        {
            if (Perceives() || alert > 0) mode = "hunt";
            vx *= 0.9f;
            return;
        }
        if (Perceives()) { target = new Vector2(p.Cx(), 0); alert = 4; }
        else if (lastHeard != null && alert > 0) { target = lastHeard; alert -= dt; }
        else target = null;
        if (target != null)
        {
            Pursue(dt, target.Value.X, 38);
            if (p != null && ArmsOK() && cool <= 0 && MathF.Abs(p.Cx() - Cx()) < 40 && MathF.Abs(p.Cy() - Cy()) < 30)
            {
                mode = "windup"; atkT = Wind(0.8f); cool = Cool(2.2f);
                int s = (int)H.Sign(p.Cx() - Cx()); facing = s != 0 ? s : 1;
                foreach (var k in knots) if (!k.broken) k.flare = 0.8f;
                Voices.Windup(this);
            }
        }
        else vx *= 0.9f;
    }
    // In the 1.5 s after its swing, a gaff strike pries an arm plate off.
    public override bool DamageKnot(Knot k, float dmg, string src)
    {
        if (src == "gaff" && k.plated && exposed > 0) src = "pry";
        return base.DamageKnot(k, dmg, src);
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        DrawHuman(c, Pal());
        if (pose == "stand")
        {
            // salt armour plates on shoulders and a pick (gone with the arm that held it)
            var sl = j["shoulderL"];
            c.FillStyle = "#cfc8b8"; c.FillRect(sl.X - 5, sl.Y - 3, 10, 4);
            c.Fill = H.Rgba(255, 255, 255, 0.25f); c.FillRect(sl.X - 5, sl.Y - 3, 10, 1);
            var hand = j["handL"];
            if (severed.Contains("forearmL") || severed.Contains("armL")) return;
            float ang = mode == "windup" ? -2.2f * facing : mode == "recover" ? 0.9f * facing : 0.3f * facing;
            c.Save(); c.Translate(hand.X, hand.Y); c.Rotate(ang);
            c.StrokeStyle = "#6a5030"; c.LineWidth = 2; c.BeginPath(); c.MoveTo(0, 0); c.LineTo(0, -16); c.StrokePath();
            c.StrokeStyle = "#9aa0a4"; c.LineWidth = 2.5f; c.BeginPath(); c.MoveTo(-6, -15); c.QuadraticCurveTo(0, -19, 7, -14); c.StrokePath();
            c.Restore();
        }
    }
}

// ---------------------------------------------------------------- Grafted (Act III choir)
public class Grafted : Body
{
    float healT;
    public Grafted(Props spec, RoomDef room) : base(spec, room)
    {
        type = "grafted";
        passive = spec.B("passive");
        boss = spec.B("boss");
        int more = boss ? 3 : 0;   // grafted Hesper is thicker; the crossing scales it after
        AddKnot("elbowL", "elbow", "elbowL", 3 + more);
        AddKnot("elbowR", "elbow", "elbowR", 3 + more);
        AddKnot("graft", "graft", "neck", 3 + more, 4);
        AddKnot("crown", "crown", "sternum", 4 + more, 5, 2);
        if (boss) AddKnot("graft2", "graft", "hip", 4);
        mode = passive ? "idle" : "hunt"; awakeMode = passive ? "idle" : "hunt";
    }
    public override bool Humanoid => true;
    public override Palette Pal() => new() { robe = boss ? "#4a2f5a" : "#3a3a5a", skin = "#9aa6b0", cowl = boss ? "#c0a060" : "#2a2a40", dark = "#1a1a2a" };
    public override bool Perceives() => !passive && base.Perceives();
    public override void Hear(float hx, float hy, float d, float r, string src) { if (!passive) base.Hear(hx, hy, d, r, src); }
    public override bool DamageKnot(Knot k, float dmg, string src) { if (passive) { passive = false; mode = "hunt"; G.OnChoirAttacked(); } return base.DamageKnot(k, dmg, src); }
    public override void DamageFlesh(float dmg, float hx, float hy, string src) { if (passive) { passive = false; mode = "hunt"; G.OnChoirAttacked(); } base.DamageFlesh(dmg, hx, hy, src); }
    public override LightSrc Light() => gone || mode == "downed" ? null : new LightSrc { x = Cx() + facing * 6, y = Cy(), r = 60, i = 0.6f, color = "255,200,120" };
    public override void Think(float dt)
    {
        var p = G.player;
        if (passive) { vx *= 0.8f; if (p != null) { int s = (int)H.Sign(p.Cx() - Cx()); facing = s != 0 ? s : 1; } return; }
        cool -= dt;
        // Retreat into water to heal.
        bool hurt = knots.Any(k => k.broken && k.type != "crown");
        if (hurt && World.IsWater(Cx(), Feet() - 2))
        {
            healT += dt;
            if (healT > 3) { healT = 0; var k = knots.Find(k => k.broken && k.type != "crown"); if (k != null) { k.broken = false; k.hp = k.max; FX.Salt(Cx(), Cy(), 8); } }
        }
        if (mode == "windup")
        {
            vx = 0; atkT -= dt;
            if (atkT <= 0 && p != null)
            {
                mode = "recover"; atkT = 1.0f;
                float a = MathF.Atan2(p.Cy() - (y + 10), p.Cx() - Cx());
                G.enemyShots.Add(new Shot { x = Cx(), y = y + 10, vx = MathF.Cos(a) * 340, vy = MathF.Sin(a) * 340, life = 2, dmg = 2, src = this });
                Snd.Harpoon(Pan());
            }
            return;
        }
        if (mode == "recover") { atkT -= dt; if (atkT <= 0) mode = "hunt"; return; }
        if (p == null) return;
        float d = H.Dist(Cx(), Cy(), p.Cx(), p.Cy());
        if (hurt && !World.IsWater(Cx(), Feet() - 2))
        {
            // look for water nearby
            foreach (var dir in new[] { -1, 1 })
                for (int i = 1; i < 12; i++)
                    if (World.IsWater(Cx() + dir * i * H.TILE, Feet() - 2)) { Pursue(dt, Cx() + dir * i * H.TILE, 50); return; }
        }
        if (Perceives() || d < 12 * H.TILE)
        {
            if (d > 7 * H.TILE) Pursue(dt, p.Cx(), 50);
            else if (d < 3 * H.TILE) Pursue(dt, Cx() - H.Sign(p.Cx() - Cx()) * 40, 50);
            else vx *= 0.8f;
            int s = (int)H.Sign(p.Cx() - Cx()); facing = s != 0 ? s : 1;
            if (cool <= 0 && ArmsOK() && Phys.Los(Cx(), y + 10, p.Cx(), p.Cy()))
            {
                mode = "windup"; atkT = Wind(0.9f); cool = Cool(boss ? 1.6f : 2.6f);
                foreach (var k in knots) if (!k.broken) k.flare = 0.9f;
            }
        }
        else vx *= 0.9f;
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        var j = Skel();
        DrawHuman(c, Pal());
        if (pose == "stand" && mode != "downed")
        {
            c.FillStyle = "#ffd080"; c.FillRect(j["handL"].X - 1.5f, j["handL"].Y - 1, 3, 3);
            c.Fill = H.Rgba(200, 240, 255, 0.6f); c.FillRect(j["neck"].X - 2, j["neck"].Y - 1, 4, 2);
        }
    }
}

// A true corpse: no knots, never stands. Still a body you can drag (Dead Weight).
public class Corpse : Body
{
    public float fadeKnots;
    public Corpse(Props spec, RoomDef room) : base(spec, room)
    {
        type = "corpse"; corpse = true;
        sailor = spec.B("sailor");
        string sp = spec.S("pose");
        SetPose(sp == "seated" || sp == "slumped" ? "seated" : "lying");
        mode = "dormant";
        if (sailor)
        {
            // The tutorial sailor shows knots that glint, then go dark.
            AddKnot("kneeL", "knee", "kneeL", 1); AddKnot("kneeR", "knee", "kneeR", 1);
            AddKnot("elbowL", "elbow", "elbowL", 1); AddKnot("elbowR", "elbow", "elbowR", 1);
            fadeKnots = 0;
        }
    }
    public override void Hear(float hx, float hy, float d, float r, string src) { }
    public override void Wake(bool force = false) { }
    public override Palette Pal() => sailor ? new Palette { robe = "#2a3a4a", skin = "#8c9890", dark = "#1a242c" } : new Palette { robe = "#3b3631", skin = "#7a837a", cowl = "#2c2824" };
    public override bool DamageKnot(Knot k, float dmg, string src) => false;
    public override void DamageFlesh(float d, float hx, float hy, string src) { Snd.Flesh(Pan()); Gore.Wound(hx, hy, 3); }
    public override void Update(float dt)
    {
        _skel = null;
        if (burnT > 0) { base.Update(dt); return; }
        if (sailor && fadeKnots > 0) { fadeKnots -= dt; if (fadeKnots <= 0) knots.Clear(); }
        Physics(dt);
    }
    public override void DrawKnots(Canvas c)
    {
        if (!sailor || knots.Count == 0) return;
        float f = fadeKnots > 0 ? fadeKnots / 3 : 1;
        foreach (var k in knots)
        {
            var p = KnotPos(k).Value;
            float v = Render.KnotVisibility(p.X, p.Y, this) * f;
            if (v < 0.05f) continue;
            c.Fill = H.Rgba(200, 245, 255, v); c.Save(); c.Translate(p.X, p.Y); c.Rotate(H.PI / 4); c.FillRect(-1.8f, -1.8f, 3.6f, 3.6f); c.Restore();
        }
    }
    public override void Draw(Canvas c)
    {
        if (gone) return;
        DrawHuman(c, Pal());
    }
    public override Props Serialize() => new(new JsonObject { ["x"] = x, ["y"] = y, ["gone"] = gone, ["pose"] = pose, ["facing"] = facing, ["knots"] = new JsonArray() });
    public override void Restore(Props s) { x = s.F("x"); y = s.F("y"); gone = s.B("gone"); facing = s.I("facing", facing); SetPose(s.S("pose") ?? pose); if (sailor) knots.Clear(); }
}

public static class BodyTypes
{
    public static Body Make(string t, Props spec, RoomDef room) => t switch
    {
        "shucker" => new Shucker(spec, room),
        "gullet" => new Gullet(spec, room),
        "bellmouth" => new Bellmouth(spec, room),
        "driftling" => new Driftling(spec, room),
        "saltwright" => new Saltwright(spec, room),
        "grafted" => new Grafted(spec, room),
        "corpse" => new Corpse(spec, room),
        "abbot" => new Abbot(spec, room),
        "undertow" => new Undertow(spec, room),
        "shoal" => new Shoal(spec, room),
        // the start
        "saltgull" => new Saltgull(spec, room),
        "netter" => new Netter(spec, room),
        "kilnman" => new Kilnman(spec, room),
        // the middle
        "bloater" => new Bloater(spec, room),
        "toller" => new Toller(spec, room),
        "cantor" => new Cantor(spec, room),
        "ossuary" => new Ossuary(spec, room),
        "kite" => new Kite(spec, room),
        // the end
        "diver" => new Diver(spec, room),
        "deepwalker" => new Deepwalker(spec, room),
        "wick" => new Wick(spec, room),
        _ => null,
    };
    public static readonly string[] START = { "shucker", "gullet", "bellmouth", "saltgull", "netter", "kilnman" };
    public static readonly string[] MID = { "saltwright", "driftling", "bloater", "toller", "cantor", "ossuary", "kite" };
    public static readonly string[] END = { "grafted", "diver", "deepwalker", "wick" };
    public static bool Has(string t) => t is "shucker" or "gullet" or "bellmouth" or "driftling" or "saltwright" or "grafted" or "corpse" or "abbot" or "undertow" or "shoal"
        || START.Contains(t) || MID.Contains(t) || END.Contains(t);
}
