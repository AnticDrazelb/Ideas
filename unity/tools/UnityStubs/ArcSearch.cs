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
        int examined = 0, placements = 0, nullMint = 0, nullAlts = 0;

        // BUDGETED, because the honest search is 400 solids by 35 placements by
        // three solves and that is forty thousand searches over a 24-orientation,
        // eight-world graph. Small cubes only — a lesson wants to be readable in
        // one glance, and a five-cube that forces eversion is a better teacher
        // than a nine-cube that merely allows it.
        const int Budget = 12;

        // MINTED WITH A FACE ARRAY, WHICH IS THE WHOLE POINT AND WAS THE BUG.
        //
        // This used to mint an ordinary solid and drop an 'E' onto one of its
        // trace cells. Under the old rule that was enough: eversion was a
        // reversal of the depth test, so any solid could be everted. It is a
        // SECOND ARRAY of types now, and SpecFor never asks for one — so every
        // "lesson" this found was the glyph winning its own column by precedence
        // and nothing to do with turning a face. Two of them printed, and both
        // were fiction.
        //
        // So the cubes are cut the way the ladder cuts an everter vault, through
        // Curate's own spec, which now asks for two layouts when the set carries
        // an E. The carve then lays a route through BOTH boards and places the
        // everter where the route crosses between them.
        var spec = Curate.V("THE FAR SIDE", 6, 2, 8, 0, 1, "E", 0.60, "");

        for (int level = 11; level <= 400 && found.Count < 4; level++)
        {
            examined++;
            for (int i = 0; i < Budget && found.Count < 4; i++)
            {
                uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0xfaceu);
                Level lv = Curate.RoughLevel(seed, spec, level, spec.parLo, out _);
                if (lv == null) { nullMint++; continue; }
                if (lv.alts == null || lv.alts.Length == 0 || lv.alts[0] == null) { nullAlts++; continue; }
                placements++;

                SolveResult with = Solver.Solve(lv, 12);
                if (!with.ok) continue;

                // A LESSON IS ALSO A CUBE, AND A CUBE THAT SOLVES IN NO FOLDS IS
                // NOT ONE. The first run of this printed CHOOSE THE SHADOW at
                // "par 1 -> 0" — a board you can walk to the core on without
                // folding once, which teaches the far side by not needing it.
                // Two folds is the floor for something a vault opens on.
                if (with.turns < 2) continue;

                // THE CONTROL IS THE SAME CUBE WITH ITS OTHER FACE TAKEN AWAY.
                //
                // Not the everter demoted to trace — that changes which cell wins
                // the column and measures the glyph rather than the mechanic.
                // Nulling `alts` leaves every cell, every type and the everter
                // itself exactly where they are, and removes only the ability to
                // turn: the difference between the two answers is the face turn
                // and nothing else.
                Level flat = lv.Clone();
                flat.alts = null;
                flat.ClearEff();
                SolveResult without = Solver.Solve(flat, 12);

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

                for (int lesson = 2; lesson <= 4; lesson++)
                {
                    if (found.ContainsKey(lesson)) continue;
                    if (!Makes(lesson, lv, with, without)) continue;

                    found[lesson] = Describe(lesson, level, i, without, with);
                    codes[lesson] = ShareCode.Encode(lv);
                    Console.WriteLine(found[lesson]);
                    Console.WriteLine("     " + codes[lesson]);
                    break;
                }
            }
        }

        Console.WriteLine("   mint returned null " + nullMint + ", no face array " + nullAlts);
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
    /// <summary>
    /// DOES THIS CUBE MAKE LESSON <paramref name="lesson"/>? Asked one lesson at
    /// a time rather than as a first-match cascade.
    ///
    /// It was a cascade — return 1, else 2, else 3, else 4 — and lesson three is
    /// common: on a face everter almost every cube has SOME square that is trace
    /// on one face and bedrock on the other. So three matched first on nearly
    /// everything and four was unreachable, and four thousand three hundred and
    /// ninety placements found three lessons and could never have found the
    /// fourth. A cube is allowed to teach more than one thing; the search just
    /// needs to be asked about each.
    /// </summary>
    static bool Makes(int lesson, Level lv, SolveResult with, SolveResult without)
    {
        switch (lesson)
        {
            // 1. UNSOLVABLE WITHOUT IT.
            case 1: return !without.ok;

            // 2. CHEAPER EVERTED — a decision rather than a gate.
            case 2: return without.ok && with.turns < without.turns;

            // 3. THE FLOOR CAN GO. Somewhere on the starting board is a square
            //    that is walkable now and is not once the faces have turned.
            case 3: return without.ok && LosesFooting(lv);

            // 4. A FOLD THAT NEEDS A FACE. Somewhere you can stand on both, a
            //    fold that one face permits and the other refuses.
            case 4: return without.ok && PolarityGatesAFold(lv);
        }
        return false;
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
            // AND THE OTHER FACE. Without it the paste block emits half a cube and
            // every lesson it was found for evaporates on arrival.
            Console.WriteLine("    " + Pts(lv.keys) + ", " + Pts(lv.doors)
                            + (lv.voxB == null ? "" : ",\n    \"" + new string(lv.voxB) + "\"") + "),");
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
    /// <summary>
    /// The cube with its other face taken away — the everter still there, still
    /// walkable, still winning its column, and simply unable to turn anything.
    /// This used to demote the glyph to trace, which measured the glyph's
    /// precedence rather than the mechanic.
    /// </summary>
    static Level Flatten(Level lv)
    {
        Level flat = lv.Clone();
        flat.alts = null;
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

    /// <summary>
    /// A FOLD THAT ONLY ONE FACE PERMITS — asked from everywhere the player could
    /// be standing, not only from the door.
    ///
    /// This tested the four folds off `lv.start` and nothing else, which is one
    /// cell of a board with a dozen, and it found nothing in four thousand
    /// placements. The claim was never about the entrance: it is that somewhere
    /// on this cube there is a swipe you can take on one face and not on the
    /// other, and where you are standing when that bites is the puzzle.
    /// </summary>
    static bool PolarityGatesAFold(Level lv)
    {
        int n = lv.n;
        Ori m = Turns.Oris[0];
        Surf[] a = Projection.Project(n, lv.Eff(0), m);
        Surf[] b = Projection.Project(n, lv.Eff(Level.Everted), m);

        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
            {
                // it has to be a square you can stand on in BOTH, or the fold
                // being refused is the footing changing rather than the fold
                if (!Projection.Walkable(lv, a, u, v, 0)) continue;
                if (!Projection.Walkable(lv, b, u, v, 0)) continue;
                Int3 cell = a[u * n + v].w;

                for (int t = 0; t < 4; t++)
                {
                    Ori m2 = Turns.All[t].f(m);
                    bool up = Projection.Landing(lv, m2, cell, 0, 0, out _);
                    bool ev = Projection.Landing(lv, m2, cell, 0, Level.Everted, out _);
                    if (up != ev) return true;
                }
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
