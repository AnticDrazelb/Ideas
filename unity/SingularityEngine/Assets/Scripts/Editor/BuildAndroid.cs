using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Singularity.EditorTools
{
    /// <summary>
    /// One button and one command line, producing the same APK.
    ///
    /// The command-line entry point is the one that matters: it applies the
    /// project settings first, so a fresh clone on a machine that has never opened
    /// this project builds the same thing as a machine that has. A build that
    /// depends on somebody having clicked through the editor once is not a build,
    /// it is a ritual.
    /// </summary>
    public static class BuildAndroid
    {
        const string OutDir = "Builds";

        [MenuItem("Singularity/Build Android APK %#b")]
        public static void FromMenu() => Run(development: true, bundle: false);

        [MenuItem("Singularity/Build Play Bundle (AAB)")]
        public static void BundleFromMenu() => Run(development: false, bundle: true);

        /// <summary>
        /// Invoked by <c>build-android.sh</c>. Exits with a non-zero code on
        /// failure so a build machine notices.
        /// </summary>
        public static void CommandLine()
        {
            string[] argv = Environment.GetCommandLineArgs();
            bool release = Array.IndexOf(argv, "-release") >= 0;
            bool bundle = Array.IndexOf(argv, "-aab") >= 0;
            bool ok = Run(development: !release && !bundle, bundle: bundle);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(bool development, bool bundle)
        {
            ProjectSetup.Apply();

            // Apply already registered these. Asking again is the cheap half of a
            // trade: a shader that is missing here is a shader the player will get
            // null for, and null for a shader is not a worse-looking build, it is a
            // black screen that installs and launches and shows nothing. Better the
            // build machine stops than the phone does.
            if (!ProjectSetup.EnsureShadersInBuild())
            {
                Debug.LogError("[Singularity] refusing to build without every shader — see above");
                return false;
            }

            AppIcon.Ensure();

            ApplyVersion();
            EditorUserBuildSettings.buildAppBundle = bundle;

            // A PLAY UPLOAD MUST BE SIGNED WITH A KEY THAT IS NOT THE DEBUG ONE,
            // and it must be the SAME key every time for the life of the listing —
            // losing it means never updating the app again. So it is read from the
            // environment and never from this repository: a keystore committed
            // alongside its password is not a keystore.
            if (!ConfigureSigning(requireRelease: bundle || !development)) return false;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[Singularity] switching to Android…");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[Singularity] could not switch to Android. Is Android Build Support installed?");
                    return false;
                }
            }

            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, bundle ? "SingularityEngine.aab" : "SingularityEngine.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Singularity] built {path} — {s.totalSize / 1048576f:0.0} MB in {s.totalTime.TotalSeconds:0}s");
                return true;
            }

            Debug.LogError($"[Singularity] build {s.result}: {s.totalErrors} error(s)");
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Debug.LogError("[Singularity] " + step.name + ": " + m.content);

            return false;
        }

        /// <summary>
        /// PLAY REJECTS AN UPLOAD WHOSE VERSION CODE IT HAS ALREADY SEEN, and it
        /// does so after the upload rather than before it. The code comes from the
        /// environment so a release pipeline can bump it; the name is what a human
        /// reads on the listing.
        /// </summary>
        static void ApplyVersion()
        {
            string name = Environment.GetEnvironmentVariable("SE_VERSION_NAME");
            string code = Environment.GetEnvironmentVariable("SE_VERSION_CODE");

            if (!string.IsNullOrEmpty(name)) PlayerSettings.bundleVersion = name;
            else if (string.IsNullOrEmpty(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "0.1.0";

            if (!string.IsNullOrEmpty(code) && int.TryParse(code, out int n))
                PlayerSettings.Android.bundleVersionCode = n;
        }

        static bool ConfigureSigning(bool requireRelease)
        {
            string store = Environment.GetEnvironmentVariable("SE_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("SE_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("SE_KEY_ALIAS");
            string aliasPass = Environment.GetEnvironmentVariable("SE_KEY_PASS");

            bool haveAll = !string.IsNullOrEmpty(store) && !string.IsNullOrEmpty(storePass)
                           && !string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(aliasPass);

            if (!haveAll)
            {
                if (requireRelease)
                {
                    Debug.LogError(
                        "[Singularity] a release build needs an upload key. Set SE_KEYSTORE, " +
                        "SE_KEYSTORE_PASS, SE_KEY_ALIAS and SE_KEY_PASS. Create one with:\n" +
                        "  keytool -genkey -v -keystore upload.keystore -alias upload " +
                        "-keyalg RSA -keysize 2048 -validity 10000\n" +
                        "Keep it somewhere you will still have in five years — losing it means " +
                        "never updating the listing again.");
                    return false;
                }
                // a development build signs with the debug key, so it needs no secrets
                PlayerSettings.Android.useCustomKeystore = false;
                return true;
            }

            if (!File.Exists(store))
            {
                Debug.LogError("[Singularity] SE_KEYSTORE points at nothing: " + store);
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = store;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log("[Singularity] signing with " + Path.GetFileName(store));
            return true;
        }
    }
}
