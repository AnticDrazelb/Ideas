using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Singularity.EditorTools
{
    /// <summary>
    /// EVERY PROJECT SETTING THIS GAME NEEDS, APPLIED IN CODE.
    ///
    /// There is no hand-written ProjectSettings.asset in this repository, and that
    /// is deliberate. It is a seven-hundred-line serialised blob whose diffs are
    /// unreadable, whose merges are unresolvable, and which silently carries
    /// whatever the last person's editor happened to think — so the settings that
    /// actually matter to this game live here instead, as a list with the reasons
    /// attached. Unity writes its own defaults for everything not named below.
    ///
    /// It runs once on first open, marks itself done in EditorPrefs, and is also
    /// available from the menu and the command line so a build machine can apply
    /// it without a human.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "singularity.setup.v1";

        static ProjectSetup()
        {
            // Deferred: touching PlayerSettings during the static constructor of an
            // InitializeOnLoad class runs while the editor is still importing, and
            // an asset database write there is how you get a project that only
            // opens correctly the second time.
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(DoneKey, false)) { EnsureScene(); return; }
                Apply();
                EditorPrefs.SetBool(DoneKey, true);
            };
        }

        [MenuItem("Singularity/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "Singularity";
            PlayerSettings.productName = "Singularity Engine";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.singularity.engine");

            // GAMMA, NOT LINEAR, AND THIS ONE IS NOT A PREFERENCE.
            //
            // The whole palette is nine hex values somebody picked by eye, and the
            // board is read off one property: a trace is brighter than the lattice
            // at every depth. Linear colour space re-maps every one of those values
            // and the two depth ramps stop being the distances they were authored
            // to be. Gamma keeps the Unity build looking like the web build,
            // because the web build is sRGB and so is the brief.
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // The interface is composed at 720x1280, but the board is centred in
            // whatever the HUD is not using rather than on the screen — so a
            // rotation is a re-layout the game already knows how to do. Both ways
            // up, and upside-down portrait left off because a phone that flips
            // while it is put down on a table is not a request.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // A puzzle game has nothing to gain from a hot phone, and the frame
            // rate is set again at runtime in Bootstrap for platforms that ignore
            // this.
            QualitySettings.vSyncCount = 0;

            // 64-bit only, because that is what a store requires and what every
            // device this will run on has. IL2CPP is the only backend that reaches
            // ARM64 — it needs the Android NDK, which comes with the Android Build
            // Support module.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 25 is Unity's own floor now — 23 is deprecated and becomes an error
            // in the next release. It is Android 7.1, which is a decade of phones.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Nothing here talks to a network. The daily is computed from the date,
            // the cubes are computed from their numbers, and the only thing that
            // would ever need a connection is a leaderboard that does not exist yet.
            PlayerSettings.Android.forceInternetPermission = false;

            // An APK, not an app bundle: this is for sideloading onto a phone.
            EditorUserBuildSettings.buildAppBundle = false;
            // The debug keystore, so a build needs no secrets to exist.
            PlayerSettings.Android.useCustomKeystore = false;

            TrySilenceSplash();

            EnsureScene();
            EnsureShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("[Singularity] project settings applied");
        }

        static void TrySilenceSplash()
        {
            // Only some licences may turn this off, and a setup step that throws on
            // a Personal seat is worse than a splash screen.
            try { PlayerSettings.SplashScreen.show = false; }
            catch { /* Personal licence: the splash stays, and that is fine */ }
        }

        /// <summary>
        /// A BUILD WITH NO SCENES PRODUCES AN APP THAT INSTALLS AND SHOWS NOTHING,
        /// which is the single most confusing failure a fresh clone can hand you.
        ///
        /// The scene is deliberately empty — every object is created from
        /// <c>Bootstrap</c> at <c>RuntimeInitializeOnLoadMethod</c>, so there is no
        /// serialised reference anywhere that can come unhooked. It still has to
        /// EXIST and be in the build list, and if it has gone missing this makes a
        /// new one rather than failing.
        /// </summary>
        /// <summary>
        /// THE EMPTY SCENE'S ONE REAL COST, AND IT COSTS THE WHOLE GAME.
        ///
        /// Every shader here is reached by <c>Shader.Find</c> and NOTHING in the
        /// project references any of them: the scene is empty on purpose, there
        /// are no materials, no prefabs, no renderers. Unity's build pipeline
        /// decides what to include by REFERENCE, so five shaders with zero
        /// references are five shaders that do not ship. In the editor everything
        /// is loaded and there is no symptom; in a player Shader.Find returns
        /// null, every material is built on nothing, and the game runs perfectly
        /// while drawing nothing at all.
        ///
        /// THERE IS NO PUBLIC API FOR THIS LIST, and that is worth knowing before
        /// anybody goes looking for one. GraphicsSettings has no
        /// alwaysIncludedShaders property — the Always Included Shaders array
        /// lives only in the serialised GraphicsSettings asset, as
        /// m_AlwaysIncludedShaders, and SerializedObject is the supported way in.
        /// It is uglier than a property and it is the only thing that exists.
        /// </summary>
        public static void EnsureShaders()
        {
            // ONE LIST, AND IT IS THE ONE THE GAME DEMANDS AT BOOT.
            //
            // This was a second copy of Shaders.Required, annotated, and the two
            // could go out of step in the one direction that matters: a shader
            // the game refuses to start without, missing from the array that
            // decides what ships. Shaders.Required is the list; the reasons live
            // beside it there.
            //
            // "Singularity/Filter" is in neither. Brightness and contrast moved
            // off the camera and onto an overlay above every canvas — see
            // ScreenFilter — so nothing loads that shader. The file stays in
            // Assets/Shaders because a camera-space filter may be wanted again;
            // demanding it at boot would be demanding something nothing uses.
            string[] want = Singularity.Game.Shaders.Required;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[Singularity] cannot open ProjectSettings/GraphicsSettings.asset");
                return;
            }

            var so = new SerializedObject(asset[0]);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogError("[Singularity] GraphicsSettings has no m_AlwaysIncludedShaders — " +
                               "add the Singularity shaders by hand under Project Settings > Graphics");
                return;
            }

            int added = 0;
            foreach (string name in want)
            {
                Shader sh = Shader.Find(name);
                if (sh == null)
                {
                    Debug.LogError("[Singularity] shader " + name + " is missing from the project entirely");
                    continue;
                }

                bool have = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) { have = true; break; }
                if (have) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                added++;
            }

            if (added == 0) return;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[Singularity] added " + added + " shader(s) to Always Included Shaders — " +
                      "without this a built player draws nothing at all");
        }

        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.ImportAsset(ScenePath);
                Debug.Log("[Singularity] created " + ScenePath);
            }

            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath && s.enabled) return;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Singularity] added " + ScenePath + " to the build");
        }
    }
}
