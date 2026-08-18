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
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public static Vector2 operator /(Vector2 a, float f) => new Vector2(a.x / f, a.y / f);
        // REAL, AND THE REASON IS TILT. These two answered `default`, so every
        // filter written with them collapsed to zero in the harness — which is
        // how Tilt's whole body passed a check run without ever producing a
        // number. Same lesson as the operators below.
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }
        public static Vector2 MoveTowards(Vector2 a, Vector2 b, float d)
        {
            Vector2 to = b - a;
            float m = to.magnitude;
            if (m <= d || m == 0f) return b;
            return new Vector2(a.x + to.x / m * d, a.y + to.y / m * d);
        }
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
    }

    /// <summary>
    /// REAL ARITHMETIC, for the reason written over Color: every operator here
    /// used to answer <c>default</c>, so a check that asked how wide a rotated
    /// solid is got zero and agreed with whatever it was comparing against.
    /// </summary>
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                // Unity's own: a vector too short to have a direction has none,
                // rather than an infinity in every component
                return m < 1e-5f ? zero : new Vector3(x / m, y / m, z / m);
            }
        }
        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(float f, Vector3 a) => a * f;
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
    }

    /// <summary>
    /// REAL, AND THE THIRD ONE THIS HARNESS HAS HAD TO LEARN THE LESSON ON.
    ///
    /// The title's attract pose is a quaternion and the camera decides how big
    /// the cube may be by summing the rotated basis — so an AngleAxis answering
    /// identity turns "does the machine stay inside the band between the masthead
    /// and the controls" into a statement about a cube that never turns. It
    /// passed, at 0% of the band, which is the giveaway a hollow stub always
    /// leaves if anybody prints the number it is asserting on.
    /// </summary>
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);

        public static Quaternion AngleAxis(float degrees, Vector3 axis)
        {
            Vector3 a = axis.normalized;
            float h = degrees * 0.5f * (float)Math.PI / 180f;
            float s = (float)Math.Sin(h);
            return new Quaternion(a.x * s, a.y * s, a.z * s, (float)Math.Cos(h));
        }

        /// <summary>Unity's order: Z, then X, then Y, applied to the same frame.</summary>
        public static Quaternion Euler(float x, float y, float z)
            => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);

        public static Quaternion LookRotation(Vector3 f, Vector3 u)
        {
            Vector3 z = f.normalized;
            if (z.sqrMagnitude < 1e-10f) return identity;
            Vector3 xa = Vector3.Cross(u, z).normalized;
            Vector3 ya = Vector3.Cross(z, xa);
            float t = xa.x + ya.y + z.z;
            if (t > 0f)
            {
                float s = (float)Math.Sqrt(t + 1f) * 2f;
                return new Quaternion((ya.z - z.y) / s, (z.x - xa.z) / s, (xa.y - ya.x) / s, s * 0.25f);
            }
            if (xa.x > ya.y && xa.x > z.z)
            {
                float s = (float)Math.Sqrt(1f + xa.x - ya.y - z.z) * 2f;
                return new Quaternion(s * 0.25f, (xa.y + ya.x) / s, (z.x + xa.z) / s, (ya.z - z.y) / s);
            }
            if (ya.y > z.z)
            {
                float s = (float)Math.Sqrt(1f + ya.y - xa.x - z.z) * 2f;
                return new Quaternion((xa.y + ya.x) / s, s * 0.25f, (ya.z + z.y) / s, (z.x - xa.z) / s);
            }
            float s2 = (float)Math.Sqrt(1f + z.z - xa.x - ya.y) * 2f;
            return new Quaternion((z.x + xa.z) / s2, (ya.z + z.y) / s2, s2 * 0.25f, (xa.y - ya.x) / s2);
        }

        public static Quaternion operator *(Quaternion a, Quaternion b)
            => new Quaternion(a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                              a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                              a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                              a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            // v + 2w(q x v) + 2(q x (q x v)) — the usual expansion, and it is
            // exact for a unit quaternion, which every one this makes is
            Vector3 u = new Vector3(q.x, q.y, q.z);
            Vector3 t = Vector3.Cross(u, v) * 2f;
            return v + t * q.w + Vector3.Cross(u, t);
        }

        public override string ToString() => "(" + x + ", " + y + ", " + z + ", " + w + ")";
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

        // Unity compares colours as Vector4s with a squared-distance epsilon
        // rather than exactly, which is the behaviour anything writing
        // `if (img.color != want)` to avoid a redundant assignment depends on.
        // Declared because UnityEngine.Color has it.
        public static bool operator ==(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b, da = a.a - b.a;
            return dr * dr + dg * dg + db * db + da * da < 9.99999944e-11f;
        }
        public static bool operator !=(Color a, Color b) => !(a == b);
        public override bool Equals(object o) => o is Color c && this == c;
        public override int GetHashCode() => 0;
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }

        // Unity converts BOTH ways implicitly. Only one direction was declared
        // here, so `Color32[] px; px[i] = someColor;` — which is how every
        // generated texture in this project is filled — did not compile against
        // the stub while compiling perfectly against the engine. That is the
        // harness being WRONGLY strict, which wastes exactly as much time as it
        // being wrongly lax, and is a good deal harder to believe.
        public static implicit operator Color32(Color c)
            => new Color32((byte)Math.Round(Mathf.Clamp01(c.r) * 255f),
                           (byte)Math.Round(Mathf.Clamp01(c.g) * 255f),
                           (byte)Math.Round(Mathf.Clamp01(c.b) * 255f),
                           (byte)Math.Round(Mathf.Clamp01(c.a) * 255f));
    }

    /// <summary>
    /// REAL, FOR THE SAME REASON MATHF AND COLOR ARE.
    ///
    /// This threw away its own constructor arguments and answered zero to every
    /// question, which is harmless exactly as long as nothing reads the answer.
    /// Layout is arithmetic ON rectangles — the glass, the window, the insets the
    /// stroke is hung from — so a hollow Rect turns "is the window square" into
    /// 0 == 0, and a check that passes for that reason is worse than no check.
    /// Unity's own semantics: x and y are the MINIMUM corner, and xMax is x plus
    /// the width, so a negative width is allowed and is a rect that is inside out.
    /// </summary>
    public struct Rect
    {
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float x; public float y;
        public float width; public float height;
        public Vector2 center => new Vector2(x + width * 0.5f, y + height * 0.5f);
        public float xMin => x; public float xMax => x + width;
        public float yMin => y; public float yMax => y + height;
        public static bool operator ==(Rect a, Rect b)
            => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);
        public override bool Equals(object o) => o is Rect r && this == r;
        public override int GetHashCode()
            => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (width.GetHashCode() >> 2) ^ (height.GetHashCode() >> 1);
        public override string ToString()
            => "(x:" + x + ", y:" + y + ", width:" + width + ", height:" + height + ")";
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
        public static float Log10(float f) => (float)Math.Log10(f);

        // Unity's own definition, which is a relative epsilon and not the
        // absolute one everybody assumes: 1e-6 scaled by the larger magnitude,
        // floored at eight times float.Epsilon.
        public static bool Approximately(float a, float b)
            => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)),
                                          float.Epsilon * 8f);

        public static int Abs(int f) => Math.Abs(f);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        /// <summary>Unity's own: t wrapped into [0, length), and never negative.</summary>
        public static float Repeat(float t, float length)
            => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);
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

    public static class Random
    {
        public static float value => 0.5f;
        public static float Range(float a, float b) => (a + b) * 0.5f;
        public static int Range(int a, int b) => a;
    }

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
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
        public static GameObject Find(string name) => null;
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localEulerAngles { get; set; }
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

        // The schematic is a LINE mesh — twelve edges a cell, for every cell in
        // the volume — and lines do not go through SetTriangles. Both of these
        // are declared because UnityEngine.Mesh has them, on the same rule the
        // note above BlendMode states: a number or a member that has to match
        // something outside this program is READ from that thing, never invented
        // because the game would like it to exist. SetIndices' List overload is
        // the same family as SetTriangles' above.
        public void SetIndices(List<int> idx, MeshTopology topology, int sub, bool calc) { }
        public void RecalculateBounds() { }
        public void MarkDynamic() { }
        public void SetColors(List<Color> c) { }

        // THE SKY'S QUAD IS FOUR VERTICES AND TWO TRIANGLES, so it is built with
        // the array properties rather than the List overloads above — those exist
        // because the board rebuilds thousands of vertices every fold and wants
        // no garbage; four does not.
        public Vector3[] vertices { get; set; }
        public int[] triangles { get; set; }
        public Bounds bounds { get; set; }
    }

    /// <summary>
    /// UnityEngine.Bounds. Declared for one line: the sky is drawn in clip space
    /// and sits at the origin, so its renderer has to be given bounds big enough
    /// that frustum culling can never reach it.
    /// </summary>
    public struct Bounds
    {
        public Bounds(Vector3 centre, Vector3 size) { center = centre; this.size = size; }
        public Vector3 center { get; set; }
        public Vector3 size { get; set; }
    }

    /// <summary>
    /// UnityEngine.MeshTopology. One is missing on purpose and is missing in
    /// Unity too — the value was a deprecated triangle strip.
    /// </summary>
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }

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

    /// <summary>
    /// UnityEngine.TextAsset — a file in Resources read as a string. Declared
    /// because the engine has it: the five-hundred-cube catalogue ships as one.
    /// </summary>
    public class TextAsset : Object { public string text => null; }

    public class Shader : Object { public static Shader Find(string n) => null; }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public Material sharedMaterial { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
        public Rendering.LightProbeUsage lightProbeUsage { get; set; }
        public Rendering.ReflectionProbeUsage reflectionProbeUsage { get; set; }
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

    /// <summary>
    /// AN ASSET THAT IS DATA. ChassisSpec is the only one, and what the harness
    /// has to prove about it is that Resources.Load&lt;T&gt; and CreateInstance&lt;T&gt;
    /// are being asked for a type that could actually be either — a plain class
    /// would compile against both calls and fail in the editor with "the script
    /// does not derive from ScriptableObject", which is a message you get on
    /// import rather than at any point a compiler is watching.
    /// </summary>
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject
            => (T)Activator.CreateInstance(typeof(T));
    }

    // THE INSPECTOR DECORATIONS. They draw nothing here and they are not meant
    // to: what they catch is a field decorated with an attribute that does not
    // exist under that name, which in the editor is a red console line at import
    // and here is a compile error.
    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }

    /// <summary>
    /// Runs its Awake/Update/LateUpdate in EDIT MODE as well as in play mode.
    /// Two components carry it — the chassis's Fit and the scanlines' — so that
    /// the editor preview lays itself out live when the Game view is resized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ExecuteAlwaysAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
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

    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int width { get; }
        public int height { get; }
    }

    public class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mips) { }
        public void SetPixels32(Color32[] px) { }
        public Color32[] GetPixels32() => null;
        public void Apply(bool mips, bool noLongerReadable) { }
        public void Apply() { }
        public byte[] EncodeToPNG() => null;
    }

    /// <summary>
    /// PNG in and PNG out, and it is a STATIC CLASS rather than two methods on
    /// Texture2D — which is the whole reason this is stubbed as its own type.
    /// Texture2D.LoadImage reads like an instance method and is an extension on
    /// ImageConversion; writing it the obvious way compiles against a stub that
    /// declares it the obvious way and fails against the engine.
    /// </summary>
    public static class ImageConversion
    {
        public static bool LoadImage(Texture2D tex, byte[] data) => true;
        public static byte[] EncodeToPNG(Texture2D tex) => null;
    }

    public enum ScaleMode { StretchToFill, ScaleAndCrop, ScaleToFit }

    /// <summary>
    /// The immediate-mode drawing the chassis window's two previews use, and
    /// nothing else. Everything here draws in SCREEN space with y DOWN, while
    /// DrawTextureWithTexCoords takes its uv rect with y UP from the bottom of
    /// the texture — the one asymmetry in the API and the one the preview code
    /// has a comment about.
    /// </summary>
    public static class GUI
    {
        public static void DrawTexture(Rect r, Texture t) { }
        public static void DrawTexture(Rect r, Texture t, ScaleMode mode) { }
        public static void DrawTextureWithTexCoords(Rect r, Texture t, Rect uv) { }
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float w, float h) => default;
    }

    public class GUILayoutOption { }

    public static class GUILayout
    {
        public static bool Button(string label, params GUILayoutOption[] opts) => false;
        public static void Label(string label, params GUILayoutOption[] opts) { }
        public static void Space(float px) { }
        public static GUILayoutOption Width(float w) => null;
        public static GUILayoutOption Height(float h) => null;
    }

    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Clamp, Repeat, Mirror, MirrorOnce }

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
        public Audio.AudioMixerGroup outputAudioMixerGroup { get; set; }
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

    namespace Audio
    {
        /// <summary>
        /// THE MIXER, WHICH THIS PROJECT CANNOT SHIP AND CAN STILL USE.
        ///
        /// There is no .mixer asset in the repository because an AudioMixer
        /// cannot be created from script — AudioMixerController is internal to
        /// the editor, so nothing supported makes a group or exposes a
        /// parameter. What the harness can prove is the shape of the calls Bus
        /// makes against one when a person has made it: FindMatchingGroups
        /// returns an ARRAY and not a group, and SetFloat takes the exposed
        /// parameter's name and not the group's, which are the two ways this
        /// goes wrong quietly.
        /// </summary>
        public class AudioMixer : Object
        {
            public AudioMixerGroup[] FindMatchingGroups(string subPath) => null;
            public bool SetFloat(string name, float value) => false;
            public bool GetFloat(string name, out float value) { value = 0f; return false; }
        }

        public class AudioMixerGroup : Object { }
    }

    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string n, int size) => null;
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
        public static T Load<T>(string path) where T : Object => null;
    }

    public static class Time
    {
        public static float time => 0;
        public static float unscaledTime => 0;
        public static float unscaledDeltaTime => 0;
        public static float realtimeSinceStartup => 0;
    }

    /// <summary>
    /// SETTABLE, AND THAT IS THE POINT. These were three expression-bodied
    /// properties answering zero, which is harmless while nothing reads them and
    /// a landmine the moment something does — Layout is arithmetic ON these
    /// numbers, so a Screen that always answers zero turns "is the window square
    /// on a 20:9 phone" into a comparison of two zeroes, which passes and means
    /// nothing. Same reason Mathf and Color are real here. The defaults are still
    /// zero, so nothing that does not set them can tell the difference.
    /// </summary>
    public static class Screen
    {
        public static int width { get; set; }
        public static int height { get; set; }
        public static SleepTimeout sleepTimeout { get; set; }
        public static Rect safeArea { get; set; }
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

        // THE TWO SENSORS THE SKY PARALLAXES ON. Both are declared because the
        // engine has both and Tilt reads whichever the device answers — see the
        // note there on why it is gravity rather than rotation rate.
        // A HOLLOW STUB IS A CHECK THAT CANNOT FAIL, and these two were hollow.
        // `gyro` returned null and `acceleration` returned zero, so every line of
        // Tilt below the sensor read had never been executed by anything — which
        // is how a device that answers zero on one channel and perfectly well on
        // the other shipped as "the sky does not move". Same lesson as Rect,
        // Screen, Vector3 and Quaternion before them: the harness only proves the
        // code it can actually run.
        public static Gyroscope gyro { get; } = new Gyroscope();
        public static Vector3 acceleration { get; set; }
    }

    /// <summary>
    /// UnityEngine.Gyroscope. Only the two members Tilt touches: the switch that
    /// has to be thrown before the device reports anything at all, and the fused
    /// gravity vector, which is the quiet version of Input.acceleration.
    /// </summary>
    public class Gyroscope
    {
        public bool enabled { get; set; }
        public Vector3 gravity { get; set; }
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
        public static bool isPlaying => false;

        /// <summary>
        /// Declared so the back-button path type-checks here. A no-op is the
        /// honest stub: nothing in this harness has an application to close, and
        /// the DECISION that leads here is Screens.DecideBack, which is a pure
        /// function and is asserted on its own.
        /// </summary>
        public static void Quit() { }
    }

    public static class QualitySettings { public static int vSyncCount { get; set; } }


    /// <summary>
    /// IMPLEMENTED FOR REAL, for the reason Mathf and Color are.
    ///
    /// A stub returning the default is harmless where nothing reads the result.
    /// Here it is the opposite: Store's whole job is what comes back out of this,
    /// and a PlayerPrefs that forgets everything turns "does a corrupt save
    /// recover from its backup" into a question that cannot be asked. The save
    /// path is the one place in this game where a bug costs a player a hundred
    /// hours they cannot get back, so it is the last place to accept a check that
    /// passes because nothing happened.
    ///
    /// An in-memory dictionary is exactly PlayerPrefs' contract minus the disk.
    /// </summary>
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, string> Store = new Dictionary<string, string>();

        public static string GetString(string k, string d) => Store.TryGetValue(k, out var v) ? v : d;
        public static void SetString(string k, string v) { Store[k] = v; }
        public static bool HasKey(string k) => Store.ContainsKey(k);
        public static void Save() { }

        /// <summary>Harness only: start from an empty device.</summary>
        public static void Wipe() => Store.Clear();
    }

    /// <summary>
    /// A REAL SERIALISER, AND IT IS NOT UNITY'S — which is the honest limit of
    /// what the save checks prove.
    ///
    /// System.Text.Json over fields matches JsonUtility on every behaviour the
    /// recovery logic turns on: malformed input throws, an explicit null leaves a
    /// null list, and a field the JSON does not mention keeps whatever the
    /// constructor put there. So the checks exercise Store's DECISIONS for real —
    /// which string is tried, what is quarantined, what a half-shaped save does.
    ///
    /// What they cannot prove is that Unity's parser agrees with this one on some
    /// input neither of us thought of. That is the same limit every stub here
    /// has, and it is smaller than the alternative, which is a save path nothing
    /// has ever run.
    /// </summary>
    public static class JsonUtility
    {
        static readonly System.Text.Json.JsonSerializerOptions Opts =
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

        public static T FromJson<T>(string s) => System.Text.Json.JsonSerializer.Deserialize<T>(s, Opts);
        public static string ToJson(object o) => System.Text.Json.JsonSerializer.Serialize(o, o.GetType(), Opts);
    }

    public static class SystemInfo
    {
        public static DeviceType deviceType => default;
        public static bool supportsGyroscope { get; set; }
    }
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

        /// <summary>
        /// The other direction, and real for the same reason. Six hex digits, no
        /// hash — Unity answers uppercase, and callers here fold the case anyway
        /// rather than depend on that.
        /// </summary>
        public static string ToHtmlStringRGB(Color c)
        {
            int B(float v) => (int)System.Math.Round(Mathf.Clamp01(v) * 255f);
            return B(c.r).ToString("X2") + B(c.g).ToString("X2") + B(c.b).ToString("X2");
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
        public enum ReflectionProbeUsage { Off, BlendProbes, Simple }
        public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }

        // THE VALUES MATTER, AND THEY ARE THE WHOLE REASON THESE TWO ENUMS ARE
        // TRANSCRIBED RATHER THAN INVENTED. They are what a material's _SrcBlend,
        // _DstBlend and _BlendOp floats are set to, so a wrong number here is a
        // wrong blend in the build — and a blend that is wrong in this particular
        // way is not subtly wrong. The screen filter shipped once with a literal
        // 4 in it for DstColor. Four is OneMinusDstColor. Every pixel in the game
        // came out INVERTED, orange interface and all, the instant either slider
        // left 100 — and no check could see it, because the checking code held
        // the same literal.
        //
        // So nothing outside these declarations may write one of these numbers
        // down. FilterChecks pins them against the documented values.
        public enum BlendMode
        {
            Zero = 0, One = 1, DstColor = 2, SrcColor = 3, OneMinusDstColor = 4,
            SrcAlpha = 5, OneMinusSrcColor = 6, DstAlpha = 7, OneMinusDstAlpha = 8,
            SrcAlphaSaturate = 9, OneMinusSrcAlpha = 10
        }

        public enum BlendOp
        {
            Add = 0, Subtract = 1, ReverseSubtract = 2, Min = 3, Max = 4
        }
    }

    namespace UI
    {
        // Graphic is a UIBehaviour is a MonoBehaviour in Unity, which is where
        // `enabled` comes from. It was declared straight off Component here, so
        // switching a graphic off — the ordinary way to hide one without
        // destroying it — did not compile against the harness while compiling
        // perfectly against the engine. Wrongly strict costs as much time as
        // wrongly lax and is harder to believe.
        public class Graphic : MonoBehaviour
        {
            public Color color { get; set; }
            public bool raycastTarget { get; set; }
            public Material material { get; set; }
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
            public bool resizeTextForBestFit { get; set; }
            public int resizeTextMinSize { get; set; }
            public int resizeTextMaxSize { get; set; }
        }

        public class Selectable : Component
        {
            public Graphic targetGraphic { get; set; }
            public ColorBlock colors { get; set; }
            public bool interactable { get; set; }
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
            public SubmitEvent onEndEdit => null;
            public class SubmitEvent { public void AddListener(Action<string> a) { } }
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
        public float scaleFactor { get; }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    namespace EventSystems
    {
        public class EventSystem : Component { public static EventSystem current => null; }
        public class StandaloneInputModule : Component { }
    }
}
