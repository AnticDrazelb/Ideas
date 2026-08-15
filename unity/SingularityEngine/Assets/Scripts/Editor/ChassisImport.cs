using UnityEditor;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// HOW THE ONE IMPORTED TEXTURE IS IMPORTED, IN CODE.
    ///
    /// The same argument as ProjectSetup, for the same reason: the alternative is
    /// a .meta file, and a .meta is a serialised blob carrying whatever the last
    /// person's editor happened to think. There is not one in this repository and
    /// this is not going to be the first.
    ///
    /// Three of these settings are not taste, they are correctness:
    ///
    /// NPOT SCALE. Unity's default for a texture this shape is ToNearest, which
    /// would quietly resample a 461x1018 picture to 512x1024. Chassis addresses
    /// that picture by MEASURED PIXEL — the bezel is 38 across, a corner piece is
    /// 160 — so a helpful resample would move every one of those numbers and put
    /// the seams a few units off the metal.
    ///
    /// MIPMAPS. The case is drawn at about one to one and never minified. Mips
    /// would cost a third more memory to make it softer.
    ///
    /// ALPHA IS TRANSPARENCY. The art was cut to its own silhouette, so the
    /// colour under the transparent parts is meaningless; this is what tells the
    /// importer to bleed the edges rather than average them toward it, and
    /// without it a bilinear sample along the cut edge fetches darkness that was
    /// never in the picture.
    /// </summary>
    public class ChassisImport : AssetPostprocessor
    {
        const string Path = "Assets/Resources/chassis.png";

        void OnPreprocessTexture()
        {
            if (assetPath != Path) return;

            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = false;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.alphaIsTransparency = true;

            // A rusted plate is the worst case for a block compressor and this is
            // the only texture in the game, so it gets the good one. Two megabytes
            // uncompressed, and the alternative is banding across every corner.
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.maxTextureSize = 2048;

            // nothing reads it back on the CPU — Sprite.Create does not need it —
            // and a readable texture keeps a second copy in system memory forever
            t.isReadable = false;
        }
    }
}
