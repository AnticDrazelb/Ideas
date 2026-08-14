using System.Collections.Generic;
using UnityEngine;
using Singularity.Game;

namespace Singularity.UI
{
    /// <summary>
    /// EVERY DRAWN MARK IN THE INTERFACE, COPIED OUT OF THE SHIPPED FILE.
    ///
    /// These strings are the markup from `assets/game/index.html` inside the APK,
    /// unedited. That is the point of them: an icon redrawn by hand from a
    /// screenshot is a different icon that resembles the original, and the whole
    /// of this pass is the claim that the Unity build and the shipped build are
    /// the same picture. Keeping the source verbatim means the claim is checkable
    /// — diff these against the file and they either match or they do not.
    ///
    /// Four families, and each has its own stroke weight in the sheet:
    ///
    ///   .chip .ic   16x16   stroke 1.4   the HUD readouts
    ///   .rIc        16x16   stroke 1.3   a calibrate row
    ///   .oIc        40x40   stroke 1.6   an object tile in the manual
    ///   .dia        96x56   stroke 1.6   a manual diagram
    ///
    /// The colour is not in the markup for the last two — it comes from the class
    /// on the &lt;svg&gt; itself, which is not carried here — so it is passed in.
    /// </summary>
    public static class Art
    {
        // ---- the HUD chips ---------------------------------------------------

        public const string IcTurn =
            "<rect x=\"1.5\" y=\"2.5\" width=\"13\" height=\"11\" rx=\"1.5\"/><path d=\"M8 2.5v11\"/>";

        public const string IcNode =
            "<path d=\"M8 1.6 14.4 8 8 14.4 1.6 8Z\"/>";

        public const string IcGo =
            "<circle cx=\"8\" cy=\"8\" r=\"6.2\"/><circle cx=\"8\" cy=\"8\" r=\"2.2\"/>";

        public const string IcHint =
            "<path d=\"M8 1.8a4.4 4.4 0 0 0-2.6 7.95V11.5h5.2V9.75A4.4 4.4 0 0 0 8 1.8Z\"/>" +
            "<path d=\"M6.4 13.6h3.2\"/>";

        // ---- the calibrate rows ---------------------------------------------

        public const string RicSound =
            "<path d=\"M2.5 6.2h2.4L8.4 3v10L4.9 9.8H2.5Z\"/><path d=\"M11 5.6a3.4 3.4 0 0 1 0 4.8\"/>" +
            "<path d=\"M12.9 3.6a6 6 0 0 1 0 8.8\"/>";

        public const string RicHaptic =
            "<rect x=\"5.2\" y=\"1.8\" width=\"5.6\" height=\"12.4\" rx=\"1.2\"/><path d=\"M2.4 5.6v4.8M13.6 5.6v4.8\"/>";

        public const string RicFx =
            "<path d=\"M8 1.6 9.7 6.3 14.4 8 9.7 9.7 8 14.4 6.3 9.7 1.6 8 6.3 6.3Z\"/>";

        public const string RicToGo =
            "<circle cx=\"8\" cy=\"8\" r=\"6.2\"/><circle cx=\"8\" cy=\"8\" r=\"2.2\"/>";

        public const string RicDepth =
            "<rect x=\"1.8\" y=\"1.8\" width=\"8\" height=\"8\" rx=\"1\"/><path d=\"M6.2 6.2h8v8h-8\"/>";

        public const string RicBuzz =
            "<path d=\"M1.6 8h2.2l2-4.4L8 12.4l2.2-6 1.6 1.6h2.6\"/>";

        public const string RicBright =
            "<circle cx=\"8\" cy=\"8\" r=\"3.1\"/>" +
            "<path d=\"M8 1v2M8 13v2M1 8h2M13 8h2M3.1 3.1l1.4 1.4M11.5 11.5l1.4 1.4M12.9 3.1l-1.4 1.4M4.5 11.5l-1.4 1.4\"/>";

        public const string RicContrast =
            "<circle cx=\"8\" cy=\"8\" r=\"6.2\"/><path d=\"M8 1.8v12.4a6.2 6.2 0 0 0 0-12.4Z\"/>";

        // ---- the four objects ------------------------------------------------

        public const string ObjTrace = "<rect x=\"7\" y=\"7\" width=\"26\" height=\"26\" rx=\"4\"/>";

        public const string ObjNode =
            "<path d=\"M20 5 35 20 20 35 5 20Z\"/><path d=\"M20 13.5 26.5 20 20 26.5 13.5 20Z\" fill=\"currentColor\"/>";

        public const string ObjLock =
            "<path d=\"M20 4.5 33.4 12.2v15.6L20 35.5 6.6 27.8V12.2Z\"/><path d=\"M12 24 24 12M16 28 28 16\"/>";

        public const string ObjPlate =
            "<circle cx=\"20\" cy=\"20\" r=\"14\"/><path d=\"M11 11 29 29\"/>";

        // ---- the manual's diagrams -------------------------------------------
        //
        // A sentence explaining a SPATIAL rule is the worst tool available: "two
        // traces that line up when you fold are connected" is nine words that mean
        // nothing until you have seen it happen, and a picture of two faces coming
        // together with an arrow between them means it immediately.

        public const string DiaFold =
            "<g class=\"dLine\"><path d=\"M10 12 30 6v38l-20 6Z\"/><path d=\"M66 12 86 6v38l-20 6Z\"/></g>" +
            "<g class=\"dTrace\"><path d=\"M14 20h12v9H14z\"/><path d=\"M70 27h12v9H70z\"/></g>" +
            "<g class=\"dRust\"><path d=\"M40 27h14\"/><path d=\"M50 23l6 4-6 4\"/></g>";

        public const string DiaSwipe =
            "<g class=\"dRust\">" +
            "<path d=\"M48 8v13M48 48V35M28 28H15M68 28h13\"/>" +
            "<path d=\"M44 12l4-5 4 5M44 44l4 5 4-5M19 24l-5 4 5 4M77 24l5 4-5 4\"/></g>" +
            "<g class=\"dLine\"><rect x=\"34\" y=\"21\" width=\"28\" height=\"14\" rx=\"2\"/></g>";

        public const string DiaTap =
            "<g class=\"dTrace\"><rect x=\"10\" y=\"18\" width=\"20\" height=\"20\" rx=\"2\"/>" +
            "<rect x=\"38\" y=\"18\" width=\"20\" height=\"20\" rx=\"2\"/>" +
            "<rect x=\"66\" y=\"18\" width=\"20\" height=\"20\" rx=\"2\"/></g>" +
            "<circle cx=\"20\" cy=\"28\" r=\"6\" fill=\"#000\" class=\"dInk\"/>" +
            "<g class=\"dArc\"><circle cx=\"76\" cy=\"28\" r=\"9\"/><circle cx=\"76\" cy=\"28\" r=\"13.5\" opacity=\".45\"/></g>";

        public const string DiaCore =
            "<g class=\"dCore\"><circle cx=\"48\" cy=\"28\" r=\"16\" opacity=\".35\"/>" +
            "<circle cx=\"48\" cy=\"28\" r=\"10\" opacity=\".7\"/>" +
            "<circle cx=\"48\" cy=\"28\" r=\"3.4\" fill=\"currentColor\"/>" +
            "<path d=\"M22 28h7M67 28h7M48 2v7M48 47v7\"/></g>";

        public const string DiaMatrix =
            "<g class=\"dLine\"><path d=\"M34 12h30v30H34z\"/><path d=\"M34 12 44 5h30l-10 7M64 42l10-7V5\"/></g>" +
            "<g class=\"dTrace\"><rect x=\"38\" y=\"16\" width=\"10\" height=\"10\" rx=\"1.5\"/>" +
            "<rect x=\"50\" y=\"28\" width=\"10\" height=\"10\" rx=\"1.5\"/></g>" +
            "<g class=\"dArc\"><circle cx=\"49\" cy=\"27\" r=\"17\" opacity=\".5\"/></g>";

        public const string DiaToGo =
            "<g class=\"dArc\"><circle cx=\"48\" cy=\"28\" r=\"13\"/><circle cx=\"48\" cy=\"28\" r=\"4.5\"/></g>" +
            "<g class=\"dRust\"><path d=\"M18 28h12M66 28h12\"/></g>";

        // ---- building --------------------------------------------------------
        //
        // Rasterising is not free — a diagram is a hundred thousand distance
        // queries — so each mark is built once, on the first screen that asks for
        // it, and kept. There are twenty-two of them in the whole game.

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// How many device pixels a mark gets per viewBox unit. The marks are laid
        /// out in CSS pixels at roughly their viewBox size, so this is the device's
        /// own density with a little headroom — enough that a 16-unit icon on a
        /// three-times screen is drawn at 96 and never resampled up.
        /// </summary>
        static float Px => Mathf.Clamp(Css.Density * 2f, 2f, 8f);

        public static Sprite Chip(string key, string markup)
            => Get("c." + key, markup, 16f, 16f, 1.4f, Color.white);

        public static Sprite Row(string key, string markup)
            => Get("r." + key, markup, 16f, 16f, 1.3f, Color.white);

        public static Sprite Obj(string key, string markup, Color ink)
            => Get("o." + key, markup, 40f, 40f, 1.6f, ink);

        public static Sprite Dia(string key, string markup)
            => Get("d." + key, markup, 96f, 56f, 1.6f, Palette.Dim2);

        static Sprite Get(string key, string markup, float w, float h, float stroke, Color ink)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            s = Svg.Make(markup, w, h, stroke, ink, Px);
            Cache[key] = s;
            return s;
        }
    }
}
