using System;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Infantry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Construye la escena de prototipo FPS «Pisagua» (ROADMAP 3.1–3.4) con primitivas: un campo de pruebas para el
    /// controlador de primera persona (rampas, escalera, túnel bajo, parapetos) y una línea de tiro con siluetas
    /// cada 100 m para practicar con el alza graduada del Comblain; humo de pólvora negra y un patio de esgrima con
    /// muñecos para la bayoneta y el corvo.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.PisaguaPrototypeBuilder.RunBatch</c>
    /// </summary>
    public static class PisaguaPrototypeBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Proto_Pisagua_FPS.unity";

        private static readonly Color SandColor = new Color(0.78f, 0.69f, 0.52f);
        private static readonly Color RockColor = new Color(0.55f, 0.47f, 0.38f);
        private static readonly Color AdobeColor = new Color(0.68f, 0.55f, 0.4f);
        private static readonly Color SackColor = new Color(0.62f, 0.56f, 0.42f);
        private static readonly Color TargetColor = new Color(0.25f, 0.2f, 0.18f);
        private static readonly Color MarkerColor = new Color(0.85f, 0.82f, 0.75f);
        private static readonly Color WoodColor = new Color(0.36f, 0.24f, 0.14f);
        private static readonly Color SteelColor = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color BrassColor = new Color(0.72f, 0.56f, 0.26f);
        private static readonly Color StrawColor = new Color(0.86f, 0.74f, 0.42f);

        [MenuItem("Pacífico/Prototipos/Construir escena FPS de Pisagua")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nWASD mover · ratón mirar · Mayús correr · C/Ctrl agacharse (corriendo: deslizarse) · Espacio saltar · " +
                "botón derecho apuntar · clic disparar · R recargar · rueda al apuntar: alza · F estocada · G tajo con el corvo · " +
                "V descarga de prueba (humo) · " +
                "Esc libera el ratón", "Aceptar");
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
            var comblain = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + WeaponCatalog.ComblainId + ".asset");
            if (comblain == null) throw new InvalidOperationException("No se generó el WeaponDataSO del Comblain.");
            var corvo = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + WeaponCatalog.CorvoId + ".asset");
            if (corvo == null) throw new InvalidOperationException("No se generó el WeaponDataSO del corvo.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEnvironment();
            CreateMovementCourse();
            CreateRifleRange();
            CreateFencingYard();
            FirstPersonController player = CreatePlayer(comblain, corvo);

            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;
            hud.Rifle = player.GetComponent<RifleController>();
            hud.Melee = player.GetComponent<MeleeController>();
            CreateAmmoCrate(comblain.ToSpec().Cartridge);
            CreateSmoke(player.ViewCamera.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Escena FPS de prototipo guardada en " + ScenePath);
        }

        // ------------------------------------------------------------------------------------------
        // Entorno
        // ------------------------------------------------------------------------------------------

        private static void CreateEnvironment()
        {
            var sun = new GameObject("Sol_del_Desierto").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);

            // Camanchaca: niebla costera que limita la visión a lo lejos.
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.82f, 0.8f, 0.76f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1600f;

            Box("Suelo_Arena", null, new Vector3(0f, -0.5f, 500f), new Vector3(600f, 1f, 1400f), SandColor, "Arena");
            // Acantilado del morro de Pisagua cerrando el fondo del campo de tiro.
            Box("Acantilado_Morro", null, new Vector3(0f, 90f, 1250f), new Vector3(900f, 180f, 60f), RockColor, "Roca");
        }

        /// <summary>Recorrido para comprobar el movimiento: pendientes, escalera, túnel, parapetos y un salto.</summary>
        private static void CreateMovementCourse()
        {
            var root = new GameObject("Recorrido_de_Movimiento").transform;
            root.position = new Vector3(-30f, 0f, 0f);

            // Rampas de 20°, 35° (se suben) y 50° (supera el slopeLimit de 45°: no se puede subir).
            float[] angles = { 20f, 35f, 50f };
            for (int i = 0; i < angles.Length; i++)
            {
                GameObject ramp = Box("Rampa_" + angles[i] + "grados", root, new Vector3(i * 8f, 0f, 10f), new Vector3(5f, 0.4f, 12f), RockColor, "Roca");
                ramp.transform.localRotation = Quaternion.Euler(-angles[i], 0f, 0f);
                ramp.transform.localPosition += new Vector3(0f, Mathf.Sin(angles[i] * Mathf.Deg2Rad) * 6f - 0.2f, 0f);
            }

            // Escalera: peldaños macizos de 0,25 m de contrahuella y 0,4 m de huella (stepOffset de 0,35 m).
            for (int i = 0; i < 8; i++)
            {
                float height = (i + 1) * 0.25f;
                Box("Peldano_" + (i + 1), root, new Vector3(28f, height * 0.5f, 4f + i * 0.4f), new Vector3(4f, height, 0.4f), AdobeColor, "Adobe");
            }

            // Túnel de 1,35 m: obliga a agacharse y comprueba que no se puede poner de pie dentro.
            Box("Tunel_Pared_Izq", root, new Vector3(36f, 0.9f, 8f), new Vector3(0.4f, 1.8f, 8f), AdobeColor, "Adobe");
            Box("Tunel_Pared_Der", root, new Vector3(38.4f, 0.9f, 8f), new Vector3(0.4f, 1.8f, 8f), AdobeColor, "Adobe");
            Box("Tunel_Techo", root, new Vector3(37.2f, 1.55f, 8f), new Vector3(2.8f, 0.4f, 8f), AdobeColor, "Adobe");

            // Parapetos de sacos (1,1 m, cubren agachado) y tapias de adobe (2,5 m) como en Tarapacá.
            for (int i = 0; i < 4; i++)
            {
                Box("Parapeto_Sacos_" + (i + 1), root, new Vector3(-8f + i * 6f, 0.55f, 30f), new Vector3(4f, 1.1f, 0.8f), SackColor, "Sacos");
            }
            Box("Tapia_Adobe_1", root, new Vector3(4f, 1.25f, 40f), new Vector3(10f, 2.5f, 0.5f), AdobeColor, "Adobe");
            Box("Tapia_Adobe_2", root, new Vector3(14f, 1.25f, 46f), new Vector3(0.5f, 2.5f, 12f), AdobeColor, "Adobe");

            // Obstáculo bajo para el salto (0,45 m: el soldado cargado apenas salta 0,55 m).
            Box("Obstaculo_Salto", root, new Vector3(20f, 0.225f, 30f), new Vector3(3f, 0.45f, 0.5f), WoodColor, "Madera");
        }

        /// <summary>Línea de tiro: siluetas cada 100 m hasta 1.000 m, con carteles de distancia.</summary>
        private static void CreateRifleRange()
        {
            var root = new GameObject("Linea_de_Tiro").transform;
            Box("Linea_de_Fuego", root, new Vector3(0f, 0.05f, 20f), new Vector3(30f, 0.1f, 0.3f), MarkerColor, "Marca");
            Font font = BuiltinFont();

            for (int d = 100; d <= 1000; d += 100)
            {
                float z = 20f + d;
                float x = (d / 100 % 2 == 0 ? 1f : -1f) * 4f;
                Box("Silueta_" + d + "m", root, new Vector3(x, 0.85f, z), new Vector3(0.5f, 1.7f, 0.1f), TargetColor, "Blanco")
                    .AddComponent<ShootingTarget>();
                Box("Poste_" + d + "m", root, new Vector3(x + 1.2f, 1.5f, z), new Vector3(0.12f, 3f, 0.12f), WoodColor, "Madera");
                GameObject board = Box("Cartel_" + d + "m", root, new Vector3(x + 1.2f, 3.2f, z), new Vector3(2.4f, 1f, 0.1f), MarkerColor, "Marca");
                if (font != null) AddLabel(board.transform, d + " m", font);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Jugador
        // ------------------------------------------------------------------------------------------

        private static FirstPersonController CreatePlayer(WeaponDataSO weapon, WeaponDataSO sidearm)
        {
            var player = new GameObject("Jugador_Infante");
            player.transform.position = new Vector3(0f, 0.05f, 16f);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.875f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f; // con 0,001 los desplazamientos lentos (agachado a 30 FPS) podían ignorarse

            var pivot = new GameObject("Pivote_Camara").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.61f, 0f);

            var cameraObject = new GameObject("Camara_Principal");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f; // el fusil está a ~0,35 m de los ojos
            camera.farClipPlane = 3000f;
            camera.fieldOfView = 70f;
            cameraObject.AddComponent<AudioListener>();

            var fps = player.AddComponent<FirstPersonController>();
            fps.Configure(pivot, camera, weapon);

            // Dotación corta a propósito: obliga a recoger la caja de cartuchos junto a la línea de fuego.
            var rifle = player.AddComponent<RifleController>();
            rifle.StartingReserve = 20;

            // Cuerpo a cuerpo (ROADMAP 3.4): bayoneta calada en el fusil y corvo al cinto.
            var melee = player.AddComponent<MeleeController>();
            melee.Sidearm = sidearm;

            CreateRifleViewModel(pivot, fps, rifle, melee);
            return fps;
        }

        /// <summary>
        /// Fusil Comblain esquemático. Origen en la base del alza; la cresta del alza y el punto de mira quedan a 0,075 m,
        /// de modo que en la pose de encare (y = -0,075) la línea de mira pasa por el centro de la cámara.
        /// Jerarquía: raíz (<see cref="IronSightViewModel"/>: cadera ↔ encare) → «Esgrima»
        /// (<see cref="MeleeViewModelAnimator"/>: guardia y estocada) → «Mecanica» (<see cref="RifleViewModelAnimator"/>:
        /// retroceso y recarga) → piezas. El corvo cuelga del pivote de cámara.
        /// </summary>
        private static void CreateRifleViewModel(Transform pivot, FirstPersonController owner, RifleController rifle, MeleeController melee)
        {
            var root = new GameObject("Fusil_Comblain_Vista").transform;
            root.SetParent(pivot, false);
            root.localPosition = new Vector3(0.22f, -0.2f, 0.45f);

            var fencing = new GameObject("Esgrima").transform;
            fencing.SetParent(root, false);

            var body = new GameObject("Mecanica").transform;
            body.SetParent(fencing, false);

            Material wood = PrototypeSceneKit.Material("Madera", WoodColor);
            Material steel = PrototypeSceneKit.Material("Acero", SteelColor, 0.5f);
            Material brass = PrototypeSceneKit.Material("Laton", BrassColor, 0.6f);

            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Culata", body, new Vector3(0f, -0.035f, -0.3f), new Vector3(0.05f, 0.09f, 0.42f), wood, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cajon_de_Mecanismos", body, new Vector3(0f, 0f, -0.02f), new Vector3(0.045f, 0.06f, 0.14f), steel, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Guardamano", body, new Vector3(0f, 0.005f, 0.3f), new Vector3(0.04f, 0.04f, 0.5f), wood, keepCollider: false);
            GameObject barrel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Canon", body, new Vector3(0f, 0.035f, 0.37f),
                new Vector3(0.022f, 0.4f, 0.022f), steel, keepCollider: false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Boca del cañón: de aquí salen el humo y el fogonazo (ROADMAP 3.3).
            var muzzle = new GameObject("Boca").transform;
            muzzle.SetParent(body, false);
            muzzle.localPosition = new Vector3(0f, 0.035f, 0.78f);
            rifle.Muzzle = muzzle;
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Punto_de_Mira", body, new Vector3(0f, 0.06f, 0.76f), new Vector3(0.004f, 0.03f, 0.006f), steel, keepCollider: false);

            // Bayoneta calada: hoja de 0,5 m a la derecha de la boca (la longitud que usa MeleeAttackProfile).
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Bayoneta_Cubo", body, new Vector3(0.018f, 0.03f, 0.77f), new Vector3(0.02f, 0.02f, 0.04f), steel, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Bayoneta_Hoja", body, new Vector3(0.022f, 0.022f, 1.03f), new Vector3(0.004f, 0.022f, 0.5f), steel, keepCollider: false);
            var bayonetTip = new GameObject("Punta_Bayoneta").transform;
            bayonetTip.SetParent(body, false);
            bayonetTip.localPosition = new Vector3(0.022f, 0.022f, 1.28f);

            // Palanca del Comblain (el guardamonte): pivota en su extremo delantero y su parte trasera baja al abrir,
            // haciendo descender el bloque de cierre.
            var lever = new GameObject("Palanca_Pivote").transform;
            lever.SetParent(body, false);
            lever.localPosition = new Vector3(0f, -0.04f, 0.03f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Palanca", lever, new Vector3(0f, -0.015f, -0.06f), new Vector3(0.015f, 0.02f, 0.12f), steel, keepCollider: false);

            // Cartucho que el soldado saca de la cartuchera y mete en la recámara (visible solo al cargar).
            GameObject round = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Cartucho", body, Vector3.zero,
                new Vector3(0.012f, 0.035f, 0.012f), brass, keepCollider: false);
            round.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Ventana de expulsión: la vaina sale hacia arriba y a la derecha.
            var port = new GameObject("Expulsion").transform;
            port.SetParent(body, false);
            port.localPosition = new Vector3(0.03f, 0.03f, 0f);

            // Alza de escalera: la corredera sube R·tan(θ) con la graduación.
            var leafPivot = new GameObject("Alza_Hoja").transform;
            leafPivot.SetParent(body, false);
            leafPivot.localPosition = new Vector3(0f, 0.045f, 0.06f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Alza_Lamina", leafPivot, new Vector3(0f, 0.015f, 0f), new Vector3(0.02f, 0.03f, 0.003f), steel, keepCollider: false);

            var viewModel = root.gameObject.AddComponent<IronSightViewModel>();
            viewModel.Configure(owner, leafPivot);

            var animator = body.gameObject.AddComponent<RifleViewModelAnimator>();
            // Giro negativo en X: la parte trasera de la palanca (z < 0 respecto al pivote) desciende.
            animator.Configure(rifle, lever, new Vector3(-50f, 0f, 0f), null, round.transform, port);

            Transform corvo = CreateCorvoViewModel(pivot, wood, steel);
            fencing.gameObject.AddComponent<MeleeViewModelAnimator>().Configure(melee, owner, bayonetTip, corvo);
        }

        /// <summary>Corvo chileno: origen en la empuñadura y hoja curva de 0,3 m hacia +Z (la de MeleeAttackProfile).</summary>
        private static Transform CreateCorvoViewModel(Transform pivot, Material wood, Material steel)
        {
            var corvo = new GameObject("Corvo_Vista").transform;
            corvo.SetParent(pivot, false);
            GameObject handle = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Mango", corvo, new Vector3(0f, 0f, -0.06f),
                new Vector3(0.028f, 0.06f, 0.028f), wood, keepCollider: false);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Hoja", corvo, new Vector3(0f, 0f, 0.1f), new Vector3(0.004f, 0.03f, 0.2f), steel, keepCollider: false);
            // La punta se curva hacia el filo, como el corvo campesino.
            GameObject tip = PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Hoja_Curva", corvo, new Vector3(0f, -0.012f, 0.245f),
                new Vector3(0.004f, 0.026f, 0.1f), steel, keepCollider: false);
            tip.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            corvo.gameObject.SetActive(false);
            return corvo;
        }

        // ------------------------------------------------------------------------------------------
        // Patio de esgrima (ROADMAP 3.4)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Muñecos de paja para practicar la estocada y el tajo, detrás de la línea de fuego y a la derecha del
        /// jugador: tres muñecos en fila, un poste fino de 8 cm (precisión del tajo) y un muñeco tras una tapia baja
        /// (la hoja no atraviesa el adobe).
        /// </summary>
        private static void CreateFencingYard()
        {
            var root = new GameObject("Patio_de_Esgrima").transform;
            root.position = new Vector3(9f, 0f, 12f);
            Material straw = PrototypeSceneKit.Material("Paja", StrawColor);
            Material wood = PrototypeSceneKit.Material("Madera", WoodColor);

            for (int i = 0; i < 3; i++) CreateDummy(root, "Muneco_" + (i + 1), new Vector3(i * 2.5f, 0f, 0f), straw, wood);

            GameObject pole = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Poste_de_Precision", root, new Vector3(7.5f, 1.1f, 0f),
                new Vector3(0.08f, 1.1f, 0.08f), wood);
            pole.AddComponent<MeleeDummy>();

            // El jugador llega desde la línea de fuego (+Z): el muñeco queda al otro lado de la tapia.
            CreateDummy(root, "Muneco_Tras_Tapia", new Vector3(10.5f, 0f, -0.9f), straw, wood);
            Box("Tapia_Baja", root, new Vector3(10.5f, 0.9f, 0f), new Vector3(1.6f, 1.8f, 0.3f), AdobeColor, "Adobe");
        }

        /// <summary>Saco de paja en un poste: torso (cápsula), cabeza (esfera, daño ×1,5) y brazos (solo visuales).</summary>
        private static void CreateDummy(Transform parent, string name, Vector3 localPosition, Material straw, Material wood)
        {
            var dummy = new GameObject(name).transform;
            dummy.SetParent(parent, false);
            dummy.localPosition = localPosition;
            // El pivote está en el suelo: el muñeco se balancea sobre la base del poste.
            PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Poste", dummy, new Vector3(0f, 0.6f, 0f), new Vector3(0.08f, 0.6f, 0.08f), wood, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Capsule, "Torso", dummy, new Vector3(0f, 1.15f, 0f), new Vector3(0.4f, 0.35f, 0.4f), straw);
            GameObject head = PrototypeSceneKit.Primitive(PrimitiveType.Sphere, "Cabeza", dummy, new Vector3(0f, 1.68f, 0f), new Vector3(0.22f, 0.22f, 0.22f), straw);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Brazos", dummy, new Vector3(0f, 1.42f, 0f), new Vector3(0.9f, 0.06f, 0.06f), wood, keepCollider: false);
            dummy.gameObject.AddComponent<MeleeDummy>().Configure(head.GetComponent<Collider>());
        }

        /// <summary>
        /// Humo de pólvora negra (ROADMAP 3.3) con la brisa marina que sopla de través sobre la línea de tiro, y el banco
        /// de pruebas de la descarga (tecla V). El material se guarda como asset para que la build incluya el sombreador.
        /// </summary>
        private static void CreateSmoke(Transform eye)
        {
            var go = new GameObject("Humo_Polvora_Negra");
            go.AddComponent<ParticleSystem>();
            var smoke = go.AddComponent<BlackPowderSmoke>();
            smoke.Wind = new Vector3(1.8f, 0f, 0.4f);

            ParticleSystem.EmissionModule emission = go.GetComponent<ParticleSystem>().emission;
            emission.enabled = false;
            Material material = SmokeMaterial();
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

            var serialized = new SerializedObject(smoke);
            serialized.FindProperty("material").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var volley = go.AddComponent<SmokeVolleyTest>();
            var volleySerialized = new SerializedObject(volley);
            volleySerialized.FindProperty("eye").objectReferenceValue = eye;
            volleySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material SmokeMaterial()
        {
            string path = ProjectPaths.Materials + "/Proto_Humo.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = BlackPowderSmoke.CreateMaterial();
            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Caja de cartuchos de Comblain junto a la línea de fuego (escasez de Tarapacá, GDD cap. 4).</summary>
        private static void CreateAmmoCrate(string cartridge)
        {
            GameObject crate = Box("Caja_Cartuchos_Comblain", null, new Vector3(3f, 0.25f, 18f), new Vector3(0.6f, 0.5f, 0.4f), WoodColor, "Madera");
            crate.GetComponent<BoxCollider>().isTrigger = true;
            crate.AddComponent<AmmoPickup>().Configure(cartridge, 40);
        }

        // ------------------------------------------------------------------------------------------

        private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Color color, string materialKey)
        {
            return PrototypeSceneKit.Primitive(PrimitiveType.Cube, name, parent, localPosition, scale, PrototypeSceneKit.Material(materialKey, color));
        }

        private static Font BuiltinFont()
        {
            // Unity 2022.2+ renombró la fuente integrada; se prueban ambos nombres.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static void AddLabel(Transform board, string text, Font font)
        {
            var label = new GameObject("Texto");
            label.transform.SetParent(board, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            // Compensa la escala no uniforme del cartel para que el texto no se deforme.
            label.transform.localScale = new Vector3(0.1f / board.localScale.x, 0.1f / board.localScale.y, 1f);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.35f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.black;
            // Sin girar: un TextMesh se lee de frente desde -Z, que es donde está la línea de fuego.
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
