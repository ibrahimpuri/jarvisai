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

    // Physical keys (layout independent, like KeyboardEvent.code) and gamepad buttons per action.
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
    };
    static readonly Dictionary<string, JoyButton[]> PAD = new()
    {
        ["jump"] = new[] { JoyButton.A }, ["dash"] = new[] { JoyButton.B }, ["gaff"] = new[] { JoyButton.X },
        ["interact"] = new[] { JoyButton.Y }, ["swing"] = new[] { JoyButton.LeftShoulder }, ["reload"] = new[] { JoyButton.RightShoulder },
        ["pause"] = new[] { JoyButton.Start }, ["map"] = new[] { JoyButton.Back }, ["sneak"] = new[] { JoyButton.LeftStick },
        ["left"] = new[] { JoyButton.DpadLeft }, ["right"] = new[] { JoyButton.DpadRight },
        ["up"] = new[] { JoyButton.DpadUp }, ["down"] = new[] { JoyButton.DpadDown },
        ["poultice"] = new[] { JoyButton.RightStick },
    };

    static HashSet<string> down = new(), prev = new();
    static readonly HashSet<string> keyDown = new(), keyPrev = new();
    public static readonly HashSet<string> TestHeld = new();   // tests: actions held down this frame
    public static bool ForceL, ForceR;                        // tests: mouse buttons
    public static Vector2 stick, aimStick;           // gamepad left / right sticks
    public static bool padAim;                       // right stick aimed recently
    public static bool enabled = true;
    public static Vector2? MouseOverride;              // tests: pin the cursor in view coordinates
    static readonly List<string> injected = new();   // presses injected by tests

    public static void Poll(Viewport vp)
    {
        prev = down; down = new HashSet<string>();
        keyPrev.Clear(); foreach (var k in keyDown) keyPrev.Add(k); keyDown.Clear();
        bool focused = enabled && DisplayServer.WindowIsFocused();
        if (focused)
        {
            foreach (var kv in BIND) foreach (var k in kv.Value) if (Input.IsPhysicalKeyPressed(k)) { down.Add(kv.Key); break; }
            var pads = Input.GetConnectedJoypads();
            if (pads.Count > 0)
            {
                int d = pads[0];
                foreach (var kv in PAD) foreach (var b in kv.Value) if (Input.IsJoyButtonPressed(d, b)) down.Add(kv.Key);
                stick = new Vector2(Input.GetJoyAxis(d, JoyAxis.LeftX), Input.GetJoyAxis(d, JoyAxis.LeftY));
                if (stick.X < -0.4f) down.Add("left");
                if (stick.X > 0.4f) down.Add("right");
                if (stick.Y < -0.5f) down.Add("up");
                if (stick.Y > 0.5f) down.Add("down");
                aimStick = new Vector2(Input.GetJoyAxis(d, JoyAxis.RightX), Input.GetJoyAxis(d, JoyAxis.RightY));
                if (aimStick.Length() > 0.35f) padAim = true;
                if (Input.GetJoyAxis(d, JoyAxis.TriggerLeft) > 0.4f) down.Add("focus");
                if (Input.GetJoyAxis(d, JoyAxis.TriggerRight) > 0.4f) down.Add("fire");
            }
        }
        foreach (var a in injected) down.Add(a);
        foreach (var a in TestHeld) down.Add(a);
        injected.Clear();
        var mp = MouseOverride ?? vp.GetMousePosition();
        if (mp.X != mouse.x || mp.Y != mouse.y) padAim = false;
        mouse.x = mp.X; mouse.y = mp.Y;
        bool l = ForceL || (focused && Input.IsMouseButtonPressed(MouseButton.Left)), r = ForceR || (focused && Input.IsMouseButtonPressed(MouseButton.Right));
        mouse.lp |= l && !mouse.l; mouse.rp |= r && !mouse.r;
        mouse.lr |= !l && mouse.l; mouse.rr |= !r && mouse.r;
        mouse.l = l; mouse.r = r;
    }
    public static void OnEvent(InputEvent ev)
    {
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

    public static void EndFrame()
    {
        mouse.lp = mouse.rp = mouse.lr = mouse.rr = false;
        mouse.wheel = 0;
    }
    public static void Clear()
    {
        prev = new HashSet<string>(down);
        mouse.l = mouse.r = false;
    }
}
