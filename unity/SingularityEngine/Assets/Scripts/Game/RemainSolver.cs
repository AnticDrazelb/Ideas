using System.Collections.Generic;
using System.Threading;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// FEWEST FROM HERE — par, live, for the position you are actually in.
    ///
    /// The fold counter says what a run has cost. Par says what the cube costs.
    /// Neither answers the question a player is actually holding: is the line I am
    /// on still a good one? Two folds over par is a fact you learn on the win
    /// card, ten moves after the mistake, when it is too late to be information
    /// and can only be a verdict.
    ///
    /// So the solver runs from the CURRENT state and the number goes on the HUD.
    /// Read it as a countdown, and read the colour as the only judgement it makes:
    ///
    ///   arc    folds spent + folds to come is still exactly par. Perfect line.
    ///   lock   it is not. You lost folds you cannot get back, and you knew the
    ///          instant it happened rather than at the end.
    ///   fault  there is no way on from here at all. Undo.
    ///
    /// IT IS NEVER ALLOWED TO COST A FRAME. The web version put this in
    /// requestIdleCallback; here it goes on a worker thread, which is better —
    /// Singularity.Core touches no Unity API, so there is nothing to marshal.
    /// States are cached, which makes undo instant and walking back and forth free.
    /// </summary>
    public class RemainSolver
    {
        public int Value = -2;          // -2 = not answered yet, -1 = no route
        public bool Over;               // the budget ran out, not the routes
        public bool Stale;              // an answer is up but a newer one is coming
        public bool Stuck;

        readonly Dictionary<long, (int v, bool o)> cache = new Dictionary<long, (int, bool)>();
        readonly object gate = new object();
        long _want, _have = long.MinValue;
        Thread _thread;
        bool _pendingStuckToast;

        public void Reset()
        {
            lock (gate) { cache.Clear(); }
            Value = -2; Over = false; Stale = false; Stuck = false;
            _want = _have = long.MinValue;
        }

        static long StateKey(Session s)
        {
            long v = Level.Vidx(s.N, s.pos);
            return ((((v * 24 + Turns.OriIndex(s.M)) << 8) | (uint)s.kmask) << 10)
                   | ((uint)s.doors << 2) | (uint)s.world;
        }

        public void Schedule(Session s)
        {
            if (s.lv == null || s.won) return;
            long k = StateKey(s);
            _want = k;

            lock (gate)
            {
                if (cache.TryGetValue(k, out var hit))
                {
                    // seen this exact state before — undo is instant
                    Apply(k, hit.v, hit.o);
                    return;
                }
            }

            // THE LAST ANSWER STAYS UP, DIMMED, while the next one is found.
            // Blanking it to a dot after every fold made the readout flicker at
            // exactly the moment the player was looking at it, and a number that is
            // two hundred milliseconds old and visibly greyed is more use than no
            // number that is perfectly current. The dot is only for the very first
            // answer of a cube, when there is nothing honest to leave up.
            Stale = true;

            if (_thread != null && _thread.IsAlive) return;   // it will pick up _want when it finishes

            var snapshot = new SolveState
            {
                pos = s.pos, ori = Turns.OriIndex(s.M), kmask = s.kmask, doors = s.doors, world = s.world
            };
            Level lv = s.lv;

            _thread = new Thread(() =>
            {
                SolveResult r = Solver.Solve(lv, 40, snapshot);
                lock (gate)
                {
                    if (cache.Count > 600) cache.Clear();     // a long session, bounded
                    cache[k] = (r.ok ? r.turns : -1, !r.ok && r.over);
                }
            })
            { IsBackground = true, Name = "to-go", Priority = System.Threading.ThreadPriority.BelowNormal };
            _thread.Start();
        }

        /// <summary>Main-thread pump. Picks up whatever the worker has finished.</summary>
        public void Tick(Session s)
        {
            if (s?.lv == null || s.won) return;
            long k = _want;
            if (k == _have && !Stale) return;

            bool got;
            (int v, bool o) e;
            lock (gate) { got = cache.TryGetValue(k, out e); }
            if (!got)
            {
                // the worker finished a stale state; re-arm for the current one
                if (_thread == null || !_thread.IsAlive) Schedule(s);
                return;
            }

            Apply(k, e.v, e.o);
            if (_pendingStuckToast)
            {
                _pendingStuckToast = false;
                s.On.Toast?.Invoke("NO ROUTE — UNDO");
                Haptics.Buzz(Haptics.Fault);
                s.On.StuckChanged?.Invoke();
            }
        }

        void Apply(long k, int v, bool over)
        {
            bool wasStuck = Stuck;
            _have = k;
            Value = v;
            Over = over;
            Stale = false;
            Stuck = v < 0 && !over;
            if (Stuck && !wasStuck) _pendingStuckToast = true;
        }

        /// <summary>What the pill should read, and how it should be judged.</summary>
        public (string text, bool slip, bool dead, bool thinking) Pill(Session s)
        {
            if (Value == -2) return ("·", false, false, true);
            if (Over) return ("40+", false, false, Stale);
            if (Value < 0) return ("✕", false, true, Stale);
            // A STALE NUMBER MUST NOT BE JUDGED. Folds have already gone up and the
            // count has not come down yet, so "spent + to come" reads one over for a
            // few frames — and a colour that means "you just lost the perfect line"
            // is the one thing that must never fire by accident.
            bool slip = !Stale && (s.turns + Value) > s.lv.par;
            return (Value.ToString(), slip, false, Stale);
        }
    }
}
