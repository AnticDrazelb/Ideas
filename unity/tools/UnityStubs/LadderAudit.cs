using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// THE LADDER THE PLAYER ACTUALLY CLIMBS, WHICH IS NOT THE ONE ContentAudit READS.
///
/// `content` mints cubes from the generator and audits those. That was the whole
/// ladder once and it is not any more: `Bootstrap` loads `catalogue.txt` and
/// `LevelSupply` serves it, so all hundred and fifty cubes — every cube any
/// player will ever see outside the daily — are the CURATED ones, chosen out of
/// thousands of candidates against a per-chapter spec, pruned, and gated on a
/// CLAIM about what the cube is for. The generator survives for the daily and
/// nothing else.
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
        public double par, steps, open, depth, legal;
        public List<double> blinds;
        public int floored;
        public int points;
        public int plates, everters, triggers, nodes;
    }

    /// <summary>
    /// EVERY SOLID THE CUBE HAS, NOT JUST THE FIRST.
    ///
    /// A trigger cube is TWO solids — `vox` and `alts[0]` — and the board is
    /// exchanged between them. Reading `vox` alone reported vault X as carrying no
    /// plates at all when its spec asks for a plate under a trigger, and split
    /// vault XII into "stacked" and "single" on evidence that was missing half of
    /// every cube in it. Session.Load has a comment about `alts` being left out of
    /// a list once before, which is the same mistake in the same shape.
    /// </summary>
    static string AllSolids(Level lv)
    {
        string all = new string(lv.vox);
        if (lv.alts != null)
            foreach (char[] a in lv.alts) if (a != null) all += new string(a);
        return all;
    }

    const float Frame = 1000f / 60f;

    /// <summary>
    /// THE GATE A VAULT IS ACTUALLY CUT TO, WHICH IS NOT THE NUMBER IN THE TABLE.
    ///
    /// Pick relaxes it across a vault — `openMax + (1 - openMax) * (1 - t) * 0.35`
    /// — so the first cube of a chapter is a way in and the last is the hardest
    /// thing in it. Comparing a chapter's MEAN against the table's value compares
    /// it against the tightest cube's gate and reports misses that are not
    /// misses. This is the mean of the gate over the chapter's slots, which is the
    /// thing a chapter mean can honestly be held to.
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

    /// <summary>The middle cube of the vault, which is the one a player meets.</summary>
    static double Median(List<double> v)
    {
        if (v.Count == 0) return 1.0;
        var c = new List<double>(v);
        c.Sort();
        return c.Count % 2 == 1 ? c[c.Count / 2] : (c[c.Count / 2 - 1] + c[c.Count / 2]) * 0.5;
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

    /// <summary>
    /// DOES THE SECOND WORLD-CHANGER EARN ITS PLACE?
    ///
    /// Curate's own probes concluded that it does not — "every time a second
    /// world-changer went in, the first stopped mattering… the extra ones are
    /// scenery, and scenery is worse than absence because the player is shown a
    /// thing that turns out not to be there" — and the back half was rebuilt to
    /// rotate one mechanic at a time because of it.
    ///
    /// One vault still stacks: SINGULARITY, the last one, carries "ET". It is also
    /// the vault that measures easiest in the back half. Those two facts want to
    /// be tested against each other rather than assumed into a story, so this
    /// splits a vault by what its cubes actually carry and prints both halves.
    ///
    ///     dotnet run --project unity/tools/UnityStubs ladder split 11
    /// </summary>
    public static void Split(string[] args)
    {
        int band = args.Length > 2 && int.TryParse(args[2], out int bb) ? bb : Vaults.LastBand;
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));

        var stacked = new List<double>();
        var single = new List<double>();
        double stackPar = 0, singlePar = 0;

        int start = Vaults.VaultStart(band), size = Vaults.VaultSize(band);
        for (int i = 0; i < size; i++)
        {
            int level = start + i;
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            SolveResult r = Solver.Solve(lv, 40);
            if (!r.ok) continue;

            string vox = AllSolids(lv);
            int kinds = 0;
            if (vox.IndexOfAny(new[] { 'A', 'B' }) >= 0) kinds++;
            if (vox.IndexOf('E') >= 0) kinds++;
            if (vox.IndexOfAny(new[] { 'T', 'U' }) >= 0) kinds++;

            Curate.RouteOpen(lv, r.turns, out _, out _, out _, out double blind);
            if (kinds >= 2) { stacked.Add(blind); stackPar += r.turns; }
            else { single.Add(blind); singlePar += r.turns; }
        }

        Console.WriteLine("VAULT " + Vaults.RomanOf(band) + " — " + Vaults.VaultName(band)
                        + ", split by how many kinds of world-changer each cube carries");
        Console.WriteLine();
        Report("two or more", stacked, stackPar);
        Report("just one", single, singlePar);

        if (stacked.Count > 0 && single.Count > 0)
        {
            double a = Median(stacked), b = Median(single);
            Console.WriteLine();
            Console.WriteLine(a > b
                ? "  the stacked cubes are EASIER by " + (a / b).ToString("0.0")
                  + "x — the second world-changer is costing the vault its difficulty, which is"
                  + " what the rotation argument predicted"
                : "  the stacked cubes are harder by " + (b / a).ToString("0.0")
                  + "x — stacking is earning its place here");
        }
    }

    static void Report(string what, List<double> v, double par)
    {
        if (v.Count == 0) { Console.WriteLine("  " + what.PadRight(14) + "none"); return; }
        Console.WriteLine("  " + what.PadRight(14) + v.Count.ToString().PadLeft(3) + " cubes   par "
                        + (par / v.Count).ToString("0.00")
                        + "   par by luck 1 in " + (1.0 / Median(v)).ToString("#,0"));
    }

    /// <summary>
    /// WHICH SPEC MAKES THE HARDEST FINALE — asked of the generator rather than
    /// argued from the vault's name.
    ///
    /// SINGULARITY is specified as the everter-with-trigger pair, and its stacked
    /// cubes measure 2.1x easier than its single-mechanic ones. That says the pair
    /// is wrong; it does not say which single mechanic is right. So cut pools for
    /// the same slots under each candidate spec and compare what comes out.
    ///
    ///     dotnet run --project unity/tools/UnityStubs ladder spec 11 [budget]
    /// </summary>
    public static void Spec(string[] args)
    {
        int band = args.Length > 2 && int.TryParse(args[2], out int bb) ? bb : Vaults.LastBand;
        int budget = args.Length > 3 && int.TryParse(args[3], out int b) ? b : 24;

        Curate.Vault baseV = Curate.Ladder[band];
        Console.WriteLine("VAULT " + Vaults.RomanOf(band) + " — " + baseV.name
                        + ", n=" + baseV.n + ", par " + baseV.parLo + "-" + baseV.parHi
                        + ", currently \"" + baseV.set + "\" with " + baseV.glyphs + " glyph(s)");
        Console.WriteLine("cutting " + budget + " candidates for each of three slots, under each spec…");
        Console.WriteLine();

        // each single kind the vault is allowed, and then the pairing itself —
        // derived from the vault rather than hard-coded, so this asks the right
        // question of whichever vault it is pointed at
        var trials = new List<(string set, int glyphs)>();
        foreach (char k in baseV.set ?? "") trials.Add((k.ToString(), 1));
        if ((baseV.set ?? "").Length > 1) trials.Add((baseV.set, baseV.set.Length));

        foreach ((string set, int glyphs) in trials)
        {
            Curate.Vault v = baseV;
            v.set = set;
            v.glyphs = glyphs;
            v.kinds = set.Length;

            var blinds = new List<double>();
            var liveOnly = new List<double>();
            double par = 0, livePar = 0; int made = 0, liveMade = 0;

            // the last five slots of the vault, which are the hardest it asks for
            for (int within = Curate.PerVault - 2; within < Curate.PerVault; within++)
            {
                int level = Vaults.VaultStart(band) + within;
                double t = within / (double)(Curate.PerVault - 1);
                int parLo = v.parLo + (int)Math.Round(t * (v.parHi - v.parLo) * 0.45);

                for (int i = 0; i < budget; i++)
                {
                    uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
                    Level lv = Curate.RoughLevel(seed, v, level, parLo, out _,
                                                 out int layers, out int live);
                    if (lv == null) continue;
                    SolveResult r = Solver.Solve(lv, 40);
                    if (!r.ok) continue;
                    Curate.RouteOpen(lv, r.turns, out _, out _, out _, out double blind);
                    blinds.Add(blind);
                    par += r.turns;
                    made++;

                    // AND THE SAME CUBES AGAIN, KEEPING ONLY THE ONES WHERE EVERY
                    // WORLD-CHANGER IS LOAD-BEARING. The rotation argument is that
                    // a second mechanic makes the first stop mattering; if that is
                    // what is happening, then the pairs where BOTH still matter
                    // should be the hard ones, and the fix is a gate on live
                    // layers rather than taking the mechanic away.
                    if (layers > 0 && live >= layers) { liveOnly.Add(blind); livePar += r.turns; liveMade++; }
                }
            }

            Console.Out.Flush();
            Console.WriteLine("  " + (set + " x" + glyphs).PadRight(8)
                            + made.ToString().PadLeft(4) + " candidates   par "
                            + (made > 0 ? (par / made).ToString("0.00") : "-").PadLeft(5)
                            + "   par by luck 1 in "
                            + (made > 0 ? (1.0 / Median(blinds)).ToString("#,0") : "-").PadLeft(9)
                            + "     every layer live: " + liveMade.ToString().PadLeft(3) + "   par "
                            + (liveMade > 0 ? (livePar / liveMade).ToString("0.00") : "-").PadLeft(5)
                            + "   1 in " + (liveMade > 0 ? (1.0 / Median(liveOnly)).ToString("#,0") : "-"));
        }

        Console.WriteLine();
        Console.WriteLine("  (raw candidates, before the prune that pulls par into the band —");
        Console.WriteLine("   what is being compared is the RAW MATERIAL each spec gives the cut)");
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
        Console.WriteLine("vault  cubes   n   par  keep-par  folds/pt  depth   par by luck   carries");
        Console.WriteLine("--------------------------------------------------------------------------------");

        var rows = new List<Row>();
        for (int band = 0; band <= Vaults.LastBand; band++)
        {
            var r = new Row { parLo = 99, parHi = 0, blinds = new List<double>() };
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

                string vox = AllSolids(lv);
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
                r.open += Curate.RouteOpen(lv, sol.turns, out int points, out double depth,
                                           out double meanLegal, out double blind);
                // a geometric mean, because these are probabilities spanning four
                // orders of magnitude and an arithmetic mean of those is the
                // largest one with rounding
                // KEPT AS A LIST, NOT A RUNNING SUM. These span nine orders of
                // magnitude, so a mean of any kind is a statement about the
                // extreme cube in the vault rather than about the vault. The
                // median is what a player meets.
                r.blinds.Add(blind);
                if (blind <= 1e-11) r.floored++;
                r.depth += depth;
                r.points += points;
                r.legal += meanLegal;
            }

            if (r.cubes == 0) continue;
            rows.Add(r);

            Console.WriteLine(
                Vaults.RomanOf(band).PadRight(6)
              + r.cubes.ToString().PadLeft(4)
              + (r.n / (double)r.cubes).ToString("0.0").PadLeft(5)
              + (r.par / r.cubes).ToString("0.00").PadLeft(7)
              + ((100.0 * r.open / r.cubes).ToString("0") + "%").PadLeft(10)
              + (r.legal / r.cubes).ToString("0.0").PadLeft(10)
              + (100.0 * r.depth / r.cubes).ToString("0").PadLeft(7) + "%"
              + ("1 in " + (1.0 / Median(r.blinds)).ToString("#,0")).PadLeft(14)
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
        Console.WriteLine("   par by luck  1 in " + (1.0 / Median(first.blinds)).ToString("#,0")
                        + " to 1 in " + (1.0 / Median(last.blinds)).ToString("#,0")
                        + "   — the chance of parring the whole route by choosing at random at every\n"
                        + "                fold, which is the two axes compounded and the one a player feels");
        Console.WriteLine("   depth               " + (100.0 * first.depth / first.cubes).ToString("0")
                        + "% to " + (100.0 * last.depth / last.cubes).ToString("0")
                        + "%   — the share of the solve that happens in a world or a face "
                        + "the opening board cannot show you");

        // IS IT MONOTONIC? A vault that is harder than the one after it is a
        // pacing fault rather than variety, and the one that matters most is a
        // TEACHING vault out of order: a new mechanic arriving on the hardest
        // cubes in the game so far, followed by three vaults of relief.
        Console.WriteLine();
        double prev = Median(rows[0].blinds);
        for (int i = 1; i < rows.Count; i++)
        {
            double now = Median(rows[i].blinds);
            if (now > prev * 1.6)
                Console.WriteLine("   vault " + Vaults.RomanOf(i).PadRight(5) + " is EASIER than "
                                + Vaults.RomanOf(i - 1) + " — 1 in " + (1.0 / now).ToString("#,0")
                                + " against 1 in " + (1.0 / prev).ToString("#,0"));
            prev = now;
        }
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].floored > 0)
                Console.WriteLine("   vault " + Vaults.RomanOf(i).PadRight(5) + " has " + rows[i].floored
                                + " cube(s) below one in a trillion, which is past what this measure resolves");

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
