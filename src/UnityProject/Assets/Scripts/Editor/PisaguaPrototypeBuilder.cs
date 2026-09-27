using System;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Construye la escena de prototipo FPS «Pisagua» (ROADMAP 3.1) con primitivas: un campo de pruebas para el
    /// controlador de primera persona (rampas, escalera, túnel bajo, parapetos) y una línea de tiro con siluetas
    /// cada 100 m para practicar con el alza graduada del Comblain.
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

        [MenuItem("Pacífico/Prototipos/Construir escena FPS de Pisagua")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nWASD mover · ratón mirar · Mayús correr · C/Ctrl agacharse (corriendo: deslizarse) · Espacio saltar · " +
                "botón derecho apuntar · rueda al apuntar: alza · Esc libera el ratón", "Aceptar");
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

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEnvironment();
            CreateMovementCourse();
            CreateRifleRange();
            FirstPersonController player = CreatePlayer(comblain);

            var hud = new GameObject("HUD_Infanteria").AddComponent<InfantryHud>();
            hud.Player = player;

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
                Box("Silueta_" + d + "m", root, new Vector3(x, 0.85f, z), new Vector3(0.5f, 1.7f, 0.1f), TargetColor, "Blanco");
                Box("Poste_" + d + "m", root, new Vector3(x + 1.2f, 1.5f, z), new Vector3(0.12f, 3f, 0.12f), WoodColor, "Madera");
                GameObject board = Box("Cartel_" + d + "m", root, new Vector3(x + 1.2f, 3.2f, z), new Vector3(2.4f, 1f, 0.1f), MarkerColor, "Marca");
                if (font != null) AddLabel(board.transform, d + " m", font);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Jugador
        // ------------------------------------------------------------------------------------------

        private static FirstPersonController CreatePlayer(WeaponDataSO weapon)
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

            CreateRifleViewModel(pivot, fps);
            return fps;
        }

        /// <summary>
        /// Fusil Comblain esquemático. Origen en la base del alza; la cresta del alza y el punto de mira quedan a 0,075 m,
        /// de modo que en la pose de encare (y = -0,075) la línea de mira pasa por el centro de la cámara.
        /// </summary>
        private static void CreateRifleViewModel(Transform pivot, FirstPersonController owner)
        {
            var root = new GameObject("Fusil_Comblain_Vista").transform;
            root.SetParent(pivot, false);
            root.localPosition = new Vector3(0.22f, -0.2f, 0.45f);

            Material wood = PrototypeSceneKit.Material("Madera", WoodColor);
            Material steel = PrototypeSceneKit.Material("Acero", SteelColor, 0.5f);

            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Culata", root, new Vector3(0f, -0.035f, -0.3f), new Vector3(0.05f, 0.09f, 0.42f), wood, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Cajon_de_Mecanismos", root, new Vector3(0f, 0f, -0.02f), new Vector3(0.045f, 0.06f, 0.14f), steel, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Palanca", root, new Vector3(0f, -0.06f, -0.04f), new Vector3(0.015f, 0.05f, 0.1f), steel, keepCollider: false);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Guardamano", root, new Vector3(0f, 0.005f, 0.3f), new Vector3(0.04f, 0.04f, 0.5f), wood, keepCollider: false);
            GameObject barrel = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Canon", root, new Vector3(0f, 0.035f, 0.37f),
                new Vector3(0.022f, 0.4f, 0.022f), steel, keepCollider: false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Punto_de_Mira", root, new Vector3(0f, 0.06f, 0.76f), new Vector3(0.004f, 0.03f, 0.006f), steel, keepCollider: false);

            // Alza de hoja: pivota en su base; su giro visual depende de la graduación.
            var leafPivot = new GameObject("Alza_Hoja").transform;
            leafPivot.SetParent(root, false);
            leafPivot.localPosition = new Vector3(0f, 0.045f, 0.06f);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Alza_Lamina", leafPivot, new Vector3(0f, 0.015f, 0f), new Vector3(0.02f, 0.03f, 0.003f), steel, keepCollider: false);

            var viewModel = root.gameObject.AddComponent<IronSightViewModel>();
            viewModel.Configure(owner, leafPivot);
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
