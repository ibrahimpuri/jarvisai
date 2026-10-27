// The lighting manager: one place that decides how the world is lit, so every layer agrees.
//
// Direct light. Every lamp, sconce, fire, flare and the lantern is a Light2D that casts the engine's 2D shadows
// from the masonry; the world shader shades each of them per pixel against relief read from the drawing. Each
// region also has a key light (a DirectionalLight2D): the moon outdoors, with shadows, so overhangs throw shade;
// indoors a soft fill from the direction the region's light comes from (the rose window, the kilns, the shafts).
//
// Indirect light. Ambient arrives as a hemisphere (one colour from above, another from below) times the region's
// ambient (the CanvasModulate). Bounce comes from the radiance buffer: every light in view is splatted as a wide
// soft blob into a quarter-resolution target, which the world, the characters and the fog sample, so light
// spills around corners and warms whatever stands near a fire.
//
// Moods. Each region has a Mood (ambient, hemisphere, key light, bounce, fog, wetness, depth of field and the
// colour grade). Crossing into another region blends the old mood into the new over a few seconds, and blends
// the region palettes the baked layers are coloured from, so nothing snaps. The tide pulls every mood colder and
// darker as it comes in. StoryFocus can lean the ambient toward the colour of whatever Maren is looking at.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public class Mood
{
    public Color ambient = new(0.62f, 0.62f, 0.62f);   // the dark the world sits in
    public Color sky = new(1, 1, 1), ground = new(0.7f, 0.7f, 0.7f);
    public Color key = new(0.7f, 0.8f, 1f);
    public float keyEnergy = 0.3f, keyAngle = 0.35f, keyHeight = 0.55f;   // angle from straight down, radians (+ toward the right)
    public float indirect = 0.7f, ao = 0.6f;
    public Color fog = new(0.3f, 0.35f, 0.38f);
    public float fogBack = 0.25f, fogFront = 0.05f, scatter = 0.6f, shafts = 0.25f;
    public float wet, spec = 0.6f, shininess = 28;
    public float farBlur = 1.3f, midBlur = 0.5f, foreBlur = 2.0f;
    public float exposure = 1, contrast = 0.2f, saturation = 0.95f, bloom = 0.45f;
    public Color lift = new(0, 0, 0), gamma = new(1, 1, 1), gain = new(1, 1, 1), shadowTint = new(0, 0, 0), highTint = new(0, 0, 0);

    public Mood Copy() => (Mood)MemberwiseClone();
    static Color L(Color a, Color b, float t) => a.Lerp(b, t);
    public static Mood Lerp(Mood a, Mood b, float t) => new()
    {
        ambient = L(a.ambient, b.ambient, t), sky = L(a.sky, b.sky, t), ground = L(a.ground, b.ground, t), key = L(a.key, b.key, t),
        keyEnergy = H.Lerp(a.keyEnergy, b.keyEnergy, t), keyAngle = H.Lerp(a.keyAngle, b.keyAngle, t), keyHeight = H.Lerp(a.keyHeight, b.keyHeight, t),
        indirect = H.Lerp(a.indirect, b.indirect, t), ao = H.Lerp(a.ao, b.ao, t), fog = L(a.fog, b.fog, t),
        fogBack = H.Lerp(a.fogBack, b.fogBack, t), fogFront = H.Lerp(a.fogFront, b.fogFront, t), scatter = H.Lerp(a.scatter, b.scatter, t), shafts = H.Lerp(a.shafts, b.shafts, t),
        wet = H.Lerp(a.wet, b.wet, t), spec = H.Lerp(a.spec, b.spec, t), shininess = H.Lerp(a.shininess, b.shininess, t),
        farBlur = H.Lerp(a.farBlur, b.farBlur, t), midBlur = H.Lerp(a.midBlur, b.midBlur, t), foreBlur = H.Lerp(a.foreBlur, b.foreBlur, t),
        exposure = H.Lerp(a.exposure, b.exposure, t), bloom = H.Lerp(a.bloom, b.bloom, t), contrast = H.Lerp(a.contrast, b.contrast, t), saturation = H.Lerp(a.saturation, b.saturation, t),
        lift = L(a.lift, b.lift, t), gamma = L(a.gamma, b.gamma, t), gain = L(a.gain, b.gain, t), shadowTint = L(a.shadowTint, b.shadowTint, t), highTint = L(a.highTint, b.highTint, t),
    };
}

public partial class LightingManager : Node
{
    public static LightingManager Inst;
    const float BLEND_TIME = 2.8f;
    public const int RAD_DIV = 4;   // the radiance buffer is a quarter of the view

    // ---- the moods -----------------------------------------------------------------------------------------
    static Color C(float r, float g, float b) => new(r, g, b);
    public static readonly Dictionary<string, Mood> MOODS = new()
    {
        // sea caves and the drowned chapel: grey-green, sea-glass light, everything damp
        ["wrackline"] = new Mood
        {
            ambient = C(0.440f, 0.510f, 0.528f), sky = C(1.06f, 1.1f, 1.1f), ground = C(0.62f, 0.72f, 0.7f), key = C(0.6f, 0.82f, 0.85f), keyEnergy = 0.32f, keyAngle = -0.4f,
            indirect = 0.85f, fog = C(0.3f, 0.42f, 0.42f), fogBack = 0.3f, fogFront = 0.06f, scatter = 0.65f, shafts = 0.22f, wet = 0.15f, spec = 0.75f,
            contrast = 0.25f, saturation = 0.9f, lift = C(0.012f, 0.022f, 0.026f), gamma = C(0.98f, 1.0f, 1.02f), shadowTint = C(0, 0.012f, 0.018f), highTint = C(0.02f, 0.014f, 0),
        },
        // the Saltworks: bone white and amber, salt hanging in the air, kiln light from below
        ["saltworks"] = new Mood
        {
            ambient = C(0.554f, 0.502f, 0.431f), sky = C(1.08f, 1.03f, 0.95f), ground = C(0.92f, 0.74f, 0.56f), key = C(1f, 0.8f, 0.55f), keyEnergy = 0.3f, keyAngle = 0.5f,
            indirect = 1.0f, fog = C(0.5f, 0.44f, 0.36f), fogBack = 0.34f, fogFront = 0.08f, scatter = 0.85f, shafts = 0.38f, wet = 0, spec = 0.3f,
            contrast = 0.3f, saturation = 0.9f, lift = C(0.02f, 0.012f, 0.004f), gain = C(1.02f, 1.0f, 0.96f), shadowTint = C(0.014f, 0.006f, 0), highTint = C(0.03f, 0.02f, 0),
        },
        // the Cloister: moss green, ivy, wet stone
        ["cloister"] = new Mood
        {
            ambient = C(0.431f, 0.502f, 0.414f), sky = C(1.0f, 1.1f, 0.95f), ground = C(0.6f, 0.72f, 0.55f), key = C(0.72f, 0.88f, 0.66f), keyEnergy = 0.3f, keyAngle = -0.25f,
            indirect = 0.75f, fog = C(0.28f, 0.38f, 0.3f), fogBack = 0.3f, fogFront = 0.06f, scatter = 0.6f, shafts = 0.3f, wet = 0.22f, spec = 0.6f,
            contrast = 0.25f, saturation = 0.8f, lift = C(0.01f, 0.02f, 0.01f), shadowTint = C(0, 0.014f, 0.006f), highTint = C(0.014f, 0.016f, 0),
        },
        // the Choir: deep blue under gold, light from the rose window, the floor sunk in water
        ["choir"] = new Mood
        {
            ambient = C(0.370f, 0.405f, 0.598f), sky = C(0.95f, 1.0f, 1.2f), ground = C(0.82f, 0.72f, 0.52f), key = C(1f, 0.86f, 0.56f), keyEnergy = 0.38f, keyAngle = 0.3f,
            indirect = 0.95f, fog = C(0.22f, 0.26f, 0.48f), fogBack = 0.32f, fogFront = 0.04f, scatter = 0.6f, shafts = 0.42f, wet = 0.35f, spec = 0.8f,
            bloom = 0.55f, contrast = 0.3f, saturation = 1.04f, lift = C(0.004f, 0.008f, 0.03f), shadowTint = C(0, 0.004f, 0.026f), highTint = C(0.035f, 0.024f, 0),
        },
        // the Ribvault: ochre and old bone, wind dust in the shafts
        ["ribvault"] = new Mood
        {
            ambient = C(0.528f, 0.458f, 0.370f), sky = C(1.1f, 1.0f, 0.85f), ground = C(0.76f, 0.62f, 0.48f), key = C(1f, 0.85f, 0.6f), keyEnergy = 0.36f, keyAngle = 0.45f,
            indirect = 0.8f, fog = C(0.46f, 0.38f, 0.28f), fogBack = 0.28f, fogFront = 0.06f, scatter = 0.7f, shafts = 0.4f, wet = 0, spec = 0.3f,
            contrast = 0.28f, saturation = 0.95f, lift = C(0.018f, 0.012f, 0.004f), shadowTint = C(0.012f, 0.006f, 0), highTint = C(0.03f, 0.018f, 0),
        },
        // the Gullet: near black, slick, lit by the things that glow in it
        ["gullet"] = new Mood
        {
            ambient = C(0.27f, 0.39f, 0.42f), sky = C(0.8f, 1.0f, 1.0f), ground = C(0.42f, 0.72f, 0.66f), key = C(0.3f, 0.9f, 0.8f), keyEnergy = 0.16f, keyAngle = 0.0f,
            indirect = 1.25f, ao = 0.8f, fog = C(0.08f, 0.22f, 0.24f), fogBack = 0.36f, fogFront = 0.08f, scatter = 1.0f, shafts = 0.04f, wet = 0.85f, spec = 1.0f, shininess = 40,
            bloom = 0.7f, contrast = 0.35f, saturation = 1.05f, lift = C(0, 0.012f, 0.014f), shadowTint = C(0, 0.012f, 0.012f), highTint = C(0, 0.02f, 0.016f),
        },
        // the Skull: rust and lamp-orange, the lighthouse above
        ["skull"] = new Mood
        {
            ambient = C(0.528f, 0.396f, 0.308f), sky = C(1.1f, 0.9f, 0.75f), ground = C(0.72f, 0.5f, 0.36f), key = C(1f, 0.7f, 0.42f), keyEnergy = 0.36f, keyAngle = 0.35f,
            indirect = 1.0f, fog = C(0.42f, 0.26f, 0.16f), fogBack = 0.26f, fogFront = 0.06f, scatter = 0.8f, shafts = 0.3f, wet = 0, spec = 0.35f,
            bloom = 0.5f, contrast = 0.3f, saturation = 1.0f, lift = C(0.02f, 0.01f, 0.004f), shadowTint = C(0.014f, 0.004f, 0), highTint = C(0.035f, 0.016f, 0),
        },
        // the shore at night: moonlight from above with shadows under every overhang, rain on everything
        ["outdoor"] = new Mood
        {
            ambient = C(0.4f, 0.48f, 0.6f), sky = C(1.15f, 1.2f, 1.32f), ground = C(0.48f, 0.54f, 0.62f), key = C(0.72f, 0.82f, 1f), keyEnergy = 0.55f, keyAngle = -0.5f, keyHeight = 0.6f,
            indirect = 0.6f, fog = C(0.2f, 0.26f, 0.34f), fogBack = 0.12f, fogFront = 0.04f, scatter = 0.55f, shafts = 0.0f, wet = 0.35f, spec = 0.9f,
            farBlur = 0.9f, bloom = 0.35f, contrast = 0.3f, saturation = 0.88f, lift = C(0.008f, 0.014f, 0.03f), shadowTint = C(0, 0.008f, 0.024f), highTint = C(0.01f, 0.01f, 0.012f),
        },
    };
    public static string Key(RoomDef r) => r == null ? "wrackline" : r.Outdoor ? "outdoor" : MOODS.ContainsKey(r.Region) ? r.Region : "wrackline";

    // ---- state ------------------------------------------------------------------------------------------------
    public Mood Cur = MOODS["wrackline"].Copy();
    Mood from, to;
    float blendT = 1;
    string curKey;
    RegionPalette palFrom, palTo;
    public RegionPalette Palette { get; private set; }
    public float Blend => blendT;          // 0 just crossed into a region, 1 settled
    public string Region => curKey;

    // what StoryFocus asks for this frame
    public Color FocusTint = new(1, 1, 1);
    public float FocusW, FocusBlur, FocusVignette;

    // nodes and materials
    public SubViewport Radiance;
    DrawLayer radianceArt;
    public DirectionalLight2D KeyLight;
    readonly List<ShaderMaterial> world = new(), entity = new(), fog = new();
    ShaderMaterial post;
    public List<LightSrc> Sources = new();
    public Vector2 LanternPos; public float LanternAim, LanternRange; public bool LanternOn, Focusing;
    public List<(float x, float y, Color c)> Windows = new();

    public override void _Ready()
    {
        Inst = this;
        Radiance = new SubViewport { TransparentBg = true, Disable3D = true, Size = new Vector2I(H.VIEW_W / RAD_DIV, H.VIEW_H / RAD_DIV), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear };
        AddChild(Radiance);
        radianceArt = new DrawLayer { Paint = PaintRadiance, Name = "radiance", Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        Radiance.AddChild(radianceArt);
    }

    // Layers register their materials so every frame's lighting reaches them.
    public void RegisterWorld(ShaderMaterial m, bool texNormals) { m.SetShaderParameter("tex_normals", texNormals); m.SetShaderParameter("radiance", Radiance.GetTexture()); world.Add(m); }
    public void RegisterEntity(ShaderMaterial m) { m.SetShaderParameter("radiance", Radiance.GetTexture()); entity.Add(m); }
    public void RegisterFog(ShaderMaterial m) { m.SetShaderParameter("radiance", Radiance.GetTexture()); fog.Add(m); }
    public void RegisterPost(ShaderMaterial m) => post = m;

    // A new room: if it belongs to another region, the old mood starts blending into the new one.
    public void OnRoom(RoomDef r, bool snap = false)
    {
        string k = Key(r);
        var raw = Palettes.Raw(r);
        if (curKey == null || snap) { curKey = k; Cur = MOODS[k].Copy(); from = to = Cur; blendT = 1; Palette = palFrom = palTo = raw; return; }
        if (k == curKey) { palTo = raw; if (blendT >= 1) Palette = raw; return; }
        from = Cur.Copy(); to = MOODS[k]; blendT = 0; curKey = k;
        palFrom = Palette ?? raw; palTo = raw;
    }

    // Everything the world needs from the light this frame.
    public void Step(float dt, RoomDef r, float tideW, float lightning)
    {
        if (r == null) return;
        dt = MathF.Min(dt, 0.05f);   // a long frame (a room bake) must not skip the blend
        if (curKey == null) OnRoom(r, true);
        if (blendT < 1)
        {
            blendT = MathF.Min(1, blendT + dt / BLEND_TIME);
            float s = blendT * blendT * (3 - 2 * blendT);
            Cur = Mood.Lerp(from, to, s);
            Palette = Palettes.Lerp(palFrom, palTo, s);
        }
        else { Cur = to.Copy(); Palette = palTo; }
        var m = Cur;
        // the tide pulls everything colder and darker as it comes in
        float tw = tideW * (r.Row >= 4 || r.Outdoor ? 1 : 0.4f);
        var amb = m.ambient.Lerp(new Color(m.ambient.R * 0.8f, m.ambient.G * 0.86f, m.ambient.B * 0.95f), tw);
        // what Maren is looking at leans the ambient toward its own colour
        if (FocusW > 0) amb = amb.Lerp(new Color(amb.R * FocusTint.R, amb.G * FocusTint.G, amb.B * FocusTint.B), FocusW);
        if (r.Outdoor && lightning > 0) amb = amb.Lerp(Colors.White, lightning * 0.5f);
        Ambient = amb;
        bool tidal = r.Row == 4 && r.FixedWater == null;
        float wetLine = tidal ? (Tide.SEA_HIGH - Tide.spring) * H.TILE : r.Deep || r.FixedWater != null ? r.Py - 1000 : 100000;
        float wet = MathF.Min(1, m.wet + tw * 0.15f + (r.Brine || r.Deep ? 0.5f : 0));
        var ambV = new Vector3(amb.R, amb.G, amb.B);
        foreach (var w in world)
        {
            w.SetShaderParameter("sky_col", m.sky); w.SetShaderParameter("ground_col", m.ground);
            w.SetShaderParameter("ambient_mod", ambV); w.SetShaderParameter("indirect", m.indirect); w.SetShaderParameter("ao_amt", m.ao);
            w.SetShaderParameter("wet_line", wetLine); w.SetShaderParameter("wet_amt", wet);
            w.SetShaderParameter("spec_amt", m.spec); w.SetShaderParameter("shininess", m.shininess);
        }
        var tint = FocusW > 0 ? Colors.White.Lerp(FocusTint, FocusW * 0.35f) : Colors.White;
        foreach (var e in entity) { e.SetShaderParameter("ambient_mod", ambV); e.SetShaderParameter("indirect", m.indirect * 0.8f); e.SetShaderParameter("tint", tint); }
        var cam = G.cam;
        float fogLow = MathF.Min(r.Py + r.Ph, float.IsFinite(World.RoomSurface(r, r.Px + r.Pw / 2)) ? World.RoomSurface(r, r.Px + r.Pw / 2) + 40 : r.Py + r.Ph);
        var fogCol = m.fog.Lerp(new Color(m.fog.R * 0.8f, m.fog.G * 0.9f, m.fog.B), tw);
        for (int i = 0; i < fog.Count; i++)
        {
            var f = fog[i]; bool back = i == 0;
            f.SetShaderParameter("fog_col", back ? fogCol * 0.8f : fogCol);
            f.SetShaderParameter("density", (back ? m.fogBack * 0.62f : m.fogFront) * (1 + tw * 0.4f));
            f.SetShaderParameter("scatter", m.scatter * (back ? 0.6f : 0.35f));
            f.SetShaderParameter("cam", cam); f.SetShaderParameter("parallax", back ? 0.75f : 1.1f);
            f.SetShaderParameter("low_y", fogLow); f.SetShaderParameter("high_y", r.Py);
            f.SetShaderParameter("shafts", m.shafts * (back ? 1 : 0.35f) * (1 - tw * 0.4f));
            f.SetShaderParameter("shaft_angle", m.keyAngle); f.SetShaderParameter("shaft_col", m.key);
        }
        if (post != null)
        {
            post.SetShaderParameter("exposure", m.exposure); post.SetShaderParameter("contrast", m.contrast); post.SetShaderParameter("saturation", m.saturation * (1 - tw * 0.08f));
            post.SetShaderParameter("lift", m.lift); post.SetShaderParameter("gamma", m.gamma); post.SetShaderParameter("gain", m.gain);
            post.SetShaderParameter("shadow_tint", m.shadowTint); post.SetShaderParameter("high_tint", m.highTint);
            post.SetShaderParameter("focus_vignette", FocusVignette);
            post.SetShaderParameter("bloom", Settings.Bloom ? m.bloom * (r.Outdoor && lightning > 0 ? 1 + lightning : 1) : 0);
            post.SetShaderParameter("lean", G.leanZoom); post.SetShaderParameter("lean_focal", G.leanFocal / new Vector2(H.VIEW_W, H.VIEW_H));
        }
        if (KeyLight != null)
        {
            KeyLight.Color = m.key; KeyLight.Energy = m.keyEnergy * (1 - tw * 0.3f);
            KeyLight.Rotation = m.keyAngle; KeyLight.Height = m.keyHeight;
            KeyLight.ShadowEnabled = r.Outdoor;
        }
        radianceArt.QueueRedraw();
    }
    public Color Ambient { get; private set; } = new(0.62f, 0.62f, 0.62f);
    // Depth of field for the planes, with whatever focus pull StoryFocus asks for.
    public float FarBlur => Cur.farBlur + FocusBlur;
    public float MidBlur => Cur.midBlur + FocusBlur * 0.6f;
    public float ForeBlur => Cur.foreBlur;

    // ---- the radiance buffer ---------------------------------------------------------------------------------------
    // Every light in view as a wide, soft blob at a quarter of the view's resolution: the bounce the world samples.
    void PaintRadiance(Canvas c)
    {
        var cam = G.cam; float k = 1f / RAD_DIV;
        void Blob(float x, float y, float r, Color col, float i)
        {
            float sx = (x - cam.X) * k, sy = (y - cam.Y) * k, sr = r * k;
            if (sx < -sr || sy < -sr || sx > H.VIEW_W * k + sr || sy > H.VIEW_H * k + sr) return;
            c.Glow(sx, sy, sr, new Color(col.R * i, col.G * i, col.B * i, 1));
        }
        foreach (var l in Sources)
        {
            var col = l.color == null ? new Color(1, 0.8f, 0.55f) : Parse(l.color);
            Blob(l.x, l.y, l.r * 2.2f, col, 0.32f * l.i);
        }
        if (LanternOn)
        {
            // the lantern throws its light ahead of her, along the beam
            var d = new Vector2(MathF.Cos(LanternAim), MathF.Sin(LanternAim));
            Blob(LanternPos.X + d.X * LanternRange * 0.35f, LanternPos.Y + d.Y * LanternRange * 0.35f, LanternRange * 1.2f, new Color(1f, 0.82f, 0.56f), Focusing ? 0.3f : 0.22f);
            Blob(LanternPos.X, LanternPos.Y, 70, new Color(1f, 0.82f, 0.56f), 0.16f);
        }
        foreach (var (x, y, col) in Windows) Blob(x, y + 40, 120, col, 0.12f);
    }
    static readonly Dictionary<string, Color> rgb = new();
    static Color Parse(string s)
    {
        if (rgb.TryGetValue(s, out var c)) return c;
        var p = s.Split(',');
        c = new Color(float.Parse(p[0]) / 255f, float.Parse(p[1]) / 255f, float.Parse(p[2]) / 255f);
        rgb[s] = c; return c;
    }
}
