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
    public static void Pause(string note = null) => Inst?.Pause(note);
    public static void Map() => Inst?.Satchel("map");
    public static void Satchel(string tab = "satchel") => Inst?.Satchel(tab);
    public static void BellMenu(Bell b) => Inst?.BellMenu(b);
    public static void ReliquaryMenu() => Inst?.Reliquary(null);
    public static void BenchMenu(bool chapel) => Inst?.Bench(chapel, null);
    public static void SluiceMenu() => Inst?.SluiceMenu();
    public static void FontMenu() => Inst?.FontMenu();
    public static void Inscription(string title, string text, Action done = null) => Inst?.Inscription(title, text, done);
    public static void Cinematic(List<string> lines, Action done, string first = null) => Inst?.Cinematic(lines, done, first);
    public static void Death(string title, string line, string detail, Action onWake) => Inst?.Death(title, line, detail, onWake);
    public static void Ending(string title, List<string> paras, string stats, List<string> fates) => Inst?.Ending(title, paras, stats, fates);
    public static void Close() => Inst?.Close();
    public static bool CinematicOpen => Inst != null && Inst.cine != null;
    public static string Kind => Inst?.kind;
    public static string DialogueWho => Inst?.DlgWho;
}

public partial class UiRoot : CanvasLayer
{
    static readonly Color TEXT = H.Col("#e6dcc0"), MUTED = H.Col("#9a8f78"), GOLD = H.Col("#d9b36a"), WARM = H.Col("#ffd27a"), BAD = H.Col("#d07a6a"), GOOD = H.Col("#8fd0a8"), INK = H.Col("#2a1c10"), INK_SOFT = H.Col("#5a4630");
    public static bool Instant;          // tests: text appears at once, no typewriter
    Control root;
    MenuBackdrop dim;
    public bool Open;
    public string kind = "";
    Theme theme;
    // dialogue state
    List<string> dlgLines; int dlgI; string dlgWho; Action dlgDone; Label dlgLabel; float dlgShown;
    // cinematic state
    public class Cine { public List<string> lines; public int i = -1; public Action done; public string first; public double timer; public Label label; public float fade; }
    public Cine cine;
    string difficulty = "keeper"; float tide = 1;
    string benchTab = "craft", satTab = "satchel";
    Action backAction;                 // what cancel (Esc, or B on a pad) does on this screen
    bool benchChapel; Action benchBack;

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
    // Every surface is painted (MenuArt): the theme only gives the words their colours and the controls their room.
    static StyleBoxEmpty Room(float l, float t, float r, float b) => new() { ContentMarginLeft = l, ContentMarginTop = t, ContentMarginRight = r, ContentMarginBottom = b };
    Theme BuildTheme()
    {
        var t = new Theme { DefaultFont = Canvas.GetFont(false, false, false), DefaultFontSize = 7 };
        t.SetStylebox("panel", "PanelContainer", Room(11, 9, 11, 9));
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) t.SetStylebox(st, "Button", Room(11, 2.5f, 7, 2.5f));
        t.SetColor("font_color", "Button", TEXT);
        t.SetColor("font_hover_color", "Button", H.Col("#fff3d0"));
        t.SetColor("font_focus_color", "Button", H.Col("#fff6dc"));
        t.SetColor("font_pressed_color", "Button", H.Col("#fff6dc"));
        t.SetColor("font_hover_pressed_color", "Button", H.Col("#fff6dc"));
        t.SetColor("font_disabled_color", "Button", new Color(TEXT, 0.35f));
        t.SetFontSize("font_size", "Button", 7);
        t.SetColor("font_color", "Label", TEXT);
        t.SetConstant("separation", "VBoxContainer", 3);
        t.SetConstant("separation", "HBoxContainer", 4);
        var sb = new StyleBoxFlat { BgColor = new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, 0.6f) };
        t.SetStylebox("grabber", "VScrollBar", sb);
        t.SetStylebox("grabber_highlight", "VScrollBar", sb);
        t.SetStylebox("grabber_pressed", "VScrollBar", sb);
        t.SetStylebox("scroll", "VScrollBar", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.3f), ContentMarginLeft = 2, ContentMarginRight = 2 });
        return t;
    }
    static int SeedOf(string s) { int h = 17; foreach (var ch in s ?? "") h = h * 31 + ch; return Math.Abs(h % 9973); }

    // ---- building blocks ---------------------------------------------------------------------------
    void Show(string k, bool clear = false)
    {
        foreach (var c in root.GetChildren()) { root.RemoveChild(c); c.QueueFree(); }
        kind = k; Open = true; root.Visible = true; backAction = null; dlgLabel = null;
        dim = new MenuBackdrop { Dim = clear ? 0 : 0.6f, Kelp = !clear, Shafts = !clear, MouseFilter = clear ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = clear ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        root.AddChild(dim);
        Inp.Clear();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    // A panel framed in one of the island's materials, the sea's growth over its edges.
    static void Ornate(PanelContainer pc, MenuArt.Look look = MenuArt.Look.Slate, int seed = 7, float growth = 1)
    {
        pc.AddThemeStyleboxOverride("panel", Room(13, 11, 13, 11));
        var cv = new Canvas();
        pc.Draw += () => { var c = cv.Begin(pc); MenuArt.Frame(c, pc.Size.X, pc.Size.Y, look, seed); };
        if (growth > 0) Callable.From(() => { if (GodotObject.IsInstanceValid(pc)) pc.AddChild(new FrameGrowth { Seed = seed, Amount = growth, Drips = look != MenuArt.Look.Parchment }); }).CallDeferred();
    }
    // A centred panel; returns its content column.
    VBoxContainer Panel(float width, float maxHeight = 340, MenuArt.Look look = MenuArt.Look.Slate, float growth = 1)
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        Ornate(pc, look, SeedOf(kind) + (int)width, growth);
        center.AddChild(pc);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(width - 26, 0), FollowFocus = true };
        pc.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(col);
        // size the scroll area to content, up to the max height
        col.Resized += () => scroll.CustomMinimumSize = new Vector2(width - 26, MathF.Min(col.Size.Y, maxHeight - 24));
        return col;
    }
    static Label Lbl(Control parent, string text, int size = 7, Color? color = null, bool italic = false, bool center = false, bool bold = false, bool caps = false)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left };
        l.AddThemeFontSizeOverride("font_size", size);
        if (italic || bold || caps) l.AddThemeFontOverride("font", Canvas.GetFont(false, italic, bold, caps));
        l.AddThemeColorOverride("font_color", color ?? TEXT);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(l);
        return l;
    }
    static Label H1(Control p, string t) { var l = Lbl(p, t, 14, GOLD, center: true); return l; }
    static Label H2(Control p, string t) { var l = Lbl(p, t, 10, GOLD); l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f)); l.AddThemeConstantOverride("shadow_offset_y", 1); return l; }
    static Label H3(Control p, string t) => Lbl(p, t, 6, WARM, caps: true);
    static Label Small(Control p, string t, bool muted = true) => Lbl(p, t, 5, muted ? MUTED : TEXT);
    // A rule with a diamond, between sections.
    static void Rule(Control p, float w = 0)
    {
        p.AddChild(new Painted((c, sz, t) =>
        {
            float cx = sz.X / 2, y = sz.Y / 2, half = (w > 0 ? w : sz.X) / 2 - 4;
            var g = new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, 0.55f);
            c.FillRectGradH(cx - half, y, half - 5, 0.7f, new Color(g.R, g.G, g.B, 0), g);
            c.FillRectGradH(cx + 5, y, half - 5, 0.7f, g, new Color(g.R, g.G, g.B, 0));
            c.Save(); c.Translate(cx, y + 0.3f); c.Rotate(H.PI / 4); c.Fill = g; c.FillRect(-1.8f, -1.8f, 3.6f, 3.6f); c.Restore();
        }, new Vector2(0, 7)) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
    }
    Button Btn(Control parent, string text, Action onPress, bool primary = false, bool selected = false, bool disabled = false, MenuArt.Btn? style = null)
    {
        var b = new Button { Text = text, Disabled = disabled, FocusMode = Control.FocusModeEnum.All, ClipText = false };
        // Buttons in a full-width column wrap long labels; buttons in a row size to their text.
        bool column = parent is VBoxContainer;
        if (column) { b.AutowrapMode = TextServer.AutowrapMode.WordSmart; b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; b.Alignment = HorizontalAlignment.Left; }
        else b.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var st = style ?? (primary ? MenuArt.Btn.Primary : column ? MenuArt.Btn.List : MenuArt.Btn.Chip);
        Dress(b, st, selected);
        b.Pressed += () => { Snd.Ui(); onPress(); };
        parent.AddChild(b);
        return b;
    }
    // Give a button its look and the right colours for the surface it is drawn on.
    static ButtonArt Dress(Button b, MenuArt.Btn st, bool on = false)
    {
        var art = new ButtonArt { Style = st, On = on, Seed = SeedOf(b.Text) };
        b.AddChild(art);
        switch (st)
        {
            case MenuArt.Btn.Primary:
                b.Alignment = HorizontalAlignment.Center;
                b.AddThemeFontOverride("font", Canvas.GetFont(false, false, false, true));
                b.AddThemeFontSizeOverride("font_size", 8);
                foreach (var k in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" }) b.AddThemeColorOverride(k, H.Col("#1a0e04"));
                b.AddThemeColorOverride("font_disabled_color", new Color(0.1f, 0.06f, 0.02f, 0.5f));
                foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(14, 3.5f, 14, 3.5f));
                break;
            case MenuArt.Btn.Chip:
                if (on) foreach (var k in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" }) b.AddThemeColorOverride(k, H.Col("#140a02"));
                foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(6, 2, 6, 2));
                b.AddThemeFontSizeOverride("font_size", 6);
                break;
            case MenuArt.Btn.Tab:
                b.AddThemeFontOverride("font", Canvas.GetFont(false, false, false, true));
                b.AddThemeFontSizeOverride("font_size", 7);
                foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(9, on ? 5 : 3, 9, on ? 7 : 5));
                if (!on) b.AddThemeColorOverride("font_color", H.Col("#c0a888"));
                break;
            case MenuArt.Btn.Danger:
                b.AddThemeColorOverride("font_color", H.Col("#d8a090")); b.AddThemeColorOverride("font_focus_color", H.Col("#ffc8b8")); b.AddThemeColorOverride("font_hover_color", H.Col("#ffc8b8"));
                break;
            case MenuArt.Btn.Title:
                b.AddThemeFontSizeOverride("font_size", 9);
                b.AddThemeColorOverride("font_color", H.Col("#cdbf9f"));
                foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(16, 3, 14, 3));
                break;
        }
        return art;
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
    void FocusFirst()
    {
        Callable.From(() =>
        {
            var b = FindButtons(root).FirstOrDefault(x => !x.Disabled && x.IsVisibleInTree());
            b?.GrabFocus();
        }).CallDeferred();
    }
    void FocusOn(Button b) => Callable.From(() => { if (GodotObject.IsInstanceValid(b) && b.IsInsideTree()) b.GrabFocus(); }).CallDeferred();
    static IEnumerable<Button> FindButtons(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Button b) yield return b;
            foreach (var x in FindButtons(c)) yield return x;
        }
    }

    // A row of button prompts, e.g. "(B) Back  (LB/RB) Change page"; actions with no control on
    // the current device are left out, and the row redraws when the player changes device.
    static RichLabel Footer(Control parent, params (string action, string label)[] items)
    {
        var l = new RichLabel(() => string.Join("     ", items.Where(i => Glyphs.Controls(i.action, Glyphs.Current).Length > 0).Select(i => "{" + i.action + "} " + i.label)), 5, MUTED, 2);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(l);
        return l;
    }

    Action inscDone;
    public void Close()
    {
        var after = kind == "inscription" ? inscDone : null; inscDone = null;
        if (after != null) Callable.From(after).CallDeferred();
        foreach (var c in root.GetChildren()) { root.RemoveChild(c); c.QueueFree(); }
        root.Visible = false; Open = false; kind = ""; dlgLines = null; dlgLabel = null;
        if (cine != null) { var d = cine.done; cine = null; if (d != null) Callable.From(d).CallDeferred(); }
        Inp.Clear();
        Input.MouseMode = G.mode == "play" ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
    }

    // ---- test hooks ------------------------------------------------------------------------------------
    public string DlgWho => dlgLines != null ? dlgWho : null;
    public void TestAccept()
    {
        if (dlgLines != null) Advance();
        else if (cine != null) NextCine();
        else if (kind == "inscription") Close();
    }
    public void TestCancel()
    {
        if (cine != null) { FinishCine(); return; }
        if (kind is "title" or "ending" or "death") return;
        Close();
    }
    // What Esc or B does on this screen.
    public void TestBack() { if (backAction != null) backAction(); else Close(); }
    public bool TestClick(string textStart)
    {
        var b = FindButtons(root).FirstOrDefault(x => x.Text.StartsWith(textStart) && !x.Disabled && x.IsVisibleInTree());
        if (b == null) return false;
        b.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }
    public string AllText()
    {
        var sb = new System.Text.StringBuilder();
        void Walk(Node n) { foreach (var c in n.GetChildren()) { if (c is Label l) sb.Append(l.Text).Append('\n'); if (c is Button bt) sb.Append(bt.Text).Append('\n'); Walk(c); } }
        Walk(root);
        return sb.ToString();
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
        if (kind == "satchel" && ev is InputEventKey k3 && k3.Pressed && !k3.Echo)
        {
            // Tab / M and I open the journal at the map and the satchel; pressed again, they close it.
            string want = k3.PhysicalKeycode is Key.Tab or Key.M ? "map" : k3.PhysicalKeycode == Key.I ? "satchel" : null;
            if (want != null)
            {
                GetViewport().SetInputAsHandled();
                if (satTab == want) Close(); else Satchel(want);
                return;
            }
        }
        if (cancel)
        {
            if (kind == "title" || kind == "ending" || kind == "death") return;
            GetViewport().SetInputAsHandled();
            if (backAction != null) backAction(); else Close();
        }
    }
    // Pad buttons the menus use beyond confirm and back: bumpers turn pages, View and Start close.
    public void PadButton(JoyButton b)
    {
        if (b is JoyButton.LeftShoulder or JoyButton.RightShoulder)
        {
            int d = b == JoyButton.RightShoulder ? 1 : -1;
            if (kind == "satchel") { int i = Array.IndexOf(SAT_TABS, satTab); Satchel(SAT_TABS[(i + d + SAT_TABS.Length) % SAT_TABS.Length]); Snd.Ui(); }
            else if (kind == "bench") { var tabs = BenchTabs(benchChapel); int i = tabs.IndexOf(benchTab); benchTab = tabs[(i + d + tabs.Count) % tabs.Count]; Bench(benchChapel, benchBack); Snd.Ui(); }
            else if (kind == "controls") { Controls(controlsBack, controlsTab == "pad" ? "keys" : "pad"); Snd.Ui(); }
        }
        else if (b == JoyButton.Back && kind == "satchel") Close();
        else if (b == JoyButton.Touchpad && kind == "satchel") { if (satTab == "map") Close(); else Satchel("map"); }
        else if (b == JoyButton.Start)
        {
            if (cine != null) FinishCine();
            else if (kind is "pause" or "satchel" or "controls" or "controller" or "settings") Close();
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
        // the typewriter: the words come at the pace of speech
        if (dlgLabel != null && GodotObject.IsInstanceValid(dlgLabel) && dlgLabel.VisibleRatio < 1)
        {
            dlgShown += (float)delta * 55;
            int total = Math.Max(1, dlgLabel.GetTotalCharacterCount());
            dlgLabel.VisibleCharacters = Math.Min(total, (int)dlgShown);
            if (dlgLabel.VisibleCharacters >= total) dlgLabel.VisibleRatio = 1;
        }
    }

    // ---- dialogue ------------------------------------------------------------------------
    // A plank of driftwood across the foot of the view, the speaker's name on a brass plate, their likeness in a
    // medallion, the words coming at the pace of speech.
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
        string npc = Story.NpcIdFor(who);
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(420, 66) };
        Ornate(pc, MenuArt.Look.Slate, SeedOf(who ?? "page") + 3, 0.8f);
        pc.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        pc.GrowHorizontal = Control.GrowDirection.Both; pc.GrowVertical = Control.GrowDirection.Begin;
        pc.OffsetLeft = -210; pc.OffsetRight = 210; pc.OffsetBottom = -12;
        root.AddChild(pc);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 9);
        pc.AddChild(row);
        if (npc != null || who != null)
            row.AddChild(new Painted((c, sz, t) => Portrait(c, sz, npc, who, t), new Vector2(50, 50)) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(col);
        if (who != null)
        {
            var name = Lbl(col, who, 7, GOLD, caps: true);
            name.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f)); name.AddThemeConstantOverride("shadow_offset_y", 1);
        }
        var text = Lbl(col, Glyphs.Plain(lines[i], Glyphs.Current), 7);
        text.CustomMinimumSize = new Vector2(0, 26);
        text.AddThemeConstantOverride("line_spacing", 1);
        dlgShown = 0; dlgLabel = text;
        if (Instant) text.VisibleRatio = 1; else text.VisibleCharacters = 0;
        var foot = Row(col);
        Footer(foot, ("confirm", i < lines.Count - 1 ? "continue" : "close"));
        foot.AddChild(new Painted((c, sz, t) =>
        {
            if (dlgLabel == null || !GodotObject.IsInstanceValid(dlgLabel) || dlgLabel.VisibleRatio < 1) return;
            float y = sz.Y / 2 + MathF.Sin(t * 4) * 1.2f;
            c.Fill = new Color(MenuArt.AMBER.R, MenuArt.AMBER.G, MenuArt.AMBER.B, 0.85f);
            c.Poly(new[] { new Vector2(sz.X - 8, y - 2), new Vector2(sz.X - 2, y - 2), new Vector2(sz.X - 5, y + 2) });
        }, new Vector2(12, 8)));
    }
    // A likeness in a brass medallion: the speaker's robe or cowl, their face, their hat, the lamp if they carry one; for a
    // page with no speaker, the page itself. Everything is cut to the round of the glass.
    static void Portrait(Canvas c, Vector2 sz, string npc, string who, float t)
    {
        float cx = sz.X / 2, cy = sz.Y / 2, r = MathF.Min(sz.X, sz.Y) / 2 - 2;
        c.Fill = new Color(0, 0, 0, 0.6f); c.FillCircle(cx + 1, cy + 1.5f, r + 2);
        c.Fill = MenuArt.BRASS_DARK; c.FillCircle(cx, cy, r + 2);
        c.Fill = MenuArt.BRASS; c.FillCircle(cx, cy, r + 1);
        c.Fill = H.Col("#0b1214"); c.FillCircle(cx, cy, r - 0.5f);
        c.Glow(cx - r * 0.3f, cy - r * 0.4f, r * 1.1f, new Color(0.3f, 0.5f, 0.5f, 0.22f));
        float ri = r - 0.6f;
        void Clip(float ex, float ey, float rx, float ry, Color col)
        {
            var pts = EllipseInCircle(ex, ey, rx, ry, cx, cy, ri);
            if (pts.Length >= 3) { c.Fill = col; c.Poly(pts); }
        }
        if (npc != null)
        {
            var look = Story.NpcLook(npc);
            var robe = H.Col(look.robe); var skin = H.Col(look.skin);
            if (look.lamp) c.Glow(cx + r * 0.6f, cy + r * 0.5f, r * 0.9f, new Color(1, 0.72f, 0.35f, 0.3f + 0.08f * MathF.Sin(t * 6)));
            // a cowl behind the head if there is no hat
            if (look.hat == null) Clip(cx, cy - r * 0.02f, r * 0.46f, r * 0.52f, robe.Darkened(0.35f));
            // shoulders
            Clip(cx, cy + r * 0.98f, r * 0.82f, r * 0.62f, robe);
            Clip(cx + r * 0.25f, cy + r * 1.02f, r * 0.6f, r * 0.55f, robe.Darkened(0.25f));
            c.Fill = skin.Darkened(0.35f); c.FillRect(cx - r * 0.11f, cy + r * 0.22f, r * 0.22f, r * 0.2f);
            // the face, lit from the left
            c.Fill = skin.Darkened(0.2f); c.BeginPath(); c.Ellipse(cx, cy - r * 0.02f, r * 0.29f, r * 0.35f, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = skin; c.BeginPath(); c.Ellipse(cx - r * 0.04f, cy - r * 0.05f, r * 0.24f, r * 0.31f, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = new Color(1, 1, 1, 0.12f); c.BeginPath(); c.Ellipse(cx - r * 0.12f, cy - r * 0.15f, r * 0.08f, r * 0.12f, 0, 0, H.PI * 2); c.FillPath();
            // sockets, eyes, a brow, the nose's shadow, the mouth
            c.Fill = new Color(0, 0, 0, 0.35f);
            c.BeginPath(); c.Ellipse(cx - r * 0.11f, cy - r * 0.06f, r * 0.08f, r * 0.05f, 0, 0, H.PI * 2); c.FillPath();
            c.BeginPath(); c.Ellipse(cx + r * 0.11f, cy - r * 0.06f, r * 0.08f, r * 0.05f, 0, 0, H.PI * 2); c.FillPath();
            bool blink = (t % 4.3f) < 0.12f;
            c.Fill = H.Col("#0b0807");
            if (blink) { c.FillRect(cx - r * 0.16f, cy - r * 0.06f, r * 0.1f, 0.5f); c.FillRect(cx + r * 0.06f, cy - r * 0.06f, r * 0.1f, 0.5f); }
            else
            {
                c.FillCircle(cx - r * 0.11f, cy - r * 0.06f, r * 0.04f); c.FillCircle(cx + r * 0.11f, cy - r * 0.06f, r * 0.04f);
                // salt has taken one of the crusted ones' eyes
                c.Fill = look.crust ? new Color(0.85f, 0.9f, 0.88f, 0.9f) : new Color(1, 0.95f, 0.85f, 0.8f);
                if (look.crust) c.FillCircle(cx + r * 0.11f, cy - r * 0.06f, r * 0.035f);
                c.FillRect(cx - r * 0.12f, cy - r * 0.08f, 0.6f, 0.6f);
            }
            c.Fill = new Color(0, 0, 0, 0.3f); c.FillRect(cx - r * 0.17f, cy - r * 0.14f, r * 0.12f, r * 0.03f); c.FillRect(cx + r * 0.05f, cy - r * 0.14f, r * 0.12f, r * 0.03f);
            c.FillRect(cx + r * 0.01f, cy - r * 0.02f, r * 0.04f, r * 0.12f);
            c.Fill = new Color(0.2f, 0.08f, 0.06f, 0.7f); c.FillRect(cx - r * 0.08f, cy + r * 0.16f, r * 0.16f, r * 0.025f);
            if (look.crust)
            {
                for (int i = 0; i < 9; i++) { c.Fill = new Color(0.9f, 0.95f, 0.95f, 0.5f + 0.4f * TileArt.Hash(i, 5)); c.FillRect(cx + r * 0.04f + TileArt.Hash(i, 3) * r * 0.22f, cy - r * 0.3f + TileArt.Hash(i, 4) * r * 0.5f, 0.8f, 0.8f); }
                MenuArt.Barnacles(c, cx + r * 0.2f, cy + r * 0.22f, r * 0.08f, 6, 3);
            }
            if (look.hat != null)
            {
                var hat = H.Col(look.hat);
                Clip(cx, cy - r * 0.3f, r * 0.55f, r * 0.09f, hat.Darkened(0.2f));
                c.Fill = hat; c.BeginPath(); c.Ellipse(cx, cy - r * 0.32f, r * 0.3f, r * 0.22f, 0, H.PI, H.PI * 2); c.FillPath();
                c.Fill = new Color(1, 1, 1, 0.12f); c.FillRect(cx - r * 0.28f, cy - r * 0.36f, r * 0.56f, r * 0.03f);
            }
        }
        else
        {
            // no face: the page itself
            c.Fill = MenuArt.PAPER; c.Save(); c.Translate(cx, cy); c.Rotate(-0.12f); c.FillRect(-r * 0.45f, -r * 0.55f, r * 0.9f, r * 1.1f); c.Restore();
            c.Stroke = new Color(MenuArt.PAPER_INK.R, MenuArt.PAPER_INK.G, MenuArt.PAPER_INK.B, 0.6f); c.LineWidth = 0.5f;
            for (int i = 0; i < 5; i++) c.Line(cx - r * 0.3f, cy - r * 0.35f + i * r * 0.18f, cx + r * 0.3f, cy - r * 0.38f + i * r * 0.18f);
        }
        // the glass over it, and a barnacle on the rim
        c.Glow(cx - r * 0.35f, cy - r * 0.4f, r * 0.4f, new Color(1, 1, 1, 0.08f));
        c.Stroke = new Color(0, 0, 0, 0.5f); c.LineWidth = 1; c.BeginPath(); c.Arc(cx, cy, r - 0.5f, 0, H.PI * 2); c.StrokePath();
        MenuArt.Barnacles(c, cx + r * 0.75f, cy + r * 0.7f, 3, 5, 4);
    }
    // The part of an ellipse inside a circle, as a polygon: both are convex, so their overlap is too.
    static Vector2[] EllipseInCircle(float ex, float ey, float rx, float ry, float cx, float cy, float r)
    {
        var pts = new List<Vector2>();
        var cc = new Vector2(cx, cy);
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * H.PI * 2;
            var p = new Vector2(ex + MathF.Cos(a) * rx, ey + MathF.Sin(a) * ry);
            if (p.DistanceTo(cc) <= r) pts.Add(p);
            var q = new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            float u = (q.X - ex) / rx, v = (q.Y - ey) / ry;
            if (u * u + v * v <= 1) pts.Add(q);
        }
        if (pts.Count < 3) return Array.Empty<Vector2>();
        var m = new Vector2(pts.Average(p => p.X), pts.Average(p => p.Y));
        return pts.OrderBy(p => MathF.Atan2(p.Y - m.Y, p.X - m.X)).ToArray();
    }
    void Advance()
    {
        if (dlgLines == null) return;
        // still coming: show it all first
        if (dlgLabel != null && GodotObject.IsInstanceValid(dlgLabel) && dlgLabel.VisibleRatio < 1) { dlgLabel.VisibleRatio = 1; return; }
        dlgI++;
        if (dlgI >= dlgLines.Count) { var d = dlgDone; dlgLines = null; Close(); d?.Invoke(); return; }
        RenderDlg();
    }
    public void Confirm(string text, Action yes, Action no = null)
    {
        Show("confirm");
        backAction = no;
        var c = Panel(230, look: MenuArt.Look.Slate, growth: 0.6f);
        Lbl(c, text, 7, TEXT, center: true);
        Rule(c);
        var r = Row(c, true); r.AddThemeConstantOverride("separation", 10);
        Btn(r, "Yes", () => { Close(); yes(); }, primary: true);
        Btn(r, "No", () => (no ?? Close)(), style: MenuArt.Btn.Chip);
        Footer(c, ("confirm", "choose"), ("cancel", "no"));
        FocusFirst();
    }

    // ---- end screens -----------------------------------------------------------------------------
    // After the last line of an ending: an epitaph on a page of the Keeper's log.
    public void Ending(string title, List<string> paras, string stats, List<string> fates)
    {
        void PanelView()
        {
            Show("ending");
            dim.Dim = 0.92f; dim.Tint = new Color(0.01f, 0.015f, 0.02f);
            var c = Panel(320, 340, MenuArt.Look.Parchment, 0.9f);
            Lbl(c, "From the Keeper's log", 5, INK_SOFT, caps: true, center: true);
            Lbl(c, title, 13, INK, center: true);
            InkRule(c);
            foreach (var t in paras) Lbl(c, t, 6, INK);
            if (fates != null && fates.Count > 0) { InkRule(c); Lbl(c, "Afterwards", 6, INK_SOFT, caps: true); foreach (var t in fates) Lbl(c, t, 5, INK); }
            InkRule(c);
            Lbl(c, stats, 5, INK_SOFT, italic: true, center: true);
            Btn(Row(c, true), "Return to title", () => { Close(); G.ToTitle(); }, primary: true);
            FocusFirst();
        }
        var lines = new List<string> { title }; lines.AddRange(paras); if (fates != null) lines.AddRange(fates);
        Cinematic(lines, PanelView, "title");
    }
    // The sea has her: black water, her last breath going up past the sinking lantern, the words, and above, the bell.
    public void Death(string title, string line, string detail, Action onWake)
    {
        Show("death");
        dim.Dim = 0.88f; dim.Tint = new Color(0.005f, 0.02f, 0.03f);
        var deep = new Painted(DeathDeep, Vector2.Zero);
        deep.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(deep);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        col.AddThemeConstantOverride("separation", 5);
        center.AddChild(col);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });
        var h = Lbl(col, title, 16, H.Col("#c8604c"), center: true, caps: true);
        h.AddThemeColorOverride("font_shadow_color", new Color(0.4f, 0, 0, 0.7f)); h.AddThemeConstantOverride("shadow_outline_size", 4);
        Rule(col);
        Lbl(col, line, 8, H.Col("#d8d0bc"), italic: true, center: true);
        Lbl(col, detail, 5, MUTED, center: true);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        Btn(Row(col, true), "Wake at the bell", () => { Close(); onWake(); Input.MouseMode = Input.MouseModeEnum.Hidden; }, primary: true);
        FocusFirst();
    }
    static void DeathDeep(Canvas c, Vector2 sz, float t)
    {
        // light from the surface, far above, and the bell somewhere up there
        c.FillRectGradV(0, 0, sz.X, 70, new Color(0.25f, 0.4f, 0.42f, 0.22f), new Color(0.25f, 0.4f, 0.42f, 0));
        for (int i = 0; i < 9; i++)
        {
            float x = (TileArt.Hash(i, 2) * sz.X + MathF.Sin(t * 0.7f + i) * 12) % sz.X, w = 20 + TileArt.Hash(i, 3) * 40;
            c.FillRect(x, 2 + MathF.Sin(t * 1.3f + i * 2) * 1.5f, w, 0.6f, new Color(0.7f, 0.9f, 0.9f, 0.18f));
        }
        for (int i = 0; i < 3; i++)
        {
            float ph = (t * 0.25f + i / 3f) % 1;
            c.Stroke = new Color(MenuArt.AMBER.R, MenuArt.AMBER.G, MenuArt.AMBER.B, 0.12f * (1 - ph)); c.LineWidth = 0.8f;
            c.BeginPath(); c.Ellipse(sz.X / 2, -6, 30 + ph * 160, 8 + ph * 40, 0, 0, H.PI * 2); c.StrokePath();
        }
        // Maren, far below, sinking with her arms up, the lantern going down beside her
        float bx = sz.X * 0.24f + MathF.Sin(t * 0.3f) * 6, by = sz.Y - 74 + MathF.Sin(t * 0.21f) * 3, rot = MathF.Sin(t * 0.25f) * 0.15f;
        c.Glow(bx, by, 34, 30, new Color(0.25f, 0.45f, 0.45f, 0.12f));
        c.Glow(bx + 14, by + 8, 30, new Color(1, 0.6f, 0.25f, 0.1f + 0.05f * MathF.Sin(t * 3)));
        c.Fill = new Color(1, 0.75f, 0.35f, 0.5f); c.FillRect(bx + 13, by + 7, 2.2f, 3);
        c.Save(); c.Translate(bx, by); c.Rotate(rot);
        var body = new Color(0.03f, 0.05f, 0.06f, 0.95f);
        c.Fill = body; c.FillCircle(0, -9, 4.2f); c.FillRect(-3.2f, -5, 6.4f, 10);
        c.Stroke = body; c.LineWidth = 1.6f;
        c.Line(-3, -4, -7, -14 + MathF.Sin(t * 0.8f)); c.Line(3, -4, 7.5f, -13 + MathF.Sin(t * 0.7f + 1));
        c.Line(-2, 5, -4, 13); c.Line(2, 5, 5, 12.5f);
        // the light from above catches her helmet and shoulders
        c.Stroke = new Color(0.45f, 0.65f, 0.65f, 0.6f); c.LineWidth = 0.7f; c.BeginPath(); c.Arc(0, -9, 4.2f, -H.PI * 0.95f, -H.PI * 0.2f); c.StrokePath();
        c.Line(-3.2f, -4.6f, 3.2f, -4.6f);
        c.Fill = new Color(0.7f, 0.55f, 0.3f, 0.5f); c.FillCircle(1.6f, -9, 1.5f);
        c.Restore();
        // her last breath, going up
        for (int i = 0; i < 14; i++)
        {
            float sp = 14 + TileArt.Hash(i, 7) * 18, ph = (t * sp / 300 + TileArt.Hash(i, 8)) % 1;
            float y = by - 12 - ph * (by - 20), x = bx + MathF.Sin(t * 2 + i) * 3 + (TileArt.Hash(i, 9) - 0.5f) * 10 * ph;
            float r = 0.6f + TileArt.Hash(i, 10) * 1.6f * (1 - ph * 0.3f);
            c.Stroke = new Color(0.75f, 0.9f, 0.92f, 0.5f * (1 - ph)); c.LineWidth = 0.5f; c.BeginPath(); c.Arc(x, y, r, 0, H.PI * 2); c.StrokePath();
            c.FillRect(x - r * 0.4f, y - r * 0.5f, 0.5f, 0.5f, new Color(1, 1, 1, 0.5f * (1 - ph)));
        }
        // dark shapes at the edge of the light, turning toward her
        for (int i = 0; i < 3; i++)
        {
            float sx = i == 1 ? sz.X - 70 + MathF.Sin(t * 0.17f) * 10 : 50 + i * 30 + MathF.Sin(t * 0.13f + i) * 12, sy = sz.Y - 40 - i * 46;
            c.Glow(sx, sy, 26, 12, new Color(0, 0, 0, 0.5f));
            c.Fill = new Color(0.9f, 0.85f, 0.7f, 0.25f + 0.15f * MathF.Sin(t * 1.7f + i)); c.FillRect(sx - 3, sy - 1, 1, 1); c.FillRect(sx + 2, sy - 1, 1, 1);
        }
    }

    // Words cut into the island: a slab of salt-whitened stone, the letters cut deep.
    public void Inscription(string title, string text, Action done = null)
    {
        if (kind == "inscription" && inscDone != null) { var prev = inscDone; inscDone = null; Callable.From(prev).CallDeferred(); }
        Show("inscription");
        inscDone = done;
        var c = Panel(240, 300, MenuArt.Look.Stone, 0.9f);
        c.AddThemeConstantOverride("separation", 7);
        Lbl(c, string.Join(" ", (title ?? "").ToUpperInvariant().ToCharArray()), 5, H.Col("#9aa4a4"), center: true);
        var t = Lbl(c, text, 8, H.Col("#dfe4e2"), center: true);
        // cut letters: dark above where the chisel went in, light below where the edge catches
        t.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        t.AddThemeConstantOverride("shadow_offset_x", -1); t.AddThemeConstantOverride("shadow_offset_y", -1);
        t.AddThemeColorOverride("font_outline_color", new Color(0.55f, 0.7f, 0.75f, 0.18f));
        t.AddThemeConstantOverride("outline_size", 2);
        var f = Footer(c, ("confirm", "close")); f.Align = 1;
    }

    // Black-screen text, one line at a time. Confirm or a click advances; cancel skips.
    public void Cinematic(List<string> lines, Action done, string first = null)
    {
        Show("cinematic");
        dim.Dim = 1; dim.Tint = Colors.Black; dim.Kelp = false; dim.Shafts = false;
        cine = new Cine { lines = lines.Where(l => !string.IsNullOrEmpty(l)).ToList(), done = done, first = first };
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(253, 0) };
        center.AddChild(label);
        cine.label = label;
        var skip = new RichLabel(() => "{confirm} continue     {cancel} skip", 4, H.Col("#5a5446"), 2);
        root.AddChild(skip);
        skip.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        skip.GrowHorizontal = Control.GrowDirection.Begin; skip.GrowVertical = Control.GrowDirection.Begin;
        skip.OffsetRight = -10; skip.OffsetBottom = -8;
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

// An item's icon as a small control, for satchel slots and lists.
public partial class IconView : Control
{
    readonly string id; readonly Canvas cv = new();
    public IconView(string id) { this.id = id; MouseFilter = MouseFilterEnum.Ignore; SizeFlagsVertical = SizeFlags.ShrinkCenter; }
    public override void _Draw() => Icons.Draw(cv.Begin(this), id, Size.X / 2, Size.Y / 2, MathF.Min(Size.X, Size.Y) * 0.95f);
}
