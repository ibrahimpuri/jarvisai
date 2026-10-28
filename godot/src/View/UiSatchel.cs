// The satchel, laid out like a survival-horror case: Maren's condition on the left, the pockets in the middle, the item
// in hand on the right with what can be done with it. Behind the other bookmarks: crafting (the thing to make, what it
// needs, what she has), her gear, the journal and the chart.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class UiRoot
{
    static readonly string[] SAT_TABS = { "map", "log", "satchel", "craft", "gear" };
    static readonly Dictionary<string, string> SAT_NAMES = new() { ["map"] = "Map", ["log"] = "Journal", ["satchel"] = "Satchel", ["craft"] = "Craft", ["gear"] = "Gear" };
    string satSel, craftSel;          // the item and the recipe last in hand, kept across redraws
    VBoxContainer satDetail;          // the right-hand column of the satchel page
    Button satSlot;                   // the pocket last in hand

    public void Satchel(string tab = null)
    {
        var p = G.player;
        if (tab != null) satTab = tab;
        Show("satchel");
        benchBackOr = null;
        var c = Sheet(14, 8, 612, 344, MenuArt.Look.Slate, 0.75f);
        // the bookmarks along the top, and a way to close it for the mouse
        var tr = Row(c); tr.AddThemeConstantOverride("separation", 3);
        foreach (var t in SAT_TABS)
        {
            string tt = t;
            var b = Btn(tr, SAT_NAMES[t], () => Satchel(tt), selected: t == satTab, style: MenuArt.Btn.Tab);
            b.FocusMode = Control.FocusModeEnum.Click;
        }
        tr.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Btn(tr, "Close", Close, style: MenuArt.Btn.Chip).FocusMode = Control.FocusModeEnum.Click;
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        c.AddChild(body);
        satDetail = null; satSlot = null;
        switch (satTab)
        {
            case "map": Hints.Did("map"); body.AddChild(new MapView { CustomMinimumSize = new Vector2(584, 276), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); break;
            case "satchel": SatchelPage(body); break;
            case "craft": CraftView(body, "any", () => Satchel("craft")); break;
            case "gear": GearPage(body); break;
            case "log": JournalPage(body); break;
        }
        Footer(c, ("tabs", "turn the page"), ("confirm", "choose"), ("cancel", "close"));
        if (satTab != "satchel" && satTab != "craft") FocusFirstIn(body);
    }
    void FocusFirstIn(Control n)
    {
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(n) || !n.IsInsideTree()) return;
            var b = FindButtons(n).FirstOrDefault(x => !x.Disabled && x.IsVisibleInTree() && x.FocusMode == Control.FocusModeEnum.All);
            b?.GrabFocus();
        }).CallDeferred();
    }

    // ---- the satchel page ---------------------------------------------------------------------------------
    void SatchelPage(HBoxContainer body)
    {
        var p = G.player;
        // condition, as the suit's gauges read it
        var cond = Fixed(body, 158);
        ConditionCard(cond);
        // the pockets
        var mid = Fixed(body, 6 * 41);
        Lbl(mid, $"Satchel  ·  {p.bag.Used()} of {p.bag.size} pockets", 6, WARM, caps: true);
        var flow = new HFlowContainer { CustomMinimumSize = new Vector2(6 * 41, 0) };
        flow.AddThemeConstantOverride("h_separation", 3); flow.AddThemeConstantOverride("v_separation", 3);
        mid.AddChild(flow);
        Button first = null, want = null;
        int used = 0;
        foreach (var (i, n) in p.bag.List())
        {
            var def = Data.Item(i);
            bool key = def.B("key");
            int pockets = Bag.SlotsFor(i, n), left = n;
            int stacks = key ? 1 : pockets;
            for (int k = 0; k < stacks; k++)
            {
                int here = key ? n : Math.Min(def.I("perSlot"), left); left -= here;
                float w = key && pockets > 1 ? 38 * 2 + 3 : 38;
                string item = i; int all = n;
                var b = SlotBtn(flow, item, key ? "" : here.ToString(), w, 34, key);
                b.FocusEntered += () => { satSel = item; satSlot = b; Detail(item, all); };
                b.MouseEntered += () => b.GrabFocus();
                b.Pressed += () => { Snd.Ui(); FocusFirstIn(satDetail); };
                first ??= b; if (item == satSel && want == null) want = b;
            }
            used += pockets;
        }
        for (; used < p.bag.size; used++) { int seed = used; flow.AddChild(new Painted((c, sz, t) => MenuArt.Slot(c, sz.X, sz.Y, false, 0, t, seed), new Vector2(38, 34))); }
        Lbl(mid, "Rivets sit 18 to a pocket, bolts 6, flasks and poultices 3, materials 10. Key items take two pockets and cannot be put down.", 5, MUTED);
        // the item in hand
        satDetail = Fixed(body, 158);
        if (first == null) { Lbl(satDetail, "The satchel is empty.", 7, MUTED, italic: true); FocusFirstIn(cond); }
        else FocusOn(want ?? first);
        backAction = () =>
        {
            // from the item's actions back to its pocket, then out
            var f = root.GetViewport().GuiGetFocusOwner();
            if (f != null && satDetail != null && satDetail.IsAncestorOf(f) && satSlot != null && GodotObject.IsInstanceValid(satSlot)) satSlot.GrabFocus();
            else Close();
        };
    }
    // A label that keeps to one line and its own width, for rows and flows.
    static Label NoWrap(Label l) { l.AutowrapMode = TextServer.AutowrapMode.Off; l.SizeFlagsHorizontal = Control.SizeFlags.Fill; return l; }
    static VBoxContainer Fixed(Control parent, float w)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(w, 0), SizeFlagsHorizontal = Control.SizeFlags.Fill };
        v.AddThemeConstantOverride("separation", 3);
        parent.AddChild(v);
        return v;
    }
    // A pocket with an item in it: the oilcloth, the item drawn large, how many.
    Button SlotBtn(Control parent, string id, string count, float w, float h, bool key)
    {
        var b = new Button { CustomMinimumSize = new Vector2(w, h), FocusMode = Control.FocusModeEnum.All, TooltipText = Data.ItemName(id) };
        foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(0, 0, 0, 0));
        var art = Dress(b, MenuArt.Btn.Flat);
        art.Extra = (c, bt, st) =>
        {
            MenuArt.Slot(c, w, h, true, st, MenuArt.Now, SeedOf(id), key);
            if (Icons.Has(id)) Icons.Draw(c, id, w / 2, h / 2 - 1.5f, MathF.Min(w, h) * 0.66f);
            else { c.Fill = H.Col(Data.Item(id).S("color") ?? "#888"); c.FillCircle(w / 2, h / 2 - 1.5f, 6); }
            if (count != "")
            {
                c.Font = "7px"; c.TextAlign = 2;
                c.Fill = new Color(0, 0, 0, 0.85f); c.FillText(count, w - 2.5f, h - 2.5f);
                c.Fill = st > 0 ? H.Col("#fff2c8") : WARM; c.FillText(count, w - 3, h - 3);
                c.TextAlign = 0;
            }
        };
        parent.AddChild(b);
        return b;
    }
    // Health as a heartbeat and a word, the lantern, breath, the Hollow, the weapon in hand, the tide.
    void ConditionCard(VBoxContainer col)
    {
        var p = G.player;
        Lbl(col, "Condition", 6, WARM, caps: true);
        col.AddChild(new HeartGauge { CustomMinimumSize = new Vector2(158, 40) });
        float hf = p.hp / (float)Math.Max(1, p.maxHp);
        var (word, wc) = hf > 0.6f ? ("Steady", GOOD) : hf > 0.3f ? ("Hurt", H.Col("#f0b050")) : ("Bleeding out", BAD);
        var wr = Row(col);
        var wl = Lbl(wr, word, 11, wc); wl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f)); wl.AddThemeConstantOverride("shadow_offset_y", 1);
        wr.AddChild(new Painted((c, sz, t) =>
        {
            // a pip for each point of health, filled for what she has left
            for (int i = 0; i < p.maxHp; i++)
            {
                float x = sz.X - (p.maxHp - i) * 9 + 3, y = sz.Y / 2;
                c.Save(); c.Translate(x, y); c.Rotate(H.PI / 4);
                c.Fill = MenuArt.BRASS_DARK; c.FillRect(-3, -3, 6, 6);
                c.Fill = i < p.hp ? (hf > 0.3f ? H.Col("#c8483a") : H.Col("#e05040").Lerp(Colors.White, 0.2f * MathF.Sin(t * 8))) : new Color(0, 0, 0, 0.6f); c.FillRect(-2.2f, -2.2f, 4.4f, 4.4f);
                c.Restore();
            }
        }, new Vector2(0, 12)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        Gauge(col, "Lantern oil", () => p.oil / p.OilMax(), MenuArt.AMBER, $"{p.bag.Count("oil")} flasks");
        float bm = p.BreathMax();
        if (float.IsFinite(bm)) Gauge(col, "Breath", () => p.breath / p.BreathMax(), H.Col("#8fc8e0"), $"{(int)bm} s");
        else Lbl(col, "Breath: the Tide Lung breathes for her.", 5, MUTED, italic: true);
        if (Hollow.owned) Gauge(col, "Resonance", () => Hollow.resonance / Hollow.MAX, MenuArt.SEAGLASS, $"{(int)Hollow.resonance}");
        Rule(col);
        // the weapon in hand
        string wn = p.cur; var wd = Data.Weapons.P(wn);
        if (wd != null)
        {
            var wrow = Row(col);
            if (Icons.Has(wn)) wrow.AddChild(new IconView(wn) { CustomMinimumSize = new Vector2(20, 20) });
            var wc2 = Col(wrow);
            Lbl(wc2, wd.S("name"), 7, TEXT);
            string ammo = wd.S("ammo");
            string load = Player.Legendary(wn) ? "legendary" : $"tier {p.WeaponTier(wn) + 1} · {p.weapons[wn].mag}/{p.MagSize(wn)} loaded · {p.bag.Count(ammo)} {Data.ItemName(ammo).ToLowerInvariant()}";
            Lbl(wc2, load, 5, MUTED);
        }
        Lbl(col, $"{Tide.PhaseName()} tide, {H.FmtTime(Tide.PhaseRemaining())} until it turns", 5, MUTED, italic: true);
    }
    static void Gauge(VBoxContainer col, string name, Func<float> f, Color tint, string note)
    {
        var r = new HBoxContainer(); col.AddChild(r);
        var l = Lbl(r, name, 5, MUTED); l.SizeFlagsHorizontal = Control.SizeFlags.Fill; l.CustomMinimumSize = new Vector2(44, 0);
        r.AddChild(new Painted((c, sz, t) =>
        {
            float v = Math.Clamp(f(), 0, 1), y = sz.Y / 2 - 2;
            c.FillRect(0, y, sz.X, 4, new Color(0, 0, 0, 0.6f));
            c.FillRectGradV(0.5f, y + 0.5f, (sz.X - 1) * v, 3, tint.Lightened(0.2f), tint.Darkened(0.35f));
            c.Stroke = new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, 0.5f); c.LineWidth = 0.5f; c.StrokeRect(0, y, sz.X, 4);
            for (int i = 1; i < 4; i++) c.FillRect(sz.X * i / 4f, y, 0.5f, 4, new Color(0, 0, 0, 0.5f));
        }, new Vector2(0, 8)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var nl = Lbl(r, note, 5, MUTED); nl.SizeFlagsHorizontal = Control.SizeFlags.Fill; nl.CustomMinimumSize = new Vector2(30, 0); nl.HorizontalAlignment = HorizontalAlignment.Right;
    }
    // The right-hand column: the item drawn large, what it is, and what she can do with it.
    void Detail(string id, int n)
    {
        if (satDetail == null || !GodotObject.IsInstanceValid(satDetail)) return;
        foreach (var ch in satDetail.GetChildren()) { satDetail.RemoveChild(ch); ch.QueueFree(); }
        var p = G.player; var def = Data.Item(id);
        bool key = def.B("key");
        Lbl(satDetail, def.S("name"), 9, GOLD);
        satDetail.AddChild(new ItemView(id) { CustomMinimumSize = new Vector2(158, 52) });
        Lbl(satDetail, key ? "Key item" : $"{n} carried · {Bag.SlotsFor(id, n)} pocket{(Bag.SlotsFor(id, n) == 1 ? "" : "s")}", 5, MUTED, caps: true);
        Lbl(satDetail, def.S("desc"), 6, TEXT);
        var acts = Col(satDetail);
        acts.AddThemeConstantOverride("separation", 1);
        if (id is "poultice" or "stitchkit" or "oil") Btn(acts, "Use", () => UseItem(id));
        Btn(acts, "Examine", () => Examine(id));
        if (Data.Recipes.Any(r => r.Counts("cost").ContainsKey(id))) Btn(acts, "Craft with it", () => { craftSel = Data.Recipes.First(r => r.Counts("cost").ContainsKey(id)).S("id"); Satchel("craft"); });
        if (!key) Btn(acts, "Drop", () => { p.bag.Remove(id, n); G.SpawnLoose(id, p.Cx(), p.y + p.h - 4, n); Snd.Thud(); Satchel(); }, style: MenuArt.Btn.Danger);
    }
    void UseItem(string id)
    {
        var p = G.player;
        switch (id)
        {
            case "poultice":
            case "stitchkit":
                if (p.hp >= p.maxHp) { H.ToastMsg("You are not hurt.", 1); Snd.Deny(); return; }
                Close();
                if (id == "poultice") p.UsePoultice(); else p.UseStitch();
                break;
            case "oil":
                if (p.oil >= p.OilMax() - 0.5f) { H.ToastMsg("The lantern is full.", 1); Snd.Deny(); return; }
                p.bag.Remove("oil", 1); p.oil = p.OilMax(); Snd.Heal(); H.ToastMsg("You fill the lantern from a flask.", 1.5f);
                Satchel();
                break;
        }
    }
    // An item turned over in the hand, in the light of the lantern.
    public void Examine(string id)
    {
        var def = Data.Item(id);
        Show("examine");
        backAction = () => Satchel("satchel");
        dim.Dim = 0.85f;
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        col.AddThemeConstantOverride("separation", 4);
        center.AddChild(col);
        col.AddChild(new ItemView(id) { Turn = true, Glow = 1.2f, CustomMinimumSize = new Vector2(300, 110) });
        H1(col, def.S("name"));
        Rule(col);
        Lbl(col, def.S("examine") ?? def.S("desc"), 7, TEXT, italic: true, center: true);
        Lbl(col, def.S("desc"), 5, MUTED, center: true);
        var r = Row(col, true);
        Btn(r, "Back", () => Satchel("satchel"), style: MenuArt.Btn.Chip);
        Footer(col, ("cancel", "back")).Align = 1;
        FocusFirst();
    }

    // ---- crafting ----------------------------------------------------------------------------------------
    // On the left, what she knows how to make; on the right, the one in hand: what it makes, what it needs against
    // what she has, and the button. Below, everything on hand to make things from.
    void CraftView(HBoxContainer body, string where, Action again)
    {
        var p = G.player;
        var recipes = Data.Recipes.ToList();
        var list = Fixed(body, 168);
        Lbl(list, where == "bench" ? "The bench" : "Recipes", 6, WARM, caps: true);
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 3);
        body.AddChild(right);
        var card = new VBoxContainer(); card.AddThemeConstantOverride("separation", 3);
        right.AddChild(card);
        right.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        Rule(right);
        Lbl(right, "On hand", 6, WARM, caps: true);
        var hand = new HFlowContainer(); hand.AddThemeConstantOverride("h_separation", 8);
        right.AddChild(hand);
        foreach (var m in Bag.MATERIALS.Concat(new[] { "oil", "poultice" }))
        {
            var h = new HBoxContainer(); hand.AddChild(h);
            h.AddChild(new IconView(m) { CustomMinimumSize = new Vector2(13, 13) });
            NoWrap(Lbl(h, $"{Data.ItemName(m)} {p.bag.Count(m)}", 5, p.bag.Count(m) > 0 ? TEXT : MUTED));
        }
        Button want = null, first = null;
        foreach (var r in recipes)
        {
            var rr = r;
            bool here = where == "bench" || r.S("where") == "any";
            bool ok = here && p.bag.Has(r.Counts("cost")) && p.bag.Room(r.S("makes")) >= Diff.Yield(r);
            var b = Btn(list, $"{Data.ItemName(r.S("makes"))}", () => { Snd.Ui(); FocusFirstIn(card); });
            b.AddThemeStyleboxOverride("normal", Room(24, 3, 7, 3)); b.AddThemeStyleboxOverride("focus", Room(24, 3, 7, 3)); b.AddThemeStyleboxOverride("hover", Room(24, 3, 7, 3)); b.AddThemeStyleboxOverride("pressed", Room(24, 3, 7, 3));
            if (!ok) b.AddThemeColorOverride("font_color", MUTED);
            var art = b.GetChildren().OfType<ButtonArt>().First();
            string makes = r.S("makes");
            art.Extra = (c, bt, st) =>
            {
                Icons.Draw(c, makes, 15, bt.Size.Y / 2, 11, ok ? 1 : 0.45f);
                c.Font = "6px"; c.TextAlign = 2; c.Fill = ok ? WARM : new Color(MUTED, 0.6f); c.FillText(ok ? "ready" : here ? "short" : "bench", bt.Size.X - 4, bt.Size.Y / 2 + 2); c.TextAlign = 0;
            };
            b.FocusEntered += () => { craftSel = rr.S("id"); RecipeCard(card, rr, where, again, list); };
            b.MouseEntered += () => b.GrabFocus();
            first ??= b; if (r.S("id") == craftSel) want = b;
        }
        if (where == "any") Lbl(list, "Simple things can be made anywhere, crouched and exposed. The rest needs a Bell Room bench.", 5, MUTED);
        backAction = () =>
        {
            var f = root.GetViewport().GuiGetFocusOwner();
            if (f != null && card.IsAncestorOf(f)) { var sel = FindButtons(list).FirstOrDefault(x => x.HasMeta("rid") && (string)x.GetMeta("rid") == craftSel) ?? FindButtons(list).FirstOrDefault(); sel?.GrabFocus(); }
            else (benchBackOr ?? Close)();
        };
        foreach (var (b, r) in FindButtons(list).Zip(recipes)) b.SetMeta("rid", r.S("id"));
        FocusOn(want ?? first);
    }
    Action benchBackOr;               // where cancel goes from the bench's crafting page
    void RecipeCard(VBoxContainer card, Props r, string where, Action again, VBoxContainer list)
    {
        foreach (var ch in card.GetChildren()) { card.RemoveChild(ch); ch.QueueFree(); }
        var p = G.player;
        string makes = r.S("makes"); var def = Data.Item(makes);
        var cost = r.Counts("cost");
        bool here = where == "bench" || r.S("where") == "any";
        bool room = p.bag.Room(makes) >= Diff.Yield(r), have = p.bag.Has(cost);
        var top = Row(card); top.AddThemeConstantOverride("separation", 8);
        top.AddChild(new ItemView(makes) { CustomMinimumSize = new Vector2(64, 52), Glow = have && here ? 1 : 0.3f });
        var tc = Col(top);
        Lbl(tc, def.S("name"), 10, GOLD);
        Lbl(tc, $"Makes {Diff.Yield(r)}  ·  " + (r.S("where") == "any" ? (where == "bench" ? "at once, at the bench" : $"{r.F("time")} s, crouched") : "at a bench, at once"), 5, MUTED, caps: true);
        Lbl(tc, def.S("desc"), 6, TEXT);
        Rule(card);
        Lbl(card, "Needs", 6, WARM, caps: true);
        foreach (var kv in cost)
        {
            var row = Row(card);
            row.AddChild(new IconView(kv.Key) { CustomMinimumSize = new Vector2(14, 14) });
            var nl = Lbl(row, Data.ItemName(kv.Key), 7, TEXT);
            int got = p.bag.Count(kv.Key);
            var cl = Lbl(row, $"{got} / {kv.Value}", 7, got >= kv.Value ? GOOD : BAD); cl.SizeFlagsHorizontal = Control.SizeFlags.Fill; cl.HorizontalAlignment = HorizontalAlignment.Right; cl.CustomMinimumSize = new Vector2(40, 0);
        }
        Rule(card);
        string why = !here ? "Needs a Bell Room bench." : !have ? "Not enough to make it." : !room ? "No room in the satchel." : null;
        var br = Row(card); br.AddThemeConstantOverride("separation", 8);
        Btn(br, "Craft", () =>
        {
            if (where == "any") { Close(); p.QuickCraft(r.S("id")); return; }
            if (!p.bag.Has(cost) || p.bag.Room(makes) < Diff.Yield(r)) return;
            p.bag.Pay(cost); p.bag.Add(makes, Diff.Yield(r)); Snd.Craft(); H.ToastMsg($"{Diff.Yield(r)} {def.S("name")}.", 1.2f); again();
        }, primary: true, disabled: why != null);
        if (why != null) Lbl(br, why, 6, BAD, italic: true).VerticalAlignment = VerticalAlignment.Center;
    }

    // ---- gear ---------------------------------------------------------------------------------------------
    void GearPage(HBoxContainer body)
    {
        var p = G.player;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FollowFocus = true };
        body.AddChild(scroll);
        var cols = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(cols);
        var a = Fixed(cols, 200);
        Lbl(a, "Weapons", 6, WARM, caps: true);
        foreach (var id in p.weaponOrder)
        {
            var d = Data.Weapons.P(id); bool leg = Player.Legendary(id);
            int tier = p.WeaponTier(id); var tiers = d.Strings("tiers");
            var row = Row(a); row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new Painted((c, sz, t) =>
            {
                MenuArt.Slot(c, sz.X, sz.Y, true, id == p.cur ? 2 : 0, t, SeedOf(id));
                if (Icons.Has(id)) Icons.Draw(c, id, sz.X / 2, sz.Y / 2, 18);
            }, new Vector2(30, 26)));
            var cc = Col(row);
            Lbl(cc, d.S("name") + (id == p.cur ? "  · in hand" : ""), 7, leg ? H.Col("#bfe8e0") : TEXT);
            cc.AddChild(new Painted((c, sz, t) =>
            {
                // the tiers, as notches filed into a brass strip
                int n = leg ? 1 : tiers.Count;
                for (int i = 0; i < n; i++)
                {
                    float x = i * 12;
                    bool on = leg || i <= tier;
                    if (on) MenuArt.Brass(c, x, 1, 10, 4, i, 0.2f, false); else c.FillRect(x, 1, 10, 4, new Color(0, 0, 0, 0.5f));
                    c.Stroke = new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, 0.4f); c.LineWidth = 0.4f; c.StrokeRect(x, 1, 10, 4);
                }
            }, new Vector2(0, 6)));
            Lbl(cc, Glyphs.Plain(tiers[Math.Min(tier, tiers.Count - 1)], Glyphs.Current), 5, MUTED);
        }
        var b = Fixed(cols, 176);
        Lbl(b, "Relics", 6, WARM, caps: true);
        foreach (var ab in p.abilities)
        {
            var d = Data.Abilities.P(ab);
            var row = Row(b); row.AddThemeConstantOverride("separation", 5);
            if (Icons.Has(ab)) row.AddChild(new IconView(ab) { CustomMinimumSize = new Vector2(16, 16) });
            var cc = Col(row);
            Lbl(cc, d.S("name"), 7, TEXT);
            Lbl(cc, Glyphs.Plain(d.S("desc"), Glyphs.Current), 5, MUTED);
        }
        var cE = Fixed(cols, 176);
        int used = p.fittings.Sum(k => Data.Fittings.P(k).I("p"));
        Lbl(cE, "Fittings", 6, WARM, caps: true);
        var gr = Row(cE);
        gr.AddChild(new Painted((c, sz, t) => PressureDial(c, sz.X / 2, sz.Y - 2, 20, used, p.pressure, t), new Vector2(50, 26)));
        Lbl(gr, $"Pressure {used} of {p.pressure}. Fittings are changed at a bench.", 5, MUTED).VerticalAlignment = VerticalAlignment.Center;
        if (p.fittings.Count == 0) Small(cE, "None fitted.");
        foreach (var f in p.fittings) { var d = Data.Fittings.P(f); Lbl(cE, d.S("name"), 7, TEXT); Lbl(cE, d.S("effect") + ". " + d.S("trade") + ".", 5, MUTED); }
        Rule(cE);
        Lbl(cE, "Brine grafts", 6, WARM, caps: true);
        if (p.grafts.Count == 0) Small(cE, "None. The Choir's font offers them.");
        foreach (var k in p.grafts) { var d = Data.Grafts.P(k); Lbl(cE, d.S("name") + $"  ({d.S("knot")} knot)", 7, H.Col("#cfe0d8")); Lbl(cE, d.S("power") + ". " + d.S("cost") + ".", 5, MUTED); }
        if (Hollow.owned)
        {
            var hl = Data.Lore.P("hollow");
            Rule(cE);
            Lbl(cE, hl.S("name"), 6, WARM, caps: true);
            foreach (var v in Hollow.VERSES.Where(v => Hollow.Has(v.id)))
            {
                Lbl(cE, v.name + "  “" + v.line + "”", 6, MenuArt.SEAGLASS, italic: true);
                Lbl(cE, Glyphs.Plain(hl.P("verses").P(v.id).S("desc"), Glyphs.Current).Split("\n\n")[0] + $" Costs {(int)v.cost}.", 5, MUTED);
            }
            if (Hollow.verses.Count < Hollow.VERSES.Length) Small(cE, "The hymn has more verses. The Hollow learns them by listening.");
        }
    }
    // A pressure gauge: a brass dial, the safe arc and the red, a needle at the rating in use.
    static void PressureDial(Canvas c, float cx, float cy, float r, int used, int max, float t)
    {
        c.Fill = MenuArt.BRASS_DARK; c.BeginPath(); c.Arc(cx, cy, r + 1.5f, H.PI, H.PI * 2); c.ClosePath(); c.FillPath();
        c.Fill = H.Col("#d8ccae"); c.BeginPath(); c.Arc(cx, cy, r, H.PI, H.PI * 2); c.ClosePath(); c.FillPath();
        c.Stroke = H.Col("#b03a2a"); c.LineWidth = 2; c.BeginPath(); c.Arc(cx, cy, r - 3, H.PI * 1.8f, H.PI * 2); c.StrokePath();
        c.Stroke = H.Col("#2a1c10"); c.LineWidth = 0.5f;
        for (int i = 0; i <= max; i++) { float a = H.PI + i / (float)Math.Max(1, max) * H.PI; c.Line(cx + MathF.Cos(a) * (r - 1), cy + MathF.Sin(a) * (r - 1), cx + MathF.Cos(a) * (r - 4), cy + MathF.Sin(a) * (r - 4)); }
        float na = H.PI + Math.Clamp(used / (float)Math.Max(1, max), 0, 1) * H.PI + MathF.Sin(t * 9) * 0.01f;
        c.Stroke = H.Col("#1a0e04"); c.LineWidth = 1; c.Line(cx, cy, cx + MathF.Cos(na) * (r - 3), cy + MathF.Sin(na) * (r - 3));
        MenuArt.Rivet(c, cx, cy, 1.6f);
    }

    // ---- journal --------------------------------------------------------------------------------------------
    // An open book: on the left page the next step of the night and how to take it; on the right, the rest.
    void JournalPage(HBoxContainer body)
    {
        var step = Guide.Current;
        var left = Page(body, 286, 0);
        var right = Page(body, 286, 1);
        if (step != null)
        {
            Lbl(left, Guide.ActName(step.act), 5, INK_SOFT, caps: true);
            Lbl(left, step.title, 11, INK);
            Lbl(left, Guide.WhereLine(step), 6, INK_SOFT, italic: true);
            var now = step.now?.Invoke();
            if (now != null) Lbl(left, now, 7, H.Col("#8a2a18"));
            Lbl(left, Glyphs.Plain(step.how, Glyphs.Current), 7, INK);
            Btn(Row(left), "Show on the map", () => Satchel("map"), style: MenuArt.Btn.Chip);
            var before = Guide.Steps.Where(s0 => s0.act == step.act && Guide.Index(s0) < Guide.Index(step) && s0.done()).ToList();
            if (before.Count > 0)
            {
                InkRule(left);
                Lbl(left, "The way so far", 6, INK_SOFT, caps: true);
                foreach (var s0 in before) Lbl(left, "✓  " + s0.title, 6, INK_SOFT);
            }
        }
        else Lbl(left, "The night is over.", 11, INK);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FollowFocus = true, CustomMinimumSize = new Vector2(262, 0) };
        right.AddChild(scroll);
        var rc = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rc.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(rc);
        var also = Guide.Also();
        if (also.Count > 0)
        {
            Lbl(rc, "Also", 6, INK_SOFT, caps: true);
            foreach (var (t0, d0) in also) { Lbl(rc, t0, 7, INK); Lbl(rc, d0, 5, INK_SOFT); }
            InkRule(rc);
        }
        Lbl(rc, $"Agathe's log ({G.pages.Count}/9)", 6, INK_SOFT, caps: true);
        if (G.pages.Count < 9) Lbl(rc, "Your mother's pages are scattered across the island. All nine show the ferry's tides: the Keeper's Dark ending needs them, and two others alive to row.", 5, INK_SOFT);
        var pr = new HFlowContainer(); pr.AddThemeConstantOverride("h_separation", 3); pr.AddThemeConstantOverride("v_separation", 3); rc.AddChild(pr);
        if (G.pages.Count == 0) Lbl(pr, "No pages yet.", 5, INK_SOFT, italic: true);
        foreach (var n in G.pages.OrderBy(x => x)) Btn(pr, "Page " + n, () => Dialogue("Agathe's log", new List<string> { Story.PageText(n) }, () => Satchel("log")));
        Lbl(rc, $"Prior's tide tables ({G.tables.Count}/4)", 6, INK_SOFT, caps: true);
        var tr2 = new HFlowContainer(); tr2.AddThemeConstantOverride("h_separation", 3); rc.AddChild(tr2);
        if (G.tables.Count == 0) Lbl(tr2, "No tables yet.", 5, INK_SOFT, italic: true);
        foreach (var n in G.tables.OrderBy(x => x)) Btn(tr2, "Table " + n, () => Dialogue("Prior's tide table", new List<string> { Story.TABLES[n] }, () => Satchel("log")));
        InkRule(rc);
        Lbl(rc, $"Stitches {G.collected.Count(k => k.StartsWith("stitch"))}/4  ·  Brass Seals {G.collected.Count(k => k.StartsWith("seal"))}/6  ·  Knots broken {G.stats.knots.Values.Sum()}", 5, INK_SOFT);
    }
    // A page of the journal: parchment, its edge darker toward the spine.
    static VBoxContainer Page(HBoxContainer body, float w, int side)
    {
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(w, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        pc.AddThemeStyleboxOverride("panel", Room(side == 0 ? 14 : 16, 10, side == 0 ? 16 : 14, 8));
        var cv = new Canvas();
        pc.Draw += () =>
        {
            var c = cv.Begin(pc); float pw = pc.Size.X, ph = pc.Size.Y;
            MenuArt.Frame(c, pw, ph, MenuArt.Look.Parchment, 40 + side);
            // the spine's shadow and the page curling into it
            if (side == 0) c.FillRectGradH(pw - 18, 0, 18, ph, new Color(0.2f, 0.12f, 0.05f, 0), new Color(0.2f, 0.12f, 0.05f, 0.45f));
            else c.FillRectGradH(0, 0, 18, ph, new Color(0.2f, 0.12f, 0.05f, 0.45f), new Color(0.2f, 0.12f, 0.05f, 0));
            // a ribbon on the left page, a water stain on the right
            if (side == 0) { c.FillRect(pw - 30, -3, 5, 40, H.Col("#6a1e18")); c.Poly(new[] { new Vector2(pw - 30, 37), new Vector2(pw - 25, 37), new Vector2(pw - 25, 41), new Vector2(pw - 27.5f, 38.5f), new Vector2(pw - 30, 41) }); }
            else { c.Glow(pw * 0.78f, ph * 0.82f, 34, 24, new Color(MenuArt.STAIN.R, MenuArt.STAIN.G, MenuArt.STAIN.B, 0.2f)); }
        };
        body.AddChild(pc);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 3);
        pc.AddChild(col);
        return col;
    }
    static void InkRule(Control p) => p.AddChild(new Painted((c, sz, t) =>
    {
        c.Stroke = new Color(MenuArt.PAPER_INK.R, MenuArt.PAPER_INK.G, MenuArt.PAPER_INK.B, 0.45f); c.LineWidth = 0.5f;
        c.BeginPath(); c.MoveTo(4, sz.Y / 2); c.QuadraticCurveTo(sz.X / 2, sz.Y / 2 + 1.2f, sz.X - 4, sz.Y / 2 - 0.5f); c.StrokePath();
    }, new Vector2(0, 6)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

    // The chart. The island is larger than the sheet: the mouse wheel, the triggers or + and - zoom in around Maren, and
    // the right stick or the arrow keys move a closer view.
    partial class MapView : Control
    {
        readonly Canvas cv = new();
        public MapView() { ClipContents = true; MouseFilter = MouseFilterEnum.Pass; }
        public override void _Draw()
        {
            var c = cv.Begin(this);
            Chart.Draw(c, Size.X, Size.Y);
        }
        float zoomCool;
        public override void _Process(double delta)
        {
            float dt = (float)delta;
            zoomCool -= dt;
            int dz = 0;
            if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.Pageup) || Input.GetJoyAxis(0, JoyAxis.TriggerRight) > 0.6f) dz = 1;
            else if (Input.IsKeyPressed(Key.Minus) || Input.IsKeyPressed(Key.Pagedown) || Input.GetJoyAxis(0, JoyAxis.TriggerLeft) > 0.6f) dz = -1;
            if (dz != 0 && zoomCool <= 0) { ZoomBy(dz); zoomCool = 0.25f; }
            var pan = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
            if (pan.Length() < 0.25f) pan = Vector2.Zero;
            if (Input.IsKeyPressed(Key.Left)) pan.X -= 1; if (Input.IsKeyPressed(Key.Right)) pan.X += 1;
            if (Input.IsKeyPressed(Key.Up)) pan.Y -= 1; if (Input.IsKeyPressed(Key.Down)) pan.Y += 1;
            if (Chart.Zoom > 0 && pan != Vector2.Zero) Chart.Pan += pan * dt * 90 / Chart.Zoom;
            QueueRedraw();
        }
        static void ZoomBy(int dz)
        {
            int z = Math.Clamp(Chart.Zoom + dz, 0, Chart.MAX_ZOOM);
            if (z == Chart.Zoom) return;
            if (z == 0) Chart.Pan = Vector2.Zero;
            Chart.Zoom = z; Snd.Ui();
        }
        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            { ZoomBy(mb.ButtonIndex == MouseButton.WheelUp ? 1 : -1); AcceptEvent(); }
        }
    }
}
