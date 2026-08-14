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
        public static Font Mono => _mono ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                          ?? Font.CreateDynamicFontFromOSFont("Courier New", 16);
        static Font _mono;

        public static Canvas Canvas(string name, int order)
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
            go.AddComponent<SafeArea>();
            return c;
        }

        /// <summary>
        /// A NOTCH IS NOT A SUGGESTION.
        ///
        /// The web original has a long note about this: env(safe-area-inset-*) reads
        /// zero inside an Android WebView however correctly the host sets
        /// viewport-fit, because a WebView is a View inside somebody else's
        /// Activity. Unity does not have that problem — Screen.safeArea is the real
        /// measurement — but it does have the same requirement, and a HUD band
        /// under a camera cutout is just as unreadable either way.
        ///
        /// Applied to the canvas root rather than to each control, so every screen
        /// inherits it and no layout has to think about it.
        /// </summary>
        class SafeArea : MonoBehaviour
        {
            RectTransform _rt;
            Rect _last;

            void Awake()
            {
                // the canvas's own rect is the whole screen; everything else is
                // parented under a child that is inset to the safe area
                _rt = (RectTransform)transform;
            }

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

        public static Text Label(Transform parent, string name, string text, int size,
                                 Color col, TextAnchor anchor,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            RectTransform rt = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Mono;
            t.fontSize = size;
            t.text = text;
            t.color = col;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
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

        const int FrameTex = 64, FrameRad = 16, FrameSlice = 20, FrameStroke = 3;

        static Sprite _fill, _line;

        /// <summary>The plate: a filled rounded rectangle.</summary>
        public static Sprite RoundFill => _fill ??= BuildFrame(false);

        /// <summary>The edge: the same rectangle as a stroke, and the thing that lights up.</summary>
        public static Sprite RoundLine => _line ??= BuildFrame(true);

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
            // the middle of the words VAULTS and BOARDS. A control is a solid thing
            // you press; whatever is behind the screen stops at its edge.
            Image plate = Framed(rt,
                primary ? Palette.Rust : Palette.Panel,
                primary ? Palette.RustHi : new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.70f));

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = plate;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            Label(rt, "label", "[ " + label + " ]", size,
                  primary ? Palette.Void : Palette.Ink, TextAnchor.MiddleCenter,
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
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
            Image bg = Framed(rt, Palette.Panel,
                                  new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.45f));

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
            float[] stopAt = { 0f, 0.13f, 0.27f, 0.42f, 0.52f, 0.62f, 0.74f, 1f };
            float[] stopA  = { 0.96f, 0.80f, 0.12f, 0f, 0f, 0.32f, 0.90f, 1f };

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

            var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 1, N), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            return img;
        }

        /// <summary>A hairline. One pixel of rust at low alpha, the only border in the game.</summary>
        public static Image Rule(Transform parent, float y, float alpha = 0.34f)
            => Panel(parent, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, alpha),
                     new Vector2(0, y), new Vector2(1, y), new Vector2(24, -1), new Vector2(-24, 1));
    }
}
