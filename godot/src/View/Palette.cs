// Each region has one colour identity, the way the areas of Hollow Knight do, and like Rain World's
// palettes it drifts with the cycle: as the tide comes in everything goes colder and darker and the fog
// thickens. Layers drawn in encoded value/accent/glow are coloured from here by the palette shader.
using System;
using System.Collections.Generic;
using Godot;

namespace Hollowmere;

public class RegionPalette
{
    public Color shade, lit, fog, accent, glow;
    public Color waterTop, waterDeep;
}

public static class Palettes
{
    static RegionPalette P(string shade, string lit, string fog, string accent, string glow, string wTop, string wDeep) => new()
    {
        shade = H.Col(shade), lit = H.Col(lit), fog = H.Col(fog), accent = H.Col(accent), glow = H.Col(glow), waterTop = H.Col(wTop), waterDeep = H.Col(wDeep),
    };
    static readonly Dictionary<string, RegionPalette> REG = new()
    {
        // sea caves and the drowned chapel: grey-green, sea-glass light
        ["wrackline"] = P("#0a1113", "#8aa4a0", "#4c6c6e", "#7fb59e", "#d4f2ea", "#3c6e74", "#081a20"),
        // the Saltworks: bone white and amber, salt hanging in the air
        ["saltworks"] = P("#130e0a", "#cbbb9e", "#7c6c58", "#f2e8d2", "#ffe0ac", "#7a7a68", "#1c1a14"),
        // the Cloister: moss green, ivy, wet stone
        ["cloister"] = P("#090f0a", "#8fb088", "#456648", "#a2d47c", "#e2f6c2", "#3a6a52", "#06160e"),
        // the Choir: deep blue under gold
        ["choir"] = P("#080a15", "#7c8ac4", "#323f78", "#e2be6a", "#ffe8b4", "#34508a", "#060a1e"),
        // the Ribvault: ochre and old bone
        ["ribvault"] = P("#120c06", "#c6a878", "#785c36", "#eed8a8", "#ffd8a0", "#6a5a3a", "#140e06"),
        // the Gullet: near black, lit by things that glow
        ["gullet"] = P("#03070a", "#3c686a", "#1a3e46", "#40e8ce", "#7cf8e8", "#1a4a50", "#020a0e"),
        // the Skull: rust and lamp-orange
        ["skull"] = P("#110803", "#c28452", "#683c1a", "#ffaa52", "#ffc674", "#6a4020", "#140804"),
    };
    static readonly RegionPalette OUTDOOR = P("#06090d", "#6c7e8c", "#2c3e4e", "#7c9c8c", "#e0eaf4", "#2e5a6a", "#06121a");

    public static RegionPalette Raw(RoomDef r) => r == null ? OUTDOOR : r.Outdoor ? OUTDOOR : REG.TryGetValue(r.Region, out var p) ? p : REG["wrackline"];
    // The palette in use: in the current room, the one LightingManager is blending toward as Maren crosses regions.
    public static RegionPalette For(RoomDef r) => r != null && r == G.room && LightingManager.Inst?.Palette != null ? LightingManager.Inst.Palette : Raw(r);
    public static RegionPalette Lerp(RegionPalette a, RegionPalette b, float t) => a == b || a == null ? b : new()
    {
        shade = a.shade.Lerp(b.shade, t), lit = a.lit.Lerp(b.lit, t), fog = a.fog.Lerp(b.fog, t), accent = a.accent.Lerp(b.accent, t), glow = a.glow.Lerp(b.glow, t),
        waterTop = a.waterTop.Lerp(b.waterTop, t), waterDeep = a.waterDeep.Lerp(b.waterDeep, t),
    };

    // Toward high water: darker, colder, wetter.
    static Color Tide(Color c, float w) => c.Lerp(new Color(c.R * 0.72f, c.G * 0.8f, c.B * 0.92f), w);

    // How far the tide has come in, eased so the colours drift rather than step.
    static float tideSmooth = -1;
    public static float TideW(float dt)
    {
        float target = Hollowmere.Tide.paused ? 0 : Hollowmere.Tide.Level01();
        tideSmooth = tideSmooth < 0 ? target : H.Approach(tideSmooth, target, dt * 0.2f);
        return tideSmooth;
    }

    // Sets one layer's material for the room. fog is how deep the layer sits; bright undoes the
    // world's ambient darkening for layers in the lit canvas.
    public static void Apply(ShaderMaterial m, RoomDef r, float w, float fog, float heightFog, float dither, float bright = 1, float vault = 0)
    {
        m.SetShaderParameter("vault", vault); m.SetShaderParameter("room_top", r.Py); m.SetShaderParameter("room_bottom", r.Py + r.Ph);
        var p = For(r);
        m.SetShaderParameter("shade_col", Tide(p.shade, w));
        m.SetShaderParameter("lit_col", Tide(p.lit, w));
        m.SetShaderParameter("fog_col", Tide(p.fog, w * 0.9f));
        m.SetShaderParameter("accent_col", Tide(p.accent, w * 0.6f));
        m.SetShaderParameter("glow_col", p.glow);
        m.SetShaderParameter("fog_amt", Math.Clamp(fog + w * 0.08f * (fog > 0 ? 1 : 0), 0, 1));
        m.SetShaderParameter("height_fog", heightFog * (1 + w * 0.6f));
        m.SetShaderParameter("fog_top", r.Py + r.Ph * 0.35f);
        m.SetShaderParameter("fog_bottom", r.Py + r.Ph);
        m.SetShaderParameter("dither", dither);
        m.SetShaderParameter("bright", bright);
    }

    static Shader shader, worldShader;
    // The lit world's material: the palette, then relief, direct and indirect light (shaders/world.gdshader).
    public static ShaderMaterial WorldMaterial(bool premul = true)
    {
        worldShader ??= GD.Load<Shader>("res://shaders/world.gdshader");
        var m = new ShaderMaterial { Shader = worldShader };
        m.SetShaderParameter("premul", premul ? 1f : 0f);
        m.SetShaderParameter("alpha_mul", 1f);
        return m;
    }
    public static ShaderMaterial Material(float blur = 0, bool premul = true, float alpha = 1)
    {
        shader ??= GD.Load<Shader>("res://shaders/palette.gdshader");
        var m = new ShaderMaterial { Shader = shader };
        m.SetShaderParameter("blur", blur);
        m.SetShaderParameter("premul", premul ? 1f : 0f);
        m.SetShaderParameter("alpha_mul", alpha);
        return m;
    }
}
