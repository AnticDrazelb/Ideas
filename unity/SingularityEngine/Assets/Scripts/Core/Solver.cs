using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>One move in a solution: a free step, or a fold that costs one.</summary>
    public struct Act
    {
        public bool isTurn;
        public int dir;
        public static readonly Act None = new Act { isTurn = false, dir = -1 };
    }

    public struct SolveResult
    {
        public bool ok;
        public int turns;
        public int steps;
        public Act first;
        public bool hasFirst;

        /// <summary>
        /// THE WHOLE LINE, not just its head.
        ///
        /// Report already walks the parent chain end to end — it has to, to count
        /// the steps — and then kept one act of it. A hint wants only the first,
        /// which is why it was written that way, but ANYTHING THAT REPLAYS A
        /// SOLUTION NEEDS ALL OF IT: asking again after each move does not work,
        /// because a step costs nothing, so two states can each be one fold from
        /// the core and each have the other as the head of an optimal line. A
        /// driver following `first` walks between them forever. That is not a
        /// theory — it is what FinishChecks did on 24 cubes before this existed.
        /// </summary>
        public List<Act> line;

        /// <summary>
        /// The search was cut short by the budget rather than exhausted — so the
        /// cube may well be solvable, just not within the folds we were willing
        /// to look for. The HUD reads this as "40+" rather than "no route", which
        /// is the difference between an honest unknown and a false accusation.
        /// </summary>
        public bool over;
    }

    /// <summary>The state a search visits: where you stand, how the engine is folded, and what you carry.</summary>
    public struct SolveState
    {
        public Int3 pos;
        public int ori;
        public int kmask;
        public int doors;
        public int world;
    }

    /// <summary>
    /// 0-1 BFS. A step costs nothing, a fold costs one — so the first time the
    /// core is reached is the FEWEST FOLDS it can possibly be done in, which is
    /// the number a cube is graded and filtered on, and the number on the HUD.
    ///
    /// Buckets rather than a deque because the cost never exceeds a couple of
    /// dozen.
    /// </summary>
    public static class Solver
    {
        public const int DefaultBudget = 40;

        private static int Popcount(int x)
        {
            int c = 0;
            while (x != 0) { x &= x - 1; c++; }
            return c;
        }

        /// <summary>
        /// THE VISITED SET'S KEY, AND IT HAS TO BE THE WHOLE STATE.
        ///
        /// Five fields pack into one long: where you stand, how the engine is
        /// folded, which nodes are taken, which locks are open, and which world
        /// you are in. Two states that differ in any of them are different
        /// states, and a key that cannot tell them apart is a search that visits
        /// one and marks the other done.
        ///
        /// WORLD GETS FIVE BITS BECAUSE THERE ARE FIVE OF THEM. It had two, from
        /// when a world was the two plate bits and nothing else — and then
        /// eversion added a third (4) and the trigger added two more (8, 16),
        /// and this line was not widened either time. The bits did not vanish;
        /// they landed on `doors`. An everted cube with no locks open hashed
        /// identically to an unevated one with lock 1 open, so the search
        /// reached the second, wrote the first off as seen, and reported a par
        /// that was too high by however many folds the discarded route saved.
        ///
        /// It was wrong on five of the five hundred — every one of them a cube
        /// with both a trigger and a lock, which is the only place the collision
        /// is reachable — and cube 500, the last one in the game, was one of
        /// them. The projection cache eight lines below carries the same warning
        /// in its own comment and was widened twice; this was missed both times,
        /// because a narrow cache hands back a wrong picture that somebody sees
        /// and a narrow visited key hands back a wrong NUMBER that looks fine.
        ///
        /// Public because RemainSolver keys its cache on the same state and used
        /// to do it with its own copy of this expression. That is how the two
        /// drifted; there is one of them now.
        /// </summary>
        public static long StateKey(int n, in SolveState s)
        {
            long v = Level.Vidx(n, s.pos);
            return ((((v * 24 + s.ori) << 8) | (uint)s.kmask) << 13)
                   | ((uint)s.doors << 5) | (uint)(s.world & 31);
        }

        private static long Key(int n, in SolveState s) => StateKey(n, s);

        public static SolveResult Solve(Level lv, int budget = DefaultBudget, SolveState? from = null)
        {
            int n = lv.n;

            // Keys are tracked as a bitmask of WHICH ones are taken, never a
            // count — a count lets the search step off a key cell and back on to
            // mint another one.
            SolveState start;
            if (from.HasValue)
            {
                start = from.Value;
            }
            else
            {
                start = new SolveState { pos = lv.start, ori = 0, kmask = 0, doors = 0, world = 0 };
                int s0 = lv.KeyIndexAt(lv.start);
                if (s0 >= 0) start.kmask = 1 << s0;
                // Opening on a plate would fire it before the first frame.
                // Generation never does that; a hand-written cube might.
                start.world = lv.GlyphAt(lv.start);
            }

            var buckets = new List<List<SolveState>>();
            var seen = new Dictionary<long, int>();
            var projCache = new Dictionary<int, Surf[]>();
            var parent = new Dictionary<long, (long from, Act act, bool hasFrom)>();
            bool truncated = false;

            // EIGHT WORLDS PER ORIENTATION, NOT FOUR. The polarity bit rides in
            // the same mask as the two plate bits, so the cache key has to make
            // room for it — multiplying by four here was correct until the moment
            // an everted world existed, and would then have handed back the
            // NEAREST projection for an everted state without any error anywhere.
            Surf[] P(int oi, int wd)
            {
                // thirty-two worlds now: three material bits and two layout bits.
                // This was widened from four to eight when eversion arrived and it
                // is the same trap — a cache keyed narrower than the world it is
                // caching silently hands back another world's projection.
                int k = oi * 32 + wd;
                if (!projCache.TryGetValue(k, out var s))
                    projCache[k] = s = Projection.Project(n, lv.Eff(wd), Turns.Oris[oi]);
                return s;
            }

            void Push(int c, in SolveState s)
            {
                while (buckets.Count <= c) buckets.Add(null);
                (buckets[c] ??= new List<SolveState>()).Add(s);
            }

            void Relax(int cost, in SolveState s, long fromKey, Act act, bool hasFrom)
            {
                if (cost > budget) { truncated = true; return; }
                long k = Key(n, s);
                if (!seen.TryGetValue(k, out int had) || had > cost)
                {
                    seen[k] = cost;
                    parent[k] = (fromKey, act, hasFrom);
                    Push(cost, s);
                }
            }

            Relax(0, start, 0, Act.None, false);

            // Walk the parent chain back from the core. A hint needs only the
            // very first action — the rest is the player's to find — but the
            // LENGTH of the chain is the second difficulty axis the vault curve
            // scales on, so it is measured here where the answer is in hand.
            SolveResult Report(int cost, long endKey)
            {
                var chain = new List<Act>();
                long k = endKey;
                int guard = 0;
                while (parent.TryGetValue(k, out var p) && p.hasFrom && guard++ < 8000)
                {
                    chain.Add(p.act);
                    k = p.from;
                }
                chain.Reverse();
                int steps = 0;
                for (int i = 0; i < chain.Count; i++) if (!chain[i].isTurn) steps++;
                return new SolveResult
                {
                    ok = true,
                    turns = cost,
                    steps = steps,
                    first = chain.Count > 0 ? chain[0] : Act.None,
                    hasFirst = chain.Count > 0,
                    line = chain
                };
            }

            for (int cost = 0; cost <= budget; cost++)
            {
                if (cost >= buckets.Count) continue;
                var q = buckets[cost];
                if (q == null) continue;

                // q grows while we walk it — intended; free steps land here.
                for (int qi = 0; qi < q.Count; qi++)
                {
                    SolveState s = q[qi];
                    long sk = Key(n, s);
                    if (seen[sk] < cost) continue;
                    if (s.pos == lv.goal) return Report(cost, sk);

                    Ori m = Turns.Oris[s.ori];
                    Surf[] surf = P(s.ori, s.world);
                    Int3 v = Projection.ViewOf(n, m, s.pos);

                    // steps — free
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
                            // a lock you can open is a step, not a wall
                            if (Popcount(s.kmask) - Popcount(s.doors) < 1) continue;
                            nd = s.doors | (1 << di);
                        }

                        int nk = s.kmask;
                        int ki = lv.KeyIndexAt(raw.w);
                        if (ki >= 0) nk |= 1 << ki;

                        // a plate fires under the foot that lands on it
                        int nw = s.world ^ Level.GlyphBit(raw.t);

                        Relax(cost,
                              new SolveState { pos = raw.w, ori = s.ori, kmask = nk, doors = nd, world = nw },
                              sk, new Act { isTurn = false, dir = t }, true);
                    }

                    // folds — one each. A plate you FOLD onto does not fire: you
                    // press it with a foot, and rotating the engine is not
                    // pressing it. That also keeps the landing just checked from
                    // being invalidated by the world changing underneath it.
                    for (int t2 = 0; t2 < 4; t2++)
                    {
                        Ori m2 = Turns.All[t2].f(m);
                        if (!Projection.Landing(lv, m2, s.pos, s.doors, s.world, out Surf land)) continue;
                        int nk2 = s.kmask;
                        int ki2 = lv.KeyIndexAt(land.w);
                        if (ki2 >= 0) nk2 |= 1 << ki2;
                        Relax(cost + 1,
                              new SolveState { pos = land.w, ori = Turns.OriIndex(m2), kmask = nk2, doors = s.doors, world = s.world },
                              sk, new Act { isTurn = true, dir = t2 }, true);
                    }
                }
            }

            return new SolveResult { ok = false, over = truncated };
        }
    }
}
