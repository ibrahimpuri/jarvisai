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
            World.flags.Set("hollowGift", true);   // the first Shucker standing with the tide, without the sea's gift (see "hollow")
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

        ["audit"] = async t =>
        {
            t.Place("pier", 26, 16);
            Tide.AdvanceTo("rising");
            for (int i = 0; i < 200; i++) { await t.Run(0.1f); if (Find("first_shucker").mode != "dormant") break; }
            G.god = true; G.player.bag.Add("rivets", 30);
            await t.Run(0.6f); t.Snap("audit_0_stand");
            int n = 0;
            foreach (var id in new[] { "elbowL", "kneeL", "kneeR", "crown" })
            {
                for (int i = 0; i < 12; i++)
                {
                    var e = Find("first_shucker"); var k = e.K(id);
                    if (k.broken) break;
                    var kp = e.KnotPos(k).Value;
                    t.AimAt(kp.X, kp.Y); t.Down("focus");
                    await t.Click(); await t.Run(0.05f); t.Snap($"audit_{++n}_{id}_hit");
                    await t.Run(0.27f);
                    if (G.player.weapons["driver"].mag == 0) { await t.Tap("reload"); await t.Run(1.4f); }
                }
                await t.Run(0.05f); t.Snap($"audit_{++n}_{id}_broken");
            }
            t.Release("focus");
            await t.Run(1f); t.Snap("audit_downed");
        },

        ["gore"] = async t =>
        {
            Settings.Gore = "full";
            World.flags.Set("hollowGift", true);   // this test is about the drowned, not the sea's gift
            t.Place("pier", 26, 16);
            Tide.AdvanceTo("rising");
            int sprayed = 0;
            for (int i = 0; i < 200; i++) { await t.Run(0.1f); if (Find("first_shucker").mode != "dormant") break; }
            G.god = true; G.player.bag.Add("rivets", 30);
            async Task Shoot(string id, string snap)
            {
                for (int i = 0; i < 12; i++)
                {
                    var e = Find("first_shucker"); var k = e.K(id);
                    if (k.broken) break;
                    var kp = e.KnotPos(k).Value;
                    t.AimAt(kp.X, kp.Y); t.Down("focus");
                    await t.Click();
                    if (Find("first_shucker").K(id).broken) { sprayed = Math.Max(sprayed, Gore.DropCount); await t.Run(0.08f); t.Snap(snap); }
                    await t.Run(0.3f);
                    if (G.player.weapons["driver"].mag == 0) { await t.Tap("reload"); await t.Run(1.4f); }
                }
            }
            await Shoot("elbowL", "gore_1_arm");
            var sh = Find("first_shucker");
            t.Ok("breaking an elbow tears off the forearm", sh.severed.Contains("forearmL"), string.Join(",", sh.severed));
            t.Ok("the forearm flies off as a piece", Gore.gibs.Any(g => g.kind == "forearm"));
            t.Ok("blood sprays", sprayed > 10, sprayed);
            await Shoot("kneeL", "gore_2_leg");
            t.Ok("breaking a knee takes the shin", Find("first_shucker").severed.Contains("shinL"));
            await t.Run(1.2f);
            t.Ok("pieces come to rest", Gore.gibs.Any(g => g.rest));
            await Shoot("kneeR", "gore_3_crawl");
            await Shoot("crown", "gore_4_down");
            t.Release("focus");
            await t.Run(1.5f); t.Snap("gore_5_after");
            Tide.AdvanceTo("high");
            await t.Run(0.6f);
            sh = Find("first_shucker");
            t.Ok("re-knotted, it grows the torn limbs back in salt", sh.severed.Count == 0 && sh.saltLimbs.Contains("forearmL") && sh.saltLimbs.Contains("shinL"), $"{sh.mode} salt={string.Join(",", sh.saltLimbs)} cut={string.Join(",", sh.severed)}");
            await t.Run(0.8f); t.Snap("gore_6_salt_limbs");
            var saved = sh.Serialize(); var copy = new Shucker(sh.spec, sh.room); copy.Restore(saved);
            t.Ok("salt limbs are saved", copy.saltLimbs.SetEquals(sh.saltLimbs));
            // a dry, lit room, with the body held at a distance: the spray has stone to land on
            t.Place("kilns", 15, 18);
            foreach (var e in G.ents.OfType<Body>()) e.gone = true;
            var hall = Data.Room["kilns"];
            var sp = Props.Empty(); sp.Set("x", hall.Px + 23 * 16 + 8); sp.Set("y", hall.Py + 19 * 16); sp.Set("key", "gore_test"); sp.Set("pose", "standing"); sp.Set("facing", -1);
            var dummy = new Shucker(sp, hall); G.ents.Add(dummy);
            await t.Run(0.3f);
            async Task Hit(string id, string snap)
            {
                for (int i = 0; i < 10 && !dummy.K(id).broken; i++)
                {
                    dummy.stun = 99; dummy.vx = 0;
                    var kp = dummy.KnotPos(dummy.K(id)).Value;
                    t.AimAt(kp.X, kp.Y); t.Down("focus");
                    await t.Click(); await t.Run(0.32f);
                    if (G.player.weapons["driver"].mag == 0) { await t.Tap("reload"); await t.Run(1.4f); }
                }
                await t.Run(0.05f); t.Snap(snap);
            }
            await Hit("elbowR", "gore_7_kilns_arm");
            await Hit("kneeR", "gore_8_kilns_leg");
            t.Release("focus");
            await t.Run(1.5f);
            t.Ok("the spray stains the stone", Gore.Stains("kilns").Count > 8, Gore.Stains("kilns").Count);
            t.Snap("gore_9_kilns_stains");
            dummy.Down("rivet"); dummy.StandUp(); dummy.stun = 99; dummy.vx = 0;
            await t.Run(0.6f); t.Snap("gore_10_salt_regrown");
            t.Ok("the kilns body regrew its arm and leg in salt", dummy.saltLimbs.Contains("forearmR") && dummy.saltLimbs.Contains("shinR"), string.Join(",", dummy.saltLimbs));
            // with gore off the drowned only shed salt
            Settings.Gore = "off";
            Gore.Clear();
            var fresh = new Shucker(sp, hall); G.ents.Add(fresh);
            fresh.DamageKnot(fresh.K("elbowL"), 99, "rivet");
            t.Ok("gore off: limbs stay on", fresh.K("elbowL").broken && fresh.severed.Count == 0 && Gore.gibs.Count == 0 && Gore.DropCount == 0, $"{fresh.severed.Count} {Gore.gibs.Count} {Gore.DropCount}");
            Settings.Gore = "full";
        },

        ["hollow"] = async t =>
        {
            // the gift: when the tide first turns on the Long Pier, the sea comes up over the boards and leaves it there
            t.Place("pier", 31, 16);
            G.god = true;
            float deck = World.SurfaceAt(G.player.Cx(), G.player.Cy());
            bool rose = false;
            for (int i = 0; i < 260 && !G.ents.OfType<HollowRelic>().Any(); i++)
            {
                await t.Run(0.1f);
                if (Hollow.surge > 1.5f && !rose) { rose = true; t.Snap("hollow_1_wave"); }
            }
            t.Ok("the sea heaves up over the pier", rose && World.flags.B("hollowGift"));
            var relic = G.ents.OfType<HollowRelic>().FirstOrDefault();
            t.Ok("and leaves the Hollow on the boards", relic != null);
            await t.Run(1.2f); t.Snap("hollow_2_relic");
            // take it up: what it is, then its first verse
            G.player.x = relic.Cx() - G.player.w / 2; G.player.y = relic.y + relic.h - G.player.h; G.player.vx = 0;
            await t.Run(0.2f);
            await t.Tap("interact");
            t.Ok("taking it shows what it is", UI.Kind == "inscription" && Hollow.owned, UI.Kind);
            await t.Run(0.3f); t.Snap("hollow_3_reveal");
            await t.Tap("interact"); await t.Run(0.3f);
            t.Ok("then the first verse, the Calling", UI.Kind == "inscription" && Hollow.Has("calling"), UI.Kind);
            t.Snap("hollow_4_calling_card");
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("it is in her hand", Hollow.held && !UI.IsOpen());
            t.Ok("the relic is gone from the pier", !G.ents.OfType<HollowRelic>().Any(e => !e.done));
            // the Calling: point above the water and sing, and the sea climbs to meet it
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            t.Place("pier", 30, 16); await t.Run(0.3f);
            Hollow.resonance = 60;
            float s0 = World.SurfaceAt(G.player.Cx(), G.player.Cy());
            t.AimAt(G.player.Cx() + 30, s0 - 4.5f * 16);
            await t.Click(); await t.Run(2.2f);
            float s1 = World.SurfaceAt(G.player.Cx(), G.player.Cy());
            t.Ok("the Calling raises the sea", s0 - s1 > 3 * 16, $"rose {(s0 - s1) / 16:0.0} tiles");
            t.Ok("and costs resonance (it drinks the sea back in slowly)", Hollow.resonance < 45, Hollow.resonance);
            t.Snap("hollow_5_calling");
            Hollow.surgeHold = 0; await t.Run(4f);
            t.Ok("then the sea settles back", MathF.Abs(World.SurfaceAt(G.player.Cx(), G.player.Cy()) - s0) < 8);
            // pointing down sends it back
            Hollow.resonance = 60;
            t.AimAt(G.player.Cx() + 40, s0 + 4 * 16);
            await t.Click(); await t.Run(2f);
            t.Ok("the Calling can send the sea down", World.SurfaceAt(G.player.Cx(), G.player.Cy()) - s0 > 2 * 16);
            Hollow.surgeHold = 0; await t.Run(4f);
            // still water does not hear it
            t.Place("brineshaft", 20, 18); Hollow.resonance = 60; await t.Run(0.2f);
            await t.Click(); await t.Run(0.2f);
            t.Ok("still water does not answer", Hollow.surgeRoom == null && Hollow.resonance == 60, Hollow.resonance);
            // brimming, a held note turns the tide
            t.Place("pier", 30, 16); await t.Run(0.2f);
            string ph0 = Tide.PhaseId();
            Hollow.resonance = Hollow.MAX;
            t.AimAt(G.player.Cx() + 40, G.player.y - 40);
            t.Down("fire"); await t.Run(1.4f); t.Release("fire");
            t.Ok("the held note spends everything", Hollow.resonance < 5 && Hollow.rush > 0, Hollow.resonance);
            await t.Run(0.6f); t.Snap("hollow_6_turning");
            for (int i = 0; i < 120 && Tide.PhaseId() == ph0; i++) await t.Run(0.1f);
            t.Ok("holding the note brimming turns the tide", Tide.PhaseId() != ph0, $"{ph0} -> {Tide.PhaseId()}");
            // the Riptide throws the drowned back
            Hollow.Learn("riptide", false); await t.Run(0.2f); UI.Close();
            t.Ok("verses can be learned", Hollow.verse == "riptide" && Hollow.verses.Count == 2);
            var kilns = Data.Room["kilns"];
            t.Place("kilns", 15, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            Shucker Spawn(string key, int tx)
            {
                var sp = Props.Empty(); sp.Set("x", kilns.Px + tx * 16 + 8); sp.Set("y", kilns.Py + 19 * 16); sp.Set("key", key); sp.Set("pose", "standing"); sp.Set("facing", -1);
                var b = new Shucker(sp, kilns); G.ents.Add(b); return b;
            }
            var a = Spawn("rip_a", 19);
            await t.Run(0.2f);
            Hollow.resonance = 60;
            float ax = a.Cx();
            t.AimAt(a.Cx(), a.Cy());
            await t.Click(); await t.Run(0.1f); t.Snap("hollow_7_riptide");
            await t.Run(0.5f);
            t.Ok("the Riptide throws the drowned back", a.Cx() - ax > 30, $"moved {a.Cx() - ax:0}");
            // the Salting crusts them, and the next blow shatters a limb
            Hollow.Learn("salting", false); await t.Run(0.2f); UI.Close();
            var sb = Spawn("salt_a", 21); await t.Run(0.3f);
            Hollow.resonance = 60;
            t.AimAt(sb.Cx(), sb.Cy() - 4);
            await t.Click(); await t.Run(0.4f);
            t.Ok("the Salting crusts a body in salt", sb.saltT > 0, sb.saltT);
            t.Snap("hollow_8_salted");
            Hollow.Hold(false);
            var hip = sb.KnotPos(sb.K("kneeL")).Value;
            t.AimAt(sb.Cx(), sb.y + 6);
            await t.Click(); await t.Run(0.3f);
            t.Ok("one blow shatters a salted limb", sb.knots.Count(k => k.broken) >= 1 && sb.severed.Count >= 1, string.Join(",", sb.severed));
            // at stone, salt rises in a pillar
            Hollow.Hold(true); Hollow.verse = "salting";
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            t.Ok("a verse just sung is still ringing", Hollow.Ringing("salting") > 0, Hollow.Ringing("salting"));
            Hollow.ringing.Clear();
            Hollow.resonance = 60;
            t.AimAt(G.player.Cx() + 40, G.player.y + G.player.h + 8);
            await t.Click(); await t.Run(0.3f);
            t.Ok("sung at stone, a pillar of salt rises", Hollow.Pillars > 0, Hollow.Pillars);
            t.Snap("hollow_9_pillar");
            await t.Run(16f);
            t.Ok("and crumbles again", Hollow.Pillars == 0);
            // the Answer: one of them turns on its own kind
            Hollow.Learn("answer", false); await t.Run(0.2f); UI.Close();
            var th = Spawn("answer_a", 19); var foe = Spawn("answer_b", 25);
            await t.Run(0.3f);
            Hollow.resonance = 60;
            t.AimAt(th.Cx(), th.Cy());
            await t.Click(); await t.Run(0.2f);
            t.Ok("the Answer is too costly to sing at 60", th.thrallT <= 0 && Hollow.resonance == 60, Hollow.resonance);
            Hollow.resonance = 80; await t.Run(0.4f);
            t.AimAt(th.Cx(), th.Cy());
            await t.Click(); await t.Run(0.2f);
            t.Ok("the Answer turns a body", th.thrallT > 0, th.thrallT);
            for (int i = 0; i < 80 && foe.knots.All(k => !k.broken); i++) { foe.stun = 0; await t.Run(0.1f); }
            t.Snap("hollow_10_answer");
            t.Ok("and it tears at its own kind", foe.knots.Any(k => k.broken));
            th.thrallT = 0.05f; await t.Run(0.3f);
            t.Ok("when the verse runs out it falls apart for good", th.gone);
            // the bell drowns it out
            t.Place("chapel", 20, 16); await t.Run(0.3f);
            Hollow.Hold(true); Hollow.resonance = 60;
            await t.Click(); await t.Run(0.2f);
            t.Ok("nothing answers in a Bell Room", Hollow.resonance == 60);
            // it is kept
            G.Save(); G.LoadGame(); await t.Run(0.3f);
            t.Ok("the Hollow and its verses are saved", Hollow.owned && Hollow.verses.Count == 4, string.Join(",", Hollow.verses));
            G.god = false;
        },

        ["voices"] = async t =>
        {
            // the dormant dead breathe when you are close; true corpses are silent
            World.flags.Set("hollowGift", true);
            t.Place("tithe", 9, 16);
            Snd.Heard.Clear();
            foreach (var b in G.ents.OfType<Body>()) b.voiceT = 0.1f;
            await t.Run(1.5f);
            t.Ok("a dormant Shucker murmurs when Maren is close", Snd.Heard.Contains("shucker:murmur"), string.Join(" ", Snd.Heard.Distinct().Take(8)));
            t.Ok("a true corpse says nothing", !Snd.Heard.Any(k => k.StartsWith("corpse:")));
            // a hunter cries out the moment it sees her, and the strings sting once
            var hall = Data.Room["tithe"];
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            var sp = Props.Empty(); sp.Set("x", hall.Px + 16 * 16); sp.Set("y", hall.Py + 17 * 16); sp.Set("key", "voice_test"); sp.Set("pose", "standing");
            var sh = new Shucker(sp, hall); G.ents.Add(sh);
            Snd.Heard.Clear(); int stings = Voices.Stings; Voices.stingCool = 0;
            G.god = true;
            await t.Run(1.2f);
            t.Ok("it cries out when it sees her", Snd.Heard.Contains("shucker:alert"));
            await t.Run(0.4f);
            t.Ok("and the strings sting", Voices.Stings == stings + 1 && (Snd.Heard.Contains("sting") || Score.Stung.Contains("seen")), Voices.Stings - stings);
            await t.Run(2f);
            t.Ok("its wet footsteps carry", Snd.Heard.Any(k => k.StartsWith("step:")));
            Snd.Heard.Clear();
            sh.DamageKnot(sh.K("kneeL"), 99, "rivet");
            t.Ok("it wails when a limb comes off", Snd.Heard.Contains("shucker:wail") && Snd.Heard.Contains("sever"), string.Join(" ", Snd.Heard));
            sh.DamageKnot(sh.K("kneeR"), 99, "rivet"); sh.DamageKnot(sh.K("crown"), 99, "rivet");
            t.Ok("and rattles when it goes down", Snd.Heard.Contains("shucker:rattle"));
            // every kind of drowned has every line
            foreach (var kind in new[] { "shucker", "bellmouth", "saltwright", "gullet", "grafted" })
            {
                var ks = Props.Empty(); ks.Set("x", hall.Px + 20 * 16); ks.Set("y", hall.Py + 17 * 16); ks.Set("key", "voice_" + kind); ks.Set("anchorY", hall.Py + 4 * 16); ks.Set("pose", "standing");
                var vb = BodyTypes.Make(kind, ks, hall);
                if (vb.mode == "dormant") vb.mode = "hunt";
                Voices.Idle(vb); vb.busyT = 0; Voices.Alert(vb); vb.busyT = 0; Voices.Windup(vb); vb.busyT = 0;
                Voices.Hurt(vb, false); vb.busyT = 0; Voices.Hurt(vb, true); Voices.Die(vb); Voices.Rise(vb);
            }
            t.Ok("every kind of drowned speaks", new[] { "bellmouth:hum", "saltwright:heave", "gullet:click", "grafted:shriek", "bellmouth:toll", "grafted:fall" }.All(k => Snd.Heard.Contains(k)), string.Join(" ", Snd.Heard.Where(k => k.Contains(':')).Distinct()));
            G.god = false;
        },

        // Staged moments for screenshots of the new systems (run with rendering and --out).
        ["showcase"] = async t =>
        {
            World.flags.Set("hollowGift", true);
            Hollow.owned = true; Hollow.verses = new() { "calling", "riptide", "salting", "answer" }; Hollow.held = true;
            G.god = true;
            var kilns = Data.Room["kilns"];
            Shucker Spawn(string key, int tx, int facing = -1)
            {
                var sp = Props.Empty(); sp.Set("x", kilns.Px + tx * 16 + 8); sp.Set("y", kilns.Py + 19 * 16); sp.Set("key", key); sp.Set("pose", "standing"); sp.Set("facing", facing);
                var b = new Shucker(sp, kilns); G.ents.Add(b); return b;
            }
            t.Place("kilns", 14, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            // the Riptide
            var a = Spawn("show_rip", 19);
            await t.Run(0.4f);
            Hollow.verse = "riptide"; Hollow.resonance = 100;
            t.AimAt(a.Cx(), a.Cy());
            await t.Click(); await t.Run(0.12f); t.Snap("show_1_riptide");
            await t.Run(1.5f);
            // the Answer: one turns on the other, singing
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            var th = Spawn("show_th", 19); var foe = Spawn("show_foe", 24);
            await t.Run(0.3f);
            Hollow.verse = "answer"; Hollow.resonance = 100;
            t.AimAt(th.Cx(), th.Cy());
            await t.Click();
            for (int i = 0; i < 60 && foe.knots.Count(k => k.broken) < 2; i++) { foe.stun = 0; await t.Run(0.1f); }
            t.Snap("show_2_answer");
            t.Ok("the answered body tore at the other", foe.knots.Any(k => k.broken));
            // low health: the faceplate crazes and she breathes hard in the helmet
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            G.player.hp = 1; await t.Run(1.8f); t.Snap("show_3_cracked");
            t.Ok("badly hurt, she breathes hard in the helmet", Snd.Heard.Contains("breathTrue"));
            G.player.hp = G.player.maxHp;
            // the journal: the Hollow and its verses
            UI.Satchel("gear"); await t.Run(0.3f); t.Snap("show_4_gear"); UI.Close();
            G.god = false;
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

        // Frame cost in the busiest rooms: CPU time per frame and draw calls, printed for the record.
        ["perf"] = async t =>
        {
            foreach (var (room, x, y) in new[] { ("breakers", 20f, 12f), ("kilns", 20f, 16f), ("cloister", 20f, 16f), ("choir", 20f, 16f), ("abbot", 20f, 16f), ("ribshaft", 10f, 40f) })
            {
                DrawLayer.ByLayer.Clear();
                t.Place(room, x, y);
                await t.Run(0.5f);
                GD.Print($"PERF {room} bake: " + string.Join(", ", DrawLayer.ByLayer.Where(kv => kv.Key.StartsWith("bake")).Select(kv => $"{kv.Key} {kv.Value:0.0} ms")));
                double proc = 0; double calls = 0; int n = 0; DrawLayer.DrawMs = 0; DrawLayer.ByLayer.Clear();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 60; i++)
                {
                    await t.Run(1 / 60f);
                    proc += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
                    calls += Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
                    n++;
                }
                GD.Print($"PERF {room}: painters {DrawLayer.DrawMs / n:0.00} ms/frame, draw calls {calls / n:0}, wall {sw.Elapsed.TotalMilliseconds / n:0.0} ms/frame (software renderer)");
                GD.Print("  " + string.Join(", ", DrawLayer.ByLayer.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value / n:0.00}")));
            }
            // the worst case for gore: a room after a massacre, every piece and stain at its cap
            {
                t.Place("kilns", 15, 18); Settings.Gore = "full";
                var kr = Data.Room["kilns"];
                for (int i = 0; i < 12; i++)
                {
                    var sp = Props.Empty(); sp.Set("x", kr.Px + (17 + i * 1.5f) * 16); sp.Set("y", kr.Py + 19 * 16); sp.Set("key", "perf" + i); sp.Set("pose", "standing");
                    var b = new Shucker(sp, kr); G.ents.Add(b);
                    foreach (var k in b.knots.Where(k => k.type != "crown")) b.DamageKnot(k, 99, "rivet");
                }
                for (int i = 0; i < 40; i++) { Gore.Spray(kr.Px + H.Rand(200, 500), kr.Py + H.Rand(200, 290), -H.PI / 2, 1.5f, 20, 40, 200); await t.Run(0.05f); }
                double before = DrawLayer.DrawMs; int frames = 60; DrawLayer.ByLayer.Clear();
                for (int i = 0; i < frames; i++) { Gore.Spray(kr.Px + 350, kr.Py + 260, -H.PI / 2, 1.5f, 12, 40, 200); await t.Run(1 / 60f); }
                GD.Print($"PERF gore: painters {(DrawLayer.DrawMs - before) / frames:0.00} ms/frame with {Gore.Stains("kilns").Count} stains, {Gore.gibs.Count} pieces, {Gore.DropCount} drops");
                GD.Print("  " + string.Join(", ", DrawLayer.ByLayer.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value / frames:0.00}")));
            }
            t.Ok("perf probe ran", true);
        },

        ["storyfocus"] = async t =>
        {
            // every monument finds somewhere to stand
            foreach (var d in Monuments.DEFS)
                t.Ok($"the {d.id} monument stands in {d.room}", Monuments.For(Data.Room[d.room]) != null);
            // stop beside the Abbot in the Kilns: the camera leans in, the background softens, the light warms, she thinks
            World.flags.Set("hollowGift", true);
            var kr = Data.Room["kilns"];
            var m = Monuments.For(kr);
            t.Place("kilns", (m.x - kr.Px) / 16 - 2, (m.floor - kr.Py) / 16 - 1);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            G.player.invulnT = 0;
            var lm = LightingManager.Inst;
            await t.Run(0.2f);
            Vector2 cam0 = G.cam;
            await t.Run(3.5f);
            t.Ok("the Abbot is what she is looking at", StoryFocus.Active?.id == "abbot", StoryFocus.Active?.id);
            t.Ok("the focus rises while she stands still", StoryFocus.W > 0.6f, StoryFocus.W);
            // the Kilns is one screen, so the camera cannot travel: the frame leans in instead
            t.Ok("the camera leans toward him", StoryFocus.CamOffset.Length() > 6 && G.leanZoom > 1.04f, $"{StoryFocus.CamOffset} lean {G.leanZoom:0.000}");
            var aimAt = G.ScreenToWorld(G.WorldToScreen(m.x, m.Cy).X, G.WorldToScreen(m.x, m.Cy).Y);
            t.Ok("aiming still lands where the cursor is", (aimAt - new Vector2(m.x, m.Cy)).Length() < 0.01f, aimAt);
            t.Ok("and follows more loosely", StoryFocus.Damping < 0.8f, StoryFocus.Damping);
            t.Ok("the background softens", lm.FocusBlur > 0.8f && lm.FarBlur > lm.Cur.farBlur + 0.8f, lm.FocusBlur);
            t.Ok("the frame closes in a little", lm.FocusVignette > 0.08f, lm.FocusVignette);
            t.Ok("the ambient leans toward his colour", lm.FocusW > 0.3f && lm.FocusTint.R > lm.FocusTint.B, lm.FocusW);
            t.Ok("a soft light comes up on him", G.frameLights.Any(l => !l.player && MathF.Abs(l.x - m.x) < 1 && l.i > 0.2f));
            t.Ok("she has a thought about him, once", G.narr.thoughts.Contains("mon_abbot"));
            t.Snap("focus_1_abbot");
            // walk away and it lets go
            G.player.x = kr.Px + 34 * 16; G.player.vx = 0;
            await t.Run(2.5f);
            t.Ok("walking away lets him go", StoryFocus.Active?.id != "abbot" || StoryFocus.W < 0.1f, $"{StoryFocus.Active?.id} {StoryFocus.W:0.00}");
            await t.Run(2f);
            t.Ok("and an unread carving nearby takes its place, more softly", StoryFocus.Active?.id == "carving" && StoryFocus.W > 0.3f && StoryFocus.W < 0.75f, $"{StoryFocus.Active?.id} {StoryFocus.W:0.00}");
            // a fight takes it away at once
            t.Place("kilns", (m.x - kr.Px) / 16 - 2, (m.floor - kr.Py) / 16 - 1);
            await t.Run(3f);
            float before = StoryFocus.W;
            var sp = Props.Empty(); sp.Set("x", G.player.Cx() + 90); sp.Set("y", m.floor); sp.Set("key", "focus_threat"); sp.Set("pose", "standing");
            var foe = new Shucker(sp, kr); foe.mode = "hunt"; G.ents.Add(foe);
            await t.Run(0.6f);
            t.Ok("a hostile nearby lets it all go, fast", before > 0.5f && StoryFocus.W < 0.15f && StoryFocus.Calm < 0.2f, $"{before:0.00} -> {StoryFocus.W:0.00} calm {StoryFocus.Calm:0.00}");
            t.Ok("and the camera stops leaning", StoryFocus.CamOffset.Length() < 4, StoryFocus.CamOffset);
            foe.gone = true;
            // crossing into another region blends the light rather than snapping it
            t.Place("cloister", 8, 16);
            await t.Run(0.05f);
            float b0 = lm.Blend; var amb0 = lm.Cur.ambient;
            t.Ok("the new region's light starts blending in", lm.Region == "cloister" && b0 < 0.3f, $"{lm.Region} {b0:0.00}");
            var want = LightingManager.MOODS["cloister"].ambient; var was = LightingManager.MOODS["saltworks"].ambient;
            t.Ok("partway between the two moods", amb0.G > MathF.Min(want.G, was.G) - 0.001f && amb0.G < MathF.Max(want.G, was.G) + 0.001f && amb0 != want, amb0);
            await t.Run(3.2f);
            t.Ok("and settles on the Cloister's", lm.Blend >= 1 && lm.Cur.ambient.IsEqualApprox(want), lm.Cur.ambient);
            t.Snap("focus_2_cloister");
        },

        ["monuments"] = async t =>
        {
            // a tour: stand beside each monument a while and look
            World.flags.Set("hollowGift", true);
            G.god = true; G.player.abilities.Add("seal");   // the Gullet's is under deep water
            foreach (var d in Monuments.DEFS)
            {
                var r = Data.Room[d.room]; var m = Monuments.For(r);
                t.Place(d.room, (m.x - r.Px) / 16 - 3, (m.floor - r.Py) / 16 - 1);
                foreach (var b in G.ents.OfType<Body>()) b.gone = true;
                G.player.invulnT = 0; G.player.facing = 1;
                // the Gullet's driftlings swarm a lit lamp: shutter it, and they leave her to look in peace
                G.player.shut = d.id == "swallowed";
                await t.Run(3.5f);
                t.Ok($"lingering by the {d.id} brings the focus up", StoryFocus.Active?.id == d.id && StoryFocus.W > 0.5f,
                    $"{StoryFocus.Active?.id} {StoryFocus.W:0.00} calm {StoryFocus.Calm:0.00} inv {G.player.invulnT:0.00} boss {G.bossBar?.type} hp {G.player.hp} near {G.ents.OfType<Body>().Where(b => !b.gone && b.Alive()).Select(b => H.Dist(b.Cx(), b.Cy(), G.player.Cx(), G.player.Cy())).DefaultIfEmpty(-1).Min():0} lit {G.player.LanternLit()} at {(G.player.Cx() - r.Px) / 16:0},{(G.player.Cy() - r.Py) / 16:0} mon {(m.x - r.Px) / 16:0},{(m.floor - r.Py) / 16:0}");
                t.Snap("monument_" + d.id);
            }
        },

        ["audio"] = async t =>
        {
            // the score follows where she is and how things stand
            World.flags.Set("hollowGift", true);
            t.Ok("the score is installed", Score.Active);
            t.Place("kilns", 15, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            G.player.invulnT = 0;
            await t.Run(4.5f);
            t.Ok("the Saltworks bed plays in the Kilns", Score.Bed == "saltworks" && Score.Target("score/bed_saltworks") > 0.9f && Score.Level("score/bed_saltworks") > 0.9f, $"{Score.Bed} {Score.Level("score/bed_saltworks"):0.00}");
            t.Ok("with the room's own ambience under it", Score.Level("amb/saltworks") > 0.8f);
            t.Ok("and no chase while nothing hunts her", Score.Target("score/chase") == 0);
            // something sees her and comes
            var kr = Data.Room["kilns"];
            var sp = Props.Empty(); sp.Set("x", G.player.Cx() + 70); sp.Set("y", G.player.y + G.player.h); sp.Set("key", "audio_hunter"); sp.Set("pose", "standing");
            var foe = new Shucker(sp, kr); foe.mode = "hunt"; foe.spotted = true; G.ents.Add(foe);
            await t.Run(1.5f);
            t.Ok("a hunter close by brings the chase in", Score.Target("score/chase") == 1 && Score.Level("score/chase") > 0.5f, Score.Level("score/chase"));
            t.Ok("and the dread under it", Score.Target("score/dread") > 0.5f, Score.Target("score/dread"));
            t.Ok("the region's bed steps back for it", Score.Target("score/bed_saltworks") < 0.7f);
            foe.gone = true;
            await t.Run(10.5f);
            t.Ok("when it is gone the chase lets go", Score.Target("score/chase") == 0 && Score.Level("score/chase") < 0.2f, Score.Level("score/chase"));
            // a Bell Room
            t.Place("saltbell", 20, 18); await t.Run(3.5f);
            t.Ok("a Bell Room plays the sanctuary", G.player.inBellRoom && Score.Target("score/sanctuary") == 1 && Score.Level("score/sanctuary") > 0.8f, $"{G.player.inBellRoom} {Score.Level("score/sanctuary"):0.00}");
            t.Ok("and the dread cannot follow her in", Score.Target("score/dread") == 0);
            // another region
            t.Place("cloister", 8, 16); await t.Run(4.5f);
            t.Ok("the Cloister has its own bed", Score.Bed == "cloister" && Score.Level("score/bed_cloister") > 0.9f && Score.Level("score/bed_saltworks") < 0.05f, $"{Score.Bed}");
            // the dread director: quiet, alone, it makes sounds; never in a fight
            Snd.Heard.Clear(); int before = Dread.Played;
            foreach (var kind in new[] { "knock", "scratch", "whisper", "steps", "drag", "scream", "breath", "chain", "child", "laugh" }) Dread.Play(kind);
            t.Ok("the dark has its sounds", new[] { "dread:knock", "dread:scratch", "dread:whisper", "dread:steps", "dread:drag", "dread:scream", "dread:breath", "dread:chain", "dread:child", "dread:laugh" }.All(k => Snd.Heard.Any(h => h.StartsWith(k))), string.Join(" ", Snd.Heard.Where(h => h.StartsWith("dread")).Distinct()));
            Score.Stung.Clear(); Dread.Play("slam");
            t.Ok("the big one comes with a scare and her gasp", Score.Stung.Contains("scare"));
            await t.Run(0.3f);
            t.Ok("she gasps", Snd.Heard.Contains("gasp"));
            int played = Dread.Played;
            var sp2 = Props.Empty(); sp2.Set("x", G.player.Cx() + 60); sp2.Set("y", G.player.y + G.player.h); sp2.Set("key", "audio_hunter2"); sp2.Set("pose", "standing");
            var foe2 = new Shucker(sp2, Data.Room["cloister"]); foe2.mode = "hunt"; G.ents.Add(foe2);
            for (int i = 0; i < 30; i++) { Dread.Update(5); await t.Run(0.02f); }
            t.Ok("never while something is coming for her", Dread.Played == played, Dread.Played - played);
            foe2.gone = true; G.player.invulnT = 0; await t.Run(0.2f);
            for (int i = 0; i < 40 && Dread.Played == played; i++) { Dread.Update(10); await t.Run(0.02f); }
            t.Ok("but alone in the quiet, it comes", Dread.Played > played, Dread.Last);
            // her body: footsteps of what is under her, the heart at the last pip, choking under water
            Snd.Heard.Clear();
            await t.Hold("right", 0.6f);
            t.Ok("her boots sound of the floor", Snd.Heard.Any(h => h.StartsWith("foot:")), string.Join(" ", Snd.Heard.Where(h => h.StartsWith("foot")).Distinct()));
            string uiWas = UI.Kind; UI.Close(); await t.Run(0.1f);
            G.player.hp = 1; Snd.Heard.Clear(); await t.Run(2f);
            t.Ok("at the last pip she hears her heart", Snd.Heard.Contains("heartTrue"), $"{G.mode} ui {uiWas}/{UI.IsOpen()} dead {G.player.dead} hp {G.player.hp}: " + string.Join(" ", Snd.Heard.Distinct().Take(12)));
            G.player.hp = G.player.maxHp;
            // the moments
            Score.Stung.Clear();
            Tide.AdvanceTo("rising"); await t.Run(0.5f);
            t.Ok("the tide turning toward High is marked", Score.Stung.Contains("turn"), string.Join(",", Score.Stung));
            // the title screen has its own music
            G.ToTitle(); await t.Run(4.5f);
            t.Ok("the title plays its theme", Score.Target("score/title") == 1 && Score.Level("score/title") > 0.9f && Score.Bed == null, Score.Level("score/title"));
        },

        ["menus"] = async t =>
        {
            G.ToTitle(); await t.Run(0.3f); t.Snap("ui_title");
            t.Ok("title menu", UI.Kind == "title", UI.Kind);
            await t.ClickButton("Settings"); await t.Run(0.2f); t.Snap("ui_settings");
            t.Ok("settings from the title", UI.Kind == "settings", UI.Kind);
            await t.ClickButton("Back");
            await t.ClickButton("Controls"); await t.Run(0.2f); t.Snap("ui_controls");
            await t.ClickButton("Back");
            await t.ClickButton("Row out"); await t.Run(0.2f); t.Snap("ui_voyage"); await t.ClickButton("Cast off"); await t.Run(1.5f); t.Snap("ui_opening");
            t.Ok("opening cinematic", UI.Kind == "cinematic", UI.Kind);
            await t.Tap("pause"); await t.Run(0.5f);
            G.player.bag.Add("scrap", 6); G.player.bag.Add("oil", 2); G.player.bag.Add("kelp", 3);
            G.player.bag.Add("poultice", 2); G.player.bag.Add("salt", 2); G.player.bag.Add("crank", 1); G.player.hp = 1;
            UI.Satchel(); await t.Run(0.3f); t.Snap("ui_satchel");
            t.Ok("the satchel shows her condition", UI.Inst.AllText().Contains("Bleeding out"), UI.Kind);
            await t.ClickButton("Examine"); await t.Run(0.3f); t.Snap("ui_examine");
            t.Ok("an item can be examined", UI.Kind == "examine" && UI.Inst.AllText().Contains(Data.Item("rivets").S("examine")), UI.Kind);
            await t.ClickButton("Back"); await t.Run(0.1f);
            t.Ok("and put back", UI.Kind == "satchel", UI.Kind);
            G.player.hp = G.player.maxHp;
            UI.Satchel("craft"); await t.Run(0.3f); t.Snap("ui_craft");
            t.Ok("crafting shows what a recipe needs against what she has", UI.Inst.AllText().Contains("Needs") && UI.Inst.AllText().Contains("On hand"), UI.Kind);
            UI.Satchel("gear"); await t.Run(0.2f); t.Snap("ui_gear");
            UI.Satchel("log"); await t.Run(0.2f); t.Snap("ui_journal");
            UI.Close(); UI.Map(); await t.Run(0.2f); t.Snap("ui_map");
            UI.Close(); UI.Pause(); await t.Run(0.2f); t.Snap("ui_pause");
            await t.ClickButton("Quit to title"); await t.Run(0.2f); t.Snap("ui_confirm");
            t.Ok("quitting asks first", UI.Kind == "confirm", UI.Kind);
            UI.Inst.TestBack(); await t.Run(0.1f);
            t.Ok("and no goes back to the pause menu", UI.Kind == "pause", UI.Kind);
            await t.ClickButton("Settings"); await t.Run(0.2f);
            t.Ok("settings from the pause menu", UI.Kind == "settings", UI.Kind);
            UI.Close();
            t.Place("chapel", 20, 16); G.player.bag.Add("oil", 1);
            await t.Tap("interact"); await t.Run(0.2f); t.Snap("ui_bell");
            await t.ClickButton("Bench"); await t.Run(0.2f); t.Snap("ui_bench");
            UI.Inst.PadButton(JoyButton.RightShoulder); await t.Run(0.2f); t.Snap("ui_bench_upgrade");
            t.Ok("the bench's upgrade page", UI.Inst.AllText().Contains("Upgrade"), UI.Kind);
            UI.Inst.PadButton(JoyButton.RightShoulder); await t.Run(0.2f); t.Snap("ui_bench_fittings");
            UI.Inst.PadButton(JoyButton.LeftShoulder); UI.Inst.PadButton(JoyButton.LeftShoulder);
            UI.Close(); UI.ReliquaryMenu(); await t.Run(0.2f); t.Snap("ui_reliquary");
            int chestBefore = G.reliquary.Count("salt");
            await t.ClickButton("Put all in the chest"); await t.Run(0.1f);
            t.Ok("the reliquary moves a stack across", G.reliquary.List().Count > 0, chestBefore);
            UI.Close();
            t.Place("chapel", 9, 16); await t.Tap("interact"); await t.Run(0.3f); t.Snap("ui_dialogue");
            UI.Close();
            t.Place("seawall", 15, 16);
            var cv = First<Carving>(); var p = G.player; p.x = cv.Cx() - 5; p.y = cv.y + 22 - p.h;
            await t.Run(0.1f); await t.Tap("interact"); await t.Run(0.8f); t.Snap("ui_inscription");
            UI.Close();
            G.player.Hurt(99); await t.Run(1.3f); t.Snap("ui_death");
            t.Ok("death screen", UI.Kind == "death", UI.Kind);
            UI.Ending("The Keeper's Dark", new List<string> { "The lamp goes out, and stays out.", "Three of them row away at dawn." }, "Knots broken 212 · Deaths 4 · 3:12:40", new List<string> { "Odile keeps the ferry." });
            UI.Inst.TestCancel(); await t.Run(0.3f); t.Snap("ui_ending");
            t.Ok("the ending's epitaph", UI.Kind == "ending", UI.Kind);
        },

        ["climb"] = async t =>
        {
            // Climbing as a PlayStation player tries it: cross to climb, a half push of the stick, a stick that is
            // never quite straight, and ledges a jump only just reaches.
            var p = G.player;
            Pad.Simulate(PadKind.PlayStation);
            async Task Foot() { t.PadReset(); p.state = "normal"; t.Place("reef", 22, 12); await t.Run(0.3f); }
            await Foot();
            float y0 = p.y;
            t.PadButton(JoyButton.A, true); await t.Run(0.8f); t.PadButton(JoyButton.A, false); await t.Run(0.05f);
            t.Ok("cross at the foot of a ladder climbs it", p.state == "ladder" && p.y < y0 - 50, $"{p.state} {y0} -> {p.y}");
            await Foot(); y0 = p.y;
            t.PadAxis(JoyAxis.LeftY, -0.5f); await t.Run(0.8f);
            t.Ok("half a push up the stick climbs", p.state == "ladder" && p.y < y0 - 50, $"{p.state} {y0} -> {p.y}");
            y0 = p.y;
            t.PadAxis(JoyAxis.LeftX, 0.45f); t.PadAxis(JoyAxis.LeftY, -0.55f); await t.Run(0.5f);
            t.Ok("a stick leaning sideways keeps her climbing", p.state == "ladder" && p.y < y0 - 25, $"{p.state} {y0} -> {p.y}");
            t.PadAxis(JoyAxis.LeftY, 0); t.PadAxis(JoyAxis.LeftX, 1); await t.Run(0.25f);
            t.Ok("a firm push sideways lets go", p.state != "ladder", p.state);
            await Foot();
            t.PadAxis(JoyAxis.LeftY, -1); await t.Run(0.5f); t.PadAxis(JoyAxis.LeftY, 0); await t.Run(0.1f);
            bool was = p.state == "ladder";
            t.PadAxis(JoyAxis.LeftX, 1); await t.PadTap(JoyButton.A); await t.Run(0.05f);
            t.Ok("cross and a direction leap off the ladder", was && p.state != "ladder" && p.vx > 50, $"{was} {p.state} {p.vx}");
            t.PadReset(); await t.Run(1.2f);
            await Foot(); float x0 = p.x;
            t.PadAxis(JoyAxis.LeftX, 1); await t.PadTap(JoyButton.A); await t.Run(0.4f);
            t.Ok("a running jump past a ladder carries on", p.state != "ladder" && p.x > x0 + 20, $"{p.state} {x0} -> {p.x}");
            t.PadReset(); await t.Run(1.0f);
            p.state = "normal"; t.Place("reef", 22, 7); p.vy = 120; t.PadButton(JoyButton.A, true); await t.Run(0.15f);
            t.Ok("cross held catches a ladder in the air", p.state == "ladder", p.state);
            t.PadButton(JoyButton.A, false); t.PadReset(); await t.Run(0.05f);
            p.state = "normal"; t.Place("reef", 22, 7); p.vy = 120; y0 = p.y; await t.Run(0.3f);
            t.Ok("with nothing held she falls past it", p.state != "ladder" && p.y > y0 + 30, $"{p.state} {y0} -> {p.y}");
            await Foot();
            t.PadAxis(JoyAxis.LeftY, -1); await t.Run(3.5f); t.PadAxis(JoyAxis.LeftY, 0); await t.Run(0.2f);
            var top = t.State();
            t.Ok("at the top she steps off onto it", top.st == "normal" && top.ground && top.ty <= 4, top);
            Pad.EndSimulate(); t.PadReset();
            await Foot(); y0 = p.y;
            t.Down("jump"); await t.Run(0.6f); t.Release("jump"); await t.Run(0.05f);
            t.Ok("space at the foot of a ladder climbs it too", p.state == "ladder" && p.y < y0 - 40, $"{p.state} {y0} -> {p.y}");
            p.state = "normal";
            // ledges: a jump that falls just short catches the lip and pulls up onto it (the cloister's pits drained
            // for the test: at Low tide the sea stands in them)
            float spring = Tide.spring; Tide.spring = -8;
            t.Place("cloister", 10, 19); await t.Run(0.4f);
            float lip = G.room.Py + 17 * 16;
            p.x = G.room.Px + 10 * 16 + 1; p.y = lip + 8 - p.h; p.vy = 60; p.vx = 0; p.onGround = false;
            t.Down("left"); await t.Run(0.6f); t.Release("left"); await t.Run(0.1f);
            var s = t.State();
            t.Ok("a jump that falls just short catches the ledge and climbs it", s.ground && s.ty == 16, s);
            // a wall higher than a jump can clear stays a wall: no catch for a lip well above where she left the floor
            t.Place("cloister", 10, 19); await t.Run(0.4f);
            p.takeoffY = lip + 64;
            p.x = G.room.Px + 10 * 16 + 1; p.y = lip + 8 - p.h; p.vy = 60; p.vx = 0; p.onGround = false;
            t.Down("left"); await t.Run(0.6f); t.Release("left"); await t.Run(0.1f);
            s = t.State();
            t.Ok("a wall taller than a jump is not climbed", s.ty == 19, s);
            // holding down lets her drop past a ledge
            t.Place("cloister", 10, 19); await t.Run(0.4f);
            p.x = G.room.Px + 10 * 16 + 1; p.y = lip + 8 - p.h; p.vy = 60; p.vx = 0; p.onGround = false;
            t.Down("left", "down"); await t.Run(0.6f); t.Release("left", "down"); await t.Run(0.1f);
            t.Ok("holding down drops past a ledge", t.State().ty == 19, t.State());
            // with real input: every running jump at the pit's 3-tile wall gets out, however late or early the jump
            int made = 0, tries = 0; var misses = new List<string>();
            foreach (int start in new[] { 11, 12, 13, 14 })
                foreach (float delay in new[] { 0f, 0.05f, 0.1f, 0.15f, 0.2f })
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        // the pit holds one of Agathe's pages: its reading (and Maren's thought on it) pauses the game,
                        // so an attempt it interrupts is run again
                        p.state = "normal"; t.Place("cloister", start, 19); await t.Run(0.25f);
                        while (UI.IsOpen()) { UI.Close(); await t.Run(0.3f); }
                        t.Down("left"); await t.Run(delay); await t.Hold("jump", 0.35f); await t.Run(0.6f); t.Release("left"); await t.Run(0.1f);
                        if (UI.IsOpen()) continue;
                        tries++; if (t.State().ty == 16) made++; else misses.Add($"{start}/{delay}");
                        break;
                    }
            t.Ok("every running jump at a 3-tile ledge makes it", made == tries && tries == 20, $"{made}/{tries} missed {string.Join(" ", misses)}");
            // and a 4-tile stone wall is still a wall, however she jumps at it (the breakers' shaft, drained)
            bool escaped = false;
            foreach (float delay in new[] { 0f, 0.1f, 0.2f })
            {
                p.state = "normal"; t.Place("breakers", 50, 20); await t.Run(0.3f);
                t.Down("left"); await t.Run(delay);
                for (int i = 0; i < 4; i++) { await t.Hold("jump", 0.4f); await t.Run(0.5f); }
                t.Release("left"); await t.Run(0.1f);
                if (t.State().ty < 17) escaped = true;
            }
            t.Ok("a 4-tile stone wall cannot be climbed by jumping", !escaped, t.State());
            Tide.spring = spring;
        },

        ["qol"] = async t =>
        {
            // the smaller frustrations, fixed
            var p = G.player;
            Pad.Simulate(PadKind.PlayStation); t.PadReset();
            // a light tilt of the stick sneaks on the ground but does not shorten a jump
            t.Place("reef", 27, 12); await t.Run(0.3f);
            t.PadAxis(JoyAxis.LeftX, 0.45f); await t.Run(0.3f);
            float x0 = p.x;
            t.PadButton(JoyButton.A, true); await t.Run(0.35f); t.PadButton(JoyButton.A, false); await t.Run(0.25f);
            t.Ok("a light tilt carries a jump at full stride", p.x - x0 > 70, $"{p.x - x0:0} px in 0.6 s");
            t.PadReset(); await t.Run(0.5f);
            // triangle opens a locker on the press, not the release, when there is nothing to burn
            t.Place("reef", 13, 12);
            var chest = First<Chest>(); p.x = chest.Cx() - p.w / 2; await t.Run(0.1f);
            t.PadButton(JoyButton.Y, true); await t.Run(0.05f);
            t.Ok("triangle acts on the press", chest.done, chest.done);
            t.PadButton(JoyButton.Y, false); await t.Run(0.1f);
            while (UI.IsOpen()) { UI.Close(); await t.Run(0.1f); }
            // the d-pad climbs while she is on a ladder, and does not spend a poultice
            p.state = "normal"; t.Place("reef", 22, 12); await t.Run(0.3f);
            t.PadAxis(JoyAxis.LeftY, -1); await t.Run(0.2f); t.PadAxis(JoyAxis.LeftY, 0); await t.Run(0.05f);
            int poultices = p.bag.Count("poultice"); p.hp = p.maxHp - 1;
            float y0 = p.y;
            t.PadButton(JoyButton.DpadUp, true); await t.Run(0.5f); t.PadButton(JoyButton.DpadUp, false); await t.Run(0.1f);
            t.Ok("d-pad up climbs on a ladder", p.state == "ladder" && p.y < y0 - 30 && p.bag.Count("poultice") == poultices && p.healT <= 0, $"{p.state} {y0} -> {p.y} poultices {poultices} -> {p.bag.Count("poultice")}");
            // and d-pad down climbs to the foot without toggling the boots as she steps off
            bool hadBoots = p.abilities.Contains("boots"); p.abilities.Add("boots"); p.boots = false;
            t.PadButton(JoyButton.DpadDown, true); await t.Run(1.2f); t.PadButton(JoyButton.DpadDown, false); await t.Run(0.1f);
            t.Ok("d-pad down climbs off the foot of a ladder and leaves the boots alone", p.state == "normal" && p.onGround && !p.boots, $"{p.state} ground={p.onGround} boots={p.boots}");
            if (!hadBoots) p.abilities.Remove("boots");
            p.hp = p.maxHp; p.state = "normal";
            // swimming follows the stick, not just its eight directions
            t.Place("cloister", 13, 19); await t.Run(0.4f);
            t.PadAxis(JoyAxis.LeftX, 0.9f); t.PadAxis(JoyAxis.LeftY, -0.3f); await t.Run(0.22f);
            t.Ok("a stick a little above level swims a little upward", p.InWater() && p.vx > 60 && p.vy < -10, $"water={p.InWater()} vx={p.vx:0} vy={p.vy:0}");
            t.PadReset(); await t.Run(0.2f);
            Pad.EndSimulate(); t.PadReset();
            // standing still and holding down looks down (in the ribshaft, two screens tall)
            t.Place("ribshaft", 22, 20); await t.Run(0.8f);
            float cy = G.cam.Y;
            t.Down("down"); await t.Run(1.0f);
            float looked = G.cam.Y - cy;
            t.Release("down"); await t.Run(0.8f);
            t.Ok("holding down while still looks down, and lets go", looked > 40 && MathF.Abs(G.cam.Y - cy) < 10, $"{looked:0} then {G.cam.Y - cy:0}");
            // a jump that clips the corner of a ceiling slides past it into the gap
            t.Place("cloister", 17, 3); await t.Run(0.3f);
            p.x = G.room.Px + 18 * 16 - 3; p.y = G.room.Py + 2 * 16 + 1; p.vy = -320; p.onGround = false;
            await t.Run(0.12f);
            t.Ok("a jump clipping a ceiling corner slips past it", p.y < G.room.Py + 2 * 16 - 4, $"y {p.y - G.room.Py:0}");
        },

        ["difficulty"] = async t =>
        {
            // every crossing, from the table out to the drowned: rivets to put a Shucker down, Maren's pips, how hard a
            // blow lands, and on the gentlest, her breath coming back and her satchel staying with her
            async Task Begin(string id)
            {
                G.NewGame(id, 1); await t.Run(0.2f);
                for (int i = 0; i < 20 && UI.IsOpen(); i++) { if (UI.Kind == "death") await t.ClickButton("Wake at the bell"); else UI.Inst.TestCancel(); await t.Run(0.1f); }
            }
            var expect = new Dictionary<string, int> { ["pilgrim"] = 3, ["keeper"] = 7, ["drowned"] = 9, ["saint"] = 10 };
            foreach (var c in Diff.ALL)
            {
                await Begin(c.id);
                var p = G.player;
                t.Ok($"{c.name}: Maren starts with {c.pips} pips", p.maxHp == c.pips && p.hp == c.pips, $"{p.hp}/{p.maxHp}");
                G.god = true; t.Place("tithe", 20, 16); await t.Run(0.2f);
                var sh = G.ents.OfType<Shucker>().FirstOrDefault(b => b.Alive());
                int shots = 0;
                while (sh != null && sh.Alive() && shots < 40)
                {
                    var crown = sh.K("crown");
                    var k = sh.IsSealed(crown) ? sh.knots.First(x => !x.broken && x.type != "crown") : crown;
                    sh.DamageKnot(k, 2, "rivet"); shots++;
                }
                t.Ok($"{c.name}: a Shucker falls to {expect[c.id]} rivets", sh != null && !sh.Alive() && shots == expect[c.id] && shots == Diff.ShuckerRivets(c), $"{shots} (table says {Diff.ShuckerRivets(c)})");
                G.god = false; p.invulnT = 0; int hp0 = p.hp;
                p.Hurt(2, null);
                int lost = hp0 - p.hp;
                t.Ok($"{c.name}: a heavy blow costs {(c.softHeavy ? 1 : 2)}{(c.extraHit > 0 ? " or 3" : "")}", c.softHeavy ? lost == 1 : lost == 2 || (c.extraHit > 0 && lost == 3), lost);
            }
            // Pilgrim: a pip back while nothing hunts her, and the satchel kept on death
            await Begin("pilgrim");
            var pp = G.player;
            float every = Diff.Get("pilgrim").regen; Diff.Get("pilgrim").regen = 0.5f;
            t.Place("reef", 28, 12); await t.Run(0.2f);
            pp.hp = pp.maxHp - 2; await t.Run(1.3f);
            Diff.Get("pilgrim").regen = every;
            t.Ok("Pilgrim: she heals while nothing hunts her", pp.hp == pp.maxHp, $"{pp.hp}/{pp.maxHp}");
            pp.bag.Add("kelp", 3); int kelp = pp.bag.Count("kelp");
            pp.invulnT = 0; pp.Hurt(99); await t.Run(1.4f);
            t.Ok("Pilgrim: dying keeps the satchel", UI.Kind == "death" && pp.bag.Count("kelp") == kelp && G.satchelDrop == null, $"{UI.Kind} kelp {pp.bag.Count("kelp")}");
            await t.ClickButton("Wake at the bell"); await t.Run(0.3f);
            // the fourth crossing sleeps until a night is finished
            Profile.finished.Clear(); Profile.endings.Clear();
            G.ToTitle(); await t.Run(0.3f);
            await t.ClickButton("Row out"); await t.Run(0.2f);
            t.Ok("the Mere-Saint crossing is asleep at first", UI.Inst.AllText().Contains("Asleep") && !UI.Inst.AllText().Contains("Mere-Saint"), UI.Kind);
            Profile.Record("keeper", "dark");
            t.Ok("finishing a night wakes it", Profile.SaintUnlocked && Profile.JustUnlocked);
            UI.Inst.Voyage(); await t.Run(0.2f);
            await t.ClickButton("Mere-Saint"); await t.Run(0.2f); t.Snap("diff_crossing");
            t.Ok("and it can be chosen", UI.Inst.AllText().Contains("The Saint is awake"), UI.Kind);
            await t.ClickButton("Cast off"); await t.Run(0.3f);
            t.Ok("the night begins on the Mere-Saint crossing", G.difficulty == "saint" && Diff.Saint, G.difficulty);
            for (int i = 0; i < 20 && UI.IsOpen(); i++) { UI.Inst.TestCancel(); await t.Run(0.1f); }
            // the seventh verse, one line in the margin of each page
            var lull = Data.Lore.P("saint").Strings("lullaby");
            t.Ok("on this crossing Agathe's pages carry the lullaby", Story.PageText(3).Contains(lull[2]) && Story.PageText(9).Contains("Hush now, my Maren"));
            // the true ending: all nine pages, the Hollow, and the music box at the lamp
            G.god = true; G.act = 3; World.flags.Set("skullOpen", true); Hollow.owned = true;
            for (int i = 1; i <= 8; i++) G.pages.Add(i);
            t.Place("lamp", 16, 16); await t.Run(0.2f);
            t.Ok("with eight pages there is no music box", !G.ents.OfType<MusicBox>().Any());
            G.pages.Add(9); t.Place("reef", 28, 12); await t.Run(0.1f); t.Place("lamp", 20, 16); await t.Run(0.3f);
            var box = G.ents.OfType<MusicBox>().FirstOrDefault();
            t.Ok("with all nine, Agathe's music box stands by the lamp", box != null);
            if (box != null) { G.player.x = box.Cx() - G.player.w / 2; await t.Run(0.1f); }
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("it asks before the verse is sung", UI.Kind == "confirm", UI.Kind);
            await t.ClickButton("Yes"); await t.Run(0.3f);
            t.Ok("the seventh verse plays out", UI.Kind == "cinematic" && G.ended == "verse" && Profile.SawTrueEnding, $"{UI.Kind} {G.ended}");
            UI.Inst.TestCancel(); await t.Run(0.4f); t.Snap("diff_true_ending");
            t.Ok("its epitaph", UI.Kind == "ending" && UI.Inst.AllText().Contains("The Seventh Verse") && UI.Inst.AllText().Contains("first verse"), UI.Kind);
            await t.ClickButton("Turn the page"); await t.Run(0.6f); t.Snap("diff_glimpse_text");
            t.Ok("then a glimpse of what comes after", UI.Kind == "cinematic", UI.Kind);
            UI.Inst.TestCancel(); await t.Run(0.6f); t.Snap("diff_glimpse_title");
            t.Ok("and the title of the next part", UI.Kind == "glimpse" && UI.Inst.AllText().Contains("THE DEEP HOURS"), UI.Kind);
            // the other crossings never show the box or the margins
            await Begin("keeper");
            for (int i = 1; i <= 9; i++) G.pages.Add(i);
            G.act = 3; World.flags.Set("skullOpen", true);
            t.Place("lamp", 20, 16); await t.Run(0.2f);
            t.Ok("on Keeper the margins are blank and there is no box", !Story.PageText(3).Contains(lull[2]) && !G.ents.OfType<MusicBox>().Any());
            G.god = false; Profile.finished.Clear(); Profile.endings.Clear();
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
            // A jumps (away from the ladder: at one, A climbs)
            t.Place("reef", 28, 12); await t.Run(0.3f);
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
            await t.Run(4.2f); p.hp = 2;
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
        // She is the prey: the drowned close on her, she has little grace after a hit, and the Hollow is a last breath.
        // The crank, the whole way round, with real inputs only: the seawall tower is shut, so down through the Tithe
        // Gallery into the hall, the crank from the altar, up with the flood, back through the gallery, the socket,
        // and up the tower. Then: someone who reached the Saltworks some other way can always get back down.
        // The island's seams: every opening at a room's edge meets an opening in the room beside it, every ladder that
        // runs off a room's edge carries on into the next, and nothing is placed inside a wall.
        // The guide: the main story's next step follows what she has done, points the way through the rooms, and is
        // written up in the journal and on the chart.
        // The Evaporation Pans with real inputs: the levers in the plaque's order, a sprint through the closing hatch,
        // onto the barge, up with the water, and off at the top into the Kilns.
        ["pans"] = async t =>
        {
            async Task<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase)> Until(string[] keys, Func<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase), bool> cond, float secs = 12)
            {
                t.Down(keys); var s = t.State();
                for (int f = 0; f < secs * 60; f += 3) { s = t.State(); if (cond(s)) break; await t.Run(0.05f); }
                t.Release(keys); await t.Run(0.05f); return t.State();
            }
            G.god = true; World.flags.Set("seawallOpen", true);
            var p = G.player;
            t.Place("saltgate", 14, 18); await t.Run(0.3f);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            var s = await Until(new[] { "right" }, s => s.tx >= 16); await t.Tap("interact"); await t.Run(0.2f);
            s = await Until(new[] { "left" }, s => s.tx <= 12); await t.Tap("interact"); await t.Run(0.2f);
            s = await Until(new[] { "right" }, s => s.tx >= 24); await t.Tap("interact"); await t.Run(0.1f);
            t.Ok("wave, skull, flame: the pan floods", World.flags.B("basinFilling"), string.Join(",", World.flags.Strings("basinSeq")));
            s = await Until(new[] { "right" }, s => s.tx >= 30, 3);
            t.Ok("there is time to run through the hatch before it shuts", s.tx >= 30, s);
            // the pan is already deep: swim up with the water to the barge, or the top
            s = await Until(new[] { "up" }, s => s.ty <= 8, 10);
            for (int k = 0; k < 40 && World.State("saltgate").pool["basin"] > 7.3f; k++) await t.Run(0.25f);
            t.Ok("the hatch is shut behind her", World.Solid(G.room.Ox + 28, G.room.Oy + 17) == 1);
            s = await Until(new[] { "up" }, s => s.ty <= 8, 4);
            t.Ok("she rises with the water to the top of the pan", t.State().ty <= 8, t.State());
            for (int k = 0; k < 4 && t.State().room != "kilns"; k++) { t.Down("up", "right"); await t.Hold("jump", 0.35f); await t.Run(0.8f); t.Release("up", "right"); await t.Run(0.2f); }
            t.Ok("and off the top of the pan into the Kilns", t.State().room == "kilns", t.State());
            G.god = false;
        },
        ["guide"] = async t =>
        {
            G.god = true;
            var p = G.player; var f = World.flags; var q = G.quests;
            t.Ok("every step's room is on the island, and reachable from the reef", Guide.Steps.All(s0 => Data.Room.ContainsKey(s0.room) && Guide.Route("reef", s0.room) != null), string.Join(",", Guide.Steps.Where(s0 => Guide.Route("reef", s0.room) == null).Select(s0 => s0.id)));
            t.Ok("a new game starts with getting ashore", Guide.Current?.id == "ashore", Guide.Current?.id);
            t.Place("pier", 20, 16); await t.Run(0.2f);
            t.Ok("on the pier, the next step is the chapel", Guide.Current?.id == "chapel", Guide.Current?.id);
            t.Ok("and a new objective is announced", Guide.bannerT > 0 && Guide.bannerTitle == Guide.Current.title, Guide.bannerTitle);
            G.titleT = 0; await t.Run(0.8f); t.Snap("guide_1_banner");
            var h = Guide.Heading(out bool thing);
            t.Ok("the pier points at the foot of its ladder up to the Seawall", h != null && !thing && MathF.Abs(h.Value.X - (G.room.Px + 4 * 16 + 8)) < 2 && h.Value.Y > G.room.Py + 14 * 16, h);
            q.Set("anselMet", true); await t.Run(0.1f);
            t.Ok("after Ansel: the crank", Guide.Current?.id == "crank", Guide.Current?.id);
            t.Place("tithe_gallery", 30, 16); await t.Run(0.2f);
            h = Guide.Heading(out thing, out var exit);
            t.Ok("in the gallery it points at the top of the ladder down, not the room's edge", h != null && exit.Y > 0 && MathF.Abs(h.Value.X - (G.room.Px + 35 * 16 + 8)) < 2 && h.Value.Y < G.room.Py + 17 * 16 + 8, $"{h} {exit}");
            G.titleT = 0; H.Toasts.Clear(); await t.Run(0.8f); t.Snap("guide_2_ladder");
            t.Place("tithe", 20, 16); await t.Run(0.2f);
            h = Guide.Heading(out thing);
            var crank = First<Crank>();
            t.Ok("in the hall it marks the crank itself", thing && h != null && MathF.Abs(h.Value.X - crank.Cx()) < 2, h);
            p.x = crank.Cx() + 40; G.SnapCamera(); G.titleT = 0; H.Toasts.Clear(); await t.Run(0.8f); t.Snap("guide_3_crank");
            UI.Map(); await t.Run(0.4f); t.Snap("guide_4_chart_crank"); UI.Close();
            p.bag.Add("crank", 1); await t.Run(0.1f);
            t.Ok("with the crank: the Seawall gate", Guide.Current?.id == "gate", Guide.Current?.id);
            G.titheFlood = 5; await t.Run(0.05f);
            t.Ok("in the flooding hall it says how to get out", Guide.Current.now?.Invoke()?.Contains("walkway ladder") == true);
            G.titheFlood = null;
            f.Set("seawallOpen", true); p.bag.Remove("crank", 1); await t.Run(0.1f);
            t.Ok("gate open: climb to the Saltworks", Guide.Current?.id == "climb", Guide.Current?.id);
            t.Place("saltgate", 10, 18); await t.Run(0.2f);
            t.Ok("in the Saltworks: cross the pans", Guide.Current?.id == "pans", Guide.Current?.id);
            f.Set("basinJam", true); await t.Run(0.05f);
            t.Ok("jammed sluices get their own advice", Guide.Current.now?.Invoke()?.Contains("Ebb") == true);
            f.Set("basinJam", false);
            t.Place("kilns", 15, 18); await t.Run(0.2f);
            t.Ok("at the Kilns: the Abbot", Guide.Current?.id == "abbot", Guide.Current?.id);
            // the journal and the chart
            UI.Satchel("log"); await t.Run(0.2f);
            t.Snap("guide_5_journal");
            t.Ok("the journal leads with the objective and how to do it", UI.Inst.AllText().Contains(Guide.Current.title) && UI.Inst.AllText().Contains("Cathedral of Salt"), UI.Kind);
            UI.Map(); await t.Run(0.3f);
            t.Ok("the chart opens", UI.Kind == "satchel");
            t.Snap("guide_6_chart_abbot");
            UI.Close();
            // steps done out of order are skipped
            f.Set("abbotDead", true); p.abilities.Add("boots"); t.Place("nave", 20, 16); await t.Run(0.2f);
            t.Ok("steps already done are skipped", Guide.Current?.id == "prior", Guide.Current?.id);
            // the guide can be put away
            Settings.Guide = false; await t.Run(0.05f);
            t.Ok("the guide can be turned off", !Guide.On);
            Settings.Guide = true;
            G.god = false;
        },
        ["worldaudit"] = async t =>
        {
            await t.Run(0.05f);
            var gaps = new List<string>(); var ladders = new List<string>(); var buried = new List<string>();
            static bool Open(char ch) => World.Solidity(ch) != 1 || ch is 'L' or 'E' or 'K' or 'Z' or 'V' or 'Y' or 'h';
            foreach (var r in Data.Rooms)
            {
                for (int ty = 0; ty < r.Th; ty++)
                    for (int tx = 0; tx < r.Tw; tx++)
                    {
                        bool edge = tx == 0 || ty == 0 || tx == r.Tw - 1 || ty == r.Th - 1;
                        if (!edge) continue;
                        int wx = r.Ox + tx, wy = r.Oy + ty;
                        char ch = World.CharAt(wx, wy);
                        if (!Open(ch)) continue;
                        // the tile across the edge
                        foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                        {
                            int nx = tx + dx, ny = ty + dy;
                            if (nx >= 0 && ny >= 0 && nx < r.Tw && ny < r.Th) continue;
                            var nr = World.RoomAtTile(wx + dx, wy + dy);
                            if (nr == null) { if (!r.Outdoor) gaps.Add($"{r.Id} ({tx},{ty}) opens onto nothing"); else GD.Print($"EDGE {r.Id} ({tx},{ty}) open to the outside"); continue; }
                            char nc = World.CharAt(wx + dx, wy + dy);
                            if (!Open(nc)) gaps.Add($"{r.Id} ({tx},{ty}) '{ch}' -> {nr.Id} '{nc}'");
                            if (World.IsLadder(wx, wy) || ch == 'H') { if (dy != 0 && nc != 'H' && nc != 'K' && nc != 'Z' && World.Solidity(nc) != 0) ladders.Add($"{r.Id} ({tx},{ty}) ladder ends at {nr.Id} '{nc}'"); }
                        }
                    }
                foreach (var spec in G.RoomSpecs(r))
                {
                    int tx = spec.I("tx"), ty = spec.I("ty");
                    if (tx < 0 || ty < 0 || tx >= r.Tw || ty >= r.Th) continue;
                    char ch = World.CharAt(r.Ox + tx, r.Oy + ty);
                    if (World.Solidity(ch) == 1 && spec.S("type") is not ("gullet" or "pews" or "wrackpool" or "driftnest")) buried.Add($"{r.Id} {spec.S("type")} at ({tx},{ty}) in '{ch}'");
                }
            }
            foreach (var g in gaps.Distinct()) GD.Print("GAP " + g);
            foreach (var g in ladders.Distinct()) GD.Print("LADDER " + g);
            foreach (var g in buried.Distinct()) GD.Print("BURIED " + g);
            t.Ok("every opening at a room's edge meets an opening beside it", gaps.Count == 0, gaps.Count + " gaps");
            t.Ok("every ladder off a room's edge carries on", ladders.Count == 0, ladders.Count + "");
            t.Ok("nothing is placed inside a wall", buried.Count == 0, buried.Count + "");
        },
        ["walkthrough"] = async t =>
        {
            async Task<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase)> Until(string[] keys, Func<(string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase), bool> cond, float secs = 12)
            {
                t.Down(keys);
                var s = t.State();
                for (int f = 0; f < secs * 60; f += 3) { s = t.State(); if (cond(s)) break; await t.Run(0.05f); }
                t.Release(keys);
                await t.Run(0.05f);
                return t.State();
            }
            G.god = true;
            var p = G.player;
            // the tower: without the crank its gate is shut, and the ladder stops under it
            t.Place("seawall", 23, 16); await t.Run(0.3f);
            var s = await Until(new[] { "up" }, s => s.room != "seawall", 5);
            t.Ok("the tower ladder stops under the shut gate", s.room == "seawall" && s.ty <= 3, s);
            s = await Until(new[] { "down" }, s => s.ground && s.st == "normal", 6);
            t.Ok("and she can climb back down to the foot of the tower", s.room == "seawall" && s.ty == 16 && s.ground, s);
            // the socket tells her where the crank is
            var sock = First<Socket>(); p.x = sock.Cx() - p.w / 2; await t.Run(0.1f);
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("the empty socket says where the crank is", UI.IsOpen() && UI.Inst.AllText().Contains("Tithe Hall"), UI.Kind);
            UI.Close(); await t.Run(0.1f);
            // the other way: west along the seawall into the Tithe Gallery
            t.Place("seawall", 3, 16); await t.Run(0.3f);
            s = await Until(new[] { "left" }, s => s.room == "tithe_gallery");
            t.Ok("west from the seawall into the Tithe Gallery", s.room == "tithe_gallery", s);
            s = await Until(new[] { "left" }, s => s.tx <= 35);
            s = await Until(new[] { "down" }, s => s.room == "tithe" && s.ground, 12);
            t.Ok("down the gallery ladder to the Tithe Hall floor", s.room == "tithe" && s.ground && s.ty == 16, s);
            // and from the hall floor a jump reaches the bottom of the walkway ladder
            t.Down("up"); await t.Hold("jump", 0.3f); await t.Run(0.3f); t.Release("up");
            t.Ok("from the floor, a jump catches the walkway ladder", p.state == "ladder", p.state);
            s = await Until(new[] { "down" }, s => s.ground, 4);
            s = await Until(new[] { "left", "sneak" }, s => s.tx <= 4, 20);
            t.Ok("across the hall to the altar", s.tx <= 5, s);
            await t.Tap("interact"); await t.Run(0.3f);
            t.Ok("the crank is taken, and the hall floods", p.bag.Count("crank") == 1 && G.titheFlood != null, $"crank={p.bag.Count("crank")}");
            // back up: under the walkway, rise with the water, climb out, and up the ladder
            s = await Until(new[] { "right" }, s => s.tx >= 34 || s.st == "swim", 20);
            s = await Until(new[] { "right" }, s => s.tx >= 34, 12);
            float waited = 0;
            while (waited < 30 && World.State("tithe").flood > 10) { await t.Run(0.5f); waited += 0.5f; }
            // a jump from the surface always carries her out, wherever she is in her bob (in open water, under no walkway)
            s = await Until(new[] { "left" }, s => s.tx <= 27, 8);
            float lowest = 1e9f;
            for (int k = 0; k < 4; k++)
            {
                s = await Until(new[] { "up" }, s => p.y < World.SurfaceAt(p.Cx(), p.Cy() + 30), 6);
                await t.Run(0.13f + k * 0.07f);
                float y0 = p.y; t.Down("jump");
                float top = y0; for (int f = 0; f < 18; f++) { await t.Run(1 / 60f); top = MathF.Min(top, p.y); }
                t.Release("jump");
                lowest = MathF.Min(lowest, y0 - top);
                await t.Run(0.8f);
            }
            t.Ok("a jump from the water's surface always leaves the water", lowest > 24, $"weakest jump rose {lowest:0} px");
            s = await Until(new[] { "right" }, s => s.tx >= 35, 8);
            p.x = G.room.Px + 35 * 16 + 3; p.vx = 0; await t.Run(0.2f);
            s = await Until(new[] { "up" }, s => s.st == "ladder", 4);
            t.Ok("from the flood she can reach the walkway ladder", s.st == "ladder", s);
            s = await Until(new[] { "up" }, s => s.room == "tithe_gallery" && s.st != "ladder", 14);
            t.Ok("up with the flood and the ladder, back to the gallery", s.room == "tithe_gallery", s);
            s = await Until(new[] { "right" }, s => s.room == "seawall");
            t.Ok("east to the seawall with the crank", s.room == "seawall" && p.bag.Count("crank") == 1, s);
            s = await Until(new[] { "right" }, s => s.tx >= 22);
            sock = First<Socket>(); p.x = sock.Cx() - p.w / 2; await t.Run(0.1f);
            await t.Tap("interact"); await t.Run(0.3f);
            t.Ok("the crank opens the gate", World.flags.B("seawallOpen") && p.bag.Count("crank") == 0);
            p.x = G.room.Px + 23 * 16 + 3; await t.Run(0.1f);
            s = await Until(new[] { "up" }, s => s.room == "saltgate" && s.st != "ladder", 14);
            t.Ok("up the tower into the Saltworks", s.room == "saltgate", s);
            s = await Until(new[] { "down" }, s => s.room == "seawall" && s.ground, 14);
            t.Ok("and back down", s.room == "seawall" && s.ground, s);
            // someone already above the shut gate (an old save) can always get down
            World.flags.Set("seawallOpen", false); World.MarkDirty();
            t.Place("saltgate", 23, 18); await t.Run(0.3f);
            s = await Until(new[] { "down" }, s => s.room == "seawall" && s.ground, 16);
            t.Ok("from above, the shut gate's bar lifts and she climbs down", s.room == "seawall" && World.flags.B("seawallOpen"), s);
            G.god = false;
        },
        ["prey"] = async t =>
        {
            var p = G.player;
            var kilns = Data.Room["kilns"];
            t.Place("kilns", 15, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            Shucker Spawn(string key, int tx)
            {
                var sp = Props.Empty(); sp.Set("x", kilns.Px + tx * 16 + 8); sp.Set("y", kilns.Py + 19 * 16); sp.Set("key", key); sp.Set("pose", "standing"); sp.Set("facing", -1);
                var b = new Shucker(sp, kilns); G.ents.Add(b); return b;
            }
            var a = Spawn("prey_a", 21);
            t.Ok("a Shucker's limbs take two rivets, its crown three", a.K("kneeL").max == 4 && a.K("crown").max == 5, $"{a.K("kneeL").max}/{a.K("crown").max}");
            // it sees her lamp, and it lurches
            await t.Run(0.2f);
            float top = 0, x0 = a.Cx();
            for (int i = 0; i < 60; i++) { await t.Run(1 / 60f); top = MathF.Max(top, MathF.Abs(a.vx)); if (a.mode != "hunt") break; }
            t.Ok("once it has seen her it lurches, near her running speed", top > 100 && top < 150, $"top speed {top:0}");
            // she has little grace after a hit
            a.gone = true; await t.Run(0.1f);
            p.hp = 5; p.invulnT = 0; p.Hurt(1, null);
            t.Ok("after a hit she has 0.6 s of grace (not 0.9)", MathF.Abs(p.invulnT - 0.6f) < 0.01f, p.invulnT);
            p.invulnT = 0; var hv = Spawn("prey_heavy", 17); p.Hurt(2, hv); hv.gone = true;
            t.Ok("a heavy blow knocks the wind out of her", p.Locked() && p.hp == 2, $"locked={p.Locked()} hp={p.hp}");
            await t.Run(0.8f);
            // the dodge is short and slow to come back
            p.invulnT = 0; await t.Tap("dash");
            t.Ok("the dash dodges for an instant and needs 0.75 s", p.dashInv <= 0.12f && p.dashCool > 0.6f, $"inv={p.dashInv:0.00} cool={p.dashCool:0.00}");
            // a grab takes eight to break and bites every second
            await t.Run(1f);
            var g = Spawn("prey_grab", 16); await t.Run(0.05f);
            p.invulnT = 0; p.hp = 5;
            p.TryGrab(g); g.mode = "grab"; g.atkT = 0.4f;
            t.Ok("breaking a grab takes eight", p.mash == 8, p.mash);
            await t.Run(2.5f);
            t.Ok("held, it bites every second", p.hp <= 3, $"hp={p.hp}");
            p.grabbedBy = null; p.state = "normal"; g.gone = true; p.hp = 5;
            await t.Run(0.2f);
            // rivets are scarce: a scrap makes two
            var rc = Data.Recipes.Find(r => r.S("id") == "rivets");
            t.Ok("a scrap makes two rivets", rc.I("n") == 2, rc.I("n"));
            // the Hollow: knots fill it, the sea only halfway, and a sung verse rings before it can be sung again
            Hollow.owned = true; Hollow.verses = new() { "calling", "riptide" }; Hollow.verse = "riptide"; Hollow.resonance = 0;
            var k = Spawn("prey_k", 18); await t.Run(0.05f);
            k.DamageKnot(k.K("kneeL"), 99, "rivet");
            t.Ok("a broken knot fills the Hollow a little (4)", MathF.Abs(Hollow.resonance - 4) < 0.01f, Hollow.resonance);
            k.gone = true;
            G.god = true;
            t.Place("gulletmouth", 16, 11); await t.Run(0.2f);
            Hollow.resonance = 40; Hollow.stillT = 0;
            await t.Run(8f);
            t.Ok("held in the sea it fills only halfway", Hollow.resonance >= Hollow.SEA_FILL - 0.5f && Hollow.resonance <= Hollow.SEA_FILL + 0.01f, Hollow.resonance);
            Hollow.Hold(true); Hollow.resonance = 80; Hollow.stillT = 0;
            t.AimAt(p.Cx() + 40, p.Cy());
            await t.Click(); await t.Run(0.1f);
            float after = Hollow.resonance;
            t.Ok("the Riptide costs 35", MathF.Abs(after - 45) < 1, after);
            await t.Run(0.8f); await t.Click(); await t.Run(0.1f);
            t.Ok("and rings for seconds before it can be sung again", Hollow.Ringing("riptide") > 0 && Hollow.resonance <= after + 0.01f, $"ring={Hollow.Ringing("riptide"):0.0} res={Hollow.resonance:0.0}");
            t.Ok("while it rings it drinks nothing from the sea", Hollow.stillT > 0 || Hollow.resonance <= after + 0.01f);
            Hollow.Hold(false); G.god = false;
        },

        // The last night's weapons: Agathe's Glass, the Saint's Ear and the Tine, the sea's hold, and the fourth ending.
        ["legend"] = async t =>
        {
            G.god = false;
            var p = G.player; p.maxHp = 7; p.hp = 7;
            p.weapons["harpoon"] = new WeaponState { tier = 1, mag = 1 }; p.weaponOrder.Add("harpoon");
            Hollow.owned = true; Hollow.verses = new() { "calling", "riptide", "salting", "answer" }; Hollow.verse = "calling";
            // before the spring tide neither is there to be had
            t.Place("ribbell", 12, 16); await t.Run(0.2f);
            t.Ok("before Act III there is no Glass in the Ribvault", !G.ents.OfType<GlassCase>().Any());
            t.Place("gulletmouth", 16, 11); await t.Run(0.2f);
            var ear = G.ents.OfType<EarSocket>().FirstOrDefault();
            t.Ok("the Saint's Ear is in the Gullet's dome", ear != null);
            p.x = ear.Cx() - p.w / 2; p.y = ear.y + ear.h + 2; p.vx = p.vy = 0;
            await t.Run(0.05f); await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("before the spring tide it is dry, only letters", UI.Kind == "inscription" && !Legend.merged, UI.Kind);
            t.Snap("legend_1_ear_dry");
            UI.Close();
            // the spring tide
            G.StartAct(3); await t.Run(0.1f);
            t.Place("ribbell", 12, 16); await t.Run(0.2f);
            var gc = G.ents.OfType<GlassCase>().FirstOrDefault();
            t.Ok("at the spring tide Agathe's Glass is waiting in the Ribvault Bell Room", gc != null);
            p.x = gc.Cx() - p.w / 2; p.y = gc.y + gc.h - p.h; await t.Run(0.05f);
            t.Snap("legend_2_glass_case");
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("taking it shows her note and what it does", UI.Kind == "inscription" && Legend.glassOwned && p.weapons.ContainsKey("glass"), UI.Kind);
            t.Snap("legend_3_glass_note");
            UI.Close();
            // the Glass burns knots, drinks oil, and must cool
            var kilns = Data.Room["kilns"];
            t.Place("kilns", 15, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            Shucker Spawn(string key, int tx, RoomDef r, int ty = 19)
            {
                var sp = Props.Empty(); sp.Set("x", r.Px + tx * 16 + 8); sp.Set("y", r.Py + ty * 16); sp.Set("key", key); sp.Set("pose", "standing"); sp.Set("facing", -1);
                var b = new Shucker(sp, r); G.ents.Add(b); return b;
            }
            var s1 = Spawn("glass_a", 19, kilns); s1.stun = 99;
            await t.Run(0.1f);
            p.SelectWeapon("glass"); p.oil = 90;
            t.AimAt(s1.Cx(), s1.Cy());
            t.Down("fire"); await t.Run(1.2f);
            t.Snap("legend_4_glass_beam");
            t.Ok("the beam burns", Legend.beaming && s1.knots.Any(k => k.hp < k.max), $"beaming={Legend.beaming}");
            t.Ok("and drinks oil fast (15 a second)", p.oil < 90 - 15 && p.oil > 90 - 22, $"oil={p.oil:0}");
            await t.Run(2.2f);
            t.Ok("after three seconds the lens must cool", Legend.coolT > 0 && !Legend.beaming, $"cool={Legend.coolT:0.0}");
            t.Release("fire"); await t.Run(0.1f);
            // the fallen burn in it, and do not stand
            foreach (var k in s1.knots) k.broken = true; s1.Down("rivet"); await t.Run(0.1f);
            Legend.coolT = 0; Legend.heat = 0; p.oil = 90;
            t.AimAt(s1.Cx(), s1.Cy() + 6);
            t.Down("fire"); await t.Run(1.6f); t.Release("fire");
            await t.Run(3.4f);
            t.Ok("a fallen body in the beam burns away", s1.gone, s1.mode);
            // the Ear runs with brine now
            t.Place("gulletmouth", 16, 11); await t.Run(0.2f);
            ear = G.ents.OfType<EarSocket>().First();
            p.x = ear.Cx() - p.w / 2; p.y = ear.y + ear.h + 2; p.vx = p.vy = 0;
            await t.Run(0.05f);
            t.Snap("legend_5_ear_wet");
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("at the spring tide the Ear runs with brine", UI.Kind == "inscription", UI.Kind);
            await t.Tap("interact"); await t.Run(0.2f);
            t.Ok("and asks for the Hollow and the harpoon", UI.Kind == "confirm", UI.Kind);
            await t.ClickButton("Yes"); await t.Run(0.3f);
            t.Ok("the bone grows them together into the Saint's Tine", Legend.merged && p.weapons.ContainsKey("tine") && !p.weapons.ContainsKey("harpoon") && p.cur == "tine", string.Join(",", p.weaponOrder));
            t.Snap("legend_6_merged");
            UI.Close();
            Hollow.Hold(true);
            t.Ok("grown together, the Hollow sings no verses", !Hollow.held);
            // the Tine runs through every body in its way
            t.Place("kilns", 15, 18);
            foreach (var b in G.ents.OfType<Body>()) b.gone = true;
            var a = Spawn("tine_a", 19, kilns); var b2 = Spawn("tine_b", 22, kilns);
            a.stun = b2.stun = 99;
            await t.Run(0.1f);
            Hollow.resonance = 60;
            p.SelectWeapon("tine");
            t.AimAt(a.Cx() + 40, a.KnotPos(a.K("elbowL")).Value.Y);
            await t.Click(); await t.Run(0.06f);
            t.Snap("legend_7_tine_throw");
            await t.Run(0.4f);
            t.Ok("a throw costs a quarter of the Hollow", Hollow.resonance < 60 - 24 + 9, Hollow.resonance);
            t.Ok("and runs through both, breaking knots in each", a.knots.Any(k => k.hp < k.max) && b2.knots.Any(k => k.hp < k.max), $"a={string.Join(",", a.knots.Select(k => k.hp))} b={string.Join(",", b2.knots.Select(k => k.hp))}");
            // empty, it drinks from her
            Hollow.resonance = 0; int hp0 = p.hp; float c0 = Legend.communion;
            await t.Run(1f); await t.Click(); await t.Run(0.2f);
            t.Ok("empty, it drinks from her", p.hp == hp0 - 1 && Legend.communion >= c0 + 19, $"hp {hp0}->{p.hp}");
            // the sea's hold counts out in the island, not under the bells
            float h0 = Legend.communion; await t.Run(2f);
            t.Ok("the sea's hold grows while she carries it", Legend.communion > h0 + 1.8f, $"{h0:0.0} -> {Legend.communion:0.0}");
            t.Place("chapel", 20, 16); await t.Run(0.3f);
            float h1 = Legend.communion; Hollow.resonance = 40; await t.Run(2f);
            t.Ok("under a bell it does not count, and the Hollow bleeds dry", MathF.Abs(Legend.communion - h1) < 0.01f && Hollow.resonance < 40, $"hold {h1:0.0}->{Legend.communion:0.0} res={Hollow.resonance:0}");
            // it is kept
            G.Save(); G.LoadGame(); await t.Run(0.3f); p = G.player;
            t.Ok("the Tine and the sea's hold are saved", Legend.merged && p.weapons.ContainsKey("tine") && Legend.communion > 20, $"{Legend.merged} {Legend.communion:0}");
            // a bench prises it apart: the harpoon comes back as it was
            Legend.Unmerge();
            t.Ok("prised free, the harpoon comes back as it was and the Hollow empty", !Legend.merged && p.weapons.TryGetValue("harpoon", out var hw) && hw.tier == 1 && Hollow.resonance == 0 && p.weaponOrder.Contains("harpoon"));
            // grown long enough it roots, and tears her coming out
            Legend.Merge(); UI.Close(); Legend.communion = Legend.HOST_SECONDS / 2 + 1; await t.Run(0.2f);
            t.Ok("halfway, it has rooted", Legend.Rooted && Legend.stage >= 2, Legend.stage);
            p.hp = 6; Legend.Unmerge();
            t.Ok("and tears her coming out", p.hp == 4, p.hp);
            // all the way, and the sea is at the lamp room's window
            Legend.Merge(); UI.Close();
            Legend.communion = Legend.HOST_SECONDS - 0.5f; await t.Run(1f);
            t.Ok("all the way, she can hear the Saint", Legend.stage == 4 && Legend.HostReady && G.narr.thoughts.Contains("host4"), Legend.stage);
            G.god = true;
            t.Place("lamp", 33, 16); await t.Run(0.3f);
            var win = G.ents.OfType<SeaWindow>().FirstOrDefault();
            t.Ok("the sea is waiting at the lamp room's window", win != null);
            t.Snap("legend_8_window");
            p.x = win.Cx() - p.w / 2; await t.Run(0.05f);
            await t.Tap("interact"); await t.Run(0.1f);
            t.Ok("it asks if she will go down", UI.Kind == "confirm", UI.Kind);
            await t.ClickButton("Yes");
            t.Ok("the ending plays", UI.Kind == "cinematic", UI.Kind);
            await t.Tap("pause"); await t.Run(0.15f);
            string all = UI.Inst.AllText();
            t.Ok("Host of the Mere, with its own fates", UI.Kind == "ending" && G.ended == "host" && all.Contains("There is no Maren Hult"), $"{UI.Kind} {G.ended}");
            t.Snap("legend_9_ending");
            G.god = false;
        },
    };
}
