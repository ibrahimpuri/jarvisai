// Keyboard, mouse and gamepad input with per-frame edge detection.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class Inp
{
    public class Mouse { public float x, y; public bool l, r, lp, rp, lr, rr; public int wheel; }
    public static readonly Mouse mouse = new();

    // Physical keys (layout independent, like KeyboardEvent.code) per action.
    public static readonly Dictionary<string, Key[]> BIND = new()
    {
        ["left"] = new[] { Key.A, Key.Left }, ["right"] = new[] { Key.D, Key.Right },
        ["up"] = new[] { Key.W, Key.Up }, ["down"] = new[] { Key.S, Key.Down },
        ["jump"] = new[] { Key.Space }, ["sneak"] = new[] { Key.Shift }, ["dash"] = new[] { Key.C },
        ["gaff"] = new[] { Key.F }, ["swing"] = new[] { Key.Q }, ["interact"] = new[] { Key.E },
        ["reload"] = new[] { Key.R }, ["shutter"] = new[] { Key.T }, ["burn"] = new[] { Key.X },
        ["poultice"] = new[] { Key.Z }, ["stitch"] = new[] { Key.G }, ["quickcraft"] = new[] { Key.V },
        ["boots"] = new[] { Key.B }, ["map"] = new[] { Key.Tab, Key.M }, ["satchel"] = new[] { Key.I },
        ["pause"] = new[] { Key.Escape, Key.P },
        ["w1"] = new[] { Key.Key1 }, ["w2"] = new[] { Key.Key2 }, ["w3"] = new[] { Key.Key3 }, ["w4"] = new[] { Key.Key4 },
        ["fire"] = new[] { Key.J }, ["focus"] = new[] { Key.K }, ["debug"] = new[] { Key.F9 },
        ["w5"] = new[] { Key.Key5 }, ["sing"] = new[] { Key.H }, ["verse"] = new[] { Key.Y },
    };

    // Gamepad layout. Face buttons are positional: A is the bottom button on every controller
    // (Xbox A, PlayStation cross, Switch B). The left stick moves; the d-pad holds the tools.
    public static readonly Dictionary<string, JoyButton> PAD = new()
    {
        ["jump"] = JoyButton.A, ["dash"] = JoyButton.B, ["gaff"] = JoyButton.X,
        ["swing"] = JoyButton.LeftShoulder, ["shutter"] = JoyButton.RightStick,
        ["boots"] = JoyButton.DpadDown, ["wprev"] = JoyButton.DpadLeft, ["wnext"] = JoyButton.DpadRight,
        ["pause"] = JoyButton.Start, ["journal"] = JoyButton.Back, ["map"] = JoyButton.Touchpad,
    };
    // One button, two actions: a tap does the first, holding it does the second.
    public const float HOLD = 0.4f;
    public static readonly (JoyButton btn, string tap, string hold)[] PAD_DUAL =
    {
        (JoyButton.Y, "interact", "burn"), (JoyButton.RightShoulder, "reload", "quickcraft"), (JoyButton.DpadUp, "poultice", "stitch"),
    };
    // Pad controls that are not buttons.
    public static readonly Dictionary<string, string> PAD_OTHER = new()
    {
        ["fire"] = "RT", ["focus"] = "LT", ["sneak"] = "L3", ["move"] = "LS", ["left"] = "LS", ["right"] = "LS",
        ["up"] = "LS", ["down"] = "LS", ["aim"] = "RS",
    };

    static HashSet<string> down = new(), prev = new();
    public static readonly HashSet<string> TestHeld = new();   // tests: actions held down this frame
    public static bool ForceL, ForceR;                        // tests: mouse buttons
    public static Vector2 stick, aimStick;           // gamepad left / right sticks, deadzone applied
    public static bool padAim => Pad.Using;          // aim from the pad rather than the mouse
    public static bool sneakLatch;                   // L3 toggles sneaking on a pad
    public static bool enabled = true;
    public static Vector2? MouseOverride;              // tests: pin the cursor in view coordinates
    static readonly List<string> injected = new();   // presses injected by tests and pad taps
    static readonly float[] holdT = new float[PAD_DUAL.Length];
    static readonly bool[] holdFired = new bool[PAD_DUAL.Length];
    static bool l3Prev, suppressNext;
    static readonly HashSet<string> suppress = new();
    static readonly HashSet<string> CONTINUOUS = new() { "left", "right", "up", "down", "sneak", "focus", "swing" };

    static Vector2 Dead(Vector2 v, float dz = 0.2f)
    {
        float len = v.Length();
        return len < dz ? Vector2.Zero : v / len * MathF.Min(1, (len - dz) / (1 - dz));
    }

    public static void Poll(Viewport vp, float dt)
    {
        prev = down; down = new HashSet<string>();
        bool focused = enabled && DisplayServer.WindowIsFocused();
        stick = aimStick = Vector2.Zero;
        if (focused)
        {
            foreach (var kv in BIND) foreach (var k in kv.Value) if (Input.IsPhysicalKeyPressed(k)) { down.Add(kv.Key); break; }
        }
        int d = Pad.Device;
        if (d >= 0 && (Pad.TestMode || (focused && Input.GetConnectedJoypads().Contains(d)))) PollPad(d, dt);
        // Whatever was held when a menu opened or closed stays dead until it is let go,
        // so the Esc or Start that closed the pause menu does not open it again.
        if (suppressNext) { suppress.UnionWith(down.Where(a => !CONTINUOUS.Contains(a))); suppressNext = false; }
        suppress.IntersectWith(down);
        down.ExceptWith(suppress);
        foreach (var a in injected) down.Add(a);
        foreach (var a in TestHeld) down.Add(a);
        injected.Clear();
        var mp = MouseOverride ?? vp.GetMousePosition();
        mouse.x = mp.X; mouse.y = mp.Y;
        bool l = ForceL || (focused && Input.IsMouseButtonPressed(MouseButton.Left)), r = ForceR || (focused && Input.IsMouseButtonPressed(MouseButton.Right));
        mouse.lp |= l && !mouse.l; mouse.rp |= r && !mouse.r;
        mouse.lr |= !l && mouse.l; mouse.rr |= !r && mouse.r;
        mouse.l = l; mouse.r = r;
    }

    static void PollPad(int d, float dt)
    {
        foreach (var kv in PAD) if (Input.IsJoyButtonPressed(d, kv.Value)) down.Add(kv.Key);
        for (int i = 0; i < PAD_DUAL.Length; i++)
        {
            var (btn, tap, hold) = PAD_DUAL[i];
            if (Input.IsJoyButtonPressed(d, btn))
            {
                holdT[i] += dt;
                if (holdT[i] >= HOLD && !holdFired[i]) { holdFired[i] = true; down.Add(hold); }
            }
            else
            {
                if (holdT[i] > 0 && !holdFired[i]) injected.Add(tap);
                holdT[i] = 0; holdFired[i] = false;
            }
        }
        stick = Dead(new Vector2(Input.GetJoyAxis(d, JoyAxis.LeftX), Input.GetJoyAxis(d, JoyAxis.LeftY)));
        if (stick.X < -0.25f) down.Add("left");
        if (stick.X > 0.25f) down.Add("right");
        if (stick.Y < -0.5f) down.Add("up");
        if (stick.Y > 0.5f) down.Add("down");
        // A light tilt creeps; L3 toggles sneaking for players who would rather not feather the stick.
        bool l3 = Input.IsJoyButtonPressed(d, JoyButton.LeftStick);
        if (l3 && !l3Prev) sneakLatch = !sneakLatch;
        l3Prev = l3;
        if (sneakLatch || (MathF.Abs(stick.X) > 0 && MathF.Abs(stick.X) < 0.55f && MathF.Abs(stick.Y) < 0.5f)) down.Add("sneak");
        aimStick = Dead(new Vector2(Input.GetJoyAxis(d, JoyAxis.RightX), Input.GetJoyAxis(d, JoyAxis.RightY)), 0.25f);
        if (Input.GetJoyAxis(d, JoyAxis.TriggerLeft) > 0.35f) down.Add("focus");
        if (Input.GetJoyAxis(d, JoyAxis.TriggerRight) > 0.35f) down.Add("fire");
    }

    public static void OnEvent(InputEvent ev)
    {
        Pad.OnEvent(ev);
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelDown) mouse.wheel += 1;
            if (mb.ButtonIndex == MouseButton.WheelUp) mouse.wheel -= 1;
        }
    }

    public static bool Held(string a) => down.Contains(a);
    public static bool Hit(string a) => down.Contains(a) && !prev.Contains(a);
    public static bool Up(string a) => !down.Contains(a) && prev.Contains(a);
    public static void Press(string a) => injected.Add(a);   // tests: hold an action for one poll
    public static bool AnyHeld() => down.Count > 0 || mouse.l || mouse.r;

    public static void EndFrame()
    {
        mouse.lp = mouse.rp = mouse.lr = mouse.rr = false;
        mouse.wheel = 0;
    }
    public static void Clear()
    {
        prev = new HashSet<string>(down);
        suppressNext = true;
        mouse.l = mouse.r = false;
        // A button still held when a menu opens or closes does nothing until it is released.
        for (int i = 0; i < PAD_DUAL.Length; i++)
        {
            holdT[i] = 0;
            holdFired[i] = Pad.Device >= 0 && Input.IsJoyButtonPressed(Pad.Device, PAD_DUAL[i].btn);
        }
    }
}
