// Room puzzles and set-pieces: Wrackline, Saltworks, Ribvault and Choir objects, collectibles, endings.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

// ---------------------------------------------------------------- Wrackline objects
public class Socket : Obj
{
    public Socket(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 14; }
    public override string Prompt() => World.flags.B("seawallOpen") ? null : G.player.bag.Count("crank") > 0 ? "E  Fit the Sluice Crank" : "E  Examine gate socket";
    public override void Interact()
    {
        if (World.flags.B("seawallOpen")) return;
        var p = G.player;
        if (p.bag.Count("crank") == 0) { UI.Say(null, "An empty socket for a sluice crank. Ansel said the crank is in the Tithe Hall, below the seawall."); return; }
        p.bag.Remove("crank", 1);
        World.flags.Set("seawallOpen", true);
        Snd.Rumble(); G.Shake(4);
        H.ToastMsg("The seawall gate grinds open. The way up to the Saltworks is clear.");
    }
    public override void Draw(Canvas c)
    {
        bool open = World.flags.B("seawallOpen");
        c.FillStyle = "#3a3a3a"; c.FillRect(x, y, w, h);
        c.Fill = H.Rgba(255, 255, 255, 0.08f); c.FillRect(x, y, w, 1);
        c.FillStyle = open ? "#c08040" : "#1a1a1a"; c.FillRect(Cx() - 2, y + 4, 4, 4);
        if (open) { c.FillStyle = "#c08040"; c.FillRect(Cx() - 1, y - 6, 2, 10); c.FillRect(Cx() - 5, y - 6, 10, 2); }
    }
}
public class Crank : Obj
{
    public Crank(Props spec, RoomDef room) : base(spec, room) { w = 14; h = 14; }
    public override string Prompt() => done ? null : "E  Lift the Sluice Crank from the altar";
    public override void Interact()
    {
        if (done) return;
        var p = G.player;
        if (p.bag.Room("crank") < 1) { H.ToastMsg("The crank takes 2 satchel slots. Make room first."); Snd.Deny(); return; }
        p.bag.Add("crank", 1);
        done = true;
        Snd.Pickup();
        G.OnCrankTaken();
    }
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#5a4a3a"; c.FillRect(x - 4, y + 6, w + 8, 8);
        c.FillStyle = "#d8d0c0"; c.FillRect(x - 5, y + 5, w + 10, 2);
        if (!done) { c.FillStyle = "#c08040"; c.FillRect(Cx() - 1, y - 4, 2, 10); c.FillRect(Cx() - 6, y - 4, 12, 2); }
    }
}
public class Barrel : Obj
{
    public Barrel(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 14; }
    public override string Prompt() => done ? null : "F  Kick over (or shoot) the oil barrel";
    public void OnShot() => Burst();
    public void OnGaff() => Burst();
    public void Burst()
    {
        if (done) return;
        done = true;
        FX.Spawn(Cx(), Cy(), 30, new FXOpt { color = "#ffb040", glow = true, smin = 40, smax = 200 });
        Fire.Spill(Cx(), y + h - 4, 4);
        G.Shake(4);
    }
    public override void Draw(Canvas c)
    {
        if (done) { c.FillStyle = "#2a2018"; c.FillRect(x, y + h - 4, w + 4, 4); return; }
        c.FillStyle = "#6a4a2a"; c.FillRect(x, y, w, h);
        c.Fill = H.Rgba(255, 220, 160, 0.12f); c.FillRect(x + 2, y, 2, h);
        c.Fill = H.Rgba(0, 0, 0, 0.25f); c.FillRect(x + w - 3, y, 3, h);
        c.FillStyle = "#3a3a3a"; c.FillRect(x, y + 3, w, 1.5f); c.FillRect(x, y + h - 4, w, 1.5f);
    }
}
public class Pews : Obj
{
    public Pews(Props spec, RoomDef room) : base(spec, room) { }
    public override void Update(float dt) { }
    public override string Prompt() => null;
    public override void Draw(Canvas c)
    {
        float gy = room.Py + 17 * H.TILE;
        if (spec.A("spans") is not JsonArray spans) return;
        foreach (var sp in spans)
        {
            var arr = sp.AsArray();
            float a = (float)arr[0].GetValue<double>(), b = (float)arr[1].GetValue<double>();
            float x0 = room.Px + a * H.TILE, x1 = room.Px + (b + 1) * H.TILE;
            c.FillStyle = "#3a2e22";
            c.FillRect(x0, gy - 8, x1 - x0, 3);
            c.FillRect(x0, gy - 14, 3, 14);
            c.FillRect(x1 - 4, gy - 8, 2, 8);
            c.Fill = H.Rgba(255, 220, 170, 0.1f); c.FillRect(x0, gy - 8, x1 - x0, 1);
        }
    }
}

// ---------------------------------------------------------------- Saltworks objects
public class Lever : Obj
{
    static readonly string[] SEQ = { "sea", "saint", "kiln" };
    public Lever(Props spec, RoomDef room) : base(spec, room) { w = 10; h = 16; }
    public override string Prompt()
    {
        var f = World.flags;
        if (f.B("basinCrossed")) return null;
        if (f.B("basinJam")) return "E  The gates are jammed until the Ebb";
        return "E  Throw the " + spec.S("icon") + " lever";
    }
    bool Pulled() => World.flags.Strings("basinSeq").Contains(spec.S("id"));
    public override void Interact()
    {
        var f = World.flags;
        if (f.B("basinCrossed") || f.B("basinJam") || Pulled()) { Snd.Deny(); return; }
        var seq = f.Strings("basinSeq"); seq.Add(spec.S("id"));
        f.Set("basinSeq", new JsonArray(seq.Select(s => (JsonNode)s).ToArray()));
        Snd.Thud(); Snd.Reload();
        int i = seq.Count - 1;
        if (seq[i] != SEQ[i])
        {
            f.Set("basinJam", true); f.Set("basinTarget", 19);
            Snd.Rumble();
            H.ToastMsg("Wrong order. The pan drains with a groan and the gates jam until the Ebb.");
            return;
        }
        f.Set("basinTarget", new[] { 15, 11, 7 }[i]);
        if (seq.Count == 3)
        {
            f.Set("basinFilling", true);
            H.ToastMsg("The sluices open in order. The pan floods and the barge lifts. The hatch is closing!");
            Snd.Rumble();
        }
        else H.ToastMsg("Water spills into the pan.");
    }
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#3a3a3a"; c.FillRect(Cx() - 4, y + h - 4, 8, 4);
        float a = Pulled() ? 0.6f : -0.6f;
        c.Save(); c.Translate(Cx(), y + h - 3); c.Rotate(a);
        c.FillStyle = "#7a6040"; c.FillRect(-1, -12, 2, 12);
        c.FillStyle = "#9a8060"; c.FillRect(-1.5f, -13, 3, 2);
        c.Restore();
        c.FillStyle = "#c0a060";
        float ix = Cx(), iy = y - 4;
        string icon = spec.S("icon");
        if (icon == "wave") { c.FillRect(ix - 4, iy, 3, 1); c.FillRect(ix - 1, iy - 1, 3, 1); c.FillRect(ix + 2, iy, 3, 1); }
        if (icon == "skull") { c.FillRect(ix - 2, iy - 2, 4, 3); c.FillStyle = "#000"; c.FillRect(ix - 1, iy - 1, 1, 1); c.FillRect(ix + 1, iy - 1, 1, 1); }
        if (icon == "flame") { c.FillStyle = "#ff9040"; c.FillRect(ix - 1, iy - 3, 2, 4); c.FillRect(ix - 2, iy - 1, 4, 2); }
    }
}
public class Barge : Obj
{
    float dy;
    public Barge(Props spec, RoomDef room) : base(spec, room) { w = spec.F("w", 4) * H.TILE; h = 6; x = spec.F("x") - 8; y = spec.F("y") - 6; }
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        float surf = World.SurfaceAt(Cx(), y);
        float floorY = room.Py + 19 * H.TILE - h;
        float target = MathF.Min(floorY, surf - h * 0.4f);
        dy = H.Approach(y, target, 40 * dt) - y;
        y += dy;
    }
    public override Platform PlatformRect() => new() { x = x, y = y, w = w, h = h, oneWay = true, dy = dy, owner = this };
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#5a4a38"; c.FillRect(x, y, w, h);
        c.Fill = H.Rgba(255, 220, 170, 0.15f); c.FillRect(x, y, w, 1);
        c.FillStyle = "#e8e8f0"; c.FillRect(x + 4, y - 4, w - 8, 4);
        c.FillStyle = "#3a2e22"; c.FillRect(x, y + h - 2, w, 2);
    }
    public override Props Serialize() => null;
}
public class Floater : Obj
{
    float restY, dy;
    public Floater(Props spec, RoomDef room) : base(spec, room) { w = spec.F("w", 3) * H.TILE; h = 5; x = spec.F("x") - 8; restY = spec.F("y") - 5; y = restY; }
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        float surf = World.SurfaceAt(Cx(), y);
        float target = MathF.Min(restY, surf - 3 + MathF.Sin(t * 1.3f + x) * 1);
        t += dt;
        dy = H.Approach(y, target, 60 * dt) - y;
        y += dy;
    }
    public override Platform PlatformRect() => new() { x = x, y = y, w = w, h = h, oneWay = true, dy = dy, owner = this };
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#3a2e22"; c.FillRect(x, y, w, 3);
        c.FillRect(x, y - 7, 3, 10); c.FillRect(x + w - 4, y, 2, 5);
        c.Fill = H.Rgba(255, 220, 170, 0.12f); c.FillRect(x, y, w, 1);
    }
    public override Props Serialize() => null;
}
public class Lift : Obj
{
    float drop;
    bool falling;
    int load;
    public Lift(Props spec, RoomDef room) : base(spec, room) { w = spec.F("w", 4) * H.TILE; h = 6; x = spec.F("x") - 8; y = spec.F("y") - H.TILE; }
    public override void Update(float dt)
    {
        if (done) return;
        if (falling)
        {
            drop += dt; y += 220 * dt;
            if (drop > 1.2f) { done = true; OpenShaft(); }
            return;
        }
        // Count knotless bodies resting on the pan.
        int n = 0;
        foreach (var e0 in G.ents)
        {
            if (e0 is not Body e || e.gone) continue;
            bool knotless = e.corpse || e.mode == "downed";
            if (knotless && !e.dragged && e.Cx() > x && e.Cx() < x + w && MathF.Abs(e.Feet() - y) < 6) n++;
        }
        load = n;
        if (n >= 2)
        {
            falling = true;
            Snd.Rumble(); G.Shake(5);
            H.ToastMsg("The pan takes the dead weight. The lift drops into the Gullet!");
            foreach (var e0 in G.ents) if (e0 is Body e && e.Cx() > x && e.Cx() < x + w && MathF.Abs(e.Feet() - y) < 8) e.gone = true;
        }
    }
    void OpenShaft()
    {
        int tx = H.FloorI((x + 24) / H.TILE);
        int ty0 = H.FloorI(room.Py / H.TILE) + 17;
        for (int ty = ty0; ty < ty0 + 6; ty++) World.SetChar(tx, ty, 'H');
        World.flags.Set("liftDropped", true);
    }
    public override Platform PlatformRect() => done ? null : new Platform { x = x, y = y, w = w, h = h, oneWay = false, dy = falling ? 220f / 60 : 0, owner = this };
    public override string Prompt() => done ? null : "Lift pan: " + load + " / 2 bodies. Drag knotless dead onto it (E near a body).";
    public override void Draw(Canvas c)
    {
        if (done) return;
        c.FillStyle = "#5a5a5a"; c.FillRect(x, y, w, h);
        c.Fill = H.Rgba(255, 255, 255, 0.12f); c.FillRect(x, y, w, 1);
        c.StrokeStyle = "#7a7a7a"; c.LineWidth = 1;
        c.BeginPath(); c.MoveTo(x + 2, y); c.LineTo(Cx(), y - 30 - drop * 40); c.LineTo(x + w - 2, y); c.StrokePath();
    }
    public override Props Serialize() { var o = Props.Empty(); o.Set("done", done); return o; }
}

// ---------------------------------------------------------------- Ribvault puzzle
public class Prism : Obj
{
    public static readonly (int, int)[] DIRS = { (1, 0), (0, 1), (-1, 0), (0, -1) };
    public int dir;
    public float lit;
    public Prism(Props spec, RoomDef room) : base(spec, room) { w = 10; h = 12; x = spec.F("x") - 5; y = spec.F("y") - 12 + 2; dir = spec.I("dir"); }
    public override string Prompt() => "E  Turn the prism";
    public override void Interact() { dir = (dir + 1) % 4; Snd.Ui(); Snd.Tink(); }
    public Vector2 Center() => new(room.Px + spec.I("tx") * H.TILE + 8, room.Py + spec.I("ty") * H.TILE + 8);
    public override void Draw(Canvas c)
    {
        var p = Center();
        c.FillStyle = "#4a4a4a"; c.FillRect(p.X - 1, p.Y + 4, 2, 4);
        c.Save(); c.Translate(p.X, p.Y); c.Rotate(H.PI / 4);
        c.Fill = lit > 0 ? H.Rgba(255, 250, 220, 0.95f) : H.Rgba(210, 240, 255, 0.7f); c.FillRect(-4, -4, 8, 8);
        c.Fill = H.Rgba(255, 255, 255, 0.5f); c.FillRect(-4, -4, 8, 1.5f);
        c.Restore();
        var (dx, dy) = DIRS[dir];
        c.FillStyle = "#ffd080"; c.FillRect(p.X + dx * 6 - 1, p.Y + dy * 6 - 1, 2, 2);
        lit = MathF.Max(0, lit - 0.05f);
    }
    public override LightSrc Light() => null;
    public override Props Serialize() { var o = Props.Empty(); o.Set("dir", dir); return o; }
    public override void Restore(Props s) { if (s != null && s.Has("dir")) dir = s.I("dir"); }
}
public class Crystal : Obj
{
    float charge;
    public Crystal(Props spec, RoomDef room) : base(spec, room) { w = 10; h = 14; }
    public override string Prompt() => null;
    public Vector2 Center() => new(room.Px + spec.I("tx") * H.TILE + 8, room.Py + spec.I("ty") * H.TILE + 8);
    public void HitByBeam(float dt)
    {
        if (done) return;
        charge += dt;
        if (charge > 2)
        {
            done = true;
            Snd.Bell(660, 0.4f); Snd.Crack(); G.Shake(3);
            // The crystal wakes and the salt seals around the vault crack open.
            for (int yy = 0; yy < room.Th; yy++) for (int xx = 0; xx < room.Tw; xx++)
                    if (World.CharAt(room.Ox + xx, room.Oy + yy) == 'S') { World.SetChar(room.Ox + xx, room.Oy + yy, ' '); FX.Salt(room.Px + xx * H.TILE + 8, room.Py + yy * H.TILE + 8, 6); }
            H.ToastMsg("The crystal wakes with a ringing note. Salt shears away somewhere above.");
        }
    }
    public override void Update(float dt) { base.Update(dt); if (!done) charge = MathF.Max(0, charge - dt * 0.3f); }
    public override LightSrc Light() => done ? new LightSrc { x = Center().X, y = Center().Y, r = 80, i = 0.7f, color = "200,240,255" } : null;
    public override void Draw(Canvas c)
    {
        var p = Center();
        c.Fill = done ? H.Col("#e0f8ff") : H.Rgba(120, 150, 170, 0.6f + charge * 0.2f);
        c.BeginPath(); c.MoveTo(p.X, p.Y - 8); c.LineTo(p.X + 5, p.Y + 2); c.LineTo(p.X, p.Y + 8); c.LineTo(p.X - 5, p.Y + 2); c.FillPath();
        c.Fill = H.Rgba(255, 255, 255, done ? 0.6f : 0.2f);
        c.BeginPath(); c.MoveTo(p.X, p.Y - 8); c.LineTo(p.X + 1.5f, p.Y + 1); c.LineTo(p.X - 5, p.Y + 2); c.FillPath();
    }
}

// ---------------------------------------------------------------- Sunken Choir objects
public class TunedBell : Obj
{
    public TunedBell(Props spec, RoomDef room) : base(spec, room) { w = 14; h = 18; }
    public override string Prompt() => "E / F  Strike the tuned bell";
    int BasinLevel() => World.flags.S("choirLevelName") == "raised" ? 3 : World.flags.I("basin");
    public override void Interact() => Ring();
    public void OnGaff() => Ring();
    void Ring()
    {
        int lvl = BasinLevel();
        Snd.Bell(new[] { 392, 330, 262, 196 }[lvl], 0.5f);
        // Crystal walls in this room shatter at their own pitch.
        var pitches = room.Raw.P("crystals");
        bool broke = false;
        if (pitches != null)
            foreach (var k in pitches.Keys)
            {
                if (pitches.I(k) != lvl) continue;
                int col = int.Parse(k.Split('@')[1]);
                for (int yy = 0; yy < room.Th; yy++)
                    if (World.CharAt(room.Ox + col, room.Oy + yy) == 'C') { World.SetChar(room.Ox + col, room.Oy + yy, ' '); FX.Salt(room.Px + col * H.TILE + 8, room.Py + yy * H.TILE + 8, 14); broke = true; }
            }
        if (broke) { Snd.Crack(); H.ToastMsg("A crystal wall sings and shatters."); }
        else H.ToastMsg(new[] { "The bell rings high and thin.", "The bell rings a little lower.", "The bell rings low.", "The bell booms, deep as the sea." }[lvl], 2.5f);
    }
    public override void Draw(Canvas c)
    {
        float x0 = Cx(), y0 = y;
        c.FillStyle = "#4a3a2a"; c.FillRect(x0 - 7, y0, 14, 2);
        c.FillStyle = "#9a8a5a"; c.BeginPath(); c.MoveTo(x0 - 2, y0 + 2); c.LineTo(x0 + 2, y0 + 2); c.LineTo(x0 + 5, y0 + 11); c.LineTo(x0 - 5, y0 + 11); c.FillPath();
        // basin below with its water level
        c.FillStyle = "#3a3a40"; c.FillRect(x0 - 7, y0 + 12, 14, 6);
        int lvl = BasinLevel();
        c.FillStyle = "#5a8aa0"; c.FillRect(x0 - 6, y0 + 17 - lvl * 1.6f, 12, lvl * 1.6f);
    }
}
public class Valve : Obj
{
    public Valve(Props spec, RoomDef room) : base(spec, room) { w = 10; h = 10; }
    public override string Prompt() => "E  Turn the basin valve (level " + World.flags.I("basin") + ")";
    public override void Interact() { World.flags.Set("basin", (World.flags.I("basin") + 1) % 3); Snd.Ui(); Snd.Splash(0.1f); }
    public override void Draw(Canvas c)
    {
        c.StrokeStyle = "#8a6a3a"; c.LineWidth = 1.5f; c.BeginPath(); c.Arc(Cx(), Cy(), 4, 0, H.PI * 2); c.StrokePath();
        c.BeginPath(); c.MoveTo(Cx() - 4, Cy()); c.LineTo(Cx() + 4, Cy()); c.MoveTo(Cx(), Cy() - 4); c.LineTo(Cx(), Cy() + 4); c.StrokePath();
    }
}
public class Sluice : Obj
{
    public Sluice(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 18; }
    public override string Prompt() => G.player.Has("tidekey") ? "E  Work the sluice with the Tide Key" : "E  Examine the sluice";
    public override void Interact()
    {
        if (!G.player.Has("tidekey")) { UI.Say(null, "A sluice wheel with a keyhole shaped like a wave. The Choir's water is held here."); return; }
        UI.SluiceMenu();
    }
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#3a3a3a"; c.FillRect(x, y + 4, w, h - 4);
        c.StrokeStyle = "#8a7a5a"; c.LineWidth = 1.5f; c.BeginPath(); c.Arc(Cx(), y + 4, 5, 0, H.PI * 2); c.StrokePath();
    }
}
public class FontObj : Obj
{
    public FontObj(Props spec, RoomDef room) : base(spec, room) { w = 16; h = 12; }
    public override string Prompt() => Story.ChoirFriendly() ? "E  The Brine font" : null;
    public override void Interact() => UI.FontMenu();
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#6a6a72"; c.FillRect(x + 3, y + 4, w - 6, h - 4);
        c.FillStyle = "#8a8a92"; c.FillRect(x, y, w, 5);
        c.Fill = H.Rgba(200, 245, 255, 0.8f); c.FillRect(x + 2, y + 1, w - 4, 2);
    }
    public override LightSrc Light() => new() { x = Cx(), y = y, r = 40, i = 0.4f, color = "180,230,255" };
}
public class Bowl : Obj
{
    public Bowl(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 10; }
    public override string Prompt() => Story.ChoirFriendly() && G.Quest("tables") != "given" && G.Quest("tables") != "sold" ? "E  The offering bowl" : null;
    public override void Interact() => Story.OfferTables();
    public override void Draw(Canvas c) { c.FillStyle = "#7a6a4a"; c.BeginPath(); c.Ellipse(Cx(), y + 4, 6, 3, 0, 0, H.PI); c.FillPath(); c.FillRect(Cx() - 1, y + 5, 2, 5); }
}

// ---------------------------------------------------------------- collectibles
public class Collectible : Obj
{
    public Collectible(Props spec, RoomDef room) : base(spec, room) { w = 10; h = 10; y -= 2; }
    public override bool IsCollectible => true;
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        base.Update(dt);
        var p = G.player;
        if (!done && p != null && H.RectsOverlap(p.x, p.y, p.w, p.h, x - 2, y - 2, w + 4, h + 4)) { done = true; G.Collect(spec); }
    }
    public override LightSrc Light() => done ? null : new LightSrc { x = Cx(), y = Cy(), r = 36, i = 0.5f, color = type == "page" || type == "table" ? "255,240,200" : "255,210,120" };
    public override void Draw(Canvas c)
    {
        if (done) return;
        float x0 = Cx(), y0 = Cy() + MathF.Sin(t * 2.5f) * 1.5f;
        string tt = type;
        if (tt == "stitch") { c.FillStyle = "#e06060"; c.BeginPath(); c.Arc(x0 - 2, y0 - 1, 2.5f, 0, H.PI * 2); c.FillPath(); c.BeginPath(); c.Arc(x0 + 2, y0 - 1, 2.5f, 0, H.PI * 2); c.FillPath(); c.BeginPath(); c.MoveTo(x0 - 4.5f, y0); c.LineTo(x0, y0 + 5); c.LineTo(x0 + 4.5f, y0); c.FillPath(); }
        else if (tt == "seal") { c.FillStyle = "#c8a050"; c.BeginPath(); c.Arc(x0, y0, 4, 0, H.PI * 2); c.FillPath(); c.FillStyle = "#8a6a30"; c.BeginPath(); c.Arc(x0, y0, 2, 0, H.PI * 2); c.FillPath(); }
        else if (tt == "page" || tt == "table") { c.FillStyle = tt == "page" ? "#f0e6cc" : "#d8e6f0"; c.FillRect(x0 - 3, y0 - 4, 6, 8); c.FillStyle = "#6a5a4a"; c.FillRect(x0 - 2, y0 - 2, 4, 0.8f); c.FillRect(x0 - 2, y0, 4, 0.8f); }
        else if (tt == "ability" || tt == "weapon")
        {
            c.Fill = H.Rgba(255, 220, 140, 0.25f); c.BeginPath(); c.Arc(x0, y0, 7, 0, H.PI * 2); c.FillPath();
            c.FillStyle = "#e0c070"; c.Save(); c.Translate(x0, y0); c.Rotate(t); c.FillRect(-3, -3, 6, 6); c.Restore();
        }
    }
}

public class WrackPool : Obj
{
    public WrackPool(Props spec, RoomDef room) : base(spec, room) { }
    public override string Prompt() => null;
    public override void Draw(Canvas c)
    {
        c.Stroke = H.Rgba(120, 180, 200, 0.35f); c.LineWidth = 1;
        float x0 = Cx(), y0 = y + h;
        for (int i = 0; i < 3; i++) { c.BeginPath(); c.Ellipse(x0, y0 - 1, 4 + i * 4 + MathF.Sin(t * 2 + i) * 1, 1.5f + i, 0, 0, H.PI * 2); c.StrokePath(); }
    }
}
public class StartObj : Obj { public StartObj(Props spec, RoomDef room) : base(spec, room) { } public override string Prompt() => null; }
public class DriftNest : Obj
{
    List<Driftling> spawned = new();
    bool used;
    public DriftNest(Props spec, RoomDef room) : base(spec, room) { }
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        bool high = spec.B("always") || Tide.PhaseId() == "high";
        spawned.RemoveAll(d => d.gone);
        if (high && spawned.Count == 0 && !used && World.IsWater(Cx(), Cy()))
        {
            used = true;
            int n = spec.I("n", 6);
            for (int i = 0; i < n; i++)
            {
                var s = Props.Empty();
                s.Set("x", Cx() + H.Rand(-20, 20)); s.Set("y", Cy() + H.Rand(-10, 10)); s.Set("key", "drift" + H.Uid());
                var d = new Driftling(s, room);
                spawned.Add(d); G.ents.Add(d);
            }
        }
        if (!high && !spec.B("always")) { foreach (var d in spawned) d.gone = true; spawned.Clear(); used = false; }
    }
    public override Props Serialize() => null;
}

// Dropped satchel after death.
public class SatchelDrop : Obj
{
    public Bag bag;
    public SatchelDrop(Props spec, RoomDef room, Bag bag) : base(spec, room) { w = 12; h = 9; x = spec.F("x") - 6; y = spec.F("y") - 9; this.bag = bag; type = "satchel"; }
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        base.Update(dt);
        var p = G.player;
        if (p != null && !p.dead && H.RectsOverlap(p.x, p.y, p.w, p.h, x, y, w, h)) G.RecoverSatchel(this);
    }
    public override LightSrc Light() => new() { x = Cx(), y = Cy(), r = 30, i = 0.5f, color = "255,220,150" };
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#6a4a2a"; c.FillRect(x, y + 2, w, h - 2);
        c.FillStyle = "#8a6a3a"; c.FillRect(x + 2, y, w - 4, 3);
        c.FillStyle = "#c0a060"; c.FillRect(Cx() - 1, y + 3, 2, 2);
    }
    public override Props Serialize() => null;
}

// ---------------------------------------------------------------- ending objects
public class Lampwork : Obj
{
    public Lampwork(Props spec, RoomDef room) : base(spec, room) { w = 30; h = 40; }
    public override string Prompt() => "E  The great lamp";
    public override void Interact() => Story.Ending("relight");
    public override LightSrc Light() => new() { x = Cx(), y = y + 8, r = 60, i = 0.4f, color = "255,230,180" };
    public override void Draw(Canvas c)
    {
        float x0 = Cx(), y0 = y;
        c.FillStyle = "#3a3a3a"; c.FillRect(x0 - 12, y0 + 24, 24, 16);
        c.FillStyle = "#6a6a5a"; c.FillRect(x0 - 10, y0 + 4, 20, 20);
        c.Fill = H.Rgba(255, 240, 200, 0.4f); c.FillRect(x0 - 8, y0 + 6, 16, 16);
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.FillRect(x0 - 1, y0 + 6, 2, 16); c.FillRect(x0 - 8, y0 + 13, 16, 2);
        c.FillStyle = "#c0a060"; c.FillRect(x0 - 12, y0, 24, 4);
    }
}
public class Beacon : Obj
{
    public Beacon(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 22; }
    public override string Prompt() => "E  The warning beacon";
    public override void Interact() => Story.Ending("dark");
    public override void Draw(Canvas c) { c.FillStyle = "#5a2a1a"; c.FillRect(x + 2, y + 6, 8, 16); c.FillStyle = "#c03020"; c.FillRect(x, y, 12, 6); }
}
public class HymnBell : Obj
{
    public HymnBell(Props spec, RoomDef room) : base(spec, room) { w = 14; h = 20; }
    public override string Prompt() => "E  The Saint's bell";
    public override void Interact() => Story.Ending("hymn");
    public override void Draw(Canvas c)
    {
        float x0 = Cx();
        c.FillStyle = "#4a4a5a"; c.FillRect(x0 - 7, y, 14, 2);
        c.FillStyle = "#7a8aa0"; c.BeginPath(); c.MoveTo(x0 - 2, y + 2); c.LineTo(x0 + 2, y + 2); c.LineTo(x0 + 6, y + 14); c.LineTo(x0 - 6, y + 14); c.FillPath();
    }
}

public static class ObjTypes
{
    public static Ent Make(string t, Props spec, RoomDef room) => t switch
    {
        "chest" => new Chest(spec, room), "pickup" => new Pickup(spec, room), "sign" => new Sign(spec, room),
        "carving" => new Carving(spec, room), "bell" => new Bell(spec, room), "reliquary" => new Reliquary(spec, room),
        "bench" => new Bench(spec, room), "npc" => new NPC(spec, room), "socket" => new Socket(spec, room),
        "crank" => new Crank(spec, room), "barrel" => new Barrel(spec, room), "pews" => new Pews(spec, room),
        "lever" => new Lever(spec, room), "barge" => new Barge(spec, room), "floater" => new Floater(spec, room),
        "lift" => new Lift(spec, room), "prism" => new Prism(spec, room), "crystal" => new Crystal(spec, room),
        "tunedbell" => new TunedBell(spec, room), "valve" => new Valve(spec, room), "sluice" => new Sluice(spec, room),
        "font" => new FontObj(spec, room), "bowl" => new Bowl(spec, room),
        "stitch" or "seal" or "page" or "table" or "ability" or "weapon" => new Collectible(spec, room),
        "wrackpool" => new WrackPool(spec, room), "start" => new StartObj(spec, room), "driftnest" => new DriftNest(spec, room),
        "lampwork" => new Lampwork(spec, room), "beacon" => new Beacon(spec, room), "hymnbell" => new HymnBell(spec, room),
        "kiln" => new Kiln(spec, room),
        _ => null,
    };
}
