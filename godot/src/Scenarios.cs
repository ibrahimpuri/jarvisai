// Scripted gameplay scenarios for the Tester, ported from the browser build's tools/play.js.
// Each drives the real game through its input layer and checks the outcome.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Hollowmere;

public static class Scenarios
{
    static Body Find(string key) => G.ents.OfType<Body>().FirstOrDefault(e => e.key == key);
    static T First<T>(Func<T, bool> f = null) where T : Ent => G.ents.OfType<T>().FirstOrDefault(e => f == null || f(e));
    static JsonArray Arr(params string[] s) => new(s.Select(x => (JsonNode)x).ToArray());

    public static readonly Dictionary<string, Func<Tester, Task>> All = new()
    {
        ["movement"] = async t =>
        {
            t.Place("reef", 25, 12); await t.Run(0.3f);
            var s = t.State();
            t.Ok("stands on the reef deck", s.room == "reef" && s.ground, s);
            await t.Hold("right", 0.4f);
            var s2 = t.State();
            t.Ok("walks right", s2.x > s.x + 30, s2);
            // jump height ~3.8 tiles
            float y0 = s2.y, minY = y0;
            t.Down("jump");
            for (int i = 0; i < 30; i++) { await t.Run(1 / 60f); minY = MathF.Min(minY, t.State().y); }
            t.Release("jump");
            t.Ok("jump rises 3-4.2 tiles", y0 - minY > 48 && y0 - minY < 68, $"rise={y0 - minY}");
            // climb the mast ladder
            t.Place("reef", 22, 12);
            await t.Hold("up", 1.5f);
            var s3 = t.State();
            t.Ok("climbs mast", s3.ty < 9, s3);
            // walk off the left edge to the pier
            t.Place("reef", 2, 16);
            await t.Hold("left", 0.6f);
            t.Ok("reaches pier", t.State().room == "pier", t.State());
        },

        ["shucker"] = async t =>
        {
            t.Place("pier", 30, 16);
            t.Ok("pier intro set tide near Rising", t.State().phase == "low", t.State());
            Tide.AdvanceTo("rising");
            string mode = null;
            for (int i = 0; i < 200; i++) { await t.Run(0.1f); mode = Find("first_shucker").mode; if (mode != "dormant") break; }
            t.Ok("first shucker stands when water reaches", mode != "dormant", mode);
            async Task<bool> Shoot(string id)
            {
                for (int i = 0; i < 12; i++)
                {
                    var e = Find("first_shucker"); var k = e.K(id);
                    if (k.broken) return true;
                    var kp = e.KnotPos(k).Value;
                    t.AimAt(kp.X, kp.Y);
                    t.Down("focus");
                    await t.Click();
                    await t.Run(0.32f);
                    if (G.player.weapons["driver"].mag == 0) { await t.Tap("reload"); await t.Run(1.4f); }
                }
                return Find("first_shucker").K(id).broken;
            }
            G.god = true; G.player.bag.Add("rivets", 30);
            var sh = Find("first_shucker");
            t.Ok("crown starts sealed", sh.IsSealed(sh.K("crown")));
            t.Ok("knee L broken", await Shoot("kneeL"));
            t.Ok("knee R broken", await Shoot("kneeR"));
            t.Ok("crawls after both knees", Find("first_shucker").pose == "crawl", Find("first_shucker").pose);
            sh = Find("first_shucker");
            t.Ok("crown unsealed after 2 limbs", !sh.IsSealed(sh.K("crown")));
            t.Ok("crown broken", await Shoot("crown"));
            t.Release("focus");
            t.Ok("shucker downed", Find("first_shucker").mode == "downed", Find("first_shucker").mode);
            await t.Run(0.3f);
            t.Ok("downed body in rising water is marked", Find("first_shucker").reknot);
            Tide.AdvanceTo("high");
            await t.Run(0.1f);
            var m2 = Find("first_shucker").mode;
            t.Ok("re-knots and stands at High", m2 is "hunt" or "windup" or "attack", m2);
            t.Snap("play_shucker");
        },

        ["dragburn"] = async t =>
        {
            G.god = true;
            t.Place("tithe", 9, 16);
            foreach (var e in G.ents.OfType<Shucker>()) e.gone = true;
            await t.Tap("interact");
            t.Ok("E starts dragging a corpse", G.player.dragging != null);
            float x0 = t.State().x;
            await t.Hold("right", 0.8f);
            float x1 = t.State().x;
            t.Ok("dragging is slow", x1 - x0 > 5 && x1 - x0 < 45, $"moved={x1 - x0}");
            await t.Tap("interact");
            t.Ok("E lets go", G.player.dragging == null);
            float oil0 = G.player.oil;
            await t.Tap("burn");
            await t.Run(3.4f);
            int gone = G.ents.OfType<Body>().Count(e => e.corpse && e.gone);
            t.Ok("X burns a body with oil", gone >= 1 && G.player.oil < oil0 - 25, $"gone={gone} oil={G.player.oil}");
        },

        ["bell"] = async t =>
        {
            t.Place("chapel", 20, 16);
            G.player.bag.Add("oil", 1);
            await t.Tap("interact");
            t.Ok("bell rekindled with oil", G.bellsLit.Contains("bell_chapel"));
            t.Ok("bell menu opens", UI.Kind == "bell", UI.Kind);
            G.player.hp = 2; Tide.SetPhase("high", 30);
            await t.ClickButton("Rest");
            t.Ok("resting shows a dream", UI.Kind == "cinematic", UI.Kind);
            await t.Tap("pause");
            var p = G.player;
            t.Ok("rest heals, saves, advances to Low", p.hp == p.maxHp && Tide.PhaseId() == "low" && G.HasSave() && G.respawn.room == "chapel", $"hp={p.hp} phase={Tide.PhaseId()} save={G.HasSave()} resp={G.respawn.room}");
            t.Place("chapel", 9, 16);
            await t.Tap("interact");
            t.Ok("Ansel speaks", UI.DialogueWho == "Brother Ansel", UI.DialogueWho);
            for (int i = 0; i < 5; i++) await t.Tap("interact");
            t.Ok("dialogue closes and sets quest", !UI.IsOpen() && G.quests.S("crank") == "active");
        },

        ["tithe"] = async t =>
        {
            G.god = true;
            t.Place("tithe", 4, 16);
            await t.Tap("interact");
            await t.Run(0.2f);
            int awake = G.ents.OfType<Shucker>().Count(e => e.mode != "dormant");
            t.Ok("crank taken triggers trap", G.player.bag.Count("crank") == 1 && World.flags.B("titheTrapped") && awake >= 3, $"awake={awake}");
            await t.Run(6);
            float? f = World.State("tithe").flood;
            t.Ok("hall floods from the grates", f != null && f < 16, f);
            var p = G.player;
            p.x = G.room.Px + 5 * 16; p.y = G.room.Py + 13 * 16; p.vy = 0;
            t.Down("swing"); await t.Run(0.12f);
            string sw = p.state;
            t.Release("swing");
            await t.Run(0.15f);
            t.Ok("Q latches onto a nearby ring", sw == "swing", sw);
            t.Place("tithe", 34, 7);
            await t.Run(0.2f);
            t.Ok("Undertow glimpse at the window", World.flags.B("glimpse"));
        },

        ["death"] = async t =>
        {
            t.Place("pier", 22, 18);
            G.player.bag.Add("scrap", 3); G.player.Hurt(99);
            await t.Run(1.2f);
            t.Ok("death drops a tidal satchel", G.player.dead && G.satchelDrop != null && G.satchelDrop.tidal && UI.Kind == "death", UI.Kind);
            await t.ClickButton("Wake at the bell");
            await t.Run(0.2f);
            t.Ok("wakes at respawn", !G.player.dead && t.State().room == "reef", t.State());
            Tide.AdvanceTo("ebbing");
            var sd = G.satchelDrop;
            t.Ok("ebb carries satchel to the Wrack Pool", sd != null && sd.room == "reef" && !sd.tidal, sd?.room);
            var p = G.player; p.x = sd.x - 5; p.y = sd.y - p.h;
            await t.Run(0.2f);
            t.Ok("satchel recovered", G.satchelDrop == null && p.bag.Count("scrap") >= 3, $"scrap={p.bag.Count("scrap")}");
        },

        ["brine"] = async t =>
        {
            G.god = true;
            t.Place("brineshaft", 20, 20);
            await t.Run(1.5f);
            var s1 = t.State();
            t.Ok("floats in brine without boots", s1.ty <= 19, s1);
            G.player.abilities.Add("boots");
            await t.Tap("boots");
            await t.Hold("left", 2.5f);
            var s2 = t.State();
            t.Ok("sinks with boots on", s2.ty > 20, s2);
        },

        ["barge"] = async t =>
        {
            G.god = true; World.flags.Set("seawallOpen", true);
            async Task Pull(int tx) { t.Place("saltgate", tx, 18); await t.Tap("interact"); await t.Run(0.08f); }
            await Pull(12); // skull first: wrong
            t.Ok("wrong lever jams", World.flags.B("basinJam"));
            Tide.AdvanceTo("ebbing");
            t.Ok("Ebb resets the puzzle", !World.flags.B("basinJam") && World.flags.Strings("basinSeq").Count == 0);
            await Pull(16); await Pull(12); await Pull(24);
            t.Ok("correct order fills the pan", World.flags.B("basinFilling"));
            World.flags.Set("basinFilling", false);
            World.flags.Set("basinSeq", Arr("sea", "saint")); World.flags.Set("basinTarget", 11);
            t.Place("saltgate", 31, 17);
            await t.Run(0.3f);
            World.flags.Set("basinSeq", Arr("sea", "saint", "kiln")); World.flags.Set("basinTarget", 7); World.flags.Set("basinFilling", true);
            await t.Run(12);
            var s = t.State();
            t.Ok("barge carries Maren up", s.ty < 9, s);
            t.Down("right", "jump"); await t.Run(0.45f); t.Release("jump"); await t.Run(1.2f); t.Release("right");
            t.Ok("reach the kilns", t.State().room == "kilns", t.State());
        },

        ["saveload"] = async t =>
        {
            t.Place("chapel", 20, 16);
            G.player.bag.Add("oil", 1); G.player.bag.Add("scrap", 4);
            await t.Tap("interact");
            await t.ClickButton("Save");
            await t.ClickButton("Leave");
            G.ToTitle();
            await t.Run(0.1f);
            await t.ClickButton("Continue");
            await t.Run(0.2f);
            t.Ok("save and continue", G.room.Id == "chapel" && G.player.bag.Count("scrap") == 4 && G.bellsLit.Contains("bell_chapel"), $"room={G.room.Id} scrap={G.player.bag.Count("scrap")}");
        },

        ["abbot"] = async t =>
        {
            G.god = true; World.flags.Set("seawallOpen", true);
            t.Place("abbot", 5, 17);
            await t.Run(0.3f);
            t.Ok("Abbot wakes when Maren approaches", First<Abbot>()?.started == true);
            int plates = 3;
            for (int i = 0; i < 40 && plates == 3; i++) { await t.Run(0.25f); plates = First<Abbot>().knots.Count(k => k.plated); }
            t.Ok("slamming a kiln knocks a plate off", plates < 3, plates);
            var a = First<Abbot>();
            var pk = a.knots.Find(k => k.plated);
            t.Ok("plated knots shrug off rivets", pk == null || !a.DamageKnot(pk, 5, "rivet"));
            foreach (var k in a.knots) k.plated = false;
            a.DamageKnot(a.K("armL"), 99, "rivet"); a.DamageKnot(a.K("armR"), 99, "rivet"); a.DamageKnot(a.K("crown"), 99, "rivet");
            await t.Run(0.1f);
            bool boots = G.ents.OfType<Collectible>().Any(e => e.type == "ability" && e.spec.S("id") == "boots");
            t.Ok("Abbot killed: Act II, boots drop, ivory", World.flags.B("abbotDead") && G.act == 2 && boots && G.player.bag.Count("ivory") >= 2, $"act={G.act} boots={boots}");
        },

        ["prisms"] = async t =>
        {
            G.god = true; foreach (var a in new[] { "boots", "jet", "tidekey" }) G.player.abilities.Add(a); G.player.oil = 90;
            t.Place("prisms", 30, 36);
            foreach (var e in G.ents.OfType<Body>()) e.gone = true;
            async Task<int> Turn(string id, int times)
            {
                var pr = First<Prism>(e => e.spec.S("id") == id);
                t.Place("prisms", pr.spec.I("tx"), pr.spec.I("ty"));
                for (int i = 0; i < times; i++) await t.Tap("interact");
                return First<Prism>(e => e.spec.S("id") == id).dir;
            }
            int a1 = await Turn("prismA", 3), b1 = await Turn("prismB", 3), c1 = await Turn("prismC", 3);
            t.Ok("prisms turn with E", a1 == 0 && b1 == 1 && c1 == 2, $"{a1},{b1},{c1}");
            t.Place("prisms", 7, 16);
            var target = First<Prism>(e => e.spec.S("id") == "prismA").Center();
            t.AimAt(target.X, target.Y);
            t.Down("focus");
            await t.Run(3.5f);
            t.Release("focus");
            t.Ok("bent beam wakes the crystal", First<Crystal>().done);
            var r = G.room; int salt = 0;
            for (int y = 0; y < r.Th; y++) for (int x = 0; x < r.Tw; x++) if (World.CharAt(r.Ox + x, r.Oy + y) == 'S') salt++;
            t.Ok("salt seal around the vault breaks", salt == 0, salt);
            t.Snap("play_prisms");
        },

        ["choir"] = async t =>
        {
            G.god = true; foreach (var a in new[] { "boots", "jet" }) G.player.abilities.Add(a); G.player.bag.Add("ivory", 1);
            t.Place("choir", 4, 16);
            await t.Tap("interact"); await t.Tap("interact");
            t.Ok("valve sets basin level 2", World.flags.I("basin") == 2, World.flags.I("basin"));
            t.Place("choir", 2, 16);
            await t.Tap("interact");
            var ch = Data.Room["choir"];
            t.Ok("tuned bell at the right pitch shatters the crystal", World.CharAt(ch.Ox + 4, ch.Oy + 19) != 'C');
            t.Place("choir", 2, 20);
            await t.Run(0.3f);
            t.Ok("Tide Key collected", G.player.Has("tidekey"));
            if (UI.IsOpen()) await t.Tap("interact");
            t.Place("choir", 36, 16);
            await t.Tap("interact");
            await t.ClickButton("Raise the water");
            await t.Run(0.5f);
            t.Ok("sluice raises the shrine water", World.flags.F("choirTarget") == 3);
            t.Place("choir", 31, 16);
            await t.Tap("interact");
            for (int i = 0; i < 4; i++) await t.Tap("interact");
            t.Place("choir", 28, 16);
            await t.Tap("interact");
            // first "Take" button belongs to the first graft listed (the Salt Ear)
            await t.ClickButton("Take");
            await t.ClickButton("Yes");
            t.Ok("font grants a graft", G.player.grafts.Contains("ear"));
            foreach (var n in new[] { 1, 2, 3, 4 }) G.tables.Add(n);
            t.Place("choir", 25, 16);
            await t.Tap("interact");
            await t.ClickButton("Yes");
            t.Ok("tables sold to the Choir", G.quests.S("tables") == "sold" && G.freeGraft);
            if (UI.IsOpen()) await t.Tap("interact");
            t.Place("choir", 36, 16);
            await t.Tap("interact");
            await t.ClickButton("Drain");
            await t.ClickButton("Yes");
            int members = G.ents.OfType<Grafted>().Count(e => !e.passive);
            t.Ok("draining turns the Choir hostile", World.flags.B("choirDrained") && World.flags.B("choirHostile") && members == 2, $"members={members}");
        },

        ["lift"] = async t =>
        {
            G.god = true;
            t.Place("priors", 30, 16);
            foreach (var e in G.ents.OfType<Saltwright>()) e.gone = true;
            for (int i = 0; i < 2; i++)
            {
                t.Place("priors", i == 1 ? 31 : 29, 16);
                await t.Tap("interact");
                t.Ok("drag corpse " + (i + 1), G.player.dragging != null);
                t.Down("left");
                for (int k = 0; k < 60; k++)
                {
                    await t.Run(0.1f);
                    if (G.player.dragging == null) break;
                    float x = (G.player.dragging.Cx() - G.room.Px) / 16;
                    if (x < (i == 1 ? 22.6f : 21.4f)) break;
                }
                t.Release("left");
                await t.Tap("interact"); await t.Run(0.4f);
            }
            await t.Run(1.5f);
            t.Ok("two bodies drop the Dead Weight lift", World.flags.B("liftDropped"));
        },

        ["undertow"] = async t =>
        {
            G.god = true; G.act = 2; Tide.SetPhase("high", 20);
            t.Place("nave", 20, 16);
            G.sinceLoad = 11; G.undertow.room = "nave";
            typeof(UndertowSystem).GetField("moveT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(G.undertow, 30f);
            await t.Run(0.3f);
            t.Ok("Undertow enters a deep sea room at High", First<Undertow>() != null);
            var u = First<Undertow>();
            if (u != null) u.DamageKnot(u.K("buoy"), 99, "rivet");
            await t.Run(0.3f);
            t.Ok("breaking the buoy knot drives it off for the tide", G.undertow.driven);
            Tide.AdvanceTo("low");
            t.Ok("it returns next tide", !G.undertow.driven);
            Tide.SetPhase("high", 20); G.undertow.room = "cloisterbell";
            t.Place("cloisterbell", 15, 16);
            await t.Run(0.3f);
            t.Ok("never enters a Bell Room", First<Undertow>() == null);
        },

        ["lair"] = async t =>
        {
            G.god = true; G.act = 3; G.player.abilities.Add("seal");
            t.Place("lair", 10, 10);
            var u = First<Undertow>(e => e.boss);
            t.Ok("Undertow waits in its lair", u != null);
            bool sealedK = u.IsSealed(u.K("ropeL"));
            u.DamageKnot(u.K("buoy"), 99, "rivet");
            bool open = !u.IsSealed(u.K("ropeL"));
            u.DamageKnot(u.K("ropeL"), 99, "rivet"); u.DamageKnot(u.K("ropeR"), 99, "rivet");
            u.DamageKnot(u.K("crown"), 99, "rivet");
            t.Ok("buoy, then ropes, then crown; skull opens", sealedK && open && World.flags.B("undertowDead") && World.flags.B("skullOpen"));
            await t.Run(0.1f);
        },

        ["shoal"] = async t =>
        {
            G.god = true; G.act = 3; World.flags.Set("skullOpen", true);
            t.Place("skullstair", 5, 16);
            var s = First<Shoal>();
            t.Ok("the Shoal is here in Act III", s != null);
            if (s == null) return;
            s.Start();
            foreach (var k in s.knots.ToList()) s.DamageKnot(k, 99, "rivet");
            int shed = G.ents.OfType<Body>().Count(e => e.shed);
            t.Ok("Shoal sheds a body per knot and dies", World.flags.B("shoalDead") && shed == 8, $"shed={shed}");
            await t.Run(0.1f);
        },

        ["teodor"] = async t =>
        {
            G.god = true; G.quests.Set("teodor", "asked"); G.quests.Set("odileMet", true);
            t.Place("nave", 14, 16);
            var td = G.ents.OfType<Body>().First(e => e.teodor);
            td.Wake(true);
            foreach (var id in new[] { "kneeL", "kneeR", "elbowL", "elbowR" }) td.DamageKnot(td.K(id), 99, "rivet");
            t.Ok("breaking every limb knot but the Crown captures Teodor", td.mode == "captured", td.mode);
            var p = G.player; p.x = td.Cx() - 5; p.y = td.Feet() - p.h;
            await t.Tap("interact");
            t.Ok("E binds him", G.quests.S("teodor") == "bound", G.quests.S("teodor"));
            t.Place("saltbell", 8, 18);
            await t.Tap("interact");
            for (int i = 0; i < 6 && UI.IsOpen(); i++) await t.Tap("interact");
            t.Ok("Odile takes him in; satchel grows to 12", G.quests.S("teodor") == "captured" && G.player.bag.size == 12, $"{G.quests.S("teodor")} size={G.player.bag.size}");
            G.StartAct(3);
            t.Place("chapel", 20, 16);
            t.Place("saltbell", 20, 18);
            var tr = Find("teodor_reknot");
            t.Ok("Act III breaches the Saltworks Bell Room", tr != null && G.breachT > 50, G.breachT);
            if (tr != null) foreach (var id in new[] { "kneeL", "kneeR", "elbowL", "elbowR" }) tr.DamageKnot(tr.K(id), 99, "rivet");
            await t.Run(0.2f);
            t.Ok("capturing him again saves Teodor and Odile", G.quests.S("teodor") == "saved" && !G.quests.B("odileLost"), G.quests.S("teodor"));
        },

        ["weapons"] = async t =>
        {
            G.god = true;
            var p = G.player;
            p.weapons["harpoon"] = new WeaponState { tier = 0, mag = 1 }; p.weapons["sling"] = new WeaponState { tier = 1, mag = 1 }; p.weapons["sawline"] = new WeaponState { tier = 0, mag = 1 };
            p.weaponOrder.AddRange(new[] { "harpoon", "sling", "sawline" });
            p.bag.Add("bolts", 4); p.bag.Add("flares", 2); p.bag.Add("spools", 2);
            t.Place("cloister", 4, 16);
            var s = First<Shucker>();
            s.Wake(true); s.mode = "hunt"; s.stun = 99; s.SetPose("stand");
            await t.Run(0.1f);
            s = First<Shucker>();
            var k = s.KnotPos(s.K("kneeL")).Value;
            await t.Tap("w2");
            t.AimAt(k.X, k.Y);
            await t.Click(); await t.Run(0.5f);
            t.Ok("harpoon bolt pins a limb", s.pinT > 0 || s.K("kneeL").hp < 3, $"pin={s.pinT} hp={s.K("kneeL").hp}");
            await t.Tap("w3");
            await t.Click(); await t.Run(0.3f);
            t.Ok("flare sling throws a flare", G.projectiles.Any(q => q.kind == "flare"));
            await t.Tap("w4"); await t.Run(0.7f);
            await t.Click(); await t.Run(0.1f);
            t.Ok("saw-line strung", G.sawlines.Count == 1, G.sawlines.Count);
        },

        ["encrust"] = async t =>
        {
            G.stats.byAct[1] = new Dictionary<string, int> { ["knee"] = 9, ["elbow"] = 2, ["crown"] = 12 };
            G.StartAct(2);
            var spec = Props.Empty(); spec.Set("x", 100); spec.Set("y", 100); spec.Set("key", "t"); spec.Set("pose", "standing");
            var s = new Shucker(spec, Data.Room["reef"]);
            G.ApplyEncrustation(s);
            var plated = s.knots.Where(k => k.plated).Select(k => k.id).ToList();
            t.Ok("Brine plates the knot type broken most (never crown), leaves 2+ open", G.encrustedType == "knee" && plated.Contains("kneeL") && s.knots.Count(k => !k.plated) >= 2, $"{G.encrustedType} [{string.Join(",", plated)}]");
            await t.Run(0.05f);
        },

        ["endings"] = async t =>
        {
            G.god = true; G.act = 3; World.flags.Set("skullOpen", true);
            t.Place("lamp", 9, 16);
            await t.Tap("interact");
            string hymn = UI.Inst.AllText();
            t.Ok("hymn refuses without 3 grafts", hymn.Contains("three grafts"), hymn.Length > 80 ? hymn[..80] : hymn);
            await t.Tap("interact");
            t.Place("lamp", 31, 16);
            await t.Tap("interact");
            string dark = UI.Inst.AllText();
            t.Ok("Keeper's Dark needs the log and survivors", dark.Contains("whole log"), dark.Length > 80 ? dark[..80] : dark);
            await t.Tap("interact");
            for (int i = 1; i <= 9; i++) G.pages.Add(i);
            await t.Tap("interact");
            await t.ClickButton("Yes");
            t.Ok("ending plays as a cinematic with fates", UI.Kind == "cinematic", UI.Kind);
            await t.Tap("pause"); await t.Run(0.15f);
            t.Ok("Keeper's Dark ending plays", UI.Kind == "ending" && G.ended == "dark", $"{UI.Kind} {G.ended}");
            t.Snap("play_ending");
        },

        ["opening"] = async t =>
        {
            // Real inputs only: reef -> pier -> seawall ladder -> chapel -> back down -> Tithe Hall.
            async Task<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase)> Until(string[] keys, Func<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase), bool> cond, float secs = 9)
            {
                t.Down(keys);
                var s = t.State();
                for (int f = 0; f < secs * 60; f += 3) { s = t.State(); if (cond(s)) break; await t.Run(0.05f); }
                t.Release(keys);
                return s;
            }
            G.god = true;
            t.Place("reef", 25, 12); await t.Run(0.3f);
            var s = await Until(new[] { "left" }, s => s.room == "pier");
            t.Ok("walk from the deck to the pier", s.room == "pier", s);
            s = await Until(new[] { "left" }, s => s.tx <= 4);
            t.Ok("reach the seawall ladder", s.tx <= 5, s);
            s = await Until(new[] { "up" }, s => s.room == "seawall" && s.st != "ladder");
            t.Ok("climb into the seawall", s.room == "seawall", s);
            s = await Until(new[] { "right" }, s => s.room == "chapel");
            t.Ok("walk into the chapel", s.room == "chapel", s);
            s = await Until(new[] { "left" }, s => s.room == "seawall" && s.tx <= 4);
            s = await Until(new[] { "down" }, s => s.room == "pier" && s.ground);
            t.Ok("climb back down to the pier", s.room == "pier" && s.ground, s);
            s = await Until(new[] { "left" }, s => s.room == "tithe");
            t.Ok("enter the Tithe Hall by the side door", s.room == "tithe", s);
            s = await Until(new[] { "left", "sneak" }, s => s.tx <= 5, 15);
            t.Ok("sneak to the altar", s.tx <= 6, s);
        },

        ["chandelier"] = async t =>
        {
            // Feedback-driven bot: pump each swing and let go when the predicted arc passes under the next
            // ring (or onto the gallery), holding Q so the next ring is caught automatically.
            G.god = true; foreach (var e in G.ents.OfType<Body>()) e.gone = true;
            t.Place("tithe", 4, 16);
            var targets = new[] { (11, 9), (20, 9), (27, 7), (34, 7) };
            t.Down("swing"); await t.Run(0.1f);
            var rings = new List<int>();
            for (int hop = 0; hop < 6; hop++)
            {
                var w0 = G.player.swing;
                if (w0 == null) break;
                int sw = (int)MathF.Round((w0.ax - G.room.Px - 8) / 16);
                rings.Add(sw);
                var tgt = targets.First(q => q.Item1 > sw);
                bool last = tgt.Item1 == 34;
                t.Down("right");
                bool released = false;
                for (int i = 0; i < 200 && !released; i++)
                {
                    var p = G.player; var w = p.swing; var r = G.room;
                    if (w == null) break;
                    float vx = MathF.Cos(w.ang) * w.av * w.len * 1.2f, vy = -MathF.Sin(w.ang) * w.av * w.len * 1.2f - 160;
                    float x = p.Cx(), y = p.y + 4;
                    float gx = r.Px + tgt.Item1 * 16 + 8, gy = r.Py + tgt.Item2 * 16 + 8;
                    bool ok = false;
                    for (float tt = 0; tt < 1.2f && !ok; tt += 1 / 60f)
                    {
                        vy += 1300 / 60f; x += vx / 60f; y += vy / 60f;
                        if (last) { if (x > r.Px + 32 * 16 && x < r.Px + 38 * 16 && MathF.Abs(y + 18 - (r.Py + 8 * 16)) < 6 && vy > 0) ok = true; }
                        else if (MathF.Abs(x - gx) < 40 && y > gy && y < gy + 100) ok = true;
                    }
                    if (ok) { await t.Tap("jump"); released = true; }
                    else await t.Run(1 / 60f);
                }
                await t.Run(0.4f);
                if (last || !released) break;
                if (G.player.state != "swing") { await t.Run(0.4f); if (G.player.state != "swing") break; }
            }
            t.Release("right", "swing");
            await t.Run(0.7f);
            var s = t.State();
            t.Ok("chain the chandelier rings to the gallery", s.ty <= 8 && s.tx >= 31, $"rings=[{string.Join(",", rings)}] end={s}");
        },

        ["menus"] = async t =>
        {
            G.ToTitle(); await t.Run(0.3f); t.Snap("ui_title");
            t.Ok("title menu", UI.Kind == "title", UI.Kind);
            await t.ClickButton("Controls"); await t.Run(0.2f); t.Snap("ui_controls");
            await t.ClickButton("Back");
            await t.ClickButton("Row out"); await t.Run(1.5f); t.Snap("ui_opening");
            t.Ok("opening cinematic", UI.Kind == "cinematic", UI.Kind);
            await t.Tap("pause"); await t.Run(0.5f);
            G.player.bag.Add("scrap", 6); G.player.bag.Add("oil", 2); G.player.bag.Add("kelp", 3);
            UI.Satchel(); await t.Run(0.2f); t.Snap("ui_satchel");
            UI.Close(); UI.Map(); await t.Run(0.2f); t.Snap("ui_map");
            UI.Close(); UI.Pause(); await t.Run(0.2f); t.Snap("ui_pause");
            UI.Close();
            t.Place("chapel", 20, 16); G.player.bag.Add("oil", 1);
            await t.Tap("interact"); await t.Run(0.2f); t.Snap("ui_bell");
            await t.ClickButton("Bench"); await t.Run(0.2f); t.Snap("ui_bench");
            UI.Close();
            t.Place("chapel", 9, 16); await t.Tap("interact"); await t.Run(0.3f); t.Snap("ui_dialogue");
            UI.Close();
            t.Place("seawall", 15, 16);
            var cv = First<Carving>(); var p = G.player; p.x = cv.Cx() - 5; p.y = cv.y + 22 - p.h;
            await t.Run(0.1f); await t.Tap("interact"); await t.Run(0.8f); t.Snap("ui_inscription");
            UI.Close();
            G.player.Hurt(99); await t.Run(1.3f); t.Snap("ui_death");
            t.Ok("death screen", UI.Kind == "death", UI.Kind);
        },

        ["controller"] = async t =>
        {
            // which family a controller belongs to
            t.Ok("DualSense reads as PlayStation", Pad.Classify("dualsense wireless controller", 0x054C) == PadKind.PlayStation);
            t.Ok("PS4 controller by name", Pad.Classify("ps4 controller", 0) == PadKind.PlayStation);
            t.Ok("Switch Pro reads as Switch", Pad.Classify("nintendo switch pro controller", 0x057E) == PadKind.Switch);
            t.Ok("Joy-Cons read as Switch", Pad.Classify("joy-con (l/r)", 0) == PadKind.Switch);
            t.Ok("Xbox Series reads as Xbox", Pad.Classify("xbox series x controller", 0x045E) == PadKind.Xbox);
            t.Ok("unknown pads fall back to Xbox prompts", Pad.Classify("8bitdo pro 2", 0x2DC8) == PadKind.Xbox);

            Pad.Simulate(PadKind.Xbox);
            t.Place("reef", 25, 12);
            var p = G.player;
            // left stick walks; a light tilt creeps
            float x0 = p.x;
            t.PadAxis(JoyAxis.LeftX, -1); await t.Run(0.4f); t.PadAxis(JoyAxis.LeftX, 0); await t.Run(0.1f);
            t.Ok("left stick walks", p.x < x0 - 40, $"{x0} -> {p.x}");
            t.PadAxis(JoyAxis.LeftX, 0.45f); await t.Run(0.1f);
            t.Ok("a light tilt sneaks", Inp.Held("sneak") && Inp.Held("right"));
            t.PadAxis(JoyAxis.LeftX, 0); await t.Run(0.1f);
            // A jumps
            await t.Run(0.3f);
            float y0 = p.y; t.PadButton(JoyButton.A, true); await t.Run(0.15f); t.PadButton(JoyButton.A, false);
            t.Ok("A jumps", p.y < y0 - 15, $"{y0} -> {p.y}");
            await t.Run(0.8f);
            // the right stick aims; with no target the aim follows it exactly
            t.PadAxis(JoyAxis.RightX, 0); t.PadAxis(JoyAxis.RightY, -1); await t.Run(0.1f);
            t.Ok("right stick aims", MathF.Abs(H.AngleDiff(p.aim, -H.PI / 2)) < 0.25f, p.aim);
            t.PadAxis(JoyAxis.RightY, 0); await t.Run(0.1f);
            // RT fires and rumbles
            int mag = p.weapons["driver"].mag;
            Pad.LastRumble = (0, 0, 0);
            t.PadAxis(JoyAxis.TriggerRight, 1); await t.Run(0.1f); t.PadAxis(JoyAxis.TriggerRight, 0); await t.Run(0.1f);
            t.Ok("RT fires", p.weapons["driver"].mag < mag, $"{mag} -> {p.weapons["driver"].mag}");
            t.Ok("firing rumbles", Pad.LastRumble.weak > 0, Pad.LastRumble);
            // tap Y interacts (on release); hold Y burns
            t.Place("reef", 13, 12);
            var chest = First<Chest>();
            p.x = chest.Cx() - p.w / 2; await t.Run(0.1f);
            await t.PadTap(JoyButton.Y); await t.Run(0.2f);
            t.Ok("tapping Y opens the locker", chest.done, chest.done);
            // tap / hold on the d-pad: poultice, stitch kit
            p.hp = 3; p.bag.Add("stitchkit", 1);
            await t.PadTap(JoyButton.DpadUp); await t.Run(0.1f);
            t.Ok("d-pad up uses a poultice", p.healKind == "poultice", p.healKind);
            await t.Run(3.2f); p.hp = 2;
            await t.PadHold(JoyButton.DpadUp, 0.5f);
            t.Ok("holding d-pad up kneels for the stitch kit", p.healKind == "stitch" && p.healT > 0, $"{p.healKind} {p.healT}");
            await t.Run(4.2f);
            // hold RB quick-crafts; tap RB reloads
            p.bag.Add("scrap", 2);
            await t.PadHold(JoyButton.RightShoulder, 0.5f);
            t.Ok("holding RB quick-crafts rivets", p.craftT > 0, p.craftT);
            await t.Run(2.5f);
            p.weapons["driver"].mag = 2;
            await t.PadTap(JoyButton.RightShoulder); await t.Run(0.05f);
            t.Ok("tapping RB reloads", p.reloadT > 0, p.reloadT);
            await t.Run(2f);
            // d-pad left / right change weapon
            p.weapons["harpoon"] = new WeaponState { tier = 0, mag = 1 }; p.weaponOrder.Add("harpoon");
            await t.PadTap(JoyButton.DpadRight);
            t.Ok("d-pad right changes weapon", p.cur == "harpoon", p.cur);
            await t.PadTap(JoyButton.DpadLeft);
            t.Ok("d-pad left changes back", p.cur == "driver", p.cur);
            // L3 latches sneaking
            await t.PadTap(JoyButton.LeftStick);
            t.Ok("L3 toggles sneaking", Inp.sneakLatch && Inp.Held("sneak"));
            await t.PadTap(JoyButton.LeftStick);
            // aim assist pulls a near miss onto a knot
            t.Place("pier", 20, 16);
            var sh = Find("first_shucker");
            var knot = sh.knots.First(k => !k.broken && !sh.IsSealed(k));
            var kp = sh.KnotPos(knot).Value; var lp = p.LanternPos();
            float exact = MathF.Atan2(kp.Y - lp.Y, kp.X - lp.X), off = exact + 0.12f;
            t.PadAxis(JoyAxis.RightX, MathF.Cos(off)); t.PadAxis(JoyAxis.RightY, MathF.Sin(off)); await t.Run(0.1f);
            lp = p.LanternPos();
            float err = sh.knots.Where(k => !k.broken && !sh.IsSealed(k)).Select(k => sh.KnotPos(k).Value)
                .Min(q => MathF.Abs(H.AngleDiff(p.aim, MathF.Atan2(q.Y - lp.Y, q.X - lp.X))));
            t.Ok("aim assist pulls a near miss onto a knot", err < 0.045f, $"err={err:0.000} (stick off by 0.12)");
            Settings.AimAssist = false; await t.Run(0.05f);
            t.Ok("aim assist can be switched off", MathF.Abs(H.AngleDiff(p.aim, off)) < 0.05f);
            Settings.AimAssist = true;
            t.PadAxis(JoyAxis.RightX, 0); t.PadAxis(JoyAxis.RightY, 0);
            // View opens the journal; the bumpers turn its pages; B closes it
            await t.PadTap(JoyButton.Back); await t.Run(0.1f);
            t.Ok("View opens the journal", UI.Kind == "satchel", UI.Kind);
            string before = UI.Inst.AllText();
            await t.PadTap(JoyButton.RightShoulder);
            t.Ok("RB turns the page", UI.Inst.AllText() != before);
            await t.PadTap(JoyButton.B); await t.Run(0.1f);
            t.Ok("B closes it", !UI.IsOpen(), UI.Kind);
            // Start pauses, Start resumes, and the same press does not reopen it
            await t.PadTap(JoyButton.Start); await t.Run(0.1f);
            t.Ok("Start pauses", UI.Kind == "pause", UI.Kind);
            await t.PadTap(JoyButton.Start); await t.Run(0.2f);
            t.Ok("Start resumes and stays resumed", !UI.IsOpen(), UI.Kind);
            // Switch: A (the right button) confirms in menus, B (bottom) backs out
            Pad.Simulate(PadKind.Switch);
            UI.Dialogue("Test", new List<string> { "one", "two" }); await t.Run(0.1f);
            await t.PadTap(JoyButton.B);
            t.Ok("Switch A (right) advances dialogue", UI.IsOpen() && UI.Inst.AllText().Contains("two"), UI.Inst.AllText());
            await t.PadTap(JoyButton.B); await t.Run(0.1f);
            t.Ok("and closes it", !UI.IsOpen());
            UI.Pause(); await t.Run(0.1f);
            await t.PadTap(JoyButton.A); await t.Run(0.1f);
            t.Ok("Switch B (bottom) backs out", !UI.IsOpen(), UI.Kind);
            // glyphs follow the controller
            Glyphs.Force = null;
            t.Ok("Switch prompt for jump is B", Glyphs.Plain("{jump}", Glyphs.Current) == "B" && Glyphs.Current == Glyphs.Style.Switch);
            t.Ok("keyboard prompt for interact is E", Glyphs.Plain("{interact} Open", Glyphs.Style.Keyboard) == "E Open");
            t.Ok("PlayStation names its buttons", Glyphs.Plain("{jump} / {fire}", Glyphs.Style.PlayStation) == "Cross / R2", Glyphs.Plain("{jump} / {fire}", Glyphs.Style.PlayStation));
            // losing the controller mid-game pauses
            Pad.OnConnection(0, false); await t.Run(0.1f);
            t.Ok("disconnecting the pad pauses the game", UI.Kind == "pause" && UI.Inst.AllText().Contains("disconnected"), UI.Inst.AllText().Split('\n').FirstOrDefault());
            UI.Close();
        },

        ["glyphs"] = async t =>
        {
            t.Place("reef", 13, 12);
            var chest = First<Chest>(); var p = G.player; p.x = chest.Cx() - p.w / 2;
            foreach (var st in new[] { Glyphs.Style.Keyboard, Glyphs.Style.Xbox, Glyphs.Style.PlayStation, Glyphs.Style.Switch })
            {
                Glyphs.Force = st;
                H.Toasts.Clear(); H.ToastMsg("Grabbed! Mash {mash} to break free.", 30);
                await t.Run(0.2f); t.Snap("glyph_prompt_" + st);
                UI.Inst.Controls(null, "pad"); await t.Run(0.2f); t.Snap("glyph_controls_" + st);
                UI.Satchel("map"); await t.Run(0.2f); t.Snap("glyph_journal_" + st);
                UI.Close();
            }
            Glyphs.Force = null; H.Toasts.Clear();
            UI.Inst.ControllerSettings(null); await t.Run(0.2f); t.Snap("ui_controller");
            t.Ok("controller settings open", UI.Kind == "controller", UI.Kind);
            UI.Close();
        },

        // The opening on the outer rocks, played with nothing but inputs.
        ["prologue"] = async t =>
        {
            var p = G.player; var r = G.room;
            float Tx() => (p.Cx() - r.Px) / H.TILE;
            float Ty() => (p.y + p.h - 1 - r.Py) / H.TILE;
            t.Ok("a new game starts on the outer rocks", r.Id == "breakers" && p.onGround, t.State());
            await t.Run(0.3f);
            t.Ok("the tide holds at Low out here", Tide.paused && Tide.PhaseId() == "low");
            t.Ok("the tide dial is not shown yet", G.dialT == 0, G.dialT);
            t.Ok("no instructions on arrival", H.Toasts.Count == 0, string.Join(" | ", H.Toasts.Select(x => x.Text)));
            await t.Run(1.5f);
            t.Ok("Maren thinks about the boat", G.narr.thoughts.Contains("boat"));
            // stand still: only then does the walk hint surface
            t.Ok("no hint before hesitating", Hints.Shown == null, Hints.Shown);
            await t.Run(5f);
            t.Ok("standing still raises the walk hint", Hints.Shown == "move", Hints.Shown);
            // walk to the first rock and lean on it
            t.Down("left"); await t.Run(1.6f);
            t.Ok("walking retires the walk hint", Hints.HasDone("move"));
            await t.Run(3.2f);
            t.Ok("leaning on the rock raises the jump hint", Hints.Shown == "jump", $"{Hints.Shown} at {Tx():0.0},{Ty():0.0}");
            // up the two steps
            for (int i = 0; i < 3 && Tx() > 56; i++) { await t.Hold("jump", 0.3f); await t.Run(0.4f); }
            t.Ok("jumps up the rocks", Tx() < 58 && Ty() < 14.5f, $"{Tx():0.0},{Ty():0.0}");
            // across the pool: jump at the lip
            for (int f = 0; f < 240 && Tx() > 52.6f; f++) await t.Run(1 / 60f);
            await t.Hold("jump", 0.3f); await t.Run(0.5f);
            t.Ok("clears the tide pool", Tx() < 47.9f && Ty() < 14.5f && !p.InWater(), $"{Tx():0.0},{Ty():0.0}");
            // into the chimney, against its west wall
            await t.Run(1.2f);
            t.Release("left"); await t.Run(0.2f);
            t.Ok("stands at the foot of the chimney", Tx() > 36.5f && Tx() < 41 && Ty() > 13, $"{Tx():0.0},{Ty():0.0}");
            t.Ok("Maren notices the timber", G.narr.thoughts.Contains("timber"));
            // cling and leap, left wall, right wall, out over the top
            int dir = -1; bool jumped = false;
            t.Down("left"); await t.Hold("jump", 0.3f);
            for (int f = 0; f < 60 * 4 && Ty() > 4.6f; f++)
            {
                if (p.hanging == dir && p.vy > 0)
                {
                    t.Down("jump"); await t.Run(1 / 60f);
                    t.Release(dir < 0 ? "left" : "right"); dir = -dir; t.Down(dir < 0 ? "left" : "right");
                    await t.Run(0.22f); t.Release("jump"); jumped = true; continue;
                }
                if (Ty() < 6 && dir > 0) { t.Release("right"); t.Down("left"); dir = -1; }
                await t.Run(1 / 60f);
            }
            await t.Run(0.6f);
            t.Ok("climbs the chimney by wall-jumping", jumped && Ty() < 5 && Tx() < 37, $"{Tx():0.0},{Ty():0.0}");
            t.Ok("wall-jumping is remembered", Hints.HasDone("walljump"));
            // along the planks to the cliff, then stall
            await t.Run(1.6f); t.Release("left"); await t.Run(3.5f);
            t.Ok("stalling on the planks raises the drop hint", Hints.Shown == "drop", $"{Hints.Shown} at {Tx():0.0},{Ty():0.0}");
            t.Down("down"); await t.Tap("jump"); t.Release("down"); await t.Run(1.2f);
            t.Ok("drops through the planks into the hollow", Ty() > 12 && Tx() < 32, $"{Tx():0.0},{Ty():0.0}");
            t.Ok("Maren sees the salt", G.narr.thoughts.Contains("salt"));
            // the salt plug: shoot the barrel from a safe distance
            t.Down("right"); for (int f = 0; f < 240 && Tx() < 29.5f; f++) await t.Run(1 / 60f); t.Release("right"); await t.Run(0.2f);
            var barrel = G.ents.OfType<Barrel>().First();
            int hp0 = p.hp;
            t.AimAt(barrel.Cx(), barrel.Cy());
            await t.Click(); await t.Run(2.5f);
            t.Ok("the shot barrel burns", barrel.done);
            t.Ok("the fire cracks the salt plug", World.CharAt(r.Ox + 21, r.Oy + 11) != 'S' && World.CharAt(r.Ox + 21, r.Oy + 12) != 'S');
            t.Ok("shooting from range keeps Maren clear of the fire", p.hp == hp0, $"{hp0} -> {p.hp}");
            await t.Run(8f);
            // the crystal tunnel: a dash carries her through untouched
            t.Down("left"); for (int f = 0; f < 300 && Tx() > 17.4f; f++) await t.Run(1 / 60f);
            await t.Tap("dash"); await t.Run(0.5f);
            t.Ok("dashes through the salt crystals unhurt", Tx() < 13.5f && p.hp == hp0, $"{Tx():0.0} hp {p.hp}");
            t.Ok("Maren eyes the crystals", G.narr.thoughts.Contains("crystals"));
            for (int f = 0; f < 60 * 3 && G.room.Id == "breakers"; f++) await t.Run(1 / 60f);
            t.Release("left");
            t.Ok("walks onto the reef", G.room.Id == "reef", G.room.Id);
            await t.Run(1.5f);
            t.Ok("the tide starts to move", !Tide.paused && World.flags.B("landed"));
            t.Ok("and the dial fades in", G.dialT > 0.5f, G.dialT);
            t.Ok("the Constance thought follows", G.narr.thoughts.Contains("start"));
            t.Ok("the reef's chest is still on the deck", G.ents.OfType<Chest>().Any());
        },

        // Falling into the pool is not a dead end, and a cut from the crystals brings the dash hint.
        ["prologue2"] = async t =>
        {
            var p = G.player; var r = G.room;
            float Tx() => (p.Cx() - r.Px) / H.TILE;
            float Ty() => (p.y + p.h - 1 - r.Py) / H.TILE;
            t.Place("breakers", 50, 18); await t.Run(0.8f);
            t.Ok("in the tide pool", p.InWater(), $"{Tx():0.0},{Ty():0.0}");
            await t.Run(3.5f);
            t.Ok("treading water raises the swim hint", Hints.Shown == "swim", Hints.Shown);
            t.Down("left", "up"); await t.Run(0.5f); await t.Hold("jump", 0.3f); await t.Run(0.4f); t.Release("up");
            await t.Hold("jump", 0.3f); await t.Run(0.6f); t.Release("left");
            t.Ok("climbs out of the pool", !p.InWater() && Ty() < 14.5f && Tx() < 48, $"{Tx():0.0},{Ty():0.0}");
            // walk into the crystals: a cut, and the dash hint comes at once
            World.SetChar(r.Ox + 21, r.Oy + 11, ' '); World.SetChar(r.Ox + 21, r.Oy + 12, ' ');
            t.Place("breakers", 19, 12); await t.Run(0.2f);
            int hp0 = p.hp;
            t.Down("left"); await t.Run(1.2f); t.Release("left");
            t.Ok("walking over crystals cuts", p.hp < hp0, $"{hp0} -> {p.hp}");
            t.Place("breakers", 18, 12); await t.Run(0.6f);
            t.Ok("and the dash hint shows straight away", Hints.Shown == "dash", Hints.Shown);
            // hurt with a poultice in hand and nothing hostile near: the poultice hint
            await t.Run(4.5f);
            t.Place("breakers", 5, 12); await t.Run(5f);
            t.Ok("hurt and safe: the poultice hint", Hints.Shown == "heal", Hints.Shown);
            await t.Tap("poultice"); await t.Run(1f);
            t.Ok("using it retires the hint", Hints.HasDone("poultice") && Hints.Shown != "heal", Hints.Shown);
            // hints can be turned off
            Settings.Hints = false; t.Place("breakers", 50, 18); await t.Run(5f);
            t.Ok("no hints when switched off", Hints.Shown == null, Hints.Shown);
            Settings.Hints = true;
        },

        // Screenshots of hints as players see them (rendered runs only).
        ["hintshots"] = async t =>
        {
            await t.Run(6f); t.Snap("hint_move_keyboard");
            Hints.Did("move");
            foreach (var (st, room, x, y, name, wait) in new[] {
                (Glyphs.Style.Keyboard, "breakers", 38, 13, "walljump", 6f), (Glyphs.Style.Xbox, "breakers", 38, 13, "walljump", 1f),
                (Glyphs.Style.PlayStation, "breakers", 25, 4, "drop", 4f), (Glyphs.Style.Switch, "breakers", 28, 12, "shoot", 8f) })
            {
                Glyphs.Force = st; t.Place(room, x, y); await t.Run(wait);
                t.Snap($"hint_{name}_{st}");
                t.Ok($"{name} hint shows", Hints.Shown == name, Hints.Shown);
            }
            Glyphs.Force = null;
        },

        ["narrative"] = async t =>
        {
            int n = Data.Lore.P("carvings").Keys.Count();
            t.Ok("every room has a carving", n == Data.Rooms.Count, $"{n} of {Data.Rooms.Count}");
            var bad = Data.Rooms.Where(r => Narr.CarvingSpecs(r, new HashSet<(int, int)>()).Count == 0).Select(r => r.Id).ToList();
            t.Ok("carvings find a floor spot", bad.Count == 0, string.Join(",", bad));
            t.Place("seawall", 15, 16);
            var cv = First<Carving>();
            var p = G.player; p.x = cv.Cx() - 5; p.y = cv.y + 22 - p.h;
            await t.Run(0.06f); await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("E reads a carving", UI.Kind == "inscription", UI.Kind);
            await t.Tap("interact"); await t.Run(0.1f);
            t.Ok("carving marked read", Narr.IsRead(cv.spec.S("id")));
            t.Place("tithe", 7, 16);
            var b = G.ents.OfType<Body>().First(e => e.corpse);
            t.AimAt(b.Cx(), b.Cy());
            t.Down("focus"); await t.Run(1.2f); t.Release("focus");
            t.Ok("the focused beam draws out a whisper", G.narr.heard.Count > 0);
            t.Ok("region epigraph registered", G.narr.regions.Contains("wrackline"));
            G.EnterRoom(Data.Room["pier"]);
            t.Ok("Maren thinks on the pier", G.narr.thoughts.Contains("pier"));
            G.Save();
            var saved = new Props(JsonNode.Parse(Godot.FileAccess.GetFileAsString("user://hollowmere_save.json")).AsObject());
            t.Ok("narrative state saved", saved.P("narr")?.Has("read") == true);
        },
    };
}
