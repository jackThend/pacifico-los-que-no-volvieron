using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Mar realista en los capítulos con costa o batalla naval: sustituye el material liso de los planos «Mar» por el
    /// shader <c>Pacifico/Mar</c> (oleaje animado, reflejo del cielo, brillo del sol, transparencia y espuma en la orilla)
    /// con el tono de cada capítulo. Las texturas de oleaje y espuma las genera <c>tools/arte/generar_agua.py</c>. Se
    /// aplica sobre las escenas ya construidas, al final de <see cref="GameBuilder.BuildAllScenes"/>; el plano conserva
    /// su colisionador.
    /// </summary>
    public static class SeaDressing
    {
        public const string Folder = "Assets/Art/Generado/Agua";
        private const string ShaderName = "Pacifico/Mar";

        private sealed class Sea
        {
            public Color Deep, Shallow;
            public float Waves = 0.4f;
            public float Whitecaps = 0.12f;
            public float Smoothness = 0.92f;
        }

        // Tono del mar de cada capítulo (valores de ajuste comprobados con AtmospherePreview).
        private static readonly Dictionary<string, Sea> Seas = new Dictionary<string, Sea>
        {
            // Rada de Iquique bajo la neblina: verde azulado apagado.
            ["Capitulo1_Rada_de_Iquique"] = new Sea { Deep = new Color(0.035f, 0.12f, 0.14f), Shallow = new Color(0.14f, 0.3f, 0.28f), Waves = 0.35f, Whitecaps = 0.05f },
            // Mar de fondo en Angamos: plomizo, picado y con cabrillas.
            ["Capitulo2_Punta_Angamos"] = new Sea { Deep = new Color(0.03f, 0.065f, 0.085f), Shallow = new Color(0.1f, 0.16f, 0.17f), Waves = 0.55f, Whitecaps = 0.15f, Smoothness = 0.88f },
            ["Capitulo3_Bahia_de_Pisagua"] = new Sea { Deep = new Color(0.03f, 0.12f, 0.17f), Shallow = new Color(0.14f, 0.36f, 0.36f), Waves = 0.35f, Whitecaps = 0.04f },
            ["Capitulo6_Morro_de_Arica"] = new Sea { Deep = new Color(0.03f, 0.1f, 0.15f), Shallow = new Color(0.12f, 0.3f, 0.32f), Waves = 0.4f, Whitecaps = 0.08f },
        };

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.SeaDressing.ApplyAllBatch</c>.</summary>
        public static void ApplyAllBatch()
        {
            try
            {
                RenderPipelineSetup.Ensure();
                ApplyAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyAll()
        {
            Shader shader = Shader.Find(ShaderName);
            Texture2D normals = Texture(Folder + "/mar_normales.png", normalMap: true);
            Texture2D foam = Texture(Folder + "/mar_espuma.png", normalMap: false);
            if (shader == null || normals == null || foam == null)
            {
                Debug.LogWarning("[Pacífico] Falta el shader o las texturas del mar: los mares se quedan lisos.");
                return;
            }
            foreach (KeyValuePair<string, Sea> entry in Seas)
            {
                string path = GameBuilder.ScenePath(entry.Key);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Material material = MaterialFor(entry.Key, entry.Value, shader, normals, foam);
                int dressed = 0;
                foreach (MeshRenderer renderer in All<MeshRenderer>())
                {
                    if (renderer.gameObject.name != "Mar") continue;
                    renderer.sharedMaterial = material;
                    // Malla dividida en 200 × 200 (el plano de Unity tiene 10 × 10): con cuadros de kilómetros lo que
                    // se interpola entre vértices hace franjas a lo lejos. El colisionador conserva su malla.
                    if (renderer.TryGetComponent(out MeshFilter filter)) filter.sharedMesh = Grid();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    dressed++;
                }
                Debug.Log("[Pacífico] Mar de " + entry.Key + ": " + dressed);
                if (dressed == 0) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Plano de 10 × 10 unidades (como el de Unity) en 200 × 200 cuadros, guardado como asset.</summary>
        private static Mesh Grid()
        {
            string path = Folder + "/Mar_Malla.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int cells = 200;
            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var normals = new Vector3[vertices.Length];
            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    vertices[z * (cells + 1) + x] = new Vector3(x * 10f / cells - 5f, 0f, z * 10f / cells - 5f);
                    normals[z * (cells + 1) + x] = Vector3.up;
                }
            }
            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int i = z * (cells + 1) + x;
                    triangles[t++] = i;
                    triangles[t++] = i + cells + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + cells + 1;
                    triangles[t++] = i + cells + 2;
                }
            }
            mesh = new Mesh { name = "Mar_Malla", indexFormat = IndexFormat.UInt32, vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material MaterialFor(string sceneName, Sea sea, Shader shader, Texture2D normals, Texture2D foam)
        {
            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
            string path = ProjectPaths.Materials + "/Mar_" + sceneName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_NormalMap", normals);
            material.SetTexture("_FoamMap", foam);
            material.SetColor("_DeepColor", sea.Deep);
            material.SetColor("_ShallowColor", sea.Shallow);
            material.SetFloat("_WaveStrength", sea.Waves);
            material.SetFloat("_Whitecaps", sea.Whitecaps);
            material.SetFloat("_Smoothness", sea.Smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string path, bool normalMap)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.anisoLevel < 16 || importer.wrapMode != TextureWrapMode.Repeat || (!normalMap && importer.sRGBTexture))
            {
                importer.textureType = type;
                importer.anisoLevel = 16;
                importer.wrapMode = TextureWrapMode.Repeat;
                if (!normalMap) importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static T[] All<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(true);
#endif
        }
    }
}
