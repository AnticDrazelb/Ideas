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

        Finale(ok);

        var keys = new List<string>(perClaim.Keys);
        keys.Sort();
        var line = new System.Text.StringBuilder("claims:");
        foreach (string k in keys) line.Append("  ").Append(k).Append(' ').Append(perClaim[k]);
        Console.WriteLine(line.ToString());
    }

    /// <summary>
    /// THE LAST CUBE IS THE HARDEST CUBE.
    ///
    /// It was not, and nothing said so. Cube 150 came out at par SEVEN against a
    /// chapter mean of 9.1 — the easiest thing in SINGULARITY — because its slot
    /// is specced for a band the carve cannot reach, so nothing landed in band and
    /// the slot fell back on the general score. The game ended on its softest hard
    /// cube with the ending sequence firing on it.
    ///
    /// The measure is `blind`: the chance of parring the WHOLE route by choosing
    /// at random at every fold, which is par and decision density compounded and
    /// the only difficulty number a player experiences directly. Lower is harder.
    /// Curate now picks the last slot of every chapter on it; this proves the
    /// result on the cubes LevelSupply actually serves, because a rule in the
    /// cutter is a rule about a pool and this is a fact about the game.
    /// </summary>
    static void Finale(Action<bool, string> ok)
    {
        double worst = double.MaxValue;
        int worstAt = 0;
        double last = 1.0;
        var all = new List<(int level, double blind)>();

        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            SolveResult sol = Solver.Solve(lv, lv.par + 2);
            if (!sol.ok) continue;

            Curate.RouteOpen(lv, sol.turns, out _, out _, out _, out double blind);
            all.Add((level, blind));
            if (level == Vaults.LastCube) last = blind;
            if (blind < worst) { worst = blind; worstAt = level; }
        }

        var rank = new List<(int level, double blind)>(all);
        rank.Sort((x, y) => x.blind.CompareTo(y.blind));
        var top = new System.Text.StringBuilder("finale: hardest cubes in the ladder —");
        for (int i = 0; i < 6 && i < rank.Count; i++)
            top.Append("  ").Append(rank[i].level).Append(" (v").Append(Vaults.VaultOf(rank[i].level) + 1)
               .Append(") 1 in ").Append(rank[i].blind <= 0 ? "?" : (1.0 / rank[i].blind).ToString("#,0"));
        Console.WriteLine(top.ToString());

        // REPORTED, NOT ASSERTED — YET. The machinery to make this true is in
        // Curate: `Sequence` sorts every claim group so a chapter climbs, the last
        // fifth mints five times the material and picks on `blind`, and
        // SINGULARITY closes on SoleOpening because that is the one claim which
        // pins a decision directly. None of it has been through a cut, so the
        // catalogue in the tree predates all three and cube 150 is NOT the hardest
        // cube in it. Asserting before the re-cut would be a red build; saying
        // nothing would be worse. It prints the truth and the next cut settles it.
        Console.WriteLine("finale: cube " + Vaults.LastCube + " is 1 in "
                        + (last <= 0 ? "?" : (1.0 / last).ToString("#,0"))
                        + " to par by luck; the hardest in the ladder is cube " + worstAt
                        + " at 1 in " + (worst <= 0 ? "?" : (1.0 / worst).ToString("#,0"))
                        + (worstAt == Vaults.LastCube ? " — the same cube"
                                                      : " — NOT the same cube"));

        // AND IT IS A GATE NOW, NOT A NOTE. This printed the gap for two commits
        // while it was being closed from the cutting end, because a check that
        // fails for a known reason on every run is a check people learn to read
        // past. It is closed, so it asserts: a re-cut regenerates the whole
        // catalogue and would put the hardest cube back wherever the seeds fell,
        // and `finale apply` is the one command that fixes it.
        ok(worstAt == Vaults.LastCube,
           "cube " + worstAt + " is harder than the last cube — run `finale apply`");
    }
}