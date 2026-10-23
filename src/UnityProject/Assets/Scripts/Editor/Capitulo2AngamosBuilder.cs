using System;
using System.IO;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using B = Pacifico.EditorTools.IquiquePrototypeBuilder;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena jugable del Capítulo 2, «Caza en alta mar» (ROADMAP 6.7): el mar frente a Punta Angamos el 8 de octubre de
    /// 1879, con el Huáscar y la Unión huyendo hacia el norte, el Cochrane del jugador cortándoles el paso y el Blanco
    /// Encalada llegando desde el sur. La misión la dirige <see cref="AngamosMissionDirector"/> con el guion de
    /// <see cref="AngamosChapter"/>; el prólogo se copia de <c>Historia_Completa_Guion.md</c> al construir la escena y
    /// las láminas de cierre se importan del Archivo Histórico. Greyboxing con primitivas (AGENTS.md §4.A).
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.Capitulo2AngamosBuilder.RunBatch</c>
    /// </summary>
    public static class Capitulo2AngamosBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Capitulo2_Punta_Angamos.unity";

        /// <summary>Láminas de época con las que se cierra el capítulo (ambas del Archivo, ninguna moderna).</summary>
        internal static readonly string[] PlateImages =
        {
            "Archivo_Historico/01_Barcos_y_Combate_Naval/10_Captura_del_Huascar_Blanco_y_Cochrane.png",
            "Archivo_Historico/01_Barcos_y_Combate_Naval/09_Huascar_Danio_Combate_Angamos.jpg",
        };

        internal static readonly string[] PlateCaptions =
        {
            "La captura del Huáscar por el Blanco Encalada y el Cochrane. Acuarela de época (Lisle). Archivo Histórico.",
            "Los daños del Huáscar tras Angamos: cada marca del casco y de la torre es un impacto. Grabado de época. Archivo Histórico.",
        };

        private static readonly Color StormSeaColor = new Color(0.2f, 0.27f, 0.3f);
        private static readonly Color HeadlandColor = new Color(0.55f, 0.5f, 0.42f);

        [MenuItem("Pacífico/Capítulos/Capítulo 2: Punta Angamos")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nCochrane: W/S máquina, A/D timón." +
                "\nEspacio o clic: andanada cuando la cubierta pase por la horizontal." +
                "\n1/2/3: brigada de averías.", "Aceptar");
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

            // La sección del capítulo viaja dentro de la escena: la build no lee el guion del repositorio.
            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            string guion = File.ReadAllText(Path.Combine(root, GuionQuotes.ScriptFile));
            string chapter = GuionQuotes.Section(guion, AngamosChapter.ChapterHeading);
            AngamosChapter.Build(GuionQuotes.Extract(chapter, AngamosChapter.ChapterHeading)); // falla aquí si el guion cambió

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavalShell shellPrefab = B.CreateShellPrefab();
            CreateEnvironment();

            // El Huáscar y la Unión huyen al norte; el Cochrane les corta el paso desde el nordeste; el Blanco, por detrás.
            ShipController huascar = B.CreateHuascar(LoadShip(ShipCatalog.HuascarId), shellPrefab, new Vector3(0f, 0f, 0f), 0f);
            ShipController cochrane = CreateIronclad("Cochrane", LoadShip(ShipCatalog.CochraneId), shellPrefab, huascar,
                new Vector3(1700f, 0f, 3000f), 235f, player: true);
            ShipController blanco = CreateIronclad("Blanco_Encalada", LoadShip(ShipCatalog.BlancoEncaladaId), shellPrefab, huascar,
                new Vector3(-600f, 0f, -4500f), 0f, player: false);
            EscapingShip union = CreateUnion(new Vector3(-180f, 0f, 260f));

            var gunner = cochrane.gameObject.AddComponent<PlayerBroadsideGunner>();
            var gunnerSettings = new SerializedObject(gunner);
            gunnerSettings.FindProperty("rollAmplitudeDeg").floatValue = AngamosChapter.RollAmplitudeDeg;
            gunnerSettings.FindProperty("rollPeriodSeconds").floatValue = AngamosChapter.RollPeriodSeconds;
            gunnerSettings.ApplyModifiedPropertiesWithoutUndo();
            gunner.Target = huascar;
            var cochraneAI = cochrane.GetComponent<BroadsideShipAI>();
            cochraneAI.enabled = false; // solo gobierna en el intercalado del Huáscar

            RamBow ram = huascar.GetComponentInChildren<RamBow>();
            var turret = huascar.GetComponent<ColesTurretController>();
            var huascarAI = huascar.gameObject.AddComponent<RammingShipAI>();
            huascarAI.Configure(cochrane, ram, turret);
            Transform tower = huascar.transform.Find("Casco/Torre_de_Mando_Visual");

            var cameraObject = new GameObject("Camara_Principal");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 9000f;
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = cochrane.transform.position + new Vector3(0f, 30f, -90f);
            var rig = cameraObject.AddComponent<ShipCameraRig>();
            rig.Target = cochrane.transform;

            var hud = new GameObject("HUD_Naval").AddComponent<NavalHud>();
            hud.Ship = cochrane;

            Material burst = PrototypeSceneKit.Material("Humo_Explosion", new Color(0.18f, 0.16f, 0.15f));
            var plates = new Texture2D[PlateImages.Length];
            for (int i = 0; i < PlateImages.Length; i++) plates[i] = HistoricalDataAssetGenerator.ImportArchiveTexture(root, PlateImages[i]);

            var director = new GameObject("Director_Capitulo2").AddComponent<AngamosMissionDirector>();
            director.Configure(chapter, cochrane, gunner, cochraneAI, huascar, huascarAI, turret, ram, tower,
                               blanco, blanco.GetComponent<BroadsideShipAI>(), union, rig, hud, burst, plates, PlateCaptions);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Capítulo 2 guardado en " + ScenePath);
        }

        /// <summary>Mar abierto y plomizo con la península de Mejillones (Punta Angamos) al este.</summary>
        private static void CreateEnvironment()
        {
            var sun = new GameObject("Sol_Cielo_Plomizo").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.8f;
            sun.color = new Color(0.86f, 0.88f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(28f, 80f, 0f); // amanecer: el sol bajo, por el este
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.62f, 0.65f, 0.68f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 900f;
            RenderSettings.fogEndDistance = 7000f;

            GameObject sea = B.Primitive(PrimitiveType.Plane, "Mar", null, Vector3.zero, new Vector3(1600f, 1f, 1600f), StormSeaColor);
            UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>()); // los proyectiles detectan el agua por altura
            B.Primitive(PrimitiveType.Cube, "Peninsula_de_Mejillones", null, new Vector3(6500f, 30f, 2000f), new Vector3(1500f, 60f, 9000f), HeadlandColor);
            B.Primitive(PrimitiveType.Cube, "Punta_Angamos", null, new Vector3(5600f, 20f, 5200f), new Vector3(700f, 40f, 900f), HeadlandColor);
        }

        /// <summary>
        /// Fragata blindada de reducto central (Cochrane, Blanco Encalada): casco de hierro, el reducto con las piezas de
        /// 250 lb en el centro, una chimenea y tres palos. Lleva su batería de costado y una IA de costado.
        /// </summary>
        internal static ShipController CreateIronclad(string name, ShipDataSO data, NavalShell shellPrefab, ShipController enemy,
                                                      Vector3 position, float heading, bool player)
        {
            ShipSpec spec = data.ToSpec();
            GameObject root = B.ShipRoot(name, data, position, heading, player, EngineOrder.FullAhead, 0.8f);
            Transform hull = root.transform.Find("Casco");

            B.Primitive(PrimitiveType.Cube, "Casco_Visual", hull, Vector3.zero, new Vector3(spec.BeamM, 5.5f, spec.LengthM), B.IronColor, keepCollider: false);
            B.Primitive(PrimitiveType.Cube, "Reducto_Central_Visual", hull, new Vector3(0f, 3.6f, 2f), new Vector3(spec.BeamM * 0.8f, 2.4f, spec.LengthM * 0.3f), B.IronColor, keepCollider: false);
            B.Primitive(PrimitiveType.Cylinder, "Chimenea", hull, new Vector3(0f, 7f, -3f), new Vector3(2.4f, 3.5f, 2.4f), B.IronColor, keepCollider: false);
            for (int i = -1; i <= 1; i++)
            {
                B.Primitive(PrimitiveType.Cylinder, "Palo_" + (i + 2), hull, new Vector3(0f, 17f, i * spec.LengthM * 0.3f), new Vector3(0.6f, 14f, 0.6f), B.WoodColor, keepCollider: false);
            }
            B.AddArmorZones(hull, spec);
            B.Zone(hull, "Reducto_Central", ArmorZone.MainBattery, new Vector3(0f, 3.6f, 2f), new Vector3(spec.BeamM * 0.8f, 2.4f, spec.LengthM * 0.3f), false);

            // Modelo CC-BY (tarea 7.2): el Odin danés de 1872, también de reducto central.
            if (ShipModelLibrary.Place(ShipModelLibrary.Odin, "Modelo_Odin", hull, spec.LengthM, ShipModelLibrary.OdinAsCochrane) != null)
            {
                ShipModelLibrary.HideStandIns(hull, "Casco_Visual", "Reducto_Central_Visual", "Chimenea", "Palo_1", "Palo_2", "Palo_3");
            }

            var battery = root.AddComponent<BroadsideBatteryController>();
            battery.ShellPrefab = shellPrefab;
            root.AddComponent<BroadsideShipAI>().Enemy = enemy;
            return root.GetComponent<ShipController>();
        }

        /// <summary>La corbeta Unión: decorado que huye (sin ficha ni combate).</summary>
        private static EscapingShip CreateUnion(Vector3 position)
        {
            var root = new GameObject("Union");
            root.transform.SetPositionAndRotation(position, Quaternion.identity);
            B.Primitive(PrimitiveType.Cube, "Casco_Visual", root.transform, new Vector3(0f, 1f, 0f), new Vector3(10f, 4.5f, 70f), B.WoodColor, keepCollider: false);
            B.Primitive(PrimitiveType.Cylinder, "Chimenea", root.transform, new Vector3(0f, 5f, -3f), new Vector3(2f, 3f, 2f), B.IronColor, keepCollider: false);
            for (int i = -1; i <= 1; i++)
            {
                B.Primitive(PrimitiveType.Cylinder, "Palo_" + (i + 2), root.transform, new Vector3(0f, 15f, i * 20f), new Vector3(0.5f, 13f, 0.5f), B.WoodColor, keepCollider: false);
            }
            var union = root.AddComponent<EscapingShip>();
            union.Configure(0f, 5.5f, 7.5f);
            return union;
        }
    }
}
