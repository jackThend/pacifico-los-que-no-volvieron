using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Naturaleza de cada capítulo: matorral seco, ramas, piedras sueltas y peñascos (CC0 de Poly Haven, de la
    /// colección de Namaqualand, un desierto parecido al de Atacama), arbustos verdes en los valles y matas de caña en
    /// Cañete (planta de maíz CC-BY de Sketchfab, de la misma familia y silueta). Cada modelo se une en una sola malla
    /// con niveles de detalle (Unity 6.2+) y se reparte sobre el suelo, lejos de edificios y parapetos. Solo es decorado:
    /// sin colisionadores, así que no cambia el NavMesh ni los disparos. Se aplica al final de
    /// <see cref="GameBuilder.BuildAllScenes"/>.
    /// </summary>
    public static class NatureDressing
    {
        private const string PrefabFolder = "Assets/Prefabs/Naturaleza";
        private const string RootName = "Naturaleza";

        private sealed class Plant
        {
            public string Name;
            /// <summary>Recurso de Poly Haven (FBX con sus texturas) o, si empieza por «glb:», GLB de Sketchfab.</summary>
            public string Source;
            public bool Cutout;
            /// <summary>Alto en metros (0: tamaño real del modelo escaneado).</summary>
            public float Height;
            public string[] Hidden = new string[0];
            /// <summary>Escala base: el matorral de Namaqualand es más bajo que la tola y la brea de Atacama.</summary>
            public float Scale = 1f;
        }

        private static readonly Dictionary<string, Plant> Plants = new Dictionary<string, Plant>
        {
            ["Matorral"] = new Plant { Name = "Matorral", Source = "wild_rooibos_bush", Cutout = true, Scale = 2.4f },
            ["Ramas"] = new Plant { Name = "Ramas", Source = "dry_branches_medium_01", Scale = 2.0f },
            ["Piedras"] = new Plant { Name = "Piedras", Source = "namaqualand_stones_01", Scale = 4f },
            ["Penasco_A"] = new Plant { Name = "Penasco_A", Source = "namaqualand_boulder_02" },
            ["Penasco_B"] = new Plant { Name = "Penasco_B", Source = "namaqualand_boulder_04" },
            ["Arbusto"] = new Plant { Name = "Arbusto", Source = "shrub_04", Cutout = true, Scale = 4.5f },
            // Caña de azúcar: la planta de maíz sin mazorcas, a 3,2 m (la caña del valle llega a 3–4 m).
            ["Cana"] = new Plant { Name = "Cana", Source = "glb:cana_maiz", Height = 3.2f, Hidden = new[] { "Cylinder.001", "Cylinder.002", "Torus", "Torus.001" } },
        };

        private sealed class Scatter
        {
            /// <summary>Nombre del suelo («Terreno» para los terrenos con relieve).</summary>
            public string Ground;
            public bool Prefix;
            public string Plant;
            public int Count;
            public float MinScale = 0.8f, MaxScale = 1.25f;
            /// <summary>Distancia mínima entre dos del mismo grupo (m).</summary>
            public float Spacing = 2f;
            /// <summary>Radio libre de edificios, muros y parapetos (m).</summary>
            public float Clearance = 1.5f;
            public float MaxSlope = 32f;
            public bool AlignToGround;
        }

        private static Scatter R(string ground, string plant, int count, float spacing = 2f, float minScale = 0.8f, float maxScale = 1.25f,
                                 float clearance = 1.5f, float maxSlope = 32f, bool prefix = false, bool align = false) =>
            new Scatter
            {
                Ground = ground, Plant = plant, Count = count, Spacing = spacing, MinScale = minScale, MaxScale = maxScale,
                Clearance = clearance, MaxSlope = maxSlope, Prefix = prefix, AlignToGround = align,
            };

        // Cantidades de ajuste: el desierto es ralo; los valles, algo más verdes.
        private static readonly Dictionary<string, Scatter[]> Scenes = new Dictionary<string, Scatter[]>
        {
            ["Capitulo3_Bahia_de_Pisagua"] = new[]
            {
                R("Terreno", "Piedras", 180, 2.5f, align: true), R("Terreno", "Matorral", 90, 3.1f), R("Terreno", "Ramas", 45, 3.8f),
                R("Terreno", "Penasco_A", 5, 25f, 0.6f, 1f, clearance: 4f, maxSlope: 45f, align: true),
            },
            ["Capitulo4_Quebrada_de_Tarapaca"] = new[]
            {
                R("Fondo_Quebrada", "Arbusto", 120, 1.9f), R("Fondo_Quebrada", "Matorral", 60, 2.5f), R("Fondo_Quebrada", "Piedras", 90, 2.5f, align: true),
                R("Ladera_", "Penasco_B", 8, 20f, 0.6f, 1.1f, clearance: 3f, maxSlope: 50f, prefix: true, align: true),
                R("Ladera_", "Piedras", 75, 3.8f, maxSlope: 50f, prefix: true, align: true),
                R("Pampa_", "Matorral", 75, 3.8f, prefix: true), R("Pampa_", "Ramas", 45, 5.0f, prefix: true), R("Pampa_", "Piedras", 60, 5.0f, prefix: true, align: true),
            },
            ["Capitulo5_Alto_de_la_Alianza"] = new[]
            {
                R("Terreno", "Matorral", 180, 3.8f), R("Terreno", "Ramas", 75, 6.2f), R("Terreno", "Piedras", 180, 3.1f, align: true),
                R("Terreno", "Penasco_A", 4, 40f, 0.5f, 0.9f, clearance: 4f, align: true), R("Terreno", "Penasco_B", 4, 40f, 0.5f, 0.9f, clearance: 4f, align: true),
            },
            ["Capitulo6_Morro_de_Arica"] = new[]
            {
                R("Ladera_Este", "Piedras", 120, 2.5f, maxSlope: 40f, align: true), R("Ladera_Este", "Matorral", 45, 3.8f, maxSlope: 40f),
                R("Ladera_Este", "Penasco_B", 6, 20f, 0.5f, 0.9f, clearance: 3f, maxSlope: 40f, align: true),
                R("Llano_de_los_Fuertes", "Matorral", 75, 5.0f), R("Llano_de_los_Fuertes", "Piedras", 90, 3.8f, align: true),
                R("Macizo_del_Morro", "Piedras", 45, 3.8f, align: true),
            },
            ["Capitulo7_Canaverales_de_Canete"] = new[]
            {
                R("Suelo", "Arbusto", 90, 2.5f, clearance: 1f),
            },
            ["Capitulo8_Reductos_de_Miraflores"] = new[]
            {
                R("Suelo", "Arbusto", 105, 2.5f), R("Suelo", "Piedras", 90, 3.1f, align: true), R("Suelo", "Ramas", 45, 3.8f), R("Suelo", "Matorral", 30, 3.8f),
            },
        };

        private static readonly Dictionary<string, GameObject> Built = new Dictionary<string, GameObject>();

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.NatureDressing.ApplyAllBatch</c>.</summary>
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
            Built.Clear();
            foreach (KeyValuePair<string, Scatter[]> entry in Scenes)
            {
                string path = GameBuilder.ScenePath(entry.Key);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GameObject old = GameObject.Find(RootName);
                if (old != null) Object.DestroyImmediate(old);
                Physics.SyncTransforms();
                var root = new GameObject(RootName).transform;

                int placed = 0;
                int seed = 0;
                foreach (char c in entry.Key) seed = seed * 31 + c; // estable entre ejecuciones (GetHashCode no lo es)
                var random = new System.Random(seed & 0x7fffffff);
                foreach (Scatter scatter in entry.Value) placed += Spread(scatter, root, random);
                if (entry.Key == "Capitulo7_Canaverales_de_Canete") placed += CaneRows(root, random);

                Debug.Log("[Pacífico] Naturaleza de " + entry.Key + ": " + placed);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        // --------------------------------------------------------------------------------------------------------
        // Reparto
        // --------------------------------------------------------------------------------------------------------

        private static int Spread(Scatter scatter, Transform root, System.Random random)
        {
            GameObject prefab = PrefabFor(scatter.Plant);
            if (prefab == null) return 0;
            var grounds = new HashSet<GameObject>();
            Bounds area = default;
            bool any = false;
            foreach (Collider collider in All<Collider>())
            {
                if (!IsGround(collider.gameObject, scatter)) continue;
                grounds.Add(collider.gameObject);
                if (!any) { area = collider.bounds; any = true; }
                else area.Encapsulate(collider.bounds);
            }
            if (!any) return 0;

            var points = new List<Vector3>();
            for (int attempt = 0; attempt < scatter.Count * 40 && points.Count < scatter.Count; attempt++)
            {
                float x = Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble());
                float z = Mathf.Lerp(area.min.z, area.max.z, (float)random.NextDouble());
                if (!Physics.Raycast(new Vector3(x, area.max.y + 100f, z), Vector3.down, out RaycastHit hit, area.size.y + 300f,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (!grounds.Contains(hit.collider.gameObject)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > scatter.MaxSlope) continue;
                if (TooClose(points, hit.point, scatter.Spacing)) continue;
                if (Blocked(hit.point, scatter.Clearance, grounds)) continue;
                points.Add(hit.point);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root, true);
                Quaternion yaw = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                Quaternion tilt = scatter.AlignToGround ? Quaternion.FromToRotation(Vector3.up, hit.normal) : Quaternion.identity;
                instance.transform.SetPositionAndRotation(hit.point - hit.normal * 0.03f, tilt * yaw);
                instance.transform.localScale = Vector3.one * Mathf.Lerp(scatter.MinScale, scatter.MaxScale, (float)random.NextDouble()) * BaseScale(scatter.Plant);
            }
            return points.Count;
        }

        private static float BaseScale(string key) => Plants.TryGetValue(key, out Plant plant) ? plant.Scale : 1f;

        private static bool IsGround(GameObject go, Scatter scatter)
        {
            if (scatter.Ground == "Terreno") return go.GetComponent<Terrain>() != null;
            return scatter.Prefix ? go.name.StartsWith(scatter.Ground, System.StringComparison.Ordinal) : go.name == scatter.Ground;
        }

        private static bool TooClose(List<Vector3> points, Vector3 point, float spacing)
        {
            float limit = spacing * spacing;
            foreach (Vector3 p in points)
            {
                if ((p - point).sqrMagnitude < limit) return true;
            }
            return false;
        }

        /// <summary>¿Hay un edificio, muro, parapeto o pieza de juego a menos de <paramref name="radius"/> metros?</summary>
        private static bool Blocked(Vector3 point, float radius, HashSet<GameObject> grounds)
        {
            foreach (Collider near in Physics.OverlapSphere(point + Vector3.up * (radius + 0.3f), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!grounds.Contains(near.gameObject)) return true;
            }
            return false;
        }

        /// <summary>
        /// Cañaverales de Cañete: las dos hileras más cercanas a la senda de cada lado se llenan de matas de caña (la
        /// primera más tupida) y su caja deja de verse; la caja conserva su colisionador, que es el que tapa la vista y
        /// el paso. Las hileras de detrás quedan como masa oscura.
        /// </summary>
        private static int CaneRows(Transform root, System.Random random)
        {
            GameObject prefab = PrefabFor("Cana");
            if (prefab == null) return 0;
            var rows = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in All<MeshRenderer>())
            {
                if (renderer.gameObject.name == "Hilera_de_Cana") rows.Add(renderer);
            }
            rows.Sort((a, b) => Mathf.Abs(a.bounds.center.x).CompareTo(Mathf.Abs(b.bounds.center.x)));
            int placed = 0;
            for (int r = 0; r < rows.Count && r < 4; r++)
            {
                Bounds box = rows[r].bounds;
                bool front = r < 2;
                int lanes = front ? 3 : 2;
                float step = front ? 0.7f : 1f;
                float side = Mathf.Sign(box.center.x);
                for (float z = box.min.z + 0.3f; z < box.max.z - 0.3f; z += step)
                {
                    for (int lane = 0; lane < lanes; lane++)
                    {
                        // La primera fila mira a la senda: va pegada al borde interior.
                        float across = lanes == 1 ? 0.5f : lane / (lanes - 1f);
                        float x = Mathf.Lerp(box.center.x - side * (box.extents.x - 0.25f), box.center.x + side * (box.extents.x - 0.4f), across);
                        x += ((float)random.NextDouble() - 0.5f) * 0.3f;
                        float jz = z + ((float)random.NextDouble() - 0.5f) * 0.3f;
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.SetParent(root, true);
                        instance.transform.SetPositionAndRotation(new Vector3(x, box.min.y, jz), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                        instance.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.1f, (float)random.NextDouble());
                        // Solo la fila de la senda proyecta sombra a la luz de la luna (rendimiento).
                        if (!front || lane > 0) instance.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                        placed++;
                    }
                }
                rows[r].enabled = false;
            }
            return placed;
        }

        // --------------------------------------------------------------------------------------------------------
        // Modelos unidos en una sola malla
        // --------------------------------------------------------------------------------------------------------

        private static GameObject PrefabFor(string key)
        {
            if (Built.TryGetValue(key, out GameObject cached)) return cached;
            GameObject prefab = Plants.TryGetValue(key, out Plant plant) ? Build(plant) : null;
            Built[key] = prefab;
            return prefab;
        }

        private static GameObject Build(Plant plant)
        {
            GameObject source = Instantiate(plant);
            if (source == null)
            {
                Debug.LogWarning("[Pacífico] Falta el modelo de naturaleza " + plant.Source);
                return null;
            }
            try
            {
                if (plant.Cutout)
                {
                    Material leaves = ArtLibrary.CutoutSurface(plant.Source);
                    if (leaves != null)
                    {
                        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
                        {
                            var materials = new Material[renderer.sharedMaterials.Length];
                            for (int i = 0; i < materials.Length; i++) materials[i] = leaves;
                            renderer.sharedMaterials = materials;
                        }
                    }
                }

                var groups = new Dictionary<Material, List<CombineInstance>>();
                var order = new List<Material>();
                // En medidas del mundo: la raíz de los FBX trae su propio giro y escala (de centímetros a metros).
                source.transform.position = Vector3.zero;
                Matrix4x4 toRoot = Matrix4x4.identity;
                foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null || IsHidden(filter.transform, plant.Hidden)) continue;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null) continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                    {
                        Material material = materials[Mathf.Min(s, materials.Length - 1)];
                        if (!groups.TryGetValue(material, out List<CombineInstance> list))
                        {
                            list = new List<CombineInstance>();
                            groups[material] = list;
                            order.Add(material);
                        }
                        list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = s, transform = toRoot * filter.transform.localToWorldMatrix });
                    }
                }
                if (order.Count == 0) return null;

                var parts = new CombineInstance[order.Count];
                for (int i = 0; i < order.Count; i++)
                {
                    var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                    part.CombineMeshes(groups[order[i]].ToArray(), true, true);
                    parts[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
                }
                var mesh = new Mesh { name = plant.Name + "_Malla", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(parts, false, false);
                foreach (CombineInstance part in parts) Object.DestroyImmediate(part.mesh);

                // Base apoyada en el origen y, si se pide, a su alto real.
                Bounds bounds = mesh.bounds;
                float scale = plant.Height > 0f && bounds.size.y > 0f ? plant.Height / bounds.size.y : 1f;
                Vector3 offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] + offset) * scale;
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
#if UNITY_6000_2_OR_NEWER
                // Niveles de detalle: de lejos Unity dibuja versiones con menos triángulos.
                MeshLodUtility.GenerateMeshLods(mesh, 8);
#endif
                HistoricalDataAssetGenerator.EnsureFolder(PrefabFolder);
                string meshPath = PrefabFolder + "/" + plant.Name + "_Malla.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject(plant.Name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = order.ToArray();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + plant.Name + ".prefab");
                Object.DestroyImmediate(go);
                Debug.Log("[Pacífico] Modelo de naturaleza " + plant.Name + ": " + mesh.vertexCount + " vértices, alto " + mesh.bounds.size.y.ToString("0.00") + " m");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static GameObject Instantiate(Plant plant)
        {
            if (plant.Source.StartsWith("glb:", System.StringComparison.Ordinal))
            {
                string path = ShipModelLibrary.Root + "/" + plant.Source.Substring(4) + ".glb";
                var asset = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                return asset != null ? (GameObject)Object.Instantiate(asset) : null;
            }
            return ArtLibrary.Model(plant.Source, plant.Name, null);
        }

        private static bool IsHidden(Transform part, string[] hidden)
        {
            for (Transform t = part; t != null; t = t.parent)
            {
                if (System.Array.IndexOf(hidden, t.name) >= 0) return true;
            }
            return false;
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
