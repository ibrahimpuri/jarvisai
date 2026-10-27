// Player settings kept between sessions: display, volume, and controller options.
using System;
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public static class Settings
{
    const string PATH = "user://settings.json";
    public static bool Fullscreen = true, Bloom = true, Shake = true;
    public static float Vibration = 1;            // 0, 0.5 or 1
    public static bool AimAssist = true, Gyro, NintendoConfirm = true, Hints = true, Guide = true;
    public static string Prompts = "auto";        // auto, xbox, playstation, switch
    public static string Gore = "full";           // full, reduced, off
    public static WorldEnvironment Env;

    public static void Load()
    {
        if (!FileAccess.FileExists(PATH)) return;
        try
        {
            var p = new Props(JsonNode.Parse(FileAccess.GetFileAsString(PATH)).AsObject());
            Fullscreen = !p.Has("fullscreen") || p.B("fullscreen");
            Bloom = !p.Has("bloom") || p.B("bloom");
            Shake = !p.Has("shake") || p.B("shake");
            if (p.Has("volume")) Snd.Volume = p.F("volume");
            if (p.Has("vibration")) Vibration = Math.Clamp(p.F("vibration"), 0, 1);
            AimAssist = !p.Has("aimAssist") || p.B("aimAssist");
            Gyro = p.B("gyro");
            Hints = !p.Has("hints") || p.B("hints");
            Guide = !p.Has("guide") || p.B("guide");
            NintendoConfirm = !p.Has("nintendoConfirm") || p.B("nintendoConfirm");
            if (p.Has("prompts")) Prompts = p.S("prompts");
            if (p.Has("gore")) Gore = p.S("gore");
            if (p.Has("music")) Score.MusicVolume = Math.Clamp(p.F("music"), 0, 1);
        }
        catch { }
    }
    public static void Save()
    {
        var o = new JsonObject
        {
            ["fullscreen"] = Fullscreen, ["bloom"] = Bloom, ["shake"] = Shake, ["volume"] = Snd.Volume,
            ["vibration"] = Vibration, ["aimAssist"] = AimAssist, ["gyro"] = Gyro, ["nintendoConfirm"] = NintendoConfirm, ["prompts"] = Prompts, ["hints"] = Hints, ["guide"] = Guide, ["gore"] = Gore, ["music"] = Score.MusicVolume,
        };
        using var f = FileAccess.Open(PATH, FileAccess.ModeFlags.Write);
        f?.StoreString(o.ToJsonString());
    }
    public static void Apply()
    {
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (Env?.Environment != null) Env.Environment.GlowEnabled = Bloom;
    }
    public static void ToggleFullscreen()
    {
        Fullscreen = DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Fullscreen && DisplayServer.WindowGetMode() != DisplayServer.WindowMode.ExclusiveFullscreen;
        Apply();
        Save();
    }
}
