using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// The launcher icon, drawn rather than commissioned — and it is the favicon
    /// the web build already uses, at a size a phone wants.
    ///
    /// Three shapes and three of the nine colours: the void it sits in, the rust
    /// frame of the instrument, and the cyan horizon of the thing inside it. An
    /// icon assembled out of the same palette as the game is the cheapest possible
    /// way for a home screen to look like the thing it opens.
    ///
    /// Generated into an asset rather than at runtime because PlayerSettings wants
    /// a real imported texture, and regenerated only when it is missing so it does
    /// not churn the repository on every build.
    /// </summary>
    public static class AppIcon
    {
        const string Dir = "Assets/Icons";
        const string Path512 = Dir + "/icon.png";
        const int Size = 512;

        [MenuItem("Singularity/Regenerate App Icon")]
        public static void Regenerate()
        {
            if (File.Exists(Path512)) File.Delete(Path512);
            Ensure();
        }

        public static void Ensure()
        {
            if (!File.Exists(Path512)) Write();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path512);
            if (tex == null) { Debug.LogWarning("[Singularity] icon asset missing after write"); return; }

            var icons = new[] { tex };
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);
            // Adaptive icons want a foreground and a background; the design already
            // has a solid ground, so the same square serves as both and nothing
            // gets cropped into a shape it was not drawn for.
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.AdaptiveForeground);
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.AdaptiveBackground);
        }

        static void Write()
        {
            Directory.CreateDirectory(Dir);

            var px = new Color32[Size * Size];
            Color32 voidC = ToC(0x00, 0x00, 0x00);
            Color32 rust = ToC(0xea, 0x58, 0x0c);
            Color32 arc = ToC(0x22, 0xd3, 0xee);

            const float S = 64f;                     // the favicon's own coordinate space
            float k = Size / S;

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / k;
                    float v = (Size - 1 - y + 0.5f) / k;   // PNG rows run down; the design runs up

                    Color32 c = voidC;

                    // the instrument's frame: a 52-unit square inset by 6, stroked 2
                    if (OnRect(u, v, 6f, 6f, 52f, 52f, 2f)) c = rust;

                    // the horizon: a circle of radius 15 about the centre, stroked 3
                    float d = Mathf.Sqrt((u - 32f) * (u - 32f) + (v - 32f) * (v - 32f));
                    if (Mathf.Abs(d - 15f) <= 1.5f) c = arc;

                    px[y * Size + x] = c;
                }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path512, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(Path512, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(Path512) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }
            Debug.Log("[Singularity] wrote " + Path512);
        }

        static bool OnRect(float u, float v, float x, float y, float w, float h, float stroke)
        {
            bool inside = u >= x && u <= x + w && v >= y && v <= y + h;
            if (!inside) return false;
            float e = Mathf.Min(Mathf.Min(u - x, x + w - u), Mathf.Min(v - y, y + h - v));
            return e <= stroke;
        }

        static Color32 ToC(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);
    }
}
