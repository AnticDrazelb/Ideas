using System;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// PLAY THE CUBE. Not solve it — PLAY it, through the same Session the telephone
/// runs, one tap and one drag at a time, until it says you won.
///
/// Every other check in this harness asks the Solver about a Level the Catalogue
/// decoded. The game does not play that Level. Session.Load takes a COPY of it,
/// and for as long as this project has had triggers that copy quietly dropped
/// their second solid, because Clone was written before `alts` existed and was
/// never told about it. Layouts fell from 2 to 1, Eff answered for a
/// single-solid cube exactly as it is written to, the trigger toggled a bit that
/// changed nothing, and 125 of the 300 catalogued cubes — every one that carries
/// a trigger, the last cube in the game among them — could not be finished by a
/// player while every check here called them solvable.
///
/// No amount of asking the solver harder would have found that. The gap was not
/// in the reasoning, it was that nothing had ever driven the object the player
/// actually touches. So this drives it:
///
///   - Session.Load, exactly as the director loads a vault cube
///   - the solver asked for a move FROM THE SESSION'S OWN STATE, not from a
///     model kept alongside it — so if the two ever disagree, the search runs
///     out of route and this fails rather than papering over it
///   - TryTurn for a fold and TapCell for a step, the two entry points input has
///   - AdvanceWalk, AdvanceAnim and StepPlateClock pumped at 60fps between
///     moves, in the same order GameDirector pumps them, so the plate clock is
///     really running while the cube is really being played
///   - and `won`, from the Session, as the only thing that counts as finished
/// </summary>
static class FinishChecks
{
    const float Frame = 1000f / 60f;

    /// <summary>Pump the session like the director does, until it is idle again.</summary>
    static float Settle(Session s, int maxFrames = 600)
    {
        float ms = 0f;
        for (int i = 0; i < maxFrames; i++)
        {
            s.AdvanceWalk(Frame);
            s.AdvanceAnim(Frame);
            s.StepPlateClock(Frame, false);
            ms += Frame;
            if (s.walking == null && s.anim == null) return ms;
        }
        return ms;
    }

    /// <summary>Play one cube to a win, or say why it could not be played.</summary>
    /// <summary>Longest unbroken stretch, in ms of animation, that any replay spent inside a plate world.</summary>
    public static float WorstPlateWindow;
    public static int WorstPlateCube;

    public static bool Play(Level source, int number, out string why, out int folds)
    {
        why = null;
        folds = 0;
        float inPlate = 0f;
        var s = new Session();
        s.Load(source, number, LoadKind.Vault);
        Settle(s);

        // PLANNED ONCE, OFF THE SESSION'S OWN BOARD, AND THEN REPLAYED.
        //
        // Off the session's board because that is the copy the player touches,
        // and the copy is what was broken. Once rather than per move because a
        // step is free: two states can each be one fold from the core with the
        // other at the head of their optimal line, and a driver that re-asks
        // after every move walks between them until it runs out of patience.
        var from = new SolveState
        {
            pos = s.pos, ori = Turns.OriIndex(s.M),
            kmask = s.kmask, doors = s.doors, world = s.world,
        };
        SolveResult r = Solver.Solve(s.lv, 40, from);
        if (!r.ok || r.line == null)
        {
            why = "the board the game loaded has no route to the core at all";
            return false;
        }

        foreach (Act a in r.line)
        {
            if (s.won) break;
            if (a.isTurn)
            {
                if (!s.TryTurn(a.dir))
                {
                    why = "the game refused a fold the solver offered, at " + s.pos
                        + " after " + folds + " folds";
                    return false;
                }
                folds++;
                float dt1 = Settle(s);
                if ((s.world & Level.Plates) != 0) { inPlate += dt1; if (inPlate > WorstPlateWindow) { WorstPlateWindow = inPlate; WorstPlateCube = number; } }
                else inPlate = 0f;
            }
            else
            {
                // A step is a TAP ON A CELL, which is the only way a player has
                // of taking one — there is no "step in direction" entry point.
                Int3 v = Projection.ViewOf(s.N, s.M, s.pos);
                Int3 was = s.pos;
                s.TapCell(v.x + Turns.All[a.dir].dx, v.y + Turns.All[a.dir].dy);
                float dt2 = Settle(s);
                if ((s.world & Level.Plates) != 0) { inPlate += dt2; if (inPlate > WorstPlateWindow) { WorstPlateWindow = inPlate; WorstPlateCube = number; } }
                else inPlate = 0f;
                if (s.pos == was)
                {
                    why = "the game would not walk to the cell the solver stepped to, from " + was
                        + " after " + folds + " folds";
                    return false;
                }
            }
        }

        if (!s.won) { why = "the whole solution was played and the cube did not finish, at " + s.pos; return false; }
        return true;
    }

    /// <summary>Trace one cube through the Session, move by move. `finish 226`.</summary>
    public static void Trace(string[] args)
    {
        int no = args.Length > 1 && int.TryParse(args[1], out int q) ? q : Vaults.LastCube;
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));
        Store.Load();
        Level lv = Catalogue.Get(no);
        Console.WriteLine("cube " + no + "  vault " + Vaults.VaultName((no - 1) / 25)
            + "  par " + lv.par + "  layouts " + lv.Layouts);

        var s = new Session();
        int reverts = 0;
        s.Load(lv, no, LoadKind.Vault);
        s.On.Reverted += () => reverts++;
        s.On.Toast += t => Console.WriteLine("        TOAST: " + t);
        Settle(s);

        var from = new SolveState { pos = s.pos, ori = Turns.OriIndex(s.M), kmask = s.kmask, doors = s.doors, world = s.world };
        SolveResult r = Solver.Solve(s.lv, 40, from);
        Console.WriteLine("planned: ok=" + r.ok + " folds=" + r.turns + " chain=" + (r.line?.Count ?? 0));
        if (!r.ok || r.line == null) return;

        int fo = 0, mi = 0;
        foreach (Act a2 in r.line)
        {
            mi++;
            if (s.won) break;
            Int3 v = Projection.ViewOf(s.N, s.M, s.pos);
            if (a2.isTurn)
            {
                bool okk = s.TryTurn(a2.dir); fo++;
                Console.WriteLine(("  " + mi).PadLeft(4) + "  FOLD " + Turns.All[a2.dir].id.PadRight(6)
                    + " accepted=" + okk + "  pos " + s.pos + " world " + s.world + " plateT " + (int)s.plateT);
                Settle(s);
            }
            else
            {
                int u2 = v.x + Turns.All[a2.dir].dx, w2 = v.y + Turns.All[a2.dir].dy;
                Surf tgt = (u2 >= 0 && w2 >= 0 && u2 < s.N && w2 < s.N) ? s.surf[u2 * s.N + w2] : default;
                Int3 was = s.pos;
                s.TapCell(u2, w2);
                Settle(s);
                Console.WriteLine(("  " + mi).PadLeft(4) + "  step " + Turns.All[a2.dir].id.PadRight(6)
                    + " to view " + u2 + "," + w2 + " cell '" + (tgt.has ? tgt.t : '?') + "' at " + tgt.w
                    + "  moved=" + (s.pos != was) + "  pos " + s.pos + " world " + s.world);
                if (s.pos == was)
                {
                    Console.WriteLine("        door index at target = " + s.lv.DoorIndexAt(tgt.w)
                        + "   doors open = " + s.doors + "   nodes = " + s.kmask);
                    break;
                }
            }
        }
        Console.WriteLine("won=" + s.won + "  reverts=" + reverts);
    }

    public static void Run(Action<bool, string> ok)
    {
        if (Catalogue.Count == 0) { Console.WriteLine("finish: no catalogue, skipping"); return; }

        // THE LAST CUBE FIRST AND BY NAME, because it is the one that ends the
        // machine and the one nobody reaches by accident.
        Level last = Catalogue.Get(Vaults.LastCube);
        if (last != null)
        {
            bool won = Play(last, Vaults.LastCube, out string why, out int folds);
            ok(won, "cube " + Vaults.LastCube + " cannot be played to a win: " + why);
            if (won)
                Console.WriteLine("finish: cube " + Vaults.LastCube + " played through the real Session to a win in "
                    + folds + " folds against a par of " + last.par);
        }

        // AND THEN EVERY OTHER CUBE, because the bug this exists for hit 125 of
        // them at once and a check that only ever looked at the last one would
        // have called the ladder healthy.
        int played = 0, failed = 0;
        string firstWhy = null;
        int firstBad = 0;
        for (int i = 1; i <= Catalogue.Count; i++)
        {
            Level lv = Catalogue.Get(i);
            if (lv == null) continue;
            if (Play(lv, i, out string why, out _)) played++;
            else { failed++; if (firstWhy == null) { firstWhy = why; firstBad = i; } }
        }
        ok(failed == 0, failed + " cubes cannot be played to a win in the real Session — first is cube "
            + firstBad + ": " + firstWhy);

        // THE BOARD THE GAME PLAYS, AUDITED. Safety's sweep runs on the Level the
        // catalogue decoded; Session plays a Clone of it. Those were different
        // objects until Clone was fixed, so asserting on one and playing the
        // other is exactly the hole this file exists to close.
        int deadOnClone = 0, firstDead = 0;
        for (int i = 1; i <= Catalogue.Count; i++)
        {
            Level lv2 = Catalogue.Get(i);
            if (lv2 == null) continue;
            var vd = Safety.Audit(lv2.Clone());
            if (!vd.Ok) { deadOnClone++; if (firstDead == 0) firstDead = i; }
        }
        ok(deadOnClone == 0, deadOnClone + " cubes can be bricked on the board the GAME loads (first cube "
            + firstDead + ") even though the decoded board is clean");
        Console.WriteLine("finish: " + Catalogue.Count + " cubes audited on the cloned board the game plays — "
            + deadOnClone + " can be bricked");
        Console.WriteLine("finish: the longest any solution stayed inside a plate's five-second window was "
            + (WorstPlateWindow / 1000f).ToString("0.00") + "s of animation (cube " + WorstPlateCube
            + "), against " + (Session.PlateMs / 1000f).ToString("0.0") + "s on the clock");
        Console.WriteLine("finish: " + played + " of " + Catalogue.Count
            + " cubes played to a win through the Session the game runs");
    }
}
