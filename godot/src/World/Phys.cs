// Axis-separated AABB movement against the tile grid and moving platforms.
using System;
using System.Collections.Generic;

namespace Hollowmere;

// Dynamic platforms registered each frame by floaters, barges and lifts.
public class Platform { public float x, y, w, h, dy; public bool oneWay; public object owner; }

// Anything that moves through the world with a box: the player, bodies, floating objects.
public abstract class Mover
{
    public float x, y, w, h, vx, vy;
    public bool onGround, wasGround, hitCeil, stepUp, dropping;
    public int hitWall;
    public Platform platform;
    public virtual float Cx() => x + w / 2;
    public virtual float Cy() => y + h / 2;
    public float Feet() => y + h;
}

public static class Phys
{
    const int TS = H.TILE;
    static List<Platform> platforms = new();
    public static List<Platform> Platforms => platforms;
    public static void SetPlatforms(List<Platform> list) => platforms = list;

    public static bool BoxSolid(float x, float y, float w, float h)
    {
        int x0 = H.FloorI(x / TS), x1 = H.FloorI((x + w - 0.01f) / TS);
        int y0 = H.FloorI(y / TS), y1 = H.FloorI((y + h - 0.01f) / TS);
        for (int ty = y0; ty <= y1; ty++) for (int tx = x0; tx <= x1; tx++) if (World.Solid(tx, ty) == 1) return true;
        foreach (var p in platforms) if (!p.oneWay && x < p.x + p.w && x + w > p.x && y < p.y + p.h && y + h > p.y) return true;
        return false;
    }

    public static void Move(Mover e, float dt)
    {
        e.onGround = false; e.hitWall = 0; e.hitCeil = false; e.platform = null;
        float total = MathF.Max(MathF.Abs(e.vx * dt), MathF.Abs(e.vy * dt));
        int steps = Math.Max(1, (int)MathF.Ceiling(total / 6));
        float sdt = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            // X axis
            float nx = e.x + e.vx * sdt;
            if (!BoxSolid(nx, e.y, e.w, e.h)) e.x = nx;
            else
            {
                // Step up a small lip (half a tile) when walking on the ground.
                if (e.stepUp && e.wasGround && !BoxSolid(nx, e.y - 7, e.w, e.h)) { e.x = nx; e.y -= 7; }
                else
                {
                    if (e.vx > 0) e.x = MathF.Floor((nx + e.w) / TS) * TS - e.w - 0.01f;
                    else if (e.vx < 0) e.x = MathF.Floor(nx / TS) * TS + TS + 0.01f;
                    e.hitWall = (int)H.Sign(e.vx); e.vx = 0;
                    if (BoxSolid(e.x, e.y, e.w, e.h)) e.x -= e.hitWall * 0.5f; // safety
                }
            }
            // Y axis
            float ny = e.y + e.vy * sdt;
            if (e.vy >= 0)
            {
                float prevBottom = e.y + e.h;
                float newBottom = ny + e.h;
                float? landY = null;
                int x0 = H.FloorI(e.x / TS), x1 = H.FloorI((e.x + e.w - 0.01f) / TS);
                int ty0 = H.FloorI(prevBottom / TS), ty1 = H.FloorI((newBottom - 0.01f) / TS);
                for (int ty = Math.Max(ty0, H.FloorI(e.y / TS)); ty <= ty1 && landY == null; ty++)
                {
                    for (int tx = x0; tx <= x1; tx++)
                    {
                        int s = World.Solid(tx, ty);
                        if (s == 1 && ty * TS >= e.y + e.h - 0.5f) { landY = ty * TS; break; }
                        if (s == 2 && !e.dropping && prevBottom <= ty * TS + 2) { landY = ty * TS; break; }
                    }
                }
                foreach (var p in platforms)
                {
                    if (e.x < p.x + p.w && e.x + e.w > p.x && prevBottom <= p.y + 1 && newBottom >= p.y && !(p.oneWay && e.dropping))
                    {
                        if (landY == null || p.y < landY) { landY = p.y; e.platform = p; }
                    }
                }
                if (landY != null && newBottom >= landY)
                {
                    e.y = landY.Value - e.h; e.vy = 0; e.onGround = true;
                }
                else if (BoxSolid(e.x, ny, e.w, e.h)) e.vy = 0;
                else e.y = ny;
            }
            else
            {
                if (!BoxSolid(e.x, ny, e.w, e.h)) e.y = ny;
                else { e.y = MathF.Floor(ny / TS) * TS + TS + 0.01f; e.vy = 0; e.hitCeil = true; }
            }
        }
        // Ground probe so standing still still counts as grounded.
        if (!e.onGround && e.vy >= 0)
        {
            float by = e.y + e.h + 1;
            int x0 = H.FloorI(e.x / TS), x1 = H.FloorI((e.x + e.w - 0.01f) / TS);
            int ty = H.FloorI(by / TS);
            for (int tx = x0; tx <= x1; tx++)
            {
                int s = World.Solid(tx, ty);
                if ((s == 1 || (s == 2 && !e.dropping)) && MathF.Abs(ty * TS - (e.y + e.h)) < 1.5f) { e.onGround = true; break; }
            }
            foreach (var p in platforms)
                if (e.x < p.x + p.w && e.x + e.w > p.x && MathF.Abs(p.y - (e.y + e.h)) < 1.5f) { e.onGround = true; e.platform = p; }
        }
        e.wasGround = e.onGround;
    }

    // Line of sight between two world points (tiles only).
    public static bool Los(float x1, float y1, float x2, float y2)
    {
        float d = H.Hypot(x2 - x1, y2 - y1);
        int n = (int)MathF.Ceiling(d / 6);
        for (int i = 1; i < n; i++)
        {
            float t = (float)i / n;
            if (World.SolidPx(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t)) return false;
        }
        return true;
    }

    // March a ray through tiles; returns hit distance or maxD.
    public static float Ray(float x, float y, float ang, float maxD)
    {
        float dx = MathF.Cos(ang), dy = MathF.Sin(ang);
        for (float d = 0; d < maxD; d += 4) if (World.SolidPx(x + dx * d, y + dy * d)) return d;
        return maxD;
    }
}
