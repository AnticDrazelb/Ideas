using System;
using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>What a vault asks of a cube. Every field is a pure function of the level number.</summary>
    public struct Spec
    {
        public int n;
        public int glyphs, glyphKinds;

        /// <summary>
        /// WHICH WORLD-CHANGING CELLS THIS CUBE MAY CARRY, as the characters
        /// themselves: "A", "AB", "E", "ABE". Null keeps glyphKinds' behaviour,
        /// which is what every shipped SpecFor asks for and what the whole
        /// generated catalogue was minted under.
        ///
        /// It exists because glyphKinds is a COUNT and the everter is not the
        /// third of anything — a cube that must teach the far side has to be able
        /// to demand one rather than hope a three-way roll lands on it.
        /// </summary>
        public string glyphSet;
        public int turns;          // the CARVE's ambition, not the acceptance band
        public int locks;
        public double density;
        public int legMin, legMax;
        public int parLo, parHi;   // the band the solver's answer has to land in
        public int minSteps;
        public int tries, decoys;
        public int band, level;
    }

    /// <summary>
    /// Carving the SOLUTION first, then filling in around it, is the only way
    /// this is tractable: a random cube is a random cube, but a cube built by
    /// walking and folding is guaranteed to contain at least the route that
    /// built it. The solver then reports the true optimum, which is usually
    /// shorter than the carve — and that difference is where the good cubes live.
    /// </summary>
    public static class Generator
    {
        public const int CapLocks = 3;
        public const int GlyphFrom = 20;

        // ---- the difficulty contract ---------------------------------------

        public static (int glyphs, int kinds) GlyphPlan(int level)
        {
            if (level < GlyphFrom) return (0, 0);
            int band = (level - 1) / 10;
            // ONE plate, always. A second lowers par (more worlds to shortcut
            // through) and costs a chunk of search time for the privilege.
            // Variety past vault V comes from WHICH plate it is, not how many.
            return (1, band >= 4 ? 2 : 1);
        }

        public static Spec SpecFor(int level)
        {
            int band = (level - 1) / 10, w = (level - 1) % 10;
            int b = Math.Min(band, 11);   // locks and decoys saturate; SIZE no longer does

            // THE SIZE LADDER IS WHERE THE DIFFICULTY ACTUALLY LIVES. Par counts
            // FOLDS, and folds live in a 24-orientation group whose graph has a
            // diameter of four, so pure rotation distance can never ask for more
            // than four. Everything above that comes from FOOTING: you cannot
            // fold where you have nowhere to stand, so a route gets forced
            // through orientations it did not want. More surface is more places
            // to force it, which is why size is the lever.
            int n = band < 1 ? 5 : band < 3 ? 6 : band < 8 ? 7 : band < 15 ? 8 : 9;

            var gp = GlyphPlan(level);

            int parLo = n == 5 ? 1 : 2;
            // The band has to widen with the cube or the ceiling is still six:
            // Solve(lv, parHi) is a BOUNDED search, so a candidate harder than
            // parHi is not scored lower, it is DISCARDED.
            int parSpan = n <= 6 ? 4 : n == 7 ? 5 : n == 8 ? 6 : 8;
            int carveTurns = Math.Min(9, 3 + b + (gp.glyphs != 0 ? 1 : 0));

            return new Spec
            {
                n = n,
                glyphs = gp.glyphs,
                glyphKinds = gp.kinds,
                turns = carveTurns,
                // locks multiply the search by 2^locks and worlds already multiply
                // it by four; a cube carrying a plate keeps its lock count down so
                // a mint stays inside a couple of hundred milliseconds on a phone
                locks = Math.Min(gp.glyphs != 0 ? 2 : CapLocks, b / 2 + (w >= 6 ? 1 : 0)),
                density = Math.Min(0.62, 0.42 + 0.021 * b + 0.004 * w),
                // Leg length is deliberately NOT scaled with the vault: a longer
                // carve lays down more trace, more trace means more shortcuts, and
                // par falls while route length barely moves.
                legMin = 2 + n / 3,
                legMax = 4 + n / 2,
                parLo = parLo,
                parHi = parLo + parSpan,
                minSteps = Math.Min(5 + band * 2 + w, n * 3),
                // HOW MANY CANDIDATES TO LOOK AT, AS A COUNT AND NEVER AS A CLOCK.
                // A millisecond budget meant a faster machine got through more
                // candidates and therefore chose a DIFFERENT winner, so one cube
                // number was two different puzzles on two devices. Work is counted,
                // not timed: every device does exactly this many passes.
                tries = gp.glyphs != 0 ? 24 : 40,
                decoys = gp.glyphs != 0 ? 3 : 4,
                band = band,
                level = level
            };
        }

        // ---- the carve ------------------------------------------------------

        /// <summary>
        /// Give view square (u,v) footing in this world, and say which world cell
        /// now provides it.
        ///
        /// Carving only ever turns lattice into trace. That is monotone: it can
        /// add footing, never remove it, so carving for the fifth fold can never
        /// break the path carved for the first. A locked cell is one an earlier
        /// leg relies on in a different world; it is never overwritten.
        /// </summary>
        public static bool Carve(int n, char[] vox, Ori m, int u, int v, int? hintDepth,
                                 int world, Dictionary<int, char> lockMap, out Int3 result)
        {
            result = default;
            var ev = new char[vox.Length];
            for (int i = 0; i < vox.Length; i++) ev[i] = Level.EffType(vox[i], world);

            // WHICH CELL WINS THIS COLUMN, and it has to be Projection.Project's
            // answer rather than a paraphrase of it — the carve is laying down
            // footing that the solver will later be asked to stand on, and if the
            // two disagree about which cell is the surface then the carve is
            // guaranteeing a route through a cell nobody can reach.
            //
            // So this is that comparison, term for term: a glyph beats a
            // non-glyph, and between two of the same kind the depth decides —
            // NEAREST normally, FARTHEST when the polarity bit is set. That last
            // clause is the whole of eversion and it is the only thing in this
            // file that ever needed to know about it. Without it the generator
            // could carve a plate but never an everter, which is why the voxel
            // census across a hundred and twenty minted cubes read E:0.
            bool evert = (world & Level.Everted) != 0;

            int best = 0;
            bool bestGlyph = false;
            bool haveBw = false;
            Int3 bw = default;

            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        int id = Level.Vidx(n, x, y, z);
                        if (ev[id] == '.') continue;
                        Int3 vw = Projection.ViewOf(n, m, new Int3(x, y, z));
                        if (vw.x != u || vw.y != v) continue;

                        bool g = Level.IsGlyph(ev[id]);
                        bool win = !haveBw
                                || (g && !bestGlyph)
                                || (g == bestGlyph && (evert ? vw.z < best : vw.z > best));
                        if (!win) continue;

                        best = vw.z;
                        bestGlyph = g;
                        bw = new Int3(x, y, z);
                        haveBw = true;
                    }

            char want = Level.BaseForWalk(world);

            if (haveBw)
            {
                int bi = Level.Vidx(n, bw);
                if (Level.IsGlyph(vox[bi])) { result = bw; return true; }   // a plate is already footing
                if (lockMap != null && lockMap.TryGetValue(bi, out char had) && had != want) return false;
                vox[bi] = want;
                if (lockMap != null) lockMap[bi] = want;
                result = bw;
                return true;
            }

            // Nothing in that column at all: place a cell at the hinted depth.
            // This is the one move that can hide something from another face,
            // which is what the verify pass exists for.
            int d = Math.Max(0, Math.Min(n - 1, hintDepth ?? (n >> 1)));
            Int3 wc = Projection.WorldOf(n, m, new Int3(u, v, d));
            int wi = Level.Vidx(n, wc);
            if (lockMap != null && lockMap.TryGetValue(wi, out char had2) && had2 != want) return false;
            vox[wi] = want;
            if (lockMap != null) lockMap[wi] = want;
            result = wc;
            return true;
        }

        public static Level Generate(uint seed, Spec opt)
        {
            var rng = Rng.Mulberry32(seed);
            int n = opt.n;
            var vox = new char[n * n * n];
            for (int i = 0; i < vox.Length; i++) vox[i] = rng.Next() < opt.density ? '#' : '.';

            Ori m = Ori.Id;
            var path = new List<Int3>();
            int world = 0;
            var lockMap = new Dictionary<int, char>();
            int nGlyph = opt.glyphs;
            int kinds = opt.glyphKinds <= 0 ? 1 : opt.glyphKinds;
            var placed = new List<(Int3 w, char kind)>();

            // the legs a plate is dropped on, spread through the route so the
            // world changes in the MIDDLE of solving rather than at one end
            int legCount = opt.turns + 1;
            var glyphLegs = new HashSet<int>();
            for (int i = 0; i < nGlyph; i++)
                glyphLegs.Add(Math.Max(1, (int)Math.Floor((double)legCount * (i + 1) / (nGlyph + 1) + 0.5)));

            int u0 = 1 + (int)Math.Floor(rng.Next() * (n - 2));
            int v0 = 1 + (int)Math.Floor(rng.Next() * (n - 2));
            if (!Carve(n, vox, m, u0, v0, null, world, lockMap, out Int3 pos)) return null;
            path.Add(pos);

            var order = new int[4];

            bool WalkOnce()
            {
                order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;
                rng.Shuffle(order);
                Int3 vv = Projection.ViewOf(n, m, pos);
                for (int t = 0; t < 4; t++)
                {
                    int u2 = vv.x + Turns.All[order[t]].dx, v2 = vv.y + Turns.All[order[t]].dy;
                    if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
                    if (!Carve(n, vox, m, u2, v2, vv.z, world, lockMap, out Int3 nx)) continue;
                    pos = nx; path.Add(pos);
                    return true;
                }
                return false;
            }

            for (int leg = 0; leg <= opt.turns; leg++)
            {
                int steps = opt.legMin + (int)Math.Floor(rng.Next() * (opt.legMax - opt.legMin + 1));
                for (int st = 0; st < steps; st++) if (!WalkOnce()) break;

                // Drop a plate under the foot standing here, and carry on carving
                // in the world it opens. Without that continuation the plate lands
                // on the final leg and the core ends up ON it, which is rejected —
                // so every candidate died and the vault fell through to fallback.
                if (glyphLegs.Contains(leg) && placed.Count < nGlyph && path.Count > 2)
                {
                    int gi = Level.Vidx(n, pos);
                    char kind;
                    if (!string.IsNullOrEmpty(opt.glyphSet))
                        kind = opt.glyphSet[(int)(rng.Next() * opt.glyphSet.Length) % opt.glyphSet.Length];
                    else
                        kind = (kinds > 1 && rng.Next() < 0.45) ? 'B' : 'A';
                    vox[gi] = kind;
                    lockMap[gi] = kind;
                    placed.Add((pos, kind));
                    world ^= Level.GlyphBit(kind);

                    int after = 2 + (int)Math.Floor(rng.Next() * 3);
                    for (int ea = 0; ea < after; ea++) if (!WalkOnce()) break;
                }

                if (leg == opt.turns) break;

                // fold, then guarantee footing on the far side of it
                int pick = (int)Math.Floor(rng.Next() * 4);
                Ori m2 = Turns.All[pick].f(m);
                Int3 lv2 = Projection.ViewOf(n, m2, pos);
                if (!Carve(n, vox, m2, lv2.x, lv2.y, null, world, lockMap, out Int3 lnd)) continue;
                m = m2; pos = lnd; path.Add(lnd);
            }

            Int3 goal = path[path.Count - 1];
            var lv = new Level
            {
                n = n, vox = vox, start = path[0], goal = goal,
                glyphs = placed, lockMap = lockMap
            };

            if (lv.start == goal) return null;
            // Neither end may be a plate: one would fire before the first frame,
            // the other would change the world at the moment the cube ends.
            if (Level.IsGlyph(vox[Level.Vidx(n, lv.start)])) return null;
            if (Level.IsGlyph(vox[Level.Vidx(n, goal)])) return null;
            if (placed.Count != nGlyph) return null;

            // Keys and locks go on the route, but NEVER on a plate. A shut lock
            // sitting on a plate blocks the plate's own column, which destroys
            // the one property plates are built around — that you can always fold
            // from one.
            Int3? SlotNear(int idx)
            {
                for (int off = 0; off < path.Count; off++)
                {
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        int j = idx + off * sgn;
                        if (j < 1 || j > path.Count - 2) continue;
                        Int3 c = path[j];
                        if (Level.IsGlyph(vox[Level.Vidx(n, c)])) continue;
                        if (c == lv.start || c == goal) continue;
                        if (lv.KeyIndexAt(c) >= 0 || lv.DoorIndexAt(c) >= 0) continue;
                        return c;
                    }
                }
                return null;
            }

            for (int i = 0; i < opt.locks; i++)
            {
                Int3? kw = SlotNear((int)Math.Floor(path.Count * (0.18 + 0.22 * i)));
                Int3? dw = SlotNear((int)Math.Floor(path.Count * (0.55 + 0.2 * i)));
                if (!kw.HasValue || !dw.HasValue || kw.Value == dw.Value) continue;
                lv.keys.Add(kw.Value);
                lv.doors.Add(dw.Value);
            }

            return lv;
        }

        /// <summary>
        /// WIDENING. The carve leaves a single thread: from any face you see the
        /// one route and walk it. Dead ends are what make a face worth reading, so
        /// a few extra surface cells are promoted to trace at random across random
        /// faces — then the cube is re-solved and kept only if the answer did not
        /// get any cheaper. Difficulty from plausible wrong turns, never hidden ones.
        /// </summary>
        public static void Widen(ref Rng.State rng, Level lv, int count, Dictionary<int, char> lockMap)
        {
            int n = lv.n;
            var pool = new List<Int3>();
            for (int c = 0; c < count; c++)
            {
                Ori m = Turns.Oris[(int)Math.Floor(rng.Next() * 24)];
                Surf[] surf = Projection.Project(n, lv.Eff(0), m);
                pool.Clear();
                for (int i = 0; i < surf.Length; i++) if (surf[i].has && surf[i].t == '#') pool.Add(surf[i].w);
                if (pool.Count == 0) continue;
                Int3 w = pool[(int)Math.Floor(rng.Next() * pool.Count)];
                int wi = Level.Vidx(n, w);
                if (Level.IsGlyph(lv.vox[wi])) continue;
                if (lockMap != null && lockMap.ContainsKey(wi)) continue;  // a route in some world needs this
                lv.vox[wi] = '+';
                lv.ClearEff();
            }
        }

        // ---- scoring and the floor -----------------------------------------

        /// <summary>
        /// How well a candidate answers the demand. Being too EASY is a much
        /// smaller sin than being too hard or malformed, because a cube below par
        /// still plays — it just under-delivers.
        ///
        /// TURNS DOMINATE, and by a wide margin. Par is the number on the HUD and
        /// the number the player is playing against; route length only breaks ties
        /// between cubes of equal par.
        /// </summary>
        public static int Score(int turns, int steps, double fill, in Spec spec)
        {
            if (fill < 0.24 || fill > 0.80) return 50;
            if (turns > spec.parHi) return 200;                        // overshoot: usable, not wanted
            if (turns < spec.parLo) return 100 + turns * 10 + Math.Min(60, steps);
            return 1000 + turns * 300 + Math.Min(90, steps) + (steps >= spec.minSteps ? 40 : 0);
        }

        /// <summary>
        /// The floor under everything: a cube that cannot fail to be solvable,
        /// because it is a straight line of traces in open space and nothing else.
        /// It is deliberately boring. It exists so that "every cube the player is
        /// handed has been proved winnable" has no exceptions — not almost none,
        /// none. The tests assert it never actually fires.
        /// </summary>
        public static Level FallbackLevel(int level)
        {
            int n = 5;
            var vox = new char[n * n * n];
            for (int i = 0; i < vox.Length; i++) vox[i] = '.';
            for (int i = 0; i < n; i++) vox[Level.Vidx(n, i, 2, 2)] = '+';
            return new Level
            {
                n = n, vox = vox,
                start = new Int3(0, 2, 2), goal = new Int3(n - 1, 2, 2),
                par = 0, level = level, band = (level - 1) / 10, fallback = true
            };
        }

        /// <summary>
        /// MINT — the only way a cube ever reaches a player.
        ///
        /// Solvability is not filtered for, it is CONSTRUCTED. The solver's job is
        /// the other half: proving the route survived the decoy pass, that the
        /// opening cell is not buried, that the keys really do open the locks in
        /// some order, and reporting the true minimum, which becomes par.
        ///
        /// Then the gate: whatever wins, the cube is solved one final time before
        /// it is handed over, and anything that fails falls back. There is no path
        /// through this function that returns an unwinnable cube — not on a bad
        /// seed, not when the budget expires, not at level nine million.
        /// </summary>
        public static Level Mint(int level, uint? seedOverride = null)
        {
            Spec spec = SpecFor(level);
            uint seed = seedOverride ?? Rng.HashSeed(level);

            Level best = null;
            int bestScore = -1;
            int tries = 0;

            for (int i = 0; i < spec.tries * 14 && tries < spec.tries; i++)
            {
                Level lv = Generate(unchecked(seed + (uint)(i * 7919)), spec);
                if (lv == null) continue;
                if (lv.keys.Count != spec.locks) continue;

                var wrng = Rng.Mulberry32(unchecked(seed + (uint)(i * 104729)));
                Widen(ref wrng, lv, spec.decoys, lv.lockMap);

                lv.ClearEff();
                Surf[] s0 = Projection.Project(lv.n, lv.Eff(0), Ori.Id);
                if (!Projection.SurfaceAt(lv.n, s0, Ori.Id, lv.start)) continue;    // opened buried
                if (lv.DoorIndexAt(lv.start) >= 0 || lv.DoorIndexAt(lv.goal) >= 0) continue;

                // One bounded solve does all the work: past parHi it stops looking,
                // so a cube that is too hard costs no more than one that is right.
                tries++;
                SolveResult r = Solver.Solve(lv, spec.parHi);
                if (!r.ok) continue;

                int solid = 0;
                for (int k = 0; k < lv.vox.Length; k++) if (lv.vox[k] != '.') solid++;
                int sc = Score(r.turns, r.steps, (double)solid / lv.vox.Length, spec);
                if (sc > bestScore)
                {
                    bestScore = sc;
                    best = lv;
                    best.par = r.turns;
                    best.steps = r.steps;
                }

                // Stop only on a cube that maxes the band on BOTH axes. Anything
                // less and the remaining budget is better spent looking: the first
                // in-band candidate is usually the weakest one that qualifies.
                if (r.turns >= spec.parHi && r.steps >= spec.minSteps) break;
            }

            best ??= FallbackLevel(level);

            SolveResult gate = Solver.Solve(best, 60);                  // THE GATE
            if (!gate.ok)
            {
                best = FallbackLevel(level);
                gate = Solver.Solve(best, 60);
            }
            best.par = gate.turns;
            best.steps = gate.steps;
            best.level = level;
            best.band = spec.band;
            return best;
        }
    }
}
