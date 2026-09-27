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

        private static void BuildScene()
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
                CreateShoreBattery("Bateria_Tierra_Norte", new Vector3(CoastFaceX - 6f, CoastTopY, 420f), esmeralda, shellPrefab),
                CreateShoreBattery("Bateria_Tierra_Sur", new Vector3(CoastFaceX - 6f, CoastTopY, -180f), esmeralda, shellPrefab),
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
            B.Primitive(PrimitiveType.Cube, "Parapeto", root.transform, new Vector3(0f, 0.6f, 3f), new Vector3(14f, 1.2f, 1.5f), B.CoastColor, keepCollider: false);
            for (int i = 0; i < 2; i++)
            {
                GameObject barrel = B.Primitive(PrimitiveType.Cylinder, "Pieza_" + (i + 1), root.transform,
                    new Vector3(i == 0 ? -3f : 3f, 1.2f, 3.2f), new Vector3(0.3f, 1.1f, 0.3f), B.BrassColor, keepCollider: false);
                barrel.transform.localRotation = Quaternion.Euler(84f, 0f, 0f);
            }
            var battery = root.AddComponent<ShoreBattery>();
            battery.Configure(target, shellPrefab);
            return battery;
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
            B.Primitive(PrimitiveType.Cube, "Casco_Visual", hull, Vector3.zero, new Vector3(spec.BeamM, 4.5f, spec.LengthM), color, keepCollider: false);
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
            B.Primitive(PrimitiveType.Cube, "Madero", root.transform, Vector3.zero, new Vector3(3.2f, 0.35f, 0.8f), B.WoodColor, keepCollider: false);
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
