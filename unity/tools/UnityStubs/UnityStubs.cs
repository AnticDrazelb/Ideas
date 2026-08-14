// A COMPILE-TIME STAND-IN FOR UNITY, AND NOTHING ELSE.
//
// This file is not part of the game and is never shipped in it. It exists so
// that Assets/Scripts can be type-checked by a plain C# compiler on a machine
// with no Unity install — CI, a container, a review box — which catches the
// entire class of mistake that otherwise only shows up when somebody opens the
// editor: a misspelled member, a wrong overload, a signature that drifted.
//
// It declares only the API surface the game actually touches, with the real
// signatures. It deliberately has no behaviour: every method is a stub, and
// running against it would do nothing at all. If the game starts using a Unity
// API that is not here, the build fails and the missing declaration is added —
// which keeps this file an honest inventory of the engine surface the project
// depends on.
//
//   dotnet build unity/tools/UnityStubs
//
// See unity/tools/README.md.

using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1, 1);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => 0;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public Vector3 normalized => this;
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => 0;
        public static Vector3 Cross(Vector3 a, Vector3 b) => default;
        public static float Dot(Vector3 a, Vector3 b) => 0;
        public static Vector3 operator +(Vector3 a, Vector3 b) => default;
        public static Vector3 operator -(Vector3 a, Vector3 b) => default;
        public static Vector3 operator -(Vector3 a) => default;
        public static Vector3 operator *(Vector3 a, float f) => default;
        public static Vector3 operator *(float f, Vector3 a) => default;
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion LookRotation(Vector3 f, Vector3 u) => default;
        public static Quaternion AngleAxis(float a, Vector3 axis) => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion Euler(Vector3 e) => default;
        public static Quaternion operator *(Quaternion a, Quaternion b) => default;
        public static Vector3 operator *(Quaternion a, Vector3 v) => default;
    }

    /// <summary>
    /// IMPLEMENTED FOR REAL, and for the same reason Mathf is.
    ///
    /// This was a struct whose arithmetic all returned its left operand and
    /// whose parser returned black, which is harmless as long as nothing reads
    /// the result — and a landmine the moment something does. The access audit
    /// reads every one of these: a stubbed Lerp turns "is a trace brighter than
    /// the lattice at every depth" into a comparison of two identical values,
    /// and a parser that answers black turns twenty contrast assertions into
    /// twenty comparisons of black against black. Both pass. Neither means
    /// anything, and a green check that means nothing is worse than no check.
    /// </summary>
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color magenta => new Color(1, 0, 1);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator /(Color c, float f) => new Color(c.r / f, c.g / f, c.b / f, c.a / f);
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t,
                             a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct Rect
    {
        public Rect(float x, float y, float w, float h) { }
        public float width => 0; public float height => 0;
        public Vector2 center => default;
        public float xMin => 0; public float xMax => 0;
        public float yMin => 0; public float yMax => 0;
        public static bool operator ==(Rect a, Rect b) => true;
        public static bool operator !=(Rect a, Rect b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
    }

    /// Mathf is implemented for real rather than stubbed. Everywhere else a stub
    /// returning zero is harmless — the call is a no-op whose result nobody reads.
    /// Here it would be a landmine: a Mathf that quietly answers 0 turns a
    /// runnable check into one that passes for the wrong reason. Implementing it
    /// is four lines a function and it is what lets the pure-logic half of the
    /// game layer actually EXECUTE under this harness, not merely compile.
    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Rad2Deg = 57.29578f;
        public const float Deg2Rad = 0.0174532924f;
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float a, float b, float d)
            => Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d;
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float PerlinNoise(float x, float y) => 0.5f;
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.AwayFromZero);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.AwayFromZero);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float InverseLerp(float a, float b, float v) => a == b ? 0f : Clamp01((v - a) / (b - a));
        public static float Exp(float f) => (float)Math.Exp(f);
    }

    public class Object
    {
        public string name;
        public HideFlags hideFlags { get; set; }
        public static void Destroy(Object o) { }
        public static void DontDestroyOnLoad(Object o) { }
        public static void DestroyImmediate(Object o) { }
        public static implicit operator bool(Object o) => true;
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
        // an empty array rather than null: the caller foreaches it
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) => new T[0];
    }

    public class Behaviour : Component { public bool enabled; }

    public static class Random { public static float value => 0.5f; }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform => null;
        public bool activeSelf => false;
        public void SetActive(bool b) { }
        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 forward => default;
        public Vector3 up => default;
        public Vector3 right => default;
        public int childCount => 0;
        public Transform parent { get; set; }
        public Transform GetChild(int i) => null;
        public Transform Find(string n) => null;
        public void SetAsFirstSibling() { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetAsLastSibling() { }
        public Vector3 TransformPoint(Vector3 v) => default;
        public Vector3 InverseTransformDirection(Vector3 v) => default;
        public IEnumerator GetEnumerator() => null;
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 pivot { get; set; }
        public Rect rect => default;
    }

    public class CanvasGroup : Component
    {
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator r) => null;
        public void StopCoroutine(Coroutine c) { }
        public void StopAllCoroutines() { }
    }

    public class Coroutine { }
    public class WaitForSecondsRealtime : IEnumerator
    {
        public WaitForSecondsRealtime(float s) { }
        public object Current => null;
        public bool MoveNext() => false;
        public void Reset() { }
    }

    public class Camera : Behaviour
    {
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float aspect => 1;
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public bool allowHDR { get; set; }
        public bool allowMSAA { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 v) => default;
    }

    public enum CameraClearFlags { SolidColor = 2 }

    public class Mesh : Object
    {
        public Rendering.IndexFormat indexFormat { get; set; }
        public int subMeshCount { get; set; }
        public void Clear() { }
        public void SetVertices(List<Vector3> v) { }
        public void SetNormals(List<Vector3> v) { }
        public void SetUVs(int ch, List<Vector2> v) { }
        public void SetUVs(int ch, List<Vector3> v) { }
        public void SetColors(List<Color32> c) { }
        public void SetColors(Color32[] c) { }
        public void SetTriangles(List<int> t, int sub, bool calc) { }
        public void RecalculateBounds() { }
        public void MarkDynamic() { }
        public void SetColors(List<Color> c) { }
    }

    public class Material : Object
    {
        public Material(Shader s) { }
        public int renderQueue { get; set; }
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
        public void SetInt(string n, int i) { }
        public void SetVector(string n, Vector3 v) { }
        public void SetVector(string n, Vector4 v) { }
        public void SetTexture(string n, Texture t) { }
    }

    public class Shader : Object { public static Shader Find(string n) => null; }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Material sharedMaterial { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public Rendering.LightProbeUsage lightProbeUsage { get; set; }
        public Material[] sharedMaterials { get; set; }
    }

    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } }

    public class SpriteRenderer : Renderer
    {
        public Material sharedMaterial { get; set; }
        public Sprite sprite { get; set; }
        public Color color { get; set; }
        public int sortingOrder { get; set; }
    }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu) => null;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu,
                                    uint extrude, SpriteMeshType meshType, Vector4 border) => null;
    }

    public enum SpriteMeshType { FullRect, Tight }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public class RenderTexture : Texture
    {
        public int width { get; set; }
        public int height { get; set; }
        public RenderTextureFormat format { get; set; }
        public FilterMode filterMode { get; set; }
        public static RenderTexture GetTemporary(int w, int h, int depth, RenderTextureFormat fmt) => null;
        public static void ReleaseTemporary(RenderTexture rt) { }
    }

    public enum RenderTextureFormat { Default, ARGB32, ARGBHalf, RGB565 }

    public static class Graphics
    {
        public static void Blit(RenderTexture s, RenderTexture d) { }
        public static void Blit(RenderTexture s, RenderTexture d, Material m) { }
        public static void Blit(RenderTexture s, RenderTexture d, Material m, int pass) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }

    [Flags] public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public class Texture : Object { public FilterMode filterMode { get; set; } public TextureWrapMode wrapMode { get; set; } }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mips) { }
        public void SetPixels32(Color32[] px) { }
        public void Apply(bool mips, bool noLongerReadable) { }
        public void Apply() { }
        public byte[] EncodeToPNG() => null;
    }

    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }

    public class AudioClip : Object
    {
        public delegate void PCMReaderCallback(float[] data);
        public delegate void PCMSetPositionCallback(int position);
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null;
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream, PCMReaderCallback read) => null;
        public bool SetData(float[] data, int offset) => true;
    }

    public class AudioListener : Behaviour { }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public bool isPlaying => false;
        public float spatialBlend { get; set; }
        public bool bypassReverbZones { get; set; }
        public void Play() { }
        public void Stop() { }
        public void PlayScheduled(double time) { }
        public void PlayOneShot(AudioClip c, float v) { }
    }

    public class AudioEchoFilter : Behaviour
    {
        public float delay { get; set; }
        public float decayRatio { get; set; }
        public float wetMix { get; set; }
        public float dryMix { get; set; }
    }

    public class AudioLowPassFilter : Behaviour { public float cutoffFrequency { get; set; } }

    public static class AudioSettings { public static double dspTime => 0; }

    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string n, int size) => null;
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public static class Time
    {
        public static float time => 0;
        public static float unscaledTime => 0;
        public static float unscaledDeltaTime => 0;
        public static float realtimeSinceStartup => 0;
    }

    public static class Screen
    {
        public static int width => 0;
        public static int height => 0;
        public static SleepTimeout sleepTimeout { get; set; }
        public static Rect safeArea => default;
        public static bool fullScreen => true;
        public static void SetResolution(int w, int h, bool fs) { }
    }

    public struct SleepTimeout { public static SleepTimeout SystemSetting => default; }

    public static class Input
    {
        public static int touchCount => 0;
        public static Touch GetTouch(int i) => default;
        public static Vector3 mousePosition => default;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
        public static bool GetKey(KeyCode k) => false;
        public static bool GetKeyDown(KeyCode k) => false;
        public static bool touchSupported => true;
    }

    public struct Touch { public Vector2 position => default; public TouchPhase phase => default; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum KeyCode { Space, LeftShift, LeftArrow, RightArrow, UpArrow, DownArrow, Z, Escape }

    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
    }

    public static class Application
    {
        public static int targetFrameRate { get; set; }
        public static bool isBatchMode => false;
    }

    public static class QualitySettings { public static int vSyncCount { get; set; } }

    public static class PlayerPrefs
    {
        public static string GetString(string k, string d) => d;
        public static void SetString(string k, string v) { }
        public static bool HasKey(string k) => false;
        public static void Save() { }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string s) => default;
        public static string ToJson(object o) => "";
    }

    public static class SystemInfo { public static DeviceType deviceType => default; }
    public enum DeviceType { Unknown, Handheld, Console, Desktop }
    public static class Handheld { public static void Vibrate() { } }

    /// <summary>
    /// Real, for the reason given on <see cref="Color"/>: the whole palette is
    /// declared as hex strings, so a parser that answers black makes every
    /// colour in the game the same colour and every check about them vacuous.
    ///
    /// Unity's accepts #RGB, #RRGGBB and #RRGGBBAA with or without the hash, and
    /// answers false on anything it cannot read. This does the same.
    /// </summary>
    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s, out Color c)
        {
            c = default;
            if (string.IsNullOrEmpty(s)) return false;
            if (s[0] == '#') s = s.Substring(1);
            if (s.Length != 3 && s.Length != 6 && s.Length != 8) return false;
            foreach (char ch in s) if (System.Uri.IsHexDigit(ch) == false) return false;

            int Hex2(int i) => Convert.ToInt32(s.Substring(i, 2), 16);
            if (s.Length == 3)
            {
                int r3 = Convert.ToInt32(new string(s[0], 2), 16);
                int g3 = Convert.ToInt32(new string(s[1], 2), 16);
                int b3 = Convert.ToInt32(new string(s[2], 2), 16);
                c = new Color(r3 / 255f, g3 / 255f, b3 / 255f, 1f);
                return true;
            }
            c = new Color(Hex2(0) / 255f, Hex2(2) / 255f, Hex2(4) / 255f,
                          s.Length == 8 ? Hex2(6) / 255f : 1f);
            return true;
        }
    }

    public class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string cls, params object[] args) { }
        public T Call<T>(string m, params object[] args) => default;
        public void Call(string m, params object[] args) { }
        public T GetStatic<T>(string f) => default;
        public void Dispose() { }
    }

    public class AndroidJavaClass : AndroidJavaObject
    {
        public AndroidJavaClass(string cls) : base(cls) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public enum RuntimeInitializeLoadType { BeforeSceneLoad, AfterSceneLoad }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    namespace Rendering
    {
        public enum IndexFormat { UInt16, UInt32 }
        public enum ShadowCastingMode { Off, On }
        public enum LightProbeUsage { Off, BlendProbes }
        public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }

        // The values matter: they are what a material's _SrcBlend/_DstBlend floats
        // are set to, so a wrong number here is a wrong blend in the build.
        public enum BlendMode
        {
            Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4,
            SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8,
            SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10
        }
    }

    namespace UI
    {
        public class Graphic : Component
        {
            public Color color { get; set; }
            public bool raycastTarget { get; set; }
            public RectTransform rectTransform => null;
        }

        public class Slider : Selectable
        {
            public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
            public Direction direction { get; set; }
            public float minValue { get; set; }
            public float maxValue { get; set; }
            public bool wholeNumbers { get; set; }
            public float value { get; set; }
            public RectTransform fillRect { get; set; }
            public RectTransform handleRect { get; set; }
            public SliderEvent onValueChanged { get; } = new SliderEvent();
            public class SliderEvent { public void AddListener(Action<float> a) { } }
        }

        public class RectMask2D : Component { }

        public class ScrollRect : Component
        {
            public enum MovementType { Unrestricted, Elastic, Clamped }
            public RectTransform content { get; set; }
            public RectTransform viewport { get; set; }
            public bool horizontal { get; set; }
            public bool vertical { get; set; }
            public MovementType movementType { get; set; }
            public float elasticity { get; set; }
            public bool inertia { get; set; }
            public float decelerationRate { get; set; }
            public float scrollSensitivity { get; set; }
            public Vector2 normalizedPosition { get; set; }
            public float verticalNormalizedPosition { get; set; }
        }

        public class Image : Graphic
        {
            public enum Type { Simple, Sliced, Tiled, Filled }
            public Sprite sprite { get; set; }
            public Type type { get; set; }
            public bool preserveAspect { get; set; }
        }

        public class Text : Graphic
        {
            public Font font { get; set; }
            public int fontSize { get; set; }
            public string text { get; set; }
            public TextAnchor alignment { get; set; }
            public bool supportRichText { get; set; }
            public HorizontalWrapMode horizontalOverflow { get; set; }
            public VerticalWrapMode verticalOverflow { get; set; }
        }

        public class Selectable : Component
        {
            public Graphic targetGraphic { get; set; }
            public ColorBlock colors { get; set; }
        }

        public struct ColorBlock
        {
            public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
            public float colorMultiplier, fadeDuration;
        }

        public class InputField : Selectable
        {
            public string text { get; set; }
            public int characterLimit { get; set; }
            public bool readOnly { get; set; }
            public Text textComponent { get; set; }
            public Graphic placeholder { get; set; }
            public LineType lineType { get; set; }
            public ContentType contentType { get; set; }
            public enum ContentType { Standard, IntegerNumber, Alphanumeric }
            public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        }

        public class Button : Selectable { public ButtonClickedEvent onClick => null; }
        public class ButtonClickedEvent { public void AddListener(Action a) { } }

        public class CanvasScaler : Component
        {
            public ScaleMode uiScaleMode { get; set; }
            public Vector2 referenceResolution { get; set; }
            public ScreenMatchMode screenMatchMode { get; set; }
            public float matchWidthOrHeight { get; set; }
            public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize }
            public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        }

        public class GraphicRaycaster : Component { }
    }

    public class Canvas : Component
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    namespace EventSystems
    {
        public class EventSystem : Component { public static EventSystem current => null; }
        public class StandaloneInputModule : Component { }
    }
}
