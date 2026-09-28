// Stubs mínimos de la API de Unity, SOLO para comprobar compilación de
// Pacifico.Runtime / Pacifico.Editor fuera del editor. No se ejecutan.
// Añadir aquí únicamente los miembros que el código del juego use.
using System;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static implicit operator bool(Object obj) => !ReferenceEquals(obj, null);
    }

    public enum PrimitiveType { Sphere = 0, Capsule = 1, Cylinder = 2, Cube = 3, Plane = 4, Quad = 5 }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public Transform transform { get; } = new Transform();
        public T AddComponent<T>() where T : Component => Activator.CreateInstance<T>();
        public T GetComponent<T>() => default;
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject();
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }

    public struct Vector2
    {
        public float x, y;
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);
    }

    public struct Ray
    {
        public Vector3 GetPoint(float distance) => default;
    }

    public struct Plane
    {
        public Plane(Vector3 inNormal, Vector3 inPoint) { }
        public bool Raycast(Ray ray, out float enter) { enter = 0f; return false; }
    }

    public static class Screen
    {
        public static int height => 0;
    }

    public struct Quaternion
    {
        public static Quaternion Euler(float x, float y, float z) => default;
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
    }
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public class AudioClip : Object { }

    public class Component : Object
    {
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default;
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 forward => Vector3.forward;
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
        public void LookAt(Vector3 worldPosition) { }
    }

    public enum RigidbodyInterpolation { None = 0, Interpolate = 1, Extrapolate = 2 }

    public class Rigidbody : Component
    {
        public bool isKinematic { get; set; }
        public bool useGravity { get; set; }
        public RigidbodyInterpolation interpolation { get; set; }
        public void MovePosition(Vector3 position) { }
        public void MoveRotation(Quaternion rotation) { }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public float farClipPlane { get; set; }
        public Ray ScreenPointToRay(Vector3 pos) => default;
        public Vector3 WorldToScreenPoint(Vector3 position) => default;
    }

    public static class Time
    {
        public static float deltaTime => 0f;
        public static float fixedDeltaTime => 0.02f;
    }

    public static class GUI
    {
        public static void Label(Rect position, string text) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }
    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => Activator.CreateInstance<T>();
    }

    public static class Application
    {
        public static string dataPath => string.Empty;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message, Object context = null) { }
        public static void LogError(object message, Object context = null) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class PropertyAttribute : Attribute { }

    public sealed class HeaderAttribute : PropertyAttribute { public HeaderAttribute(string header) { } }
    public sealed class TooltipAttribute : PropertyAttribute { public TooltipAttribute(string tooltip) { } }
    public sealed class TextAreaAttribute : PropertyAttribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int minLines, int maxLines) { }
    }
    public sealed class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }
    public sealed class MinAttribute : PropertyAttribute { public MinAttribute(float min) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
    }
}

namespace UnityEngine.InputSystem
{
    public class ButtonControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public sealed class KeyControl : ButtonControl { }

    public sealed class Vector2Control
    {
        public Vector2 ReadValue() => default;
    }

    public class Mouse
    {
        public static Mouse current => null;
        public Vector2Control position { get; } = new Vector2Control();
        public ButtonControl leftButton { get; } = new ButtonControl();
    }

    public class Keyboard
    {
        public static Keyboard current => null;
        public KeyControl wKey { get; } = new KeyControl();
        public KeyControl aKey { get; } = new KeyControl();
        public KeyControl sKey { get; } = new KeyControl();
        public KeyControl dKey { get; } = new KeyControl();
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { }
}
