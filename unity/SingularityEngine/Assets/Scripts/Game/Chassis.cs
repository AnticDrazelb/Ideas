using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// THE MACHINE YOU ARE HOLDING.
    ///
    /// Everything else in this interface is a reading. This is the thing the
    /// readings are mounted in: a salvaged steel case, rusted through at the
    /// corners, bolted at four points, with the glass sunk into a machined recess.
    ///
    /// It is not decoration, and the argument for it is the same one the aperture
    /// made. The fiction is that you are inside a broken machine looking at its
    /// diagnostics. Every screen in this game already reads as an instrument
    /// panel — framed plates, hairlines, one lit control, a monospace face — and
    /// then that panel was floating on nothing. Chrome with no chassis is a
    /// costume; give it a housing and the same pixels become a device.
    ///
    /// THE ONE IMPORTED ASSET IN THE PROJECT, AND IT IS WORTH SAYING WHY.
    ///
    /// Everything else here is generated: the glyphs, the frames, the glow, the
    /// icon, the sound. That is a real principle and it has paid for itself — a
    /// game with no assets has nothing to load, nothing to unhook, and nothing in
    /// a diff that a person cannot read. This is the exception, and the exception
    /// is honest: the housing went through four generated passes — flat grey,
    /// corrugated grey, pillowy grey, and a fairly good milled bezel — and not one
    /// of them was the object in the reference. Rust does not come out of value
    /// noise. Neither does forty years of somebody else's handling.
    ///
    /// So the case is a photograph of the thing it is meant to be, cut to its own
    /// silhouette, with the glass taken out of the middle. Nothing about that is
    /// a one-off: the cut lives in the editor as <c>Singularity → Chassis</c>,
    /// which takes any picture of a machine, finds its silhouette, lets you place
    /// the opening against a live overlay and writes both the PNG and the
    /// measurements. The mock-up this one came from and the Python the cut was
    /// ported from are also in the repository, under unity/tools/chassis, because
    /// an asset nobody can regenerate is exactly the unreviewable blob this
    /// project spent its whole life avoiding.
    ///
    /// TWO RULES SURVIVE FROM THE GENERATED VERSION, AND THEY STILL BIND.
    ///
    /// 1. NO TYPE EVER SITS ON THE METAL. The whole access audit is a set of
    ///    contrast measurements against four dark grounds, and a rusted steel
    ///    ground would invalidate every one of them. The metal is a border; the
    ///    words live on the glass, which is as dark as it ever was. This is now
    ///    enforced by construction rather than by care: the HUD and every screen
    ///    are inset to the opening, so there is nowhere on the metal to put a word.
    ///
    /// 2. IT IS NEVER BEHIND THE BOARD. The canvases are screen-space overlays and
    ///    draw after the camera, so anything painted across the middle would paint
    ///    over the cube. The case is twelve pieces around the edge — eight corner
    ///    arms and four sides — and the reason it is twelve rather than eight is
    ///    exactly this rule: a square piece at each corner would be simpler and
    ///    would hang a transparent quad over four corners of the board. There is
    ///    no quad over the middle at all, not even a clear one.
    /// </summary>
    public static class Chassis
    {
        // ---- what is in the picture ------------------------------------------
        //
        // Every number here is a MEASUREMENT OF AN ASSET, so it lives with the
        // asset: Resources/chassis.asset, a ChassisSpec, written by the tool under
        // Singularity → Chassis when the case is cut. They used to be consts in
        // this file with a comment saying a Python script had printed them, which
        // was true and was also a trap — the numbers and the picture they describe
        // could drift apart with nothing to notice, and swapping the case meant
        // editing C# with a calculator open.
        //
        // THE FALLBACK IS THE SHIPPED CASE, so a clone with no .asset file draws
        // the housing this game was built with rather than no housing at all. It
        // is a floor, not a default: the tool always writes the spec.

        static ChassisSpec _spec;

        /// <summary>
        /// Loaded once and kept. A property rather than a const because the whole
        /// point of the spec is that a different case can be dropped in, and none
        /// of the four callers of the insets could ever have seen that happen
        /// while the compiler was folding them into their call sites.
        /// </summary>
        public static ChassisSpec Spec
        {
            get
            {
                if (_spec == null)
                {
                    _spec = Resources.Load<ChassisSpec>("chassis");
                    if (_spec == null)
                    {
                        _spec = ScriptableObject.CreateInstance<ChassisSpec>();
                        _spec.name = "chassis (defaults)";
                    }
                    else if (!_spec.Consistent)
                    {
                        // A crop that disagrees with the art it describes places
                        // all twelve pieces wrong, and does it subtly — a few
                        // pixels of seam rather than a visible fault. Say so.
                        Debug.LogWarning("Chassis: chassis.asset says the art is " +
                                         _spec.artW + "x" + _spec.artH + " but its crop is " +
                                         (_spec.crop.z - _spec.crop.x) + "x" + (_spec.crop.w - _spec.crop.y) +
                                         ". Re-cut it from Singularity → Chassis.");
                    }
                }
                return _spec;
            }
        }

        /// <summary>Forget the loaded spec. The editor tool calls this after a re-cut.</summary>
        public static void Reload() => _spec = null;

        // ---- what the rest of the interface is allowed to know ----------------

        /// <summary>Display edge to glass, in canvas units. Everything readable lives inside these.</summary>
        public static float InsetLeft => Spec.left * Spec.scale;
        public static float InsetRight => Spec.right * Spec.scale;
        public static float InsetTop => Spec.top * Spec.scale;
        public static float InsetBottom => Spec.bottom * Spec.scale;

        static float Corner => Spec.corner * Spec.scale;

        // ---- the panel -------------------------------------------------------

        /// <summary>
        /// The housing, on its own canvas behind everything.
        /// </summary>
        public static Canvas Build()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Chassis", 5);
            var root = Singularity.UI.UiKit.Rect(c.transform, "panel",
                                                 Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.gameObject.AddComponent<Fit>();
            return c;
        }

        /// <summary>
        /// FITTED WHEN THE PANEL'S SIZE IS KNOWN, WHICH IS NEVER IN Awake.
        ///
        /// The corners are placed at a fixed size and the four sides take up
        /// whatever is left, so the pieces have to be laid out against a real
        /// rect — and they have to be laid out again when it changes: a rotation,
        /// a fold, a desktop window dragged wider. The anchors do most of the work
        /// on their own; this exists for the sizes that anchors cannot express and
        /// to hold the guard for a panel too small to take the corners at all.
        /// </summary>
        [ExecuteAlways]
        class Fit : MonoBehaviour
        {
            Texture2D _tex;
            bool _built;
            Vector2 _fitted = Vector2.zero;
            readonly RectTransform[] _piece = new RectTransform[12];

            // The art's own height, cached the moment Compose reads it, because
            // Piece flips y against it once per piece and a property that reloads
            // a ScriptableObject is not what that arithmetic should cost.
            float _artH;
            float _k;

            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                float w = rt.rect.width, h = rt.rect.height;
                if (w < Corner * 2f + 8f || h < Corner * 2f + 8f) return;
                if (Mathf.Abs(w - _fitted.x) < 1f && Mathf.Abs(h - _fitted.y) < 1f) return;
                _fitted = new Vector2(w, h);

                if (!_built) { Compose(rt); _built = true; }
                Stretch();
            }

            /// <summary>
            /// The twelve windows onto the case, once.
            ///
            /// Each is a sub-rectangle of the ONE texture, which is why the seams
            /// between them cannot show: a sprite cut from the middle of a texture
            /// still filters against its neighbours' texels, so two pieces that are
            /// adjacent in the art and adjacent on the screen are continuous
            /// without a single pixel of overlap to hide the join.
            /// </summary>
            void Compose(RectTransform root)
            {
                _tex = Resources.Load<Texture2D>("chassis");
                if (_tex == null)
                {
                    Debug.LogWarning("Chassis: Resources/chassis.png is missing; the case will not be drawn.");
                    return;
                }

                ChassisSpec s = Spec;
                _artH = s.artH;
                _k = s.scale;
                float Aw = s.armW, Ah = s.armH, Sw = s.sideW, Bh = s.bandH, C = s.corner;
                float W = s.artW, H = s.artH;
                float K = s.scale;

                // top left, and then the same L three more times. The vertical arm
                // runs the full depth of the corner; the horizontal arm takes the
                // rest of the top, so between them they cover every lit texel of
                // the corner and none of the board.
                Piece(root, 0, "tl_v", 0f, 0f, Aw, C, 0, 1, 0f);
                Piece(root, 1, "tl_h", Aw, 0f, C - Aw, Ah, 0, 1, Aw * K);
                Piece(root, 2, "tr_v", W - Aw, 0f, Aw, C, 1, 1, 0f);
                Piece(root, 3, "tr_h", W - C, 0f, C - Aw, Ah, 1, 1, -Aw * K);
                Piece(root, 4, "bl_v", 0f, H - C, Aw, C, 0, 0, 0f);
                Piece(root, 5, "bl_h", Aw, H - Ah, C - Aw, Ah, 0, 0, Aw * K);
                Piece(root, 6, "br_v", W - Aw, H - C, Aw, C, 1, 0, 0f);
                Piece(root, 7, "br_h", W - C, H - Ah, C - Aw, Ah, 1, 0, -Aw * K);

                // and the four sides, which are the only pieces that stretch
                Piece(root, 8, "bandT", C, 0f, W - C * 2f, Bh, 0, 1, 0f);
                Piece(root, 9, "bandB", C, H - Bh, W - C * 2f, Bh, 0, 0, 0f);
                Piece(root, 10, "railL", 0f, C, Sw, H - C * 2f, 0, 0, 0f);
                Piece(root, 11, "railR", W - Sw, C, Sw, H - C * 2f, 1, 0, 0f);
            }

            /// <summary>
            /// One window. <paramref name="ax"/> and <paramref name="ay"/> say
            /// which corner of the panel it hangs off, and the piece is pivoted on
            /// that same corner so <paramref name="ox"/> is simply how far along
            /// that edge it starts. The source rectangle is in the art's pixels
            /// with y measured DOWN from the top, because that is how the picture
            /// was measured and Sprite.Create is the only place that has to know
            /// Unity counts the other way.
            /// </summary>
            void Piece(RectTransform root, int i, string name,
                       float sx, float sy, float sw, float sh,
                       int ax, int ay, float ox)
            {
                var rect = new UnityEngine.Rect(sx, _artH - sy - sh, sw, sh);
                RectTransform rt = Singularity.UI.UiKit.Rect(root, name,
                                                             new Vector2(ax, ay), new Vector2(ax, ay),
                                                             Vector2.zero, Vector2.zero);
                rt.pivot = new Vector2(ax, ay);
                rt.anchoredPosition = new Vector2(ox, 0f);
                rt.sizeDelta = new Vector2(sw * _k, sh * _k);

                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Sprite.Create(_tex, rect, new Vector2(0.5f, 0.5f), 1f);
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.raycastTarget = false;
                _piece[i] = rt;
            }

            /// <summary>
            /// The four sides take whatever the corners left. Nothing else moves:
            /// the eight arms are anchored to their own corner at a fixed size, so
            /// a resize is four numbers.
            /// </summary>
            void Stretch()
            {
                if (_piece[8] == null) return;

                for (int i = 8; i <= 9; i++)
                {
                    _piece[i].anchorMin = new Vector2(0f, _piece[i].anchorMin.y);
                    _piece[i].anchorMax = new Vector2(1f, _piece[i].anchorMax.y);
                    _piece[i].pivot = new Vector2(0.5f, _piece[i].pivot.y);
                    _piece[i].anchoredPosition = new Vector2(0f, _piece[i].anchoredPosition.y);
                    _piece[i].sizeDelta = new Vector2(-Corner * 2f, _piece[i].sizeDelta.y);
                }
                for (int i = 10; i <= 11; i++)
                {
                    _piece[i].anchorMin = new Vector2(_piece[i].anchorMin.x, 0f);
                    _piece[i].anchorMax = new Vector2(_piece[i].anchorMax.x, 1f);
                    _piece[i].pivot = new Vector2(_piece[i].pivot.x, 0.5f);
                    _piece[i].anchoredPosition = new Vector2(_piece[i].anchoredPosition.x, 0f);
                    _piece[i].sizeDelta = new Vector2(_piece[i].sizeDelta.x, -Corner * 2f);
                }
            }
        }
    }
}
