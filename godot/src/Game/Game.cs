// The game: rooms, tide consequences, acts, saving and the main step.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public static class G
{
    const int TS = H.TILE;
    const string SAVE_PATH = "user://hollowmere_save.json";

    public class Ring { public float x, y, r, t; }
    public class Tracer { public float x1, y1, x2, y2, t; }
    public class Stats
    {
        public Dictionary<string, int> knots = new();
        public Dictionary<int, Dictionary<string, int>> byAct = new() { [1] = new(), [2] = new(), [3] = new() };
        public int deaths; public float time;
    }
    public class Spot { public string room; public float x, y; }
    public class SatchelInfo { public string room; public float x, y; public Bag bag; public bool tidal; }

    public static string mode = "title";
    public static RoomDef room;
    public static List<Ent> ents = new();
    public static Player player;
    public static Vector2 cam;
    public static List<Projectile> projectiles = new();
    public static List<Shot> enemyShots = new();
    public static List<SawLine> sawlines = new();
    public static List<Tracer> tracers = new();
    public static List<Ring> noiseRings = new();
    public static List<LightSrc> frameLights = new();
    // The story-focus lean: the finishing pass magnifies the frame a little about a focal point (view px), so aiming
    // and anything else that maps the screen to the world goes through these two.
    public static float leanZoom = 1;
    public static Vector2 leanFocal = new(H.VIEW_W / 2f, H.VIEW_H / 2f);
    public static Vector2 ScreenToWorld(float sx, float sy) => cam + leanFocal + (new Vector2(sx, sy) - leanFocal) / leanZoom;
    public static Vector2 WorldToScreen(float wx, float wy) => leanFocal + (new Vector2(wx, wy) - cam - leanFocal) * leanZoom;
    public static int act = 1;
    public static Props quests = Props.Empty();
    public static Stats stats = new();
    public static HashSet<int> pages = new(), tables = new();
    public static HashSet<string> collected = new(), bellsLit = new(), tips = new();
    public static Bag reliquary;
    public static Spot respawn;
    public static SatchelInfo satchelDrop;
    public static string difficulty = "keeper";
    public static float swell, valveT, sinceLoad;
    public static float tension;   // how wrong things are this frame (the swell, the Undertow, a boss): the score reads it
    public static UndertowSystem undertow;
    public static Body bossBar;
    public static string encrustedType;
    public static bool freeGraft;
    public static float shakeAmt, flash, pressureWarn, titleT, muzzle;
    public static float dialT = 1;                 // the tide dial fades in once Maren reaches the reef
    public static string promptText;
    public static bool god;
    public static string ended;
    public static Dictionary<string, Spot> wrackPools = new();
    public static NarrState narr;
    public static float? titheFlood;
    // the Evaporation Pans: seconds since the last lever opened the sluices; the hatch is shut once it reaches HATCH_CLOSE
    public static float basinFillT;
    public const float HATCH_CLOSE = 3.2f;
    public static float breachT, glimpseT, frameDt = 1 / 60f;
    public static float stepDt = 1 / 60f;   // this frame's step, set by Main whatever the mode (the view's blends run on it)
    public static bool gusting;
    public static float Now;                       // seconds since start (real time)
    public static float shakeX, shakeY;            // this frame's camera shake offset
    public static event Action RoomChanged;

    // ------------------------------------------------------------------ timers (setTimeout)
    static readonly List<(float t, Action fn)> timers = new();
    public static void After(float seconds, Action fn) => timers.Add((Now + seconds, fn));
    static void RunTimers()
    {
        if (timers.Count == 0) return;
        var due = timers.Where(t => t.t <= Now).ToList();
        timers.RemoveAll(t => t.t <= Now);
        foreach (var t in due) t.fn();
    }

    // ------------------------------------------------------------------ setup
    public static void Init()
    {
        // A pad button with a tap and a hold waits for the release only when holding it would do something else.
        Inp.HoldUseful = i => player == null || i switch
        {
            0 => player.NearestDownedBody(26) != null,     // Y / triangle held burns a body
            1 => player.bag.Count("scrap") > 0,              // RB / R1 held quick-crafts rivets
            2 => player.bag.Count("stitchkit") > 0,          // d-pad up held kneels for a stitch kit
            _ => true,
        };
        Data.Load();
        Profile.Load();
        World.Build();
        FindWrackPools();
        Tide.On(OnPhase);
        player = new Player();
        room = Data.Room["reef"];
        reliquary = new Bag(40);
        undertow = new UndertowSystem();
        narr = new NarrState();
        // Title backdrop: the reef at night.
        EnterRoom(Data.Room["reef"], true);
        PlaceAtStart();
    }

    static void FindWrackPools()
    {
        foreach (var r in Data.Rooms)
            for (int ty = 0; ty < r.Map.Length; ty++)
                for (int tx = 0; tx < r.Map[ty].Length; tx++)
                    if (r.Ents.TryGetValue(r.Map[ty][tx], out var e) && e.S("type") == "wrackpool" && !wrackPools.ContainsKey(r.Region))
                        wrackPools[r.Region] = new Spot { room = r.Id, x = r.Px + tx * TS + 8, y = r.Py + (ty + 1) * TS - 2 };
        foreach (var r in Data.Rooms)
            foreach (var e in r.Ents2)
                if (e.S("type") == "wrackpool" && !wrackPools.ContainsKey(r.Region))
                    wrackPools[r.Region] = new Spot { room = r.Id, x = r.Px + e.I("tx") * TS + 8, y = r.Py + (e.I("ty") + 1) * TS - 2 };
    }

    static void PlaceAtStart()
    {
        var s = ents.FirstOrDefault(e => e.type == "start");
        var p = player;
        if (s != null) { p.x = s.Cx() - p.w / 2; p.y = s.y + s.h - p.h; }
        respawn ??= new Spot { room = room.Id, x = p.x, y = p.y };
        SnapCamera();
    }

    public static void NewGame(string diff, float tide)
    {
        timers.Clear();
        difficulty = diff;
        var C = Diff.C;
        Tide.scale = tide * C.tide;
        Tide.t = 0; Tide.cycle = 0; Tide.spring = 0;
        World.Reset();
        player = new Player();
        player.maxHp = player.hp = C.pips;
        if (C.startScrap > 0) player.bag.Add("scrap", C.startScrap);
        act = 1; quests = Props.Empty(); stats = new Stats();
        pages = new(); tables = new(); collected = new(); bellsLit = new(); reliquary = new Bag(40);
        respawn = null; satchelDrop = null; swell = 0; valveT = 0; undertow = new UndertowSystem(); bossBar = null;
        encrustedType = null; freeGraft = false; ended = null; tips = new(); mode = "play"; narr = new NarrState();
        titheFlood = null; breachT = 0; glimpseT = 0;
        Narr.Reset();
        Hints.Reset();
        FX.parts.Clear();
        Gore.Reset(); Voices.Reset(); Hollow.Reset(); Legend.Reset(); Dread.Reset(); Guide.Reset();
        dialT = 0;
        // Maren comes ashore on the outer rocks; the tide holds at Low until she reaches the reef.
        EnterRoom(Data.Room["breakers"], true);
        PlaceAtStart();
        titleT = 6;
        var lines = Data.Lore.Strings("opening").ToList();
        // the Saint awake: one more line before she sets out
        if (Diff.Saint) lines.Add(Data.Lore.P("saint").S("opening"));
        lines.AddRange(Data.Lore.Strings("openingAfter"));
        UI.Cinematic(lines, () => After(1.2f, () => Narr.Think("boat")), "title");
    }

    public static void ToTitle() { mode = "title"; UI.Title(); }

    // ------------------------------------------------------------------ rooms
    static string SpecKey(Props spec) => spec.S("id") ?? $"{spec.S("type")}@{spec.I("tx")},{spec.I("ty")}";

    public static List<Props> RoomSpecs(RoomDef r)
    {
        var o = new List<Props>();
        for (int ty = 0; ty < r.Map.Length; ty++)
            for (int tx = 0; tx < r.Map[ty].Length; tx++)
                if (r.Ents.TryGetValue(r.Map[ty][tx], out var legend))
                {
                    var s = new Props(JsonNode.Parse(legend.O.ToJsonString()).AsObject());
                    s.Set("tx", tx); s.Set("ty", ty);
                    o.Add(s);
                }
        foreach (var e in r.Ents2) o.Add(new Props(JsonNode.Parse(e.O.ToJsonString()).AsObject()));
        var taken = new HashSet<(int, int)>(o.Select(s => (s.I("tx"), s.I("ty"))));
        o.AddRange(Narr.CarvingSpecs(r, taken));
        foreach (var s in o)
        {
            s.Set("x", r.Px + s.I("tx") * TS + 8);
            s.Set("y", r.Py + (s.I("ty") + 1) * TS);
            s.Set("key", SpecKey(s));
        }
        return o;
    }

    static Ent BuildEntity(Props spec, RoomDef r)
    {
        var f = World.flags;
        string t = spec.S("type");
        // As the night goes on the drowned move: later kinds come up into the places she has been, and earlier ones
        // go down into the deep (spawns marked fromAct appear from that act, untilAct up to it).
        if (spec.Has("fromAct") && act < spec.I("fromAct")) return null;
        if (spec.Has("untilAct") && act > spec.I("untilAct")) return null;
        if (t == "gullet")
        {
            int ty = spec.I("ty");
            while (ty > 0 && World.Solid(r.Ox + spec.I("tx"), r.Oy + ty) != 1) ty--;
            spec.Set("anchorY", r.Py + (ty + 1) * TS);
            spec.Set("x", r.Px + spec.I("tx") * TS + 8);
            return new Gullet(spec, r);
        }
        if (t == "abbot") return f.B("abbotDead") ? null : new Abbot(spec, r);
        if (t == "almoner") return f.B("almonerDead") ? null : new Almoner(spec, r);
        if (t == "precentor") return f.B("precentorDead") ? null : new Precentor(spec, r);
        if (t == "mother") return f.B("motherDead") ? null : new HangingMother(spec, r);
        if (t == "shoal") return act >= 3 && !f.B("shoalDead") ? new Shoal(spec, r) : null;
        if (t == "grafted") return act >= 3 ? new Grafted(spec, r) : null;
        // the last night's weapons: Agathe's Glass is left for Maren at the spring tide; the Ear is always there
        if (t == "burningglass") return act >= 3 && !Legend.glassOwned ? new GlassCase(spec, r) : null;
        if (t == "earsocket") return new EarSocket(spec, r);
        if (t == "choirmember")
        {
            if (quests.B("hesperDead") && f.B("choirDrained")) return null;
            spec.Set("passive", Story.ChoirFriendly());
            return new Grafted(spec, r);
        }
        if (BodyTypes.Has(t)) return BodyTypes.Make(t, spec, r);
        return ObjTypes.Make(t, spec, r);
    }

    public static void SaveRoomState()
    {
        if (room == null) return;
        var st = World.State(room.Id);
        foreach (var e in ents)
        {
            if (e.key == null) continue;
            var s = e.Serialize();
            if (s != null) st.ents[e.key] = s;
        }
    }

    static readonly HashSet<string> COLLECTS = new() { "stitch", "seal", "page", "table", "ability", "weapon" };
    public static void EnterRoom(RoomDef r, bool initial = false)
    {
        if (!initial) SaveRoomState();
        string prevRegion = room?.Region;
        room = r;
        ents = new List<Ent>();
        projectiles = new(); enemyShots = new(); sawlines = new(); tracers = new();
        Fire.Clear();
        Gore.Clear();
        Snd.Acoustics(r);
        bossBar = null;
        var st = World.State(r.Id);
        bool firstVisit = !st.visited;
        st.visited = true;
        foreach (var spec in RoomSpecs(r))
        {
            var e = BuildEntity(spec, r);
            if (e == null) continue;
            st.ents.TryGetValue(e.key ?? "", out var saved);
            if (saved != null)
            {
                if (saved.B("reknotted") && e is Body b0)
                {
                    var copy = new Props(JsonNode.Parse(saved.O.ToJsonString()).AsObject());
                    copy.O.Remove("knots"); copy.Set("mode", "hunt");
                    b0.Restore(copy); b0.StandUp(); saved.O.Remove("reknotted");
                }
                else e.Restore(saved);
            }
            else if (e is Body b1 && act >= 2) ApplyEncrustation(b1);
            if (e.IsCollectible || COLLECTS.Contains(e.type)) { if (saved != null && saved.B("done")) continue; }
            ents.Add(e);
        }
        // Loose items dropped or carried here.
        foreach (var l in st.loose ?? new List<Loose>()) ents.Add(MakeLoosePickup(l, r));
        if (satchelDrop != null && satchelDrop.room == r.Id) ents.Add(MakeSatchel(r));
        RoomEvents(r, firstVisit);
        Hollow.OnRoomEnter(r);
        Legend.OnRoomEnter(r);
        sinceLoad = 0;
        if (firstVisit || prevRegion != r.Region) titleT = 2.5f;
        if (Narr.EnterRegion(r.Region)) titleT = 6;
        Narr.OnRoomEnter();
        MarkSeen();
        RoomChanged?.Invoke();
    }
    static Pickup MakeLoosePickup(Loose l, RoomDef r)
    {
        var s = Props.Empty(); s.Set("type", "pickup"); s.Set("item", l.item); s.Set("n", l.n); s.Set("x", l.x); s.Set("y", l.y);
        return new Pickup(s, r) { looseRef = l };
    }
    static SatchelDrop MakeSatchel(RoomDef r)
    {
        var s = Props.Empty(); s.Set("type", "satchel"); s.Set("x", satchelDrop.x); s.Set("y", satchelDrop.y);
        return new SatchelDrop(s, r, satchelDrop.bag);
    }

    static void MarkSeen() => World.State(room.Id).seen.Add(Tide.PhaseId());

    static Props Spec(params (string k, JsonNode v)[] kv) { var s = Props.Empty(); foreach (var (k, v) in kv) s.Set(k, v); return s; }

    static void RoomEvents(RoomDef r, bool firstVisit)
    {
        var f = World.flags; var q = quests;
        if (r.Id == "pier" && !f.B("pierIntro"))
        {
            f.Set("pierIntro", true);
            if (Tide.PhaseId() == "low" && Tide.PhaseRemaining() > 14) Tide.SetPhase("low", Tide.Len(0) - 12);
        }
        if (r.Id == "kilns" && f.B("basinFilling")) f.Set("basinCrossed", true);
        // a room can have a thought of its own, named for it, for the first time she comes in
        string enterThought = r.Id switch { "skullstair" => "skull", "lamp" => "agathe", _ => Data.L("thoughts", r.Id) != null ? r.Id : null };
        if (enterThought != null) Narr.Think(enterThought);
        // Reaching the reef: the tide starts to move, and the dial shows it.
        if (r.Id != "breakers" && !f.B("landed"))
        {
            f.Set("landed", true);
            if (r.Id == "reef") Narr.Think("start");
            // From here on a fall wakes Maren on the Constance's deck, not back out on the rocks.
            if (respawn?.room == "breakers") respawn = ReefDeck();
        }
        Tide.paused = r.Id == "breakers" && !f.B("landed");
        // Act III: the Brine breaches one Bell Room.
        if (act >= 3 && r.Id == BreachRoom() && !f.B("breachDone"))
        {
            if (r.Id == "saltbell" && q.S("teodor") == "captured")
            {
                var t = new Shucker(Spec(("x", r.Px + 30 * TS), ("y", r.Py + 19 * TS), ("key", "teodor_reknot"), ("pose", "standing"), ("teodor", true)), r);
                t.mode = "hunt"; t.breach = true;
                ents.Add(t); bossBar = t;
                breachT = 60;
                Snd.Toll();
                H.ToastMsg("The spring tide breaches the Saltworks Bell Room. Teodor has re-knotted, and he is going for Odile! (60 s)", 5);
            }
            else if (r.Id == "cloisterbell")
            {
                for (int i = 0; i < 2; i++)
                {
                    var s = new Shucker(Spec(("x", r.Px + (14 + i * 10) * TS), ("y", r.Py + 17 * TS), ("key", "breach" + i), ("pose", "standing")), r);
                    ApplyEncrustation(s); ents.Add(s);
                }
                f.Set("breachDone", true);
                H.ToastMsg("The spring tide has breached this Bell Room. The bell is drowned out. It is not safe here.", 4);
            }
        }
        if (r.Id == "choir" && act >= 3 && f.B("choirDrained") && !q.B("hesperDead"))
        {
            var hh = new Grafted(Spec(("x", r.Px + 31 * TS), ("y", r.Py + 17 * TS), ("key", "hesper_boss"), ("boss", true)), r);
            hh.hesper = true; ents.Add(hh); bossBar = hh;
            H.ToastMsg("Hesper Vane waits in the dry shrine, lamp raised. \"You took the Saint's water.\"", 4);
        }
        if (r.Id == "lamp" && Story.ChoirFriendly() && !q.B("hesperDead") && act >= 3)
            ents.Add(new NPC(Spec(("type", "npc"), ("id", "hesper"), ("x", r.Px + 11 * TS), ("y", r.Py + 17 * TS), ("key", "hesper_lamp")), r));
        if (r.Id == "lair" && !f.B("undertowDead"))
        {
            var b = new Undertow(Spec(("x", r.Px + 20 * TS), ("y", r.Py + 11 * TS), ("key", "undertow_boss"), ("boss", true), ("dir", 1)), r);   // you come in from behind, out of the Buoy Chapel
            ents.Add(b); bossBar = b;
            Snd.Buoy(0.9f, 0.6f);
            H.ToastMsg("The buoy bell tolls in the dark water. The Undertow is home.", 4);
        }
        if (r.Id == "abbot" && f.B("abbotDead") && !player.Has("boots"))
            ents.Add(new Collectible(Spec(("type", "ability"), ("id", "boots"), ("x", r.Px + 20 * TS), ("y", r.Py + 18 * TS), ("key", "boots_drop")), r));
    }

    static string BreachRoom() => quests.S("teodor") == "captured" && !World.flags.B("breachDone") ? "saltbell" : "cloisterbell";
    public static bool BellRoomBreached(string id) => act >= 3 && id == "cloisterbell" && World.flags.B("breachDone") && BreachRoom() == "cloisterbell" || (id == "saltbell" && breachT > 0);

    // ------------------------------------------------------------------ tide consequences
    static void OnPhase(string ph, string prev)
    {
        if (mode != "play") return;
        Snd.TideBell();
        H.ToastMsg("The tide turns: " + Tide.PhaseName() + ".", 2.5f);
        if (room != null) MarkSeen();
        undertow?.OnPhase(ph);
        if (ph == "rising") { Hints.TideTurned(); Score.Sting("turn", 0.85f); }
        var f = World.flags;
        if (ph == "rising")
        {
            // Off-screen: every downed body below the high-water line will be reached by the water.
            foreach (var r in Data.Rooms)
            {
                if (r == room) continue;
                var st = World.StateOrNull(r.Id);
                if (st == null) continue;
                foreach (var s in st.ents.Values)
                {
                    if (s == null || s.S("mode") != "downed" || s.B("gone") || s.A("knots") == null || s.A("knots").Count == 0) continue;
                    if (InTideWater(r, s)) s.Set("reknot", true);
                }
            }
            if (room.Tidal) Narr.Think("rising");
        }
        if (ph == "high")
        {
            if (room.Tidal && room.FixedWater == null) Narr.Think("firstHigh");
            foreach (var r in Data.Rooms)
            {
                var st = World.StateOrNull(r.Id);
                if (st == null || r == room) continue;
                foreach (var s in st.ents.Values)
                    if (s != null && s.B("reknot") && s.S("mode") == "downed" && !s.B("gone")) { s.Set("reknot", false); s.Set("reknotted", true); s.Set("mode", "hunt"); }
            }
            int n = 0;
            foreach (var e0 in ents.ToArray()) if (e0 is Body e && e.reknot && e.mode == "downed" && !e.gone) { e.StandUp(); n++; }
            if (n > 0) H.ToastMsg(n == 1 ? "A body you left in the water grows new knots and stands." : n + " bodies you left in the water grow new knots and stand.", 3.5f);
        }
        if (ph == "ebbing")
        {
            // Currents drag loose items seaward to each region's Wrack Pool. Bolts that sink are lost.
            foreach (var r in Data.Rooms)
            {
                var st = World.StateOrNull(r.Id);
                if (st?.loose == null) continue;
                var keep = new List<Loose>();
                foreach (var l in st.loose)
                {
                    bool inWater = World.RoomSurface(r, l.x) < l.y - 2 && r.Tidal;
                    if (!inWater) { keep.Add(l); continue; }
                    if (l.item == "bolts") continue;
                    wrackPools.TryGetValue(r.Region, out var wp);
                    if (wp != null && wp.room != r.Id) { var ws = World.State(wp.room); (ws.loose ??= new()).Add(new Loose { item = l.item, n = l.n, x = wp.x + H.Rand(-8, 8), y = wp.y }); }
                    else if (wp != null) { l.x = wp.x + H.Rand(-8, 8); l.y = wp.y; keep.Add(l); }
                }
                st.loose = keep;
            }
            if (room != null) RefreshLoose();
            var sd = satchelDrop;
            if (sd != null && sd.tidal)
            {
                if (wrackPools.TryGetValue(Data.Room[sd.room].Region, out var wp))
                {
                    sd.room = wp.room; sd.x = wp.x; sd.y = wp.y; sd.tidal = false;
                    H.ToastMsg("The ebb carries your satchel to the Wrack Pool. It is marked on your map.", 4);
                    if (room != null) { ents.RemoveAll(e => e.type == "satchel"); if (sd.room == room.Id) ents.Add(MakeSatchel(room)); }
                }
            }
            // Ebb resets the barge puzzle if nobody crossed.
            if (!f.B("basinCrossed")) { f.Set("basinSeq", new JsonArray()); f.Set("basinJam", false); f.Set("basinFilling", false); f.Set("basinTarget", 19); }
        }
    }
    static bool InTideWater(RoomDef r, Props s)
    {
        float bx = s.F("x") + 6, by = s.F("y") + 4;
        if (r.FixedWater != null || r.Id == "choir" || r.Pools != null) return World.RoomSurface(r, bx) < by;
        if (!r.Tidal) return false;
        return by > (Tide.SEA_HIGH - Tide.spring) * TS;
    }
    static void RefreshLoose()
    {
        ents.RemoveAll(e => e.looseRef != null);
        foreach (var l in World.State(room.Id).loose ?? new List<Loose>()) ents.Add(MakeLoosePickup(l, room));
    }

    // ------------------------------------------------------------------ helpers used by entities
    public static void SpawnLoose(string item, float x, float y, int n = 1)
    {
        var st = World.State(room.Id);
        st.loose ??= new List<Loose>();
        var l = new Loose { item = item, n = n, x = x, y = y };
        st.loose.Add(l);
        ents.Add(MakeLoosePickup(l, room));
    }
    public static void SpawnPickup(string item, float x, float y) => SpawnLoose(item, x, y, 1);
    public static void RemoveLoose(Ent e)
    {
        if (e.looseRef == null) return;
        World.State(room.Id).loose?.Remove(e.looseRef);
    }
    public static void AddSwell(float n)
    {
        swell += n;
        // Pacing valve: after a peak, 60-90 s with no new wake-ups.
        if (swell > 9 && valveT <= 0) { valveT = H.Rand(60, 90); swell = 0; }
    }
    public static bool CanWake() => valveT <= 0;
    // A heartbeat's pause on a solid hit, so it lands. Skipped under the test harness, which runs on fixed time.
    public static float hitStop;
    public static void HitStop(float s) => hitStop = MathF.Max(hitStop, s);
    public static void Shake(float n)
    {
        shakeAmt = MathF.Max(shakeAmt, n);
        // the pad feels every jolt the camera does, whether or not screen shake is on
        Pad.Rumble(MathF.Min(1, 0.12f * n + 0.08f), n >= 2 ? MathF.Min(1, 0.11f * n) : 0, 0.08f + 0.05f * n);
    }
    // Where the reef's start marker stands: the Constance's deck.
    static Spot ReefDeck()
    {
        var r = Data.Room["reef"];
        for (int ty = 0; ty < r.Map.Length; ty++)
            for (int tx = 0; tx < r.Map[ty].Length; tx++)
                if (r.Ents.TryGetValue(r.Map[ty][tx], out var e) && e.S("type") == "start")
                    return new Spot { room = r.Id, x = r.Px + tx * TS + 8 - player.w / 2, y = r.Py + (ty + 1) * TS - player.h };
        return new Spot { room = r.Id, x = r.Px + 25 * TS, y = r.Py + 12 * TS };
    }
    static bool RingNear(Player p, int r)
    {
        int tx = H.FloorI(p.Cx() / TS), ty = H.FloorI(p.Cy() / TS);
        for (int dy = -r; dy <= 2; dy++) for (int dx = -r; dx <= r; dx++) if (World.CharAt(tx + dx, ty + dy) == 'o') return true;
        return false;
    }
    public static void Tip(string id, string text, float t = 4) { if (tips.Contains(id)) return; tips.Add(id); H.ToastMsg(text, t); }
    public static string Quest(string k) => quests.S(k);
    public static void SetQuest(string k, string v) => quests.Set(k, v);
    public static string BellIdFor(string roomId)
    {
        var r = Data.Room[roomId];
        foreach (var row in r.Map) foreach (var ch in row) if (r.Ents.TryGetValue(ch, out var e) && e.S("type") == "bell") return e.S("id");
        return null;
    }

    public static void ApplyEncrustation(Body b)
    {
        string type = encrustedType;
        if (type == null || b.knots.Count == 0 || b.boss || b.type == "driftling") return;
        foreach (var k in b.knots.Where(k => k.type == type && !k.plated).ToList())
        {
            if (b.knots.Count(q => !q.plated) <= 2) break;
            k.plated = true; k.braced = true;
        }
    }

    public static void StartAct(int n)
    {
        if (act >= n) return;
        // Adaptive encrustation: the Brine armours what the player broke most.
        var counts = stats.byAct.GetValueOrDefault(act) ?? new();
        string best = null; int bn = 0;
        foreach (var kv in counts) if (kv.Key != "crown" && kv.Value > bn) { bn = kv.Value; best = kv.Key; }
        encrustedType = best;
        act = n;
        if (n == 2) Narr.Think("actTwo");
        if (n == 3) Narr.Think("actThree");
        Legend.OnActStart(n);
        if (n == 2) H.ToastMsg("Act II. The Undertow begins to hunt at High tide." + (best != null ? $" New Brine bodies grow salt plates over the {best}." : ""), 6);
        if (n == 3)
        {
            Tide.spring = 2;
            H.ToastMsg("Act III. Spring tides raise every water line by one level. The Brine is breaching the Bell Rooms.", 6);
        }
    }

    public static void OnBodyDowned(Body b, string src)
    {
        if (!b.boss) Narr.Think("firstDown");

        AddSwell(0.5f);
        if (b.breach) { breachT = 0; World.flags.Set("breachDone", true); quests.Set("teodor", "dead"); bossBar = null; H.ToastMsg("Teodor falls for the last time. Odile is safe.", 4); }
        if (b.hesper) { quests.Set("hesperDead", true); bossBar = null; H.ToastMsg("Hesper Vane falls. The Choir's hymn goes unfinished.", 4); }
    }
    public static void OnBodyBurned(Body b) { }
    public static void OnChoirAttacked()
    {
        if (World.flags.B("choirHostile")) return;
        World.flags.Set("choirHostile", true);
        foreach (var e0 in ents) if (e0 is Grafted e && e.passive) { e.passive = false; e.mode = "hunt"; }
        H.ToastMsg("The Choir turns on you.", 3);
    }
    public static void OnCrankTaken()
    {
        var f = World.flags;
        if (f.B("titheTrapped")) return;
        f.Set("titheTrapped", true);
        // The trap: the hall's bell tolls, every active body wakes, and water pours up through the grates.
        Snd.Toll(); Shake(5);
        foreach (var e0 in ents.ToArray()) if (e0 is Body e && !e.corpse && e.mode == "dormant") { e.lastHeard = new Vector2(player.Cx(), player.Cy()); e.Wake(true); }
        foreach (var e0 in ents.ToArray()) if (e0 is Gullet gl && gl.mode == "hang") gl.Hear(player.Cx(), player.Cy(), 0, 999, "scream");
        titheFlood = 0;
        H.ToastMsg("The crank's counterweight drops. The hall bell tolls overhead, and water pours up through the floor grates!", 5);
        Narr.Think("titheTrap");

    }
    public static void OnTagsTaken() { }
    public static void OnCacheOpened()
    {
        var p = player;
        // On the drained path the cache replaces Hesper's satchel as the second expansion.
        if (!quests.B("hesperSatchel") && !quests.B("cacheSatchel")) { quests.Set("cacheSatchel", true); p.bag.size = Math.Max(p.bag.size + 4, 12); }
        H.ToastMsg("Odile's salvage cache, with a big salvage satchel. Satchel: " + p.bag.size + " slots.", 4);
    }
    public static void OnAbbotKilled(Body a)
    {
        World.flags.Set("abbotDead", true);
        bossBar = null;
        ents.Add(new Collectible(Spec(("type", "ability"), ("id", "boots"), ("x", a.Cx()), ("y", a.Feet()), ("key", "boots_drop")), room));
        player.bag.Add("ivory", 2);
        H.ToastMsg("The Abbot of Salt crumbles. 2 Mere-ivory. Something heavy clatters to the floor: his Ballast Boots.", 5);
        StartAct(2);
    }
    public static void OnAlmonerKilled(Body a)
    {
        World.flags.Set("almonerDead", true);
        World.MarkDirty();
        bossBar = null;
        player.bag.Add("ivory", 2);
        Snd.Rumble();
        H.ToastMsg("The Almoner goes down across his table. 2 Mere-ivory. The chain at his collar goes slack, and the chains on the doors west fall away.", 6);
        Narr.Think("almonerDead");
    }
    public static void OnPrecentorKilled(Body b)
    {
        World.flags.Set("precentorDead", true);
        World.MarkDirty();
        bossBar = null;
        player.bag.Add("ivory", 2);
        Snd.Rumble();
        H.ToastMsg("The Precentor's last note runs out of him, and the organ falls silent. 2 Mere-ivory. The doors east grind open.", 6);
        Narr.Think("precentorDead");
    }
    public static void OnMotherKilled(Body b)
    {
        World.flags.Set("motherDead", true);
        World.MarkDirty();
        bossBar = null;
        player.bag.Add("ivory", 2);
        Snd.Rumble();
        H.ToastMsg("The Hanging Mother goes still across the ribs. 2 Mere-ivory. Below and behind you, bone that had grown across the doors cracks and falls away.", 6);
        Narr.Think("motherDead");
    }
    public static void OnUndertowKilled()
    {
        World.flags.Set("undertowDead", true); World.flags.Set("skullOpen", true);
        undertow.dead = true; bossBar = null;
        World.MarkDirty();
        player.bag.Add("ivory", 2);
        H.ToastMsg("The Undertow dies in its lair. Far above, at the top of the Ribvault, the hatch into the Skull grinds open.", 6);
    }
    public static void OnShoalKilled()
    {
        World.flags.Set("shoalDead", true);
        World.MarkDirty();
        bossBar = null;
        player.bag.Add("ivory", 2);
        Snd.Rumble();
        H.ToastMsg("The Shoal comes apart. 2 Mere-ivory. The way on to the lamp is open.", 6);
    }
    public static void Collect(Props spec)
    {
        Story.Collect(spec);
        var d = Props.Empty(); d.Set("done", true);
        World.State(room.Id).ents[spec.S("key")] = d;
    }
    // The bell-cable lifts, from Act II: a lit Bell Room rides its cable to any other lit Bell Room in its region.
    public static List<(RoomDef room, string bell, string name)> CableTargets(Ent from)
    {
        var list = new List<(RoomDef, string, string)>();
        if (act < 2 || from?.room == null) return list;
        foreach (var r in Data.Rooms)
        {
            if (!r.BellRoom || r == from.room || r.Region != from.room.Region) continue;
            foreach (var sp in RoomSpecs(r))
                if (sp.S("type") == "bell" && bellsLit.Contains(sp.S("id"))) list.Add((r, sp.S("id"), sp.S("name") ?? r.Name));
        }
        return list;
    }
    public static void RideCable(RoomDef r)
    {
        Snd.Toll(); Shake(1);
        EnterRoom(r);
        var bell = ents.OfType<Bell>().FirstOrDefault();
        if (bell != null) { player.x = bell.Cx() - 20; player.y = bell.y + bell.h - player.h; }
        player.vx = player.vy = 0; player.state = "normal";
        SnapCamera();
        H.ToastMsg("The cable sings as the cage runs down it, bell to bell. " + r.Name + ".", 3);
    }
    public static void SetRespawn(Ent bell) => respawn = new Spot { room = bell.room.Id, x = bell.Cx() - 20, y = bell.y + bell.h - player.h };

    public static void SetChoirLevel(string name)
    {
        var f = World.flags;
        int lvl = name switch { "mid" => 17, "raised" => 3, _ => 21 };
        f.Set("choirLevelName", name);
        f.Set("choirTarget", lvl);
        if (name == "drained")
        {
            f.Set("choirDrained", true);
            quests.Set("choirWater", "drained");
            OnChoirAttacked();
            Snd.Rumble(); Shake(5);
            H.ToastMsg("The shrine drains with a roar. Steps emerge from the silt. The Choir screams.", 5);
            World.MarkDirty();
        }
        else
        {
            Snd.Rumble();
            H.ToastMsg(name == "raised" ? "The water climbs toward the vault above the shrine." : "The water settles back to the shrine line.", 3);
        }
    }

    // ------------------------------------------------------------------ interaction
    public static void Interact()
    {
        var p = player;
        if (p.dragging != null) { p.StopDrag(); return; }
        Obj best = null; float bd = 1e9f;
        foreach (var e0 in ents)
        {
            if (e0 is not Obj e) continue;
            if (!e.Near(p)) continue;
            string pr = e.Prompt();
            if (pr == null || pr.StartsWith("{gaff}") || pr.StartsWith("Lift pan")) continue;
            float d = MathF.Abs(e.Cx() - p.Cx());
            if (d < bd) { bd = d; best = e; }
        }
        if (best != null) { best.Interact(); return; }
        var b = p.NearestDownedBody(24);
        if (b != null)
        {
            if (b.teodor && b.mode == "captured")
            {
                quests.Set("teodor", "bound");
                b.gone = true;
                World.State(room.Id).ents[b.key] = b.Serialize();
                Snd.Hook();
                H.ToastMsg("You bind Teodor with cord. Odile's people will carry him up to the Saltworks Bell Room. Tell her.", 5);
                return;
            }
            p.StartDrag(b);
        }
    }
    static string ComputePrompt()
    {
        var p = player;
        if (p.dragging != null) return "{interact} Let go   (dragging is slow; haul it above the tide line)";
        string best = null; float bd = 1e9f;
        foreach (var e0 in ents)
        {
            if (e0 is not Obj e || !e.Near(p)) continue;
            string pr = e.Prompt(); if (pr == null) continue;
            float d = MathF.Abs(e.Cx() - p.Cx());
            if (d < bd) { bd = d; best = pr; }
        }
        if (best != null) return best;
        var b = p.NearestDownedBody(24);
        if (b != null)
        {
            if (b.teodor && b.mode == "captured") return "{interact} Bind Teodor for Odile";
            return "{interact} Drag body   {burn} Burn it (oil)";
        }
        return null;
    }

    // ------------------------------------------------------------------ death and rest
    public static void OnPlayerDeath()
    {
        Pad.Rumble(0.6f, 1f, 0.7f);
        var p = player;
        if (p.dead) return;
        p.dead = true; p.grabbedBy = null; if (p.dragging != null) p.StopDrag();
        stats.deaths++;
        Score.Sting("death");
        bool drowned = p.HeadUnder() && p.breath <= 0.01f;
        if (drowned) Snd.Choke();
        Snd.MarenDies(drowned);
        // The satchel stays where she fell; weapons and fittings stay with her. (On the gentlest crossing she keeps it.)
        if (Diff.C.keepSatchel)
        {
            Snd.Toll();
            var (t0, l0) = Narr.DeathText();
            After(0.9f, () => UI.Death(t0, l0, "The pilgrim's grace: your satchel is still on your shoulder.", Respawn));
            return;
        }
        var bag = new Bag(99);
        foreach (var kv in p.bag.List()) bag.items[kv.Key] = kv.Value;
        if (satchelDrop != null) foreach (var kv in satchelDrop.bag.List()) bag.items[kv.Key] = bag.items.GetValueOrDefault(kv.Key) + kv.Value;
        p.bag.Clear();
        bool tidal = room.Tidal && p.InWater();
        satchelDrop = new SatchelInfo { room = room.Id, x = p.Cx(), y = p.y + p.h, bag = bag, tidal = tidal };
        Snd.Toll();
        var (title, line) = Narr.DeathText();
        string detail = tidal ? "You died in tidal water. Your satchel lies where you fell; the next Ebb will carry it to this region's Wrack Pool, marked on your map." : "Your satchel lies where you fell. Nothing is ever lost for good, but getting it back is its own small expedition.";
        After(0.9f, () => UI.Death(title, line, detail, Respawn));
    }
    public static void Respawn()
    {
        var p = player; var r = respawn;
        p.dead = false; p.hp = p.maxHp; p.breath = float.IsFinite(p.BreathMax()) ? p.BreathMax() : 30; p.state = "normal"; p.vx = p.vy = 0; p.invulnT = 2;
        p.grabbedBy = null; p.healT = 0; p.reloadT = 0; p.craftT = 0;
        if (p.oil < 30) p.oil = 30;
        EnterRoom(Data.Room[r.room]);
        p.x = r.x; p.y = r.y;
        SnapCamera();
        breachT = 0;
        if (stats.deaths == 1) After(0.6f, () => Narr.Think("firstDeath"));
    }
    public static void RecoverSatchel(Ent obj)
    {
        var p = player; var sd = satchelDrop;
        if (sd == null) return;
        foreach (var kv in sd.bag.List())
        {
            int left = p.bag.Add(kv.Key, kv.Value);
            if (left > 0) reliquary.Add(kv.Key, left);
        }
        satchelDrop = null;
        ents.Remove(obj);
        Snd.Pickup();
        H.ToastMsg("You recover your satchel. Anything that did not fit went to the Reliquary.", 4);
    }
    public static void Rest(Bell bell)
    {
        var p = player;
        // the bell hurts what carries the sea: the Tide Lung, or the Tine grown in her hand
        bool salted = p.grafts.Contains("lung") || Legend.merged;
        int heal = salted ? (int)MathF.Ceiling((p.maxHp - p.hp) / 2f) : p.maxHp - p.hp;
        if (salted && heal < p.maxHp - p.hp) H.ToastMsg(Legend.merged ? "The bell's note grinds at the bone in your hand. Healing is halved." : "The bell's resonance grinds at the salt in your chest. Healing is halved.", 3);
        p.hp = Math.Min(p.maxHp, p.hp + heal);
        // the bell's long note fills the Hollow halfway while she sleeps (the Tine it bleeds dry)
        if (Hollow.owned) Hollow.resonance = Legend.merged ? 0 : MathF.Max(Hollow.resonance, Hollow.SEA_FILL);
        SetRespawn(bell);
        SaveRoomState();
        Tide.AdvanceTo("low");
        titheFlood = null; World.State("tithe").flood = null;
        Save();
        Snd.RoomBell();
        flash = 0;
        UI.Cinematic(new List<string> { Narr.NextDream() }, () => H.ToastMsg("You wake by the bell. The tide has turned all the way round to Low.", 3));
    }

    // ------------------------------------------------------------------ save / load
    public static bool HasSave() => Godot.FileAccess.FileExists(SAVE_PATH);
    // What the title says of the save: the Bell Room she last rang, the act, how long she has been out, how hard.
    public static (string bell, int act, float time, string difficulty)? SaveSummary()
    {
        if (!HasSave()) return null;
        try
        {
            var d = new Props(JsonNode.Parse(Godot.FileAccess.GetFileAsString(SAVE_PATH)).AsObject());
            string id = d.P("respawn")?.S("room");
            return (id != null && Data.Room.TryGetValue(id, out var r) ? r.Name : "the island", d.I("act", 1), d.P("stats")?.F("time") ?? 0, d.S("difficulty") ?? "keeper");
        }
        catch { return null; }
    }
    public static void DeleteSave() { if (HasSave()) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SAVE_PATH)); }
    public static void Save()
    {
        SaveRoomState();
        JsonArray IA(IEnumerable<int> s) => new(s.Select(v => (JsonNode)v).ToArray());
        JsonArray SA(IEnumerable<string> s) => new(s.Select(v => (JsonNode)v).ToArray());
        var data = new JsonObject
        {
            ["v"] = 1, ["player"] = player.ToJson(), ["flags"] = JsonNode.Parse(World.flags.O.ToJsonString()),
            ["states"] = new JsonObject(World.States.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value.ToJson()))),
            ["tide"] = new JsonObject { ["t"] = Tide.t, ["cycle"] = Tide.cycle, ["scale"] = Tide.scale, ["spring"] = Tide.spring },
            ["act"] = act, ["quests"] = JsonNode.Parse(quests.O.ToJsonString()),
            ["stats"] = new JsonObject
            {
                ["knots"] = new JsonObject(stats.knots.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value))),
                ["byAct"] = new JsonObject(stats.byAct.Select(a => new KeyValuePair<string, JsonNode>(a.Key.ToString(), new JsonObject(a.Value.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value)))))),
                ["deaths"] = stats.deaths, ["time"] = stats.time,
            },
            ["pages"] = IA(pages), ["tables"] = IA(tables), ["collected"] = SA(collected), ["bellsLit"] = SA(bellsLit),
            ["reliquary"] = reliquary.ToJson(),
            ["respawn"] = new JsonObject { ["room"] = respawn.room, ["x"] = respawn.x, ["y"] = respawn.y },
            ["satchelDrop"] = satchelDrop == null ? null : new JsonObject { ["room"] = satchelDrop.room, ["x"] = satchelDrop.x, ["y"] = satchelDrop.y, ["tidal"] = satchelDrop.tidal, ["bag"] = satchelDrop.bag.ToJson() },
            ["difficulty"] = difficulty, ["encrustedType"] = encrustedType, ["freeGraft"] = freeGraft, ["tips"] = SA(tips),
            ["undertowDead"] = undertow.dead, ["narr"] = narr.ToJson(), ["hollow"] = Hollow.ToJson(), ["legend"] = Legend.ToJson(),
            // where every room sat when this was saved, so a later, larger island can move what the save remembers
            ["origins"] = new JsonObject(Data.Rooms.Select(r => new KeyValuePair<string, JsonNode>(r.Id, new JsonArray(r.Px, r.Py)))),
        };
        using var fa = Godot.FileAccess.Open(SAVE_PATH, Godot.FileAccess.ModeFlags.Write);
        if (fa == null) { H.ToastMsg("Could not save (storage unavailable)."); return; }
        fa.StoreString(data.ToJsonString());
    }
    public static void LoadGame()
    {
        Props d = null;
        try { d = new Props(JsonNode.Parse(Godot.FileAccess.GetFileAsString(SAVE_PATH)).AsObject()); } catch { }
        if (d == null) { UI.Title(); return; }
        timers.Clear();
        World.Reset();
        World.flags = d.P("flags") ?? Props.Empty();
        var states = new Dictionary<string, RoomState>();
        if (d.P("states") is Props sp) foreach (var k in sp.Keys) states[k] = RoomState.FromJson(sp.P(k).O);
        var moved = RoomMoves(d.P("origins"));
        foreach (var (id, st) in states) MoveState(st, moved(id));
        World.States = states;
        var td = d.P("tide"); Tide.t = td.F("t"); Tide.cycle = td.I("cycle"); Tide.scale = td.F("scale", 1); Tide.spring = td.F("spring");
        player = new Player(); player.Load(d.P("player"));
        act = d.I("act", 1); quests = d.P("quests") ?? Props.Empty();
        var s = d.P("stats"); stats = new Stats { knots = s.Counts("knots"), deaths = s.I("deaths"), time = s.F("time") };
        if (s.P("byAct") is Props ba) foreach (var k in ba.Keys) stats.byAct[int.Parse(k)] = ba.Counts(k);
        pages = new HashSet<int>(d.A("pages").Select(n => (int)n.GetValue<double>()));
        tables = new HashSet<int>(d.A("tables").Select(n => (int)n.GetValue<double>()));
        collected = new HashSet<string>(d.Strings("collected")); bellsLit = new HashSet<string>(d.Strings("bellsLit"));
        reliquary = Bag.From(d.P("reliquary"));
        var rs = d.P("respawn"); var rm = moved(rs.S("room"));
        respawn = new Spot { room = rs.S("room"), x = rs.F("x") + rm.X, y = rs.F("y") + rm.Y };
        var sdp = d.P("satchelDrop"); var sm = sdp == null ? default : moved(sdp.S("room"));
        satchelDrop = sdp == null ? null : new SatchelInfo { room = sdp.S("room"), x = sdp.F("x") + sm.X, y = sdp.F("y") + sm.Y, tidal = sdp.B("tidal"), bag = Bag.From(sdp.P("bag")) };
        difficulty = d.S("difficulty") ?? "keeper"; encrustedType = d.S("encrustedType"); freeGraft = d.B("freeGraft");
        tips = new HashSet<string>(d.Strings("tips")); narr = NarrState.From(d.P("narr")); dialT = 1;
        undertow = new UndertowSystem { dead = d.B("undertowDead") }; mode = "play"; ended = null; swell = 0; valveT = 0;
        titheFlood = null; breachT = 0;
        Narr.Reset();
        Hints.Reset();
        Gore.Reset(); Voices.Reset(); Hollow.Load(d.P("hollow")); Legend.Load(d.P("legend")); Guide.Reset();
        player.hp = player.maxHp;
        EnterRoom(Data.Room[respawn.room], true);
        player.x = respawn.x; player.y = respawn.y;
        SnapCamera();
        titleT = 3;
    }

    // How far each room has moved since a save was made, in world pixels. A save made before rooms recorded their
    // origins comes from the 26-room island, whose rooms sat where PROTO puts them (anything else it names is taken to
    // have moved as most of that island did, 30 screens east and 5 south).
    static Func<string, Godot.Vector2> RoomMoves(Props origins)
    {
        return id =>
        {
            if (id == null || !Data.Room.TryGetValue(id, out var r)) return default;
            if (origins == null)
                return PROTO.TryGetValue(id, out var c) ? new Godot.Vector2(r.Px - c.col * H.SCREEN_W * TS, r.Py - c.row * H.SCREEN_H * TS)
                    : new Godot.Vector2(30 * H.SCREEN_W * TS, 5 * H.SCREEN_H * TS);
            if (origins.A(id) is not JsonArray o || o.Count < 2) return default;
            return new Godot.Vector2(r.Px - (float)o[0].GetValue<double>(), r.Py - (float)o[1].GetValue<double>());
        };
    }
    // The 26-room island's grid, in screens.
    static readonly Dictionary<string, (int col, int row)> PROTO = new()
    {
        ["reef"] = (7, 4), ["breakers"] = (8, 4), ["pier"] = (6, 4), ["seawall"] = (6, 3), ["chapel"] = (7, 3), ["tithe"] = (5, 4), ["tithe_gallery"] = (5, 3),
        ["saltgate"] = (6, 2), ["saltbell"] = (5, 2), ["kilns"] = (7, 2), ["abbot"] = (7, 1), ["brineshaft"] = (4, 2),
        ["cloister"] = (4, 4), ["cloisterbell"] = (3, 3), ["nave"] = (3, 4), ["priors"] = (2, 4), ["choirsteps"] = (2, 3),
        ["choir"] = (1, 3), ["choirbell"] = (0, 3), ["ribshaft"] = (1, 1), ["ribbell"] = (2, 1), ["prisms"] = (3, 1),
        ["gulletmouth"] = (2, 5), ["lair"] = (3, 5), ["skullstair"] = (2, 0), ["lamp"] = (3, 0),
    };
    static void MoveState(RoomState st, Godot.Vector2 by)
    {
        if (by == default) return;
        if (st.loose != null) foreach (var l in st.loose) { l.x += by.X; l.y += by.Y; }
        foreach (var e in st.ents.Values)
            if (e != null && e.Has("x") && e.Has("y")) { e.Set("x", e.F("x") + by.X); e.Set("y", e.F("y") + by.Y); }
    }

    public static string StatsSummary()
    {
        int k = stats.knots.Values.Sum();
        string held = Legend.communion > 0 ? $" · The Tine in hand: {H.FmtTime(Legend.communion)}" : "";
        return $"Knots broken: {k} · Deaths: {stats.deaths} · Time: {H.FmtTime(stats.time)} · Agathe's pages: {pages.Count}/9 · Grafts: {player.grafts.Count}{held} · {Diff.C.name}";
    }

    // ------------------------------------------------------------------ camera
    public static void SnapCamera() => UpdateCamera(1, true);
    static void UpdateCamera(float dt, bool snap = false)
    {
        var p = player; var r = room;
        float look = p.facing * 30;
        float tx = p.Cx() + look - H.VIEW_W / 2, ty = p.Cy() - H.VIEW_H / 2 - 10;
        // looking up or down on purpose, and down ahead of a long fall to see where she will land
        if (p.lookT > 0.4f) ty += p.lookDir * 76;
        else if (!p.onGround && p.state == "normal" && p.vy > 260) ty += MathF.Min(56, (p.vy - 260) * 0.25f);
        if (!snap) { tx += StoryFocus.CamOffset.X; ty += StoryFocus.CamOffset.Y; }
        // in a boss's hall the frame leans toward him, so a boss hung high over her stays in view
        if (bossBar != null && !bossBar.gone && bossBar.room == r)
        {
            float bx = bossBar.Cx() - H.VIEW_W / 2, by = bossBar.Cy() - H.VIEW_H / 2;
            tx = H.Lerp(tx, bx, 0.25f);
            ty = H.Lerp(ty, by, MathF.Min(0.45f, MathF.Max(0, p.Cy() - bossBar.Cy()) / (H.VIEW_H * 1.2f)));
        }
        if (r.Pw <= H.VIEW_W) tx = r.Px + (r.Pw - H.VIEW_W) / 2; else tx = H.Clamp(tx, r.Px, r.Px + r.Pw - H.VIEW_W);
        if (r.Ph <= H.VIEW_H) ty = r.Py + (r.Ph - H.VIEW_H) / 2; else ty = H.Clamp(ty, r.Py, r.Py + r.Ph - H.VIEW_H);
        if (snap) { cam = new Vector2(tx, ty); return; }
        float k = MathF.Min(1, dt * 6 * StoryFocus.Damping);
        cam = new Vector2(H.Lerp(cam.X, tx, k), H.Lerp(cam.Y, ty, k));
    }

    // ------------------------------------------------------------------ main step
    public static void Step(float dt)
    {
        Now += dt;
        RunTimers();
        frameDt = dt;
        if (mode != "play" || UI.IsOpen())
        {
            if (mode == "title" && !UI.IsOpen()) UI.Title();
            // keep the backdrop alive on the title screen
            if (mode == "title") { Tide.Update(0); frameLights = new(); leanZoom = 1; }
            return;
        }
        if (Inp.Hit("pause")) { UI.Pause(); return; }
        if (Inp.Hit("map")) { UI.Map(); return; }
        if (Inp.Hit("satchel")) { UI.Satchel("satchel"); return; }
        if (Inp.Hit("journal")) { UI.Satchel(null); return; }
        if (Inp.Hit("debug"))
        {
            if (Inp.Held("sneak"))
            {
                var pp = player;
                foreach (var a in new[] { "swing", "boots", "jet", "tidekey", "seal", "lens" }) pp.abilities.Add(a);
                foreach (var wn in new[] { "harpoon", "sling", "sawline" }) if (!pp.weapons.ContainsKey(wn)) { pp.weapons[wn] = new WeaponState { tier = 0, mag = 1 }; pp.weaponOrder.Add(wn); }
                pp.bag.size = 16; pp.bag.Add("rivets", 36); pp.bag.Add("oil", 3); H.ToastMsg("DEBUG: all tools.");
            }
            else { god = !god; H.ToastMsg("DEBUG: invulnerable " + (god ? "on" : "off")); }
        }
        stats.time += dt;
        sinceLoad += dt;
        Tide.Update(dt);
        var p = player;

        // Choir water and basin animate toward their targets.
        var f = World.flags;
        var cst = World.State("choir");
        if (f.Has("choirTarget")) cst.choirLevel = H.Approach(cst.choirLevel ?? 17, f.F("choirTarget"), dt * 1.2f);
        var sst = World.State("saltgate");
        sst.pool ??= new Dictionary<string, float>();
        if (f.Has("basinTarget")) sst.pool["basin"] = H.Approach(sst.pool.TryGetValue("basin", out var bl) ? bl : 19, f.F("basinTarget"), dt * (f.B("basinFilling") ? 1.1f : 2));
        if (f.B("basinFilling") && !f.B("basinCrossed"))
        {
            float was = basinFillT; basinFillT += dt;
            if (was < HATCH_CLOSE && basinFillT >= HATCH_CLOSE) { World.MarkDirty(); if (room?.Id == "saltgate") { Snd.Thud(); Shake(2); } }
        }
        else basinFillT = 0;
        // Tithe Hall flood set-piece.
        if (titheFlood != null)
        {
            titheFlood += dt;
            float t = titheFlood.Value; var st = World.State("tithe");
            if (t < 25) st.flood = H.Lerp(17, 9.5f, t / 25);
            else if (t < 80) st.flood = 9.5f;
            else if (t < 110) st.flood = H.Lerp(9.5f, 18, (t - 80) / 30);
            else { st.flood = null; titheFlood = null; }
        }
        if (breachT > 0)
        {
            breachT -= dt;
            if (breachT <= 0 && !World.flags.B("breachDone"))
            {
                World.flags.Set("breachDone", true); quests.Set("odileLost", true); quests.Set("teodor", "dead");
                foreach (var e in ents) if (e.key == "teodor_reknot") e.gone = true;
                bossBar = null;
                H.ToastMsg("Too late. Odile Marrow is dead, and her bench with her.", 5);
            }
            else if (ents.Any(e => e.key == "teodor_reknot" && e is Body b && b.mode == "captured"))
            {
                World.flags.Set("breachDone", true); breachT = 0; quests.Set("teodor", "saved"); bossBar = null;
                foreach (var e in ents) if (e.key == "teodor_reknot") e.gone = true;
                H.ToastMsg("You break every limb knot again and leave his Crown whole. Teodor sags. This time, the Brine lets go. Odile weeps.", 6);
            }
        }

        // Dynamic platforms
        var plats = new List<Platform>();
        foreach (var e in ents) { var pl = e.PlatformRect(); if (pl != null) plats.Add(pl); }
        Phys.SetPlatforms(plats);

        p.Update(dt);
        // Room transitions: walk off any open edge into the neighbouring room.
        var r = room;
        if (!p.dead && (p.Cx() < r.Px || p.Cx() >= r.Px + r.Pw || p.Cy() < r.Py || p.Cy() >= r.Py + r.Ph))
        {
            var nr = World.RoomAtPx(p.Cx(), p.Cy());
            if (nr != null && nr != r)
            {
                if (p.dragging != null) p.StopDrag();
                EnterRoom(nr);
            }
            else { p.x = H.Clamp(p.x, r.Px, r.Px + r.Pw - p.w); p.y = H.Clamp(p.y, r.Py, r.Py + r.Ph - p.h); }
        }
        foreach (var e in ents.ToArray()) e.Update(dt);
        // Re-knot marking: a downed body touched by water during the Rising tide.
        if (Tide.PhaseId() == "rising")
            foreach (var e0 in ents) if (e0 is Body e && e.mode == "downed" && !e.gone && !e.dragged && e.knots.Count > 0 && World.IsWater(e.Cx(), e.Feet() - 3)) e.reknot = true;
        ents.RemoveAll(e => e is Body b && b.gone && (b.type == "driftling" || b.type == "undertow" || b.shed));
        foreach (var pr in projectiles.ToArray()) pr.Update(dt);
        projectiles.RemoveAll(q => q.dead);
        foreach (var s in sawlines.ToArray()) s.Update(dt);
        sawlines.RemoveAll(s => s.dead);
        foreach (var s in enemyShots)
        {
            s.vy += s.grav * dt;
            s.x += s.vx * dt; s.y += s.vy * dt; s.life -= dt;
            if (World.SolidPx(s.x, s.y)) s.life = 0;
            float sz = s.kind == "net" ? 6 : s.kind == "bowl" ? 4 : 2;
            if (H.RectsOverlap(s.x - sz, s.y - sz, sz * 2, sz * 2, p.x, p.y, p.w, p.h))
            {
                // a thrown net does not wound: it holds
                if (s.kind == "net") { if (!p.Invuln()) p.Entangle(2.6f); }
                else p.Hurt((int)s.dmg, s.src as Mover);
                s.life = 0;
            }
        }
        enemyShots.RemoveAll(s => s.life <= 0);
        Fire.Update(dt);
        FX.Update(dt);
        Gore.Update(dt);
        Voices.Update(dt);
        Dread.Update(dt);
        Hollow.Update(dt);
        Legend.Update(dt);
        Guide.Update(dt);
        AmbientOneShots(dt);
        Narr.UpdateWhispers(dt);
        undertow.Update(dt);

        // Tutorial: the sailor's knots glint under the focused beam, then go dark.
        if (room.Id == "reef" && p.focusing)
        {
            var s = ents.OfType<Corpse>().FirstOrDefault(e => e.sailor);
            if (s != null && s.knots.Count > 0 && s.fadeKnots == 0 && p.BeamHits(s.Cx(), s.Cy())) { s.fadeKnots = 3; After(1.5f, () => Narr.Think("sailorKnots")); }
        }
        if (room.Id == "pier")
        {
            var s = ents.OfType<Body>().FirstOrDefault(e => e.key == "first_shucker");
            if (s != null && s.mode != "dormant") Narr.Think("firstStand");
        }
        // A few things Maren notices for herself.
        var had = narr.thoughts;
        if (!had.Contains("lowOil") && p.LanternLit() && p.oil < p.OilMax() * 0.15f) Narr.Think("lowOil");
        if (!had.Contains("rings") && p.Has("swing") && RingNear(p, 6)) Narr.Think("rings");
        if (!had.Contains("bellmouth") && ents.OfType<Body>().Any(b => b.type == "bellmouth" && b.Alive() && H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy()) < 220)) Narr.Think("bellmouth");
        // the first of each kind she sees: it goes in the journal's bestiary, and the new ones get a thought of her own
        foreach (var e0 in ents)
        {
            if (e0 is not Body b || b.corpse || b.gone || !b.Alive() || b.boss || narr.kinds.Contains(b.Kind)) continue;
            if (H.Dist(b.Cx(), b.Cy(), p.Cx(), p.Cy()) > 220 || !Phys.Los(p.Cx(), p.Cy(), b.Cx(), b.Cy())) continue;
            narr.kinds.Add(b.Kind);
            Narr.Think("see_" + b.Kind);
        }
        Hints.Update(dt);
        // Undertow glimpse at the Tithe window.
        if (room.Id == "tithe" && f.B("titheTrapped") && !f.B("glimpse") && p.y < room.Py + 8 * TS)
        {
            f.Set("glimpse", true); glimpseT = 4;
            Snd.Buoy(0.9f, 0.7f); Shake(6);
            if (titheFlood != null) titheFlood = MathF.Max(titheFlood.Value, 25);
            var st = World.State("tithe"); if (st.flood != null) st.flood -= 1;
            After(4.5f, () => Narr.Think("glimpse"));
            H.ToastMsg("At the window, the stained glass darkens. Something vast slides past outside with the slow clang of a buoy bell. It does not fight yet.", 7);
        }
        if (glimpseT > 0) glimpseT -= dt;
        // Wind gusts are telegraphed by streaks of salt dust.
        if (room.Raw.Has("windZones"))
        {
            float w = p.WindAt();
            if (w != 0 && H.Rand() < 48 * dt) FX.Spawn(cam.X + (w > 0 ? 0 : H.VIEW_W), cam.Y + H.Rand() * H.VIEW_H, 1, new FXOpt { ang = w > 0 ? 0 : H.PI, spread = 0.05f, smin = MathF.Abs(w) * 2, smax = MathF.Abs(w) * 3, color = "rgba(230,225,210,0.6)", grav = 0, lmin = 1.5f, lmax = 2.5f, size = 1.2f });
            if (w != 0 && !gusting) { gusting = true; Snd.Burst(); }
            if (w == 0) gusting = false;
        }

        // Timers and feedback
        valveT -= dt;
        swell = MathF.Max(0, swell - dt * 0.12f);
        shakeAmt = MathF.Max(0, shakeAmt - dt * 20);
        flash = MathF.Max(0, flash - dt);
        pressureWarn = MathF.Max(0, pressureWarn - dt * 2);
        muzzle = MathF.Max(0, muzzle - dt);
        titleT -= dt;
        foreach (var t in H.Toasts) t.T -= dt;
        H.Toasts.RemoveAll(t => t.T <= 0);
        foreach (var n in noiseRings) n.t -= dt;
        noiseRings.RemoveAll(n => n.t <= 0);
        foreach (var t in tracers) t.t -= dt;
        tracers.RemoveAll(t => t.t <= 0);
        if (H.FloorI(stats.time) % 2 == 0) MarkSeen();

        // what Maren is looking at (camera, focus and light lean toward it; see StoryFocus)
        StoryFocus.Update(dt);
        // Lights for this frame
        var lights = new List<LightSrc> { new() { player = true } };
        foreach (var e in ents) { var l = e.Light(); if (l != null) lights.Add(l); }
        foreach (var pr in projectiles) { var l = pr.Light(); if (l != null) lights.Add(l); }
        StoryFocus.AddLights(lights);
        Legend.AddLights(lights);
        frameLights = lights;

        promptText = ComputePrompt();
        // The tide holds at Low on the outer rocks until Maren reaches the reef; then the dial appears.
        Tide.paused = room.Id == "breakers" && !f.B("landed");
        if (f.B("landed")) dialT = MathF.Min(1, dialT + dt * 0.6f);
        UpdateCamera(dt);
        shakeX = (H.Rand() - 0.5f) * shakeAmt; shakeY = (H.Rand() - 0.5f) * shakeAmt;
        // Ambience follows the tide and the tension.
        tension = H.Clamp(swell / 9 + (undertow.live != null ? 0.6f : 0) + (bossBar != null ? 0.4f : 0), 0, 1);
        Snd.Ambience(room.Tidal || room.Outdoor ? Tide.Level01() : 0.2f, tension, p.HeadUnder(), p.inBellRoom);
    }

    // The island is never quite silent: the Saint's ribs settle under the abbey, and somewhere far off a bell is rung.
    static float groanT = 40, farBellT = 70, breathT, heartT;
    static void AmbientOneShots(float dt)
    {
        // badly hurt, she hears herself breathing in the helmet
        breathT -= dt;
        if (player.hp <= 2 && !player.dead && !player.HeadUnder() && breathT <= 0) { breathT = player.hp <= 1 ? 1.6f : 2.5f; Snd.Breath(player.hp <= 1); }
        // and at the last pip, her heart, louder than the room
        heartT -= dt;
        if (player.hp <= 1 && !player.dead && heartT <= 0) { heartT = 0.78f; Snd.Heartbeat(true); }
        else if (player.hp == 2 && !player.dead && heartT <= 0) { heartT = 1.05f; Snd.Heartbeat(false); }
        if (room.Outdoor || player.inBellRoom) return;
        groanT -= dt; farBellT -= dt;
        if (groanT <= 0) { groanT = H.Rand(50, 110); Snd.SaintGroan(H.Rand(-0.7f, 0.7f)); }
        if (farBellT <= 0) { farBellT = H.Rand(80, 160); Snd.FarBell(H.Rand(-0.9f, 0.9f)); }
    }
}
