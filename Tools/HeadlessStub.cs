// Functional UnityEngine / UnityEditor stand-in used only to run the project's pure
// logic (navigation, vision, movement) headlessly with mcs/mono for verification.
// Never shipped; not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m < 1e-9f ? zero : this / m; } }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
        public static Vector2 operator /(Vector2 a, float b) { return new Vector2(a.x / b, a.y / b); }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
        public static Vector2 MoveTowards(Vector2 a, Vector2 b, float step) { Vector2 d = b - a; float m = d.magnitude; return m <= step || m < 1e-9f ? b : a + d / m * step; }
        public override string ToString() { return "(" + x.ToString("F2") + "," + y.ToString("F2") + ")"; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public Vector3 normalized { get { float m = magnitude; return m < 1e-9f ? zero : this / m; } }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
        public static Vector3 operator /(Vector3 a, float b) { return new Vector3(a.x / b, a.y / b, a.z / b); }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float step)
        {
            Vector3 d = b - a; float m = d.magnitude;
            return m <= step || m < 1e-9f ? b : a + d / m * step;
        }
        public override string ToString() { return "(" + x.ToString("F2") + "," + y.ToString("F2") + "," + z.ToString("F2") + ")"; }
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float nx = q.x * 2f, ny = q.y * 2f, nz = q.z * 2f;
            float xx = q.x * nx, yy = q.y * ny, zz = q.z * nz;
            float xy = q.x * ny, xz = q.x * nz, yz = q.y * nz;
            float wx = q.w * nx, wy = q.w * ny, wz = q.w * nz;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }
        public static Quaternion Euler(float px, float py, float pz)
        {
            float hx = px * Mathf.Deg2Rad * .5f, hy = py * Mathf.Deg2Rad * .5f, hz = pz * Mathf.Deg2Rad * .5f;
            float sx = (float)Math.Sin(hx), cx = (float)Math.Cos(hx);
            float sy = (float)Math.Sin(hy), cy = (float)Math.Cos(hy);
            float sz = (float)Math.Sin(hz), cz = (float)Math.Cos(hz);
            return new Quaternion(
                sx * cy * cz + cx * sy * sz, cx * sy * cz - sx * cy * sz,
                cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz);
        }
        public static Quaternion LookRotation(Vector3 forward)
        {
            Vector3 f = forward.normalized;
            if (f.magnitude < 1e-6f) return identity;
            Vector3 up = new Vector3(0, 1, 0);
            if (Math.Abs(Vector3.Dot(f, up)) > .9999f) up = new Vector3(0, 0, 1);
            Vector3 r = Vector3.Cross(up, f).normalized, u = Vector3.Cross(f, r);
            float trace = r.x + u.y + f.z;
            if (trace > 0)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                return new Quaternion((u.z - f.y) / s, (f.x - r.z) / s, (r.y - u.x) / s, .25f * s);
            }
            if (r.x > u.y && r.x > f.z)
            {
                float s = Mathf.Sqrt(1f + r.x - u.y - f.z) * 2f;
                return new Quaternion(.25f * s, (u.x + r.y) / s, (f.x + r.z) / s, (u.z - f.y) / s);
            }
            if (u.y > f.z)
            {
                float s = Mathf.Sqrt(1f + u.y - r.x - f.z) * 2f;
                return new Quaternion((u.x + r.y) / s, .25f * s, (f.y + u.z) / s, (f.x - r.z) / s);
            }
            float t = Mathf.Sqrt(1f + f.z - r.x - u.y) * 2f;
            return new Quaternion((f.x + r.z) / t, (f.y + u.z) / t, .25f * t, (r.y - u.x) / t);
        }
        static float Dot(Quaternion a, Quaternion b) { return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w; }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegrees)
        {
            float dot = Dot(from, to);
            if (dot < 0) { to = new Quaternion(-to.x, -to.y, -to.z, -to.w); dot = -dot; }
            dot = Math.Min(1f, Math.Max(-1f, dot));
            float angle = (float)(2.0 * Math.Acos(dot) * 180.0 / Math.PI);
            if (angle < 1e-4f || maxDegrees >= angle) return to;
            float ratio = maxDegrees / angle, theta = (float)Math.Acos(dot), sin = (float)Math.Sin(theta);
            float a1 = (float)Math.Sin((1 - ratio) * theta) / sin, a2 = (float)Math.Sin(ratio * theta) / sin;
            return new Quaternion(from.x * a1 + to.x * a2, from.y * a1 + to.y * a2, from.z * a1 + to.z * a2, from.w * a1 + to.w * a2);
        }
    }
    public struct Color { public float r, g, b, a; public Color(float r,float g,float b,float a) { this.r=r; this.g=g; this.b=b; this.a=a; } public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; this.a=1; } }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin { get { return x; } }
        public float yMin { get { return y; } }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
        public static Rect MinMaxRect(float a, float b, float c, float d) { return new Rect(a, b, c - a, d - b); }
    }
    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) { return new Matrix4x4(); } }
    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f;
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Abs(float v) { return Math.Abs(v); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public const float PI = 3.14159274f;
        public static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Tan(float v) { return (float)Math.Tan(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
    }
    public class Object
    {
        public string name = "";
        public static void DestroyImmediate(Object o) { }
        public static T FindFirstObjectByType<T>() where T : Component { return null; }
    }
    public class Transform : Object
    {
        public GameObject owner;
        public Vector3 position, localScale = new Vector3(1, 1, 1);
        public Quaternion rotation = Quaternion.identity;
        public Vector3 forward { get { return rotation * Vector3.forward; } }
        public void SetParent(Transform t) { }
        public void LookAt(Vector3 target)
        {
            Vector3 d = target - position;
            if (d.magnitude > 1e-6f) rotation = Quaternion.LookRotation(d);
        }
    }
    public class Component : Object
    {
        public GameObject owner;
        public Transform transform { get { return owner.transform; } }
        public GameObject gameObject { get { return owner; } }
        public T GetComponent<T>() where T : Component { return owner.GetComponent<T>(); }
    }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public enum PrimitiveType { Cube, Capsule, Sphere }
    public class GameObject : Object
    {
        readonly List<Component> components = new List<Component>();
        public int layer;
        public Transform transform;
        public GameObject() { transform = new Transform(); transform.owner = this; }
        public GameObject(string n) : this() { name = n; }
        public static GameObject CreatePrimitive(PrimitiveType t)
        {
            var go = new GameObject(t.ToString());
            go.AddComponent<Renderer>(); go.AddComponent<Collider>();
            return go;
        }
        public T AddComponent<T>() where T : Component
        {
            var c = (T)Activator.CreateInstance(typeof(T));
            c.owner = this; components.Add(c); return c;
        }
        public T GetComponent<T>() where T : Component
        {
            foreach (var c in components) if (c is T) return (T)c;
            return null;
        }
    }
    public class Shader : Object { public static Shader Find(string n) { return new Shader(); } }
    public class Material : Object { public Color color; public Material(Shader s) { } }
    public class Renderer : Component { public bool enabled; public Material sharedMaterial; }
    public class Collider : Component { public bool enabled; }
    public class Texture : Object { }
    public class Texture2D : Texture { public static Texture2D whiteTexture=new Texture2D(); }
    public class RenderTexture : Texture { public RenderTexture(int w, int h, int d) { } public void Release() { } }
    public class TextAsset : Object { public string text; }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Behaviour
    {
        public RenderTexture targetTexture;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float fieldOfView, nearClipPlane, depth, orthographicSize;
        public int cullingMask;
        public bool orthographic;
        public Vector3 WorldToViewportPoint(Vector3 v) { return Vector3.zero; }
    }
    public enum ScaleMode { StretchToFill }
    public static class GUI
    {
        public static Matrix4x4 matrix; public static Color color;
        public static void Label(Rect r, string s) { }
        public static bool Button(Rect r, string s) { return false; }
        public static void DrawTexture(Rect r, Texture t, ScaleMode m) { }
        public static Vector2 BeginScrollView(Rect a, Vector2 p, Rect b) { return p; }
        public static void EndScrollView() { }
    }
    public static class Screen { public static int width = 1280, height = 800; }
    public static class Time { public static float deltaTime = .02f; }
    public static class Resources
    {
        public static string Root = "Assets/Resources/";
        public static T Load<T>(string path) where T : Object
        {
            string file = Root + path + ".json";
            if (!System.IO.File.Exists(file)) return null;
            var asset = new TextAsset(); asset.text = System.IO.File.ReadAllText(file);
            return asset as T;
        }
    }
    // Enough of JsonUtility to load teams.json onto [Serializable] classes: public
    // fields only, unknown keys ignored, arrays and nested objects supported.
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) { int i = 0; return (T)Bind(typeof(T), Parse(json, ref i)); }
        static object Bind(Type type, object node)
        {
            if (node == null) return type.IsValueType ? Activator.CreateInstance(type) : null;
            if (type == typeof(string)) return Convert.ToString(node);
            if (type == typeof(int)) return Convert.ToInt32(node, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return Convert.ToSingle(node, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return Convert.ToBoolean(node);
            if (type.IsArray)
            {
                var list = node as List<object>; var element = type.GetElementType();
                if (list == null) return Array.CreateInstance(element, 0);
                var array = Array.CreateInstance(element, list.Count);
                for (int i = 0; i < list.Count; i++) array.SetValue(Bind(element, list[i]), i);
                return array;
            }
            var map = node as Dictionary<string, object>;
            var target = Activator.CreateInstance(type);
            if (map == null) return target;
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (map.ContainsKey(field.Name)) field.SetValue(target, Bind(field.FieldType, map[field.Name]));
            return target;
        }
        static object Parse(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            char c = s[i];
            if (c == '{')
            {
                var map = new Dictionary<string, object>(); i++;
                while (true)
                {
                    while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                    if (s[i] == '}') { i++; break; }
                    string key = (string)Parse(s, ref i);
                    while (char.IsWhiteSpace(s[i]) || s[i] == ':') i++;
                    map[key] = Parse(s, ref i);
                }
                return map;
            }
            if (c == '[')
            {
                var list = new List<object>(); i++;
                while (true)
                {
                    while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                    if (s[i] == ']') { i++; break; }
                    list.Add(Parse(s, ref i));
                }
                return list;
            }
            if (c == '"')
            {
                var text = new StringBuilder(); i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; text.Append(s[i] == 'n' ? '\n' : s[i]); }
                    else text.Append(s[i]);
                    i++;
                }
                i++; return text.ToString();
            }
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }
    }
    public static class Debug
    {
        public static void Log(object o) { Console.WriteLine(o); }
        public static void LogException(Exception e) { Console.WriteLine(e); }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public static class SceneManager { public static Scene GetActiveScene() { return new Scene(); } }
}
namespace UnityEditor
{
    using UnityEngine;
    [AttributeUsage(AttributeTargets.Method)] public class MenuItemAttribute : Attribute { public MenuItemAttribute(string p) { } }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public static class PlayerSettings { public static string productName; public static int defaultScreenWidth, defaultScreenHeight; }
    public static class AssetDatabase { public static void SaveAssets() { } }
}
namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;
    public enum NewSceneSetup { EmptyScene }
    public enum NewSceneMode { Single }
    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup s, NewSceneMode m) { return new Scene(); }
        public static void SaveScene(Scene s, string path) { }
        public static Scene OpenScene(string path) { return new Scene(); }
    }
}
