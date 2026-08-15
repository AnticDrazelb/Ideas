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
        static Fit _fit;

        // ---- the panel is alive ----------------------------------------------
        //
        // A case, a pane of dirty glass and a line pitch, and the whole thing
        // perfectly still. Everything in this machine was drawn by somebody and
        // nothing in it is POWERED, which is a difference the eye reads
        // immediately even when it cannot say what it is looking at.
        //
        // Three behaviours, and they are deliberately almost nothing:
        //
        //   THE BREATHE. The rows brighten and fade by a tenth of their own
        //   strength, on two slow waves that do not share a period so it never
        //   settles into a pulse. It is under the threshold at which you would
        //   call it an animation; it is over the one at which a display looks
        //   switched on.
        //
        //   THE ROLL. Once every half a minute or so, a soft band travels down
        //   the glass — the frame sync of something that has not been serviced in
        //   thirty years. Rare enough that it reads as the machine's fault rather
        //   than as a screensaver.
        //
        //   THE WARM-UP. The same band, once, faster and brighter, when the thing
        //   is switched on.
        //
        // ADDITIVE, LIKE THE GRIME. It can only put light on the glass, never
        // take it away, so nothing that was legible stops being — and the whole
        // lot stops dead when motion is set to STILL, which is what that setting
        // is for.

        /// <summary>Light on the glass when the machine is switched on.</summary>
        public static void WarmUp()
        {
            if (_fit != null) _fit.Sweep(1.05f, 0.55f);
        }

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

            _fit = rt.gameObject.AddComponent<Fit>();
            _fit.Init(c, img);

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

            RectTransform _band;      // the roll, parked off-screen until it runs
            Image _bandImg;
            float _t = -1f, _dur, _peak, _next = 24f;

            public void Init(Canvas c, Image img) { _canvas = c; _img = img; }

            /// <summary>Send the band down the glass once.</summary>
            public void Sweep(float dur, float peak)
            {
                if (Access.MotionAmount <= 0f) return;
                _t = 0f; _dur = Mathf.Max(0.2f, dur); _peak = peak;
            }

            void Update()
            {
                float motion = Access.MotionAmount;

                // THE BREATHE. Two waves that do not share a period, so it never
                // lands on a beat you could count.
                float lift = motion <= 0f ? 0f
                    : (Mathf.Sin(Time.unscaledTime * 0.61f) * 0.6f
                     + Mathf.Sin(Time.unscaledTime * 0.23f) * 0.4f) * 0.10f * motion;
                _img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Strength * (1f + lift)));

                if (motion <= 0f) { if (_band != null && _band.gameObject.activeSelf) _band.gameObject.SetActive(false); return; }

                if (_t < 0f)
                {
                    _next -= Time.unscaledDeltaTime;
                    if (_next <= 0f) { Sweep(1.7f, 0.30f); _next = Random.Range(26f, 44f); }
                    return;
                }

                _t += Time.unscaledDeltaTime;
                float k = _t / _dur;
                if (k >= 1f) { _t = -1f; if (_band != null) _band.gameObject.SetActive(false); return; }

                if (_band == null) BuildBand();
                if (!_band.gameObject.activeSelf) _band.gameObject.SetActive(true);

                // down the glass, and fading out as it goes
                _band.anchorMin = new Vector2(0f, 1f - k);
                _band.anchorMax = new Vector2(1f, 1f - k);
                float fade = Mathf.Sin(k * Mathf.PI);   // in and out, no hard ends
                _bandImg.color = new Color(1f, 1f, 1f, _peak * fade * motion);
            }

            void BuildBand()
            {
                _band = Singularity.UI.UiKit.Rect((RectTransform)transform, "roll",
                                                  new Vector2(0f, 1f), new Vector2(1f, 1f),
                                                  Vector2.zero, Vector2.zero);
                _band.pivot = new Vector2(0.5f, 0.5f);
                _band.sizeDelta = new Vector2(0f, 120f);

                // a soft bar: no edges to catch on, because a hard-edged band
                // travelling down a screen is a wipe and this is a fault
                const int H = 64;
                var tex = new Texture2D(1, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "roll"
                };
                var px = new Color32[H];
                for (int y = 0; y < H; y++)
                {
                    float u = y / (float)(H - 1);
                    float a = Mathf.Sin(u * Mathf.PI);
                    px[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);

                _bandImg = _band.gameObject.AddComponent<Image>();
                _bandImg.sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 1f);
                _bandImg.material = Glass.Additive;
                _bandImg.raycastTarget = false;
            }

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
