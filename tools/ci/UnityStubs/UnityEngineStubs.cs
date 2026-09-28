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

    public class GameObject : Object { }
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public class AudioClip : Object { }

    public class Component : Object
    {
        public GameObject gameObject => null;
    }

    public class Behaviour : Component { }
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
