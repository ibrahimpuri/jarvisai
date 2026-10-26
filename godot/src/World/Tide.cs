// The tide clock: one global cycle that every other system hangs on.
using System;
using System.Collections.Generic;

namespace Hollowmere;

public static class Tide
{
    public record Phase(string Id, string Name, float Len);
    public static readonly Phase[] PHASES =
    {
        new("low", "Low", 180), new("rising", "Rising", 120), new("high", "High", 180), new("ebbing", "Ebbing", 120),
    };
    // World-space water surfaces (in tiles, y grows downward).
    public const float SEA_LOW = 109, SEA_HIGH = 101;

    public static float t;          // seconds into the current cycle, already scaled
    public static int cycle;
    public static float scale = 1;  // tide-length multiplier
    public static float spring;     // Act III spring tides raise every line
    public static bool paused;
    public static readonly List<Action<string, string>> listeners = new();

    public static float Len(int i) => PHASES[i].Len * scale;
    public static float Total() { float s = 0; for (int i = 0; i < 4; i++) s += Len(i); return s; }
    public static int Index()
    {
        float acc = 0;
        for (int i = 0; i < 4; i++) { acc += Len(i); if (t < acc) return i; }
        return 3;
    }
    public static string PhaseId() => PHASES[Index()].Id;
    public static string PhaseName() => PHASES[Index()].Name;
    public static float PhaseStart(int i) { float acc = 0; for (int k = 0; k < i; k++) acc += Len(k); return acc; }
    public static float PhaseRemaining() { int i = Index(); return PhaseStart(i) + Len(i) - t; }
    public static float PhaseProgress() { int i = Index(); return (t - PhaseStart(i)) / Len(i); }

    public static float SeaLevel() => SeaLevel(t);
    public static float SeaLevel(float atT)
    {
        float lo = SEA_LOW - spring, hi = SEA_HIGH - spring;
        float acc = 0;
        for (int i = 0; i < 4; i++)
        {
            float L = Len(i);
            if (atT < acc + L || i == 3)
            {
                float p = H.Clamp((atT - acc) / L, 0, 1);
                float s = p * p * (3 - 2 * p);
                if (i == 0) return lo;
                if (i == 1) return H.Lerp(lo, hi, s);
                if (i == 2) return hi;
                return H.Lerp(hi, lo, s);
            }
            acc += L;
        }
        return lo;
    }
    // 0 at low water, 1 at high water.
    public static float Level01() => H.Clamp((SEA_LOW - spring - SeaLevel()) / (SEA_LOW - SEA_HIGH), 0, 1);

    public static void On(Action<string, string> fn) => listeners.Add(fn);
    public static void Update(float dt)
    {
        if (paused) return;
        int before = Index();
        t += dt;
        float total = Total();
        if (t >= total) { t -= total; cycle++; }
        int after = Index();
        if (after != before) foreach (var fn in listeners.ToArray()) fn(PHASES[after].Id, PHASES[before].Id);
    }
    // Jump the clock forward, firing every phase change on the way.
    public static void AdvanceTo(string phaseId)
    {
        int target = Array.FindIndex(PHASES, p => p.Id == phaseId);
        int guard = 0;
        do
        {
            int i = Index();
            float step = PhaseStart(i) + Len(i) - t + 0.01f;
            Update(step);
            guard++;
        } while (Index() != target && guard < 8);
    }
    public static void SetPhase(string phaseId, float intoSeconds = 0)
    {
        int i = Array.FindIndex(PHASES, p => p.Id == phaseId);
        t = PhaseStart(i) + intoSeconds;
    }
}
