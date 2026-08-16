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
        public static string GetAssetPath(UnityEngine.Object o) => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) => null;
    }

    // ---- the serialised back door ---------------------------------------------
    //
    // A CAUTIONARY NOTE, BECAUSE THIS FILE JUST FAILED AT ITS ONE JOB.
    //
    // The first attempt at Always Included Shaders was written against
    // GraphicsSettings.alwaysIncludedShaders, which does not exist. A stub was
    // then added HERE that declared it — so the invention compiled, the harness
    // went green, and Unity rejected it on open with three CS0117s.
    //
    // That is not the harness being wrong, it is the harness being MISUSED. The
    // rule at the top of UnityStubs.cs is that a declaration is added because the
    // engine has it, never because the game wants it; a stub written to make a
    // guess compile converts "I am not sure this API exists" into a green tick,
    // which is worse than no harness at all. These four are real, and are the
    // supported way to reach a project setting that has no public property.
    public class SerializedProperty
    {
        public int arraySize { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int i) => null;
        public void InsertArrayElementAtIndex(int i) { }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object target) { }
        public SerializedProperty FindProperty(string path) => null;
        public bool ApplyModifiedProperties() => false;
        public void Update() { }
    }

    // ---- the chassis window's surface, and only its surface --------------------
    //
    // IMGUI is enormous and almost none of it is used here. What is declared is
    // what ChassisWindow.cs calls, at the overloads it calls them at, because the
    // failure this catches is the one that costs a round trip through the editor:
    // a Slider whose arguments are in the wrong order, an ObjectField missing its
    // allowSceneObjects flag, a LabelField that does not take two strings.

    public enum MessageType { None, Info, Warning, Error }

    public class EditorWindow : UnityEngine.ScriptableObject
    {
        public UnityEngine.Vector2 minSize { get; set; }
        public void Repaint() { }
        public static T GetWindow<T>(string title) where T : EditorWindow => null;
    }

    public class GUIStyle { }

    public static class Selection
    {
        public static UnityEngine.GameObject activeGameObject { get; set; }
    }

    public class SceneView : EditorWindow
    {
        public static void RepaintAll() { }
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle wordWrappedLabel => null;
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
    }

    public static class EditorGUI
    {
        public static void DrawRect(UnityEngine.Rect r, UnityEngine.Color c) { }
        public static void LabelField(UnityEngine.Rect r, string label) { }
    }

    public static class EditorGUILayout
    {
        public static void LabelField(string label) { }
        public static void LabelField(string label, string value) { }
        public static void LabelField(string label, GUIStyle style) { }
        public static void Space() { }
        public static void HelpBox(string message, MessageType type) { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static int IntField(string label, int value) => value;
        public static float FloatField(string label, float value) => value;
        public static float Slider(string label, float value, float min, float max) => value;
        public static string TextField(string label, string value) => value;
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object value,
                                                     Type type, bool allowSceneObjects) => value;

        /// <summary>
        /// The scroll view as a SCOPE, because the begin/end pair leaks a mismatched
        /// layout group the moment anything in the body throws — and an OnGUI that
        /// throws once throws every frame.
        /// </summary>
        public class ScrollViewScope : IDisposable
        {
            public ScrollViewScope(UnityEngine.Vector2 pos) { }
            public UnityEngine.Vector2 scrollPosition => default;
            public void Dispose() { }
        }
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

    // NO ADAPTIVE MEMBERS HERE, AND THAT ABSENCE IS THE POINT. An earlier version
    // of this stub had AdaptiveForeground and AdaptiveBackground because I assumed
    // they were here; they are not, they belong to SetPlatformIcons, and the
    // harness cheerfully agreed with me because I had written both sides. Unity
    // caught it on first open. A stub proves the SHAPE of a call, never the
    // existence of the thing being called.
    public enum IconKind { Any = -1, Application = 0, Settings = 1, Notification = 2, Spotlight = 3, Store = 4 }

    public enum TextureImporterType { Default, NormalMap, GUI, Sprite, Cursor }

    public class AssetImporter
    {
        public static AssetImporter GetAtPath(string path) => null;
        public void SaveAndReimport() { }
    }

    public enum TextureImporterNPOTScale { None, ToNearest, ToLarger, ToSmaller }

    public enum TextureImporterCompression { Uncompressed, Compressed, CompressedHQ, CompressedLQ }

    public class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public TextureImporterNPOTScale npotScale { get; set; }
        public TextureImporterCompression textureCompression { get; set; }
        public bool mipmapEnabled { get; set; }
        public bool alphaIsTransparency { get; set; }
        public bool isReadable { get; set; }
        public int maxTextureSize { get; set; }
        public UnityEngine.TextureWrapMode wrapMode { get; set; }
        public UnityEngine.FilterMode filterMode { get; set; }
    }

    // THE FONT IMPORTER, AND THE THREE ENUMS WHOSE MEMBER NAMES ARE THE WHOLE
    // POINT OF STUBBING IT. The settings themselves are booleans and enum values
    // — nothing here can check that HintedSmooth is a better choice than Smooth,
    // and nothing here proves TrueTypeFontImporter has these properties at all.
    // What it proves is that FontImport.cs says the names Unity's API says, so a
    // typo in one of them is a compile error in this harness rather than a silent
    // no-op discovered when a phone renders the interface in Roboto.
    public enum FontTextureCase { Dynamic = -2, Unicode = -1, ASCII = 0, ASCIIUpperCase = 1, ASCIILowerCase = 2, CustomSet = 3 }

    public enum FontRenderingMode { Smooth = 0, HintedSmooth = 1, HintedRaster = 2, OSDefault = 3 }

    public enum AscentCalculationMode { Legacy = 0, FaceAscender = 1, FaceBoundingBox = 2 }

    public class TrueTypeFontImporter : AssetImporter
    {
        public FontTextureCase fontTextureCase { get; set; }
        public FontRenderingMode fontRenderingMode { get; set; }
        public AscentCalculationMode ascentCalculationMode { get; set; }
        public bool includeFontData { get; set; }
    }

    /// <summary>The import hook itself. Only the two members Chassis's importer uses.</summary>
    public class AssetPostprocessor
    {
        public string assetPath { get; }
        public AssetImporter assetImporter { get; }
    }

    [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8 }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1, CoreCLR = 2 }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }

    [Flags]
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 32, All = -1 }

    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0,
        // Carrying Unity's own deprecation, so the harness reproduces the warning
        // rather than letting it arrive on somebody else's machine.
        [Obsolete("Minimum supported Android API level is 25.")] AndroidApiLevel23 = 23,
        [Obsolete("Minimum supported Android API level is 25.")] AndroidApiLevel24 = 24,
        AndroidApiLevel25 = 25, AndroidApiLevel29 = 29,
        AndroidApiLevel33 = 33, AndroidApiLevel34 = 34, AndroidApiLevel35 = 35
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
