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

        // ---- the dead margin, and why the case had a hole in the top of it ----
        //
        // HOW MUCH BLACK IS IN FRONT OF THE METAL ON EACH EDGE, measured off the
        // asset: the first row or column of the band that is lit ALL THE WAY
        // ACROSS. They are not the same and the top is not close — the case's top
        // edge DIPS between the corners, twenty-one pixels of notch spanning the
        // entire width of the band, and the other three are just the silhouette's
        // own outer edge.
        //
        // This is what made the first attempt at the cutout wrong. Growing a band
        // to reach the display edge scales everything in it, black included, so a
        // deeper top band meant a deeper NOTCH — the twenty-one rows stretched
        // with the metal and the camera ended up sitting in them. The case looked
        // stretched and the phone's camera was visibly not part of it, which is
        // exactly what it was: it was in the hole.
        //
        // So the dead rows are cut out of the band's source instead, and the depth
        // they used to occupy is filled with metal — see Fill.
        const float DeadT = 21f, DeadB = 4f, DeadL = 8f, DeadR = 10f;

        /// <summary>
        /// How deep a slice of pure metal to stretch across the fill. Thin on
        /// purpose: it is a stretch of featureless bezel, and the less of the
        /// picture it carries the less there is to smear.
        /// </summary>
        const float Strip = 6f;

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

        // ---- the cutouts that exist ------------------------------------------

        /// <summary>
        /// EVERY CAMERA CUTOUT ANDROID PHONES ACTUALLY HAVE, which is a short list
        /// and has been for years.
        ///
        /// The generic answer — one ring around whatever rectangle turns up — is
        /// the lowest common denominator of all of these, and it looks like it. A
        /// bathtub notch wants a machined slot that runs off the edge of the
        /// panel; a punch-hole wants a tight port with a lip all the way round.
        /// Giving both the same collar gives neither the right one.
        ///
        /// So this is a catalogue, and adding to it is the intended way to handle a
        /// shape that turns up later: one entry in <see cref="Ports"/>, one row of
        /// numbers. The forms have not moved in years and a new one only matters
        /// once enough people are holding it.
        /// </summary>
        public enum Cut
        {
            None,
            /// <summary>A round or near-round island. The common case, centred or in a corner.</summary>
            Hole,
            /// <summary>Two lenses under one opening — a racetrack, still an island.</summary>
            Pill,
            /// <summary>A waterdrop: small, centred, and joined to the edge of the display.</summary>
            Tear,
            /// <summary>The wide notch. A quarter of the edge or more, and part of the edge.</summary>
            Notch,
        }

        /// <summary>
        /// WHICH ONE IS THIS, FROM THE SHAPE ALONE.
        ///
        /// Measured, not looked up. The catalogue is of FORMS, and a form can be
        /// recognised — where a device list has to be told, has to be maintained,
        /// is wrong the day a phone ships, and is already wrong for a foldable
        /// whose two displays disagree. The same four numbers name the form on a
        /// handset nobody here has ever held.
        ///
        /// <paramref name="along"/> is the cutout's extent ALONG the edge it sits
        /// on as a fraction of that edge, <paramref name="ratio"/> is how much
        /// longer it is than it is deep, and <paramref name="attached"/> is whether
        /// it touches the display's edge or floats clear of it — which is the whole
        /// of the difference between a waterdrop and a punch-hole.
        /// </summary>
        public static Cut Classify(float along, float ratio, bool attached)
        {
            if (along <= 0.001f) return Cut.None;
            if (along >= 0.25f) return Cut.Notch;
            if (attached) return Cut.Tear;
            return ratio >= 1.8f ? Cut.Pill : Cut.Hole;
        }

        // ---- the panel -------------------------------------------------------

        /// <summary>
        /// The housing, on its own canvas behind everything — and THE CASE REACHES
        /// THE EDGE OF THE DISPLAY, WHICH IS THE ONLY THING THAT DOES.
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
            readonly RectTransform[] _piece = new RectTransform[16];

            /// <summary>
            /// Two is the most any shipping phone has — a pill for two lenses is
            /// ONE cutout, and the dual-hole layouts report two. Four is headroom.
            /// </summary>
            const int MaxPorts = 4;

            readonly RectTransform[] _port = new RectTransform[MaxPorts];
            readonly Sprite[] _art = new Sprite[8];

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
                Ports(rt);
            }

            /// <summary>
            /// THE CAMERA IS A FITTING IN THE PANEL, NOT A GAP IN IT.
            ///
            /// Filling the band with metal stopped the camera sitting in a hole,
            /// but flat metal with a black dot punched through it is still a phone
            /// with a phone's camera in it — the dot is the one thing on this
            /// display the game cannot draw, so the only way it reads as part of
            /// the machine is if the metal AROUND it says so. A machined shoulder
            /// and a lit lip, and the black becomes the port something looks out
            /// of, next to a panel that already has eight bolts in it.
            ///
            /// Screen.cutouts is the whole reason this needs no device list and no
            /// second copy of the art. safeArea is a rectangle and can only say
            /// "something is in the way along this edge"; cutouts gives the actual
            /// shapes, so a centre punch-hole, a corner one, a teardrop, a wide
            /// notch and a pill for two lenses are all just rects — and each gets a
            /// port cut to its own size, in its own place, on a phone nobody here
            /// has ever seen. Two of them get two ports.
            /// </summary>
            void Ports(RectTransform root)
            {
                float k = Owner != null ? Owner.scaleFactor : 1f;
                if (k <= 0f) k = 1f;
                float sw = Screen.width, sh = Screen.height;
                if (sw <= 0f || sh <= 0f) return;

                Rect[] cut = Screen.cutouts;
                int n = 0;
                for (int i = 0; i < cut.Length && n < MaxPorts; i++)
                {
                    float w = cut[i].width, h = cut[i].height;
                    if (w <= 1f || h <= 1f) continue;

                    // Which edge it belongs to, then the three numbers the
                    // catalogue is indexed by. A cutout on a long edge measures
                    // itself against that edge, which is what keeps a landscape
                    // phone from calling its punch-hole a notch.
                    float gapT = sh - cut[i].yMax, gapB = cut[i].yMin;
                    float gapL = cut[i].xMin, gapR = sw - cut[i].xMax;
                    bool onSide = Mathf.Min(gapL, gapR) < Mathf.Min(gapT, gapB);

                    float along = onSide ? h / sh : w / sw;
                    float ratio = onSide ? h / Mathf.Max(w, 1f) : w / Mathf.Max(h, 1f);
                    bool attached = Mathf.Min(Mathf.Min(gapT, gapB), Mathf.Min(gapL, gapR))
                                    <= Mathf.Max(2f, sh * 0.004f);

                    Cut style = Classify(along, ratio, attached);
                    if (style == Cut.None) continue;

                    Sprite art = Art(style);
                    if (art == null) continue;

                    if (_port[n] == null) _port[n] = BuildPort(root, n);
                    RectTransform rt = _port[n];
                    rt.GetComponent<Image>().sprite = art;

                    // THE CONTRACT WITH ports.py: every sheet is exactly twice its
                    // hole, hole centred. So the port lines up on any phone at any
                    // diameter without either side knowing the other's numbers.
                    rt.sizeDelta = new Vector2(w / k * 2f, h / k * 2f);
                    rt.anchoredPosition = new Vector2((cut[i].xMin + w * 0.5f) / k,
                                                      (cut[i].yMin + h * 0.5f) / k);
                    rt.gameObject.SetActive(true);
                    n++;
                }

                for (int i = n; i < MaxPorts; i++)
                    if (_port[i] != null) _port[i].gameObject.SetActive(false);
            }

            /// <summary>
            /// The sheet for one form, loaded once. A form with no art is not an
            /// error worth stopping for — the fill already put metal where the
            /// camera is, and a missing port only costs the shoulder around it.
            /// </summary>
            Sprite Art(Cut style)
            {
                int i = (int)style;
                if (_art[i] != null) return _art[i];

                var tex = Resources.Load<Texture2D>("port-" + style.ToString().ToLower());
                if (tex == null)
                {
                    Debug.LogWarning("Chassis: no port art for " + style);
                    return null;
                }
                _art[i] = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height),
                                        new Vector2(0.5f, 0.5f), 1f);
                return _art[i];
            }

            /// <summary>
            /// The shoulder and the lip. Both are tones rather than colours — a
            /// darkening and a lightening of whatever metal is underneath — so the
            /// port belongs to the bezel it lands on without this file needing an
            /// opinion about what rusted steel looks like.
            /// </summary>
            RectTransform BuildPort(RectTransform root, int i)
            {
                RectTransform rt = Singularity.UI.UiKit.Rect(root, "port" + i,
                                                             Vector2.zero, Vector2.zero,
                                                             Vector2.zero, Vector2.zero);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = rt.gameObject.AddComponent<Image>();
                img.type = Image.Type.Simple;
                img.color = Color.white;      // the sheet carries the whole port
                img.raycastTarget = false;
                return rt;
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

                // The four sides, with the dead rows CUT OUT OF THE SOURCE rather
                // than drawn and stretched. Each one keeps the art's own scale
                // exactly — it is moved inward by the margin it no longer draws,
                // not stretched over it — so the metal in these four is never
                // distorted however deep the display's inset turns out to be.
                Piece(root, 8, "bandT", C, DeadT, W - C * 2f, Bh - DeadT, 0, 1, 0f);
                Piece(root, 9, "bandB", C, H - Bh, W - C * 2f, Bh - DeadB, 0, 0, 0f);
                Piece(root, 10, "railL", DeadL, C, Sw - DeadL, H - C * 2f, 0, 0, 0f);
                Piece(root, 11, "railR", W - Sw, C, Sw - DeadR, H - C * 2f, 1, 0, 0f);

                // and the fills: a thin slice of PURE METAL per edge, taken from
                // just inside where the metal starts and stretched over everything
                // between the display edge and the band. This is the metal the
                // camera sits in.
                Piece(root, 12, "fillT", C, DeadT + 2f, W - C * 2f, Strip, 0, 1, 0f);
                Piece(root, 13, "fillB", C, H - DeadB - 2f - Strip, W - C * 2f, Strip, 0, 0, 0f);
                Piece(root, 14, "fillL", DeadL + 2f, C, Strip, H - C * 2f, 0, 0, 0f);
                Piece(root, 15, "fillR", W - DeadR - 2f - Strip, C, Strip, H - C * 2f, 1, 0, 0f);
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

                // THE BAND KEEPS ITS OWN SCALE AND MOVES; THE FILL DOES THE GROWING.
                //
                // `off` is how far in from the display edge the band's metal now
                // starts: the display's own inset, plus the dead margin the band no
                // longer draws. The band sits at exactly its authored depth below
                // that — so its inner edge lands on pad + BandH*K, which is where it
                // was before any of this, and the opening does not move. The fill
                // covers everything outside it, and the fill is the only thing that
                // stretches.
                for (int i = 8; i <= 9; i++)
                {
                    bool top = i == 8;
                    float pad = top ? pt : pb, dead = (top ? DeadT : DeadB) * K;
                    float off = pad + dead;
                    Span(_piece[i], true, cornerL, cornerR);
                    _piece[i].anchoredPosition = new Vector2((cornerL - cornerR) * 0.5f, top ? -off : off);
                    _piece[i].sizeDelta = new Vector2(-(cornerL + cornerR), BandH * K - dead);

                    Span(_piece[i + 4], true, cornerL, cornerR);
                    _piece[i + 4].anchoredPosition = new Vector2((cornerL - cornerR) * 0.5f, 0f);
                    _piece[i + 4].sizeDelta = new Vector2(-(cornerL + cornerR), off);
                    _piece[i + 4].gameObject.SetActive(off > 0.5f);
                }
                for (int i = 10; i <= 11; i++)
                {
                    bool left = i == 10;
                    float pad = left ? pl : pr, dead = (left ? DeadL : DeadR) * K;
                    float off = pad + dead;
                    Span(_piece[i], false, cornerT, cornerB);
                    _piece[i].anchoredPosition = new Vector2(left ? off : -off, (cornerB - cornerT) * 0.5f);
                    _piece[i].sizeDelta = new Vector2(SideW * K - dead, -(cornerT + cornerB));

                    Span(_piece[i + 4], false, cornerT, cornerB);
                    _piece[i + 4].anchoredPosition = new Vector2(0f, (cornerB - cornerT) * 0.5f);
                    _piece[i + 4].sizeDelta = new Vector2(off, -(cornerT + cornerB));
                    _piece[i + 4].gameObject.SetActive(off > 0.5f);
                }
            }

            /// <summary>
            /// Stretch a piece along the edge it belongs to, between the two
            /// corners at that edge's ends. Horizontal for the two bands, vertical
            /// for the two rails; the other axis is left to the caller, because
            /// that is the axis that says how deep the metal is.
            /// </summary>
            static void Span(RectTransform rt, bool horizontal, float a, float b)
            {
                if (horizontal)
                {
                    rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                    rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                    rt.pivot = new Vector2(0.5f, rt.pivot.y);
                }
                else
                {
                    rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
                    rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
                    rt.pivot = new Vector2(rt.pivot.x, 0.5f);
                }
            }
        }
    }
}
