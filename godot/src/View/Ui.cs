// Menus layered over the game: title, pause, dialogue, cinematics, Bell Rooms, bench, satchel, map.
// The game pauses while any of them is open. Sizes are in the 640x360 base resolution.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class UI
{
    public static UiRoot Inst;
    public static bool IsOpen() => Inst != null && Inst.Open;
    public static void Say(string who, string text, Action done = null) => Inst?.Dialogue(who, new List<string> { text }, done);
    public static void Dialogue(string who, List<string> lines, Action done = null) => Inst?.Dialogue(who, lines, done);
    public static void Confirm(string text, Action yes) => Inst?.Confirm(text, yes);
    public static void Title() => Inst?.Title();
    public static void Pause() => Inst?.Pause();
    public static void Map() => Inst?.Map();
    public static void Satchel() => Inst?.Satchel();
    public static void BellMenu(Bell b) => Inst?.BellMenu(b);
    public static void ReliquaryMenu() => Inst?.Reliquary(null);
    public static void BenchMenu(bool chapel) => Inst?.Bench(chapel, null);
    public static void SluiceMenu() => Inst?.SluiceMenu();
    public static void FontMenu() => Inst?.FontMenu();
    public static void Inscription(string title, string text) => Inst?.Inscription(title, text);
    public static void Cinematic(List<string> lines, Action done, string first = null) => Inst?.Cinematic(lines, done, first);
    public static void Death(string title, string line, string detail, Action onWake) => Inst?.Death(title, line, detail, onWake);
    public static void Ending(string title, List<string> paras, string stats, List<string> fates) => Inst?.Ending(title, paras, stats, fates);
    public static void Close() => Inst?.Close();
    public static bool CinematicOpen => Inst != null && Inst.cine != null;
    public static string Kind => Inst?.kind;
}

public partial class UiRoot : CanvasLayer
{
    static readonly Color TEXT = H.Col("#e6dcc0"), MUTED = H.Col("#9a8f78"), GOLD = H.Col("#d2aa5a"), WARM = H.Col("#ffd27a"), BAD = H.Col("#d07a6a"), GOOD = H.Col("#8fbf8f");
    Control root;
    ColorRect dim;
    public bool Open;
    public string kind = "";
    Theme theme;
    // dialogue state
    List<string> dlgLines; int dlgI; string dlgWho; Action dlgDone;
    // cinematic state
    public class Cine { public List<string> lines; public int i = -1; public Action done; public string first; public double timer; public Label label; public float fade; }
    public Cine cine;
    string difficulty = "keeper"; float tide = 1;
    string benchTab = "craft", satTab = "satchel";

    public override void _Ready()
    {
        Layer = 20;
        UI.Inst = this;
        theme = BuildTheme();
        root = new Control { Theme = theme, MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        root.Visible = false;
    }

    // ---- theme ----------------------------------------------------------------------------------
    static StyleBoxFlat Box(Color bg, Color border, int bw = 1, float pad = 6)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(bw);
        s.ContentMarginLeft = s.ContentMarginRight = pad; s.ContentMarginTop = s.ContentMarginBottom = pad * 0.6f;
        return s;
    }
    Theme BuildTheme()
    {
        var t = new Theme { DefaultFont = Canvas.GetFont(false, false, false), DefaultFontSize = 7 };
        var panel = Box(new Color(18 / 255f, 16 / 255f, 13 / 255f, 0.95f), H.Col("#5a4c36"), 1, 9);
        panel.ShadowColor = new Color(0, 0, 0, 0.6f); panel.ShadowSize = 8;
        t.SetStylebox("panel", "PanelContainer", panel);
        t.SetStylebox("normal", "Button", Box(H.Col("#241f18"), H.Col("#5a4c36"), 1, 5));
        t.SetStylebox("hover", "Button", Box(H.Col("#33291c"), GOLD, 1, 5));
        t.SetStylebox("pressed", "Button", Box(H.Col("#3d3020"), GOLD, 1, 5));
        t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), WARM, 1, 5));
        t.SetStylebox("disabled", "Button", Box(new Color(36 / 255f, 31 / 255f, 24 / 255f, 0.4f), new Color(90 / 255f, 76 / 255f, 54 / 255f, 0.4f), 1, 5));
        t.SetColor("font_color", "Button", TEXT);
        t.SetColor("font_hover_color", "Button", H.Col("#fff3d0"));
        t.SetColor("font_focus_color", "Button", H.Col("#fff3d0"));
        t.SetColor("font_pressed_color", "Button", H.Col("#fff3d0"));
        t.SetColor("font_disabled_color", "Button", new Color(TEXT, 0.4f));
        t.SetFontSize("font_size", "Button", 6);
        t.SetColor("font_color", "Label", TEXT);
        t.SetConstant("separation", "VBoxContainer", 3);
        t.SetConstant("separation", "HBoxContainer", 4);
        var sb = new StyleBoxFlat { BgColor = new Color(0.35f, 0.3f, 0.21f, 0.8f) };
        t.SetStylebox("grabber", "VScrollBar", sb);
        t.SetStylebox("grabber_highlight", "VScrollBar", sb);
        t.SetStylebox("scroll", "VScrollBar", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.2f) });
        return t;
    }

    // ---- building blocks ---------------------------------------------------------------------------
    void Show(string k, bool clear = false)
    {
        foreach (var c in root.GetChildren()) { root.RemoveChild(c); c.QueueFree(); }
        kind = k; Open = true; root.Visible = true;
        dim = new ColorRect { Color = clear ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.45f), MouseFilter = clear ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = clear ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        root.AddChild(dim);
        Inp.Clear();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    // A centred panel; returns its content column.
    VBoxContainer Panel(float width, float maxHeight = 340)
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        center.AddChild(pc);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(width - 18, 0) };
        pc.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(col);
        // size the scroll area to content, up to the max height
        col.Resized += () => scroll.CustomMinimumSize = new Vector2(width - 18, MathF.Min(col.Size.Y, maxHeight - 20));
        return col;
    }
    static Label Lbl(Control parent, string text, int size = 7, Color? color = null, bool italic = false, bool center = false, bool bold = false)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left };
        l.AddThemeFontSizeOverride("font_size", size);
        if (italic || bold) l.AddThemeFontOverride("font", Canvas.GetFont(false, italic, bold));
        l.AddThemeColorOverride("font_color", color ?? TEXT);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(l);
        return l;
    }
    static Label H1(Control p, string t) { var l = Lbl(p, t, 14, GOLD, center: true); return l; }
    static Label H2(Control p, string t) => Lbl(p, t, 9, GOLD);
    static Label H3(Control p, string t) => Lbl(p, t, 6, WARM);
    static Label Small(Control p, string t, bool muted = true) => Lbl(p, t, 5, muted ? MUTED : TEXT);
    Button Btn(Control parent, string text, Action onPress, bool primary = false, bool selected = false, bool disabled = false)
    {
        var b = new Button { Text = text, Disabled = disabled, FocusMode = Control.FocusModeEnum.All, ClipText = false };
        b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (primary || selected) b.AddThemeStyleboxOverride("normal", Box(selected ? H.Col("#33291c") : H.Col("#241f18"), GOLD, 1, 5));
        if (selected) b.AddThemeColorOverride("font_color", H.Col("#fff3d0"));
        b.Pressed += () => { Snd.Ui(); onPress(); };
        parent.AddChild(b);
        return b;
    }
    static HBoxContainer Row(Control parent, bool center = false)
    {
        var h = new HBoxContainer { Alignment = center ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin };
        parent.AddChild(h);
        return h;
    }
    static VBoxContainer Col(Control parent, float minW = 0)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(minW, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(v);
        return v;
    }
    // A listing row: name + description on the left, controls on the right.
    HBoxContainer ItemRow(Control parent, string name, string desc = null, string mutedSuffix = null)
    {
        var h = new HBoxContainer();
        parent.AddChild(h);
        var left = Col(h);
        var l = Lbl(left, name + (mutedSuffix != null ? "  " + mutedSuffix : ""), 6);
        if (desc != null) Lbl(left, desc, 5, MUTED);
        parent.AddChild(new HSeparator { Modulate = new Color(1, 1, 1, 0.25f) });
        return h;
    }
    void FocusFirst()
    {
        Callable.From(() =>
        {
            var b = FindButtons(root).FirstOrDefault(x => !x.Disabled);
            b?.GrabFocus();
        }).CallDeferred();
    }
    static IEnumerable<Button> FindButtons(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Button b) yield return b;
            foreach (var x in FindButtons(c)) yield return x;
        }
    }

    public void Close()
    {
        foreach (var c in root.GetChildren()) { root.RemoveChild(c); c.QueueFree(); }
        root.Visible = false; Open = false; kind = ""; dlgLines = null;
        if (cine != null) { var d = cine.done; cine = null; if (d != null) Callable.From(d).CallDeferred(); }
        Inp.Clear();
        Input.MouseMode = G.mode == "play" ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
    }

    // ---- input ---------------------------------------------------------------------------------------
    public override void _Input(InputEvent ev)
    {
        if (!Open) return;
        bool advance = ev.IsActionPressed("ui_accept") || (ev is InputEventKey k && k.Pressed && !k.Echo && (k.PhysicalKeycode == Key.E || k.PhysicalKeycode == Key.Space || k.PhysicalKeycode == Key.Enter))
                       || (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left);
        bool cancel = ev.IsActionPressed("ui_cancel") || (ev is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.PhysicalKeycode == Key.Escape);
        if (dlgLines != null && advance) { GetViewport().SetInputAsHandled(); Advance(); return; }
        if (cine != null)
        {
            if (cancel) { GetViewport().SetInputAsHandled(); FinishCine(); }
            else if (advance) { GetViewport().SetInputAsHandled(); NextCine(); }
            return;
        }
        if (kind == "inscription" && (advance || cancel)) { GetViewport().SetInputAsHandled(); Close(); return; }
        bool toggleKey = ev is InputEventKey k3 && k3.Pressed && !k3.Echo &&
            (((k3.PhysicalKeycode == Key.Tab || k3.PhysicalKeycode == Key.M) && kind == "map") || (k3.PhysicalKeycode == Key.I && kind == "satchel"));
        if (cancel || toggleKey)
        {
            if (kind == "title" || kind == "ending" || kind == "death") return;
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
    public override void _Process(double delta)
    {
        if (cine?.label != null)
        {
            cine.fade = MathF.Min(1, cine.fade + (float)delta / 1.1f);
            cine.label.Modulate = new Color(1, 1, 1, cine.fade);
            cine.timer -= delta;
            if (cine.timer <= 0) NextCine();
        }
    }

    // ---- dialogue ------------------------------------------------------------------------
    public void Dialogue(string who, List<string> lines, Action done)
    {
        dlgWho = who; dlgLines = lines.Where(l => !string.IsNullOrEmpty(l)).ToList(); dlgI = 0; dlgDone = done;
        if (dlgLines.Count == 0) { dlgLines = null; done?.Invoke(); return; }
        RenderDlg();
    }
    void RenderDlg()
    {
        var lines = dlgLines; int i = dlgI; string who = dlgWho;
        Show("dialogue", true);
        dlgLines = lines; dlgI = i; dlgWho = who;
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(253, 0) };
        pc.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        pc.GrowHorizontal = Control.GrowDirection.Both; pc.GrowVertical = Control.GrowDirection.Begin;
        pc.OffsetLeft = -126; pc.OffsetRight = 126; pc.OffsetBottom = -14;
        root.AddChild(pc);
        var col = new VBoxContainer();
        pc.AddChild(col);
        if (who != null) Lbl(col, who, 6, GOLD);
        Lbl(col, lines[i], 6).CustomMinimumSize = new Vector2(0, 16);
        var more = Lbl(col, i < lines.Count - 1 ? "E / Space  continue" : "E / Space  close", 4, MUTED);
        more.HorizontalAlignment = HorizontalAlignment.Right;
    }
    void Advance()
    {
        if (dlgLines == null) return;
        dlgI++;
        if (dlgI >= dlgLines.Count) { var d = dlgDone; dlgLines = null; Close(); d?.Invoke(); return; }
        RenderDlg();
    }
    public void Confirm(string text, Action yes)
    {
        Show("confirm");
        var c = Panel(190);
        Lbl(c, text, 6);
        var r = Row(c, true);
        Btn(r, "Yes", () => { Close(); yes(); }, primary: true);
        Btn(r, "No", Close);
        FocusFirst();
    }

    // ---- title -------------------------------------------------------------------------------
    public void Title()
    {
        Show("title");
        G.mode = "title";
        var c = Panel(190);
        H1(c, "HOLLOWMERE");
        Lbl(c, "The tide redraws the map every ten minutes, and brings the dead back with it.", 5, MUTED, italic: true, center: true);
        H3(c, "Difficulty");
        var r = Row(c);
        foreach (var (id, name) in new[] { ("pilgrim", "Pilgrim"), ("keeper", "Keeper"), ("drowned", "Drowned") })
            Btn(r, name, () => { difficulty = id; Title(); }, selected: difficulty == id);
        Small(c, difficulty switch { "pilgrim" => "More scrap and a slower tide.", "keeper" => "As designed.", _ => "Saving costs 1 tallow, fewer lifeline drops, harder hits." });
        H3(c, "Tide length");
        var r2 = Row(c);
        foreach (var v in new[] { 1f, 1.5f, 2f }) Btn(r2, (v * 100) + "%", () => { tide = v; Title(); }, selected: tide == v);
        Small(c, "More time between tides, without softening combat.");
        var col = Col(c);
        Btn(col, "Row out to the lighthouse", () => { Close(); G.NewGame(difficulty, tide); }, primary: true);
        if (G.HasSave()) Btn(col, "Continue from the last Bell Room", () => { Close(); G.LoadGame(); Input.MouseMode = Input.MouseModeEnum.Hidden; });
        Btn(col, "Controls", () => Controls(Title));
        Btn(col, "Toggle fullscreen (F11)", Settings.ToggleFullscreen);
        Btn(col, "Quit to desktop", () => GetTree().Quit());
        Lbl(c, "Mouse + keyboard or a gamepad. Best with headphones. F11 or Alt+Enter toggles fullscreen.", 4, MUTED, center: true);
        FocusFirst();
    }

    public void Controls(Action back)
    {
        Show("controls");
        var c = Panel(230);
        H2(c, "Controls");
        var k = new (string, string)[]
        {
            ("A / D", "Move"), ("W / S", "Climb ladders, swim, shorten / lengthen swing rope"), ("Space", "Jump (down + Space drops through planks)"),
            ("Shift (hold)", "Sneak: footsteps 2 m instead of 6 m"), ("C", "Dash (0.2 s invulnerable). Jet Valve: air and water bursts in any direction"),
            ("Mouse", "Aim"), ("Left click / J", "Fire the selected weapon"), ("Right click / K (hold)", "Focused beam: makes knots glint, double oil"),
            ("F", "Gaff strike (melee, pry plates, kick barrels)"), ("Q (hold)", "Gaff Swing from an iron ring / hook-pull a light enemy"),
            ("E", "Interact, talk, drag a downed body"), ("X", "Burn a downed body with lantern oil"), ("R", "Reload (locks you in place)"),
            ("T", "Shutter the lantern (nearly invisible, blind to weak points)"), ("Z", "Poultice (2 pips over 3 s)"), ("G", "Stitch Kit (full heal, kneel 4 s)"),
            ("V", "Quick-craft 3 rivets from 1 scrap (2 s, crouched)"), ("B", "Ballast Boots on / off"), ("1-4 / wheel", "Select weapon"),
            ("I", "Satchel, crafting, logbook"), ("Tab / M", "Map"), ("Esc / P", "Pause"),
            ("Gamepad", "Left stick move · A jump · B dash · X gaff · Y interact · LB swing · RB reload · RT fire · LT beam · right stick aim"),
        };
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6); grid.AddThemeConstantOverride("v_separation", 1);
        c.AddChild(grid);
        foreach (var (a, b) in k)
        {
            var key = Lbl(grid, a, 5, WARM); key.SizeFlagsHorizontal = Control.SizeFlags.Fill; key.CustomMinimumSize = new Vector2(52, 0);
            Lbl(grid, b, 5);
        }
        H3(c, "How Hollowmere works");
        Small(c, "Every Brine body is held up by salt knots at its joints. Rivets on flesh barely scratch; hit the knots. The Crown knot in the chest is sealed until two limb knots break. A dropped body lying in water when the next Rising tide reaches it will stand again at High tide: haul it above the tide line (E) or burn it (X).", false);
        Small(c, "The tide dial (top right) runs a 10-minute cycle: Low, Rising, High, Ebbing. Painted tide marks on pillars show where the water will reach. Bell Rooms are safe; resting there advances the tide to the next Low.", false);
        Btn(Row(c, true), "Back", () => (back ?? Close)());
        FocusFirst();
    }

    public void Pause()
    {
        Show("pause");
        var c = Panel(190);
        H2(c, "Paused");
        Small(c, $"{G.room.Name} · {Data.Regions.P(G.room.Region).S("name")} · Act {new string('I', G.act)}");
        H3(c, "Volume");
        var r = Row(c);
        foreach (var v in new[] { 0f, 0.35f, 0.7f, 1f })
            Btn(r, v == 0 ? "Off" : Mathf.RoundToInt(v * 100) + "%", () => { Snd.SetVolume(v); Settings.Save(); Pause(); }, selected: MathF.Abs(Snd.Volume - v) < 0.01f);
        H3(c, "Display");
        var r2 = Row(c);
        Btn(r2, "Fullscreen (F11)", Settings.ToggleFullscreen);
        Btn(r2, Settings.Bloom ? "Glow: on" : "Glow: off", () => { Settings.Bloom = !Settings.Bloom; Settings.Apply(); Settings.Save(); Pause(); }, selected: Settings.Bloom);
        Btn(r2, Settings.Shake ? "Shake: on" : "Shake: off", () => { Settings.Shake = !Settings.Shake; Settings.Save(); Pause(); }, selected: Settings.Shake);
        var col = Col(c);
        Btn(col, "Resume", Close, primary: true);
        Btn(col, "Controls", () => Controls(Pause));
        Btn(col, "Quit to title (progress since the last save is lost)", () => { Close(); G.ToTitle(); });
        Btn(col, "Quit to desktop (progress since the last save is lost)", () => GetTree().Quit());
        FocusFirst();
    }

    // ---- Bell Room ------------------------------------------------------------------------------
    bool PayDrowned()
    {
        var p = G.player;
        if (G.difficulty != "drowned") return true;
        if (p.bag.Count("tallow") < 1) { H.ToastMsg("Saving on Drowned costs 1 tallow."); Snd.Deny(); return false; }
        p.bag.Remove("tallow", 1);
        return true;
    }
    public void BellMenu(Bell bell)
    {
        var p = G.player;
        Show("bell");
        var c = Panel(200);
        H2(c, (bell.spec.S("name") ?? G.room.Name) + " Bell Room");
        Small(c, $"No Brine body will ever enter while the bell rings. Tide: {Tide.PhaseName()}, {H.FmtTime(Tide.PhaseRemaining())} left.");
        var col = Col(c);
        Btn(col, "Save" + (G.difficulty == "drowned" ? " (1 tallow)" : ""), () => { if (!PayDrowned()) return; G.SetRespawn(bell); G.Save(); H.ToastMsg("Saved."); BellMenu(bell); });
        Btn(col, "Rest: full health" + (p.grafts.Contains("lung") ? " (halved: Tide Lung)" : "") + ", save, and wait for the next Low tide", () => { if (!PayDrowned()) return; Close(); G.Rest(bell); }, primary: true);
        Btn(col, "Reliquary chest (shared by every Bell Room)", () => Reliquary(() => BellMenu(bell)));
        Btn(col, "Bench: craft, upgrades, fittings" + (bell.room.Id == "chapel" ? ", surgery" : ""), () => Bench(bell.room.Id == "chapel", () => BellMenu(bell)));
        Btn(col, "Leave", Close);
        Small(c, "Resting skips time. Every body left lying in water re-knots on the way.");
        FocusFirst();
    }

    public void Reliquary(Action back)
    {
        var p = G.player; var chest = G.reliquary;
        Show("reliquary");
        var c = Panel(270);
        H2(c, "Reliquary");
        var cols = Row(c);
        void Rows(VBoxContainer col, Bag bag, bool put)
        {
            var list = bag.List();
            if (list.Count == 0) Small(col, "Empty.");
            foreach (var (i, n) in list)
            {
                var row = ItemRow(col, Data.ItemName(i), null, "x" + n);
                Btn(row, "1", () => Move(i, false, put));
                Btn(row, "All", () => Move(i, true, put));
            }
        }
        void Move(string i, bool all, bool put)
        {
            if (put) { int n = Math.Min(p.bag.Count(i), all ? 999 : 1); int moved = n - chest.Add(i, n); p.bag.Remove(i, moved); }
            else { int n = Math.Min(chest.Count(i), all ? 999 : 1); int left = p.bag.Add(i, n); chest.Remove(i, n - left); if (left > 0) H.ToastMsg("Satchel full."); }
            Reliquary(back);
        }
        var a = Col(cols, 120); H3(a, $"Satchel ({p.bag.Used()}/{p.bag.size} slots)"); Rows(a, p.bag, true);
        var b = Col(cols, 120); H3(b, "Reliquary chest"); Rows(b, chest, false);
        Btn(Row(c), "Back", () => (back ?? Close)());
        FocusFirst();
    }

    void RecipeRows(VBoxContainer c, string where, Action after)
    {
        var p = G.player;
        foreach (var r in Data.Recipes.Where(r => where == "bench" || r.S("where") == "any"))
        {
            var cost = r.Counts("cost");
            bool ok = p.bag.Has(cost) && p.bag.Room(r.S("makes")) >= r.I("n");
            string costs = string.Join(", ", cost.Select(kv => $"{kv.Value} {Data.ItemName(kv.Key).ToLowerInvariant()} ({p.bag.Count(kv.Key)})"));
            var row = ItemRow(c, $"{r.I("n")} {Data.ItemName(r.S("makes"))}", costs + " · " + (r.S("where") == "any" ? "anywhere, " + r.F("time") + " s crouched" : "bench, instant"));
            Btn(row, "Craft", () =>
            {
                if (where == "any") { Close(); p.QuickCraft(r.S("id")); return; }
                if (!p.bag.Has(cost)) return;
                p.bag.Pay(cost); p.bag.Add(r.S("makes"), r.I("n")); Snd.Craft(); after();
            }, disabled: !ok);
        }
    }

    public void Bench(bool chapel, Action back)
    {
        var p = G.player;
        Show("bench");
        var c = Panel(270);
        H2(c, "Bench");
        var tabs = new List<string> { "craft", "upgrade", "fittings" }; if (chapel) tabs.Add("surgery");
        if (!tabs.Contains(benchTab)) benchTab = "craft";
        var tr = Row(c);
        foreach (var t in tabs) Btn(tr, char.ToUpper(t[0]) + t[1..], () => { benchTab = t; Bench(chapel, back); }, selected: t == benchTab);
        void Again() => Bench(chapel, back);
        if (benchTab == "craft") RecipeRows(c, "bench", Again);
        if (benchTab == "upgrade")
        {
            foreach (var id in p.weaponOrder)
            {
                var w = p.weapons[id]; var def = Data.Weapons.P(id);
                var next = w.tier + 1 < Data.UpgradeCost.Count ? Data.UpgradeCost[w.tier + 1] : null;
                var tiers = def.Strings("tiers");
                string cost = next != null ? string.Join(", ", next.Select(kv => $"{kv.Value} {Data.ItemName(kv.Key).ToLowerInvariant()} ({p.bag.Count(kv.Key)})")) : "";
                var row = ItemRow(c, def.S("name"), next != null ? "Next: " + tiers[w.tier + 1] + " · " + cost : "Fully upgraded.", "tier " + (w.tier + 1));
                if (next != null) Btn(row, "Upgrade", () =>
                {
                    if (!p.bag.Has(next)) return;
                    p.bag.Pay(next); w.tier++; Snd.Craft(); H.ToastMsg(def.S("name") + ": " + tiers[w.tier].Split(':')[0]); Again();
                }, disabled: !p.bag.Has(next));
            }
            Small(c, "Tiers add behaviours, not just damage. Mere-ivory comes from bosses and hidden vaults.");
        }
        if (benchTab == "fittings")
        {
            int used = p.fittings.Sum(k => Data.Fittings.P(k).I("p"));
            Small(c, $"Pressure Rating: {used} / {p.pressure}. Fittings are swapped only at Bell Rooms, so a loadout is a commitment for the next push.", false);
            if (p.fittingsOwned.Count == 0) Small(c, "You have not found any fittings yet. Search containers.");
            foreach (var f in p.fittingsOwned)
            {
                var d = Data.Fittings.P(f); bool on = p.fittings.Contains(f);
                var row = ItemRow(c, d.S("name"), $"{d.S("effect")}. Trade-off: {d.S("trade")}.", $"({d.I("p")})");
                Btn(row, on ? "Equipped" : "Equip", () =>
                {
                    if (p.fittings.Contains(f)) p.fittings.Remove(f);
                    else
                    {
                        int u = p.fittings.Sum(k => Data.Fittings.P(k).I("p"));
                        if (u + d.I("p") > p.pressure) { H.ToastMsg("Not enough Pressure Rating for that fitting."); Snd.Deny(); return; }
                        p.fittings.Add(f);
                    }
                    if (p.oil > p.OilMax()) p.oil = p.OilMax();
                    var wd = p.weapons["driver"]; if (wd.mag > p.MagSize("driver")) { p.bag.Add("rivets", wd.mag - p.MagSize("driver")); wd.mag = p.MagSize("driver"); }
                    Again();
                }, selected: on);
            }
            Small(c, "Diver: Cork Lining, Muffled Soles, Tallow Reservoir. Rigger: Rivet Feeder, Bilge Pump. Breaker: Knot Splitter, Lead Plates.");
        }
        if (benchTab == "surgery")
        {
            if (G.quests.B("surgeryUsed")) Lbl(c, "Ansel has already used his one good scalpel on you.", 6);
            else if (p.grafts.Count == 0) Small(c, "You carry no grafts.");
            else foreach (var k in p.grafts.ToList())
                {
                    var gd = Data.Grafts.P(k);
                    var row = ItemRow(c, gd.S("name"), $"{gd.S("power")}. Cost: {gd.S("cost")}.");
                    Btn(row, "Cut out (-1 pip)", () => Confirm($"Cut out the {gd.S("name")}? You lose 1 health pip permanently. Ansel can do this only once.", () =>
                    {
                        p.grafts.Remove(k); p.maxHp = Math.Max(3, p.maxHp - 1); p.hp = Math.Min(p.hp, p.maxHp); G.quests.Set("surgeryUsed", true);
                        Snd.Hurt(); H.ToastMsg("The graft is cut out. You will carry the scar.");
                        Again();
                    }));
                }
        }
        Btn(Row(c), "Back", () => (back ?? Close)());
        FocusFirst();
    }

    // ---- satchel / logbook ----------------------------------------------------------------------
    public void Satchel()
    {
        var p = G.player;
        Show("satchel");
        var c = Panel(270);
        var tr = Row(c);
        foreach (var (t, n) in new[] { ("satchel", "Satchel"), ("craft", "Craft"), ("gear", "Gear"), ("log", "Logbook") })
            Btn(tr, n, () => { satTab = t; Satchel(); }, selected: t == satTab);
        if (satTab == "satchel")
        {
            Small(c, $"{p.bag.Used()} / {p.bag.size} slots. Rivets 18 per slot, bolts 6, flasks and poultices 3, raw materials 10. Key items take 2 slots.");
            var grid = new GridContainer { Columns = 5 };
            grid.AddThemeConstantOverride("h_separation", 3); grid.AddThemeConstantOverride("v_separation", 3);
            c.AddChild(grid);
            int count = 0;
            foreach (var (i, n) in p.bag.List())
            {
                var def = Data.Item(i);
                int s = Bag.SlotsFor(i, n), left = n;
                for (int k = 0; k < s; k++)
                {
                    int here = def.B("key") ? n : Math.Min(def.I("perSlot"), left); left -= here;
                    var slot = new PanelContainer { CustomMinimumSize = new Vector2(48, 22), TooltipText = def.S("desc") };
                    slot.AddThemeStyleboxOverride("panel", Box(H.Col("#1b1813"), H.Col("#5a4c36"), 1, 3));
                    grid.AddChild(slot);
                    var v = new VBoxContainer(); slot.AddChild(v);
                    var top = new HBoxContainer(); v.AddChild(top);
                    var nm = Lbl(top, def.S("name") + (def.B("key") ? " (key)" : ""), 5);
                    var col = H.Col(def.S("color")); nm.AddThemeColorOverride("font_color", col.Lerp(TEXT, 0.4f));
                    if (!def.B("key")) Lbl(top, here.ToString(), 5, WARM).HorizontalAlignment = HorizontalAlignment.Right;
                    string item = i; int all = n;
                    if (k == 0) Btn(v, "Drop", () => { p.bag.Remove(item, all); G.SpawnLoose(item, p.Cx(), p.y + p.h - 4, all); Satchel(); });
                    count++;
                }
            }
            for (; count < p.bag.size; count++)
            {
                var slot = new PanelContainer { CustomMinimumSize = new Vector2(48, 22), Modulate = new Color(1, 1, 1, 0.35f) };
                slot.AddThemeStyleboxOverride("panel", Box(H.Col("#1b1813"), H.Col("#5a4c36"), 1, 3));
                grid.AddChild(slot);
                Lbl(slot, "—", 5, MUTED, center: true);
            }
        }
        if (satTab == "craft")
        {
            Small(c, "Simple recipes work anywhere but take time with Maren crouched and exposed. Complex ones need a Bell Room bench.");
            RecipeRows(c, "any", Satchel);
        }
        if (satTab == "gear")
        {
            var cols = Row(c);
            var a = Col(cols, 120);
            H3(a, $"Health {p.hp}/{p.maxHp} · Pressure {p.pressure}");
            foreach (var ab in p.abilities) { var d = Data.Abilities.P(ab); ItemRow(a, d.S("name"), d.S("desc")); }
            H3(a, "Weapons");
            foreach (var id in p.weaponOrder) { var d = Data.Weapons.P(id); ItemRow(a, d.S("name"), d.Strings("tiers")[p.weapons[id].tier], "tier " + (p.weapons[id].tier + 1)); }
            var b = Col(cols, 120);
            H3(b, "Fittings");
            if (p.fittings.Count == 0) Small(b, "None equipped.");
            foreach (var f in p.fittings) { var d = Data.Fittings.P(f); ItemRow(b, d.S("name"), d.S("effect") + " / " + d.S("trade")); }
            H3(b, "Brine Grafts");
            if (p.grafts.Count == 0) Small(b, "None. The Choir offers them.");
            foreach (var k in p.grafts) { var d = Data.Grafts.P(k); ItemRow(b, d.S("name"), d.S("power") + ". " + d.S("cost") + ".", "(" + d.S("knot") + " knot)"); }
        }
        if (satTab == "log")
        {
            var q = G.quests; var f = World.flags;
            var obj = new List<string>();
            if (!f.B("seawallOpen")) obj.Add("Fetch the Sluice Crank from the Tithe Hall altar and fit it to the seawall gate.");
            else if (G.act == 1) obj.Add("Cross the Saltworks and face the Abbot of Salt.");
            else if (!p.Has("jet")) obj.Add("Find a way through the Lower Cloister. The Prior's door answers to the hymnal.");
            else if (!p.Has("tidekey")) obj.Add("Climb to the Sunken Choir.");
            else if (!p.Has("seal")) obj.Add("Use the Tide Key to reach the Ribvault above the Choir.");
            else if (!f.B("skullOpen")) obj.Add("Weight the lift in the Prior's Walk and dive the Gullet. The Undertow waits there.");
            else obj.Add("Climb from the Ribvault Bell Room to the Lantern Skull.");
            if (q.S("teodor") == "asked") obj.Add("Odile: find Teodor in the Lower Cloister. Bring his tags, or break every limb knot but the Crown and bind him.");
            if (G.tables.Count > 0 && q.S("tables") != "given" && q.S("tables") != "sold") obj.Add($"Prior's tide tables: {G.tables.Count}/4. Ansel or the Choir's bowl.");
            H3(c, "Objectives");
            foreach (var o in obj) ItemRow(c, o);
            H3(c, $"Agathe's log ({G.pages.Count}/9)");
            var pr = Row(c);
            if (G.pages.Count == 0) Small(pr, "No pages yet.");
            foreach (var n in G.pages.OrderBy(x => x)) Btn(pr, "Page " + n, () => Dialogue("Agathe's log", new List<string> { Story.PAGES[n] }, Satchel));
            H3(c, $"Prior's tide tables ({G.tables.Count}/4)");
            var tr2 = Row(c);
            if (G.tables.Count == 0) Small(tr2, "No tables yet.");
            foreach (var n in G.tables.OrderBy(x => x)) Btn(tr2, "Table " + n, () => Dialogue("Prior's tide table", new List<string> { Story.TABLES[n] }, Satchel));
            Small(c, $"Stitches: {G.collected.Count(k => k.StartsWith("stitch"))}/4 · Brass Seals: {G.collected.Count(k => k.StartsWith("seal"))}/6 · Knots broken: {G.stats.knots.Values.Sum()}");
        }
        Btn(Row(c), "Close (I)", Close);
        FocusFirst();
    }

    public void Map()
    {
        Show("map");
        var c = Panel(270);
        var mapCtl = new MapView { CustomMinimumSize = new Vector2(253, 157) };
        c.AddChild(mapCtl);
        Btn(Row(c), "Close (Tab)", Close);
        FocusFirst();
    }
    partial class MapView : Control
    {
        readonly Canvas cv = new();
        public override void _Draw()
        {
            var c = cv.Begin(this);
            c.Scale(Size.X / 760f, Size.Y / 470f);
            Hud.DrawMap(c, 760, 470);
        }
    }

    // ---- Choir menus ---------------------------------------------------------------------------
    public void SluiceMenu()
    {
        var f = World.flags;
        if (f.B("choirDrained")) { Dialogue(null, new List<string> { "The shrine is drained. The sluice is jammed open with silt; it will never hold water again." }, null); return; }
        Show("sluice");
        var c = Panel(200);
        H2(c, "Choir sluice");
        Small(c, "The Tide Key turns in the wave-shaped lock. The Choir's water does not answer the moon; it answers the key.", false);
        var col = Col(c);
        string lvl = f.S("choirLevelName");
        Btn(col, "Raise the water to the vault (swim up to the Ribvault)", () => { G.SetChoirLevel("raised"); Close(); }, disabled: lvl == "raised");
        Btn(col, "Return the water to the shrine line", () => { G.SetChoirLevel("mid"); Close(); }, disabled: lvl == null || lvl == "mid");
        var drain = Btn(col, "Drain the shrine (permanent)", () => Confirm("Drain the Sunken Choir for good? Hesper wants the shrine kept flooded for the Saint's hymn. The Choir will turn on you.", () => G.SetChoirLevel("drained")));
        drain.AddThemeColorOverride("font_color", BAD);
        Btn(col, "Leave it", Close);
        FocusFirst();
    }
    public void FontMenu()
    {
        var p = G.player;
        Show("font");
        var c = Panel(210);
        H2(c, "The Brine font");
        Small(c, "Each graft gives a strong power but grows a knot on Maren herself, which enemies can strike. Grafts change how others treat you, and which endings are open. A graft can be cut out once, at a Keeper's surgery.", false);
        foreach (var k in Data.Grafts.Keys)
        {
            var d = Data.Grafts.P(k);
            var row = ItemRow(c, d.S("name"), $"{d.S("power")}. Cost: {d.S("cost")}.", "(" + d.S("knot") + ")");
            if (p.grafts.Contains(k)) Lbl(row, "Taken", 5, MUTED);
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
                });
            });
        }
        Btn(Row(c), "Step back", Close);
        FocusFirst();
    }

    // ---- end screens -----------------------------------------------------------------------------
    public void Ending(string title, List<string> paras, string stats, List<string> fates)
    {
        void PanelView()
        {
            Show("ending");
            var c = Panel(200);
            Lbl(c, title, 11, GOLD, center: true);
            foreach (var t in paras) Lbl(c, t, 6);
            if (fates != null && fates.Count > 0) { H3(c, "Afterwards"); foreach (var t in fates) Small(c, t, false); }
            Small(c, stats);
            Btn(Row(c, true), "Return to title", () => { Close(); G.ToTitle(); }, primary: true);
            FocusFirst();
        }
        var lines = new List<string> { title }; lines.AddRange(paras); if (fates != null) lines.AddRange(fates);
        Cinematic(lines, PanelView, "title");
    }
    public void Death(string title, string line, string detail, Action onWake)
    {
        Show("death");
        var c = Panel(190);
        H2(c, title);
        Lbl(c, line, 7, TEXT, italic: true);
        Small(c, detail);
        Btn(Row(c, true), "Wake at the bell", () => { Close(); onWake(); Input.MouseMode = Input.MouseModeEnum.Hidden; }, primary: true);
        FocusFirst();
    }

    // Words cut into the island, shown like an inscription.
    public void Inscription(string title, string text)
    {
        Show("inscription");
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(213, 0) };
        var bx = Box(new Color(18 / 255f, 22 / 255f, 24 / 255f, 0.96f), H.Col("#3c4448"), 1, 14);
        bx.ShadowColor = new Color(0, 0, 0, 0.7f); bx.ShadowSize = 10;
        pc.AddThemeStyleboxOverride("panel", bx);
        center.AddChild(pc);
        var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 8);
        pc.AddChild(col);
        Lbl(col, string.Join(" ", (title ?? "").ToUpperInvariant().ToCharArray()), 5, MUTED, center: true);
        var t = Lbl(col, text, 8, H.Col("#d8dfe0"), center: true);
        t.AddThemeColorOverride("font_shadow_color", new Color(0.6f, 0.78f, 0.86f, 0.25f));
        t.AddThemeConstantOverride("shadow_outline_size", 2);
        Lbl(col, "E  close", 4, MUTED, center: true);
    }

    // Black-screen text, one line at a time. E / Space / click advances, Esc skips.
    public void Cinematic(List<string> lines, Action done, string first = null)
    {
        Show("cinematic");
        dim.Color = Colors.Black;
        cine = new Cine { lines = lines.Where(l => !string.IsNullOrEmpty(l)).ToList(), done = done, first = first };
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(253, 0) };
        center.AddChild(label);
        cine.label = label;
        var skip = Lbl(root, "E  continue    Esc  skip", 4, H.Col("#5a5446"));
        skip.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        skip.GrowHorizontal = Control.GrowDirection.Begin; skip.GrowVertical = Control.GrowDirection.Begin;
        skip.OffsetRight = -10; skip.OffsetBottom = -8; skip.HorizontalAlignment = HorizontalAlignment.Right;
        NextCine();
    }
    void NextCine()
    {
        if (cine == null) return;
        cine.i++;
        if (cine.i >= cine.lines.Count) { FinishCine(); return; }
        string text = cine.lines[cine.i];
        bool big = cine.first == "title" && cine.i == 0;
        var l = cine.label;
        l.Text = big ? string.Join(" ", text.ToUpperInvariant().ToCharArray()) : text;
        l.AddThemeFontSizeOverride("font_size", big ? 12 : 8);
        l.AddThemeColorOverride("font_color", big ? GOLD : H.Col("#e2dac4"));
        cine.fade = 0;
        l.Modulate = new Color(1, 1, 1, 0);
        cine.timer = 2.6 + text.Length * 0.045;
    }
    void FinishCine()
    {
        if (cine == null) return;
        var done = cine.done;
        cine = null;
        Close();
        done?.Invoke();
    }
}
