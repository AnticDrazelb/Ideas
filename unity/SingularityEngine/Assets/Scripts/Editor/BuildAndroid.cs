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
        const string Apk = "SingularityEngine.apk";

        [MenuItem("Singularity/Build Android APK %#b")]
        public static void FromMenu() => Run(development: true);

        /// <summary>
        /// Invoked by <c>build-android.sh</c>. Exits with a non-zero code on
        /// failure so a build machine notices.
        /// </summary>
        public static void CommandLine()
        {
            bool dev = Environment.GetCommandLineArgs().Length == 0
                       || Array.IndexOf(Environment.GetCommandLineArgs(), "-release") < 0;
            bool ok = Run(dev);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(bool development)
        {
            ProjectSetup.Apply();

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
            string path = Path.Combine(OutDir, Apk);

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
    }
}
