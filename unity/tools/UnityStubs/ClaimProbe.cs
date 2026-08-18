using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// IS THIS CLAIM FINDABLE, OR IS IT A DEFINITION?
///
/// Two claims have already been written into the ladder and taken back out of it,
/// and both failures had the same shape: a sentence that sounded like a good
/// argument for a cube, gated on, and then discovered to describe almost nothing.
/// GlyphCheaper failed twenty of twenty because it is the logical complement of
/// RequiresGlyph. OrderGated held five of fifteen because it can only ask its
/// question of a cube that opens on a fold, and most cubes open on a step.
///
/// Both were caught late — after a cut, from a check run reporting failures by
/// the dozen. That is an expensive way to learn a claim is empty, and it is
/// avoidable: the cubes are already sitting in the catalogue, so any claim can be
/// asked of them before a single slot is cut against it.
///
/// A claim wanted on a chapter's five slots needs to hold on ENOUGH of that
/// chapter's material for the pool to contain some — the cutter reads its two
/// hundred candidates in score order and takes the first that makes the claim, so
/// a rate of one in five is comfortable, one in twenty is a gamble, and zero is
/// GlyphCheaper again.
///
///     dotnet run --project unity/tools/UnityStubs claims
/// </summary>
static class ClaimProbe
{
    static readonly Claims.Claim[] Asked =
    {
        Claims.Of(Claims.Kind.HoldTheGlyph, 0),
        Claims.Of(Claims.Kind.HoldTheGlyph, 1),
        Claims.Of(Claims.Kind.FiresTwice),
        Claims.Of(Claims.Kind.Revisit, 2),
        Claims.Of(Claims.Kind.Revisit, 3),
        Claims.Of(Claims.Kind.Revisit, 4),
        Claims.Of(Claims.Kind.BlindGoal),
        Claims.Of(Claims.Kind.OrderGated),
        Claims.Of(Claims.Kind.SoleOpening),
        Claims.Of(Claims.Kind.Detour),
        Claims.Of(Claims.Kind.RequiresGlyph),
    };

    public static void Run()
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));

        Console.WriteLine("HOW OFTEN EACH CLAIM HOLDS, on the hundred and fifty as they stand.");
        Console.WriteLine("A claim the cutter is asked to find needs material to find it in.");
        Console.WriteLine();

        int bands = Curate.Ladder.Length;
        var hit = new int[Asked.Length, bands];
        var seen = new int[bands];

        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            int band = Vaults.VaultOf(level);
            if (band < 0 || band >= bands) continue;

            SolveResult sol = Solver.Solve(lv, Curate.Ladder[band].parHi + 2);
            if (!sol.ok) continue;
            seen[band]++;

            for (int c = 0; c < Asked.Length; c++)
                if (Claims.Holds(Asked[c], lv, sol)) hit[c, band]++;
        }

        Console.Write("claim".PadRight(20));
        for (int b = 0; b < bands; b++) Console.Write(Vaults.RomanOf(b).PadLeft(5));
        Console.WriteLine("   all");

        for (int c = 0; c < Asked.Length; c++)
        {
            Console.Write(Asked[c].ToString().PadRight(20));
            int tot = 0, all = 0;
            for (int b = 0; b < bands; b++)
            {
                Console.Write((seen[b] == 0 ? "-" : hit[c, b].ToString()).PadLeft(5));
                tot += hit[c, b]; all += seen[b];
            }
            Console.WriteLine(("  " + tot + "/" + all).PadLeft(8));
        }

        Console.WriteLine();
        Console.WriteLine("read down a column for what a chapter's cubes already do, and across a row");
        Console.WriteLine("for whether a claim has anything to describe at all.");
    }
}
