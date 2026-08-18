using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;
using Singularity.UI;

/// <summary>
/// THE TUTORIAL IS A CLAIM ABOUT THE CONTENT, SO IT IS CHECKED AGAINST IT.
///
/// The coach says two things and the order it says them in is the whole design:
/// the walk first, the fold at the moment the fold becomes the answer. Both
/// halves are properties of the ten authored cubes rather than of the coach, and
/// both rot the same way every hand-placed tutorial rots — somebody re-cuts
/// cube one, its opening becomes a fold, and a prompt that says TAP TO MOVE is
/// now a lie printed over a board where tapping does nothing.
///
/// So this plays cube one and cube two through the real Session, one move at a
/// time, and asks the coach what it would be showing at every state along the
/// way. It is the same standard ArcChecks holds the eversion cubes to: a lesson
/// that stops being true is a failed check rather than a thing nobody notices.
/// </summary>
static class CoachChecks
{
    const float Frame = 1000f / 60f;

    static void Settle(Session s, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            s.AdvanceWalk(Frame);
            s.AdvanceAnim(Frame);
            s.StepPlateClock(Frame, false);
            if (s.walking == null && s.anim == null) return;
        }
    }

    public static void Run(Action<bool, string> ok)
    {
        Opening(ok);
        Played(ok, 1);
        Played(ok, 2);
        Retires(ok);
        Elsewhere(ok);
        Alone(ok);
        Ring(ok);
        WorldVerbs(ok);
    }

    /// <summary>
    /// THE FOUR THE COACH USED TO LEAVE UNTAUGHT, MEASURED ON THE SHIPPED LADDER.
    ///
    /// The node and its lock, the second plate, the everter and the trigger are
    /// rules, not gestures — none of them is guessable from the art, and until
    /// now the only one with any teaching at all was the plate, which has a card.
    /// The other three arrived by chapter position and hope.
    ///
    /// No lesson names a cube number, so what this proves is that the ladder and
    /// the coach agree WITHOUT one: for each verb it finds the first cube in the
    /// catalogue that carries it, and asserts the lesson fires exactly there, is
    /// silent on every cube before it, and has a square on the board to ring.
    /// Re-cut the catalogue into a different order and this check moves with it;
    /// re-cut it so a verb never appears at all and this check fails, which is the
    /// point.
    /// </summary>
    static void WorldVerbs(Action<bool, string> ok)
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));

        var verbs = new (int bit, Coach.Lesson lesson, string name)[]
        {
            (Coach.LearnedNode,      Coach.Lesson.Node,      "the node and its lock"),
            (Coach.LearnedSubstrate, Coach.Lesson.Substrate, "the plate that opens the solid"),
            (Coach.LearnedEverter,   Coach.Lesson.Everter,   "the everter"),
            (Coach.LearnedTrigger,   Coach.Lesson.Trigger,   "the trigger"),
        };

        // ---- where the ladder actually introduces each one -------------------
        var firstAt = new int[verbs.Length];
        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            int carries = Coach.Carries(lv, true);
            for (int i = 0; i < verbs.Length; i++)
                if (firstAt[i] == 0 && (carries & verbs[i].bit) != 0) firstAt[i] = level;
        }

        for (int i = 0; i < verbs.Length; i++)
        {
            ok(firstAt[i] > 0, "nothing in the ladder carries " + verbs[i].name
                             + ", so its lesson can never fire");
            if (firstAt[i] == 0) continue;

            // it has somewhere to put the ring, on the board as that cube opens
            Level lv = LevelSupply.Get(firstAt[i]);
            var s0 = new Session();
            s0.Load(lv, firstAt[i], LoadKind.Vault);
            ok(Coach.MarkAt(s0, verbs[i].lesson, out int _, out int __),
               "the coach has nothing to ring for " + verbs[i].name + " on cube " + firstAt[i]);

            // a save that has already cleared this cube is never told about it
            int carried = Coach.Carries(lv, true);
            ok((Coach.RetireSeen(0, firstAt[i], firstAt[i] + 1, carried) & verbs[i].bit) != 0,
               "a save that has cleared cube " + firstAt[i] + " is still going to be taught " + verbs[i].name);
            ok(Coach.RetireSeen(0, firstAt[i], firstAt[i], carried) == 0,
               "a player arriving at cube " + firstAt[i] + " for the first time was retired past "
               + verbs[i].name);
        }

        // ---- AND THE PLAYER WALKS THE LADDER, WHICH IS THE REAL QUESTION -----
        //
        // Asking each cube in isolation is asking the wrong thing, and the first
        // version of this check did: cube 76 introduces the second plate AND
        // carries a lock, so put to it cold it answers "the node" — correctly, and
        // meaninglessly, because nobody arrives at cube 76 without having met one.
        // What has to hold is the SEQUENCE. So a player is walked from cube one to
        // the end, learning each verb on the cube that shows it, and every lesson
        // the coach gives along the way is recorded.
        var spoken = new List<(int level, Coach.Lesson lesson)>();
        int held = Coach.LearnedAll;
        for (int level = Coach.LastCube + 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            Coach.Lesson want = Coach.Next(held, LoadKind.Vault, level, false, false,
                                           Coach.Carries(lv, true));
            if (want == Coach.Lesson.None) continue;
            spoken.Add((level, want));
            foreach (var v in verbs) if (want == v.lesson) held |= v.bit;
        }

        ok(spoken.Count == verbs.Length,
           "the coach gives " + spoken.Count + " world-verb lessons across the ladder, not " + verbs.Length);

        for (int i = 0; i < verbs.Length && i < spoken.Count; i++)
        {
            ok(spoken[i].lesson == verbs[i].lesson,
               "lesson " + (i + 1) + " on the way up is " + spoken[i].lesson
               + ", where the ladder introduces " + verbs[i].name + " first");
            ok(spoken[i].level == firstAt[i],
               verbs[i].name + " is taught on cube " + spoken[i].level
               + " and first carried on cube " + firstAt[i]);
        }

        // ---- the plate card and the second plate never speak at once ---------
        //
        // PLATES has a full screen of its own, shown on the first cube carrying
        // either kind. A chapter-VI cube reached before that screen has been seen
        // would otherwise get the card AND a line about the second plate, in the
        // same breath, about two different plates.
        int both = 0;
        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv != null && (Coach.Carries(lv, false) & Coach.LearnedSubstrate) != 0) both++;
        }
        ok(both == 0, both + " cubes would show the plate card and the second-plate line together");

        var line = new System.Text.StringBuilder("coach: taught where the ladder introduces them —");
        for (int i = 0; i < verbs.Length; i++)
            line.Append(i == 0 ? " " : ", ").Append(verbs[i].name).Append(" at ").Append(firstAt[i]);
        Console.WriteLine(line.ToString());
        Console.WriteLine("coach: " + Coach.LineFor(Coach.Lesson.Node) + " / "
                        + Coach.LineFor(Coach.Lesson.Substrate) + " / "
                        + Coach.LineFor(Coach.Lesson.Everter) + " / "
                        + Coach.LineFor(Coach.Lesson.Trigger));
    }

    /// <summary>
    /// THE MEASUREMENT THE SCRIPT IS BUILT ON. Every one of the first four
    /// authored cubes opens on a STEP, which is why the coach teaches the walk
    /// first and why teaching the fold first would be teaching a move the cube
    /// does not want yet.
    /// </summary>
    static void Opening(Action<bool, string> ok)
    {
        int steps = 0;
        for (int level = 1; level <= 4; level++)
        {
            Level lv = Baked.Levels[level - 1].ToLevel(level);
            SolveResult r = Solver.Solve(lv, 20);
            ok(r.ok && r.hasFirst, "cube " + level + " has no line to read an opening off");
            if (r.ok && r.hasFirst && !r.first.isTurn) steps++;
        }
        ok(steps == 4, "only " + steps + " of the first four cubes open on a step — the coach teaches the walk first");
        Console.WriteLine("coach: the first four authored cubes all open on a step, which is the order the coach uses");
    }

    /// <summary>
    /// Play the cube along its own optimal line and ask the coach at every state.
    /// Two things must hold and they are the whole contract:
    ///
    ///   before the player has walked   the prompt is TAP, never SWIPE
    ///   the first state whose next act is a fold   the prompt is SWIPE
    /// </summary>
    static void Played(Action<bool, string> ok, int level)
    {
        Level lv = Baked.Levels[level - 1].ToLevel(level);
        var s = new Session();
        s.Load(lv, level, LoadKind.Vault);

        int taught = 0;                       // a save that has never played
        bool sawStep = false, sawFold = false;
        string name = Baked.Levels[level - 1].name;

        for (int move = 0; move < 40 && !s.won; move++)
        {
            if (!s.Advice(out Act a, 20)) break;

            Coach.Lesson want = Coach.Next(taught, s.kind, s.levelNo, s.won,
                                           (taught & Coach.LearnedFold) == 0
                                           && (taught & Coach.LearnedStep) != 0 && a.isTurn,
                                           Coach.Carries(s.lv, true));

            if ((taught & Coach.LearnedStep) == 0)
            {
                ok(want == Coach.Lesson.Step,
                   name + ": the coach is showing " + want + " before the player has walked");
                if (want == Coach.Lesson.Step) sawStep = true;

                // and it has somewhere to put the ring
                ok(Coach.FarEnd(s, out int _, out int __),
                   name + ": the coach has no plain trace to ring on the opening board");
            }
            else if (a.isTurn && (taught & Coach.LearnedFold) == 0)
            {
                ok(want == Coach.Lesson.Fold,
                   name + ": the next move is a fold and the coach is showing " + want);
                if (want == Coach.Lesson.Fold) sawFold = true;
            }

            // make the move the line asks for, through the same two entry points
            // input has, and learn from it exactly as the director does
            if (a.isTurn)
            {
                ok(s.TryTurn(a.dir), name + ": the line asks for a fold the session refuses");
                taught |= Coach.LearnedFold;
            }
            else
            {
                Int3 v = Projection.ViewOf(s.N, s.M, s.pos);
                s.TapCell(v.x + Turns.All[a.dir].dx, v.y + Turns.All[a.dir].dy);
                taught |= Coach.LearnedStep;
            }
            Settle(s);
        }

        ok(s.won, name + ": the cube did not finish along its own line");
        ok(sawStep, name + ": the walk was never taught");
        ok(sawFold, name + ": the fold was never taught");
        Console.WriteLine("coach: " + name + " (cube " + level + ") teaches the walk at the start and the fold "
                        + "at the state where folding is the answer");
    }

    /// <summary>
    /// ONE TEACHER AT A TIME.
    ///
    /// The plate has its own card and it fires on the first cube that carries
    /// one; the matrix has its one nudge and it fires on the third. Both would
    /// land on top of the coach if a plate or an everter ever appeared in the two
    /// cubes it speaks on — a modal card over a prompt that is waiting for a
    /// swipe, on somebody's first minute. The cubes are authored, so today they
    /// do not; this is what stops a re-cut of vault I from changing that quietly.
    /// </summary>
    static void Alone(Action<bool, string> ok)
    {
        for (int level = 1; level <= Coach.LastCube; level++)
        {
            BakedLevel b = Baked.Levels[level - 1];
            string vox = new string(b.ToLevel(level).vox);
            ok(vox.IndexOfAny(new[] { 'A', 'B' }) < 0,
               b.name + " carries a plate, so the plate card fires over the coach on cube " + level);
            ok(vox.IndexOf('E') < 0,
               b.name + " carries an everter, which is vault IX's lesson arriving on cube " + level);
        }

        // and the matrix nudge waits until the coach has finished
        ok(3 > Coach.LastCube, "the matrix nudge fires on cube 3, which the coach is still speaking on");
        Console.WriteLine("coach: neither cube it speaks on carries a plate or an everter, and the matrix "
                        + "nudge waits until cube 3 — one teacher at a time");
    }

    /// <summary>Once both are proved, the coach is silent — on any cube, in any state.</summary>
    static void Retires(Action<bool, string> ok)
    {
        for (int level = 1; level <= 3; level++)
            foreach (bool fold in new[] { true, false })
                ok(Coach.Next(Coach.LearnedAll, LoadKind.Vault, level, false, fold, 0) == Coach.Lesson.None,
                   "the coach speaks again on cube " + level + " after both verbs are proved");

        ok(Coach.Migrate(0, 4) == Coach.LearnedAll, "a save that has cleared cubes is not migrated to taught");
        ok(Coach.Migrate(0, 1) == 0, "a save that has never cleared a cube was migrated to taught");
        ok(Coach.Migrate(Coach.LearnedStep, 1) == Coach.LearnedStep, "migration overwrote a real bit");

        // and half a lesson is remembered as half a lesson
        ok(Coach.Next(Coach.LearnedStep, LoadKind.Vault, 1, false, true, 0) == Coach.Lesson.Fold,
           "a player who has walked but never folded is not offered the fold");
        ok(Coach.Next(Coach.LearnedStep, LoadKind.Vault, 1, false, false, 0) == Coach.Lesson.None,
           "the fold is being taught at a state where folding is not the answer");

        // AND FIRST CONTACT IS NEVER SHARED WITH A WORLD VERB. A cube one that
        // somehow carried a lock would otherwise be teaching the walk and the node
        // in the same breath, on the first screen anybody ever sees.
        ok(Coach.Next(0, LoadKind.Vault, 1, false, true, Coach.LearnedNode) == Coach.Lesson.Step,
           "a world verb outranks the walk on the first cube");
        ok(Coach.Next(Coach.LearnedAll, LoadKind.Vault, 1, false, true, Coach.LearnedNode) == Coach.Lesson.None,
           "the coach teaches a world verb inside first contact");
        Console.WriteLine("coach: both verbs proved retires it for good, and a save past cube one starts retired");
    }

    /// <summary>
    /// Not on the daily, not on a made cube, not on a cube visited by number, and
    /// not past the two authored cubes that cannot be finished without folding.
    /// </summary>
    static void Elsewhere(Action<bool, string> ok)
    {
        foreach (LoadKind k in new[] { LoadKind.Daily, LoadKind.Made, LoadKind.Practice })
            foreach (int carries in new[] { 0, Coach.LearnedEverter, Coach.LearnedTrigger })
                ok(Coach.Next(0, k, 1, false, true, carries) == Coach.Lesson.None,
                   "the coach speaks on a " + k + " cube");

        ok(Coach.Next(0, LoadKind.Vault, Coach.LastCube + 1, false, true, 0) == Coach.Lesson.None,
           "the coach is still teaching the verbs on cube " + (Coach.LastCube + 1));
        ok(Coach.Next(0, LoadKind.Vault, 1, true, true, Coach.LearnedEverter) == Coach.Lesson.None,
           "the coach speaks over a won cube");
        Console.WriteLine("coach: silent on the daily, on a made cube, on practice, past cube "
                        + Coach.LastCube + " for the two verbs, and over a win");
    }

    /// <summary>
    /// THE RING NEVER LANDS ON SOMETHING THAT FIRES. A tap onto a plate turns the
    /// board inside out for five seconds and a tap onto an everter turns the
    /// solid over; either one is the largest event in the game going off inside a
    /// sentence that says TAP TO MOVE. Every state of both coached cubes is
    /// checked, and so is every state of the first ten, because the rule is about
    /// the pick and not about the cube.
    /// </summary>
    static void Ring(Action<bool, string> ok)
    {
        int states = 0, rings = 0;
        for (int level = 1; level <= 10; level++)
        {
            Level lv = Baked.Levels[level - 1].ToLevel(level);
            var s = new Session();
            s.Load(lv, level, LoadKind.Vault);

            for (int move = 0; move < 40 && !s.won; move++)
            {
                states++;
                if (Coach.FarEnd(s, out int u, out int v))
                {
                    rings++;
                    Surf c = s.surf[u * s.N + v];
                    ok(c.has, "cube " + level + ": the ring landed on a square with nothing in it");
                    ok(!Level.IsGlyph(c.t),
                       "cube " + level + ": the ring landed on a '" + c.t + "', which fires when you step on it");
                    ok(s.lv.KeyIndexAt(c.w) < 0 && s.lv.DoorIndexAt(c.w) < 0,
                       "cube " + level + ": the ring landed on a node or a lock");
                    ok(s.reach[u * s.N + v], "cube " + level + ": the ring landed somewhere unreachable");
                }

                if (!s.Advice(out Act a, 20)) break;
                if (a.isTurn) s.TryTurn(a.dir);
                else
                {
                    Int3 p = Projection.ViewOf(s.N, s.M, s.pos);
                    s.TapCell(p.x + Turns.All[a.dir].dx, p.y + Turns.All[a.dir].dy);
                }
                Settle(s);
            }
        }
        Console.WriteLine("coach: " + rings + " rings placed across " + states
                        + " states of the ten authored cubes, none on a glyph, a node or a lock");
    }
}
