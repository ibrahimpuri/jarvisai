// Native gamepad support: which controller is in use (Xbox, PlayStation or Switch), whether the
// player is on the pad or on keyboard and mouse, rumble, the PlayStation light bar and gyro aim.
// Buttons are positional (SDL3): A is always the bottom face button, whatever its label.
using System;
using System.Linq;
using Godot;

namespace Hollowmere;

public enum PadKind { Xbox, PlayStation, Switch }

public static class Pad
{
    public static int Device = -1;            // the pad used most recently
    public static PadKind Kind = PadKind.Xbox;
    public static string Name = "";
    public static bool Using;                 // last input came from a pad (prompts, aim and rumble follow it)
    public static Vector2 GyroDelta;          // gyro aim this frame, in view pixels
    public static (float weak, float strong, float dur) LastRumble;   // tests
    public static Action<string> Notice;      // connect / disconnect messages
    public static bool TestMode;              // tests drive a pretend pad through Input.ParseInputEvent

    static float rumbleUntil, rumbleLevel, lightAgain;
    static Color lastLight = new(-1, -1, -1);
    static int gyroDevice = -1; static float calibrateT;

    public static void Init()
    {
        Input.JoyConnectionChanged += OnConnection;
        var pads = Input.GetConnectedJoypads();
        if (pads.Count > 0) Select(pads[0]);
        ApplyUiMap();
    }

    // Which family a controller belongs to, from its SDL name and USB vendor.
    public static PadKind Detect(int dev)
    {
        string s = Input.GetJoyName(dev).ToLowerInvariant();
        int vendor = 0;
        var info = Input.GetJoyInfo(dev);
        if (info.ContainsKey("raw_name")) s += " " + info["raw_name"].AsString().ToLowerInvariant();
        if (info.ContainsKey("vendor_id")) vendor = info["vendor_id"].AsInt32();
        return Classify(s, vendor);
    }
    public static PadKind Classify(string name, int vendor)
    {
        if (vendor == 0x054C || new[] { "playstation", "dualsense", "dualshock", "ps3", "ps4", "ps5", "sony" }.Any(name.Contains)) return PadKind.PlayStation;
        if (vendor == 0x057E || new[] { "nintendo", "switch", "joy-con", "joycon", "pro controller" }.Any(name.Contains)) return PadKind.Switch;
        return PadKind.Xbox;
    }

    static void Select(int dev)
    {
        if (dev == Device) return;
        Device = dev;
        Name = Input.GetJoyName(dev);
        Kind = Detect(dev);
        lastLight = new Color(-1, -1, -1);
        if (gyroDevice >= 0 && gyroDevice != dev) { Input.SetJoyMotionSensorsEnabled(gyroDevice, false); gyroDevice = -1; }
        ApplyUiMap();
    }

    // Called for every input event: whichever device spoke last owns the prompts.
    public static void OnEvent(InputEvent ev)
    {
        switch (ev)
        {
            case InputEventJoypadButton jb when jb.Pressed:
                Select(jb.Device); SetUsing(true); break;
            case InputEventJoypadMotion jm when MathF.Abs(jm.AxisValue) > 0.5f:
                Select(jm.Device); SetUsing(true); break;
            case InputEventKey k when k.Pressed:
            case InputEventMouseButton mb when mb.Pressed:
                SetUsing(false); break;
            case InputEventMouseMotion mm when mm.Relative.Length() > 3:
                SetUsing(false); break;
        }
    }
    static void SetUsing(bool pad)
    {
        if (Using == pad) return;
        Using = pad;
        if (!pad && Device >= 0) Input.StopJoyVibration(Device);
    }

    public static void Simulate(PadKind kind)
    {
        TestMode = true; Device = 0; Kind = kind; Name = "Test " + kind + " controller"; Using = true;
        ApplyUiMap();
    }
    public static void EndSimulate()
    {
        TestMode = false; Device = -1; Using = false; Kind = PadKind.Xbox; Name = "";
        ApplyUiMap();
    }

    public static void OnConnection(long device, bool connected)
    {
        int dev = (int)device;
        if (connected)
        {
            if (Device < 0 || !Input.GetConnectedJoypads().Contains(Device)) Select(dev);
            Notice?.Invoke(Input.GetJoyName(dev) + " connected.");
            return;
        }
        if (dev != Device) return;
        bool wasUsing = Using;
        Device = -1; Using = false; gyroDevice = -1;
        var rest = Input.GetConnectedJoypads();
        if (rest.Count > 0) Select(rest[0]);
        if (wasUsing) Notice?.Invoke("disconnected");
    }

    // ---- menus: Switch controllers confirm with A (the right button), back with B ----------------------
    public static bool SwapConfirm => Glyphs.PadStyle == Glyphs.Style.Switch && Settings.NintendoConfirm;
    public static JoyButton ConfirmButton => SwapConfirm ? JoyButton.B : JoyButton.A;
    public static JoyButton CancelButton => SwapConfirm ? JoyButton.A : JoyButton.B;
    public static void ApplyUiMap()
    {
        void Set(string action, JoyButton b)
        {
            if (!InputMap.HasAction(action)) return;
            foreach (var e in InputMap.ActionGetEvents(action)) if (e is InputEventJoypadButton) InputMap.ActionEraseEvent(action, e);
            InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = b, Device = -1 });
        }
        Set("ui_accept", ConfirmButton);
        Set("ui_cancel", CancelButton);
        // the left stick moves menu focus as well as the d-pad
        foreach (var (action, axis, sign) in new[] { ("ui_left", JoyAxis.LeftX, -1f), ("ui_right", JoyAxis.LeftX, 1f), ("ui_up", JoyAxis.LeftY, -1f), ("ui_down", JoyAxis.LeftY, 1f) })
        {
            if (!InputMap.HasAction(action)) continue;
            if (!InputMap.ActionGetEvents(action).Any(e => e is InputEventJoypadMotion m && m.Axis == axis && MathF.Sign(m.AxisValue) == sign))
                InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = sign, Device = -1 });
            if (!InputMap.ActionGetEvents(action).Any(e => e is InputEventJoypadButton))
                InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = action switch { "ui_left" => JoyButton.DpadLeft, "ui_right" => JoyButton.DpadRight, "ui_up" => JoyButton.DpadUp, _ => JoyButton.DpadDown }, Device = -1 });
        }
    }

    // ---- rumble ----------------------------------------------------------------------------------
    // A stronger effect already playing is never cut short by a weaker one.
    public static void Rumble(float weak, float strong, float dur)
    {
        LastRumble = (weak, strong, dur);
        if (!Using || Device < 0 || Settings.Vibration <= 0) return;
        float now = Time.GetTicksMsec() / 1000f, level = MathF.Max(weak, strong);
        if (now < rumbleUntil && level < rumbleLevel) return;
        rumbleLevel = level; rumbleUntil = now + dur;
        float v = Settings.Vibration;
        Input.StartJoyVibration(Device, Math.Clamp(weak * v, 0, 1), Math.Clamp(strong * v, 0, 1), dur);
    }
    public static void StopRumble() { if (Device >= 0) Input.StopJoyVibration(Device); rumbleUntil = 0; }

    // ---- light bar (DualSense, DualShock 4): it glows like Maren's lantern ------------------------------
    public static void Light(Color c)
    {
        if (Device < 0 || !Input.HasJoyLight(Device)) return;
        float now = Time.GetTicksMsec() / 1000f;
        bool changed = MathF.Abs(c.R - lastLight.R) + MathF.Abs(c.G - lastLight.G) + MathF.Abs(c.B - lastLight.B) > 0.04f;
        if (!changed || now < lightAgain) return;
        lastLight = c; lightAgain = now + 0.08f;
        Input.SetJoyLight(Device, c);
    }
    public static void UpdateLight(float time)
    {
        if (Device < 0) return;
        var p = G.player;
        if (G.mode != "play" || p == null) { Light(new Color(0.35f, 0.2f, 0.06f)); return; }
        if (p.dead) { Light(Colors.Black); return; }
        Color c;
        if (!p.LanternLit()) c = new Color(0.05f, 0.04f, 0.03f);
        else if (p.focusing) c = new Color(1f, 0.86f, 0.55f);
        else { float f = 0.35f + 0.65f * Math.Clamp(p.oil / p.OilMax(), 0, 1); c = new Color(1f * f, 0.58f * f, 0.16f * f); }
        if (p.HeadUnder()) c = c.Lerp(new Color(0.08f, 0.4f, 0.5f), 0.55f);
        if (p.hp <= 1) c = c.Lerp(new Color(0.9f, 0.05f, 0.02f), 0.5f + 0.5f * MathF.Sin(time * 6));
        Light(c);
    }

    // ---- gyro aim (DualSense, DualShock 4, Switch Pro, Joy-Con) ---------------------------------------
    public static bool HasGyro => Device >= 0 && Input.HasJoyMotionSensors(Device);
    public static void UpdateGyro(float dt)
    {
        GyroDelta = Vector2.Zero;
        bool want = Settings.Gyro && Using && HasGyro;
        if (!want)
        {
            if (gyroDevice >= 0) { Input.SetJoyMotionSensorsEnabled(gyroDevice, false); gyroDevice = -1; }
            return;
        }
        if (gyroDevice != Device)
        {
            gyroDevice = Device;
            Input.SetJoyMotionSensorsEnabled(Device, true);
            Calibrate();
        }
        if (calibrateT > 0)
        {
            calibrateT -= dt;
            if (calibrateT <= 0 && Input.IsJoyMotionSensorsCalibrating(Device)) Input.StopJoyMotionSensorsCalibration(Device);
            return;
        }
        var g = Input.GetJoyGyroscope(Device);   // rad/s; positive is counter-clockwise
        float yaw = MathF.Abs(g.Y) > 0.02f ? g.Y : 0, pitch = MathF.Abs(g.X) > 0.02f ? g.X : 0;
        GyroDelta = new Vector2(-yaw, -pitch) * 260f * dt;
    }
    // Hold the controller still for a moment.
    public static void Calibrate()
    {
        if (!HasGyro) return;
        if (!Input.IsJoyMotionSensorsEnabled(Device)) Input.SetJoyMotionSensorsEnabled(Device, true);
        Input.StartJoyMotionSensorsCalibration(Device);
        calibrateT = 1.2f;
    }
}
