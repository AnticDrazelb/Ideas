using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// THE EVERTER AS IT WAS MEANT TO BE — a cell is an OBJECT WITH FACES, and
/// turning every one of them rewires the board.
///
/// Measured before the level format, the share codes, the solver's state key and
/// the catalogue are asked to move, because all four of them move for this and
/// none of them should move for a mechanic that turns out not to make boards.
///
/// ---- the rule ----------------------------------------------------------
///
/// A cell stops being one character. It is a small solid with six faces, and the
/// one that matters is the one pointing at the camera. The board is still the
/// nearest occupied cell in each column — that does not change — but what that
/// square IS comes from the face it is presenting. An everter turns every cell
/// in place, at once, staggered, and a different face is now the surface.
///
/// ---- the one decision that makes it playable ---------------------------
///
/// The faces are indexed in VIEW SPACE, not in cell space.
///
/// If a cell's faces were fixed to the cell, folding would change which face
/// points at the camera as well as which cells are on the surface, so one swipe
/// would rewire the board twice over and nothing about it could be predicted.
/// Indexed in view space, a cell always presents face[state] to the camera
/// however the solid is folded: FOLDING changes which cells you can see, EVERTING
/// changes what they are, and the two verbs stay separable. That is the whole
/// design and it is the difference between a mechanic and a shuffle.
///
/// ---- what follows from it, and it is the good part ---------------------
///
/// Occupancy never changes, so the same cell wins its column in every state. The
/// board's SILHOUETTE is stable and only the circuit drawn on it rearranges —
/// which is a far more legible transform than the everter that shipped, where
/// every square you were looking at is replaced by a cell from the other end of
/// the solid. It is also, finally, the strapline: fold the engine until its
/// circuits align.
///
/// ---- what this probe asks ----------------------------------------------
///
/// Faces are free — a generator can paint any pattern it likes on face 1 — so
/// "can state 1 be connected" is trivially yes and not worth asking. The
/// questions that are worth asking are the ones the SOLID decides:
///
///   how much surface is there to rewire?   the board's squares are fixed, so
///                                          this is the ceiling on the mechanic
///   how much of it is spare?               a square already carrying the state-0
///                                          route cannot be freely repainted
///                                          without changing the cube you have
///   does it survive a fold?                the surface set changes with the
///                                          orientation, and a face pattern
///                                          authored for one view has to still
///                                          mean something in the other 23
///
///     dotnet run --project unity/tools/UnityStubs faces
/// </summary>
static class FaceProbe
{
    /// <summary>The biggest 4-connected run of squares on the screen grid.</summary>
    static int LargestComponent(bool[] mask, int n)
    {
        var seen = new bool[n * n];
        int best = 0;
        for (int start = 0; start < n * n; start++)
        {
            if (!mask[start] || seen[start]) continue;
            var q = new List<int> { start };
            seen[start] = true;
            for (int i = 0; i < q.Count; i++)
            {
                int u = q[i] / n, v = q[i] % n;
                for (int t = 0; t < 4; t++)
                {
                    int u2 = u + Turns.All[t].dx, v2 = v + Turns.All[t].dy;
                    if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
                    int k = u2 * n + v2;
                    if (seen[k] || !mask[k]) continue;
                    seen[k] = true;
                    q.Add(k);
                }
            }
            if (q.Count > best) best = q.Count;
        }
        return best;
    }

    public static void Run()
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));
        if (Catalogue.Count == 0) { Console.WriteLine("faces: no catalogue"); return; }

        Console.WriteLine("A CELL WITH FACES — what the solid can carry, measured on the cubes that ship.");
        Console.WriteLine();

        double surface = 0, live = 0, spare = 0, cubes = 0;
        double surfaceAllOri = 0, sharedAllOri = 0;
        double reachIfRepainted = 0, biggestFree = 0;

        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            cubes++;
            int n = lv.n;

            // ---- the surface, in the view the cube opens in ------------------
            Surf[] s0 = Projection.Project(n, lv.Eff(0), Ori.Id);
            int shown = 0, walk = 0;
            for (int i = 0; i < n * n; i++)
            {
                if (!s0[i].has) continue;
                shown++;
                if (Level.IsWalkType(s0[i].t)) walk++;
            }
            surface += shown;
            live += walk;
            spare += shown - walk;

            // ---- and how stable the surface is across every fold -------------
            //
            // A face pattern is painted on CELLS. Fold the solid and a different
            // set of cells is on the surface, so the pattern the player learned
            // is partly replaced by cells they have never seen the front of. The
            // share of the surface that is the SAME CELL after a fold is the
            // share of the rewiring that survives the fold, and it is the number
            // that says whether the two verbs can be reasoned about together.
            var seen = new HashSet<Int3>();
            int oris = 0;
            double shareSum = 0;
            for (int o = 0; o < 24; o++)
            {
                Ori m = Turns.Oris[o];
                Surf[] s = Projection.Project(n, lv.Eff(0), m);

                var here = new HashSet<Int3>();
                int cnt = 0;
                for (int i = 0; i < n * n; i++) if (s[i].has) { here.Add(s[i].w); cnt++; }
                surfaceAllOri += cnt;
                oris++;

                int same = 0;
                for (int i = 0; i < n * n; i++)
                    if (s0[i].has && here.Contains(s0[i].w)) same++;
                shareSum += shown == 0 ? 0 : same / (double)shown;

                foreach (Int3 w in here) seen.Add(w);
            }
            sharedAllOri += shareSum / Math.Max(1, oris);

            // ---- the ceiling on a repaint -----------------------------------
            //
            // If face 1 could be painted freely on every surface square, the
            // biggest connected route it could carry is bounded by the surface
            // itself — every square is available, so the answer is simply how
            // much surface there is, and the question becomes whether that is
            // enough to be a board. Thirteen walkable squares is what the game
            // plays on today.
            reachIfRepainted += shown;

            // ---- AND IS THE FREE SURFACE JOINED UP? -------------------------
            //
            // Forty inert squares are only raw material if some of them are
            // NEXT to each other. A second route needs a connected run through
            // them, so the number that decides the mechanic is the largest
            // component of the surface that is not already carrying the route.
            var free = new bool[n * n];
            for (int i = 0; i < n * n; i++)
                free[i] = s0[i].has && !Level.IsWalkType(s0[i].t);
            biggestFree += LargestComponent(free, n);
        }

        Console.WriteLine("per cube, in the view it opens in:");
        Console.WriteLine("   squares showing a cell        " + (surface / cubes).ToString("0.0"));
        Console.WriteLine("   of those, walkable now        " + (live / cubes).ToString("0.0"));
        Console.WriteLine("   of those, inert and free      " + (spare / cubes).ToString("0.0")
                        + "   <- what a second face can be painted on without");
        Console.WriteLine("                                       disturbing the route you already have");
        Console.WriteLine();
        Console.WriteLine("   the board plays on            " + (live / cubes).ToString("0.0")
                        + " walkable squares today");
        Console.WriteLine("   a repaint could offer up to   " + (reachIfRepainted / cubes).ToString("0.0")
                        + " — the whole surface");
        Console.WriteLine("   largest JOINED run of inert surface   " + (biggestFree / cubes).ToString("0.0")
                        + "   <- the route a second face could actually carry");
        Console.WriteLine();
        // ---- AND HOW CLOSE THE MACHINERY ALREADY IS ------------------------
        //
        // A trigger cube already carries a SECOND CHAR ARRAY. Level.alts holds
        // it, Eff(world) picks which one to start from off a world bit, the
        // solver already carries `world` through its search, and ShareCode
        // already encodes it. That is every moving part a face everter needs.
        //
        // The only difference is a constraint: a trigger's second solid
        // rearranges the ground, so occupancy changes and the silhouette moves.
        // A face everter is the same object with occupancy LOCKED — same cells
        // present, different types — so the board's shape holds still and only
        // its circuit rewires. Measured here, because "the format already
        // supports it" is a claim about the shipped cubes and not about my
        // reading of a class.
        int trig = 0; double occSame = 0, typeDiff = 0;
        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv?.alts == null || lv.alts.Length == 0 || lv.alts[0] == null) continue;
            trig++;
            char[] a = lv.vox, b = lv.alts[0];
            int same = 0, diff = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if ((a[i] == '.') == (b[i] == '.')) same++;
                if (a[i] != b[i]) diff++;
            }
            occSame += same / (double)a.Length;
            typeDiff += diff / (double)a.Length;
        }
        if (trig > 0)
        {
            Console.WriteLine();
            Console.WriteLine("the second solid a TRIGGER already carries, over " + trig + " cubes:");
            Console.WriteLine("   cells whose occupancy matches the first solid   "
                            + (100.0 * occSame / trig).ToString("0") + "%");
            Console.WriteLine("   cells whose type differs                       "
                            + (100.0 * typeDiff / trig).ToString("0") + "%");
            Console.WriteLine("   a face everter is this object with occupancy locked at 100%");
        }

        Console.WriteLine();
        Console.WriteLine("across all 24 folds:");
        Console.WriteLine("   share of the surface that is the SAME CELL as the opening view   "
                        + (100.0 * sharedAllOri / cubes).ToString("0") + "%");
        Console.WriteLine("   distinct cells that reach the surface in some fold, per cube     "
                        + (surfaceAllOri / cubes / 24.0).ToString("0.0") + " a fold");
    }
}
