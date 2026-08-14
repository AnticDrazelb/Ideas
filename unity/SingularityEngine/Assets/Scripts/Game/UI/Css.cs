using UnityEngine;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// THE SHIPPED BUILD'S :root, EVALUATED THE WAY A BROWSER EVALUATES IT.
    ///
    /// The reference for this interface is not a screenshot, it is the APK: the
    /// whole game is one index.html inside it, and the `:root` block at the top of
    /// that file is a complete design token set — every colour, radius, control
    /// height and type step the interface uses. This file is that block, ported
    /// value for value.
    ///
    /// WHY THE NUMBERS ARE NOT CONSTANTS. Every size in that token set is a
    /// `clamp(min, Nvw, max)` — a floor, a proportion of the viewport width, and a
    /// ceiling. An earlier pass through this port wrote them down as the single
    /// number each one produces at a 360 CSS px viewport, which is right on exactly
    /// one device and wrong on every other: `--t-micro` is 8.5px at 360 wide and
    /// 10px (its ceiling) on anything past 455, and `--h-btn` grows by six points
    /// across the same range. A fixed number is not the shipped value, it is one
    /// sample of it. So the clamp is the thing that gets ported, and it is
    /// re-evaluated whenever the viewport changes.
    ///
    /// ONE CANVAS UNIT IS ONE CSS PIXEL, which is what makes the numbers below
    /// usable verbatim rather than through a conversion factor. See
    /// <see cref="UiKit.Canvas"/> for how that is arranged and why the previous
    /// arrangement could not have been exact.
    /// </summary>
    public static class Css
    {
        // ---- the viewport ---------------------------------------------------
        //
        // A CSS PIXEL IS ANDROID'S DENSITY-INDEPENDENT PIXEL. That is not a
        // coincidence or an approximation: Chromium defines one CSS px as one dp
        // on Android, which is why the shipped build's `2.2vw` and the platform's
        // own 48dp touch target are measured in the same unit. So the viewport
        // this file evaluates against is the screen in dp, and the density that
        // converts it is read from the platform rather than guessed.

        static float _density = -1f;

        /// <summary>
        /// Physical pixels per CSS pixel. `DisplayMetrics.density` on Android,
        /// because that is the number Chromium itself divides by; `Screen.dpi/160`
        /// everywhere else, which is the same definition with a less reliable
        /// numerator.
        /// </summary>
        public static float Density
        {
            get
            {
                if (_density > 0f) return _density;
                _density = ReadDensity();
                return _density;
            }
        }

        static float ReadDensity()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var res = activity.Call<AndroidJavaObject>("getResources");
                using var dm = res.Call<AndroidJavaObject>("getDisplayMetrics");
                float d = dm.Get<float>("density");
                if (d > 0.1f) return d;
            }
            catch (System.Exception) { }
#endif
            // Screen.dpi is 0 on displays that do not report one; a phone-like 2.75
            // is a better guess than a divide by zero.
            float dpi = Screen.dpi;
            return dpi > 1f ? Mathf.Clamp(dpi / 160f, 0.75f, 6f) : 2.75f;
        }

        /// <summary>The viewport in CSS pixels — what `100vw` and `100vh` mean.</summary>
        public static float VpW => Screen.width / Density;
        public static float VpH => Screen.height / Density;

        public static float Vw(float n) => VpW * n * 0.01f;
        public static float Vh(float n) => VpH * n * 0.01f;

        static float Clamp(float lo, float pref, float hi) => Mathf.Clamp(pref, lo, hi);

        // ---- the shape of the device ----------------------------------------
        //
        // The four classes the shipped build toggles on <html>, with the shipped
        // thresholds. They are the whole of its responsive behaviour.

        /// <summary>`html.vpLand` — W > H * 1.18. A little past square, so a near-square tablet stays held.</summary>
        public static bool VpLand => Layout.IsLandscape;

        /// <summary>`html.tv` — docked: landscape, wide, and no touchscreen at all.</summary>
        public static bool Tv => Layout.IsDocked;

        /// <summary>`html.vpShort` — under 640 CSS px tall. The title screen drops its pitch.</summary>
        public static bool VpShort => VpH < 640f;

        /// <summary>`html.vpTiny` — under 520. The wordmark comes down a step.</summary>
        public static bool VpTiny => VpH < 520f;

        // ---- shape -----------------------------------------------------------

        public const float RChip = 6f, RCtl = 6f, RBtn = 7f, RCard = 10f, RSheet = 12f, RCell = 4f;
        /// <summary>--r-pill. 999 in the sheet, which is "however round it can be".</summary>
        public const float RPill = 999f;

        /// <summary>--hair. The only stroke weight the readouts own.</summary>
        public const float Hair = 1f;
        /// <summary>--rule. And a heavier one for things you press.</summary>
        public const float Rule = 2f;

        // ---- control heights -------------------------------------------------

        public static float HRead => Tv ? Clamp(40f, Vh(5.4f), 64f) : Clamp(38f, Vw(9.6f), 42f);
        public static float HCtl => Tv ? Clamp(48f, Vh(6.4f), 76f) : Clamp(44f, Vw(11.5f), 48f);
        public static float HBtn => Tv ? Clamp(54f, Vh(7.6f), 88f) : Clamp(46f, Vw(12.5f), 52f);
        public static float HRow => Tv ? Clamp(56f, Vh(7.4f), 86f) : Clamp(50f, Vw(13.5f), 58f);

        // ---- the type ladder -------------------------------------------------
        //
        // Nine steps, and nothing in the interface is allowed a tenth. The floor of
        // the smallest is 8.5 CSS px, and nothing may be smaller than that.

        public static float TMicro => Tv ? Clamp(9f, Vh(1.15f), 14f) : Clamp(8.5f, Vw(2.2f), 10f);
        public static float TTiny => Tv ? Clamp(10f, Vh(1.3f), 16f) : Clamp(9.5f, Vw(2.5f), 11f);
        public static float TFine => Tv ? Clamp(11f, Vh(1.5f), 18f) : Clamp(10.5f, Vw(2.8f), 12f);
        public static float TSm => Tv ? Clamp(12.5f, Vh(1.85f), 22f) : Clamp(12f, Vw(3.1f), 13.5f);
        public static float TMd => Tv ? Clamp(14f, Vh(2.15f), 26f) : Clamp(13f, Vw(3.6f), 15.5f);
        public static float TLg => Tv ? Clamp(16f, Vh(2.5f), 30f) : Clamp(15f, Vw(4.1f), 17.5f);
        public static float TXl => Tv ? Clamp(19f, Vh(3.2f), 38f) : Clamp(18f, Vw(5.1f), 22f);
        public static float T2Xl => Tv ? Clamp(23f, Vh(4f), 48f) : Clamp(22f, Vw(6.3f), 28f);
        public static float T3Xl => Tv ? Clamp(32f, Vh(5.8f), 70f) : Clamp(30f, Vw(9.2f), 42f);

        /// <summary>A type step as Unity wants it: whole units, and never zero.</summary>
        public static int Pt(float cssPx) => Mathf.Max(1, Mathf.RoundToInt(cssPx));

        // ---- tracking --------------------------------------------------------
        //
        // --track is 0.1em on the body and every component overrides it. This is
        // the single most load-bearing property in the whole sheet: the interface
        // is monospace capitals, and monospace capitals without tracking are a
        // password field. See <see cref="Trk"/> for how it is applied, since uGUI's
        // Text has no letter-spacing of its own.

        public const float Track = 0.1f;

        // ---- the colours -----------------------------------------------------
        //
        // These live in Palette, which was already extracted value for value and
        // checked against the APK. The three that are compositions rather than
        // plain values are here, because they are *this* sheet's rather than the
        // board's.

        /// <summary>--edge. rgba(234,88,12,.34) — the border every control wears.</summary>
        public static Color Edge => new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.34f);
        /// <summary>--edge-2. The quieter one, for a control that is not the subject.</summary>
        public static Color Edge2 => new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.16f);
        /// <summary>--edge-on. A control that is lit.</summary>
        public static Color EdgeOn => new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.85f);

        /// <summary>The primary's inset rim: rust-hi at 55%.</summary>
        public static Color PrimaryRim => new Color(Palette.RustHi.r, Palette.RustHi.g, Palette.RustHi.b, 0.55f);
        /// <summary>The primary's seat: `0 3px 0 rgba(120,40,0,.75)`.</summary>
        public static Color PrimarySeat => new Color32(120, 40, 0, 191);
        /// <summary>Every other control's seat: `0 2px 0 rgba(0,0,0,.55)`.</summary>
        public static Color Seat => new Color(0f, 0f, 0f, 0.55f);

        // ---- the layer -------------------------------------------------------
        //
        // .layer's padding, which is the margin every screen in the game is
        // composed inside. The safe area is added to it by the canvas.

        public const float PadX = 20f, PadY = 22f;

        /// <summary>.btn / .btn2 / .settings max-width — 340. .card is 360, .manual and .grid are wider.</summary>
        public const float BtnMax = 340f, RowMax = 360f, CardMax = 360f, ManualMax = 380f, GridMax = 440f;
    }
}
