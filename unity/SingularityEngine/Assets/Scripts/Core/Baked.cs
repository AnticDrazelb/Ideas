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

        public BakedLevel(int n, string name, int par, string vox, Int3 start, Int3 goal, Int3[] keys, Int3[] doors)
        {
            this.n = n; this.name = name; this.par = par; this.vox = vox;
            this.start = start; this.goal = goal; this.keys = keys; this.doors = doors;
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
    /// Everything from cube eleven is cut on demand by <see cref="Generator.Mint"/>.
    /// </summary>
    public static class Baked
    {
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
