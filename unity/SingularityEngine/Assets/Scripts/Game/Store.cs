using System;
using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    [Serializable]
    public class MadeCube
    {
        public string key;       // the cube's own canonical id, so storing one twice is impossible
        public string name;
        public string id;
        public string code;      // the share code — the cargo, where the id is only the name
        public int n;
        public int par;
        public int best = -1;
        public long tbest = -1;
    }

    [Serializable]
    public class SaveData
    {
        // `best` is fewest folds per cube, `tbest` is fastest clear in ms per
        // cube, `vbest` is the posted total for a whole vault keyed by band.
        // Missing keys are the honest record of a cube nobody has cleared, which
        // is what makes "is this vault finished" a lookup rather than a guess.
        public List<int> bestK = new List<int>();
        public List<int> bestV = new List<int>();
        // A BEST IS MEANINGLESS WITHOUT THE PAR IT WAS SCORED AGAINST. Par for a
        // generated cube costs a couple of hundred milliseconds to mint, so a grid
        // of ten cannot ask for it — but it is free at the moment the cube is
        // cleared, which is the only moment anything needs it. Recorded then.
        public List<int> parK = new List<int>();
        public List<int> parV = new List<int>();
        public List<int> tbestK = new List<int>();
        public List<long> tbestV = new List<long>();
        public List<int> vbestK = new List<int>();
        public List<long> vbestV = new List<long>();

        public int reached = 1;
        public int sound = 1, haptic = 1, fx = 1, togo = 1, depth = 0;
        public int taught = 0, sawPlate = 0, sawPeek = 0, peekHinted = 0, taughtForge = 0;
        public int buzz = 100, bright = 100, contrast = 100;

        // ---- access ---------------------------------------------------------
        //
        // ONE SWITCH WAS ANSWERING TWO PEOPLE. `motion` is the camera and the
        // bent clock — vestibular; `light` is sparks, bloom and the full-screen
        // flash — photosensitive. They were both `fx`, they are different
        // criteria with different answers, and each now has an OFF rather than
        // a quieter setting. See Access.
        public int motion = 0;                       // 0 full, 1 reduced, 2 still
        public int light = 0;                        // 0 full, 1 reduced, 2 none
        public int legible = 0, captions = 0, assist = 0;
        public int vol = 100, room = 100;            // instrument level, room level

        /// <summary>
        /// A SAVE THAT PREDATES A SETTING HAS AN OPINION ABOUT IT ANYWAY.
        ///
        /// Motion used to be half of the EFFECTS bit, so a player who turned
        /// effects off had already said they wanted less movement — and a new
        /// field defaulting to zero would silently hand them full camera shake
        /// back on the update that was supposed to help them. The version rides
        /// with the save so that intent can be carried across exactly once.
        /// </summary>
        public int v = 0;

        public int dailyDay = -1, dailyBest = 0, dailySolved = 0;
        public List<int> dhistDay = new List<int>();
        public List<int> dhistFolds = new List<int>();
        public int streakCur = 0, streakBest = 0, streakLast = -1;
        public int postW = -1, postM = -1, postS = -1;

        public List<MadeCube> made = new List<MadeCube>();
        public string draft = "";
        public List<string> hidden = new List<string>();
    }

    /// <summary>
    /// PERSISTENCE — one key, and it is allowed to fail.
    ///
    /// A device with storage disabled should still play; it just forgets. Every
    /// read and write here is wrapped, and a failure is a shrug rather than an
    /// exception, exactly as the original's localStorage handling was.
    ///
    /// Writes are debounced: a level clear touches several fields in a row and
    /// there is no reason to serialise the whole save five times for one event.
    /// </summary>
    public static class Store
    {
        const string Key = "turnkey-v2";

        /// <summary>
        /// THE LAST SAVE THAT WAS KNOWN TO PARSE, AND THE ONE THAT DID NOT.
        ///
        /// A save is the only thing in this game a player cannot get back. The
        /// board is deterministic, the settings take a minute to redo — thirty
        /// vaults of bests are a hundred hours somebody spent, and there is no
        /// support channel to recover them from.
        ///
        /// Before this there was one key and one behaviour: a save that failed to
        /// parse was replaced with an empty one, and the next write — which is at
        /// most a quarter of a second later, because the debounce fires on the
        /// first setting the game touches — put the empty one over the top of it.
        /// The corrupt save was not ignored, it was DESTROYED, silently, with no
        /// copy anywhere.
        ///
        /// So: <see cref="Backup"/> holds the previous string that parsed, written
        /// one step behind the live one, and is tried when the live one fails.
        /// <see cref="Quarantine"/> holds whatever could not be read at all, and
        /// is never written over — a player who lands there has lost their
        /// progress from the game's point of view and still has their bytes.
        /// </summary>
        const string Backup = "turnkey-v2-prev";
        const string Quarantine = "turnkey-v2-unreadable";

        /// <summary>
        /// Which of the three the game is actually running on. The interface asks,
        /// because a player whose save did not load has to be TOLD — starting
        /// somebody over without a word is the worst thing this code can do, and
        /// it is also the thing they will assume happened whenever a level list
        /// looks wrong.
        /// </summary>
        public enum Origin { Fresh, Live, Recovered, Lost }
        public static Origin LoadedFrom { get; private set; } = Origin.Fresh;

        public static SaveData Data { get; private set; } = new SaveData();

        static readonly Dictionary<int, int> Best = new Dictionary<int, int>();
        static readonly Dictionary<int, int> Pars = new Dictionary<int, int>();
        static readonly Dictionary<int, long> TBest = new Dictionary<int, long>();
        static readonly Dictionary<int, long> VBest = new Dictionary<int, long>();
        static readonly Dictionary<int, int> DHist = new Dictionary<int, int>();

        static float _saveAt = -1f;
        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            string live = Read(Key), prev = Read(Backup);
            bool had = !string.IsNullOrEmpty(live) || !string.IsNullOrEmpty(prev);

            if (Parse(live)) LoadedFrom = string.IsNullOrEmpty(live) ? Origin.Fresh : Origin.Live;
            else if (Parse(prev))
            {
                LoadedFrom = Origin.Recovered;
                Keep(live);
                Debug.LogWarning("[Singularity] the save did not parse; recovered the previous one. " +
                                 "The unreadable copy is under " + Quarantine + ".");
            }
            else
            {
                LoadedFrom = had ? Origin.Lost : Origin.Fresh;
                Data = new SaveData();
                if (had)
                {
                    Keep(live);
                    Debug.LogError("[Singularity] neither the save nor its backup could be read. " +
                                   "Starting fresh; the unreadable copy is under " + Quarantine + ".");
                }
            }

            // EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who
            // has set "reduce motion" at the OS level has already told every app
            // they open how much shake they want, and making them find a switch
            // to say it again is not a preference, it is a tax.
            //
            // AND NOW IT ACTUALLY ASKS. This was
            //
            //     Data.fx = SystemInfo.deviceType == DeviceType.Handheld ? 1 : 1;
            //
            // whose two branches are the same number, so the conditional decided
            // nothing and the device was never consulted. See Platform: on Android
            // the question is the animator duration scale, and zero is how a
            // person says stop moving things.
            //
            // ONLY MOTION. The legacy `fx` bit seeded both motion and light
            // together because it used to be one switch, and that conflation is
            // exactly what the access pass split apart: shake and a bent clock are
            // vestibular, sparks and a full-screen flash are photosensitive, and
            // an animation-scale of zero is a statement about the first only.
            // Migrate still reads `fx` for saves written before the split.
            if (!had)
            {
                Data.fx = 1;
                if (Platform.AnimationsOff) Data.motion = (int)Access.MotionLevel.Reduced;

                // A SAVE WITH NO HISTORY HAS NOTHING TO MIGRATE, and saying so is
                // what stops the migration below from overwriting the line above:
                // it reads the legacy `fx` bit, finds the 1 that was just written,
                // and sets motion back to Full. Migration is for saves written
                // before the split, and this one was written after it.
                Data.v = 1;
            }

            Migrate();

            Rehydrate(Data.bestK, Data.bestV, Best);
            Rehydrate(Data.parK, Data.parV, Pars);
            Rehydrate(Data.tbestK, Data.tbestV, TBest);
            Rehydrate(Data.vbestK, Data.vbestV, VBest);
            Rehydrate(Data.dhistDay, Data.dhistFolds, DHist);
        }

        static string Read(string key)
        {
            try { return PlayerPrefs.GetString(key, ""); }
            catch { return ""; }
        }

        /// <summary>
        /// Try one stored string. Empty means "there was nothing here", which is a
        /// clean first run and not a failure — so it succeeds, with defaults.
        ///
        /// A THROW IS THE EASY CASE. The one that actually loses progress is JSON
        /// that parses and is the wrong SHAPE: JsonUtility does not object to a
        /// missing field, it leaves the default, so a truncated write comes back
        /// as a real SaveData with half a player's history quietly absent and no
        /// exception anywhere. <see cref="Whole"/> is the check for that.
        /// </summary>
        static bool Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) { Data = new SaveData(); return true; }
            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(raw);
                if (d == null || !Whole(d)) return false;
                Data = d;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Is this a SaveData or only shaped like one?
        ///
        /// EVERY LIST HAS AN INITIALISER AND THAT IS NOT ENOUGH. JsonUtility runs
        /// the constructor and then overwrites what the JSON mentions, so an
        /// absent field keeps its empty list — but an explicit `"bestK": null`
        /// sets it to null, and a null list reaching Rehydrate is a
        /// NullReferenceException thrown out of Load, at startup, before anything
        /// has drawn. The game does not fail to load a save; it fails to launch.
        ///
        /// The paired lists are also checked for agreeing lengths. Rehydrate
        /// already walks the shorter of the two, so a mismatch cannot crash — it
        /// silently drops the tail of somebody's records, which is worse, because
        /// nothing anywhere would ever say so.
        /// </summary>
        static bool Whole(SaveData d)
            => Paired(d.bestK, d.bestV) && Paired(d.parK, d.parV)
            && Paired(d.tbestK, d.tbestV) && Paired(d.vbestK, d.vbestV)
            && Paired(d.dhistDay, d.dhistFolds);

        static bool Paired<T>(List<int> k, List<T> v)
            => k != null && v != null && k.Count == v.Count;

        /// <summary>
        /// Put an unreadable save somewhere it cannot be written over. Once only:
        /// a second bad launch must not overwrite the quarantine with the empty
        /// string the first one left behind.
        /// </summary>
        static void Keep(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            try
            {
                if (PlayerPrefs.HasKey(Quarantine)) return;
                PlayerPrefs.SetString(Quarantine, raw);
                PlayerPrefs.Save();
            }
            catch { /* a device that cannot remember cannot quarantine either */ }
        }

        /// <summary>
        /// Carry an old save's intent into the settings that did not exist when
        /// it was written. Runs once per save, and the version is what makes
        /// "once" true — a migration that runs every load is a setting the player
        /// cannot change.
        /// </summary>
        static void Migrate()
        {
            // EVERY LAUNCH, NOT ONCE — this one is not a version step, it is a
            // reading of the save that is true whenever it is asked. A player who
            // has cleared a cube has been past the two the coach speaks on, and
            // must never be handed a tutorial on their four-hundredth. See Coach.
            Data.taught = Singularity.UI.Coach.Migrate(Data.taught, Data.reached);

            if (Data.v >= 1) return;
            // EFFECTS OFF MEANT BOTH LESS MOVEMENT AND LESS LIGHT, and it still
            // means both — it splits into two settings that start where it left
            // them, rather than into two settings that start switched on.
            bool wasOn = Data.fx != 0;
            Data.motion = wasOn ? (int)Access.MotionLevel.Full : (int)Access.MotionLevel.Reduced;
            Data.light = wasOn ? (int)Access.LightLevel.Full : (int)Access.LightLevel.Reduced;
            Data.v = 1;
            Save();
        }

        static void Rehydrate<T>(List<int> k, List<T> v, Dictionary<int, T> into)
        {
            into.Clear();
            for (int i = 0; i < k.Count && i < v.Count; i++) into[k[i]] = v[i];
        }

        static void Dehydrate<T>(Dictionary<int, T> from, List<int> k, List<T> v)
        {
            k.Clear(); v.Clear();
            foreach (var kv in from) { k.Add(kv.Key); v.Add(kv.Value); }
        }

        public static void Save()
        {
            _saveAt = Time.realtimeSinceStartup + 0.22f;
        }

        /// <summary>Called once a frame; performs the debounced write.</summary>
        public static void Tick()
        {
            if (_saveAt < 0f || Time.realtimeSinceStartup < _saveAt) return;
            _saveAt = -1f;
            Flush();
        }

        public static void Flush()
        {
            try
            {
                Dehydrate(Best, Data.bestK, Data.bestV);
                Dehydrate(Pars, Data.parK, Data.parV);
                Dehydrate(TBest, Data.tbestK, Data.tbestV);
                Dehydrate(VBest, Data.vbestK, Data.vbestV);
                Dehydrate(DHist, Data.dhistDay, Data.dhistFolds);

                // ONE STEP BEHIND, AND ONLY EVER FROM A STRING THAT PARSED.
                //
                // The backup is the PREVIOUS live value, not a second copy of the
                // one being written — two copies of the same corruption are one
                // copy of it. It is taken from the key rather than re-serialised
                // so that whatever the last successful write actually put on disk
                // is what gets kept, including the bytes a partial write left.
                //
                // Rolled before the new value goes down, so the window in which
                // neither key is good is a single SetString rather than a frame.
                string had = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(had) && LoadedFrom != Origin.Lost)
                    PlayerPrefs.SetString(Backup, had);

                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
                PlayerPrefs.Save();
            }
            catch { /* a device that cannot remember still gets to play */ }
        }

        // ---- typed accessors ------------------------------------------------

        public static bool TryBest(int level, out int folds) => Best.TryGetValue(level, out folds);
        public static void SetBest(int level, int folds) { Best[level] = folds; Save(); }

        /// <summary>The par this level was proved to have, remembered from the run that cleared it.</summary>
        public static bool TryPar(int level, out int par) => Pars.TryGetValue(level, out par);
        public static void SetPar(int level, int par) { Pars[level] = par; Save(); }

        public static bool TryTimeBest(int level, out long ms) => TBest.TryGetValue(level, out ms);
        public static void SetTimeBest(int level, long ms) { TBest[level] = ms; Save(); }

        public static bool TryVaultBest(int band, out long total) => VBest.TryGetValue(band, out total);
        public static void SetVaultBest(int band, long total) { VBest[band] = total; Save(); }

        public static IReadOnlyDictionary<int, int> DailyHistory => DHist;
        public static void SetDaily(int day, int folds) { DHist[day] = folds; Save(); }

        public static MadeCube Made(string key) => Data.made.Find(m => m.key == key);

        public static void PruneDaily(int today, int keepDays)
        {
            var drop = new List<int>();
            foreach (var kv in DHist) if (today - kv.Key > keepDays) drop.Add(kv.Key);
            foreach (int d in drop) DHist.Remove(d);
        }
    }
}
