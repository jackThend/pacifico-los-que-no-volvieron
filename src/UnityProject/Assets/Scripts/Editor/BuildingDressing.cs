using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Edificios y objetos de época: cambia el color liso de los materiales de construcción de los constructores (adobe,
    /// encalado, pirca, caliza, techos de barro y teja, madera) por texturas CC0 de Poly Haven sacadas de muros, techos y
    /// tablones reales, y deja junto a las paredes de casas e iglesias unos pocos objetos de 1879 (barriles, cubos,
    /// tinajas, bancos, mesas y cestos). Solo cambia el aspecto: las piezas conservan sus colisionadores y los objetos
    /// no tienen. Se aplica al final de <see cref="GameBuilder.BuildAllScenes"/>.
    /// </summary>
    public static class BuildingDressing
    {
        private const string RootName = "Objetos_de_Epoca";

        /// <summary>Material de los constructores (sin «Proto_») → textura y repetición en metros (valores de ajuste).</summary>
        private static readonly Dictionary<string, (string texture, float tile, Color tint)> Surfaces = new Dictionary<string, (string, float, Color)>
        {
            // Muros de adobe con revoque de barro gastado.
            ["Adobe"] = ("rough_plaster_broken", 3f, Color.white),
            ["Tapia"] = ("rough_plaster_broken", 3f, Color.white),
            ["Casa"] = ("rough_plaster_broken", 3f, Color.white),
            // La iglesia, encalada y algo sucia.
            ["Iglesia"] = ("painted_plaster_wall", 3f, new Color(0.93f, 0.89f, 0.8f)),
            // Pircas de piedra seca y fuertes de caliza.
            ["Pirca"] = ("stone_wall", 2f, Color.white),
            ["Caliza"] = ("coral_fort_wall_01", 3f, Color.white),
            // Techos de caña cubiertos de barro («torta de barro»); el campanario, de teja.
            ["Techo"] = (ArtLibrary.DryMud, 3f, Color.white),
            ["Tejado"] = ("clay_roof_tiles_02", 2f, Color.white),
            // Tablones gastados y madera basta.
            ["Madera"] = ("weathered_brown_planks", 1.5f, Color.white),
            ["Porton"] = ("weathered_brown_planks", 1.5f, Color.white),
            ["Cajon"] = ("weathered_brown_planks", 1.2f, Color.white),
            ["Madera_Carro"] = ("weathered_brown_planks", 1.5f, Color.white),
            ["Madera_Pisagua"] = ("weathered_brown_planks", 1.5f, Color.white),
            ["Cureña"] = ("rough_wood", 1f, Color.white),
            ["Rueda"] = ("rough_wood", 1f, Color.white),
            ["Estaca"] = ("rough_wood", 1f, Color.white),
            ["Tronco"] = ("rough_wood", 1f, Color.white),
        };

        /// <summary>Edificios junto a cuyas paredes se dejan objetos (prefijo del nombre de la pieza).</summary>
        private static readonly string[] Buildings = { "Casa_", "Iglesia_Nave", "Casa_Hacienda" };

        /// <summary>Objetos de época de Poly Haven y su escala (tamaño real del escaneo).</summary>
        private static readonly (string id, float scale, bool alongWall)[] Props =
        {
            ("wine_barrel_01", 1f, false), ("wooden_bucket_01", 1f, false), ("ceramic_pot", 1.6f, false),
            ("painted_wooden_bench", 1f, true), ("wicker_basket_01", 1f, false), ("wooden_table_02", 1f, true),
        };

        private static readonly string[] Scenes =
        {
            "Capitulo1_Rada_de_Iquique", "Capitulo2_Punta_Angamos", "Capitulo3_Bahia_de_Pisagua", "Capitulo4_Quebrada_de_Tarapaca",
            "Capitulo5_Alto_de_la_Alianza", "Capitulo6_Morro_de_Arica", "Capitulo7_Canaverales_de_Canete", "Capitulo8_Reductos_de_Miraflores",
        };

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.BuildingDressing.ApplyAllBatch</c>.</summary>
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
            foreach (string sceneName in Scenes)
            {
                string path = GameBuilder.ScenePath(sceneName);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GameObject old = GameObject.Find(RootName);
                if (old != null) Object.DestroyImmediate(old);
                Physics.SyncTransforms();

                int dressed = 0;
                foreach (MeshRenderer renderer in All<MeshRenderer>())
                {
                    Material material = renderer.sharedMaterial;
                    if (!renderer.enabled || material == null || !material.name.StartsWith("Proto_", System.StringComparison.Ordinal)) continue;
                    if (!Surfaces.TryGetValue(material.name.Substring(6), out var surface)) continue;
                    if (ArtLibrary.Dress(renderer.gameObject, surface.texture, surface.tile, surface.tint)) dressed++;
                }

                int seed = 0;
                foreach (char c in sceneName) seed = seed * 31 + c;
                int props = PlaceProps(new System.Random(seed & 0x7fffffff));
                Debug.Log("[Pacífico] Edificios de " + sceneName + ": " + dressed + " piezas con textura, " + props + " objetos");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>De uno a tres objetos junto a las paredes largas de cada casa, sobre el suelo y sin tapar nada.</summary>
        private static int PlaceProps(System.Random random)
        {
            var houses = new List<Collider>();
            foreach (Collider collider in All<Collider>())
            {
                foreach (string prefix in Buildings)
                {
                    if (collider.gameObject.name.StartsWith(prefix, System.StringComparison.Ordinal) && collider is BoxCollider)
                    {
                        houses.Add(collider);
                        break;
                    }
                }
            }
            if (houses.Count == 0) return 0;
            var root = new GameObject(RootName).transform;
            int placed = 0;
            foreach (Collider house in houses)
            {
                Transform t = house.transform;
                Vector3 size = Vector3.Scale(((BoxCollider)house).size, t.lossyScale);
                if (size.y < 1.5f) continue; // muretes y techos
                int count = 1 + random.Next(3);
                for (int i = 0; i < count; i++)
                {
                    // Una cara lateral al azar (±X o ±Z local) y un punto a lo largo de ella, a medio metro de la pared.
                    bool alongX = random.Next(2) == 0;
                    float sign = random.Next(2) == 0 ? -1f : 1f;
                    float along = ((float)random.NextDouble() - 0.5f) * 0.8f;
                    Vector3 local = alongX
                        ? new Vector3(along * size.x, 0f, sign * (size.z * 0.5f + 0.55f))
                        : new Vector3(sign * (size.x * 0.5f + 0.55f), 0f, along * size.z);
                    Vector3 outward = alongX ? t.forward * sign : t.right * sign;
                    Vector3 point = t.position + t.right * local.x + t.forward * local.z;
                    if (!Physics.Raycast(new Vector3(point.x, house.bounds.max.y + 5f, point.z), Vector3.down, out RaycastHit hit,
                                         house.bounds.size.y + 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider == house || Vector3.Angle(hit.normal, Vector3.up) > 15f) continue;
                    if (Physics.CheckSphere(hit.point + Vector3.up * 0.7f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

                    var prop = Props[random.Next(Props.Length)];
                    GameObject model = ArtLibrary.Model(prop.id, prop.id, root);
                    if (model == null) continue;
                    foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    model.transform.localScale *= prop.scale;
                    Quaternion facing = prop.alongWall
                        ? Quaternion.LookRotation(outward, Vector3.up)
                        : Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    model.transform.rotation = facing * model.transform.rotation;
                    Bounds bounds = ArtLibrary.WorldBounds(model);
                    model.transform.position += hit.point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    placed++;
                }
            }
            return placed;
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
