using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// COLOUR IS DATA, NOT MATERIAL.
    ///
    /// Nine values, each with exactly one meaning, and nothing here is allowed a
    /// tenth. There is no "shade of" anything: a colour on screen is a claim
    /// about what a thing IS, so two things that are not the same thing never
    /// share one, and one thing never changes colour to suit a background.
    ///
    /// You are always arc. The way out is always core. Nodes are always node. A
    /// player who learns those on cube three must still be able to read cube nine
    /// hundred at a glance.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s.StartsWith("#") ? s : "#" + s, out Color c);
            return c;
        }

        public static readonly Color Void    = Hex("#000000");   // unrendered space. Not dark blue. Black.
        public static readonly Color Void2   = Hex("#050709");
        public static readonly Color Void3   = Hex("#0b0f16");

        public static readonly Color Panel   = Hex("#0d1117");
        public static readonly Color PanelHi = Hex("#161c26");
        public static readonly Color Card    = Hex("#141924");
        public static readonly Color Scrim   = new Color(0, 0, 0, 0.72f);

        public static readonly Color Grid    = Hex("#1e293b");   // inactive lattice
        public static readonly Color GridHi  = Hex("#334155");
        public static readonly Color Trace   = Hex("#38bdf8");   // active circuit, walkable
        public static readonly Color TraceLo = Hex("#0c4a6e");
        public static readonly Color Arc     = Hex("#22d3ee");   // you, data flow, a perfect collapse
        public static readonly Color Rust    = Hex("#ea580c");   // the instrument itself
        public static readonly Color RustHi  = Hex("#fb923c");
        public static readonly Color Node    = Hex("#a3e635");   // collectible data
        public static readonly Color Lock    = Hex("#facc15");   // closed gate
        public static readonly Color Core    = Hex("#f97316");   // the way out
        public static readonly Color Fault   = Hex("#ef4444");   // you tried to break physics

        public static readonly Color Ink     = Hex("#e2e8f0");
        public static readonly Color Dim     = Hex("#64748b");
        public static readonly Color Dim2    = Hex("#334155");

        // ---- the two depth ramps -------------------------------------------
        //
        //   lattice   #1a2332 (lum 34) .. #3b485c (lum 71)
        //   trace     #0b8ed0 (lum 119) .. #38bdf8 (lum 165)
        //
        // A gap of 48 between the materials, 46 of spread within the trace, and
        // the near trace is exactly the value the brief names. The whole board is
        // read off one property: A TRACE IS BRIGHTER THAN THE LATTICE, ALWAYS, at
        // every depth — so "may I stand there" survives a dim screen and a
        // colour-blind eye.

        public static readonly Color TraceFar   = new Color32(11, 142, 208, 255);
        public static readonly Color TraceNear  = new Color32(56, 189, 248, 255);
        public static readonly Color LatticeFar = new Color32(26, 35, 50, 255);
        public static readonly Color LatticeNear= new Color32(59, 72, 92, 255);

        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// The band guarantee, asserted rather than assumed. The palette is four
        /// numbers somebody could edit, and a silent regression here does not look
        /// like a bug — it looks like the game got hard.
        /// </summary>
        public static float BandGap() => Luminance(TraceFar) - Luminance(LatticeNear);

        /// <summary>The flat-board colour of a cell — the same arithmetic the shader does per pixel.</summary>
        public static Color Tile(char t, int d, int n)
        {
            float near = Mathf.Clamp01(d / (float)Mathf.Max(1, n - 1));
            Color a = t == '+' ? TraceFar : LatticeFar;
            Color b = t == '+' ? TraceNear : LatticeNear;
            return Color.Lerp(a, b, near);
        }
    }
}
