using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// The entry point, and there is deliberately nothing in the scene.
    ///
    /// Every object this game needs — camera, cube, canvases, materials, glyph
    /// textures — is made in code, so the scene file is empty and there is no
    /// serialised reference anywhere that could come unhooked. A Unity project
    /// whose behaviour lives entirely in .cs files is one you can review in a
    /// diff, merge without conflicts, and reason about without opening the editor.
    ///
    /// The web original had the same property for the same reason: one file, no
    /// assets, nothing to load.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // A puzzle game has nothing to gain from a hot phone. Sixty is plenty
            // and the battery is part of the experience.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            // UGUI does not route a single click without one of these, and nothing
            // in the scene creates it because there is nothing in the scene. This
            // is the one piece of Unity plumbing the game cannot supply for itself.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                                        typeof(UnityEngine.EventSystems.EventSystem),
                                        typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Object.DontDestroyOnLoad(es);
            }

            var go = new GameObject("SINGULARITY ENGINE");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameDirector>();
        }
    }
}
