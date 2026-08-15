using UnityEngine;
using UnityEngine.UI;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// The interface, built in code.
    ///
    /// There are no prefabs and no scene-authored canvases anywhere in this
    /// project, and that is a deliberate choice rather than a shortcut. Every
    /// screen in the original is a composition of type, hairlines and nine
    /// colours with no images at all; expressing that as serialised YAML would
    /// make it unreviewable in a diff and unmergeable by two people at once,
    /// while expressing it as code keeps the reasons next to the values — which
    /// is how the original was written and most of why it reads the way it does.
    ///
    /// THREE PLANES, AND THE DIFFERENCE BETWEEN THEM IS SMALL ON PURPOSE. Void is
    /// the ground. Panel is anything you can press. Card is anything that has come
    /// forward and taken the screen. Black is a background, not a material.
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

        public static Font Mono
        {
            get
            {
                if (_mono != null) return _mono;
                _mono = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_mono == null) _mono = Font.CreateDynamicFontFromOSFont("Courier New", 16);
                return _mono;
            }
        }
        static Font _mono;

        /// <summary>
        /// Every canvas this kit has made, so legibility can repaint the
        /// interface where it stands. A list rather than a Find call because
        /// there are exactly two of them and they are both made right here —
        /// searching the scene for something you handed out yourself is how a
        /// third canvas somebody adds later quietly stops being repainted.
        /// </summary>
        public static readonly System.Collections.Generic.List<Canvas> Canvases
            = new System.Collections.Generic.List<Canvas>();

        /// <summary>
        /// EVERY CANVAS IS THE WHOLE DISPLAY. The <paramref name="safe"/> flag is
        /// kept so existing call sites compile and it does nothing.
        ///
        /// The safe area is a RECTANGLE, and no rectangle can describe a four
        /// millimetre dot in the middle of the top edge — the only one that
        /// excludes it is missing the whole band it sits in. Honouring it cost a
        /// full-width strip of display to dodge a camera that the case's own metal
        /// is already deeper than, on top of the bezel spent there anyway. So the
        /// strip is not spent, and the notch is not thought about.
        /// </summary>
        public static Canvas Canvas(string name, int order, bool safe = true)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            Object.DontDestroyOnLoad(go);
            Canvases.Add(c);
            return c;
        }


        /// <summary>
        /// Swap the interface between the shipped palette and the legible one,
        /// in place. See Palette.TypeSwaps for why type and surfaces are walked
        /// separately and why doing it twice is harmless.
        ///
        /// Alpha is carried through untouched, so the hairline that is rust at
        /// .20 is still the same hairline at .20 afterwards, and a colour that is
        /// in neither list — an interpolated tint, a button's pressed state — is
        /// left exactly where it was.
        /// </summary>
        public static void Repaint()
        {
            bool legible = Access.Legible;

            bool Swap(Palette.Swap[] list, Color had, out Color want)
            {
                foreach (Palette.Swap s in list)
                {
                    Color from = legible ? s.shipped : s.legible;
                    Color to = legible ? s.legible : s.shipped;
                    if (Same(had, from)) { want = new Color(to.r, to.g, to.b, had.a); return true; }
                }
                want = had;
                return false;
            }

            for (int i = Canvases.Count - 1; i >= 0; i--)
            {
                Canvas c = Canvases[i];
                if (c == null) { Canvases.RemoveAt(i); continue; }   // == null, not ?., see Ensure

                foreach (Text t in c.GetComponentsInChildren<Text>(true))
                    if (Swap(Palette.TypeSwaps, t.color, out Color to)) t.color = to;

                foreach (Image im in c.GetComponentsInChildren<Image>(true))
                    if (Swap(Palette.FillSwaps, im.color, out Color to)) im.color = to;
            }
        }

        /// <summary>Equal to the byte a colour was authored as. Ignores alpha.</summary>
        static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;

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

        public static Image Panel(Transform parent, string name, Color col,
                                  Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// A label.
        ///
        /// OVERFLOW IS THE DEFAULT AND IT IS THE RIGHT ONE for what most of this
        /// interface is: a number in a chip, a word on a button, a caption under
        /// an icon. Those are laid out by their centre and are routinely wider
        /// than the rect they are centred in, deliberately.
        ///
        /// It is exactly wrong for a SENTENCE, and that difference is what
        /// <paramref name="wrap"/> is for. A sentence in a fixed box with overflow
        /// on does not get smaller or break — it keeps going, out of the plate,
        /// over the bezel and off the machine, and the reader loses the end of
        /// every line. Anything written as prose asks to wrap.
        /// </summary>
        /// <summary>
        /// WRAPPING IS THE DEFAULT, AND OVERFLOW IS THE THING YOU HAVE TO ASK FOR.
        ///
        /// It was the other way round, and the other way round is a layout bug
        /// waiting on a long word. A label set to Overflow does not stop at its own
        /// box — it keeps drawing straight across whatever is beside it, and the
        /// two labels do not blend or clip, they INTERLEAVE. CALIBRATE printed
        /// "BRIGHTNESS125%" for exactly this reason: the name's box ran to 62% of
        /// the row and the reading's box started at 30%, so the two overlapped by a
        /// third of the row and the only thing hiding it was that most labels
        /// happened to be short enough.
        ///
        /// Wrapping cannot do that. The worst it can do is take a second line,
        /// which is visible, honest and fixable — where an overflow is invisible
        /// until the one string that is too long, in the one language nobody
        /// tested, on the one phone that is narrower than the reference.
        ///
        /// Vertical overflow stays ON: text may take the room it needs, because a
        /// clipped line is text the player cannot read and this game would rather
        /// look wrong than go quiet. Where the box genuinely cannot grow, the
        /// answer is <see cref="Fit"/> — shrink to fit, with a floor.
        /// </summary>
        public static Text Label(Transform parent, string name, string text, int size,
                                 Color col, TextAnchor anchor,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax,
                                 bool wrap = true)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Mono;
            t.fontSize = size;
            t.text = text;
            t.color = col;
            t.alignment = anchor;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// MAKE THIS ONE FIT, WHATEVER IT SAYS.
        ///
        /// For the labels whose text is DATA rather than authored copy — a vault's
        /// name, a cube's name, whatever somebody typed into the Forge — where the
        /// box is fixed by the layout around it and the string is not known when
        /// the screen is written. "VAULT I · CALIBRATION" at thirty-two between
        /// two arrows is four characters wider than the gap, so it slid under both
        /// of them and lost its first and last letter.
        ///
        /// Shrinking is the honest answer there and truncation is not: a name with
        /// its end cut off is a different name, and this game has cubes whose
        /// names differ in the last word. Best fit is bounded below so it can
        /// never shrink to something nobody can read — if it would have to, the
        /// layout is wrong and should be seen to be wrong.
        /// </summary>
        public static Text Fit(Text t, int min)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = t.fontSize;
            return t;
        }

        // ---- the frame ------------------------------------------------------
        //
        // EVERY PRESSABLE THING IN THIS GAME IS A LIT FRAME, and it took a
        // side-by-side with the shipped build to notice that this port had been
        // drawing flat rectangles instead. The brackets around a label say "this is
        // pressable"; the frame is what makes the whole interface look like an
        // INSTRUMENT rather than a list of words on black. It is not decoration —
        // it is the difference the eye reads first.
        //
        // Generated rather than imported, and nine-sliced, so one 64px texture is
        // every button, chip, field and card at every size, and the corner radius
        // stays the same number of real pixels whatever the control's shape.

        // ---- THE SHIPPED METRICS, EXTRACTED RATHER THAN EYEBALLED -----------
        //
        // Every number below is read off the APK's own :root custom properties.
        // The canvas here is 720 units wide against a viewport that is about 360
        // CSS pixels, so ONE CSS PIXEL IS TWO CANVAS UNITS and the conversion is
        // the only arithmetic in it.
        //
        //   --r-btn      7px   -> 14    --h-btn   46px -> 92
        //   --r-card    10px   -> 20    --h-ctl   44px -> 88
        //   --rule       2px   ->  4    --h-row   50px -> 100
        //   .btn         min-height  h-btn +  6px -> 104
        //   .btn.primary min-height  h-btn + 14px -> 120
        //   --edge      rgba(234,88,12,.34)   the border every control wears
        //   primary     inset rgba(251,146,60,.55) + a 22px rust glow
        //
        // The border was twice as strong as this and the corners nearly twice as
        // round, which together are most of why the controls read as an app's
        // rather than an instrument's.

        /// <summary>Standard control height — .btn min-height.</summary>
        public const float BtnH = 104f;
        /// <summary>The one lit object on a screen — .btn.primary min-height.</summary>
        public const float PrimaryH = 120f;
        /// <summary>--h-row, for list rows.</summary>
        public const float RowH = 100f;

        /// <summary>--edge. The border weight every control shares.</summary>
        public static Color Edge => new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.34f);
        /// <summary>The primary's inset rim: rust-hi at 55%.</summary>
        public static Color EdgeOn => new Color(Palette.RustHi.r, Palette.RustHi.g, Palette.RustHi.b, 0.55f);

        const int FrameTex = 64, FrameRad = 14, FrameSlice = 20, FrameStroke = 4;

        static Sprite _fill, _line, _glow;

        /// <summary>
        /// THE PRIMARY CASTS. In the shipped build this is one line of CSS —
        /// box-shadow: 0 0 22px -4px rgba(234,88,12,.7) — and it is most of what
        /// makes that button look switched ON rather than filled in.
        ///
        /// It is worth knowing that this is NOT the camera bloom the board gets.
        /// The interface is a screen-space overlay and draws after the camera's
        /// post pass, so no amount of bloom would ever have reached it; the glow
        /// was always going to have to be drawn. A soft-edged plate behind the
        /// button, additive, is the same picture for one more quad.
        /// </summary>
        public static Sprite GlowFill { get { if (_glow == null) _glow = BuildGlow(); return _glow; } }

        /// <summary>How far the primary's glow reaches past its own edge. 22px blur, -4 spread.</summary>
        public const float GlowReach = 36f;

        static Sprite BuildGlow()
        {
            const int N = 128, Rad = 14, Soft = 36;   // in canvas units, 1:1 with texels here
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "glow"
            };
            var px = new Color32[N * N];
            const float H = N * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - H) - (H - Soft - Rad);
                    float dy = Mathf.Abs(y + 0.5f - H) - (H - Soft - Rad);
                    float o = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = o + Mathf.Min(Mathf.Max(dx, dy), 0f) - Rad;
                    // a blur falls off faster than linear; squaring it is close
                    // enough to a gaussian at this radius and costs nothing
                    float a = Mathf.Clamp01(1f - d / Soft);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            const int b = Soft + Rad;
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>The plate: a filled rounded rectangle.</summary>
        public static Sprite RoundFill { get { if (_fill == null) _fill = BuildFrame(false); return _fill; } }

        /// <summary>The edge: the same rectangle as a stroke, and the thing that lights up.</summary>
        public static Sprite RoundLine { get { if (_line == null) _line = BuildFrame(true); return _line; } }

        static Sprite BuildFrame(bool stroke)
        {
            var tex = new Texture2D(FrameTex, FrameTex, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = stroke ? "frame_line" : "frame_fill"
            };

            var px = new Color32[FrameTex * FrameTex];
            const float H = FrameTex * 0.5f;

            for (int y = 0; y < FrameTex; y++)
                for (int x = 0; x < FrameTex; x++)
                {
                    // signed distance to a rounded rectangle: negative inside
                    float dx = Mathf.Abs(x + 0.5f - H) - (H - FrameRad);
                    float dy = Mathf.Abs(y + 0.5f - H) - (H - FrameRad);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                                             + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - FrameRad;

                    float a = stroke
                        // a band hugging the edge from the inside, feathered both ways
                        ? Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + FrameStroke + 0.5f)
                        : Mathf.Clamp01(0.5f - d);

                    px[y * FrameTex + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, FrameTex, FrameTex), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect,
                                 new Vector4(FrameSlice, FrameSlice, FrameSlice, FrameSlice));
        }

        // ---- the pill -------------------------------------------------------
        //
        // The same nine-slice trick with the radius run all the way to the half
        // height, so the ends are semicircles at any width. A toggle wants to be a
        // PILL rather than a small rectangle for one reason: a rectangle that says
        // ON and a rectangle that says OFF are the same object with different text,
        // and a pill with the knob at the other end is a different SHAPE. Shape
        // reads across a room; a three-letter word does not.

        static Sprite _pill, _bar, _disc;

        // A NINE-SLICE BORDER HAS TO FIT INSIDE THE CONTROL IT IS STRETCHED OVER.
        //
        // These were one 64px stadium with a 30px border on every side. Sixty pixels
        // of border in a forty-two pixel switch leaves nothing for the middle, so
        // Unity scales the slices down to fit — by a different amount on each axis,
        // and by a different amount again on a six-pixel slider track. That is the
        // whole of "the pills are not uniform and the sliders look like squashed
        // circles": one sprite asked to be three sizes it could not be.
        //
        // So there are two, each sized for what it is stretched over, and the thin
        // one is used only on the track.
        public static Sprite PillFill { get { if (_pill == null) _pill = Round(32, 16, 15); return _pill; } }
        public static Sprite BarFill { get { if (_bar == null) _bar = Round(16, 8, 7); return _bar; } }

        /// <summary>The knob, and every other circle the interface needs.</summary>
        public static Sprite Disc { get { if (_disc == null) _disc = Round(64, 32, 0); return _disc; } }

        static Sprite Round(int size, int rad, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = border == 0 ? "disc" : "stadium" + size
            };
            var px = new Color32[size * size];
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - h) - (h - rad);
                    float dy = Mathf.Abs(y + 0.5f - h) - (h - rad);
                    float o = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float d = o + Mathf.Min(Mathf.Max(dx, dy), 0f) - rad;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>
        /// A switch that is a shape rather than a word. Orange and knob-right is on;
        /// slate and knob-left is off, and you can tell which from further away than
        /// you can read the label.
        /// </summary>
        /// <summary>
        /// THE TARGET IS NOT THE INK.
        ///
        /// A pill drawn forty-two units tall is a good-looking switch and half a
        /// tap target: 2.5.5 at AAA asks for 44 CSS px on the shortest side,
        /// which is eighty-eight units here. Growing the pill to meet that would
        /// make it a rectangle with rounded ends rather than a pill, and the
        /// SHAPE is the whole reason a switch beats a three-letter word — you can
        /// read knob-left from across a room.
        ///
        /// So the pressable rect and the drawn pill are separated. The outer rect
        /// takes whatever height it is given, carries the Button and an invisible
        /// raycast plate, and the pill is drawn inside it at the size it was
        /// designed. Nothing about the picture changes; the thing you can hit
        /// stops being the picture.
        /// </summary>
        public static Button Switch(Transform parent, string name, bool on, System.Func<bool, bool> set,
                                    Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform hit = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var plate = hit.gameObject.AddComponent<Image>();
            plate.color = new Color(0, 0, 0, 0);

            const float PillH = 42f;
            RectTransform rt = Rect(hit, "pill", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                                    new Vector2(0, -PillH * 0.5f), new Vector2(0, PillH * 0.5f));

            var track = rt.gameObject.AddComponent<Image>();
            track.sprite = PillFill;
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;

            RectTransform kr = Rect(rt, "knob", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            kr.sizeDelta = new Vector2(34, 34);
            var knob = kr.gameObject.AddComponent<Image>();
            knob.sprite = Disc;
            knob.color = Palette.Ink;
            knob.raycastTarget = false;

            Text lbl = Label(rt, "state", "", 19, Palette.Void, TextAnchor.MiddleCenter,
                             Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));

            void Paint(bool v)
            {
                // Inert rather than Dim2: this is a SURFACE, and under legibility
                // the quiet type it used to share a value with goes the other way.
                track.color = v ? Palette.Rust : Palette.Inert;
                kr.anchorMin = kr.anchorMax = new Vector2(v ? 1f : 0f, 0.5f);
                kr.anchoredPosition = new Vector2(v ? -22f : 22f, 0f);
                lbl.text = v ? "ON" : "OFF";
                lbl.color = v ? Palette.Void : Palette.Ink;
                lbl.alignment = v ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            }
            Paint(on);

            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = track;
            btn.onClick.AddListener(() => Paint(set(true)));
            return btn;
        }

        /// <summary>
        /// A continuous control for a continuous quantity. These were plus and minus
        /// buttons, which is a fine way to change a number by one and a poor way to
        /// answer "how bright, out of how bright it goes" — a stepper shows you a
        /// reading, a slider shows you a POSITION IN A RANGE, and brightness is only
        /// ever adjusted by comparison with the ends.
        /// </summary>
        public static Slider Bar(Transform parent, string name, int lo, int hi, int now,
                                 System.Action<int> set,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);

            // A SLIDER WITH NO GRAPHIC ON ITSELF CAN ONLY BE DRAGGED BY ITS
            // HANDLE, and this one's handle is twenty-eight units across.
            //
            // Slider takes pointer events on its OWN GameObject, and the
            // raycaster only finds a GameObject that has a Graphic with
            // raycastTarget set. There was none here — the target graphic was the
            // handle, which is a child — so a tap anywhere on the track did
            // nothing at all and the only way to move the value was to catch a
            // twenty-eight unit disc. That is a bug and a 2.5.5 failure at the
            // same time, and one invisible plate is the whole of both fixes: the
            // track becomes tappable along its length, and the target becomes
            // however tall this rect is.
            var catcher = rt.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);

            const float TrackH = 14f, Knob = 28f;

            RectTransform bg = Rect(rt, "track", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                                    new Vector2(Knob * 0.5f, -TrackH * 0.5f), new Vector2(-Knob * 0.5f, TrackH * 0.5f));
            var bgi = bg.gameObject.AddComponent<Image>();
            bgi.sprite = BarFill;
            bgi.type = Image.Type.Sliced;
            bgi.color = Palette.Inert;      // a surface, not type — see Palette.Inert

            RectTransform fillArea = Rect(rt, "fillArea", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                                          new Vector2(Knob * 0.5f, -TrackH * 0.5f), new Vector2(-Knob * 0.5f, TrackH * 0.5f));
            RectTransform fill = Rect(fillArea, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = BarFill;
            fi.type = Image.Type.Sliced;
            fi.color = Palette.Rust;

            // THE HANDLE'S AREA IS A BAND, NOT THE WHOLE ROW.
            //
            // Slider drives handleRect's anchors to (v,0)-(v,1) of this rect and
            // leaves sizeDelta alone, so a handle with sizeDelta 30 inside a
            // hundred-pixel row comes out a hundred and thirty tall and thirty
            // wide. That is the squashed circle. Give it a band its own height and
            // ask for no extra, and it is the circle it was drawn as.
            RectTransform handleArea = Rect(rt, "handleArea", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                                            new Vector2(Knob * 0.5f, -Knob * 0.5f), new Vector2(-Knob * 0.5f, Knob * 0.5f));
            RectTransform handle = Rect(handleArea, "handle", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            handle.sizeDelta = new Vector2(Knob, 0f);
            var hi2 = handle.gameObject.AddComponent<Image>();
            hi2.sprite = Disc;
            hi2.color = Palette.Ink;

            var s = rt.gameObject.AddComponent<Slider>();
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

        /// <summary>A plate and its edge, stretched over the whole of <paramref name="rt"/>.</summary>
        public static Image Framed(RectTransform rt, Color fill, Color edge)
        {
            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = RoundFill;
            plate.type = Image.Type.Sliced;
            plate.color = fill;

            RectTransform er = Rect(rt, "edge", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var line = er.gameObject.AddComponent<Image>();
            line.sprite = RoundLine;
            line.type = Image.Type.Sliced;
            line.color = edge;
            line.raycastTarget = false;

            return plate;
        }

        /// <summary>
        /// A button is a bracketed label inside a lit frame: [ MENU ]. The brackets
        /// are part of the design system rather than decoration — they say "this is
        /// pressable" in the same monospace the rest of the interface speaks.
        ///
        /// A PRIMARY IS SOLID AND EVERYTHING ELSE IS A HOLE WITH AN EDGE. There is
        /// exactly one primary on any screen, and it is the only filled shape in the
        /// interface, so "the thing to press" needs no arrow, no pulse and no hint
        /// text — it is the one that is on.
        /// </summary>
        public static Button Bracketed(Transform parent, string name, string label,
                                       System.Action onClick, int size = 26, bool primary = false)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // OPAQUE, ALWAYS. The pause card is deliberately translucent — it sits
            // over a puzzle somebody is in the middle of and should let it through —
            // but that is the CARD's job, not its buttons'. A translucent plate put
            // the board inside the controls, so the cube was visibly running through
            // the middle of the words on them. A control is a solid thing you
            // press; whatever is behind the screen stops at its edge.
            // THE GLOW GOES BEHIND, WHICH MEANS BESIDE. UGUI draws a parent's own
            // graphic before its children, so a child can never be behind its
            // parent's plate — the glow has to be a sibling, ordered first.
            if (primary)
            {
                RectTransform gr = Rect(parent, name + "_glow", Vector2.zero, Vector2.one,
                                        new Vector2(-GlowReach, -GlowReach), new Vector2(GlowReach, GlowReach));
                var g = gr.gameObject.AddComponent<Image>();
                g.sprite = GlowFill;
                g.type = Image.Type.Sliced;
                g.color = new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.70f);
                g.raycastTarget = false;
                gr.SetAsFirstSibling();
                rt.SetAsLastSibling();
            }

            Image plate = Framed(rt, primary ? Palette.Rust : Palette.Panel,
                                     primary ? EdgeOn : Edge);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            // --t-sm on a control, --t-md on a primary; black on the rust, because
            // the shipped rule is literally color:#000
            Label(rt, "label", "[ " + label + " ]", size,
                  primary ? Palette.Void : Palette.Ink, TextAnchor.MiddleCenter,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>
        /// A PRESSABLE THING THAT IS NOT A BUTTON.
        ///
        /// Three framed buttons in a column are three equal offers, and a card that
        /// offers three equal things has not decided what it is for. The one action
        /// the screen exists for keeps its plate; everything else becomes a
        /// bracketed label with nothing behind it — still obviously pressable,
        /// because the brackets are what say so, and audibly quieter because it has
        /// no weight on the page.
        ///
        /// The invisible Image is not decoration either: a Text alone has nothing
        /// for the raycaster to hit across its whole rect, so the target would be
        /// the glyphs and not the gap between them.
        /// </summary>
        public static Button Link(Transform parent, string name, string label,
                                  System.Action onClick, int size = 22, Color? tint = null)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;

            Text t = Label(rt, "label", "[ " + label + " ]", size, tint ?? Palette.Dim,
                           TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>
        /// MOVE THE ONE LIT THING, without rebuilding either control.
        ///
        /// A screen has exactly one primary and it is meant to be "the thing you
        /// came here to do" — but on a screen where that CHANGES, a primary baked
        /// in at construction is a promise the screen cannot keep. The Forge is
        /// the case: its coach names the next step, and the step is VERIFY until
        /// the cube is proved and SAVE afterwards, while the plate sat on SAVE
        /// from the moment the editor opened. A button lit before it can do
        /// anything is worse than no primary at all.
        ///
        /// Only a control BUILT primary can be lit again later — the glow is a
        /// sibling object that is created on request — so a screen that intends to
        /// move its primary builds both that way and switches one off.
        /// </summary>
        public static void SetPrimary(Button b, bool on)
        {
            if (b == null) return;

            var plate = b.GetComponent<Image>();
            if (plate != null) plate.color = on ? Palette.Rust : Palette.Panel;

            Transform edge = b.transform.Find("edge");
            if (edge != null)
            {
                var e = edge.GetComponent<Image>();
                if (e != null) e.color = on ? EdgeOn : Edge;
            }

            Transform label = b.transform.Find("label");
            if (label != null)
            {
                var t = label.GetComponent<Text>();
                if (t != null) t.color = on ? Palette.Void : Palette.Ink;
            }

            // the glow is a SIBLING, because a child can never draw behind its
            // parent's own graphic — see Bracketed
            Transform parent = b.transform.parent;
            if (parent != null)
            {
                Transform glow = parent.Find(b.gameObject.name + "_glow");
                if (glow != null && glow.gameObject.activeSelf != on) glow.gameObject.SetActive(on);
            }
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = "[ " + text + " ]";
        }

        /// <summary>
        /// The only free text in the game: a cube's name, and a pasted share code.
        /// Both are folded to the character set the interface already speaks before
        /// they go anywhere, which is a house-style decision that also means no
        /// authored string can ever be markup.
        /// </summary>
        public static InputField Field(Transform parent, string name, string placeholder,
                                       Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            Image bg = Framed(rt, Palette.Panel, Edge);

            Text text = Label(rt, "text", "", 22, Palette.Ink, TextAnchor.MiddleLeft,
                              Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            text.raycastTarget = false;
            text.supportRichText = false;

            Text hint = Label(rt, "placeholder", placeholder, 20, Palette.Dim2, TextAnchor.MiddleLeft,
                              Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            hint.raycastTarget = false;

            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>
        /// THE TITLE SCREEN IS A STAGE, NOT A PANE OF GLASS.
        ///
        /// A flat scrim over the whole screen darkens the one thing the screen is
        /// actually about — the cube turning behind it. So the chrome occupies a
        /// band at each end and THE MIDDLE IS LEFT COMPLETELY ALONE: nothing at all
        /// between the player and the object. The gradient only darkens where words
        /// have to sit on top of it.
        ///
        /// The stops are the original's, and they are load-bearing rather than
        /// tasteful — the two zero-alpha stops in the middle are what make it a
        /// stage instead of a tint.
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

            // position down the screen -> alpha of black
            // The clear window sits LOWER than the original's, because this port
            // prints two extra lines at the top — the vault and the cube it will
            // resume — and the pitch underneath them was landing at a third of the
            // way down, where the old ramp had already gone transparent. Type over
            // an unscrimmed turning cube is type nobody can read.
            float[] stopAt = { 0f, 0.18f, 0.36f, 0.46f, 0.56f, 0.66f, 0.76f, 1f };
            float[] stopA  = { 0.96f, 0.88f, 0.16f, 0f, 0f, 0.34f, 0.90f, 1f };

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
            img.sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 1, N), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            return img;
        }

        /// <summary>
        /// A SCREEN THAT DOES NOT FIT IS NOT A LAYOUT PROBLEM, IT IS A SCROLL.
        ///
        /// The manual was tuned until it fitted a 1280-tall canvas, which is a
        /// number, not a phone: a short device or a large system font puts the last
        /// two entries under the button again, and the only honest answer to "how
        /// much text is there" is "as much as there is". Everything below goes in a
        /// clipped viewport with a content rect that is however tall it turns out
        /// to be.
        ///
        /// Returns the CONTENT to fill; call <see cref="EndScroll"/> with its final
        /// height when you have finished filling it.
        /// </summary>
        public static RectTransform Scroll(RectTransform parent, string name,
                                           Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform view = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);

            // the mask needs something to raycast against or the drag never starts,
            // and it has to be invisible or it is a panel
            var catcher = view.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect(view, "content", new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
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
            return content;
        }

        /// <summary>Tell a scroll how tall its content turned out to be.</summary>
        public static void EndScroll(RectTransform content, float height)
            => content.sizeDelta = new Vector2(0f, height);

        /// <summary>One of the drawn marks from <see cref="Glyphs"/>, in the interface.</summary>
        public static Image Icon(Transform parent, string role, Color col, float size,
                                 Vector2 anchor, Vector2 offset)
        {
            RectTransform rt = Rect(parent, role, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = offset;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Glyphs.For(role);
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A hairline. One pixel of rust at low alpha, the only border in the game.</summary>
        public static Image Rule(Transform parent, float y, float alpha = 0.34f)
            => Panel(parent, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, alpha),
                     new Vector2(0, y), new Vector2(1, y), new Vector2(24, -1), new Vector2(-24, 1));
    }
}
