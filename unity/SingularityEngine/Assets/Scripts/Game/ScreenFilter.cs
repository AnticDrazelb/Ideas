using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// BRIGHTNESS AND CONTRAST, OVER EVERYTHING — WHICH IS WHAT IT ALWAYS SAID.
    ///
    /// This used to be a blit on the camera, and its own comment claimed it sat
    /// there "because it has to catch the board AND the interface". It could not.
    /// A camera image effect runs in OnRenderImage; every canvas in this game is
    /// a SCREEN-SPACE OVERLAY, which composites after the camera has finished and
    /// is untouched by anything a camera does. So the board was filtered and the
    /// interface was not, and the setting was half a setting.
    ///
    /// The only thing that can reach an overlay canvas is another overlay canvas
    /// above it. So that is what this is: full-bleed quads at the top of the
    /// sorting order, doing the arithmetic in BLEND MODES rather than in a
    /// fragment shader, because a UI graphic cannot read the framebuffer and does
    /// not need to.
    ///
    /// THE FORMULA IS THE OLD SHADER'S, EXACTLY:
    ///
    ///     out = saturate((in * b - 0.5) * c + 0.5)   =   in * (b*c) + 0.5 * (1 - c)
    ///
    /// A GAIN AND AN OFFSET, and getting that decomposition wrong is what turned
    /// the screen white the first time this shipped. BRIGHTNESS MULTIPLIES. It
    /// always did — the old shader's first line is `c.rgb *= _Brightness` — and
    /// rewriting it as an additive (b - 1) meant the maximum brightness setting
    /// added 0.6 to every pixel on a game whose background is 0.05. Every dark
    /// value in the palette collapsed into the same white, which is precisely as
    /// broken as it sounds and looked like a corrupted framebuffer rather than
    /// like a setting.
    ///
    /// Contrast pivots about mid grey. Doing it about black would just be
    /// brightness again, and doing it in gamma space is the right call here: the
    /// whole palette is authored as hex values somebody picked by eye, so the
    /// adjustment should move them the way the eye expects rather than the way
    /// the physics does. That is the old shader's comment and it still holds.
    ///
    /// THE ORDER OF THE TWO STAGES IS NOT FREE, because the framebuffer clamps
    /// between them. Gain first is exact whenever the gain is at or below one —
    /// nothing can overflow. Above one it is offset first, on the same argument
    /// run the other way: an intermediate that overflows is one whose true result
    /// overflows too, so both clamp to the same place. Solve picks; the harness
    /// proves it, at every slider position, against the formula above.
    ///
    /// AT 100/100 NOTHING HAPPENS — the same guarantee the original had. Every
    /// quad switches off, nothing is submitted, and the cost of these two
    /// settings is zero for everyone who leaves them alone.
    /// </summary>
    public class ScreenFilter : MonoBehaviour
    {
        const int Order = 32000;        // above the glass at 25, below nothing

        /// <summary>
        /// Offset, then up to two multiplies. Two, because `DstColor One` gives
        /// dst*(src+1) and a source colour cannot exceed one, so one quad tops out
        /// at a doubling — and the sliders can ask for 1.6 × 1.5 = 2.4.
        /// </summary>
        public const int Stages = 3;

        readonly Image[] _quad = new Image[Stages];
        readonly Material[] _mat = new Material[Stages];
        readonly Stage[] _plan = new Stage[Stages];
        int _brightWas = -1, _contrastWas = -1;

        // UnityEngine.Rendering.BlendMode / BlendOp, as the numbers the shader's
        // [Enum] properties take. The two factors are stored as their enum value
        // AND used as their arithmetic value where that is the same number —
        // Zero is 0 and One is 1 — which is what makes Run below three lines.
        public const float FZero = 0f, FOne = 1f, FDstColor = 4f;
        public const float OpAdd = 0f, OpRevSub = 2f;

        /// <summary>One quad: a blend op, two factors, and the grey it is drawn in.</summary>
        public struct Stage
        {
            public bool on;
            public float op, src, dst, grey;
        }

        public static ScreenFilter Attach(Camera cam)
        {
            // THE CAMERA IS NOT USED AND THE ARGUMENT IS KEPT. Every call site
            // passes it and the filter no longer lives there; changing the
            // signature would be a bigger diff than the fix, and this is the one
            // place where "why is this parameter here" has a written answer.
            var go = new GameObject("Screen filter");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            var f = go.AddComponent<ScreenFilter>();
            f.Compose();
            f.Refresh();
            return f;
        }

        void Compose()
        {
            // NOT UiKit.Canvas. That one insets every child to the safe area,
            // which is the correct rule for anything that has to be READ and the
            // exact opposite of the rule for this: a filter that stops at the
            // notch is a filter with an unfiltered strip along the top of it. And
            // it comes with a GraphicRaycaster, which is a full-screen collider
            // over the entire interface looking for a reason to exist.
            var go = new GameObject("filter canvas", typeof(Canvas), typeof(CanvasScaler));
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Order;

            for (int i = 0; i < Stages; i++)
                _quad[i] = Quad(go.transform, "stage" + i, out _mat[i]);
        }

        Image Quad(Transform under, string name, out Material mat)
        {
            RectTransform rt = Singularity.UI.UiKit.Rect(under, name,
                                                          Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();

            // NO SPRITE, DELIBERATELY. This was UiKit.RoundFill, which is a
            // rounded nine-slice — stretched over the whole screen it filtered the
            // middle and left four enormous unfiltered corners. A null sprite
            // draws a plain white quad, which is the only shape this wants.
            img.sprite = null;

            // A FULL-SCREEN GRAPHIC THAT EATS EVERY TAP IS THE CLASSIC WAY TO SHIP
            // A GAME NOBODY CAN PLAY, and this one is over the entire interface.
            img.raycastTarget = false;

            // A MISSING SHADER MUST COST THE SETTING, NOT THE GAME. Shaders.Ok
            // runs before any of this and says so on screen if Glyph did not
            // ship — but boot has to survive long enough to get there, and
            // `new Material(null)` throws.
            Shader sh = Shader.Find("Singularity/Glyph");
            mat = sh == null ? null : new Material(sh) { name = name };
            if (mat != null) img.material = mat;
            img.gameObject.SetActive(false);
            return img;
        }

        /// <summary>Read the settings back out of the store; call after Calibrate changes one.</summary>
        public void Refresh()
        {
            int bi = Store.Data.bright, ci = Store.Data.contrast;
            if (bi == _brightWas && ci == _contrastWas) return;
            _brightWas = bi; _contrastWas = ci;
            if (_quad[0] == null) return;

            Solve(bi, ci, _plan);
            for (int i = 0; i < Stages; i++)
            {
                Stage s = _plan[i];
                bool on = s.on && _mat[i] != null;
                if (_quad[i].gameObject.activeSelf != on) _quad[i].gameObject.SetActive(on);
                if (!on) continue;
                _mat[i].SetFloat("_BlendOp", s.op);
                _mat[i].SetFloat("_SrcBlend", s.src);
                _mat[i].SetFloat("_DstBlend", s.dst);
                _quad[i].color = new Color(s.grey, s.grey, s.grey, 1f);
            }
        }

        // ---- the arithmetic, with nothing in it that needs an engine ----------

        /// <summary>
        /// Fill <paramref name="into"/> with the quads that perform the setting,
        /// in draw order. Returns whether any of them are needed at all.
        /// </summary>
        public static bool Solve(int bi, int ci, Stage[] into)
        {
            for (int i = 0; i < into.Length; i++) into[i] = default;
            if (bi == 100 && ci == 100) return false;

            float b = bi / 100f, c = ci / 100f;
            float g = b * c;             // brightness MULTIPLIES
            float k = 0.5f * (1f - c);   // and contrast pivots about mid grey

            int at = 0;
            if (g <= 1f) { Gain(into, ref at, g); Offset(into, ref at, k); }
            else { Offset(into, ref at, k / g); Gain(into, ref at, g); }
            return at > 0;
        }

        static void Gain(Stage[] into, ref int at, float g)
        {
            if (Mathf.Abs(g - 1f) < 1e-4f) return;

            // dst * src, which is the whole of it below unity
            if (g < 1f)
            {
                into[at++] = new Stage { on = true, op = OpAdd, src = FDstColor, dst = FZero, grey = g };
                return;
            }

            // and dst * (src + 1) above it, since a source colour cannot exceed
            // one — so the amount ABOVE unity is what gets sent, and a gain past
            // two takes two quads. Splitting it evenly keeps both of them in the
            // half of the range where an eight-bit intermediate has the most to
            // say.
            int n = g > 2f ? 2 : 1;
            float each = n == 1 ? g : Mathf.Sqrt(g);
            for (int i = 0; i < n; i++)
                into[at++] = new Stage { on = true, op = OpAdd, src = FDstColor, dst = FOne, grey = each - 1f };
        }

        static void Offset(Stage[] into, ref int at, float k)
        {
            float mag = Mathf.Abs(k);
            if (mag < 1e-4f) return;

            // NO COMBINATION OF FACTORS CAN TAKE LIGHT AWAY, which is why
            // Glyph.shader exposes a blend op at all. Any contrast above 100 makes
            // this term negative, so the reverse-subtract path is the ordinary one.
            into[at++] = new Stage
            {
                on = true,
                op = k >= 0f ? OpAdd : OpRevSub,
                src = FOne, dst = FOne,
                grey = Mathf.Min(1f, mag),
            };
        }

        /// <summary>
        /// What the framebuffer will hold after the quads, for one channel. This
        /// is the blend hardware written out: factor times source, factor times
        /// destination, and a clamp after every stage because the target is
        /// eight-bit unsigned and there is nowhere to keep the overflow.
        /// </summary>
        public static float Run(Stage[] plan, float x)
        {
            foreach (Stage s in plan)
            {
                if (!s.on) continue;
                float sf = s.src == FDstColor ? x : s.src;   // Zero is 0, One is 1
                float df = s.dst == FDstColor ? x : s.dst;
                x = s.op == OpRevSub ? x * df - s.grey * sf : s.grey * sf + x * df;
                x = Mathf.Clamp01(x);
            }
            return x;
        }

        /// <summary>The old shader's fragment, line for line. The thing being matched.</summary>
        public static float Reference(int bi, int ci, float x)
        {
            float v = x * (bi / 100f);
            v = (v - 0.5f) * (ci / 100f) + 0.5f;
            return Mathf.Clamp01(v);
        }
    }
}
