using System;
using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// The four objects, drawn rather than loaded.
    ///
    /// A packaged app should not pay a download for four shapes it can draw, and
    /// the shapes are the ones the manual teaches: a diamond is a NODE, a slashed
    /// hex is a LOCK, concentric rings are the CORE, and the filled disc with a
    /// horizon around it is YOU. They are generated once into textures at a size
    /// that stays sharp on a dense phone screen, in white, and tinted at the
    /// SpriteRenderer — because the colour of a thing is data (see Palette) and
    /// belongs to the thing, not to its picture.
    /// </summary>
    public static class Glyphs
    {
        const int R = 128;   // texture resolution; one glyph is one cell wide

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Material _mat, _add;

        static Shader Sh()
        {
            Shader sh = Shader.Find("Singularity/Glyph");
            if (sh == null) { Debug.LogError("Singularity/Glyph shader missing"); sh = Shader.Find("Sprites/Default"); }
            return sh;
        }

        /// <summary>
        /// One shared material for every glyph. It draws with the depth test off,
        /// because whether a glyph is visible has ALREADY been decided by the
        /// projection — see Singularity/Glyph.shader.
        /// </summary>
        public static Material Material => _mat ??= new Material(Sh()) { name = "glyph" };

        /// <summary>
        /// The same shader set to add rather than cover, for the two parts of the
        /// marker that are LIGHT — the photon ring and the lensing halo. They sit
        /// on top of a hole that has already removed the board, so anything less
        /// than additive would be a bright ring painted ON something rather than
        /// light bending around an absence.
        /// </summary>
        public static Material Additive
        {
            get
            {
                if (_add != null) return _add;
                _add = new Material(Sh()) { name = "glyph+" };
                _add.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _add.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                return _add;
            }
        }

        public static Sprite For(string role)
        {
            if (Cache.TryGetValue(role, out Sprite s)) return s;
            s = Build(role);
            Cache[role] = s;
            return s;
        }

        static Sprite Build(string role)
        {
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "glyph_" + role
            };

            var px = new Color32[R * R];
            for (int y = 0; y < R; y++)
                for (int x = 0; x < R; x++)
                {
                    // sample on a -1..1 square, with a little supersampling so the
                    // diagonals do not stair-step at small sizes
                    var acc = Color.clear;
                    const int SS = 3;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / SS) / R) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / SS) / R) * 2f - 1f;
                            acc += role == "halo" ? Halo(u, v)
                                                  : new Color(1f, 1f, 1f, Sample(role, u, v));
                        }
                    acc /= SS * SS;
                    px[y * R + x] = new Color32((byte)acc.r.ScaleTo255(), (byte)acc.g.ScaleTo255(),
                                                (byte)acc.b.ScaleTo255(), (byte)acc.a.ScaleTo255());
                }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, R, R), new Vector2(0.5f, 0.5f), R);
        }

        static float Sample(string role, float u, float v)
        {
            switch (role)
            {
                case "node":
                {
                    // a diamond, with a solid pip at its centre
                    float d = Mathf.Abs(u) + Mathf.Abs(v);
                    float ring = Band(d, 0.86f, 0.13f);
                    float pip = d < 0.34f ? 1f : 0f;
                    return Mathf.Max(ring, pip);
                }
                case "lock":
                {
                    // a hexagonal barrier with two bars struck through it
                    float hex = Hex(u, v);
                    float ring = Band(hex, 0.86f, 0.12f);
                    float bar1 = Bar(u, v, -0.16f, 0.085f);
                    float bar2 = Bar(u, v, 0.16f, 0.085f);
                    float bars = Mathf.Max(bar1, bar2) * (hex < 0.9f ? 1f : 0f);
                    return Mathf.Max(ring, bars);
                }
                case "core":
                {
                    // the way out: an aperture with a horizon, plus four ticks
                    float r = Mathf.Sqrt(u * u + v * v);
                    float outer = Band(r, 0.92f, 0.07f) * 0.5f;
                    float mid = Band(r, 0.62f, 0.09f);
                    float centre = r < 0.22f ? 1f : 0f;
                    float ticks = 0f;
                    if (Mathf.Abs(v) < 0.05f && Mathf.Abs(u) > 0.72f && Mathf.Abs(u) < 0.99f) ticks = 1f;
                    if (Mathf.Abs(u) < 0.05f && Mathf.Abs(v) > 0.72f && Mathf.Abs(v) < 0.99f) ticks = 1f;
                    return Mathf.Max(Mathf.Max(outer, mid), Mathf.Max(centre, ticks));
                }
                case "plate":
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ring = Band(r, 0.80f, 0.10f);
                    float slash = Bar(u, v, 0f, 0.075f) * (r < 0.84f ? 1f : 0f);
                    return Mathf.Max(ring, slash);
                }
                case "eye":
                {
                    // a rounded slab, so squashing it for a blink or a smile still
                    // reads as an eye rather than as a line
                    float d = Mathf.Max(Mathf.Abs(u) - 0.45f, 0f);
                    float r = Mathf.Sqrt(d * d + v * v);
                    return r < 0.82f ? 1f : Mathf.Clamp01(1f - (r - 0.82f) / 0.12f);
                }
                case "ring":
                {
                    // THE EDGE DOES THE FINDING. One thin, very bright circle right
                    // on the silhouette. On a pale deck this ring is the whole
                    // difference between "a hole in the world" and "a hole in the
                    // floor", and it is the only reason a black object is allowed to
                    // be the player marker in a game where dark already means
                    // impassable.
                    float r = Mathf.Sqrt(u * u + v * v);
                    return Band(r, 0.965f, 0.0525f);      // lineWidth rr*0.105
                }
                case "arc":
                {
                    // Brighter over one arc, travelling: the whole of "it is turning".
                    float r = Mathf.Sqrt(u * u + v * v);
                    float band = Band(r, 0.965f, 0.025f);  // lineWidth rr*0.05
                    if (band <= 0f) return 0f;
                    float a = Mathf.Atan2(v, u);
                    if (a < 0f) a += Mathf.PI * 2f;
                    // two radians of it, feathered at both ends rather than butt-capped
                    const float Span = 2.0f, Fade = 0.10f;
                    if (a > Span + Fade) return 0f;
                    float ends = Mathf.Min(Mathf.Clamp01(a / Fade), Mathf.Clamp01((Span + Fade - a) / Fade));
                    return band * ends;
                }
                default: // "hole" — the event horizon itself
                {
                    // A HOLE, NOT A BALL. This is drawn opaque and black, so it does
                    // not cover the deck it stands on — it REMOVES it, down to the
                    // same nothing the void columns are. A dark disc would be a spot
                    // of black ON the board; this is an absence OF board, and the eye
                    // reads the difference long before it can name it.
                    float r = Mathf.Sqrt(u * u + v * v);
                    return r < 0.985f ? 1f : Mathf.Clamp01(1f - (r - 0.985f) / 0.015f);
                }
            }
        }

        /// <summary>
        /// THE LENSING HALO, and the one detail that decides whether any of this works.
        ///
        /// A radial gradient FILLS ITS INNER CIRCLE with the stop-0 colour, so a
        /// "donut" whose first stop is bright is a filled disc with extra steps —
        /// which is exactly what floods the horizon and turns the one black thing on
        /// the board into a slate-grey disc. Stop 0 is transparent; the light starts
        /// a hair outside it. The original carries a paragraph about getting this
        /// wrong, and it is worth carrying the fix across with it.
        ///
        /// Sampled so r=1 is 2.1 marker radii, which is where the halo ends.
        /// </summary>
        static Color Halo(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v) * 2.1f;      // back into marker radii
            float t = (r - 0.86f) / (2.1f - 0.86f);
            if (t <= 0f || t >= 1f) return Color.clear;

            Color c0 = new Color(0.133f, 0.827f, 0.933f, 0f);        // #22d3ee, transparent
            Color c1 = new Color(0.133f, 0.827f, 0.933f, 0.34f);
            Color c2 = new Color(0.055f, 0.647f, 0.914f, 0.15f);     // #0ea5e9
            Color c3 = new Color(0.047f, 0.290f, 0.427f, 0f);        // #0c4a6e, transparent

            if (t < 0.04f) return Color.Lerp(c0, c1, t / 0.04f);
            if (t < 0.22f) return Color.Lerp(c1, c2, (t - 0.04f) / 0.18f);
            return Color.Lerp(c2, c3, (t - 0.22f) / 0.78f);
        }

        static float Band(float value, float at, float halfWidth)
            => Mathf.Clamp01(1f - Mathf.Abs(value - at) / halfWidth);

        static float Hex(float u, float v)
        {
            // distance-ish field for a flat-top hexagon
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            return Mathf.Max(av, au * 0.8660254f + av * 0.5f);
        }

        static float Bar(float u, float v, float offset, float halfWidth)
        {
            // a 45-degree bar, offset along its normal
            float d = Mathf.Abs((u - v) * 0.70710678f - offset);
            return Mathf.Clamp01(1f - d / halfWidth);
        }

        static byte ScaleTo255(this float f) => (byte)Mathf.RoundToInt(Mathf.Clamp01(f) * 255f);
    }
}
