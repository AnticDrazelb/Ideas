using Singularity.Game;
using UnityEditor;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// THE HOUSING, IN THE GAME VIEW, WITHOUT PRESSING PLAY.
    ///
    /// This is the honest answer to "make it a real scene". The scene here is
    /// empty on purpose — every object is built in code at
    /// RuntimeInitializeOnLoadMethod, so there is no serialised reference
    /// anywhere that can come unhooked, and that property is worth more than
    /// anything a hand-authored .unity would buy. What the empty scene actually
    /// costs is the thing people mean when they ask for a real one: **you cannot
    /// see any of it in the editor.** Every layout question — does the case fit
    /// this aspect, does the glass line up with the bezel, do the scanlines land
    /// inside the opening — needs a build, or at least a play session, and the
    /// Game view sits there showing nothing.
    ///
    /// So the scene gets populated on demand, FROM THE SAME CODE. Singularity →
    /// Housing → Show builds the chassis, the glass and the scanlines exactly as
    /// the game builds them; drag the Game view to any aspect and the housing
    /// re-fits live. Nothing here duplicates the game's layout, which is the
    /// entire point — a preview that is a second implementation is a preview
    /// that can be right while the game is wrong.
    ///
    /// EVERYTHING IT MAKES IS DontSave. Not a convention, a guarantee: these
    /// objects cannot be serialised into Main.unity by somebody hitting Ctrl-S
    /// with the preview up, so the empty scene stays empty and this cannot
    /// become the thing it exists to avoid.
    ///
    /// The two Fit components carry [ExecuteAlways] for this, and that is the
    /// only change the runtime needed. They lay the case out when the panel's
    /// size changes, which in the editor is every time the Game view is
    /// resized — so the preview is live rather than a snapshot.
    /// </summary>
    public static class HousingPreview
    {
        const string Root = "~Housing Preview";
        const string Menu = "Singularity/Housing/";

        [MenuItem(Menu + "Show")]
        public static void Show()
        {
            Hide();

            var root = new GameObject(Root);
            root.hideFlags = HideFlags.DontSave;

            // A CAMERA CLEARING TO THE GAME'S OWN BLACK, because the alternative
            // is the editor's blue-grey skybox behind a housing that is supposed
            // to be a machine in the dark, and every judgement about how the
            // metal reads is a judgement against its background.
            var camGo = new GameObject("preview camera", typeof(Camera));
            camGo.hideFlags = HideFlags.DontSave;
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // The three canvases the game makes, in the order it makes them:
            // the case at 5, the rows at 24, the dirt at 25.
            Adopt(root, Chassis.Build());
            Adopt(root, Scanlines.Build());
            Adopt(root, Glass.Build());

            Selection.activeGameObject = root;
            SceneView.RepaintAll();
        }

        [MenuItem(Menu + "Hide")]
        public static void Hide()
        {
            var old = GameObject.Find(Root);
            if (old == null) return;

            // THE KIT'S CANVAS LIST HAS TO BE TOLD. UiKit keeps every canvas it
            // has made so legibility can repaint the interface where it stands,
            // and a static list in the editor outlives the objects in it —
            // without this, showing the preview twice leaves the first set of
            // destroyed canvases in there forever and the next Repaint walks
            // them.
            Singularity.UI.UiKit.Canvases.Clear();

            Object.DestroyImmediate(old);
            SceneView.RepaintAll();
        }

        [MenuItem(Menu + "Hide", true)]
        static bool CanHide() => GameObject.Find(Root) != null;

        /// <summary>
        /// Under the one root, so Hide is a single DestroyImmediate and so the
        /// hierarchy has one line in it rather than four. Reparenting a canvas is
        /// fine — a screen-space overlay does not care where it hangs — and it is
        /// only possible at all because UiKit no longer calls DontDestroyOnLoad
        /// outside play mode, which would have pinned these to a scene of their
        /// own.
        /// </summary>
        static void Adopt(GameObject root, Canvas c)
        {
            if (c == null) return;
            c.gameObject.hideFlags = HideFlags.DontSave;
            c.transform.SetParent(root.transform, false);
            foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }
    }
}
