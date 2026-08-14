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
        public static readonly Color Node    = Hex("#a3e635");   // collectible data
        public static readonly Color Lock    = Hex("#facc15");   // closed gate

        public static readonly Color Ink     = Hex("#e2e8f0");

        // ---- the five that move, and the mode that moves them ----------------
        //
        // THE SHIPPED VALUES ARE THE DEFAULT AND THEY STAY EXACT. All twenty-one
        // were checked against the APK's own :root block value by value, and
        // nothing here is allowed to quietly undo that.
        //
        // But nine of the pairs this interface actually prints are below WCAG
        // AAA and two are below AA. DIM2 on the void is 2.03:1 — that is the
        // footer on the title screen and every field placeholder in the game.
        // Black on the primary's rust is 5.90:1, and that is the one button
        // every screen is built around. Those are measurements, not opinions,
        // and the answer a shipping game gives is a MODE: the default stays the
        // picture that was matched, and a player who needs the contrast gets it
        // without being handed a different game.
        //
        // Five values move; the other sixteen were already past 7:1. Each new
        // value is the SMALLEST step in lightness, at the same hue and
        // saturation, that clears 7:1 against all four of the dark grounds this
        // interface uses — void, panel, panelHi and card. They were solved for
        // rather than picked, which is why they look like nothing in particular.

        static readonly Color RustShipped   = Hex("#ea580c");
        static readonly Color RustLegible   = Hex("#f68950");
        static readonly Color CoreShipped   = Hex("#f97316");
        static readonly Color CoreLegible   = Hex("#fa8838");
        static readonly Color FaultShipped  = Hex("#ef4444");
        static readonly Color FaultLegible  = Hex("#f58686");
        static readonly Color DimShipped    = Hex("#64748b");
        static readonly Color DimLegible    = Hex("#9ba7b7");
        static readonly Color Dim2Shipped   = Hex("#334155");
        static readonly Color Dim2Legible   = Hex("#96a8c0");

        /// <summary>
        /// The instrument itself — as a mark, as type, and as the one filled
        /// surface in the interface.
        ///
        /// ONE VALUE SERVES BOTH BECAUSE OF AN IDENTITY, not because of luck. A
        /// colour that clears 7:1 against black as type is exactly a colour that
        /// black clears 7:1 against as a surface: the ratio is symmetric. So the
        /// same lift that makes the vault name readable makes the primary's
        /// literal color:#000 label readable, and the primary does not need —
        /// and must not have — a second rust of its own.
        ///
        /// Neither black nor white clears 7:1 on the SHIPPED rust: white is
        /// worse, at 3.56:1 against black's 5.90. No choice of ink fixes that
        /// button. The surface is what has to move.
        /// </summary>
        public static Color Rust   => Access.Legible ? RustLegible : RustShipped;

        /// <summary>The rust one step up. 7.55:1 on the worst ground already, so it does not move.</summary>
        public static readonly Color RustHi = Hex("#fb923c");

        public static Color Core  => Access.Legible ? CoreLegible : CoreShipped;
        public static Color Fault => Access.Legible ? FaultLegible : FaultShipped;
        public static Color Dim   => Access.Legible ? DimLegible : DimShipped;

        /// <summary>The quietest type in the interface: footers, placeholders, the vault caption.</summary>
        public static Color Dim2  => Access.Legible ? Dim2Legible : Dim2Shipped;

        /// <summary>
        /// THE SAME SLATE, AS A SURFACE RATHER THAN AS TYPE — and it is a
        /// separate name because it is a separate job.
        ///
        /// The shipped build spends one token on both the quiet type and the
        /// inert half of a control: the track behind a slider, the body of a
        /// switch that is off. That is exactly the conflation this file opens by
        /// forbidding, and it only became visible under legibility, where the two
        /// want to go in opposite directions. Type has to get LIGHTER to be read
        /// against a dark panel; a track has to stay DARK or the rust fill on top
        /// of it stops being visible — at Dim2's legible value the slider's own
        /// reading would sink into its track at 1.17:1.
        ///
        /// So the track keeps the shipped slate in both modes, which holds the
        /// fill against it at 3.32:1, past 1.4.11's 3:1 for a graphical object
        /// you have to tell from its neighbour.
        /// </summary>
        public static readonly Color Inert = Hex("#334155");

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
        //
        // That last claim is simulated and asserted rather than believed; see
        // Access.Simulate and the band checks. It holds under all three
        // dichromacies, with the narrowest margin under deuteranopia.

        public static readonly Color TraceFar   = new Color32(11, 142, 208, 255);
        public static readonly Color TraceNear  = new Color32(56, 189, 248, 255);
        public static readonly Color LatticeFar = new Color32(26, 35, 50, 255);

        static readonly Color LatticeNearShipped = new Color32(59, 72, 92, 255);
        static readonly Color LatticeNearLegible = new Color32(50, 61, 78, 255);

        /// <summary>
        /// The near lattice is the only board colour legibility moves, and it is
        /// the one that decides the whole thing. The band is measured at its
        /// WORST PAIR — the farthest trace against the nearest lattice, which is
        /// where the two ramps come closest — and that pair is 2.56:1, under
        /// 1.4.11's 3:1 for a graphical object you have to tell from its
        /// neighbour. Sinking the near lattice by fifteen per cent of its
        /// lightness puts it at 3.03:1 and widens the game's own luminance band
        /// from 48 to 59 out of 255. It costs the trace nothing, because the
        /// trace does not move: a trace is still exactly the value the brief
        /// names, at every depth, in both modes.
        /// </summary>
        public static Color LatticeNear => Access.Legible ? LatticeNearLegible : LatticeNearShipped;

        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // ---- swapping an interface that is already on screen ------------------
        //
        // Every screen here is built once, in code, with its colours baked into
        // the Graphics as they are made. That is the right shape for an interface
        // with no prefabs and the wrong shape for a setting that has to take
        // effect while the screen it changes is on top of the stack.
        //
        // Rebuilding the canvas would work, and would throw away scroll
        // positions, input focus and whatever the player was in the middle of.
        // So the two palettes are treated as what they are — a bijection — and
        // the interface is walked, swapping one set for the other. Nothing in the
        // legible set collides with anything in the shipped set, so a colour
        // already on screen says without ambiguity which palette it came from,
        // and the walk is idempotent and needs no bookkeeping.
        //
        // THE TWO LISTS ARE THE POINT. The shipped build spends one token on the
        // quiet type AND the inert surface — that is Dim2 and Inert, the same
        // #334155 — so a walk that looked only at values could not tell a
        // placeholder from a slider track and would drag both the same way. Type
        // lives on Text and surfaces live on Image, which is exactly the
        // distinction the two lists make, and it is the reason this can be a walk
        // at all rather than a rebuild.

        public readonly struct Swap
        {
            public readonly Color shipped, legible;
            public Swap(Color shipped, Color legible) { this.shipped = shipped; this.legible = legible; }
        }

        /// <summary>The roles that appear as TYPE, and only ever on a Text.</summary>
        public static readonly Swap[] TypeSwaps =
        {
            new Swap(RustShipped,  RustLegible),
            new Swap(CoreShipped,  CoreLegible),
            new Swap(FaultShipped, FaultLegible),
            new Swap(DimShipped,   DimLegible),
            new Swap(Dim2Shipped,  Dim2Legible),
        };

        /// <summary>
        /// The roles that appear as a SURFACE. Only rust: it is the one filled
        /// shape in the interface, and Inert deliberately does not move.
        /// </summary>
        public static readonly Swap[] FillSwaps =
        {
            new Swap(RustShipped, RustLegible),
        };

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
