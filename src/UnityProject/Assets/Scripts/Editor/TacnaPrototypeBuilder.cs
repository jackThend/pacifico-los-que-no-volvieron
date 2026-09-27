using System;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Construye la escena de prototipo RTS «Tacna» (ROADMAP 4): la meseta del Intiorko con dunas, la zanja y los
    /// parapetos de la línea aliada, tres escuadras del Batallón Colorados de Bolivia (el jugador, con Remington) y
    /// tres escuadras chilenas (Comblain) al pie de la meseta. El NavMesh se genera al cargar la escena.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.TacnaPrototypeBuilder.RunBatch</c>
    /// </summary>
    public static class TacnaPrototypeBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Proto_Tacna_RTS.unity";
        private const string TerrainFolder = ProjectPaths.Materials + "/Terreno_Tacna";

        /// <summary>Campo de 500 × 500 m y 40 m de desnivel máximo.</summary>
        private static readonly Vector3 TerrainSize = new Vector3(500f, 40f, 500f);
        private const int HeightmapResolution = 513;

        private static readonly Color SandColor = new Color(0.8f, 0.7f, 0.52f);
        private static readonly Color SackColor = new Color(0.62f, 0.56f, 0.42f);
        private static readonly Color ColoradoRed = new Color(0.62f, 0.1f, 0.09f);
        private static readonly Color ColoradoTrim = new Color(0.9f, 0.88f, 0.8f);
        private static readonly Color ChileBlue = new Color(0.14f, 0.17f, 0.32f);
        private static readonly Color ChileTrim = new Color(0.58f, 0.12f, 0.1f);

        [MenuItem("Pacífico/Prototipos/Construir escena RTS de Tacna")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nClic o recuadro: seleccionar escuadras · clic derecho: mover (arrastrar: frente) · clic derecho sobre el " +
                "enemigo: atacar · 1 línea · 2 guerrilla · H alto · WASD, Q/E y rueda: cámara", "Aceptar");
        }

        public static void RunBatch()
        {
            try
            {
                BuildScene();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO remington = LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO comblain = LoadWeapon(WeaponCatalog.ComblainId);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment();
            Terrain terrain = CreateTerrain();
            CreateDefences(terrain);

            new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();

            var smoke = new GameObject("Humo_Polvora_Negra");
            smoke.AddComponent<ParticleSystem>();
            BlackPowderSmoke smokeComponent = smoke.AddComponent<BlackPowderSmoke>();
            // Sin viento: la camanchaca de la mañana del 26 de mayo de 1880.
            var smokeSerialized = new SerializedObject(smokeComponent);
            smokeSerialized.FindProperty("maxPuffs").intValue = 160;
            smokeSerialized.FindProperty("muzzleFlash").boolValue = false;
            smokeSerialized.ApplyModifiedPropertiesWithoutUndo();

            Camera camera = CreateCamera();
            new GameObject("Mando_del_Jugador").AddComponent<RtsCommander>().ViewCamera = camera;

            // Aliados: el Batallón Colorados en la meseta, detrás de la zanja, mirando al sur.
            Material red = PrototypeSceneKit.Material("Uniforme_Colorados", ColoradoRed);
            Material white = PrototypeSceneKit.Material("Correaje_Colorados", ColoradoTrim);
            Squad(terrain, "Colorados · 1.ª Compañía", Faction.Bolivia, remington, 12, FormationType.Line, true, new Vector3(-40f, 0f, 140f), 180f, red, white);
            Squad(terrain, "Colorados · 2.ª Compañía", Faction.Bolivia, remington, 12, FormationType.Line, true, new Vector3(0f, 0f, 140f), 180f, red, white);
            Squad(terrain, "Colorados · Cazadores", Faction.Bolivia, remington, 8, FormationType.Skirmish, true, new Vector3(55f, 0f, 145f), 180f, red, white);

            // Chilenos: al pie de la meseta, mirando al norte, a ~355 m (fuera del alcance eficaz de ambos bandos).
            Material blue = PrototypeSceneKit.Material("Uniforme_Chile", ChileBlue);
            Material kepi = PrototypeSceneKit.Material("Quepis_Chile", ChileTrim);
            Squad(terrain, "2.º de Línea · 1.ª Compañía", Faction.Chile, comblain, 12, FormationType.Line, false, new Vector3(-30f, 0f, -215f), 0f, blue, kepi);
            Squad(terrain, "2.º de Línea · 2.ª Compañía", Faction.Chile, comblain, 10, FormationType.Skirmish, false, new Vector3(35f, 0f, -215f), 0f, blue, kepi);
            Squad(terrain, "Atacama · Guerrilla", Faction.Chile, comblain, 12, FormationType.Skirmish, false, new Vector3(95f, 0f, -212f), 0f, blue, kepi);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Escena RTS de prototipo guardada en " + ScenePath);
        }

        private static WeaponDataSO LoadWeapon(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + id + ".asset");
            if (asset == null) throw new InvalidOperationException("No se generó el WeaponDataSO de " + id + ".");
            return asset;
        }

        // ------------------------------------------------------------------------------------------
        // Entorno y terreno
        // ------------------------------------------------------------------------------------------

        private static void CreateEnvironment()
        {
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 30f, 0f);

            // Camanchaca: la neblina densa de la mañana de la batalla.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.8f, 0.79f, 0.76f);
        }

        /// <summary>
        /// Terreno de dunas con la meseta al norte: altura base, subida de 14 m entre z = 20 y z = 90, dunas de dos
        /// escalas (ruido de Perlin determinista) y la zanja aliada al borde de la meseta.
        /// </summary>
        private static Terrain CreateTerrain()
        {
            HistoricalDataAssetGenerator.EnsureFolder(TerrainFolder);
            var data = new TerrainData { heightmapResolution = HeightmapResolution };
            data.size = TerrainSize;
            var heights = new float[HeightmapResolution, HeightmapResolution];
            for (int iz = 0; iz < HeightmapResolution; iz++)
            {
                for (int ix = 0; ix < HeightmapResolution; ix++)
                {
                    float x = ix / (float)(HeightmapResolution - 1) * TerrainSize.x - TerrainSize.x * 0.5f;
                    float z = iz / (float)(HeightmapResolution - 1) * TerrainSize.z - TerrainSize.z * 0.5f;
                    heights[iz, ix] = HeightAt(x, z) / TerrainSize.y;
                }
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { SandLayer() };
            AssetDatabase.CreateAsset(data, TerrainFolder + "/Tacna_TerrainData.asset");

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Meseta_del_Intiorko";
            go.transform.position = new Vector3(-TerrainSize.x * 0.5f, 0f, -TerrainSize.z * 0.5f);
            return go.GetComponent<Terrain>();
        }

        /// <summary>Altura del terreno en metros (coordenadas del mundo, centradas en el origen).</summary>
        public static float HeightAt(float x, float z)
        {
            float plateau = 14f * MathUtil.SmoothStep(20f, 90f, z);
            float dunes = 3.2f * Mathf.PerlinNoise(x / 70f + 13.1f, z / 70f + 7.7f) + 1.1f * Mathf.PerlinNoise(x / 19f + 3.3f, z / 19f + 41.9f);
            // En la meseta el suelo es más llano (pampa).
            dunes *= Mathf.Lerp(1f, 0.35f, MathUtil.SmoothStep(60f, 100f, z));
            float trench = TrenchDepth(x, z);
            return 2f + plateau + dunes - trench;
        }

        /// <summary>Zanja de la línea aliada: 1,3 m de hondo y ~3 m de ancho en z ≈ 92, de x = −90 a x = 90.</summary>
        private static float TrenchDepth(float x, float z)
        {
            float across = 1f - MathUtil.SmoothStep(0.6f, 1.6f, Mathf.Abs(z - 92f));
            float along = 1f - MathUtil.SmoothStep(86f, 92f, Mathf.Abs(x));
            return 1.3f * across * along;
        }

        private static TerrainLayer SandLayer()
        {
            string texturePath = TerrainFolder + "/Arena_Tacna.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "Arena_Tacna", wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        float n = Mathf.PerlinNoise(x * 0.21f, y * 0.21f) * 0.6f + Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.4f;
                        texture.SetPixel(x, y, SandColor * (0.85f + 0.25f * n));
                    }
                }
                texture.Apply(true);
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            string layerPath = TerrainFolder + "/Arena_Tacna.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer != null) return layer;
            layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(8f, 8f) };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        /// <summary>Parapetos de sacos delante de la zanja, con huecos para salir al contraataque.</summary>
        private static void CreateDefences(Terrain terrain)
        {
            var root = new GameObject("Linea_Aliada").transform;
            Material sacks = PrototypeSceneKit.Material("Sacos", SackColor);
            for (int i = -4; i <= 4; i++)
            {
                if (i == 0) continue; // hueco central
                float x = i * 20f;
                Vector3 p = new Vector3(x, 0f, 88.5f);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.5f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Parapeto_" + (i + 5), root, p, new Vector3(14f, 1.1f, 1.2f), sacks);
            }
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Camara_Tactica");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 1500f;
            camera.fieldOfView = 50f;
            go.AddComponent<AudioListener>();
            // Detrás de la línea aliada, mirando al sur (hacia el enemigo).
            go.transform.SetPositionAndRotation(new Vector3(0f, 70f, 215f), Quaternion.Euler(45f, 180f, 0f));
            go.AddComponent<RtsCameraController>();
            return camera;
        }

        private static void Squad(Terrain terrain, string name, Faction faction, WeaponDataSO weapon, int count, FormationType formation,
                                  bool player, Vector3 position, float headingDeg, Material uniform, Material trim)
        {
            var go = new GameObject("Escuadra_" + name);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, headingDeg, 0f));
            go.AddComponent<SquadController>().Configure(name, faction, weapon, count, formation, player, uniform, trim);
        }
    }
}
