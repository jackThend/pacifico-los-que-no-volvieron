using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Suelos realistas de cada capítulo con texturas CC0 de Poly Haven (fotos de arena, grava, tierra y roca reales):
    /// los terrenos con relieve mezclan tres texturas según la pendiente (roca en las laderas empinadas) y manchas de
    /// ruido; los suelos hechos con cajas reciben su textura repetida en metros reales. Se aplica sobre las escenas ya
    /// construidas, al final de <see cref="GameBuilder.BuildAllScenes"/>, sin tocar colisionadores ni NavMesh.
    /// </summary>
    public static class GroundDressing
    {
        private sealed class TerrainMix
        {
            public string Base, Patch, Steep;
            public float BaseTile = 9f, PatchTile = 9f, SteepTile = 12f;
            /// <summary>Pendiente (grados) a la que empieza y termina el paso a roca.</summary>
            public float RockFrom = 22f, RockTo = 38f;
            /// <summary>Parte del suelo llano cubierta por manchas de la segunda textura (0–1).</summary>
            public float PatchShare = 0.35f;
        }

        private sealed class Surface
        {
            public string Name;
            public bool Prefix;
            public string Texture;
            public float Tile = 4f;
        }

        private sealed class Ground
        {
            public TerrainMix Terrain;
            public Surface[] Surfaces = new Surface[0];
        }

        /// <summary>
        /// Corrección de color de cada textura en el terreno: las de Namaqualand son algo más rojizas que el desierto
        /// de Atacama y el posproceso cálido las acerca al rosa (valores de ajuste).
        /// </summary>
        private static readonly Dictionary<string, Color> TerrainTints = new Dictionary<string, Color>
        {
            [ArtLibrary.SandyGravel] = new Color(0.96f, 1.06f, 1.14f),
            [ArtLibrary.CoastSand] = new Color(1.04f, 1.04f, 1.06f),
            [ArtLibrary.TrampledSand] = new Color(0.9f, 0.9f, 0.94f),
            [ArtLibrary.CrackedRock] = new Color(0.78f, 0.8f, 0.84f),
            [ArtLibrary.DryGroundRocks] = new Color(0.94f, 1f, 1.08f),
        };

        private static Color TintOf(string id) => TerrainTints.TryGetValue(id, out Color tint) ? tint : Color.white;

        private static Surface S(string name, string texture, float tile, bool prefix = false) =>
            new Surface { Name = name, Texture = texture, Tile = tile, Prefix = prefix };

        // Elección de texturas por capítulo (valores de ajuste comprobados con AtmospherePreview).
        private static readonly Dictionary<string, Ground> Grounds = new Dictionary<string, Ground>
        {
            // Playa y riscos de la caleta.
            ["Capitulo3_Bahia_de_Pisagua"] = new Ground
            {
                Terrain = new TerrainMix { Base = ArtLibrary.CoastSand, Patch = ArtLibrary.SandyGravel, Steep = ArtLibrary.CrackedRock },
            },
            // Fondo de quebrada pedregoso, laderas de roca y la pampa de grava arriba.
            ["Capitulo4_Quebrada_de_Tarapaca"] = new Ground
            {
                Surfaces = new[]
                {
                    S("Fondo_Quebrada", ArtLibrary.DryGroundRocks, 3.5f), S("Ladera_", ArtLibrary.CrackedRock, 10f, prefix: true),
                    S("Pampa_", ArtLibrary.SandyGravel, 7f, prefix: true), S("Cauce_Seco", ArtLibrary.TrampledSand, 3f),
                },
            },
            // Meseta de arena y grava del Intiorko.
            ["Capitulo5_Alto_de_la_Alianza"] = new Ground
            {
                Terrain = new TerrainMix { Base = ArtLibrary.SandyGravel, Patch = ArtLibrary.TrampledSand, Steep = ArtLibrary.CrackedRock, PatchShare = 0.45f },
            },
            // El morro: roca en el macizo, grava y tierra pedregosa en la ladera y el llano de los fuertes (que solo se ve
            // de lejos y casi de canto: textura más grande para que no se formen franjas).
            ["Capitulo6_Morro_de_Arica"] = new Ground
            {
                Surfaces = new[]
                {
                    S("Macizo_del_Morro", ArtLibrary.CrackedRock, 12f), S("Ladera_Este", ArtLibrary.DryGroundRocks, 4f),
                    S("Llano_de_los_Fuertes", ArtLibrary.SandyGravel, 10f),
                },
            },
            // Tierra de cultivo del valle y los bordos de las acequias.
            ["Capitulo7_Canaverales_de_Canete"] = new Ground
            {
                Surfaces = new[] { S("Suelo", ArtLibrary.FarmSoil, 3f), S("Bordo_", ArtLibrary.FarmSoil, 2f, prefix: true) },
            },
            // Chacras secas y reductos de tierra apisonada.
            ["Capitulo8_Reductos_de_Miraflores"] = new Ground
            {
                Surfaces = new[] { S("Suelo", ArtLibrary.DryMud, 3.5f), S("Reducto_", ArtLibrary.DryGroundRocks, 3f, prefix: true) },
            },
        };

        /// <summary>Modo batch: <c>-executeMethod Pacifico.EditorTools.GroundDressing.ApplyAllBatch</c>.</summary>
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
            foreach (KeyValuePair<string, Ground> entry in Grounds)
            {
                string path = GameBuilder.ScenePath(entry.Key);
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int dressed = 0;
                if (entry.Value.Terrain != null)
                {
                    foreach (Terrain terrain in All<Terrain>())
                    {
                        if (Paint(terrain, entry.Value.Terrain)) dressed++;
                    }
                }
                foreach (Surface surface in entry.Value.Surfaces)
                {
                    foreach (MeshRenderer renderer in All<MeshRenderer>())
                    {
                        string name = renderer.gameObject.name;
                        bool match = surface.Prefix ? name.StartsWith(surface.Name, System.StringComparison.Ordinal) : name == surface.Name;
                        if (match && ArtLibrary.Dress(renderer.gameObject, surface.Texture, surface.Tile, Color.white)) dressed++;
                    }
                }
                Debug.Log("[Pacífico] Suelos de " + entry.Key + ": " + dressed);
                if (dressed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Tres capas (llano, manchas y roca) pintadas según la pendiente y un ruido de dos escalas.</summary>
        private static bool Paint(Terrain terrain, TerrainMix mix)
        {
            TerrainLayer baseLayer = ArtLibrary.Layer(mix.Base, mix.BaseTile, TintOf(mix.Base));
            TerrainLayer patchLayer = ArtLibrary.Layer(mix.Patch, mix.PatchTile, TintOf(mix.Patch));
            TerrainLayer steepLayer = ArtLibrary.Layer(mix.Steep, mix.SteepTile, TintOf(mix.Steep));
            TerrainData data = terrain.terrainData;
            if (baseLayer == null || patchLayer == null || steepLayer == null || data == null) return false;

            data.terrainLayers = new[] { baseLayer, patchLayer, steepLayer };
            data.alphamapResolution = 512;
            int r = data.alphamapResolution;
            Vector3 size = data.size;
            float threshold = 1f - mix.PatchShare;
            var maps = new float[r, r, 3];
            for (int y = 0; y < r; y++)
            {
                for (int x = 0; x < r; x++)
                {
                    float u = x / (r - 1f), v = y / (r - 1f);
                    float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mix.RockFrom, mix.RockTo, data.GetSteepness(u, v)));
                    float noise = 0.7f * Mathf.PerlinNoise(u * size.x / 40f + 5.3f, v * size.z / 40f + 1.7f)
                                + 0.3f * Mathf.PerlinNoise(u * size.x / 9f + 11.1f, v * size.z / 9f + 3.9f);
                    float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.2f, threshold + 0.2f, noise)) * (1f - rock);
                    maps[y, x, 0] = 1f - rock - patch;
                    maps[y, x, 1] = patch;
                    maps[y, x, 2] = rock;
                }
            }
            data.SetAlphamaps(0, 0, maps);
            terrain.basemapDistance = 400f;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            return true;
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
