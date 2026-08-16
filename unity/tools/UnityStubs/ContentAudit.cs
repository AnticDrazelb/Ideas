using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// IS THE GAME'S OWN IDEA LOAD-BEARING IN ITS CONTENT?
///
/// Everything else measured in this harness is craft — contrast, headroom,
/// allocation, whether a save survives a battery pull. None of it is why anybody
/// remembers a puzzle game. What is remembered is whether the central idea is
/// actually the thing you have to think about, cube after cube, and that is a
/// property of the CONTENT rather than of the code.
///
/// This game has one idea: fold the solid, and a different axis becomes depth,
/// so the map rewires. Ten cubes are authored. Everything from eleven onward is
/// minted by a generator. So the question a content audit has to answer is not
/// "does the generator produce solvable cubes" — Validation already refuses the
/// ones it does not — but:
///
///   DOES THE SOLUTION REQUIRE A FOLD AT ALL? A cube you can walk from start to
///   core without folding once is a maze, not this game. It will still verify,
///   still have a par, still look right.
///
///   HOW DEEP IS THE FOLD? One fold is a corner. Four is a plan.
///
///   IS THE OPENING A DECISION? If half the legal first moves keep you on par,
///   the cube has no opening — it has a shape you wander until it works.
/// </summary>
static class ContentAudit
{
    public static void Run()
    {
        const int Last = 240;
        Console.WriteLine("cube   n  par  folds  steps  folds-on-par/legal");
        Console.WriteLine("----------------------------------------------------");

        var foldHist = new Dictionary<int, int>();
        int walkable = 0, solved = 0, thin = 0;
        double openSum = 0;
        int openN = 0;
        int freeOpen = 0, decisionOpen = 0, forcedStep = 0;
        var bin = new double[10, 4];   // per decile: cubes, folds, steps, size

        for (int level = 1; level <= Last; level++)
        {
            Level lv = Baked.TryAt(level, out BakedLevel b)
                     ? b.ToLevel(level)
                     : Generator.Mint(level);
            if (lv == null) continue;

            SolveResult r = Solver.Solve(lv);
            if (!r.ok) { Console.WriteLine(level + ": no route"); continue; }
            solved++;

            foldHist[r.turns] = foldHist.TryGetValue(r.turns, out int had) ? had + 1 : 1;
            if (r.turns == 0) walkable++;
            if (r.turns <= 1) thin++;

            // THE OPENING. Every legal first act, solved from where it leaves you:
            // how many of them are still on par? One means the cube opens with a
            // decision. Most of them means it opens with a shrug.
            int onPar = 0, legal = 0;
            foreach (SolveState next in FoldOpenings(lv))
            {
                legal++;
                SolveResult after = Solver.Solve(lv, 40, next);
                if (after.ok && after.turns + 1 <= r.turns) onPar++;
            }
            if (legal == 0) forcedStep++;
            else if (onPar == legal) freeOpen++;
            else decisionOpen++;
            if (legal > 0) { openSum += onPar / (double)legal; openN++; }

            int dec = Math.Min(9, (level - 1) * 10 / Last);
            bin[dec, 0] += 1; bin[dec, 1] += r.turns; bin[dec, 2] += r.steps; bin[dec, 3] += lv.n;

            if (level <= 14 || level % 24 == 0)
                Console.WriteLine(string.Format("{0,4}  {1,2} {2,4} {3,6} {4,6}   {5,3}/{6,-3}",
                    level, lv.n, lv.par, r.turns, r.steps, onPar, legal));
        }

        Console.WriteLine();
        Console.WriteLine("folds required at par, across " + solved + " cubes:");
        var keys = new List<int>(foldHist.Keys); keys.Sort();
        foreach (int k in keys)
            Console.WriteLine(string.Format("   {0,2} fold(s)  {1,4}  {2,5:0.0}%  {3}",
                k, foldHist[k], 100.0 * foldHist[k] / solved, new string('#', foldHist[k] * 40 / solved)));

        Console.WriteLine();
        Console.WriteLine(string.Format("WALKABLE WITHOUT FOLDING: {0} of {1}  ({2:0.0}%)", walkable, solved,
                          100.0 * walkable / solved));
        Console.WriteLine(string.Format("ONE FOLD OR FEWER:        {0} of {1}  ({2:0.0}%)", thin, solved,
                          100.0 * thin / solved));
        Console.WriteLine(string.Format("MEAN SHARE OF OPENING FOLDS THAT KEEP PAR: {0:0.0}%", 100.0 * openSum / openN));
        Console.WriteLine();
        Console.WriteLine("THE OPENING, classified:");
        Console.WriteLine(string.Format("   no legal fold at all, you must step   {0,4}  ({1:0.0}%)",
                          forcedStep, 100.0 * forcedStep / solved));
        Console.WriteLine(string.Format("   every legal fold keeps par            {0,4}  ({1:0.0}%)   <- no decision",
                          freeOpen, 100.0 * freeOpen / solved));
        Console.WriteLine(string.Format("   some folds lose par                   {0,4}  ({1:0.0}%)   <- a decision",
                          decisionOpen, 100.0 * decisionOpen / solved));

        Console.WriteLine();
        Console.WriteLine("DOES IT GET HARDER, OR ONLY BIGGER?  (cubes 1.." + Last + ", by tenth)");
        Console.WriteLine("  tenth   cubes   mean n   mean folds   mean steps   steps/fold");
        for (int d = 0; d < 10; d++)
        {
            if (bin[d, 0] == 0) continue;
            double c = bin[d, 0];
            Console.WriteLine(string.Format("   {0,3}    {1,5}   {2,6:0.0}   {3,10:0.00}   {4,10:0.0}   {5,10:0.0}",
                d + 1, (int)c, bin[d, 3] / c, bin[d, 1] / c, bin[d, 2] / c, bin[d, 2] / bin[d, 1]));
        }
    }

    /// <summary>
    /// THE FOUR FOLDS FROM THE START, AND WHERE EACH LEAVES YOU.
    ///
    /// Only the folds, and only from the start. Steps are free — they do not
    /// change par — so the question "is the opening a decision" is a question
    /// about which way you turn the engine first. And these are the solver's own
    /// public helpers called in the solver's own order rather than a second
    /// implementation of its expansion, because a content audit that disagreed
    /// with the search would be measuring itself.
    /// </summary>
    static IEnumerable<SolveState> FoldOpenings(Level lv)
    {
        var start = new SolveState { pos = lv.start, ori = 0, kmask = 0, doors = 0, world = 0 };
        int s0 = lv.KeyIndexAt(lv.start);
        if (s0 >= 0) start.kmask = 1 << s0;
        start.world = lv.GlyphAt(lv.start);

        Ori m = Turns.Oris[start.ori];
        for (int t = 0; t < 4; t++)
        {
            Ori m2 = Turns.All[t].f(m);
            if (!Projection.Landing(lv, m2, start.pos, start.doors, start.world, out Surf land)) continue;
            int nk = start.kmask;
            int ki = lv.KeyIndexAt(land.w);
            if (ki >= 0) nk |= 1 << ki;
            yield return new SolveState
            {
                pos = land.w, ori = Turns.OriIndex(m2),
                kmask = nk, doors = start.doors, world = start.world
            };
        }
    }
}
