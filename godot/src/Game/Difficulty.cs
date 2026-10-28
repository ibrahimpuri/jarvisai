// The four crossings: how hard the night is, as one table the rest of the game reads. Pilgrim is for the story,
// Keeper is the island as it was made, Drowned is for those who know it, and the Mere-Saint, earned by finishing the
// night once, is the island with the Saint awake under it. Profile remembers what a player has finished.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Hollowmere;

public class Crossing
{
    public string id, name, line;
    public int pips;                     // Maren's health at the start
    public float knotHp, bossHp;         // how strong every knot is (the great dead use bossHp)
    public float menace;                 // how fast the drowned walk and lunge
    public float windup, strikeCool;     // how long they telegraph a blow, and how long they wait between blows
    public float extraHit;               // chance a blow lands a pip harder
    public bool softHeavy;               // a heavy blow lands a pip softer and does not knock the wind out of her
    public float grace, dodge, dodgeCool; // grace after a hit; the dash's window and its recovery
    public int mash;                     // presses to break a grab (half when the grabbing arm is cracked)
    public int rivetsPerScrap, lootScrap, startScrap;
    public float lifeline;               // chance a container adds scrap when she has nothing left to fight with
    public int poultice;                 // pips a poultice restores
    public bool saveCost;                // saving at a bell costs a tallow candle
    public float tide;                   // the tide's length
    public float hearing;                // how far the drowned hear her
    public float regen;                  // seconds per pip she recovers while nothing hunts her (0: never)
    public float hints;                  // how long a hint waits before it shows
    public bool keepSatchel;             // dying does not leave the satchel behind
    public float oil;                    // lantern oil use
    public float assist;                 // strength of the pad's aim assist
    public float knotSight;              // knots this close glint even in the dark (0: only in the lantern's light)
    public bool earned;                  // must be unlocked by finishing the night
}

public static class Diff
{
    public static readonly Crossing[] ALL =
    {
        new()
        {
            id = "pilgrim", name = "Pilgrim", line = "For the story. Seven pips, and the drowned come apart in three rivets. Their knots glint in the dark, hints come quickly, you heal while nothing hunts you, and you keep your satchel when you die.",
            pips = 7, knotHp = 0.4f, bossHp = 0.55f, menace = 0.75f, windup = 1.35f, strikeCool = 1.4f, extraHit = 0, softHeavy = true,
            grace = 1.1f, dodge = 0.22f, dodgeCool = 0.45f, mash = 4, rivetsPerScrap = 3, lootScrap = 1, startScrap = 4, lifeline = 1,
            poultice = 3, saveCost = false, tide = 1.25f, hearing = 0.7f, regen = 18, hints = 0.5f, keepSatchel = true, oil = 0.7f, assist = 1.5f, knotSight = 150,
        },
        new()
        {
            id = "keeper", name = "Keeper", line = "As the island was made. Five pips; a Shucker takes seven rivets. The tide is fair and the dead are not.",
            pips = 5, knotHp = 1, bossHp = 1, menace = 1, windup = 1, strikeCool = 1, extraHit = 0, softHeavy = false,
            grace = 0.6f, dodge = 0.12f, dodgeCool = 0.75f, mash = 8, rivetsPerScrap = 2, lootScrap = 0, startScrap = 0, lifeline = 1,
            poultice = 2, saveCost = false, tide = 1, hearing = 1, regen = 0, hints = 1, keepSatchel = false, oil = 1, assist = 1,
        },
        new()
        {
            id = "drowned", name = "Drowned", line = "For those who know the island. Four pips, harder knots, quicker blows that sometimes land harder. Every save costs a tallow candle.",
            pips = 4, knotHp = 1.25f, bossHp = 1.2f, menace = 1.12f, windup = 0.9f, strikeCool = 0.88f, extraHit = 0.15f, softHeavy = false,
            grace = 0.5f, dodge = 0.1f, dodgeCool = 0.8f, mash = 10, rivetsPerScrap = 2, lootScrap = 0, startScrap = 0, lifeline = 0.5f,
            poultice = 2, saveCost = true, tide = 1, hearing = 1.15f, regen = 0, hints = 1, keepSatchel = false, oil = 1.1f, assist = 1,
        },
        new()
        {
            id = "saint", name = "Mere-Saint", line = "The Saint is awake, and it is listening. Four pips; the drowned are at their strongest, hear you from further off and strike hardest. The tide breathes faster. Agathe's log holds something the other crossings never show.",
            pips = 4, knotHp = 1.5f, bossHp = 1.35f, menace = 1.2f, windup = 0.85f, strikeCool = 0.8f, extraHit = 0.3f, softHeavy = false,
            grace = 0.45f, dodge = 0.1f, dodgeCool = 0.85f, mash = 12, rivetsPerScrap = 2, lootScrap = 0, startScrap = 0, lifeline = 0.35f,
            poultice = 2, saveCost = true, tide = 0.85f, hearing = 1.3f, regen = 0, hints = 1, keepSatchel = false, oil = 1.2f, assist = 1, earned = true,
        },
    };
    public static Crossing Get(string id) => ALL.FirstOrDefault(c => c.id == id) ?? ALL[1];
    public static Crossing C => Get(G.difficulty);
    public static bool Saint => G.difficulty == "saint";

    // A knot's health on this crossing: never below one, and rounded so a rivet's two points count cleanly.
    public static float Knot(float hp, bool boss) => MathF.Max(1, MathF.Round(hp * (boss ? C.bossHp : C.knotHp)));
    // What a recipe makes on this crossing (rivets from scrap are the one that changes).
    public static int Yield(Props recipe) => recipe.S("id") == "rivets" ? C.rivetsPerScrap : recipe.I("n");
    // Rivets to put down a Shucker on a crossing, for the menu: two limb knots, then the crown.
    public static int ShuckerRivets(Crossing c)
    {
        int R(float hp) => (int)MathF.Ceiling(MathF.Max(1, MathF.Round(hp * c.knotHp)) / 2f);
        return R(4) * 2 + R(5);
    }
}

// What this player has done across every night: the crossings finished and the endings seen. It lives beside the
// save, and survives a new game.
public static class Profile
{
    const string PATH = "user://hollowmere_profile.json";
    public static HashSet<string> finished = new(), endings = new();
    public static bool Persist = true;     // tests keep it in memory
    public static bool SaintUnlocked => finished.Count > 0;
    public static bool SawTrueEnding => endings.Contains("verse");

    public static void Load()
    {
        finished.Clear(); endings.Clear();
        if (!Persist || !Godot.FileAccess.FileExists(PATH)) return;
        try
        {
            var o = new Props(JsonNode.Parse(Godot.FileAccess.GetFileAsString(PATH)).AsObject());
            finished.UnionWith(o.Strings("finished")); endings.UnionWith(o.Strings("endings"));
        }
        catch { }
    }
    public static void Record(string crossing, string ending)
    {
        bool newSaint = !SaintUnlocked;
        finished.Add(crossing); endings.Add(ending);
        if (Persist)
        {
            JsonArray A(IEnumerable<string> s) => new(s.Select(v => (JsonNode)v).ToArray());
            using var fa = Godot.FileAccess.Open(PATH, Godot.FileAccess.ModeFlags.Write);
            fa?.StoreString(new JsonObject { ["finished"] = A(finished), ["endings"] = A(endings) }.ToJsonString());
        }
        JustUnlocked = newSaint;
    }
    public static bool JustUnlocked;       // the night just finished woke the Saint for the first time
}
