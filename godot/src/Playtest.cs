// The playtest sweep: plays every room of the island the way a player would and reports what goes wrong.
// Run with  --test pt_doors pt_soak pt_objects pt_bells  (each takes minutes; they are not in the default run).
//   pt_doors    walks, swims and climbs through every open door with real inputs, jumping when it is blocked
//   pt_soak     leaves the drowned to hunt her in every room, then looks for bodies in walls, under floors or NaN
//   pt_objects  stands at every object and uses it, and looks for ones she cannot stand at
//   pt_bells    lights and rests at every Bell Room, saves, reloads, and checks she wakes where she rested
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Hollowmere;

public static class Playtest
{
    public static readonly HashSet<string> Slow = new() { "pt_doors", "pt_soak", "pt_objects", "pt_bells", "pt_debug", "pt_menus", "pt_route" };

    // late in the game, every gate open and every tool in hand
    static void LateGame()
    {
        G.god = true; G.act = 3;
        var f = World.flags;
        foreach (var fl in new[] { "seawallOpen", "abbotDead", "almonerDead", "priorWedged", "precentorDead", "motherDead", "liftDropped", "undertowDead", "skullOpen", "shoalDead", "basinFilling", "basinCrossed" }) f.Set(fl, true);
        foreach (var a in new[] { "swing", "boots", "jet", "tidekey", "seal", "lens" }) G.player.abilities.Add(a);
        G.quests.Set("anselMet", true); G.quests.Set("hesperMet", true);
        World.MarkDirty();
    }

    static void Reset(Tester t)
    {
        var p = G.player;
        t.Release("left", "right", "up", "down", "jump", "sneak", "dash", "interact", "focus", "fire");
        p.vx = p.vy = 0; p.state = "normal"; p.grabbedBy = null; p.dead = false; p.hp = p.maxHp; p.breath = 30; p.swing = null;
        if (p.dragging != null) p.StopDrag();
        UI.Close();
    }
    static void Clear() { foreach (var b in G.ents.OfType<Body>()) b.gone = true; G.bossBar = null; }

    static bool Open(int wx, int wy) => World.Solid(wx, wy) != 1;
    static bool Floor(int wx, int wy) => World.Solid(wx, wy) != 0 || World.IsLadder(wx, wy);
    // does her body overlap solid rock?
    static bool Embedded(Mover e)
    {
        for (float yy = e.y + 2; yy <= e.y + e.h - 2; yy += 6)
            for (float xx = e.x + 2; xx <= e.x + e.w - 2; xx += 4)
                if (World.SolidPx(xx, yy)) return true;
        return false;
    }

    // Hold a direction toward a door, hopping when blocked, until she is in another room.
    static async Task<RoomDef> Walk(Tester t, string dir, float secs, bool swim = false)
    {
        var p = G.player; var start = G.room;
        t.Down(dir);
        float lastX = p.x, lastY = p.y;
        for (float el = 0; el < secs; el += 0.2f)
        {
            await t.Run(0.2f);
            if (G.room != start) break;
            bool stuck = MathF.Abs(p.x - lastX) < 2 && MathF.Abs(p.y - lastY) < 2;
            if (stuck && (dir == "left" || dir == "right"))
            {
                // as a player would: a hop on land, a stroke up through the water
                if (swim || p.InWater()) { t.Down("up"); await t.Run(0.4f); t.Release("up"); }
                else await t.Tap("jump");
            }
            lastX = p.x; lastY = p.y;
        }
        t.Release(dir);
        return G.room;
    }

    static List<(int a, int b)> Segments(Func<int, bool> open, int n)
    {
        var o = new List<(int, int)>();
        for (int i = 0; i < n; i++)
        {
            if (!open(i)) continue;
            int j = i; while (j + 1 < n && open(j + 1)) j++;
            o.Add((i, j)); i = j;
        }
        return o;
    }

    static async Task Doors(Tester t)
    {
        LateGame();
        var bad = new List<string>(); int tried = 0, passed = 0, skipped = 0, gated = 0;
        foreach (var r in Data.Rooms)
        {
            foreach (var phase in r.Map.Any(row => row.Contains('E')) ? new[] { "low", "ebbing" } : new[] { "low" })
            {
                Tide.SetPhase(phase, 5);
                // ---- the side doors
                foreach (bool west in new[] { true, false })
                {
                    int ex = west ? 0 : r.Tw - 1, step = west ? -1 : 1;
                    foreach (var (y0, y1) in Segments(y => Open(r.Ox + ex, r.Oy + y), r.Th))
                    {
                        var nb = World.RoomAtTile(r.Ox + ex + step, r.Oy + y0);
                        if (nb == null || nb == r) continue;
                        // a gate on the far side that is shut for now (the Choir's, until it is drained)
                        if (!Enumerable.Range(y0, y1 - y0 + 1).Any(y => Open(r.Ox + ex + step, r.Oy + y))) { gated++; continue; }
                        bool water = World.IsWater(r.Px + ex * 16 + 8, r.Py + ((y0 + y1) / 2) * 16 + 8);
                        (int x, int y) spot = (-1, -1);
                        foreach (int dx in new[] { 2, 3, 4, 5, 1 })
                        {
                            int sx = west ? dx : r.Tw - 1 - dx;
                            for (int sy = Math.Min(r.Th - 2, y1 + 3); sy >= Math.Max(1, y0 - 1) && spot.x < 0; sy--)
                                if (Open(r.Ox + sx, r.Oy + sy) && Open(r.Ox + sx, r.Oy + sy - 1) && (water || Floor(r.Ox + sx, r.Oy + sy + 1))) spot = (sx, sy);
                            if (spot.x >= 0) break;
                        }
                        if (spot.x < 0) { skipped++; continue; }
                        tried++;
                        Reset(t);
                        G.EnterRoom(r); t.Place(r.Id, spot.x, spot.y); Clear(); await t.Run(0.1f);
                        var got = await Walk(t, west ? "left" : "right", 4.5f, water);
                        string where = $"{r.Id} {(west ? "W" : "E")} rows {y0}-{y1} [{phase}] -> {nb.Id}";
                        if (got != nb) { bad.Add($"STUCK {where}: still in {got?.Id} at {t.State()}"); continue; }
                        await t.Run(0.5f);
                        if (Embedded(G.player)) bad.Add($"WALL {where}: in rock after the door at {t.State()}");
                        else passed++;
                    }
                }
                // ---- the floor and roof
                foreach (bool top in new[] { true, false })
                {
                    int ey = top ? 0 : r.Th - 1, step = top ? -1 : 1;
                    foreach (var (x0, x1) in Segments(x => Open(r.Ox + x, r.Oy + ey), r.Tw))
                    {
                        var nb = World.RoomAtTile(r.Ox + x0, r.Oy + ey + step);
                        if (nb == null || nb == r) continue;
                        int lx = -1;
                        for (int x = x0; x <= x1; x++) if (World.IsLadder(r.Ox + x, r.Oy + ey)) { lx = x; break; }
                        int cx = lx >= 0 ? lx : (x0 + x1) / 2;
                        bool water = World.IsWater(r.Px + cx * 16 + 8, r.Py + ey * 16 + 8);
                        if (top && lx < 0 && !water) { skipped++; continue; }   // a hole in the roof, reached by jumping
                        tried++;
                        Reset(t);
                        G.EnterRoom(r);
                        int sy = top ? 3 : r.Th - 4;
                        if (!top && lx < 0) sy = r.Th - 3;
                        t.Place(r.Id, cx, sy); Clear();
                        G.player.x = r.Px + cx * 16 + 8 - G.player.w / 2f;
                        await t.Run(0.05f);
                        string where = $"{r.Id} {(top ? "roof" : "floor")} cols {x0}-{x1}{(lx >= 0 ? " ladder" : water ? " water" : " drop")} -> {nb.Id}";
                        var got = await Walk(t, top ? "up" : "down", 6f, water);
                        if (got != nb) { bad.Add($"STUCK {where}: still in {got?.Id} at {t.State()}"); continue; }
                        await t.Run(0.5f);
                        if (Embedded(G.player)) bad.Add($"WALL {where}: in rock after the door at {t.State()}");
                        else passed++;
                    }
                }
            }
        }
        foreach (var b in bad) GD.Print("  " + b);
        t.Ok("every door can be walked, swum or climbed through", bad.Count == 0, $"{passed} of {tried} through, {bad.Count} problems, {skipped} reached only by jumping, {gated} gated shut");
    }

    static async Task Soak(Tester t)
    {
        LateGame();
        var bad = new List<string>(); int bodies = 0;
        foreach (var r in Data.Rooms)
        {
            Reset(t);
            Tide.SetPhase("high", 20);
            G.EnterRoom(r);
            var spot = Spot(r);
            t.Place(r.Id, spot.x, spot.y);
            await t.Run(6f);
            foreach (var b in G.ents.OfType<Body>().Where(b => !b.gone && b.room == r))
            {
                bodies++;
                string id = $"{r.Id} {b.type} ({b.key})";
                if (!float.IsFinite(b.x) || !float.IsFinite(b.y) || !float.IsFinite(b.vx) || !float.IsFinite(b.vy)) { bad.Add($"NAN {id}"); continue; }
                if (b.Cx() < r.Px - 8 || b.Cx() > r.Px + r.Pw + 8 || b.Cy() < r.Py - 40 || b.y > r.Py + r.Ph + 8) bad.Add($"OUT {id} at {(b.Cx() - r.Px) / 16:0},{(b.Cy() - r.Py) / 16:0}");
                else if (b.type is not ("gullet" or "gulletpair" or "undertow" or "shoal" or "driftling") && Embedded(b) && World.SolidPx(b.Cx(), b.Cy())) bad.Add($"WALL {id} at {(b.Cx() - r.Px) / 16:0},{(b.Cy() - r.Py) / 16:0}");
            }
            if (Embedded(G.player)) bad.Add($"PLAYER {r.Id} pushed into rock at {t.State()}");
        }
        foreach (var b in bad) GD.Print("  " + b);
        t.Ok("the drowned stay out of walls and inside their rooms", bad.Count == 0, $"{bodies} bodies in {Data.Rooms.Count} rooms, {bad.Count} problems");
    }

    static (int x, int y) Spot(RoomDef r)
    {
        var spots = new List<(int, int)>();
        for (int ty = r.Th - 2; ty > 1; ty--) for (int tx = 2; tx < r.Tw - 2; tx++)
            {
                int wx = r.Ox + tx, wy = r.Oy + ty;
                if (World.Solid(wx, wy + 1) != 0 && World.Solid(wx, wy) == 0 && World.Solid(wx, wy - 1) == 0 && World.CharAt(wx, wy) != 'x' && World.CharAt(wx, wy + 1) != 'x') spots.Add((tx, ty));
            }
        return spots.OrderBy(a => Math.Abs(a.Item1 - r.Tw / 2)).FirstOrDefault((r.Tw / 2, r.Th / 2));
    }

    static readonly HashSet<string> ENDS = new() { "lampwork", "beacon", "hymnbell", "boat", "window", "start", "musicbox" };
    static async Task Objects(Tester t)
    {
        LateGame();
        var bad = new List<string>(); int used = 0;
        foreach (var r in Data.Rooms)
        {
            Reset(t);
            Tide.SetPhase("low", 5);
            G.EnterRoom(r); Clear();
            var objs = G.ents.OfType<Obj>().Where(o => !o.gone && o.Prompt() != null && !ENDS.Contains(o.type ?? "")).ToList();
            foreach (var o in objs)
            {
                if (o.gone || !G.ents.Contains(o)) continue;
                Reset(t);
                var p = G.player;
                string id = $"{r.Id} {o.type} ({o.spec.S("id") ?? o.key}) at {(o.Cx() - r.Px) / 16:0},{(o.y + o.h - r.Py) / 16 - 1:0}";
                // somewhere to stand within reach of it, as a player would: beside it, under it, or swimming at it
                int ox = H.FloorI(o.Cx() / 16), oy = H.FloorI((o.y + o.h - 3) / 16);
                bool reached = false;
                var cands = new List<(int x, int y)>();
                for (int dy = -1; dy <= 3; dy++) for (int dx = 0; dx <= 2; dx++) foreach (int sx in dx == 0 ? new[] { 0 } : new[] { -dx, dx }) cands.Add((ox + sx, oy + dy));
                foreach (var (cx, cy) in cands)
                {
                    bool wet = World.IsWater(cx * 16 + 8, cy * 16 + 8);
                    if (!Open(cx, cy) || !Open(cx, cy - 1) || !(wet || Floor(cx, cy + 1))) continue;
                    p.x = cx * 16 + 8 - p.w / 2f; p.y = (cy + 1) * 16 - p.h; p.vx = p.vy = 0; p.state = "normal";
                    if (Embedded(p)) continue;
                    await t.Run(0.12f);
                    if (G.promptText != null) { reached = true; break; }
                }
                if (!reached) { bad.Add($"REACH {id}: nowhere to stand within reach of its prompt"); continue; }
                p.bag.Add("oil", 1);
                await t.Tap("interact"); await t.Run(0.25f);
                for (int i = 0; i < 6 && UI.IsOpen(); i++) { UI.Close(); await t.Run(0.05f); }
                used++;
                if (G.room != r) { bad.Add($"MOVED {id}: using it took her to {G.room?.Id}"); G.EnterRoom(r); Clear(); }
            }
        }
        foreach (var b in bad) GD.Print("  " + b);
        t.Ok("every object can be stood at and used", bad.Count == 0, $"{used} used, {bad.Count} problems");
    }

    static async Task Bells(Tester t)
    {
        LateGame();
        var bad = new List<string>(); int n = 0;
        foreach (var r in Data.Rooms.Where(r => r.BellRoom))
        {
            Reset(t);
            G.EnterRoom(r); Clear();
            var bell = G.ents.OfType<Bell>().FirstOrDefault();
            if (bell == null) { bad.Add($"NOBELL {r.Id}"); continue; }
            G.bellsLit.Add(bell.spec.S("id"));
            var p = G.player;
            p.x = bell.Cx() - p.w / 2; p.y = bell.y + bell.h - p.h;
            await t.Run(0.1f);
            await t.Tap("interact"); await t.Run(0.2f);
            if (!UI.IsOpen() || UI.Kind != "bell") { bad.Add($"NOMENU {r.Id}: the lit bell opened {UI.Kind}"); continue; }
            if (!await t.ClickButton("Save")) { bad.Add($"NOSAVE {r.Id}: no Save on the bell's menu"); continue; }
            await t.Run(0.1f); UI.Close(); await t.Run(0.1f);
            if (G.respawn?.room != r.Id) { bad.Add($"RESPAWN {r.Id}: resting did not set the respawn ({G.respawn?.room})"); continue; }
            G.Save();
            float sx = G.respawn.x, sy = G.respawn.y;
            G.EnterRoom(Data.Room["pier"], true);
            G.LoadGame(); UI.Close(); await t.Run(0.3f);
            n++;
            if (G.room?.Id != r.Id) bad.Add($"LOAD {r.Id}: woke in {G.room?.Id}");
            else if (MathF.Abs(p.x - sx) > 2 && MathF.Abs(G.player.x - sx) > 2) bad.Add($"LOAD {r.Id}: woke at {G.player.x - r.Px:0},{G.player.y - r.Py:0} not {sx - r.Px:0},{sy - r.Py:0}");
            else if (Embedded(G.player)) bad.Add($"LOAD {r.Id}: woke inside rock");
            else if (!G.player.onGround && !G.player.HeadUnder()) bad.Add($"LOAD {r.Id}: woke in mid-air");
            LateGame();
        }
        foreach (var b in bad) GD.Print("  " + b);
        t.Ok("every Bell Room rests, saves and wakes her where she rested", bad.Count == 0, $"{n} Bell Rooms, {bad.Count} problems");
    }

    // a door played step by step, with her state printed every fifth of a second
    static async Task Trace(Tester t, string room, int tx, int ty, string dir, float secs, bool hop)
    {
        LateGame(); Tide.SetPhase("low", 5);
        Reset(t); G.EnterRoom(Data.Room[room]); t.Place(room, tx, ty); Clear(); await t.Run(0.1f);
        var p = G.player;
        GD.Print($"TRACE {room} {tx},{ty} {dir}");
        t.Down(dir);
        float lx = p.x, ly = p.y;
        for (float el = 0; el < secs; el += 0.2f)
        {
            await t.Run(0.2f);
            var s = t.State();
            GD.Print($"  {el:0.0}s {s} water={p.InWater()} head={p.HeadUnder()} vx={p.vx:0} vy={p.vy:0}");
            if (hop && MathF.Abs(p.x - lx) < 2 && MathF.Abs(p.y - ly) < 2) await t.Tap("jump");
            lx = p.x; ly = p.y;
            if (G.room.Id != room) break;
        }
        t.Release(dir);
    }
    static async Task Debug(Tester t)
    {
        // how far is each boss from the nearest Bell Room, the way back after a death
        foreach (var boss in new[] { "abbot", "refectory", "organloft", "spine", "lair", "brainpan" })
        {
            var best = Data.Rooms.Where(b => b.BellRoom).Select(b => (b.Id, Guide.Route(b.Id, boss)?.Count ?? 999)).OrderBy(x => x.Item2).Take(3);
            GD.Print($"BOSS {boss}: " + string.Join(", ", best.Select(x => $"{x.Id} {x.Item2 - 1} rooms")));
        }
        await Trace(t, "hithe", 2, 18, "left", 3, true);
        await Trace(t, "baptistery", 39, 21, "down", 3, false);
    }

    // Act III's breach of the Cloister's Bell Room: drowned out until its two drowned are down, then a Bell Room again.
    static async Task BellBreach(Tester t)
    {
        G.god = true; G.act = 3;
        var f = World.flags; var p = G.player;
        G.bellsLit.Add("bell_cloister"); p.bag.Add("oil", 3);
        t.Place("cloisterbell", 10, 16); await t.Run(0.3f);
        var breach = G.ents.OfType<Body>().Where(b => b.key.StartsWith("breach")).ToList();
        t.Ok("the spring tide breaches the Cloister's Bell Room", f.B("cloisterBreached") && breach.Count == 2 && G.BellRoomBreached("cloisterbell"));
        var bell = G.ents.OfType<Bell>().First();
        p.x = bell.Cx() - p.w / 2; p.y = bell.y + bell.h - p.h; await t.Run(0.1f);
        await t.Tap("interact"); await t.Run(0.2f);
        t.Ok("its bell is drowned out", UI.Kind == "dialogue", UI.Kind);
        UI.Close();
        t.Place("cloister", 20, 16); await t.Run(0.2f);
        t.Place("cloisterbell", 10, 16); await t.Run(0.2f);
        breach = G.ents.OfType<Body>().Where(b => b.key.StartsWith("breach")).ToList();
        t.Ok("leaving does not lose them: they are waiting when she comes back", breach.Count == 2);
        foreach (var b in breach) foreach (var k in b.knots.ToList()) b.DamageKnot(k, 99, "rivet");
        await t.Run(0.5f);
        t.Ok("with both down, the room is cleared", f.B("cloisterCleared") && !G.BellRoomBreached("cloisterbell"));
        bell = G.ents.OfType<Bell>().First();
        p.x = bell.Cx() - p.w / 2; p.y = bell.y + bell.h - p.h; await t.Run(0.1f);
        await t.Tap("interact"); await t.Run(0.2f);
        t.Ok("and its bell rings for her again", UI.Kind == "bell", UI.Kind);
        UI.Close();
        // the other breach, at the Saltworks, leaves the Cloister's bell alone
        f.Set("cloisterBreached", false); f.Set("cloisterCleared", false); f.Set("breachDone", true);
        t.Ok("after the Saltworks breach the Cloister's bell is not drowned out", !G.BellRoomBreached("cloisterbell"));
    }

    // Killed by a boss in its hall, she can try again from the door instead of walking back from the bell.
    static async Task BossRetry(Tester t)
    {
        var f = World.flags; var p = G.player;
        foreach (var fl in new[] { "seawallOpen", "abbotDead", "almonerDead", "priorWedged", "precentorDead", "motherDead", "liftDropped" }) f.Set(fl, true);
        foreach (var a in new[] { "boots", "jet", "tidekey", "seal" }) p.abilities.Add(a);
        G.act = 3;
        G.respawn = new G.Spot { room = "drownedbell", x = Data.Room["drownedbell"].Px + 200, y = Data.Room["drownedbell"].Py + 16 * 16 };
        // in from the Buoy Chapel, swimming
        G.god = true;
        t.Place("buoychapel", 3, 13); Clear(); await t.Run(0.1f);
        t.Down("left");
        for (int i = 0; i < 30 && G.room.Id != "lair"; i++) await t.Run(0.1f);
        t.Release("left");
        t.Ok("she swims into the lair", G.room.Id == "lair" && G.doorSpot?.room == "lair", G.room.Id);
        var u = G.ents.OfType<Undertow>().FirstOrDefault(e => e.boss);
        if (u == null) { t.Ok("the Undertow is home", false); return; }
        u.DamageKnot(u.K("buoy"), 99, "rivet");
        await t.Run(0.3f);
        var lair = Data.Room["lair"];
        p.x = lair.Px + 40 * 16; p.y = lair.Py + 12 * 16; await t.Run(0.1f);
        G.god = false; p.invulnT = 0; p.hp = 1; p.Hurt(5);
        for (int i = 0; i < 30 && UI.Kind != "death"; i++) await t.Run(0.1f);
        t.Ok("killed in its hall, the death screen offers the door", UI.Kind == "death" && UI.Inst.AllText().Contains("Try again from the door"), UI.Kind);
        t.Snap("retry_death");
        await t.ClickButton("Try again from the door"); await t.Run(0.4f);
        var lr = Data.Room["lair"];
        u = G.ents.OfType<Undertow>().FirstOrDefault(e => e.boss);
        t.Ok("she wakes inside the door she came in by", G.room.Id == "lair" && !p.dead && p.hp == p.maxHp && p.Cx() > lr.Px + 60 * 16, $"{G.room.Id} {(p.Cx() - lr.Px) / 16:0}");
        t.Ok("and the Undertow is whole again", u != null && !u.K("buoy").broken && G.bossBar == u);
        t.Ok("her satchel lies where she fell", G.satchelDrop?.room == "lair");
        // anywhere else, only the bell
        G.god = true; t.Place("road", 20, 15); await t.Run(0.2f); Clear();
        G.god = false; p.invulnT = 0; p.hp = 1; p.Hurt(5);
        for (int i = 0; i < 30 && UI.Kind != "death"; i++) await t.Run(0.1f);
        t.Ok("killed by anything else, she wakes at the bell", UI.Kind == "death" && !UI.Inst.AllText().Contains("Try again from the door"));
        await t.ClickButton("Wake at the bell"); await t.Run(0.3f);
        t.Ok("at the bell", G.room.Id == "drownedbell", G.room.Id);
        G.god = true;
    }

    // From Act II every lit Bell Room answers every other, across the island.
    static async Task Cables(Tester t)
    {
        G.god = true; var p = G.player;
        foreach (var b in new[] { "bell_chapel", "bell_salt", "bell_cloister", "bell_drowned" }) G.bellsLit.Add(b);
        t.Place("chapel", 10, 16); Clear(); await t.Run(0.2f);
        var bell = G.ents.OfType<Bell>().First();
        p.x = bell.Cx() - p.w / 2; p.y = bell.y + bell.h - p.h; await t.Run(0.1f);
        await t.Tap("interact"); await t.Run(0.2f);
        t.Ok("in Act I there are no cables yet", UI.Kind == "bell" && !UI.Inst.AllText().Contains("Ride the"), UI.Kind);
        UI.Close();
        G.act = 2;
        await t.Tap("interact"); await t.Run(0.2f);
        t.Ok("from Act II the bell offers the cables to other regions", UI.Inst.AllText().Contains("Ride the cables to another region"));
        await t.ClickButton("Ride the cables to another region"); await t.Run(0.2f);
        string all = UI.Inst.AllText();
        t.Ok("which list every lit Bell Room, region by region", UI.Kind == "cable" && all.Contains("Saltworks") && all.Contains("The Gullet") && all.Contains("Drowned Bell") && !all.Contains("Ferry"), all.Length > 200 ? all[..200] : all);
        t.Snap("cables_menu");
        await t.ClickButton("The Drowned Bell"); await t.Run(0.3f);
        t.Ok("and she rides to one in another region", G.room.Id == "drownedbell" && !UI.IsOpen(), G.room.Id);
    }

    // the menus and the chart late in a game, for looking at (run rendered, with --out)
    static async Task Menus(Tester t)
    {
        LateGame();
        foreach (var r in Data.Rooms) World.State(r.Id).visited = true;
        foreach (var r in Data.Rooms.Where(r => r.BellRoom)) foreach (var sp in G.RoomSpecs(r)) if (sp.S("type") == "bell") G.bellsLit.Add(sp.S("id"));
        foreach (var (room, tx, ty) in new[] { ("pier", 20, 16), ("deep", 60, 18), ("brainpan", 20, 16) })
        {
            t.Place(room, tx, ty); Clear(); await t.Run(0.3f);
            UI.Map(); await t.Run(0.5f); t.Snap("menu_map_" + room); UI.Close(); await t.Run(0.1f);
        }
        foreach (var tab in new[] { "log", "satchel", "gear" }) { UI.Satchel(tab); await t.Run(0.4f); t.Snap("menu_" + tab); UI.Close(); await t.Run(0.1f); }
        UI.Pause(); await t.Run(0.4f); t.Snap("menu_pause"); UI.Close(); await t.Run(0.1f);
        // low on air in the Deep: the nearest vent is marked
        t.Place("deep", 120, 18); Clear(); await t.Run(0.3f);
        G.player.breath = 8; await t.Run(0.4f); t.Snap("hud_air_pointer");
        t.Place("throatgate", 30, 15); Clear(); await t.Run(0.3f);
        G.player.breath = 8; await t.Run(0.4f); t.Snap("hud_air_pointer_near");
        G.player.breath = 30;
        t.Place("keeperswatch", 10, 16); Clear(); await t.Run(0.2f);
        var bell = G.ents.OfType<Bell>().First(); var p = G.player;
        p.x = bell.Cx() - p.w / 2; p.y = bell.y + bell.h - p.h; await t.Run(0.1f);
        await t.Tap("interact"); await t.Run(0.4f); t.Snap("menu_bell");
        await t.ClickButton("Ride the cables"); await t.Run(0.4f); t.Snap("menu_cables");
        UI.Close();
        t.Ok("the menus open", true);
    }

    // ---- the autopilot: plays her from room to room the way the guide's arrow points, with the pad's own inputs ----
    // where to head in the room she is in, on the way to a room: the nearest tile of the opening into the next room
    static Vector2 exitDir;
    static Vector2? Toward(string target)
    {
        var r = G.room; var p = G.player;
        exitDir = Vector2.Zero;
        if (r.Id == target) return null;
        var route = Guide.Route(r.Id, target);
        if (route == null || route.Count < 2) return null;
        var seam = Guide.Links(r.Id)[route[1]];
        var best = seam.OrderBy(q => H.Dist(p.Cx(), p.Cy(), q.tx * 16 + 8, q.ty * 16 + 8)).First();
        int lx = best.tx, ly = best.ty;
        exitDir = lx == r.Ox ? new Vector2(-1, 0) : lx == r.Ox + r.Tw - 1 ? new Vector2(1, 0) : ly == r.Oy ? new Vector2(0, -1) : new Vector2(0, 1);
        // a ladder off the floor or roof: aim at the end of it she can reach, as the arrow does
        if (World.IsLadder(lx, ly) && (ly == r.Oy || ly == r.Oy + r.Th - 1))
        {
            int step = ly == r.Oy ? 1 : -1;
            while (World.IsLadder(lx, ly + step) && ly + step > r.Oy && ly + step < r.Oy + r.Th - 1) ly += step;
        }
        // just beyond the edge, so walking at it carries her through
        float ox = lx == r.Ox ? -12 : lx == r.Ox + r.Tw - 1 ? 12 : 0, oy = ly == r.Oy ? -12 : ly == r.Oy + r.Th - 1 ? 12 : 0;
        return new Vector2(lx * 16 + 8 + ox, ly * 16 + 8 + oy);
    }

    static readonly string[] MOVES = { "left", "right", "up", "down", "jump", "dash" };
    // Steer toward the next opening until she is in the target room, or has made no headway for a while.
    static async Task<(bool ok, string why)> Pilot(Tester t, string target, float maxSecs)
    {
        var p = G.player;
        bool trace = !string.IsNullOrEmpty(OS.GetEnvironment("PT_TRACE"));
        float best = 1e9f, sinceBetter = 0, el = 0;
        string room = G.room.Id; int tries = 0;
        bool atDoor = false; float bestAlong = -1e9f;
        while (el < maxSecs)
        {
            if (G.room.Id == target) { t.Release(MOVES); return (true, null); }
            if (G.room.Id != room) { room = G.room.Id; best = 1e9f; sinceBetter = 0; tries = 0; atDoor = false; bestAlong = -1e9f; }
            var to = Toward(target);
            if (to == null) { t.Release(MOVES); return (false, $"no route from {G.room.Id}"); }
            float dx = to.Value.X - p.Cx(), dy = to.Value.Y - p.Cy();
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            // headway: closer to the opening, or, once there, further on through it
            if (dist < 20) atDoor = true;
            float along = p.Cx() * exitDir.X + p.Cy() * exitDir.Y;
            if (atDoor && along > bestAlong + 4) { bestAlong = along; sinceBetter = 0; }
            else if (dist < best - 6) { best = dist; sinceBetter = 0; tries = 0; } else sinceBetter += 0.1f;
            if (trace && (int)(el * 10) % 5 == 0) GD.Print($"    {el:0.0}s {G.room.Id} {(p.Cx() - G.room.Px) / 16:0.0},{(p.Feet() - G.room.Py) / 16:0.0} -> {(to.Value.X - G.room.Px) / 16:0.0},{(to.Value.Y - G.room.Py) / 16:0.0} {p.state} ground={p.onGround} wet={p.InWater()} best={best:0} since={sinceBetter:0.0}");
            if (sinceBetter > 6) { t.Release(MOVES); return (false, $"no headway in {G.room.Id} at {(p.Cx() - G.room.Px) / 16:0},{(p.Feet() - G.room.Py) / 16:0} toward {(to.Value.X - G.room.Px) / 16:0},{(to.Value.Y - G.room.Py) / 16:0} ({p.state}{(p.InWater() ? ", in water" : "")})"); }
            t.Release(MOVES);
            // at the opening: go on through it, up or down a ladder or out the side
            if (atDoor && (dist < 40 || p.state == "ladder"))
            {
                if (exitDir.Y < 0) t.Down("up"); else if (exitDir.Y > 0) t.Down("down");
                else { t.Down(exitDir.X < 0 ? "left" : "right"); if (sinceBetter > 0.5f && p.onGround) await t.Tap("jump"); }
                await t.Run(0.1f); el += 0.1f;
                continue;
            }
            int hx = dx > 5 ? 1 : dx < -5 ? -1 : 0;
            if (sinceBetter > 1.5f && (int)(sinceBetter / 1.5f) % 3 == 2) hx = -hx;   // stuck a while: back off and come again
            if (hx > 0) t.Down("right"); else if (hx < 0) t.Down("left");
            bool wet = p.InWater();
            if (wet || p.state == "ladder")
            {
                if (dy < -6) t.Down("up"); else if (dy > 6) t.Down("down");
                if (p.state == "ladder" && MathF.Abs(dy) < 10 && hx != 0) await t.Tap("jump");
                if (wet && sinceBetter > 1 && dy > -6) t.Down("up");
            }
            else
            {
                bool ladderHere = p.OnLadderTile() || World.IsLadder(H.FloorI(p.Cx() / 16), H.FloorI((p.Feet() - 20) / 16));
                if (dy < -20 && ladderHere && MathF.Abs(dx) < 24) t.Down("up");
                else if (dy > 4 && p.LadderBelow() && MathF.Abs(dx) < 24) t.Down("down");
                else if (dy > 20 && p.OnOneWay() && MathF.Abs(dx) < 40) { t.Down("down"); await t.Tap("jump"); }
                else if (p.onGround && (dy < -20 || sinceBetter > 0.4f))
                {
                    // a hop, and the jet across when there is a gap or a wall in the way
                    t.Down("jump"); await t.Run(0.25f);
                    if (sinceBetter > 0.8f || MathF.Abs(dx) > 48) { t.Release("jump"); await t.Tap("dash"); }
                    tries++;
                }
            }
            await t.Run(0.1f); el += 0.1f;
        }
        t.Release(MOVES);
        return (false, $"out of time in {G.room.Id} at {(p.Cx() - G.room.Px) / 16:0},{(p.Feet() - G.room.Py) / 16:0}");
    }

    // From each objective to the next, steering only by the way the guide points.
    static async Task Route(Tester t)
    {
        LateGame();
        var only = OS.GetEnvironment("PT_FROM");
        var bad = new List<string>(); int legs = 0, done = 0;
        string prev = "reef";
        Reset(t); G.EnterRoom(Data.Room["reef"]); t.Place("reef", 25, 12);
        foreach (var s in Guide.Steps)
        {
            if (s.room == prev) continue;
            string from = prev; prev = s.room;
            if (!string.IsNullOrEmpty(only) && from != only) continue;
            var route = Guide.Route(from, s.room);
            if (route == null) { bad.Add($"{from} -> {s.room}: no route"); continue; }
            legs++;
            Tide.SetPhase("low", 5);
            if (G.room?.Id != from)
            {
                // a leg that did not arrive: put her in this one's room, just inside the door she would have come in by
                Reset(t);
                var r = Data.Room[from]; var sp = Spot(r);
                G.EnterRoom(r); t.Place(from, sp.x, sp.y);
                var back = Guide.Route(from, "reef");
                if (back != null && back.Count > 1 && Guide.Links(from).TryGetValue(back[1], out var seam) && seam.Count > 0)
                {
                    var d = seam[seam.Count / 2];
                    int ix = d.tx == r.Ox ? d.tx + 2 : d.tx == r.Ox + r.Tw - 1 ? d.tx - 2 : d.tx, iy = d.ty == r.Oy ? d.ty + 3 : d.ty == r.Oy + r.Th - 1 ? d.ty - 2 : d.ty;
                    G.player.x = ix * 16 + 8 - G.player.w / 2f; G.player.y = (iy + 1) * 16 - G.player.h;
                }
                await t.Run(0.1f);
            }
            G.god = true;
            var (ok, why) = await Pilot(t, s.room, 20 + 12 * route.Count);
            if (ok) done++; else bad.Add($"{from} -> {s.room} ({route.Count - 1} rooms, step {s.id}): {why}");
            GD.Print($"  leg {from} -> {s.room}: {(ok ? "ok" : why)}");
        }
        foreach (var b in bad) GD.Print("  STUCK " + b);
        t.Ok("the guide's way from each objective to the next can be played", bad.Count == 0, $"{done} of {legs} legs");
    }

    // The guide leads through the doors that are open now, not through one the story still holds shut.
    static async Task GuideRoutes(Tester t)
    {
        var f = World.flags;
        // the Saltworks mine: the chute is a drop, the Salt Chapel's altar only climbs from the west, and the Collapse's
        // salt floor shuts the short ladder down to the Picks
        f.Set("seawallOpen", true); f.Set("basinFilling", true); f.Set("basinCrossed", true);
        World.MarkDirty(); await t.Run(0.05f);
        var r = Guide.Route("kilns", "abbot");
        t.Ok("the way up to the Abbot goes through the mine, not up the salt chute", r != null && !r.Contains("chute") && r.Contains("minehead") && r.Contains("pillars"), string.Join(">", r ?? new()));
        f.Set("abbotDead", true); G.player.abilities.Add("boots");
        r = Guide.Route("abbot", "cloister");
        t.Ok("and the way back down does not cross the Salt Chapel against its altar", r != null && !(r.Contains("saltchapel") && r.Contains("chute")) && r.Contains("winze"), string.Join(">", r ?? new()));
        foreach (var fl in new[] { "almonerDead", "priorWedged" }) f.Set(fl, true);
        World.MarkDirty(); await t.Run(0.05f);
        r = Guide.Route("choir", "organloft");
        t.Ok("with the Precentor alive, the way to the Organ Loft does not go through his doors", r != null && !CrossesShut(r), string.Join(">", r ?? new()));
        f.Set("precentorDead", true);
        r = Guide.Route("ribbell", "spine");
        t.Ok("with the Hanging Mother alive, the way up to the Spine climbs the west ribs", r != null && !r.Contains("breastplate") && !r.Contains("sternumbell") && r.Contains("ribladder"), string.Join(">", r ?? new()));
        f.Set("motherDead", true);
        var r2 = Guide.Route("ribbell", "spine");
        t.Ok("and once she is down, through her doors", r2 != null && r2.Count < r.Count, string.Join(">", r2 ?? new()));
        f.Set("liftDropped", true);
        r = Guide.Route("gulletmouth", "lair");
        t.Ok("with the Undertow alive, the way into its lair goes round by the Buoy Chapel", r != null && r.Contains("buoychapel") && r.Count > 5, string.Join(">", r ?? new()));
        t.Place("gulletmouth", 30, 15); await t.Run(0.1f);
        t.Ok("and the Gullet Mouth says how far round it is", Guide.WhereLine(Guide.Steps.First(s0 => s0.id == "undertow")).Contains("rooms away"), Guide.WhereLine(Guide.Steps.First(s0 => s0.id == "undertow")));
        f.Set("undertowDead", true);
        r = Guide.Route("gulletmouth", "lair");
        t.Ok("once it is dead, straight through the epiglottis", r != null && r.Count == 2, string.Join(">", r ?? new()));
    }
    static bool CrossesShut(List<string> route)
    {
        for (int i = 0; i + 1 < route.Count; i++)
            if (Guide.Links(route[i])[route[i + 1]].All(q => World.Solid(q.tx, q.ty) == 1)) return true;
        return false;
    }

    public static void Register(Dictionary<string, Func<Tester, Task>> all)
    {
        all["guideroutes"] = GuideRoutes;
        all["pt_route"] = Route;
        all["pt_menus"] = Menus;
        all["cables"] = Cables;
        all["bossretry"] = BossRetry;
        all["bellbreach"] = BellBreach;
        all["pt_debug"] = Debug;
        all["pt_doors"] = Doors;
        all["pt_soak"] = Soak;
        all["pt_objects"] = Objects;
        all["pt_bells"] = Bells;
    }
}
