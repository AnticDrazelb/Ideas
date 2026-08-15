using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// THE DISPLAY HAS ROWS.
    ///
    /// The case says the interface is mounted in a machine and the glass says
    /// there is something between you and it. This says the picture itself is
    /// being SCANNED — that what you are reading is a signal on a panel with a
    /// line pitch, rather than a page. It is the cheapest of the three effects
    /// and it does the most per unit: an image with rows in it is a readout, and
    /// the same image without them is a screenshot.
    ///
    /// IT GOES UNDER THE GLASS AND OVER EVERYTHING ELSE, which is the whole of
    /// its place in the stack and is not arbitrary. The rows are the panel, so
    /// they are behind the dirt on the outside of the pane and in front of every
    /// pixel the panel is displaying — the board, the readout, and any menu that
    /// has come forward. Chassis 5, HUD 10, screens 20, this 24, glass 25.
    ///
    /// IT CAN ONLY DARKEN, AND ONLY EVERY THIRD ROW. Black at a third alpha over
    /// one row in three, which is an eleven percent dimming averaged over the
    /// eye and nothing at all on a ground that is already black. That matters
    /// because the access audit measures the interface's type against four dark
    /// grounds: a layer that lifted the ground would flatten every one of those
    /// pairs, and a layer that dims the foreground by a ninth moves Ink on Void
    /// from 16.8:1 to 15.0:1 — still more than twice the AAA floor.
    ///
    /// AND IT IS BUILT IN DEVICE PIXELS, not canvas units, which is the only
    /// detail here worth any care. A three-unit pitch stretched by a canvas
    /// scaler that is not an integer gives you lines that are two pixels wide in
    /// one band of the screen and one in the next, and the beat between them
    /// crawls when anything moves. So the texture is one texel wide by exactly as
    /// many rows as the opening has device pixels, drawn at one to one, and the
    /// pitch is rounded to whole pixels once. It is rebuilt when the display
    /// changes shape and never otherwise.
    /// </summary>
    public static class Scanlines
    {
        /// <summary>Line pitch and line weight, in canvas units. Rounded to whole device pixels.</summary>
        public const float Pitch = 3f, Weight = 1f;

        /// <summary>
        /// How far a line darkens what is under it.
        ///
        /// Chosen by looking, at a fifth and at two fifths, against a stand-in
        /// board of lit cells. A fifth is invisible on anything but the brightest
        /// cell; two fifths starts to read as blinds over the picture rather than
        /// as the picture being scanned. This is a third, which shows on a lit
        /// cell and disappears on the void.
        /// </summary>
        public const float Strength = 0.34f;

        static GameObject _pane;

        /// <summary>The rows, on their own canvas under the glass.</summary>
        public static Canvas Build()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Scanlines", 24);

            // a full-bleed child first, because UiKit.SafeArea zeroes the offsets
            // of anything parented straight to a canvas — see the note there
            RectTransform safe = Singularity.UI.UiKit.Rect(c.transform, "safe",
                                                           Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform rt = Singularity.UI.UiKit.Rect(safe, "rows",
                                                         Vector2.zero, Vector2.one,
                                                         new Vector2(Chassis.InsetLeft, Chassis.InsetBottom),
                                                         new Vector2(-Chassis.InsetRight, -Chassis.InsetTop));
            _pane = rt.gameObject;

            var img = rt.gameObject.AddComponent<Image>();
            img.type = Image.Type.Simple;
            img.color = Color.white;          // the texture carries the whole effect
            img.raycastTarget = false;        // it is in front of every button on the screen

            var fit = rt.gameObject.AddComponent<Fit>();
            fit.Init(c, img);

            Refresh();
            return c;
        }

        /// <summary>Off under legibility, on otherwise. Safe to call before Build.</summary>
        public static void Refresh()
        {
            if (_pane == null) return;   // == null, not ?., see UiKit.Ensure
            bool want = !Access.Legible;
            if (_pane.activeSelf != want) _pane.SetActive(want);
        }

        /// <summary>
        /// One texel wide by one device pixel per row, rebuilt when that number
        /// changes. The same shape of watcher the chassis uses, and for the same
        /// reason: this cannot be drawn until something has a size, and the size
        /// is not known in Awake.
        /// </summary>
        class Fit : MonoBehaviour
        {
            Canvas _canvas;
            Image _img;
            Texture2D _tex;
            int _rows;

            public void Init(Canvas c, Image img) { _canvas = c; _img = img; }

            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                float scale = _canvas.scaleFactor;
                if (scale <= 0f) return;

                int rows = Mathf.RoundToInt(rt.rect.height * scale);
                if (rows < 8 || rows == _rows) return;
                _rows = rows;
                Paint(rows, scale);
            }

            void Paint(int rows, float scale)
            {
                // whole pixels, and at least one lit row between every dark one,
                // or a pitch of two on a low-density display would be a grey wash
                int pitch = Mathf.Max(2, Mathf.RoundToInt(Pitch * scale));
                int weight = Mathf.Clamp(Mathf.RoundToInt(Weight * scale), 1, pitch - 1);

                if (_tex != null) Object.Destroy(_tex);

                var tex = new Texture2D(1, rows, TextureFormat.RGBA32, false)
                {
                    // POINT, NOT BILINEAR. At one to one there is nothing to
                    // interpolate, and a half-pixel of drift should move which row
                    // is dark rather than smear every line across two.
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "scanlines"
                };

                var px = new Color32[rows];
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(Strength) * 255f);
                for (int y = 0; y < rows; y++)
                {
                    // counted from the TOP, so the pattern is anchored to the top
                    // of the opening and does not shuffle when the height changes
                    int down = rows - 1 - y;
                    px[y] = new Color32(0, 0, 0, (down % pitch) < weight ? a : (byte)0);
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);

                _tex = tex;
                _img.sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 1, rows),
                                            new Vector2(0.5f, 0.5f), 1f);
            }
        }
    }
}
