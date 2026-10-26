using System.Collections.Generic;
using System.IO;
using Pacifico.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Buques vivos: humo de carbón en cada chimenea (<see cref="FunnelSmoke"/>), espuma de estela en la popa y ola de
    /// proa en los buques que navegan (<see cref="ShipWake"/>), y colores de época en los modelos de Sketchfab: velas de
    /// lona cruda en lugar de blanco puro y piezas pintadas de amarillo o naranja en madera oscura. Las texturas del humo
    /// y de la espuma se generan aquí. Se aplica al final de <see cref="GameBuilder.BuildAllScenes"/>.
    /// </summary>
    public static class ShipDressing
    {
        private const string Folder = "Assets/Materials/Buques";

        private static readonly string[] Scenes =
        {
            "Capitulo1_Rada_de_Iquique", "Capitulo2_Punta_Angamos", "Capitulo3_Bahia_de_Pisagua", "Capitulo6_Morro_de_Arica",
        };

        /// <summary>Material del modelo (por prefijo) → color por el que se multiplica (valores de ajuste).</summary>
        private static readonly (string prefix, Color tint)[] Recolors =
        {
            // Velas de lona cruda (el modelo las trae blanco puro).
            ("01_-_Default1", new Color(0.86f, 0.82f, 0.72f)),
            ("material", new Color(0.86f, 0.82f, 0.72f)),
            ("Tall_ships_antwerp", new Color(0.9f, 0.86f, 0.76f)),
            // Barandillas amarillas y remates naranjas: madera oscura.
            ("Barierka", new Color(0.2f, 0.14f, 0.09f)),
            ("pokad", new Color(0.3f, 0.2f, 0.12f)),
        };

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.ShipDressing.ApplyAllBatch</c>.</summary>
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
            HistoricalDataAssetGenerator.EnsureFolder(Folder);
            Material smoke = ParticleMaterial("Humo_Chimenea", PuffTexture());
            Material foam = ParticleMaterial("Espuma_Estela", FoamTexture());
            foreach (string sceneName in Scenes)
            {
                string path = GameBuilder.ScenePath(sceneName);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                float seaLevel = SeaLevel();
                int stacks = 0, wakes = 0, recolored = 0;

                foreach (Transform t in All<Transform>())
                {
                    if (t.name != "Chimenea" || IsUnder(t, "Locomotoras") || t.parent == null || t.parent.Find("Humo_Chimenea") != null) continue;
                    // Boca de la chimenea (cilindro de Unity: mide 2 de alto a escala 1). El humo cuelga del casco, que no
                    // está escalado, para que el penacho no se deforme; la chimenea de primitivas puede estar oculta bajo
                    // el modelo, pero marca su sitio.
                    Vector3 top = t.position + t.up * t.lossyScale.y;
                    var go = new GameObject("Humo_Chimenea", typeof(ParticleSystem));
                    go.transform.SetParent(t.parent, true);
                    go.transform.SetPositionAndRotation(top + Vector3.up * 0.3f, Quaternion.Euler(-90f, 0f, 0f));
                    var funnel = go.AddComponent<FunnelSmoke>();
                    funnel.Setup(smoke, 5f);
                    stacks++;
                }

                foreach (ShipController ship in All<ShipController>())
                {
                    if (ship.transform.Find("Estela_Popa") != null) continue;
                    Bounds hull = ShipModelLibrary.LocalBounds(ship.gameObject, ship.transform);
                    float beam = Mathf.Clamp(hull.size.x, 4f, 20f);
                    float water = ship.transform.InverseTransformPoint(new Vector3(0f, seaLevel + 0.12f, 0f)).y;
                    Wake(ship.transform, "Estela_Popa", new Vector3(hull.center.x, water, hull.min.z + 1.5f), foam, beam, 18f, 1.6f);
                    Wake(ship.transform, "Ola_de_Proa", new Vector3(hull.center.x, water, hull.max.z - 1f), foam, beam * 0.5f, 3f, 2.5f);
                    wakes++;
                }

                foreach (Renderer renderer in All<Renderer>())
                {
                    // Solo las piezas del modelo de la Esmeralda (la fragata de Sketchfab): otros modelos usan los mismos nombres.
                    if (renderer is ParticleSystemRenderer) continue;
                    string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
                    if (string.IsNullOrEmpty(source) || !source.EndsWith(ShipModelLibrary.Constitution + ".glb", System.StringComparison.Ordinal)) continue;
                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material tinted = Recolor(materials[i]);
                        if (tinted == null) continue;
                        materials[i] = tinted;
                        changed = true;
                    }
                    if (!changed) continue;
                    renderer.sharedMaterials = materials;
                    recolored++;
                }

                Debug.Log("[Pacífico] Buques de " + sceneName + ": " + stacks + " chimeneas con humo, " + wakes + " estelas, " + recolored + " piezas recoloreadas");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Fotos de comprobación: en Iquique y Angamos hace navegar cada buque unos segundos (para que salgan el humo y la
        /// estela) y lo fotografía de costado y desde atrás. Modo batch sin <c>-nographics</c>:
        /// <c>-executeMethod Pacifico.EditorTools.ShipDressing.PreviewBatch -pacifico-preview &lt;carpeta&gt;</c>
        /// </summary>
        public static void PreviewBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/ShipDressingPreview");
                Directory.CreateDirectory(folder);
                int n = 1;
                foreach (string sceneName in new[] { "Capitulo1_Rada_de_Iquique", "Capitulo2_Punta_Angamos" })
                {
                    EditorSceneManager.OpenScene(GameBuilder.ScenePath(sceneName), OpenSceneMode.Single);
                    DynamicGI.UpdateEnvironment();
                    var awake = typeof(ShipWake).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    foreach (FunnelSmoke stack in All<FunnelSmoke>())
                    {
                        var serialized = new SerializedObject(stack);
                        FunnelSmoke.Configure(stack.GetComponent<ParticleSystem>(), (Material)serialized.FindProperty("material").objectReferenceValue, 5f, new Vector3(2.5f, 0f, 0.8f));
                    }
                    var wakes = All<ShipWake>();
                    foreach (ShipWake wake in wakes) awake.Invoke(wake, null);
                    var ships = All<ShipController>();
                    // 25 s a unos 5 m/s: cada paso avanza el buque y deja correr las partículas.
                    for (int step = 0; step < 125; step++)
                    {
                        foreach (ShipController ship in ships) ship.transform.position += ship.transform.forward * 1f;
                        foreach (ShipWake wake in wakes)
                        {
                            ParticleSystem.EmissionModule emission = wake.GetComponent<ParticleSystem>().emission;
                            emission.rateOverTime = wake.name == "Ola_de_Proa" ? 12f : 8f;
                        }
                        foreach (ParticleSystem particles in All<ParticleSystem>()) particles.Simulate(0.2f, false, false, false);
                    }
                    foreach (ShipController ship in ships)
                    {
                        Bounds b = ShipModelLibrary.LocalBounds(ship.gameObject, null);
                        Vector3 c = b.center;
                        float size = Mathf.Max(b.size.x, b.size.z);
                        ShipModelPreview.Shoot(folder, n + "_" + ship.name + "_costado", c + ship.transform.right * size * 1.2f + Vector3.up * size * 0.35f, c + Vector3.up * 4f, 50f);
                        ShipModelPreview.Shoot(folder, n + "_" + ship.name + "_estela", c - ship.transform.forward * size * 1.6f + ship.transform.right * size * 0.4f + Vector3.up * size * 0.5f, c - ship.transform.forward * size * 0.4f, 55f);
                        n++;
                    }
                }
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Wake(Transform ship, string name, Vector3 local, Material foam, float beam, float seconds, float rate)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(ship, false);
            go.transform.localPosition = local;
            go.AddComponent<ShipWake>().Setup(foam, beam, seconds, rate);
        }

        private static float SeaLevel()
        {
            GameObject sea = GameObject.Find("Mar");
            return sea != null ? sea.transform.position.y : 0f;
        }

        private static bool IsUnder(Transform t, string ancestor)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (p.name.Contains(ancestor)) return true;
            }
            return false;
        }

        /// <summary>Copia teñida del material del modelo si su nombre está en <see cref="Recolors"/>; null si no.</summary>
        private static Material Recolor(Material source)
        {
            if (source == null || source.name.StartsWith("Buque_", System.StringComparison.Ordinal)) return null;
            foreach (var (prefix, tint) in Recolors)
            {
                if (!source.name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (prefix == "material" && source.name != "material") return null; // solo el «material» blanco de las velas
                string path = Folder + "/Buque_" + source.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) return material;
                material = new Material(source) { name = "Buque_" + source.name };
                foreach (string property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                {
                    if (material.HasProperty(property)) material.SetColor(property, material.GetColor(property) * tint);
                }
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            return null;
        }

        private static Material ParticleMaterial(string name, Texture2D texture)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = Pacifico.Effects.BlackPowderSmoke.CreateMaterial();
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Bocanada de humo: disco de borde suave con grumos (ruido de Perlin), blanco con transparencia.</summary>
        private static Texture2D PuffTexture() => SaveTexture("bocanada_humo", 128, (u, v) =>
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float noise = 0.65f + 0.35f * Mathf.PerlinNoise(u * 6f + 3.1f, v * 6f + 7.7f);
            return Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, r)) * noise;
        });

        /// <summary>Mancha de espuma: grumos y hebras blancas (ruido de dos escalas) que se apagan hacia el borde.</summary>
        private static Texture2D FoamTexture() => SaveTexture("espuma_estela", 256, (u, v) =>
        {
            float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float blobs = Mathf.PerlinNoise(u * 7f + 1.3f, v * 7f + 9.2f) * 0.65f + Mathf.PerlinNoise(u * 23f + 4.4f, v * 23f + 2.2f) * 0.35f;
            float foam = Mathf.SmoothStep(0.45f, 0.7f, blobs);
            return foam * Mathf.Clamp01(1f - Mathf.SmoothStep(0.5f, 1f, r));
        });

        private static Texture2D SaveTexture(string name, int size, System.Func<float, float, float> alpha)
        {
            string path = Folder + "/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size)));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static T[] All<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}
