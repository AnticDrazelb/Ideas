namespace Singularity.Core
{
    public struct Verdict
    {
        public bool ok;
        public string why;
        public int par;
        public int steps;
    }

    /// <summary>
    /// THE RULES A CUBE HAS TO PASS.
    ///
    /// Par is the solver's proven minimum, and every scored thing in this game
    /// reads it, so a cube without one cannot be played — which makes the solver
    /// the Forge's real validator rather than a nicety bolted on at the end. A
    /// cube that cannot be finished is not a hard cube, it is a broken one.
    ///
    /// The geometry checks are the same ones Mint applies to its own candidates,
    /// in the same order, so a hand-built cube is held to exactly the standard a
    /// generated one is.
    /// </summary>
    public static class Validation
    {
        /// <summary>
        /// Does any of the twenty-four orientations put the core on a visible
        /// face? Zero means it is walled in on all six sides and no amount of
        /// folding will ever expose it.
        /// </summary>
        public static bool GoalEverShows(Level lv)
        {
            int n = lv.n;
            for (int i = 0; i < Turns.Oris.Length; i++)
            {
                Surf[] s = Projection.Project(n, lv.Eff(0), Turns.Oris[i]);
                if (Projection.SurfaceAt(n, s, Turns.Oris[i], lv.goal)) return true;
            }
            return false;
        }

        public static Verdict Validate(Level lv)
        {
            int n = lv.n;

            if (lv.start == lv.goal) return Fail("START AND EXIT ARE THE SAME CELL");
            if (lv.keys.Count != lv.doors.Count) return Fail("EVERY NODE NEEDS A LOCK");
            if (!Level.IsWalkType(lv.vox[Level.Vidx(n, lv.start)])) return Fail("START IS NOT ON A TRACE");
            if (!Level.IsWalkType(lv.vox[Level.Vidx(n, lv.goal)])) return Fail("EXIT IS NOT ON A TRACE");

            lv.ClearEff();
            Surf[] s0 = Projection.Project(n, lv.Eff(0), Ori.Id);
            if (!Projection.SurfaceAt(n, s0, Ori.Id, lv.start)) return Fail("START IS BURIED");

            // AND THE EXIT HAS TO BE ON A FACE OF SOMETHING, EVER. The commonest
            // authoring mistake there is — filling a deck solid and putting the
            // exit in the middle of it — otherwise reaches the solver and comes
            // back as "NO SOLUTION EXISTS". True, and useless.
            if (!GoalEverShows(lv)) return Fail("EXIT IS SEALED INSIDE THE SOLID");

            if (lv.DoorIndexAt(lv.start) >= 0 || lv.DoorIndexAt(lv.goal) >= 0)
                return Fail("A LOCK SITS ON THE START OR THE EXIT");

            SolveResult r = Solver.Solve(lv, 60);
            if (!r.ok) return Fail("NO SOLUTION EXISTS");

            // A CUBE YOU CAN FINISH WITHOUT FOLDING IS A CORRIDOR. The generator
            // can never produce one, but the Forge has no floor of its own — and
            // par is the whole scoring system, so a par of zero makes every clear
            // worse than perfect and the win card reads as nonsense.
            if (r.turns < 1) return Fail("NO FOLD NEEDED — THAT IS A CORRIDOR, NOT A CUBE");

            return new Verdict { ok = true, par = r.turns, steps = r.steps };
        }

        static Verdict Fail(string why) => new Verdict { ok = false, why = why };
    }
}
