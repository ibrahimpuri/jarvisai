// The guide: where the story goes next, so nobody is left lost on the island.
//
// The main story is a chain of steps, each done by something the game already records (a room visited, a flag, an
// item, an ability). The current step is the first one not yet done, so steps taken out of order are simply skipped.
// It is shown three ways:
//   - on the HUD, under the tide dial: the step's title and where it is, and when it changes, a banner;
//   - over the world (drawn with the HUD, so it reads in the dark): a pointer to the way on (the opening into the next
//     room on the route), or, in the step's own room, a marker over the thing to find; at the screen's edge when out of view;
//   - in the journal and on the map: the whole of the step, how to do it, the route there in red ink.
// The route is found over the island's seams: every pair of rooms whose open tiles touch. It is built once, from the
// rooms themselves, so it never disagrees with the map. The guide can be turned off in the pause menu.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Guide
{
    public class Step
    {
        public string id, act, title, how, room, what;
        public Func<bool> done;
        public Func<string> now;    // a more pressing line for where she stands, when there is one
    }

    static bool Visited(string id) => World.StateOrNull(id)?.visited == true;
    static Props F => World.flags;
    static Player P => G.player;

    public static readonly List<Step> Steps = new()
    {
        // ---- Act I: the Wrackline ----
        new() { id = "ashore", act = "I", room = "pier", title = "Get ashore",
            how = "The Constance is holed through. Walk west across the reef to the Long Pier.",
            done = () => Visited("pier") },
        new() { id = "chapel", act = "I", room = "chapel", what = "npc:ansel", title = "Find the bell that is still ringing",
            how = "Climb the ladder at the landward end of the Long Pier up onto the Seawall, then go east into the Seawall Chapel. Someone is still ringing its bell. Talk to him.",
            done = () => G.quests.B("anselMet") },
        new() { id = "crank", act = "I", room = "tithe", what = "crank", title = "Fetch the Sluice Crank",
            how = "Brother Ansel says it lies on the altar of the Tithe Hall, below the Seawall. Go west along the Seawall through the Tithe Gallery and down its ladder, or in by the side door at the landward end of the Long Pier. Go at Low tide: the hall floods as the tide comes in. Sneak: the brothers in the pews are only sleeping.",
            done = () => P.bag.Count("crank") > 0 || F.B("seawallOpen") },
        new() { id = "gate", act = "I", room = "seawall", what = "socket", title = "Open the Seawall gate",
            how = "Fit the crank into the socket at the foot of the Seawall's tower.",
            now = () => G.room?.Id == "tithe" && G.titheFlood != null ? "The hall is flooding. Ride the water up to the walkway ladder by the east wall, climb to the Tithe Gallery, and go east along the Seawall." : null,
            done = () => F.B("seawallOpen") },
        new() { id = "climb", act = "I", room = "saltgate", title = "Climb to the Saltworks",
            how = "Up the ladder inside the Seawall's tower, through the gate the crank opened.",
            done = () => Visited("saltgate") },
        // ---- Act I: the Saltworks ----
        new() { id = "pans", act = "I", room = "kilns", title = "Cross the Evaporation Pans",
            how = "The brass plaque reads SEAWARD, THEN SAINT-WARD, THEN THE KILN. Throw the wave lever, then the skull, then the flame, then run east through the hatch before it shuts, and rise with the barge (or swim) as the pan floods. The way on to the Kilns is at the top of the pan, on the east side.",
            now = () => F.B("basinJam") && !F.B("basinCrossed") ? "Wrong order: the sluices are jammed. Wait for the Ebb (or rest at a bell), then throw them again: wave, skull, flame." : null,
            done = () => Visited("kilns") },
        new() { id = "abbot", act = "I", room = "abbot", title = "Face the Abbot of Salt",
            how = "The Cathedral of Salt is at the top of the salt mine. From the Kilns climb to the Upper Kilns, up the Furnace Stair to the Mine Head (its Bell Room is just east), and into the mine. The drift above the Pick Gallery has fallen in, so go the long way: east through the Brine Seep and the Crystal Drift, up the Winze, and back west past the Ivory Seam and the Pillar Hall to the Abbot's Stair. His salt plates only fall when he strikes his own furnaces: stand by a kiln and let him swing at you, then break what the plates were covering.",
            done = () => F.B("abbotDead") },
        // ---- Act II: the Cloister and the Choir ----
        new() { id = "boots", act = "II", room = "abbot", what = "ability:boots", title = "Take the Abbot's Ballast Boots",
            how = "They fell where he crumbled, in the Cathedral of Salt. With them on you sink, walk the seabed, and stand against the Ebb's current.",
            done = () => P.Has("boots") },
        new() { id = "cloister", act = "II", room = "cloister", title = "Go down into the Lower Cloister",
            how = "From Odile's Bell Room in the Saltworks, put the Ballast Boots on and sink down the Brine Shaft. (At Low tide the Tithe Hall's west door opens onto the Cloister too.)",
            done = () => Visited("cloister") || Visited("nave") || Visited("priors") },
        new() { id = "west", act = "II", room = "refectory", title = "Cross the Lower Cloister",
            how = "The Prior's Walk is at the far west end of the Lower Cloister. Go west from the Bell-Rope Nave into the Lavatorium, then along the lower floor (the Chapter House, the Slype, the Undercroft) or up the Night Stair and along the upper floor (the Dorter, the Library, the Counting House). The two floors meet at the Refectory. The lower floor floods at High tide.",
            done = () => Visited("refectory") || F.B("almonerDead") },
        new() { id = "almoner", act = "II", room = "refectory", title = "Break the Almoner",
            how = "He is chained by the neck to the Refectory's long table, and the doors west are chained to him. Break two of his limbs and the crown in his chest opens. Knees first and he can only crawl, but when the tide is in he swims after you. Elbows first and he cannot grab or throw, but he heaves the table: be off the floor and the table when his knots flare. His chain does not reach the sideboards by the walls.",
            done = () => F.B("almonerDead") },
        new() { id = "prior", act = "II", room = "priors", what = "ability:jet", title = "Open the Prior's door",
            how = "Go on west from the Refectory, past the Almonry (ring its bell), to the Prior's Walk at the far end of the Cloister. The hymnal says the door opens \"when the sea turns its back\": come at the Ebb. The Prior kept a Jet Valve behind it.",
            done = () => P.Has("jet") },
        new() { id = "choir", act = "II", room = "choir", what = "npc:hesper", title = "Climb to the Sunken Choir",
            how = "Up from the Prior's Walk to the Choir Steps (or west along the upper floor, through the Prior's Lodging), then west into the Sunken Choir. The Choir will not harm you while you keep its water. Hesper Vane leads them.",
            done = () => G.quests.B("hesperMet") || Visited("narthex") || P.Has("tidekey") },
        new() { id = "organ", act = "II", room = "organloft", title = "Find the Precentor",
            how = "West from the Choir Bell Room, through the Narthex and the Drowned Stalls (keep to the stall tops), the Song School and the Baptistery, where the crystal across the door shatters at the note of the bell over its basin: turn the valve until they match. On west past the Bone Choir and the Cantor's Vestry to the West Window, and up the bell tower to the organ loft.",
            done = () => Visited("organloft") || F.B("precentorDead") },
        new() { id = "precentor", act = "II", room = "organloft", title = "Silence the Precentor",
            how = "He hangs from the organ, out of reach. Shelter from his scream behind the two piers. Break his jaw while he draws breath, and the scream chokes. Or listen to the note the salt in his throat rings when he breathes in, set the loft's valve so its bell rings the same, and strike it: the salt shatters and the plates over his pipes fall away. Two broken limbs open the crown in his chest.",
            done = () => F.B("precentorDead") },
        new() { id = "tidekey", act = "II", room = "tuning", what = "ability:tidekey", title = "Take the Tide Key",
            how = "In the Tuning Chamber, through the organ loft's upper door, east of the gallery.",
            done = () => P.Has("tidekey") },
        new() { id = "ribvault", act = "II", room = "ribbell", title = "Rise into the Ribvault",
            how = "Go back east along the galleries (the Choir-Master's Rooms, the Pipe Gallery, the Bellows, the Triforium) and drop from the Night Gallery into the Narthex. In the Sunken Choir, turn the Tide Key in the sluice and raise the water to the vault, then swim up into the Saint's ribs and on to the Ribvault Bell Room.",
            done = () => Visited("ribbell") || P.Has("seal") },
        new() { id = "spine", act = "II", room = "spine", title = "Climb to the Spine",
            how = "Up the ladder from the Ribvault Bell Room into the Breastbone, west through the West Ribs (swing across the gap on the ring) to the Lung, up the Rib Ladder, east along the Collarbone and up onto the Vertebrae, then east to the Spine. Mind the wind.",
            done = () => Visited("spine") || F.B("motherDead") },
        new() { id = "mother", act = "II", room = "spine", title = "Bring down the Hanging Mother",
            how = "She hangs out of reach on six tendrils. Cut four and she falls onto the deck of ribs: break the knots along her spine while she is down, before she hauls herself back up on new tendrils. Two broken spine knots open the crown in her back. Every cut tendril drops one of her young.",
            done = () => F.B("motherDead") },
        new() { id = "seal", act = "II", room = "prisms", what = "ability:seal", title = "Take the Pressure Seal",
            how = "Down from the Spine: through the Sternum Bell (ring it), down its bell-rope well into the East Ribs, west through the Floating Ribs and down to the Lantern Lung, then west into the Lamp Prisms. Turn the salt prisms so a focused beam (hold focus) reaches the dead crystal in its niche, and the way to the Seal opens.",
            done = () => P.Has("seal") },
        // ---- Act III: the spring tide ----
        new() { id = "gullet", act = "III", room = "gulletmouth", title = "Dive the Gullet",
            how = "With the Pressure Seal you can dive deep water. Weight the lift in the Prior's Walk and go down into the Gullet Mouth below it. Its east wall is shut: go west through the Throat Gate and down the Long Throat. Breathe at the air vents and in the domes (the Drowned Bell, the Last Air).",
            done = () => Visited("gulletmouth") || F.B("undertowDead") },
        new() { id = "undertow", act = "III", room = "lair", title = "Kill the Undertow",
            how = "Its lair is east of the Gullet Mouth, shut from that side. Go round: down the Long Throat to the Stomach, east through the Pylorus and down to the Deep, east along the Road, up the Up-Current, west through the Brine Galleries and the Buoy Chapel, and into the lair from behind. Break the knots that hold it together.",
            done = () => F.B("undertowDead") },
        new() { id = "skull", act = "III", room = "skullstair", title = "Climb into the Lantern Skull",
            how = "The hatch into the Skull has opened, at the Skull Gate on top of the Ribvault: ride the bell-cable to the Sternum Bell, go back into the Spine and out by its upper door, through the Atlas, and climb the ladder under the hatch.",
            done = () => Visited("skullstair") },
        new() { id = "crown", act = "III", room = "crown", title = "Cross the Crown",
            how = "East from the Skull Stair, through the Sinus and the Keeper's Watch (ring its bell), and out onto the Crown, the top of the Skull, in the wind. Swing the breaks in the bone from the iron rings.",
            done = () => Visited("crown") || F.B("shoalDead") },
        new() { id = "shoal", act = "III", room = "brainpan", title = "Break the Shoal",
            how = "Something made of many drowned turns in the Brainpan, east across the Crown. Break it apart: its death lifts the bone shutter onto the lamp.",
            done = () => F.B("shoalDead") || Visited("lamp") },
        new() { id = "agathe", act = "III", room = "lamp", what = "npc:agathe", title = "Reach the lamp. Find your mother.",
            how = "The Lamp is east of the Brainpan, at the top of the Skull. Agathe is there. Her room, the Skull's second Bell Room, is beyond it.",
            done = () => G.quests.B("agatheMet") },
        new() { id = "decide", act = "III", room = "lamp", title = "Decide what a Keeper is for",
            how = "Light the great lamp, ring the Saint's bell, or fire the old red beacon. Agathe told you what each will cost.",
            now = () => Legend.HostReady ? "Or go to the window, where the sea is waiting for you." : null,
            done = () => G.ended != null },
    };

    public static Step Current => Steps.FirstOrDefault(s => !s.done());
    public static int Index(Step s) => Steps.IndexOf(s);
    public static string ActName(string act) => act switch { "I" => "Act I · The Wrackline and the Saltworks", "II" => "Act II · The Cloister and the Choir", _ => "Act III · The spring tide" };
    public static bool On => Settings.Guide;

    // ---- the island's seams ------------------------------------------------------------------------------
    static Dictionary<string, Dictionary<string, List<(int tx, int ty)>>> seams;
    static bool OpenTile(char ch) => World.Solidity(ch) != 1 || ch is 'L' or 'E' or 'K' or 'Z' or 'V' or 'Y' or 'h' or 'Q' or 'J' or 'U' or 'N' or 'O';
    static void BuildSeams()
    {
        seams = new();
        foreach (var r in Data.Rooms)
        {
            var links = seams[r.Id] = new();
            for (int ty = 0; ty < r.Th; ty++)
                for (int tx = 0; tx < r.Tw; tx++)
                {
                    if (tx != 0 && ty != 0 && tx != r.Tw - 1 && ty != r.Th - 1) continue;
                    int wx = r.Ox + tx, wy = r.Oy + ty;
                    if (!OpenTile(r.Map[ty][tx])) continue;
                    foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int nx = tx + dx, ny = ty + dy;
                        if (nx >= 0 && ny >= 0 && nx < r.Tw && ny < r.Th) continue;
                        var nr = World.RoomAtTile(wx + dx, wy + dy);
                        if (nr == null || nr == r) continue;
                        if (!OpenTile(nr.Map[wy + dy - nr.Oy][wx + dx - nr.Ox])) continue;
                        if (!links.TryGetValue(nr.Id, out var l)) links[nr.Id] = l = new();
                        l.Add((wx, wy));
                    }
                }
        }
    }
    public static IReadOnlyDictionary<string, List<(int tx, int ty)>> Links(string room) { if (seams == null) BuildSeams(); return seams[room]; }

    // The shortest way through the rooms from one to another (both ends included), or null.
    public static List<string> Route(string from, string to)
    {
        if (seams == null) BuildSeams();
        if (from == null || to == null || !seams.ContainsKey(from) || !seams.ContainsKey(to)) return null;
        var prev = new Dictionary<string, string> { [from] = null };
        var q = new Queue<string>(); q.Enqueue(from);
        while (q.Count > 0)
        {
            var a = q.Dequeue();
            if (a == to) break;
            foreach (var b in seams[a].Keys) if (!prev.ContainsKey(b)) { prev[b] = a; q.Enqueue(b); }
        }
        if (!prev.ContainsKey(to)) return null;
        var path = new List<string>();
        for (var c = to; c != null; c = prev[c]) path.Add(c);
        path.Reverse();
        return path;
    }

    // Where the thing to find is, in its room: a live entity if it is here, else where the room places it.
    public static Vector2? ThingPos(Step s)
    {
        var r = Data.Room.GetValueOrDefault(s.room);
        if (r == null) return null;
        if (s.what == null) return null;
        string type = s.what, id = null;
        int c = s.what.IndexOf(':'); if (c > 0) { type = s.what[..c]; id = s.what[(c + 1)..]; }
        bool Match(Props sp) => sp != null && sp.S("type") == type && (id == null || sp.S("id") == id);
        if (G.room == r)
            foreach (var e in G.ents)
                if (!e.gone && e.spec != null && Match(e.spec) && !(e is Obj o && o.done)) return new Vector2(e.Cx(), e.y);
        foreach (var sp in G.RoomSpecs(r)) if (Match(sp)) return new Vector2(sp.F("x"), sp.F("y") - 16);
        return null;
    }

    // Where to head now, in the room she is in: the thing itself, or the opening into the next room on the route.
    public static Vector2? Heading(out bool thing) => Heading(out thing, out _);
    public static Vector2? Heading(out bool thing, out Vector2 exit)
    {
        thing = false; exit = Vector2.Zero;
        var s = Current; var p = P; var r = G.room;
        if (s == null || p == null || r == null) return null;
        if (r.Id == s.room)
        {
            var at = ThingPos(s);
            thing = at != null;
            return at;
        }
        var route = Route(r.Id, s.room);
        if (route == null || route.Count < 2) return null;
        var seam = Links(r.Id)[route[1]];
        // the nearest tile of the opening
        (int tx, int ty) best = seam[0]; float bd = 1e9f;
        foreach (var t in seam)
        {
            float d = H.Dist(p.Cx(), p.Cy(), t.tx * H.TILE + 8, t.ty * H.TILE + 8);
            if (d < bd) { bd = d; best = t; }
        }
        exit = best.tx == r.Ox ? new Vector2(-1, 0) : best.tx == r.Ox + r.Tw - 1 ? new Vector2(1, 0) : best.ty == r.Oy ? new Vector2(0, -1) : new Vector2(0, 1);
        // a ladder through the floor or the ceiling: point at the end of it she can reach, not the room's edge
        int lx = best.tx, ly = best.ty;
        if (World.IsLadder(lx, ly) && (ly == r.Oy || ly == r.Oy + r.Th - 1))
        {
            int step = ly == r.Oy ? 1 : -1;
            while (World.IsLadder(lx, ly + step) && ly + step > r.Oy && ly + step < r.Oy + r.Th - 1) ly += step;
            if (step < 0) ly -= 1;   // stand on the top rung
        }
        return new Vector2(lx * H.TILE + 8, ly * H.TILE + 8);
    }

    public static string WhereLine(Step s)
    {
        var r = Data.Room.GetValueOrDefault(s.room);
        if (r == null) return "";
        if (G.room == r) return "here, in " + r.Name;
        var route = Route(G.room?.Id, s.room);
        string region = Data.Regions.P(r.Region)?.S("name") ?? "";
        string by = route != null && route.Count > 2 ? $" · {route.Count - 1} rooms away" : route != null && route.Count == 2 ? " · next room" : "";
        return r.Name + (region.Length > 0 && region != r.Name ? ", " + region : "") + by;
    }

    // ---- what else there is to do ---------------------------------------------------------------------------
    public static List<(string title, string detail)> Also()
    {
        var o = new List<(string, string)>();
        var q = G.quests; var f = F; var p = P;
        if (!Hollow.owned && Visited("pier") && !f.B("hollowGift")) o.Add(("Something on the Long Pier", "The first time the tide turns while you stand on the Long Pier, the sea will bring you something."));
        else if (!Hollow.owned && f.B("hollowGift")) o.Add(("The Mere's Hollow", "The sea left something on the Long Pier's boards. Take it up."));
        if (q.S("teodor") == "asked") o.Add(("Odile's partner, Teodor", "He went down into the Lower Cloister and did not come up. Bring Odile his brass tags, or break every limb knot but his Crown and bind him (interact) for her."));
        if (q.S("teodor") == "bound") o.Add(("Teodor, bound", "Take him back to Odile in the Saltworks Bell Room."));
        if (q.S("teodor") == "killed" && p.bag.Count("tags") > 0) o.Add(("Teodor's tags", "Give them to Odile in the Saltworks Bell Room."));
        if (G.tables.Count > 0 && q.S("tables") != "given" && q.S("tables") != "sold") o.Add(($"The Prior's tide tables ({G.tables.Count}/4)", "Ansel in the Seawall Chapel wants all four. So does the Choir's bowl, and it pays differently."));
        if (G.act >= 3 && !Legend.glassOwned) o.Add(("A lantern that is not yours", "Agathe left something for you in the Ribvault Bell Room."));
        if (G.act >= 3 && Hollow.owned && !Legend.merged && !f.B("tineGrown") && Visited("gulletmouth")) o.Add(("The Saint's Ear", "A hollow in the roof of the Gullet Mouth's dome runs with brine. It is the shape of the Hollow."));
        if (Legend.merged) o.Add(("The Tine in your hand", Legend.Rooted ? "It has rooted. A bench can still prise it out, and it will tear you." : "A bench can prise the Hollow free again."));
        return o;
    }

    // ---- every frame: a new objective is announced ----------------------------------------------------------
    static string shownId;
    public static float bannerT;
    public static string bannerTitle;
    public static void Reset() { shownId = null; bannerT = 0; }
    public static void Update(float dt)
    {
        if (bannerT > 0) bannerT -= dt;
        var s = Current;
        string id = s?.id;
        if (id == shownId) return;
        bool first = shownId == null;
        shownId = id;
        if (s == null || first) return;   // loading a game does not announce what was already known
        bannerTitle = s.title; bannerT = 5;
        if (On) Snd.Bell(392, 0.12f);
    }

    // ---- drawing --------------------------------------------------------------------------------------------
    static readonly Color INK = new(0.96f, 0.86f, 0.6f);
    // under the tide dial: the objective and where it is
    public static void DrawHud(Canvas c)
    {
        if (!On || G.mode != "play") return;
        var s = Current;
        if (s == null) return;
        float x = H.VIEW_W - 10, y = 70;
        string title = s.title, where = s.now?.Invoke() != null ? "see the journal" : WhereLine(s);
        c.Font = "7px caps"; c.TextAlign = 2;
        float tw = c.MeasureText(title);
        c.Font = "italic 6px Georgia, serif"; float lw = c.MeasureText(s.now?.Invoke() ?? where); c.Font = "7px caps";
        float bw = MathF.Max(tw, MathF.Min(lw, 260)) + 16;
        c.Glow(x - bw / 2 + 4, y + 2, bw / 2 + 18, 14, new Color(0, 0, 0, 0.7f));
        c.Fill = new Color(0, 0, 0, 0.5f); c.FillRect(x - bw, y - 8, bw + 6, 20);
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillText(title, x + 0.6f, y + 0.6f);
        c.Fill = INK; c.FillText(title, x, y);
        // a diamond before it
        c.Save(); c.Translate(x - tw - 6, y - 2.5f); c.Rotate(H.PI / 4); c.Fill = INK; c.FillRect(-1.6f, -1.6f, 3.2f, 3.2f); c.Restore();
        c.Font = "italic 6px Georgia, serif"; c.Fill = new Color(0.82f, 0.76f, 0.62f, 0.85f);
        string line = s.now?.Invoke() ?? where;
        if (line.Length > 70) line = line[..67] + "…";
        c.FillText(line, x, y + 9);
        c.TextAlign = 0;
        // a new objective: a banner across the top of the view
        if (bannerT > 0 && bannerTitle != null)
        {
            float a = MathF.Min(1, MathF.Min(bannerT, 5 - bannerT) * 2);
            float cx = H.VIEW_W / 2f, by = 118;
            c.Glow(cx, by, 150, 20, new Color(0, 0, 0, 0.55f * a));
            c.TextAlign = 1;
            c.Font = "6px caps"; c.Fill = new Color(0.75f, 0.68f, 0.52f, a); c.FillText("new objective", cx, by - 7);
            c.Font = "10px Georgia, serif"; c.Fill = new Color(0.98f, 0.9f, 0.68f, a); c.FillText(bannerTitle, cx, by + 6);
            c.TextAlign = 0;
        }
        // the pointer: at the edge of the view when the way on is out of sight
        var h = Heading(out bool thing, out Vector2 exit);
        if (h == null) return;
        var sp = G.WorldToScreen(h.Value.X, h.Value.Y);
        float m = 2;
        bool inside = sp.X > m && sp.X < H.VIEW_W - m && sp.Y > m && sp.Y < H.VIEW_H - m;
        if (inside) { Marker(c, sp, thing, exit); return; }
        // kept clear of the vitals, the tracker and the weapon plate
        float l = 24, rgt = H.VIEW_W - 24, tp = 96, bt = H.VIEW_H - 64;
        var ctr = new Vector2((l + rgt) / 2, (tp + bt) / 2);
        var d = sp - ctr; float ang = MathF.Atan2(d.Y, d.X);
        float kx = (rgt - l) / 2 / MathF.Max(0.001f, MathF.Abs(d.X)), ky = (bt - tp) / 2 / MathF.Max(0.001f, MathF.Abs(d.Y));
        var at = ctr + d * MathF.Min(kx, ky);
        float pulse = 0.65f + 0.35f * MathF.Sin(G.Now * 3);
        c.Save(); c.Translate(at.X, at.Y); c.Rotate(ang);
        c.Fill = new Color(0, 0, 0, 0.5f); c.Poly(new[] { new Vector2(7, 0), new Vector2(-4, -5.5f), new Vector2(-1.5f, 0), new Vector2(-4, 5.5f) });
        c.Fill = new Color(INK.R, INK.G, INK.B, 0.85f * pulse); c.Poly(new[] { new Vector2(6, 0), new Vector2(-3.5f, -4.5f), new Vector2(-1, 0), new Vector2(-3.5f, 4.5f) });
        c.Restore();
    }

    // In view: a marker over the thing to find, or chevrons at the opening that leads on. Drawn with the HUD, unlit,
    // so it reads in the dark.
    static void Marker(Canvas c, Vector2 at, bool thing, Vector2 exit)
    {
        float bob = MathF.Sin(G.Now * 2.6f) * 1.5f;
        float a = 0.7f + 0.3f * MathF.Sin(G.Now * 3);
        if (thing)
        {
            float y = at.Y - 14 + bob;
            c.Glow(at.X, y, 10, new Color(1.1f, 0.85f, 0.45f, 0.3f * a));
            c.Save(); c.Translate(at.X, y); c.Rotate(H.PI / 4);
            c.Fill = new Color(0, 0, 0, 0.6f); c.FillRect(-2.8f, -2.8f, 5.6f, 5.6f);
            c.Fill = new Color(INK.R, INK.G, INK.B, a); c.FillRect(-2, -2, 4, 4);
            c.Restore();
            c.Fill = new Color(INK.R, INK.G, INK.B, 0.75f * a); c.Poly(new[] { new Vector2(at.X - 2, y + 5), new Vector2(at.X + 2, y + 5), new Vector2(at.X, y + 8) });
            return;
        }
        // the way on: a pair of chevrons at the opening, pointing out of the room
        var dir = exit == Vector2.Zero ? new Vector2(1, 0) : exit;
        var p0 = at - dir * (12 - bob);
        float ang = MathF.Atan2(dir.Y, dir.X);
        for (int i = 0; i < 2; i++)
        {
            var q = p0 + dir * (i * 5.5f);
            c.Save(); c.Translate(q.X, q.Y); c.Rotate(ang);
            c.Fill = new Color(0, 0, 0, 0.45f); c.Poly(new[] { new Vector2(4, 0), new Vector2(-2.6f, -4.4f), new Vector2(-0.6f, 0), new Vector2(-2.6f, 4.4f) });
            c.Fill = new Color(INK.R, INK.G, INK.B, (0.55f + 0.3f * i) * a); c.Poly(new[] { new Vector2(3.2f, 0), new Vector2(-2, -3.6f), new Vector2(-0.4f, 0), new Vector2(-2, 3.6f) });
            c.Restore();
        }
    }
}
