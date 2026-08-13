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
        public List<int> tbestK = new List<int>();
        public List<long> tbestV = new List<long>();
        public List<int> vbestK = new List<int>();
        public List<long> vbestV = new List<long>();

        public int reached = 1;
        public int sound = 1, haptic = 1, fx = 1, togo = 1, depth = 0;
        public int taught = 0, sawPlate = 0, sawPeek = 0, peekHinted = 0, taughtForge = 0;
        public int buzz = 100, bright = 100, contrast = 100;

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

        public static SaveData Data { get; private set; } = new SaveData();

        static readonly Dictionary<int, int> Best = new Dictionary<int, int>();
        static readonly Dictionary<int, long> TBest = new Dictionary<int, long>();
        static readonly Dictionary<int, long> VBest = new Dictionary<int, long>();
        static readonly Dictionary<int, int> DHist = new Dictionary<int, int>();

        static float _saveAt = -1f;
        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string raw = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(raw)) Data = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData();
            }
            catch { Data = new SaveData(); }

            // EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who
            // has set "reduce motion" at the OS level has already told every app
            // they open how much shake they want, and making them find a switch
            // to say it again is not a preference, it is a tax.
            if (!PlayerPrefs.HasKey(Key)) Data.fx = SystemInfo.deviceType == DeviceType.Handheld ? 1 : 1;

            Rehydrate(Data.bestK, Data.bestV, Best);
            Rehydrate(Data.tbestK, Data.tbestV, TBest);
            Rehydrate(Data.vbestK, Data.vbestV, VBest);
            Rehydrate(Data.dhistDay, Data.dhistFolds, DHist);
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
                Dehydrate(TBest, Data.tbestK, Data.tbestV);
                Dehydrate(VBest, Data.vbestK, Data.vbestV);
                Dehydrate(DHist, Data.dhistDay, Data.dhistFolds);
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
                PlayerPrefs.Save();
            }
            catch { /* a device that cannot remember still gets to play */ }
        }

        // ---- typed accessors ------------------------------------------------

        public static bool TryBest(int level, out int folds) => Best.TryGetValue(level, out folds);
        public static void SetBest(int level, int folds) { Best[level] = folds; Save(); }

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
