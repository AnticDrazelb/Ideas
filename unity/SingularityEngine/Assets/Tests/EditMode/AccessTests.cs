using NUnit.Framework;
using UnityEngine;
using Singularity.Game;
using Singularity.UI;

namespace Singularity.Tests
{
    /// <summary>
    /// THE AAA STANDARD, ASSERTED.
    ///
    /// Every claim this project makes about being readable, hearable and
    /// reachable is a claim about a number, and each of those numbers is checked
    /// here. Most of them are also checked outside Unity, by the stub harness in
    /// <c>unity/tools</c> — duplicated rather than replaced, for the same reason
    /// the Forge's are: the harness proves the arithmetic, and the Test Runner
    /// proves it still holds with Unity's own colour type, sprite generation and
    /// PlayerPrefs underneath it.
    ///
    /// The ones that only live here are the ones that need a real engine: a
    /// generated sprite has to actually differ from another generated sprite,
    /// and a Color32 has to actually round-trip.
    /// </summary>
    public class AccessTests
    {
        [SetUp]
        public void Setup()
        {
            Store.Load();
            Store.Data.legible = 0;
            Store.Data.motion = 0;
            Store.Data.light = 0;
        }

        [TearDown]
        public void TearDown() => Store.Data.legible = 0;

        // ---- the maths itself ------------------------------------------------

        [Test]
        public void ContrastIsTheStandardsOwnArithmetic()
        {
            // white on black is 21:1 by definition, and it is the one ratio that
            // can be checked without trusting anything else in this file
            Assert.AreEqual(21f, Access.Contrast(Color.white, Color.black), 0.01f);
            Assert.AreEqual(1f, Access.Contrast(Color.white, Color.white), 0.001f);

            // and it is symmetric: an ink on a ground is the same number as the
            // ground on the ink, which is why one lift fixes the vault caption
            // and the primary's black label at the same time
            Assert.AreEqual(Access.Contrast(Palette.Rust, Palette.Void),
                            Access.Contrast(Palette.Void, Palette.Rust), 0.001f);
        }

        [Test]
        public void RelativeLuminanceIsNotThePalettesLuminance()
        {
            // These are two different functions doing two different jobs and the
            // whole file depends on not confusing them: Palette's is a straight
            // weighted sum of the sRGB values, which is what the depth ramps were
            // authored against; this one linearises first, which is what makes a
            // ratio computed from it mean "can a human read this".
            // AreNotEqual takes no tolerance — its third argument is the message —
            // so the distance is the thing to assert. On rust they are 0.445 and
            // 0.245, which is not a rounding difference, it is a different answer.
            Assert.Greater(Mathf.Abs(Palette.Luminance(Palette.Rust) - Access.Relative(Palette.Rust)), 0.02f,
                           "the two luminances have converged, so one of them is now wrong");
        }

        // ---- what a dichromat sees -------------------------------------------

        [Test]
        public void ATraceIsBrighterThanTheLatticeToEveryEye()
        {
            // The claim the whole board is read off. The worst pair is the
            // FARTHEST trace against the NEAREST lattice, because that is where
            // the two ramps come closest — so every depth is checked against the
            // brightest lattice there is.
            foreach (int legible in new[] { 0, 1 })
            {
                Store.Data.legible = legible;
                string mode = legible == 0 ? "shipped" : "legible";

                foreach (var eye in Access.AllSight)
                    for (int n = 2; n <= 9; n++)
                        for (int d = 0; d < n; d++)
                        {
                            float trace = Access.Relative(Access.Simulate(Palette.Tile('+', d, n), eye));
                            float lattice = Access.Relative(Access.Simulate(Palette.Tile('#', n - 1, n), eye));
                            Assert.Greater(trace, lattice,
                                mode + "/" + eye + ": a trace at depth " + d + " of " + n
                                + " is not brighter than the nearest lattice");
                        }
            }
        }

        [Test]
        public void SimulationLeavesATrichromatAlone()
        {
            Color c = Palette.Node;
            Color same = Access.Simulate(c, Access.Sight.Trichromat);
            Assert.AreEqual(c.r, same.r, 1e-5f);
            Assert.AreEqual(c.g, same.g, 1e-5f);
            Assert.AreEqual(c.b, same.b, 1e-5f);

            // and it does something to everybody else, or the whole check above
            // is comparing a colour with itself four times
            Color deutan = Access.Simulate(Palette.Node, Access.Sight.Deutan);
            Assert.Greater(Mathf.Abs(c.r - deutan.r), 0.01f,
                           "deuteranopia left the node unchanged, so the simulation is a no-op "
                           + "and every band check above is comparing a colour with itself");
        }

        // ---- what the interface prints ---------------------------------------

        [Test]
        public void LegibilityPutsEveryPrintedPairAtItsFloor()
        {
            Store.Data.legible = 1;
            foreach (var p in Access.Printed)
            {
                float r = Access.Contrast(Access.Named(p.ink), Access.Named(p.ground));
                Assert.GreaterOrEqual(r, p.floor,
                    p.ink + " on " + p.ground + " is " + r.ToString("0.00") + ":1 — " + p.why);
            }
        }

        [Test]
        public void TheDefaultIsStillTheShippedBuild()
        {
            // Legibility is a mode, and the argument for it being a mode is that
            // the default stays the picture that was matched to the APK. That
            // holds for every value here EXCEPT the three the audit found under
            // AA — Dim, Dim2 and the near lattice. AAA behind a mode is a
            // shipping choice; AA in the default is not one, and matching a
            // reference that fails it only reproduces the failure. Each moved by
            // the smallest step at its own hue and saturation that clears the
            // bar. Nothing else in this project would notice a palette drifting
            // one step at a time, which is why this test is here at all.
            Store.Data.legible = 0;
            Assert.AreEqual("EA580C", Hex(Palette.Rust));
            Assert.AreEqual("F97316", Hex(Palette.Core));
            Assert.AreEqual("EF4444", Hex(Palette.Fault));
            Assert.AreEqual("75859B", Hex(Palette.Dim));
            Assert.AreEqual("516888", Hex(Palette.Dim2));
            Assert.AreEqual("313C4D", Hex(Palette.LatticeNear));
            Assert.AreEqual(17, Access.SmallestType);
        }

        /// <summary>Six hex digits, case folded — Unity's case is not worth depending on.</summary>
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c).ToUpperInvariant();

        [Test]
        public void NoRoleIsSilentlyMisspelt()
        {
            // Named answers magenta rather than black for a role it does not
            // know, because black would quietly PASS a contrast check against a
            // light ground and a typo would look like a fix.
            Assert.AreEqual(Color.magenta, Access.Named("not a colour"));
        }

        // ---- colour is never the only channel --------------------------------

        [Test]
        public void TheFourObjectsAreToldApartByShape()
        {
            // Node and Lock are 1.02:1 against each other — they are all but
            // identical in luminance, and to a deuteranope they are neighbours in
            // hue as well. Nothing is wrong with that, because neither is ever
            // drawn as a bare colour: each is a MARK, and the marks are what the
            // manual teaches. This is the assertion that keeps that true.
            string[] roles = { "node", "lock", "core", "plate" };
            for (int i = 0; i < roles.Length; i++)
                for (int j = i + 1; j < roles.Length; j++)
                    Assert.AreNotSame(Glyphs.For(roles[i]), Glyphs.For(roles[j]),
                        roles[i] + " and " + roles[j] + " share a glyph, so only colour tells them apart");

            // and the same role twice is the same sprite, or the cache is broken
            // and every cell is paying for a texture
            Assert.AreSame(Glyphs.For("node"), Glyphs.For("node"));
        }

        // ---- what a hand has to do -------------------------------------------

        [Test]
        public void EveryControlHeightClearsTheTapTarget()
        {
            // 2.5.5 at AAA: 44 CSS px on the shortest side, which is 88 units at
            // this canvas's two-to-one. These are the three heights every
            // pressable thing in the game is built from.
            Assert.GreaterOrEqual(UiKit.BtnH, Access.TapTarget);
            Assert.GreaterOrEqual(UiKit.PrimaryH, Access.TapTarget);
            Assert.GreaterOrEqual(UiKit.RowH, Access.TapTarget);
        }

        [Test]
        public void StillIsZeroAndNoneIsNone()
        {
            Store.Data.motion = (int)Access.MotionLevel.Still;
            Assert.AreEqual(0f, Access.MotionAmount);

            Store.Data.light = (int)Access.LightLevel.None;
            Assert.AreEqual(0f, Access.LightAmount);
            Assert.IsFalse(Access.Light);
            Assert.IsFalse(Access.Bloom);

            // a corrupt save must not become a fourth level
            Store.Data.motion = 99;
            Assert.AreEqual(Access.MotionLevel.Still, Access.Motion);
            Store.Data.motion = -1;
            Assert.AreEqual(Access.MotionLevel.Full, Access.Motion);
        }

        [Test]
        public void TheFlashBudgetIsTheStandards()
        {
            Assert.LessOrEqual(Access.FlashesPerSecond, 3);
        }
    }
}
