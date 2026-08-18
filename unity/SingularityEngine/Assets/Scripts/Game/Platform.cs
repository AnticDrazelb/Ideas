using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE ONE QUESTION THIS GAME ASKS THE DEVICE ABOUT THE PERSON HOLDING IT.
    ///
    /// Store.Load carried this comment for as long as it has existed:
    ///
    ///     EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who has
    ///     set "reduce motion" at the OS level has already told every app they
    ///     open how much shake they want, and making them find a switch to say it
    ///     again is not a preference, it is a tax.
    ///
    /// and under it, this line:
    ///
    ///     Data.fx = SystemInfo.deviceType == DeviceType.Handheld ? 1 : 1;
    ///
    /// Both branches are one. The conditional decides nothing, the OS is never
    /// asked, and every fresh install starts at full motion however the person
    /// has set their phone up. A comment promising an accessibility behaviour
    /// over code that does not do it is worse than neither, because it is the
    /// reason nobody goes looking.
    ///
    /// Android has no "reduce motion" switch as such. What it has — and what
    /// every accessibility guide points at, and what the platform's own
    /// animations honour — is the three animation scales under Developer options
    /// and Accessibility. Setting any of them to zero is how a person on Android
    /// says "stop moving things", and `animator_duration_scale` is the one that
    /// governs in-app animation rather than window transitions.
    ///
    /// IT IS ONLY EVER A DEFAULT. It seeds the setting on a save that has never
    /// been written; it is not consulted again, and it never overrides a choice
    /// the player has made in CALIBRATE. Somebody who turns their phone's
    /// animations off and then asks this game for full motion gets full motion.
    ///
    /// AND A FAILURE IS A SHRUG. The same rule Store's persistence follows and
    /// the same one Haptics follows: every call is wrapped, and a device that
    /// will not answer leaves the default exactly where it was.
    /// </summary>
    public static class Platform
    {
        static bool? _stillWanted;

        /// <summary>
        /// Has the person asked their device to stop animating? Cached, because it
        /// is read once at load and cannot change what it seeds afterwards.
        /// </summary>
        public static bool AnimationsOff
        {
            get
            {
                if (_stillWanted.HasValue) return _stillWanted.Value;
                _stillWanted = Ask();
                return _stillWanted.Value;
            }
        }

        static bool Ask()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var global = new AndroidJavaClass("android.provider.Settings$Global");

                // Settings.Global.getFloat(ContentResolver, String, float) — the
                // overload WITH a default, which returns it rather than throwing
                // when the key has never been written. The one without a default
                // throws SettingNotFoundException, and a throw here would cost the
                // launch a frame on every device that has never opened Developer
                // options, which is nearly all of them.
                float scale = global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
                return scale <= 0.001f;
            }
            catch
            {
                // an OS that will not answer has not asked for anything
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>For the harness, which has no device to ask.</summary>
        public static void Override(bool? off) => _stillWanted = off;
    }
}
