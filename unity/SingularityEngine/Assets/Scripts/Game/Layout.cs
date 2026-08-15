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
        /// THE HOUSING IS NOT ONE NUMBER ANY MORE.
        ///
        /// It was, while it was drawn: a border you could make as thick as you
        /// liked and the same on all four sides. The case is a photograph of a
        /// real object now, and that object's bezel is half again as deep at the
        /// top and bottom as it is at the sides — so there are four numbers, and
        /// they live in <see cref="Chassis"/> because they are measurements OF THE
        /// ART. This is only the seam: it is where the rest of the interface asks
        /// "how much of the edge is not mine", and nothing outside these two files
        /// needs to know that the answer came out of a picture.
        /// </summary>
        public static float ChassisLeft => Chassis.InsetLeft;
        public static float ChassisRight => Chassis.InsetRight;
        public static float ChassisTop => Chassis.InsetTop;
        public static float ChassisBottom => Chassis.InsetBottom;

        /// <summary>
        /// THE GLASS, IN THE UNITS EVERY SCREEN IS AUTHORED IN.
        ///
        /// A screen that has to do arithmetic about its own height — the Forge is
        /// the one, because its deck takes whatever the fixed bands below it do
        /// not — used to do it against the literal 1280. That was the canvas, and
        /// the canvas is not what a screen gets: it is inset into the opening, and
        /// the housing takes a hundred and fifty units of height. So the Forge
        /// sized its deck as though it had the whole display, and the bottom row
        /// of the column went off the bottom of the plate.
        ///
        /// The reference numbers rather than the live rect, deliberately: these
        /// are read while the screen is being BUILT, before any canvas has been
        /// laid out and while every rect in the game still measures zero.
        /// </summary>
        public static float PlateWidth => RefWidth - ChassisLeft - ChassisRight;
        public static float PlateHeight => RefHeight - ChassisTop - ChassisBottom;

        /// <summary>
        /// The rectangle left for the board, in pixels, after the safe area, the
        /// overscan, the housing and the two HUD bands.
        ///
        /// THE BANDS ARE MEASURED FROM THE GLASS, not from the display. They are
        /// the space the HUD occupies, the HUD is inset into the opening, and a
        /// band measured from the display edge would have counted the bezel twice
        /// — which is how the cube ends up sitting a bezel's width higher than the
        /// aperture drawn around it.
        /// </summary>
        public static Rect BoardRect()
        {
            Rect safe = Screen.safeArea;
            int ov = OverscanPixels;

            // the bands are authored against the reference height and scale with
            // whatever the canvas scaler is doing, which is the shorter axis
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            if (scale <= 0f) scale = 1f;

            float top = (TopBand + ChassisTop) * scale + ov;
            float bottom = (BottomBand + ChassisBottom) * scale + ov;
            float left = ChassisLeft * scale + ov;
            float right = ChassisRight * scale + ov;

            return new Rect(
                safe.xMin + left,
                safe.yMin + bottom,
                Mathf.Max(1f, safe.width - left - right),
                Mathf.Max(1f, safe.height - top - bottom));
        }
    }
}
