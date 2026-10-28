// Entry point: builds the scene, runs the game step every frame, and hands input to the game.
using System;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class Main : Node2D
{
    WorldView view;
    Tester tester;

    public override void _Ready()
    {
        Settings.Load();
        Pad.Init();
        Pad.Notice = OnPadNotice;
        AddChild(new Snd());
        Snd.SetVolume(Snd.Volume);
        AddChild(new Score());
        G.Init();
        view = new WorldView();
        AddChild(view);
        AddChild(new UiRoot());
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Canvas,
            GlowEnabled = Settings.Bloom,
            GlowIntensity = 0.75f,
            GlowStrength = 1.0f,
            GlowBloom = 0.02f,
            GlowHdrThreshold = 0.82f,
            GlowHdrScale = 2.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
        };
        env.Set("glow_levels/1", 1.0f); env.Set("glow_levels/2", 1.0f); env.Set("glow_levels/3", 0.8f); env.Set("glow_levels/4", 0.5f); env.Set("glow_levels/5", 0.3f);
        Settings.Env = new WorldEnvironment { Environment = env };
        AddChild(Settings.Env);

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--test") || args.Contains("--shots"))
        {
            UiRoot.Instant = true;
            Profile.Persist = false; Profile.Load();   // tests start from a player who has finished nothing
            tester = new Tester(args);
            AddChild(tester);
        }
        else
        {
            Settings.Apply();
            UI.Title();
        }
    }

    public override void _Input(InputEvent ev)
    {
        Inp.OnEvent(ev);
        if (ev is InputEventJoypadButton jb && jb.Pressed && UI.IsOpen()) UI.Inst.PadButton(jb.ButtonIndex);
        if (ev is InputEventKey k && k.Pressed && !k.Echo && (k.PhysicalKeycode == Key.F11 || (k.PhysicalKeycode == Key.Enter && k.AltPressed)))
        {
            Settings.ToggleFullscreen();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        // Alt-tabbing away pauses the game.
        if (what == NotificationApplicationFocusOut && G.mode == "play" && !UI.IsOpen() && G.player != null && !G.player.dead && tester == null) UI.Pause();
    }

    public override void _Process(double delta)
    {
        float dt = MathF.Min(0.033f, (float)delta);
        if (tester != null && tester.FixedStep) dt = 1 / 60f;
        if (G.hitStop > 0) { G.hitStop -= dt; if (tester == null && G.mode == "play" && !UI.IsOpen()) dt *= 0.06f; }
        Inp.Poll(GetViewport(), dt);
        Pad.UpdateGyro(dt);
        tester?.BeforeStep();
        G.stepDt = dt;
        try { G.Step(dt); }
        catch (Exception e) { GD.PushError(e.ToString()); if (tester != null) tester.Errors.Add(e.ToString()); }
        Inp.EndFrame();
        Pad.UpdateLight((float)(Time.GetTicksMsec() / 1000.0));
        // the cursor shows only in menus, and only while the mouse is the thing in use
        var want = !UI.IsOpen() ? (G.mode == "play" ? Input.MouseModeEnum.Hidden : Input.MouseMode) : Pad.Using ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
        if (Input.MouseMode != want) Input.MouseMode = want;
    }

    // A controller connected or dropped. Losing the pad mid-game pauses, as on a console.
    void OnPadNotice(string msg)
    {
        if (tester != null && !Pad.TestMode) return;
        if (msg == "disconnected")
        {
            if (G.mode == "play" && !UI.IsOpen() && G.player != null && !G.player.dead) UI.Pause("Controller disconnected. Reconnect it, or carry on with keyboard and mouse.");
            return;
        }
        if (G.mode == "play") H.ToastMsg(msg, 2.5f);
    }
}
