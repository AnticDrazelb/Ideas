using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Singularity.Core;

/// <summary>
/// CHOOSING FIVE HUNDRED CUBES OUT OF A HUNDRED THOUSAND.
///
/// The generated ladder has a ceiling and the audit says where: mean par climbs
/// 2.71 → 5.17 by cube 120 and then stops, going 5.83 → 5.54 over the last two
/// tenths while the board size sits at nine. From roughly the halfway point the
/// game is the same difficulty forever. That is not a tuning problem, it is what
/// a generator that carves one self-avoiding path can reach.
///
/// So the ladder stops being generated and becomes a FIXED FIVE HUNDRED, the
/// same for every player, in order. This tool is how they are chosen.
///
/// IT DOES NOT AUTHOR ANYTHING. Every cube it emits came out of the same
/// Generator the game already ships, proved by the same Solver — what changes is
/// that a hundred candidates are minted for each slot and the one that best fits
/// a DESIGNED difficulty target is kept, instead of the first that clears a
/// band. Curation, not authorship: the generator is a good writer of cubes and a
/// poor editor of them.
///
/// WHAT IT SELECTS ON, and why par is not enough. The audit's sharpest number is
/// not par, it is this: on 25% of cubes EVERY legal opening fold keeps par, so a
/// quarter of the game opens with a move that cannot be got wrong. A par-5 cube
/// where four folds in five lose par is harder than a par-7 where none of them
/// do, and the generator never optimised for it because its own Score function
/// only counts turns and steps. Decision density is the lever nobody pulled.
///
/// TWO STAGES, because the second one is expensive. Minting and solving a
/// candidate is one bounded solve; measuring its decision density is one more
/// solve per legal opening fold, which is up to a dozen. So stage one mints and
/// keeps whatever lands in the slot's par band, and only the survivors are
/// measured.
///
///     dotnet run --project unity/tools/UnityStubs curate probe   # what it costs
///     dotnet run --project unity/tools/UnityStubs curate 25      # 25 candidates a slot
///     dotnet run --project unity/tools/UnityStubs curate         # the shipped budget
/// </summary>
static class Curate
{
    // ---- the ladder ------------------------------------------------------
    //
    // TWENTY VAULTS OF TWENTY-FIVE. A vault is the unit a player finishes in a
    // sitting and the unit the vault list shows as one rack, and twenty-five is
    // three columns by nine rows minus two — the shape that grid already draws
    // best.
    //
    // WHAT EACH VAULT IS FOR. The first four teach the four verbs; nothing after
    // vault VII introduces anything at all. That is deliberate and it is the
    // shape of every puzzle game worth the comparison: the vocabulary is small
    // and closed early, and the remaining three hundred and fifty cubes are
    // recombination under rising pressure. A game that is still teaching you a
    // new rule at cube four hundred has not found its idea.

    public struct Vault
    {
        public string name;
        public int n;                 // board size
        public int parLo, parHi;      // the band a cube must land in to be kept
        public int locks;             // node/lock pairs
        public int glyphs, kinds;     // plates: how many, and how many kinds
        public string set;            // which world-changers it may carry: "A", "AB", "E", "ABE"
        public double openMax;        // most of the opening folds that may keep par
        public string teaches;
    }

    // THE PAR BANDS ARE WHAT THE PRUNE CAN REACH, WHICH IS NOT WHAT THE CARVE
    // CAN. Three runs said the carve tops out around par seven — thirty-five to
    // thirty-nine of every forty candidates landing below the band, at every
    // size, however the spec was written. That was true and it was the wrong
    // thing to measure. A cube is easy because the carve left it swimming in
    // footing; take the spare footing away and the same cube is hard. Pruned,
    // the probe's own winners went 4 to 9 and 5 to TWELVE.
    //
    // So these run to fifteen, and the old ceiling was never the generator's —
    // it was Widen's. SpecFor still says the true bound and it still holds:
    // Folds live in a 24-orientation group whose graph has a diameter of four, so
    // rotation distance can never ask for more than four, and everything above
    // that comes from FOOTING forcing a route through orientations it did not
    // want. One self-avoiding carve can only force so much.
    //
    // So par stops being the ladder. It rises from 1 to 7 across the twenty
    // vaults because it can, and then DECISION DENSITY carries the rest: openMax
    // falls from 1.00 to 0.20, which is the difference between a cube where every
    // opening fold keeps par and one where four in five throw the route away.
    // That is the axis the generated ladder never had and it is not capped by the
    // orientation group at all.
    public static readonly Vault[] Ladder =
    {
        // ---- the verbs, one at a time ------------------------------------
        V("CALIBRATION", 5, 2, 4, 0, 0, null,  1.00, "the fold, the walk, the core"),
        V("REFRACTION",  5, 3, 5, 0, 0, null,  0.80, "folds that cost you the route"),
        V("INVERSION",   6, 3, 6, 1, 0, null,  0.70, "nodes and the locks they open"),
        V("ENTROPY",     6, 4, 6, 1, 0, null,  0.62, "two pairs, and the order between them"),
        V("EMBERFALL",   6, 4, 7, 0, 1, "A",   0.58, "the plate: two worlds, one board"),
        V("SUBSTRATE",   6, 4, 7, 1, 1, "B",   0.54, "the other plate, which opens the solid"),
        V("THE FAR SIDE",7, 5, 8, 0, 1, "E",   0.50, "the everter: the column shows its far cell"),
        V("THRESHOLD",   7, 5, 8, 0, 1, "T",   0.48, "the trigger: the board itself is exchanged"),

        // ---- and then nothing new, for three hundred cubes -------------------
        //
        // ONE MECHANIC A CUBE, MOSTLY, WHICH IS THE OPPOSITE OF WHAT I PLANNED.
        //
        // The plan was to layer everything from vault VIII and let the
        // combinations carry three hundred cubes. Four probes said no, and then a
        // whole five-hundred-slot cut of the layered ladder said it again, against
        // the rotating one, slot for slot:
        //
        //     vault           layered par/route/depth   rotating
        //     SALT REACH        7.4  38.4%  55.3%      7.8  28.4%  64.0%
        //     THE HOLLOW        7.3  40.1%  61.7%      8.6  28.9%  64.3%
        //     COPPER MARCH      8.2  37.4%  57.7%     10.0  35.6%  66.7%
        //     TIDE WELL         8.3  41.6%  61.3%     10.4  35.5%  65.7%
        //
        // Rotating wins par at every back-half vault by one to two, wins the route
        // measure everywhere, and wins depth almost everywhere — while carrying
        // one world-changer instead of three and taking a third of the time to
        // cut. Layered live-glyph ratios ran 1.6 to 2.4 of 2 to 3; rotating is 1
        // of 1.
        //
        // Every time a second world-changer went in, the first stopped mattering.
        // The carve has one route and it can only run through so many worlds
        // before the extra ones are scenery, and scenery is worse than absence
        // because the player is shown a thing that turns out not to be there.
        //
        // So the back half rotates rather than stacks. A vault is one mechanic at
        // a time, at rising size and tightening decision density, and the pairs
        // that do appear are the two that measured cleanest together.
        //
        // AND THE EVERTER IS DOWN TO TWO VAULTS, on the first cut's own numbers.
        // Of the four it had, GLASS SPINE came back with 0.4 live layers of 1 and
        // the worst route figure in the ladder at 44.8%, and COPPER MARCH with
        // 0.5 — better than half their everters were sitting in cubes no solution
        // touched. The trigger vaults next to them ran 60 to 66% depth against the
        // everter vaults' 38 to 43. GLASS SPINE is cut, which is also what lands
        // this on five hundred rather than five hundred and twenty-five, and
        // COPPER MARCH becomes a trigger vault. THE FAR SIDE keeps the everter
        // because that is where it is taught, and two more carry it afterwards.
        // ---- TWELVE VAULTS, AND THE LADDER STOPS AT THREE HUNDRED -------------
        //
        // This was twenty vaults and five hundred cubes. The cap came down for a
        // reason worth writing next to the list: the safety gate above rejects
        // any cube a player can brick, and a gate that rejects candidates is only
        // survivable if the slot has candidates to spare. Five hundred slots at
        // sixty candidates each was already falling back on out-of-band cubes
        // before the gate existed — a re-cut measured 32 of them under their own
        // vault's floor. Two hundred fewer slots and a bigger pool per slot is
        // the same generator asked an easier question, and a ladder that is
        // shorter and honest beats one that is long and padded.
        //
        // WHICH FOUR SURVIVE THE BACK HALF is chosen on continuity rather than on
        // which cubes were best. Board size has to climb without a gap — the
        // teaching set ends at n=7, so the mastery vaults run 8, 8, 9, 9, and
        // dropping straight to FROST GATE would have skipped n=8 entirely. Par
        // bands rise monotonically. And the four are not all triggers: ASH
        // TERRACE is the plate-under-trigger pair and SINGULARITY is the
        // everter-with-trigger pair, so both combinations that measured cleanest
        // together still appear after they are taught.
        //
        // What goes: REVENANT, COLD FORGE, THE CANT, BONE WORKS, THE HOLLOW,
        // FROST GATE, SHALE KEEP, TIDE WELL. Their mechanics are all still taught
        // in the first eight and all still practised in the last four.
        V("SALT REACH",  8, 6, 10, 1, 1, "T",  0.40, ""),
        V("ASH TERRACE", 8, 7, 12, 1, 2, "AT", 0.32, "a plate under a trigger"),
        V("COPPER MARCH",9, 8, 13, 1, 1, "T",  0.28, ""),
        V("SINGULARITY", 9, 9, 15, 2, 2, "ET", 0.22, ""),
    };

    static Vault V(string name, int n, int lo, int hi, int locks, int glyphs, string set,
                   double openMax, string teaches)
        => new Vault
        {
            name = name, n = n, parLo = lo, parHi = hi, locks = locks,
            glyphs = glyphs, kinds = set == null ? 0 : set.Length, set = set,
            openMax = openMax, teaches = teaches
        };

    public const int PerVault = 25;
    public static int Total => Ladder.Length * PerVault;

    /// <summary>How many candidates a slot looks at before it settles.</summary>
    const int DefaultBudget = 60;

    // ---- what a candidate is worth ---------------------------------------

    class Cand
    {
        public Level lv;
        public int par, steps, legal, onPar;
        public bool loadBearing;      // does the world-changer it carries actually change par?
        public int spare;             // trace cells the prune could take away
        public double routeOpen = 1.0;   // the same question asked at every fold, not just the first
        public int layers;            // world-changers the cube carries
        public int liveLayers;        // ... and how many of them change par on their own
        public double depth;          // share of the route's worlds that are not the opening one
        public int points;            // how many folds along the route were a decision point
        public double open => legal == 0 ? 0.0 : onPar / (double)legal;
        public double fill;
        public string id;
        public double score;
    }

    public static void Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "probe") { Probe(); return; }
        if (args.Length > 1 && args[1] == "compare") { Compare(); return; }
        if (args.Length > 1 && args[1] == "trigger") { Trigger(); return; }
        int budget = args.Length > 1 && int.TryParse(args[1], out int b) ? b : DefaultBudget;

        Console.WriteLine("CURATING " + Total + " CUBES — " + Ladder.Length + " vaults of " + PerVault
                          + ", " + budget + " candidates a slot");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        var chosen = new Cand[Total];
        var taken = new HashSet<string>();
        object gate = new object();
        int done = 0;

        // A SLOT IS INDEPENDENT OF EVERY OTHER SLOT, which is what makes this
        // parallel — and the seed is a pure function of the slot, so the answer
        // does not depend on how many cores ran it.
        Parallel.For(0, Total, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            slot =>
            {
                Cand best = Pick(slot, budget);
                lock (gate)
                {
                    chosen[slot] = best;
                    if (++done % 25 == 0)
                        Console.WriteLine(string.Format("   {0,4}/{1} slots  {2,6:0.0}s",
                                          done, Total, sw.Elapsed.TotalSeconds));
                }
            });

        Console.WriteLine();
        Report(chosen, taken, sw);
        Emit(chosen);
    }

    /// <summary>
    /// THE SEED IS THE SLOT. Every candidate a slot ever looks at is derived from
    /// its own number, so this run and the next one choose the same cube, on any
    /// machine, at any core count. A curation that is not reproducible is a
    /// curation nobody can review.
    /// </summary>
    static Cand Pick(int slot, int budget)
    {
        Vault v = Ladder[slot / PerVault];
        int level = slot + 1;
        int within = slot % PerVault;

        // The band tightens across a vault, so the twenty-fifth cube of a vault
        // is the hardest thing in it and the first is a way in.
        double t = within / (double)(PerVault - 1);
        int parLo = v.parLo + (int)Math.Round(t * (v.parHi - v.parLo) * 0.45);
        double openMax = v.openMax + (1.0 - v.openMax) * (1.0 - t) * 0.35;

        Spec spec = SpecOf(v, level, parLo);

        // THE EASY ONES WERE THE POOL ALL ALONG.
        //
        // Every run until now threw away thirty-five to thirty-nine of every
        // forty candidates for landing BELOW the par band, and then went hunting
        // for difficulty among the handful left. That had it backwards. A cube is
        // easy because the carve left it swimming in footing, and the prune takes
        // footing away — so an "easy" candidate is not a failure, it is raw
        // material, and there are forty times more of it.
        //
        // So stage one now keeps anything that SOLVES, without an opinion about
        // par. Stage two prunes a shortlist and only then asks whether the result
        // is in band. The pool at the hard end goes from one-in-forty to
        // essentially everything.
        var pool = new List<Cand>();
        for (int i = 0; i < budget; i++)
        {
            uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
            Cand c = Rough(seed, spec, level);
            if (c == null) continue;

            // MEASURED HERE, NOT LATER. This was deferred to a second stage on the
            // grounds that decision density costs "one solve per legal opening
            // fold, which is up to a dozen". It is at most FOUR — Openings walks
            // the four turns off the identity orientation and nothing else — so
            // the whole reason for splitting the pipeline was an estimate that was
            // wrong by a factor of three, and the split is what broke the run.
            Measure(c, spec);
            c.score = Value(c, parLo, v.parHi, openMax, v);
            pool.Add(c);
        }
        if (pool.Count == 0) return null;

        // SORTED BY WHAT THEY ARE WORTH, NOT BY HOW MUCH THERE IS TO CUT.
        //
        // This sorted on spare trace, reasoning that a cube with more footing has
        // more for the prune to take. True, and it is exactly backwards: the cubes
        // with the most spare footing are the ones where every fold is legal and
        // free, so the shortlist was being filled with mush before the scorer ever
        // saw it. Decision density came out at forty per cent — worse than not
        // curating at all, and worse than the run before it.
        //
        // The pool is scored in full now, so the shortlist is simply the best of
        // it, and the prune is offered the good cubes rather than the loose ones.
        pool.Sort((a, bb) => bb.score.CompareTo(a.score));
        int shortlist = Math.Min(Shortlist, pool.Count);

        // The unpruned winner is in the running on its own account: if the prune
        // improves nothing, nothing is what it should change.
        Cand asIs = Safety.Audit(pool[0].lv).Ok ? pool[0] : null;

        // HOW FAR TO PRUNE IS A CHOICE, NOT A MAXIMUM.
        //
        // Pruning to exhaustion doubles par and destroys the cube. Measured over
        // five hundred slots: par 2.2->7.2 became 3.3->10.2, and the share of
        // opening folds that keep par went 7% to THIRTY-SIX — barely better than
        // the shipped ladder's 46 — while route length halved. What is left after
        // a total prune is a corridor in fold-space: a long forced sequence with
        // nothing to get wrong, which is the same failure as a free opening
        // wearing the opposite mask.
        //
        // The scorer could not save it because every shortlisted candidate had
        // been pruned the same distance, so it was choosing between seven
        // identically over-pruned cubes. So the DEPTH varies across the
        // shortlist instead — a quarter, a half, three quarters, all of it — and
        // the scorer picks the amount of pruning as well as the cube. Nothing
        // about the pass changed; it is asked a different question.
        Cand best = asIs;
        for (int i = 0; i < shortlist; i++)
        {
            Cand c = pool[i];
            double frac = Depths[i % Depths.Length];
            if (frac > 0.0)
            {
                c.par = Tighten(c.lv, c.par, (int)Math.Ceiling(c.spare * frac));
                c.steps = c.lv.steps;
                Measure(c, spec);
            }

            // SCORED ON THE ROUTE, NOT THE OPENING. The pool is shortlisted on the
            // opening because that is cheap and roughly right; the winner is chosen
            // on the measure that survives a change of par. They disagree in
            // DIRECTION on exactly this comparison — pruning improves the opening
            // figure from 29% to 15% and worsens the route figure from 32% to 44%
            // — so every run before this one was tuned on a signal pointing the
            // wrong way.
            c.routeOpen = RouteOpen(c.lv, c.par, out c.points, out c.depth);
            c.score = Value(c, parLo, v.parHi, openMax, v);

            // A CUBE THAT CAN BE BRICKED IS NOT A CANDIDATE, AT ANY SCORE.
            //
            // This is a gate and not a term in Value on purpose. Everything else
            // the scorer weighs is a matter of degree — a cube can be a little
            // loose at the opening, a fold outside its band, and still be the
            // best thing available for a slot. There is no amount of decision
            // density that compensates for a state a player can walk into and
            // never walk out of, so it cannot be allowed to trade against
            // anything. See Safety: 134 of the shipped 500 fail this.
            if (!Safety.Audit(c.lv).Ok) continue;

            if (best == null || c.score > best.score) best = c;
            continue;
            c.par = Tighten(c.lv, c.par, (int)Math.Ceiling(c.spare * frac));
            c.steps = c.lv.steps;
            Measure(c, spec);
            c.score = Value(c, parLo, v.parHi, openMax, v);
            if (best == null || c.score > best.score) best = c;
        }
        return best;
    }

    /// <summary>
    /// How much of a cube's spare footing each shortlist slot is allowed to take.
    /// Zero is in the list on purpose: sometimes the carve was already right, and
    /// a tool that can only subtract will always subtract.
    /// </summary>
    static readonly double[] Depths = { 0.0, 0.25, 0.5, 0.8, 1.0 };

    /// <summary>How many of a slot's candidates are worth the prune.</summary>
    const int Shortlist = 10;

    static Spec SpecOf(Vault v, int level, int parLo)
        => new Spec
        {
            n = v.n,
            glyphs = v.glyphs,
            glyphKinds = v.kinds,
            glyphSet = v.set,
            layouts = v.set != null && v.set.IndexOf('T') >= 0 ? 2 : 1,
            // a cube may carry a trigger and a plate; it never carries two triggers

            turns = Math.Min(11, 3 + v.parHi),
            locks = v.locks,
            // Denser than the generated ladder ran, because a sparse cube is a
            // corridor and a corridor has no decisions in it. The audit's
            // "6.9 reachable of 63.5 visible" is that number.
            density = Math.Min(0.66, 0.44 + 0.010 * v.n * 2),
            legMin = 2 + v.n / 3,
            legMax = 4 + v.n / 2,
            parLo = parLo,
            parHi = v.parHi,
            minSteps = Math.Min(6 + v.n * 2, v.n * 3),
            tries = 1,
            decoys = v.glyphs != 0 ? 4 : 5,
            band = level / PerVault,
            level = level
        };

    /// <summary>Why a candidate was thrown away, for the probe. Order matters.</summary>
    public enum Reject { Kept, NoCarve, WrongLocks, Buried, MarkOnDoor, Unsolvable, TooEasy }

    /// <summary>Mint one candidate and measure it. Null if it is not a cube at all.</summary>
    static Cand Make(uint seed, Spec spec, Vault v, int level) => Make(seed, spec, v, level, out _);

    /// <summary>
    /// Stage one: is this a cube at all, and does it solve? No opinion about par,
    /// because the prune has not run yet and par is what the prune changes.
    /// </summary>
    /// <summary>
    /// A raw candidate's LEVEL, for the audit that asks what a pool looks like
    /// rather than which cube wins it. The same seed, the same spec and the same
    /// carve the cut uses — a second way of making a candidate would measure a
    /// pool the ladder was never chosen from.
    /// </summary>
    public static Level RoughLevel(uint seed, Vault v, int level, int parLo, out double stageOne)
        => RoughLevel(seed, v, level, parLo, out stageOne, out _, out _);

    public static Level RoughLevel(uint seed, Vault v, int level, int parLo, out double stageOne,
                                   out int layers, out int liveLayers)
    {
        stageOne = double.MinValue;
        layers = liveLayers = 0;
        Spec spec = SpecOf(v, level, parLo);
        Cand c = Rough(seed, spec, level);
        if (c == null) return null;

        // exactly what Pick scores it with in stage one, which is to say with
        // routeOpen still at the 1.0 nobody has measured yet
        Measure(c, spec);
        layers = c.layers;
        liveLayers = c.liveLayers;
        double openMax = v.openMax + (1.0 - v.openMax) * 0.35;
        stageOne = Value(c, v.parLo, v.parHi, openMax, v);
        return c.lv;
    }

    /// <summary>How many of the pool Pick prunes and re-scores. See Shortlist.</summary>
    public static int ShortlistSize => Shortlist;

    static Cand Rough(uint seed, Spec spec, int level)
    {
        Level lv = Generator.Generate(seed, spec);
        if (lv == null || lv.keys.Count != spec.locks) return null;

        var wrng = Rng.Mulberry32(unchecked(seed + 104729u));
        Generator.Widen(ref wrng, lv, spec.decoys, lv.lockMap);
        lv.ClearEff();

        Surf[] s0 = Projection.Project(lv.n, lv.Eff(0), Ori.Id);
        if (!Projection.SurfaceAt(lv.n, s0, Ori.Id, lv.start)) return null;
        if (lv.DoorIndexAt(lv.start) >= 0 || lv.DoorIndexAt(lv.goal) >= 0) return null;

        SolveResult r = Solver.Solve(lv, spec.parHi + 6);
        if (!r.ok) return null;

        lv.par = r.turns; lv.steps = r.steps; lv.level = level; lv.band = spec.band;

        int solid = 0, trace = 0;
        for (int k = 0; k < lv.vox.Length; k++)
        {
            if (lv.vox[k] != '.') solid++;
            if (lv.vox[k] == '+') trace++;
        }
        return new Cand
        {
            lv = lv, par = r.turns, steps = r.steps,
            fill = solid / (double)lv.vox.Length,
            spare = trace,
        };
    }

    /// <summary>Everything that costs a solve per answer, run once on a pruned cube.</summary>
    static void Measure(Cand c, Spec spec)
    {
        Level lv = c.lv;
        c.legal = 0; c.onPar = 0;

        int solid = 0;
        for (int k = 0; k < lv.vox.Length; k++) if (lv.vox[k] != '.') solid++;
        c.fill = solid / (double)lv.vox.Length;

        // EVERY LAYER ON ITS OWN, not the stack as a whole.
        //
        // Flattening all three at once and finding par moved says only that ONE of
        // them mattered — a cube with a load-bearing plate and two decorative
        // everters passes that test and is a lie about its own difficulty. So each
        // is flattened by itself: how many of the world-changers this cube carries
        // are actually holding the route up?
        c.layers = 0; c.liveLayers = 0;
        if (spec.glyphs > 0)
        {
            var at = new List<int>();
            for (int k = 0; k < lv.vox.Length; k++) if (Level.IsGlyph(lv.vox[k])) at.Add(k);
            c.layers = at.Count;

            foreach (int k in at)
            {
                char had = lv.vox[k];
                lv.vox[k] = '+';
                lv.ClearEff();
                SolveResult f = Solver.Solve(lv, c.par + 3);
                if (!f.ok || f.turns != c.par) c.liveLayers++;
                lv.vox[k] = had;
            }
            lv.ClearEff();
            c.loadBearing = c.liveLayers > 0;
        }

        foreach (SolveState next in Openings(lv))
        {
            c.legal++;
            SolveResult after = Solver.Solve(lv, c.par + 1, next);
            if (after.ok && after.turns + 1 <= c.par) c.onPar++;
        }
    }

    static Cand Make(uint seed, Spec spec, Vault v, int level, out Reject why)
    {
        why = Reject.Kept;
        Level lv = Generator.Generate(seed, spec);
        if (lv == null) { why = Reject.NoCarve; return null; }
        if (lv.keys.Count != spec.locks) { why = Reject.WrongLocks; return null; }

        var wrng = Rng.Mulberry32(unchecked(seed + 104729u));
        Generator.Widen(ref wrng, lv, spec.decoys, lv.lockMap);
        lv.ClearEff();

        Surf[] s0 = Projection.Project(lv.n, lv.Eff(0), Ori.Id);
        if (!Projection.SurfaceAt(lv.n, s0, Ori.Id, lv.start)) { why = Reject.Buried; return null; }
        if (lv.DoorIndexAt(lv.start) >= 0 || lv.DoorIndexAt(lv.goal) >= 0) { why = Reject.MarkOnDoor; return null; }

        SolveResult r = Solver.Solve(lv, spec.parHi);
        if (!r.ok) { why = Reject.Unsolvable; return null; }
        if (r.turns < spec.parLo) { why = Reject.TooEasy; return null; }

        lv.par = r.turns;
        lv.steps = r.steps;
        lv.level = level;
        lv.band = spec.band;

        int solid = 0;
        for (int k = 0; k < lv.vox.Length; k++) if (lv.vox[k] != '.') solid++;

        var c = new Cand { lv = lv, par = r.turns, steps = r.steps, fill = solid / (double)lv.vox.Length };

        // IS THE MECHANIC LOAD-BEARING, OR IS IT DECORATION?
        //
        // The arc check measures this and the number is bleak: of forty generated
        // cubes carrying an everter, SEVEN have a route that is any different for
        // it being there. The other thirty-three have a polarity flip sitting in
        // them that no solution ever stands on — the cube contains the mechanic
        // and does not use it, which is the worst of both, because the player is
        // shown a thing that turns out not to matter.
        //
        // One extra solve answers it: flatten every world-changer to plain footing
        // and ask for par again. If the answer moves, the route went through it.
        // Cheap, because only a handful of candidates get this far.
        if (spec.glyphs > 0)
        {
            var flat = new Level
            {
                n = lv.n, vox = (char[])lv.vox.Clone(), start = lv.start, goal = lv.goal,
                keys = lv.keys, doors = lv.doors, lockMap = lv.lockMap,
                level = lv.level, band = lv.band,
            };
            for (int k = 0; k < flat.vox.Length; k++)
                if (Level.IsGlyph(flat.vox[k])) flat.vox[k] = '+';
            flat.ClearEff();
            SolveResult f = Solver.Solve(flat, r.turns + 3);
            c.loadBearing = !f.ok || f.turns != r.turns;
        }

        // STAGE TWO, and only for something already worth measuring. Every legal
        // opening fold, solved from where it leaves you: how many are still on
        // par? One means the cube opens with a decision. All of them means it
        // opens with a shrug, and a shrug is what a quarter of the shipped
        // ladder opens with.
        foreach (SolveState next in Openings(lv))
        {
            c.legal++;
            SolveResult after = Solver.Solve(lv, r.turns + 1, next);
            if (after.ok && after.turns + 1 <= r.turns) c.onPar++;
        }
        return c;
    }

    /// <summary>
    /// DECISION DENSITY ALONG THE WHOLE ROUTE, not at the first move.
    ///
    /// Every number in this tool until now measured the OPENING fold, and four
    /// runs were compared on it before anybody noticed that it does not transfer.
    /// On a par-three cube the opening is a third of the puzzle; on a par-ten cube
    /// it is a tenth. So "the pruned ladder scores 26% and the unpruned one scores
    /// 7%" was never a comparison — it was two different questions with the same
    /// name, and the pruned cubes were being judged on a tenth of themselves.
    ///
    /// This walks the route instead. At each fold along an optimal solution: how
    /// many of the legal folds from here keep par? Average over the whole route.
    /// A cube that offers a real choice at every one of ten folds is a hard cube
    /// however easy its first move was, and a cube whose first move is agonising
    /// and whose next nine are forced is not.
    ///
    /// WHERE IT IS APPROXIMATE, and it is worth being exact about that. It
    /// measures at the LANDING of each fold, and the player may walk before
    /// folding again — walking is free and can reach several cells, from which
    /// different folds are legal. So this counts the choices on one optimal line
    /// rather than every choice available on every line. It is a lower bound on
    /// the branching and an honest one; the alternative is the full game tree.
    ///
    /// It costs about four solves a fold, so ten times the opening measure on a
    /// par-ten cube. That is why the pool is still shortlisted on the opening and
    /// only the shortlist is walked — and this time the arithmetic has been done
    /// rather than guessed.
    /// </summary>
    /// <summary>
    /// PUBLIC BECAUSE THE AUDIT MUST ASK THE SAME QUESTION THE SCORER ASKED.
    ///
    /// The share of legal folds that keep par, measured at every fold along the
    /// route rather than only the first, and enumerated from everywhere the free
    /// walk reaches rather than from the landing cell — both of which the comment
    /// inside says why. LadderAudit reports the shipped ladder against the same
    /// `openMax` these vaults were cut to, and a second implementation of this
    /// would be a second definition of the ladder's own difficulty axis.
    /// </summary>
    public static double RouteOpen(Level lv, int par, out int points, out double depth)
        => RouteOpen(lv, par, out points, out depth, out _);

    /// <summary>
    /// THE SAME WALK, ALSO REPORTING HOW MANY FOLDS THERE WERE TO CHOOSE FROM.
    ///
    /// routeOpen is a RATIO — the share of legal folds that keep par — and a ratio
    /// cannot tell a corridor from a meadow. A cube where one fold is legal and it
    /// keeps par scores 100%; so does a cube where four are legal and all four
    /// keep par. The first is a forced sequence with nothing to get wrong and the
    /// second is a board with no decisions on it, and this file already names both
    /// as failures — "the same failure as a free opening wearing the opposite
    /// mask" — while scoring them identically.
    ///
    /// The prune is what makes the difference matter. Taking footing away removes
    /// legal folds, so a heavily pruned cube can walk its ratio UP without a
    /// single decision being added. Anything drawing a conclusion from routeOpen
    /// wants the branching factor beside it.
    /// </summary>
    public static double RouteOpen(Level lv, int par, out int points, out double depth,
                                   out double meanLegal)
        => RouteOpen(lv, par, out points, out depth, out meanLegal, out _);

    /// <summary>
    /// AND THE ONE THAT IS ACTUALLY DIFFICULTY: the chance of walking the whole
    /// route on par by choosing at random at every fold.
    ///
    /// The ratio is per-decision and difficulty is not. A cube with three
    /// decisions at 28% and a cube with ten at 40% have the ratio pointing the
    /// wrong way and the second is far harder, because getting to the core on par
    /// means getting EVERY one of them right: 0.28^3 is 2.2% and 0.40^10 is
    /// 0.01%. That is the product of the two axes the ladder treats as separate,
    /// and it is the number a player experiences as "can I still par this".
    /// </summary>
    public static double RouteOpen(Level lv, int par, out int points, out double depth,
                                   out double meanLegal, out double blind)
    {
        points = 0;
        double total = 0, legalSum = 0;
        blind = 1.0;
        SolveState at = Opened(lv);
        int left = par;

        // DEPTH: how much of the solve happens somewhere the opening board cannot
        // show you. Two things count as depth and they are the same thing seen
        // twice — a WORLD other than the one you started in, which is a different
        // board on the same solid, and an ORIENTATION other than the one you were
        // handed, which is a different face of it. A route that never leaves
        // either is a puzzle played entirely on the picture it opened with, and
        // that is what a five-hundred cube ladder cannot be three hundred of.
        var worlds = new HashSet<int> { at.world };
        var oris = new HashSet<int> { at.ori };

        for (int guard = 0; guard < par + 2 && left > 0; guard++)
        {
            // EVERY FOLD AVAILABLE FROM HERE, and "here" is not one cell.
            //
            // Stepping is free, so before folding again the player may be
            // anywhere the walk reaches — and a fold with no footing under your
            // feet may have plenty two cells away. The first version of this
            // enumerated folds from the landing cell alone and reported ZERO
            // decision points on par-thirteen cubes, because from that one cell
            // nothing was legal. It was not measuring a forced route, it was
            // measuring the wrong position, and every 100% it printed was the
            // no-data case defaulting to fully-forced.
            var landings = new Dictionary<long, SolveState>();
            foreach (SolveState from in FreeWalk(lv, at))
                foreach (SolveState cand in Folds(lv, from))
                    landings[SKey(lv.n, cand)] = cand;

            int legal = 0, onPar = 0;
            bool haveNext = false;
            SolveState next = default;

            foreach (SolveState cand in landings.Values)
            {
                legal++;
                SolveResult r = Solver.Solve(lv, left, cand);
                if (!r.ok || r.turns + 1 > left) continue;
                onPar++;
                if (!haveNext) { next = cand; haveNext = true; }
            }

            if (legal > 0)
            {
                total += onPar / (double)legal;
                legalSum += legal;
                points++;

                // ONLY WHILE THE WALK STILL HAS A ROUTE. onPar reaches zero when
                // this walk has lost the line rather than when the cube is
                // impossibly tight — the enumeration is of landings reachable by a
                // free walk, and a route whose next fold is not among them ends
                // here. Multiplying by that zero reports a cube as infinitely hard
                // when what happened is that the measurement stopped, which put
                // eleven of vault VII's twenty-five below one in a trillion and
                // made the vault look like a spike.
                if (onPar > 0) blind *= onPar / (double)legal;
            }
            if (!haveNext) break;
            at = next;
            worlds.Add(at.world);
            oris.Add(at.ori);
            left--;
        }

        // eight worlds and twenty-four orientations are the whole space; a route
        // is scored on the share of each it actually needed
        depth = 0.5 * Math.Min(1.0, (worlds.Count - 1) / 3.0)
              + 0.5 * Math.Min(1.0, (oris.Count - 1) / 6.0);
        meanLegal = points == 0 ? 0.0 : legalSum / points;
        return points == 0 ? 1.0 : total / points;
    }

    /// <summary>
    /// Everywhere a free walk reaches from a state — the solver's zero-cost
    /// expansion, lifted out. It is a set of STATES and not of cells: a step onto
    /// a plate flips the world under your foot and a step through a lock spends a
    /// key, so two routes to the same square can arrive as different situations.
    /// </summary>
    static IEnumerable<SolveState> FreeWalk(Level lv, SolveState from)
    {
        int n = lv.n;
        var seen = new HashSet<long> { SKey(n, from) };
        var q = new List<SolveState> { from };

        for (int i = 0; i < q.Count; i++)
        {
            SolveState s = q[i];
            Ori m = Turns.Oris[s.ori];
            Surf[] surf = Projection.Project(n, lv.Eff(s.world), m);
            Int3 v = Projection.ViewOf(n, m, s.pos);

            for (int t = 0; t < 4; t++)
            {
                int u2 = v.x + Turns.All[t].dx, v2 = v.y + Turns.All[t].dy;
                if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
                Surf raw = surf[u2 * n + v2];
                if (!raw.has || !Level.IsWalkType(raw.t)) continue;

                int nd = s.doors;
                int di = lv.DoorIndexAt(raw.w);
                if (di >= 0 && (s.doors & (1 << di)) == 0)
                {
                    if (Bits(s.kmask) - Bits(s.doors) < 1) continue;
                    nd = s.doors | (1 << di);
                }
                int nk = s.kmask;
                int ki = lv.KeyIndexAt(raw.w);
                if (ki >= 0) nk |= 1 << ki;

                var ns = new SolveState
                {
                    pos = raw.w, ori = s.ori, kmask = nk, doors = nd,
                    world = s.world ^ Level.GlyphBit(raw.t),
                };
                if (seen.Add(SKey(n, ns))) q.Add(ns);
            }
        }
        return q;
    }

    /// <summary>
    /// THE THIRD COPY OF THE STATE KEY, AND THE WORST OF THEM. Mixed-radix
    /// rather than bit-packed, and both radices were too small: kmask got a
    /// base of 8, so a cube with four nodes wrapped, and world got 8 when a
    /// world runs to 31. This flood decides which landings the curator can
    /// reach, so its collisions did not report a wrong number — they hid
    /// candidates from the cut.
    ///
    /// It is the search's key now, like the other two.
    /// </summary>
    static long SKey(int n, SolveState s) => Solver.StateKey(n, s);

    static int Bits(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

    // ---- the prune -------------------------------------------------------

    /// <summary>
    /// TAKE AWAY EVERYWHERE THE PLAYER DOES NOT NEED TO STAND.
    ///
    /// Three experiments now say the same thing: whatever the spec asks for,
    /// thirty-five to thirty-nine of every forty candidates come in EASIER than
    /// the band. The carve intends seven folds and the solver finds four. That is
    /// not the carve failing to build a hard route — it builds one — it is the
    /// carve leaving so much trace behind it that the solver can cut the corner.
    /// Widen then adds more on purpose.
    ///
    /// So this is Widen run backwards. Every trace cell that is not a mark is
    /// tried as LATTICE, and the removal is kept if the cube still solves. It is
    /// monotone in the only direction that matters — taking footing away can
    /// never lower par — so the pass either tightens the cube or leaves it alone,
    /// and it cannot make an unsolvable one.
    ///
    /// TRACE TO LATTICE, NOT TRACE TO VOID. Both stop you standing there; only
    /// one keeps the silhouette. A void changes which cell wins its column, so
    /// carving footing out as void would quietly redraw the board, and the cube
    /// somebody looked at would not be the cube that got measured. Lattice is
    /// solid, occludes exactly as before, and is simply not somewhere you can put
    /// your feet — which is the whole of what is being removed. It is also the
    /// game's own vocabulary rather than a tool's invention.
    ///
    /// IT LIVES HERE AND NOT IN Generator, deliberately. This costs one solve per
    /// candidate cell — seconds on a nine-cube — which is nothing at bake time and
    /// fatal on a phone minting the daily. The shipped generator is untouched, so
    /// no daily and no existing cube moves.
    /// </summary>
    static int Tighten(Level lv, int par, int cap)
    {
        int n = lv.n;

        // the cells a route must be able to stand on, whatever else goes
        var keep = new HashSet<int> { Level.Vidx(n, lv.start), Level.Vidx(n, lv.goal) };
        foreach (Int3 k in lv.keys) keep.Add(Level.Vidx(n, k));
        foreach (Int3 d in lv.doors) keep.Add(Level.Vidx(n, d));

        var pool = new List<int>();
        for (int i = 0; i < lv.vox.Length; i++)
            if (lv.vox[i] == '+' && !keep.Contains(i)) pool.Add(i);

        // A DETERMINISTIC ORDER, because the answer has to be the same on every
        // machine. Seeded off the cube itself so two different cubes are not
        // pruned in the same sequence.
        var rng = Rng.Mulberry32(unchecked((uint)(lv.level * 2654435761u) ^ (uint)pool.Count));
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Next() * (i + 1)) % (i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int tried = 0;
        foreach (int i in pool)
        {
            if (tried++ >= cap) break;
            lv.vox[i] = '#';
            lv.ClearEff();

            // Still reachable at all, and the opening still not buried — a prune
            // that leaves the player standing inside the machine is not a prune.
            Surf[] s0 = Projection.Project(n, lv.Eff(0), Ori.Id);
            SolveResult r = Projection.SurfaceAt(n, s0, Ori.Id, lv.start)
                          ? Solver.Solve(lv, par + 6)
                          : default;
            if (!r.ok) { lv.vox[i] = '+'; lv.ClearEff(); continue; }
            par = r.turns;
            lv.par = r.turns;
            lv.steps = r.steps;
        }
        return par;
    }

    /// <summary>
    /// What a candidate is worth against the slot it is being considered for.
    ///
    /// The terms are ordered by how much they matter and the weights say so.
    /// DECISION DENSITY dominates, because it is the thing the generated ladder
    /// has none of. Par is a band rather than a maximum — a cube far above the
    /// band is not better, it is a cube from a later vault that wandered in.
    /// </summary>
    static double Value(Cand c, int parLo, int parHi, double openMax, Vault v)
    {
        double s = 0;

        // in the band, and nearer the top of it is better
        if (c.par < parLo || c.par > parHi) return -1000 + c.par;
        s += 120 + (c.par - parLo) * 30;

        // A CUBE WITH NO OPENING DECISION IS NOT A PUZZLE, WHATEVER ITS PAR, and
        // after the probe this is not merely the biggest term, it is the ladder.
        // Par tops out around seven however hard the spec asks; the share of
        // opening folds that throw the route away does not top out at all. So a
        // par-6 cube where nothing is free outscores a par-7 where everything is,
        // by a distance, and that is the correct preference rather than a
        // consolation for a generator that cannot go higher.
        if (c.legal == 0) s -= 200;                 // forced first step: not wrong, but not a choice
        else s += (1.0 - c.open) * 200.0;

        // AND THE SAME QUESTION ASKED AT EVERY FOLD, which is where the weight
        // belongs. routeOpen defaults to 1.0 on a candidate that has not been
        // walked, so an unwalked cube simply gains nothing here rather than
        // winning by not having been measured.
        s += (1.0 - c.routeOpen) * 900.0;
        if (c.routeOpen > openMax) s -= (c.routeOpen - openMax) * 600.0;

        // and a cube with only one legal opening fold has not offered a choice
        // either, however costly that fold is
        if (c.legal == 1) s -= 90;

        // A CUBE THAT CARRIES A MECHANIC AND DOES NOT USE IT IS WORSE THAN ONE
        // WITHOUT. Vault VII is called THE FAR SIDE and its job is to teach the
        // everter; a cube where the everter is scenery teaches that the everter is
        // scenery. Weighted near the decision-density term because it is the same
        // kind of claim — that the thing on screen is the thing that matters.
        // EVERY LAYER HAS TO EARN ITS PLACE. A cube carrying three world-changers
        // of which one matters is not a three-layer cube, it is a one-layer cube
        // with two decorations — and it is worse than an honest one-layer cube,
        // because the player is shown two things that turn out not to be there.
        if (v.glyphs > 0)
        {
            s += c.liveLayers * 220.0;
            s -= (c.layers - c.liveLayers) * 300.0;
            if (c.liveLayers == 0) s -= 300.0;
        }

        // AND THE ROUTE HAS TO LEAVE THE BOARD IT OPENED ON. Depth is the share of
        // the world-and-orientation space a solution actually needed; a cube solved
        // without ever seeing a different face or a different board is a flat
        // puzzle whatever its par.
        s += c.depth * 420.0;

        // A ROUTE YOU HAVE TO WALK IS A ROUTE YOU HAVE TO READ, and the prune
        // eats walking first — it takes away trace, and trace is what you walk on.
        // A par-10 cube reached in three steps is ten folds in a row with nothing
        // between them. Weighted up, and with a floor under it, because losing the
        // walk is how the last run turned good cubes into fold sequences.
        s += Math.Min(90, c.steps) * 2.2;
        if (c.steps < c.par) s -= (c.par - c.steps) * 45.0;

        // Fill: too sparse is a corridor, too solid is a brick with a groove in it.
        double want = 0.52;
        s -= Math.Abs(c.fill - want) * 260.0;

        return s;
    }

    /// <summary>
    /// Every legal first fold, as the state it leaves you in. Same enumeration
    /// ContentAudit walks — the four turns off the identity orientation, each
    /// asked whether it has anywhere to land.
    /// </summary>
    static SolveState Opened(Level lv)
    {
        var start = new SolveState { pos = lv.start, ori = 0, kmask = 0, doors = 0, world = 0 };
        int s0 = lv.KeyIndexAt(lv.start);
        if (s0 >= 0) start.kmask = 1 << s0;
        start.world = lv.GlyphAt(lv.start);
        return start;
    }

    static IEnumerable<SolveState> Openings(Level lv) => Folds(lv, Opened(lv));

    /// <summary>Every legal fold from a state, as the state it lands you in.</summary>
    static IEnumerable<SolveState> Folds(Level lv, SolveState start)
    {
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

    // ---- what came out ---------------------------------------------------

    /// <summary>
    /// WRITE THE CATALOGUE OUT, and refuse to write a broken one.
    ///
    /// Two claims have to hold before a cube can be in a fixed ladder, and they
    /// are different claims. It has to SOLVE at the par it is shipped with —
    /// which the solver proved when the cube was chosen and is proved again here
    /// against the encoded form, because what ships is the code and not the
    /// object it came from. And it has to be DISTINCT from every other cube under
    /// all twenty-four orientations and every solid it carries, or the same
    /// puzzle appears twice in one ladder and a player who recognises it is
    /// right.
    ///
    /// The file is share codes and nothing else. Not a C# array — five hundred
    /// literals is a file nobody can review and two people cannot merge, which is
    /// the argument this project already makes about ProjectSettings.asset — and
    /// not a binary, because a catalogue you cannot read in a diff is a catalogue
    /// whose changes nobody can see.
    /// </summary>
    static void Emit(Cand[] chosen)
    {
        string dir = Repo.ResourceDir ?? Path.Combine("unity", "SingularityEngine", "Assets", "Resources");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "catalogue.txt");

        var lines = new List<string>();
        var seen = new HashSet<string>();
        int bad = 0, missing = 0;

        for (int i = 0; i < chosen.Length; i++)
        {
            Cand c = chosen[i];
            if (c == null) { missing++; lines.Add("-"); continue; }

            string code = ShareCode.Encode(c.lv);

            // THROUGH THE CODE, NOT AROUND IT. The catalogue ships as text; a cube
            // that solves in memory and not after a round trip is a cube that does
            // not solve.
            Level back = ShareCode.Decode(code);
            if (back == null) { bad++; lines.Add("-"); continue; }
            back.par = c.par;
            SolveResult r = Solver.Solve(back, c.par);
            if (!r.ok || r.turns != c.par) { bad++; lines.Add("-"); continue; }

            string id = Identity.CanonId(back);
            if (!seen.Add(id)) { bad++; lines.Add("-"); continue; }

            lines.Add(string.Format("{0} {1} {2} {3}", c.par, c.steps, id, code));
        }

        using (var w = new StreamWriter(path))
        {
            w.WriteLine("# SINGULARITY ENGINE — the catalogue. One cube a line, in order.");
            w.WriteLine("# par steps id code");
            w.WriteLine("# " + Ladder.Length + " vaults of " + PerVault + ", cut by tools/UnityStubs curate.");
            foreach (Vault v in Ladder)
                w.WriteLine(string.Format("# {0,-14} n={1} par {2}-{3} {4}{5}",
                    v.name, v.n, v.parLo, v.parHi,
                    v.set == null ? "bare" : v.set,
                    string.IsNullOrEmpty(v.teaches) ? "" : "  — " + v.teaches));
            foreach (string l in lines) w.WriteLine(l);
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(
            "catalogue: {0} cubes written to {1} — {2} empty, {3} rejected at the gate",
            lines.Count - missing - bad, path, missing, bad));
    }

    static void Report(Cand[] chosen, HashSet<string> taken, Stopwatch sw)
    {
        int empty = 0;
        foreach (Cand c in chosen) if (c == null) empty++;

        Console.WriteLine("vault                  n   par        steps  route  depth  layers   fill  empty");
        for (int b = 0; b < Ladder.Length; b++)
        {
            Vault v = Ladder[b];
            int nn = 0, parSum = 0, stepSum = 0, gaps = 0, parMin = 99, parMax = 0, liveSum = 0;
            double openSum = 0, fillSum = 0, routeSum = 0, depthSum = 0;
            for (int i = 0; i < PerVault; i++)
            {
                Cand c = chosen[b * PerVault + i];
                if (c == null) { gaps++; continue; }
                nn++; parSum += c.par; stepSum += c.steps;
                openSum += c.open; fillSum += c.fill;
                routeSum += c.routeOpen; depthSum += c.depth; liveSum += c.liveLayers;
                parMin = Math.Min(parMin, c.par); parMax = Math.Max(parMax, c.par);
            }
            if (nn == 0) { Console.WriteLine(string.Format("{0,-20} {1,2}   — nothing found", v.name, v.n)); continue; }
            Console.WriteLine(string.Format(
                "{0,-20} {1,2}  {2,4:0.0} [{3}-{4}]  {5,5:0.0}  {6,5:0.0}% {7,5:0.0}%  {8,4:0.0}/{9}  {10,4:0.0}%  {11,4}",
                v.name, v.n, parSum / (double)nn, parMin, parMax, stepSum / (double)nn,
                100.0 * routeSum / nn, 100.0 * depthSum / nn, liveSum / (double)nn, v.glyphs,
                100.0 * fillSum / nn, gaps));
        }

        // ---- the two things that must be true ----------------------------
        //
        // Every cube distinct under all twenty-four orientations, and every cube
        // solvable at the par it is being shipped with. The first is what stops
        // the same puzzle appearing twice in one ladder; the second is the
        // promise the whole catalogue rests on.
        int dupes = 0;
        foreach (Cand c in chosen)
        {
            if (c == null) continue;
            c.id = Identity.CanonId(c.lv);
            if (!taken.Add(c.id)) dupes++;
        }

        Console.WriteLine();
        Console.WriteLine(string.Format("{0} slots, {1} filled, {2} empty, {3} duplicate identities",
                          Total, Total - empty, empty, dupes));
        Console.WriteLine(string.Format("done in {0:0.0}s", sw.Elapsed.TotalSeconds));
    }

    /// <summary>
    /// WHAT THE GENERATOR CAN AND CANNOT REACH, before a curation run is worth
    /// starting. Every candidate a middle slot of each vault looks at, tallied by
    /// why it was thrown away — because "nothing found" is four different
    /// problems and they want four different fixes.
    /// </summary>
    /// <summary>
    /// THE ONE COMPARISON THAT WAS NEVER MADE. The same slot's best candidate,
    /// unpruned and pruned, measured BOTH ways — at the opening, which is what
    /// five runs were judged on, and along the route, which is what the question
    /// actually was. If the pruned cubes are genuinely more forced, the route
    /// figure will say so too. If it does not, the last three runs were compared
    /// on an artefact.
    /// </summary>
    /// <summary>
    /// IS A LAYOUT SWAP WORTH A FOURTH VERB? Measured before it is built.
    ///
    /// A, B and E are readings of one solid; a trigger exchanges the solid. That
    /// is a different KIND of thing and it sounds like it must add difficulty —
    /// but so did the everter, and the everter moved nothing. So the same three
    /// cubes are minted at each size with and without one, and the two measures
    /// that have survived scrutiny are compared: decision density along the whole
    /// route, and how much of the world-and-orientation space the solve needed.
    ///
    /// Nothing is shipped by this. ShareCode, Identity, the Forge and the save
    /// format have not been touched — a second voxel array exists in Level and
    /// the carve can fill it, which is exactly enough to answer the question and
    /// nothing like enough to release.
    /// </summary>
    static void Trigger()
    {
Console.WriteLine("            n  set   lay  made  par  steps  route  depth  live/carried");

        foreach (int n in new[] { 6, 7, 8, 9 })
        foreach (string set in new[] { "AB", "ABE", "T", "TU", "ABT", "TUE" })
        {
            int made = 0, parSum = 0, stepSum = 0, live = 0, carried = 0;
            double routeSum = 0, depthSum = 0;

            for (uint i = 0; i < 220 && made < 14; i++)
            {
                var spec = new Spec
                {
                    // one glyph a kind, so "TU" really is two triggers and four
                    // boards rather than one trigger rolled twice
                    n = n, glyphs = Math.Max(1, set.Length - 1), glyphKinds = set.Length,
                    glyphSet = set,
                    layouts = set.IndexOf('U') >= 0 ? 4 : set.IndexOf('T') >= 0 ? 2 : 1,
                    turns = 8, locks = 1, density = 0.50,
                    legMin = 2 + n / 3, legMax = 4 + n / 2,
                    parLo = 1, parHi = 14, minSteps = 6,
                    tries = 1, decoys = 4, band = 8, level = 200 + n,
                };
                Level lv = Generator.Generate(unchecked(i * 7919u + (uint)n * 131u), spec);
                if (lv == null || lv.keys.Count != spec.locks) continue;

                var wr = Rng.Mulberry32(unchecked(i * 104729u));
                Generator.Widen(ref wr, lv, spec.decoys, lv.lockMap);
                lv.ClearEff();

                Surf[] s0 = Projection.Project(lv.n, lv.Eff(0), Ori.Id);
                if (!Projection.SurfaceAt(lv.n, s0, Ori.Id, lv.start)) continue;
                SolveResult r = Solver.Solve(lv, spec.parHi);
                if (!r.ok) continue;

                lv.par = r.turns; lv.level = spec.level;
                made++; parSum += r.turns; stepSum += r.steps;
                routeSum += RouteOpen(lv, r.turns, out _, out double d);
                depthSum += d;

                for (int k = 0; k < lv.vox.Length; k++)
                {
                    if (!Level.IsGlyph(lv.vox[k])) continue;
                    carried++;
                    char had = lv.vox[k];
                    lv.vox[k] = '+'; lv.ClearEff();
                    SolveResult f = Solver.Solve(lv, r.turns + 3);
                    if (!f.ok || f.turns != r.turns) live++;
                    lv.vox[k] = had;
                }
                lv.ClearEff();
            }

            if (made == 0) { Console.WriteLine(string.Format("            {0}  {1,-5}    — nothing", n, set)); continue; }
            Console.WriteLine(string.Format(
                "            {0}  {1,-5} {2,3} {3,5} {4,4:0.0} {5,6:0.0} {6,6:0}% {7,6:0}%  {8,4}/{9}",
                n, set, set.IndexOf('U') >= 0 ? 4 : set.IndexOf('T') >= 0 ? 2 : 1,
                made, parSum / (double)made, stepSum / (double)made,
                100 * routeSum / made, 100 * depthSum / made, live, carried));
        }
    }

    static void Compare()
    {
        Console.WriteLine("                        UNPRUNED                    PRUNED");
        Console.WriteLine("vault           n   par steps  open  route pts   par steps  open  route pts");

        double su = 0, sp = 0, ru = 0, rp = 0;
        int nn = 0;

        for (int b = 0; b < Ladder.Length; b++)
        {
            Vault v = Ladder[b];
            int slot = b * PerVault + PerVault / 2;
            int level = slot + 1;
            double t = (PerVault / 2) / (double)(PerVault - 1);
            int parLo = v.parLo + (int)Math.Round(t * (v.parHi - v.parLo) * 0.45);
            Spec spec = SpecOf(v, level, parLo);

            // the best of the pool on the opening measure, exactly as Pick does
            Cand best = null;
            for (int i = 0; i < 60; i++)
            {
                uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
                Cand c = Rough(seed, spec, level);
                if (c == null) continue;
                Measure(c, spec);
                c.score = Value(c, parLo, v.parHi, v.openMax, v);
                if (best == null || c.score > best.score) best = c;
            }
            if (best == null) { Console.WriteLine(string.Format("{0,-14} {1}   — nothing", v.name, v.n)); continue; }

            double uOpen = best.open;
            double uRoute = RouteOpen(best.lv, best.par, out int uPts, out _);
            int uPar = best.par, uSteps = best.steps;

            // and the same cube with everywhere it does not need to stand removed
            best.par = Tighten(best.lv, best.par, 400);
            best.steps = best.lv.steps;
            Measure(best, spec);
            double pRoute = RouteOpen(best.lv, best.par, out int pPts, out _);

            Console.WriteLine(string.Format(
                "{0,-14} {1}  {2,3} {3,5} {4,5:0}% {5,5:0}% {6,3}  {7,4} {8,5} {9,5:0}% {10,5:0}% {11,3}",
                v.name, v.n, uPar, uSteps, 100 * uOpen, 100 * uRoute, uPts,
                best.par, best.steps, 100 * best.open, 100 * pRoute, pPts));

            su += uOpen; sp += best.open; ru += uRoute; rp += pRoute; nn++;
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(
            "mean   unpruned: opening {0:0.0}%  route {1:0.0}%     pruned: opening {2:0.0}%  route {3:0.0}%",
            100 * su / nn, 100 * ru / nn, 100 * sp / nn, 100 * rp / nn));
    }

    static void Probe()
    {
        const int N = 40;
        Console.WriteLine(string.Format("{0,-14} {1} {2,6}  {3,5} {4,5} {5,5} {6,5} {7,5} {8,5}   {9}",
            "vault", "n", "ms/40", "carve", "locks", "burd", "hard", "easy", "KEPT", "best"));

        var sw = new Stopwatch();
        for (int b = 0; b < Ladder.Length; b++)
        {
            Vault v = Ladder[b];
            int slot = b * PerVault + PerVault / 2;
            int level = slot + 1;
            double t = (PerVault / 2) / (double)(PerVault - 1);
            int parLo = v.parLo + (int)Math.Round(t * (v.parHi - v.parLo) * 0.45);
            Spec spec = SpecOf(v, level, parLo);

            var tally = new int[7];
            Cand best = null;
            sw.Restart();
            for (int i = 0; i < N; i++)
            {
                uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
                Cand c = Make(seed, spec, v, level, out Reject why);
                tally[(int)why]++;
                if (c == null) continue;
                c.score = Value(c, parLo, v.parHi, v.openMax, v);
                if (best == null || c.score > best.score) best = c;
            }
            sw.Stop();

            // AND WHAT THE PRUNE BUYS. The strongest candidate the slot found,
            // then the same cube with everywhere it does not need to stand taken
            // away — which is the whole question this run exists to answer.
            string gain = "";
            if (best != null)
            {
                int was = best.par;
                var sw2 = Stopwatch.StartNew();
                int now = Tighten(best.lv, best.par, 400);
                sw2.Stop();
                int legal = 0, onPar = 0;
                foreach (SolveState next in Openings(best.lv))
                {
                    legal++;
                    SolveResult after = Solver.Solve(best.lv, now + 1, next);
                    if (after.ok && after.turns + 1 <= now) onPar++;
                }
                gain = string.Format(" -> par {0} open {1}% in {2:0}ms",
                                     now, legal == 0 ? 0 : 100 * onPar / legal, sw2.Elapsed.TotalMilliseconds);
            }

            Console.WriteLine(string.Format("{0,-14} {1} {2,6:0}  {3,5} {4,5} {5,5} {6,5} {7,5} {8,5}   {9}{10}",
                v.name, v.n, sw.Elapsed.TotalMilliseconds,
                tally[(int)Reject.NoCarve], tally[(int)Reject.WrongLocks], tally[(int)Reject.Buried],
                tally[(int)Reject.Unsolvable], tally[(int)Reject.TooEasy], tally[(int)Reject.Kept],
                best == null ? "— nothing in range " + parLo + ".." + v.parHi
                             : "par " + best.par + " open " + (100 * best.open).ToString("0") + "%",
                gain));
        }
    }
}
