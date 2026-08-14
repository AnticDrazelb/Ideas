using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// THE MANUAL IS DRAWN, NOT DESCRIBED — AND THE DRAWINGS ARE THE SHIPPED ONES.
    ///
    /// Every mark in the shipped interface is inline SVG: the seven manual
    /// diagrams, the four object tiles, the four HUD chip icons and the eight
    /// calibrate row icons. They are a dozen shapes each, they inherit
    /// currentColor so they are painted out of the same nine values as everything
    /// else, and they stay sharp at any density with nothing to load.
    ///
    /// This port had been substituting approximations — a `▤` where the fold chip
    /// has a drawn cell, a generic square where the manual has two decks and an
    /// arrow. That is the one part of "does it look the same" that no amount of
    /// metric-matching can reach, so the path data is carried across verbatim from
    /// the APK (see <see cref="Art"/>) and rasterised here.
    ///
    /// ROUND JOINS AND ROUND CAPS FALL OUT OF THE METHOD RATHER THAN BEING ADDED.
    /// The sheet asks for `stroke-linejoin:round; stroke-linecap:round` on every
    /// one of these, and a stroke rendered as "every point within half a stroke
    /// width of the skeleton" is exactly that shape — the round join is what the
    /// distance to a shared endpoint already describes. So the rasteriser measures
    /// distance rather than building geometry, which is also why one sample per
    /// texel is enough to be smooth: the distance IS the coverage.
    /// </summary>
    public static class Svg
    {
        // ---- the model ------------------------------------------------------

        class Shape
        {
            public List<List<Vector2>> subs = new List<List<Vector2>>();
            public List<bool> closed = new List<bool>();
            public Color stroke;
            public bool hasStroke;
            public Color fill;
            public bool hasFill;
            public float width;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- entry ----------------------------------------------------------

        /// <summary>
        /// Rasterise a fragment of the shipped markup into a sprite.
        /// </summary>
        /// <param name="markup">The element source, copied from index.html.</param>
        /// <param name="vbW">viewBox width. <param name="vbH">viewBox height.</param>
        /// <param name="strokeW">The sheet's stroke-width for this family of marks.</param>
        /// <param name="ink">What `currentColor` and an unclassed stroke resolve to.</param>
        /// <param name="px">Device pixels per viewBox unit.</param>
        public static Sprite Make(string markup, float vbW, float vbH, float strokeW, Color ink, float px)
        {
            List<Shape> shapes = Parse(markup, strokeW, ink);

            int w = Mathf.Max(1, Mathf.CeilToInt(vbW * px));
            int h = Mathf.Max(1, Mathf.CeilToInt(vbH * px));

            var acc = new Color[w * h];
            for (int i = 0; i < acc.Length; i++) acc[i] = new Color(0f, 0f, 0f, 0f);

            foreach (Shape s in shapes) Draw(acc, w, h, s, px);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "svg"
            };
            var px32 = new Color32[w * h];
            for (int i = 0; i < acc.Length; i++)
            {
                Color c = acc[i];
                // un-premultiply for the texture, since UI shaders expect straight alpha
                float a = Mathf.Clamp01(c.a);
                float k = a > 0.0001f ? 1f / a : 0f;
                px32[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r * k) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g * k) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b * k) * 255f),
                    (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px32);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), px);
        }

        // ---- rasterising ----------------------------------------------------
        //
        // Row 0 of a texture is the BOTTOM and the SVG y axis points down, so the
        // sample's y is flipped on the way in. Everything else is in viewBox units.

        static void Draw(Color[] acc, int w, int h, Shape s, float px)
        {
            float half = s.width * 0.5f;

            // only visit the texels the shape can possibly touch
            float pad = (s.hasStroke ? half : 0f) + 1.5f / px;
            Bounds2(s, out float x0, out float y0, out float x1, out float y1);
            int ix0 = Mathf.Max(0, Mathf.FloorToInt((x0 - pad) * px));
            int ix1 = Mathf.Min(w - 1, Mathf.CeilToInt((x1 + pad) * px));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt((y0 - pad) * px));
            int iy1 = Mathf.Min(h - 1, Mathf.CeilToInt((y1 + pad) * px));

            for (int ty = iy0; ty <= iy1; ty++)
                for (int tx = ix0; tx <= ix1; tx++)
                {
                    var p = new Vector2((tx + 0.5f) / px, (ty + 0.5f) / px);

                    if (s.hasFill)
                    {
                        float d = SignedDistance(s, p);
                        float a = Mathf.Clamp01(0.5f - d * px);
                        if (a > 0f) Blend(acc, w, h, tx, ty, s.fill, a);
                    }
                    if (s.hasStroke)
                    {
                        float d = Distance(s, p) - half;
                        float a = Mathf.Clamp01(0.5f - d * px);
                        if (a > 0f) Blend(acc, w, h, tx, ty, s.stroke, a);
                    }
                }
        }

        static void Blend(Color[] acc, int w, int h, int tx, int ty, Color c, float a)
        {
            // texture row 0 is the bottom; the SVG y axis points down
            int row = h - 1 - ty;
            if (row < 0 || row >= h) return;
            int i = row * w + tx;

            float sa = c.a * a;
            Color dst = acc[i];
            float outA = sa + dst.a * (1f - sa);
            acc[i] = new Color(
                c.r * sa + dst.r * dst.a * (1f - sa),
                c.g * sa + dst.g * dst.a * (1f - sa),
                c.b * sa + dst.b * dst.a * (1f - sa),
                outA);
            // the accumulator holds premultiplied colour; normalise back so the
            // next blend reads a straight colour
            if (outA > 0.0001f)
                acc[i] = new Color(acc[i].r / outA, acc[i].g / outA, acc[i].b / outA, outA);
        }

        static void Bounds2(Shape s, out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = float.MaxValue; x1 = y1 = float.MinValue;
            foreach (var sub in s.subs)
                foreach (Vector2 p in sub)
                {
                    if (p.x < x0) x0 = p.x;
                    if (p.y < y0) y0 = p.y;
                    if (p.x > x1) x1 = p.x;
                    if (p.y > y1) y1 = p.y;
                }
            if (x0 > x1) { x0 = y0 = 0f; x1 = y1 = 0f; }
        }

        static float Distance(Shape s, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < s.subs.Count; i++)
            {
                List<Vector2> sub = s.subs[i];
                if (sub.Count == 1) { best = Mathf.Min(best, Vector2.Distance(p, sub[0])); continue; }
                int n = sub.Count;
                int segs = s.closed[i] ? n : n - 1;
                for (int k = 0; k < segs; k++)
                {
                    float d = SegDist(p, sub[k], sub[(k + 1) % n]);
                    if (d < best) best = d;
                }
            }
            return best == float.MaxValue ? 1e9f : best;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float len = Vector2.Dot(ab, ab);
            float t = len > 1e-9f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / len) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Negative inside. Nonzero winding, which is SVG's default fill rule.</summary>
        static float SignedDistance(Shape s, Vector2 p)
        {
            float d = Distance(s, p);
            int wind = 0;
            foreach (List<Vector2> sub in s.subs)
            {
                int n = sub.Count;
                if (n < 3) continue;
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = sub[k], b = sub[(k + 1) % n];
                    if (a.y <= p.y)
                    {
                        if (b.y > p.y && Cross(a, b, p) > 0f) wind++;
                    }
                    else if (b.y <= p.y && Cross(a, b, p) < 0f) wind--;
                }
            }
            return wind != 0 ? -d : d;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p)
            => (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);

        // ---- parsing --------------------------------------------------------

        static readonly Regex ElRx = new Regex(
            @"<(g|path|rect|circle)\b([^>]*?)(/?)>|</g>", RegexOptions.Compiled);
        static readonly Regex AttrRx = new Regex(
            "([a-zA-Z-]+)\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);

        static List<Shape> Parse(string markup, float strokeW, Color ink)
        {
            var outp = new List<Shape>();

            // <g class="..."> sets the colour for everything inside it, which is the
            // whole of how these drawings are coloured: one class per group, and the
            // shapes inherit.
            var stack = new List<(Color col, float op)>();
            stack.Add((ink, 1f));

            foreach (Match m in ElRx.Matches(markup))
            {
                if (m.Value == "</g>")
                {
                    if (stack.Count > 1) stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                string tag = m.Groups[1].Value;
                string attrs = m.Groups[2].Value;
                var a = new Dictionary<string, string>();
                foreach (Match am in AttrRx.Matches(attrs)) a[am.Groups[1].Value] = am.Groups[2].Value;

                (Color col, float op) top = stack[stack.Count - 1];
                Color col = top.col;
                float op = top.op;

                if (a.TryGetValue("class", out string cls))
                {
                    Color? c = ClassColour(cls);
                    if (c.HasValue) col = c.Value;
                }
                if (a.TryGetValue("opacity", out string ops) && Num(ops, out float o)) op *= o;

                if (tag == "g")
                {
                    stack.Add((col, op));
                    continue;
                }

                var s = new Shape { width = strokeW, stroke = col, hasStroke = true };

                // `fill` on a shape overrides the sheet's `fill:none`. The two the
                // markup uses are currentColor and #000.
                if (a.TryGetValue("fill", out string f))
                {
                    if (f == "none") s.hasFill = false;
                    else if (f == "currentColor") { s.hasFill = true; s.fill = col; }
                    else { s.hasFill = true; s.fill = Palette.Hex(f); }
                }

                // .dia .dTrace rect, .dia .dTrace path { fill: rgba(56,189,248,.16) }
                if (!s.hasFill && a.TryGetValue("class", out string c2) && c2.Contains("dTrace"))
                {
                    s.hasFill = true;
                    s.fill = new Color(Palette.Trace.r, Palette.Trace.g, Palette.Trace.b, 0.16f);
                }

                if (op < 1f)
                {
                    s.stroke = new Color(s.stroke.r, s.stroke.g, s.stroke.b, s.stroke.a * op);
                    s.fill = new Color(s.fill.r, s.fill.g, s.fill.b, s.fill.a * op);
                }

                switch (tag)
                {
                    case "path":
                        if (a.TryGetValue("d", out string d)) Path(s, d);
                        break;
                    case "rect":
                        RectShape(s, F(a, "x"), F(a, "y"), F(a, "width"), F(a, "height"), F(a, "rx"));
                        break;
                    case "circle":
                        Circle(s, F(a, "cx"), F(a, "cy"), F(a, "r"));
                        break;
                }

                if (s.subs.Count > 0) outp.Add(s);
            }
            return outp;
        }

        static float F(Dictionary<string, string> a, string k)
            => a.TryGetValue(k, out string v) && Num(v, out float f) ? f : 0f;

        static bool Num(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, Inv, out f);

        /// <summary>
        /// The diagram classes, straight out of the sheet. `.dInk` is arc rather
        /// than ink, which looks like a mistake and is not: it is the colour the
        /// player character is drawn in everywhere else.
        /// </summary>
        static Color? ClassColour(string cls)
        {
            if (cls.Contains("dLine")) return Palette.Dim2;
            if (cls.Contains("dTrace")) return Palette.Trace;
            if (cls.Contains("dArc")) return Palette.Arc;
            if (cls.Contains("dRust")) return Palette.Rust;
            if (cls.Contains("dNode")) return Palette.Node;
            if (cls.Contains("dLock")) return Palette.Lock;
            if (cls.Contains("dCore")) return Palette.Core;
            if (cls.Contains("dInk")) return Palette.Arc;
            return null;
        }

        // ---- geometry -------------------------------------------------------

        const int ArcSegs = 24;

        static void RectShape(Shape s, float x, float y, float w, float h, float rx)
        {
            var pts = new List<Vector2>();
            if (rx <= 0.001f)
            {
                pts.Add(new Vector2(x, y));
                pts.Add(new Vector2(x + w, y));
                pts.Add(new Vector2(x + w, y + h));
                pts.Add(new Vector2(x, y + h));
            }
            else
            {
                float r = Mathf.Min(rx, Mathf.Min(w, h) * 0.5f);
                const int K = 5;
                void Corner(float cx, float cy, float a0)
                {
                    for (int i = 0; i <= K; i++)
                    {
                        float ang = a0 + Mathf.PI * 0.5f * i / K;
                        pts.Add(new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r));
                    }
                }
                Corner(x + w - r, y + r, -Mathf.PI * 0.5f);
                Corner(x + w - r, y + h - r, 0f);
                Corner(x + r, y + h - r, Mathf.PI * 0.5f);
                Corner(x + r, y + r, Mathf.PI);
            }
            s.subs.Add(pts);
            s.closed.Add(true);
        }

        static void Circle(Shape s, float cx, float cy, float r)
        {
            var pts = new List<Vector2>();
            const int K = 48;
            for (int i = 0; i < K; i++)
            {
                float a = Mathf.PI * 2f * i / K;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            s.subs.Add(pts);
            s.closed.Add(true);
        }

        static readonly Regex TokRx = new Regex(@"[MmLlHhVvAaZz]|-?\d*\.?\d+(?:[eE][-+]?\d+)?",
                                                RegexOptions.Compiled);

        /// <summary>
        /// The subset of the path grammar these drawings use: moveto, lineto, the
        /// two axis shorthands, elliptical arc and close, in both cases. There is
        /// not a single bezier in the shipped artwork — every curve in it is a
        /// circular arc — so there is no curve flattening here and none is needed.
        /// </summary>
        static void Path(Shape s, string d)
        {
            var toks = new List<string>();
            foreach (Match m in TokRx.Matches(d)) toks.Add(m.Value);

            List<Vector2> cur = null;
            Vector2 pen = Vector2.zero, start = Vector2.zero;
            char cmd = 'M';
            int i = 0;

            float Next() => i < toks.Count && Num(toks[i], out float f) ? (i++, f).Item2 : 0f;
            bool More() => i < toks.Count && !IsCmd(toks[i][0]);

            void Open()
            {
                cur = new List<Vector2> { pen };
                s.subs.Add(cur);
                s.closed.Add(false);
            }

            while (i < toks.Count)
            {
                if (IsCmd(toks[i][0])) { cmd = toks[i][0]; i++; }
                else if (cmd == 'M') cmd = 'L';
                else if (cmd == 'm') cmd = 'l';

                bool rel = char.IsLower(cmd);
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        {
                            float x = Next(), y = Next();
                            pen = rel ? pen + new Vector2(x, y) : new Vector2(x, y);
                            start = pen;
                            Open();
                            break;
                        }
                    case 'L':
                        {
                            float x = Next(), y = Next();
                            pen = rel ? pen + new Vector2(x, y) : new Vector2(x, y);
                            if (cur == null) Open(); else cur.Add(pen);
                            break;
                        }
                    case 'H':
                        {
                            float x = Next();
                            pen = new Vector2(rel ? pen.x + x : x, pen.y);
                            if (cur == null) Open(); else cur.Add(pen);
                            break;
                        }
                    case 'V':
                        {
                            float y = Next();
                            pen = new Vector2(pen.x, rel ? pen.y + y : y);
                            if (cur == null) Open(); else cur.Add(pen);
                            break;
                        }
                    case 'A':
                        {
                            float rx = Next(), ry = Next(), rot = Next();
                            float large = Next(), sweep = Next();
                            float x = Next(), y = Next();
                            Vector2 to = rel ? pen + new Vector2(x, y) : new Vector2(x, y);
                            if (cur == null) Open();
                            Arc(cur, pen, to, rx, ry, rot, large > 0.5f, sweep > 0.5f);
                            pen = to;
                            break;
                        }
                    case 'Z':
                        {
                            if (cur != null && cur.Count > 0)
                            {
                                s.closed[s.subs.IndexOf(cur)] = true;
                                pen = start;
                            }
                            cur = null;
                            break;
                        }
                    default:
                        i++;
                        break;
                }

                // a command letter may be followed by several coordinate sets
                if (!More() && i < toks.Count && !IsCmd(toks[i][0])) i++;
            }
        }

        static bool IsCmd(char c) => "MmLlHhVvAaZzCcSsQqTt".IndexOf(c) >= 0;

        /// <summary>
        /// SVG's endpoint arc, converted to a centre and swept. The conversion is
        /// the one in the specification's appendix, including the radius
        /// correction — the shipped markup has arcs whose radii are exactly half
        /// the chord, and floating point makes those come out marginally too small
        /// to reach, which without the correction is a NaN and a hole in the icon.
        /// </summary>
        static void Arc(List<Vector2> into, Vector2 p0, Vector2 p1,
                        float rx, float ry, float rotDeg, bool large, bool sweep)
        {
            if (Mathf.Approximately(rx, 0f) || Mathf.Approximately(ry, 0f)) { into.Add(p1); return; }

            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            float phi = rotDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);

            Vector2 d = (p0 - p1) * 0.5f;
            var p = new Vector2(cos * d.x + sin * d.y, -sin * d.x + cos * d.y);

            float rx2 = rx * rx, ry2 = ry * ry, px2 = p.x * p.x, py2 = p.y * p.y;
            float lam = px2 / rx2 + py2 / ry2;
            if (lam > 1f)
            {
                float k = Mathf.Sqrt(lam);
                rx *= k; ry *= k;
                rx2 = rx * rx; ry2 = ry * ry;
            }

            float num = rx2 * ry2 - rx2 * py2 - ry2 * px2;
            float den = rx2 * py2 + ry2 * px2;
            float co = den > 1e-9f ? Mathf.Sqrt(Mathf.Max(0f, num / den)) : 0f;
            if (large == sweep) co = -co;

            var cp = new Vector2(co * rx * p.y / ry, -co * ry * p.x / rx);
            Vector2 mid = (p0 + p1) * 0.5f;
            var c = new Vector2(cos * cp.x - sin * cp.y + mid.x, sin * cp.x + cos * cp.y + mid.y);

            float a0 = Mathf.Atan2((p.y - cp.y) / ry, (p.x - cp.x) / rx);
            float a1 = Mathf.Atan2((-p.y - cp.y) / ry, (-p.x - cp.x) / rx);
            float sweepAng = a1 - a0;
            if (!sweep && sweepAng > 0f) sweepAng -= Mathf.PI * 2f;
            else if (sweep && sweepAng < 0f) sweepAng += Mathf.PI * 2f;

            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweepAng) / (Mathf.PI * 2f) * ArcSegs));
            for (int k = 1; k <= steps; k++)
            {
                float a = a0 + sweepAng * k / steps;
                float ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                into.Add(new Vector2(cos * ex - sin * ey + c.x, sin * ex + cos * ey + c.y));
            }
        }
    }
}
