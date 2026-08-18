using System;
using Singularity.Core;
using Singularity.UI;

using Singularity.Game;
/// <summary>
/// TEN WORDS, AND WHEN THEY ARE SAID.
///
/// The word after a chapter is the only voice in the game, which makes it the one
/// screen where being wrong is loud: said twice it is a toll booth, said on the
/// wrong cube it is a non-sequitur, and said on a daily it is the machine
/// addressing somebody who is not playing the story.
/// </summary>
static class ChapterChecks
{
    public static void Run(Action<bool, string> ok)
    {
        // ---- one word a chapter, and the last one is the ending's -------------
        ok(Chapters.Words.Length == Vaults.RankedVaults,
           "there are " + Chapters.Words.Length + " words for " + Vaults.RankedVaults + " chapters");

        int said = 0;
        for (int i = 0; i < Chapters.Words.Length; i++)
        {
            string w = Chapters.Words[i];
            if (w.Length == 0) continue;
            said++;
            ok(w == w.ToLowerInvariant(), "chapter " + (i + 1) + "'s word is not lower case: " + w);
            ok(w.EndsWith("."), "chapter " + (i + 1) + "'s word does not end in a full stop: " + w);
            ok(w.IndexOf(' ') < 0, "chapter " + (i + 1) + " says more than one word: " + w);
        }
        ok(Chapters.Words[Chapters.Words.Length - 1].Length == 0,
           "the last chapter has a word of its own, which competes with the ending");

        // ---- THE COUNTDOWN IS REAL, which is the whole reason it opens on one --
        //
        // A player reads "nine." after the first chapter as nine to go and forms a
        // hypothesis. A list that never returns to it spends that once. Three
        // numbers, four chapters apart, and the gap to the end shortening is the
        // only device on the list that can make somebody dread the last chapter.
        ok(Chapters.Words[0] == "nine." && Chapters.Words[4] == "half." && Chapters.Words[8] == "one.",
           "the countdown does not land on chapters one, five and nine");

        // ---- fired on the last cube of a chapter, and nowhere else ------------
        int ends = 0, wrong = 0;
        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            bool last = level < Vaults.LastCube
                     && Vaults.VaultOf(level + 1) != Vaults.VaultOf(level);
            bool fires = Chapters.Ends(level, false, false);
            if (fires) ends++;
            if (fires != last) wrong++;
        }
        ok(wrong == 0, wrong + " cubes fire the chapter word somewhere other than a chapter boundary");
        ok(ends == said, ends + " boundaries have a word and " + said + " words are written");

        // ---- never on a daily, a made cube, or the last cube ------------------
        int end9 = Vaults.VaultStart(8) + Vaults.VaultSize(8) - 1;
        ok(!Chapters.Ends(end9, true, false), "the machine speaks after a daily");
        ok(!Chapters.Ends(end9, false, true), "the machine speaks after a made cube");
        ok(!Chapters.Ends(Vaults.LastCube, false, false),
           "a chapter word fires on the last cube, over the ending");

        // ---- ONCE. Going back for a par must not replay it --------------------
        int mask = 0;
        int spoken = 0;
        for (int pass = 0; pass < 2; pass++)
            for (int level = 1; level <= Vaults.LastCube; level++)
                if (Chapters.Due(level, false, false, mask))
                {
                    spoken++;
                    mask = Chapters.Mark(mask, level);
                }
        ok(spoken == said, "walking the ladder twice says " + spoken + " words, not " + said);

        // ---- and it is over before it outstays itself -------------------------
        float longest = 0;
        foreach (string w in Chapters.Words)
            if (w.Length > 0) longest = Math.Max(longest, Chapters.Seconds(w));
        ok(longest < 4.0f, "a chapter word holds the screen for " + longest.ToString("0.00") + "s");
        ok(Chapters.DeafFor > 0f, "a chapter word can be dismissed by the tap that opened it");

        Console.WriteLine("chapter: " + said + " words across " + ends
                        + " boundaries, said once each, longest " + longest.ToString("0.00") + "s");
        var line = new System.Text.StringBuilder("chapter:");
        foreach (string w in Chapters.Words) line.Append(' ').Append(w.Length == 0 ? "—" : w);
        Console.WriteLine(line.ToString() + " " + Chapters.Farewell + " (the ending)");
    }
}

/// <summary>
/// FINISHING RELOCKS THE LADDER, and the ways that can go wrong are all quiet.
///
/// A soft lock only half the screens know about is a lock a player walks around
/// by accident. A lock with no way out strands somebody who wants to show a
/// friend the last cube. And an unlock switch that moves the frontier destroys
/// the lock permanently the first time it is used, which nothing on screen would
/// report — the player would simply find, months later, that finishing no longer
/// did anything.
/// </summary>
static class ProgressChecks
{
    public static void Run(Action<bool, string> ok)
    {
        var save = Store.Data;
        int reached = save.reached, runs = save.runs, unlocked = save.unlocked;

        // ---- the gate is one question, asked one way ------------------------
        save.unlocked = 0; save.reached = 20;
        ok(Store.Open(20) && !Store.Open(21), "the frontier does not gate at reached");
        save.unlocked = 1;
        ok(Store.Open(Vaults.LastCube), "the unlock switch does not open the last cube");
        save.unlocked = 0;

        // ---- a finished machine is a fact about runs, not about reached ------
        ok(!Vaults.Cleared(0), "a save that has never finished reads as finished");
        ok(Vaults.Cleared(1), "a save that has finished once does not read as finished");
        ok(Vaults.Resume(1) == 1, "a relocked ladder does not resume at cube one");

        // ---- and the words are still there for the second run ---------------
        //
        // The chapter words are said once EVER rather than once a run. A machine
        // that says nine again on the second lap is a machine repeating itself,
        // and the countdown only lands the first time anyway.
        int mask = 0;
        foreach (int level in new[] { 15, 30, 45 }) mask = Chapters.Mark(mask, level);
        ok(!Chapters.Due(15, false, false, mask), "a second run repeats the chapter words");

        save.reached = reached; save.runs = runs; save.unlocked = unlocked;
        Console.WriteLine("progress: the ladder gates at reached, relocks on the last cube, "
                        + "and the switch opens it without moving the frontier");
    }
}
