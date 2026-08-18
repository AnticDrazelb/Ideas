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
        /// THE APERTURE'S OWN INSET — how far the stroke stands off whatever it is
        /// hung on, at the sides and at the ends.
        ///
        /// These two numbers were literals in Hud, where they drew a stroke, and
        /// the camera had never heard of them: it fitted the cube to the whole
        /// board rect, so the moment a fold turned the solid past square it grew
        /// wider than its own frame and crossed the line that is supposed to be
        /// the edge of the window. One definition, three consumers now — the
        /// stroke, the rect the camera contains the solid inside, and the four
        /// fold marks that hang on the stroke's inner edge.
        ///
        /// The vertical one is also the whole of the gap at the top and bottom of
        /// the play screen: ten units of case between the stroke and the rule that
        /// closes the readout, and ten between the stroke and the control band.
        /// See <see cref="Window"/>.
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

        public static Rect ApertureRect() => Inset(BoardRect());

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

            return Window(free);
        }

        /// <summary>
        /// THE WINDOW IS AS WIDE AS THE BOARD AND AS TALL AS THE BAND.
        ///
        /// Everything above measures the space the board is ALLOWED, and on a
        /// 20:9 phone that space is 1236 by 2008. The board is a square fitted to
        /// the shorter of those, so it is 1236 either way.
        ///
        /// The window used to be cut to that square, in both axes. That fixed a
        /// real thing — a frame twice as tall as its contents reads as contents
        /// that failed to load, not as air — and it paid for it with two bands of
        /// dead plate: about four hundred pixels under the rule that closes the
        /// readout, and four hundred more over the three controls, each a stretch
        /// of case with nothing in it and no edge to say why. The housing is a
        /// photograph of an instrument, and an instrument does not have
        /// unaccounted-for gaps between its window and its chrome.
        ///
        /// So the cut is in ONE axis now, not two: to the board across, and to
        /// the whole band down. The stroke's top lands <see cref="ApertureY"/>
        /// under the readout's rule and its bottom the same distance over the
        /// control band — the inset it already has at the sides — so the window
        /// MEETS the chrome instead of floating between two helpings of it.
        ///
        /// THE BOARD DOES NOT MOVE AND DOES NOT CHANGE SIZE. Fit sizes the cube
        /// from the SHORTER side of this rect and offsets it by the rect's CENTRE.
        /// The shorter side is still the width, and taking the height out to the
        /// full band about its own centre does not move the centre. The cube is
        /// the same cube in the same place; what changed is where the stroke is
        /// drawn and where the four fold marks hanging on it sit.
        ///
        /// AND THE FOLD STILL COSTS THE SAME IN BOTH AXES, because the camera is
        /// not contained by this rect — see <see cref="ContainRect"/>. That was
        /// the square's second argument and it is the one that had nothing to do
        /// with dead plate, so it is kept rather than spent.
        ///
        /// LANDSCAPE IS NOT A SPECIAL CASE, it is the same rule with the other
        /// answer. Turned, the bands bind the HEIGHT — 488 against 2756 of free
        /// width — so the shorter side is the height, the width is cut to it, and
        /// the window comes out the square it already was. The plate this gives
        /// back is only ever there when the long axis is the one the bands are
        /// measured on, which is the shape the game is held in.
        /// </summary>
        static Rect Window(Rect r)
        {
            float w = Mathf.Max(1f, Mathf.Min(r.width, r.height));
            float h = Mathf.Max(1f, r.height);
            Vector2 c = r.center;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// THE SQUARE THE BOARD ACTUALLY OCCUPIES, concentric with the window.
        ///
        /// The window is the stroke; this is the shape of the thing inside it. On
        /// a phone the two differ by the eight hundred pixels of height the window
        /// took back, and something still has to know the smaller number —
        /// CameraRig.Fit sizes the cube from the shorter side of the window, and
        /// this is the square that side describes.
        /// </summary>
        public static Rect BoardSquare()
        {
            Rect w = BoardRect();
            float side = Mathf.Max(1f, Mathf.Min(w.width, w.height));
            Vector2 c = w.center;
            return new Rect(c.x - side * 0.5f, c.y - side * 0.5f, side, side);
        }

        /// <summary>
        /// WHERE THE SOLID HAS TO STAY, WHICH IS NO LONGER THE WHOLE WINDOW.
        ///
        /// CameraRig.Room pulls the camera back until the leaned or mid-fold solid
        /// fits inside a rectangle. That rectangle was the aperture, and while the
        /// aperture was square the two questions had one answer. They do not any
        /// more: containing against the tall window would make a horizontal fold
        /// cost fifteen per cent and a vertical one cost nothing, because the
        /// height would never be the binding constraint again. A solid is as tall
        /// as it is wide and it turns both ways; the framing it provokes should
        /// not care which way.
        ///
        /// So the camera holds it inside the SQUARE, inset exactly as the stroke
        /// is inset, and the height the window gained is headroom rather than
        /// slack. This rect is the aperture the camera used to be given, to the
        /// pixel — nothing about how it moves has changed — and it is inside the
        /// stroke by construction, so containing against it still guarantees the
        /// thing the stroke exists to promise: the cube does not cross the line.
        /// </summary>
        public static Rect ContainRect() => Inset(BoardSquare());

        /// <summary>
        /// HOW MUCH ROOM THE CAMERA LEAVES ROUND THE CUBE, and it lives here
        /// rather than in CameraRig's signature because the HUD now has to know
        /// it. Fit sizes the solid so that n * this many world units span the
        /// shorter side of the window, which is the same as saying the drawn cube
        /// is 1/1.28 of that side and the rest is air.
        ///
        /// It was a default argument on Fit and nothing else could see it, which
        /// was fine while the only thing inside the window was the board. Four
        /// lines of type live in there now.
        /// </summary>
        public const float FitMargin = 1.28f;

        /// <summary>
        /// THE BAND BETWEEN THE STROKE AND THE BOARD, in canvas units, at each of
        /// the window's two ends.
        ///
        /// This is the space the window took back, and it is where the plate
        /// clock, the caption, the toast and the coach's line go — so it is worth
        /// a number rather than an eyeball. It is symmetric because Fit centres
        /// the cube on the rect's centre and this is cut about the same one.
        ///
        /// On a phone it is a hundred and seventy units and the stack is a
        /// hundred and twelve. On a 4:3 tablet the bands bind the HEIGHT, the
        /// window is square, and it is seventy — see LayoutChecks.Stack, which is
        /// where that difference is stated rather than discovered.
        /// </summary>
        public static float ClearBand()
        {
            Rect w = BoardRect();
            float shorter = Mathf.Max(1f, Mathf.Min(w.width, w.height));
            float drawn = shorter / FitMargin;
            return Mathf.Max(0f, (w.height - drawn) * 0.5f) / Mathf.Max(0.0001f, CanvasScale);
        }

        /// <summary>
        /// The stroke's own inset, applied to a rect. One definition, two callers
        /// — the window the player sees and the square the camera works against —
        /// so the two cannot disagree about how thick the frame is.
        /// </summary>
        static Rect Inset(Rect b)
        {
            float s = CanvasScale;
            float ix = ApertureX * s, iy = ApertureY * s;
            return new Rect(b.xMin + ix, b.yMin + iy,
                            Mathf.Max(1f, b.width - ix * 2f),
                            Mathf.Max(1f, b.height - iy * 2f));
        }
    }
}
