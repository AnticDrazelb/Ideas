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

        /// <summary>
        /// A button is a bracketed label on a panel, because that is what every
        /// control in this game looks like: [ MENU ]. The brackets are part of the
        /// design system, not decoration — they say "this is pressable" without
        /// needing a border to do it.
        /// </summary>
        public static Button Bracketed(Transform parent, string name, string label,
                                       System.Action onClick, int size = 26, bool primary = false)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = primary ? new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, 0.16f) : Palette.Panel;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            Label(rt, "label", "[ " + label + " ]", size,
                  primary ? Palette.RustHi : Palette.Ink, TextAnchor.MiddleCenter,
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
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Palette.Panel;

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

        /// <summary>A hairline. One pixel of rust at low alpha, the only border in the game.</summary>
        public static Image Rule(Transform parent, float y, float alpha = 0.34f)
            => Panel(parent, "rule", new Color(Palette.Rust.r, Palette.Rust.g, Palette.Rust.b, alpha),
                     new Vector2(0, y), new Vector2(1, y), new Vector2(24, -1), new Vector2(-24, 1));
    }
}
