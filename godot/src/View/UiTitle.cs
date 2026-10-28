// The title, the crossing (a new game's choices), the settings, the controls and the pause menu.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class UiRoot
{
    // ---- title -------------------------------------------------------------------------------
    // The island at night from the water; the name of the place grown over by the sea; a driftwood signpost on the
    // rocks below it, its boards the choices, a lantern hung by the one in hand.
    public void Title()
    {
        Show("title");
        G.mode = "title";
        dim.Dim = 0; dim.Kelp = false; dim.Shafts = false;
        var vista = new TitleVista { ShowName = false };
        vista.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(vista);
        var logo = new Painted((c, sz, t) => TitleLogo(c, 44, 84, t), Vector2.Zero);
        logo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(logo);
        var sign = new TitleSign();
        sign.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(sign);
        var col = new VBoxContainer { Position = new Vector2(72, 146) };
        col.AddThemeConstantOverride("separation", 4);
        root.AddChild(col);
        sign.Boards = col;
        var save = G.SaveSummary();
        if (G.HasSave()) TitleBtn(col, "Continue from the last Bell Room", () => { Close(); G.LoadGame(); Input.MouseMode = Input.MouseModeEnum.Hidden; });
        TitleBtn(col, "Row out to the lighthouse", Voyage);
        TitleBtn(col, "Settings", () => SettingsMenu(Title));
        TitleBtn(col, "Controls", () => Controls(Title));
        TitleBtn(col, "Controller settings", () => ControllerSettings(Title));
        TitleBtn(col, "Quit to desktop", () => GetTree().Quit());
        var foot = new VBoxContainer { Position = new Vector2(22, 326), CustomMinimumSize = new Vector2(300, 0) };
        foot.AddThemeConstantOverride("separation", 0);
        root.AddChild(foot);
        if (save.HasValue)
        {
            var (bell, act, time, diff) = save.Value;
            Lbl(foot, $"Last rang the bell at {bell} · Act {new string('I', act)} · {Clock(time)} on the island · {Diff.Get(diff).name}", 5, H.Col("#b0a080"), italic: true);
        }
        if (Profile.SawTrueEnding) Lbl(foot, "The seventh verse has been sung. Somewhere far out at sea, something heard it.", 5, H.Col("#8fd8c8"), italic: true);
        else if (Profile.SaintUnlocked) Lbl(foot, "Far below the island, the Saint is awake. A new crossing waits.", 5, H.Col("#8fd8c8"), italic: true);
        Lbl(foot, "Keyboard and mouse, or an Xbox, PlayStation or Switch controller. Best with headphones, in the dark.", 4, MUTED);
        Lbl(foot, PadLine(), 4, MUTED);
        FocusFirst();
    }
    static string Clock(float s) { int m = (int)(s / 60); return m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m}m"; }
    // The signpost's boards: driftwood, each as long as its words.
    void TitleBtn(Control parent, string text, Action onPress)
    {
        var b = Btn(parent, text, onPress, style: MenuArt.Btn.Title);
        b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        b.AutowrapMode = TextServer.AutowrapMode.Off;
        b.CustomMinimumSize = new Vector2(0, 16);
    }
    // The name of the place, as if it had been under the water: tarnished brass letters, a tide line across them,
    // salt on their tops, barnacles at their feet, kelp hanging from them and water running off.
    static void TitleLogo(Canvas c, float x, float y, float t)
    {
        const string NAME = "HOLLOWMERE";
        c.Font = "36px caps";
        float track = 2.2f, total = 0;
        var xs = new float[NAME.Length]; var ws = new float[NAME.Length];
        for (int i = 0; i < NAME.Length; i++) { xs[i] = x + total; ws[i] = c.MeasureText(NAME[i].ToString()); total += ws[i] + track; }
        total -= track;
        float cap = 24, top = y - cap;
        c.Glow(x + total / 2, y - cap / 2, total * 0.7f, 44, new Color(0.9f, 0.65f, 0.3f, 0.08f));
        for (int i = 0; i < NAME.Length; i++)
        {
            string ch = NAME[i].ToString();
            c.Fill = new Color(0, 0, 0, 0.75f); c.FillText(ch, xs[i] + 1.5f, y + 2);
            c.Fill = H.Col("#2a1e10");
            c.FillText(ch, xs[i] - 0.7f, y); c.FillText(ch, xs[i] + 0.7f, y); c.FillText(ch, xs[i], y - 0.7f); c.FillText(ch, xs[i], y + 0.9f);
            c.Fill = H.Col("#f0d590"); c.FillText(ch, xs[i], y - 0.6f);
            c.Fill = H.Col("#c49a52"); c.FillText(ch, xs[i], y);
        }
        // where the water stood on it: below the line the brass is dark and green
        float tide = y - cap * 0.38f;
        c.FillRectGradV(x - 3, tide, total + 6, y + 2 - tide, new Color(0.05f, 0.14f, 0.12f, 0.45f), new Color(0.02f, 0.06f, 0.05f, 0.65f));
        c.FillRect(x - 6, tide - 0.4f, total + 12, 0.7f, new Color(0.55f, 0.8f, 0.72f, 0.3f));
        for (int i = 0; i < 7; i++) { c.Glow(x + TileArt.Hash(i, 71) * total, tide + 3 + TileArt.Hash(i, 72) * 6, 6, 3, new Color(MenuArt.VERDIGRIS.R, MenuArt.VERDIGRIS.G, MenuArt.VERDIGRIS.B, 0.3f)); }
        // salt on the tops of the letters
        for (int i = 0; i < NAME.Length; i++) MenuArt.Salt(c, xs[i] + 1, top + 1.2f, ws[i] - 2, 300 + i, 2.2f);
        // barnacles at the feet of the first and last letters and on the M
        MenuArt.Barnacles(c, xs[0] + 3, y - 2, 6, 31, 9);
        MenuArt.Barnacles(c, xs[6] + ws[6] - 3, top + 3, 3, 32, 4);
        MenuArt.Barnacles(c, xs[9] + ws[9] - 2, y - 1, 5, 33, 7);
        // wrack up the H, kelp hanging from the letters, water running off them
        MenuArt.Wrack(c, new Vector2(xs[0] + 1, y + 4), -H.PI / 2 - 0.1f, 26, t, 41);
        foreach (var (i, len, seed) in new[] { (1, 26f, 51), (3, 15f, 52), (5, 34f, 53), (7, 19f, 54), (8, 12f, 55) })
            MenuArt.Kelp(c, new Vector2(xs[i] + ws[i] * 0.55f, y - 1), H.PI / 2 + (TileArt.Hash(i, 56) - 0.5f) * 0.3f, len, t, seed, 1.8f);
        MenuArt.Drips(c, x + 4, x + total - 4, y + 1, t, 61, 6, 44);
        // the line under it
        c.Font = "italic 8px serif"; c.TextAlign = 1;
        Hud.Flourish(c, x + total / 2, y + 12, total / 2 + 4, 0.9f);
        c.Fill = new Color(0, 0, 0, 0.7f); c.FillText("A tide-bound descent through a drowned island", x + total / 2 + 0.8f, y + 25.8f);
        c.Fill = H.Col("#bfb294"); c.FillText("A tide-bound descent through a drowned island", x + total / 2, y + 25);
        c.TextAlign = 0;
    }

    // The signpost: a driftwood post driven into the rocks, kelp wound up it, a little bell on its arm, and a lantern on
    // a chain that moves to the board in hand.
    partial class TitleSign : Control
    {
        readonly Canvas cv = new();
        public Control Boards;
        float ly = -1, lv;
        public TitleSign() { MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Process(double d)
        {
            if (Boards != null)
            {
                Button on = null;
                foreach (var ch in Boards.GetChildren()) if (ch is Button b && (b.HasFocus() || b.IsHovered())) { on = b; if (b.HasFocus()) break; }
                if (on != null)
                {
                    float want = on.GlobalPosition.Y - GlobalPosition.Y + on.Size.Y / 2;
                    if (ly < 0) ly = want;
                    // a spring, so the lantern swings a little when it moves
                    lv += ((want - ly) * 60 - lv * 11) * (float)d; ly += lv * (float)d;
                }
            }
            QueueRedraw();
        }
        public override void _Draw()
        {
            if (Boards == null) return;
            var c = cv.Begin(this); float t = MenuArt.Now;
            float px = Boards.GlobalPosition.X - GlobalPosition.X - 9, top = Boards.GlobalPosition.Y - GlobalPosition.Y - 22, bot = 292;
            // the post
            c.Fill = new Color(0, 0, 0, 0.5f); c.FillRect(px + 2, top + 2, 8, bot - top);
            c.FillRectGradH(px - 3.5f, top, 7.5f, bot - top, H.Col("#2a2219"), H.Col("#17120d"));
            c.FillRectGradH(px - 1.5f, top, 2.2f, bot - top, new Color(1, 0.9f, 0.7f, 0.07f), new Color(1, 0.9f, 0.7f, 0));
            c.Stroke = new Color(0, 0, 0, 0.45f); c.LineWidth = 0.4f;
            for (int i = 0; i < 5; i++) { float gx = px - 2.5f + i * 1.4f; c.Line(gx, top + 4, gx + MathF.Sin(i) * 0.6f, bot); }
            c.Fill = new Color(0, 0, 0, 0.5f); c.BeginPath(); c.Ellipse(px, top + (bot - top) * 0.62f, 1.6f, 2.6f, 0, 0, H.PI * 2); c.FillPath();
            // its split top and an arm with the bell
            c.Fill = H.Col("#2a2219"); c.Poly(new[] { new Vector2(px - 3.5f, top), new Vector2(px - 1, top - 5), new Vector2(px + 1.5f, top - 2), new Vector2(px + 4, top - 4), new Vector2(px + 4, top) });
            c.FillRectGradV(px - 3.5f, top + 4, 30, 3.5f, H.Col("#2e251b"), H.Col("#17120d"));
            MenuArt.Nail(c, px, top + 5.7f);
            MenuArt.Bell(c, px + 22, top + 7.5f, 5.5f, t, 0.18f, 3);
            // iron bands, and kelp wound round
            foreach (float by in new[] { top + 14, bot - 34 }) { c.FillRect(px - 4, by, 8.5f, 2, H.Col("#1e2224")); MenuArt.Rivet(c, px + 2, by + 1, 0.7f); c.Glow(px, by + 2, 4, 6, new Color(MenuArt.RUST.R, MenuArt.RUST.G, MenuArt.RUST.B, 0.35f)); }
            for (int i = 0; i < 3; i++)
                MenuArt.Kelp(c, new Vector2(px - 3 + i * 3, bot), -H.PI / 2 + (i - 1) * 0.08f, (bot - top) * (0.45f + i * 0.12f), t * 0.5f, 80 + i * 9, 1.8f, 0.9f);
            MenuArt.Wrack(c, new Vector2(px + 3, bot - 2), -H.PI / 2 + 0.25f, 30, t, 91);
            MenuArt.Barnacles(c, px, bot - 6, 7, 92, 12);
            MenuArt.Barnacles(c, px + 3, bot - 40, 3, 93, 4);
            // a nail at each board, and a rope lashing
            foreach (var ch in Boards.GetChildren())
                if (ch is Button b)
                {
                    float y = b.GlobalPosition.Y - GlobalPosition.Y + b.Size.Y / 2;
                    c.Stroke = new Color(0.55f, 0.48f, 0.34f, 0.7f); c.LineWidth = 0.7f;
                    c.Line(px - 3.5f, y - 3, px + 4, y - 1.5f); c.Line(px - 3.5f, y + 1, px + 4, y + 2.5f);
                }
            if (ly < 0) return;
            // the lantern on its chain, hung from a hook on the post
            float sway = MathF.Sin(t * 1.3f) * 0.06f - lv * 0.004f;
            float hx = px - 8, hy = ly - 15;
            c.Stroke = H.Col("#2a2826"); c.LineWidth = 1; c.Line(px - 3.5f, hy, hx - 1, hy); c.Line(hx - 1, hy, hx - 1, hy + 1.5f);
            c.Save(); c.Translate(hx - 1, hy + 1.5f); c.Rotate(sway);
            MenuArt.Hook(c, 0, 0, 6);
            c.Glow(0, 14, 46, 30, new Color(1, 0.7f, 0.32f, 0.14f));
            MenuArt.Lantern(c, 0, 14.5f, 9, t, 1, 4);
            c.Restore();
        }
    }

    // ---- the crossing ---------------------------------------------------------------------------------
    // Choices for a new game, asked once the player has decided to set out: how hard the night, how long the tides.
    public void Voyage()
    {
        Show("voyage");
        backAction = Title;
        dim.Dim = 0.25f; dim.Kelp = true;
        var vista = new TitleVista { ShowName = false };
        vista.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(vista);
        root.MoveChild(vista, 0);
        var c = Panel(340);
        H2(c, "The crossing");
        Lbl(c, "The tide redraws the map every ten minutes, and brings the dead back with it.", 5, MUTED, italic: true);
        Rule(c);
        H3(c, "How hard the night");
        if (Diff.Get(difficulty).earned && !Profile.SaintUnlocked) difficulty = "keeper";
        var r = Row(c, true); r.AddThemeConstantOverride("separation", 7);
        foreach (var cr in Diff.ALL)
        {
            string emblem = cr.id;
            bool locked = cr.earned && !Profile.SaintUnlocked;
            var b = Btn(r, locked ? "Asleep" : cr.name, () =>
            {
                if (locked) { H.ToastMsg("Finish the night once, any way you can, and the Saint wakes.", 3); Snd.Deny(); return; }
                difficulty = emblem; Voyage();
            }, style: MenuArt.Btn.Flat, selected: difficulty == cr.id);
            Card(b, 70, 62, difficulty == cr.id, (cv, w, h, st) =>
            {
                float cx = w / 2, cy = 22;
                bool hot = difficulty == emblem || st > 0;
                if (emblem == "pilgrim") MenuArt.Candle(cv, cx, cy + 3, 13, MenuArt.Now, hot ? 1 : 0.5f);
                else if (emblem == "keeper") MenuArt.Lantern(cv, cx, cy + 2, 10, MenuArt.Now, hot ? 1 : 0.45f, 2);
                else if (emblem == "drowned") MenuArt.Skull(cv, cx, cy, 12, MenuArt.Now, 6);
                else MenuArt.SaintEye(cv, cx, cy, 17, MenuArt.Now, !locked);
            });
            if (difficulty == cr.id) FocusOn(b);
        }
        var cur = Diff.Get(difficulty);
        Lbl(c, cur.line, 6, TEXT, italic: true, center: true);
        // the night in numbers, so the crossings can be told apart at a glance
        float minutes = 10 * tide * cur.tide;
        string facts = $"Maren {cur.pips} pips  ·  a Shucker falls to {Diff.ShuckerRivets(cur)} rivets  ·  a tide every {minutes:0.#} min  ·  {cur.rivetsPerScrap} rivets a scrap";
        if (cur.regen > 0) facts += "  ·  heals while safe";
        if (cur.keepSatchel) facts += "  ·  satchel kept on death";
        if (cur.saveCost) facts += "  ·  saving costs tallow";
        Lbl(c, facts, 5, cur.earned ? H.Col("#8fd8c8") : MUTED, caps: true, center: true);
        if (cur.earned && !Profile.SawTrueEnding) Lbl(c, "Something under the island has been waiting a long time for a Hult to hear it.", 5, H.Col("#8fd8c8"), italic: true, center: true);
        Rule(c);
        H3(c, "How long the tides");
        var r2 = Row(c, true);
        foreach (var v in new[] { 1f, 1.5f, 2f }) Btn(r2, (v * 10) + " minutes", () => { tide = v; Voyage(); }, selected: tide == v);
        Small(c, "More time between tides, without softening the dead.").HorizontalAlignment = HorizontalAlignment.Center;
        Rule(c);
        var go = Row(c, true); go.AddThemeConstantOverride("separation", 10);
        Btn(go, "Cast off", () => { Close(); G.NewGame(difficulty, tide); }, primary: true);
        Btn(go, "Back", Title, style: MenuArt.Btn.Chip);
        Footer(c, ("confirm", "choose"), ("cancel", "back"));
    }
    // A tall card for a choice: a slate tablet with an emblem drawn on it and its name across the foot.
    static void Card(Button b, float w, float h, bool on, Action<Canvas, float, float, int> emblem)
    {
        b.CustomMinimumSize = new Vector2(w, h);
        b.Alignment = HorizontalAlignment.Center;
        b.AddThemeFontOverride("font", Canvas.GetFont(false, false, false, true));
        b.AddThemeFontSizeOverride("font_size", 8);
        foreach (var s0 in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(s0, Room(4, h - 16, 4, 3));
        var art = b.GetChildren().OfType<ButtonArt>().First();
        art.Extra = (c, bt, st) =>
        {
            float lit = st > 0 ? 1 : 0;
            MenuArt.Slate(c, 0, 0, w, h, (int)w + 5);
            c.Stroke = new Color(MenuArt.BRASS.R, MenuArt.BRASS.G, MenuArt.BRASS.B, on ? 0.9f : 0.3f + 0.4f * lit); c.LineWidth = on ? 1.2f : 0.7f; c.StrokeRect(0.5f, 0.5f, w - 1, h - 1);
            if (on) { c.Glow(w / 2, h / 2, w * 0.7f, h * 0.7f, new Color(1, 0.75f, 0.4f, 0.12f)); MenuArt.Brass(c, 4, h - 15, w - 8, 12, 5, 0.25f, false); }
            else c.FillRect(4, h - 15, w - 8, 12, new Color(0, 0, 0, 0.35f));
            if (lit > 0 && !on) c.Glow(w / 2, h / 2, w * 0.6f, h * 0.6f, new Color(1, 0.75f, 0.4f, 0.08f));
            emblem(c, w, h, st);
            MenuArt.Barnacles(c, w - 4, 4, 3, (int)w, 4);
        };
        if (on) foreach (var k in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color" }) b.AddThemeColorOverride(k, INK);
    }

    // ---- settings -----------------------------------------------------------------------------------------
    public void SettingsMenu(Action back)
    {
        Show("settings");
        backAction = () => (back ?? Close)();
        if (G.mode == "title") { dim.Dim = 0.3f; var vista = new TitleVista { ShowName = false }; vista.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(vista); root.MoveChild(vista, 0); }
        var c = Panel(262);
        H2(c, "Settings");
        void Again() { Settings.Save(); SettingsMenu(back); }
        Lbl(c, "Sound", 6, WARM, caps: true);
        var r = Row(c); Lbl(r, "Volume", 6).CustomMinimumSize = new Vector2(52, 0); r.GetChild<Label>(0).SizeFlagsHorizontal = Control.SizeFlags.Fill;
        foreach (var v in new[] { 0f, 0.35f, 0.7f, 1f })
            Btn(r, v == 0 ? "Off" : Mathf.RoundToInt(v * 100) + "%", () => { Snd.SetVolume(v); Again(); }, selected: MathF.Abs(Snd.Volume - v) < 0.01f);
        var rm = Row(c); Lbl(rm, "Music", 6).CustomMinimumSize = new Vector2(52, 0); rm.GetChild<Label>(0).SizeFlagsHorizontal = Control.SizeFlags.Fill;
        foreach (var v in new[] { 0f, 0.4f, 0.8f, 1f })
            Btn(rm, v == 0 ? "Off" : Mathf.RoundToInt(v * 100) + "%", () => { Score.SetMusicVolume(v); Again(); }, selected: MathF.Abs(Score.MusicVolume - v) < 0.01f);
        Rule(c);
        Lbl(c, "Picture", 6, WARM, caps: true);
        var r2 = Row(c);
        Btn(r2, Settings.Fullscreen ? "Fullscreen: on" : "Fullscreen: off", () => { Settings.ToggleFullscreen(); Again(); }, selected: Settings.Fullscreen);
        Btn(r2, Settings.Bloom ? "Glow: on" : "Glow: off", () => { Settings.Bloom = !Settings.Bloom; Settings.Apply(); Again(); }, selected: Settings.Bloom);
        Btn(r2, Settings.Shake ? "Shake: on" : "Shake: off", () => { Settings.Shake = !Settings.Shake; Again(); }, selected: Settings.Shake);
        Rule(c);
        Lbl(c, "Help on the way", 6, WARM, caps: true);
        var r3 = Row(c);
        Btn(r3, Settings.Hints ? "Hints: on" : "Hints: off", () => { Settings.Hints = !Settings.Hints; Again(); }, selected: Settings.Hints);
        Btn(r3, Settings.Guide ? "Guide: on" : "Guide: off", () => { Settings.Guide = !Settings.Guide; Again(); }, selected: Settings.Guide);
        Small(c, "The guide marks the next step of the story on the screen, the chart and in the journal.");
        Rule(c);
        Lbl(c, "Gore", 6, WARM, caps: true);
        var r4 = Row(c);
        foreach (var (v, label) in new[] { ("full", "Full"), ("reduced", "Reduced"), ("off", "Off (salt only)") })
            Btn(r4, label, () => { Settings.Gore = v; Again(); }, selected: Settings.Gore == v);
        Small(c, "Hollowmere is for adults: bodies come apart. With gore off they crumble to salt instead.");
        Rule(c);
        Btn(Row(c, true), "Back", () => (back ?? Close)(), style: MenuArt.Btn.Chip);
        Footer(c, ("confirm", "choose"), ("cancel", "back"));
        FocusFirst();
    }

    // ---- controls -----------------------------------------------------------------------------------------
    Action controlsBack; string controlsTab = "keys";
    static string PadLine()
    {
        if (Pad.Device < 0) return "No controller connected.";
        string kind = Pad.Kind switch { PadKind.PlayStation => "PlayStation", PadKind.Switch => "Switch", _ => "Xbox" };
        return $"Controller: {Pad.Name} ({kind} layout).";
    }
    public void Controls(Action back, string tab = null)
    {
        controlsBack = back;
        controlsTab = tab ?? (Pad.Using ? "pad" : "keys");
        Show("controls");
        backAction = () => (back ?? Close)();
        if (G.mode == "title") { dim.Dim = 0.35f; var vista = new TitleVista { ShowName = false }; vista.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(vista); root.MoveChild(vista, 0); }
        var c = Panel(300);
        H2(c, "Controls");
        var tr = Row(c);
        Btn(tr, "Keyboard and mouse", () => Controls(back, "keys"), selected: controlsTab == "keys", style: MenuArt.Btn.Tab);
        Btn(tr, "Controller", () => Controls(back, "pad"), selected: controlsTab == "pad", style: MenuArt.Btn.Tab);
        if (controlsTab == "pad") Small(c, PadLine() + " Prompts follow the controller in your hands.");
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 2);
        c.AddChild(grid);
        if (controlsTab == "keys")
        {
            var k = new (string, string)[]
            {
                ("A / D", "Move"), ("W / S", "Climb ladders, swim, shorten / lengthen swing rope. Standing still: look up or down"),
                ("Space", "Jump (down + Space drops through planks). At a ladder: climb it; held in the air: catch one"),
                ("Shift (hold)", "Sneak: footsteps 2 m instead of 6 m"), ("C", "Dash (0.2 s invulnerable). Jet Valve: air and water bursts in any direction"),
                ("Mouse", "Aim"), ("Left click / J", "Fire the selected weapon"), ("Right click / K (hold)", "Focused beam: makes knots glint, double oil"),
                ("F", "Gaff strike (melee, pry plates, kick barrels)"), ("Q (hold)", "Gaff Swing from an iron ring / hook-pull a light enemy"),
                ("E", "Interact, talk, drag a downed body"), ("X", "Burn a downed body with lantern oil"), ("R", "Reload (locks you in place)"),
                ("T", "Shutter the lantern (nearly invisible, blind to weak points)"), ("Z", "Poultice (2 pips over 3 s)"), ("G", "Stitch Kit (full heal, kneel 4 s)"),
                ("V", "Quick-craft 2 rivets from 1 scrap (2.5 s, crouched)"), ("B", "Ballast Boots on / off"), ("1-4 / wheel", "Select weapon"),
                ("I", "Satchel, crafting, journal"), ("Tab / M", "Map"), ("Esc / P", "Pause"), ("F11", "Fullscreen"),
            };
            if (Hollow.owned) k = k.Concat(new[] { ("5", "Take the Mere's Hollow in hand"), ("H", "Sing the verse (brimming, hold the Calling to turn the tide)"), ("Y", "Change verse") }).ToArray();
            foreach (var (a, b) in k)
            {
                var key = Lbl(grid, a, 5, WARM); key.SizeFlagsHorizontal = Control.SizeFlags.Fill; key.CustomMinimumSize = new Vector2(66, 0);
                Lbl(grid, b, 5);
            }
        }
        else
        {
            var rows = new (string, string)[]
            {
                ("{move}", "Move, climb ladders, swim. A light tilt creeps quietly. Standing still: look up or down"), ("{sneak}", "Sneak on / off"),
                ("{drop}", "Drop through planks"), ("{aim}", "Aim. With the stick left alone, Maren aims where she walks"),
                ("{jump}", "Jump. At a ladder with no direction held: climb it. Held in the air: catch one"), ("{dash}", "Dash (0.2 s invulnerable)"), ("{gaff}", "Gaff strike: melee, pry plates, kick barrels"),
                ("{interact}", "Interact, talk, drag a downed body"), ("{burn}", "Burn a downed body with lantern oil"),
                ("{swing}", "Gaff Swing from an iron ring / hook-pull a light enemy"),
                ("{fire}", "Fire the selected weapon"), ("{focus}", "Focused beam: makes knots glint, double oil"),
                ("{reload}", "Reload"), ("{quickcraft}", "Quick-craft 2 rivets from 1 scrap"),
                ("{poultice}", "Poultice (on a ladder the d-pad climbs instead)"), ("{stitch}", "Stitch Kit"), ("{boots}", "Ballast Boots on / off"),
                ("{weapon}", "Change weapon"), ("{shutter}", "Shutter the lantern"),
                ("{journal}", "Map, satchel and journal; the bumpers turn the page"), ("{pause}", "Pause"),
            };
            if (Hollow.owned) rows = rows.Concat(new[] { ("{hollow}", "Take the Mere's Hollow in hand (it sits after your weapons)"), ("{sing}", "With the Hollow in hand: sing the verse. Brimming, hold the Calling to turn the tide"), ("{verse}", "With the Hollow in hand: change verse") }).ToArray();
            foreach (var (a, b) in rows)
            {
                var g = new RichLabel(() => a, 6, WARM) { MinWidth = 54 };
                grid.AddChild(g);
                Lbl(grid, b, 5);
            }
        }
        Rule(c);
        H3(c, "How Hollowmere works");
        Small(c, "Every Brine body is held up by salt knots at its joints. Rivets on flesh barely scratch; hit the knots. The Crown knot in the chest is sealed until two limb knots break. A dropped body lying in water when the next Rising tide reaches it will stand again at High tide: haul it above the tide line or burn it.", false);
        Small(c, "The tide dial (top right) runs a 10-minute cycle: Low, Rising, High, Ebbing. Painted tide marks on pillars show where the water will reach. Bell Rooms are safe; resting there advances the tide to the next Low.", false);
        Small(c, "Climbing: a jump that only just reaches a ledge catches its lip if you push toward it, and Maren pulls herself up (hold down to drop past instead). Walls higher than a jump still need a ladder, a rope, timber to wall-jump on or the tide.", false);
        var br = Row(c, true); br.AddThemeConstantOverride("separation", 8);
        Btn(br, "Back", () => (back ?? Close)(), style: MenuArt.Btn.Chip);
        Btn(br, "Controller settings", () => ControllerSettings(() => Controls(back, controlsTab)), style: MenuArt.Btn.Chip);
        Footer(c, ("tabs", "switch list"), ("cancel", "back"));
        FocusFirst();
    }

    public void ControllerSettings(Action back)
    {
        Show("controller");
        backAction = () => (back ?? Close)();
        if (G.mode == "title") { dim.Dim = 0.35f; var vista = new TitleVista { ShowName = false }; vista.SetAnchorsPreset(Control.LayoutPreset.FullRect); root.AddChild(vista); root.MoveChild(vista, 0); }
        var c = Panel(250);
        H2(c, "Controller");
        Small(c, PadLine());
        void Again() { Settings.Save(); ControllerSettings(back); }
        Rule(c);
        H3(c, "Vibration");
        var r = Row(c);
        foreach (var (v, n) in new[] { (0f, "Off"), (0.5f, "Gentle"), (1f, "Full") })
            Btn(r, n, () => { Settings.Vibration = v; Again(); Pad.Rumble(0.5f * v, 0.5f * v, 0.25f); }, selected: MathF.Abs(Settings.Vibration - v) < 0.01f);
        H3(c, "Aim");
        var r2 = Row(c);
        Btn(r2, Settings.AimAssist ? "Aim assist: on" : "Aim assist: off", () => { Settings.AimAssist = !Settings.AimAssist; Again(); }, selected: Settings.AimAssist);
        bool gyro = Pad.HasGyro;
        Btn(r2, Settings.Gyro ? "Gyro aim: on" : "Gyro aim: off", () => { Settings.Gyro = !Settings.Gyro; Again(); if (Settings.Gyro) H.ToastMsg("Hold the controller still for a moment.", 2); }, selected: Settings.Gyro, disabled: !gyro && !Settings.Gyro);
        if (gyro) Btn(r2, "Recalibrate gyro", () => { Pad.Calibrate(); H.ToastMsg("Hold the controller still for a moment.", 2); });
        Small(c, gyro ? "Gyro aim tilts the aim like a mouse; the right stick still works alongside it." : "Gyro aim needs a PlayStation or Switch controller.");
        H3(c, "Button prompts");
        var r3 = Row(c);
        foreach (var (v, n) in new[] { ("auto", "Match controller"), ("xbox", "Xbox"), ("playstation", "PlayStation"), ("switch", "Switch") })
            Btn(r3, n, () => { Settings.Prompts = v; Pad.ApplyUiMap(); Again(); }, selected: Settings.Prompts == v);
        H3(c, "Switch controllers: confirm in menus with");
        var r4 = Row(c);
        Btn(r4, "A (right button)", () => { Settings.NintendoConfirm = true; Pad.ApplyUiMap(); Again(); }, selected: Settings.NintendoConfirm);
        Btn(r4, "B (bottom button)", () => { Settings.NintendoConfirm = false; Pad.ApplyUiMap(); Again(); }, selected: !Settings.NintendoConfirm);
        Rule(c);
        Btn(Row(c, true), "Back", () => (back ?? Close)(), style: MenuArt.Btn.Chip);
        Footer(c, ("confirm", "choose"), ("cancel", "back"));
        FocusFirst();
    }

    // ---- pause ---------------------------------------------------------------------------------------------
    // A slate down the left of the view, the game held still behind it; on the right, where she is, what the tide is
    // doing and what the night asks of her next.
    public void Pause(string note = null)
    {
        Show("pause");
        var c = Sheet(26, 22, 168, 316, MenuArt.Look.Slate);
        H2(c, "The tide waits");
        if (note != null) Lbl(c, note, 6, BAD);
        Small(c, $"{G.room.Name} · {Data.Regions.P(G.room.Region).S("name")}");
        Rule(c);
        var col = Col(c);
        Btn(col, "Resume", Close, primary: true);
        Btn(col, "Journal", () => Satchel("log"));
        Btn(col, "Map", () => Satchel("map"));
        Btn(col, "Satchel", () => Satchel("satchel"));
        Btn(col, "Settings", () => SettingsMenu(() => Pause()));
        Btn(col, "Controls", () => Controls(() => Pause()));
        Btn(col, "Controller settings", () => ControllerSettings(() => Pause()));
        Rule(c);
        Btn(c, "Quit to title", () => Confirm("Quit to the title? Everything since you last rang a bell is lost.", () => G.ToTitle(), () => Pause()), style: MenuArt.Btn.Danger);
        Btn(c, "Quit to desktop", () => Confirm("Quit to the desktop? Everything since you last rang a bell is lost.", () => GetTree().Quit(), () => Pause()), style: MenuArt.Btn.Danger);
        Footer(c, ("confirm", "choose"), ("cancel", "resume"));
        // the night so far, on a scrap of chart
        var n = Sheet(410, 236, 204, 0, MenuArt.Look.Parchment, 0.7f);
        Lbl(n, $"Act {new string('I', G.act)} · {Diff.C.name} · {Tide.PhaseName()} tide, {H.FmtTime(Tide.PhaseRemaining())} left", 5, INK_SOFT, caps: true);
        var step = Guide.Current;
        if (step != null)
        {
            Lbl(n, step.title, 9, INK);
            Lbl(n, Guide.WhereLine(step), 5, INK_SOFT, italic: true);
            var now = step.now?.Invoke();
            Lbl(n, Glyphs.Plain(now ?? step.how, Glyphs.Current), 5, INK);
        }
        else Lbl(n, "The night is over.", 9, INK);
        FocusFirst();
    }
    // A panel placed on the screen rather than centred; returns its content column.
    VBoxContainer Sheet(float x, float y, float w, float h, MenuArt.Look look = MenuArt.Look.Slate, float growth = 1)
    {
        var pc = new PanelContainer { Position = new Vector2(x, y), CustomMinimumSize = new Vector2(w, h) };
        Ornate(pc, look, SeedOf(kind) + (int)(x + w), growth);
        root.AddChild(pc);
        var col = new VBoxContainer();
        pc.AddChild(col);
        return col;
    }
}
