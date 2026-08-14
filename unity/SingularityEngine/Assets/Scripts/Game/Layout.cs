using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// A TELEVISION HAS NO TOUCHSCREEN, AND A TABLET IS NOT A TELEVISION.
    ///
    /// Three shapes, and the difference between them is not width, it is what the
    /// player is holding:
    ///
    ///   HELD      a phone in portrait. The board fits the width and the two HUD
    ///             bands take the rest. This is the shape everything is composed
    ///             against.
    ///   TURNED    the same device on its side. The board fits the HEIGHT now,
    ///             and the bands stay where they are — which is why they are
    ///             anchored to edges rather than sized as fractions.
    ///   DOCKED    wide, landscape and no touchscreen at all: a television, or a
    ///             desktop pretending to be one.
    ///
    /// THE OVERSCAN MARGIN IS NOT A DESIGN DECISION, IT IS A PLATFORM RULE. A TV
    /// throws away the outer few percent of the picture, so nothing that has to be
    /// read may live there. It applies only where the display is actually wide
    /// enough to be one, because insetting a phone by three and a half percent for
    /// no reason is just a smaller game.
    /// </summary>
    public static class Layout
    {
        /// <summary>The same 1.18 the web build uses: a little past square, so a near-square tablet stays "held".</summary>
        public static bool IsLandscape => Screen.width > Screen.height * 1.18f;

        static bool? _touch;

        /// <summary>
        /// Whether this device has a touchscreen at all. Cached: it cannot change,
        /// and it is the question that separates a tablet on its side from a TV.
        /// </summary>
        public static bool HasTouch
        {
            get
            {
                _touch ??= Input.touchSupported || SystemInfo.deviceType == DeviceType.Handheld;
                return _touch.Value;
            }
        }

        public static bool IsDocked => !HasTouch && IsLandscape && Screen.width >= 900;

        /// <summary>Fraction of the shorter axis to give up at every edge. Zero unless docked.</summary>
        public static float Overscan => IsDocked ? 0.035f : 0f;

        public static int OverscanPixels => Mathf.RoundToInt(Mathf.Min(Screen.width, Screen.height) * Overscan);

        // ---- what the board actually gets ------------------------------------
        //
        // THE BANDS ARE MEASURED, NOT GUESSED. Reserving a FRACTION of the height
        // for chrome is right for the layout it was written against and wrong the
        // moment a button is added — the web build hit exactly that and the cube
        // came out sitting under the primary action of the whole game.
        //
        // They are also not constants, for the same reason nothing else in this
        // interface is: the HUD is built out of `clamp()` values that move with the
        // viewport, so a band written down as one number is that band's height on
        // one device. These are computed from the same tokens the HUD is laid out
        // with, in CSS pixels, and converted to real ones at the end.

        /// <summary>
        /// #barTop: 8 above it, the chip row, a 3 gap and the context line.
        /// </summary>
        public static float TopBandCss
        {
            get
            {
                float ic = Mathf.Clamp(UI.Css.Vw(4.6f), 15f, 21f);
                float n = Mathf.Clamp(UI.Css.Vw(5.6f), 17f, 26f);
                float pad = Mathf.Clamp(UI.Css.Vw(2.4f), 7f, 12f);
                return 8f + (pad * 2f + Mathf.Max(ic, n)) + 3f + UI.Css.TFine * 1.6f;
            }
        }

        /// <summary>
        /// #barBot and the fold ticks above it: 12 off the floor, a control, 14,
        /// and the ticks themselves.
        /// </summary>
        public static float BottomBandCss => 12f + UI.Css.HCtl + 14f + 5f + 8f;

        /// <summary>
        /// The rectangle left for the board, in pixels, after the safe area, the
        /// overscan and the two HUD bands.
        /// </summary>
        public static Rect BoardRect()
        {
            Rect safe = Screen.safeArea;
            int ov = OverscanPixels;

            float d = UI.Css.Density;
            float top = TopBandCss * d + ov;
            float bottom = BottomBandCss * d + ov;

            return new Rect(
                safe.xMin + ov,
                safe.yMin + bottom,
                Mathf.Max(1f, safe.width - ov * 2f),
                Mathf.Max(1f, safe.height - top - bottom));
        }
    }
}
