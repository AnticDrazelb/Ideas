using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// WHAT A CUBE IS FOR, WRITTEN DOWN AS SOMETHING THE SOLVER CAN CHECK.
///
/// The three hundred cubes this replaces were chosen by a scoring function
/// against generic proxies — par band, decision density, depth. Those are good
/// proxies and they are still here, but a proxy cannot say what a cube is ABOUT.
/// A level that scores well can still be another one of those, and a hundred of
/// them in a row is where a puzzle game loses people.
///
/// A claim is the difference. RequiresGlyph says: neutralise the thing this cube
/// carries and it cannot be finished. FootingGated says: at the opening, exactly
/// one fold has anything under it. Those are arguments, and a cube either makes
/// one or it does not — so a claim is a GATE in the cutter and an ASSERTION in
/// the check run, and a cube that quietly stops making its argument is a failed
/// build rather than a thing nobody notices.
///
/// EVERY ONE OF THESE IS CHEAP ON PURPOSE. The cutter evaluates them on every
/// candidate it shortlists, so a claim that needed the route replayed through a
/// Session would cost more than the search it is filtering. Each is a handful of
/// solves or a flood fill, and each is measured on the board as the game hands
/// it over rather than on a model kept alongside it.
/// </summary>
static class Claims
{
    public enum Kind
    {
        None,
        /// <summary>No route to the core without folding at all.</summary>
        MustFold,
        /// <summary>Of the folds legal at the start, exactly one keeps par.</summary>
        SoleOpening,
        /// <summary>Exactly one fold has footing at the start: the choice is already gone.</summary>
        FootingGated,
        /// <summary>A square walkable now that is not walkable after the first fold of the line.</summary>
        FalseFloor,
        /// <summary>Two squares touching on screen whose cells are far apart in the solid.</summary>
        DistantNeighbours,
        /// <summary>The first fold of the optimal line moves the core further away.</summary>
        Detour,
        /// <summary>Neutralise the glyph this cube carries and it is unsolvable.</summary>
        RequiresGlyph,
        /// <summary>
        /// THE KEY IS LOAD-BEARING: demote it to plain trace, the lock never
        /// opens, and there is no route. Chapter IV's argument.
        /// </summary>
        RequiresKey,
        /// <summary>Two opening folds where one order keeps par and the other loses it.</summary>
        OrderGated,

        // ---- and the three that are about the ROUTE rather than the opening ----
        //
        // Everything above is a statement about the board as it is handed over, or
        // about one fold taken from it. They are cheap and they are real, but they
        // share a ceiling: a claim that only looks at the opening cannot say what
        // a cube is like to PLAY, and the back half of the ladder is where that
        // stops being good enough. Forty-six cubes cut against RequiresGlyph is
        // forty-six cubes proving their mechanic is load-bearing — which is the
        // floor every glyph cube should clear, not a chapter's argument.
        //
        // These three read the optimal line itself.

        /// <summary>
        /// FIRING IT AT THE FIRST OPPORTUNITY THROWS THE ROUTE AWAY. The glyph is
        /// standing there, you can walk to it, and touching it now costs you the
        /// cube. The obvious move is wrong, which is the difference between a
        /// puzzle and a search.
        /// </summary>
        HoldTheGlyph,

        /// <summary>
        /// ONE PRESS IS NOT ENOUGH — the optimal line changes world at least
        /// twice. The plate card says "tap it again to press it again" and until
        /// now nothing in the ladder required anybody to.
        /// </summary>
        FiresTwice,

        /// <summary>
        /// THE SAME CELL, IN k DIFFERENT STATES. The route comes back to ground it
        /// has already stood on and the ground has changed underneath it — the one
        /// thing this game can do that a flat puzzle cannot.
        /// </summary>
        Revisit,

        /// <summary>
        /// YOU CANNOT SEE WHERE YOU ARE GOING. The core is in the solid but it is
        /// not the cell winning its column, so the opening board does not show it
        /// anywhere: the first fold is taken blind.
        /// </summary>
        BlindGoal,
    }

    public readonly struct Claim
    {
        public readonly Kind kind;
        public readonly int arg;
        public Claim(Kind k, int a = 0) { kind = k; arg = a; }
        public override string ToString() => arg == 0 ? kind.ToString() : kind + "(" + arg + ")";
    }

    public static Claim Of(Kind k, int arg = 0) => new Claim(k, arg);

    /// <summary>
    /// THE CUBE WITH ITS MECHANIC TAKEN AWAY, and what that means depends on the
    /// mechanic. A plate is demoted to plain trace, so it can still be stood on
    /// and can no longer be fired. An everter and a trigger both ride on the
    /// second array, so removing that leaves the glyph in place, still walkable,
    /// still winning its column, and simply unable to do anything — which is the
    /// honest control, because demoting the glyph would change which cell wins
    /// the column and measure precedence rather than the mechanic.
    /// </summary>
    public static Level Neutralise(Level lv)
    {
        Level flat = lv.Clone();
        bool any = false;

        for (int i = 0; i < flat.vox.Length; i++)
            if (flat.vox[i] == 'A' || flat.vox[i] == 'B') { flat.vox[i] = '+'; any = true; }

        if (flat.alts != null)
        {
            foreach (char[] a in flat.alts)
                if (a != null)
                    for (int i = 0; i < a.Length; i++)
                        if (a[i] == 'A' || a[i] == 'B') a[i] = '+';
            flat.alts = null;
            any = true;
        }

        if (!any) return null;
        flat.ClearEff();
        return flat;
    }

    /// <summary>Does this cube carry anything a claim about glyphs could be about?</summary>
    public static bool CarriesGlyph(Level lv)
    {
        foreach (char c in lv.vox) if (Level.IsGlyph(c)) return true;
        if (lv.alts != null)
            foreach (char[] a in lv.alts)
                if (a != null) foreach (char c in a) if (Level.IsGlyph(c)) return true;
        return false;
    }

    // ---- the board, as the game hands it over ------------------------------

    static Surf[] Board(Level lv, Ori m, int world) => Projection.Project(lv.n, lv.Eff(world), m);

    /// <summary>Every screen square a walk from the player's own square reaches.</summary>
    static bool[] Reach(Level lv, Surf[] surf, Int3 pos, Ori m)
    {
        int n = lv.n;
        var seen = new bool[n * n];
        Int3 v = Projection.ViewOf(n, m, pos);
        if (v.x < 0 || v.y < 0 || v.x >= n || v.y >= n) return seen;
        if (!Projection.Walkable(lv, surf, v.x, v.y, 0)) return seen;

        var q = new List<int> { v.x * n + v.y };
        seen[q[0]] = true;
        for (int i = 0; i < q.Count; i++)
        {
            int u = q[i] / n, w = q[i] % n;
            for (int t = 0; t < 4; t++)
            {
                int u2 = u + Turns.All[t].dx, w2 = w + Turns.All[t].dy;
                if (u2 < 0 || w2 < 0 || u2 >= n || w2 >= n) continue;
                int k = u2 * n + w2;
                if (seen[k] || !Projection.Walkable(lv, surf, u2, w2, 0)) continue;
                seen[k] = true;
                q.Add(k);
            }
        }
        return seen;
    }

    /// <summary>The folds that have footing from the opening position.</summary>
    static List<int> LegalOpening(Level lv)
    {
        var got = new List<int>();
        for (int t = 0; t < 4; t++)
        {
            Ori m2 = Turns.All[t].f(Ori.Id);
            if (Projection.Landing(lv, m2, lv.start, 0, 0, out _)) got.Add(t);
        }
        return got;
    }

    /// <summary>The state one fold on, or null if the fold has no footing.</summary>
    static SolveState? AfterFold(Level lv, SolveState from, int t)
    {
        Ori m = Turns.Oris[from.ori];
        Ori m2 = Turns.All[t].f(m);
        if (!Projection.Landing(lv, m2, from.pos, from.doors, from.world, out Surf landed)) return null;
        return new SolveState
        {
            // the cell the fold puts you on, which is the one that wins its
            // column in the new orientation — not a coordinate arrived at by
            // arithmetic that could disagree with the projection
            pos = landed.w,
            ori = Turns.OriIndex(m2),
            kmask = from.kmask,
            doors = from.doors,
            world = from.world
        };
    }

    /// <summary>
    /// THE STATE THE CUBE IS HANDED OVER IN, AND THE START CELL COUNTS.
    ///
    /// This used to be pos/ori and three zeroes, which is right for almost every
    /// cube and wrong for the ones that begin ON something: a start cell that is a
    /// node is a key already held, and one that is a glyph is a world already
    /// fired. Curate has had this right in `Opened` all along and the claims kept
    /// their own shorter, wronger copy — so on those cubes every claim built on it
    /// was reasoning about a board the player never sees.
    /// </summary>
    static SolveState Opening(Level lv)
    {
        var s = new SolveState { pos = lv.start, ori = Turns.OriIndex(Ori.Id), kmask = 0, doors = 0, world = 0 };
        int k = lv.KeyIndexAt(lv.start);
        if (k >= 0) s.kmask = 1 << k;
        s.world = lv.GlyphAt(lv.start);
        return s;
    }

    /// <summary>
    /// ONE STEP, BY THE SAME RULES THE SOLVER STEPPED BY — the cell that wins the
    /// column in this fold, a door consumed if one is held, a key picked up, and
    /// the world flipped by whatever the cell turns out to be.
    /// </summary>
    static bool Step(Level lv, SolveState from, int dir, out SolveState to)
    {
        to = from;
        int n = lv.n;
        Ori m = Turns.Oris[from.ori];
        Surf[] surf = Projection.Project(n, lv.Eff(from.world), m);
        Int3 v = Projection.ViewOf(n, m, from.pos);

        int u2 = v.x + Turns.All[dir].dx, w2 = v.y + Turns.All[dir].dy;
        if (u2 < 0 || w2 < 0 || u2 >= n || w2 >= n) return false;
        Surf cell = surf[u2 * n + w2];
        if (!cell.has || !Level.IsWalkType(cell.t)) return false;

        int doors = from.doors, kmask = from.kmask;
        int di = lv.DoorIndexAt(cell.w);
        if (di >= 0 && (doors & (1 << di)) == 0)
        {
            if (Popcount(kmask) - Popcount(doors) < 1) return false;
            doors |= 1 << di;
        }
        int ki = lv.KeyIndexAt(cell.w);
        if (ki >= 0) kmask |= 1 << ki;

        to = new SolveState
        {
            pos = cell.w, ori = from.ori, kmask = kmask, doors = doors,
            world = from.world ^ Level.GlyphBit(cell.t),
        };
        return true;
    }

    static int Popcount(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

    /// <summary>
    /// EVERY STATE THE OPTIMAL LINE PASSES THROUGH, the opening included.
    ///
    /// The header of this file says a claim that replayed the route would cost
    /// more than the search it filters. That was an estimate and it was wrong by a
    /// wide margin: a replay is one projection per act — twenty of them on the
    /// longest cube in the game — against a solve that expands thousands of
    /// states. The three claims that use it are the cheapest in the file.
    /// </summary>
    static List<SolveState> Route(Level lv, SolveResult sol)
    {
        var path = new List<SolveState> { Opening(lv) };
        if (sol.line == null) return path;

        SolveState s = path[0];
        foreach (Act a in sol.line)
        {
            if (a.isTurn)
            {
                SolveState? next = AfterFold(lv, s, a.dir);
                if (next == null) return path;
                s = next.Value;
            }
            else if (!Step(lv, s, a.dir, out s)) return path;
            path.Add(s);
        }
        return path;
    }

    // ---- and whether a cube makes its claim --------------------------------

    public static bool Holds(Claim c, Level lv, SolveResult sol)
    {
        if (c.kind == Kind.None) return true;
        if (!sol.ok) return false;

        switch (c.kind)
        {
            case Kind.MustFold: return MustFold(lv);
            case Kind.SoleOpening: return SoleOpening(lv, sol);
            case Kind.FootingGated: return LegalOpening(lv).Count == 1;
            case Kind.FalseFloor: return FalseFloor(lv, sol);
            case Kind.DistantNeighbours: return DistantNeighbours(lv, c.arg);
            case Kind.Detour: return Detour(lv, sol);
            case Kind.RequiresGlyph: return RequiresGlyph(lv);
            case Kind.RequiresKey: return RequiresKey(lv);
            case Kind.OrderGated: return OrderGated(lv, sol);
            case Kind.HoldTheGlyph: return HoldTheGlyph(lv, sol, c.arg);
            case Kind.FiresTwice: return FiresTwice(lv, sol);
            case Kind.Revisit: return Revisit(lv, sol, Math.Max(2, c.arg));
            case Kind.BlindGoal: return BlindGoal(lv);
        }
        return false;
    }

    /// <summary>
    /// A cube you can walk from the start to the core without folding once is a
    /// maze, not this game — and it would still verify, still have a par and
    /// still look right. This is the gate that matters most.
    /// </summary>
    static bool MustFold(Level lv)
    {
        Surf[] s = Board(lv, Ori.Id, 0);
        bool[] seen = Reach(lv, s, lv.start, Ori.Id);
        Int3 g = Projection.ViewOf(lv.n, Ori.Id, lv.goal);
        if (g.x < 0 || g.y < 0 || g.x >= lv.n || g.y >= lv.n) return true;
        // and the goal has to actually be the cell showing in that column
        Surf cell = s[g.x * lv.n + g.y];
        return !(seen[g.x * lv.n + g.y] && cell.has && cell.w == lv.goal);
    }

    /// <summary>
    /// Of the folds that are legal from the start, exactly one keeps you on par.
    /// A first move where every option is fine is a first move that does not
    /// matter; where none is fine the cube opens on a step instead.
    /// </summary>
    static bool SoleOpening(Level lv, SolveResult sol)
    {
        List<int> legal = LegalOpening(lv);
        if (legal.Count < 2) return false;

        int keeps = 0;
        foreach (int t in legal)
        {
            SolveState? next = AfterFold(lv, Opening(lv), t);
            if (next == null) continue;
            SolveResult r = Solver.Solve(lv, sol.turns, next.Value);
            if (r.ok && r.turns + 1 <= sol.turns) keeps++;
        }
        return keeps == 1;
    }

    /// <summary>
    /// Somewhere on the opening board is a square you can stand on that the first
    /// fold of the only good line takes away. The floor is not a promise.
    /// </summary>
    static bool FalseFloor(Level lv, SolveResult sol)
    {
        if (sol.line == null) return false;
        int t = -1;
        foreach (Act a in sol.line) if (a.isTurn) { t = a.dir; break; }
        if (t < 0) return false;

        SolveState? next = AfterFold(lv, Opening(lv), t);
        if (next == null) return false;

        int n = lv.n;
        Surf[] a0 = Board(lv, Ori.Id, 0);
        Surf[] a1 = Board(lv, Turns.Oris[next.Value.ori], next.Value.world);
        bool[] reach0 = Reach(lv, a0, lv.start, Ori.Id);

        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
            {
                if (!reach0[u * n + v]) continue;
                if (!Projection.Walkable(lv, a0, u, v, 0)) continue;
                if (!Projection.Walkable(lv, a1, u, v, 0)) return true;
            }
        return false;
    }

    /// <summary>
    /// TWO SQUARES TOUCHING ON SCREEN AND FAR APART IN THE SOLID — the one thing
    /// the collapse does that nothing else can, and the cube has to contain a
    /// crossing the player can actually stand on rather than one in the fill.
    /// </summary>
    static bool DistantNeighbours(Level lv, int d)
    {
        int n = lv.n;
        Surf[] s = Board(lv, Ori.Id, 0);
        bool[] reach = Reach(lv, s, lv.start, Ori.Id);

        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
            {
                int k = u * n + v;
                if (!reach[k] || !s[k].has) continue;
                for (int t = 0; t < 4; t++)
                {
                    int u2 = u + Turns.All[t].dx, v2 = v + Turns.All[t].dy;
                    if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
                    int k2 = u2 * n + v2;
                    if (!reach[k2] || !s[k2].has) continue;

                    Int3 a = s[k].w, b = s[k2].w;
                    int gap = Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) + Math.Abs(a.z - b.z);
                    if (gap >= d) return true;
                }
            }
        return false;
    }

    /// <summary>
    /// The first fold of the optimal line puts the core further from the player
    /// than it was. You have to go the wrong way on purpose.
    /// </summary>
    static bool Detour(Level lv, SolveResult sol)
    {
        if (sol.line == null) return false;
        int t = -1;
        foreach (Act a in sol.line) if (a.isTurn) { t = a.dir; break; }
        if (t < 0) return false;

        SolveState? next = AfterFold(lv, Opening(lv), t);
        if (next == null) return false;

        int Before(Int3 p, Ori m)
        {
            Int3 pv = Projection.ViewOf(lv.n, m, p);
            Int3 gv = Projection.ViewOf(lv.n, m, lv.goal);
            return Math.Abs(pv.x - gv.x) + Math.Abs(pv.y - gv.y);
        }
        return Before(next.Value.pos, Turns.Oris[next.Value.ori]) > Before(lv.start, Ori.Id);
    }

    /// <summary>Take the mechanic away and there is no route at all.</summary>
    static bool RequiresGlyph(Level lv)
    {
        Level flat = Neutralise(lv);
        if (flat == null) return false;
        return !Solver.Solve(flat, 20).ok;
    }

    /// <summary>
    /// TAKE THE KEY AWAY AND THE DOOR NEVER OPENS.
    ///
    /// There was a GlyphCheaper here — solvable without the mechanic, and at
    /// least one fold cheaper with it — and it failed on twenty of twenty cubes,
    /// which is a definition rather than a shortage. Neutralising a plate, an
    /// everter or a trigger does not make a cube COSTLIER, it makes it
    /// UNSOLVABLE: the cells that were only reachable in the other world are
    /// simply gone. So "cheaper with" is the complement of RequiresGlyph rather
    /// than a claim beside it, and it has no cubes to describe.
    ///
    /// This is the claim that band actually wanted, on the chapter that has keys
    /// in it: the node demoted to plain trace, still walkable and no longer a
    /// key, so the lock stays shut and the route has to not exist.
    /// </summary>
    static bool RequiresKey(Level lv)
    {
        if (lv.keys == null || lv.keys.Count == 0) return false;
        if (lv.doors == null || lv.doors.Count == 0) return false;

        Level flat = lv.Clone();
        flat.keys = new List<Int3>();
        flat.ClearEff();
        return !Solver.Solve(flat, 20).ok;
    }

    /// <summary>
    /// TWO FOLDS AND AN ORDER BETWEEN THEM. Both are legal at the start; taking
    /// them one way round keeps par and the other way round does not. The cube
    /// is not asking which fold, it is asking which first.
    /// </summary>
    static bool OrderGated(Level lv, SolveResult sol)
    {
        List<int> legal = LegalOpening(lv);
        if (legal.Count < 2) return false;

        for (int i = 0; i < legal.Count; i++)
            for (int j = 0; j < legal.Count; j++)
            {
                if (i == j) continue;
                int keptAB = Pair(lv, sol, legal[i], legal[j]);
                int keptBA = Pair(lv, sol, legal[j], legal[i]);
                if (keptAB == 1 && keptBA == 0) return true;
            }
        return false;
    }

    // ---- and the three that read the line ----------------------------------

    /// <summary>
    /// TOUCH IT NOW AND YOU HAVE LOST THE CUBE.
    ///
    /// Every state a free walk from the opening can reach WITHOUT folding is
    /// enumerated; the ones where the world has changed are the glyphs the player
    /// can get to before they have done anything. If solving from any of those
    /// still comes in on par, the mechanic is safe to press on sight and the cube
    /// is not asking a question about it. The claim holds only when every one of
    /// them loses.
    ///
    /// It has to be every one, and it has to be reachable BEFORE a fold, because
    /// that is the state a player is actually in when the temptation is there. A
    /// glyph four folds deep is not the obvious move.
    ///
    /// Note this is not "you must never fire it" — the route almost always fires
    /// it later. It is "not from here, not yet", which is a thing a player learns
    /// by losing a cube to it once and never forgets.
    /// </summary>
    static bool HoldTheGlyph(Level lv, SolveResult sol, int mode)
    {
        SolveState open = Opening(lv);
        var fired = new List<SolveState>();
        foreach (SolveState st in Walk(lv, open))
            if (st.world != open.world) fired.Add(st);

        if (fired.Count == 0) return false;          // nothing to be tempted by

        int lost = 0;
        foreach (SolveState st in fired)
        {
            SolveResult r = Solver.Solve(lv, sol.turns, st);
            if (!(r.ok && r.turns <= sol.turns)) lost++;
        }

        // mode 0: EVERY temptation loses — the glyph must be held, full stop.
        // mode 1: at least one does — there is a trap on the board, and telling it
        //         from the safe one is the cube's question.
        return mode == 0 ? lost == fired.Count : lost > 0;
    }

    /// <summary>
    /// THE CORE IS NOT ON THE OPENING BOARD. It is in the solid, but something
    /// nearer the camera wins its column, so there is nowhere on screen to aim at
    /// and the first fold is taken on faith.
    /// </summary>
    static bool BlindGoal(Level lv)
    {
        Surf[] s = Board(lv, Ori.Id, 0);
        Int3 g = Projection.ViewOf(lv.n, Ori.Id, lv.goal);
        if (g.x < 0 || g.y < 0 || g.x >= lv.n || g.y >= lv.n) return true;
        Surf cell = s[g.x * lv.n + g.y];
        return !(cell.has && cell.w == lv.goal);
    }

    /// <summary>
    /// ONE PRESS IS NOT ENOUGH. The optimal line's world changes at least twice,
    /// so the cube cannot be finished by firing the thing once and walking out —
    /// you go through, and you come back through.
    /// </summary>
    static bool FiresTwice(Level lv, SolveResult sol)
    {
        List<SolveState> path = Route(lv, sol);
        int changes = 0;
        for (int i = 1; i < path.Count; i++)
            if (path[i].world != path[i - 1].world) changes++;
        return changes >= 2;
    }

    /// <summary>
    /// THE SAME CELL, TWICE, FACING A DIFFERENT WAY.
    ///
    /// A route that stands on a cell it has already stood on is only interesting
    /// if something about it changed, so the pair has to differ in ORIENTATION or
    /// in WORLD — the same ground seen down a different axis, or the same ground
    /// with the lattice inverted under it. Standing on a cell twice in the same
    /// state is a route that wasted a step, and the solver does not produce those.
    /// </summary>
    static bool Revisit(Level lv, SolveResult sol, int k)
    {
        List<SolveState> path = Route(lv, sol);
        var states = new Dictionary<Int3, HashSet<int>>();
        foreach (SolveState st in path)
        {
            if (!states.TryGetValue(st.pos, out HashSet<int> set))
                states[st.pos] = set = new HashSet<int>();
            set.Add(st.ori * 64 + st.world);
            if (set.Count >= k) return true;
        }
        return false;
    }

    /// <summary>
    /// EVERY STATE A FREE WALK REACHES from here — no folding, and the world
    /// flipping under the player's feet as they cross whatever is on the floor.
    /// </summary>
    static List<SolveState> Walk(Level lv, SolveState from)
    {
        int n = lv.n;
        var seen = new HashSet<long> { Solver.StateKey(n, from) };
        var q = new List<SolveState> { from };

        for (int i = 0; i < q.Count; i++)
            for (int t = 0; t < 4; t++)
                if (Step(lv, q[i], t, out SolveState ns) && seen.Add(Solver.StateKey(n, ns)))
                    q.Add(ns);

        return q;
    }

    /// <summary>1 if folding a then b keeps par, 0 if it does not, -1 if b has no footing.</summary>
    static int Pair(Level lv, SolveResult sol, int a, int b)
    {
        SolveState? one = AfterFold(lv, Opening(lv), a);
        if (one == null) return -1;
        SolveState? two = AfterFold(lv, one.Value, b);
        if (two == null) return 0;
        SolveResult r = Solver.Solve(lv, sol.turns, two.Value);
        return r.ok && r.turns + 2 <= sol.turns ? 1 : 0;
    }
}
