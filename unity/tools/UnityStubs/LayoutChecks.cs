using System;
using UnityEngine;
using Singularity.Game;

/// <summary>
/// THE WINDOW IS THE SHAPE OF THE THING IN IT, AND THAT IS A NUMBER.
///
/// The play screen frames the board in a stroke — the aperture — and the camera
/// contains the solid inside the same rectangle. Two things follow from that,
/// and neither of them is a taste:
///
///   1. The stroke and the camera must be told about ONE rectangle. Layout owns
///      it; the HUD asks for it in its own units. A private copy of the sum in
///      either place is a line the cube can cross with nothing to notice.
///   2. A frame twice as tall as its contents does not read as air, it reads as
///      contents that failed to load. On 1440x2960 the window was 1250 by 2075
///      around a board 977 square — 78% of the width and 47% of the height.
///
/// So the window is cut to the square its contents are. This asserts it on the
/// shapes real phones actually are, and prints the fill either way, because the
/// claim being made is a proportion and a proportion is checkable.
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
            Landscape(ok);
            Insets(ok);
            Peek(ok);
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
        float baseSize = n * 1.28f * Screen.height / (2f * shorter);
        float pxPerWorld = Screen.height / (2f * baseSize);
        float side = n * pxPerWorld;                 // the board, in pixels
        return (side / ap.width, side / ap.height);
    }

    static void Portrait(Device d, Action<bool, string> ok)
    {
        Use(d.w, d.h);
        Rect ap = Layout.ApertureRect();
        (float fx, float fy) = Fill();

        // The aperture is the board rect less ApertureX and ApertureY, which are
        // 16 and 10 — so it is six units off square by construction, and that is
        // the stroke's own inset rather than slack in the window.
        float off = Mathf.Abs(ap.width - ap.height);
        float tol = (Layout.ApertureX - Layout.ApertureY) * 2f * Layout.CanvasScale + 1f;
        ok(off <= tol,
           d.name + ": the window is " + ap.width.ToString("0") + " by " + ap.height.ToString("0")
                  + ", which is " + off.ToString("0") + " off square");

        // and the board covers the same proportion of both axes, because the
        // window is the shape the board is
        ok(Mathf.Abs(fx - fy) < 0.04f,
           d.name + ": the board fills " + (fx * 100f).ToString("0") + "% of the window across and "
                  + (fy * 100f).ToString("0") + "% down");

        // 1.28 of margin is the camera's, and it is the only thing between the
        // board and the stroke. Anything under two thirds is a small game in a
        // large case, which is what this check exists to stop coming back.
        ok(fx > 0.66f,
           d.name + ": the board covers only " + (fx * 100f).ToString("0") + "% of its own window");

        Console.WriteLine("layout: " + d.name + "  window " + ap.width.ToString("0") + "x" + ap.height.ToString("0")
                        + "  board fills " + (fx * 100f).ToString("0") + "% across, "
                        + (fy * 100f).ToString("0") + "% down");
    }

    /// <summary>
    /// WHAT THE SQUARE WINDOW COSTS THE MATRIX, WHICH IS A COST I INTRODUCED.
    ///
    /// Holding to peek leans the solid over, and CameraRig.Room pulls the camera
    /// back until the leaned solid fits the window. Against the old tall window
    /// the height was never the binding constraint, so a lean cost whatever the
    /// WIDTH demanded and nothing more. Against a square, the height binds too —
    /// so the board visibly shrinks further when the player holds, and that is a
    /// feel change rather than a bug.
    ///
    /// It is bounded rather than discovered later: a pull past about a third
    /// would read as the game backing away from the player at the moment they
    /// leaned in. The number is computed with Room's own arithmetic.
    /// </summary>
    static void Peek(Action<bool, string> ok)
    {
        Use(1440, 2960);
        Rect ap = Layout.ApertureRect();
        float screenH = Screen.height;
        const int n = 5;

        Rect board = Layout.BoardRect();
        float shorter = Mathf.Max(1f, Mathf.Min(board.width, board.height));
        float baseSize = n * 1.28f * screenH / (2f * shorter);

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

        float rest = Pull(ap, baseSize, 0f, 0f);
        float peek = Pull(ap, baseSize, CubeView.PeekYaw, CubeView.PeekPitch);
        float worst = Pull(ap, baseSize, CubeView.PeekYaw, CubeView.PeekMaxPitch);

        // AND THE SAME QUESTION OF THE WINDOW THIS REPLACED, because a number
        // with nothing beside it cannot tell you whether it is a regression.
        // The old window was the whole band between the two HUD bands, uncut —
        // 1250 by 2075 on this display — and the cube was sized from its shorter
        // side, which was the same width. So the only thing that changed for the
        // matrix is how much HEIGHT the frame has, and the honest check is
        // whether taking that height away costs the lean anything.
        Rect tall = new Rect(ap.xMin, ap.center.y - 2035f * 0.5f, ap.width, 2035f);
        float wasPeek = Pull(tall, baseSize, CubeView.PeekYaw, CubeView.PeekPitch);

        ok(rest <= 1.001f, "the board does not fit its own window at rest — the camera pulls out by "
                         + ((rest - 1f) * 100f).ToString("0") + "% before anything has moved");
        // THE LEAN COSTS WHAT IT ALWAYS COST. The width was already the binding
        // constraint at this pose, so squaring the window took away height the
        // lean was not using. If that ever stops being true this fails, and the
        // answer will be to give the window a little vertical slack rather than
        // to discover it on a phone.
        ok(peek <= wasPeek + 0.02f,
           "squaring the window made the matrix pull out " + ((peek - 1f) * 100f).ToString("0")
         + "% against the old window's " + ((wasPeek - 1f) * 100f).ToString("0")
         + "% — the frame is now taking height the lean needs");
        ok(worst <= 1.60f, "a full-pitch orbit pulls out " + ((worst - 1f) * 100f).ToString("0")
                         + "%, which is the game backing away as the player leans in");

        Console.WriteLine("layout: at rest the board fits its window exactly; matrix pulls out "
                        + ((peek - 1f) * 100f).ToString("0") + "% against the old window's "
                        + ((wasPeek - 1f) * 100f).ToString("0") + "%, and the furthest orbit "
                        + ((worst - 1f) * 100f).ToString("0") + "%");
    }

    /// <summary>The same device on its side: the square is cut out of the height instead.</summary>
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
