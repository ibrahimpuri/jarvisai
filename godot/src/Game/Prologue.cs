// The first room's furniture: invisible zones that give Maren a thought or let a hint surface,
// and the rowboat she came in on, holed on the outer rocks.
using System;
using Godot;

namespace Hollowmere;

public class Zone : Obj
{
    readonly string hint, think;
    public Zone(Props spec, RoomDef room) : base(spec, room)
    {
        x = room.Px + spec.I("tx") * H.TILE; y = room.Py + spec.I("ty") * H.TILE;
        w = spec.I("w") * H.TILE; h = spec.I("h") * H.TILE;
        hint = spec.S("hint"); think = spec.S("think");
    }
    public override bool HasPrompt => false;
    public override void Update(float dt)
    {
        var p = G.player;
        if (p == null || p.dead || !H.RectsOverlap(p.x, p.y, p.w, p.h, x, y, w, h)) return;
        if (hint != null) Hints.InZone(hint);
        if (think != null) Narr.Think(think);
    }
}

public class Boat : Obj
{
    public Boat(Props spec, RoomDef room) : base(spec, room) { w = 44; h = 14; x = spec.F("x") - 22; y = spec.F("y") - 10; }
    public override bool HasPrompt => false;
    public override void Draw(Canvas c)
    {
        // Settled by the stern, rocking a little in the swell.
        float cx = Cx(), sy = spec.F("y") + MathF.Sin(t * 1.3f) * 0.8f;
        c.Save(); c.Translate(cx, sy); c.Rotate(-0.12f + MathF.Sin(t * 0.9f) * 0.02f);
        c.FillStyle = "#2c2118";
        c.BeginPath(); c.MoveTo(-23, -8); c.QuadraticCurveTo(-4, 9, 21, -7); c.LineTo(19, -10); c.QuadraticCurveTo(-4, 2, -21, -11); c.ClosePath(); c.FillPath();
        c.FillStyle = "#5e4a30"; c.FillRect(-22, -11, 42, 2);
        c.FillStyle = "#3d2f20"; for (int i = -14; i <= 12; i += 9) c.FillRect(i, -9, 2, 8);
        // the hole the rocks punched through
        c.FillStyle = "#0c0a08"; c.BeginPath(); c.MoveTo(2, -3); c.LineTo(9, -6); c.LineTo(7, 1); c.LineTo(3, 2); c.ClosePath(); c.FillPath();
        c.Restore();
        // an oar adrift beside it
        c.Stroke = H.Col("#5e4a30"); c.LineWidth = 1.5f;
        float ox = cx - 34, oy = sy - 1 + MathF.Sin(t * 1.7f + 1) * 0.8f;
        c.Line(ox - 10, oy, ox + 9, oy - 2);
        c.FillStyle = "#5e4a30"; c.FillRect(ox + 8, oy - 4, 6, 3);
    }
}
