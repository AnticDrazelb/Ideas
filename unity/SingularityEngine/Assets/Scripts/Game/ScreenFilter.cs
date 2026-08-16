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
    /// above it. So that is what this is: two full-bleed quads at the top of the
    /// sorting order, doing the arithmetic in BLEND MODES rather than in a
    /// fragment shader, because a UI graphic cannot read the framebuffer and does
    /// not need to.
    ///
    ///     out = in * c + (0.5 * (1 - c) + (b - 1))
    ///
    /// A gain and an offset. The gain is a multiply — DstColor·Zero below unity,
    /// and DstColor·One with (c-1) above it, since a source colour cannot exceed
    /// one in an LDR buffer. The offset is additive, and REVERSE-SUBTRACTIVE when
    /// it is negative, which is the ordinary case: any contrast above 100% makes
    /// it so. That is the whole reason Glyph.shader now exposes a blend op.
    ///
    /// AT 100/100 NOTHING HAPPENS — the same guarantee the original had. Both
    /// quads switch off, nothing is submitted, and the cost of these two settings
    /// is zero for everyone who leaves them alone.
    /// </summary>
    public class ScreenFilter : MonoBehaviour
    {
        const int Order = 32000;        // above the glass at 25, below nothing

        Image _gain, _offset;
        Material _gainMat, _offsetMat;
        int _brightWas = -1, _contrastWas = -1;

        public static ScreenFilter Attach(Camera cam)
        {
            // THE CAMERA IS NOT USED AND THE ARGUMENT IS KEPT. Every call site
            // passes it and the filter no longer lives there; changing the
            // signature would be a bigger diff than the fix, and this is the one
            // place where "why is this parameter here" has a written answer.
            var go = new GameObject("Screen filter");
            Object.DontDestroyOnLoad(go);
            var f = go.AddComponent<ScreenFilter>();
            f.Compose();
            f.Refresh();
            return f;
        }

        void Compose()
        {
            var canvas = Singularity.UI.UiKit.Canvas("ScreenFilter", Order);
            RectTransform full = Singularity.UI.UiKit.Rect(canvas.transform, "full",
                                                           Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _gain = Quad(full, "gain", out _gainMat);
            _offset = Quad(full, "offset", out _offsetMat);
        }

        Image Quad(Transform under, string name, out Material mat)
        {
            RectTransform rt = Singularity.UI.UiKit.Rect(under, name,
                                                          Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Singularity.UI.UiKit.RoundFill;
            img.type = Image.Type.Simple;

            // A FULL-SCREEN GRAPHIC THAT EATS EVERY TAP IS THE CLASSIC WAY TO SHIP
            // A GAME NOBODY CAN PLAY, and this one is over the entire interface.
            img.raycastTarget = false;

            mat = new Material(Shader.Find("Singularity/Glyph")) { name = name };
            img.material = mat;
            return img;
        }

        /// <summary>Read the settings back out of the store; call after Calibrate changes one.</summary>
        public void Refresh()
        {
            int bi = Store.Data.bright, ci = Store.Data.contrast;
            if (bi == _brightWas && ci == _contrastWas) return;
            _brightWas = bi; _contrastWas = ci;
            if (_gain == null) return;

            float b = bi / 100f, c = ci / 100f;
            bool identity = bi == 100 && ci == 100;
            _gain.gameObject.SetActive(!identity);
            _offset.gameObject.SetActive(!identity);
            if (identity) return;

            // ---- the gain ----------------------------------------------------
            if (c <= 1f)
            {
                Set(_gainMat, BlendOpAdd, BlendDstColor, BlendZero);
                _gain.color = new Color(c, c, c, 1f);
            }
            else
            {
                // dst*(src+1): a source colour cannot be above one, so the amount
                // ABOVE unity is what gets sent
                Set(_gainMat, BlendOpAdd, BlendDstColor, BlendOne);
                float over = Mathf.Min(1f, c - 1f);
                _gain.color = new Color(over, over, over, 1f);
            }

            // ---- the offset --------------------------------------------------
            float k = 0.5f * (1f - c) + (b - 1f);
            float mag = Mathf.Min(1f, Mathf.Abs(k));
            Set(_offsetMat, k >= 0f ? BlendOpAdd : BlendOpRevSub, BlendOne, BlendOne);
            _offset.color = new Color(mag, mag, mag, 1f);
            _offset.gameObject.SetActive(mag > 0.001f);
        }

        // UnityEngine.Rendering.BlendMode / BlendOp, as the numbers the shader's
        // [Enum] properties take. Named here so the call sites read as English.
        const float BlendZero = 0f, BlendOne = 1f, BlendDstColor = 4f;
        const float BlendOpAdd = 0f, BlendOpRevSub = 2f;

        static void Set(Material m, float op, float src, float dst)
        {
            m.SetFloat("_BlendOp", op);
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
        }
    }
}
