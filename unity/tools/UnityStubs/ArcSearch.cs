using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// AUTHORING BY CONSTRAINT — four cubes that provably teach eversion.
///
/// A mechanic is not a feature you add, it is an argument you make one cube at a
/// time, and the argument only works if each cube CANNOT be solved by ignoring
/// the lesson. A hand-placed cube that happens to be solvable the old way teaches
/// nothing and the designer never finds out.
///
/// So the cubes are searched for rather than drawn. Take a minted solid, put an
/// everter on one of its trace cells, and ask the solver two questions — once
/// with the everter, once with that same cell as ordinary trace. The difference
/// between the two answers IS the lesson, and it is proved rather than intended.
///
///   1  THERE IS ANOTHER BOARD    unsolvable without everting, solvable with
///   2  CHOOSE THE SHADOW         solvable either way, strictly cheaper everted
///   3  THE SHADOW BETRAYS YOU    a square you can stand on until you evert
///   4  THE SYSTEMS INTERLOCK     a fold that is legal in one polarity only
///
/// That order is not arbitrary. Introduce, complicate, bound, then invert — the
/// mechanic is shown to exist, shown to be a choice, shown to have a cost, and
/// then shown to interact with the rule the player already had.
/// </summary>
static class ArcSearch
{
    public static void Run()
    {
        Console.WriteLine("searching minted solids for cubes that cannot be solved without eversion");
        Console.WriteLine();

        var found = new Dictionary<int, string>();
        var codes = new Dictionary<int, string>();
        int examined = 0, placements = 0;

        // BUDGETED, because the honest search is 400 solids by 35 placements by
        // three solves and that is forty thousand searches over a 24-orientation,
        // eight-world graph. Small cubes only — a lesson wants to be readable in
        // one glance, and a five-cube that forces eversion is a better teacher
        // than a nine-cube that merely allows it.
        const int Budget = 12;

        for (int level = 11; level <= 160 && found.Count < 4; level++)
        {
            Level src = Generator.Mint(level);
            if (src == null || src.n > 6) continue;
            examined++;

            SolveResult plain = Solver.Solve(src, 12);
            if (!plain.ok) continue;

            int tried = 0;
            for (int i = 0; i < src.vox.Length && tried < Budget; i++)
            {
                if (src.vox[i] != '+') continue;
                if (src.Vidx(src.start) == i || src.Vidx(src.goal) == i) continue;
                tried++; placements++;

                Level lv = src.Clone();
                lv.vox[i] = 'E';
                lv.ClearEff();
                SolveResult with = Solver.Solve(lv, 12);
                if (!with.ok) continue;

                Level flat = lv.Clone();
                flat.vox[i] = '+';
                flat.ClearEff();
                SolveResult without = Solver.Solve(flat, 12);

                // LESSON ONE HAS TO BE SCULPTED, NOT SPRINKLED. Dropping an
                // everter onto a cube that already works cannot make it
                // impossible without one — so if the control still solves, take
                // trace away until only the everted route survives. That is what
                // a puzzle author actually does: remove everything the intended
                // solution does not need, and what is left can only be solved the
                // intended way.
                if (without.ok && !found.ContainsKey(1))
                {
                    Level carved = Sculpt(lv);
                    if (carved != null)
                    {
                        found[1] = Describe(1, level, i, Solver.Solve(Flatten(carved), 12),
                                            Solver.Solve(carved, 12));
                        Console.WriteLine(found[1]);
                        codes[1] = ShareCode.Encode(carved);
                        Console.WriteLine("     " + codes[1]);
                    }
                }

                int lesson = Classify(lv, with, without);
                if (lesson == 0 || found.ContainsKey(lesson)) continue;

                found[lesson] = Describe(lesson, level, i, without, with);
                codes[lesson] = ShareCode.Encode(lv);
                Console.WriteLine(found[lesson]);
                Console.WriteLine("     " + codes[lesson]);
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("examined " + examined + " solids, " + placements + " everter placements, found "
                          + found.Count + " of the 4 lessons");
        Console.WriteLine();
        Emit(codes);
    }

    /// <summary>
    /// Which lesson, if any, this cube is forced to teach. The strongest claim
    /// wins: a cube that is impossible without the mechanic is a better first
    /// lesson than one that is merely cheaper with it.
    /// </summary>
    static int Classify(Level lv, SolveResult with, SolveResult without)
    {
        // 1. UNSOLVABLE WITHOUT IT.
        if (!without.ok) return 1;

        // 2. CHEAPER EVERTED — a decision rather than a gate.
        if (with.turns < without.turns) return 2;

        // 3. THE FLOOR CAN GO. Somewhere on the starting board is a square that
        //    is walkable now and is not walkable once everted.
        if (LosesFooting(lv)) return 3;

        // 4. A FOLD THAT NEEDS A POLARITY. From the start, a fold that is refused
        //    upright and permitted everted, or the other way round.
        if (PolarityGatesAFold(lv)) return 4;

        return 0;
    }

    static readonly string[] ArcNames =
    {
        "", "THE FAR SIDE", "TWO SHADOWS", "NOTHING UNDERNEATH", "TURNED INSIDE OUT"
    };

    /// <summary>
    /// Print the four cubes as C# ready to paste into Baked. A share code is the
    /// right thing to carry them out of the search — it is already the format a
    /// made cube is stored in, and it is checksummed — but an AUTHORED cube
    /// belongs in source where a person can read it and a diff can show it moving.
    /// </summary>
    static void Emit(Dictionary<int, string> codes)
    {
        Console.WriteLine("// ---- paste into Baked.cs -------------------------------------------");
        for (int i = 1; i <= 4; i++)
        {
            if (!codes.TryGetValue(i, out string code)) continue;
            Level lv = ShareCode.Decode(code);
            if (lv == null) { Console.WriteLine("// lesson " + i + " failed to decode"); continue; }
            SolveResult r = Solver.Solve(lv, 12);
            Console.WriteLine(string.Format(
                "new BakedLevel({0}, \"{1}\", {2}, \"{3}\",", lv.n, ArcNames[i], r.turns, new string(lv.vox)));
            Console.WriteLine(string.Format("    new Int3({0}, {1}, {2}), new Int3({3}, {4}, {5}),",
                lv.start.x, lv.start.y, lv.start.z, lv.goal.x, lv.goal.y, lv.goal.z));
            Console.WriteLine("    " + Pts(lv.keys) + ", " + Pts(lv.doors) + "),");
        }
    }

    static string Pts(List<Int3> ps)
    {
        if (ps.Count == 0) return "new Int3[0]";
        var sb = new System.Text.StringBuilder("new[] { ");
        for (int i = 0; i < ps.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("new Int3(").Append(ps[i].x).Append(", ").Append(ps[i].y).Append(", ").Append(ps[i].z).Append(')');
        }
        return sb.Append(" }").ToString();
    }

    /// <summary>The same solid with every everter demoted to ordinary trace — the control.</summary>
    static Level Flatten(Level lv)
    {
        Level flat = lv.Clone();
        for (int i = 0; i < flat.vox.Length; i++) if (flat.vox[i] == 'E') flat.vox[i] = '+';
        flat.ClearEff();
        return flat;
    }

    /// <summary>
    /// REMOVE UNTIL ONLY THE INTENDED SOLUTION SURVIVES.
    ///
    /// Take trace cells away one at a time, keeping every removal that leaves the
    /// cube solvable WITH the everter. Stop the moment the control — the same
    /// solid with the everter demoted to plain trace — stops solving at all. What
    /// is left is a cube that cannot be finished by ignoring the mechanic, which
    /// is the only thing a first lesson may be.
    ///
    /// Start, core, nodes and locks are never touched; removing one of those
    /// would change the puzzle rather than tighten it.
    /// </summary>
    static Level Sculpt(Level lv)
    {
        Level cur = lv.Clone();
        for (int i = 0; i < cur.vox.Length; i++)
        {
            if (cur.vox[i] != '+') continue;
            if (cur.Vidx(cur.start) == i || cur.Vidx(cur.goal) == i) continue;
            bool reserved = false;
            foreach (Int3 k in cur.keys) if (cur.Vidx(k) == i) reserved = true;
            foreach (Int3 d in cur.doors) if (cur.Vidx(d) == i) reserved = true;
            if (reserved) continue;

            Level cut = cur.Clone();
            cut.vox[i] = '#';
            cut.ClearEff();
            if (!Solver.Solve(cut, 12).ok) continue;     // the intended route needs it

            cur = cut;
            if (!Solver.Solve(Flatten(cur), 12).ok) return cur;
        }
        return null;
    }

    static bool LosesFooting(Level lv)
    {
        int n = lv.n;
        Surf[] a = Projection.Project(n, lv.Eff(0), Turns.Oris[0]);
        Surf[] b = Projection.Project(n, lv.Eff(Level.Everted), Turns.Oris[0]);
        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
                if (Projection.Walkable(lv, a, u, v, 0) && !Projection.Walkable(lv, b, u, v, 0))
                    return true;
        return false;
    }

    static bool PolarityGatesAFold(Level lv)
    {
        Ori m = Turns.Oris[0];
        for (int t = 0; t < 4; t++)
        {
            Ori m2 = Turns.All[t].f(m);
            bool up = Projection.Landing(lv, m2, lv.start, 0, 0, out _);
            bool ev = Projection.Landing(lv, m2, lv.start, 0, Level.Everted, out _);
            if (up != ev) return true;
        }
        return false;
    }

    static string Describe(int lesson, int level, int cell, SolveResult plain, SolveResult with)
    {
        string[] name =
        {
            "", "THERE IS ANOTHER BOARD", "CHOOSE THE SHADOW",
            "THE SHADOW BETRAYS YOU", "THE SYSTEMS INTERLOCK"
        };
        return string.Format("  {0}. {1,-24} from solid {2,3}, everter at cell {3,3}   "
                             + "par {4} -> {5}", lesson, name[lesson], level, cell, plain.turns, with.turns);
    }
}
