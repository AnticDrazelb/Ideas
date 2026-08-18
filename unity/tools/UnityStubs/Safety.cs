using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// CAN THIS CUBE BE BRICKED?
///
/// Every check in this project until now asks whether a cube CAN be solved. None
/// of them asks whether it can stop being solvable, and those are different
/// questions: the solver answers the first from the opening position and never
/// looks at what the player might do instead.
///
/// It matters because two of this game's rules are one-way in combination.
/// Eversion survives the plate clock on purpose — a polarity that expired would
/// make the arc cubes solvable on paper and impossible in the hand — so once a
/// cube is everted the only way back is another everter. And folding onto a
/// glyph does not fire it, so a player can come to rest on the one cell that
/// would have changed the world back. Put those together on a cube whose everter
/// stops being reachable after eversion and the machine is simply over, with the
/// board still on screen and every fold still legal.
///
/// It is not a corner case. Swept across the shipped catalogue, 134 of the 500
/// cubes hold at least one state a player can legally reach and not come back
/// from — BONE WORKS on 22 of its 25 — and cube 500 is one of them: fold onto
/// its trigger while everted with no node in hand and there is nothing left to
/// do. The plate clock makes it worse rather than better, because ThrowToPlate
/// puts an expired player on the nearest GLYPH, which on that cube is the
/// trigger, which is the trap.
///
/// The audit is a forward flood from the opening state over legal moves, then a
/// reverse flood from every state standing on the core. Anything the first
/// reaches and the second does not is a dead end. Both floods are over the same
/// edge set the solver uses, so a cube that passes here cannot strand a player
/// the solver would have allowed.
///
/// It is cheap enough to gate the cut with: about 1,900 states on an average
/// cube, 38ms, and 19 seconds for the whole ladder.
/// </summary>
static class Safety
{
    public struct Verdict
    {
        public int states;      // everything reachable by legal play
        public int dead;        // ...from which the core cannot be reached
        public bool overflowed; // the guard tripped; treat as unproven, never as safe
        public bool Ok => !overflowed && dead == 0;
    }

    static int Pop(int x) { int c = 0; while (x != 0) { x &= x - 1; c++; } return c; }

    /// <summary>Every state one move from here, by exactly the solver's rules.</summary>
    static void Succ(Level lv, in SolveState s, List<SolveState> outp)
    {
        outp.Clear();
        int n = lv.n;
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
                if (Pop(s.kmask) - Pop(s.doors) < 1) continue;
                nd = s.doors | (1 << di);
            }
            int nk = s.kmask;
            int ki = lv.KeyIndexAt(raw.w);
            if (ki >= 0) nk |= 1 << ki;

            outp.Add(new SolveState
            {
                pos = raw.w, ori = s.ori, kmask = nk, doors = nd,
                world = s.world ^ Level.GlyphBit(raw.t),
            });
        }

        // A FOLD ONTO A GLYPH DOES NOT FIRE IT, which is the rule that makes the
        // trap reachable — you can come to rest on the everter, or the trigger,
        // without pressing either.
        for (int t2 = 0; t2 < 4; t2++)
        {
            Ori m2 = Turns.All[t2].f(m);
            if (!Projection.Landing(lv, m2, s.pos, s.doors, s.world, out Surf land)) continue;
            int nk2 = s.kmask;
            int ki2 = lv.KeyIndexAt(land.w);
            if (ki2 >= 0) nk2 |= 1 << ki2;
            outp.Add(new SolveState
            {
                pos = land.w, ori = Turns.OriIndex(m2), kmask = nk2,
                doors = s.doors, world = s.world,
            });
        }
    }

    public static Verdict Audit(Level lv, int guard = 400000)
    {
        int n = lv.n;
        var start = new SolveState { pos = lv.start, ori = 0, kmask = 0, doors = 0, world = 0 };
        int s0 = lv.KeyIndexAt(lv.start);
        if (s0 >= 0) start.kmask = 1 << s0;
        start.world = lv.GlyphAt(lv.start);

        var id = new Dictionary<long, int>();
        var states = new List<SolveState>();
        var fwd = new List<List<int>>();
        var stack = new Stack<int>();

        int Add(in SolveState s)
        {
            long k = Solver.StateKey(n, s);
            if (id.TryGetValue(k, out int q)) return q;
            q = states.Count;
            id[k] = q;
            states.Add(s);
            fwd.Add(null);
            stack.Push(q);
            return q;
        }

        Add(start);
        var buf = new List<SolveState>();
        while (stack.Count > 0)
        {
            if (states.Count > guard) return new Verdict { states = states.Count, overflowed = true };
            int i = stack.Pop();
            Succ(lv, states[i], buf);
            var lst = new List<int>(buf.Count);
            foreach (var ns in buf) lst.Add(Add(ns));
            fwd[i] = lst;
        }

        int N = states.Count;
        var rev = new List<int>[N];
        for (int i = 0; i < N; i++)
        {
            var l = fwd[i];
            if (l == null) continue;
            foreach (int j in l) (rev[j] ??= new List<int>()).Add(i);
        }

        var good = new bool[N];
        var q2 = new Queue<int>();
        for (int i = 0; i < N; i++)
            if (states[i].pos == lv.goal) { good[i] = true; q2.Enqueue(i); }
        while (q2.Count > 0)
        {
            var r = rev[q2.Dequeue()];
            if (r == null) continue;
            foreach (int j in r) if (!good[j]) { good[j] = true; q2.Enqueue(j); }
        }

        int dead = 0;
        for (int i = 0; i < N; i++) if (!good[i]) dead++;
        return new Verdict { states = N, dead = dead };
    }
}
