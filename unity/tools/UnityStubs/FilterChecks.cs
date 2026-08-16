using System;
using Singularity.Game;

/// <summary>
/// DOES THE SCREEN FILTER STILL SAY WHAT THE SLIDER SAYS?
///
/// Brightness and contrast are the two settings a player reaches for when the
/// game is unreadable, which means they are the two settings most likely to be
/// touched by somebody who cannot see what they are doing. They are also, since
/// the filter moved off the camera and onto a stack of overlay quads, the only
/// arithmetic in this game that is performed by BLEND HARDWARE rather than by
/// code — a multiply expressed as a source factor, a subtraction expressed as a
/// blend op, and a clamp between every pair of them that nobody wrote.
///
/// That is a lot of ways to be wrong quietly, and the first version was: it
/// decomposed `in*b*c + 0.5*(1-c)` into a gain of c and an offset of (b-1),
/// turning the brightness control from a multiply into an addition. At the top
/// of its travel it added 0.6 to every pixel of a game whose background is
/// 0.05. Nothing about that is subtle on a screen and nothing about it was
/// visible in a diff.
///
/// So this walks EVERY position of both sliders — the ranges Calibrate actually
/// offers — runs the plan through a written-out model of the blend stage, and
/// compares it to the old camera shader's fragment. One eight-bit step of slack,
/// because that is the resolution the answer is stored at.
/// </summary>
static class FilterChecks
{
    // the two Bar() ranges in Screens.Calibrate
    const int BrightLo = 60, BrightHi = 160;
    const int ContrastLo = 70, ContrastHi = 150;

    const float Step = 1f / 255f;

    public static void Run(Action<bool, string> ok)
    {
        var plan = new ScreenFilter.Stage[ScreenFilter.Stages];

        // AT 100/100 NOTHING IS SUBMITTED. Not "nothing visible happens" —
        // nothing is drawn at all, which is the promise the setting makes to
        // every player who never opens it.
        ok(!ScreenFilter.Solve(100, 100, plan), "the filter draws a quad at 100/100");

        float worst = 0f;
        int worstB = 0, worstC = 0;
        float worstX = 0f;
        int stages = 0;

        for (int bi = BrightLo; bi <= BrightHi; bi++)
        for (int ci = ContrastLo; ci <= ContrastHi; ci++)
        {
            bool any = ScreenFilter.Solve(bi, ci, plan);
            int used = 0;
            foreach (ScreenFilter.Stage s in plan) if (s.on) used++;
            if (used > stages) stages = used;

            if (!any)
            {
                // the only way out is the identity, and only 100/100 is it
                ok(bi == 100 && ci == 100,
                   "the filter does nothing at brightness " + bi + " contrast " + ci);
                continue;
            }

            // A GREY OUTSIDE 0..1 IS A QUAD THAT CANNOT BE DRAWN. The colour is
            // the only value that leaves this file for the GPU and it silently
            // saturates there, so a plan that needs 1.4 gets 1.0 and is wrong by
            // however much it asked for.
            foreach (ScreenFilter.Stage s in plan)
            {
                if (!s.on) continue;
                if (s.grey < 0f || s.grey > 1f)
                {
                    ok(false, "a stage at brightness " + bi + " contrast " + ci
                              + " wants a colour of " + s.grey.ToString("0.000"));
                    return;
                }
            }

            for (int i = 0; i <= 255; i++)
            {
                float x = i * Step;
                float got = ScreenFilter.Run(plan, x);
                float want = ScreenFilter.Reference(bi, ci, x);
                float err = Math.Abs(got - want);
                if (err > worst) { worst = err; worstB = bi; worstC = ci; worstX = x; }
            }
        }

        Console.WriteLine(string.Format(
            "filter: {0} slider pairs x 256 levels, at most {1} quads, worst error {2:0.0000} "
            + "({3:0} of 255) at brightness {4} contrast {5} on an input of {6:0.00}",
            (BrightHi - BrightLo + 1) * (ContrastHi - ContrastLo + 1), stages,
            worst, worst * 255f, worstB, worstC, worstX));

        ok(worst <= Step + 1e-5f,
           "the blend chain differs from the shader it replaced by "
           + (worst * 255f).ToString("0.0") + " of 255, at brightness " + worstB
           + " contrast " + worstC + " on an input of " + worstX.ToString("0.00"));

        ok(stages <= ScreenFilter.Stages,
           "a plan wanted " + stages + " quads and there are only " + ScreenFilter.Stages);

        // AND THE DIRECTION IS THE ONE THE LABEL PROMISES. A filter that matched
        // the formula but had the sliders backwards would pass everything above:
        // the formula would be being computed faithfully, of the wrong thing.
        Monotonic(ok, plan);
    }

    /// <summary>
    /// More brightness is never darker, and more contrast never moves a value
    /// toward mid grey. Checked on the mid-tone the board actually lives in
    /// rather than on 0.5, where a contrast change is a fixed point and proves
    /// nothing at all.
    /// </summary>
    static void Monotonic(Action<bool, string> ok, ScreenFilter.Stage[] plan)
    {
        const float Lit = 0.62f, Dark = 0.18f;

        float lastB = -1f;
        for (int bi = BrightLo; bi <= BrightHi; bi++)
        {
            ScreenFilter.Solve(bi, 100, plan);
            float v = ScreenFilter.Run(plan, Dark);
            if (v < lastB - 1e-4f)
            {
                ok(false, "brightness " + bi + " is darker than " + (bi - 1));
                return;
            }
            lastB = v;
        }

        for (int ci = ContrastLo; ci <= ContrastHi; ci++)
        {
            ScreenFilter.Solve(100, ci, plan);
            float lit = ScreenFilter.Run(plan, Lit);
            float dark = ScreenFilter.Run(plan, Dark);
            if (lit - dark < (Lit - Dark) * (ci / 100f) - 2f * Step)
            {
                ok(false, "contrast " + ci + " separates 0.18 from 0.62 by "
                          + (lit - dark).ToString("0.000") + ", less than it asks for");
                return;
            }
        }

        ok(true, "");
    }
}
