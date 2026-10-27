// NPCs, quests, collectibles and endings.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hollowmere;

public class NpcLook { public string robe, skin, hat; public bool crust, lamp; }

public static class Story
{
    public static readonly Dictionary<int, string> PAGES = new()
    {
        [1] = "Keeper's log, I. The monks did not build the lamp to guide ships. They built it to burn. Its beam is cut through a lens of Mere-ivory, and the Brine cannot abide it.",
        [2] = "Keeper's log, II. The Abbot ordered the kilns stoked day and night. \"Salt is the Saint's blood,\" he said. The salt was alive, and it climbed his arms like frost.",
        [3] = "Keeper's log, III. Brother Ansel rings his bell every hour. He says the sound keeps the crust from his heart. I have seen the white on his hands. He is not wrong, and he is not well.",
        [4] = "Keeper's log, IV. The Prior hid his tide tables when the Choir began to graft. The tables say when each door breathes. Whoever holds them holds the island.",
        [5] = "Keeper's log, V. Hesper took the first graft in the font. She says the Saint is singing to her. She says if the hymn is finished the island will go home to the sea. She is not afraid.",
        [6] = "Keeper's log, VI. I lit the lamp on the night of the spring tide, as Keepers have for two hundred years. Three of the Choir fell in the courtyard, smoking. The beam does not care who it burns.",
        [7] = "Keeper's log, VII. I put the lamp out. Forty ships will pass this reef before the equinox. Without the light some will wreck. With it, everyone who carries a graft will die. I have not slept.",
        [8] = "Keeper's log, VIII. There is a third way. The old warning beacon in the skull burns red, not white; it warns ships off without burning anyone. And the rope ferry still reaches the mainland. It needs hands to row it.",
        [9] = "Keeper's log, IX. Maren, if you are reading this, you came for the lamp. Before you light it, count who is still alive. Then decide what a Keeper is for.",
    };
    public static readonly Dictionary<int, string> TABLES = new()
    {
        [1] = "Prior's tide table, page one. \"At Low the crypt door gives.\" Columns of numbers, each crossed through with a hymn line.",
        [2] = "Prior's tide table, page two. \"The Choir water does not answer the moon. It answers the key.\"",
        [3] = "Prior's tide table, page three. \"My door I keep for the Ebb, when the sea turns its back.\"",
        [4] = "Prior's tide table, page four. \"At the spring tide every line climbs one mark. Even the Bell Rooms are not deep enough.\"",
    };

    static readonly Dictionary<string, NpcLook> LOOK = new()
    {
        ["ansel"] = new NpcLook { robe = "#4a4038", skin = "#a0a898", crust = true },
        ["odile"] = new NpcLook { robe = "#5a4430", skin = "#c09878", hat = "#3a3028" },
        ["hesper"] = new NpcLook { robe = "#3a3a5a", skin = "#a8b0c0", crust = true, lamp = true, hat = "#c0a060" },
        ["agathe"] = new NpcLook { robe = "#2a3a4a", skin = "#c0a890", hat = "#b08840", lamp = true },
    };
    static readonly Dictionary<string, string> NAMES = new() { ["ansel"] = "Brother Ansel", ["odile"] = "Odile Marrow", ["hesper"] = "Hesper Vane", ["agathe"] = "Agathe Hult" };

    public static string NpcName(string id) => NAMES.GetValueOrDefault(id, id);
    public static NpcLook NpcLook(string id) => LOOK.GetValueOrDefault(id, LOOK["ansel"]);
    public static bool ChoirFriendly() => !World.flags.B("choirDrained") && !World.flags.B("choirHostile");
    public static bool NpcPresent(string id, string roomId)
    {
        var q = G.quests;
        if (id == "ansel") return q.S("tables") != "sold";
        if (id == "odile") return !q.B("odileLost");
        if (id == "hesper") return ChoirFriendly() && !q.B("hesperDead");
        return true;
    }

    public static void Talk(string id)
    {
        var p = G.player; var q = G.quests;
        void Say(List<string> lines, Action done = null) => UI.Dialogue(NAMES[id], lines.Where(l => l != null).ToList(), done);
        if (id == "ansel")
        {
            if (!q.B("anselMet"))
            {
                q.Set("anselMet", true);
                Say(new()
                {
                    "Agathe's girl. You have her jaw. Come in, come in; nothing that walks out there will cross this threshold while the bell rings.",
                    "Forty days since the lamp went dark. The Brine took the Abbey in a week. It grows knots at the joints of the drowned and works them like puppets.",
                    "You will have seen the chalk: haul them or burn them. Leave one lying in the water and the next tide gives it back.",
                    "If you mean to reach the lamp you must go up through the Saltworks. The seawall gate wants its Sluice Crank. The crank lies on the altar of the Tithe Hall, below us.",
                    "And mind the hall's bell rope. Rest here when you must. Resting turns the tide to the next Low, and the tide does not care what you left lying in it.",
                }, () => G.SetQuest("crank", "active"));
                return;
            }
            if (G.tables.Count >= 4 && q.S("tables") != "given" && q.S("tables") != "sold")
            {
                Say(new()
                {
                    "The Prior's tables! All four. Bless you, child.",
                    "Take this. I ground it from the Prior's own spectacles and a sliver of Mere-ivory. Through it you will see the knots in the dark, without wasting oil.",
                }, () =>
                {
                    q.Set("tables", "given"); p.abilities.Add("lens"); Snd.Pickup();
                    H.ToastMsg("Saltsight Lens: knots show in darkness without burning oil.");
                });
                return;
            }
            var hints = new List<string>();
            if (!World.flags.B("seawallOpen")) hints.Add("The crank. Tithe Hall, below the seawall. Go at Low tide; you have Low and Rising before the pews go under.");
            else if (G.act == 1) hints.Add("The Abbot of Salt waits above the kilns. They say his plates only fall when he strikes his own furnaces.");
            if (G.tables.Count > 0 && G.tables.Count < 4) hints.Add($"You have {G.tables.Count} of the Prior's four tide tables. Bring me all four. The Choir would pay for them too, but I would rather you did not.");
            if (G.act >= 2 && !p.Has("jet")) hints.Add("The Prior's door answers to the hymnal. \"When the sea turns its back\": that is the Ebb.");
            if (G.act >= 2 && G.encrustedType != null) hints.Add($"The Brine is learning. The ones that come back now are braced at the {G.encrustedType}, where you have been breaking them. It always leaves two knots open.");
            if (p.grafts.Count > 0) hints.Add("I can see the salt in you. If you want a graft cut out, use my bench. It costs a pip of your health, and only once.");
            if (hints.Count == 0) hints.Add("The lamp is in the skull of the Saint. Your mother is there, I think. Go carefully.");
            var al = Narr.NpcLore("ansel"); if (al != null) hints.Add(al);
            Say(hints);
            return;
        }
        if (id == "odile")
        {
            if (!q.B("odileMet"))
            {
                q.Set("odileMet", true);
                Say(new()
                {
                    "Another diver! Odile Marrow. Salvage smith, when there is salvage. Use my bench all you like. Scrap and Mere-ivory buy weapon tiers.",
                    "My partner Teodor went down into the Lower Cloister a week ago and did not come up. If you find him... bring me his tags. Or bring me him.",
                    "I am not stupid. If he is walking, he is Brine. But I have heard the Brine lets go of some of them, if the Crown is left whole.",
                }, () => { if (!q.Has("teodor")) q.Set("teodor", "asked"); });
                return;
            }
            if (p.bag.Count("tags") > 0 && q.S("teodor") == "killed")
            {
                Say(new() { "His tags.", "...", "Thank you for not leaving him down there. Here: I finished his harpoon. Barbed bolts. It is the best work I have done.", "And take his satchel. He would want it used." }, () =>
                {
                    p.bag.Remove("tags", 1);
                    if (!p.weapons.ContainsKey("harpoon")) { p.weapons["harpoon"] = new WeaponState { tier = 0, mag = 1 }; p.weaponOrder.Add("harpoon"); p.bag.Add("bolts", 4); }
                    p.weapons["harpoon"].tier = 2;
                    p.bag.size = Math.Max(p.bag.size, 12);
                    q.Set("teodor", "tags");
                    H.ToastMsg("Harpoon Gun upgraded to tier 3 (Barbed). Satchel expanded to " + p.bag.size + " slots.");
                });
                return;
            }
            if (q.S("teodor") == "bound")
            {
                Say(new() { "You... you brought him back whole. His Crown is still lit.", "I will keep him tied in the back room, where the bell is loudest. Perhaps the Brine will let go.", "Take his satchel. I owe you more than that." }, () =>
                {
                    q.Set("teodor", "captured"); p.bag.size = Math.Max(p.bag.size, 12);
                    H.ToastMsg("Satchel expanded to " + p.bag.size + " slots.");
                });
                return;
            }
            var lines = new List<string>();
            if (q.S("teodor") == "asked") lines.Add("Any sign of Teodor? He wore brass tags at his collar. Shuckers in the Cloister, mostly.");
            if (q.S("teodor") == "captured" && G.act < 3) lines.Add("Teodor is quiet today. Sometimes he hums.");
            if (World.flags.B("choirDrained") && !q.B("cacheTold")) { lines.Add("You drained the Choir? Then my old salvage cache is lying in the open on the shrine floor. Take what is in it."); q.Set("cacheTold", true); }
            if (lines.Count == 0) lines.Add("Bring me scrap and Mere-ivory and I will make your tools nastier. That is what I am for.");
            var ol = Narr.NpcLore("odile"); if (ol != null) lines.Add(ol);
            Say(lines);
            return;
        }
        if (id == "hesper")
        {
            if (!q.B("hesperMet"))
            {
                q.Set("hesperMet", true);
                Say(new()
                {
                    "A Keeper's daughter in the Saint's own shrine. Be welcome. We will not harm you while you keep the water.",
                    "The Saint sings, diver. Under the salt and the bone, it is still singing. The Choir is learning the hymn.",
                    "The font will give you the Saint's gifts, if you want them. Each one grows a knot on you. Each one makes you more like us.",
                    "And if you find the Prior's tide tables, lay them in our bowl. The Saint will reward you.",
                });
                return;
            }
            if (p.Has("tidekey") && !q.B("hesperSatchel") && !q.B("cacheSatchel"))
            {
                Say(new() { "You hold the Tide Key, and still the shrine is full. You could have drained us, and you did not.", "Take this. A Choir satchel, woven of kelp and salt. It will hold more than you think." }, () =>
                {
                    q.Set("hesperSatchel", true); p.bag.size = Math.Max(p.bag.size + 4, 12);
                    H.ToastMsg("Satchel expanded to " + p.bag.size + " slots.");
                });
                return;
            }
            Say(new() { Narr.NpcLore("hesper"), G.act >= 3 ? "The spring tide is here. When you reach the lamp, I will be there, if you want the hymn finished." : "Three gifts, and the Choir at your side, and you could finish the Saint's hymn yourself." });
            return;
        }
        if (id == "agathe")
        {
            q.Set("agatheMet", true);
            int n = G.pages.Count;
            var lines = new List<string>
            {
                "Maren. You rowed out. Of course you did.",
                "I put the lamp out. Its beam burns the Brine out of anything it touches, and that includes everyone who took a graft. Hesper. Ansel. Maybe you.",
            };
            if (n < 9) lines.Add($"I wrote it all down. My log is scattered across the island; you have {n} of my 9 pages.");
            lines.Add("There are three things a Keeper could do tonight.");
            lines.Add("Light the lamp: the great lamp at the centre. The Brine burns out of the island, and everyone carrying a graft dies with it.");
            lines.Add("Ring the Saint's bell: the hymn can only be finished by someone with three grafts and the Choir beside her. The island goes home to the sea.");
            lines.Add("Or fire the old red beacon and leave the lamp dark: ships are warned off and no one is burned. But we need my whole log to know the ferry's tides, and at least two others alive to row it.");
            // the Tine in her hand, grown all the way: Agathe sees it, and the window
            if (Legend.HostReady) { var L = Data.Lore.P("legend"); lines.Add(L.S("agathe")); lines.Add(L.S("choice")); }
            else if (Legend.merged) lines.Add("And put down whatever that is in your hand. It is breathing, Maren.");
            Say(lines);
        }
    }

    public static void OfferTables()
    {
        if (G.tables.Count == 0) { UI.Say(null, "An offering bowl of salt-white bone. Hesper said the Choir wants the Prior's tide tables."); return; }
        if (G.tables.Count < 4) { UI.Say(null, $"The bowl is sized for four pages. You have {G.tables.Count}."); return; }
        UI.Confirm("Lay all four tide tables in the Choir's bowl? Ansel will not get them.", () =>
        {
            G.quests.Set("tables", "sold");
            G.freeGraft = true;
            Snd.Toll();
            H.ToastMsg("The Choir takes the tables. Far below, a bell cracks and falls silent.");
            UI.Say("Hesper Vane", "The Saint is pleased. The font will give you one gift freely, without the knot weighing on your path. Ansel's bell has rung its last. Do not grieve; he was already ours.");
        });
    }

    public static void Collect(Props spec)
    {
        var p = G.player;
        Snd.Pickup();
        switch (spec.S("type"))
        {
            case "stitch":
                if (p.maxHp < 9) p.maxHp++;
                p.hp = p.maxHp;
                H.ToastMsg($"Keeper's Stitch. Health rises to {p.maxHp} pips.");
                G.collected.Add(spec.S("id"));
                break;
            case "seal":
                p.pressure = Math.Min(10, p.pressure + 1);
                H.ToastMsg($"Brass Seal. The suit's Pressure Rating rises to {p.pressure}.");
                G.collected.Add(spec.S("id"));
                break;
            case "page":
                G.pages.Add(spec.I("n"));
                UI.Say($"Agathe's log ({G.pages.Count}/9)", PAGES[spec.I("n")]);
                break;
            case "table":
                G.tables.Add(spec.I("n"));
                UI.Say($"Prior's tide table ({G.tables.Count}/4)", TABLES[spec.I("n")]);
                break;
            case "ability":
                {
                    string aid = spec.S("id");
                    var a = Data.Abilities.P(aid);
                    p.abilities.Add(aid);
                    Snd.Bell(520, 0.3f);
                    UI.Say(a.S("name"), a.S("desc"));
                    if (aid == "jet") World.flags.Set("priorWedged", true);
                    if (aid == "seal") G.StartAct(3);
                    break;
                }
            case "weapon":
                {
                    string wid = spec.S("id");
                    var def = Data.Weapons.P(wid);
                    if (!p.weapons.ContainsKey(wid)) { p.weapons[wid] = new WeaponState { tier = 0, mag = def.I("mag") }; p.weaponOrder.Add(wid); }
                    var ammo = wid switch { "harpoon" => ("bolts", 4), "sling" => ("flares", 3), "sawline" => ("spools", 2), _ => (null, 0) };
                    if (ammo.Item1 != null) p.bag.Add(ammo.Item1, ammo.Item2);
                    UI.Say(def.S("name"), wid switch
                    {
                        "harpoon" => "Pins a limb to a wall for 4 s. Bolts can be recovered if they do not sink at Ebb. Change weapon with {weapon}.",
                        "sling" => "Throws flares that burn for 10 s and draw Driftlings. A lit flare stuck to a body lets the swarm strip its knots for you.",
                        _ => "Strings a wire at knee height that cuts the knots of anything crossing it.",
                    });
                    break;
                }
        }
    }

    // ---- endings ---------------------------------------------------------------------------
    public static List<string> Survivors()
    {
        var q = G.quests;
        var list = new List<string>();
        if (q.S("tables") != "sold") list.Add("Brother Ansel");
        if (!q.B("odileLost")) list.Add("Odile Marrow");
        if (!World.flags.B("choirDrained") && !q.B("hesperDead")) list.Add("Hesper Vane");
        if (q.S("teodor") == "saved") list.Add("Teodor");
        return list;
    }
    static readonly Dictionary<string, string> TITLES = new() { ["relight"] = "Relight the Lamp", ["hymn"] = "The Saint's Hymn", ["dark"] = "Keeper's Dark", ["host"] = "Host of the Mere" };
    public static void Ending(string kind)
    {
        var p = G.player; var q = G.quests;
        if (kind == "hymn")
        {
            bool allied = ChoirFriendly() && !q.B("hesperDead");
            if (p.grafts.Count < 3 || !allied)
            {
                UI.Say(null, $"The Saint's bell will not sound for you. The hymn needs three grafts (you carry {p.grafts.Count}) and an allied Choir{(allied ? "" : " (the Choir is not with you)")}.");
                return;
            }
        }
        if (kind == "dark")
        {
            var s = Survivors();
            if (G.pages.Count < 9 || s.Count < 2)
            {
                UI.Say(null, $"Without Agathe's whole log ({G.pages.Count}/9 pages) and at least two others alive to row the ferry ({s.Count}: {(s.Count > 0 ? string.Join(", ", s) : "none")}), the beacon would only light an empty island.");
                return;
            }
        }
        if (kind == "host" && !Legend.HostReady)
        {
            UI.Say(null, "The window is only a window. Far below, the reef, and the sea where the sea should be.");
            return;
        }
        UI.Confirm(kind == "host" ? "Go down to the sea at the window? There is no coming back from it." : $"{TITLES[kind]}. This ends the night. Are you sure?", () => Finish(kind));
    }
    public static void Finish(string kind)
    {
        var p = G.player; var q = G.quests;
        var text = new List<string>();
        if (kind == "relight")
        {
            text.Add("You open the shutters and the great lamp catches. The beam swings out across the reef, white as salt.");
            text.Add("Everywhere it touches, the Brine boils away. Knots crack in every flooded room on the island at once. The drowned lie down for good.");
            var dead = new List<string>();
            if (q.S("tables") != "sold") dead.Add("Brother Ansel, still ringing his bell");
            if (!q.B("hesperDead") && !World.flags.B("choirDrained")) dead.Add("Hesper Vane and the Choir");
            if (dead.Count > 0) text.Add("The beam does not care who it burns: " + string.Join("; ", dead) + ".");
            if (p.grafts.Count >= 2) text.Add("The salt in your own body flares. You hear the bell of the Seawall Chapel once, far away, and then nothing. The ships come home by your light.");
            else text.Add("Ships pass the reef safely by morning. You keep the lamp, as your mother did. You do not sleep well.");
        }
        else if (kind == "hymn")
        {
            text.Add("You ring the Saint's bell and the Choir answers from below. The hymn comes out of you in a voice that is not yours.");
            text.Add("The ribs groan. The skull tilts. Slowly, with the patience of something very large and very old, the Mere-Saint rolls over and slides into the deep, and the island goes with it.");
            text.Add("You go too, singing. Agathe watches from the rope ferry. The reef is clear forever.");
        }
        else if (kind == "host")
        {
            // the sea's new host: she does what the Abbot could not
            text.Add("You do not go to the lamp. You go to the window, and the sea is waiting under it, up to the sill, two hundred and twelve steps above the reef, where the tide tables say it cannot be.");
            text.Add("The Tine opens in your hand like a flower made of bone. It was never a weapon. It was an ear, and it has been listening to you since the pier. Now you hear what it heard: the Saint under the island, three hundred years into dying, so tired, and so glad that someone came.");
            text.Add("It asks. You say yes. Under the island something vast lets go at last, the Skull settles on its bones, and in every flooded room on Hollowmere the drowned turn their faces toward the lamp.");
            text.Add("Your mother says your name. You hear it the way the sea hears it. You take her hand, and she does not let go, and you walk down into the water together, and she stops struggling before the second stair.");
            text.Add("The lamp is never lit again. That winter the reef takes forty ships, and at every High tide their crews come up the Long Pier with knots at their joints, singing in D, to the one who called them home.");
        }
        else
        {
            text.Add("You leave the lamp dark and set fire to the old red beacon. It burns all night, warning every ship away from the reef.");
            text.Add("At the ebb you load the rope ferry: " + string.Join(", ", Survivors()) + ", Agathe and you. No one is burned. No one is left behind who could be carried.");
            text.Add("The mainland is warned. Somewhere behind you, the Brine keeps the island for itself, and waits.");
        }
        G.ended = kind;
        G.DeleteSave();
        UI.Ending(TITLES[kind], text, G.StatsSummary(), Narr.Fates(kind));
    }
}
