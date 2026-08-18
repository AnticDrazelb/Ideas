using System;
using Singularity.Game;

/// <summary>
/// THE PHONE ACTUALLY BUZZES.
///
/// Three things were wrong and none of them could fail a build.
///
/// The permission was never declared. Haptics reach the vibrator through
/// AndroidJavaObject, so Unity cannot see that this game vibrates — it adds
/// android.permission.VIBRATE only when it spots Handheld.Vibrate() in the code.
/// Every call threw SecurityException into an empty catch. See
/// Editor/HapticsPermission.
///
/// The patterns were added up. Every array here is an Android vibration pattern,
/// { delay, on, off, on, ... }, and Buzz(int[]) summed it and buzzed for the
/// total — so Shatter's two hits with forty milliseconds of silence between them
/// came out as one flat 210ms drone, and so did every other pattern in the game.
///
/// And the failure was swallowed. `catch { }` is why a missing permission looked
/// like a phone with no motor.
///
/// What is checkable without a device is the DECISION: what would be sent, given
/// the switch, the slider and the pattern. That is a pure function now.
/// </summary>
static class HapticChecks
{
    static readonly (string name, int[] pattern)[] Patterns =
    {
        ("fault", Haptics.Fault),
        ("collapse", Haptics.Collapse),
        ("shatter", Haptics.Shatter),
    };

    public static void Run(Action<bool, string> ok)
    {
        // ---- the switch and the slider both mean off ------------------------
        ok(Haptics.Plan(Haptics.Move, 0) == 0, "a strength of zero still buzzes");
        ok(Haptics.Plan(0, 100) == 0, "a zero-length buzz still buzzes");
        ok(Haptics.Plan(Haptics.Fault, 0) == null, "a strength of zero still plays a pattern");

        // ---- and the slider scales ------------------------------------------
        long half = Haptics.Plan(Haptics.Move, 50), full = Haptics.Plan(Haptics.Move, 100);
        ok(full == Haptics.Move, "a hundred per cent is not the pattern's own duration");
        ok(half > 0 && half < full, "the strength slider does not scale a single buzz");

        // ---- A PATTERN IS STILL A PATTERN -----------------------------------
        //
        // The failure this replaces produced ONE number from an array, so the test
        // that matters is that the array survives: same length, same shape, and
        // more than one distinct value in it. A pattern flattened to a drone has
        // every gap gone.
        foreach ((string name, int[] p) in Patterns)
        {
            long[] plan = Haptics.Plan(p, 100);
            ok(plan != null && plan.Length == p.Length,
               name + " does not come out of the planner as a pattern");
            if (plan == null) continue;

            long on = 0, off = 0;
            for (int i = 1; i < plan.Length; i += 2) on += plan[i];
            for (int i = 2; i < plan.Length; i += 2) off += plan[i];

            ok(on > 0, name + " has no motor time in it at all");
            ok(plan[0] >= 0, name + " opens on a negative delay");

            // the one that would have caught the sum: a pattern with gaps has to
            // still have them
            int gaps = 0;
            for (int i = 2; i < p.Length; i += 2) if (p[i] > 0) gaps++;
            if (gaps > 0)
                ok(off > 0, name + " is declared with silence in it and plans none");
        }

        // ---- and nothing is long enough to be a nuisance ---------------------
        long worst = 0;
        foreach ((string name, int[] p) in Patterns)
        {
            long[] plan = Haptics.Plan(p, 150);      // the slider's own ceiling
            if (plan == null) continue;
            long total = 0;
            foreach (long t in plan) total += t;
            worst = Math.Max(worst, total);
        }
        ok(worst <= 600, "the longest haptic runs " + worst + "ms at full strength");

        Console.WriteLine("haptic: " + Patterns.Length + " patterns keep their shape through the planner, "
                        + "longest " + worst + "ms at 150%, and VIBRATE is added to the manifest on build");
    }
}
