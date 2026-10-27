// World: the room grid, tile queries in world space, water, and per-room persistence.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Hollowmere;

// A loose item lying in a room (dropped, spilled, or carried by the Ebb).
public class Loose { public string item; public int n; public float x, y; }

public class RoomState
{
    public Dictionary<int, char> tiles = new();          // local index -> tile override
    public Dictionary<string, Props> ents = new();       // saved entity states by key
    public HashSet<string> seen = new();                 // tide phases seen here
    public bool visited, explored, broken;
    public Dictionary<string, float> pool;               // pool id -> water level (tiles)
    public float? choirLevel, flood;
    public List<Loose> loose;

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["tiles"] = new JsonObject(tiles.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key.ToString(), kv.Value.ToString()))),
            ["ents"] = new JsonObject(ents.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value == null ? null : JsonNode.Parse(kv.Value.O.ToJsonString())))),
            ["seen"] = new JsonArray(seen.Select(s => (JsonNode)s).ToArray()),
            ["visited"] = visited, ["explored"] = explored, ["broken"] = broken,
        };
        if (pool != null) o["pool"] = new JsonObject(pool.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value)));
        if (choirLevel != null) o["choirLevel"] = choirLevel.Value;
        if (flood != null) o["flood"] = flood.Value;
        if (loose != null) o["loose"] = new JsonArray(loose.Select(l => (JsonNode)new JsonObject { ["item"] = l.item, ["n"] = l.n, ["x"] = l.x, ["y"] = l.y }).ToArray());
        return o;
    }
    public static RoomState FromJson(JsonObject o)
    {
        var p = new Props(o);
        var s = new RoomState { visited = p.B("visited"), explored = p.B("explored"), broken = p.B("broken") };
        if (p.P("tiles") is Props t) foreach (var k in t.Keys) s.tiles[int.Parse(k)] = t.S(k)[0];
        if (p.P("ents") is Props e) foreach (var k in e.Keys) s.ents[k] = e.P(k);
        foreach (var x in p.Strings("seen")) s.seen.Add(x);
        if (p.P("pool") is Props pl) { s.pool = new(); foreach (var k in pl.Keys) s.pool[k] = pl.F(k); }
        if (p.Has("choirLevel")) s.choirLevel = p.F("choirLevel");
        if (p.Has("flood")) s.flood = p.F("flood");
        if (p.A("loose") is JsonArray la) s.loose = la.Select(n => { var q = new Props(n.AsObject()); return new Loose { item = q.S("item"), n = q.I("n"), x = q.F("x"), y = q.F("y") }; }).ToList();
        return s;
    }
}

public static class World
{
    const int TS = H.TILE;
    // Room lookup is a flat array over the screen grid; each room keeps a flat live tile array.
    static RoomDef[] grid = Array.Empty<RoomDef>();
    static int GRID_W = 1, GRID_H = 1;
    public static Props flags = Props.Empty();               // global world flags
    static Dictionary<string, RoomState> states = new();     // per-room persistent state
    public static event Action Dirty;                        // tiles changed (renderer rebuilds)

    public static void Build()
    {
        foreach (var r in Data.Rooms)
        {
            r.Tw = r.W * H.SCREEN_W; r.Th = r.H * H.SCREEN_H;
            r.Ox = r.Col * H.SCREEN_W; r.Oy = r.Row * H.SCREEN_H;
            r.Px = r.Ox * TS; r.Py = r.Oy * TS; r.Pw = r.Tw * TS; r.Ph = r.Th * TS;
            GRID_W = Math.Max(GRID_W, r.Col + r.W); GRID_H = Math.Max(GRID_H, r.Row + r.H);
            // Strip entity characters out of the tile layer.
            r.Base = new char[r.Tw * r.Th];
            for (int y = 0; y < r.Th; y++)
            {
                string row = y < r.Map.Length ? r.Map[y] : "";
                for (int x = 0; x < r.Tw; x++)
                {
                    char ch = x < row.Length ? row[x] : ' ';
                    r.Base[y * r.Tw + x] = r.Ents.ContainsKey(ch) ? ' ' : ch;
                }
            }
            r.Live = (char[])r.Base.Clone();
        }
        grid = new RoomDef[GRID_W * GRID_H];
        foreach (var r in Data.Rooms) for (int c = 0; c < r.W; c++) for (int k = 0; k < r.H; k++) grid[(r.Row + k) * GRID_W + r.Col + c] = r;
    }
    // Rebuild every room's live tiles from its map plus the saved changes.
    static void RefreshLive()
    {
        foreach (var r in Data.Rooms)
        {
            r.Live = (char[])r.Base.Clone();
            if (states.TryGetValue(r.Id, out var st)) foreach (var kv in st.tiles) r.Live[kv.Key] = kv.Value;
        }
        Dirty?.Invoke();
    }

    public static void Reset() { flags = Props.Empty(); states = new(); RefreshLive(); }
    public static void MarkDirty() => Dirty?.Invoke();

    public static RoomDef RoomAtTile(int tx, int ty)
    {
        int c = (int)MathF.Floor((float)tx / H.SCREEN_W), r = (int)MathF.Floor((float)ty / H.SCREEN_H);
        if (c < 0 || r < 0 || c >= GRID_W || r >= GRID_H) return null;
        return grid[r * GRID_W + c];
    }
    public static RoomDef RoomAtPx(float x, float y) => RoomAtTile(H.FloorI(x / TS), H.FloorI(y / TS));

    public static RoomState State(string id)
    {
        if (!states.TryGetValue(id, out var s)) states[id] = s = new RoomState();
        return s;
    }
    public static RoomState StateOrNull(string id) => states.TryGetValue(id, out var s) ? s : null;
    public static Dictionary<string, RoomState> States { get => states; set { states = value; RefreshLive(); } }

    public static char CharAt(int tx, int ty)
    {
        var r = RoomAtTile(tx, ty);
        if (r == null) return '#';
        return r.Live[(ty - r.Oy) * r.Tw + (tx - r.Ox)];
    }
    public static void SetChar(int tx, int ty, char ch)
    {
        var r = RoomAtTile(tx, ty);
        if (r == null) return;
        int i = (ty - r.Oy) * r.Tw + (tx - r.Ox);
        State(r.Id).tiles[i] = ch;
        r.Live[i] = ch;
        Dirty?.Invoke();
    }

    // 0 empty, 1 solid, 2 one-way platform.
    public static int Solidity(char ch)
    {
        switch (ch)
        {
            case '#': case '_': case 'T': case 'R': case 'M': case 'P': case 'S': case 'C': case 'G': return 1;
            case '=': case 'b': return 2;
            case 'E': return Tide.PhaseId() == "ebbing" ? 0 : 1;
            case 'L': return Tide.PhaseId() == "low" ? 0 : 1;
            case 'V': return (flags.B("priorWedged") || Tide.PhaseId() == "ebbing") ? 0 : 1;
            case 'K': return flags.B("seawallOpen") ? 0 : 1;
            case 'Z': return flags.B("skullOpen") ? 0 : 1;
            case 'Y': return flags.B("choirDrained") ? 0 : 1;
            case 'y': return flags.B("choirDrained") ? 2 : 0;
            case 'h': return (flags.B("basinFilling") && !flags.B("basinCrossed")) ? 1 : 0;
            default: return 0;
        }
    }
    public static int Solid(int tx, int ty)
    {
        int s = Solidity(CharAt(tx, ty));
        // The top rung of a ladder is a platform you can stand on (S climbs down).
        if (s == 0 && IsLadder(tx, ty) && !IsLadder(tx, ty - 1)) return 2;
        return s;
    }
    public static bool SolidPx(float x, float y) => Solid(H.FloorI(x / TS), H.FloorI(y / TS)) == 1;
    public static bool IsLadder(int tx, int ty)
    {
        char ch = CharAt(tx, ty);
        return ch == 'H' || (ch == 'K' && flags.B("seawallOpen")) || (ch == 'Z' && flags.B("skullOpen"));
    }
    public static bool IsWallHang(int tx, int ty) { char ch = CharAt(tx, ty); return ch == 'T' || ch == 'R'; }

    // ---- water -------------------------------------------------------------
    // Water surface in world pixels for a room at a world-pixel x, or +Infinity for none.
    public static float RoomSurface(RoomDef r, float x)
    {
        var st = State(r.Id);
        if (r.Pools != null)
        {
            int tx = H.FloorI(x / TS) - r.Ox;
            foreach (var p in r.Pools)
            {
                if (tx >= p.X0 && tx <= p.X1)
                {
                    float lvl = st.pool != null && st.pool.TryGetValue(p.Id, out var v) ? v : p.Empty;
                    return (r.Oy + lvl) * TS;
                }
            }
        }
        if (r.Id == "choir")
        {
            float lvl = st.choirLevel ?? 17;
            return (r.Oy + lvl) * TS;
        }
        if (r.FixedWater != null) return (r.Oy + r.FixedWater.Value) * TS;
        // the Mere's Hollow can call the sea up into a room, or send it back down
        float sea = Tide.SeaLevel() * TS - Hollow.SurgePx(r);
        if (st.flood != null) sea = MathF.Min(sea, (r.Oy + st.flood.Value) * TS);
        if (sea >= r.Py + r.Ph) return float.PositiveInfinity;
        return sea;
    }
    public static float SurfaceAt(float x, float y)
    {
        var r = RoomAtPx(x, y);
        return r != null ? RoomSurface(r, x) : float.PositiveInfinity;
    }
    public static bool IsWater(float x, float y)
    {
        if (SolidPx(x, y)) return false;
        return y > SurfaceAt(x, y);
    }
    public static bool IsBrine(float x, float y) { var r = RoomAtPx(x, y); return r != null && r.Brine && IsWater(x, y); }
    public static bool IsDeep(float x, float y) { var r = RoomAtPx(x, y); return r != null && r.Deep && IsWater(x, y); }

    // Is the cell above the high-tide line for its room? (used for hauling bodies clear)
    public static bool AboveTideLine(RoomDef r, float x, float y)
    {
        if (r.FixedWater != null || r.Id == "choir" || r.Pools != null) return !IsWater(x, y);
        float hi = (Tide.SEA_HIGH - Tide.spring) * TS;
        return y < hi;
    }
}
