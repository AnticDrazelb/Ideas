using System;
using Singularity.Core;
using Singularity.Game;

/// Runs the Forge's model — the half of the editor that is pure logic — under the
/// stub harness. Store falls back to a fresh in-memory save because the stubbed
/// PlayerPrefs returns nothing, which is exactly the "device with storage
/// disabled" path the original is written to survive, so this is not a special
/// case invented for the test.
public static class ForgeChecks
{
    static int fails;
    static void Ok(bool b, string what) { if (!b) { Console.WriteLine("FAIL: " + what); fails++; } }

    public static int Run()
    {
        Store.Load();

        // a name is the only free text in the game, and it is not representable as markup
        // the punctuation goes and the space stays, exactly as the original folds it
        Ok(Forge.CleanName("<img onerror=alert(1)>") == "IMG ONERRORALERT1", "name not folded: '" + Forge.CleanName("<img onerror=alert(1)>") + "'");
        Ok(Forge.CleanName("  the   long   way  ") == "THE LONG WAY", "whitespace: '" + Forge.CleanName("  the   long   way  ") + "'");
        Ok(Forge.CleanName("abcdefghijklmnopqrstuvwxyz").Length == 18, "name not capped");

        // an empty cube is not a cube
        var f = Forge.New(5);
        Ok(!f.Verify().ok, "an empty forge verified");

        // build a cube out of a real generated one and check it round-trips through the editor
        Level src = Generator.Mint(11);
        var rec = new MadeCube { key = "k", name = "TEST", n = src.n, par = src.par, code = ShareCode.Encode(src) };
        var g = Forge.From(rec);
        Ok(g.n == src.n, "size lost on load");
        Ok(new string(g.vox) == new string(src.vox), "voxels lost on load");
        Ok(g.start.HasValue && g.start.Value == src.start, "start lost on load");
        Ok(g.goal.HasValue && g.goal.Value == src.goal, "goal lost on load");
        Ok(g.keys.Count == src.keys.Count && g.doors.Count == src.doors.Count, "locks lost on load");

        // and that a cube already in the catalogue is refused as a duplicate
        var v = g.Verify();
        Ok(!v.ok && v.message.Contains("ALREADY EXISTS"), "a catalogued cube was not caught: " + v.message);

        // growing keeps everything; shrinking drops what no longer fits, marks included
        var h = Forge.New(5);
        h.tool = '+'; h.layer = 4; h.Apply(4, 4);
        h.tool = 'S'; h.Apply(4, 4);
        Ok(h.start.HasValue, "start not placed");
        h.Resize(9);
        Ok(h.start.HasValue && h.start.Value == new Int3(4, 4, 4), "grow moved the start");
        Ok(h.vox[Level.Vidx(9, 4, 4, 4)] == '+', "grow lost a voxel");
        h.Resize(5);
        Ok(h.start.HasValue, "shrink dropped an in-range start");
        h.layer = 4; h.tool = '+'; h.Apply(4, 4);
        h.Resize(4);
        Ok(!h.start.HasValue, "shrink kept a start that is now outside the cube");

        // a mark cannot outlive the cell it stands on
        var m = Forge.New(5);
        m.layer = 2; m.tool = 'S'; m.Apply(1, 1);
        Ok(m.start.HasValue, "start not placed");
        m.tool = '.'; m.Apply(1, 1);
        Ok(!m.start.HasValue, "emptying a cell left its start floating in the void");

        // nodes and locks are placed and removed in pairs
        var q = Forge.New(5);
        q.layer = 0; q.tool = 'K'; q.Apply(1, 1);
        q.tool = 'D'; q.Apply(2, 2);
        Ok(q.keys.Count == 1 && q.doors.Count == 1, "a node/lock pair did not form");
        q.tool = 'K'; q.Apply(1, 1);
        Ok(q.keys.Count == 0 && q.doors.Count == 0, "removing a node left its lock behind");

        // a corridor is refused, because par is the whole scoring system
        var c = Forge.New(5);
        c.layer = 2; c.tool = '+';
        for (int x = 0; x < 5; x++) c.Apply(x, 2);
        c.tool = 'S'; c.Apply(0, 2);
        c.tool = 'G'; c.Apply(4, 2);
        var cv = c.Verify();
        Ok(!cv.ok && cv.message.Contains("CORRIDOR"), "a corridor verified: " + cv.message);

        // import refuses junk, and a broken-but-well-formed cube
        Ok(!Forge.Import("not a code", out string im1) && im1.Length > 0, "junk imported");
        Ok(!Forge.Import(ShareCode.Encode(Generator.FallbackLevel(1)), out string im2)
           && im2.Contains("BROKEN"), "a corridor imported: " + im2);

        Daily();

        fails += AccessChecks.Run();

        Console.WriteLine(fails == 0 ? "CHECKS PASSED" : fails + " CHECKS FAILED");
        return fails;
    }

    static void Daily()
    {
        // A MISS COSTS MORE THAN THE WORST HONEST ATTEMPT, or skipping a hard cube
        // would be the optimal play.
        Ok(DailyBoards.DayMiss > 0, "no miss penalty");

        // every window is the inverse of the function that names it
        for (int day = 18000; day < 18000 + 400; day++)
        {
            foreach (var p in DailyBoards.Periods)
            {
                int which = p.Of(day);
                var span = p.Span(which);
                Ok(day >= span.a && day <= span.b,
                   p.label + ": day " + day + " is in period " + which + " but its span is " + span.a + ".." + span.b);
                Ok(p.Of(span.a) == which && p.Of(span.b) == which,
                   p.label + ": period " + which + " span ends outside itself");
                if (span.a > 0) Ok(p.Of(span.a - 1) == which - 1, p.label + ": gap before period " + which);
                Ok(p.Of(span.b + 1) == which + 1, p.label + ": gap after period " + which);
            }
        }

        // spans do not overlap and do not leave holes
        foreach (var p in DailyBoards.Periods)
            for (int w = 200; w < 260; w++)
            {
                var a = p.Span(w);
                var b = p.Span(w + 1);
                Ok(b.a == a.b + 1, p.label + ": period " + w + " and " + (w + 1) + " are not contiguous");
            }

        // a week is seven days, a season is ninety-one
        var wk = Singularity.Core.Daily.WeekSpan(1000);
        Ok(wk.b - wk.a + 1 == 7, "a week is not seven days");
        var se = Singularity.Core.Daily.SeasonSpan(200);
        Ok(se.b - se.a + 1 == DailyBoards.SeasonDays, "a season is the wrong length");

        // and a month is a real calendar month, leap years included
        int feb2024 = Singularity.Core.Daily.MonthOf(Singularity.Core.Daily.DayIndexAt(
            new DateTimeOffset(new DateTime(2024, 2, 15, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds()));
        var febSpan = Singularity.Core.Daily.MonthSpan(feb2024);
        Ok(febSpan.b - febSpan.a + 1 == 29, "February 2024 is not 29 days: " + (febSpan.b - febSpan.a + 1));

        // an empty window scores -1: a fresh install must not open by posting a
        // wall of penalties for a month it was not installed for
        Ok(DailyBoards.SpanTotal((900000, 900006)) == -1, "an empty window scored");

        // the period rides ABOVE the score, so the newest completed window
        // outranks every older one however good the older one was
        Ok(DailyBoards.Packed(101, 9999) > DailyBoards.Packed(100, 0), "an old perfect period outranks a new one");
        // and within one period, lower total ranks higher
        Ok(DailyBoards.Packed(100, 5) > DailyBoards.Packed(100, 6), "a worse total outranked a better one");

        // ---- the Forge coach ---------------------------------------------
        //
        // It has to name ONE next thing, and it has to be the RIGHT one at each
        // stage — a coach that says "verify it" before there is a start is worse
        // than silence, because it sends you to a button that cannot work.
        var fg = Forge.New(5);
        Ok(fg.Advice().step == 1, "an empty cube was not asked for trace");

        // lay the eight it asks for
        fg.tool = '+';
        for (int i = 0; i < 8; i++) fg.Apply(i % 5, i / 5);
        Ok(fg.Advice().step == 2, "eight traces did not advance to START, got " + fg.Advice().step);

        fg.tool = 'S'; fg.Apply(0, 0);
        Ok(fg.Advice().step == 3, "a placed start did not advance to EXIT");

        fg.tool = 'G'; fg.Apply(1, 0);
        Ok(fg.Advice().step == 4, "a placed exit did not advance to VERIFY");

        // and a verified cube must not stay at VERIFY, nor keep saying so after
        // the cube it was proved about has changed underneath it
        fg.proven = true;
        Ok(fg.Advice().step == 5, "a proven cube still asked to be verified");
        fg.tool = '#'; fg.Apply(4, 4);
        Ok(!fg.proven, "an edit left a stale VERIFIED behind it");
        Ok(fg.Advice().step == 4, "an edited cube did not go back to VERIFY");
    }

    public static void Main(string[] args)
    {
        // There was a `preview` verb here that rendered the chassis texture to a
        // PNG, because a generated texture is the one thing in this project that
        // cannot be judged by reading it. The chassis is a photograph now — see
        // unity/tools/chassis — so there is nothing left to render and the honest
        // move is to delete the harness rather than leave it printing a picture
        // of something the game no longer draws.
        Environment.ExitCode = Run();
    }
}
