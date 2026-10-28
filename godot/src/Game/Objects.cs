// Interactable objects. Each has x,y,w,h in world pixels, a prompt and an Interact().
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public class Obj : Ent
{
    public bool done;
    public float t;
    public Obj(Props spec, RoomDef room)
    {
        this.spec = spec; this.room = room; key = spec.S("key"); type = spec.S("type");
        w = spec.F("w", 12); h = spec.F("h", 14);
        x = spec.F("x") - w / 2; y = spec.F("y") - h;
    }
    public override bool HasPrompt => true;
    public bool NearR(Player p, float r) => MathF.Abs(p.Cx() - Cx()) < r + w / 2 && MathF.Abs(p.Cy() - Cy()) < 26 + h / 2;
    public override bool Near(Player p) => NearR(p, 22);
    public override void Update(float dt) { t += dt; }
    public override Props Serialize() { if (!done) return null; var o = Props.Empty(); o.Set("done", true); return o; }
    public override void Restore(Props s) { if (s != null && s.B("done")) done = true; }
    protected static JsonObject Dict(Dictionary<string, int> d) => new(d.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value)));
}

// ---------------------------------------------------------------- containers
public class Chest : Obj
{
    public Dictionary<string, int> contents;
    public string fitting;
    bool lifeline;
    public Chest(Props spec, RoomDef room) : base(spec, room)
    {
        w = 16; h = 11; x = spec.F("x") - 8; y = spec.F("y") - 11;
        contents = spec.Counts("loot"); fitting = spec.S("fitting");
    }
    bool Hidden() => spec.B("needsDrained") && !World.flags.B("choirDrained");
    public override string Prompt() => Hidden() ? null : done ? null : "{interact} Open " + (spec.S("label") ?? "container");
    public override void Interact()
    {
        if (Hidden() || done) return;
        var p = G.player;
        // Lifeline rule: scarcity bites without soft-locking anyone.
        int rivets = p.bag.Count("rivets") + (p.weapons.TryGetValue("driver", out var dr) ? dr.mag : 0);
        bool mats = Bag.MATERIALS.Any(m => p.bag.Count(m) > 0);
        if (rivets < 3 && !mats && !lifeline && H.Rand() < Diff.C.lifeline) { contents["scrap"] = contents.GetValueOrDefault("scrap") + 2; lifeline = true; }
        var got = new List<string>();
        foreach (var kv in contents.ToList())
        {
            string item = kv.Key; int n = kv.Value;
            int n2 = item == "scrap" ? n + Diff.C.lootScrap : n;
            int left = p.bag.Add(item, n2);
            if (n2 - left > 0) got.Add($"{n2 - left} {Data.ItemName(item)}");
            if (left > 0) contents[item] = left; else contents.Remove(item);
        }
        if (fitting != null) { p.fittingsOwned.Add(fitting); got.Add("fitting: " + Data.Fittings.P(fitting).S("name")); fitting = null; }
        if (contents.Count == 0) done = true;
        Snd.Pickup();
        if (got.Count > 0) H.ToastMsg("Found " + string.Join(", ", got) + ".");
        if (!done) H.ToastMsg("Your satchel is full. Some is left behind.");
        if (spec.S("id") == "ch_cache") G.OnCacheOpened();
    }
    public override void Draw(Canvas c)
    {
        if (Hidden()) return;
        float bx = x - 1, bw = w + 2, by = y + 4, bh = h - 4;
        // the box: planks, iron corners and bands
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.BeginPath(); c.Ellipse(Cx(), y + h, bw * 0.6f, 1.4f, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#4e3a26"; c.FillRect(bx, by, bw, bh);
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.FillRect(bx, by + bh / 2, bw, 0.6f);
        c.Fill = H.Rgba(255, 220, 170, 0.08f); c.FillRect(bx, by, bw, 1);
        c.FillStyle = "#2a2622"; c.FillRect(bx, by, 1.6f, bh); c.FillRect(bx + bw - 1.6f, by, 1.6f, bh); c.FillRect(bx + 3.5f, by, 1.2f, bh); c.FillRect(bx + bw - 4.7f, by, 1.2f, bh);
        c.FillStyle = "#7a7a70"; c.FillCircle(bx + 0.8f, by + 1, 0.4f); c.FillCircle(bx + bw - 0.8f, by + 1, 0.4f);
        if (done)
        {
            // lid thrown back, the inside dark and empty
            c.FillStyle = "#16120e"; c.FillRect(bx + 1, by - 0.5f, bw - 2, 2);
            c.FillStyle = "#3a2c1e"; c.Poly(new[] { new Vector2(bx, by), new Vector2(bx + bw, by), new Vector2(bx + bw + 1, by - 5), new Vector2(bx + 1, by - 6.5f) });
            c.Fill = H.Rgba(0, 0, 0, 0.3f); c.Poly(new[] { new Vector2(bx, by), new Vector2(bx + bw, by), new Vector2(bx + bw + 0.3f, by - 1.6f), new Vector2(bx + 0.3f, by - 2) });
            return;
        }
        // the domed lid
        c.FillStyle = "#6a4e32";
        c.BeginPath(); c.MoveTo(bx, by); c.LineTo(bx, by - 1.5f); c.QuadraticCurveTo(Cx(), by - 6.5f, bx + bw, by - 1.5f); c.LineTo(bx + bw, by); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 230, 180, 0.18f); c.BeginPath(); c.MoveTo(bx + 1, by - 1.6f); c.QuadraticCurveTo(Cx(), by - 6, bx + bw - 1, by - 1.6f); c.LineTo(bx + bw - 1, by - 1); c.QuadraticCurveTo(Cx(), by - 5, bx + 1, by - 1); c.ClosePath(); c.FillPath();
        c.FillStyle = "#2a2622"; c.FillRect(bx, by - 0.6f, bw, 1.2f);
        // brass lock plate, and a glint now and then
        c.FillStyle = "#c0a060"; c.FillRect(Cx() - 1.6f, by - 1.2f, 3.2f, 3.6f);
        c.FillStyle = "#2a2018"; c.FillRect(Cx() - 0.4f, by + 0.4f, 0.8f, 1.4f);
        float gl = MathF.Max(0, MathF.Sin(t * 1.3f + x)); if (gl > 0.96f) c.Glow(Cx() + 1, by - 1, 3, H.Rgba(255, 240, 200, 0.8f));
    }
    public override Props Serialize()
    {
        var o = new JsonObject { ["done"] = done, ["contents"] = Dict(contents) };
        if (fitting != null) o["fitting"] = fitting;
        return new Props(o);
    }
    public override void Restore(Props s) { done = s.B("done"); if (s.Has("contents")) contents = s.Counts("contents"); fitting = s.S("fitting"); }
}

public class Pickup : Obj
{
    bool warned;
    public Pickup(Props spec, RoomDef room) : base(spec, room) { w = 8; h = 8; x = spec.F("x") - 4; y = spec.F("y") - 8; vy = 0; }
    public override string Prompt() => null;
    public override void Update(float dt)
    {
        base.Update(dt);
        var p = G.player;
        if (!done && p != null && H.RectsOverlap(p.x, p.y, p.w, p.h, x - 4, y - 4, w + 8, h + 8)) Collect(p);
        // settle on the ground
        vx = 0;
        vy = MathF.Min(vy + 600 * dt, 200);
        if (World.IsWater(Cx(), Cy())) vy = MathF.Min(vy, 30);
        Phys.Move(this, dt);
    }
    public void Collect(Player p)
    {
        string item = spec.S("item"); int n = spec.I("n", 1);
        int left = p.bag.Add(item, n);
        if (left == n) { if (!warned) { H.ToastMsg("No room in the satchel for " + Data.ItemName(item) + "."); warned = true; } return; }
        spec.Set("n", left);
        if (looseRef != null) looseRef.n = left;
        Snd.Pickup();
        H.ToastMsg($"+{n - left} {Data.ItemName(item)}", 2);
        if (left <= 0) { done = true; G.RemoveLoose(this); }
        if (item == "tags") G.OnTagsTaken();
        if (item == "crank") G.OnCrankTaken();
    }
    public override void Draw(Canvas c)
    {
        if (done) return;
        string item = spec.S("item");
        float bob = MathF.Sin(t * 3) * 1, cx = Cx(), cy = Cy() - 1 + bob;
        c.Glow(cx, cy, 10, H.Rgba(255, 220, 150, 0.22f + 0.08f * MathF.Sin(t * 4)));
        if (Icons.Has(item)) Icons.Draw(c, item, cx, cy, 9);
        else
        {
            var col = H.Col(Data.Item(item).S("color"));
            c.Fill = col; c.FillRect(x + 1, y + 2 + bob, w - 2, h - 3);
            c.Fill = H.Rgba(255, 255, 255, 0.35f); c.FillRect(x + 1, y + 2 + bob, w - 2, 1);
        }
        // a slow sparkle crossing it
        float sp = (t * 0.7f + x * 0.01f) % 1;
        if (sp < 0.25f) { float k = sp / 0.25f; c.Glow(cx - 4 + k * 8, cy - 3 + k * 2, 2, H.Rgba(255, 250, 230, 0.9f * MathF.Sin(k * H.PI))); }
    }
    public override Props Serialize() => null;
}

// Words cut into the island: read with E, shown as an inscription.
public class Carving : Obj
{
    public Carving(Props spec, RoomDef room) : base(spec, room) { w = 14; h = 18; x = spec.F("x") - 7; y = spec.F("y") - 22; }
    public override string Prompt() => "{interact} Read the carving";
    public override void Interact() { Narr.MarkRead(spec.S("id")); UI.Inscription(spec.S("title"), spec.S("text")); }
    public override LightSrc Light() => Narr.IsRead(spec.S("id")) ? null : new LightSrc { x = Cx(), y = Cy(), r = 26, i = 0.35f, color = "200,220,230" };
    public override void Draw(Canvas c)
    {
        bool read = Narr.IsRead(spec.S("id"));
        float pulse = read ? 0 : (MathF.Sin(t * 2) + 1) * 0.5f;
        float cx = Cx(), top = y, bot = y + h;
        // a standing stone, round-shouldered, sunk into a little cairn
        c.FillStyle = "#283034";
        c.BeginPath(); c.MoveTo(cx - 7, bot); c.LineTo(cx - 7, top + 5); c.QuadraticCurveTo(cx - 7, top, cx, top - 0.5f); c.QuadraticCurveTo(cx + 7, top, cx + 7, top + 5); c.LineTo(cx + 7, bot); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 255, 255, 0.07f); c.BeginPath(); c.MoveTo(cx - 6, bot); c.LineTo(cx - 6, top + 5); c.QuadraticCurveTo(cx - 6, top + 1, cx - 2, top + 0.5f); c.LineTo(cx - 3, top + 3); c.LineTo(cx - 4.5f, bot); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.FillRect(cx + 4.5f, top + 4, 2.5f, h - 4);
        c.FillStyle = "#3a4246"; c.FillCircle(cx - 6, bot - 0.5f, 2.2f); c.FillCircle(cx + 6.5f, bot - 0.8f, 2.6f); c.FillCircle(cx + 2, bot, 1.6f);
        c.FillStyle = "#3e5a40"; c.FillRect(cx - 7, bot - 2.5f, 4, 1.2f); c.FillRect(cx + 3, bot - 1.6f, 4, 1);
        // the cut letters, glowing until they have been read
        var ink = read ? H.Rgba(120, 135, 140, 0.55f) : H.Rgba(170, 225, 245, 0.55f + pulse * 0.45f);
        c.Stroke = ink; c.LineWidth = 0.6f;
        for (int i = 0; i < 5; i++)
        {
            float ly = top + 5 + i * 2.6f, lx = cx - 4;
            for (int k = 0; k < 4; k++)
            {
                float hsh = TileArt.Hash((int)x + i * 7, k * 13);
                float gx = lx + k * 2.2f;
                if (hsh < 0.33f) c.Line(gx, ly - 0.8f, gx, ly + 0.8f);
                else if (hsh < 0.66f) { c.Line(gx - 0.6f, ly + 0.8f, gx, ly - 0.8f); c.Line(gx, ly - 0.8f, gx + 0.6f, ly + 0.8f); }
                else { c.Line(gx - 0.6f, ly, gx + 0.6f, ly); c.Line(gx, ly - 0.8f, gx, ly + 0.8f); }
            }
        }
        if (!read) c.Glow(cx, top + 10, 12, H.Rgba(150, 220, 245, 0.10f + pulse * 0.12f));
    }
    public override Props Serialize() => null;
}

public class Sign : Obj
{
    public Sign(Props spec, RoomDef room) : base(spec, room) { }
    public override string Prompt() => "{interact} Read";
    public override void Interact() => UI.Say(null, spec.S("text"));
    public override void Draw(Canvas c)
    {
        c.Fill = H.Rgba(230, 230, 210, 0.5f);
        c.FillRect(Cx() - 5, y + 2, 10, 1); c.FillRect(Cx() - 4, y + 5, 8, 1); c.FillRect(Cx() - 5, y + 8, 7, 1);
    }
}

// ---------------------------------------------------------------- Bell Room
public class Bell : Obj
{
    public Bell(Props spec, RoomDef room) : base(spec, room) { w = 18; h = 30; x = spec.F("x") - 9; y = spec.F("y") - 30; }
    public bool Lit() => G.bellsLit.Contains(spec.S("id"));
    bool Silenced() => spec.S("id") == "bell_chapel" && G.Quest("tables") == "sold";
    public override string Prompt() => Silenced() ? "{interact} The bell is silent" : G.BellRoomBreached(room.Id) ? "{interact} The bell is drowned out" : Lit() ? "{interact} Bell Room" : "{interact} Rekindle the bell lamp (1 oil flask)";
    public override void Interact()
    {
        if (Silenced()) { UI.Say(null, "The bell hangs cracked and silent. Without its resonance, this room is no longer safe."); return; }
        if (G.BellRoomBreached(room.Id)) { UI.Say(null, "Brine water has breached the room and the bell's note is drowned out. Clear the room before you can rest here."); return; }
        if (!Lit())
        {
            var p = G.player;
            if (p.bag.Count("oil") < 1) { UI.Say(null, "The bell lamp is cold. You need an oil flask to rekindle it."); Snd.Deny(); return; }
            p.bag.Remove("oil", 1);
            G.bellsLit.Add(spec.S("id"));
            Snd.RoomBell();
            H.ToastMsg("Bell Room rekindled. No Brine body will enter while it rings.");
            G.SetRespawn(this);
            G.Save();
        }
        UI.BellMenu(this);
    }
    public override LightSrc Light() => Lit() && !Silenced() ? new LightSrc { x = Cx(), y = y + 6, r = 150, i = 0.9f, color = "255,200,110" } : null;
    public override void Draw(Canvas c)
    {
        float x0 = Cx(), y0 = y;
        bool lit = Lit(), silent = Silenced();
        // the frame: two posts, braces and a yoke
        c.FillStyle = "#3a2a1a"; c.FillRect(x0 - 11, y0 + 1, 2.4f, 29); c.FillRect(x0 + 8.6f, y0 + 1, 2.4f, 29);
        c.FillStyle = "#4a3622"; c.FillRect(x0 - 12, y0 - 1, 24, 3);
        c.Fill = H.Rgba(255, 220, 160, 0.1f); c.FillRect(x0 - 12, y0 - 1, 24, 0.8f); c.FillRect(x0 - 11, y0 + 1, 0.8f, 29);
        c.Stroke = H.Col("#3a2a1a"); c.LineWidth = 1.4f; c.Line(x0 - 9, y0 + 7, x0 - 5, y0 + 2); c.Line(x0 + 9, y0 + 7, x0 + 5, y0 + 2);
        c.FillStyle = "#2a2622"; c.FillCircle(x0 - 9.8f, y0 + 0.5f, 0.6f); c.FillCircle(x0 + 9.8f, y0 + 0.5f, 0.6f);
        float sw = MathF.Sin(t * 1.5f) * (lit ? 0.07f : 0.012f);
        c.Save(); c.Translate(x0, y0 + 2); c.Rotate(sw);
        // the bell: crown, waist, flared lip
        string bronze = silent ? "#4a4030" : "#9a7a3a";
        c.FillStyle = "#2a2622"; c.FillRect(-1.5f, -0.5f, 3, 2.5f);
        c.FillStyle = bronze;
        c.BeginPath(); c.MoveTo(-3.2f, 2); c.QuadraticCurveTo(0, 0.4f, 3.2f, 2); c.QuadraticCurveTo(4.4f, 6, 4.8f, 10); c.QuadraticCurveTo(5.4f, 13.4f, 8, 14.6f); c.LineTo(-8, 14.6f); c.QuadraticCurveTo(-5.4f, 13.4f, -4.8f, 10); c.QuadraticCurveTo(-4.4f, 6, -3.2f, 2); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 240, 200, silent ? 0.05f : 0.3f);
        c.BeginPath(); c.MoveTo(-2.2f, 2.6f); c.QuadraticCurveTo(-3.2f, 7, -3.4f, 10.5f); c.QuadraticCurveTo(-4, 13, -5.6f, 14.2f); c.LineTo(-4.2f, 14.2f); c.QuadraticCurveTo(-2.6f, 12.6f, -2.2f, 10.4f); c.QuadraticCurveTo(-1.8f, 6.6f, -1.2f, 2.6f); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.3f);
        c.BeginPath(); c.MoveTo(2.2f, 2.6f); c.QuadraticCurveTo(3.6f, 7, 4, 10.5f); c.QuadraticCurveTo(4.6f, 13.2f, 7, 14.4f); c.LineTo(3.6f, 14.4f); c.QuadraticCurveTo(2.6f, 12, 2.4f, 10); c.ClosePath(); c.FillPath();
        c.FillStyle = silent ? "#5a5040" : "#c8a460"; c.FillRect(-8.2f, 13.8f, 16.4f, 1.6f); c.FillRect(-4.9f, 9.6f, 9.8f, 0.8f);
        if (silent) { c.Stroke = H.Rgba(0, 0, 0, 0.6f); c.LineWidth = 0.6f; c.Line(1, 4, 2.4f, 9); c.Line(2.4f, 9, 1.6f, 13.5f); }
        c.FillStyle = "#3a3026"; c.FillCircle(MathF.Sin(t * 1.5f + 0.6f) * (lit ? 1.4f : 0.2f), 15.6f, 1.3f);
        c.Restore();
        // the bell lamp hanging from the right post
        float lx = x0 + 6, ly = y0 + 20;
        c.Stroke = H.Col("#2a2622"); c.LineWidth = 0.6f; c.Line(x0 + 8.6f, ly - 3, lx, ly - 3); c.Line(lx, ly - 3, lx, ly - 1.5f);
        c.FillStyle = "#2a2622"; c.FillRect(lx - 2, ly - 1.5f, 4, 1); c.FillRect(lx - 2, ly + 4, 4, 1);
        c.FillStyle = lit ? "#ffd27a" : "#3a3020"; c.FillRect(lx - 1.5f, ly - 0.5f, 3, 4.5f);
        if (lit) { c.Fill = H.Rgba(255, 250, 225, 0.95f); c.FillRect(lx - 0.4f, ly + 0.5f, 0.8f, 2); }
        c.FillStyle = "#2a2622"; c.FillRect(lx - 0.25f, ly - 0.5f, 0.5f, 4.5f);
    }
}
public class Reliquary : Obj
{
    public Reliquary(Props spec, RoomDef room) : base(spec, room) { w = 18; h = 12; x = spec.F("x") - 9; y = spec.F("y") - 12; }
    public override string Prompt() => "{interact} Reliquary chest";
    public override void Interact() => UI.ReliquaryMenu();
    public override void Draw(Canvas c)
    {
        // a coffer for relics: dark wood, gilt corners, a little pitched roof with a bell on the gable
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.BeginPath(); c.Ellipse(Cx(), y + h, w * 0.6f, 1.4f, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#4a2e22"; c.FillRect(x, y + 3, w, h - 3);
        c.FillStyle = "#5a3a2a"; c.Poly(new[] { new Vector2(x - 1, y + 3.5f), new Vector2(Cx(), y - 2), new Vector2(x + w + 1, y + 3.5f) });
        c.FillStyle = "#c0a060"; c.FillRect(x - 1, y + 3, w + 2, 1.2f); c.FillRect(x - 0.5f, y + h - 1.4f, w + 1, 1.4f);
        foreach (float cx2 in new[] { x, x + w - 2 }) { c.FillRect(cx2, y + 3, 2, 2.4f); c.FillRect(cx2, y + h - 3.6f, 2, 2.4f); }
        c.Fill = H.Rgba(255, 245, 200, 0.45f); c.FillRect(x - 1, y + 3, w + 2, 0.5f);
        // the emblem
        float ex = Cx(), ey = y + 7.2f;
        c.FillStyle = "#d8b870"; c.BeginPath(); c.MoveTo(ex - 2, ey + 2); c.QuadraticCurveTo(ex - 2, ey - 2, ex, ey - 2.4f); c.QuadraticCurveTo(ex + 2, ey - 2, ex + 2, ey + 2); c.ClosePath(); c.FillPath();
        c.FillRect(ex - 2.6f, ey + 2, 5.2f, 0.7f);
        c.FillStyle = "#c0a060"; c.FillCircle(Cx(), y - 2.6f, 1);
        c.Glow(ex, ey, 6, H.Rgba(255, 220, 140, 0.12f + 0.06f * MathF.Sin(t * 2)));
    }
}
public class Bench : Obj
{
    public Bench(Props spec, RoomDef room) : base(spec, room) { w = 22; h = 12; x = spec.F("x") - 11; y = spec.F("y") - 12; }
    public override string Prompt() => "{interact} Bench: craft, upgrade, fittings";
    public override void Interact() => UI.BenchMenu(room.Id == "chapel");
    public override void Draw(Canvas c)
    {
        // a heavy workbench: thick top, splayed legs, a vise on one end, tools laid out
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.BeginPath(); c.Ellipse(Cx(), y + h, w * 0.6f, 1.4f, 0, 0, H.PI * 2); c.FillPath();
        c.FillStyle = "#3a2c1e"; c.Poly(new[] { new Vector2(x + 1, y + 6), new Vector2(x + 3.5f, y + 6), new Vector2(x + 2.5f, y + h), new Vector2(x - 0.5f, y + h) });
        c.Poly(new[] { new Vector2(x + w - 3.5f, y + 6), new Vector2(x + w - 1, y + 6), new Vector2(x + w + 0.5f, y + h), new Vector2(x + w - 2.5f, y + h) });
        c.FillRect(x + 1.5f, y + 9.5f, w - 3, 1.2f);
        c.FillStyle = "#5a4430"; c.FillRect(x - 1, y + 3, w + 2, 3.4f);
        c.Fill = H.Rgba(255, 220, 170, 0.16f); c.FillRect(x - 1, y + 3, w + 2, 0.8f);
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.FillRect(x - 1, y + 5.6f, w + 2, 0.8f); c.FillRect(x + 6, y + 3.8f, 0.5f, 2); c.FillRect(x + 14, y + 3.8f, 0.5f, 2);
        // vise
        c.FillStyle = "#4a4a46"; c.FillRect(x - 2.5f, y - 0.5f, 4, 4); c.FillRect(x - 3.5f, y + 1, 1, 2.6f);
        c.FillStyle = "#7a7a72"; c.FillRect(x - 2.5f, y - 0.5f, 4, 0.8f);
        c.Stroke = H.Col("#6a6a62"); c.LineWidth = 0.6f; c.Line(x - 4.5f, y + 2.3f, x - 2.5f, y + 2.3f);
        // hammer, tongs, a few rivets
        c.FillStyle = "#6a5034"; c.FillRect(x + 4, y + 2, 6, 1);
        c.FillStyle = "#7a7a72"; c.FillRect(x + 9, y + 0.6f, 1.8f, 2.6f);
        c.Stroke = H.Col("#5a5a54"); c.LineWidth = 0.6f; c.Line(x + 12, y + 2.6f, x + 18, y + 2); c.Line(x + 12, y + 2.8f, x + 18, y + 2.9f);
        c.FillStyle = "#b0a080"; c.FillCircle(x + 19.5f, y + 2.4f, 0.6f); c.FillCircle(x + 20.6f, y + 2.6f, 0.5f);
    }
}

public class NPC : Obj
{
    public string id;
    public NPC(Props spec, RoomDef room) : base(spec, room) { w = 12; h = 26; x = spec.F("x") - 6; y = spec.F("y") - 26; id = spec.S("id"); }
    bool Present() => Story.NpcPresent(id, room.Id);
    public override string Prompt() => Present() ? "{interact} Speak with " + Story.NpcName(id) : null;
    public override void Interact() { if (Present()) Story.Talk(id); }
    public override void Draw(Canvas c)
    {
        if (!Present()) return;
        float x0 = Cx(), gy = y + h; int f = G.player != null && G.player.Cx() < x0 ? -1 : 1;
        var look = Story.NpcLook(id);
        var robe = H.Col(look.robe).Lerp(H.Col("#0e0f10"), 0.55f); var skin = H.Col(look.skin).Lerp(H.Col("#e8e2d4"), 0.45f);
        float br = MathF.Sin(t * 1.6f) * 0.4f, sway = MathF.Sin(t * 0.9f) * 0.5f;
        float sh = gy - 18 + br, hy = gy - 21.5f + br;
        // shadow, then the long robe with a ragged hem
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.BeginPath(); c.Ellipse(x0, gy, 7, 1.3f, 0, 0, H.PI * 2); c.FillPath();
        c.Fill = robe;
        c.Poly(new[] { new Vector2(x0 - 3.8f, sh), new Vector2(x0 + 3.8f, sh), new Vector2(x0 + 5.6f + sway * 0.3f, gy), new Vector2(x0 + 3, gy - 1.2f), new Vector2(x0 + 0.5f + sway, gy), new Vector2(x0 - 2, gy - 1), new Vector2(x0 - 5.8f + sway, gy) });
        c.Fill = H.Rgba(0, 0, 0, 0.25f); c.Poly(new[] { new Vector2(x0 - 3.8f * f, sh), new Vector2(x0 - 1 * f, sh), new Vector2(x0 - 1.6f * f + sway, gy), new Vector2(x0 - 5.8f * f + sway, gy) });
        c.Fill = H.Rgba(255, 255, 255, 0.07f); c.FillRect(x0 + f * 2 - 0.5f, sh + 1, 1, 15);
        c.Stroke = H.Rgba(0, 0, 0, 0.35f); c.LineWidth = 0.5f; c.Line(x0 + f * 0.6f, sh + 7, x0 + f * 1.2f + sway, gy - 1);
        // cord belt
        c.FillStyle = "#8a7650"; c.FillRect(x0 - 4.3f, sh + 6.5f, 8.6f, 1); c.FillCircle(x0 + f * 1.5f, sh + 7, 0.8f);
        c.Stroke = H.Col("#8a7650"); c.LineWidth = 0.6f; c.Line(x0 + f * 1.5f, sh + 7, x0 + f * 1.8f + sway * 0.5f, sh + 11);
        // salt crust
        if (look.crust) { c.Fill = H.Rgba(230, 240, 250, 0.75f); c.FillCircle(x0 - 2, gy - 13, 1.2f); c.FillCircle(x0 + 1.5f, gy - 8, 0.9f); c.FillCircle(x0 - 3.2f, gy - 5, 0.8f); c.FillRect(x0 - 4.5f, gy - 2, 3, 1); }
        // hands folded at the waist, or one holding a lamp out
        if (look.lamp)
        {
            float lx = x0 + f * 7, ly = gy - 11 + br + MathF.Sin(t * 1.3f) * 0.4f;
            c.Stroke = robe; c.LineWidth = 2.2f; c.Line(x0 + f * 2.5f, sh + 2, x0 + f * 5, sh + 5.5f);
            c.Fill = skin; c.FillCircle(x0 + f * 5.6f, sh + 6, 1);
            c.Stroke = H.Col("#2a2622"); c.LineWidth = 0.6f; c.Line(x0 + f * 5.6f, sh + 6, lx, ly - 3);
            c.FillStyle = "#2a2622"; c.FillRect(lx - 1.8f, ly - 3, 3.6f, 0.9f); c.FillRect(lx - 1.8f, ly + 2.2f, 3.6f, 0.9f);
            c.FillStyle = "#ffd27a"; c.FillRect(lx - 1.3f, ly - 2.1f, 2.6f, 4.3f);
            c.Fill = H.Rgba(255, 250, 225, 0.95f); c.FillRect(lx - 0.4f, ly - 1.2f, 0.8f, 2);
            c.Glow(lx, ly, 9, H.Rgba(255, 190, 110, 0.35f));
        }
        else
        {
            c.Fill = robe; c.FillCircle(x0 + f * 1.4f, sh + 6, 2.2f);
            c.Fill = skin; c.FillCircle(x0 + f * 2.4f, sh + 6.2f, 1.1f);
        }
        // head turned toward Maren: a face, a hood or a hat
        c.Fill = skin; c.FillCircle(x0 + f * 0.8f, hy, 3.3f);
        c.Fill = H.Rgba(0, 0, 0, 0.18f); c.BeginPath(); c.Arc(x0 + f * 0.8f, hy, 3.3f, H.PI * 0.1f, H.PI * 0.9f); c.FillPath();
        float blink = (t % 4.3f) < 0.12f ? 0.2f : 1;
        c.Fill = H.Rgba(20, 20, 24, 0.85f); c.FillRect(x0 + f * 2.2f - 0.4f, hy - 0.8f, 0.8f, 0.9f * blink);
        c.Fill = H.Rgba(0, 0, 0, 0.3f); c.FillRect(x0 + f * 2 - 0.6f, hy + 1.6f, 1.2f, 0.4f);
        switch (look.hat)
        {
            case null:
                // a monk's cowl
                c.Fill = robe;
                c.BeginPath(); c.MoveTo(x0 + f * 3.4f, hy - 1); c.QuadraticCurveTo(x0 + f * 2.6f, hy - 4.6f, x0 - f * 0.4f, hy - 4.4f); c.QuadraticCurveTo(x0 - f * 4.4f, hy - 3.6f, x0 - f * 4, hy + 1.5f); c.LineTo(x0 - f * 3.8f, sh + 1); c.LineTo(x0 + f * 1.2f, sh + 0.5f); c.QuadraticCurveTo(x0 - f * 0.6f, hy + 1.5f, x0 + f * 0.4f, hy - 1.6f); c.QuadraticCurveTo(x0 + f * 1.6f, hy - 2.8f, x0 + f * 3.4f, hy - 1); c.ClosePath(); c.FillPath();
                break;
            case "#3a3028":
                // a salt-worker's wide hat
                c.FillStyle = look.hat; c.BeginPath(); c.Ellipse(x0 + f * 0.6f, hy - 2.4f, 6, 1.3f, 0, 0, H.PI * 2); c.FillPath();
                c.BeginPath(); c.MoveTo(x0 - 2.6f + f * 0.6f, hy - 2.4f); c.QuadraticCurveTo(x0 + f * 0.6f, hy - 6.6f, x0 + 2.6f + f * 0.6f, hy - 2.4f); c.ClosePath(); c.FillPath();
                c.Fill = H.Rgba(230, 230, 236, 0.5f); c.FillRect(x0 - 2 + f * 0.6f, hy - 3.6f, 4, 0.6f);
                break;
            case "#c0a060":
                // a circlet of brass over a veil
                c.Fill = robe; c.BeginPath(); c.Arc(x0, hy - 0.4f, 3.7f, H.PI * 0.85f, H.PI * 2.15f); c.LineTo(x0 - f * 3.6f, sh + 1); c.ClosePath(); c.FillPath();
                c.FillStyle = look.hat; c.FillRect(x0 - 3.4f, hy - 3.2f, 6.8f, 1);
                for (int i = -1; i <= 1; i++) c.Poly(new[] { new Vector2(x0 + i * 2.2f - 0.7f, hy - 3.2f), new Vector2(x0 + i * 2.2f, hy - 5), new Vector2(x0 + i * 2.2f + 0.7f, hy - 3.2f) });
                c.Glow(x0, hy - 3.5f, 4, H.Rgba(255, 220, 140, 0.25f));
                break;
            default:
                // a keeper's brass cap with a peak
                c.FillStyle = look.hat; c.BeginPath(); c.Arc(x0 + f * 0.6f, hy - 1, 3.4f, H.PI, H.PI * 2); c.ClosePath(); c.FillPath();
                c.FillRect(x0 + f * 0.6f - (f > 0 ? 0 : 5.2f), hy - 1.4f, 5.2f, 0.9f);
                c.Fill = H.Rgba(255, 240, 200, 0.35f); c.FillRect(x0 - 1.5f, hy - 3.6f, 2.4f, 0.6f);
                break;
        }
    }
    public override LightSrc Light() => Present() && Story.NpcLook(id).lamp ? new LightSrc { x = Cx(), y = Cy(), r = 50, i = 0.5f, color = "255,200,120" } : null;
    public override Props Serialize() => null;
}
