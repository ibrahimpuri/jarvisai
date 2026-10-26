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
    public HashSet<string> held = new();

    public Tester(string[] a) { args = a; }

    public override async void _Ready()
    {
        Inp.enabled = false;
        Inp.HeldOverride = a => held.Contains(a);
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
        var img = GetViewport().GetTexture().GetImage();
        string path = outDir.TrimEnd('/') + "/" + name + ".png";
        img.SavePng(path);
    }
    void StartGame()
    {
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
            held.Clear();
            try { await sc(this); }
            catch (Exception e) { Check(n, false, e.Message); Errors.Add(e.ToString()); }
        }
    }

    // ---- helpers for scenarios ----
    public async Task Run(float seconds) => await Frames((int)(seconds * 60));
    public async Task Hold(string action, float seconds) { held.Add(action); await Run(seconds); held.Remove(action); }
    public async Task Tap(string action) { Inp.Press(action); await Frames(2); }
    public void Ok(string name, bool ok, string info = "") => Check(name, ok, info);
    public void Snap(string name) => Shot(name);
}
