using System;
using UnityEngine;
using Singularity.Game;

/// <summary>
/// THE SKY IS ALLOWED BEHIND THE BOARD ON TWO CONDITIONS, AND THIS IS WHERE THEY
/// ARE KEPT.
///
/// CameraRig used to clear to black and say why: a void column is unrendered
/// space, and anything painted there is the display claiming to have data it
/// does not have. There is a starfield behind the machine now — deliberately,
/// because a black rectangle behind glass reads as a screen that is off — and
/// the argument it replaced was right about two specific things, both of which
/// are numbers:
///
///   1. AN AREA FILL COMPETES WITH THE DEPTH RAMP. The board is read off one
///      property — a trace is brighter than the lattice at every depth — and
///      the far end of that ramp is the darkest thing the game draws. Lift the
///      floor behind it and the bottom of the ramp stops being a distance. So
///      the field's one area fill, the nebula, is held below the dimmest cell
///      in the deepest vault, with room to spare.
///
///   2. A POINT IS NOT A CLAIM. Stars may be bright — a dim star is a smudge,
///      not a subtle star — because they are two or three pixels across against
///      a cell of nearly two hundred. That ratio is asserted rather than
///      eyeballed, on the real phone shapes, at the smallest cube the game
///      builds and the largest.
///
/// And one thing that is not about the picture at all: the parallax is motion,
/// so CALIBRATE governs it. At STILL it must be exactly zero.
/// </summary>
static class SkyChecks
{
    public static void Run(Action<bool, string> ok)
    {
        Fill(ok);
        Points(ok);
        Governed(ok);
    }

    /// <summary>The nebula against the far end of the deepest vault's ramp.</summary>
    static void Fill(Action<bool, string> ok)
    {
        Color tint = Sky.Tint;
        Color neb = new Color(tint.r * Sky.NebulaPeak, tint.g * Sky.NebulaPeak, tint.b * Sky.NebulaPeak, 1f);

        // the dimmest cell this game draws: the lattice at the far end of the
        // ramp, in the vault where the machine has corroded furthest
        Color dimmest = Palette.LatticeFarOf(Singularity.Core.Vaults.LastBand);

        float ln = Access.Relative(neb), ld = Access.Relative(dimmest);
        ok(ln < ld * 0.5f,
           "the nebula is " + ln.ToString("0.0000") + " against the dimmest cell's " + ld.ToString("0.0000")
         + " — the sky is lifting the floor the depth ramp is measured from");

        // and the brightest star is NOT held to that, on purpose — it is a point,
        // and Points is what makes that true. This asserts the intent rather than
        // leaving the peak free to drift up to white.
        ok(Sky.StarPeak > ld && Sky.StarPeak <= 1f,
           "the brightest star is " + Sky.StarPeak.ToString("0.00")
         + ", which is not a star, it is a smudge");

        // AND IT IS UNDER THE GLOW'S KNEE. A star over the threshold comes back
        // from Bloom as a halo several times its own size, and Points below would
        // be asserting the size of something that is not what reaches the screen.
        ok(Sky.StarPeak < Bloom.Threshold,
           "a star peaks at " + Sky.StarPeak.ToString("0.00") + " against the bloom's knee at "
         + Bloom.Threshold.ToString("0.00") + " — the sky glows, and a glowing point is not a point");

        Console.WriteLine("sky: the nebula sits at " + ln.ToString("0.0000") + " against the dimmest cell's "
                        + ld.ToString("0.0000") + ", and a star peaks at " + Sky.StarPeak.ToString("0.00")
                        + ", under the bloom's knee at " + Bloom.Threshold.ToString("0.00"));
    }

    struct Device
    {
        public string name; public int w, h;
        public Device(string n, int w, int h) { name = n; this.w = w; this.h = h; }
    }

    static readonly Device[] Phones =
    {
        new Device("16:9   1080x1920", 1080, 1920),
        new Device("20:9   1440x2960", 1440, 2960),
        new Device("4:3    1536x2048", 1536, 2048),
    };

    /// <summary>A star against a cell, on real shapes, at both ends of the ladder.</summary>
    static void Points(Action<bool, string> ok)
    {
        int savedW = Screen.width, savedH = Screen.height;
        Rect savedSafe = Screen.safeArea;
        try
        {
            float worst = float.MaxValue; string worstName = "";
            foreach (Device d in Phones)
            {
                Screen.width = d.w; Screen.height = d.h;
                Screen.safeArea = new Rect(0, 0, d.w, d.h);

                Rect b = Layout.BoardRect();
                float side = Mathf.Max(1f, Mathf.Min(b.width, b.height)) / Layout.FitMargin;

                // the LARGEST cube the ladder builds is the one with the smallest
                // cells, so that is the case the ratio has to survive
                foreach (int n in new[] { 3, 9 })
                {
                    float cell = side / n;
                    float star = Sky.StarPx * 2f;          // diameter, in pixels
                    float ratio = cell / star;
                    if (ratio < worst) { worst = ratio; worstName = d.name + " at " + n + " cells"; }

                    ok(ratio > 20f,
                       d.name + ": a star is " + star.ToString("0.0") + " pixels against a cell's "
                              + cell.ToString("0") + " on an " + n + "-cube — only " + ratio.ToString("0")
                              + " times, which is an object rather than a point");
                }
            }

            ok(Sky.StarPx * 2f <= 4f,
               "a star is " + (Sky.StarPx * 2f).ToString("0.0") + " pixels across, which is no longer a point");

            Console.WriteLine("sky: the tightest a star ever is against a cell is 1 to " + worst.ToString("0")
                            + " (" + worstName + "), at " + (Sky.StarPx * 2f).ToString("0.0") + " pixels across");
        }
        finally
        {
            Screen.width = savedW; Screen.height = savedH; Screen.safeArea = savedSafe;
        }
    }

    /// <summary>
    /// THE PARALLAX IS MOTION AND CALIBRATE GOVERNS IT. Sky multiplies its travel
    /// by Access.MotionAmount, which is zero at STILL — so this asserts the thing
    /// the multiplication promises, at all three settings, rather than trusting
    /// that the line stays where it is.
    /// </summary>
    static void Governed(Action<bool, string> ok)
    {
        int saved = Store.Data.motion;
        try
        {
            Store.Data.motion = (int)Access.MotionLevel.Still;
            ok(Sky.Travel * Access.MotionAmount == 0f,
               "the sky still drifts at STILL, by " + (Sky.Travel * Access.MotionAmount).ToString("0.0000"));

            Store.Data.motion = (int)Access.MotionLevel.Reduced;
            float red = Sky.Travel * Access.MotionAmount;

            Store.Data.motion = (int)Access.MotionLevel.Full;
            float full = Sky.Travel * Access.MotionAmount;

            ok(red > 0f && red < full, "REDUCED does not sit between STILL and FULL for the sky");

            // and the travel is small enough to be a background rather than a
            // thing that moves: a twentieth of half the screen height
            ok(full > 0.005f && full < 0.05f,
               "the sky travels " + (full * 100f).ToString("0.0") + "% of the half-screen at full lean");

            Console.WriteLine("sky: the field travels " + (full * 100f).ToString("0.0") + "% at FULL, "
                            + (red * 100f).ToString("0.0") + "% at REDUCED and nothing at STILL");
        }
        finally { Store.Data.motion = saved; }
    }
}
