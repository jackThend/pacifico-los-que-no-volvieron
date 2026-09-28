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

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
    }
}
