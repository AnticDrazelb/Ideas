using System;
using UnityEngine;
using Singularity.Game;
using Singularity.UI;

/// <summary>
/// THE WINDOW MEETS THE CHROME, AND THAT IS A NUMBER.
///
/// The play screen frames the board in a stroke — the aperture — and the camera
/// contains the solid inside a rectangle. Three things follow, and none of them
/// is a taste:
///
///   1. The stroke and the camera must be told about rectangles that come from
///      ONE definition. Layout owns them; the HUD asks for its own in canvas
///      units. A private copy of the sum in either place is a line the cube can
///      cross with nothing to notice.
///   2. A frame twice as tall as its contents does not read as air, it reads as
///      contents that failed to load. On 1440x2960 the window was 1250 by 2075
///      around a board 977 square — 78% of the width and 47% of the height.
///   3. Cutting it to that square answered 2 and bought two bands of dead plate
///      instead: four hundred pixels of case under the readout's rule and four
///      hundred over the controls, with nothing in either. So the cut is in one
///      axis now — to the board across, to the whole band down — and what has to
///      be asserted is that the stroke actually REACHES the chrome at both ends
///      rather than landing near it.
///
/// This asserts all of that on the shapes real phones are, and prints the fill
/// either way, because the claim being made is a proportion and a proportion is
/// checkable.
/// </summary>
static class LayoutChecks
{
    struct Device
    {
        public string name;
        public int w, h;
        public Device(string n, int w, int h) { name = n; this.w = w; this.h = h; }
    }

    static readonly Device[] Phones =
    {
        new Device("16:9   1080x1920", 1080, 1920),
        new Device("18:9   1080x2160", 1080, 2160),
        new Device("19.5:9 1170x2532", 1170, 2532),
        new Device("20:9   1440x2960", 1440, 2960),
        new Device("21:9   1080x2520", 1080, 2520),
        new Device("4:3    1536x2048", 1536, 2048),
    };

    public static void Run(Action<bool, string> ok)
    {
        int savedW = Screen.width, savedH = Screen.height;
        Rect savedSafe = Screen.safeArea;

        try
        {
            foreach (Device d in Phones) Portrait(d, ok);
            FoldMarks(ok);
            Stack(ok);
            Landscape(ok);
            Insets(ok);
            Peek(ok);
            BackButton(ok);
        }
        finally
        {
            Screen.width = savedW;
            Screen.height = savedH;
            Screen.safeArea = savedSafe;
        }
    }

    static void Use(int w, int h)
    {
        Screen.width = w;
        Screen.height = h;
        Screen.safeArea = new Rect(0, 0, w, h);
    }

    /// <summary>How much of the window the board itself covers, on each axis.</summary>
    static (float x, float y) Fill(int n = 5)
    {
        Rect ap = Layout.ApertureRect();
        // Fit's own arithmetic: the cube is sized so that n*margin world units
        // span the SHORTER side of the board rect, and orthographicSize is half
        // the world height of the whole display.
        Rect board = Layout.BoardRect();
        float shorter = Mathf.Max(1f, Mathf.Min(board.width, board.height));
        float baseSize = n * Layout.FitMargin * Screen.height / (2f * shorter);
        float pxPerWorld = Screen.height / (2f * baseSize);
        float side = n * pxPerWorld;                 // the board, in pixels
        return (side / ap.width, side / ap.height);
    }

    static void Portrait(Device d, Action<bool, string> ok)
    {
        Use(d.w, d.h);
        Rect g = Layout.GlassRect(), ap = Layout.ApertureRect();
        float s = Layout.CanvasScale;
        (float fx, float fy) = Fill();

        // THE STROKE REACHES THE CHROME AT BOTH ENDS, by exactly its own inset:
        // ApertureY under the rule that closes the readout, ApertureY over the
        // band the three controls live in. This is the check that fails the
        // moment somebody reserves height here and does not give it to anything.
        float wantTop = g.yMax - (Layout.TopBand + Layout.ApertureY) * s;
        float wantBot = g.yMin + (Layout.BottomBand + Layout.ApertureY) * s;
        ok(Mathf.Abs(ap.yMax - wantTop) < 1f,
           d.name + ": the window stops " + ((wantTop - ap.yMax) / s).ToString("0")
                  + " units short of the readout's rule");
        ok(Mathf.Abs(ap.yMin - wantBot) < 1f,
           d.name + ": the window stops " + ((ap.yMin - wantBot) / s).ToString("0")
                  + " units short of the control band");

        // AND IT IS NEVER WIDER THAN THE BOARD. The cut that is still made is the
        // horizontal one: dead plate beside the cube is the same fault as dead
        // plate above it, and on a 4:3 tablet it is the one that binds.
        ok(ap.width <= ap.height + 1f,
           d.name + ": the window is " + ap.width.ToString("0") + " across against "
                  + ap.height.ToString("0") + " down — wider than the board it frames");

        // 1.28 of margin is the camera's, and it is the only thing between the
        // board and the stroke ACROSS, which is the axis the cube is sized from.
        // Anything under two thirds is a small game in a large case, which is what
        // this check exists to stop coming back.
        ok(fx > 0.66f,
           d.name + ": the board covers only " + (fx * 100f).ToString("0") + "% of its own window");

        // and the square the camera works against is inside the stroke, which is
        // the promise the stroke makes
        Rect hold = Layout.ContainRect();
        ok(hold.xMin >= ap.xMin - 0.5f && hold.xMax <= ap.xMax + 0.5f
        && hold.yMin >= ap.yMin - 0.5f && hold.yMax <= ap.yMax + 0.5f,
           d.name + ": the rect the camera contains the solid inside reaches outside the stroke");

        Console.WriteLine("layout: " + d.name + "  window " + ap.width.ToString("0") + "x" + ap.height.ToString("0")
                        + "  board fills " + (fx * 100f).ToString("0") + "% across, "
                        + (fy * 100f).ToString("0") + "% down");
    }

    /// <summary>
    /// FOUR LINES OF TYPE MOVED INSIDE THE WINDOW, AND SOMETHING HAS TO SAY THEY
    /// FIT.
    ///
    /// The plate clock, the sound caption, the toast and the coach's line all
    /// used to live in the dead plate outside the aperture. There is none, so
    /// they hang on the stroke's inner edge instead, past the fold marks, in the
    /// band between the stroke and the board — and that band is not the same size
    /// on every shape, so "there is room" is a claim rather than an observation.
    ///
    /// WHERE THE WINDOW IS TALLER THAN THE BOARD the band is real and the stack
    /// has to fit inside it. That is every portrait phone: the housing binds the
    /// width, the bands bind the height, and the difference is the room.
    ///
    /// WHERE IT IS SQUARE there is no band worth the name — a 4:3 tablet, or the
    /// same phone turned on its side — because the height is what bound the board
    /// in the first place. The lines share the window with the board there, which
    /// is what they did before this: the old toast sat across the down fold's
    /// arrow and the old plate clock straddled the stroke on exactly that shape.
    /// What is still asserted is that they clear the marks and stay inside the
    /// window, and the clearance is printed either way.
    /// </summary>
    static void Stack(Action<bool, string> ok)
    {
        // the two stacks, measured inward from the stroke
        float up = Hud.CaptionTop + Hud.CaptionHeight;
        float down = Hud.ToastFoot + 34f;          // one line of the toast, set at 26

        foreach (Device d in Phones)
        {
            Use(d.w, d.h);
            float clear = Layout.ClearBand();
            Rect w = Layout.BoardRect();
            bool band = w.height > w.width + 1f;
            float s = Layout.CanvasScale;
            float win = Layout.ApertureRect().height / s;

            if (band)
            {
                ok(up <= clear, d.name + ": the caption reaches " + (up - clear).ToString("0")
                              + " units past the top of the board");
                ok(down <= clear, d.name + ": the toast reaches " + (down - clear).ToString("0")
                                + " units past the bottom of the board");
            }

            // and on every shape: past the marks, and the two ends do not meet
            ok(Coach.LineFoot >= Hud.FoldEdge + Hud.FoldIcon * 0.5f,
               d.name + ": the coach's line is drawn through the down fold's arrow");
            ok(up + down <= win, d.name + ": the two stacks overlap in the middle of a "
                               + win.ToString("0") + "-unit window");

            Console.WriteLine("layout: " + d.name + "  band " + clear.ToString("0")
                            + " units clear of the board, stack " + up.ToString("0") + " up / "
                            + down.ToString("0") + " down" + (band ? "" : "  (square window — no band)"));
        }
    }

    /// <summary>
    /// THE FOUR MARKS ARE ON THE FRAME, NOT BESIDE IT.
    ///
    /// The fold arrows hang on the aperture's inner edge, and they used to hang
    /// half a tap target in from it — because the mark and the pressable plate
    /// were one position and the plate is the thing that has to fit. Twenty-nine
    /// clear units inside a stroke is not a mark on the frame, it is four things
    /// floating in the window.
    ///
    /// These are units rather than pixels, so the answer is the same on every
    /// display and the check is asked once.
    /// </summary>
    static void FoldMarks(Action<bool, string> ok)
    {
        float clear = Hud.FoldEdge - Hud.FoldIcon * 0.5f;
        ok(clear > 0f, "the fold arrow overlaps the stroke by " + (-clear).ToString("0") + " units");
        ok(clear <= 12f, "the fold arrow stands " + clear.ToString("0")
                       + " units off the stroke, which is beside the frame rather than on it");

        // the hit plate is a separate number and still fits inside the housing
        ok(Access.TapTarget * 0.5f >= Hud.FoldEdge,
           "the assisted fold's plate is seated further out than the arrow it draws");

        Console.WriteLine("layout: the fold arrows sit " + clear.ToString("0")
                        + " units inside the stroke, on an " + Access.TapTarget.ToString("0")
                        + "-unit plate seated " + (Access.TapTarget * 0.5f).ToString("0") + " in");
    }

    /// <summary>
    /// THE BACK GESTURE REACHES EVERY SCREEN.
    ///
    /// Android's back arrives as KeyCode.Escape, and the one line reading it
    /// opened the pause card while playing and did nothing anywhere else — so on
    /// the manual, the vault rack, calibrate, access, the Forge and its editor a
    /// back swipe got no response at all. That does not read as "not a control
    /// here"; it reads as the app having hung, and it is the first thing an
    /// Android reviewer tries.
    ///
    /// Screens.DecideBack is a pure function of what is on top of the stack, so
    /// every screen's answer is assertable without building a canvas.
    /// </summary>
    static void BackButton(Action<bool, string> ok)
    {
        // nothing up: the caller opens the pause card, and this must not claim it
        ok(Screens.DecideBack(null, false) == Screens.BackAct.None,
           "back did something with an empty stack");

        // every ordinary screen pops
        foreach (string id in new[] { "manual", "vaults", "calibrate", "access", "pause", "plate", "forge" })
            ok(Screens.DecideBack(id, false) == Screens.BackAct.Pop,
               "back on the " + id + " screen does not go back");

        // a finished cube has nowhere to go back TO — popping it drops the player
        // onto the board they just collapsed, with nothing left to do on it
        ok(Screens.DecideBack("win", false) == Screens.BackAct.Menu,
           "back on the win card pops to the finished board");

        // and the root asks before it takes you at your word
        ok(Screens.DecideBack("title", false) == Screens.BackAct.Ask,
           "back on the title leaves the game without asking");
        ok(Screens.DecideBack("title", true) == Screens.BackAct.Leave,
           "a second back press on the title does not leave");

        ok(Screens.LeaveWindow >= 1.2f && Screens.LeaveWindow <= 4f,
           "the window to confirm leaving is " + Screens.LeaveWindow + "s, which is not a human pause");

        Console.WriteLine("layout: back pops every screen, goes to the front from a win card, and asks once "
                        + "on the title before leaving");
    }

    /// <summary>
    /// WHAT THE SQUARE COSTS THE MATRIX, WHICH IS A COST THE STROKE NO LONGER
    /// SHARES.
    ///
    /// Holding to peek leans the solid over, and CameraRig.Room pulls the camera
    /// back until the leaned solid fits. Against the old TALL window the height
    /// was never the binding constraint, so a lean cost whatever the WIDTH
    /// demanded and nothing more; against a square, the height binds too, and the
    /// board visibly shrinks further when the player holds.
    ///
    /// The window is tall again, and the camera is NOT contained by it — it is
    /// contained by Layout.ContainRect, the square inside it — precisely so that
    /// a fold keeps costing the same in both axes. So this asks its question of
    /// that rect, and the number it prints has to be the same one the square
    /// window produced, because it is the same rectangle.
    ///
    /// It is bounded rather than discovered later: a pull past about a third
    /// would read as the game backing away from the player at the moment they
    /// leaned in. The number is computed with Room's own arithmetic.
    /// </summary>
    static void Peek(Action<bool, string> ok)
    {
        Use(1440, 2960);
        Rect hold = Layout.ContainRect();
        float screenH = Screen.height;
        const int n = 5;

        Rect board = Layout.BoardRect();
        float shorter = Mathf.Max(1f, Mathf.Min(board.width, board.height));
        float baseSize = n * Layout.FitMargin * screenH / (2f * shorter);

        float Pull(Rect win, float size, float yaw, float pitch)
        {
            Quaternion r = Quaternion.AngleAxis(-yaw * Mathf.Rad2Deg, Vector3.up)
                         * Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right);
            Vector3 rx = r * Vector3.right, ry = r * Vector3.up, rz = r * Vector3.forward;
            float e = n * 0.5f * 1.04f;
            float ex = e * (Mathf.Abs(rx.x) + Mathf.Abs(ry.x) + Mathf.Abs(rz.x));
            float ey = e * (Mathf.Abs(rx.y) + Mathf.Abs(ry.y) + Mathf.Abs(rz.y));
            float need = Mathf.Max(ex * screenH / win.width, ey * screenH / win.height);
            return Mathf.Max(1f, need / size);
        }

        float rest = Pull(hold, baseSize, 0f, 0f);
        float peek = Pull(hold, baseSize, CubeView.PeekYaw, CubeView.PeekPitch);
        float worst = Pull(hold, baseSize, CubeView.PeekYaw, CubeView.PeekMaxPitch);

        // AND THE SAME QUESTION OF THE UNCUT BAND, because a number with nothing
        // beside it cannot tell you whether it is a regression. That band — the
        // whole space between the two HUD bands, 2075 on this display — is what
        // the STROKE is drawn around now, and the honest check is whether holding
        // the camera to the square inside it costs the lean anything.
        Rect tall = new Rect(hold.xMin, hold.center.y - 2035f * 0.5f, hold.width, 2035f);
        float wasPeek = Pull(tall, baseSize, CubeView.PeekYaw, CubeView.PeekPitch);

        ok(rest <= 1.001f, "the board does not fit its own window at rest — the camera pulls out by "
                         + ((rest - 1f) * 100f).ToString("0") + "% before anything has moved");
        // THE LEAN COSTS WHAT IT ALWAYS COST. The width was already the binding
        // constraint at this pose, so holding to the square takes away height the
        // lean was not using. If that ever stops being true this fails, and the
        // answer will be to give the containment a little vertical slack rather
        // than to discover it on a phone.
        ok(peek <= wasPeek + 0.02f,
           "containing to the square makes the matrix pull out " + ((peek - 1f) * 100f).ToString("0")
         + "% against the open band's " + ((wasPeek - 1f) * 100f).ToString("0")
         + "% — the square is now taking height the lean needs");
        ok(worst <= 1.60f, "a full-pitch orbit pulls out " + ((worst - 1f) * 100f).ToString("0")
                         + "%, which is the game backing away as the player leans in");

        // and the stroke is not what does the containing — that is the whole
        // point of there being two rects, and a regression here is silent
        ok(Layout.ApertureRect().height > hold.height + 1f,
           "the window and the rect the camera holds the solid inside are the same shape again");

        Console.WriteLine("layout: at rest the board fits its window exactly; matrix pulls out "
                        + ((peek - 1f) * 100f).ToString("0") + "% against the open band's "
                        + ((wasPeek - 1f) * 100f).ToString("0") + "%, and the furthest orbit "
                        + ((worst - 1f) * 100f).ToString("0") + "%");
    }

    /// <summary>
    /// The same device on its side, where the rule gives the other answer: the
    /// bands bind the HEIGHT, so the height is already the shorter side, the
    /// width is cut to it and the window comes out the square it always was.
    /// </summary>
    static void Landscape(Action<bool, string> ok)
    {
        Use(2960, 1440);
        Rect ap = Layout.ApertureRect();
        ok(ap.width > 1f && ap.height > 1f, "landscape: the window collapsed to nothing");
        float off = Mathf.Abs(ap.width - ap.height);
        float tol = (Layout.ApertureX - Layout.ApertureY) * 2f * Layout.CanvasScale + 1f;
        ok(off <= tol, "landscape: the window is " + off.ToString("0") + " off square");
        Console.WriteLine("layout: turned 2960x1440  window " + ap.width.ToString("0") + "x" + ap.height.ToString("0"));
    }

    /// <summary>
    /// The insets the HUD builds its stroke from describe the SAME rectangle the
    /// camera frames against. This is the check that would have failed while the
    /// two of them each did their own arithmetic.
    /// </summary>
    static void Insets(Action<bool, string> ok)
    {
        Use(1440, 2960);
        Rect g = Layout.GlassRect(), ap = Layout.ApertureRect();
        Layout.ApertureInsets(out float l, out float b, out float r, out float t);
        float s = Layout.CanvasScale;

        float x0 = g.xMin + l * s, y0 = g.yMin + b * s;
        float x1 = g.xMax - r * s, y1 = g.yMax - t * s;

        ok(Mathf.Abs(x0 - ap.xMin) < 0.5f && Mathf.Abs(y0 - ap.yMin) < 0.5f
        && Mathf.Abs(x1 - ap.xMax) < 0.5f && Mathf.Abs(y1 - ap.yMax) < 0.5f,
           "the insets the HUD draws from are not the rect the camera frames against");

        // and the scale is the canvas's own, not the min of the two ratios — the
        // seven per cent that used to separate them on a tall phone
        float mine = Mathf.Sqrt((1440f / Layout.RefWidth) * (2960f / Layout.RefHeight));
        ok(Mathf.Abs(Layout.CanvasScale - mine) < 0.0001f, "CanvasScale is not the scaler's own factor");

        Console.WriteLine("layout: the stroke and the camera agree on the window to half a pixel, at "
                        + Layout.CanvasScale.ToString("0.000") + " units per pixel");
    }
}
