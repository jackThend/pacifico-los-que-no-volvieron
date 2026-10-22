using System;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Narrative;
using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Narrative;
using Pacifico.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using B = Pacifico.EditorTools.IquiquePrototypeBuilder;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 1, «Madera y blindaje» (ROADMAP 6.1): la rada de Iquique el 21 de mayo de 1879 con
    /// la Esmeralda frente a la costa y sus baterías de tierra, el Huáscar entrando desde el sur, la Independencia y
    /// la Covadonga alejándose hacia Punta Gruesa, los náufragos y la carta de Grau. La misión la dirige
    /// <see cref="IquiqueMissionDirector"/> con el guion de <see cref="IquiqueChapter"/>; las frases se copian de
    /// <c>Historia_Completa_Guion.md</c> al construir la escena. Greyboxing con primitivas (AGENTS.md §4.A).
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo1IquiqueBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo1IquiqueBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo1_Rada_de_Iquique.unity";

        /// <summary>Cara de la costa que mira a la rada (el cubo de <see cref="IquiquePrototypeBuilder"/>) y su altura.</summary>
        private const float CoastFaceX = 1400f;
        private const float CoastTopY = 40f;
        /// <summary>Tramo de costa vestido con rocas, a cada lado de la rada (más allá lo cubre la neblina).</summary>
        private const float CoastDressHalfLengthM = 1300f;
        /// <summary>Las baterías, en lo alto de la costa a pocos metros del borde.</summary>
        private const float BatteryX = CoastFaceX + 8f;
        private static readonly float[] BatteryZ = { 420f, -180f };
        /// <summary>Delante de cada batería las rocas quedan por debajo del borde para que se vean las piezas.</summary>
        private const float BatteryClearanceM = 30f;

        [MenuItem("Pacífico/Capítulos/Capítulo 1: Rada de Iquique")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nActo I (Esmeralda): Espacio dispara la andanada con la cubierta horizontal." +
                "\nActo II (Huáscar): ratón apunta la torre, clic dispara." +
                "\nRescate: W/S máquina, A/D timón; acércate despacio a los náufragos.", "Aceptar");
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
            ShipDataSO LoadShip(string id)
            {
                var data = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + id + ".asset");
                if (data == null) throw new InvalidOperationException("No se generó el ShipDataSO " + id + ".");
                return data;
            }
            string collectiblePath = ProjectPaths.CollectibleData + "/Collectible_" + CollectibleCatalog.GrauLetterId + ".asset";
            var letter = AssetDatabase.LoadAssetAtPath<CollectibleDataSO>(collectiblePath);
            if (letter == null) throw new InvalidOperationException("No se generó el coleccionable de la carta de Grau (" + collectiblePath + ").");

            // La sección del capítulo viaja dentro de la escena: la build no lee el guion del repositorio.
            string guion = File.ReadAllText(Path.Combine(HistoricalDataAssetGenerator.RepositoryRoot, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, IquiqueChapter.ChapterHeading);
            IquiqueChapter.Build(GuionQuotes.Extract(chapter, IquiqueChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavalShell shellPrefab = B.CreateShellPrefab();
            B.CreateEnvironment();
            DressCoast();
            RenderSettings.fogStartDistance = 400f; // «el cielo de Iquique está cubierto por una neblina pálida»
            RenderSettings.fogEndDistance = 3200f;

            // Huáscar: entra desde el sur hacia la rada. Esmeralda: cerca de la costa, bajo las baterías.
            ShipController huascar = B.CreateHuascar(LoadShip(ShipCatalog.HuascarId), shellPrefab, new Vector3(350f, 0f, -550f), 10f);
            ShipController esmeralda = B.CreateEsmeralda(LoadShip(ShipCatalog.EsmeraldaId), shellPrefab, huascar, new Vector3(1050f, 0f, 150f), 200f);
            var gunner = esmeralda.gameObject.AddComponent<PlayerBroadsideGunner>();
            gunner.Target = huascar;

            RamBow ram = huascar.GetComponentInChildren<RamBow>();
            var turret = huascar.GetComponent<ColesTurretController>();
            var huascarAI = huascar.gameObject.AddComponent<RammingShipAI>();
            huascarAI.Configure(esmeralda, ram, turret);

            CreateDistantChase();
            ShoreBattery[] batteries =
            {
                CreateShoreBattery("Bateria_Tierra_Norte", new Vector3(BatteryX, CoastTopY, BatteryZ[0]), esmeralda, shellPrefab),
                CreateShoreBattery("Bateria_Tierra_Sur", new Vector3(BatteryX, CoastTopY, BatteryZ[1]), esmeralda, shellPrefab),
            };
            SurvivorGroup survivors = CreateSurvivorTemplate();

            var cameraObject = new GameObject("Camara_Principal");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 6000f;
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = esmeralda.transform.position + new Vector3(0f, 30f, -90f);
            var rig = cameraObject.AddComponent<ShipCameraRig>();
            rig.Target = esmeralda.transform;

            var hud = new GameObject("HUD_Naval").AddComponent<NavalHud>();
            hud.Ship = esmeralda;
            var viewer = new GameObject("Visor_de_Documentos").AddComponent<DocumentViewer>();

            var director = new GameObject("Director_Capitulo1").AddComponent<IquiqueMissionDirector>();
            director.Configure(chapter, esmeralda, gunner, esmeralda.GetComponent<BroadsideShipAI>(), huascar, huascarAI, turret, ram,
                               batteries, rig, hud, survivors, viewer, letter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 1 guardado en " + ScenePath);
        }

        /// <summary>Batería de campaña en lo alto de la costa: parapeto de sacos y dos piezas.</summary>
        private static ShoreBattery CreateShoreBattery(string name, Vector3 position, ShipController target, NavalShell shellPrefab)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, -90f, 0f)); // mira al oeste, a la rada
            ArtLibrary.Dress(B.Primitive(PrimitiveType.Cube, "Parapeto", root.transform, new Vector3(0f, 0.6f, 3f), new Vector3(14f, 1.2f, 1.5f), B.CoastColor, keepCollider: false),
                             ArtLibrary.GravellySand, 2f, Color.white);
            for (int i = 0; i < 2; i++)
            {
                if (PlaceGun("Pieza_" + (i + 1), root.transform, new Vector3(i == 0 ? -3f : 3f, 0f, 0.6f))) continue;
                GameObject barrel = B.Primitive(PrimitiveType.Cylinder, "Pieza_" + (i + 1), root.transform,
                    new Vector3(i == 0 ? -3f : 3f, 1.2f, 3.2f), new Vector3(0.3f, 1.1f, 0.3f), B.BrassColor, keepCollider: false);
                barrel.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);
            }
            var battery = root.AddComponent<ShoreBattery>();
            battery.Configure(target, shellPrefab);
            return battery;
        }

        /// <summary>
        /// Cañón de avancarga sobre cureña (modelo CC0) detrás del parapeto, con la boca hacia la rada (el +Z de la
        /// batería). False si el modelo no está: la batería conserva sus cilindros.
        /// </summary>
        private static bool PlaceGun(string name, Transform battery, Vector3 localPosition)
        {
            GameObject gun = ArtLibrary.Model(ArtLibrary.Cannon, name, battery);
            if (gun == null) return false;
            ArtLibrary.FitHeight(gun, 1.3f); // la boca asoma sobre el parapeto de 1,2 m
            ArtLibrary.Face(gun, ArtLibrary.MuzzleDirection(gun), battery.forward);
            gun.transform.localPosition = localPosition;
            Bounds bounds = ArtLibrary.WorldBounds(gun);
            gun.transform.position += Vector3.up * (battery.position.y - bounds.min.y);
            return true;
        }

        /// <summary>
        /// La cara de la costa que mira a la rada, vestida con rocas escaneadas de una costa árida (modelos CC0):
        /// acantilados de la altura de la costa en el tramo que se ve desde la rada y peñascos en la línea del agua.
        /// Encima, la arena con grava. Sin los modelos, la costa queda como el bloque del prototipo.
        /// </summary>
        private static void DressCoast()
        {
            GameObject coast = GameObject.Find("Costa_de_Iquique");
            if (coast != null) ArtLibrary.Dress(coast, ArtLibrary.GravellySand, 6f, Color.white);
            // Pared de roca estratificada en la cara que mira a la rada: se ve entre las rocas y bajo las baterías.
            GameObject face = B.Primitive(PrimitiveType.Cube, "Cara_Rocosa", null, new Vector3(CoastFaceX - 0.5f, CoastTopY * 0.5f, 0f),
                                          new Vector3(1f, CoastTopY, 5000f), B.CoastColor, keepCollider: false);
            if (!ArtLibrary.Dress(face, ArtLibrary.CliffSide, 12f, Color.white)) UnityEngine.Object.DestroyImmediate(face);

            var root = new GameObject("Costa_Rocas").transform;
            var random = new System.Random(1879);
            string[] cliffs = { ArtLibrary.AridCliffLarge, ArtLibrary.AridCliffSmall, ArtLibrary.AridCliffLarge, ArtLibrary.Mountainside };
            int count = 0;
            for (float z = -CoastDressHalfLengthM; z < CoastDressHalfLengthM; count++)
            {
                string id = cliffs[count % cliffs.Length];
                GameObject cliff = ArtLibrary.Model(id, "Acantilado_" + (count + 1), root);
                if (cliff == null) break;
                // La silueta de un escaneo queda por debajo de su caja: hace falta bastante más altura que la de la costa
                // para cubrir la cara del bloque (comprobado en la previsualización del navegador).
                float height = CoastTopY + 28f + (float)random.NextDouble() * 16f;
                ArtLibrary.FitHeight(cliff, height);
                ArtLibrary.Face(cliff, ArtLibrary.FacingDirection(cliff, id), Vector3.left);
                cliff.transform.Rotate(0f, ((float)random.NextDouble() - 0.5f) * 24f, 0f, Space.World);
                Bounds bounds = ArtLibrary.WorldBounds(cliff);
                if (NearBattery(z, z + bounds.size.z))
                {
                    ArtLibrary.FitHeight(cliff, CoastTopY);
                    bounds = ArtLibrary.WorldBounds(cliff);
                }
                // Pie bajo el agua, mitad empotrado en la costa y solapado con el anterior para no dejar huecos.
                cliff.transform.position += new Vector3(CoastFaceX - bounds.center.x, -6f - bounds.min.y, z + bounds.extents.z - bounds.center.z);
                z += bounds.size.z * 0.75f;
            }

            for (float z = -CoastDressHalfLengthM; z < CoastDressHalfLengthM; z += 40f + (float)random.NextDouble() * 50f)
            {
                GameObject rocks = ArtLibrary.Model(ArtLibrary.Boulders, "Penascos", root);
                if (rocks == null) break;
                ArtLibrary.FitHeight(rocks, 2.5f + (float)random.NextDouble() * 4f);
                rocks.transform.Rotate(0f, (float)random.NextDouble() * 360f, 0f, Space.World);
                Bounds bounds = ArtLibrary.WorldBounds(rocks);
                float x = CoastFaceX - 14f - (float)random.NextDouble() * 18f;
                rocks.transform.position += new Vector3(x - bounds.center.x, -1.5f - bounds.min.y, z - bounds.center.z);
            }
        }

        private static bool NearBattery(float zFrom, float zTo)
        {
            foreach (float z in BatteryZ)
            {
                if (zTo > z - BatteryClearanceM && zFrom < z + BatteryClearanceM) return true;
            }
            return false;
        }

        /// <summary>
        /// Al sur, la Independencia persigue a la Covadonga hacia Punta Gruesa: solo decorado lejano (siguen su rumbo
        /// sin intervenir en la rada).
        /// </summary>
        private static void CreateDistantChase()
        {
            var covadongaData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.CovadongaId + ".asset");
            var independenciaData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.IndependenciaId + ".asset");
            if (covadongaData != null) DistantShip("Covadonga", covadongaData, new Vector3(-1100f, 0f, -1900f), 188f, B.WoodColor, 2);
            if (independenciaData != null) DistantShip("Independencia", independenciaData, new Vector3(-1000f, 0f, -1300f), 190f, B.IronColor, 3);
        }

        private static void DistantShip(string name, ShipDataSO data, Vector3 position, float heading, Color color, int masts)
        {
            ShipSpec spec = data.ToSpec();
            GameObject root = B.ShipRoot(name, data, position, heading, player: false, EngineOrder.FullAhead, 0.8f);
            root.GetComponent<ShipDamageController>().PlayerControlled = false;
            Transform hull = root.transform.Find("Casco");
            GameObject hullVisual = B.Primitive(PrimitiveType.Cube, "Casco_Visual", hull, Vector3.zero, new Vector3(spec.BeamM, 4.5f, spec.LengthM), color, keepCollider: false);
            if (color == B.IronColor) B.Plated(hullVisual);
            else B.Planked(hullVisual);
            for (int i = 0; i < masts; i++)
            {
                float z = (i - (masts - 1) * 0.5f) * spec.LengthM * 0.28f;
                B.Primitive(PrimitiveType.Cylinder, "Palo_" + (i + 1), hull, new Vector3(0f, 14f, z), new Vector3(0.5f, 12f, 0.5f), B.WoodColor, keepCollider: false);
            }
        }

        /// <summary>Plantilla inactiva de un grupo de náufragos: un madero y cabezas asomando.</summary>
        private static SurvivorGroup CreateSurvivorTemplate()
        {
            var root = new GameObject("Naufragos_Plantilla");
            ArtLibrary.Dress(B.Primitive(PrimitiveType.Cube, "Madero", root.transform, Vector3.zero, new Vector3(3.2f, 0.35f, 0.8f), B.WoodColor, keepCollider: false),
                             ArtLibrary.BrownPlanks, 1.5f, Color.white);
            GameObject crate = ArtLibrary.Model(ArtLibrary.WoodenCrate, "Caja_a_la_Deriva", root.transform);
            if (crate != null)
            {
                ArtLibrary.FitHeight(crate, 0.45f);
                // Media caja hundida junto al madero.
                crate.transform.localPosition = new Vector3(2.6f, -0.25f, 0.9f);
                crate.transform.localRotation = Quaternion.Euler(8f, 35f, -6f);
            }
            Material skin = PrototypeSceneKit.Material("Piel", new Color(0.62f, 0.47f, 0.36f));
            for (int i = 0; i < 3; i++)
            {
                PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Naufrago_" + (i + 1), root.transform,
                    new Vector3(-1.1f + i * 1.1f, 0.35f, i % 2 == 0 ? 0.55f : -0.55f), Vector3.one * 0.32f, skin, keepCollider: false);
            }
            var group = root.AddComponent<SurvivorGroup>();
            root.SetActive(false);
            return group;
        }
    }
}
