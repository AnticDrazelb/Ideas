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
        // came out sitting under the primary action of the whole game. These are
        // the heights the HUD actually occupies, in the same reference units the
        // canvas scaler works in.

        public const float RefWidth = 720f, RefHeight = 1280f;
        public const float TopBand = 162f, BottomBand = 128f;

        /// <summary>
        /// How much of every edge the housing takes. See <see cref="Chassis"/>.
        ///
        /// It is a border rather than a background, so this is the entire cost of
        /// it: everything the player reads is inset by this much, and the board
        /// gets whatever is left after the bands as it always did. Twenty-eight
        /// units is fourteen CSS pixels at this canvas's two-to-one — enough to
        /// carry a rivet and read as a rail, small enough that no screen had to
        /// be re-laid out around it.
        /// </summary>
        public const float ChassisEdge = 28f;

        /// <summary>
        /// The rectangle left for the board, in pixels, after the safe area, the
        /// overscan and the two HUD bands.
        /// </summary>
        public static Rect BoardRect()
        {
            Rect safe = Screen.safeArea;
            int ov = OverscanPixels;

            // the bands are authored against the reference height and scale with
            // whatever the canvas scaler is doing, which is the shorter axis
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            if (scale <= 0f) scale = 1f;

            float top = TopBand * scale + ov;
            float bottom = BottomBand * scale + ov;

            return new Rect(
                safe.xMin + ov,
                safe.yMin + bottom,
                Mathf.Max(1f, safe.width - ov * 2f),
                Mathf.Max(1f, safe.height - top - bottom));
        }
    }
}
