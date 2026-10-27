// StoryFocus: the world notices when Maren stops to look at it.
//
// It keeps a list of the things in a room that tell its story: the room's monument (see Monuments), carvings not yet
// read, the bells, the font, the Hollow lying where the sea left it. Each frame it weighs how near Maren is to each one
// and how still she is, and the nearest one in reach becomes the focus. As the focus rises, smoothly, never snapping:
//   - the camera eases toward it (an offset of part of the way from Maren to the thing) and follows her more loosely,
//     so the frame settles and lingers the way Hollow Knight's does when you reach a vista;
//   - the far and middle planes soften (focus blur) and the edges of the frame close in a little (focus vignette),
//     so the eye goes where the light is;
//   - the ambient light leans toward the thing's own colour, and a soft light comes up on it;
//   - if she stays a moment beside a monument, she has a thought about it, once.
// None of it happens in a fight: anything hostile nearby, a recent hit, or a boss in the room lets it all go, fast,
// so the camera never drifts away from danger.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Hollowmere;

public static class StoryFocus
{
    public class Poi
    {
        public string id, thought;
        public float x, y;
        public float reach = 150, inner = 48;  // weight is 1 inside inner, falling to 0 at reach
        public float pull = 0.4f, maxPull = 64;   // how far toward it the camera leans (fraction of the gap, capped)
        public float blur = 1.4f, vignette = 0.14f, light = 0.5f, weight = 1, lean = 0.05f;
        public Color tint = new(1, 1, 1);
        public Ent ent;                        // for things that can stop mattering (a carving once read)
    }

    public static readonly List<Poi> Pois = new();
    public static Poi Active { get; private set; }
    public static float W { get; private set; }            // the focus, 0..1, smoothed
    public static Vector2 CamOffset { get; private set; }
    public static float Damping { get; private set; } = 1; // multiplies the camera's follow speed
    public static float Calm { get; private set; } = 1;    // 0 in a fight
    static RoomDef built;
    static float linger;
    public static bool Enabled = true;

    const float DANGER_R = 260;
    static bool Hostile(Body b) => b.Alive() && !b.corpse && !b.passive && b.mode != "dormant" && b.knots.Any(k => !k.broken);

    static int entCount;
    static void Build(RoomDef r, bool fresh)
    {
        if (fresh) { Active = null; W = 0; linger = 0; CamOffset = Vector2.Zero; Damping = 1; G.leanZoom = 1; }
        built = r; entCount = G.ents.Count; Pois.Clear();
        var m = Monuments.For(r);
        if (m != null)
            Pois.Add(new Poi { id = m.id, thought = "mon_" + m.id, x = m.x, y = m.Cy, reach = 170, inner = 56, pull = 0.45f, maxPull = 72, blur = 1.6f, vignette = 0.16f, light = 0.3f, lean = 0.06f, tint = m.tint });
        foreach (var e in G.ents)
        {
            Poi p = e switch
            {
                Carving => new Poi { id = "carving", reach = 110, inner = 30, pull = 0.3f, maxPull = 40, blur = 0.9f, vignette = 0.08f, light = 0.3f, weight = 0.7f, tint = new Color(0.9f, 1.02f, 1.12f) },
                Bell or HymnBell or TunedBell => new Poi { id = "bell", reach = 120, inner = 34, pull = 0.25f, maxPull = 36, blur = 0.8f, vignette = 0.06f, light = 0.25f, weight = 0.55f, tint = new Color(1.1f, 1.02f, 0.86f) },
                FontObj => new Poi { id = "font", reach = 120, inner = 34, pull = 0.3f, maxPull = 40, blur = 1.0f, vignette = 0.08f, light = 0.3f, weight = 0.6f, tint = new Color(0.9f, 1.04f, 1.1f) },
                HollowRelic => new Poi { id = "hollow", reach = 140, inner = 40, pull = 0.4f, maxPull = 56, blur = 1.4f, vignette = 0.14f, light = 0.45f, weight = 0.9f, tint = new Color(0.82f, 1.08f, 1.12f) },
                _ => null,
            };
            if (p == null) continue;
            p.ent = e; p.x = e.Cx(); p.y = e.Cy();
            Pois.Add(p);
        }
    }

    static bool Live(Poi p) => p.ent == null ? true : !p.ent.gone && !(p.ent is Carving cv && Narr.IsRead(cv.spec.S("id"))) && !(p.ent is Obj o && o.done && o is HollowRelic);

    public static void Update(float dt)
    {
        var pl = G.player; var r = G.room;
        if (pl == null || r == null) return;
        if (built != r) Build(r, true);
        else if (G.ents.Count != entCount) Build(r, false);   // something arrived (the sea leaves things) or left
        if (Active != null && !Pois.Contains(Active)) Active = Pois.FirstOrDefault(q => q.id == Active.id && q.ent == Active.ent);
        float px = pl.Cx(), py = pl.Cy();

        // a fight lets everything go, quickly; peace brings it back slowly
        bool danger = pl.dead || G.bossBar != null || pl.invulnT > 0
            || G.ents.OfType<Body>().Any(b => !b.gone && Hostile(b) && H.Dist(b.Cx(), b.Cy(), px, py) < DANGER_R);
        Calm = Approach(Calm, danger ? 0 : 1, dt * (danger ? 5f : 0.6f));

        // the nearest thing worth looking at, by how near she is and how much it matters
        Poi best = null; float bw = 0;
        foreach (var p in Pois)
        {
            if (!Live(p)) continue;
            if (p.ent != null) { p.x = p.ent.Cx(); p.y = p.ent.Cy(); }
            float d = H.Dist(px, py, p.x, p.y);
            float w = (1 - Smooth(p.inner, p.reach, d)) * p.weight;
            if (w > bw) { bw = w; best = p; }
        }
        // running past it is not looking at it: stillness lets the focus rise
        float speed = MathF.Abs(pl.vx) + MathF.Abs(pl.vy) * 0.5f;
        float still = 1 - Math.Clamp((speed - 40) / 260, 0, 0.55f);
        float target = Enabled ? bw * still * Calm : 0;
        // change of subject: let the old one fade before the new one rises
        if (best != Active && Active != null && W > 0.05f) target = 0;
        else if (best != Active) Active = best;
        W = Approach(W, target, dt * (target > W ? 0.9f : 2.2f));
        if (W < 0.002f && target == 0) Active = best;

        var lm = LightingManager.Inst;
        if (Active == null || W <= 0.001f)
        {
            CamOffset = Vector2.Zero; Damping = 1; linger = 0; G.leanZoom = 1;
            if (lm != null) { lm.FocusW = 0; lm.FocusBlur = 0; lm.FocusVignette = 0; }
            return;
        }
        var a = Active;
        var gap = new Vector2(a.x - px, a.y - py) * a.pull;
        if (gap.Length() > a.maxPull) gap = gap.Normalized() * a.maxPull;
        CamOffset = gap * W;
        Damping = 1 - 0.5f * W;
        // the frame leans in, about a point between her and it, so both stay in view (this is what moves in a
        // one-screen room, where the camera itself cannot)
        var me = new Vector2(px, py) - G.cam; var it = new Vector2(a.x, a.y) - G.cam;
        G.leanFocal = me.Lerp(it, 0.45f).Clamp(new Vector2(40, 30), new Vector2(H.VIEW_W - 40, H.VIEW_H - 30));
        G.leanZoom = 1 + a.lean * W;
        if (lm != null) { lm.FocusTint = a.tint; lm.FocusW = W * 0.6f; lm.FocusBlur = W * a.blur; lm.FocusVignette = W * a.vignette; }

        // stay a moment beside a monument and she thinks about it, once
        if (a.thought != null && W > 0.55f && H.Dist(px, py, a.x, a.y) < a.inner * 1.6f) linger += dt; else linger = MathF.Max(0, linger - dt);
        if (linger > 1.6f) { Narr.Think(a.thought); linger = -999; }
    }

    // a soft light comes up on whatever she is looking at
    public static void AddLights(List<LightSrc> lights)
    {
        if (Active == null || W < 0.02f) return;
        var t = Active.tint;
        lights.Add(new LightSrc { x = Active.x, y = Active.y, r = 70 + 30 * W, i = Active.light * W, color = $"{(int)Math.Clamp(t.R * 200, 0, 255)},{(int)Math.Clamp(t.G * 200, 0, 255)},{(int)Math.Clamp(t.B * 200, 0, 255)}" });
    }

    static float Approach(float v, float to, float step) => v < to ? MathF.Min(to, v + step) : MathF.Max(to, v - step);
    static float Smooth(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
}
