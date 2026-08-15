using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

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
                // EditorPrefs is per EDITOR INSTALL, not per project, so a second
                // clone on the same machine arrives with the flag already set and
                // skips the whole of Apply. Both of these are idempotent and cheap,
                // and both describe state that lives in files this repository does
                // not carry — the scene list and GraphicsSettings.asset — so they
                // run on every open rather than trusting the flag.
                if (EditorPrefs.GetBool(DoneKey, false)) { EnsureScene(); EnsureShadersInBuild(); return; }
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

            // THE APP TAKES THE WHOLE DISPLAY, CUTOUT INCLUDED.
            //
            // Left alone, Android keeps a notched phone's app clear of the cutout
            // by letterboxing the entire band it sits in — you lose a strip the
            // full width of the display to hold one camera. Asking for the pixels
            // is the only way to get them; what is DRAWN out there is then this
            // game's problem, and it already has the measurement it needs, because
            // Screen.safeArea is honoured by Layout.BoardRect and by UiKit's
            // SafeArea on every canvas.
            PlayerSettings.Android.renderOutsideSafeArea = true;

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
            EnsureShadersInBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[Singularity] project settings applied");
        }

        /// <summary>
        /// EVERY SHADER THIS GAME USES, NAMED, BECAUSE NOTHING ELSE NAMES THEM.
        ///
        /// Unity puts a shader in a player build if a material in a built scene
        /// references it, if something under Resources references it, or if it is
        /// listed here. This project does none of the first two ON PURPOSE — the
        /// scene is empty and every material is <c>new Material(Shader.Find(...))</c>
        /// at runtime — so without this list all five are stripped.
        ///
        /// That failure is invisible in the editor and total on the device.
        /// <c>Shader.Find</c> searches the whole asset database in the editor and
        /// only the built-in set in a player, so the editor looks perfect and the
        /// phone gets null for all five, the cage material throws on construction,
        /// <c>GameDirector.Awake</c> dies at the fourth line, nothing after it is
        /// ever built, and the camera clears an empty scene to <c>Palette.Void</c>.
        /// A black screen, from a project that runs.
        /// </summary>
        public static readonly string[] Shaders =
        {
            "Singularity/Cell",     // the board — every cell, at every depth
            "Singularity/Fx",       // the cage, and the effects quads
            "Singularity/Glyph",    // the glyph atlas, and the glass
            "Singularity/Bloom",    // the rim on the picture
            "Singularity/Grade",    // brightness and contrast, over everything
        };

        /// <summary>
        /// Adds anything in <see cref="Shaders"/> that is not already in Graphics
        /// Settings → Always Included Shaders. Returns false if a shader named
        /// above is not in the project at all, which is a build worth stopping.
        /// </summary>
        public static bool EnsureShadersInBuild()
        {
            var so = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogError("[Singularity] no m_AlwaysIncludedShaders on GraphicsSettings");
                return false;
            }

            var have = new HashSet<Shader>();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Shader s && s != null)
                    have.Add(s);

            bool ok = true, changed = false;
            foreach (string name in Shaders)
            {
                Shader sh = Shader.Find(name);
                if (sh == null)
                {
                    // Not "will be stripped" — not in the project. A rename, or a
                    // compile error in the .shader, and both need a human.
                    Debug.LogError("[Singularity] shader missing from the project: " + name);
                    ok = false;
                    continue;
                }

                if (!have.Add(sh)) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                changed = true;
                Debug.Log("[Singularity] always-included: " + name);
            }

            if (changed) so.ApplyModifiedProperties();
            return ok;
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
