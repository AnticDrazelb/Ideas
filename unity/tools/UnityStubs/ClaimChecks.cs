using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// EVERY CUBE STILL MAKES ITS ARGUMENT.
///
/// A hand-authored level is not a level somebody liked once. It is a level built
/// to say something, and the thing that separates the two is whether anybody
/// would notice if it stopped saying it. Three hundred curated cubes could rot
/// silently — a tweak to the generator, to footing, to the projection, and a
/// cube that used to force its mechanic quietly starts admitting the lazy
/// solution. Nobody sees it, because it still solves.
///
/// So the claim each slot was cut against is re-proved here, on the cube the
/// game actually serves, on every run. This is the same standard ArcChecks holds
/// the four everter cubes to, applied to the whole ladder.
///
/// The authored cubes are reported rather than asserted against the chapter's
/// claim: the ten at the head of the ladder and the four at the head of THE FAR
/// SIDE carry their own arguments, proved in ArcChecks and in the fact that they
/// were sculpted rather than chosen.
/// </summary>
static class ClaimChecks
{
    public static void Run(Action<bool, string> ok)
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));

        int held = 0, authored = 0, unsolved = 0;
        var missed = new List<string>();
        var perClaim = new Dictionary<string, int>();

        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) { unsolved++; continue; }

            // the authored ones answer to their own arguments, not the chapter's
            if (Baked.TryAt(level, out _)) { authored++; continue; }

            int band = Vaults.VaultOf(level);
            if (band < 0 || band >= Curate.Ladder.Length) continue;
            Curate.Vault v = Curate.Ladder[band];
            int within = level - Vaults.VaultStart(band);
            Claims.Claim claim = v.ClaimAt(within);
            if (claim.kind == Claims.Kind.None) continue;

            SolveResult sol = Solver.Solve(lv, v.parHi + 2);
            if (!sol.ok) { unsolved++; missed.Add("cube " + level + " does not solve"); continue; }

            string key = claim.ToString();
            perClaim.TryGetValue(key, out int n);
            perClaim[key] = n + 1;

            if (Claims.Holds(claim, lv, sol)) held++;
            else missed.Add("cube " + level + " (" + v.name + ") no longer makes its claim " + key);
        }

        foreach (string m in missed) ok(false, m);
        ok(unsolved == 0, unsolved + " cubes in the ladder do not solve at all");

        Console.WriteLine("claims: " + held + " of " + (held + missed.Count)
                        + " cut cubes still make the argument they were cut for, "
                        + "and " + authored + " authored ones answer to their own");

        var keys = new List<string>(perClaim.Keys);
        keys.Sort();
        var line = new System.Text.StringBuilder("claims:");
        foreach (string k in keys) line.Append("  ").Append(k).Append(' ').Append(perClaim[k]);
        Console.WriteLine(line.ToString());
    }
}
