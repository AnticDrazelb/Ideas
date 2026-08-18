using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// THE LADDER THE PLAYER ACTUALLY CLIMBS, WHICH IS NOT THE ONE ContentAudit READS.
///
/// `content` mints cubes from the generator and audits those. That was the whole
/// ladder once and it is not any more: `Bootstrap` loads `catalogue.txt` and
/// `LevelSupply` serves it, so the first three hundred cubes — every cube most
/// players will ever see — are the CURATED ones, chosen out of thousands of
/// candidates against a per-vault spec and then pruned. The generator is what
/// happens after cube three hundred.
///
/// The distinction matters because the design argument written against the
/// generator's numbers reads as a verdict on the game. It says par asymptotes
/// near 5.8 folds and stops, that the ladder gets bigger rather than harder, and
/// that the last cubes are drawn from one fixed difficulty distribution. Every
/// one of those sentences is true of `content` and none of them is true of what
/// ships. This prints what ships, in the same shape, so the two can be read side
/// by side and the doc can be corrected against a measurement rather than a
/// memory.
///
///     dotnet run --project unity/tools/UnityStubs ladder
/// </summary>
static class LadderAudit
{
    struct Row
    {
        public int cubes, n;
        public int parLo, parHi;
        public double par, steps, open, depth;
        public int points;
        public int plates, everters, triggers, nodes;
    }

    const float Frame = 1000f / 60f;

    /// <summary>
    /// THE GATE A VAULT IS ACTUALLY CUT TO, WHICH IS NOT THE NUMBER IN THE TABLE.
    ///
    /// Pick relaxes it across a vault — `openMax + (1 - openMax) * (1 - t) * 0.35`
    /// — so the first cube of a vault is a way in and the twenty-fifth is the
    /// hardest thing in it. Comparing a vault's MEAN against the table's value
    /// compares it against the tightest cube's gate and reports misses that are
    /// not misses. This is the mean of the gate over the twenty-five slots, which
    /// is the thing a vault mean can honestly be held to.
    /// </summary>
    static double EffectiveGate(int band)
    {
        double open = Curate.Ladder[band].openMax, sum = 0;
        for (int within = 0; within < Curate.PerVault; within++)
        {
            double t = within / (double)(Curate.PerVault - 1);
            sum += open + (1.0 - open) * (1.0 - t) * 0.35;
        }
        return sum / Curate.PerVault;
    }

    static void Settle(Session s, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            s.AdvanceWalk(Frame);
            s.AdvanceAnim(Frame);
            s.StepPlateClock(Frame, false);
            if (s.walking == null && s.anim == null) return;
        }
    }

    /// <summary>
    /// IS THE GATE UNREACHABLE, OR IS THE SCORER LOSING?
    ///
    /// The shipped ladder misses its own decision-density gate in the last two
    /// vaults, and there are only two explanations. Either the scorer's 900-point
    /// term is being outvoted by everything else in Value, in which case the fix
    /// is a weight; or cubes that tight do not exist at n=9 with a par band in the
    /// teens, in which case the ladder was designed around an axis the generator
    /// cannot supply and the doc should say so.
    ///
    /// One is a tuning change and the other is a design finding, and guessing
    /// between them is exactly what this repository does not do. So: cut a pool
    /// for a late slot the way Curate cuts one, and print the DISTRIBUTION of the
    /// quantity rather than the winner.
    ///
    ///     dotnet run --project unity/tools/UnityStubs ladder gate
    /// </summary>
    public static void Gate(string[] args)
    {
        int slot = args.Length > 2 && int.TryParse(args[2], out int sl) ? sl : Curate.Total - 1;
        int budget = args.Length > 3 && int.TryParse(args[3], out int b) ? b : 60;

        Curate.Vault v = Curate.Ladder[slot / Curate.PerVault];
        int level = slot + 1, within = slot % Curate.PerVault;
        double t = within / (double)(Curate.PerVault - 1);
        int parLo = v.parLo + (int)Math.Round(t * (v.parHi - v.parLo) * 0.45);
        double openMax = v.openMax + (1.0 - v.openMax) * (1.0 - t) * 0.35;

        Console.WriteLine("cube " + level + " — vault " + v.name + ", n=" + v.n
                        + ", par " + parLo + "-" + v.parHi + ", gate " + (openMax * 100.0).ToString("0") + "%");
        Console.WriteLine("cutting " + budget + " candidates the way Curate cuts them…");
        Console.WriteLine();

        var open = new List<double>();
        var byStageOne = new List<(double score, double route)>();
        var pars = new List<(int par, double route)>();
        int solved = 0;
        for (int i = 0; i < budget; i++)
        {
            uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
            Level lv = Curate.RoughLevel(seed, v, level, parLo, out double stageOne);
            if (lv == null) continue;
            SolveResult r = Solver.Solve(lv, 40);
            if (!r.ok) continue;
            solved++;
            double route = Curate.RouteOpen(lv, r.turns, out _, out _);
            open.Add(route);
            byStageOne.Add((stageOne, route));
            pars.Add((r.turns, route));
        }

        // the shortlist, exactly as Pick takes it: the best of the pool by the
        // score it has BEFORE the route is measured
        byStageOne.Sort((a, b) => b.score.CompareTo(a.score));
        var shortlisted = new List<double>();
        for (int i = 0; i < Math.Min(Curate.ShortlistSize, byStageOne.Count); i++)
            shortlisted.Add(byStageOne[i].route);

        if (open.Count == 0) { Console.WriteLine("gate: nothing solved"); return; }

        // AND THE PART THAT SAYS WHY. Stage one scores the whole pool while every
        // candidate still carries the DEFAULT routeOpen of 1.0, so the term that
        // is supposed to dominate contributes the same nothing to all of them;
        // the shortlist is chosen on the OPENING figure instead. Curate's own
        // comment says those two "disagree in DIRECTION". If that is true, the
        // shortlist is selected against the thing it is about to be judged on,
        // and the tight cubes are gone before the measure that wants them runs.
        Console.WriteLine();
        double poolMean = 0; foreach (double o in open) poolMean += o;
        poolMean /= open.Count;
        double shortMean = 0; int took = 0;
        foreach (double o in shortlisted) { shortMean += o; took++; }
        if (took > 0)
        {
            shortMean /= took;
            Console.WriteLine("  the whole pool averages   " + (poolMean * 100.0).ToString("0") + "% keep-par");
            Console.WriteLine("  the shortlist averages    " + (shortMean * 100.0).ToString("0")
                            + "% keep-par   (" + took + " of " + open.Count + ", chosen before it was measured)");
            Console.WriteLine(shortMean > poolMean
                ? "  the shortlist is WORSE than the pool it was drawn from on the one measure that decides the winner"
                : "  the shortlist is at least as good as its pool");
        }
        open.Sort();

        int under = 0;
        foreach (double o in open) if (o <= openMax) under++;

        Console.WriteLine("  candidates that solve      " + solved + " of " + budget);
        Console.WriteLine("  keep-par, best of them     " + (open[0] * 100.0).ToString("0") + "%");
        Console.WriteLine("  keep-par, median           " + (open[open.Count / 2] * 100.0).ToString("0") + "%");
        Console.WriteLine("  keep-par, worst            " + (open[open.Count - 1] * 100.0).ToString("0") + "%");
        Console.WriteLine("  inside the gate            " + under + " of " + open.Count
                        + "  (" + (100.0 * under / open.Count).ToString("0") + "%)");
        Console.WriteLine();
        // AND THE QUESTION THAT DECIDES WHETHER THIS IS TUNING OR DESIGN: is any
        // ONE cube both inside the par band and inside the gate? The two are
        // measured on the same candidates before either is pruned, so if the
        // answer is none, the ladder's two axes are asking for something that does
        // not exist at this size — and the prune is what the cut reaches for to
        // close the par gap, which the file's own comment says costs exactly this
        // measure ("pruning improves the opening figure and worsens the route").
        int inBand = 0, both = 0, bestInBand = 1000;
        foreach ((int par, double route) in pars)
        {
            if (par < parLo || par > v.parHi) continue;
            inBand++;
            if (route <= openMax) both++;
            if (route * 100 < bestInBand) bestInBand = (int)(route * 100);
        }
        Console.WriteLine();
        Console.WriteLine("  in the par band UNPRUNED   " + inBand + " of " + open.Count);
        Console.WriteLine("  in the band AND the gate   " + both);
        if (inBand > 0)
            Console.WriteLine("  best keep-par in the band  " + bestInBand + "%");
        Console.WriteLine(both > 0
            ? "  both at once EXISTS unpruned — the cut does not have to choose"
            : "  NOTHING is both in the band and inside the gate before pruning, so the cut has to"
            + " prune to reach the par band, and pruning is what puts keep-par up");
        Console.WriteLine();

        Console.WriteLine(both > 0
            ? "  the gate is REACHABLE at this slot without pruning — the cut does not have to\n"
            + "  choose between par and decisions, so a ladder outside the gate is a scoring problem"
            : "  the two axes CONFLICT at this slot. Cubes inside the gate exist (" + under + " of "
              + open.Count + ") and cubes in the par band do not (" + inBand + " of " + open.Count
              + "), so the cut must prune to reach par — and pruning is precisely what raises\n"
              + "  keep-par. The gate is not missed here by a scorer losing a vote; it is missed\n"
              + "  because the ladder is asking for two things the carve cannot supply at once.");
    }

    public static void Run()
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));
        if (Catalogue.Count == 0)
        {
            Console.WriteLine("ladder: no catalogue — nothing to audit");
            return;
        }

        Console.WriteLine("THE SHIPPED LADDER — " + (Vaults.LastBand + 1) + " vaults, "
                        + Vaults.VaultEnd + " cubes, served exactly as LevelSupply serves them");
        Console.WriteLine();
        Console.WriteLine("vault  cubes   n   par  (lo-hi)  steps/fold  keep-par   gate   depth   carries");
        Console.WriteLine("---------------------------------------------------------------------------------");

        var rows = new List<Row>();
        for (int band = 0; band <= Vaults.LastBand; band++)
        {
            var r = new Row { parLo = 99, parHi = 0 };
            int start = Vaults.VaultStart(band), size = Vaults.VaultSize(band);

            for (int i = 0; i < size; i++)
            {
                int level = start + i;
                // THROUGH THE GAME'S OWN SUPPLY, not out of the catalogue. The
                // authored cubes win first — vault I's ten and the four eversion
                // cubes at 146-149 — so reading the catalogue directly audits
                // fourteen cubes nobody is ever served.
                Level lv = LevelSupply.Get(level);
                if (lv == null) continue;
                SolveResult sol = Solver.Solve(lv, 40);
                if (!sol.ok) continue;

                r.cubes++;
                r.n += lv.n;
                r.par += sol.turns;
                r.steps += sol.steps;
                if (sol.turns < r.parLo) r.parLo = sol.turns;
                if (sol.turns > r.parHi) r.parHi = sol.turns;

                string vox = new string(lv.vox);
                if (vox.IndexOfAny(new[] { 'A', 'B' }) >= 0) r.plates++;
                if (vox.IndexOf('E') >= 0) r.everters++;
                if (vox.IndexOfAny(new[] { 'T', 'U' }) >= 0) r.triggers++;
                if (lv.keys != null && lv.keys.Count > 0) r.nodes++;

                // THE DECISION AXIS, ASKED WITH THE CURATION'S OWN FUNCTION.
                //
                // This started as a private re-implementation — folds legal from
                // the START CELL, on the FIRST fold only — and printed 69% for the
                // last vault against a gate of 0.22, which reads as the ladder
                // ending on its softest cubes. It was measuring a different
                // quantity badly: stepping is free, so before folding the player
                // may be anywhere the walk reaches, and a fold with no footing
                // underfoot may have plenty two cells away. Curate.RouteOpen says
                // exactly that in a comment about the same mistake, made once
                // before, in the same file this now calls into.
                r.open += Curate.RouteOpen(lv, sol.turns, out int points, out double depth);
                r.depth += depth;
                r.points += points;
            }

            if (r.cubes == 0) continue;
            rows.Add(r);

            Console.WriteLine(
                Vaults.RomanOf(band).PadRight(6)
              + r.cubes.ToString().PadLeft(4)
              + (r.n / (double)r.cubes).ToString("0.0").PadLeft(5)
              + (r.par / r.cubes).ToString("0.00").PadLeft(7)
              + ("  " + r.parLo + "-" + r.parHi).PadLeft(9)
              + (r.steps / Math.Max(1.0, r.par)).ToString("0.0").PadLeft(11)
              + ((100.0 * r.open / r.cubes).ToString("0") + "%").PadLeft(11)
              + (EffectiveGate(band) * 100.0).ToString("0").PadLeft(6) + "%"
              + (100.0 * r.depth / r.cubes).ToString("0").PadLeft(7) + "%"
              + ("   " + r.plates + "A " + r.everters + "E " + r.triggers + "T " + r.nodes + "N"));
        }

        Console.WriteLine();

        // THE TWO SENTENCES THE DESIGN DOC MAKES, ANSWERED AGAINST WHAT SHIPS.
        Row first = rows[0], last = rows[rows.Count - 1];
        double parFirst = first.par / first.cubes, parLast = last.par / last.cubes;
        Console.WriteLine("does it get harder, or only bigger?");
        Console.WriteLine("   par          " + parFirst.ToString("0.00") + " in vault I to "
                        + parLast.ToString("0.00") + " in vault " + Vaults.RomanOf(rows.Count - 1)
                        + "   (x" + (parLast / parFirst).ToString("0.0") + ")");

        Console.WriteLine("   folds that keep par   " + (100.0 * first.open / first.cubes).ToString("0")
                        + "% in vault I to " + (100.0 * last.open / last.cubes).ToString("0")
                        + "% in vault " + Vaults.RomanOf(rows.Count - 1)
                        + "   — the decision axis, and the one the orientation group does not cap");
        Console.WriteLine("   depth               " + (100.0 * first.depth / first.cubes).ToString("0")
                        + "% to " + (100.0 * last.depth / last.cubes).ToString("0")
                        + "%   — the share of the solve that happens in a world or a face "
                        + "the opening board cannot show you");

        // AND WHETHER EACH VAULT IS INSIDE THE GATE IT WAS CUT TO.
        int over = 0;
        for (int band = 0; band < rows.Count; band++)
        {
            double got = rows[band].open / rows[band].cubes;
            double gate = EffectiveGate(band);
            if (got > gate + 0.02)
            {
                over++;
                Console.WriteLine("   vault " + Vaults.RomanOf(band).PadRight(5) + " is at "
                                + (got * 100.0).ToString("0") + "% against a gate of "
                                + (gate * 100.0).ToString("0") + "%");
            }
        }
        Console.WriteLine("   " + (over == 0 ? "every vault is inside the gate it was cut to"
                                             : over + " of 12 vaults are outside the gate they were cut to"));

        // THE TREND, WHICH IS THE FINDING RATHER THAN ANY ONE VAULT.
        //
        // The gate falls from 100% to 36% across the ladder because the design
        // says par stops being the difficulty and decision density carries the
        // back half. Whether the CUBES follow it down is a separate question, and
        // a slope fitted over twelve points answers it in one number.
        double n = rows.Count, sx = 0, sy = 0, sxy = 0, sxx = 0, gx = 0, gy = 0, gxy = 0, gxx = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            double y = rows[i].open / rows[i].cubes, g = EffectiveGate(i);
            sx += i; sy += y; sxy += i * y; sxx += i * i;
            gx += i; gy += g; gxy += i * g; gxx += i * i;
        }
        double slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);
        double gslope = (n * gxy - gx * gy) / (n * gxx - gx * gx);
        Console.WriteLine();
        Console.WriteLine("   the gate falls " + (gslope * 100.0).ToString("0.0")
                        + " points a vault across the ladder.");
        Console.WriteLine("   the cubes " + (slope < 0 ? "fall " : "RISE ")
                        + Math.Abs(slope * 100.0).ToString("0.0") + " points a vault.");
        Console.WriteLine(slope >= gslope * 0.5
            ? "   so decision density is NOT the back half's difficulty axis in the shipped ladder,\n"
            + "   whatever the ladder was designed around. Par, board size and depth carry all of it."
            : "   so decision density does tighten with the gate, as designed.");
    }
}
