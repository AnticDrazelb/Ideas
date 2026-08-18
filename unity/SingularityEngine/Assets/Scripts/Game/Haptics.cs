using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// A SWITCH SAYS WHETHER, A SLIDER SAYS HOW MUCH.
    ///
    /// Haptics were one bit for a thing that is not one bit: a pattern that reads
    /// as a ratchet on one phone is a jolt on another, and the only person who
    /// can settle that is the one holding the phone. So intensity scales the
    /// duration of every pattern, and zero is off.
    /// </summary>
    public static class Haptics
    {
        public const int Fold = 10, Move = 20;
        public static readonly int[] Fault = { 5, 5, 5, 5, 5 };
        public static readonly int[] Collapse = { 0, 50, 100 };

        /// <summary>
        /// The machine coming apart, which is the heaviest thing that happens and
        /// is now a separate event from the collapse that precedes it — the core
        /// takes you, and half a second later the engine fails. Two hits, not one.
        /// </summary>
        public static readonly int[] Shatter = { 0, 30, 40, 140 };

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static AndroidJavaObject Vibrator
        {
            get
            {
                if (_vibrator != null) return _vibrator;
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                catch { /* a device with no motor is not an error */ }
                return _vibrator;
            }
        }
#endif

        /// <summary>Is the motor allowed to run at all, and how hard.</summary>
        public static bool On => Store.Data.haptic != 0 && Store.Data.buzz > 0;

        /// <summary>
        /// ONE DURATION, SCALED. Pure, so the harness can hold it to the switch
        /// and the slider without a phone in the room.
        /// </summary>
        public static long Plan(int ms, int strength)
            => ms <= 0 || strength <= 0 ? 0L : (long)Mathf.RoundToInt(ms * strength / 100f);

        /// <summary>
        /// AND A PATTERN STAYS A PATTERN.
        ///
        /// This used to ADD THE ARRAY UP and buzz for the total, which is not a
        /// scaling of the pattern — it is the destruction of it. Every array here
        /// is an Android vibration pattern, `{ delay, on, off, on, ... }`, so
        /// Shatter's two hits thirty and a hundred and forty long with forty of
        /// silence between them came out as one flat two hundred and ten
        /// millisecond drone, and so did every other "pattern" in the game. They
        /// were all the same buzz at different lengths.
        ///
        /// The strength scales every entry, so the shape holds and the whole
        /// pattern stretches or shrinks — which is what a duration-based intensity
        /// control can honestly do.
        /// </summary>
        public static long[] Plan(int[] pattern, int strength)
        {
            if (pattern == null || pattern.Length == 0 || strength <= 0) return null;
            var outp = new long[pattern.Length];
            long any = 0;
            for (int i = 0; i < pattern.Length; i++)
            {
                outp[i] = (long)Mathf.RoundToInt(pattern[i] * strength / 100f);
                if (i % 2 == 1) any += outp[i];       // odd entries are the ON times
            }
            return any > 0 ? outp : null;
        }

        public static void Buzz(int ms)
        {
            if (!On) return;
            long scaled = Plan(ms, Store.Data.buzz);
            if (scaled <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Fire(scaled, null);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void Buzz(int[] pattern)
        {
            if (!On) return;
            long[] scaled = Plan(pattern, Store.Data.buzz);
            if (scaled == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Fire(0, scaled);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool _warned;

        /// <summary>
        /// THE MOTOR, THROUGH WHICHEVER DOOR THIS ANDROID HAS.
        ///
        /// `vibrate(long)` and `vibrate(long[], int)` were deprecated at API 26
        /// and several manufacturers have since made them do nothing at all, so
        /// the modern path is tried first: a VibrationEffect, which is the only
        /// call that is not deprecated on a phone anybody is buying today. The old
        /// call is kept underneath because it is the only one that exists below
        /// API 26 and it costs one catch block.
        ///
        /// AND A FAILURE IS SAID ONCE. This was `catch { }`, which is how a
        /// missing manifest permission turned into a silent motor that nobody
        /// could account for. Once per launch, named, and never again — a log line
        /// a frame is its own bug.
        /// </summary>
        static void Fire(long ms, long[] pattern)
        {
            AndroidJavaObject v = Vibrator;
            if (v == null) return;

            try
            {
                using var fx = new AndroidJavaClass("android.os.VibrationEffect");
                AndroidJavaObject effect = pattern != null
                    ? fx.CallStatic<AndroidJavaObject>("createWaveform", pattern, -1)
                    : fx.CallStatic<AndroidJavaObject>("createOneShot", ms, -1);   // -1 is DEFAULT_AMPLITUDE
                v.Call("vibrate", effect);
                return;
            }
            catch { /* below API 26, or an OEM that hides the class */ }

            try
            {
                if (pattern != null) v.Call("vibrate", pattern, -1);
                else v.Call("vibrate", ms);
            }
            catch (System.Exception e)
            {
                if (_warned) return;
                _warned = true;
                Debug.LogWarning("[Singularity] the vibrator refused: " + e.Message
                               + " — check android.permission.VIBRATE is in the manifest");
            }
        }
#endif
    }
}
