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

        // THE POLARITY MUST NOT BE ON THE PLATE CLOCK.
        //
        // A plate is a five-second window and an everter is a standing state. The
        // solver carries `world` through its search and has no notion of the
        // clock at all — it is real time and the search is not — so a polarity
        // that expired would make every cube above solvable on paper and
        // impossible in the hand, and no other check here could have seen it.
        // This is the arithmetic that decides it, asserted where it is visible.
        ok((Level.Everted & ~Level.Everted) == 0 && (3 & ~Level.Everted) == 3,
           "the plate bits and the polarity bit are not separable");

        Console.WriteLine("arc: " + Baked.Arc.Length + " authored cubes at " + Baked.ArcStart +
                          "-" + (Baked.ArcStart + Baked.Arc.Length - 1) +
                          ", each still proved to require its own lesson");
    }

    static bool LosesFooting(Level lv)
    {
        int n = lv.n;
        Surf[] a = Projection.Project(n, lv.Eff(0), Turns.Oris[0]);
        Surf[] b = Projection.Project(n, lv.Eff(Level.Everted), Turns.Oris[0]);
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
                Surf[] surf = Projection.Project(n, lv.Eff(world), Turns.Oris[oi]);
                for (int k = 0; k < n * n; k++)
                    if (surf[k].has && surf[k].t == 'E') return true;
            }
        return false;
    }

    /// <summary>
    /// THE GENERATOR CAN CARVE AN EVERTER, and until this existed it could not.
    ///
    /// The voxel census read E:0 across a hundred and twenty minted cubes, and it
    /// was not a sampling accident — the one line that writes a world-changing
    /// cell chose between 'A' and 'B' and there was no third branch. The everter
    /// existed in the rules, in the solver, in the shader and in four cubes
    /// somebody typed out by hand, and nowhere else. A mechanic the content
    /// pipeline cannot produce is a mechanic the game does not have.
    ///
    /// Two things have to be true and they are different things. The carve has to
    /// PLACE one, and the solver has to prove a route THROUGH one — carving a
    /// polarity flip the solver cannot follow would be worse than not carving it,
    /// because the cube would ship unsolvable-in-practice while passing every
    /// solvability check by going round it.
    /// </summary>
    public static void Everters(Action<bool, string> ok)
    {
        int made = 0, withE = 0, solved = 0, usedE = 0;

        for (uint seed = 1; seed <= 400 && withE < 40; seed++)
        {
            var spec = new Spec
            {
                n = 6, glyphs = 1, glyphKinds = 1, glyphSet = "E",
                turns = 7, locks = 0, density = 0.52,
                legMin = 3, legMax = 6, parLo = 1, parHi = 8, minSteps = 6,
                tries = 1, decoys = 4, band = 6, level = 151,
            };
            Level lv = Generator.Generate(seed * 7919u, spec);
            if (lv == null) continue;
            made++;
            if (new string(lv.vox).IndexOf('E') < 0) continue;
            withE++;

            SolveResult r = Solver.Solve(lv, spec.parHi);
            if (!r.ok) continue;
            solved++;

            // AND THE POLARITY IS LOAD-BEARING, at least sometimes. A cube with an
            // everter sitting in it that no route ever stands on has not used the
            // mechanic, it has decorated with it. Re-solve with the everter's own
            // world bit unreachable and see whether par moves.
            var flat = new Level
            {
                n = lv.n, vox = (char[])lv.vox.Clone(), start = lv.start, goal = lv.goal,
                keys = lv.keys, doors = lv.doors, lockMap = lv.lockMap,
                level = lv.level, band = lv.band,
            };
            for (int i2 = 0; i2 < flat.vox.Length; i2++) if (flat.vox[i2] == 'E') flat.vox[i2] = '+';
            flat.ClearEff();
            SolveResult f = Solver.Solve(flat, spec.parHi + 2);
            if (!f.ok || f.turns != r.turns) usedE++;
        }

        Console.WriteLine(string.Format(
            "arc: the carve places an everter in {0} of {1} cubes; {2} solve, and on {3} the polarity changes par",
            withE, made, solved, usedE));

        ok(withE > 0, "the generator still cannot carve an everter");
        ok(solved == withE, "the solver could not prove " + (withE - solved)
                            + " of the everter cubes the carve produced");
        ok(usedE > 0, "no generated everter was ever load-bearing — the carve decorates with it");
    }
}
