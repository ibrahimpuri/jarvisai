// Automated checks run inside the real game: room screenshots and scripted scenarios.
// Usage: Hollowmere --  --shots <dir> [room...]     or     --test <scenario...> [--out <dir>]
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Hollowmere;

public partial class Tester : Node
{
    public bool FixedStep = true;
    public readonly List<string> Errors = new();
    readonly string[] args;
    int pass, fail;
    string outDir = "user://shots";

    public Tester(string[] a) { args = a; }

    public override async void _Ready()
    {
        Inp.enabled = false;
        int oi = Array.IndexOf(args, "--out");
        if (oi >= 0 && oi + 1 < args.Length) outDir = args[oi + 1];
        DirAccess.MakeDirRecursiveAbsolute(outDir.StartsWith("user://") ? ProjectSettings.GlobalizePath(outDir) : outDir);
        await Frames(2);
        try
        {
            if (args.Contains("--shots")) await Shots();
            if (args.Contains("--test")) await Tests();
        }
        catch (Exception e) { Errors.Add(e.ToString()); }
        foreach (var e in Errors.Distinct()) GD.Print("ERROR: " + e);
        GD.Print($"RESULT pass={pass} fail={fail} errors={Errors.Count}");
        GetTree().Quit(fail > 0 || Errors.Count > 0 ? 1 : 0);
    }
    public void BeforeStep() { }

    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    void Check(string name, bool ok, string info = "")
    {
        if (ok) pass++; else fail++;
        GD.Print((ok ? "PASS " : "FAIL ") + name + (info.Length > 0 ? "  " + info : ""));
    }
    void Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;   // nothing to capture
        var img = GetViewport().GetTexture()?.GetImage();
        if (img == null) return;   // headless: nothing to capture
        string path = outDir.TrimEnd('/') + "/" + name + ".png";
        img.SavePng(path);
    }
    void StartGame()
    {
        G.DeleteSave();
        Inp.TestHeld.Clear(); Inp.ForceL = Inp.ForceR = false; Inp.MouseOverride = null;
        if (Pad.TestMode) { PadReset(); Pad.EndSimulate(); }
        Glyphs.Force = null; Inp.sneakLatch = false;
        G.god = false;
        G.NewGame("keeper", 1);
        UI.Close();
        Narr.Reset();
        H.Toasts.Clear();
        G.titleT = 0;
    }
    static void PlaceInRoom(RoomDef r)
    {
        var p = G.player;
        var spots = new List<(int, int)>();
        for (int ty = r.Th - 2; ty > 1; ty--) for (int tx = 1; tx < r.Tw - 1; tx++)
            {
                int wx = r.Ox + tx, wy = r.Oy + ty;
                if (World.Solid(wx, wy + 1) == 1 && World.Solid(wx, wy) == 0 && World.Solid(wx, wy - 1) == 0) spots.Add((tx, ty));
            }
        var s = spots.OrderBy(a => Math.Abs(a.Item1 - r.Tw / 2)).FirstOrDefault((r.Tw / 2, r.Th / 2));
        p.x = r.Px + s.Item1 * 16 + 3; p.y = r.Py + (s.Item2 + 1) * 16 - p.h;
        G.SnapCamera();
    }

    async Task Shots()
    {
        int si = Array.IndexOf(args, "--shots");
        var only = args.Skip(si + 1).TakeWhile(a => !a.StartsWith("--")).ToList();
        if (only.Count > 0 && !Data.Room.ContainsKey(only[0])) { outDir = only[0]; only = only.Skip(1).ToList(); DirAccess.MakeDirRecursiveAbsolute(outDir); }
        StartGame();
        G.god = true;
        var ids = only.Count > 0 ? only : Data.Rooms.Select(r => r.Id).ToList();
        foreach (var id in ids)
        {
            G.EnterRoom(Data.Room[id]);
            PlaceInRoom(Data.Room[id]);
            G.titleT = 0; H.Toasts.Clear();
            Inp.MouseOverride = new Vector2(560, 180);
            var aimEnv = OS.GetEnvironment("HM_AIM");
            if (!string.IsNullOrEmpty(aimEnv)) { var a = aimEnv.Split(','); Inp.MouseOverride = new Vector2(float.Parse(a[0]), float.Parse(a[1])); }
            await Frames(20);
            Shot("room_" + id);
        }
        Check("room screenshots", Errors.Count == 0, ids.Count + " rooms");
    }

    async Task Tests()
    {
        int ti = Array.IndexOf(args, "--test");
        var names = args.Skip(ti + 1).TakeWhile(a => !a.StartsWith("--")).ToList();
        if (names.Count == 0) names = Scenarios.All.Keys.ToList();
        foreach (var n in names)
        {
            GD.Print("--- " + n);
            if (!Scenarios.All.TryGetValue(n, out var sc)) { Check(n, false, "no such scenario"); continue; }
            StartGame();
            await Frames(8);
            try { await sc(this); }
            catch (Exception e) { Check(n, false, e.Message); Errors.Add(e.ToString()); }
        }
    }

    // ---- helpers for scenarios ----
    public async Task Run(float seconds) => await Frames(Math.Max(1, (int)(seconds * 60)));
    public void Down(params string[] actions) { foreach (var a in actions) Inp.TestHeld.Add(a); }
    public void Release(params string[] actions) { foreach (var a in actions) Inp.TestHeld.Remove(a); }
    public async Task Hold(string action, float seconds) { Down(action); await Run(seconds); Release(action); await Frames(1); }
    // a key press: through the UI when a menu is open, otherwise to the game
    public async Task Tap(string action)
    {
        if (UI.IsOpen())
        {
            if (action == "interact" || action == "jump") UI.Inst.TestAccept();
            else if (action == "pause") UI.Inst.TestCancel();
            await Frames(2);
            return;
        }
        Down(action); await Frames(1); Release(action); await Frames(1);
    }
    public async Task Click() { Inp.ForceL = true; await Frames(2); Inp.ForceL = false; await Frames(1); }
    public async Task<bool> ClickButton(string text) { bool ok = UI.Inst.TestClick(text); await Frames(2); return ok; }
    public void AimAt(float wx, float wy) => Inp.MouseOverride = new Vector2(wx - G.cam.X, wy - G.cam.Y);
    public void Place(string roomId, float tx, float ty)
    {
        var r = Data.Room[roomId];
        if (G.room != r) G.EnterRoom(r);
        var p = G.player;
        p.x = r.Px + tx * 16 + 3; p.y = r.Py + (ty + 1) * 16 - p.h; p.vx = p.vy = 0;
        G.SnapCamera();
    }
    public (string room, float x, float y, int tx, int ty, int hp, string st, bool ground, string phase) State()
    {
        var p = G.player; var r = G.room;
        return (r.Id, MathF.Round(p.x), MathF.Round(p.y), H.FloorI(p.Cx() / 16) - r.Ox, H.FloorI((p.y + p.h - 1) / 16) - r.Oy, p.hp, p.state, p.onGround, Tide.PhaseId());
    }
    public void Ok(string name, bool ok, object info = null) => Check(name, ok, info?.ToString() ?? "");

    // A pretend controller on device 0, fed through Godot's own input pipeline.
    public void PadButton(JoyButton b, bool pressed) => Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = b, Pressed = pressed, Pressure = pressed ? 1 : 0 });
    public void PadAxis(JoyAxis a, float v) => Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = a, AxisValue = v });
    public async Task PadTap(JoyButton b) { PadButton(b, true); await Frames(3); PadButton(b, false); await Frames(3); }
    public async Task PadHold(JoyButton b, float seconds) { PadButton(b, true); await Run(seconds); PadButton(b, false); await Frames(2); }
    public void PadReset()
    {
        foreach (JoyButton b in Enum.GetValues(typeof(JoyButton))) if (b >= 0 && b < JoyButton.SdlMax && Input.IsJoyButtonPressed(0, b)) PadButton(b, false);
        foreach (var a in new[] { JoyAxis.LeftX, JoyAxis.LeftY, JoyAxis.RightX, JoyAxis.RightY, JoyAxis.TriggerLeft, JoyAxis.TriggerRight }) PadAxis(a, 0);
    }
    public void Snap(string name) => Shot(name);
}
