using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// THE SUPPLY, AND THE CUTTER — minting on a thread that is not the one
    /// drawing.
    ///
    /// This was the worst defect in the web original and it was invisible from
    /// inside it: cutting a cube costs a couple of hundred milliseconds, and the
    /// prebuild was doing it on the main thread about two seconds into every
    /// level. Measured there as three stalls of 408, 116 and 587ms per load. On a
    /// mid-range phone that is plausibly one and a half to three seconds of
    /// completely frozen game, every level, for the whole game. Nothing else
    /// about the craft survives that.
    ///
    /// The web fix was a Blob worker running the file's own source, which needed
    /// an elaborate dance to guarantee there was never a second copy of the
    /// generator to drift out of step. Here it is simply a background thread
    /// calling the same static methods, because Singularity.Core touches no Unity
    /// API and has no state that is not passed in — which is the whole reason it
    /// was written with `noEngineReferences` set.
    ///
    /// THE SYNCHRONOUS PATH STILL EXISTS AND IS STILL CORRECT. It runs when a
    /// level is reached faster than it could be cut. It blocks, exactly as it
    /// always did; it is simply no longer the path taken on every level.
    /// </summary>
    public static class LevelSupply
    {
        const int CacheLimit = 24;

        static readonly Dictionary<int, Level> Cache = new Dictionary<int, Level>();
        static readonly List<int> Order = new List<int>();
        static readonly object Gate = new object();
        static readonly HashSet<int> InFlight = new HashSet<int>();

        /// <summary>Results the cutter has finished, waiting to be picked up on the main thread.</summary>
        static readonly Queue<(int level, Level lv, Action<Level> then)> Done
            = new Queue<(int, Level, Action<Level>)>();

        public static Level Get(int level)
        {
            if (Baked.TryAt(level, out BakedLevel baked)) return baked.ToLevel(level);

            // THE CATALOGUE FIRST, and for five hundred cubes it is the only
            // answer. The ladder is cut offline out of a hundred thousand
            // candidates and shipped as text — see Core/Catalogue — so a cube is
            // read rather than minted, which is what lets it have been CHOSEN.
            //
            // The generator stays underneath for two reasons and both matter: the
            // daily still borrows it, and a build whose catalogue asset failed to
            // import should be a game with a worse ladder rather than no game.
            Level fromCat = Catalogue.Get(level);
            if (fromCat != null)
            {
                Put(level, fromCat);
                return fromCat;
            }

            lock (Gate) { if (Cache.TryGetValue(level, out Level hit)) return hit; }

            Level lv = Generator.Mint(level);
            Put(level, lv);
            return lv;
        }

        static void Put(int level, Level lv)
        {
            lock (Gate)
            {
                if (!Cache.ContainsKey(level)) Order.Add(level);
                Cache[level] = lv;
                while (Order.Count > CacheLimit)
                {
                    Cache.Remove(Order[0]);
                    Order.RemoveAt(0);
                }
            }
        }

        public static bool Has(int level)
        {
            if (Baked.TryAt(level, out _)) return true;
            lock (Gate) return Cache.ContainsKey(level);
        }

        /// <summary>Cut the next cube while this one is being played.</summary>
        public static void Prebuild(int level, Action<Level> then = null)
        {
            if (Baked.TryAt(level, out _) || Has(level))
            {
                then?.Invoke(Baked.TryAt(level, out BakedLevel b2) ? b2.ToLevel(level) : Get(level));
                return;
            }

            lock (Gate) { if (!InFlight.Add(level)) return; }

            var t = new Thread(() =>
            {
                Level lv = null;
                try { lv = Generator.Mint(level); }
                catch (Exception e) { Debug.LogWarning("cutter failed for cube " + level + ": " + e.Message); }
                lock (Gate)
                {
                    InFlight.Remove(level);
                    // A cube that arrived while the player was already past it is
                    // still worth keeping: the cache is keyed by level, not by when
                    // it was asked for, and the next visit is then free.
                    if (lv != null) Done.Enqueue((level, lv, then));
                }
            })
            { IsBackground = true, Name = "cutter/" + level, Priority = System.Threading.ThreadPriority.BelowNormal };
            t.Start();
        }

        /// <summary>Main-thread pump. Callbacks must not run on the cutter.</summary>
        public static void Tick()
        {
            while (true)
            {
                (int level, Level lv, Action<Level> then) job;
                lock (Gate)
                {
                    if (Done.Count == 0) return;
                    job = Done.Dequeue();
                }
                Put(job.level, job.lv);
                job.then?.Invoke(job.lv);
            }
        }
    }
}
