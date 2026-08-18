using System;
using System.Reflection;
using Singularity.Game;
using UnityEngine;

/// <summary>
/// THE ONE THING A PLAYER CANNOT GET BACK.
///
/// Everything else in this game is reproducible. The board is deterministic, the
/// settings take a minute to redo, a cube can be built again. Thirty vaults of
/// bests are a hundred hours somebody spent, there is no server holding a copy,
/// and there is no support channel to recover them from.
///
/// The save path had never been executed by anything. It could not be: the
/// harness stubbed PlayerPrefs to forget and JsonUtility to return the default,
/// so "does a corrupt save recover from its backup" was not a question that could
/// be asked. Both are implemented for real now, which is what these checks are
/// standing on — and the honest limit is that they prove Store's DECISIONS
/// rather than that Unity's parser agrees with System.Text.Json on some input
/// neither thought of.
///
/// Four launches, and the game has to survive all of them:
///
///   NOTHING THERE      a clean install
///   GOOD               the ordinary case
///   LIVE CORRUPT       a write interrupted by a battery pull
///   BOTH CORRUPT       and the bytes still have to exist afterwards
/// </summary>
static class SaveChecks
{
    const string Key = "turnkey-v2";
    const string Backup = "turnkey-v2-prev";
    const string Quarantine = "turnkey-v2-unreadable";

    /// <summary>
    /// Store loads once per process by design — a second Load would throw away
    /// whatever is in memory. Testing four launches means clearing that latch,
    /// which is the one thing here reaching past the public surface, and it
    /// beats the alternative of not testing the save path at all.
    /// </summary>
    static void Relaunch()
    {
        typeof(Store).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static)
                     ?.SetValue(null, false);
        Store.Load();
    }

    public static void Run(Action<bool, string> ok)
    {
        // ---- a clean install ------------------------------------------------
        PlayerPrefs.Wipe();
        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Fresh,
           "a clean install did not report itself as fresh, it said " + Store.LoadedFrom);
        ok(Store.Data.reached == 1, "a clean install did not start at vault one");
        ok(Store.Data.motion == (int)Access.MotionLevel.Full,
           "a clean install on an ordinary device did not start at full motion");
        ok(Store.Data.light == (int)Access.LightLevel.Full,
           "a clean install did not start at full light");

        // ---- a clean install on a phone that has asked for stillness ---------
        //
        // The line that seeds this used to be `Data.fx = deviceType == Handheld
        // ? 1 : 1`, whose branches are the same number — so the comment above it
        // promised to respect the device's own reduce-motion setting and the code
        // never asked. It asks now, and this is the difference between the two.
        //
        // AND ONLY MOTION MOVES. An animation scale of zero is a vestibular
        // statement; sparks and the full-screen flash answer to a different
        // person, and the access pass exists because those two were one switch.
        Platform.Override(true);
        PlayerPrefs.Wipe();
        Relaunch();
        ok(Store.Data.motion == (int)Access.MotionLevel.Reduced,
           "a device with its animations switched off still got full motion");
        ok(Store.Data.light == (int)Access.LightLevel.Full,
           "a device with its animations switched off also lost its light, which is a different criterion");

        // and a choice already made is never overridden by the platform
        Store.Data.motion = (int)Access.MotionLevel.Full;
        Store.Flush();
        Relaunch();
        ok(Store.Data.motion == (int)Access.MotionLevel.Full,
           "the platform's preference overrode a choice the player had already made");
        Platform.Override(false);

        PlayerPrefs.Wipe();
        Relaunch();

        // ---- the ordinary case ----------------------------------------------
        Store.Data.reached = 12;
        Store.SetBest(7, 19);
        Store.Flush();
        string good = PlayerPrefs.GetString(Key, "");
        ok(!string.IsNullOrEmpty(good), "Flush wrote nothing");

        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Live, "a good save did not load live");
        ok(Store.Data.reached == 12 && Store.TryBest(7, out int b7) && b7 == 19,
           "a good save came back missing its progress");

        // ---- a write interrupted -------------------------------------------
        //
        // The backup only exists once a second write has happened, which is the
        // real shape of it: a player who is corrupted on their very first write
        // has nothing to fall back to and that cannot be helped. This one has
        // played twice.
        Store.Data.reached = 20;
        Store.Flush();
        ok(PlayerPrefs.GetString(Backup, "") == good,
           "the backup is not the previous live value");

        PlayerPrefs.SetString(Key, good.Substring(0, good.Length / 2));   // truncated
        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Recovered,
           "a truncated save did not recover from its backup, it said " + Store.LoadedFrom);
        ok(Store.Data.reached == 12, "the recovered save is not the one that was backed up");
        ok(PlayerPrefs.HasKey(Quarantine), "the unreadable save was not kept anywhere");

        // ---- AND THE BYTES ARE NEVER WRITTEN OVER ----------------------------
        //
        // A SECOND BAD LAUNCH IS THE CASE THAT MATTERS, and checking that a GOOD
        // launch leaves the quarantine alone does not test it — a good launch
        // never reaches the code that writes there, so that check passes whether
        // the guard exists or not. It has to be a different corruption.
        string kept = PlayerPrefs.GetString(Quarantine, "");
        ok(!string.IsNullOrEmpty(kept), "the quarantine is empty before the second-launch check");
        PlayerPrefs.SetString(Key, "a different kind of broken");
        PlayerPrefs.SetString(Backup, "also broken, differently");
        Relaunch();
        ok(PlayerPrefs.GetString(Quarantine, "") == kept,
           "a second bad launch overwrote the quarantined save — the first player's bytes are gone");

        // ---- both gone -------------------------------------------------------
        PlayerPrefs.Wipe();
        PlayerPrefs.SetString(Key, "{ not json");
        PlayerPrefs.SetString(Backup, "also not json");
        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Lost,
           "an unreadable pair did not report itself lost, it said " + Store.LoadedFrom);
        ok(Store.Data.reached == 1, "a lost save did not fall back to a fresh one");
        ok(PlayerPrefs.GetString(Quarantine, "") == "{ not json",
           "the unreadable save was not quarantined");

        // AND IT MUST NOT WRITE OVER WHAT IT COULD NOT READ. A save that failed
        // to parse might still be recoverable by hand; the previous behaviour put
        // an empty one over the top of it within a quarter of a second, because
        // that is when the debounce fires on the first setting the game touches.
        Store.Flush();
        ok(PlayerPrefs.GetString(Backup, "") == "also not json",
           "a lost launch overwrote the backup with its own empty save");

        // ---- the shape, not the syntax ---------------------------------------
        //
        // The failure that actually loses progress is JSON that PARSES and is the
        // wrong shape. JsonUtility does not object to a missing field, so a
        // half-written save comes back as a real SaveData with half a history
        // quietly absent and no exception anywhere for a catch to see.
        PlayerPrefs.Wipe();
        Relaunch();
        Store.Data.reached = 9;
        Store.SetBest(3, 11);
        Store.SetBest(4, 12);
        Store.Flush();
        string shaped = PlayerPrefs.GetString(Key, "");
        Store.Flush();                                    // so there is a backup to find

        // a null list: valid JSON, and a NullReferenceException out of Load —
        // at startup, before anything has drawn. The game does not fail to load
        // a save, it fails to launch.
        PlayerPrefs.SetString(Key, shaped.Replace("\"bestK\":[3,4]", "\"bestK\":null"));
        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Recovered,
           "a save with a null list was accepted instead of recovered, it said " + Store.LoadedFrom);

        // paired lists of different lengths: Rehydrate walks the shorter of the
        // two, so this cannot crash — it silently drops the tail of somebody's
        // records, which is worse, because nothing would ever say so
        PlayerPrefs.SetString(Key, shaped.Replace("\"bestV\":[11,12]", "\"bestV\":[11]"));
        Relaunch();
        ok(Store.LoadedFrom == Store.Origin.Recovered,
           "a save whose paired lists disagree was accepted, it said " + Store.LoadedFrom);
        ok(Store.TryBest(4, out int b4) && b4 == 12,
           "the record the mismatched save would have dropped is gone");

        PlayerPrefs.Wipe();
        Console.WriteLine("save: four launches — fresh, live, recovered, lost — and nothing is ever destroyed");
    }
}
