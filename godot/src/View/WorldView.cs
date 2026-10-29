// The world on screen: sky, back walls, bodies, water, masonry, effects, real 2D lights with
// shadows cast by the masonry, bloom, rain and a finishing post pass. Reads game state; never writes it.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public partial class DrawLayer : Node2D
{
    public Action<Canvas> Paint;
    readonly Canvas cv = new();
    public static double DrawMs;    // time spent in the painters, for the perf probe
    public static readonly Dictionary<string, double> ByLayer = new();
    public override void _Draw()
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var c = cv.Begin(this, true);
        Paint?.Invoke(c);
        c.Flush();
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        DrawMs += ms; ByLayer[Name] = ByLayer.GetValueOrDefault(Name) + ms;
    }
}

public partial class WorldView : Node2D
{
    const int TS = H.TILE;
    const int VW = H.VIEW_W, VH = H.VIEW_H;

    CanvasModulate modulate;
    bool silhouettes = true;
    DrawLayer maren;
    const int MAREN_BIT = 2;   // the light-mask bit Maren's layer is on
    LightingManager lighting;
    DirectionalLight2D keyLight;
    ShaderMaterial fogBackMat, fogFrontMat;
    DrawLayer back, deco, entities, water, waterLines, solid, haze, dyn, fxShaded, fxGlow, fxText, sky, rain, overlay, hud;
    DrawLayer far, midL, nearL, fore, foreMotes;
    // Static room art is baked once per room into offscreen textures, in the palette shader's encoding,
    // and drawn as single quads: the far, mid and near depths on their own parallax canvases, the back wall
    // and rock in the lit world, the out-of-focus foreground over everything.
    SubViewport backBake, solidBake, farBake, midBake, nearBake, foreBake;
    DrawLayer backArt, solidArt, farArt, midArt, nearArt, foreArt;
    ShaderMaterial farMat, midMat, nearMat, backMat, solidMat, decoMat, dynMat, foreMat;
    Rect2 farArea, midArea, nearArea, foreArea;
    int bakeWait, bakes;
    const float BAKE = 2;   // the bakes hold twice the pixels so props and edges stay crisp when the view is scaled up
    const float FAR_F = 0.3f, MID_F = 0.62f, NEAR_F = 0.84f, FORE_F = 1.12f;   // parallax: how fast each depth moves with the camera
    const float AMBIENT = 0.62f;   // the lit world's darkness; palettes are authored as seen with no lamp near
    List<DecorItem> decor = new();
    static readonly Dictionary<string, List<DecorItem>> decorCache = new();
    Node2D occluders, lightRoot;
    PointLight2D lantern, aura;
    readonly List<PointLight2D> pool = new();
    Camera2D camera;
    ColorRect postRect;
    ShaderMaterial waterMat, postMat;
    Texture2D lightTex, coneWide, coneFocus;
    Color ambient = new(0.35f, 0.38f, 0.42f);

    // per-room cache
    RoomDef cached;
    bool dirty = true;
    readonly List<(int x, int y)> dynCells = new(), windows = new(), ceilings = new(), tops = new();
    readonly List<int> pillars = new();

    // atmosphere
    class Drop { public float x, y, v, l; }
    class Mote { public string kind; public float x, y, vx, vy, life, p; }
    readonly List<Drop> drops = new();
    readonly List<Mote> motes = new();
    float lightningNext = 12, lightningFlash, lightningSecond = -1, underSmooth;

    public override void _Ready()
    {
        // Light shapes are imported textures (tools/make_light_textures.py writes them).
        lightTex = GD.Load<Texture2D>("res://textures/light_radial.png");
        coneWide = GD.Load<Texture2D>("res://textures/light_cone_wide.png");
        coneFocus = GD.Load<Texture2D>("res://textures/light_cone_focus.png");

        // one place decides how everything is lit (see LightingManager)
        lighting = new LightingManager { Name = "lighting" };
        AddChild(lighting);
        // sky behind everything, in screen space and outside the world's light
        var skyLayer = new CanvasLayer { Layer = -10 };
        AddChild(skyLayer);
        sky = new DrawLayer { Paint = PaintSky };
        skyLayer.AddChild(sky);
        // the depths: their own canvases, so no lamp reaches them, moving slower than the world
        farMat = Palettes.Material(); midMat = Palettes.Material(); nearMat = Palettes.Material(0.4f); foreMat = Palettes.Material(1.6f, true, 0.96f);
        // the lit world: palette, relief, direct and indirect light; the props drawn live are flat but lit the same way
        backMat = Palettes.WorldMaterial(); solidMat = Palettes.WorldMaterial(); decoMat = Palettes.WorldMaterial(false); dynMat = Palettes.WorldMaterial(false);
        lighting.RegisterWorld(backMat, true); lighting.RegisterWorld(solidMat, true); lighting.RegisterWorld(decoMat, false); lighting.RegisterWorld(dynMat, false);
        farBake = MakeBake(out farArt, c => Encoded(c, cc => Strata.PaintFar(cc, G.room, farArea)));
        midBake = MakeBake(out midArt, c => Encoded(c, cc => Strata.PaintMid(cc, G.room, midArea)));
        nearBake = MakeBake(out nearArt, c => Encoded(c, cc => Strata.PaintNear(cc, G.room, nearArea)));
        foreBake = MakeBake(out foreArt, c => Encoded(c, cc => Strata.PaintFore(cc, G.room, foreArea, (x, y) => Terrain.IsMass(At(G.room, x, y)))));
        far = DepthLayer(-8, farBake, () => farArea, FAR_F, farMat);
        midL = DepthLayer(-7, midBake, () => midArea, MID_F, midMat);
        // fog hanging between the depths and the playfield, lit from within by the lamps
        var fogLayer = new CanvasLayer { Layer = -6 };
        AddChild(fogLayer);
        var fogShader = GD.Load<Shader>("res://shaders/fog.gdshader");
        fogBackMat = new ShaderMaterial { Shader = fogShader };
        fogLayer.AddChild(new ColorRect { Material = fogBackMat, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(VW, VH) });
        lighting.RegisterFog(fogBackMat);
        // the ruin's own bones in silhouette, in front of the fog and behind the wall
        nearL = DepthLayer(-5, nearBake, () => nearArea, NEAR_F, nearMat);
        fogFrontMat = new ShaderMaterial { Shader = fogShader };
        fogFrontMat.SetShaderParameter("scale", 1.6f);

        modulate = new CanvasModulate { Color = ambient };
        AddChild(modulate);
        backBake = MakeBake(out backArt, PaintBack);
        solidBake = MakeBake(out solidArt, PaintSolid);
        back = Add(new DrawLayer { Paint = c => DrawBake(c, backBake), TextureFilter = TextureFilterEnum.Linear, Material = backMat });
        deco = Add(new DrawLayer { Paint = c => { c.Encode = true; c.EncGain = 1.5f; Decor.PaintDynamic(c, decor, G.Now, ViewRect()); }, Material = decoMat });
        var entShader = GD.Load<Shader>("res://shaders/entity.gdshader");
        var entMat = new ShaderMaterial { Shader = entShader };
        entities = Add(new DrawLayer { Paint = PaintEntities, Material = entMat });
        lighting.RegisterEntity(entMat);
        // Maren on her own light bit: the room's lamps and the key light reach her, her own lantern and aura do not
        // (they light the world around her, the way a Hollow Knight character is never lit by its own glow)
        var marenMat = new ShaderMaterial { Shader = entShader };
        maren = Add(new DrawLayer { Paint = PaintMaren, Material = marenMat, LightMask = MAREN_BIT });
        lighting.RegisterEntity(marenMat);
        water = Add(new DrawLayer { Paint = PaintWater });
        waterMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        water.Material = waterMat;
        waterLines = Add(new DrawLayer { Paint = PaintWaterLines });
        solid = Add(new DrawLayer { Paint = c => DrawBake(c, solidBake), TextureFilter = TextureFilterEnum.Linear, Material = solidMat });
        // a thin haze in front of the world, so a lamp carried through the hall glows in the air around it
        haze = Add(new DrawLayer { Paint = c => { c.Fill = Colors.White; c.FillRect(G.cam.X - 8, G.cam.Y - 8, VW + 16, VH + 16); }, Material = fogFrontMat });
        lighting.RegisterFog(fogFrontMat);
        // HM_GFX_OFF=fog,haze,rim hides the fog planes or the characters' ink and rim, to judge each one's part
        var off = (System.Environment.GetEnvironmentVariable("HM_GFX_OFF") ?? "").Split(',');
        fogLayer.Visible = !off.Contains("fog"); haze.Visible = !off.Contains("haze"); silhouettes = !off.Contains("rim");
        dyn = Add(new DrawLayer { Paint = c => { c.Encode = true; c.EncGain = 1.0f; PaintDynamic(c); }, Material = dynMat });
        var fxMat = new ShaderMaterial { Shader = entShader };
        fxShaded = Add(new DrawLayer { Paint = PaintFxShaded, Material = fxMat });
        lighting.RegisterEntity(fxMat);
        fxGlow = Add(new DrawLayer { Paint = PaintFxGlow, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add, LightMode = CanvasItemMaterial.LightModeEnum.Unshaded } });
        fxText = Add(new DrawLayer { Paint = PaintFxText, Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded } });
        occluders = Add(new Node2D());
        lightRoot = Add(new Node2D());

        // lights stand a little off the wall (height), so the relief in the world shader turns toward them
        lantern = new PointLight2D { Texture = coneWide, Color = new Color(1f, 0.86f, 0.62f), Energy = 1.25f, ShadowEnabled = true, ShadowFilter = Light2D.ShadowFilterEnum.Pcf13, ShadowFilterSmooth = 2f, BlendMode = Light2D.BlendModeEnum.Add, Height = 26 };
        lightRoot.AddChild(lantern);
        aura = new PointLight2D { Texture = lightTex, Color = new Color(1f, 0.88f, 0.7f), Energy = 0.8f, ShadowEnabled = true, ShadowFilter = Light2D.ShadowFilterEnum.Pcf5, ShadowFilterSmooth = 2f, Height = 30 };
        lightRoot.AddChild(aura);
        // the region's key light: the moon outdoors (with shadows), a soft fill from the region's light indoors
        keyLight = new DirectionalLight2D { ShadowEnabled = false, ShadowFilter = Light2D.ShadowFilterEnum.Pcf5, ShadowFilterSmooth = 3f, Energy = 0.3f, RangeItemCullMask = 1 | MAREN_BIT };
        lightRoot.AddChild(keyLight);
        lighting.KeyLight = keyLight;

        camera = new Camera2D { AnchorMode = Camera2D.AnchorModeEnum.FixedTopLeft, Enabled = true };
        AddChild(camera);

        // the foreground, out of focus and in front of everything in the world
        fore = DepthLayer(1, foreBake, () => foreArea, FORE_F, foreMat);
        foreMotes = new DrawLayer { Paint = PaintForeMotes };
        fore.GetParent().AddChild(foreMotes);

        // rain, glimpses and the finishing pass sit above the world, below the HUD
        var postLayer = new CanvasLayer { Layer = 5 };
        AddChild(postLayer);
        rain = new DrawLayer { Paint = PaintRain };
        postLayer.AddChild(rain);
        overlay = new DrawLayer { Paint = PaintOverlay };
        postLayer.AddChild(overlay);
        postMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/post.gdshader") };
        lighting.RegisterPost(postMat);
        postRect = new ColorRect { Material = postMat, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(VW, VH) };
        postLayer.AddChild(postRect);

        var hudLayer = new CanvasLayer { Layer = 10 };
        AddChild(hudLayer);
        hud = new DrawLayer { Paint = c => { if (G.mode == "play" && !UI.CinematicOpen) Hud.Draw(c); } };
        hudLayer.AddChild(hud);

        World.Dirty += () => dirty = true;
        G.RoomChanged += () => { dirty = true; roomFade = 1; lighting.OnRoom(G.room); };
    }
    DrawLayer Add(DrawLayer d) { AddChild(d); return d; }
    static void Encoded(Canvas c, Action<Canvas> paint) { if (G.room == null) return; c.Encode = true; paint(c); }
    // A canvas that follows the camera, holding one baked depth drawn with parallax.
    DrawLayer DepthLayer(int layer, SubViewport vp, Func<Rect2> area, float f, ShaderMaterial mat)
    {
        var cl = new CanvasLayer { Layer = layer, FollowViewportEnabled = true };
        AddChild(cl);
        var d = new DrawLayer { TextureFilter = TextureFilterEnum.Linear, Material = mat };
        d.Paint = c =>
        {
            var r = G.room;
            if (r == null || cached != r || bakeWait > 0) return;
            var a = area();
            var o = (G.cam - Strata.CamCentre(r)) * (1 - f);
            c.Fill = Colors.White;
            c.Texture(vp.GetTexture(), a.Position.X + o.X, a.Position.Y + o.Y, a.Size.X, a.Size.Y);
        };
        cl.AddChild(d);
        return d;
    }
    SubViewport MakeBake(out DrawLayer art, Action<Canvas> paint)
    {
        bakes++;
        var vp = new SubViewport { TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear };
        AddChild(vp);
        art = new DrawLayer { Paint = paint, Name = "bake" + bakes };
        vp.AddChild(art);
        return vp;
    }
    void DrawBake(Canvas c, SubViewport vp)
    {
        var r = G.room;
        if (r == null || cached != r || bakeWait > 0) return;
        c.Fill = Colors.White;
        c.Texture(vp.GetTexture(), r.Px, r.Py, r.Pw, r.Ph);
    }
    T Add<T>(T n) where T : Node { AddChild(n); return n; }
    static Rect2 ViewRect() => new(G.cam, new Vector2(VW, VH));

    // ---- per-room cache -------------------------------------------------------------------------
    char At(RoomDef r, int lx, int ly) => (lx < 0 || ly < 0 || lx >= r.Tw || ly >= r.Th) ? World.CharAt(r.Ox + lx, r.Oy + ly) : r.Live[ly * r.Tw + lx];
    bool SolidAt(RoomDef r, int lx, int ly) => TileArt.IsSolidCh(At(r, lx, ly));

    void Rebuild()
    {
        var r = G.room;
        cached = r; dirty = false;
        dynCells.Clear(); windows.Clear(); ceilings.Clear(); tops.Clear(); pillars.Clear();
        var pset = new HashSet<int>();
        for (int ly = 0; ly < r.Th; ly++) for (int lx = 0; lx < r.Tw; lx++)
            {
                char ch = r.Live[ly * r.Tw + lx];
                int tx = r.Ox + lx, ty = r.Oy + ly;
                if (ch == 'w') windows.Add((tx, ty));
                if (TileArt.DYNAMIC.Contains(ch)) dynCells.Add((tx, ty));
                if (ch == '|') pset.Add(tx);
                if (!SolidAt(r, lx, ly) && SolidAt(r, lx, ly - 1) && TileArt.SOLIDS.Contains(At(r, lx, ly - 1))) ceilings.Add((tx, ty));
                if (!SolidAt(r, lx, ly) && SolidAt(r, lx, ly + 1)) tops.Add((tx, ty + 1));
            }
        pillars.AddRange(pset);
        BuildOccluders(r);
        // set dressing is decided once per room, so it does not shuffle when a door opens or a pickup goes
        if (!decorCache.TryGetValue(r.Id, out decor))
            decorCache[r.Id] = decor = Decor.Build(r, (lx, ly) => SolidAt(r, lx, ly), G.ents);
        ambient = new Color(AMBIENT, AMBIENT, AMBIENT);
        // the room's surfaces: its back wall and its rock, each its own material (art/materials)
        var (wallSurface, rockSurface) = Art.SurfacesFor(r);
        Art.ApplySurface(backMat, wallSurface); Art.ApplySurface(solidMat, rockSurface);
        farArea = Strata.Area(r, FAR_F); midArea = Strata.Area(r, MID_F); nearArea = Strata.Area(r, NEAR_F); foreArea = Strata.Area(r, FORE_F);
        var room = new Rect2(r.Px, r.Py, r.Pw, r.Ph);
        foreach (var (vp, art, area, scale) in new[] { (backBake, backArt, room, BAKE), (solidBake, solidArt, room, BAKE), (farBake, farArt, farArea, 1.5f), (midBake, midArt, midArea, 1.5f), (nearBake, nearArt, nearArea, 1f), (foreBake, foreArt, foreArea, 1f) })
        {
            vp.Size = new Vector2I((int)(area.Size.X * scale), (int)(area.Size.Y * scale));
            art.Scale = new Vector2(scale, scale);
            art.Position = -area.Position * scale;
            art.QueueRedraw();
            vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }
        bakeWait = 2;
    }

    // Masonry casts shadows: one occluder per run of exposed solid tiles in each row. The face toward a
    // light stays lit (front faces are culled) and the shadow starts behind the first course of stone.
    void BuildOccluders(RoomDef r)
    {
        foreach (var c in occluders.GetChildren()) c.QueueFree();
        bool Occ(int lx, int ly) { char ch = At(r, lx, ly); return (TileArt.SOLIDS.Contains(ch) && ch != '_') || ch == 'S' || ch == 'C'; }
        bool Exposed(int lx, int ly) => !SolidAt(r, lx - 1, ly) || !SolidAt(r, lx + 1, ly) || !SolidAt(r, lx, ly - 1) || !SolidAt(r, lx, ly + 1);
        for (int ly = 0; ly < r.Th; ly++)
        {
            int run = -1;
            for (int lx = 0; lx <= r.Tw; lx++)
            {
                bool on = lx < r.Tw && Occ(lx, ly) && Exposed(lx, ly);
                if (on && run < 0) run = lx;
                if (!on && run >= 0)
                {
                    float x0 = r.Px + run * TS, x1 = r.Px + lx * TS, y0 = r.Py + ly * TS, y1 = y0 + TS;
                    var poly = new OccluderPolygon2D { Polygon = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }, CullMode = OccluderPolygon2D.CullModeEnum.CounterClockwise, Closed = true };
                    occluders.AddChild(new LightOccluder2D { Occluder = poly });
                    run = -1;
                }
            }
        }
    }

    // ---- frame ----------------------------------------------------------------------------------------
    public override void _Process(double delta)
    {
        if (G.room == null) return;
        if (dirty || cached != G.room) Rebuild();
        float dt = (float)delta;
        if (bakeWait > 0) bakeWait--;
        UpdateAtmosphere(dt);
        var p = G.player;
        float sx = Settings.Shake ? G.shakeX : 0, sy = Settings.Shake ? G.shakeY : 0;
        camera.Position = G.cam + new Vector2(sx, sy);
        UpdateLights(p);
        UpdatePost(p, dt);
        UpdatePalettes(dt);
        lighting.Step(G.stepDt, G.room, tideW, lightningFlash);   // game time, so a test's fixed step and a real frame blend alike
        modulate.Color = lighting.Ambient;
        // depth of field on the planes, with any focus pull StoryFocus asks for
        farMat.SetShaderParameter("blur", lighting.FarBlur);
        midMat.SetShaderParameter("blur", lighting.MidBlur);
        foreMat.SetShaderParameter("blur", lighting.ForeBlur);
        // the foreground opens a window around Maren rather than hide her
        foreMat.SetShaderParameter("cut_pos", new Vector2((p.Cx() - G.cam.X) / VW, (p.Cy() - G.cam.Y) / VH));
        foreMat.SetShaderParameter("cut_r", p.dead ? 0f : 0.16f);
        foreach (var l in new[] { sky, far, midL, nearL, back, deco, entities, maren, water, waterLines, solid, haze, dyn, fxShaded, fxGlow, fxText, fore, foreMotes, rain, overlay, hud }) l.QueueRedraw();
        if (sky.Name != "sky") { sky.Name = "sky"; far.Name = "far"; midL.Name = "mid"; nearL.Name = "near"; back.Name = "back"; deco.Name = "deco"; entities.Name = "entities"; maren.Name = "maren"; water.Name = "water"; waterLines.Name = "waterLines"; solid.Name = "solid"; dyn.Name = "dyn"; fxShaded.Name = "fxShaded"; fxGlow.Name = "fxGlow"; fxText.Name = "fxText"; fore.Name = "fore"; foreMotes.Name = "foreMotes"; rain.Name = "rain"; overlay.Name = "overlay"; hud.Name = "hud"; }
    }

    // Colours every encoded layer from the region's palette, drifting with the tide.
    float tideW;
    void UpdatePalettes(float dt)
    {
        var r = G.room;
        tideW = Palettes.TideW(dt);
        float lit = 1 / AMBIENT;
        // a painted vista carries its own aerial perspective, so it takes less of the fog
        Palettes.Apply(farMat, r, tideW, Strata.Vista(r) != null ? 0.2f : 0.42f, 0.3f, 0.03f, 1, r.Outdoor ? 0 : 0.5f);
        Palettes.Apply(midMat, r, tideW, 0.22f, 0.35f, 0.025f, 1, r.Outdoor ? 0 : 0.35f);
        Palettes.Apply(nearMat, r, tideW, 0.05f, 0.14f, 0.015f);
        Palettes.Apply(backMat, r, tideW, 0.12f, 0.25f, 0.015f, lit);
        Palettes.Apply(decoMat, r, tideW, 0.08f, 0.2f, 0, lit);
        Palettes.Apply(solidMat, r, tideW, 0, 0.12f, 0, lit);
        Palettes.Apply(dynMat, r, tideW, 0, 0.1f, 0, lit);
        Palettes.Apply(foreMat, r, tideW, 0, 0, 0);
    }

    void UpdateLights(Player p)
    {
        bool lit = p.LanternLit() && !p.dead;
        lantern.Visible = lit;
        if (lit)
        {
            float t = G.Now;
            float flick = 1 + MathF.Sin(t * 17) * 0.02f + MathF.Sin(t * 41) * 0.012f;
            float range = (p.focusing ? 260 : 160) * flick;
            lantern.Texture = p.focusing ? coneFocus : coneWide;
            lantern.TextureScale = range / 256f;
            lantern.Position = p.LanternPos();
            lantern.Rotation = p.aim;
            lantern.Energy = (p.focusing ? 1.9f : 1.5f) * flick;
        }
        aura.Visible = !p.dead;
        aura.Position = new Vector2(p.Cx(), p.Cy());
        aura.TextureScale = (lit ? 52 : 28) / 128f;
        aura.Energy = lit ? 1.1f : 0.55f;

        // every other light this frame: lamps, bells, flares, glowing collectibles, fire
        var srcs = new List<LightSrc>();
        foreach (var l in G.frameLights) if (!l.player) srcs.Add(l);
        foreach (var f in Fire.cells.Values) srcs.Add(new LightSrc { x = f.tx * TS + 8, y = f.ty * TS + 8, r = 60, i = 0.8f, color = "255,140,50" });
        // the room's own candles and sconces, nearest the middle of the view first
        var mid = G.cam + new Vector2(VW / 2f, VH / 2f);
        srcs.AddRange(Decor.Lights(decor, G.Now, ViewRect()).OrderBy(l => (new Vector2(l.x, l.y) - mid).LengthSquared()).Take(10));
        while (pool.Count < srcs.Count)
        {
            var pl = new PointLight2D { Texture = lightTex, ShadowFilter = Light2D.ShadowFilterEnum.Pcf5, ShadowFilterSmooth = 2f, Height = 34, RangeItemCullMask = 1 | MAREN_BIT };
            lightRoot.AddChild(pl); pool.Add(pl);
        }
        // the same lights, and the windows, feed the radiance buffer the world's bounce light comes from
        lighting.Sources = srcs;
        lighting.LanternOn = lit; lighting.LanternPos = p.LanternPos(); lighting.LanternAim = p.aim; lighting.LanternRange = p.focusing ? 260 : 160; lighting.Focusing = p.focusing;
        lighting.Windows.Clear();
        if (!G.room.Outdoor)
            foreach (var (tx, ty) in windows)
            {
                float wx = tx * TS + 8, wy = ty * TS + 8;
                if (wx < G.cam.X - 200 || wx > G.cam.X + VW + 200 || wy < G.cam.Y - 200 || wy > G.cam.Y + VH + 200) continue;
                lighting.Windows.Add((wx, wy, WINDOW_HUES[(int)(TileArt.Hash(tx, ty) * 4) % 4]));
            }
        for (int i = 0; i < pool.Count; i++)
        {
            var pl = pool[i];
            if (i >= srcs.Count) { pl.Visible = false; continue; }
            var l = srcs[i];
            pl.Visible = true;
            pl.Position = new Vector2(l.x, l.y);
            pl.TextureScale = l.r * 1.25f / 128f;
            var col = ParseRgb(l.color ?? "255,200,120");
            pl.Color = col.Lerp(Colors.White, 0.35f);
            pl.Energy = l.i * 1.4f;
            pl.ShadowEnabled = l.r >= 60;
        }
    }
    static readonly Color[] WINDOW_HUES = { H.Rgba(255, 160, 140), H.Rgba(150, 180, 255), H.Rgba(255, 225, 150), H.Rgba(150, 240, 200) };
    static readonly Dictionary<string, Color> rgbCache = new();
    static Color ParseRgb(string s)
    {
        if (rgbCache.TryGetValue(s, out var c)) return c;
        var p = s.Split(',');
        c = new Color(float.Parse(p[0]) / 255f, float.Parse(p[1]) / 255f, float.Parse(p[2]) / 255f);
        rgbCache[s] = c;
        return c;
    }

    void UpdatePost(Player p, float dt)
    {
        postRect.Size = GetViewport().GetVisibleRect().Size;
        bool under = p.HeadUnder() && !p.dead;
        underSmooth = H.Approach(underSmooth, under ? 1 : 0, dt * 3);
        var r = G.room;
        postMat.SetShaderParameter("under", underSmooth);
        postMat.SetShaderParameter("tint", r.Brine ? new Color(120 / 255f, 140 / 255f, 120 / 255f, 0.18f * underSmooth) : new Color(20 / 255f, 60 / 255f, 80 / 255f, 0.28f * underSmooth));
        Color flash = new(0, 0, 0, 0);
        if (G.flash > 0) flash = new Color(160 / 255f, 20 / 255f, 20 / 255f, MathF.Min(0.8f, G.flash));
        else if (lightningFlash > 0 && r.Outdoor) flash = new Color(210 / 255f, 225 / 255f, 240 / 255f, lightningFlash * 0.16f);
        postMat.SetShaderParameter("flash", flash);
        postMat.SetShaderParameter("pressure", G.pressureWarn);
    }

    // ---- atmosphere: rain, lightning, motes, drips, bubbles ------------------------------------------
    (int x, int y)? PickVisible(List<(int x, int y)> list)
    {
        for (int i = 0; i < 4 && list.Count > 0; i++)
        {
            var q = H.Pick(list); float x = q.x * TS, y = q.y * TS;
            if (x > G.cam.X - TS && x < G.cam.X + VW && y > G.cam.Y - TS && y < G.cam.Y + VH) return q;
        }
        return null;
    }
    void UpdateAtmosphere(float dt)
    {
        var r = G.room; var cam = G.cam;
        if (r.Outdoor)
        {
            int want0 = 120 + (int)(170 * tideW);
            if (drops.Count > want0) drops.RemoveRange(want0, drops.Count - want0);
            while (drops.Count < want0) drops.Add(new Drop { x = H.Rand() * (VW + 120) - 60, y = H.Rand() * VH, v = 300 + H.Rand() * 160, l = 6 + H.Rand() * 8 });
            foreach (var d in drops) { d.y += d.v * dt; d.x -= d.v * 0.28f * dt; if (d.y > VH || d.x < -40) { d.y = -10; d.x = H.Rand() * (VW + 120); } }
            var top = tops.Count > 0 && H.Rand() < dt * 40 ? PickVisible(tops) : null;
            if (top != null)
            {
                float x = top.Value.x * TS + H.Rand() * TS, y = top.Value.y * TS;
                if (!World.SolidPx(x, y - 4)) FX.Spawn(x, y, 2, new FXOpt { color = "rgba(170,200,215,0.7)", ang = -H.PI / 2, spread = 0.9f, smin = 20, smax = 50, grav = 300, lmin = 0.15f, lmax = 0.3f, size = 1 });
            }
            lightningNext -= dt;
            if (lightningNext <= 0)
            {
                lightningNext = (14 + H.Rand() * 22) * (1 - tideW * 0.6f); lightningFlash = 1; lightningSecond = 0.18f;
                G.After(0.7f + H.Rand() * 1.2f, Snd.Rumble);
            }
        }
        else drops.Clear();
        if (lightningSecond >= 0) { lightningSecond -= dt; if (lightningSecond < 0) lightningFlash = MathF.Max(lightningFlash, 0.7f); }
        lightningFlash = MathF.Max(0, lightningFlash - dt * 2.6f);

        string reg = r.Region;
        int want = r.Outdoor ? 0 : 52;
        if (motes.Count > want) motes.RemoveRange(want, motes.Count - want);
        while (motes.Count < want) motes.Add(SpawnMote(true));
        float t = G.Now;
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var m = motes[i];
            m.life -= dt;
            if (m.kind == "drip")
            {
                m.vy += 500 * dt;
                if (World.SolidPx(m.x, m.y + 2) || World.IsWater(m.x, m.y + 2)) { if (H.Rand() < 0.35f) Snd.Drip(m.x, m.y); FX.Spawn(m.x, m.y, 3, new FXOpt { color = "rgba(160,200,215,0.7)", ang = -H.PI / 2, spread = 1, smin = 15, smax = 45, grav = 300, lmin = 0.2f, lmax = 0.4f, size = 1 }); m.life = 0; }
            }
            else if (m.kind == "bubble") { m.vy = -18; m.vx = MathF.Sin(t * 3 + m.p) * 6; if (!World.IsWater(m.x, m.y)) m.life = 0; }
            else m.vx += MathF.Sin(t * 0.7f + m.p) * 2 * dt;
            m.x += m.vx * dt; m.y += m.vy * dt;
            bool off = m.x < cam.X - 40 || m.x > cam.X + VW + 40 || m.y < cam.Y - 40 || m.y > cam.Y + VH + 40;
            if (m.life <= 0 || off) motes[i] = SpawnMote(false);
        }
    }
    Mote SpawnMote(bool initial)
    {
        var cam = G.cam; string reg = G.room.Region;
        float x = cam.X + H.Rand() * VW, y = cam.Y + H.Rand() * VH;
        float r = H.Rand();
        var ceil = (reg is "wrackline" or "cloister" or "choir" or "gullet") && r < 0.12f + 0.2f * tideW ? PickVisible(ceilings) : null;
        if (ceil != null) return new Mote { kind = "drip", x = ceil.Value.x * TS + 4 + H.Rand() * 8, y = ceil.Value.y * TS + 1, life = 4 };
        if (World.IsWater(x, y)) return new Mote { kind = "bubble", x = x, y = y, vy = -18, life = 3 + H.Rand() * 3, p = H.Rand() * 6 };
        if (reg == "saltworks" && r < 0.6f) return new Mote { kind = "salt", x = x, y = initial ? y : cam.Y - 5, vx = 4, vy = 9 + H.Rand() * 6, life = 12, p = H.Rand() * 6 };
        if (reg is "ribvault" or "skull" && r < 0.25f) return new Mote { kind = "ember", x = x, y = y, vx = (H.Rand() - 0.5f) * 4, vy = -4 - H.Rand() * 4, life = 5 + H.Rand() * 5, p = H.Rand() * 6 };
        return new Mote { kind = "dust", x = x, y = y, vx = (H.Rand() - 0.5f) * 4, vy = (H.Rand() - 0.5f) * 3, life = 6 + H.Rand() * 8, p = H.Rand() * 6 };
    }

    // ---- painters -------------------------------------------------------------------------------------
    void PaintSky(Canvas c)
    {
        var r = G.room; var cam = G.cam; float t = G.Now;
        if (r == null) return;
        if (!r.Outdoor)
        {
            var fog = Palettes.For(r).fog;
            c.FillRectGradV(0, 0, VW, VH, fog, fog * 0.8f);
            return;
        }
        Backdrop.Outdoor(c, cam, t, lightningFlash);
    }

    void PaintBack(Canvas c)
    {
        var r = G.room; if (r == null || cached != r) return;
        c.Encode = true;
        Strata.PaintBackWall(c, r, (lx, ly) => SolidAt(r, lx, ly), decor);
        c.EncGain = 1.5f;
        Decor.PaintArchitecture(c, r, (lx, ly) => SolidAt(r, lx, ly));
        for (int ly = 0; ly < r.Th; ly++) for (int lx = 0; lx < r.Tw; lx++)
            {
                char ch = r.Live[ly * r.Tw + lx];
                float x = r.Px + lx * TS, y = r.Py + ly * TS; int tx = r.Ox + lx, ty = r.Oy + ly;
                if (ch == 'w') Window(c, x, y, tx, ty);
                else if (TileArt.DECOR.Contains(ch)) TileArt.Decor(c, ch, x, y, tx, ty);
            }
        c.EncGain = 1.4f;
        Decor.PaintBackProps(c, r, decor);
        // the room's monument, if something happened here (StoryFocus finds it by the same placement)
        Monuments.Paint(c, r);
        // tide marks on pillars (static per room; drawn with the back wall)
        if (r.Tidal && r.FixedWater == null)
        {
            float lo = (Tide.SEA_LOW - Tide.spring) * TS, hi = (Tide.SEA_HIGH - Tide.spring) * TS;
            c.Font = "6px monospace";
            foreach (int tx in pillars)
                for (int i = 0; i < 4; i++)
                {
                    float y = H.Lerp(lo, hi, i / 3f);
                    c.Fill = Canvas.VA(0.7f, 0.5f, 0.8f); c.FillRect(tx * TS + 3, y - 0.5f, TS - 6, 1.2f);
                    c.Fill = Canvas.VA(0.7f, 0.5f, 0.7f); c.FillText(new[] { "I", "II", "III", "IV" }[i], tx * TS + 4, y - 2);
                }
        }
    }
    // A stained-glass lancet in the back wall, lit from outside.
    static void Window(Canvas c, float x, float y, int tx, int ty)
    {
        c.Fill = Canvas.Gl(0.4f, 0.9f); c.FillRect(x + 3, y, TS - 6, TS);
        c.Fill = Canvas.VA(0.85f, 0.4f + TileArt.Hash(tx, ty) * 0.4f, 0.45f); c.FillRect(x + 3, y, TS - 6, TS);
        c.Fill = Canvas.V(0.08f); c.FillRect(x + 7.2f, y, 1.6f, TS); c.FillRect(x + 3, y + 7, TS - 6, 1.4f);
        c.FillRect(x + 2, y, 1.4f, TS); c.FillRect(x + TS - 3.4f, y, 1.4f, TS);
    }

    void PaintSolid(Canvas c)
    {
        var r = G.room; if (r == null || cached != r) return;
        var pal = TileArt.PAL[r.Region];
        bool SA(int lx, int ly) => SolidAt(r, lx, ly);
        c.Encode = true;
        Terrain.Paint(c, r, (x, y) => At(r, x, y));
        // timber and bone keep their own drawing, coloured by the palette like the rest
        c.EncGain = 1.4f;
        for (int ly = 0; ly < r.Th; ly++) for (int lx = 0; lx < r.Tw; lx++)
            {
                char ch = r.Live[ly * r.Tw + lx];
                if (!TileArt.SOLIDS.Contains(ch) || Terrain.IsMass(ch)) continue;
                c.EncGain = ch == 'R' ? 1.4f : 0.95f;
                TileArt.SolidTile(c, ch, r.Px + lx * TS, r.Py + ly * TS, r.Ox + lx, r.Oy + ly, r, pal, SA, lx, ly);
            }
    }

    void PaintDynamic(Canvas c)
    {
        var cam = G.cam;
        foreach (var (tx, ty) in dynCells)
        {
            float x = tx * TS, y = ty * TS;
            if (x + TS < cam.X || x > cam.X + VW || y + TS < cam.Y || y > cam.Y + VH) continue;
            TileArt.Dynamic(c, World.CharAt(tx, ty), x, y, tx, ty, G.Now);
        }
    }

    void PaintEntities(Canvas c)
    {
        if (G.room == null) return;
        foreach (var e in G.ents) if (!e.isBody) e.Draw(c);
        foreach (var s in G.sawlines) s.Draw(c);
        foreach (var e in G.ents)
            if (e.isBody && !e.gone)
            {
                c.Flash = e is Body b && b.hitFlash > 0 ? MathF.Min(1, b.hitFlash / 0.08f) * 0.85f : 0;
                var mark = c.Mark();
                e.Draw(c);
                if (silhouettes && OnScreen(e)) Silhouette(c, mark, e.Cx(), e.Cy());
            }
        c.Flash = 0;
        Gore.DrawPieces(c);
        Hollow.DrawWorld(c);
        Legend.DrawWorld(c);
        foreach (var pr in G.projectiles) pr.Draw(c);
        foreach (var s in G.enemyShots)
        {
            // the Almoner's alms-bowls tumble; everything else streaks
            if (s.kind == "bowl") { c.FillStyle = "#6a6a66"; c.BeginPath(); c.Ellipse(s.x, s.y, 4, 2.2f, s.life * 9, 0, H.PI * 2); c.FillPath(); c.Fill = H.Rgba(220, 220, 210, 0.4f); c.FillCircle(s.x - 1, s.y - 1, 1); continue; }
            c.StrokeStyle = "#a0b0b8"; c.LineWidth = 1.5f; c.Line(s.x, s.y, s.x - s.vx * 0.02f, s.y - s.vy * 0.02f);
        }
        foreach (var m in motes)
        {
            switch (m.kind)
            {
                case "drip": c.Fill = H.Rgba(170, 210, 225, 0.8f); c.FillRect(m.x, m.y, 1, 2.5f); break;
                case "bubble": c.Stroke = H.Rgba(190, 225, 240, 0.5f); c.LineWidth = 0.6f; c.BeginPath(); c.Arc(m.x, m.y, 1.4f, 0, H.PI * 2); c.StrokePath(); break;
                case "salt": c.Fill = H.Rgba(240, 242, 246, 0.6f); c.FillRect(m.x, m.y, 1, 1); break;
                case "ember": break;
                default: c.Fill = H.Rgba(220, 210, 180, 0.35f); c.FillRect(m.x, m.y, 1, 1); break;
            }
        }
    }

    void PaintMaren(Canvas c)
    {
        if (G.room == null || G.player == null) return;
        var pm = c.Mark();
        G.player.Draw(c);
        if (silhouettes && !G.player.dead) Silhouette(c, pm, G.player.Cx(), G.player.Cy());
    }

    // Characters stand off the world the way they do in Hollow Knight: a crisp dark edge on the side away from the
    // light, and a rim in the nearest light's colour along the side that faces it. Both are the character itself,
    // drawn in one colour a fraction of a pixel to either side, behind the real drawing.
    void Silhouette(Canvas c, (int, int, int) mark, float cx, float cy)
    {
        var (dir, col, k) = RimLight(cx, cy);
        var ink = (new Vector2(-dir.X * 0.85f, -dir.Y * 0.85f + 0.25f), new Color(0.015f, 0.015f, 0.02f, 1));
        if (k <= 0.12f) { c.EchoBehind(mark, new[] { ink }); return; }
        float g = MathF.Min(1, 0.3f + k);
        var rim = (dir * 0.75f, new Color(MathF.Min(1, col.R * g), MathF.Min(1, col.G * g), MathF.Min(1, col.B * g), 1));
        c.EchoBehind(mark, new[] { ink, rim });
    }
    // the light a character's rim comes from: the strongest lamp near it, or the region's key light
    (Vector2 dir, Color col, float k) RimLight(float x, float y)
    {
        LightSrc best = null; float bw = 0;
        foreach (var l in lighting.Sources)
        {
            float d = H.Dist(x, y, l.x, l.y);
            float w = l.i * Math.Clamp(1 - d / (l.r * 3 + 40), 0, 1);
            if (w > bw && d > 2) { bw = w; best = l; }
        }
        var m = lighting.Cur;
        if (best != null && bw > m.keyEnergy * 0.5f)
            return (new Vector2(best.x - x, best.y - y).Normalized(), best.color == null ? new Color(1, 0.8f, 0.55f) : ParseRgb(best.color), Math.Clamp(bw * 1.1f, 0, 0.9f));
        return (new Vector2(-MathF.Sin(m.keyAngle), -MathF.Cos(m.keyAngle)), m.key, m.keyEnergy * 0.9f);
    }
    static bool OnScreen(Ent e) => e.x + e.w > G.cam.X - 40 && e.x < G.cam.X + VW + 40 && e.y + e.h > G.cam.Y - 60 && e.y < G.cam.Y + VH + 40;

    // Water spans across the visible part of the room, filled with the water shader.
    void PaintWater(Canvas c)
    {
        var r = G.room; if (r == null) return;
        var cam = G.cam; float t = G.Now;
        float x0 = MathF.Max(r.Px, cam.X - 4), x1 = MathF.Min(r.Px + r.Pw, cam.X + VW + 4);
        float bottom = MathF.Min(r.Py + r.Ph, cam.Y + VH + 4);
        float top = float.PositiveInfinity;
        var span = new List<Vector2>();
        void Flush()
        {
            if (span.Count >= 2)
            {
                var pts = new List<Vector2>(span) { new(span[^1].X, bottom), new(span[0].X, bottom) };
                // the span's surface, in screen UV, carried to the shader in the vertex colour for its reflection
                float sy = float.PositiveInfinity; foreach (var q in span) sy = MathF.Min(sy, q.Y);
                float enc = Math.Clamp(((sy - cam.Y) / VH + 1) / 3, 0, 1);
                float hi = MathF.Floor(enc * 255) / 255, lo = enc * 255 - MathF.Floor(enc * 255);
                c.Fill = new Color(hi, lo, 0, 1); c.Poly(pts.ToArray());
            }
            span.Clear();
        }
        for (float x = x0; x <= x1 + 3; x += 4)
        {
            float s = World.RoomSurface(r, x);
            if (!float.IsFinite(s) || s >= bottom) { Flush(); continue; }
            float y = s + MathF.Sin(x * 0.05f + t * 2) * 0.8f + MathF.Sin(x * 0.13f - t * 1.3f) * 0.4f;
            if (span.Count > 0 && MathF.Abs(span[^1].Y - y) > 12) { span.Add(new Vector2(x - 0.01f, span[^1].Y)); Flush(); }
            span.Add(new Vector2(x, y));
            top = MathF.Min(top, y);
        }
        Flush();
        if (!float.IsFinite(top)) return;
        if (r.Brine) { waterMat.SetShaderParameter("top_col", H.Rgba(190, 205, 190, 0.38f)); waterMat.SetShaderParameter("deep_col", H.Rgba(110, 130, 115, 0.62f)); waterMat.SetShaderParameter("caustic_col", new Vector3(0.9f, 0.95f, 0.85f)); }
        else
        {
            var pal = Palettes.For(r);
            var wt = pal.waterTop.Lerp(pal.waterDeep, tideW * 0.3f); wt.A = r.Deep ? 0.5f : 0.4f;
            var wd = pal.waterDeep; wd.A = r.Deep ? 0.85f : 0.72f;
            waterMat.SetShaderParameter("top_col", wt); waterMat.SetShaderParameter("deep_col", wd);
            waterMat.SetShaderParameter("caustic_col", new Vector3(pal.glow.R, pal.glow.G, pal.glow.B) * 0.75f);
        }
        waterMat.SetShaderParameter("top_y", top);
        waterMat.SetShaderParameter("ambient_mod", new Vector3(lighting.Ambient.R, lighting.Ambient.G, lighting.Ambient.B));
        waterMat.SetShaderParameter("reflect_amt", r.Brine ? 0.3f : r.Deep ? 0.45f : 0.72f);
    }

    void PaintWaterLines(Canvas c)
    {
        var r = G.room; if (r == null) return;
        var cam = G.cam; float t = G.Now;
        float x0 = MathF.Max(r.Px, cam.X - 4), x1 = MathF.Min(r.Px + r.Pw, cam.X + VW + 4);
        float bottom = MathF.Min(r.Py + r.Ph, cam.Y + VH + 4);
        c.Stroke = r.Brine ? H.Rgba(235, 245, 235, 0.6f) : H.Rgba(150, 210, 228, 0.55f); c.LineWidth = 1;
        var line = new List<Vector2>();
        void FlushLine() { if (line.Count >= 2) { c.BeginPath(); c.MoveTo(line[0].X, line[0].Y); for (int i = 1; i < line.Count; i++) c.LineTo(line[i].X, line[i].Y); c.StrokePath(); } line.Clear(); }
        int k = 0;
        for (float x = x0; x <= x1 + 3; x += 4, k++)
        {
            float s = World.RoomSurface(r, x);
            if (!float.IsFinite(s) || s >= bottom) { FlushLine(); continue; }
            float y = s + MathF.Sin(x * 0.05f + t * 2) * 0.8f + MathF.Sin(x * 0.13f - t * 1.3f) * 0.4f;
            if (line.Count > 0 && MathF.Abs(line[^1].Y - y) > 12) FlushLine();
            line.Add(new Vector2(x, y));
            if (k % 3 == 0 && MathF.Sin(x * 0.3f + t * 3.1f) > 0.75f) { c.Fill = H.Rgba(220, 240, 250, 0.5f); c.FillRect(x, y - 0.5f, 3, 1); }
        }
        FlushLine();
        // Ebb currents
        if (Tide.PhaseId() == "ebbing" && r.Seaward != 0)
        {
            c.Stroke = H.Rgba(160, 210, 230, 0.2f);
            for (int i = 0; i < 14; i++)
            {
                float px = ((i * 97 + t * 60 * r.Seaward) % r.Pw + r.Pw) % r.Pw + r.Px;
                float s = World.RoomSurface(r, px);
                if (!float.IsFinite(s)) continue;
                float py = s + 10 + (i * 37) % MathF.Max(10, r.Py + r.Ph - s - 20);
                c.Line(px, py, px + 12 * r.Seaward, py);
            }
        }
    }

    void PaintFxShaded(Canvas c)
    {
        // blood soaks into the stone's surface, so it is drawn over the rock
        Gore.DrawStains(c);
        foreach (var q in FX.parts)
        {
            if (q.glow) continue;
            var col = q.color; col.A *= MathF.Max(0, q.life / q.max);
            c.Fill = col; c.FillRect(q.x - q.size / 2, q.y - q.size / 2, q.size, q.size);
        }
    }

    // Additive, unshaded: anything that gives off light. Values above 1 bloom.
    void PaintFxGlow(Canvas c)
    {
        var r = G.room; if (r == null) return;
        var p = G.player; float t = G.Now; var cam = G.cam;
        // soft halos around every light source
        foreach (var l in G.frameLights)
        {
            if (l.player) continue;
            var col = ParseRgb(l.color ?? "255,200,120");
            c.Glow(l.x, l.y, l.r * 0.9f, new Color(col.R, col.G, col.B, 0.15f * l.i));
            c.Glow(l.x, l.y, 5, new Color(col.R * 1.6f, col.G * 1.6f, col.B * 1.6f, 0.9f * l.i));
        }
        Decor.PaintGlow(c, decor, t, ViewRect());
        // light shafts through stained glass
        if (!r.Outdoor)
        {
            foreach (var (tx, ty) in windows)
            {
                float x = tx * TS, y = ty * TS;
                if (x < cam.X - 160 || x > cam.X + VW + 40 || y < cam.Y - 200 || y > cam.Y + VH + 20) continue;
                var hc = WINDOW_HUES[(int)(TileArt.Hash(tx, ty) * 4) % 4];
                float a = 0.09f + 0.03f * MathF.Sin(t * 0.6f + tx);
                var top = new Color(hc.R, hc.G, hc.B, a); var bot = new Color(hc.R, hc.G, hc.B, 0);
                c.FillPolyGrad(new[] { new Vector2(x + 2, y), new Vector2(x + 14, y), new Vector2(x + 70, y + 160), new Vector2(x + 40, y + 160) }, new[] { top, top, bot, bot });
                c.Glow(x + 8, y + 8, 10, new Color(hc.R, hc.G, hc.B, 0.25f));
            }
        }
        // fire
        foreach (var f in Fire.cells.Values)
        {
            float fx = f.tx * TS, fy = f.ty * TS;
            float fl = MathF.Sin(t * 20 + f.tx) * 2, fl2 = MathF.Sin(t * 27 + f.tx * 3) * 1.5f;
            c.Fill = new Color(0.8f, 0.24f, 0.08f, 0.6f); c.BeginPath(); c.MoveTo(fx, fy + TS); c.QuadraticCurveTo(fx + 3, fy + 6, fx + 8, fy + fl); c.QuadraticCurveTo(fx + 13, fy + 6, fx + 16, fy + TS); c.FillPath();
            c.Fill = new Color(1.3f, 0.7f, 0.2f, 0.8f); c.BeginPath(); c.MoveTo(fx + 3, fy + TS); c.QuadraticCurveTo(fx + 5, fy + 9, fx + 8, fy + 4 + fl2); c.QuadraticCurveTo(fx + 11, fy + 9, fx + 13, fy + TS); c.FillPath();
            c.Fill = new Color(1.6f, 1.4f, 0.9f, 0.9f); c.BeginPath(); c.MoveTo(fx + 6, fy + TS); c.LineTo(fx + 8, fy + 10 - fl); c.LineTo(fx + 10, fy + TS); c.FillPath();
        }
        // the lantern itself, and the warm wash of its beam, stopped by walls
        if (p.LanternLit() && !p.dead)
        {
            float range = p.focusing ? 260 : 160, spread = p.focusing ? 0.24f : 0.7f;
            var o = p.LanternPos();
            const int n = 30;
            var pts = new Vector2[n + 2]; var cols = new Color[n + 2];
            pts[0] = o; cols[0] = new Color(1f, 0.78f, 0.47f, p.focusing ? 0.22f : 0.18f);
            for (int i = 0; i <= n; i++)
            {
                float a = p.aim - spread + spread * 2 * i / n;
                float d = Phys.Ray(o.X, o.Y, a, range);
                pts[i + 1] = new Vector2(o.X + MathF.Cos(a) * (d + 4), o.Y + MathF.Sin(a) * (d + 4));
                cols[i + 1] = new Color(1f, 0.78f, 0.47f, 0.18f * (1 - d / range) * (p.focusing ? 1.2f : 1));
            }
            c.FillPolyGrad(pts, cols);
            var lp = p.LanternPos();
            c.Glow(lp.X, lp.Y + 1.5f, p.focusing ? 9 : 7, new Color(1.6f, 1.25f, 0.7f, 0.9f));
            if (G.muzzle > 0) c.Glow(p.Cx() + MathF.Cos(p.aim) * 14, p.y + 9 + MathF.Sin(p.aim) * 14, 12, new Color(2f, 1.6f, 1f, G.muzzle * 12));
        }
        // knots: they glint, they do not reflect
        foreach (var e in G.ents) if (e.isBody && !e.gone) e.DrawKnots(c);
        foreach (var tr in G.tracers) { c.Stroke = new Color(1.6f, 1.4f, 1.1f, tr.t * 10); c.LineWidth = 1; c.Line(tr.x1, tr.y1, tr.x2, tr.y2); }
        foreach (var q in FX.parts)
        {
            if (!q.glow) continue;
            float a = MathF.Max(0, q.life / q.max);
            var col = q.color;
            c.Fill = new Color(col.R * 1.5f, col.G * 1.5f, col.B * 1.5f, col.A * a);
            c.FillRect(q.x - q.size / 2, q.y - q.size / 2, q.size, q.size);
        }
        foreach (var m in motes) if (m.kind == "ember") { c.Fill = new Color(1.4f, 0.6f, 0.2f, 0.5f + 0.3f * MathF.Sin(t * 5 + m.p)); c.FillRect(m.x, m.y, 1, 1); }
        if (p.beamPath != null && p.beamPath.Count > 1)
        {
            c.Stroke = new Color(1.3f, 1.2f, 0.9f, 0.3f); c.LineWidth = 3;
            c.BeginPath(); c.MoveTo(p.beamPath[0].X, p.beamPath[0].Y);
            foreach (var q in p.beamPath) c.LineTo(q.X, q.Y);
            c.StrokePath();
            c.Stroke = new Color(1.6f, 1.5f, 1.2f, 0.35f); c.LineWidth = 1;
            c.BeginPath(); c.MoveTo(p.beamPath[0].X, p.beamPath[0].Y);
            foreach (var q in p.beamPath) c.LineTo(q.X, q.Y);
            c.StrokePath();
        }
    }

    void PaintFxText(Canvas c)
    {
        Narr.DrawWorld(c);
        foreach (var n in G.noiseRings) { c.Stroke = H.Rgba(200, 200, 180, n.t * 0.12f); c.LineWidth = 1; c.BeginPath(); c.Arc(n.x, n.y, n.r * (1 - n.t), 0, H.PI * 2); c.StrokePath(); }
    }

    // Soft specks drifting close to the lens, the way Hollow Knight fills its air.
    readonly (float x, float y, float s, float p)[] foreSpecks = Enumerable.Range(0, 14).Select(i => (TileArt.Hash(i, 71) * 760, TileArt.Hash(i, 72) * 420, 1.5f + TileArt.Hash(i, 73) * 3, TileArt.Hash(i, 74) * 6)).ToArray();
    void PaintForeMotes(Canvas c)
    {
        var r = G.room; if (r == null || r.Outdoor) return;
        float t = G.Now; var cam = G.cam;
        var glow = Palettes.For(r).glow;
        foreach (var m in foreSpecks)
        {
            float x = ((m.x + t * 6 * (0.5f + m.p / 6) - cam.X * 1.3f) % 760 + 760) % 760 - 60 + cam.X;
            float y = ((m.y - t * 3 - cam.Y * 1.3f + MathF.Sin(t * 0.4f + m.p) * 10) % 420 + 420) % 420 - 30 + cam.Y;
            c.Glow(x, y, m.s * 2.4f, new Color(glow.R, glow.G, glow.B, 0.07f + 0.04f * MathF.Sin(t + m.p)));
        }
    }

    void PaintRain(Canvas c)
    {
        if (drops.Count == 0) return;
        c.Stroke = H.Rgba(170, 195, 210, 0.28f); c.LineWidth = 1;
        c.BeginPath();
        foreach (var d in drops) { c.MoveTo(d.x, d.y); c.LineTo(d.x + d.l * 0.28f, d.y - d.l); }
        c.StrokePath();
    }

    static void DrawFaceplateCracks(Canvas c, float k)
    {
        var rng = new Random(7);
        foreach (var (sx, sy, ang) in new[] { (0f, 30f, 0.2f), (VW, 300f, H.PI - 0.3f), (60f, VH, -1.2f), (VW - 40f, 0f, 1.9f), (0f, 250f, -0.4f), (VW, 70f, H.PI + 0.35f) }.Take(k >= 1 ? 6 : 3))
        {
            float x = sx, y = sy, a = ang;
            int segs = k >= 1 ? 9 : 6;
            for (int i = 0; i < segs; i++)
            {
                float len = 10 + (float)rng.NextDouble() * 16, na = a + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                float nx = x + MathF.Cos(na) * len, ny = y + MathF.Sin(na) * len;
                c.Stroke = new Color(0.85f, 0.92f, 0.95f, 0.42f * k * (1 - i / (float)segs * 0.6f)); c.LineWidth = 0.8f; c.Line(x, y, nx, ny);
                if (rng.NextDouble() < 0.4)
                {
                    float ba = na + ((float)rng.NextDouble() < 0.5f ? 0.8f : -0.8f), bl = len * 0.6f;
                    c.Stroke = new Color(0.85f, 0.92f, 0.95f, 0.28f * k); c.LineWidth = 0.5f; c.Line(nx, ny, nx + MathF.Cos(ba) * bl, ny + MathF.Sin(ba) * bl);
                }
                x = nx; y = ny; a = na;
            }
        }
    }

    float roomFade;
    void PaintOverlay(Canvas c)
    {
        // a brief dip to black between rooms, and the edges reddening when Maren is one hit from death
        if (roomFade > 0) { roomFade = MathF.Max(0, roomFade - G.frameDt * 4.5f); c.Fill = new Color(0, 0, 0, roomFade * roomFade * 0.85f); c.FillRect(0, 0, VW, VH); }
        var pl = G.player;
        if (G.mode == "play" && pl != null && !pl.dead && pl.hp <= 1)
        {
            float pulse = 0.55f + 0.45f * MathF.Sin(G.Now * 5.5f);
            c.FillRectGradV(0, 0, VW, 50, new Color(0.45f, 0, 0, 0.35f * pulse), new Color(0.45f, 0, 0, 0));
            c.FillRectGradV(0, VH - 50, VW, 50, new Color(0.45f, 0, 0, 0), new Color(0.45f, 0, 0, 0.35f * pulse));
            c.FillRectGradH(0, 0, 70, VH, new Color(0.45f, 0, 0, 0.35f * pulse), new Color(0.45f, 0, 0, 0));
            c.FillRectGradH(VW - 70, 0, 70, VH, new Color(0.45f, 0, 0, 0), new Color(0.45f, 0, 0, 0.35f * pulse));
        }
        // At two pips or less the faceplate she sees through is crazed: cracks creep in from the edges.
        if (G.mode == "play" && pl != null && !pl.dead && pl.hp <= 2) DrawFaceplateCracks(c, pl.hp <= 1 ? 1 : 0.55f);
        // The Undertow passing the Tithe window.
        if (G.glimpseT > 0)
        {
            float t = 4 - G.glimpseT;
            float x = -200 + t * 220;
            c.Fill = new Color(0, 0, 0, 0.75f);
            c.BeginPath(); c.Ellipse(x, 60, 160, 40, 0, 0, H.PI * 2); c.FillPath();
            c.Fill = H.Rgba(120, 40, 30, 0.6f); c.BeginPath(); c.MoveTo(x - 30, 40); c.LineTo(x - 20, 10); c.LineTo(x - 10, 40); c.FillPath();
        }
    }
}
