using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// THE MACHINE YOU ARE HOLDING.
    ///
    /// Everything else in this interface is a reading. This is the thing the
    /// readings are mounted in: a worn gunmetal panel with the screen recessed
    /// into it, rails down both sides and rivets holding it together.
    ///
    /// It is not decoration, and the argument for it is the same one the aperture
    /// made. The fiction is that you are inside a broken machine looking at its
    /// diagnostics. Every screen in this game already reads as an instrument
    /// panel — framed plates, hairlines, one lit control, a monospace face — and
    /// then that panel was floating on nothing. Chrome with no chassis is a
    /// costume; give it a housing and the same pixels become a device.
    ///
    /// THREE RULES, AND THEY ARE WHAT KEEP IT HONEST.
    ///
    /// 1. NO TYPE EVER SITS ON THE METAL. The whole access audit is a set of
    ///    contrast measurements against four dark grounds, and a light grey
    ///    ground would invalidate every one of them. The metal is a border and a
    ///    rail; the words live on the screen, which is as dark as it ever was.
    ///
    /// 2. IT IS NEVER BEHIND THE BOARD. The canvases are screen-space overlays
    ///    and draw after the camera, so anything painted across the middle would
    ///    paint over the cube. The chassis is four strips around the edge and the
    ///    centre is untouched — a void column is still unrendered space.
    ///
    /// 3. THE GREYS ARE MATERIAL, NOT DATA, which is why they are here and not in
    ///    Palette. That file opens by forbidding a tenth colour, and it is right
    ///    to: every value in it is a claim about what a thing IS. These are a
    ///    claim about what the panel is MADE OF, which is a different kind of
    ///    statement and does not get to sit in the same list.
    ///
    /// Generated rather than imported, like every other texture here — the glyphs,
    /// the frames, the glow, the icon. One 128px tile, seamless by construction,
    /// and the whole panel is that tile repeated.
    /// </summary>
    public static class Chassis
    {
        // ---- the material ----------------------------------------------------

        /// <summary>Gunmetal, and the range wear moves it through.</summary>
        static readonly Color Base = Palette.Hex("#4b515a");
        static readonly Color Deep = Palette.Hex("#31363d");   // grime, and the shadow in a groove
        static readonly Color Worn = Palette.Hex("#6f7883");   // rubbed back toward bare metal
        static readonly Color Lip  = Palette.Hex("#939ba6");   // a scratch, catching the light

        const int Tile = 128;

        static Sprite _metal;

        public static Sprite Metal { get { if (_metal == null) _metal = BuildMetal(); return _metal; } }

        // ---- noise -----------------------------------------------------------
        //
        // TILEABLE BY CONSTRUCTION, which is the only reason the panel can be one
        // 128px texture repeated instead of a screen-sized one. The lattice
        // coordinates are taken modulo the period before they are hashed, so the
        // value at x = period is the value at x = 0 and the seam has nowhere to
        // appear. Perlin would not do this: Mathf.PerlinNoise has no period.

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0xFFFFFF;
        }

        static int Wrap(int v, int period) => ((v % period) + period) % period;

        /// <summary>
        /// A PERIOD PER AXIS, and the reason is a seam.
        ///
        /// Brushed metal is noise stretched hard along one axis — a hundred and
        /// ten across, two along — and a single period cannot describe that. With
        /// one, the layer wraps on the axis whose scale happens to match it and
        /// runs off the end of the other, which puts a faint tonal step across
        /// every tile boundary. It is invisible in a 128px swatch and a row of
        /// stripes once the panel repeats it five times.
        /// </summary>
        static float Value(float x, float y, int px, int py)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);                  // smoothstep, so the lattice does not show
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(Wrap(x0, px), Wrap(y0, py));
            float b = Hash(Wrap(x0 + 1, px), Wrap(y0, py));
            float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py));
            float d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py));

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Octaves, each at twice the frequency and half the weight.</summary>
        static float Fbm(float u, float v, int lowest, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = lowest;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u * period, v * period, period, period) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>
        /// One texel of the panel, and it is PUBLIC so it can be looked at.
        ///
        /// The pixel maths is separated from the Texture2D it fills for one
        /// reason: a texture is the one thing in this project that cannot be
        /// judged by reading it. So the stub harness calls this — the real
        /// function, not a copy of it — and writes a PNG, which is how the brush
        /// frequency and the wear weight were chosen rather than guessed.
        ///
        /// It was very nearly a second implementation in another language, which
        /// is exactly the "two code paths, one truth" the README complains about
        /// in the web build's two renderers. One path. The preview cannot drift
        /// from the game because it IS the game.
        /// </summary>
        public static Color Texel(int x, int y)
        {
            float u = x / (float)Tile, v = y / (float)Tile;

            // BRUSHED, AND THAT IS THE WHOLE LOOK. A hundred and ten
            // across against two along: noise stretched that hard stops
            // being noise and becomes a machined surface. It carries most
            // of the weight here, and the first two attempts at this file
            // failed because it did not.
            float brush = Value(u * 110f, v * 2f, 110, 2) - 0.5f;

            // a second, coarser pass so the grain is not one frequency
            float fine = Value(u * 42f, v * 3f, 42, 3) - 0.5f;

            // WORN IN PATCHES, BUT QUIETLY. Soft blotches say where the
            // panel has been handled. Kept low: the tile repeats about
            // five times across a band, and a strong blotch is the one
            // feature big enough to make that repeat visible.
            float wear = Fbm(u, v, 4, 4);

            // and the per-pixel grain that stops any of it looking like a gradient
            float grain = Hash(x, y) - 0.5f;

            float k = Mathf.Clamp01(0.47f + (wear - 0.5f) * 0.26f
                                    + brush * 0.38f + fine * 0.14f + grain * 0.06f);

            // Below the midpoint it darkens toward grime; above it, it
            // rubs back toward bare metal. Two ramps rather than one keeps
            // the middle — which is most of the panel — near the base
            // colour instead of washing the whole thing out.
            Color c = k < 0.5f
                ? Color.Lerp(Deep, Base, k * 2f)
                : Color.Lerp(Base, Worn, (k - 0.5f) * 2f);

            // SCRATCHES, RUNNING WITH THE GRAIN. Where another stretched
            // band crosses a high threshold, lift it toward bare metal.
            // No line drawing and no bookkeeping, and they wrap because
            // the noise under them wraps. Across the grain they read as
            // scanlines, which is worth knowing: the first pass had them
            // that way round and the panel looked like a CRT.
            float streak = Value(u * 72f, v * 2f, 72, 2);
            if (streak > 0.90f) c = Color.Lerp(c, Lip, (streak - 0.90f) / 0.10f * 0.40f);

            return c;
        }

        /// <summary>The tile, one call to <see cref="Texel"/> per pixel.</summary>
        public const int TileSize = Tile;

        static Sprite BuildMetal()
        {
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                name = "chassis"
            };

            var px = new Color32[Tile * Tile];
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                    px[y * Tile + x] = Texel(x, y);

            tex.SetPixels32(px);
            tex.Apply(false, true);

            // ONE PIXEL PER UNIT, so a tiled Image repeats every 128 canvas units
            // and the grain is the same size on every panel it is stretched over.
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, Tile, Tile), new Vector2(0.5f, 0.5f), 1f);
        }

        // ---- the panel -------------------------------------------------------

        /// <summary>
        /// The housing, on its own canvas behind everything.
        ///
        /// Four strips and not one plate with a hole in it: the middle has to stay
        /// genuinely untouched, because the board is drawn by the camera and every
        /// canvas here is an overlay on top of it. The two rails run the full
        /// height and the two bands run between them, so the corners are covered
        /// once and there is no seam to line up.
        /// </summary>
        public static Canvas Build()
        {
            Canvas c = Singularity.UI.UiKit.Canvas("Chassis", 5);
            var root = Singularity.UI.UiKit.Rect(c.transform, "panel",
                                                 Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            float e = Layout.ChassisEdge;

            Strip(root, "railL", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(e, 0));
            Strip(root, "railR", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-e, 0), new Vector2(0, 0));
            Strip(root, "bandT", new Vector2(0, 1), new Vector2(1, 1), new Vector2(e, -e), new Vector2(-e, 0));
            Strip(root, "bandB", new Vector2(0, 0), new Vector2(1, 0), new Vector2(e, 0), new Vector2(-e, e));

            // THE RECESS. One dark line all the way round the opening, which is
            // the shadow the screen sits down into. Without it the metal and the
            // screen are two flat shapes that happen to be adjacent; with it the
            // screen is BELOW the metal, and that is the whole illusion.
            Singularity.UI.UiKit.Panel(root, "recess", new Color(0f, 0f, 0f, 0.85f),
                                       Vector2.zero, Vector2.one,
                                       new Vector2(e - 3f, e - 3f), new Vector2(-(e - 3f), -(e - 3f)));

            Rivets(root, e);
            return c;
        }

        static void Strip(RectTransform under, string name,
                          Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            RectTransform rt = Singularity.UI.UiKit.Rect(under, name, aMin, aMax, oMin, oMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Metal;
            // TILED, NOT STRETCHED. A 128px grain stretched down a 28-unit rail is
            // a smear; repeated, it is the same metal at the same scale everywhere,
            // which is what makes the four strips read as one panel.
            img.type = Image.Type.Tiled;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Rivets down both rails. Two quads each — a dark seat and a lit head
        /// pushed up-left — because a rivet is only ever read as "round thing with
        /// the light on one side of it", and at this size that is two circles.
        /// </summary>
        static void Rivets(RectTransform under, float e)
        {
            const int Count = 7;
            float d = Mathf.Min(e * 0.46f, 13f);

            for (int i = 0; i < Count; i++)
            {
                float t = (i + 0.5f) / Count;
                Rivet(under, "rvL" + i, new Vector2(0, t), e * 0.5f, d);
                Rivet(under, "rvR" + i, new Vector2(1, t), -e * 0.5f, d);
            }
        }

        static void Rivet(RectTransform under, string name, Vector2 anchor, float x, float d)
        {
            void Disc(string n, Color col, float dx, float dy, float size)
            {
                RectTransform rt = Singularity.UI.UiKit.Rect(under, n, anchor, anchor, Vector2.zero, Vector2.zero);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(x + dx, dy);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = Singularity.UI.UiKit.Disc;
                img.color = col;
                img.raycastTarget = false;
            }

            Disc(name + "s", new Color(0f, 0f, 0f, 0.62f), 0f, -1f, d);
            Disc(name + "h", new Color(Lip.r, Lip.g, Lip.b, 0.85f), 0f, 0.5f, d * 0.72f);
        }
    }
}
