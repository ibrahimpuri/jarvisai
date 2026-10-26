// The satchel: slot-limited stacks, plus the shared Reliquary chest.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Hollowmere;

public class Bag
{
    public static readonly string[] MATERIALS = { "scrap", "cord", "kelp", "salt", "tallow", "spirit" };
    public int size;
    public Dictionary<string, int> items = new();
    public Bag(int size) { this.size = size; }

    public static int SlotsFor(string item, int n)
    {
        var def = Data.Item(item);
        if (def.B("key")) return def.I("slots");
        return (int)MathF.Ceiling((float)n / def.I("perSlot"));
    }
    public int Count(string item) => items.TryGetValue(item, out var n) ? n : 0;
    public int Used() { int s = 0; foreach (var kv in items) if (kv.Value > 0) s += SlotsFor(kv.Key, kv.Value); return s; }
    // How many of item can still be added.
    public int Room(string item)
    {
        var def = Data.Item(item);
        int free = size - Used();
        if (def.B("key")) return Count(item) > 0 ? 0 : (free >= def.I("slots") ? 1 : 0);
        int per = def.I("perSlot"), have = Count(item);
        int partial = have % per == 0 ? 0 : per - (have % per);
        return partial + free * per;
    }
    public int Add(string item, int n = 1)
    {
        int can = Math.Min(n, Room(item));
        if (can > 0) items[item] = Count(item) + can;
        return n - can; // leftover
    }
    public int Remove(string item, int n = 1)
    {
        int have = Count(item), take = Math.Min(have, n);
        items[item] = have - take;
        if (items[item] <= 0) items.Remove(item);
        return take;
    }
    public bool Has(Dictionary<string, int> cost) => cost.All(kv => Count(kv.Key) >= kv.Value);
    public void Pay(Dictionary<string, int> cost) { foreach (var kv in cost) Remove(kv.Key, kv.Value); }
    public List<KeyValuePair<string, int>> List() => items.Where(kv => kv.Value > 0).ToList();
    public void Clear() => items.Clear();
    public JsonObject ToJson() => new() { ["size"] = size, ["items"] = new JsonObject(items.Select(kv => new KeyValuePair<string, JsonNode>(kv.Key, kv.Value))) };
    public static Bag From(Props o)
    {
        var b = new Bag(o.I("size"));
        foreach (var kv in o.Counts("items")) b.items[kv.Key] = kv.Value;
        return b;
    }
    public Bag Clone() { var b = new Bag(size); foreach (var kv in items) b.items[kv.Key] = kv.Value; return b; }
}
