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

        /// <summary>
        /// The scaler's match, and the exponent <see cref="CanvasScale"/> raises
        /// each ratio to. UiKit.Canvas reads it rather than repeating it — one
        /// number, so the pixel arithmetic in this file cannot describe a canvas
        /// the game is not drawing on.
        /// </summary>
        public const float CanvasMatch = 0.5f;
        public const float TopBand = 162f, BottomBand = 128f;

        /// <summary>
        /// THE TITLE'S OWN TWO BANDS, and they are not the HUD's.
        ///
        /// The front screen reserves a masthead — wordmark, the vault and cube it
        /// will resume, and the rule that closes them — and a block of controls
        /// stacked up from the floor. What is left between the two is the only
        /// place the machine itself can be, and it is the one thing on that screen
        /// worth looking at.
        ///
        /// They live here rather than as literals in Screens because three
        /// separate things have to agree about them: the screen that draws the
        /// masthead, the scrim that has to be clear where the cube is, and the
        /// camera that frames the cube into the gap. They were literals, the scrim
        /// was a gradient tuned by eye against a paragraph that has since been
        /// deleted, and the camera had never heard of either.
        /// </summary>
        public const float TitleMastBottom = 346f;

        /// <summary>The controls, measured up from the plate's floor. See Screens.BuildTitle.</summary>
        public const float TitleFootTop = 452f;

        /// <summary>
        /// How far the title's scrim takes to get the black off the type, at each
        /// end. The machine may not stand inside it — a cube under a gradient is
        /// the smudge the whole restaging is about — so the band it is framed into
        /// starts past it. See UiKit.Stage.
        /// </summary>
        public const float TitleScrimRamp = 30f;

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
        /// THE APERTURE — the window the board is framed in, and now the rect the
        /// board has to stay INSIDE.
        ///
        /// These two numbers were literals in Hud, where they drew a stroke, and
        /// the camera had never heard of them: it fitted the cube to the whole
        /// board rect, so the moment a fold turned the solid past square it grew
        /// wider than its own frame and crossed the line that is supposed to be
        /// the edge of the window. One definition, two consumers — the stroke and
        /// the camera cannot disagree about where the window is.
        /// </summary>
        public const float ApertureX = 16f, ApertureY = 10f;

        /// <summary>
        /// THE SCALE THE INTERFACE IS ACTUALLY DRAWN AT, AND IT WAS NOT THIS.
        ///
        /// Everything in this file converts canvas units to pixels, and it used
        /// <c>Min(w/720, h/1280)</c> to do it. The canvas does not: <see
        /// cref="UiKit.Canvas"/> sets <c>MatchWidthOrHeight</c> to 0.5, which is
        /// the GEOMETRIC MEAN of the two ratios, and Unity computes it as
        /// <c>pow(w/rw, 1-m) * pow(h/rh, m)</c>.
        ///
        /// On a 16:9 phone the two answers are within a per cent of each other,
        /// which is why nothing ever looked wrong. On 1440x2960 they are 2.000 and
        /// 2.151 — seven and a half per cent apart — so the camera believed the
        /// readout ended thirty-six pixels above where the readout drew itself,
        /// and every band in here was measured against a ruler the interface was
        /// not using. Two definitions of one number is the bug; this is the
        /// number, and the min is gone.
        /// </summary>
        public static float CanvasScale
        {
            get
            {
                float w = Screen.width, h = Screen.height;
                if (w <= 0f || h <= 0f) return 1f;
                // Unity's own: pow(w/rw, 1-m) * pow(h/rh, m)
                return Mathf.Pow(w / RefWidth, 1f - CanvasMatch) * Mathf.Pow(h / RefHeight, CanvasMatch);
            }
        }

        /// <summary>
        /// The glass: the safe area, less the overscan, less the housing. Every
        /// other rectangle in this file is cut out of this one, and it is exactly
        /// the rect the HUD's own root occupies — which is what lets the stroke
        /// and the camera be told about the window in the same sentence.
        /// </summary>
        public static Rect GlassRect()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f)
                safe = new Rect(0, 0, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

            int ov = OverscanPixels;
            float s = CanvasScale;
            float left = ChassisLeft * s + ov;
            float right = ChassisRight * s + ov;
            float top = ChassisTop * s + ov;
            float bottom = ChassisBottom * s + ov;

            return new Rect(safe.xMin + left, safe.yMin + bottom,
                            Mathf.Max(1f, safe.width - left - right),
                            Mathf.Max(1f, safe.height - top - bottom));
        }

        public static Rect ApertureRect()
        {
            Rect b = BoardRect();
            float s = CanvasScale;
            float ix = ApertureX * s, iy = ApertureY * s;
            return new Rect(b.xMin + ix, b.yMin + iy,
                            Mathf.Max(1f, b.width - ix * 2f),
                            Mathf.Max(1f, b.height - iy * 2f));
        }

        /// <summary>
        /// WHERE THE MACHINE GOES ON THE FRONT SCREEN — the gap between the
        /// masthead and the controls, which is a different rectangle from the
        /// play window and was being framed with the play window's centre.
        ///
        /// The attract cube was fitted to <see cref="BoardRect"/>, whose centre is
        /// pulled down by the HUD's two bands being different heights — bands that
        /// are not even drawn on this screen. So the one object the title is about
        /// sat seventy units low, crowding the primary and leaving a hole under
        /// the rule.
        /// </summary>
        /// <summary>
        /// How far the machine is allowed to stand BEHIND the controls.
        ///
        /// The four buttons and the primary are opaque plates — Palette.Panel is
        /// #0d1117 at full alpha — so a solid passing behind them is occluded
        /// rather than muddled, and the scrim over that band is opaque too, so it
        /// cannot peek through the gutters between them either. It already did
        /// this: framed into the play window at the old margin, the tumbling cube
        /// reached ninety units past the top of the control block on a 16:9 phone.
        /// That was an accident of two rectangles disagreeing. This is the same
        /// composition on purpose, and the same number, so the front screen does
        /// not get SMALLER on the phone it was composed against.
        /// </summary>
        public const float TitleBehind = 90f;

        public static Rect TitleRect()
        {
            Rect g = GlassRect();
            float s = CanvasScale;

            // The top is the masthead plus the scrim's ramp: the machine may not
            // stand where the type is, and it may not stand in the gradient that
            // gets the black off the type either — a cube under a ramp is the
            // smudge this whole change is about.
            float top = (TitleMastBottom + TitleScrimRamp) * s;
            float bottom = (TitleFootTop - TitleBehind) * s;

            // A SHORT DEVICE STILL GETS A BAND. The masthead and the controls are
            // fixed heights and the plate is not, so on the shortest phone this
            // has to answer is a sliver — and a sliver is still where the cube
            // goes. It is never allowed to invert, which would put the cube behind
            // the wordmark.
            float h = Mathf.Max(120f * s, g.height - top - bottom);
            float y = g.yMin + Mathf.Max(0f, g.height - top - h);
            return new Rect(g.xMin, y, g.width, h);
        }

        /// <summary>
        /// THE WINDOW, SAID IN THE UNITS THE HUD BUILDS IN — insets into the
        /// glass, so the stroke can be hung on the same rectangle the camera is
        /// framing against instead of on a second guess at it.
        ///
        /// The stroke used to be four constants: the two bands and the two
        /// aperture insets, written straight into the RectTransform. That was one
        /// definition of the window in Hud and another in Layout, and the README
        /// says why that is the one thing this rect must not have — the camera
        /// contains the solid inside ITS window, and anything it does not know
        /// about is a line the cube can cross.
        /// </summary>
        public static void ApertureInsets(out float left, out float bottom, out float right, out float top)
        {
            Rect g = GlassRect(), a = ApertureRect();
            float s = Mathf.Max(0.0001f, CanvasScale);
            left = (a.xMin - g.xMin) / s;
            bottom = (a.yMin - g.yMin) / s;
            right = (g.xMax - a.xMax) / s;
            top = (g.yMax - a.yMax) / s;
        }

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
            Rect g = GlassRect();
            float s = CanvasScale;

            // the two bands are the space the HUD occupies, measured from the
            // glass — see the note above this method
            float top = TopBand * s;
            float bottom = BottomBand * s;

            Rect free = new Rect(g.xMin, g.yMin + bottom,
                                 Mathf.Max(1f, g.width),
                                 Mathf.Max(1f, g.height - top - bottom));

            return Square(free);
        }

        /// <summary>
        /// THE WINDOW IS THE SHAPE OF THE THING IN IT.
        ///
        /// Everything above measures the space the board is ALLOWED, and that
        /// space is whatever the housing and the two bands leave — on a 20:9
        /// phone, 1250 by 2075. The board is a square fitted to the shorter of
        /// those two, so it came out 977 across and 977 tall: 78% of the window's
        /// width and 47% of its height, with five hundred pixels of nothing above
        /// it and five hundred below.
        ///
        /// That is not empty space, it is a FRAME DRAWN AROUND EMPTY SPACE, and
        /// the two read completely differently. The aperture is a stroke that says
        /// "this is the window"; a window twice as tall as its contents says the
        /// contents failed to load. Every screenshot of the play screen showed a
        /// small game inside a large case.
        ///
        /// So the window is cut to the square its contents are, about the same
        /// centre. THE BOARD DOES NOT MOVE AND DOES NOT CHANGE SIZE — Fit sizes
        /// the cube from the SHORTER side and offsets it by the rect's CENTRE, and
        /// this changes neither. The only thing that changes is where the stroke
        /// is drawn, and the four fold marks that hang on it, which come in from
        /// the edges of the display to the edges of the board where they belong.
        ///
        /// It also makes a fold cost the same in both axes. Room() contains the
        /// solid inside this rect, and a solid mid-fold is root two wide: against
        /// a window 1250 by 2075 a horizontal fold pulled the camera out 15% and a
        /// vertical fold pulled it out not at all, because the height was never
        /// the binding constraint. Against a square they are the same 15%. The
        /// gesture is symmetric, so the framing it provokes should be.
        /// </summary>
        static Rect Square(Rect r)
        {
            float side = Mathf.Max(1f, Mathf.Min(r.width, r.height));
            Vector2 c = r.center;
            return new Rect(c.x - side * 0.5f, c.y - side * 0.5f, side, side);
        }
    }
}
