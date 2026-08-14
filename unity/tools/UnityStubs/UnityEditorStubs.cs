// THE EDITOR SURFACE, STUBBED FOR THE SAME REASON THE RUNTIME ONE IS.
//
// The build scripts are the highest-stakes code in the project: a compile error
// in an Editor assembly stops Unity entering play mode at all, so a typo there
// does not break the build, it breaks the editor. And they are the one part
// nobody exercises until the moment somebody is trying to produce an APK.
//
// So they are type-checked here alongside everything else. Same rules as
// UnityStubs.cs: real signatures, no behaviour, and if the project starts using
// an API that is missing, the fix is to add the declaration.

using System;

namespace UnityEngine
{
    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }
    public enum LogType { Error, Assert, Warning, Log, Exception }

    namespace SceneManagement
    {
        public struct Scene { public string path => null; public bool IsValid() => true; }
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string path) { }
        public MenuItemAttribute(string path, bool validate) { }
        public MenuItemAttribute(string path, bool validate, int priority) { }
    }

    public static class EditorApplication
    {
        public static Action delayCall;
        public static void Exit(int code) { }
    }

    public static class EditorPrefs
    {
        public static bool GetBool(string key, bool def) => def;
        public static void SetBool(string key, bool v) { }
    }

    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void ImportAsset(string path) { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void Refresh() { }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path => null;
        public bool enabled => false;
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public enum BuildTarget { NoTarget, Android, iOS, StandaloneLinux64, StandaloneOSX, StandaloneWindows64, WebGL }
    public enum BuildTargetGroup { Unknown, Standalone, Android, iOS, WebGL }

    [Flags]
    public enum BuildOptions
    {
        None = 0, Development = 1, AutoRunPlayer = 4, ShowBuiltPlayer = 8,
        AllowDebugging = 32, ConnectWithProfiler = 256
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildTargetGroup targetGroup;
        public BuildOptions options;
    }

    public static class BuildPipeline
    {
        public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) => null;
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget => BuildTarget.Android;
        public static bool buildAppBundle { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup g, BuildTarget t) => true;
    }

    public enum IconKind { Any = -1, Application = 0, Settings = 1, Notification = 2, Spotlight = 3, Store = 4, AdaptiveForeground = 5, AdaptiveBackground = 6 }

    public enum TextureImporterType { Default, NormalMap, GUI, Sprite, Cursor }

    public class AssetImporter
    {
        public static AssetImporter GetAtPath(string path) => null;
        public void SaveAndReimport() { }
    }

    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public bool mipmapEnabled { get; set; }
        public bool alphaIsTransparency { get; set; }
    }

    [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8 }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1, CoreCLR = 2 }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }

    [Flags]
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 32, All = -1 }

    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0, AndroidApiLevel23 = 23, AndroidApiLevel24 = 24,
        AndroidApiLevel29 = 29, AndroidApiLevel33 = 33, AndroidApiLevel34 = 34
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static UnityEngine.ColorSpace colorSpace { get; set; }

        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }

        public static string bundleVersion { get; set; }
        public static void SetApplicationIdentifier(Build.NamedBuildTarget t, string id) { }
        public static void SetIcons(Build.NamedBuildTarget t, UnityEngine.Texture2D[] icons, IconKind kind) { }
        public static void SetScriptingBackend(Build.NamedBuildTarget t, ScriptingImplementation i) { }

        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static bool forceInternetPermission { get; set; }
            public static bool useCustomKeystore { get; set; }
            public static string keystoreName { get; set; }
            public static string keystorePass { get; set; }
            public static string keyaliasName { get; set; }
            public static string keyaliasPass { get; set; }
            public static int bundleVersionCode { get; set; }
        }

        public static class SplashScreen
        {
            public static bool show { get; set; }
        }
    }

    namespace Build
    {
        public struct NamedBuildTarget
        {
            public static NamedBuildTarget Android => default;
            public static NamedBuildTarget iOS => default;
            public static NamedBuildTarget Standalone => default;
        }

        namespace Reporting
        {
            public enum BuildResult { Unknown, Succeeded, Failed, Cancelled }

            public struct BuildSummary
            {
                public BuildResult result => BuildResult.Succeeded;
                public ulong totalSize => 0;
                public TimeSpan totalTime => default;
                public int totalErrors => 0;
            }

            public struct BuildStepMessage
            {
                public UnityEngine.LogType type => UnityEngine.LogType.Log;
                public string content => null;
            }

            public struct BuildStep
            {
                public string name => null;
                public BuildStepMessage[] messages => null;
            }

            public class BuildReport
            {
                public BuildSummary summary => default;
                public BuildStep[] steps => null;
            }
        }
    }

    namespace SceneManagement
    {
        public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
        public enum NewSceneMode { Single, Additive }

        public static class EditorSceneManager
        {
            public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
            public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string path) => true;
        }
    }
}
