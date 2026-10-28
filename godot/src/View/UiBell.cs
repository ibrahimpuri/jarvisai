// The Bell Room and what is kept there: the bell's own menu, the reliquary chest every Bell Room shares, the bench
// (crafting, the weapons, the fittings, Ansel's surgery); and the Choir's sluice and its font.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class UiRoot
{
    // ---- Bell Room ------------------------------------------------------------------------------
    bool PayDrowned()
    {
        var p = G.player;
        if (!Diff.C.saveCost) return true;
        if (p.bag.Count("tallow") < 1) { H.ToastMsg($"Saving on {Diff.C.name} costs 1 tallow."); Snd.Deny(); return false; }
        p.bag.Remove("tallow", 1);
        return true;
    }
    // The bell swinging on its yoke over a slate of choices.
    public void BellMenu(Bell bell)
    {
        var p = G.player;
        Show("bell");
        var c = Panel(236, look: MenuArt.Look.Slate);
        c.AddChild(new Painted((cv, sz, t) =>
        {
            cv.Glow(sz.X / 2, sz.Y * 0.6f, 70, 34, new Color(1, 0.75f, 0.4f, 0.1f));
            MenuArt.Bell(cv, sz.X / 2, 6, 19, t, 0.14f, 11);
        }, new Vector2(0, 54)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var title = H1(c, (bell.spec.S("name") ?? G.room.Name) + " Bell Room");
        Lbl(c, "No Brine body will ever enter while the bell rings.", 6, MUTED, italic: true, center: true);
        Lbl(c, $"{Tide.PhaseName()} tide  ·  {H.FmtTime(Tide.PhaseRemaining())} until it turns", 5, MUTED, caps: true, center: true);
        Rule(c);
        var col = Col(c);
        Btn(col, "Save" + (Diff.C.saveCost ? " (1 tallow)" : ""), () => { if (!PayDrowned()) return; G.SetRespawn(bell); G.Save(); H.ToastMsg("Saved."); BellMenu(bell); });
        Btn(col, "Rest: " + (p.grafts.Contains("lung") ? "heal by half (Tide Lung)" : "heal") + ", save, wait for Low tide", () => { if (!PayDrowned()) return; Close(); G.Rest(bell); }, primary: true);
        Btn(col, "Reliquary chest (shared by every Bell Room)", () => Reliquary(() => BellMenu(bell)));
        Btn(col, "Bench: craft, upgrades, fittings" + (bell.room.Id == "chapel" ? ", surgery" : ""), () => Bench(bell.room.Id == "chapel", () => BellMenu(bell)));
        Btn(col, "Leave", Close);
        Rule(c);
        Small(c, "Resting skips time. Every body left lying in water re-knots on the way.").HorizontalAlignment = HorizontalAlignment.Center;
        Footer(c, ("confirm", "choose"), ("cancel", "leave"));
        FocusFirst();
    }

    // ---- reliquary ----------------------------------------------------------------------------------
    // Like a survival-horror item box: the satchel's pockets on one side, the chest's on the other. Choosing a pocket
    // moves what is in it across.
    string relSel; bool relSide;
    public void Reliquary(Action back)
    {
        var p = G.player; var chest = G.reliquary;
        Show("reliquary");
        backAction = () => (back ?? Close)();
        var c = Sheet(40, 20, 560, 320, MenuArt.Look.Wood, 0.8f);
        H2(c, "Reliquary");
        Lbl(c, "The same chest waits in every Bell Room. What is put in here is safe from the tide, and from her own death.", 5, MUTED, italic: true);
        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 14);
        c.AddChild(cols);
        var info = new VBoxContainer(); info.AddThemeConstantOverride("separation", 2);
        Button want = null, first = null;
        void Move(string i, bool all, bool put)
        {
            if (put) { int n = Math.Min(p.bag.Count(i), all ? 999 : 1); int moved = n - chest.Add(i, n); p.bag.Remove(i, moved); }
            else { int n = Math.Min(chest.Count(i), all ? 999 : 1); int left = p.bag.Add(i, n); chest.Remove(i, n - left); if (left > 0) { H.ToastMsg("Satchel full."); Snd.Deny(); } }
            Snd.Pickup(); relSel = i; relSide = put;
            Reliquary(back);
        }
        void Info(string i, bool put)
        {
            foreach (var ch in info.GetChildren()) { info.RemoveChild(ch); ch.QueueFree(); }
            var row = Row(info); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ItemView(i) { CustomMinimumSize = new Vector2(40, 32) });
            var cc = Col(row);
            Lbl(cc, Data.ItemName(i) + $"  ·  {(put ? p.bag.Count(i) : chest.Count(i))}", 8, GOLD);
            Lbl(cc, Data.Item(i).S("desc"), 5, MUTED);
            var br = Row(cc);
            Btn(br, put ? "Put all in the chest" : "Take all", () => Move(i, true, put));
            Btn(br, put ? "Put one in" : "Take one", () => Move(i, false, put));
        }
        void Side(string title, Bag bag, bool put, int empties)
        {
            var col = Fixed(cols, 6 * 41);
            Lbl(col, title, 6, WARM, caps: true);
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(6 * 41 + 6, 150), FollowFocus = true };
            col.AddChild(scroll);
            var flow = new HFlowContainer { CustomMinimumSize = new Vector2(6 * 41, 0) };
            flow.AddThemeConstantOverride("h_separation", 3); flow.AddThemeConstantOverride("v_separation", 3);
            scroll.AddChild(flow);
            foreach (var (i, n) in bag.List())
            {
                var def = Data.Item(i); bool key = def.B("key");
                string item = i;
                var b = SlotBtn(flow, i, key ? "" : n.ToString(), key && Bag.SlotsFor(i, n) > 1 ? 79 : 38, 34, key);
                b.Pressed += () => Move(item, true, put);
                b.FocusEntered += () => Info(item, put);
                b.MouseEntered += () => b.GrabFocus();
                first ??= b; if (i == relSel && put == relSide) want = b;
            }
            for (int e = 0; e < empties; e++) { int seed = e; flow.AddChild(new Painted((cv, sz, t) => MenuArt.Slot(cv, sz.X, sz.Y, false, 0, t, seed), new Vector2(38, 34))); }
        }
        Side($"Satchel  ·  {p.bag.Used()} of {p.bag.size} pockets", p.bag, true, Math.Max(0, p.bag.size - p.bag.Used()));
        Side("The chest", chest, false, Math.Max(0, 12 - chest.List().Count));
        Rule(c);
        c.AddChild(info);
        if (first == null) Lbl(info, "Nothing to move.", 6, MUTED, italic: true);
        var foot = Row(c);
        Footer(foot, ("confirm", "move it across"), ("cancel", "back"));
        Btn(foot, "Back", () => (back ?? Close)(), style: MenuArt.Btn.Chip);
        if (first != null) FocusOn(want ?? first); else FocusFirst();
    }

    // ---- bench ----------------------------------------------------------------------------------------
    // A workbench of salt-bleached planks. Its pages: make things, improve the weapons, set the suit's fittings, and in
    // the chapel, Ansel's surgery.
    string benchSel;
    public void Bench(bool chapel, Action back)
    {
        var p = G.player;
        Show("bench");
        benchChapel = chapel; benchBack = back;
        backAction = () => (back ?? Close)();
        benchBackOr = () => (back ?? Close)();
        var c = Sheet(26, 14, 588, 332, MenuArt.Look.Wood, 0.8f);
        var tabs = BenchTabs(chapel);
        if (!tabs.Contains(benchTab)) benchTab = "craft";
        var tr = Row(c); tr.AddThemeConstantOverride("separation", 3);
        NoWrap(Lbl(tr, "Bench", 10, GOLD));
        tr.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        foreach (var t in tabs)
        {
            string tt = t;
            Btn(tr, char.ToUpper(t[0]) + t[1..], () => { benchTab = tt; Bench(chapel, back); }, selected: t == benchTab, style: MenuArt.Btn.Tab).FocusMode = Control.FocusModeEnum.Click;
        }
        tr.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Btn(tr, "Back", () => (back ?? Close)(), style: MenuArt.Btn.Chip).FocusMode = Control.FocusModeEnum.Click;
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        c.AddChild(body);
        void Again() => Bench(chapel, back);
        switch (benchTab)
        {
            case "craft": CraftView(body, "bench", Again); break;
            case "upgrade": UpgradePage(body, Again); break;
            case "fittings": FittingsPage(body, Again); break;
            case "surgery": SurgeryPage(body, Again); break;
        }
        Footer(c, ("tabs", "change page"), ("confirm", "choose"), ("cancel", "back"));
        if (benchTab is "surgery") FocusFirstIn(body);
    }
    static List<string> BenchTabs(bool chapel) { var t = new List<string> { "craft", "upgrade", "fittings" }; if (chapel) t.Add("surgery"); return t; }

    // Like a gunsmith's counter: the weapons down the left; on the right the one in hand, its tiers as a ladder, the
    // next one's cost against what she has.
    void UpgradePage(HBoxContainer body, Action again)
    {
        var p = G.player;
        var list = Fixed(body, 168);
        Lbl(list, "Weapons", 6, WARM, caps: true);
        var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 3);
        body.AddChild(card);
        Button first = null, want = null;
        foreach (var id in p.weaponOrder)
        {
            string wid = id;
            var def = Data.Weapons.P(id);
            var b = Btn(list, def.S("name"), () => FocusFirstIn(card));
            foreach (var s0 in new[] { "normal", "hover", "pressed", "focus" }) b.AddThemeStyleboxOverride(s0, Room(26, 4, 7, 4));
            b.GetChildren().OfType<ButtonArt>().First().Extra = (cv, bt, st) => { if (Icons.Has(wid)) Icons.Draw(cv, wid, 16, bt.Size.Y / 2, 14); };
            b.FocusEntered += () => { benchSel = wid; WeaponCard(card, wid, again); };
            b.MouseEntered += () => b.GrabFocus();
            first ??= b; if (id == benchSel) want = b;
        }
        Lbl(list, "Tiers add behaviours, not just damage. Mere-ivory comes from the great dead and from hidden vaults.", 5, MUTED);
        backAction = () =>
        {
            var f = root.GetViewport().GuiGetFocusOwner();
            if (f != null && card.IsAncestorOf(f)) (FindButtons(list).FirstOrDefault(x => x.Text == Data.Weapons.P(benchSel)?.S("name")) ?? first)?.GrabFocus();
            else (benchBack ?? Close)();
        };
        FocusOn(want ?? first);
    }
    void WeaponCard(VBoxContainer card, string id, Action again)
    {
        foreach (var ch in card.GetChildren()) { card.RemoveChild(ch); ch.QueueFree(); }
        var p = G.player; var w = p.weapons[id]; var def = Data.Weapons.P(id);
        var top = Row(card); top.AddThemeConstantOverride("separation", 8);
        top.AddChild(new ItemView(id) { CustomMinimumSize = new Vector2(70, 46) });
        var tc = Col(top);
        Lbl(tc, def.S("name"), 10, GOLD);
        if (Player.Legendary(id))
        {
            Lbl(tc, "Legendary", 5, H.Col("#bfe8e0"), caps: true);
            Lbl(tc, Glyphs.Plain(def.Strings("tiers")[0], Glyphs.Current), 6, TEXT);
            Rule(card);
            // the legendary weapons are not improved on; the Tine can be prised apart again
            if (id == "tine")
            {
                var U = Data.Lore.P("legend").P("unmerge");
                Lbl(card, Legend.Rooted ? "It has rooted. Prising the Hollow free will tear you (2 pips)." : "Prise the Hollow free: the Harpoon Gun comes back as it was, the Hollow comes back empty.", 6, TEXT);
                Btn(Row(card), "Prise free", () => Confirm(U.S(Legend.Rooted ? "rooted" : "ask"), () => { Legend.Unmerge(); again(); }, again), style: MenuArt.Btn.Danger);
            }
            else Lbl(card, "Agathe's lens. Nothing at a bench improves on it.", 6, MUTED, italic: true);
            return;
        }
        var tiers = def.Strings("tiers");
        Lbl(tc, $"Tier {w.tier + 1} of {tiers.Count}", 5, MUTED, caps: true);
        Rule(card);
        // the ladder of tiers: what it has, what is next, what is beyond
        for (int i = 0; i < tiers.Count; i++)
        {
            int ti = i;
            var row = Row(card); row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new Painted((cv, sz, t) =>
            {
                float cy = sz.Y / 2;
                bool had = ti <= w.tier, next = ti == w.tier + 1;
                if (ti < tiers.Count - 1) cv.FillRect(sz.X / 2 - 0.4f, cy + 4, 0.8f, sz.Y, new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, 0.3f));
                cv.Save(); cv.Translate(sz.X / 2, cy); cv.Rotate(H.PI / 4);
                cv.Fill = MenuArt.BRASS_DARK; cv.FillRect(-3.4f, -3.4f, 6.8f, 6.8f);
                cv.Fill = had ? MenuArt.BRASS_LIGHT : next ? new Color(MenuArt.AMBER.R, MenuArt.AMBER.G, MenuArt.AMBER.B, 0.4f + 0.3f * MathF.Sin(t * 4)) : new Color(0, 0, 0, 0.6f);
                cv.FillRect(-2.4f, -2.4f, 4.8f, 4.8f);
                cv.Restore();
            }, new Vector2(10, 12)));
            var txt = tiers[i]; int colon = txt.IndexOf(':');
            string name = colon > 0 ? txt[..colon] : $"Tier {i + 1}", what = colon > 0 ? txt[(colon + 1)..].Trim() : txt;
            var cc = Col(row);
            Lbl(cc, name, 6, i <= w.tier ? TEXT : i == w.tier + 1 ? WARM : MUTED);
            if (i >= w.tier) Lbl(cc, Glyphs.Plain(what, Glyphs.Current), 5, MUTED);
        }
        var next = w.tier + 1 < Data.UpgradeCost.Count && w.tier + 1 < tiers.Count ? Data.UpgradeCost[w.tier + 1] : null;
        Rule(card);
        if (next == null) { Lbl(card, "Nothing more a bench can do for it.", 6, MUTED, italic: true); return; }
        var cr = Row(card); cr.AddThemeConstantOverride("separation", 10);
        foreach (var kv in next)
        {
            var h = Row(cr);
            h.AddChild(new IconView(kv.Key) { CustomMinimumSize = new Vector2(14, 14) });
            int got = p.bag.Count(kv.Key);
            NoWrap(Lbl(h, $"{Data.ItemName(kv.Key)} {got} / {kv.Value}", 6, got >= kv.Value ? GOOD : BAD));
        }
        bool ok = p.bag.Has(next);
        var br = Row(card); br.AddThemeConstantOverride("separation", 8);
        Btn(br, "Upgrade", () =>
        {
            if (!p.bag.Has(next)) return;
            p.bag.Pay(next); w.tier++; Snd.Craft(); H.ToastMsg(def.S("name") + ": " + tiers[w.tier].Split(':')[0]); again();
        }, primary: true, disabled: !ok);
        if (!ok) Lbl(br, "Not enough to pay for it.", 6, BAD, italic: true).VerticalAlignment = VerticalAlignment.Center;
    }

    // The suit's fittings against its pressure rating, read off a gauge.
    void FittingsPage(HBoxContainer body, Action again)
    {
        var p = G.player;
        int used = p.fittings.Sum(k => Data.Fittings.P(k).I("p"));
        var list = Fixed(body, 220);
        Lbl(list, "Fittings", 6, WARM, caps: true);
        if (p.fittingsOwned.Count == 0) Lbl(list, "You have not found any fittings yet. Search containers.", 6, MUTED, italic: true);
        var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 4);
        body.AddChild(card);
        card.AddChild(new Painted((cv, sz, t) => PressureDial(cv, sz.X / 2, sz.Y - 4, 40, used, p.pressure, t), new Vector2(0, 48)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Lbl(card, $"Pressure rating {used} of {p.pressure}", 8, used > p.pressure ? BAD : GOLD, center: true);
        Lbl(card, "Fittings are swapped only at Bell Rooms, so a loadout is a commitment for the next push.", 5, MUTED, center: true);
        Rule(card);
        var info = new VBoxContainer(); card.AddChild(info);
        Button first = null, want = null;
        foreach (var f in p.fittingsOwned)
        {
            string fid = f;
            var d = Data.Fittings.P(f); bool on = p.fittings.Contains(f);
            var b = Btn(list, (on ? "Equipped: " : "Equip: ") + d.S("name") + $"  ({d.I("p")})", () =>
            {
                if (p.fittings.Contains(fid)) p.fittings.Remove(fid);
                else
                {
                    int u = p.fittings.Sum(k => Data.Fittings.P(k).I("p"));
                    if (u + d.I("p") > p.pressure) { H.ToastMsg("Not enough Pressure Rating for that fitting."); Snd.Deny(); return; }
                    p.fittings.Add(fid);
                }
                if (p.oil > p.OilMax()) p.oil = p.OilMax();
                var wd = p.weapons["driver"]; if (wd.mag > p.MagSize("driver")) { p.bag.Add("rivets", wd.mag - p.MagSize("driver")); wd.mag = p.MagSize("driver"); }
                benchSel = fid; Snd.Craft(); again();
            }, selected: on);
            b.FocusEntered += () =>
            {
                foreach (var ch in info.GetChildren()) { info.RemoveChild(ch); ch.QueueFree(); }
                Lbl(info, d.S("name"), 8, GOLD);
                Lbl(info, d.S("effect") + ".", 6, TEXT);
                Lbl(info, "Trade-off: " + d.S("trade") + ".", 6, H.Col("#d8a090"));
                Lbl(info, $"Takes {d.I("p")} of the suit's pressure rating.", 5, MUTED);
            };
            b.MouseEntered += () => b.GrabFocus();
            first ??= b; if (f == benchSel) want = b;
        }
        Lbl(list, "Diver: Cork Lining, Muffled Soles, Tallow Reservoir. Rigger: Rivet Feeder, Bilge Pump. Breaker: Knot Splitter, Lead Plates.", 5, MUTED);
        if (first != null) FocusOn(want ?? first);
    }

    void SurgeryPage(HBoxContainer body, Action again)
    {
        var p = G.player;
        var col = Col(body);
        col.AddChild(new Painted((cv, sz, t) =>
        {
            // Ansel's tray: a scalpel, a curved needle, a dish of spirit
            float x = 8, y = sz.Y / 2;
            cv.FillRect(x, y - 8, 120, 16, new Color(0.5f, 0.52f, 0.5f, 0.3f));
            cv.Stroke = new Color(0.85f, 0.88f, 0.9f, 0.9f); cv.LineWidth = 1; cv.Line(x + 10, y, x + 40, y - 2);
            cv.Fill = new Color(0.85f, 0.88f, 0.9f); cv.Poly(new[] { new Vector2(x + 40, y - 3.5f), new Vector2(x + 52, y - 3), new Vector2(x + 40, y - 0.5f) });
            cv.BeginPath(); cv.Arc(x + 70, y, 6, 0.2f, H.PI - 0.2f); cv.StrokePath();
            cv.Fill = new Color(0.6f, 0.75f, 0.9f, 0.4f); cv.BeginPath(); cv.Ellipse(x + 100, y, 12, 5, 0, 0, H.PI * 2); cv.FillPath();
            cv.Glow(x + 30, y + 3, 14, 4, new Color(0.5f, 0.05f, 0.03f, 0.4f));
        }, new Vector2(0, 26)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Lbl(col, "Ansel's surgery", 8, GOLD);
        Lbl(col, "A graft can be cut out once. The knot comes with it, and a piece of her.", 6, MUTED, italic: true);
        Rule(col);
        if (G.quests.B("surgeryUsed")) Lbl(col, "Ansel has already used his one good scalpel on you.", 6);
        else if (p.grafts.Count == 0) Lbl(col, "You carry no grafts.", 6, MUTED, italic: true);
        else foreach (var k in p.grafts.ToList())
            {
                var gd = Data.Grafts.P(k);
                Lbl(col, gd.S("name") + $"  ({gd.S("knot")} knot)", 8, H.Col("#cfe0d8"));
                Lbl(col, $"{gd.S("power")}. Cost: {gd.S("cost")}.", 5, MUTED);
                Btn(Row(col), "Cut out (-1 pip)", () => Confirm($"Cut out the {gd.S("name")}? You lose 1 health pip permanently. Ansel can do this only once.", () =>
                {
                    p.grafts.Remove(k); p.maxHp = Math.Max(3, p.maxHp - 1); p.hp = Math.Min(p.hp, p.maxHp); G.quests.Set("surgeryUsed", true);
                    Snd.Hurt(); H.ToastMsg("The graft is cut out. You will carry the scar.");
                    again();
                }, again), style: MenuArt.Btn.Danger);
            }
    }

    // ---- Choir menus ---------------------------------------------------------------------------
    public void SluiceMenu()
    {
        var f = World.flags;
        if (f.B("choirDrained")) { Dialogue(null, new List<string> { "The shrine is drained. The sluice is jammed open with silt; it will never hold water again." }, null); return; }
        Show("sluice");
        var c = Panel(236, look: MenuArt.Look.Stone);
        H2(c, "Choir sluice");
        Lbl(c, "The Tide Key turns in the wave-shaped lock. The Choir's water does not answer the moon; it answers the key.", 6, MUTED, italic: true);
        Rule(c);
        var col = Col(c);
        string lvl = f.S("choirLevelName");
        Btn(col, "Raise the water to the vault (swim up to the Ribvault)", () => { G.SetChoirLevel("raised"); Close(); }, disabled: lvl == "raised");
        Btn(col, "Return the water to the shrine line", () => { G.SetChoirLevel("mid"); Close(); }, disabled: lvl == null || lvl == "mid");
        Btn(col, "Drain the shrine (permanent)", () => Confirm("Drain the Sunken Choir for good? Hesper wants the shrine kept flooded for the Saint's hymn. The Choir will turn on you.", () => G.SetChoirLevel("drained"), SluiceMenu), style: MenuArt.Btn.Danger);
        Btn(col, "Leave it", Close);
        Footer(c, ("confirm", "choose"), ("cancel", "leave"));
        FocusFirst();
    }
    // The Brine font: a basin of live salt, and what it will grow in her.
    public void FontMenu()
    {
        var p = G.player;
        Show("font");
        dim.Tint = new Color(0.02f, 0.06f, 0.06f);
        var c = Panel(300, look: MenuArt.Look.Stone, growth: 1.3f);
        c.AddChild(new Painted((cv, sz, t) =>
        {
            // the basin, its water glittering with live salt
            float cx = sz.X / 2, cy = sz.Y / 2 + 2;
            cv.Glow(cx, cy, 80, 14, new Color(MenuArt.SEAGLASS.R, MenuArt.SEAGLASS.G, MenuArt.SEAGLASS.B, 0.18f + 0.06f * MathF.Sin(t * 1.3f)));
            cv.Fill = H.Col("#2a3234"); cv.BeginPath(); cv.Ellipse(cx, cy, 60, 8, 0, 0, H.PI * 2); cv.FillPath();
            cv.Fill = H.Col("#0f3a38"); cv.BeginPath(); cv.Ellipse(cx, cy - 1, 54, 6, 0, 0, H.PI * 2); cv.FillPath();
            for (int i = 0; i < 24; i++)
            {
                float a = TileArt.Hash(i, 3) * H.PI * 2, d = MathF.Sqrt(TileArt.Hash(i, 4));
                float x = cx + MathF.Cos(a) * d * 50, y = cy - 1 + MathF.Sin(a) * d * 5;
                cv.Fill = new Color(0.85f, 1, 0.95f, 0.3f + 0.6f * MathF.Max(0, MathF.Sin(t * 3 + i * 1.7f))); cv.FillRect(x, y, 0.8f, 0.8f);
            }
            MenuArt.Salt(cv, cx - 58, cy - 4, 116, 5, 3);
        }, new Vector2(0, 22)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        H2(c, "The Brine font");
        Lbl(c, "Each graft gives a strong power but grows a knot on Maren herself, which the dead can strike. Grafts change how others treat her, and which endings are open. A graft can be cut out once, at a Keeper's surgery.", 5, MUTED);
        Rule(c);
        foreach (var k in Data.Grafts.Keys)
        {
            var d = Data.Grafts.P(k);
            var row = Row(c); row.AddThemeConstantOverride("separation", 6);
            var cc = Col(row);
            Lbl(cc, d.S("name") + $"  ({d.S("knot")})", 7, p.grafts.Contains(k) ? MUTED : H.Col("#cfe0d8"));
            Lbl(cc, $"{d.S("power")}. Cost: {d.S("cost")}.", 5, MUTED);
            if (p.grafts.Contains(k)) NoWrap(Lbl(row, "Taken", 6, MUTED, italic: true));
            else Btn(row, G.freeGraft ? "Take (free)" : "Take (1 Mere-ivory)", () =>
            {
                if (!G.freeGraft && p.bag.Count("ivory") < 1) { H.ToastMsg("The font asks an offering of Mere-ivory for each graft."); Snd.Deny(); return; }
                Confirm($"Take the {d.S("name")}? {d.S("power")}. It grows a knot at your {d.S("knot").ToLowerInvariant()}: {d.S("cost")}.", () =>
                {
                    p.grafts.Add(k);
                    if (G.freeGraft) G.freeGraft = false; else p.bag.Remove("ivory", 1);
                    Snd.Crack(); Snd.Bell(220, 0.4f); G.flash = 0.2f;
                    if (k == "lung") p.breath = 30;
                    G.After(0.4f, () => Narr.Think("firstGraft"));
                    H.ToastMsg($"The {d.S("name")} takes root. Salt crawls under your skin.");
                }, FontMenu);
            });
        }
        Rule(c);
        Btn(Row(c, true), "Step back", Close, style: MenuArt.Btn.Chip);
        Footer(c, ("confirm", "choose"), ("cancel", "step back"));
        FocusFirst();
    }
}
