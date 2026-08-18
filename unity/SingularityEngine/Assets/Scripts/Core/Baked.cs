using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>One of the ten authored cubes of VAULT I.</summary>
    public readonly struct BakedLevel
    {
        public readonly int n;
        public readonly string name;
        public readonly int par;
        public readonly string vox;
        public readonly Int3 start, goal;
        public readonly Int3[] keys, doors;

        /// <summary>
        /// THE OTHER FACE, and null on every cube that does not turn.
        ///
        /// An everter is a second array of types over the same cells — see
        /// Level.LayoutOf — so a cube that teaches one cannot be written down as a
        /// single voxel string. The four at 146-149 could not be stored at all
        /// until this existed: the search emitted them, the paste block dropped
        /// their other half, and every lesson they were found for evaporated.
        /// </summary>
        public readonly string voxB;

        public BakedLevel(int n, string name, int par, string vox, Int3 start, Int3 goal,
                          Int3[] keys, Int3[] doors, string voxB = null)
        {
            this.n = n; this.name = name; this.par = par; this.vox = vox;
            this.start = start; this.goal = goal; this.keys = keys; this.doors = doors;
            this.voxB = voxB;
        }

        public Level ToLevel(int level)
        {
            var lv = new Level
            {
                n = n,
                vox = vox.ToCharArray(),
                start = start,
                goal = goal,
                keys = new List<Int3>(keys),
                doors = new List<Int3>(doors),
                par = par,
                level = level,
                voxB = voxB?.ToCharArray(),
                band = Vaults.VaultOf(level),
                name = name,
                authored = true
            };
            return lv;
        }
    }

    /// <summary>
    /// VAULT I is authored: ten cubes, verified offline, carrying the teaching
    /// beats. The first two show you a way out you cannot walk to, which is the
    /// entire lesson and is not something a generator can be trusted to stage.
    ///
    /// VAULT IX is four more, and they exist because of a measurement. The
    /// content audit shows par flatlining at 5.8 folds from about cube 100 — by
    /// 151 every generation parameter is at its ceiling, so the ladder stops
    /// getting harder and only gets bigger. A new idea has to arrive there or
    /// nothing does, and a new idea has to be TAUGHT: one cube at a time, by
    /// cubes that cannot be solved while ignoring it.
    ///
    /// Everything else is cut on demand by <see cref="Generator.Mint"/>.
    /// </summary>
    public static class Baked
    {
        /// <summary>
        /// The head of THE FAR SIDE, where the everter is introduced.
        ///
        /// This was 146 — the first four of a vault IX that no longer exists. The
        /// ladder is ten chapters of fifteen now and the everter's chapter opens
        /// at ninety-one, so that is where the four cubes that teach it go. A
        /// teaching cube in the wrong chapter is a cube teaching a mechanic the
        /// player met sixty levels ago.
        /// </summary>
        public const int ArcStart = 91;

        /// <summary>
        /// EVERSION, IN FOUR CUBES. Introduce, complicate, bound, invert.
        ///
        /// Not drawn — SEARCHED, and each one is PROVED to require its lesson by
        /// solving the same solid twice: once with the everter, once with that
        /// cell demoted to ordinary trace. The difference between the two answers
        /// is the lesson. See unity/tools/UnityStubs/ArcSearch.cs, and the
        /// harness re-proves all four on every run — a cube that stops teaching
        /// its lesson is a failed check rather than a thing nobody notices.
        ///
        ///   THE FAR SIDE          no route at all without everting
        ///   TWO SHADOWS           a route either way, one fold cheaper everted
        ///   NOTHING UNDERNEATH    a square you can stand on until you evert
        ///   TURNED INSIDE OUT     a fold that only one polarity permits
        ///
        /// THE FIRST WAS SCULPTED RATHER THAN PLACED. Dropping an everter onto a
        /// working cube cannot make it impossible without one, so trace was
        /// removed — one cell at a time, keeping every cut that left the everted
        /// route intact — until the un-everted route died. That is what authoring
        /// a puzzle actually is: taking away everything the intended solution
        /// does not need.
        /// </summary>
        public static readonly BakedLevel[] Arc =
        {
            new BakedLevel(6, "THE FAR SIDE", 2, "#.#.#.E.##..#..#..#+#.+.+.##..+..###+.####...+#.#+....####.+.#..++#.++.#++#.##++.#.###.##...###+##+..+...#....#...#..####..#.+..+#..##.#+.#...#+#..##.#####..#.#....##.#..#####.####..###..##.######...#.##...#####.##.#",
                new Int3(2, 3, 3), new Int3(0, 0, 4),
                new Int3[0], new Int3[0],
                "#.+.#.E.+#..+..#..+##.#.+.+#..#..####.####...##.#+....####.#.#..###.+#.####.###+.#.###.##...######+..#...+....#...#..####..#.#..+#..##.+#.#...###..##.#+++#..+.#....##.#..#####.+###..++#..+#.###+++...#.##...#####.##.#"),
            new BakedLevel(6, "TWO SHADOWS", 2, "###.#..#.##+...##....##.......###....#.####.#.###.####.######..#++..#.......+..#...#.##.#####.#+...+..#..++.+#.####..###.#..##.###.#+##.#+.#..+#....+.++##.+..##.###.+.##.#.+.#.#.E.+.#.+.#.##.+.....####.++#+....##..++",
                new Int3(4, 3, 5), new Int3(0, 3, 0),
                new Int3[0], new Int3[0],
                "###.#..#.###...##....++.......###....#.####.#.###.+##+.#+##+#..+++..#.......#..+...#.+#.##+##.##...#..#..##.++.####..###.#..##.##+.####.##.#..++....#.####.#..##.###.+.##.#.#.#.#.E.#.#.#.#.##.#.....####.####....##..+#"),
            new BakedLevel(6, "NOTHING UNDERNEATH", 2, "+.#.+.E.##..#..#..++#.+.+.##..+..###+.####...+#.#+....####.+.#..++#.++.#++#.##++.#.###.##...###+##+..+...#....#...#..####..#.+..+#..##.#+.#...#+#..##.#####..#.#....##.#..#####.####..###..##.######...#.##...#####.##.#",
                new Int3(2, 3, 3), new Int3(0, 0, 4),
                new Int3[0], new Int3[0],
                "#.+.#.E.+#..+..#..+##.#.+.+#..#..####.####...##.#+....####.#.#..###.+#.####.###+.#.###.##...######+..#...+....#...#..####..#.#..+#..##.+#.#...###..##.#+++#..+.#....##.#..#####.+###..++#..+#.###+++...#.##...#####.##.#"),
            new BakedLevel(6, "TURNED INSIDE OUT", 2, ".+++++.####+.+#..#....##+..###+.###.#...#+#.#+..+###.E...+#+.+.+....+..##...##..###.....#..+#...+.#.##+++#.#.##.....###.###.######.##.##...+..###.###.##.##...#..###.##.#.###.###.#..##..###..#...#####+###.##.####..###",
                new Int3(3, 1, 4), new Int3(3, 0, 0),
                new Int3[0], new Int3[0],
                ".##++#.###++.##..+....++#..##+#.###.#...+##.#+..####.E...###.#.+....#..##...++..#++.....+..##...#.#.######.#.##.....###.###.+#####.##.##...#..###.###.##.##...#..###.##.#.###.###.#..##..###..#...#########.##.####..###")
        };

        /// <summary>
        /// The authored cube for a level number, if there is one. A lookup rather
        /// than an index, because authored cubes no longer live only at the front
        /// of the ladder and the next arc will not either.
        /// </summary>
        public static bool TryAt(int level, out BakedLevel lv)
        {
            if (level >= 1 && level <= Levels.Length) { lv = Levels[level - 1]; return true; }
            if (level >= ArcStart && level < ArcStart + Arc.Length) { lv = Arc[level - ArcStart]; return true; }
            lv = default;
            return false;
        }

        public static readonly BakedLevel[] Levels =
        {
            new BakedLevel(
                n: 5, name: "FOOTING", par: 1,
                vox: "......#..#..+..+##+#.#..#...#.##..##.#...#..##+...#..#.#+..#.##.#.+.#.#+..###...#..##.#.###+...#........#....#+..#.#+.#+...+.",
                start: new Int3(1, 1, 4), goal: new Int3(4, 4, 3),
                keys: new Int3[0],
                doors: new Int3[0]),
            new BakedLevel(
                n: 5, name: "THE TURN", par: 1,
                vox: ".#.#.#..#.###.+#.#++#+#....##.#+.+##........###..+#........#.##...........+.###+...##.#.+#..#..+#...#.#.#.#.#.#.###.#.+..#.#.",
                start: new Int3(3, 1, 1), goal: new Int3(3, 3, 2),
                keys: new Int3[0],
                doors: new Int3[0]),
            new BakedLevel(
                n: 5, name: "BURIED", par: 2,
                vox: "#####..##.##.##.#..###.##.....####.##...#....++##.#.##..+###..#.#..###+...+##+.+##.+#..##.+#....##.+.#..#..+.+#..#.+......#.#",
                start: new Int3(1, 1, 4), goal: new Int3(2, 3, 0),
                keys: new Int3[0],
                doors: new Int3[0]),
            new BakedLevel(
                n: 5, name: "TWO FACES", par: 2,
                vox: "..#++.#.##.###+..##++#.+..##+...#.....+..#.+.+..++###.#.####+#......##.##.#.####++......+...#....+..#....#...###...#.#..+.#..",
                start: new Int3(1, 3, 1), goal: new Int3(3, 1, 0),
                keys: new Int3[0],
                doors: new Int3[0]),
            new BakedLevel(
                n: 6, name: "THE LONG WAY", par: 2,
                vox: "#...###..+...+.#.#..+#...+.#.###.##....++.+.#..#.##.###..###..+....#.+.#..#.+.....##...#.#...#.+.#.+#...#...#....######.+.####...#..#...###..##......#.##....###..#.##.....##.#..#.#...##..#.#...##..##.#...#+#.....##.#",
                start: new Int3(2, 1, 4), goal: new Int3(3, 0, 1),
                keys: new[] { new Int3(3, 2, 4) },
                doors: new[] { new Int3(2, 0, 3) }),
            new BakedLevel(
                n: 6, name: "TUMBLER", par: 3,
                vox: ".#.##.##.##..####...##.#.#....#.##.........##.##...#.#..#.####.....++..+.....##+.##.#.##.#..##.##....##.++#++#..#####..#.....#.##..+#....####....+.....+...#.+..#..+.....+..+#..#.#..###.#.#.#....####+.###.##.#.+.+....",
                start: new Int3(3, 2, 5), goal: new Int3(1, 4, 1),
                keys: new Int3[0],
                doors: new Int3[0]),
            new BakedLevel(
                n: 6, name: "WARDEN", par: 3,
                vox: "###...##.....##.+.##..##....#.#.##.###..#+###..##.#.#+.#...##+.#....#..##.###...#..#.#..#.+......##..#.+.#####.##.#..#.#....####.#..#..##.+#..#.+#....#.###...##...#..+.##..+..##.#+...#+.....##..#.++++#..+.##.++####.+",
                start: new Int3(1, 1, 4), goal: new Int3(4, 5, 4),
                keys: new[] { new Int3(0, 2, 3) },
                doors: new[] { new Int3(4, 4, 3) }),
            new BakedLevel(
                n: 6, name: "OUBLIETTE", par: 3,
                vox: "##+.......#.+#..#.+.....#.##+..##....#..+#+#.###+##.##++..+.####..###.##.#.####..#+...#..###+.#.##.#...#..#...#..#.+...#####.####+..##...###.+.....###........#####...#+.++#.+...+####.#.#...#...##.#.#.#.+.....+....#..",
                start: new Int3(3, 3, 5), goal: new Int3(0, 1, 3),
                keys: new[] { new Int3(1, 4, 4) },
                doors: new[] { new Int3(4, 1, 3) }),
            new BakedLevel(
                n: 6, name: "THE CANT", par: 3,
                vox: "##+.#.#....+...+###..+.#+..#....#....##+.+...#.+#.##.+....##.#...#.#.###...+##+.+###.#..#...#.##.#.......#####..#.##....#.#.##..#.#....+#++..+.....#...#####+.#.##.##.#..+.++.......#..#.#..#.#.##.###..#..#.....++..+..",
                start: new Int3(1, 4, 4), goal: new Int3(5, 0, 1),
                keys: new[] { new Int3(0, 5, 5) },
                doors: new[] { new Int3(3, 1, 0) }),
            new BakedLevel(
                n: 7, name: "SIX WAYS", par: 4,
                vox: "......+##..+.+...#+..#.#.......##..#+....#...#..#..#.#.+....#+.#...+..###.####..#.#####..#..##.#..##...###..#.##.#.#.++.###.#..#####..+###..##.....###.##..#.###.#.####..##..##..#....#.#..#...######...##..##..#..#.#.#....###.###....+...#...##.#+...+..+..#...#..##++.+..+.###.+....#++#...#..+.##.+.##.#..#..#..#...#...####..##.##...+##.#.+..#..#",
                start: new Int3(1, 5, 5), goal: new Int3(4, 1, 2),
                keys: new[] { new Int3(0, 6, 6) },
                doors: new[] { new Int3(6, 2, 2) }),
        };
    }
}
