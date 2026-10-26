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
        if (rivets < 3 && !mats && !lifeline && (G.difficulty != "drowned" || H.Rand() < 0.5f)) { contents["scrap"] = contents.GetValueOrDefault("scrap") + 2; lifeline = true; }
        var got = new List<string>();
        foreach (var kv in contents.ToList())
        {
            string item = kv.Key; int n = kv.Value;
            int n2 = G.difficulty == "pilgrim" && item == "scrap" ? n + 1 : n;
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
        c.FillStyle = done ? "#2a2420" : "#4a3a28";
        c.FillRect(x, y + 2, w, h - 2);
        c.Fill = H.Rgba(0, 0, 0, 0.25f); c.FillRect(x, y + h - 2, w, 2);
        c.FillStyle = done ? "#3a3028" : "#6a5238";
        c.FillRect(x - 1, y, w + 2, 4);
        c.Fill = H.Rgba(255, 230, 180, done ? 0.05f : 0.15f); c.FillRect(x - 1, y, w + 2, 1);
        c.Fill = H.Rgba(30, 20, 10, 0.6f); c.FillRect(x + 3, y + 4, 1, h - 4); c.FillRect(x + w - 4, y + 4, 1, h - 4);
        if (!done) { c.FillStyle = "#c0a060"; c.FillRect(Cx() - 1, y + 3, 2, 3); }
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
        var col = H.Col(Data.Item(spec.S("item")).S("color"));
        float bob = MathF.Sin(t * 3) * 1;
        c.Fill = H.Rgba(255, 230, 160, 0.15f); c.BeginPath(); c.Arc(Cx(), Cy() + bob, 7, 0, H.PI * 2); c.FillPath();
        c.Fill = col; c.FillRect(x + 1, y + 2 + bob, w - 2, h - 3);
        c.Fill = H.Rgba(255, 255, 255, 0.35f); c.FillRect(x + 1, y + 2 + bob, w - 2, 1);
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
        c.FillStyle = "#2b3236"; c.FillRect(x, y, w, h);
        c.FillStyle = "#3c454a"; c.FillRect(x + 1, y + 1, w - 2, 2);
        c.Fill = H.Rgba(0, 0, 0, 0.35f); c.FillRect(x + w - 2, y + 2, 2, h - 2);
        c.Fill = read ? H.Rgba(150, 165, 170, 0.45f) : H.Rgba(190, 225, 240, 0.45f + pulse * 0.4f);
        for (int i = 0; i < 5; i++) { float lw = 8 - ((i * 3) % 4); c.FillRect(x + 3, y + 5 + i * 2.6f, lw, 1); }
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
        c.FillStyle = "#3a2a1a"; c.FillRect(x0 - 10, y0, 2, 30); c.FillRect(x0 + 8, y0, 2, 30); c.FillRect(x0 - 10, y0, 20, 3);
        c.Fill = H.Rgba(255, 220, 160, 0.08f); c.FillRect(x0 - 10, y0, 1, 30);
        float sw = MathF.Sin(t * 1.5f) * (Lit() ? 0.06f : 0.01f);
        c.Save(); c.Translate(x0, y0 + 3); c.Rotate(sw);
        c.FillStyle = Silenced() ? "#4a4030" : "#a08040";
        c.BeginPath(); c.MoveTo(-3, 0); c.LineTo(3, 0); c.LineTo(7, 14); c.LineTo(-7, 14); c.ClosePath(); c.FillPath();
        c.Fill = H.Rgba(255, 240, 200, Silenced() ? 0.05f : 0.25f);
        c.BeginPath(); c.MoveTo(-2, 1); c.LineTo(-0.5f, 1); c.LineTo(-3, 13); c.LineTo(-5.5f, 13); c.ClosePath(); c.FillPath();
        c.FillStyle = "#c0a060"; c.FillRect(-8, 13, 16, 2);
        c.Restore();
        c.FillStyle = Lit() ? "#ffd27a" : "#3a3020";
        c.FillRect(x0 - 2, y0 + 22, 4, 6);
    }
}
public class Reliquary : Obj
{
    public Reliquary(Props spec, RoomDef room) : base(spec, room) { w = 18; h = 12; x = spec.F("x") - 9; y = spec.F("y") - 12; }
    public override string Prompt() => "{interact} Reliquary chest";
    public override void Interact() => UI.ReliquaryMenu();
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#5a4030"; c.FillRect(x, y + 2, w, h - 2);
        c.Fill = H.Rgba(0, 0, 0, 0.25f); c.FillRect(x, y + h - 2, w, 2);
        c.FillStyle = "#c0a060"; c.FillRect(x, y, w, 3); c.FillRect(Cx() - 2, y + 4, 4, 4);
        c.Fill = H.Rgba(255, 245, 200, 0.4f); c.FillRect(x, y, w, 1);
    }
}
public class Bench : Obj
{
    public Bench(Props spec, RoomDef room) : base(spec, room) { w = 22; h = 12; x = spec.F("x") - 11; y = spec.F("y") - 12; }
    public override string Prompt() => "{interact} Bench: craft, upgrade, fittings";
    public override void Interact() => UI.BenchMenu(room.Id == "chapel");
    public override void Draw(Canvas c)
    {
        c.FillStyle = "#4a3a2a"; c.FillRect(x, y + 3, w, 3);
        c.FillRect(x + 1, y + 6, 2, 6); c.FillRect(x + w - 3, y + 6, 2, 6);
        c.Fill = H.Rgba(255, 220, 170, 0.12f); c.FillRect(x, y + 3, w, 1);
        c.FillStyle = "#8a8a8a"; c.FillRect(x + 4, y, 5, 3);
        c.FillStyle = "#6a5a3a"; c.FillRect(x + 13, y + 1, 6, 2);
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
        c.FillStyle = look.robe;
        c.BeginPath(); c.MoveTo(x0 - 6, gy); c.LineTo(x0 - 4, gy - 18); c.LineTo(x0 + 4, gy - 18); c.LineTo(x0 + 6, gy); c.FillPath();
        c.Fill = H.Rgba(0, 0, 0, 0.2f);
        c.BeginPath(); c.MoveTo(x0 - 6 * f, gy); c.LineTo(x0 - 4 * f, gy - 18); c.LineTo(x0 - 1 * f, gy - 18); c.LineTo(x0 - 2 * f, gy); c.FillPath();
        c.FillStyle = look.skin; c.BeginPath(); c.Arc(x0 + f, gy - 21, 3.6f, 0, H.PI * 2); c.FillPath();
        if (look.hat != null) { c.FillStyle = look.hat; c.FillRect(x0 - 4 + f, gy - 26, 8, 3); }
        if (look.crust) { c.Fill = H.Rgba(230, 240, 250, 0.8f); c.FillRect(x0 - 4, gy - 14, 3, 2); c.FillRect(x0 + 1, gy - 9, 2, 2); }
        if (look.lamp) { c.FillStyle = "#ffd080"; c.FillRect(x0 + f * 6, gy - 12, 3, 3); }
    }
    public override LightSrc Light() => Present() && Story.NpcLook(id).lamp ? new LightSrc { x = Cx(), y = Cy(), r = 50, i = 0.5f, color = "255,200,120" } : null;
    public override Props Serialize() => null;
}
