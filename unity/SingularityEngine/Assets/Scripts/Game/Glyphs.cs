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

        static Material _mat;

        /// <summary>
        /// One shared material for every glyph. It draws with the depth test off,
        /// because whether a glyph is visible has ALREADY been decided by the
        /// projection — see Singularity/Glyph.shader.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (_mat != null) return _mat;
                Shader sh = Shader.Find("Singularity/Glyph");
                if (sh == null) { Debug.LogError("Singularity/Glyph shader missing"); sh = Shader.Find("Sprites/Default"); }
                _mat = new Material(sh) { name = "glyph" };
                return _mat;
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
                    float a = 0f;
                    const int SS = 3;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / SS) / R) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / SS) / R) * 2f - 1f;
                            a += Sample(role, u, v);
                        }
                    a /= SS * SS;
                    px[y * R + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp01(a).ScaleTo255());
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
                default: // "player" — the singularity itself
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    float horizon = Band(r, 0.74f, 0.11f);
                    // the hole is a HOLE: opaque black is drawn by the renderer's
                    // own dark colour behind it, so the sprite carries only the
                    // horizon and a soft accretion edge
                    float glow = Mathf.Clamp01(1f - Mathf.Abs(r - 0.90f) / 0.16f) * 0.35f;
                    float disc = r < 0.66f ? 1f : 0f;
                    return Mathf.Max(Mathf.Max(horizon, glow), disc * 0.92f);
                }
            }
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
