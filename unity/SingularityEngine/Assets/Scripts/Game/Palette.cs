using UnityEngine;
using Singularity.Core;

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

        /// <summary>
        /// THE RING ROUND AN EMPTY CELL IN THE FORGE, AND YOU COULD NOT SEE IT.
        ///
        /// This was #334155 — 2.03:1 against the plate, and 1.41:1 against the
        /// Grid fill it outlines. In a LEVEL EDITOR, whose entire task is putting
        /// cells onto a grid, the grid was the least visible thing on the screen.
        /// It is very likely part of why a first-time Forge cube verified 101
        /// times in 400 before the editor started saying which cells are faces.
        ///
        /// It is 3.37:1 on the plate now, which is 1.4.11's floor for a component
        /// you have to perceive to operate. The fill under it stays where it is:
        /// an inactive cell is meant to recede, and the ring is what says the
        /// cell is there at all.
        ///
        /// It is also the debris colour, and brighter chips off a fold read
        /// better rather than worse, so nothing there had to be traded.
        /// </summary>
        public static readonly Color GridHi  = Hex("#4d6280");
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
        //
        // AND THE DEFAULT MOVED TOO, ONCE, FOR A REASON THAT IS NOT TASTE.
        //
        // "The default stays the picture that was matched" held for every value
        // here except three, and the audit is what found them: Dim was #64748b,
        // which is 4.41:1 on black and 3.70:1 on a card — under AA, in the colour
        // that carries every body paragraph in this game. Dim2 was #334155 at
        // 2.03:1 and was setting five pieces of real type. AAA behind a mode is a
        // legitimate shipping choice; AA in the default is not one, and matching
        // a reference that fails it only reproduces the failure. Both moved by
        // the same rule the legible values did, to the smallest step that clears
        // 4.5:1 on all four grounds.

        static readonly Color RustShipped   = Hex("#ea580c");
        static readonly Color RustLegible   = Hex("#f68950");
        static readonly Color CoreShipped   = Hex("#f97316");
        static readonly Color CoreLegible   = Hex("#fa8838");
        static readonly Color FaultShipped  = Hex("#ef4444");
        static readonly Color FaultLegible  = Hex("#f58686");
        static readonly Color DimShipped    = Hex("#75859b");
        static readonly Color DimLegible    = Hex("#9ba7b7");
        static readonly Color Dim2Shipped   = Hex("#516888");
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

        /// <summary>
        /// THE QUIETEST MARK IN THE INTERFACE — AND IT IS NOT TYPE ANY MORE.
        ///
        /// It was: the title footer, a field placeholder, a locked vault's name,
        /// the HUD's vault caption. All five were 2.03:1 on black, and the reason
        /// they cannot simply be lifted to AA is arithmetic rather than taste. At
        /// 4.5:1 on near-black this lands at 5.56:1 against Dim's 5.58 — two type
        /// tiers that measure the same are one type tier with two names. So the
        /// words went to Dim, where they are read, and this kept the job it was
        /// always better at: an unlit pip, an unreachable cell, the edge of a
        /// vault you have not opened. Those are graphical objects, they answer to
        /// 1.4.11's 3:1, and it clears that on every ground.
        ///
        /// THE LOCKED VAULT IS THE ONE WORTH SPELLING OUT. Its name used to be
        /// set in this colour to say "locked", which is 1.4.1 — information
        /// carried by contrast alone — and it was carrying it at a ratio nobody
        /// could read. The lock is said by the edge and by the pips. The name is
        /// just a name, and now you can read it.
        /// </summary>
        public static Color Dim2  => Access.Legible ? Dim2Legible : Dim2Shipped;

        /// <summary>
        /// THE SAME SLATE, AS A SURFACE RATHER THAN AS TYPE — and it is a
        /// separate name because it is a separate job.
        ///
        /// The shipped build spent one token on both the quiet mark and the inert
        /// half of a control: the track behind a slider, the body of a switch
        /// that is off. That is exactly the conflation this file opens by
        /// forbidding, and it only became visible under legibility, where the two
        /// want to go in opposite directions. A mark has to get LIGHTER to be
        /// told from a dark panel; a track has to stay DARK or the rust fill on
        /// top of it stops being visible.
        ///
        /// THEY ARE DIFFERENT VALUES NOW, WHICH IS WHY THIS IS SAFE. They were
        /// both #334155, and the only thing keeping a walk of the interface from
        /// dragging a placeholder and a slider track the same way was that one
        /// lives on a Text and the other on an Image. That is still true and it
        /// is no longer load-bearing: the track is #313f52, eight thousandths of
        /// lightness down, which is invisible and is exactly the step that takes
        /// the rust fill against it from 2.91:1 to 3.00 — over 1.4.11's floor for
        /// a graphical object you have to tell from its neighbour, which it had
        /// been sitting a hair under since the port was written.
        /// </summary>
        public static readonly Color Inert = Hex("#313f52");

        // ---- the two depth ramps -------------------------------------------
        //
        //   lattice   #1a2332 (lum 34) .. #313c4d (lum 59)
        //   trace     #0b8ed0 (lum 119) .. #38bdf8 (lum 165)
        //
        // A gap of 60 between the materials, 46 of spread within the trace, and
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

        static readonly Color LatticeNearShipped = new Color32(49, 60, 77, 255);
        static readonly Color LatticeNearLegible = new Color32(40, 49, 63, 255);

        /// <summary>
        /// THE COLOUR THAT DECIDES THE WHOLE BOARD, AND IT WAS MEASURED IN THE
        /// WRONG PLACE.
        ///
        /// The worst pair on the board is the farthest trace against the nearest
        /// lattice, where the two ramps come closest, and the audit measured it
        /// exactly once — against the UN-AGED lattice, which is the best case it
        /// ever has. The lattice corrodes down the ladder, so it walks toward the
        /// trace: the pair that read 2.56:1 in vault one was 2.30:1 in vault
        /// thirty, and the legibility mode did not help, because the corroded end
        /// of the ramp is shared. Thirty vaults, both palettes, every one of them
        /// under 1.4.11's 3:1, and nothing said so — the band check runs at every
        /// vault but asks whether the trace is BRIGHTER, not whether you can tell
        /// the two apart.
        ///
        /// So both ends of the ramp sank: this one, and LatticeNearDeep with it.
        /// The tightest ratio anywhere on the ladder is 3.07:1 now, and the game's
        /// own luminance band went from 0.1362 to 0.1692 — a quarter wider — as a
        /// side effect. It costs the trace nothing, because the trace does not
        /// move: a trace is still exactly the value the brief names, at every
        /// depth, in both modes.
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
        // THE TWO LISTS ARE STILL THE POINT, and they are no longer the only
        // thing holding the walk together. The shipped build spent one token on
        // the quiet mark AND the inert surface — Dim2 and Inert, both #334155 —
        // so a walk that looked only at values could not tell a placeholder from
        // a slider track and would drag both the same way. Type lives on Text and
        // surfaces live on Image, which is the distinction these lists make. The
        // two values have since diverged for a contrast reason of their own, so
        // the ambiguity is gone as well; the lists stay, because a palette that
        // grows a second collision should not also have to grow a walk.

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

        // ---- the machine corrodes as you go deeper ---------------------------
        //
        // Eight vaults, each with a name, a difficulty and its own room, and the
        // board was the same four numbers from cube one to cube a hundred and
        // twenty. Progression was a number in the corner and nothing else.
        //
        // THE TRACE DOES NOT MOVE, AND THAT IS THE RULE THIS OBEYS. It is the
        // signal — the one colour that means "you can stand here" — and every
        // contrast assertion in the audit is anchored to it. What ages is the
        // LATTICE: the inert body of the machine, which has no job except to be
        // darker than the trace. So the circuit stays exactly as legible as it
        // was at cube one and the metal it is printed on goes over to rust,
        // which is also the same thing that happened to the case.
        //
        // The two ends are luminance-matched to within a hundredth, so the band
        // guarantee is not being spent on a mood. AccessChecks proves it for
        // every vault rather than for the one this file was written against.

        static readonly Color LatticeFarDeep  = new Color32(43, 37, 35, 255);
        static readonly Color LatticeNearDeep = new Color32(69, 57, 50, 255);

        /// <summary>
        /// How far through the ladder a vault sits, 0 at the first and 1 at the
        /// last. Thirty vaults, so a step is a thirtieth — the drift is meant to
        /// be something you notice having happened, not something you watch.
        /// </summary>
        public static float VaultAge(int band)
            => Mathf.Clamp01(band / (float)Mathf.Max(1, Vaults.RankedVaults - 1));

        public static Color LatticeFarOf(int band) => Color.Lerp(LatticeFar, LatticeFarDeep, VaultAge(band));
        public static Color LatticeNearOf(int band) => Color.Lerp(LatticeNear, LatticeNearDeep, VaultAge(band));

        /// <summary>The flat-board colour of a cell — the same arithmetic the shader does per pixel.</summary>
        /// <summary>
        /// The everter, and it is deliberately NOT on either depth ramp.
        ///
        /// Trace runs blue and lattice runs slate, and the whole board is read off
        /// the one property that a trace is brighter than the lattice at every
        /// depth. An everter is neither material: it is the control that swaps
        /// which face of the solid you are standing on, and putting it on a ramp
        /// would make it read as "a trace that is slightly wrong". It is its own
        /// hue for the same reason a node and a lock are.
        ///
        /// Violet because it is the only unused corner of the wheel this palette
        /// occupies — rust, amber, lime, cyan and blue are all spoken for — and
        /// because it stays distinguishable from the trace under all three
        /// dichromacies, which the audit asserts rather than assumes.
        /// </summary>
        public static readonly Color Evert = Hex("#a78bfa");

        /// <summary>
        /// THE SCHEMATIC. What the lattice is drawn in while it is being LOOKED
        /// THROUGH rather than played on.
        ///
        /// A held MATRIX is not a view of the board, it is a view of the MACHINE:
        /// the fill goes and every cell becomes a wire box, at every depth, with
        /// nothing occluding anything. That is a different job from the settled
        /// board — nobody is asking "can I stand there" with their thumb held down
        /// — and it gets its own two colours, cool where the housing is rust,
        /// because a schematic drawn in the same ink as the object is a schematic
        /// nobody can tell apart from it.
        ///
        /// The two still separate the way everything in this game separates: the
        /// trace wire is far brighter than the lattice wire, so the one property
        /// the whole board is read off survives into the x-ray. The audit asserts
        /// it rather than trusting it.
        ///
        /// These are ADDITIVE and land on the void, so they are the light itself
        /// rather than a surface colour — which is why they are allowed to be this
        /// bright when nothing else in the palette is.
        /// </summary>
        /// It is a periwinkle rather than the everter's violet on purpose: an
        /// everter is a walk type, so its own wire is drawn in WireTrace, but its
        /// GLYPH is violet and it is sitting over this. Thirty-one degrees of hue
        /// between them is what stops "the violet marker" and "the violet lattice"
        /// being the same read at a glance — the shape settles it either way, but
        /// the shape should not have to.
        public static readonly Color WireTrace = Hex("#9fe8ff");
        public static readonly Color WireLattice = Hex("#8098d8");

        /// <summary>
        /// THE TRIGGER, and it is not on either depth ramp for the same reason the
        /// everter is not: it does not change what a cell IS, it changes which
        /// board you are standing on. A green because it is the last unspoken
        /// corner of this wheel — rust, amber, lime, cyan, blue and violet are all
        /// taken — and because it has to be told apart from the everter's violet
        /// at a glance, the two being the only marks that rearrange the board
        /// rather than recolour it.
        /// </summary>
        public static readonly Color Trigger = Hex("#34d399");

        public static Color Tile(char t, int d, int n, int band = 0)
        {
            if (t == 'E') return Evert;
            if (t == 'T' || t == 'U') return Trigger;
            float near = Mathf.Clamp01(d / (float)Mathf.Max(1, n - 1));
            Color a = Level.IsWalkType(t) ? TraceFar : LatticeFarOf(band);
            Color b = Level.IsWalkType(t) ? TraceNear : LatticeNearOf(band);
            return Color.Lerp(a, b, near);
        }
    }
}
