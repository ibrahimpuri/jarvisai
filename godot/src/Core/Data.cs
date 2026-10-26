// Game data exported from the JS build (tools/export-data.js): rooms, items, recipes, lore.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

// A loosely typed view of a JSON object, for the entity specs and lore that vary per type.
public class Props
{
    public readonly JsonObject O;
    public Props(JsonObject o) { O = o ?? new JsonObject(); }
    public static Props Empty() => new(new JsonObject());

    public bool Has(string k) => O.ContainsKey(k) && O[k] != null;
    public JsonNode N(string k) => O.ContainsKey(k) ? O[k] : null;
    public string S(string k, string def = null)
    {
        var n = N(k);
        if (n == null) return def;
        if (n is JsonValue v)
        {
            if (v.TryGetValue(out string s)) return s;
            return v.ToString();
        }
        return n.ToJsonString();
    }
    // Values parsed from JSON and values set from C# (float, int) are stored differently.
    public static bool Num(JsonValue v, out double d)
    {
        if (v.TryGetValue(out d)) return true;
        if (v.TryGetValue(out float f)) { d = f; return true; }
        if (v.TryGetValue(out int i)) { d = i; return true; }
        if (v.TryGetValue(out long l)) { d = l; return true; }
        if (v.TryGetValue(out decimal m)) { d = (double)m; return true; }
        d = 0; return false;
    }
    public float F(string k, float def = 0)
    {
        var n = N(k);
        if (n is JsonValue v)
        {
            if (Num(v, out double d)) return (float)d;
            if (v.TryGetValue(out bool b)) return b ? 1 : 0;
        }
        return def;
    }
    public int I(string k, int def = 0) => Has(k) ? (int)F(k, def) : def;
    // JS truthiness
    public bool B(string k)
    {
        var n = N(k);
        if (n == null) return false;
        if (n is JsonValue v)
        {
            if (v.TryGetValue(out bool b)) return b;
            if (Num(v, out double d)) return d != 0;
            if (v.TryGetValue(out string s)) return s.Length > 0;
        }
        return true;
    }
    public Props P(string k) => N(k) is JsonObject o ? new Props(o) : null;
    public JsonArray A(string k) => N(k) as JsonArray;
    public List<string> Strings(string k) => A(k)?.Select(x => x?.ToString()).ToList() ?? new List<string>();
    public Dictionary<string, int> Counts(string k, JsonObject direct = null)
    {
        var d = new Dictionary<string, int>();
        if (direct != null) { foreach (var kv in direct) if (kv.Value is JsonValue jv0 && Num(jv0, out double x0)) d[kv.Key] = (int)x0; return d; }
        if (N(k) is JsonObject o) foreach (var kv in o) if (kv.Value is JsonValue jv && Num(jv, out double x)) d[kv.Key] = (int)x;
        return d;
    }
    public IEnumerable<string> Keys => O.Select(kv => kv.Key);
    public void Set(string k, JsonNode v) => O[k] = v;
}

public class Pool { public string Id; public int X0, X1; public float Empty, Full; public Props Raw; }

public class RoomDef
{
    public string Id, Name, Region, Intro;
    public int Col, Row, W = 1, H = 1;
    public bool Outdoor, Deep, Brine, BellRoom;
    public int Seaward;
    public float? FixedWater;
    public List<Pool> Pools;
    public string[] Map;
    public Dictionary<char, Props> Ents = new();
    public Props Raw;
    // computed by World.Build
    public int Tw, Th, Ox, Oy;
    public float Px, Py, Pw, Ph;
    public char[] Base, Live;
}

public static class Data
{
    public static JsonObject Root;
    public static Props Regions, Items, Weapons, Fittings, Grafts, Abilities, Lore;
    public static List<Props> Recipes = new();
    public static List<Dictionary<string, int>> UpgradeCost = new();
    public static List<string> KnotTypes = new();
    public static List<RoomDef> Rooms = new();
    public static Dictionary<string, RoomDef> Room = new();

    public static void Load()
    {
        string json = Godot.FileAccess.GetFileAsString("res://data/game.json");
        Root = JsonNode.Parse(json).AsObject();
        Regions = new Props(Root["regions"].AsObject());
        Items = new Props(Root["items"].AsObject());
        Weapons = new Props(Root["weapons"].AsObject());
        Fittings = new Props(Root["fittings"].AsObject());
        Grafts = new Props(Root["grafts"].AsObject());
        Abilities = new Props(Root["abilities"].AsObject());
        Lore = new Props(Root["lore"].AsObject());
        Recipes = Root["recipes"].AsArray().Select(n => new Props(n.AsObject())).ToList();
        UpgradeCost = Root["upgradeCost"].AsArray().Select(n => n == null ? null : new Props(n.AsObject()).Counts(null, n.AsObject())).ToList();
        KnotTypes = Root["knotTypes"].AsArray().Select(n => n.ToString()).ToList();
        Rooms.Clear(); Room.Clear();
        foreach (var n in Root["rooms"].AsArray())
        {
            var p = new Props(n.AsObject());
            var r = new RoomDef
            {
                Raw = p, Id = p.S("id"), Name = p.S("name"), Region = p.S("region"), Intro = p.S("intro"),
                Col = p.I("col"), Row = p.I("row"), W = p.I("w", 1), H = p.I("h", 1),
                Outdoor = p.B("outdoor"), Deep = p.B("deep"), Brine = p.B("brine"), BellRoom = p.B("bellRoom"),
                Seaward = p.I("seaward"),
                FixedWater = p.Has("fixedWater") ? p.F("fixedWater") : null,
                Map = p.Strings("map").ToArray(),
            };
            if (p.A("pools") is JsonArray pools)
                r.Pools = pools.Select(x => { var q = new Props(x.AsObject()); return new Pool { Id = q.S("id"), X0 = q.I("x0"), X1 = q.I("x1"), Empty = q.F("empty"), Full = q.F("full"), Raw = q }; }).ToList();
            if (p.P("ents") is Props ents) foreach (var k in ents.Keys) r.Ents[k[0]] = ents.P(k);
            Rooms.Add(r); Room[r.Id] = r;
        }
    }

    public static Props Item(string id) => Items.P(id);
    public static string ItemName(string id) => Items.P(id)?.S("name") ?? id;
    public static string L(params string[] path)
    {
        Props p = Lore;
        for (int i = 0; i < path.Length - 1 && p != null; i++) p = p.P(path[i]);
        return p?.S(path[^1]);
    }
}
