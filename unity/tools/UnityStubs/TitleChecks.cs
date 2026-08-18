using System;
using UnityEngine;
using Singularity.Game;
using Singularity.UI;

/// <summary>
/// THE FRONT SCREEN'S ONE OBJECT, AND WHETHER IT FITS THE HOLE IT IS PUT IN.
///
/// The title reserves a masthead at the top and a block of controls at the
/// floor; the gap between them is where the machine is drawn, and it is about a
/// third of the plate. Three separate things have to agree about that gap — the
/// screen that draws the masthead, the scrim that has to be clear across it, and
/// the camera that frames the cube into it — and before this they were three
/// different opinions: two literals in Screens, a gradient tuned by eye against
/// a paragraph that has since been deleted, and a camera framing into the HUD's
/// window on a screen where the HUD is not drawn.
///
/// So the gap is Layout's, and this asserts what the camera does with it, using
/// the camera's OWN containment arithmetic — the same sum of the rotated basis
/// that CameraRig.Room computes — over the entire drift cycle rather than at the
/// one angle somebody happened to look at.
/// </summary>
static class TitleChecks
{
    /// <summary>Half the solid, plus the hair CameraRig.Room adds. Its number, not a copy of it.</summary>
    const float Skin = 1.04f;

    public static void Run(Action<bool, string> ok)
    {
        int savedW = Screen.width, savedH = Screen.height;
        Rect savedSafe = Screen.safeArea;

        try
        {
            Band(ok, 1080, 1920, "16:9");
            Band(ok, 1440, 2960, "20:9");
            Band(ok, 1080, 2520, "21:9");
            Swing(ok);
            Scrim(ok);
        }
        finally
        {
            Screen.width = savedW; Screen.height = savedH; Screen.safeArea = savedSafe;
        }
    }

    static void Use(int w, int h)
    {
        Screen.width = w; Screen.height = h;
        Screen.safeArea = new Rect(0, 0, w, h);
    }

    /// <summary>
    /// The band is between the two things it is between, it is not upside down,
    /// and its centre is where the cube will be drawn — which is NOT the play
    /// window's centre, and that difference is the bug this replaced.
    /// </summary>
    static void Band(Action<bool, string> ok, int w, int h, string name)
    {
        Use(w, h);
        Rect g = Layout.GlassRect(), t = Layout.TitleRect(), b = Layout.BoardRect();
        float s = Layout.CanvasScale;

        ok(t.height > 1f, name + ": the title band has no height");
        ok(t.yMax <= g.yMax - Layout.TitleMastBottom * s + 0.5f,
           name + ": the title band reaches up into the masthead");
        // it MAY stand behind the control block, by exactly the allowance — see
        // Layout.TitleBehind — and not one unit further
        ok(t.yMin >= g.yMin + (Layout.TitleFootTop - Layout.TitleBehind) * s - 0.5f || t.height <= 120f * s + 0.5f,
           name + ": the title band reaches further behind the controls than the allowance");

        float drop = (b.center.y - t.center.y) / s;
        Console.WriteLine("title: " + name + "  band " + t.width.ToString("0") + "x" + t.height.ToString("0")
                        + "  and the play window's centre is " + drop.ToString("0")
                        + " units below it — the cube used to be framed there");
    }

    /// <summary>
    /// THE SCRIM IS CLEAR WHERE THE MACHINE IS, AND BLACK WHERE THE TYPE IS.
    ///
    /// Those are the only two things it is for, and it was answering neither: the
    /// clear stripe was a tenth of the plate tall — its stops tuned against a
    /// paragraph of pitch that has since been deleted — while the cube drawn under
    /// it was a fifth of the plate. That is why the machine reads as a smudge on
    /// the front screen. It is not dark; it is under a gradient shaped for
    /// something that is not there.
    ///
    /// Measured on the plate the scrim is anchored to, on every phone shape,
    /// because the two pieces are anchored in UNITS and the plate is not the same
    /// height on any two of them.
    /// </summary>
    static void Scrim(Action<bool, string> ok)
    {
        foreach (var d in new[] { (1080, 1920, "16:9"), (1440, 2960, "20:9"), (1080, 2520, "21:9") })
        {
            Use(d.Item1, d.Item2);
            float s = Layout.CanvasScale;
            Rect g = Layout.GlassRect(), band = Layout.TitleRect();
            float H = g.height / s;                       // the live plate, in units

            float Alpha(float down) => Mathf.Max(UiKit.StageTopAlpha(down), UiKit.StageBottomAlpha(H - down));

            // OVER THE TYPE. The wordmark, the vault line and the cube name all
            // sit above the rule that closes the masthead.
            for (float y = 40f; y <= UiKit.StageTypeBottom; y += 12f)
                ok(Alpha(y) > 0.60f,
                   d.Item3 + ": the masthead is only scrimmed to " + Alpha(y).ToString("0.00") + " at "
                 + y.ToString("0") + " units down — type over a turning cube is type nobody can read");

            // OVER THE CONTROLS, including the gutters between them, so the cube
            // standing behind the block cannot show through the gaps.
            for (float y = H - Layout.TitleFootTop + 20f; y <= H - 20f; y += 12f)
                ok(Alpha(y) > 0.60f,
                   d.Item3 + ": the controls are only scrimmed to " + Alpha(y).ToString("0.00")
                 + " at " + y.ToString("0") + " units down");

            // AND CLEAR ACROSS THE MACHINE. The cube is framed into the band, and
            // the band starts below the ramp — so everything of it that is not
            // deliberately behind the control block must be in the clear.
            float top = (g.yMax - band.yMax) / s;
            // everything of the machine that is above the point the control
            // block's scrim starts coming on — below that it is behind opaque
            // plates on purpose
            float visible = H - (Layout.TitleFootTop + Layout.TitleScrimRamp);

            float worst = 0f;
            for (float y = top; y <= visible; y += 2f) worst = Mathf.Max(worst, Alpha(y));
            ok(worst < 0.02f, d.Item3 + ": the machine sits under " + worst.ToString("0.00")
                            + " of black — the scrim's clear window does not contain its band");

            int clear = 0, of = 0;
            for (float y = 0; y < H; y += 1f) { of++; if (Alpha(y) < 0.02f) clear++; }
            Console.WriteLine("title: " + d.Item3 + " the scrim is clear across "
                            + (100f * clear / of).ToString("0") + "% of the plate and over 0.60 across all the type");
        }
    }

    /// <summary>
    /// WALK THE WHOLE CYCLE. The pose is a pure function of time, so there is no
    /// excuse for checking it at one angle: the yaw and the pitch beat at
    /// different rates, so the widest silhouette is somewhere neither of them is
    /// at its own extreme, and that is exactly the frame a comment gets wrong.
    /// </summary>
    static void Swing(Action<bool, string> ok)
    {
        Use(1440, 2960);
        Rect band = Layout.TitleRect();
        float screenH = Mathf.Max(1f, Screen.height);

        const int n = 5;
        float shorter = Mathf.Max(1f, Mathf.Min(band.width, band.height));
        float baseSize = n * GameDirector.AttractMargin * screenH / (2f * shorter);

        float worstX = 0f, worstY = 0f, worstAt = 0f, worstFill = 0f;

        // a full beat of the slower of the two, sampled finely enough that a
        // corner cannot slip between two samples
        float cycle = 1f / Mathf.Min(CubeView.AttractYawHz, CubeView.AttractPitchHz);
        for (int i = 0; i <= 4000; i++)
        {
            float t = cycle * i / 4000f;
            Quaternion r = CubeView.AttractPose(t);
            Vector3 rx = r * Vector3.right, ry = r * Vector3.up, rz = r * Vector3.forward;

            float e = n * 0.5f * Skin;
            float ex = e * (Mathf.Abs(rx.x) + Mathf.Abs(ry.x) + Mathf.Abs(rz.x));
            float ey = e * (Mathf.Abs(rx.y) + Mathf.Abs(ry.y) + Mathf.Abs(rz.y));

            // the same conversion Room does: orthographicSize is half the world
            // height of the WHOLE display, so a rect only sees its own fraction
            float fx = ex * screenH / (baseSize * band.width);
            float fy = ey * screenH / (baseSize * band.height);

            if (fx > worstX) { worstX = fx; }
            if (fy > worstY) { worstY = fy; worstAt = t; }
            float fill = Mathf.Max(fx, fy);
            if (fill > worstFill) worstFill = fill;
        }

        Console.WriteLine("title: band " + band.width.ToString("0") + "x" + band.height.ToString("0")
                        + "  worstX " + worstX.ToString("0.000") + "  worstY " + worstY.ToString("0.000"));
        ok(worstX <= 1f, "the attract cube is " + ((worstX - 1f) * 100f).ToString("0.0")
                       + "% wider than the band it is framed into at its worst angle");
        ok(worstY <= 1f, "the attract cube is " + ((worstY - 1f) * 100f).ToString("0.0")
                       + "% taller than the band it is framed into at its worst angle");

        // AND IT HAS TO ACTUALLY FILL IT. A cube that never touches its band is
        // the bug this was written for — the old margin left it at little more
        // than half — so the check has a floor as well as a ceiling.
        ok(worstFill > 0.86f, "the attract cube only ever reaches "
                            + (worstFill * 100f).ToString("0") + "% of its band");

        // the old framing, for the record: the play window, and enough margin for
        // a solid that turns through every angle
        Rect play = Layout.BoardRect();
        float wasShorter = Mathf.Max(1f, Mathf.Min(play.width, play.height));
        float wasSize = n * 2.30f * screenH / (2f * wasShorter);
        float was = n * screenH / (2f * wasSize);
        float now = n * screenH / (2f * baseSize);

        Console.WriteLine("title: the attract cube reaches " + (worstFill * 100f).ToString("0")
                        + "% of its band at its widest (t=" + worstAt.ToString("0.0") + "s), and is "
                        + ((now / was - 1f) * 100f).ToString("0") + "% larger than the tumbling one it replaced — "
                        + was.ToString("0") + "px to " + now.ToString("0") + "px");
    }
}
