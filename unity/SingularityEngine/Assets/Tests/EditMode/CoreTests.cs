using System.Collections.Generic;
using NUnit.Framework;
using Singularity.Core;

namespace Singularity.Tests
{
    /// <summary>
    /// The port's contract with the original.
    ///
    /// Every cube in this game is a pure function of its number, and that is not
    /// a nice property — it is load-bearing. It is the entire reason the daily
    /// works with no server, the reason a cube number can be typed into a text
    /// field and produce the same puzzle on someone else's phone, and the reason
    /// the baked catalogue of already-minted identities means anything at all.
    ///
    /// So these tests do not check that the engine is reasonable. They check that
    /// it is the SAME. The fixtures below were taken from the JavaScript original
    /// running against the same level numbers, and every one of them was verified
    /// byte-for-byte at port time; they are here so that the next person to touch
    /// the generator finds out immediately if they have quietly changed what cube
    /// 4,127 is.
    /// </summary>
    public class CoreTests
    {
        readonly struct Fixture
        {
            public readonly int level, par, steps, n;
            public readonly string id;
            public Fixture(int level, int par, int steps, int n, string id)
            { this.level = level; this.par = par; this.steps = steps; this.n = n; this.id = id; }
        }

        static readonly Fixture[] Fixtures =
        {
            new Fixture(11, 3, 3, 6, "brmN8qb8"),
            new Fixture(15, 3, 6, 6, "nvtffdG0"),
            new Fixture(19, 2, 6, 6, "bXJ6AF12"),
            new Fixture(20, 4, 5, 6, "ekDErnwF"),      // the first cube to carry a plate
            new Fixture(21, 2, 9, 6, "WvgBVWTo"),
            new Fixture(25, 2, 17, 6, "mj_L66uK"),
            new Fixture(31, 4, 9, 7, "D1eez7QI"),
            new Fixture(41, 4, 9, 7, "-0Y1i5rc"),      // both plate kinds are available from here
            new Fixture(48, 4, 15, 7, "56bZRLI6"),     // the daily's difficulty, forever
            new Fixture(55, 4, 15, 7, "o5qTMPue"),
            new Fixture(66, 4, 9, 7, "eESpYzqD"),
            new Fixture(81, 6, 7, 8, "muV5zZO6"),      // the size ladder reaches eight
            new Fixture(90, 4, 19, 8, "YyO8qG62"),     // the last authored-length vault
            new Fixture(100, 5, 19, 8, "9Fr3nMCS"),
            new Fixture(121, 6, 10, 8, "lI6C2MGY"),
            new Fixture(151, 5, 12, 9, "PsupahAN"),    // and nine
            new Fixture(500, 6, 12, 9, "rEAGbL8a"),
            new Fixture(4127, 7, 12, 9, "8VGD1ej4"),
        };

        // ---- the generator is the same generator ----------------------------

        [Test]
        public void MintedCubesMatchTheOriginal()
        {
            foreach (Fixture f in Fixtures)
            {
                Level lv = Generator.Mint(f.level);
                Assert.AreEqual(f.n, lv.n, "cube " + f.level + " changed size");
                Assert.AreEqual(f.par, lv.par, "cube " + f.level + " changed par");
                Assert.AreEqual(f.steps, lv.steps, "cube " + f.level + " changed route length");
                Assert.AreEqual(f.id, Identity.CanonId(lv), "cube " + f.level + " is a different cube");
            }
        }

        [Test]
        public void MintIsDeterministic()
        {
            foreach (int level in new[] { 11, 37, 48, 151, 4127 })
            {
                string a = new string(Generator.Mint(level).vox);
                string b = new string(Generator.Mint(level).vox);
                Assert.AreEqual(a, b, "cube " + level + " is not a pure function of its number");
            }
        }

        [Test]
        public void RngMatchesMulberry32()
        {
            // the first eight draws from seed 12345, taken from the original
            double[] want =
            {
                0.9797282677609473, 0.3067522644996643, 0.484205421525985, 0.817934412509203,
                0.5094283693470061, 0.34747186047025025, 0.07375754183158278, 0.7663964673411101
            };
            var rng = Rng.Mulberry32(12345u);
            for (int i = 0; i < want.Length; i++)
                Assert.AreEqual(want[i], rng.Next(), 0d, "draw " + i + " diverged");
        }

        [Test]
        public void HashSeedMatches()
        {
            Assert.AreEqual(1228498187u, Rng.HashSeed(1));
            Assert.AreEqual(3709338170u, Rng.HashSeed(2));
            Assert.AreEqual(103210291u, Rng.HashSeed(5));
        }

        // ---- the promise attached to every cube -----------------------------

        [Test]
        public void NoCubeReachesThePlayerUnwinnable()
        {
            // This is the one guarantee the whole supply rests on, and it is
            // structural rather than a filter: solvability is CONSTRUCTED by the
            // carve and then proved by the solver before the cube is handed over.
            for (int level = 11; level <= 60; level++)
            {
                Level lv = Generator.Mint(level);
                SolveResult r = Solver.Solve(lv, 60);
                Assert.IsTrue(r.ok, "cube " + level + " is a lie");
                Assert.AreEqual(lv.par, r.turns, "cube " + level + " claims a par it cannot meet");
            }
        }

        [Test]
        public void TheFallbackNeverActuallyFires()
        {
            // The fallback exists so that "every cube has been proved winnable"
            // has no exceptions. It is deliberately boring, and it should never be
            // what a player is handed.
            for (int level = 11; level <= 60; level++)
                Assert.IsFalse(Generator.Mint(level).fallback, "cube " + level + " fell through to the fallback");
        }

        [Test]
        public void EveryAuthoredCubeSolvesAtItsClaimedPar()
        {
            for (int i = 0; i < Baked.Levels.Length; i++)
            {
                Level lv = Baked.Levels[i].ToLevel(i + 1);
                SolveResult r = Solver.Solve(lv, 60);
                Assert.IsTrue(r.ok, Baked.Levels[i].name + " does not solve");
                Assert.AreEqual(Baked.Levels[i].par, r.turns, Baked.Levels[i].name + " is not the par it claims");
            }
        }

        // ---- the projection model -------------------------------------------

        [Test]
        public void ViewAndWorldAreExactRoundTrips()
        {
            for (int n = 5; n <= 9; n++)
                foreach (Ori m in Turns.Oris)
                    for (int y = 0; y < n; y++)
                        for (int z = 0; z < n; z++)
                            for (int x = 0; x < n; x++)
                            {
                                var p = new Int3(x, y, z);
                                Int3 back = Projection.WorldOf(n, m, Projection.ViewOf(n, m, p));
                                Assert.AreEqual(p, back, "round trip failed at " + p + " in orientation " + m.Key);
                            }
        }

        [Test]
        public void ThereAreExactlyTwentyFourOrientations()
        {
            Assert.AreEqual(24, Turns.Oris.Length);
            var seen = new HashSet<string>();
            foreach (Ori m in Turns.Oris) Assert.IsTrue(seen.Add(m.Key), "duplicate orientation");
        }

        [Test]
        public void EveryFoldIsLegalWhileStandingOnAPlate()
        {
            // A plate is always the surface of its column, cut clean through the
            // rock. That is what makes plates PIVOTS: in a game where where you
            // stand decides which folds you have, a square that gives you all four
            // is worth walking a long way for. If this ever stops being true the
            // mechanic quietly stops working and nothing else would notice.
            int checkedPlates = 0;
            for (int level = 20; level <= 70; level++)
            {
                Level lv = Generator.Mint(level);
                for (int i = 0; i < lv.vox.Length; i++)
                {
                    if (!Level.IsGlyph(lv.vox[i])) continue;
                    int n = lv.n;
                    int y = i / (n * n), rr = i - y * n * n, z = rr / n, x = rr - z * n;
                    var cell = new Int3(x, y, z);

                    for (int world = 0; world < 4; world++)
                        foreach (Ori m in Turns.Oris)
                            for (int t = 0; t < 4; t++)
                            {
                                Ori m2 = Turns.All[t].f(m);
                                Assert.IsTrue(
                                    Projection.Landing(lv, m2, cell, ~0, world, out _),
                                    "cube " + level + ": a fold was refused while standing on a plate");
                            }
                    checkedPlates++;
                }
            }
            Assert.Greater(checkedPlates, 0, "no plates were found to check");
        }

        // ---- identity and share codes ---------------------------------------

        [Test]
        public void ACubeFoldedIsTheSameCube()
        {
            // A cube's identity is the smallest of the twenty-four ways it can be
            // written down, so rebuilding one lying on its side is still rebuilding
            // the same cube — and a plain byte compare would not notice.
            Level lv = Generator.Mint(37);
            string id = Identity.CanonId(lv);
            foreach (Ori m in Turns.Oris)
            {
                Level rotated = Rotate(lv, m);
                Assert.AreEqual(id, Identity.CanonId(rotated), "identity is not fold-invariant");
            }
        }

        static Level Rotate(Level lv, Ori m)
        {
            int n = lv.n;
            var vox = new char[n * n * n];
            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        Int3 v = Projection.ViewOf(n, m, new Int3(x, y, z));
                        vox[Level.Vidx(n, v)] = lv.vox[Level.Vidx(n, x, y, z)];
                    }

            var outp = new Level { n = n, vox = vox, par = lv.par };
            outp.start = Projection.ViewOf(n, m, lv.start);
            outp.goal = Projection.ViewOf(n, m, lv.goal);
            foreach (Int3 k in lv.keys) outp.keys.Add(Projection.ViewOf(n, m, k));
            foreach (Int3 d in lv.doors) outp.doors.Add(Projection.ViewOf(n, m, d));
            return outp;
        }

        [Test]
        public void ShareCodesRoundTrip()
        {
            foreach (int level in new[] { 11, 20, 41, 55 })
            {
                Level lv = Generator.Mint(level);
                Level back = ShareCode.Decode(ShareCode.Encode(lv));
                Assert.IsNotNull(back, "cube " + level + " failed to decode");
                Assert.AreEqual(new string(lv.vox), new string(back.vox));
                Assert.AreEqual(lv.start, back.start);
                Assert.AreEqual(lv.goal, back.goal);
                CollectionAssert.AreEqual(lv.keys, back.keys);
                CollectionAssert.AreEqual(lv.doors, back.doors);
            }
        }

        [Test]
        public void AMistypedShareCodeIsRefused()
        {
            // The checksum is the whole point: a code that has lost a character
            // must fail rather than decode into a different, probably broken cube.
            Assert.IsNull(ShareCode.Decode("AAAAAAAAAAAA"));
            Assert.IsNull(ShareCode.Decode(""));
            Assert.IsNull(ShareCode.Decode("not a code at all"));

            string good = ShareCode.Encode(Generator.Mint(11));
            Assert.IsNotNull(ShareCode.Decode(good));
            Assert.IsNull(ShareCode.Decode(good.Substring(0, good.Length - 1)), "a truncated code decoded");
        }

        [Test]
        public void SmartPunctuationInAPastedCodeIsRepaired()
        {
            // The alphabet contains a hyphen and the world is full of things that
            // "helpfully" replace one.
            string good = ShareCode.Encode(Generator.Mint(20));
            string mangled = good.Replace("-", "—");        // em dash
            Assert.IsNotNull(ShareCode.Decode(mangled), "an em-dashed code was thrown away");
        }

        // ---- the vault ladder ------------------------------------------------

        [Test]
        public void VaultBoundariesAreConsistentBothWays()
        {
            for (int band = 0; band < 40; band++)
            {
                int start = Vaults.VaultStart(band);
                Assert.AreEqual(band, Vaults.VaultOf(start), "vault " + band + " does not contain its own first cube");
                Assert.AreEqual(band, Vaults.VaultOf(start + Vaults.VaultSize(band) - 1), "vault " + band + " does not contain its own last cube");
                if (band > 0)
                    Assert.AreEqual(band - 1, Vaults.VaultOf(start - 1), "a gap before vault " + band);
            }
        }

        [Test]
        public void GeneratedVaultNamesUseTheWholeWordList()
        {
            // The stride has to be coprime with the list length or most of the list
            // never appears — this was band*5 against ten words, so every generated
            // vault was called REACH or WORKS.
            var seen = new HashSet<string>();
            for (int band = Vaults.Authored.Length; band < Vaults.Authored.Length + 40; band++)
                seen.Add(Vaults.VaultName(band));
            Assert.Greater(seen.Count, 30, "the generated vault names repeat far too soon");
        }

        [Test]
        public void LevelNamesAreNeverBlank()
        {
            // hashSeed is unsigned 32-bit, and a signed shift here indexed
            // negatively and named half the cubes past level 40 "undefined".
            for (int level = 1; level <= 400; level++)
            {
                string name = Vaults.LevelName(level);
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), "cube " + level + " has no name");
                Assert.IsFalse(name.Contains("undefined"), "cube " + level + " is named badly");
            }
        }

        // ---- the Forge's standard -------------------------------------------

        [Test]
        public void ValidationRejectsACorridor()
        {
            // A cube you can finish without folding is a corridor. The generator
            // can never produce one; the Forge has no floor of its own, and par is
            // the whole scoring system, so a par of zero makes every clear worse
            // than perfect and the win card reads as nonsense.
            Level corridor = Generator.FallbackLevel(1);
            Verdict v = Validation.Validate(corridor);
            Assert.IsFalse(v.ok);
            Assert.IsTrue(v.why.Contains("CORRIDOR"));
        }

        [Test]
        public void ValidationAcceptsEveryGeneratedCubeWithoutAPlate()
        {
            // A hand-built cube is held to exactly the standard a generated one is,
            // so every generated one has to clear the hand-built checks —
            //
            // UP TO THE POINT WHERE PLATES ARRIVE, AND NOT PAST IT. This looks like
            // a hole and is not one, and it is worth writing down because it caught
            // this port's own test suite out.
            //
            // Validation is the FORGE's validator, and the Forge authors in world
            // zero: it asks whether the exit is on a trace, meaning a '+' in the
            // cube as written down. The carve does not work that way. Once it drops
            // a plate it keeps carving in the world that plate opened, where footing
            // is spelled '#' — so the exit of a plate cube is very often a '#' in
            // the base representation and perfectly walkable in the world the route
            // actually arrives through.
            //
            // The original never notices because it never runs validateLevel over a
            // generated cube. The guarantee that matters for those is the one below
            // and above: the solver proves every one of them winnable before it is
            // handed over.
            for (int level = 11; level < Generator.GlyphFrom; level++)
            {
                Verdict v = Validation.Validate(Generator.Mint(level));
                Assert.IsTrue(v.ok, "cube " + level + " fails the Forge's own standard: " + v.why);
            }
        }

        [Test]
        public void APlateCubeCarriesItsExitInAFlippedWorld()
        {
            // The other half of the note above, asserted rather than assumed: if
            // this ever stops being true, the carve has stopped continuing past the
            // plate, and the plate has gone back to being a footnote on the last leg.
            bool anyFlipped = false;
            for (int level = Generator.GlyphFrom; level <= 40; level++)
            {
                Level lv = Generator.Mint(level);
                char goal = lv.vox[Level.Vidx(lv.n, lv.goal)];
                if (goal == '#') anyFlipped = true;
                Assert.IsTrue(Solver.Solve(lv, 60).ok, "cube " + level + " is not winnable");
            }
            Assert.IsTrue(anyFlipped, "no plate cube routed through a flipped world");
        }

        // ---- the daily -------------------------------------------------------

        [Test]
        public void TheDailyIsTheSameCubeForEverybodyOnTheSameDay()
        {
            const int day = 20500;
            uint seed = Daily.DailySeed(day);
            Level a = Generator.Mint(Daily.SpecLevel, seed);
            Level b = Generator.Mint(Daily.SpecLevel, seed);
            Assert.AreEqual(new string(a.vox), new string(b.vox));
            Assert.AreNotEqual(seed, Daily.DailySeed(day + 1), "two days share a cube");
        }

        [Test]
        public void TheDayBoundaryIsGlobalNotLocal()
        {
            // A shared ranking of "today's cube" needs everyone to agree on which
            // day it is, so the boundary is one instant everywhere: midnight UTC-7.
            long midnight = 20500L * 86400000L + Daily.DayAnchorMs;
            Assert.AreEqual(20500, Daily.DayIndexAt(midnight));
            Assert.AreEqual(20500, Daily.DayIndexAt(midnight + 86399999L));
            Assert.AreEqual(20501, Daily.DayIndexAt(midnight + 86400000L));
        }
    }
}
