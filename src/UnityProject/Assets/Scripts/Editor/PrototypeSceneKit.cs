using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Utilidades compartidas por los constructores de escenas de prototipo (greyboxing con primitivas, AGENTS.md §4.A).
    /// </summary>
    internal static class PrototypeSceneKit
    {
        /// <summary>Material URP Lit (o Standard si URP no está activo) guardado como asset y reutilizado por clave.</summary>
        public static Material Material(string key, Color color, float smoothness = 0.25f)
        {
            string path = ProjectPaths.Materials + "/Proto_" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
                                           Vector3 localScale, Material material, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
