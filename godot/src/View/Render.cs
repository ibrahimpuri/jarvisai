// Gameplay-facing render queries (what the lantern reveals), shared by bodies and the player.
using System;

namespace Hollowmere;

public static class Render
{
    // How visible a knot at (x, y) is to Maren this frame: lens, focused beam, nearby glow, other lights.
    public static float KnotVisibility(float x, float y, Body body)
    {
        var p = G.player;
        if (p == null || p.dead) return 0;
        float v = 0;
        float d = H.Dist(p.Cx(), p.Cy(), x, y);
        if (p.Has("lens") && d < 170 && Phys.Los(p.Cx(), p.y + 6, x, y)) v = MathF.Max(v, 0.95f * (1 - d / 260));
        // on the gentlest crossing the knots near her glint even in the dark
        float sight = Diff.C.knotSight;
        if (sight > 0 && d < sight) v = MathF.Max(v, 0.4f * (1 - d / sight) + 0.2f);
        if (p.LanternLit())
        {
            if (p.focusing)
            {
                var lp = p.LanternPos();
                float a = MathF.Atan2(y - lp.Y, x - lp.X);
                float dd = H.Dist(lp.X, lp.Y, x, y);
                if (dd < 260 && MathF.Abs(H.AngleDiff(p.aim, a)) < 0.25f && Phys.Los(lp.X, lp.Y, x, y)) v = 1;
            }
            if (v < 0.5f && d < 80) v = MathF.Max(v, 0.35f * (1 - d / 80) + 0.12f);
        }
        foreach (var l in G.frameLights)
        {
            if (l.player) continue;
            float dl = H.Dist(l.x, l.y, x, y);
            if (dl < l.r * 0.6f) v = MathF.Max(v, 0.45f * (1 - dl / (l.r * 0.6f)));
        }
        return v;
    }
}
