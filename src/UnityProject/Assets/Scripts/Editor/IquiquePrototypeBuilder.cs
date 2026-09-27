using System;
using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Construye la escena de prototipo «Rada de Iquique» (Fase 2) con primitivas (greyboxing, AGENTS.md §4.A):
    /// el Huáscar controlado por el jugador (torre Coles, espolón, zonas de blindaje) frente a la Esmeralda con IA
    /// y batería de costado. Sirve para verificar en el motor la maniobra, la artillería, el blindaje y las averías.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.IquiquePrototypeBuilder.RunBatch</c>
    /// </summary>
    public static class IquiquePrototypeBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Proto_Iquique.unity";
        private const string ShellPrefabPath = ProjectPaths.Prefabs + "/Proto_NavalShell.prefab";

        private static readonly Color SeaColor = new Color(0.16f, 0.28f, 0.34f);
        private static readonly Color CoastColor = new Color(0.72f, 0.64f, 0.5f);
        private static readonly Color IronColor = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color WoodColor = new Color(0.36f, 0.24f, 0.14f);
        private static readonly Color BrassColor = new Color(0.55f, 0.45f, 0.25f);

        [MenuItem("Pacífico/Prototipos/Construir escena naval de Iquique")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nW/S telégrafo · A/D timón · ratón apunta la torre · clic dispara · R/F convergencia · Mayús telémetro · 1/2/3 averías", "Aceptar");
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
            var huascarData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.HuascarId + ".asset");
            var esmeraldaData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.EsmeraldaId + ".asset");
            if (huascarData == null || esmeraldaData == null) throw new InvalidOperationException("No se generaron los ShipDataSO.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavalShell shellPrefab = CreateShellPrefab();

            CreateEnvironment();
            ShipController huascar = CreateHuascar(huascarData, shellPrefab);
            ShipController esmeralda = CreateEsmeralda(esmeraldaData, shellPrefab, huascar);

            var camera = new GameObject("Camara_Principal");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.farClipPlane = 6000f;
            cam.fieldOfView = 60f;
            camera.AddComponent<AudioListener>();
            camera.transform.position = huascar.transform.position + new Vector3(0f, 30f, -90f);
            camera.AddComponent<ShipCameraRig>().Target = huascar.transform;

            var hud = new GameObject("HUD_Naval").AddComponent<NavalHud>();
            hud.Ship = huascar;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Escena de prototipo guardada en " + ScenePath + " (" + esmeralda.name + " contra " + huascar.name + ").");
        }

        // ------------------------------------------------------------------------------------------
        // Entorno
        // ------------------------------------------------------------------------------------------

        private static void CreateEnvironment()
        {
            var sun = new GameObject("Sol_Neblina_de_Iquique").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.78f, 0.8f, 0.8f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 600f;
            RenderSettings.fogEndDistance = 4500f;

            GameObject sea = Primitive(PrimitiveType.Plane, "Mar", null, Vector3.zero, new Vector3(600f, 1f, 600f), SeaColor);
            Object.DestroyImmediate(sea.GetComponent<Collider>()); // los proyectiles detectan el agua por altura

            // Costa de Iquique al este: referencia visual y límite de maniobra.
            Primitive(PrimitiveType.Cube, "Costa_de_Iquique", null, new Vector3(1600f, 20f, 0f), new Vector3(400f, 40f, 5000f), CoastColor);
        }

        // ------------------------------------------------------------------------------------------
        // Huáscar
        // ------------------------------------------------------------------------------------------

        private static ShipController CreateHuascar(ShipDataSO data, NavalShell shellPrefab)
        {
            ShipSpec spec = data.ToSpec();
            GameObject root = ShipRoot("Huascar", data, new Vector3(0f, 0f, -1400f), 0f, player: true, EngineOrder.HalfAhead, 0.5f);
            Transform hull = root.transform.Find("Casco");

            float length = spec.LengthM;
            float beam = spec.BeamM;
            Primitive(PrimitiveType.Cube, "Casco_Visual", hull, Vector3.zero, new Vector3(beam, 4f, length), IronColor, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Chimenea", hull, new Vector3(0f, 5f, -6f), new Vector3(2.2f, 3f, 2.2f), IronColor, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Palo", hull, new Vector3(0f, 10f, -12f), new Vector3(0.5f, 9f, 0.5f), WoodColor, keepCollider: false);

            AddArmorZones(hull, spec);
            Zone(hull, "Torre_de_Mando", ArmorZone.ConningTower, new Vector3(0f, 3.2f, 4f), new Vector3(2.4f, 2.4f, 2.4f), false);
            Primitive(PrimitiveType.Cylinder, "Torre_de_Mando_Visual", hull, new Vector3(0f, 3.2f, 4f), new Vector3(2.4f, 1.2f, 2.4f), IronColor, keepCollider: false);

            // Torre Coles: pivote de giro → cuna de elevación → dos piezas de 300 lb.
            var pivot = new GameObject("Torre_Coles").transform;
            pivot.SetParent(hull, false);
            pivot.localPosition = new Vector3(0f, 2.5f, 12f);
            GameObject turretBody = Primitive(PrimitiveType.Cylinder, "Torre_Coles_Visual", pivot, Vector3.zero, new Vector3(6.7f, 1.3f, 6.7f), IronColor, keepCollider: false);
            // Caja en espacio local del cilindro (altura 2, radio 0,5): una cápsula degeneraría en esfera.
            turretBody.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            turretBody.AddComponent<ArmorZoneMarker>().Zone = ArmorZone.MainBattery;

            var cradle = new GameObject("Cuna").transform;
            cradle.SetParent(pivot, false);
            cradle.localPosition = new Vector3(0f, 0.6f, 2.6f);
            var muzzles = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.2f : 1.2f;
                GameObject barrel = Primitive(PrimitiveType.Cylinder, "Canon_300lb_" + (i == 0 ? "Babor" : "Estribor"), cradle,
                    new Vector3(x, 0f, 2.2f), new Vector3(0.6f, 2.4f, 0.6f), IronColor, keepCollider: false);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var muzzle = new GameObject("Boca_" + i).transform;
                muzzle.SetParent(cradle, false);
                muzzle.localPosition = new Vector3(x, 0f, 4.8f);
                muzzles[i] = muzzle;
            }

            // Espolón de proa.
            var ram = new GameObject("Espolon");
            ram.transform.SetParent(hull, false);
            ram.transform.localPosition = new Vector3(0f, -0.8f, length * 0.5f + 1f);
            var ramCollider = ram.AddComponent<BoxCollider>();
            ramCollider.isTrigger = true;
            ramCollider.size = new Vector3(2f, 2.5f, 3f);
            ram.AddComponent<RamBow>().Owner = root.GetComponent<ShipController>();

            var turret = root.AddComponent<ColesTurretController>();
            turret.Configure(root.GetComponent<ShipController>(), pivot, cradle, muzzles, shellPrefab);
            return root.GetComponent<ShipController>();
        }

        // ------------------------------------------------------------------------------------------
        // Esmeralda
        // ------------------------------------------------------------------------------------------

        private static ShipController CreateEsmeralda(ShipDataSO data, NavalShell shellPrefab, ShipController enemy)
        {
            ShipSpec spec = data.ToSpec();
            GameObject root = ShipRoot("Esmeralda", data, new Vector3(900f, 0f, 0f), 200f, player: false, EngineOrder.QuarterAhead, 0.5f);
            Transform hull = root.transform.Find("Casco");

            Primitive(PrimitiveType.Cube, "Casco_Visual", hull, Vector3.zero, new Vector3(spec.BeamM, 5f, spec.LengthM), WoodColor, keepCollider: false);
            for (int i = -1; i <= 1; i++)
            {
                Primitive(PrimitiveType.Cylinder, "Palo_" + (i + 2), hull, new Vector3(0f, 16f, i * 18f), new Vector3(0.6f, 14f, 0.6f), WoodColor, keepCollider: false);
            }
            Primitive(PrimitiveType.Cylinder, "Chimenea", hull, new Vector3(0f, 5f, -4f), new Vector3(1.8f, 3f, 1.8f), IronColor, keepCollider: false);
            AddArmorZones(hull, spec);

            var battery = root.AddComponent<BroadsideBatteryController>();
            battery.ShellPrefab = shellPrefab;
            root.AddComponent<BroadsideShipAI>().Enemy = enemy;
            return root.GetComponent<ShipController>();
        }

        // ------------------------------------------------------------------------------------------
        // Piezas comunes
        // ------------------------------------------------------------------------------------------

        private static GameObject ShipRoot(string name, ShipDataSO data, Vector3 position, float heading, bool player,
                                           EngineOrder order, float speedFraction)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var controller = root.AddComponent<ShipController>();
            var hull = new GameObject("Casco").transform;
            hull.SetParent(root.transform, false);

            var so = new SerializedObject(controller);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("playerControlled").boolValue = player;
            so.FindProperty("initialOrder").intValue = (int)order;
            so.FindProperty("initialSpeedFraction").floatValue = speedFraction;
            so.FindProperty("hullVisual").objectReferenceValue = hull;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<ArmoredHull>();
            var damage = root.AddComponent<ShipDamageController>();
            damage.PlayerControlled = player;
            return root;
        }

        /// <summary>Colisionadores de blindaje: flotación (abre vías de agua), cinturón central, extremos y cubierta.</summary>
        private static void AddArmorZones(Transform hull, ShipSpec spec)
        {
            float length = spec.LengthM;
            float beam = spec.BeamM + 0.2f;
            float centralLength = length * 0.5f;
            float endLength = (length - centralLength) * 0.5f;

            Zone(hull, "Linea_de_Flotacion", ArmorZone.BeltMidships, new Vector3(0f, 0.4f, 0f), new Vector3(beam, 0.8f, centralLength), true);
            Zone(hull, "Cinturon_Central", ArmorZone.BeltMidships, new Vector3(0f, 1.4f, 0f), new Vector3(beam, 1.2f, centralLength), false);
            Zone(hull, "Cinturon_Proa", ArmorZone.BeltEnds, new Vector3(0f, 1f, (centralLength + endLength) * 0.5f), new Vector3(beam, 2f, endLength), true);
            Zone(hull, "Cinturon_Popa", ArmorZone.BeltEnds, new Vector3(0f, 1f, -(centralLength + endLength) * 0.5f), new Vector3(beam, 2f, endLength), true);
            Zone(hull, "Cubierta", ArmorZone.Deck, new Vector3(0f, 2.1f, 0f), new Vector3(beam - 0.4f, 0.2f, length), false);
        }

        private static void Zone(Transform parent, string name, ArmorZone zone, Vector3 position, Vector3 size, bool belowWaterline)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider>().size = size;
            var marker = go.AddComponent<ArmorZoneMarker>();
            marker.Zone = zone;
            marker.BelowWaterline = belowWaterline;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
                                            Vector3 localScale, Color color, bool keepCollider = true)
        {
            return PrototypeSceneKit.Primitive(type, name, parent, localPosition, localScale, MaterialFor(name, color), keepCollider);
        }

        private static Material MaterialFor(string name, Color color)
        {
            string key = color == SeaColor ? "Mar" : color == CoastColor ? "Costa" : color == IronColor ? "Hierro"
                : color == WoodColor ? "Madera" : color == BrassColor ? "Bronce" : name;
            return PrototypeSceneKit.Material(key, color, key == "Mar" ? 0.8f : 0.25f);
        }

        private static NavalShell CreateShellPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NavalShell>(ShellPrefabPath);
            if (existing != null) return existing;

            GameObject shell = Primitive(PrimitiveType.Sphere, "Proyectil", null, Vector3.zero, Vector3.one * 0.6f, BrassColor, keepCollider: false);
            shell.AddComponent<NavalShell>();
            var trail = shell.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.4f;
            trail.endWidth = 0f;
            trail.sharedMaterial = shell.GetComponent<Renderer>().sharedMaterial;

            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Prefabs);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(shell, ShellPrefabPath);
            Object.DestroyImmediate(shell);
            return prefab.GetComponent<NavalShell>();
        }
    }
}
