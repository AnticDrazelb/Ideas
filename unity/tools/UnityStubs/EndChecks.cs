using System;
using System.Collections;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;
using UnityEngine;

/// <summary>
/// RUN THE ENDING. Actually run it, sixty frames a second, all eight seconds.
///
/// This is the only sequence in the game that cannot be reached without first
/// solving five hundred cubes, and it shipped having never executed a single
/// line. When it did not work there was nothing to look at but the source, and
/// the source was correct — which is the exact position a harness exists to
/// prevent, and the reason Ending was pulled out of GameDirector at all.
///
/// The stage here records rather than draws, so what is asserted is the
/// TRAJECTORY: that it terminates, that the instrument and the board go all the
/// way down and all the way back, that the camera closes and reopens, that
/// nothing is asked for at a size the closed frame cannot show, and that the
/// rumble rises instead of pinning. Every one of those is a bug this sequence
/// has actually had.
/// </summary>
static class EndChecks
{
    /// <summary>A stage that writes everything down and draws nothing.</summary>
    sealed class Probe : IStage
    {
        public float Dt => 1f / 60f;
        public float HalfHeight { get; set; } = 1.02f;   // a 5-cube board at full close
        public float Aspect { get; set; } = 720f / 1560f;

        public readonly List<float> ui = new List<float>();
        public readonly List<float> board = new List<float>();
        public readonly List<float> close = new List<float>();
        public readonly List<float> rumble = new List<float>();
        public readonly List<float> blob = new List<float>();
        public readonly List<float> rings = new List<float>();
        public readonly List<bool> input = new List<bool>();

        public int bangs, chimes, sparks, clears, titles;
        public float hush = -1f;

        // the order things happened in, which is most of what an ending IS
        public readonly List<string> log = new List<string>();

        public void Ui(float a) { ui.Add(a); log.Add("ui"); }
        public void Board(float a) { board.Add(a); log.Add("board"); }
        public void Close(float k) { close.Add(k); log.Add("close"); }
        public void Rumble(float amt) { rumble.Add(amt); }
        public void Blob(float size, float alpha) { blob.Add(size); }
        public void Ring(float r0, float r1, float d, float w, bool core) { rings.Add(r1); }
        public void Sparks(float speed, float size) { sparks++; }
        public void Bang() { bangs++; log.Add("bang"); }
        public void Chime() { chimes++; log.Add("chime"); }
        public void Hush(float s) { hush = s; log.Add("hush"); }
        public void Input(bool on) { input.Add(on); log.Add(on ? "input on" : "input off"); }
        public void Clear() { clears++; log.Add("clear"); }
        public void Title() { titles++; log.Add("title"); }
    }

    public static void Run(Action<bool, string> ok)
    {
        var p = new Probe();
        IEnumerator seq = Ending.Run(p);

        // DRIVEN EXACTLY AS THE DIRECTOR DRIVES IT, including the frame cap, so
        // a sequence that runs away here runs away there too.
        int frames = 0;
        string threw = null;
        bool finished = false;
        try
        {
            while (frames < Ending.FrameCap)
            {
                if (!seq.MoveNext()) { finished = true; break; }
                frames++;
            }
        }
        catch (Exception e) { threw = e.ToString(); }

        ok(threw == null, "the ending threw: " + threw);
        if (threw != null) return;

        ok(finished, "the ending did not finish inside " + Ending.FrameCap + " frames");

        float seconds = frames / 60f;
        float want = Ending.Close + Ending.Grow + Ending.Blow + Ending.Black;
        Console.WriteLine(string.Format(
            "ending: ran to the end in {0} frames — {1:0.00}s of a {2:0.00}s sequence, " +
            "{3} blobs, {4} rings, {5} bangs", frames, seconds, want, p.blob.Count, p.rings.Count, p.sparks));

        // a frame either side, because each phase's loop runs one frame past its
        // own boundary before the test fails
        ok(Math.Abs(seconds - want) < 0.15f,
           "the ending ran " + seconds.ToString("0.00") + "s and the four moves add to " + want.ToString("0.00"));

        // ---- the instrument goes, and comes back -------------------------
        ok(p.ui.Count > 0 && p.ui[0] > 0.98f, "the ending starts by dimming the instrument instead of ending on it");
        ok(Min(p.ui) <= 0.0001f, "the instrument never reaches nothing — lowest is " + Min(p.ui).ToString("0.000"));
        ok(p.ui[p.ui.Count - 1] >= 0.999f, "the instrument is left faded at " + p.ui[p.ui.Count - 1].ToString("0.000"));

        // ---- and so does the board ---------------------------------------
        ok(Min(p.board) <= 0.0001f, "the board never reaches black — lowest is " + Min(p.board).ToString("0.000"));
        ok(p.board[p.board.Count - 1] >= 0.999f, "the board is left dark at " + p.board[p.board.Count - 1].ToString("0.000"));

        // THE ONE THAT STRANDS A PLAYER. A board left at Close 1 is a camera
        // pointing at a sixth of a cube with no way to move it.
        ok(Max(p.close) >= 0.999f, "the camera never fully closes — highest is " + Max(p.close).ToString("0.000"));
        ok(p.close[p.close.Count - 1] <= 0.0001f, "the camera is left closed at " + p.close[p.close.Count - 1].ToString("0.000"));

        // ---- input is off for the whole of it, and on at the end ----------
        ok(p.input.Count >= 2 && !p.input[0], "the ending does not take the board away from the player first");
        ok(p.input[p.input.Count - 1], "the ending leaves the player unable to press anything");
        ok(p.titles == 1, "the ending shows the title " + p.titles + " times");
        ok(p.clears == 1, "the ending clears the debris " + p.clears + " times");
        ok(p.bangs == 1 && p.sparks == 1 && p.rings.Count == 2 && p.chimes == 1,
           "the shockwave fired " + p.bangs + " bangs, " + p.sparks + " bursts, "
           + p.rings.Count + " rings and " + p.chimes + " chimes");

        // ---- the sound is told how long the picture is --------------------
        ok(Math.Abs(p.hush - (want - 2.4f)) < 0.001f,
           "the bed is ducked for " + p.hush.ToString("0.00") + "s against a " + want.ToString("0.00") + "s ending");

        // ---- THE RUMBLE RISES, IT DOES NOT PIN ---------------------------
        //
        // Shake ADDS and trauma decays at 2.4 a second, so calling it every frame
        // for two seconds wins that race about ten to one and holds the frame at
        // maximum from a fifth of the way in. That is what "shakes violently and
        // nothing else" looked like. Rumble sets a level, so the level itself has
        // to be a ramp — and it must start low, or the star's first second is
        // already as loud as its last.
        ok(p.rumble.Count > 30, "the ending barely rumbles — " + p.rumble.Count + " frames of it");
        ok(p.rumble[0] <= 0.12f, "the rumble opens at " + p.rumble[0].ToString("0.000") + ", which is a shove, not a swell");
        ok(Max(p.rumble) <= 0.85f, "the rumble reaches " + Max(p.rumble).ToString("0.000") + " — at that level the frame leaves the star");
        int rose = 0;
        for (int i = 1; i < p.rumble.Count; i++) if (p.rumble[i] > p.rumble[i - 1]) rose++;
        ok(rose > p.rumble.Count / 2, "the rumble rises on only " + rose + " of " + p.rumble.Count + " frames");

        // ---- and nothing is asked for at the wrong scale ------------------
        //
        // Every size in the last three moves is measured off the frame. A blob
        // that starts bigger than the frame has nowhere to grow; one that ends
        // smaller than it never fills the screen; and a ring whose radius is a
        // world-unit constant crosses the closed frame in a fortieth of its life.
        // THE TEST IS *WHEN* IT FILLS THE FRAME, NOT WHETHER IT DOES.
        //
        // "Ends bigger than the screen" is far too weak to catch the bug it is
        // aimed at: the shipped 0.4 + 26 constant also ends bigger than every
        // screen, which is precisely why it looked fine on paper. What it does
        // wrong is get there at forty per cent of the growth and spend the other
        // sixty white on white — the star stops being a star a third of the way
        // in and the remaining second and a bit is a blank rectangle. Measured
        // off the frame it crosses at ninety-five per cent, which is what "grows
        // and glows" has to mean if the growth is to be the thing you watch.
        Screen(ok, p, "the shipped frame");

        float frame = 2f * p.HalfHeight * Math.Max(1f, p.Aspect);
        foreach (float r in p.rings)
            ok(r > frame && r < frame * 12f,
               "a shockwave ring runs to " + r.ToString("0.00") + " against a frame of " + frame.ToString("0.00"));

        // AND IT HAS TO HOLD ON A DIFFERENT SCREEN. The whole point of measuring
        // off the camera is that a tablet and a tall phone get the same picture,
        // and a constant cannot do that for both however it is chosen.
        foreach (var (hh, asp, what) in new[]
                 { (1.02f, 720f / 1560f, "a tall phone"), (3.4f, 1f, "a square window"),
                   (2.1f, 1280f / 800f, "a landscape tablet") })
        {
            var q = new Probe { HalfHeight = hh, Aspect = asp };
            IEnumerator s2 = Ending.Run(q);
            int n = 0;
            while (n < Ending.FrameCap && s2.MoveNext()) n++;
            Screen(ok, q, what);
        }
    }

    /// <summary>
    /// The star opens well inside the frame, ends past it, and does not cross it
    /// until the growth is nearly over.
    /// </summary>
    static void Screen(Action<bool, string> ok, Probe p, string what)
    {
        float frame = 2f * p.HalfHeight * Math.Max(1f, p.Aspect);
        ok(p.blob.Count > 0, "on " + what + " the star never draws");
        if (p.blob.Count == 0) return;

        ok(p.blob[0] < frame * 0.5f,
           "on " + what + " the star opens at " + p.blob[0].ToString("0.00")
           + " against a frame of " + frame.ToString("0.00"));
        ok(Max(p.blob) > frame,
           "on " + what + " the star only reaches " + Max(p.blob).ToString("0.00")
           + " against a frame of " + frame.ToString("0.00"));

        // the grow phase is every blob before the shockwave's, and the shockwave
        // opens at the width the grow ended on, so the crossing is looked for in
        // the growth alone
        int grow = (int)(Ending.Grow * 60f);
        int fillsAt = -1;
        for (int i = 0; i < p.blob.Count && i < grow; i++)
            if (p.blob[i] >= frame) { fillsAt = i; break; }

        ok(fillsAt < 0 || fillsAt > grow * 6 / 10,
           "on " + what + " the star fills the frame " + (100 * fillsAt / Math.Max(1, grow))
           + "% into a growth that has " + (100 - 100 * fillsAt / Math.Max(1, grow)) + "% left to run");
    }

    /// <summary>
    /// AND IT HAS TO BE REACHABLE. Everything above proves the sequence is right
    /// once it starts; this is the separate question of whether anything starts
    /// it. The predicate is pure, so it can simply be asked.
    /// </summary>
    public static void Reach(Action<bool, string> ok)
    {
        // the last cube ends the machine however you arrived at it — including
        // by typing 500 into the seed box, which is the only way to reach the
        // ending without solving four hundred and ninety-nine cubes first and
        // was therefore the one route that used to do something else
        ok(Vaults.EndsTheMachine(Vaults.LastCube, false, false), "cube 500 played from the vault does not end the machine");
        ok(Vaults.EndsTheMachine(Vaults.LastCube, false, false), "cube 500 practised from the seed box does not end the machine");

        // and nothing else does
        ok(!Vaults.EndsTheMachine(Vaults.LastCube - 1, false, false), "cube 499 ends the machine");
        ok(!Vaults.EndsTheMachine(Vaults.LastCube, true, false), "a daily ends the machine");
        ok(!Vaults.EndsTheMachine(Vaults.LastCube, false, true), "a made cube ends the machine");

        // THE CUBE ITSELF HAS TO BE FINISHABLE, or none of the above matters.
        // The catalogue sweep proves all five hundred solve; this names the one
        // that the ending hangs off, so a future edit to the last line of the
        // ladder cannot quietly make the ending unreachable.
        Level last = Catalogue.Get(Vaults.LastCube);
        ok(last != null, "there is no cube " + Vaults.LastCube + " in the catalogue");
        if (last == null) return;

        SolveResult r = Solver.Solve(last, last.par);
        ok(r.ok && r.turns == last.par,
           "cube " + Vaults.LastCube + " does not solve at its par of " + last.par);
        Console.WriteLine("ending: cube " + Vaults.LastCube + " solves in " + r.turns
                        + " folds, and reaching it ends the machine from the vault and the seed box alike");
    }

    static float Min(List<float> v)
    {
        float m = float.MaxValue;
        foreach (float f in v) if (f < m) m = f;
        return v.Count == 0 ? 0f : m;
    }

    static float Max(List<float> v)
    {
        float m = float.MinValue;
        foreach (float f in v) if (f > m) m = f;
        return v.Count == 0 ? 0f : m;
    }
}
