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

        public static void Buzz(int ms)
        {
            if (Store.Data.haptic == 0 || Store.Data.buzz <= 0 || ms <= 0) return;
            int scaled = Mathf.RoundToInt(ms * Store.Data.buzz / 100f);
            if (scaled <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Vibrator?.Call("vibrate", (long)scaled); } catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void Buzz(int[] pattern)
        {
            if (pattern == null || pattern.Length == 0) return;
            int total = 0;
            foreach (int p in pattern) total += p;
            Buzz(total);
        }
    }
}
