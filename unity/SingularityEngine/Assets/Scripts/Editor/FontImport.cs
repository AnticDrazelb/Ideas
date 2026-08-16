using UnityEditor;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// HOW THE ONE IMPORTED TYPEFACE IS IMPORTED, IN CODE.
    ///
    /// Same argument as ChassisImport and ProjectSetup: the alternative is a .meta
    /// file, which is a serialised blob carrying whatever the last person's editor
    /// happened to think, and there is not one in this repository.
    ///
    /// DYNAMIC, NOT AN ATLAS. The other font mode bakes a fixed character set into
    /// a texture at authoring time, which is right for a game whose type is all
    /// one size and all known in advance. This one is neither: the interface runs
    /// from 17pt captions to a 44pt heading, best-fit slides a name through every
    /// size in between, and the Forge lets a player name a cube whatever their
    /// keyboard can produce. Dynamic rasterises what is actually asked for.
    ///
    /// THE FONT DATA HAS TO TRAVEL. includeFontData is what puts the .ttf in the
    /// player build instead of a reference to a face the device is assumed to
    /// have. Without it the game renders in whatever the OS substitutes — which on
    /// Android is Roboto, i.e. the proportional face this commit just spent a day
    /// getting away from — and the licence obligation stops being satisfiable
    /// because there is no longer a font being distributed to attach it to.
    ///
    /// HINTED, AND SMOOTH. The smallest type in the game is 17 units on a 720-wide
    /// canvas, so on a phone it lands somewhere near 17 device pixels and on a
    /// small desktop window rather less. Unhinted stems at that size go soft and
    /// uneven — one vertical lands on a pixel boundary and its neighbour does not,
    /// and a monospace makes that especially visible because the eye has a column
    /// of identical stems to compare down the page. Hinting snaps them. Smooth
    /// keeps the antialiasing, because the alternative is a hard raster that
    /// crawls when the panel breathes.
    /// </summary>
    public class FontImport : AssetPostprocessor
    {
        const string Face = "Assets/Resources/mono.ttf";

        void OnPreprocessAsset()
        {
            if (assetPath != Face) return;
            var t = assetImporter as TrueTypeFontImporter;
            if (t == null) return;

            t.fontTextureCase = FontTextureCase.Dynamic;
            t.includeFontData = true;
            t.fontRenderingMode = FontRenderingMode.HintedSmooth;

            // FACE, NOT LEGACY. The two calculations differ by about eight percent
            // of the em on this font, and every vertical measurement in the
            // interface — the four rows of the plate lesson especially — was taken
            // against the face's own ascent and descent. Legacy would move all of
            // them at once.
            t.ascentCalculationMode = AscentCalculationMode.FaceAscender;
        }
    }
}
