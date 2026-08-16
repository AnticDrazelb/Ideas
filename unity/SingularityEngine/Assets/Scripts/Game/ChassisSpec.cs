using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE MEASUREMENTS OF WHATEVER CASE IS CURRENTLY IN THE MACHINE.
    ///
    /// Chassis used to carry these as consts, with a comment saying a Python
    /// script had printed them. That was true and it was also a trap: the numbers
    /// and the picture they describe could drift apart with nothing to notice,
    /// and swapping the case meant editing C# with a calculator open. Every one of
    /// them is a measurement OF AN ASSET, so they belong to the asset.
    ///
    /// This is a ScriptableObject rather than a JSON file or a header baked into
    /// the PNG because it is the only one of the three the editor can draw an
    /// inspector for, and being able to drag a slider and watch the bezel move is
    /// the entire point of the exercise. See Singularity → Chassis.
    ///
    /// EVERY LENGTH HERE IS IN THE ART'S OWN PIXELS except <see cref="scale"/>,
    /// which is the exchange rate to canvas units and the only number in the file
    /// that is a decision rather than an observation.
    ///
    /// IT IS OPTIONAL, AND THAT IS DELIBERATE. Chassis falls back to the shipped
    /// case's numbers if the asset is missing, so a clone with no .asset file
    /// still draws the housing it was built with. The fallback is not a default
    /// anybody should rely on — it is there so that a missing file is a case that
    /// still fits rather than an interface with no edges.
    /// </summary>
    [CreateAssetMenu(fileName = "chassis", menuName = "Singularity/Chassis Spec")]
    public class ChassisSpec : ScriptableObject
    {
        [Header("the picture")]
        [Tooltip("Width and height of Resources/chassis.png, in its own pixels.")]
        public int artW = 461;
        public int artH = 1018;

        [Header("display edge to glass, per side")]
        [Tooltip("The case is not symmetrical and pretending otherwise puts a screen's " +
                 "own background over the metal on one edge. Four numbers, always.")]
        public float left = 38f;
        public float right = 38f;
        public float top = 60f;
        public float bottom = 62f;

        [Header("the nine-slice")]
        [Tooltip("How much of the art a corner piece takes. Both cuts have to land in a " +
                 "stretch of edge whose profile is CONSTANT — past the notch on the top, " +
                 "above the waist on the side — because what stretches has to be " +
                 "featureless along the direction it stretches in.")]
        public float corner = 160f;

        [Tooltip("How deep each piece has to be to hold all the metal in its stretch. The " +
                 "arms are wider than the bezel by the opening's corner radius, because " +
                 "the metal reaches further in where the glass turns the corner.")]
        public float armW = 58f;
        public float armH = 80f;
        public float sideW = 44f;
        public float bandH = 66f;

        [Header("art pixels to canvas units")]
        [Tooltip("FIXED, rather than whatever makes the case fit. Scaling the art to the " +
                 "display would make the bezel thicker on a tall phone than on a short one " +
                 "and move the layout underneath it — a bad trade for a housing whose whole " +
                 "job is to be the one thing that never moves.")]
        public float scale = 1.25f;

        [Header("how it was cut, so it can be cut again")]
        [Tooltip("The mock-up this case came out of, if it is in the project. Kept as a " +
                 "reference rather than a path so renaming it cannot silently orphan the " +
                 "recipe.")]
        public Texture2D source;

        [Tooltip("Or a path, for a render that is NOT in the project — the reference " +
                 "mock-up lives in unity/tools/chassis and there is no reason to import " +
                 "two thirds of a megabyte of source art into a game that will never ship " +
                 "it. Relative paths are from the project folder, the one above Assets. " +
                 "Ignored when source is set.")]
        public string sourcePath = "";

        [Tooltip("Rows of the source to consider at all. The reference mock-up ends in five " +
                 "rows of pure white, which are not part of any machine.")]
        public int sourceRows = 1024;

        [Tooltip("The case's bounding box within the source: x0, y0, x1, y1, y measured DOWN. " +
                 "Measured by the tool rather than typed — the silhouette knows where it ends.")]
        public Vector4 crop = new Vector4(50, 6, 511, 1024);

        [Tooltip("The recess trough, in source pixels, and the radius its corners turn at. " +
                 "This is the one thing the tool cannot find for you: on a mock-up with the " +
                 "game running on the screen, the glass is not reliably darker than the metal " +
                 "around it. Nudge it and watch the overlay.")]
        public Vector4 glass = new Vector4(88, 66, 472, 961);
        public float glassRadius = 15f;

        [Tooltip("Luminance under which a pixel may be background. The flood fill starts at " +
                 "the border and can only cross pixels below this, so it finds the black " +
                 "around the case and cannot leak inside it — the case's own darkest metal " +
                 "is about twice this.")]
        public float darkBelow = 9f;

        /// <summary>
        /// The one that is checked rather than trusted: a spec whose crop does not
        /// match the art it describes will place all twelve pieces wrong, and it
        /// will do it subtly — a few pixels of seam rather than a visible fault.
        /// </summary>
        public bool Consistent =>
            artW > 0 && artH > 0 &&
            Mathf.Approximately(artW, crop.z - crop.x) &&
            Mathf.Approximately(artH, crop.w - crop.y);
    }
}
