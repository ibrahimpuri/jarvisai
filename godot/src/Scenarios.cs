// Scripted gameplay scenarios for the Tester: each drives the real game through its input layer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hollowmere;

public static class Scenarios
{
    public static readonly Dictionary<string, Func<Tester, Task>> All = new()
    {
        ["boot"] = async t =>
        {
            t.Ok("starts on the reef", G.room.Id == "reef");
            Godot.GD.Print($"player {G.player.x},{G.player.y} room {G.room.Px},{G.room.Py} ents {string.Join(",", G.ents.Select(e => e.type))}");
            t.Ok("player placed", G.player.x > G.room.Px && G.player.x < G.room.Px + G.room.Pw);
            float x0 = G.player.x;
            await t.Hold("right", 1.0f);
            t.Ok("walks right", G.player.x > x0 + 60, $"dx={G.player.x - x0:F0}");
            await t.Run(0.3f);
            t.Ok("stands on ground", G.player.onGround);
            t.Snap("boot");
        },
    };
}
