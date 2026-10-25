using System;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Narrative;
using Pacifico.Tactics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using A = Pacifico.EditorTools.Capitulo5AltoDeLaAlianzaBuilder;
using T = Pacifico.EditorTools.TacnaPrototypeBuilder;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 3, «Sangre en el salitre» (ROADMAP 6.8): la bahía de Pisagua con la playa, la ladera
    /// de arena, el terraplén del ferrocarril salitrero con sus nidos de fusileros, la pampa con la estación, los
    /// estanques de agua y las locomotoras, los fuertes de Punta Pichalo y Punta Guacas en las puntas, la flota y los
    /// transportes fondeados y cuatro botes de desembarco. El relieve es <see cref="PisaguaTerrain"/>; la misión la dirige
    /// <see cref="PisaguaMissionDirector"/>. Greyboxing con primitivas (AGENTS.md §4.A). Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo3PisaguaBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo3PisaguaBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo3_Bahia_de_Pisagua.unity";
        internal const string PlateImage = "Archivo_Historico/03_Lugares_y_Campos_de_Batalla/02_Desembarco_y_Toma_de_Pisagua_Grabado.jpg";
        internal const string PlateCaption = "Desembarco y toma de Pisagua, 2 de noviembre de 1879. Grabado de época. Archivo Histórico.";

        private const string TerrainAsset = ProjectPaths.Materials + "/Terreno_Tacna/Pisagua_TerrainData.asset";
        private static readonly Vector3 TerrainSize = new Vector3(600f, 60f, 600f);
        private const float TerrainBase = -8f;
        private const int HeightmapResolution = 513;

        private static readonly Color SeaColor = new Color(0.16f, 0.3f, 0.36f);
        private static readonly Color StoneColor = new Color(0.5f, 0.45f, 0.38f);
        private static readonly Color WoodColor = new Color(0.38f, 0.26f, 0.15f);
        private static readonly Color IronColor = new Color(0.13f, 0.13f, 0.14f);
        private static readonly Color AlliedCoat = new Color(0.3f, 0.3f, 0.34f);

        [MenuItem("Pacífico/Capítulos/Capítulo 3: Bahía de Pisagua")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nB y clic (o los botones de los fuertes): blanco de la flota." +
                "\nClic o recuadro: seleccionar · clic derecho: mover o atacar · 1/2: línea o guerrilla · H: alto." +
                "\nWASD, Q/E y rueda: cámara.", "Aceptar");
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

        internal static void BuildScene()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            WeaponDataSO remington = T.LoadWeapon(WeaponCatalog.RemingtonId);
            WeaponDataSO comblain = T.LoadWeapon(WeaponCatalog.ComblainId);
            string dispatchPath = ProjectPaths.CollectibleData + "/Collectible_" + PisaguaChapter.RewardId + ".asset";
            var dispatch = AssetDatabase.LoadAssetAtPath<CollectibleDataSO>(dispatchPath);
            if (dispatch == null) throw new InvalidOperationException("No se generó el coleccionable del despacho de Pisagua (" + dispatchPath + ").");

            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            string guion = File.ReadAllText(Path.Combine(root, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, PisaguaChapter.ChapterHeading);
            PisaguaChapter.Build(GuionQuotes.Extract(chapter, PisaguaChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment();
            Terrain terrain = CreateTerrain();
            var baker = new GameObject("NavMesh_Tiempo_de_Ejecucion").AddComponent<NavMeshRuntimeBaker>();
            baker.transform.position = new Vector3(0f, 20f, 0f);
            var bakerSo = new SerializedObject(baker);
            bakerSo.FindProperty("size").vector3Value = new Vector3(600f, 140f, 600f);
            bakerSo.ApplyModifiedPropertiesWithoutUndo();
            A.CreateSmoke();

            Material stone = PrototypeSceneKit.Material("Piedra_Pisagua", StoneColor);
            Material wood = PrototypeSceneKit.Material("Madera_Pisagua", WoodColor);
            Material iron = PrototypeSceneKit.Material("Hierro_Pisagua", IronColor);
            Material dust = PrototypeSceneKit.Material("Polvo", new Color(0.8f, 0.72f, 0.58f));
            Material smoke = PrototypeSceneKit.Material("Humo_Explosion", new Color(0.18f, 0.16f, 0.15f));
            Material water = PrototypeSceneKit.Material("Espuma", new Color(0.85f, 0.88f, 0.9f));

            Transform covers = new GameObject("Puestos_a_Cubierto").transform;
            CreateBeachAndSlope(terrain, covers, stone);
            Transform embankment = CreateEmbankment(terrain, covers, stone, wood, iron);
            CreatePlateau(terrain, covers, stone, wood, iron, dust, smoke, out DemolitionObjective tanks, out DemolitionObjective engines, out Transform rally);

            CoastalFortBattery[] forts =
            {
                Fort(terrain, PisaguaChapter.Pichalo, new Vector3(-PisaguaTerrain.HeadlandX, 0f, -60f), dust, smoke, stone),
                Fort(terrain, PisaguaChapter.Guacas, new Vector3(PisaguaTerrain.HeadlandX, 0f, -60f), dust, smoke, stone),
            };
            Transform[] ships =
            {
                Ship("Cochrane", new Vector3(-120f, 0f, -560f), 90f, 64f, iron, wood),
                Ship("OHiggins", new Vector3(120f, 0f, -540f), 90f, 60f, wood, wood),
            };
            Ship("Transporte_1", new Vector3(-40f, 0f, -420f), 80f, 70f, iron, wood);
            Ship("Transporte_2", new Vector3(60f, 0f, -440f), 100f, 70f, iron, wood);
            var support = new GameObject("Artilleria_de_la_Flota").AddComponent<NavalGunfireSupport>();
            support.Configure(forts, ships, water, smoke);

            LandingCraft[] boats =
            {
                Boat("Atacama · 1.ª Compañía", new Vector3(-40f, 0f, -300f), wood),
                Boat("Zapadores · 1.ª Compañía", new Vector3(40f, 0f, -300f), wood),
                Boat("Atacama · 2.ª Compañía", new Vector3(-15f, 0f, -305f), wood),
                Boat("Zapadores · 2.ª Compañía", new Vector3(15f, 0f, -305f), wood),
            };

            Material allied = PrototypeSceneKit.Material("Uniforme_Aliado_Pisagua", AlliedCoat);
            Material trim = PrototypeSceneKit.Material("Correaje_Colorados", T.ColoradoTrim);
            SquadController[] nests =
            {
                T.Squad(terrain, "Fusileros bolivianos · terraplén", Faction.Bolivia, remington, PisaguaChapter.MenPerNest, FormationType.Line, false,
                        new Vector3(-30f, 0f, PisaguaTerrain.EmbankmentZ - 1f), 180f, allied, trim),
                T.Squad(terrain, "Fusileros peruanos · terraplén", Faction.Peru, remington, PisaguaChapter.MenPerNest, FormationType.Line, false,
                        new Vector3(30f, 0f, PisaguaTerrain.EmbankmentZ - 1f), 180f, allied, trim),
            };
            // Mantienen su puesto (la IA de asalto de Tacna no va con ellos): responden al fuego desde el terraplén.
            foreach (SquadController nest in nests) UnityEngine.Object.DestroyImmediate(nest.GetComponent<SquadAI>());
            SquadController guard = T.Squad(terrain, "Guardia de la estación", Faction.Peru, remington, 6, FormationType.Skirmish, false,
                                            new Vector3(45f, 0f, 178f), 180f, allied, trim);
            UnityEngine.Object.DestroyImmediate(guard.GetComponent<SquadAI>());
            Transform[] counter =
            {
                A.Marker(terrain, "Contraataque_1", new Vector3(-50f, 0f, 275f), 180f),
                A.Marker(terrain, "Contraataque_2", new Vector3(50f, 0f, 280f), 180f),
            };

            Camera camera = CreateCamera();
            var commander = new GameObject("Mando_del_Jugador").AddComponent<RtsCommander>();
            commander.ViewCamera = camera;
            var viewer = new GameObject("Visor_de_Documentos").AddComponent<DocumentViewer>();
            Texture2D plate = HistoricalDataAssetGenerator.ImportArchiveTexture(root, PlateImage);

            var chile = new PisaguaMissionDirector.Uniform
            {
                coat = PrototypeSceneKit.Material("Uniforme_Chile", T.ChileBlue),
                trim = PrototypeSceneKit.Material("Quepis_Chile", T.ChileTrim),
            };
            chile.model = SoldierModelLibrary.Prefab(Faction.Chile);
            var alliedUniform = new PisaguaMissionDirector.Uniform { coat = allied, trim = trim, model = SoldierModelLibrary.Prefab(Faction.Peru) };
            var director = new GameObject("Director_Capitulo3").AddComponent<PisaguaMissionDirector>();
            director.Configure(chapter, boats, forts, support, comblain, chile, nests, embankment, rally, counter, remington, alliedUniform,
                               tanks, engines, commander, plate, PlateCaption, viewer, dispatch);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 3 guardado en " + ScenePath);
        }

        private static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        private static Vector3 OnGround(Terrain terrain, float x, float z, float lift = 0f)
        {
            var p = new Vector3(x, 0f, z);
            p.y = Ground(terrain, p) + lift;
            return p;
        }

        // ------------------------------------------------------------------------------------------
        // Entorno y terreno
        // ------------------------------------------------------------------------------------------

        private static void CreateEnvironment()
        {
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 20f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0012f;
            RenderSettings.fogColor = new Color(0.82f, 0.8f, 0.76f);

            GameObject sea = PrototypeSceneKit.Primitive(PrimitiveType.Plane, "Mar", null, new Vector3(0f, PisaguaTerrain.SeaLevel, -200f), new Vector3(220f, 1f, 220f),
                PrototypeSceneKit.Material("Mar_Pisagua", SeaColor, 0.8f));
            UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>()); // ni NavMesh ni raycasts sobre el agua
        }

        /// <summary>Terreno de <see cref="PisaguaTerrain"/>: 600 × 600 m, de −8 m (fondo) a +52 m.</summary>
        private static Terrain CreateTerrain()
        {
            var data = new TerrainData { heightmapResolution = HeightmapResolution };
            data.size = TerrainSize;
            var heights = new float[HeightmapResolution, HeightmapResolution];
            for (int iz = 0; iz < HeightmapResolution; iz++)
            {
                for (int ix = 0; ix < HeightmapResolution; ix++)
                {
                    float x = ix / (float)(HeightmapResolution - 1) * TerrainSize.x - TerrainSize.x * 0.5f;
                    float z = iz / (float)(HeightmapResolution - 1) * TerrainSize.z - TerrainSize.z * 0.5f;
                    heights[iz, ix] = Mathf.Clamp01((PisaguaTerrain.HeightAt(x, z) - TerrainBase) / TerrainSize.y);
                }
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { T.SandLayer() };
            AssetDatabase.DeleteAsset(TerrainAsset);
            AssetDatabase.CreateAsset(data, TerrainAsset);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Bahia_de_Pisagua";
            go.transform.position = new Vector3(-TerrainSize.x * 0.5f, TerrainBase, -TerrainSize.z * 0.5f);
            return go.GetComponent<Terrain>();
        }

        private static void Cover(Terrain terrain, Transform parent, Vector3 position, float headingDeg, float protection)
        {
            var go = new GameObject("Puesto");
            go.transform.SetParent(parent, false);
            position.y = Ground(terrain, position);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, headingDeg, 0f));
            go.AddComponent<CoverPoint>().Protection = protection;
        }

        /// <summary>Rocas al pie de la playa y en la ladera: el único abrigo de los que desembarcan (protegen hacia el norte).</summary>
        private static void CreateBeachAndSlope(Terrain terrain, Transform covers, Material stone)
        {
            var root = new GameObject("Rocas_de_la_Playa").transform;
            float[] beachX = { -70f, -35f, 0f, 35f, 70f };
            foreach (float x in beachX)
            {
                Vector3 c = OnGround(terrain, x, 3f, 0.6f);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Roca", root, c, new Vector3(7f, 1.6f, 2.4f), stone);
                for (float dx = -3f; dx <= 3f; dx += 1f) Cover(terrain, covers, new Vector3(x + dx, 0f, 1.2f), 0f, 0.55f);
            }
            float[] slopeX = { -55f, -20f, 20f, 55f };
            for (int i = 0; i < slopeX.Length; i++)
            {
                float z = 26f + (i % 2) * 8f;
                Vector3 c = OnGround(terrain, slopeX[i], z, 0.4f);
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Pliegue_de_Duna", root, c, new Vector3(5f, 1f, 1.6f), stone);
                for (float dx = -2f; dx <= 2f; dx += 1f) Cover(terrain, covers, new Vector3(slopeX[i] + dx, 0f, z - 1.4f), 0f, 0.35f);
            }
        }

        /// <summary>El terraplén del ferrocarril: parapetos de piedra hacia el mar, puestos detrás y las vías.</summary>
        private static Transform CreateEmbankment(Terrain terrain, Transform covers, Material stone, Material wood, Material iron)
        {
            var root = new GameObject("Terraplen_del_Ferrocarril").transform;
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue; // paso del camino de herradura
                float x = i * 22f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Parapeto_de_Piedra", root, OnGround(terrain, x, PisaguaTerrain.EmbankmentZ - 4f, 0.5f), new Vector3(16f, 1.1f, 1.2f), stone);
                for (float dx = -7.5f; dx <= 7.5f; dx += 1f) Cover(terrain, covers, new Vector3(x + dx, 0f, PisaguaTerrain.EmbankmentZ - 2.8f), 180f, PisaguaChapter.NestCover);
            }
            for (int r = 0; r < 2; r++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Riel", root, new Vector3(0f, PisaguaTerrain.EmbankmentHeight + 0.1f, PisaguaTerrain.EmbankmentZ + 2f + r * 1.4f),
                    new Vector3(560f, 0.15f, 0.12f), iron, keepCollider: false);
            }
            var centre = new GameObject("Centro_del_Terraplen").transform;
            centre.position = OnGround(terrain, 0f, PisaguaTerrain.EmbankmentZ);
            return centre;
        }

        /// <summary>La pampa: la estación, los estanques de agua dulce y las locomotoras, con parapetos hacia el interior.</summary>
        private static void CreatePlateau(Terrain terrain, Transform covers, Material stone, Material wood, Material iron, Material dust, Material smoke,
                                          out DemolitionObjective tanks, out DemolitionObjective engines, out Transform rally)
        {
            var root = new GameObject("Estacion_de_Pisagua").transform;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Estacion", root, OnGround(terrain, 0f, 168f, 4f), new Vector3(22f, 8f, 10f), wood);

            var tanksRoot = new GameObject("Estanques_de_Agua_Dulce").transform;
            tanksRoot.position = OnGround(terrain, -45f, 150f);
            var tanksVisual = new GameObject("Estanques_Visual").transform;
            tanksVisual.SetParent(tanksRoot, false);
            for (int i = 0; i < 3; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Estanque_" + (i + 1), tanksVisual, new Vector3(-9f + i * 9f, 3f, 0f), new Vector3(7f, 3f, 7f), iron);
            }
            tanks = tanksRoot.gameObject.AddComponent<DemolitionObjective>();
            tanks.Configure("Estanques de agua dulce", tanksVisual, smoke);

            var enginesRoot = new GameObject("Locomotoras").transform;
            enginesRoot.position = OnGround(terrain, 45f, 160f);
            var enginesVisual = new GameObject("Locomotoras_Visual").transform;
            enginesVisual.SetParent(enginesRoot, false);
            for (int i = 0; i < 2; i++)
            {
                GameObject body = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Locomotora_" + (i + 1), enginesVisual, new Vector3(-4f + i * 8f, 2f, 0f), new Vector3(3f, 3.4f, 10f), iron);
                GameObject stack = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Chimenea", enginesVisual, new Vector3(-4f + i * 8f, 4.5f, 3.5f), new Vector3(0.8f, 1.2f, 0.8f), iron, keepCollider: false);
                // Modelo CC-BY: locomotora 0-4-0 de 1840, de 10 m como el bloque. Al volarla, DemolitionObjective aplasta
                // y ennegrece todo Locomotoras_Visual, también el modelo.
                if (PropModelLibrary.Place(PropModelLibrary.Locomotive, "Modelo_Locomotora_" + (i + 1), enginesVisual,
                        new Vector3(-4f + i * 8f, 0.3f, 0f), 3.4f, 0f, lengthAlongZ: true) != null)
                {
                    PropModelLibrary.Hide(body, stack);
                }
            }
            engines = enginesRoot.gameObject.AddComponent<DemolitionObjective>();
            engines.Configure("Locomotoras", enginesVisual, smoke);

            // Parapetos de sacos al norte de los objetivos: para defenderlos del contraataque que llega del interior.
            float[] wallX = { -60f, -30f, 30f, 60f };
            foreach (float x in wallX)
            {
                GameObject parapet = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Parapeto", root, OnGround(terrain, x, 186f, 0.5f), new Vector3(14f, 1.1f, 1.2f), stone);
                PropModelLibrary.Wall(PropModelLibrary.SandbagWall, parapet); // modelo CC-BY de sacos de arpillera
                for (float dx = -6.5f; dx <= 6.5f; dx += 1f) Cover(terrain, covers, new Vector3(x + dx, 0f, 184.8f), 0f, 0.6f);
            }

            rally = new GameObject("Punto_de_Repliegue").transform;
            rally.SetPositionAndRotation(OnGround(terrain, 0f, 178f), Quaternion.identity); // hacia el interior: +z
        }

        // ------------------------------------------------------------------------------------------
        // Fuertes, flota y botes
        // ------------------------------------------------------------------------------------------

        private static CoastalFortBattery Fort(Terrain terrain, string name, Vector3 position, Material dust, Material smoke, Material stone)
        {
            position = OnGround(terrain, position.x, position.z);
            var root = new GameObject("Fuerte_" + name.Replace(' ', '_')).transform;
            root.position = position;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Terraplen_del_Fuerte", root, new Vector3(0f, 0.8f, -6f), new Vector3(24f, 1.6f, 2f), stone);
            SquadArtillery guns = A.Gun(terrain, "Piezas_" + name.Replace(' ', '_'), Faction.Peru, position + new Vector3(0f, 0f, -2f), 180f, dust);
            guns.transform.SetParent(root, true);
            var fort = root.gameObject.AddComponent<CoastalFortBattery>();
            fort.Configure(name, guns, smoke);
            return fort;
        }

        internal static Transform Ship(string name, Vector3 position, float heading, float length, Material hull, Material masts)
        {
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Casco", root, new Vector3(0f, 1.5f, 0f), new Vector3(12f, 5f, length), hull, keepCollider: false);
            for (int i = -1; i <= 1; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Palo", root, new Vector3(0f, 15f, i * length * 0.3f), new Vector3(0.6f, 13f, 0.6f), masts, keepCollider: false);
            }

            // Modelos CC-BY (tarea 7.2): el Cochrane como el Odin de reducto central y la corbeta O'Higgins con la
            // fragata de la Esmeralda. Los transportes siguen siendo de primitivas.
            bool dressed = name == "Cochrane"
                ? ShipModelLibrary.Place(ShipModelLibrary.Odin, "Modelo_Odin", root, length, ShipModelLibrary.OdinAsCochrane) != null
                : name == "OHiggins" &&
                  ShipModelLibrary.Place(ShipModelLibrary.Constitution, "Modelo_Fragata", root, length, IquiquePrototypeBuilder.EsmeraldaModelFit) != null;
            if (dressed) ShipModelLibrary.HideStandIns(root, "Casco", "Palo");
            return root;
        }

        internal static LandingCraft Boat(string squadName, Vector3 start, Material wood)
        {
            var root = new GameObject("Bote_" + squadName.Replace(' ', '_').Replace("·", string.Empty)).transform;
            root.position = new Vector3(start.x, 0.3f, start.z);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Casco", root, Vector3.zero, new Vector3(2.6f, 0.8f, 9f), wood, keepCollider: false);
            // Modelo CC-BY (tarea 7.2): bote de remos de 7,5 m; las cabezas de la tropa asoman sobre la regala.
            if (ShipModelLibrary.Place(ShipModelLibrary.Boat, "Modelo_Bote", root, 7.5f, ShipModelLibrary.RowBoat) != null)
            {
                ShipModelLibrary.HideStandIns(root, "Casco");
            }
            Material blue = PrototypeSceneKit.Material("Uniforme_Chile", T.ChileBlue);
            var heads = new Transform[PisaguaChapter.MenPerBoat];
            for (int i = 0; i < heads.Length; i++)
            {
                Vector3 local = new Vector3(i % 2 == 0 ? -0.6f : 0.6f, 0.75f, -3.6f + (i / 2) * 1.3f);
                heads[i] = PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Soldado_" + (i + 1), root, local, Vector3.one * 0.55f, blue, keepCollider: false).transform;
            }
            var boat = root.gameObject.AddComponent<LandingCraft>();
            boat.Configure(squadName, PisaguaChapter.MenPerBoat, new Vector3(start.x * 0.8f, 0f, -8f), heads);
            return boat;
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Camara_Tactica");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 2000f;
            camera.fieldOfView = 50f;
            go.AddComponent<AudioListener>();
            // Desde el mar, mirando a la playa y al risco.
            go.transform.SetPositionAndRotation(new Vector3(0f, 80f, -190f), Quaternion.Euler(42f, 0f, 0f));
            var rig = go.AddComponent<RtsCameraController>();
            var so = new SerializedObject(rig);
            so.FindProperty("boundsMin").vector2Value = new Vector2(-280f, -320f);
            so.FindProperty("boundsMax").vector2Value = new Vector2(280f, 290f);
            so.FindProperty("maxHeight").floatValue = 170f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return camera;
        }
    }
}
