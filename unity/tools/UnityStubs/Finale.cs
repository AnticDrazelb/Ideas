using System;
using System.Collections.Generic;
using System.IO;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// THE HARDEST CUBE IN THE GAME SHOULD BE THE LAST ONE, AND IF IT IS NOT, MOVE IT.
///
/// This was approached from the cutting end twice and neither attempt worked.
/// First by choosing the final slot on difficulty rather than score, which only
/// searches one slot's material and made the finale WEAKER. Then by minting five
/// times the candidates for the last fifth, which took the run from nine minutes
/// to sixty-two and got cube 150 to 1 in 1,369,694 — a thirty-eight-fold
/// improvement, and still behind cube 135 at 1 in 6,569,136.
///
/// Both were the same mistake in different clothes: trying to MAKE the hardest
/// cube appear in one particular slot, when the hardest cube already exists and
/// is simply in the wrong place. Swap the two. The ladder holds a hundred and
/// fifty cubes and the question is only which order they are met in.
///
/// WHAT MAKES IT LEGITIMATE IS THE CLAIM CHECK, and it is the whole of this file.
/// A cube is not interchangeable with another cube: each was cut against the
/// argument its slot makes, and a swap that ignores that would leave two cubes
/// sitting in slots whose claims they do not answer — which ClaimChecks would
/// then report as two broken arguments, correctly. So the swap is CONDITIONAL.
/// Each cube is asked whether it makes the claim of the slot it is moving INTO,
/// and the exchange happens only if both do. Where they do not, this says so and
/// changes nothing, because a finale that is hardest by breaking two chapters is
/// not an improvement.
///
/// The cubes themselves are untouched: the catalogue is one share code a line and
/// a swap moves two lines, so both cubes are byte-identical afterwards.
///
///     dotnet run --project unity/tools/UnityStubs finale        # what it would do
///     dotnet run --project unity/tools/UnityStubs finale apply  # do it
/// </summary>
static class Finale
{
    /// <summary>The chance of parring a cube's whole route by choosing at random.</summary>
    static double Blind(Level lv)
    {
        SolveResult sol = Solver.Solve(lv, lv.par + 2);
        if (!sol.ok) return 1.0;
        Curate.RouteOpen(lv, sol.turns, out _, out _, out _, out double blind);
        return blind;
    }

    static string Odds(double blind) => blind <= 0 ? "?" : (1.0 / blind).ToString("#,0");

    /// <summary>The claim the ladder asks of the cube sitting at this level.</summary>
    static Claims.Claim ClaimAt(int level)
    {
        int band = Vaults.VaultOf(level);
        if (band < 0 || band >= Curate.Ladder.Length) return Claims.Of(Claims.Kind.None);
        return Curate.Ladder[band].ClaimAt(level - Vaults.VaultStart(band));
    }

    /// <summary>Does this cube make the argument that level is cut for?</summary>
    static bool Answers(Level lv, int level)
    {
        Claims.Claim c = ClaimAt(level);
        if (c.kind == Claims.Kind.None) return true;
        int band = Vaults.VaultOf(level);
        SolveResult sol = Solver.Solve(lv, Curate.Ladder[band].parHi + 2);
        return sol.ok && Claims.Holds(c, lv, sol);
    }

    public static void Run(string[] args)
    {
        bool apply = args.Length > 1 && args[1] == "apply";
        string path = Repo.Resource("catalogue.txt");
        if (!File.Exists(path)) { Console.WriteLine("finale: no catalogue"); return; }
        Catalogue.Load(File.ReadAllText(path));

        int last = Vaults.LastCube;

        // ---- where the hard cubes actually are -------------------------------
        var blind = new double[last + 1];
        int hardest = 0;
        for (int level = 1; level <= last; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            blind[level] = Blind(lv);
            if (hardest == 0 || blind[level] < blind[hardest]) hardest = level;
        }

        Console.WriteLine("cube " + last + " is 1 in " + Odds(blind[last]) + " to par by luck");
        Console.WriteLine("the hardest in the ladder is cube " + hardest + " at 1 in " + Odds(blind[hardest]));
        Console.WriteLine();

        if (hardest == last)
        {
            Console.WriteLine("finale: they are the same cube — nothing to move");
            return;
        }

        // ---- would the exchange leave both arguments standing? ---------------
        Level a = LevelSupply.Get(hardest), b = LevelSupply.Get(last);
        bool aFits = Answers(a, last), bFits = Answers(b, hardest);

        Console.WriteLine("moving cube " + hardest + " to slot " + last
                        + ", which asks for " + ClaimAt(last) + ":  " + (aFits ? "it does" : "IT DOES NOT"));
        Console.WriteLine("moving cube " + last + " to slot " + hardest
                        + ", which asks for " + ClaimAt(hardest) + ":  " + (bFits ? "it does" : "IT DOES NOT"));
        Console.WriteLine();

        if (!aFits || !bFits)
        {
            Console.WriteLine("finale: the swap would break an argument, so nothing is changed.");
            Console.WriteLine("        A finale that is hardest by breaking two chapters is not one.");
            return;
        }

        if (!apply)
        {
            Console.WriteLine("finale: the swap holds. Run `finale apply` to write it.");
            return;
        }

        // ---- two lines change places, and nothing else ------------------------
        //
        // The share codes move rather than the cubes being re-encoded, so both are
        // byte-identical afterwards and the only thing that has changed is which
        // order they are met in.
        var lines = new List<string>(File.ReadAllLines(path));
        int ia = IndexOf(lines, hardest), ib = IndexOf(lines, last);
        if (ia < 0 || ib < 0) { Console.WriteLine("finale: could not find both cubes in the file"); return; }

        (lines[ia], lines[ib]) = (lines[ib], lines[ia]);
        File.WriteAllLines(path, lines);
        Console.WriteLine("finale: cube " + hardest + " and cube " + last + " have changed places.");
        Console.WriteLine("        The game now ends on 1 in " + Odds(blind[hardest]) + ".");
    }

    /// <summary>Which line of the file is this cube, skipping the header comments.</summary>
    static int IndexOf(List<string> lines, int level)
    {
        int seen = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (t.Length == 0 || t[0] == '#') continue;
            if (++seen == level) return i;
        }
        return -1;
    }
}
