using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// The interface, built in code, against the shipped build's own stylesheet.
    ///
    /// There are no prefabs and no scene-authored canvases anywhere in this
    /// project, and that is a deliberate choice rather than a shortcut. Every
    /// screen in the original is a composition of type, hairlines and nine colours
    /// with no images at all; expressing that as serialised YAML would make it
    /// unreviewable in a diff and unmergeable by two people at once, while
    /// expressing it as code keeps the reasons next to the values — which is how
    /// the original was written and most of why it reads the way it does.
    ///
    /// THREE PLANES, AND THE DIFFERENCE BETWEEN THEM IS SMALL ON PURPOSE. Void is
    /// the ground. Panel is anything you can press. Card is anything that has come
    /// forward and taken the screen. Black is a background, not a material.
    ///
    /// Every metric below comes from <see cref="Css"/>, which is the APK's `:root`
    /// ported clamp for clamp. Nothing in this file is allowed a number of its own.
    /// </summary>
    public static class UiKit
    {
        /// <summary>
        /// NEVER ?? OR ?. AGAINST A UnityEngine.Object. THIS IS NOT STYLE.
        ///
        /// Unity overloads operator== so that a reference to a destroyed or absent
        /// native object compares equal to null. The null-coalescing and
        /// null-conditional operators do NOT use that overload — they test the
        /// managed reference, which is a real, non-null object. So
        ///
        ///     GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()
        ///
        /// reads as "get it or make it" and behaves as "get it", handing back a
        /// live C# handle to a component that is not there. Nothing fails until
        /// something touches a member, and then it throws from a line that looks
        /// unrelated. That is exactly how the title screen died: a CanvasGroup that
        /// was never added, on a reference that was never null.
        ///
        /// An explicit == null goes through the overload and is correct. This
        /// helper exists so there is one place to be right about it.
        /// </summary>
        public static T Ensure<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        // ---- the type -------------------------------------------------------
        //
        // THE INTERFACE IS MONOSPACE, AND THIS PORT WAS NOT.
        //
        // The sheet's font stack is
        //
        //   ui-monospace, SFMono-Regular, "SF Mono", "JetBrains Mono",
        //   "IBM Plex Mono", Menlo, Consolas, "Liberation Mono", monospace
        //
        // — every entry of which is a fixed-pitch face, and on an Android WebView
        // it resolves to the system's own monospace. What this port had been
        // asking for was `LegacyRuntime.ttf`, which is Unity's built-in
        // proportional face, with a Courier fallback that could never be reached
        // because the builtin never returns null. So every screen was set in the
        // wrong genre of typeface: proportional where the original is fixed-pitch,
        // which changes the colour of every line of text in the game and is most
        // of why a side-by-side reads as "similar" rather than "the same".
        //
        // The stack is walked in the sheet's own order, and the builtin is the
        // last resort rather than the first.

        static readonly string[] MonoStack =
        {
            "monospace",            // Android's own alias, and what the WebView picks
            "Roboto Mono",
            "Droid Sans Mono",
            "DejaVu Sans Mono",
            "Liberation Mono",
            "Menlo",
            "Consolas",
            "Courier New",
        };

        public static Font Mono
        {
            get
            {
                if (_mono != null) return _mono;
                for (int i = 0; i < MonoStack.Length && _mono == null; i++)
                    _mono = Font.CreateDynamicFontFromOSFont(MonoStack[i], 16);
                if (_mono == null) _mono = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _mono;
            }
        }
        static Font _mono;

        // ---- the canvas -----------------------------------------------------

        /// <summary>
        /// ONE CANVAS UNIT IS ONE CSS PIXEL.
        ///
        /// This canvas used to be 720x1280 reference units with `matchWidthOrHeight
        /// = 0.5`, and a comment saying one CSS pixel was two canvas units. Those
        /// two statements cannot both be true. A 0.5 match is a geometric blend of
        /// the width fit and the height fit, so the canvas is only 720 units across
        /// on a device whose aspect is exactly 720:1280 — on an ordinary 1080x2400
        /// phone the scale factor comes out sqrt(1.5 * 1.875) = 1.677, the canvas
        /// is 644 units wide, and the conversion is 1.79 units per CSS pixel rather
        /// than 2. Every metric in the interface was landing about a tenth small,
        /// by a different amount on every device.
        ///
        /// A constant pixel size at the device's own density removes the conversion
        /// instead of correcting it: a canvas unit becomes a density-independent
        /// pixel, which is the same thing Chromium calls a CSS pixel. The whole
        /// stylesheet then applies verbatim, `clamp()` and `vw` included, and it is
        /// right on every device rather than on one.
        /// </summary>
        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            s.scaleFactor = Css.Density;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<SafeArea>();
            return c;
        }

        /// <summary>
        /// A NOTCH IS NOT A SUGGESTION.
        ///
        /// The web original has a long note about this: env(safe-area-inset-*)
        /// reads zero inside an Android WebView however correctly the host sets
        /// viewport-fit, because a WebView is a View inside somebody else's
        /// Activity, so the host measures the insets and writes them onto :root
        /// itself. Unity does not have that problem — Screen.safeArea is the real
        /// measurement — but it has the same requirement, and a HUD band under a
        /// camera cutout is just as unreadable either way.
        ///
        /// Applied to the canvas root rather than to each control, so every screen
        /// inherits it and no layout has to think about it.
        /// </summary>
        class SafeArea : MonoBehaviour
        {
            RectTransform _rt;
            Rect _last;

            void Awake() => _rt = (RectTransform)transform;

            void Update()
            {
                Rect safe = Screen.safeArea;
                if (Screen.width == 0 || Screen.height == 0) return;

                // A TELEVISION THROWS AWAY THE OUTER FEW PERCENT OF THE PICTURE, so
                // nothing that has to be read may live there. This is a platform
                // rule rather than a taste, and it applies only where the display is
                // actually wide enough to be one.
                int ov = Layout.OverscanPixels;
                if (ov > 0)
                    safe = new Rect(safe.xMin + ov, safe.yMin + ov,
                                    Mathf.Max(1f, safe.width - ov * 2f),
                                    Mathf.Max(1f, safe.height - ov * 2f));

                if (safe == _last) return;
                _last = safe;

                for (int i = 0; i < _rt.childCount; i++)
                {
                    var child = _rt.GetChild(i) as RectTransform;
                    if (child == null) continue;
                    child.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                    child.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                    child.offsetMin = Vector2.zero;
                    child.offsetMax = Vector2.zero;
                }
            }
        }

        // ---- boxes ----------------------------------------------------------

        public static RectTransform Rect(Transform parent, string name,
                                         Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>A box that fills its parent.</summary>
        public static RectTransform Fill(Transform parent, string name)
            => Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Panel(Transform parent, string name, Color col,
                                  Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        // ---- text -----------------------------------------------------------

        /// <summary>
        /// A run of type, with the sheet's tracking on it.
        /// </summary>
        /// <param name="track">letter-spacing in em, as the rule states it.</param>
        public static Text Label(Transform parent, string name, string text, float cssSize,
                                 Color col, TextAnchor anchor, float track,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            return Type(rt, name, text, cssSize, col, anchor, track);
        }

        /// <summary>The same, filling a box that already exists.</summary>
        public static Text Type(Transform parent, string name, string text, float cssSize,
                                Color col, TextAnchor anchor, float track)
        {
            RectTransform rt = Fill(parent, name + "_t");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Mono;
            t.fontSize = Css.Pt(cssSize);
            // The body sets `text-transform:uppercase` on html, so every string in
            // the interface is capitals however it was written in the markup.
            t.text = text == null ? "" : text.ToUpperInvariant();
            t.color = col;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;

            if (track > 0.0001f)
            {
                var k = rt.gameObject.AddComponent<Trk>();
                k.spacing = track * cssSize;
            }
            return t;
        }

        /// <summary>A paragraph: it wraps, and its line-height is the sheet's.</summary>
        public static Text Para(Transform parent, string name, string text, float cssSize,
                                Color col, TextAnchor anchor, float track)
        {
            Text t = Type(parent, name, text, cssSize, col, anchor, track);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        // ---- the frame ------------------------------------------------------
        //
        // EVERY PRESSABLE THING IN THIS GAME IS A LIT FRAME. The brackets around a
        // label say "this is pressable"; the frame is what makes the whole
        // interface look like an INSTRUMENT rather than a list of words on black.
        // It is not decoration — it is the difference the eye reads first.
        //
        // Generated rather than imported, and nine-sliced, so one small texture is
        // every button, chip, field and card at every size, and the corner radius
        // stays the same number of CSS pixels whatever the control's shape.
        //
        // THE RADIUS IS A PARAMETER BECAUSE THE SHEET HAS SIX OF THEM. --r-chip and
        // --r-ctl are 6, --r-btn is 7, --r-card is 10, --r-sheet is 12 and a board
        // cell is 4. One shared sprite at a single radius is six controls drawn
        // with somebody else's corner.

        static readonly Dictionary<int, Sprite> Frames = new Dictionary<int, Sprite>();

        /// <summary>
        /// A rounded rectangle at a given CSS radius — filled, or as an inset
        /// stroke of a given CSS width.
        /// </summary>
        public static Sprite Frame(float radius, float stroke)
        {
            int key = Mathf.RoundToInt(radius * 100f) * 1000 + Mathf.RoundToInt(stroke * 100f);
            if (Frames.TryGetValue(key, out Sprite s) && s != null) return s;

            float px = Mathf.Clamp(Css.Density, 1f, 4f);
            // enough texture for both corners plus a middle row to stretch
            float sizeCss = radius * 2f + 4f;
            int n = Mathf.Max(8, Mathf.CeilToInt(sizeCss * px));
            float half = n * 0.5f;
            float radPx = radius * px;
            float strokePx = stroke * px;

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "frame" + key
            };

            var buf = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // signed distance to a rounded rectangle: negative inside
                    float dx = Mathf.Abs(x + 0.5f - half) - (half - radPx);
                    float dy = Mathf.Abs(y + 0.5f - half) - (half - radPx);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                                             + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radPx;

                    float a = stroke > 0f
                        // a band hugging the edge from the inside, feathered both ways
                        ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + strokePx + 0.5f)
                        : Mathf.Clamp01(0.5f - d);

                    buf[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }

            tex.SetPixels32(buf);
            tex.Apply(false, true);

            // The nine-slice border is in texels and the pixels-per-unit is the
            // density, so a corner keeps its CSS radius at any control size.
            float border = radPx + px;
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), px,
                              0, SpriteMeshType.FullRect,
                              new Vector4(border, border, border, border));
            Frames[key] = s;
            return s;
        }

        /// <summary>A circle, for a knob or a mark.</summary>
        public static Sprite Disc => Frame(500f, 0f);

        /// <summary>A stadium — `--r-pill` is 999, which is "however round it can be".</summary>
        public static Sprite Pill(float stroke) => Frame(500f, stroke);

        // ---- box-shadow -----------------------------------------------------

        /// <summary>
        /// `box-shadow: inset 0 0 0 Npx COLOUR` — the edge every control wears.
        /// A sibling image rather than part of the plate, because a plate that is
        /// its own border cannot change one without the other.
        /// </summary>
        public static Image Edge(RectTransform rt, Color col, float radius, float stroke)
        {
            RectTransform er = Fill(rt, "edge");
            var line = er.gameObject.AddComponent<Image>();
            line.sprite = Frame(radius, stroke);
            line.type = Image.Type.Sliced;
            line.color = col;
            line.raycastTarget = false;
            return line;
        }

        /// <summary>
        /// `box-shadow: 0 Npx 0 COLOUR` — an un-blurred drop, which is what gives
        /// every control in this interface its seat. It is drawn as a copy of the
        /// shape offset downward, behind the plate, which is exactly what the
        /// declaration means.
        /// </summary>
        public static Image Seat(RectTransform rt, Color col, float radius, float dy)
        {
            RectTransform sr = Rect(rt.parent, rt.name + "_seat", rt.anchorMin, rt.anchorMax,
                                    rt.offsetMin + new Vector2(0f, -dy), rt.offsetMax + new Vector2(0f, -dy));
            sr.pivot = rt.pivot;
            sr.sizeDelta = rt.sizeDelta;
            sr.anchoredPosition = rt.anchoredPosition + new Vector2(0f, -dy);
            var img = sr.gameObject.AddComponent<Image>();
            img.sprite = Frame(radius, 0f);
            img.type = Image.Type.Sliced;
            img.color = col;
            img.raycastTarget = false;
            sr.SetSiblingIndex(rt.GetSiblingIndex());
            return img;
        }

        /// <summary>
        /// THE PRIMARY CASTS. In the shipped build this is one line of CSS —
        /// `box-shadow: 0 0 22px -4px rgba(234,88,12,.7)` — and it is most of what
        /// makes that button look switched ON rather than filled in.
        ///
        /// It is worth knowing that this is NOT the camera bloom the board gets.
        /// The interface is a screen-space overlay and draws after the camera's
        /// post pass, so no amount of bloom would ever have reached it; the glow
        /// was always going to have to be drawn. A soft-edged plate behind the
        /// button, one more quad, is the same picture.
        /// </summary>
        public static Image Glow(RectTransform rt, Color col, float blur, float spread)
        {
            float reach = blur + spread;
            RectTransform gr = Rect(rt.parent, rt.name + "_glow", rt.anchorMin, rt.anchorMax,
                                    Vector2.zero, Vector2.zero);
            gr.pivot = rt.pivot;
            gr.sizeDelta = rt.sizeDelta + new Vector2(reach * 2f, reach * 2f);
            gr.anchoredPosition = rt.anchoredPosition;
            var g = gr.gameObject.AddComponent<Image>();
            g.sprite = GlowSprite(blur, spread);
            g.type = Image.Type.Sliced;
            g.color = col;
            g.raycastTarget = false;
            gr.SetSiblingIndex(rt.GetSiblingIndex());
            return g;
        }

        static readonly Dictionary<int, Sprite> Glows = new Dictionary<int, Sprite>();

        static Sprite GlowSprite(float blur, float spread)
        {
            int key = Mathf.RoundToInt(blur * 10f) * 1000 + Mathf.RoundToInt(spread * 10f) + 500000;
            if (Glows.TryGetValue(key, out Sprite s) && s != null) return s;

            float px = Mathf.Clamp(Css.Density * 0.5f, 1f, 2f);
            float reach = blur + spread;
            float radius = Css.RBtn;
            int n = Mathf.Max(16, Mathf.CeilToInt((reach * 2f + radius * 2f + 4f) * px));
            float half = n * 0.5f;
            float inner = half - (reach + radius) * px;

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "glow" + key
            };
            var buf = new Color32[n * n];
            float radPx = radius * px, blurPx = Mathf.Max(1f, blur * px);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) - inner;
                    float dy = Mathf.Abs(y + 0.5f - half) - inner;
                    float o = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = o + Mathf.Min(Mathf.Max(dx, dy), 0f) - radPx;
                    // a blur falls off faster than linear; squaring it is close
                    // enough to a gaussian at this radius and costs nothing
                    float a = Mathf.Clamp01(1f - d / blurPx);
                    buf[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
            tex.SetPixels32(buf);
            tex.Apply(false, true);

            float b = (reach + radius) * px;
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), px,
                              0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Glows[key] = s;
            return s;
        }

        // ---- pressing -------------------------------------------------------

        /// <summary>
        /// `:active` IS A STATE, NOT A TINT.
        ///
        /// Every control in the sheet answers a press by changing what it is made
        /// of and sitting down by the rule it is shadowed with:
        ///
        ///     .btn:active{ background:var(--rust); color:#000;
        ///                  transform:translateY(var(--rule));
        ///                  box-shadow:inset 0 0 0 var(--rule) var(--rust),
        ///                             0 0 0 rgba(0,0,0,.55); }
        ///
        /// — the seat collapses to nothing as the button moves down onto it, which
        /// is the whole illusion. uGUI's own ColorBlock can only multiply the
        /// target graphic, so it can tint a plate and it can do none of the rest.
        /// This does the rest.
        /// </summary>
        public class Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public Image plate, edge, seat;
            public Text label;
            public Color plateUp, plateDown, edgeUp, edgeDown, inkUp, inkDown;
            public float travel;
            RectTransform _rt;
            Vector2 _home;
            bool _down;

            void Awake()
            {
                _rt = (RectTransform)transform;
                _home = _rt.anchoredPosition;
            }

            public void OnPointerDown(PointerEventData e) => Set(true);
            public void OnPointerUp(PointerEventData e) => Set(false);

            void OnDisable() { if (_down) Set(false); }

            void Set(bool down)
            {
                _down = down;
                if (_rt == null) return;
                if (plate != null) plate.color = down ? plateDown : plateUp;
                if (edge != null) edge.color = down ? edgeDown : edgeUp;
                if (label != null) label.color = down ? inkDown : inkUp;
                if (seat != null) seat.enabled = !down;
                _rt.anchoredPosition = _home + new Vector2(0f, down ? -travel : 0f);
            }

            /// <summary>Re-read where the control lives, after a layout pass has moved it.</summary>
            public void Rehome()
            {
                if (_rt == null) _rt = (RectTransform)transform;
                if (!_down) _home = _rt.anchoredPosition;
            }
        }

        // ---- .btn -----------------------------------------------------------

        public enum Kind
        {
            /// <summary>.btn — a hole with an edge.</summary>
            Btn,
            /// <summary>.btn.primary — the one filled shape on a screen.</summary>
            Primary,
            /// <summary>.btn.ghost — no box, no ground, dim ink.</summary>
            Ghost,
        }

        /// <summary>The height a `.btn` of this kind occupies, margin excluded.</summary>
        public static float BtnHeight(Kind k)
            => k == Kind.Primary ? Css.HBtn + 14f
             : k == Kind.Ghost ? Css.HCtl
             : Css.HBtn + 6f;

        /// <summary>The margin that follows it. `.btn` is 10, a primary is 16, a ghost none.</summary>
        public static float BtnMargin(Kind k)
            => k == Kind.Primary ? 16f : k == Kind.Ghost ? 0f : 10f;

        /// <summary>
        /// A button is a bracketed label inside a lit frame: [ MENU ]. The brackets
        /// are part of the design system rather than decoration — they say "this is
        /// pressable" in the same monospace the rest of the interface speaks.
        ///
        /// A PRIMARY IS SOLID AND EVERYTHING ELSE IS A HOLE WITH AN EDGE. There is
        /// exactly one primary on any screen, and it is the only filled shape in
        /// the interface, so "the thing to press" needs no arrow, no pulse and no
        /// hint text — it is the one that is on.
        /// </summary>
        public static Button Btn(RectTransform slot, string label, System.Action onClick,
                                 Kind kind = Kind.Btn, float? sizeOverride = null, float? trackOverride = null)
        {
            bool primary = kind == Kind.Primary;
            bool ghost = kind == Kind.Ghost;

            float size = sizeOverride ?? (primary ? Css.TMd : ghost ? Css.TFine : Css.TSm);
            float track = trackOverride ?? (primary ? 0.22f : ghost ? 0.22f : 0.20f);

            Image plate = null, edge = null, seat = null;

            if (!ghost)
            {
                if (primary)
                {
                    Glow(slot, new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.7f), 22f, -4f);
                    seat = Seat(slot, Css.PrimarySeat, Css.RBtn, 3f);
                }
                else
                {
                    seat = Seat(slot, Css.Seat, Css.RBtn, Css.Rule);
                }

                plate = slot.gameObject.AddComponent<Image>();
                plate.sprite = Frame(Css.RBtn, 0f);
                plate.type = Image.Type.Sliced;
                plate.color = primary ? Palette.Rust : Palette.Panel;

                edge = Edge(slot, primary ? Css.PrimaryRim : Css.Edge, Css.RBtn, Css.Rule);
            }
            else
            {
                // a ghost still has to be hittable across its whole rect, and a
                // Text alone gives the raycaster only the glyphs
                plate = slot.gameObject.AddComponent<Image>();
                plate.color = new Color(0f, 0f, 0f, 0f);
            }

            // THE PRIMARY'S END TICKS. Two pseudo-elements, seven CSS pixels wide,
            // inset four from each end, drawn as a hairline of black at half alpha
            // on three sides. They are the detail that makes the one lit control in
            // the interface read as a MACHINED part rather than a coloured
            // rectangle, and this port did not have them at all.
            if (primary) EndTicks(slot);

            Text t = Type(slot, "label", "[ " + label + " ]", size,
                          primary ? Palette.Void : ghost ? Palette.Dim : Palette.Ink,
                          TextAnchor.MiddleCenter, track);

            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate;
            // the colour transition is done by Press, which can move the control
            // and collapse its seat as well as tint it
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            btn.colors = colors;

            var press = slot.gameObject.AddComponent<Press>();
            press.plate = plate;
            press.edge = edge;
            press.seat = seat;
            press.label = t;
            press.travel = primary ? 3f : ghost ? 0f : Css.Rule;
            press.plateUp = plate.color;
            press.plateDown = ghost ? plate.color : primary ? Palette.RustHi : Palette.Rust;
            press.edgeUp = edge != null ? edge.color : Color.clear;
            press.edgeDown = edge != null ? (primary ? Css.PrimaryRim : Palette.Rust) : Color.clear;
            press.inkUp = t.color;
            press.inkDown = ghost ? Palette.Rust : primary ? Palette.Void : Palette.Void;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        static void EndTicks(RectTransform slot)
        {
            void Tick(bool left)
            {
                RectTransform r = Rect(slot, left ? "tickL" : "tickR",
                                       new Vector2(left ? 0f : 1f, 0f), new Vector2(left ? 0f : 1f, 1f),
                                       new Vector2(left ? 4f : -11f, 4f), new Vector2(left ? 11f : -4f, -4f));
                var ink = new Color(0f, 0f, 0f, 0.5f);
                // top and bottom on both, and the outer side on each
                Panel(r, "t", ink, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -Css.Hair), Vector2.zero);
                Panel(r, "b", ink, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, Css.Hair));
                Panel(r, "s", ink,
                      new Vector2(left ? 0 : 1, 0), new Vector2(left ? 0 : 1, 1),
                      new Vector2(left ? 0 : -Css.Hair, 0), new Vector2(left ? Css.Hair : 0, 0));
            }
            Tick(true);
            Tick(false);
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = ("[ " + text + " ]").ToUpperInvariant();
        }

        // ---- .pill ----------------------------------------------------------
        //
        // .pill{ height:var(--h-ctl); min-width:var(--h-ctl); padding:0 15px;
        //        border-radius:var(--r-ctl); background:var(--panel);
        //        box-shadow:inset 0 0 0 var(--hair) var(--edge-2);
        //        font-size:var(--t-tiny); letter-spacing:.19em; color:var(--dim); }

        public static Button PillBtn(RectTransform slot, string label, System.Action onClick,
                                     Color? tint = null, float? size = null, float track = 0.19f)
        {
            var plate = slot.gameObject.AddComponent<Image>();
            plate.sprite = Frame(Css.RCtl, 0f);
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Panel;

            Image edge = Edge(slot, Css.Edge2, Css.RCtl, Css.Hair);

            Text t = Type(slot, "label", label, size ?? Css.TTiny, tint ?? Palette.Dim,
                          TextAnchor.MiddleCenter, track);

            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate;
            var colors = btn.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            btn.colors = colors;

            var press = slot.gameObject.AddComponent<Press>();
            press.plate = plate;
            press.edge = edge;
            press.label = t;
            press.plateUp = plate.color;
            press.plateDown = Palette.Rust;
            press.edgeUp = edge.color;
            press.edgeDown = Palette.Rust;
            press.inkUp = t.color;
            press.inkDown = Palette.Void;
            press.travel = 0f;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>`[disabled]` — opacity .34 and nothing to press.</summary>
        public static void Disable(Button b, bool off)
        {
            var cg = Ensure<CanvasGroup>(b.gameObject);
            cg.alpha = off ? 0.34f : 1f;
            cg.blocksRaycasts = !off;
            b.interactable = !off;
        }

        // ---- .sw ------------------------------------------------------------
        //
        // .sw{ width:54px;height:26px;border-radius:var(--r-pill);background:var(--dim-2); }
        // .sw::before{ content:'OFF'; ... font-size:8px; padding-right:8px }
        // .sw::after { content:''; top:3px;left:3px;width:20px;height:20px;border-radius:50% }
        // .sw.on{ background:var(--rust) } .sw.on::after{ transform:translateX(28px);background:#fff }

        public const float SwW = 54f, SwH = 26f, SwKnob = 20f, SwInset = 3f, SwThrow = 28f;

        /// <summary>
        /// A switch that is a SHAPE rather than a word. Orange and knob-right is
        /// on; slate and knob-left is off, and you can tell which from further away
        /// than you can read the label — which is the entire argument for a pill
        /// over a checkbox.
        /// </summary>
        public static Button Switch(RectTransform slot, bool on, System.Func<bool, bool> set)
        {
            slot.sizeDelta = new Vector2(SwW, SwH);

            var track = slot.gameObject.AddComponent<Image>();
            track.sprite = Pill(0f);
            track.type = Image.Type.Sliced;

            // box-shadow:inset 0 0 0 var(--hair) rgba(255,255,255,.10)
            RectTransform er = Fill(slot, "edge");
            var rim = er.gameObject.AddComponent<Image>();
            rim.sprite = Pill(Css.Hair);
            rim.type = Image.Type.Sliced;
            rim.color = new Color(1f, 1f, 1f, 0.10f);
            rim.raycastTarget = false;

            Text lbl = Type(slot, "state", "", 8f, Palette.Dim, TextAnchor.MiddleRight, 0.1f);
            var lr = (RectTransform)lbl.transform;
            lr.offsetMin = new Vector2(9f, 0f);
            lr.offsetMax = new Vector2(-8f, 0f);

            RectTransform kr = Rect(slot, "knob", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    Vector2.zero, Vector2.zero);
            kr.sizeDelta = new Vector2(SwKnob, SwKnob);
            kr.pivot = new Vector2(0f, 0.5f);
            var knob = kr.gameObject.AddComponent<Image>();
            knob.sprite = Disc;
            knob.type = Image.Type.Sliced;
            knob.raycastTarget = false;

            void Paint(bool v)
            {
                track.color = v ? Palette.Rust : Palette.Dim2;
                kr.anchoredPosition = new Vector2(SwInset + (v ? SwThrow : 0f), 0f);
                knob.color = v ? Color.white : Palette.Ink;
                lbl.text = v ? "ON" : "OFF";
                // .sw.on::before{ color:#3a1200 }
                lbl.color = v ? new Color32(0x3a, 0x12, 0x00, 0xff) : Palette.Dim;
                lbl.alignment = v ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            }
            Paint(on);

            var btn = slot.gameObject.AddComponent<Button>();
            btn.targetGraphic = track;
            var colors = btn.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            btn.colors = colors;
            btn.onClick.AddListener(() => Paint(set(true)));
            return btn;
        }

        // ---- the range ------------------------------------------------------
        //
        // input[type=range]{ height:3px; background:linear-gradient(90deg,
        //   var(--rust) 0 var(--fill), var(--dim-2) var(--fill) 100%) }
        // ::-webkit-slider-thumb{ width:17px;height:17px;border-radius:50%;
        //   background:var(--ink);box-shadow:0 0 0 1px rgba(0,0,0,.6) }

        public const float RangeTrack = 3f, RangeThumb = 17f;

        /// <summary>
        /// A continuous control for a continuous quantity. These were plus and
        /// minus buttons, which is a fine way to change a number by one and a poor
        /// way to answer "how bright, out of how bright it goes" — a stepper shows
        /// you a reading, a slider shows you a POSITION IN A RANGE, and brightness
        /// is only ever adjusted by comparison with the ends.
        /// </summary>
        public static Slider Range(RectTransform slot, int lo, int hi, int now, System.Action<int> set)
        {
            RectTransform bg = Rect(slot, "track", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                    new Vector2(RangeThumb * 0.5f, -RangeTrack * 0.5f),
                                    new Vector2(-RangeThumb * 0.5f, RangeTrack * 0.5f));
            var bgi = bg.gameObject.AddComponent<Image>();
            bgi.sprite = Frame(2f, 0f);
            bgi.type = Image.Type.Sliced;
            bgi.color = Palette.Dim2;

            RectTransform fillArea = Rect(slot, "fillArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                          new Vector2(RangeThumb * 0.5f, -RangeTrack * 0.5f),
                                          new Vector2(-RangeThumb * 0.5f, RangeTrack * 0.5f));
            RectTransform fill = Fill(fillArea, "fill");
            var fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = Frame(2f, 0f);
            fi.type = Image.Type.Sliced;
            fi.color = Palette.Rust;

            // THE HANDLE'S AREA IS A BAND, NOT THE WHOLE ROW.
            //
            // Slider drives handleRect's anchors to (v,0)-(v,1) of this rect and
            // leaves sizeDelta alone, so a handle with sizeDelta 17 inside a
            // fifty-unit row comes out sixty-seven tall and seventeen wide. That is
            // the squashed circle. Give it a band its own height and ask for no
            // extra, and it is the circle it was drawn as.
            RectTransform handleArea = Rect(slot, "handleArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                            new Vector2(RangeThumb * 0.5f, -RangeThumb * 0.5f),
                                            new Vector2(-RangeThumb * 0.5f, RangeThumb * 0.5f));
            RectTransform handle = Rect(handleArea, "handle", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                        Vector2.zero, Vector2.zero);
            handle.sizeDelta = new Vector2(RangeThumb, 0f);
            var hi2 = handle.gameObject.AddComponent<Image>();
            hi2.sprite = Disc;
            hi2.type = Image.Type.Sliced;
            hi2.color = Palette.Ink;

            var s = slot.gameObject.AddComponent<Slider>();
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = lo;
            s.maxValue = hi;
            s.wholeNumbers = true;
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = hi2;
            s.value = now;
            s.onValueChanged.AddListener(v => set(Mathf.RoundToInt(v)));
            return s;
        }

        // ---- .edIn ----------------------------------------------------------
        //
        // .edIn{ font-size:var(--t-tiny);letter-spacing:.1em;color:var(--ink);
        //        background:var(--panel);padding:10px 12px;border-radius:var(--r-card);
        //        box-shadow:inset 0 0 0 var(--hair) var(--edge-2) }

        /// <summary>
        /// The only free text in the game: a cube's name, a cube number, and a
        /// pasted share code. All three are folded to the character set the
        /// interface already speaks before they go anywhere, which is a house-style
        /// decision that also means no authored string can ever be markup.
        /// </summary>
        public static InputField Field(RectTransform slot, string placeholder)
        {
            var bg = slot.gameObject.AddComponent<Image>();
            bg.sprite = Frame(Css.RCard, 0f);
            bg.type = Image.Type.Sliced;
            bg.color = Palette.Panel;
            Edge(slot, Css.Edge2, Css.RCard, Css.Hair);

            Text text = Type(slot, "text", "", Css.TTiny, Palette.Ink, TextAnchor.MiddleLeft, 0.1f);
            var tr = (RectTransform)text.transform;
            tr.offsetMin = new Vector2(12f, 0f);
            tr.offsetMax = new Vector2(-12f, 0f);

            Text hint = Type(slot, "placeholder", placeholder, Css.TTiny, Palette.Dim2,
                             TextAnchor.MiddleLeft, 0.1f);
            var hr = (RectTransform)hint.transform;
            hr.offsetMin = new Vector2(12f, 0f);
            hr.offsetMax = new Vector2(-12f, 0f);

            var field = slot.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        // ---- marks ----------------------------------------------------------

        /// <summary>One of the shipped drawings, at a size in CSS pixels.</summary>
        public static Image Mark(Transform parent, string name, Sprite sprite, Color col,
                                 float w, float h, Vector2 anchor, Vector2 offset)
        {
            RectTransform rt = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = offset;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        // ---- the scroller ---------------------------------------------------

        /// <summary>
        /// A SCREEN THAT DOES NOT FIT IS NOT A LAYOUT PROBLEM, IT IS A SCROLL.
        ///
        /// `.layer` is `overflow-y:auto`, so this is not a special case for the
        /// manual — it is what every screen in the game does when the content is
        /// taller than the device. The two elastic spacers collapse and the column
        /// runs from the top.
        ///
        /// Returns the CONTENT, with a <see cref="Flow"/> already on it.
        /// </summary>
        public static Flow Column(RectTransform layer, bool centred = true)
        {
            RectTransform view = Rect(layer, "view", Vector2.zero, Vector2.one,
                                      new Vector2(Css.PadX, Css.PadY), new Vector2(-Css.PadX, -Css.PadY));

            // the mask needs something to raycast against or the drag never starts,
            // and it has to be invisible or it is a panel
            var catcher = view.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect(view, "content", new Vector2(0f, 1f), new Vector2(1f, 1f),
                                         Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);

            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            // Clamped rather than elastic: this is a document, and a document that
            // bounces off its own end reads as a toy. Inertia stays, because a flick
            // through a page of rules is the one gesture this screen exists for.
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = true;
            sr.decelerationRate = 0.135f;
            sr.scrollSensitivity = 28f;

            Flow f = Flow.On(content, view);
            f.centred = centred;
            return f;
        }

        /// <summary>A child of a column: an empty box, ready to be filled.</summary>
        public static RectTransform Slot(Flow col, string name, float height, float maxW, float marginBottom)
        {
            RectTransform rt = Rect(((MonoBehaviour)col).transform, name,
                                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            col.Add(rt, height, maxW, marginBottom);
            return rt;
        }

        // ---- the title's stage ----------------------------------------------

        /// <summary>
        /// THE TITLE SCREEN IS A STAGE, NOT A PANE OF GLASS.
        ///
        /// A flat scrim over the whole screen darkens the one thing the screen is
        /// actually about — the cube turning behind it. So the chrome occupies a
        /// band at each end and THE MIDDLE IS LEFT COMPLETELY ALONE: nothing at all
        /// between the player and the object. The gradient only darkens where words
        /// have to sit on top of it.
        ///
        /// The stops are the shipped ones, read straight off `#scTitle`, and they
        /// are load-bearing rather than tasteful — the two zero-alpha stops in the
        /// middle are what make it a stage instead of a tint.
        /// </summary>
        public static Image Stage(RectTransform rt)
        {
            const int N = 256;
            var tex = new Texture2D(1, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "stage"
            };

            // linear-gradient(180deg,
            //   rgba(0,0,0,.96) 0%, rgba(0,0,0,.80) 13%, rgba(0,0,0,.12) 27%,
            //   rgba(0,0,0,0) 42%, rgba(0,0,0,0) 52%,
            //   rgba(0,0,0,.32) 62%, rgba(0,0,0,.90) 74%, rgba(0,0,0,1) 100%)
            float[] stopAt = { 0f, 0.13f, 0.27f, 0.42f, 0.52f, 0.62f, 0.74f, 1f };
            float[] stopA = { 0.96f, 0.80f, 0.12f, 0f, 0f, 0.32f, 0.90f, 1f };

            var px = new Color32[N];
            for (int i = 0; i < N; i++)
            {
                float t = i / (float)(N - 1);
                float down = 1f - t;                 // texture row 0 is the BOTTOM
                int s = 0;
                while (s < stopAt.Length - 2 && down > stopAt[s + 1]) s++;
                float k = Mathf.InverseLerp(stopAt[s], stopAt[s + 1], down);
                float a = Mathf.Lerp(stopA[s], stopA[s + 1], k);
                px[i] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            Image img = Ensure<Image>(rt.gameObject);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, N), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            return img;
        }

        /// <summary>
        /// `.stick` — the gradient that nails GOT IT to the bottom of a scrolling
        /// page. `linear-gradient(180deg, transparent, rgba(0,0,0,.94) 34%, #000 62%)`,
        /// so the last line of the manual fades out under the button instead of
        /// being cut off by it.
        /// </summary>
        public static Image StickFade(RectTransform rt)
        {
            const int N = 64;
            var tex = new Texture2D(1, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "stick"
            };
            var px = new Color32[N];
            for (int i = 0; i < N; i++)
            {
                float down = 1f - i / (float)(N - 1);
                float a = down < 0.34f ? Mathf.Lerp(0f, 0.94f, down / 0.34f)
                        : down < 0.62f ? Mathf.Lerp(0.94f, 1f, (down - 0.34f) / 0.28f)
                        : 1f;
                px[i] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            Image img = Ensure<Image>(rt.gameObject);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, N), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// A LINEAR-GRADIENT WITH A HARD STOP IS A PROGRESS RAIL. Both the vault
        /// swatch and the boards rail are written that way in the shipped build —
        /// `linear-gradient(90deg,var(--arc) 0 62%,transparent 0)` — which is a
        /// filled bar and an empty one, not a fade.
        /// </summary>
        public static void Rail(RectTransform rt, Color col, float fraction, Color? rest = null)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) Object.Destroy(rt.GetChild(i).gameObject);
            Panel(rt, "on", col, Vector2.zero, new Vector2(Mathf.Clamp01(fraction), 1f), Vector2.zero, Vector2.zero);
            if (rest.HasValue)
                Panel(rt, "off", rest.Value, new Vector2(Mathf.Clamp01(fraction), 0f), Vector2.one,
                      Vector2.zero, Vector2.zero);
        }
    }
}
