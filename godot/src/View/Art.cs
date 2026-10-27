// Art: the made assets the build uses where they exist, and what the code draws where they do not. Everything an
// artist (or tools/paint_*.py) adds lives under res://art, and nothing breaks when a file is missing:
//   art/materials/<surface>_d.png, _n.png   tiling surface materials for the lit walls and rock (see Surface)
//   art/vistas/<region>.png                 a painted far vista per region, in the palette encoding
// Like the rooms, these are drawn in encoded value/accent/glow and coloured by the region's palette, so the tide
// recolours them too. docs/ART.md says how to make and add them.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public static class Art
{
    // HM_ART=off draws everything from code alone, to compare
    public static readonly bool Enabled = System.Environment.GetEnvironmentVariable("HM_ART") != "off";
    static readonly Dictionary<string, Texture2D> cache = new();
    public static Texture2D Tex(string path)
    {
        if (!Enabled) return null;
        if (cache.TryGetValue(path, out var t)) return t;
        string p = "res://art/" + path + ".png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null;
        cache[path] = t;
        return t;
    }

    // ---- surface materials ----------------------------------------------------------------------------------
    // scale: world px per tile; grain: how far the material's grain moves the drawing's value; bump: its normal's
    // weight against the drawing's relief; accent: how much moss, salt or weed it lets grow; cavity: how dark its pits
    public record Surface(string Name, float Scale, float Grain, float Bump, float Accent, float Cavity);
    public static readonly Dictionary<string, Surface> SURFACES = new()
    {
        ["ashlar"] = new("ashlar", 200, 0.42f, 0.9f, 0.3f, 0.4f),
        ["rock"] = new("rock", 240, 0.5f, 1.0f, 0.35f, 0.5f),
        ["brick"] = new("brick", 170, 0.42f, 0.9f, 0.28f, 0.4f),
        ["saltrock"] = new("saltrock", 220, 0.36f, 0.8f, 0.4f, 0.35f),
        ["bone"] = new("bone", 220, 0.45f, 0.9f, 0.25f, 0.4f),
        ["flesh"] = new("flesh", 260, 0.45f, 1.1f, 0.6f, 0.4f),
        ["shore"] = new("shore", 220, 0.5f, 1.0f, 0.4f, 0.45f),
    };
    // which surfaces a room is made of: its back wall, and the rock Maren walks on
    public static (Surface wall, Surface rock) SurfacesFor(RoomDef r) => Strata.Style(r) switch
    {
        "works" => (SURFACES["brick"], SURFACES["saltrock"]),
        "bone" => (SURFACES["bone"], SURFACES["bone"]),
        "flesh" => (SURFACES["flesh"], SURFACES["flesh"]),
        "shore" => (SURFACES["shore"], SURFACES["shore"]),
        _ => (SURFACES["ashlar"], SURFACES["rock"]),
    };
    public static void ApplySurface(ShaderMaterial m, Surface s, float amt = 1)
    {
        var d = s == null ? null : Tex("materials/" + s.Name + "_d");
        var n = s == null ? null : Tex("materials/" + s.Name + "_n");
        if (d == null || n == null) { m.SetShaderParameter("mat_amt", 0f); return; }
        m.SetShaderParameter("mat_detail", d); m.SetShaderParameter("mat_normal", n);
        m.SetShaderParameter("mat_amt", amt); m.SetShaderParameter("mat_scale", s.Scale);
        m.SetShaderParameter("mat_grain", s.Grain); m.SetShaderParameter("mat_bump", s.Bump);
        m.SetShaderParameter("mat_accent", s.Accent); m.SetShaderParameter("mat_cavity", s.Cavity);
    }
}
