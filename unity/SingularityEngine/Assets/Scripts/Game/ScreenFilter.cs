using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// The brightness and contrast controls, as the topmost layer of the screen.
    ///
    /// The web original does this with a CSS filter over the whole document, which
    /// is free because the compositor was going to composite anyway. Unity has no
    /// equivalent, and the obvious substitute — a blit on the camera — IS WHAT
    /// THIS USED TO BE AND IT DID NOT WORK. Every canvas in this game is a
    /// ScreenSpaceOverlay, an overlay canvas is composited after every camera has
    /// finished, and OnRenderImage never sees it. So the grade reached the cube
    /// and nothing else: the housing, the glass, the HUD and every screen came
    /// through untouched, and on CALIBRATE itself — all interface, no board —
    /// both sliders moved a number and changed nothing on the display.
    ///
    /// It has to catch the board AND the interface. A player raising brightness to
    /// read the board in sunlight has not asked for the HUD to stay dim, and a
    /// player who cannot see the difference while the slider is under their thumb
    /// has been told the control is broken.
    ///
    /// So it is two quads on a canvas above everything, and the arithmetic is in
    /// the BLENDER rather than in a shader — see Grade.shader for why that is
    /// exact rather than an approximation. The important property survives intact:
    /// AT 100/100 NOTHING HAPPENS. Both quads switch off and the cost of these two
    /// settings is zero for everyone who leaves them alone.
    /// </summary>
    public class ScreenFilter : MonoBehaviour
    {
        // UnityEngine.Rendering.BlendMode / BlendOp, as the numbers ShaderLab wants.
        const float Zero = 0f, One = 1f, DstColor = 2f;
        const float OpAdd = 0f, OpRevSub = 2f;

        /// <summary>
        /// Above the pane at 25, because the grade is the last thing that happens
        /// to the picture and the glass is part of the picture.
        /// </summary>
        const int Order = 100;

        /// <summary>
        /// Three is the worst case and it is reached: a subtract for the contrast
        /// pivot, then two multiplies, because one blend can only express a gain
        /// of two (dst*(1+src), and src cannot exceed one) while brightness and
        /// contrast at their maxima together ask for 2.4.
        /// </summary>
        const int Quads = 3;

        readonly Image[] _quad = new Image[Quads];
        readonly Material[] _mat = new Material[Quads];
        int _brightWas = int.MinValue, _contrastWas = int.MinValue;

        /// <summary>
        /// FULL BLEED, and for the same reason the housing is: the metal in the
        /// cutout band is part of the picture too, and a grade that stopped at the
        /// safe area would put a visibly brighter stripe across the top of the
        /// case the moment anybody touched the slider.
        /// </summary>
        public static ScreenFilter Attach()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Grade", Order, safe: false);
            var f = c.gameObject.AddComponent<ScreenFilter>();
            f.Build(c);
            f.Refresh();
            return f;
        }

        void Build(Canvas c)
        {
            Shader sh = Shader.Find("Singularity/Grade");
            if (sh == null) { Debug.LogError("Singularity/Grade shader missing"); return; }

            // ORDER IS SIBLING ORDER, and which operation lands in which slot is
            // decided per setting rather than fixed — see Refresh.
            for (int i = 0; i < Quads; i++)
            {
                _mat[i] = new Material(sh) { name = "grade" + i, hideFlags = HideFlags.HideAndDontSave };
                _quad[i] = Quad(c, "grade" + i, _mat[i]);
                _quad[i].gameObject.SetActive(false);
            }
        }

        static Image Quad(Canvas c, string name, Material m)
        {
            RectTransform rt = Singularity.UI.UiKit.Rect(c.transform, name,
                                                         Vector2.zero, Vector2.one,
                                                         Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.material = m;
            img.raycastTarget = false;   // it is over every button in the game
            return img;
        }

        /// <summary>Read the settings back out of the store; call after Calibrate changes one.</summary>
        public void Refresh()
        {
            int b = Store.Data.bright, c = Store.Data.contrast;
            if (b == _brightWas && c == _contrastWas) return;
            _brightWas = b; _contrastWas = c;
            if (_quad[0] == null) return;

            float bright = b / 100f, contrast = c / 100f;

            // out = in * m + a, which is the whole of what these two controls mean.
            float m = bright * contrast;
            float a = 0.5f * (1f - contrast);

            // WHICH WAY ROUND IS NOT A STYLE CHOICE, IT IS THE ONLY SOURCE OF
            // ERROR IN THIS WHOLE APPROACH.
            //
            // Each quad lands in an eight-bit framebuffer, so whatever the one
            // before it produced was clamped to 0..1 before this one reads it.
            // Multiplying UP first and subtracting after loses everything the gain
            // pushed past white; subtracting first and multiplying up after does
            // not. The mirror is true at the other end. So the sign of the offset
            // picks the order, and the pivot is pre-divided by the gain when it
            // goes first so the arithmetic still comes out at in*m + a.
            //
            // Checked against the affine it replaces across every setting the two
            // sliders can reach, at every input level: worst deviation 0.0017,
            // which is under one eight-bit step, and exactly zero at 100/100.
            int n = 0;
            if (a < 0f)
                n = Op(n, One, One, OpRevSub, -a / Mathf.Max(m, 0.002f));

            float mm = m;
            while (mm > 2.0001f) { n = Op(n, DstColor, One, OpAdd, 1f); mm *= 0.5f; }
            if (Mathf.Abs(mm - 1f) >= 0.002f)
                n = mm <= 1f ? Op(n, DstColor, Zero, OpAdd, mm)
                             : Op(n, DstColor, One, OpAdd, mm - 1f);

            if (a >= 0.002f)
                n = Op(n, One, One, OpAdd, a);

            // AT 100/100 THIS LEAVES EVERY QUAD OFF, which is the rule the blit it
            // replaces followed and the reason these two settings cost nothing at
            // all to anybody who never opens the screen.
            for (int i = n; i < Quads; i++) _quad[i].gameObject.SetActive(false);
        }

        /// <summary>One blend, in the next free slot. Returns the slot after it.</summary>
        int Op(int i, float src, float dst, float op, float v)
        {
            if (i >= Quads) return i;
            Blend(_mat[i], src, dst, op);
            _quad[i].color = Grey(Mathf.Clamp01(v));
            _quad[i].gameObject.SetActive(true);
            return i + 1;
        }

        static void Blend(Material m, float src, float dst, float op)
        {
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
            m.SetFloat("_Op", op);
        }

        /// <summary>The coefficient, as a colour. Alpha is never a factor in any blend used here.</summary>
        static Color Grey(float v) => new Color(v, v, v, 1f);
    }
}
