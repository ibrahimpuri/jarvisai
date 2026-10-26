// Player settings kept between sessions: fullscreen, volume, glow, screen shake.
using System.Text.Json.Nodes;
using Godot;

namespace Hollowmere;

public static class Settings
{
    const string PATH = "user://settings.json";
    public static bool Fullscreen = true, Bloom = true, Shake = true;
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
        }
        catch { }
    }
    public static void Save()
    {
        var o = new JsonObject { ["fullscreen"] = Fullscreen, ["bloom"] = Bloom, ["shake"] = Shake, ["volume"] = Snd.Volume };
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
