using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// AAA — AND IT IS THREE LETTERS BECAUSE IT IS THREE SENSES.
    ///
    /// The game speaks in light, in sound and in the motor. A player who cannot
    /// take delivery of one of those three is not playing a harder version of
    /// this game, they are playing a broken one — a plate clock that can only be
    /// heard is a five-second timer with no display to somebody deaf, and a
    /// board read off brightness alone is unplayable on a phone held in sunlight
    /// by anybody at all.
    ///
    /// So each sense gets a grade, each grade is a NUMBER rather than an
    /// intention, and every number here is asserted by a test:
    ///
    ///   APPARENT    every piece of text the interface prints reaches WCAG AAA,
    ///               7:1, against the ground it is printed on; every graphical
    ///               object reaches 1.4.11's 3:1 against its neighbour; and no
    ///               fact is carried by hue alone.
    ///   AUDIBLE     everything the game says with a sound, it also says in
    ///               words; the room and the instrument have separate levels.
    ///   ATTAINABLE  every control is at least 44 CSS px (2.5.5, 88 units
    ///               here) on its shortest side; every verb has a form that
    ///               needs no swipe and no hold; and motion has an OFF, not a
    ///               quieter setting.
    ///
    /// THE DEFAULT IS THE SHIPPED BUILD, EXACTLY. This file adds settings; it
    /// does not move the game underneath somebody who did not ask. The colours
    /// were matched to the APK value by value and they stay matched — legibility
    /// is a mode, and the audit below is what proves the mode does what it says.
    /// </summary>
    public static class Access
    {
        // ---- what the player asked for --------------------------------------

        /// <summary>
        /// TWO DIFFERENT PEOPLE ARE ASKING FOR TWO DIFFERENT THINGS, and one
        /// switch called EFFECTS was answering both of them badly.
        ///
        /// Camera shake, the zoom punch and the bent clock are VESTIBULAR: they
        /// move the frame, and for the player they trouble, forty per cent of a
        /// 0.95-trauma plate fire is still a plate fire. Sparks, bloom and the
        /// full-screen flash are PHOTOSENSITIVE: they change the brightness of
        /// the whole picture, which is a different criterion (2.3.1) with a
        /// different answer. Splitting them is not more knobs for its own sake —
        /// it is the difference between a setting that works and one that makes
        /// somebody choose which symptom to keep.
        /// </summary>
        public enum MotionLevel { Full = 0, Reduced = 1, Still = 2 }

        public static MotionLevel Motion
            => (MotionLevel)Mathf.Clamp(Store.Data.motion, 0, 2);

        /// <summary>
        /// What every camera impulse and every bent clock is multiplied by.
        /// STILL IS ZERO, and that is the whole reason this exists: the old
        /// EFFECTS bit bottomed out at 0.4, so there was no way to ask this game
        /// to hold still.
        /// </summary>
        public static float MotionAmount
            => Motion == MotionLevel.Full ? 1f : Motion == MotionLevel.Reduced ? 0.4f : 0f;

        /// <summary>Sparks, bloom and the full-screen flash — the light channel.</summary>
        public enum LightLevel { Full = 0, Reduced = 1, None = 2 }

        public static LightLevel Lighting
            => (LightLevel)Mathf.Clamp(Store.Data.light, 0, 2);

        /// <summary>Whether anything at all is drawn as light on top of the board.</summary>
        public static bool Light => Lighting != LightLevel.None;

        /// <summary>Whether the picture gets a second copy of itself laid over it.</summary>
        public static bool Bloom => Lighting == LightLevel.Full;

        public static float LightAmount
            => Lighting == LightLevel.Full ? 1f : Lighting == LightLevel.Reduced ? 0.4f : 0f;

        /// <summary>
        /// THE FLASH IS THE ONE EFFECT WITH A SEIZURE IN IT.
        ///
        /// 2.3.1 allows three flashes in any one second. Every flash in this
        /// game is player-triggered — a fold, a node, a plate — so a fast hand
        /// on a small cube can outrun that on its own, without the game ever
        /// having done anything wrong: nothing in the design says "no more than
        /// three folds a second", and nothing should. So the cap is enforced
        /// where the flash is RAISED rather than trusted to the pace of play,
        /// and NONE turns it off outright.
        /// </summary>
        public const int FlashesPerSecond = 3;

        /// <summary>Text and interface lifted to 7:1. Off by default; see <see cref="Palette"/>.</summary>
        public static bool Legible => Store.Data.legible != 0;

        /// <summary>Every sound also printed as words.</summary>
        public static bool Captions => Store.Data.captions != 0;

        /// <summary>
        /// A FOLD THAT IS NOT A SWIPE, AND A MATRIX THAT IS NOT A HOLD.
        ///
        /// Both of the gestures this game is built on are two-part: travel far
        /// enough, or stay still long enough. Neither is available to a player
        /// using a head pointer, a switch, or one unsteady finger — and there is
        /// no difficulty argument for the swipe, because the fold it performs is
        /// already on the keyboard for anybody on a desktop.
        /// </summary>
        public static bool Assist => Store.Data.assist != 0;

        /// <summary>Instrument level and room level, 0..150, as percentages.</summary>
        public static float Volume => Mathf.Clamp(Store.Data.vol, 0, 150) / 100f;
        public static float Room => Mathf.Clamp(Store.Data.room, 0, 150) / 100f;

        // ---- the standard's own arithmetic -----------------------------------
        //
        // WCAG 2.1 defines contrast on RELATIVE LUMINANCE, which is not the
        // luminance Palette uses. Palette's is a straight weighted sum of the
        // sRGB values, which is the right thing there — it answers "is this
        // swatch brighter than that one" for two swatches of the same material,
        // and it is what the depth ramps were authored against. This one
        // linearises first, which is what makes a ratio computed from it mean
        // "can a human read this", and the two are different enough that using
        // either for the other's job gives a wrong answer with a plausible shape.

        public const float AAAText = 7f;
        public const float AAText = 4.5f;
        /// <summary>1.4.11: a graphical object against its neighbour.</summary>
        public const float NonText = 3f;

        // ---- and the standard's own sizes ------------------------------------
        //
        // ONE CSS PIXEL IS TWO CANVAS UNITS. The shipped build's viewport is
        // about 360 CSS px wide and the CanvasScaler here references 720, so
        // every number the WCAG text quotes in CSS pixels doubles on its way in
        // and the conversion is the only arithmetic involved.

        /// <summary>--t-micro, 8.5 CSS px. Nothing in the interface may be smaller.</summary>
        public const int SmallestType = 17;

        /// <summary>
        /// 2.5.5 at AAA: 44 CSS px on the shortest side of anything you press.
        ///
        /// The buttons were never the problem — .btn is 104 units and .primary is
        /// 120, both comfortably past it. The controls INSIDE a settings row
        /// were: a switch drawn 42 units tall and a slider handle drawn 28 are
        /// half the target and less, and they are the two things in this game
        /// most likely to be pressed by somebody who is already struggling with
        /// it, on the screen they opened to make it easier.
        /// </summary>
        public const float TapTarget = 88f;

        static float Linear(float c)
            => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        /// <summary>WCAG relative luminance. Alpha is not part of it.</summary>
        public static float Relative(Color c)
            => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        /// <summary>The contrast ratio between two opaque colours, 1..21.</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Relative(a), lb = Relative(b);
            float hi = Mathf.Max(la, lb), lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        // ---- and what a dichromat sees ---------------------------------------
        //
        // The palette's central claim is that A TRACE IS BRIGHTER THAN THE
        // LATTICE AT EVERY DEPTH, so the board survives a colour-blind eye. That
        // claim was true when it was made and it is the kind of thing that stops
        // being true silently, three commits after somebody nudges one hex value
        // to look nicer — and the failure does not look like a bug, it looks
        // like the game got hard.
        //
        // So it is simulated and asserted rather than believed. Viénot, Brettel
        // and Mollon (1999): to LMS through Hunt-Pointer-Estevez, collapse the
        // missing axis onto the plane the remaining two span, and back.

        public enum Sight { Trichromat, Protan, Deutan, Tritan }

        static readonly float[] ToLms =
        {
            0.31399022f, 0.63951294f, 0.04649755f,
            0.15537241f, 0.75789446f, 0.08670142f,
            0.01775239f, 0.10944209f, 0.87256922f
        };

        static readonly float[] FromLms =
        {
             5.47221206f, -4.64196010f,  0.16963708f,
            -1.12524190f,  2.29317094f, -0.16789520f,
             0.02980165f, -0.19318073f,  1.16364789f
        };

        static readonly Dictionary<Sight, float[]> Collapse = new Dictionary<Sight, float[]>
        {
            { Sight.Protan, new[] { 0f, 1.05118294f, -0.05116099f, 0f, 1f, 0f, 0f, 0f, 1f } },
            { Sight.Deutan, new[] { 1f, 0f, 0f, 0.95130920f, 0f, 0.04866992f, 0f, 0f, 1f } },
            { Sight.Tritan, new[] { 1f, 0f, 0f, 0f, 1f, 0f, -0.86744736f, 1.86727089f, 0f } }
        };

        static void Mul(float[] m, ref float x, ref float y, ref float z)
        {
            float a = m[0] * x + m[1] * y + m[2] * z;
            float b = m[3] * x + m[4] * y + m[5] * z;
            float c = m[6] * x + m[7] * y + m[8] * z;
            x = a; y = b; z = c;
        }

        static float Encode(float c)
        {
            c = Mathf.Clamp01(c);
            return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }

        /// <summary>The same colour, as one of the three dichromacies receives it.</summary>
        public static Color Simulate(Color c, Sight s)
        {
            if (s == Sight.Trichromat) return c;
            float x = Linear(c.r), y = Linear(c.g), z = Linear(c.b);
            Mul(ToLms, ref x, ref y, ref z);
            Mul(Collapse[s], ref x, ref y, ref z);
            Mul(FromLms, ref x, ref y, ref z);
            return new Color(Encode(x), Encode(y), Encode(z), c.a);
        }

        /// <summary>Every eye this build claims to be readable by.</summary>
        public static readonly Sight[] AllSight =
            { Sight.Trichromat, Sight.Protan, Sight.Deutan, Sight.Tritan };

        // ---- the audit, as the interface actually prints it -------------------
        //
        // A contrast rule nobody has written down is a rule that lasts until the
        // next colour is picked. This is the list of pairs the interface really
        // puts on screen, in one place, so the check is against the GAME rather
        // than against a palette swatch — the two stop agreeing the moment
        // somebody prints a hint in Dim on a Card instead of on the void, which
        // is a change no palette-only test would ever notice.
        //
        // `why` is not documentation. It is what a failure prints, and the
        // difference between a red line that names the screen and one that says
        // "pair 14" is whether the next person can fix it.

        public readonly struct Pair
        {
            public readonly string ink, ground, why;
            public readonly float floor;
            public Pair(string ink, string ground, float floor, string why)
            { this.ink = ink; this.ground = ground; this.floor = floor; this.why = why; }
        }

        public static Color Named(string role)
        {
            switch (role)
            {
                case "Void": return Palette.Void;
                case "Panel": return Palette.Panel;
                case "PanelHi": return Palette.PanelHi;
                case "Card": return Palette.Card;
                case "Ink": return Palette.Ink;
                case "Dim": return Palette.Dim;
                case "Dim2": return Palette.Dim2;
                case "Inert": return Palette.Inert;
                case "Grid": return Palette.Grid;
                case "GridHi": return Palette.GridHi;
                case "Rust": return Palette.Rust;
                case "RustHi": return Palette.RustHi;
                case "Core": return Palette.Core;
                case "Fault": return Palette.Fault;
                case "Node": return Palette.Node;
                case "Lock": return Palette.Lock;
                case "Arc": return Palette.Arc;
                case "Trace": return Palette.Trace;
                case "TraceFar": return Palette.TraceFar;
                case "TraceNear": return Palette.TraceNear;
                case "LatticeFar": return Palette.LatticeFar;
                case "LatticeNear": return Palette.LatticeNear;
                default: return Color.magenta;      // loud on purpose: a typo must not read as a pass
            }
        }

        /// <summary>Every ink-on-ground the interface prints, and the floor it has to clear.</summary>
        public static readonly Pair[] Printed =
        {
            new Pair("Ink",  "Void",    AAAText, "the wordmark, and most body text"),
            new Pair("Ink",  "Panel",   AAAText, "a label on a control plate"),
            new Pair("Ink",  "PanelHi", AAAText, "a label on a raised plate"),
            new Pair("Ink",  "Card",    AAAText, "a label on a card"),
            new Pair("Ink",  "Inert",   AAAText, "the OFF half of a switch"),
            new Pair("Dim",  "Void",    AAAText, "the pitch under the masthead"),
            new Pair("Dim",  "Panel",   AAAText, "a hint under a calibrate row"),
            new Pair("Dim",  "Card",    AAAText, "a hint on a card"),
            new Pair("Dim",  "PanelHi", AAAText, "a hint on a raised plate — the worst of the four grounds"),
            new Pair("Rust", "Void",    AAAText, "ENGINE, and every vault caption"),
            new Pair("Rust", "Panel",   AAAText, "the reading beside a slider"),
            new Pair("Void", "Rust",    AAAText, "the primary's label — literally color:#000"),
            new Pair("Core", "Void",    AAAText, "the core readout"),
            new Pair("Fault","Void",    AAAText, "every refusal the interface has to explain"),
            new Pair("Node", "Void",    AAAText, "the node count"),
            new Pair("Lock", "Void",    AAAText, "the hint count"),
            new Pair("Arc",  "Void",    AAAText, "TO GO, on a perfect line"),
            new Pair("Trace","Void",    AAAText, "the trace readout"),

            // graphical objects rather than type: 1.4.11's 3:1, against the thing
            // they have to be told apart FROM rather than against the page
            new Pair("TraceFar", "LatticeNear", NonText, "the board's worst pair: farthest trace, nearest lattice"),
            new Pair("Rust",     "Inert",       NonText, "a slider's reading against its own track"),

            // DIM2 IS A MARK NOW, NOT TYPE, so it is measured against 1.4.11's
            // 3:1 rather than against a text floor — and the five places it was
            // carrying words have moved to Dim. It could not have cleared a text
            // floor and stayed a tier: at 4.5:1 on near-black it lands on top of
            // Dim, and two type tiers that measure the same are one type tier
            // with two names.
            new Pair("Dim2",     "Void",        NonText, "an unlit pip, an unreachable cell, a locked vault's edge"),
            new Pair("Dim2",     "PanelHi",     NonText, "the same marks on a raised plate"),
            new Pair("GridHi",   "Void",        NonText, "the ring round an empty cell in the Forge"),
            new Pair("GridHi",   "Panel",       NonText, "the same ring on a control plate"),
        };
    }
}
