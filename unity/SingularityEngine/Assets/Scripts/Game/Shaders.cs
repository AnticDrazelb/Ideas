using UnityEngine;
using UnityEngine.UI;

namespace Singularity.Game
{
    /// <summary>
    /// DID THE SHADERS ACTUALLY SHIP?
    ///
    /// Every shader in this game is reached by <c>Shader.Find</c>, and nothing in
    /// the project references any of them — the scene is empty on purpose, there
    /// are no materials, no prefabs and no renderers. That is the property the
    /// whole project is built on, and this is what it costs: Unity decides what
    /// to include in a player by REFERENCE, so five shaders with zero references
    /// are five shaders that do not ship.
    ///
    /// In the editor every asset is loaded, so Shader.Find works and there is no
    /// symptom at all. In a player it returns null, every material is built on
    /// nothing, and the game runs perfectly while drawing nothing. Splash, then
    /// black — no exception, no error, no clue.
    ///
    /// The fix is Always Included Shaders, applied by ProjectSetup. This is the
    /// second line of defence: if it was not applied, the game says so IN WORDS
    /// ON THE SCREEN rather than presenting as a dead device.
    ///
    /// IT DRAWS WITH UNITY'S OWN UI SHADER, deliberately. Anything of ours is
    /// exactly what is being reported missing, so the message has to be made of
    /// something that cannot be stripped: uGUI's default material ships whenever
    /// the UI module does.
    /// </summary>
    public static class Shaders
    {
        /// <summary>The five, in the order they matter. Cell first: without it there is no game.</summary>
        public static readonly string[] Required =
        {
            "Singularity/Cell",
            "Singularity/Glyph",
            "Singularity/Fx",
            // "Singularity/Filter" is NOT here any more. Brightness and contrast
            // moved off the camera and onto an overlay above every canvas — see
            // ScreenFilter — so nothing loads that shader. The file stays in
            // Assets/Shaders because a camera-space filter may be wanted again;
            // demanding it at boot would be demanding something nothing uses.
            "Singularity/Bloom",
        };

        public static bool Ok(out string missing)
        {
            missing = "";
            foreach (string name in Required)
                if (Shader.Find(name) == null)
                    missing = missing.Length == 0 ? name : missing + ", " + name;
            return missing.Length == 0;
        }

        /// <summary>
        /// Say it on the screen, at a size that survives a phone, in the one
        /// material that cannot itself be the thing that is missing.
        /// </summary>
        public static void Complain(string missing)
        {
            Debug.LogError("[Singularity] shaders missing from this build: " + missing +
                           ". Nothing will draw. Run Singularity -> Apply Project Settings, " +
                           "which adds them to Always Included Shaders, then rebuild.");

            var go = new GameObject("Shaders missing", typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(go);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 32767;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280);

            var t = new GameObject("text", typeof(Text));
            t.transform.SetParent(go.transform, false);
            var text = t.GetComponent<Text>();
            text.font = Singularity.UI.UiKit.Mono;
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.90f, 0.86f, 0.82f);
            text.text = "SHADERS MISSING FROM THIS BUILD\n\n" + missing.Replace(", ", "\n") +
                        "\n\nNothing can be drawn.\n\n" +
                        "In Unity: Singularity → Apply Project Settings,\nthen rebuild.";

            var rt = (RectTransform)t.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(48, 48);
            rt.offsetMax = new Vector2(-48, -48);
        }
    }
}
