using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    // THE PAR BANDS ARE WHAT THE GENERATOR CAN ACTUALLY REACH, and the probe is
    // where that number came from rather than a hope. Forty candidates a vault,
    // and thirty-five to thirty-nine of them landed BELOW the band every time —
    // not unsolvable, not uncarvable, just easier than asked for. The ceiling is
    // about par seven at n=9 and it is structural: SpecFor already says why.
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
        V("CALIBRATION", 5, 1, 3, 0, 0, null,  1.00, "the fold, the walk, the core"),
        V("REFRACTION",  5, 2, 4, 0, 0, null,  0.80, "folds that cost you the route"),
        V("INVERSION",   6, 2, 4, 1, 0, null,  0.70, "nodes and the locks they open"),
        V("ENTROPY",     6, 2, 5, 1, 0, null,  0.62, "two pairs, and the order between them"),
        V("EMBERFALL",   6, 3, 5, 0, 1, "A",   0.58, "the plate: two worlds, one board"),
        V("SUBSTRATE",   6, 3, 6, 1, 1, "AB",  0.54, "the other plate, and a lock behind it"),
        V("THE FAR SIDE",7, 3, 6, 0, 1, "E",   0.50, "the everter: the column shows its far cell"),

        // ---- and then nothing new, for three hundred and twenty-five cubes ---
        //
        // From here every cube may carry any of the three, which is the whole
        // point of closing the vocabulary: the interest is in which one turned up
        // and what it is next to, not in learning a fourth thing.
        V("REVENANT",    7, 3, 6, 1, 1, "ABE", 0.48, ""),
        V("COLD FORGE",  7, 4, 7, 2, 1, "ABE", 0.45, ""),
        V("THE CANT",    7, 4, 7, 1, 1, "ABE", 0.42, ""),
        V("SALT REACH",  8, 4, 7, 2, 1, "ABE", 0.40, ""),
        V("BONE WORKS",  8, 4, 7, 1, 1, "ABE", 0.38, ""),
        V("GLASS SPINE", 8, 4, 8, 2, 1, "ABE", 0.35, ""),
        V("THE HOLLOW",  8, 5, 8, 2, 1, "ABE", 0.33, ""),
        V("ASH TERRACE", 8, 5, 8, 2, 1, "ABE", 0.30, ""),
        V("FROST GATE",  9, 5, 8, 2, 1, "ABE", 0.28, ""),
        V("COPPER MARCH",9, 5, 9, 2, 1, "ABE", 0.26, ""),
        V("SHALE KEEP",  9, 5, 9, 2, 1, "ABE", 0.24, ""),
        V("TIDE WELL",   9, 6, 9, 2, 1, "ABE", 0.22, ""),
        V("SINGULARITY", 9, 6, 10, 2, 1, "ABE", 0.20, ""),
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
        public double open => legal == 0 ? 0.0 : onPar / (double)legal;
        public double fill;
        public string id;
        public double score;
    }

    public static void Run(string[] args)
    {
        if (args.Length > 1 && args[1] == "probe") { Probe(); return; }
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

        Cand best = null;
        for (int i = 0; i < budget; i++)
        {
            uint seed = unchecked((uint)(level * 2654435761u) ^ (uint)(i * 40503u) ^ 0x5eed1eu);
            Cand c = Make(seed, spec, v, level);
            if (c == null) continue;
            c.score = Value(c, parLo, v.parHi, openMax, v);
            if (best == null || c.score > best.score) best = c;
        }
        return best;
    }

    static Spec SpecOf(Vault v, int level, int parLo)
        => new Spec
        {
            n = v.n,
            glyphs = v.glyphs,
            glyphKinds = v.kinds,
            glyphSet = v.set,
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
        else s += (1.0 - c.open) * 900.0;
        if (c.open > openMax) s -= (c.open - openMax) * 600.0;

        // and a cube with only one legal opening fold has not offered a choice
        // either, however costly that fold is
        if (c.legal == 1) s -= 90;

        // A CUBE THAT CARRIES A MECHANIC AND DOES NOT USE IT IS WORSE THAN ONE
        // WITHOUT. Vault VII is called THE FAR SIDE and its job is to teach the
        // everter; a cube where the everter is scenery teaches that the everter is
        // scenery. Weighted near the decision-density term because it is the same
        // kind of claim — that the thing on screen is the thing that matters.
        if (v.glyphs > 0) s += c.loadBearing ? 260.0 : -340.0;

        // A route you have to walk is a route you have to READ. Steps are capped
        // so a cube cannot win on wandering alone.
        s += Math.Min(90, c.steps) * 0.8;

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
    static IEnumerable<SolveState> Openings(Level lv)
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

    // ---- what came out ---------------------------------------------------

    static void Report(Cand[] chosen, HashSet<string> taken, Stopwatch sw)
    {
        int empty = 0;
        foreach (Cand c in chosen) if (c == null) empty++;

        Console.WriteLine("vault                  n   par        steps    open   fill   empty");
        for (int b = 0; b < Ladder.Length; b++)
        {
            Vault v = Ladder[b];
            int nn = 0, parSum = 0, stepSum = 0, gaps = 0, parMin = 99, parMax = 0;
            double openSum = 0, fillSum = 0;
            for (int i = 0; i < PerVault; i++)
            {
                Cand c = chosen[b * PerVault + i];
                if (c == null) { gaps++; continue; }
                nn++; parSum += c.par; stepSum += c.steps;
                openSum += c.open; fillSum += c.fill;
                parMin = Math.Min(parMin, c.par); parMax = Math.Max(parMax, c.par);
            }
            if (nn == 0) { Console.WriteLine(string.Format("{0,-20} {1,2}   — nothing found", v.name, v.n)); continue; }
            Console.WriteLine(string.Format(
                "{0,-20} {1,2}  {2,4:0.0} [{3}-{4}]  {5,5:0.0}  {6,5:0.0}%  {7,4:0.0}%  {8,4}",
                v.name, v.n, parSum / (double)nn, parMin, parMax, stepSum / (double)nn,
                100.0 * openSum / nn, 100.0 * fillSum / nn, gaps));
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

            Console.WriteLine(string.Format("{0,-14} {1} {2,6:0}  {3,5} {4,5} {5,5} {6,5} {7,5} {8,5}   {9}",
                v.name, v.n, sw.Elapsed.TotalMilliseconds,
                tally[(int)Reject.NoCarve], tally[(int)Reject.WrongLocks], tally[(int)Reject.Buried],
                tally[(int)Reject.Unsolvable], tally[(int)Reject.TooEasy], tally[(int)Reject.Kept],
                best == null ? "— nothing in range " + parLo + ".." + v.parHi
                             : "par " + best.par + " (want " + parLo + ".." + v.parHi + "), open "
                               + (100 * best.open).ToString("0") + "%"));
        }
    }
}
