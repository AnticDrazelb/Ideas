using System;
using Singularity.Core;
using Singularity.Game;

/// THE ACCESS AUDIT, RUN WITHOUT UNITY.
///
/// Every claim this project makes about being readable is a claim about
/// arithmetic on colours, and arithmetic on colours is exactly the kind of thing
/// that can be checked by a plain console program — so it is, and it is checked
/// against the GAME's own palette rather than against a copy of it.
///
/// This is the half of the AAA standard that does not need a screen. What it
/// cannot tell you is whether a control is where a thumb can reach it; that half
/// is in Assets/Tests/EditMode/AccessTests.cs, which needs a real layout.
public static class AccessChecks
{
    static int fails;
    static void Ok(bool b, string what) { if (!b) { Console.WriteLine("FAIL: " + what); fails++; } }

    public static int Run()
    {
        Store.Load();

        Parsed();
        Shipped();
        Band();
        Printed();
        Motion();
        Light();

        return fails;
    }

    /// <summary>
    /// THE HARNESS'S OWN COLOUR MATHS, FIRST.
    ///
    /// Everything below is a comparison between two parsed colours, so a parser
    /// that answers black would turn the whole file green and mean nothing —
    /// which is the exact trap the tools README describes Mathf being made real
    /// to avoid. Two known values and one known ratio are enough to prove the
    /// floor this file is standing on.
    /// </summary>
    static void Parsed()
    {
        UnityEngine.ColorUtility.TryParseHtmlString("#38bdf8", out var trace);
        Ok(Math.Abs(trace.r - 56f / 255f) < 1e-4, "the harness cannot parse a hex colour");
        Ok(Math.Abs(trace.g - 189f / 255f) < 1e-4, "the harness parses green wrong");
        Ok(Math.Abs(trace.b - 248f / 255f) < 1e-4, "the harness parses blue wrong");

        // white on black is 21:1 by definition, and it is the only ratio in the
        // standard that can be checked without trusting anything else
        Ok(Math.Abs(Access.Contrast(UnityEngine.Color.white, UnityEngine.Color.black) - 21f) < 0.01f,
           "white on black is not 21:1 — the contrast maths is wrong");

        // and Lerp has to actually interpolate, or the depth ramps are one colour
        var mid = UnityEngine.Color.Lerp(UnityEngine.Color.black, UnityEngine.Color.white, 0.5f);
        Ok(Math.Abs(mid.r - 0.5f) < 1e-4, "the harness's Lerp does not interpolate");
    }

    /// <summary>
    /// THE DEFAULT IS THE SHIPPED BUILD, TO THE BYTE.
    ///
    /// Legibility is a mode, and the whole argument for it being a mode is that
    /// the default stays the picture that was matched to the APK value by value.
    /// If that ever stops being true this file should be the thing that says so,
    /// because nothing else will: a palette drifting by one step per commit looks
    /// like nothing at all.
    /// </summary>
    static void Shipped()
    {
        Store.Data.legible = 0;

        void Is(UnityEngine.Color c, string hex, string what)
        {
            UnityEngine.ColorUtility.TryParseHtmlString(hex, out var want);
            Ok(Math.Abs(c.r - want.r) < 1e-4 && Math.Abs(c.g - want.g) < 1e-4 && Math.Abs(c.b - want.b) < 1e-4,
               what + " is not the shipped " + hex);
        }

        Is(Palette.Rust, "#ea580c", "rust");
        Is(Palette.RustHi, "#fb923c", "rust-hi");
        Is(Palette.Core, "#f97316", "core");
        Is(Palette.Fault, "#ef4444", "fault");
        // THREE VALUES NO LONGER MATCH THE APK, AND THE REASON IS IN THE PIN.
        //
        // Dim was #64748b, which is 4.41:1 on black and 3.70 on a card — under
        // AA, in the colour that carries every body paragraph in the game. Dim2
        // was #334155 at 2.03:1 and was carrying five pieces of real text.
        // Matching a reference that fails AA reproduces the failure, so these
        // moved: the smallest lift at the same hue and saturation that clears
        // 4.5:1 on all four dark grounds. The pin stays, because catching a
        // value that drifts by accident is still the job.
        Is(Palette.Dim, "#75859b", "dim");
        Is(Palette.Dim2, "#516888", "dim2");
        Is(Palette.Ink, "#e2e8f0", "ink");
        Is(Palette.Node, "#a3e635", "node");
        Is(Palette.Lock, "#facc15", "lock");
        Is(Palette.Arc, "#22d3ee", "arc");
        Is(Palette.Trace, "#38bdf8", "trace");
        Is(Palette.Panel, "#0d1117", "panel");
        // and the near lattice sank, because the board's own worst pair was
        // 2.30:1 at the deepest vault — see the ratio walk in Band()
        Is(Palette.LatticeNear, "#313c4d", "the near lattice");

        // the smallest type in the interface is --t-micro, 8.5 CSS px -> 17 units
        Ok(Access.SmallestType == 17, "the smallest type is no longer seventeen units");
    }

    /// <summary>
    /// THE BAND GUARANTEE: a trace is brighter than the lattice, at every depth,
    /// to every eye.
    ///
    /// This is the claim the whole board is read off, and it is stated in
    /// Palette as prose. Prose does not survive somebody nudging a hex value to
    /// look nicer, and the regression does not look like a bug — it looks like
    /// the game got hard. The worst pair is the FARTHEST trace against the
    /// NEAREST lattice, because that is where the two ramps come closest.
    /// </summary>
    static void Band()
    {
        foreach (int legible in new[] { 0, 1 })
        {
            Store.Data.legible = legible;
            string mode = legible == 0 ? "shipped" : "legible";

            Ok(Palette.BandGap() > 0f, mode + ": the palette's own luminance band has inverted");

            // EVERY VAULT, NOT THE ONE THE PALETTE WAS WRITTEN AGAINST.
            //
            // The lattice ages down the ladder now — the machine corroding as you
            // go deeper — and an ageing lattice is a lattice walking toward the
            // trace. The band guarantee is the one property the whole board is
            // read off, so it is proved at every vault, at every depth, under
            // every dichromacy. A mood that ate a tenth of the band would look
            // like the game getting hard rather than like a bug, which is exactly
            // the failure this file exists to make impossible.
            float worst = 99f;
            string worstAt = "";

            foreach (var eye in Access.AllSight)
                for (int band = 0; band < Vaults.RankedVaults; band++)
                {
                    // every depth, not just the ends: the ramps are linear, so the
                    // ends bound it — but the ends are checked as CELLS, through the
                    // same Tile() the shader mirrors, so a change to the ramp maths
                    // is caught as well as a change to the four colours.
                    for (int n = 2; n <= 9; n++)
                        for (int d = 0; d < n; d++)
                        {
                            var trace = Access.Simulate(Palette.Tile('+', d, n, band), eye);
                            // the brightest lattice anywhere on the board is the
                            // nearest one, and it is what a far trace has to beat
                            var lattice = Access.Simulate(Palette.Tile('#', n - 1, n, band), eye);
                            float gap = Access.Relative(trace) - Access.Relative(lattice);
                            if (gap < worst) { worst = gap; worstAt = mode + "/" + eye + "/vault " + (band + 1); }
                            Ok(gap > 0f,
                               mode + "/" + eye + ": in vault " + (band + 1) + ", a trace at depth " + d
                               + " of " + n + " is not brighter than the nearest lattice");
                        }
                }

            Console.WriteLine("access: " + mode + " palette, narrowest trace-over-lattice margin "
                              + worst.ToString("0.0000") + " at " + worstAt);

            // AND THE SAME PAIR AS A CONTRAST RATIO, AT EVERY VAULT.
            //
            // The printed table has TraceFar-on-LatticeNear in it and measures it
            // exactly once, against the un-aged lattice — which is the BEST case.
            // The lattice corrodes down the ladder, so it walks toward the trace,
            // and the pair that read 2.56:1 in vault one was 2.30:1 in vault
            // thirty with nothing anywhere saying so. The band check above ran at
            // every vault and could not catch it, because it asks whether the
            // trace is BRIGHTER and not whether you can tell them apart.
            float tight = 99f;
            int tightAt = 0;
            for (int band = 0; band < Vaults.RankedVaults; band++)
            {
                float r = Access.Contrast(Palette.TraceFar, Palette.LatticeNearOf(band));
                if (r < tight) { tight = r; tightAt = band + 1; }
            }
            Ok(tight >= Access.NonText,
               mode + ": the board's worst pair is " + tight.ToString("0.00") + ":1 in vault " + tightAt
               + ", under 1.4.11's " + Access.NonText.ToString("0.0") + " for two things you have to tell apart");
            Console.WriteLine("access: " + mode + " palette, tightest trace-against-lattice ratio "
                              + tight.ToString("0.00") + ":1 at vault " + tightAt + " of " + Vaults.RankedVaults);
        }
    }

    /// <summary>
    /// Every ink-on-ground the interface prints, against the floor it has to
    /// clear. The shipped mode is MEASURED and reported; the legible mode is
    /// ENFORCED, because that is the mode whose entire purpose is the number.
    /// </summary>
    static void Printed()
    {
        // TWO STANDARDS, NOT ONE, BECAUSE THEY MEAN DIFFERENT THINGS.
        //
        // The table's floor is AAA — 7:1 for type, 3:1 for a graphical object —
        // and the shipped palette deliberately does not meet all of it: that is
        // what the legibility mode is for, and a default that matches the
        // original build is a legitimate shipping choice.
        //
        // AA IS NOT A CHOICE. 4.5:1 for type is the baseline every platform's
        // certification asks for and the one an accessibility regulation names,
        // so a shipped default under it is a defect rather than a preference.
        // Reporting one number for both hid that distinction for the whole port:
        // "11 of 21 below their floor" was true, unactionable, and quietly
        // contained three body-text pairs under 4.5.
        //
        // So the shipped palette is ASSERTED against AA and MEASURED against
        // AAA, and every pair is named rather than counted.
        Store.Data.legible = 0;
        int belowAAA = 0;
        foreach (var p in Access.Printed)
        {
            float r0 = Access.Contrast(Access.Named(p.ink), Access.Named(p.ground));
            bool text = p.floor > Access.NonText + 0.001f;
            float aa = text ? Access.AAText : Access.NonText;
            if (r0 < p.floor) belowAAA++;
            Console.WriteLine(string.Format("   {0} {1,-9} on {2,-12} {3,5:0.00}   AA {4,4:0.0} {5}   AAA {6,4:0.0} {7}",
                r0 < aa ? "FAIL " : "  ok ", p.ink, p.ground, r0,
                aa, r0 >= aa ? "pass" : "FAIL", p.floor, r0 >= p.floor ? "pass" : "----"));
            Ok(r0 >= aa, "shipped: " + p.ink + " on " + p.ground + " is " + r0.ToString("0.00")
                         + ", under the " + aa.ToString("0.0") + " this has to clear even in the default palette"
                         + " (" + p.why + ")");
        }
        Console.WriteLine("access: shipped palette clears AA everywhere; " + belowAAA + " of "
                          + Access.Printed.Length + " pairs are short of AAA, which is what the "
                          + "legibility mode is for");

        Store.Data.legible = 1;
        foreach (var p in Access.Printed)
        {
            float r = Access.Contrast(Access.Named(p.ink), Access.Named(p.ground));
            Ok(r >= p.floor,
               "legible: " + p.ink + " on " + p.ground + " is " + r.ToString("0.00")
               + ":1, under " + p.floor.ToString("0.0") + " — " + p.why);
        }

        // and a typo in a role name must not read as a pass
        Ok(Access.Contrast(Access.Named("no such colour"), Palette.Void) > 1f,
           "an unknown role did not come back loud");
    }

    /// <summary>STILL IS ZERO. That is the entire reason the setting was split off.</summary>
    static void Motion()
    {
        Store.Data.motion = (int)Access.MotionLevel.Full;
        Ok(Access.MotionAmount == 1f, "full motion is not full");

        Store.Data.motion = (int)Access.MotionLevel.Reduced;
        Ok(Access.MotionAmount > 0f && Access.MotionAmount < 1f, "reduced motion is not between");

        Store.Data.motion = (int)Access.MotionLevel.Still;
        Ok(Access.MotionAmount == 0f, "STILL still moves the camera");

        // a stored value outside the enum must not become a fourth level
        Store.Data.motion = 99;
        Ok(Access.Motion == Access.MotionLevel.Still, "a corrupt motion setting escaped the clamp");
        Store.Data.motion = -1;
        Ok(Access.Motion == Access.MotionLevel.Full, "a negative motion setting escaped the clamp");
        Store.Data.motion = 0;
    }

    /// <summary>
    /// The light channel has its own off, and 2.3.1's budget is three flashes a
    /// second — a number the game has to KNOW rather than merely stay under.
    /// </summary>
    static void Light()
    {
        Store.Data.light = (int)Access.LightLevel.Full;
        Ok(Access.LightAmount == 1f && Access.Bloom && Access.Light, "full light is not full");

        Store.Data.light = (int)Access.LightLevel.Reduced;
        Ok(Access.Light && !Access.Bloom, "reduced light still pays for a bloom pass");

        Store.Data.light = (int)Access.LightLevel.None;
        Ok(Access.LightAmount == 0f && !Access.Light, "NONE still draws light");

        Ok(Access.FlashesPerSecond <= 3, "the flash budget is over 2.3.1's three a second");
        Store.Data.light = 0;
    }
}
