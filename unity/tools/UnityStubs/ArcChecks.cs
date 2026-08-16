using System;
using Singularity.Core;

/// <summary>
/// THE FOUR ARC CUBES STILL TEACH WHAT THEY WERE BUILT TO TEACH.
///
/// A teaching cube is not a level, it is a CLAIM: this one cannot be finished
/// while ignoring the mechanic. The claim is what makes it worth its slot, and
/// the claim is exactly the thing that rots — a tweak to the generator, to the
/// projection, to footing, and a cube that used to force eversion quietly starts
/// admitting the old solution. Nobody notices, because it still solves.
///
/// So every claim is re-proved on every run, the same way it was found: solve
/// the cube, then solve the same solid with its everter demoted to ordinary
/// trace, and compare. See ArcSearch for how they were searched for.
/// </summary>
static class ArcChecks
{
    public static void Run(Action<bool, string> ok)
    {
        for (int i = 0; i < Baked.Arc.Length; i++)
        {
            int level = Baked.ArcStart + i;
            BakedLevel b = Baked.Arc[i];
            Level lv = b.ToLevel(level);

            SolveResult with = Solver.Solve(lv, 12);
            ok(with.ok, b.name + " (cube " + level + ") does not solve at all");
            if (!with.ok) continue;
            ok(with.turns == b.par,
               b.name + " claims par " + b.par + " and solves in " + with.turns);

            // the control: the same solid, everter demoted to plain trace
            Level flat = lv.Clone();
            bool hadEverter = false;
            for (int c = 0; c < flat.vox.Length; c++)
                if (flat.vox[c] == 'E') { flat.vox[c] = '+'; hadEverter = true; }
            flat.ClearEff();
            ok(hadEverter, b.name + " has no everter in it — it cannot be teaching eversion");
            SolveResult without = Solver.Solve(flat, 12);

            switch (i)
            {
                case 0:     // THE FAR SIDE — no route at all without everting
                    ok(!without.ok, b.name + " is solvable without the everter, so it teaches nothing");
                    break;

                case 1:     // TWO SHADOWS — a route either way, cheaper everted
                    ok(without.ok, b.name + " is unsolvable without the everter, which is the FIRST lesson");
                    ok(without.ok && with.turns < without.turns,
                       b.name + " is not cheaper everted (" + with.turns + " vs " + without.turns + ")");
                    break;

                case 2:     // NOTHING UNDERNEATH — a square that loses its footing
                    ok(LosesFooting(lv), b.name + " has no square that loses footing when everted");
                    break;

                case 3:     // TURNED INSIDE OUT — a fold only one polarity permits
                    ok(PolarityGatesAFold(lv), b.name + " has no fold that depends on polarity");
                    break;
            }

            // AND THE EVERTER MUST BE REACHABLE. A lesson you cannot walk to is a
            // decoration; the solver proving a route exists does not prove the
            // route goes through the thing being taught.
            ok(Reachable(lv), b.name + " has an everter the player can never stand on");
        }

        Console.WriteLine("arc: " + Baked.Arc.Length + " authored cubes at " + Baked.ArcStart +
                          "-" + (Baked.ArcStart + Baked.Arc.Length - 1) +
                          ", each still proved to require its own lesson");
    }

    static bool LosesFooting(Level lv)
    {
        int n = lv.n;
        Surf[] a = Projection.Project(n, lv.Eff(0), Turns.Oris[0], false);
        Surf[] b = Projection.Project(n, lv.Eff(Level.Everted), Turns.Oris[0], true);
        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
                if (Projection.Walkable(lv, a, u, v, 0) && !Projection.Walkable(lv, b, u, v, 0)) return true;
        return false;
    }

    static bool PolarityGatesAFold(Level lv)
    {
        Ori m = Turns.Oris[0];
        for (int t = 0; t < 4; t++)
        {
            Ori m2 = Turns.All[t].f(m);
            bool up = Projection.Landing(lv, m2, lv.start, 0, 0, out _);
            bool ev = Projection.Landing(lv, m2, lv.start, 0, Level.Everted, out _);
            if (up != ev) return true;
        }
        return false;
    }

    /// <summary>Does the everter surface anywhere, in either polarity, in any orientation?</summary>
    static bool Reachable(Level lv)
    {
        int n = lv.n;
        for (int oi = 0; oi < Turns.Oris.Length; oi++)
            for (int pol = 0; pol < 2; pol++)
            {
                int world = pol == 0 ? 0 : Level.Everted;
                Surf[] surf = Projection.Project(n, lv.Eff(world), Turns.Oris[oi], pol != 0);
                for (int k = 0; k < n * n; k++)
                    if (surf[k].has && surf[k].t == 'E') return true;
            }
        return false;
    }
}
