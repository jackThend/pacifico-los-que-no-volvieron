// Stubs mínimos de UnityEditor (solo compilación). Ver UnityEngineStubs.cs.
using System;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute { public MenuItem(string itemName) { } }

    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => null;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parentFolder, string newFolderName) => string.Empty;
        public static void SaveAssets() { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InitializeOnLoadAttribute : Attribute { }

    public class SceneAsset : UnityEngine.Object { }

    public static class EditorApplication
    {
        public delegate void CallbackFunction();
        public static CallbackFunction delayCall;
        public static bool isCompiling => false;
        public static bool isUpdating => false;
        public static bool isPlayingOrWillChangePlaymode => false;
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }
    public enum NewSceneMode { Single = 0, Additive = 1 }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static UnityEngine.SceneManagement.Scene GetActiveScene() => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string dstScenePath) => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string scenePath) => default;
    }
}
