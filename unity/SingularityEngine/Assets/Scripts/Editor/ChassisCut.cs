using System;

namespace Singularity.EditorTools
{
    /// <summary>
    /// CUTTING A CASE OUT OF A PICTURE OF A MACHINE.
    ///
    /// A straight port of unity/tools/chassis/cut.py, and it is a port rather than
    /// a rewrite on purpose: the Python is still in the repository, it is still the
    /// thing the shipped asset was made with, and having two implementations that
    /// agree PIXEL FOR PIXEL is the only way to know this one is right. The
    /// harness under unity/tools does exactly that comparison.
    ///
    /// NO UnityEngine TYPES IN THIS FILE. It takes bytes and returns bytes. That
    /// is what makes the comparison possible — the checker can run it with no
    /// editor and no engine — and it also keeps the interesting part separable
    /// from the window that drives it, which is mostly sliders.
    ///
    /// THREE THINGS HAPPEN TO THE PICTURE, and the order matters:
    ///
    /// 1. THE BACKGROUND COMES OFF AS A MATTE, NOT AS ALPHA. A flood fill from the
    ///    border across everything below <see cref="Settings.darkBelow"/> finds the
    ///    black around the case and cannot leak inside it, because the case's own
    ///    darkest metal is about twice that. What is left is the silhouette, and it
    ///    is not a rectangle — a notch in the top edge, a waist, a foot, eight
    ///    bolts — and keeping that shape is most of why the housing reads as an
    ///    object rather than a border.
    ///
    ///    It MULTIPLIES the colour instead of cutting the alpha, and the surround
    ///    ships opaque black. That looks like a mistake and is the fix for one: the
    ///    board is drawn by the camera across the whole display and the case is the
    ///    only thing in front of it, so every pixel the silhouette cut away was a
    ///    hole straight through the machine, and mid-fold the cube came out through
    ///    the notch. The camera clears to #000000, so an opaque black surround is
    ///    pixel-for-pixel what was already there.
    ///
    /// 2. THE GLASS COMES OUT. A rounded rectangle, and the only transparent part
    ///    of the asset.
    ///
    /// 3. THE COLOUR BLEEDS OUTWARD UNDER IT. Four passes of eight-neighbour
    ///    averaging into everything the glass made transparent, so that a bilinear
    ///    sample along the inside edge of the bezel fetches metal rather than the
    ///    black that would otherwise be sitting behind the hole.
    /// </summary>
    public static class ChassisCut
    {
        public struct Settings
        {
            /// <summary>Rows of the source to consider. The reference mock-up ends in five rows of pure white.</summary>
            public int rows;

            /// <summary>The case's bounding box in the source: x0, y0 inclusive, x1, y1 exclusive, y down.</summary>
            public int cropX0, cropY0, cropX1, cropY1;

            /// <summary>The recess trough in source pixels, and the radius its corners turn at.</summary>
            public float glassX0, glassY0, glassX1, glassY1, glassRadius;

            /// <summary>Luminance under which a pixel may be background.</summary>
            public float darkBelow;

            /// <summary>Passes of colour bleed under the glass.</summary>
            public int bleed;

            public static Settings Default => new Settings
            {
                rows = 0, darkBelow = 9f, bleed = 4, glassRadius = 15f
            };
        }

        /// <summary>What the cut measured, in the cut art's own pixels.</summary>
        public struct Result
        {
            public byte[] rgba;
            public int width, height;
            public float left, right, top, bottom;
        }

        /// <summary>
        /// The silhouette's bounding box, which is the crop. Verified against the
        /// hand-measured CROP in cut.py: the same four numbers to the pixel, which
        /// is why the tool no longer asks anybody to type them.
        /// </summary>
        public static bool Silhouette(byte[] rgba, int w, int h, float darkBelow,
                                      out bool[] inside, out int x0, out int y0, out int x1, out int y1)
        {
            inside = Flood(rgba, w, h, darkBelow);
            x0 = w; y0 = h; x1 = 0; y1 = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (inside[y * w + x])
                    {
                        if (x < x0) x0 = x;
                        if (x >= x1) x1 = x + 1;
                        if (y < y0) y0 = y;
                        if (y >= y1) y1 = y + 1;
                    }
            return x1 > x0 && y1 > y0;
        }

        /// <summary>
        /// True where the pixel is NOT background: a four-connected flood of
        /// everything under the threshold, started from every border pixel.
        /// </summary>
        static bool[] Flood(byte[] rgba, int w, int h, float darkBelow)
        {
            int n = w * h;
            var dark = new bool[n];
            for (int i = 0; i < n; i++) dark[i] = Luma(rgba, i) < darkBelow;

            var outside = new bool[n];
            var q = new int[n];
            int head = 0, tail = 0;

            void Seed(int i) { if (dark[i] && !outside[i]) { outside[i] = true; q[tail++] = i; } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }

            while (head < tail)
            {
                int i = q[head++];
                int y = i / w, x = i - y * w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (y > 0) Seed(i - w);
                if (y < h - 1) Seed(i + w);
            }

            var inside = new bool[n];
            for (int i = 0; i < n; i++) inside[i] = !outside[i];
            return inside;
        }

        static double Luma(byte[] rgba, int i)
            => rgba[i * 4] * 0.299 + rgba[i * 4 + 1] * 0.587 + rgba[i * 4 + 2] * 0.114;

        /// <summary>
        /// The whole cut. <paramref name="rgba"/> is the source, four bytes per
        /// pixel, row-major from the TOP — which is how a picture is measured and
        /// the opposite of how Unity hands you a texture, so whoever calls this
        /// flips and flips back.
        /// </summary>
        public static Result Run(byte[] rgba, int srcW, int srcH, Settings s)
        {
            int h = s.rows > 0 ? Math.Min(s.rows, srcH) : srcH;
            int w = srcW;
            int n = w * h;

            bool[] inside = Flood(rgba, w, h, s.darkBelow);

            // ---- one texel of feather, so the cut edge is not a staircase ------
            //
            // Separable, vertical then horizontal, and OUT OF RANGE COUNTS AS
            // ZERO — which is not a choice, it is what numpy's convolve does at
            // the ends of an array and therefore what the Python this is checked
            // against does. It only matters in the outermost row and column,
            // where there is nothing but background anyway.
            var mask = new double[n];
            for (int i = 0; i < n; i++) mask[i] = inside[i] ? 1.0 : 0.0;
            var tmp = new double[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double a = y > 0 ? mask[(y - 1) * w + x] : 0.0;
                    double c = y < h - 1 ? mask[(y + 1) * w + x] : 0.0;
                    tmp[y * w + x] = 0.25 * a + 0.5 * mask[y * w + x] + 0.25 * c;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double a = x > 0 ? tmp[y * w + x - 1] : 0.0;
                    double c = x < w - 1 ? tmp[y * w + x + 1] : 0.0;
                    mask[y * w + x] = 0.25 * a + 0.5 * tmp[y * w + x] + 0.25 * c;
                }

            // ---- the glass: a rounded rectangle, taken out ---------------------
            double cx = (s.glassX0 + s.glassX1) * 0.5, cy = (s.glassY0 + s.glassY1) * 0.5;
            double hx = (s.glassX1 - s.glassX0) * 0.5, hy = (s.glassY1 - s.glassY0) * 0.5;
            double r = s.glassRadius;

            var rgb = new double[n * 3];
            var alpha = new double[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double qx = Math.Abs(x + 0.5 - cx) - (hx - r);
                    double qy = Math.Abs(y + 0.5 - cy) - (hy - r);
                    double mx = Math.Max(qx, 0.0), my = Math.Max(qy, 0.0);
                    double d = Math.Sqrt(mx * mx + my * my) + Math.Min(Math.Max(qx, qy), 0.0) - r;
                    double cover = Clamp01(0.5 - d);

                    rgb[i * 3] = rgba[i * 4] * mask[i];
                    rgb[i * 3 + 1] = rgba[i * 4 + 1] * mask[i];
                    rgb[i * 3 + 2] = rgba[i * 4 + 2] * mask[i];
                    alpha[i] = Clamp01(1.0 - cover);
                }

            // ---- bleed the colour outward --------------------------------------
            var solid = new bool[n];
            for (int i = 0; i < n; i++) solid[i] = alpha[i] > 0.02;
            var acc = new double[3];
            var next = new bool[n];
            for (int pass = 0; pass < s.bleed; pass++)
            {
                Array.Copy(solid, next, n);
                var filled = new double[n * 3];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (solid[i]) continue;
                        acc[0] = acc[1] = acc[2] = 0.0;
                        int cnt = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                int j = ny * w + nx;
                                if (!solid[j]) continue;
                                acc[0] += rgb[j * 3]; acc[1] += rgb[j * 3 + 1]; acc[2] += rgb[j * 3 + 2];
                                cnt++;
                            }
                        if (cnt == 0) continue;
                        filled[i * 3] = acc[0] / cnt;
                        filled[i * 3 + 1] = acc[1] / cnt;
                        filled[i * 3 + 2] = acc[2] / cnt;
                        next[i] = true;
                    }
                for (int i = 0; i < n; i++)
                    if (next[i] && !solid[i])
                    {
                        rgb[i * 3] = filled[i * 3];
                        rgb[i * 3 + 1] = filled[i * 3 + 1];
                        rgb[i * 3 + 2] = filled[i * 3 + 2];
                    }
                Array.Copy(next, solid, n);
            }

            // ---- crop ----------------------------------------------------------
            int ox0 = s.cropX0, oy0 = s.cropY0, ox1 = s.cropX1, oy1 = s.cropY1;
            int ow = ox1 - ox0, oh = oy1 - oy0;
            var outp = new byte[ow * oh * 4];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int si = (y + oy0) * w + (x + ox0);
                    int di = (y * ow + x) * 4;
                    outp[di] = Trunc(rgb[si * 3]);
                    outp[di + 1] = Trunc(rgb[si * 3 + 1]);
                    outp[di + 2] = Trunc(rgb[si * 3 + 2]);
                    outp[di + 3] = Trunc(alpha[si] * 255.0);
                }

            return new Result
            {
                rgba = outp,
                width = ow,
                height = oh,
                left = s.glassX0 - ox0,
                right = ox1 - 1 - s.glassX1,
                top = s.glassY0 - oy0,
                bottom = oy1 - 1 - s.glassY1,
            };
        }

        static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        /// <summary>
        /// TRUNCATES, and that is not sloppiness. numpy's astype(uint8) throws the
        /// fraction away rather than rounding, so a value of 46.9 ships as 46; a
        /// C# cast does the same thing, and matching it is the difference between
        /// "the two implementations agree" and "the two implementations agree
        /// except on about a third of the pixels by one".
        /// </summary>
        static byte Trunc(double v) => (byte)(v < 0.0 ? 0.0 : (v > 255.0 ? 255.0 : v));
    }
}
