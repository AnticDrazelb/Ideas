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
    /// silhouette, with the glass taken out of the middle. The script that cut it
    /// and the mock-up it was cut from are both in the repository — see
    /// unity/tools/chassis — because an asset nobody can regenerate is exactly the
    /// unreviewable blob this project spent its whole life avoiding.
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
        // Every number below was MEASURED off the asset by the script that made
        // it, not eyeballed here: unity/tools/chassis/cut.py prints them. They are
        // in the art's own pixels, and nothing but this file may care about them.

        const float ArtW = 461f, ArtH = 1018f;          // the case, cropped to itself

        // display edge to the glass, per side. The case is not symmetrical — the
        // top and bottom bezels are half again as deep as the sides, and the
        // bottom is deeper than the top — and pretending otherwise is what would
        // put a screen's own background over the metal on one edge.
        const float ArtLeft = 38f, ArtRight = 38f, ArtTop = 60f, ArtBottom = 62f;

        // How much of the art a corner piece takes. Both cuts land in a stretch of
        // edge whose PROFILE IS CONSTANT — past the notch on the top edge, above
        // the waist on the side — which is the whole trick to a nine-slice that
        // does not shear: what stretches has to be featureless along the direction
        // it stretches in.
        const float ArtCorner = 160f;

        // and how deep each piece has to be to hold all the metal in its stretch.
        // The arms are wider than the bezel by the opening's corner radius,
        // because the metal reaches further in where the glass turns the corner.
        const float ArmW = 58f, ArmH = 80f, SideW = 44f, BandH = 66f;

        /// <summary>
        /// Art pixels to canvas units.
        ///
        /// FIXED, RATHER THAN "WHATEVER MAKES THE CASE FIT". Scaling the art to
        /// the display would make the bezel thicker on a tall phone than on a
        /// short one and the layout underneath it would move, which is a bad trade
        /// for a housing whose whole job is to be the one thing that never moves.
        /// At five to four the bezel is the same forty-eight units everywhere and
        /// the corners are never distorted; the sides absorb the difference, which
        /// is what sides are for.
        /// </summary>
        const float K = 1.25f;

        // ---- what the rest of the interface is allowed to know ----------------

        /// <summary>Display edge to glass, in canvas units. Everything readable lives inside these.</summary>
        public const float InsetLeft = ArtLeft * K;
        public const float InsetRight = ArtRight * K;
        public const float InsetTop = ArtTop * K;
        public const float InsetBottom = ArtBottom * K;

        const float Corner = ArtCorner * K;

        // ---- the panel -------------------------------------------------------

        /// <summary>
        /// The housing, on its own canvas behind everything.
        /// </summary>
        /// <summary>
        /// THE CASE REACHES THE EDGE OF THE DISPLAY, AND IT IS THE ONLY THING THAT
        /// DOES.
        ///
        /// A notched phone costs you a band the FULL WIDTH of the screen, because
        /// Screen.safeArea is a rectangle and no rectangle can describe "a four
        /// millimetre dot in the middle of the top edge". A punch-hole is as
        /// expensive as a bathtub notch. Held inside that rectangle the whole
        /// machine sat in the middle of the display with a dead black strip above
        /// it, which is the one arrangement that makes a phone's camera look like
        /// a fault.
        ///
        /// So the housing alone opts out and takes the whole display. The bezel
        /// then grows on each edge by exactly the inset it just escaped, which
        /// PUTS THE OPENING BACK WHERE IT WAS TO THE PIXEL — Glass, Scanlines, the
        /// HUD, the screens and the board all still measure from the safe area and
        /// none of them had to change or even know. What changes is that the metal
        /// is deeper on the edge the camera is on, and the camera is now a hole in
        /// a steel panel that already has eight bolts in it.
        ///
        /// This needs to know nothing about which phone it is. A device whose
        /// cutout is bottom-left, or which has two of them, or none, gets the same
        /// four numbers from the same measurement.
        /// </summary>
        public static Canvas Build()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Chassis", 5, safe: false);
            var root = Singularity.UI.UiKit.Rect(c.transform, "panel",
                                                 Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // handed the canvas rather than looking for one: it is right here, and
            // the scale factor is the only way to turn a measurement in pixels into
            // the units this layout is authored in
            root.gameObject.AddComponent<Fit>().Owner = c;
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
        class Fit : MonoBehaviour
        {
            public Canvas Owner;

            Texture2D _tex;
            bool _built;
            Vector2 _fitted = Vector2.zero;
            float _padL = -1f, _padR = -1f, _padT = -1f, _padB = -1f;
            readonly RectTransform[] _piece = new RectTransform[12];

            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                float w = rt.rect.width, h = rt.rect.height;
                if (w < Corner * 2f + 8f || h < Corner * 2f + 8f) return;

                Singularity.UI.UiKit.SafeInsets(Owner, out float pl, out float pr,
                                                out float pt, out float pb);

                // THE SAFE AREA IS A SECOND REASON TO RE-FIT, and it is not implied
                // by the first. This panel is the whole display now, so its size no
                // longer changes when the inset does — a rotation that moves the
                // cutout from the top edge to the side leaves w and h identical and
                // would have been missed entirely.
                bool sized = Mathf.Abs(w - _fitted.x) >= 1f || Mathf.Abs(h - _fitted.y) >= 1f;
                bool inset = Mathf.Abs(pl - _padL) >= 0.5f || Mathf.Abs(pr - _padR) >= 0.5f
                          || Mathf.Abs(pt - _padT) >= 0.5f || Mathf.Abs(pb - _padB) >= 0.5f;
                if (!sized && !inset) return;

                _fitted = new Vector2(w, h);
                _padL = pl; _padR = pr; _padT = pt; _padB = pb;

                if (!_built) { Compose(rt); _built = true; }
                Stretch(pl, pr, pt, pb);
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

                const float Aw = ArmW, Ah = ArmH, Sw = SideW, Bh = BandH, C = ArtCorner;
                const float W = ArtW, H = ArtH;

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
                var rect = new UnityEngine.Rect(sx, ArtH - sy - sh, sw, sh);
                RectTransform rt = Singularity.UI.UiKit.Rect(root, name,
                                                             new Vector2(ax, ay), new Vector2(ax, ay),
                                                             Vector2.zero, Vector2.zero);
                rt.pivot = new Vector2(ax, ay);
                rt.anchoredPosition = new Vector2(ox, 0f);
                rt.sizeDelta = new Vector2(sw * K, sh * K);

                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Sprite.Create(_tex, rect, new Vector2(0.5f, 0.5f), 1f);
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.raycastTarget = false;
                _piece[i] = rt;
            }

            /// <summary>
            /// The four sides take whatever the corners left, and EVERY PIECE IS
            /// DEEPER ON AN EDGE THE DISPLAY IS HIDING SOMETHING BEHIND.
            ///
            /// The four pads are the safe area's insets. Adding each one to the
            /// depth of the pieces on its edge is what lets the case reach the
            /// physical edge of the display while its OPENING stays exactly where
            /// the safe area would have put it — so nothing downstream moves, and
            /// the extra metal all goes on the side the camera is on.
            ///
            /// The corners grow with it, which is the one thing here that is not
            /// free: a corner arm is a fixed window onto the art and stretching it
            /// stretches the rounded silhouette and the bolt inside it. It is a
            /// vertical stretch of a few percent on the one edge that has an inset
            /// at all, against the alternative of a black band the full width of
            /// the phone, and that trade is not close.
            /// </summary>
            void Stretch(float pl, float pr, float pt, float pb)
            {
                if (_piece[8] == null) return;

                float cornerL = Corner + pl, cornerR = Corner + pr;
                float cornerT = Corner + pt, cornerB = Corner + pb;

                // the eight arms: index says which corner it hangs off and whether
                // it is the vertical or the horizontal half of the L — see Compose
                for (int i = 0; i <= 7; i++)
                {
                    if (_piece[i] == null) continue;
                    int ax = (i / 2) % 2, ay = i < 4 ? 1 : 0;
                    float padX = ax == 0 ? pl : pr;
                    float padY = ay == 1 ? pt : pb;

                    if (i % 2 == 0)
                    {
                        _piece[i].anchoredPosition = Vector2.zero;
                        _piece[i].sizeDelta = new Vector2(ArmW * K + padX, Corner + padY);
                    }
                    else
                    {
                        float along = ArmW * K + padX;
                        _piece[i].anchoredPosition = new Vector2(ax == 0 ? along : -along, 0f);
                        _piece[i].sizeDelta = new Vector2((ArtCorner - ArmW) * K, ArmH * K + padY);
                    }
                }

                for (int i = 8; i <= 9; i++)
                {
                    _piece[i].anchorMin = new Vector2(0f, _piece[i].anchorMin.y);
                    _piece[i].anchorMax = new Vector2(1f, _piece[i].anchorMax.y);
                    _piece[i].pivot = new Vector2(0.5f, _piece[i].pivot.y);
                    _piece[i].anchoredPosition = new Vector2((cornerL - cornerR) * 0.5f, 0f);
                    _piece[i].sizeDelta = new Vector2(-(cornerL + cornerR),
                                                      BandH * K + (i == 8 ? pt : pb));
                }
                for (int i = 10; i <= 11; i++)
                {
                    _piece[i].anchorMin = new Vector2(_piece[i].anchorMin.x, 0f);
                    _piece[i].anchorMax = new Vector2(_piece[i].anchorMax.x, 1f);
                    _piece[i].pivot = new Vector2(_piece[i].pivot.x, 0.5f);
                    _piece[i].anchoredPosition = new Vector2(0f, (cornerB - cornerT) * 0.5f);
                    _piece[i].sizeDelta = new Vector2(SideW * K + (i == 10 ? pl : pr),
                                                      -(cornerT + cornerB));
                }
            }
        }
    }
}
