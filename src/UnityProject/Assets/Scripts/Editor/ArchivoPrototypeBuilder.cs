using System;
using Pacifico.Core.Narrative;
using Pacifico.Data;
using Pacifico.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Escena de prototipo del visor de documentos (ROADMAP 5.1): el escritorio de caoba del camarote de Grau (GDD,
    /// notas del capítulo 1) con la carta a Carmela Carvajal, su retrato y el plano militar de Arica. Al empezar se
    /// abre la carta de Grau en el visor; al cerrarlo, se puede examinar cualquier documento con un clic.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.ArchivoPrototypeBuilder.RunBatch</c>
    /// </summary>
    public static class ArchivoPrototypeBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Proto_Archivo_Camarote.unity";

        private const string CarmelaPhoto = "Archivo_Historico/05_Cartas_y_Documentos/02_Carmela_Carvajal_Briones.jpg";
        private const string AricaPlan = "Archivo_Historico/05_Cartas_y_Documentos/05_Plano_Militar_Batalla_de_Arica_1880.jpg";

        private static readonly Color Mahogany = new Color(0.33f, 0.14f, 0.08f);
        private static readonly Color Panelling = new Color(0.42f, 0.28f, 0.17f);
        private static readonly Color Brass = new Color(0.72f, 0.56f, 0.26f);

        [MenuItem("Pacífico/Prototipos/Construir visor de documentos (camarote de Grau)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath +
                "\n\nAl pulsar Play se abre la carta de Grau. Arrastrar: girar · F: dar la vuelta · rueda: acercar · " +
                "botón derecho: desplazar · T: transcripción · R: restablecer · Esc: cerrar. Después, clic en otro documento.", "Aceptar");
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
            string collectiblePath = ProjectPaths.CollectibleData + "/Collectible_" + CollectibleCatalog.GrauLetterId + ".asset";
            var grau = AssetDatabase.LoadAssetAtPath<CollectibleDataSO>(collectiblePath);
            if (grau == null) throw new InvalidOperationException("No se generó el coleccionable de la carta de Grau (" + collectiblePath + ").");
            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            Texture2D carmela = HistoricalDataAssetGenerator.ImportArchiveTexture(root, CarmelaPhoto);
            Texture2D arica = HistoricalDataAssetGenerator.ImportArchiveTexture(root, AricaPlan);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateRoom();

            // Documentos sobre el escritorio (tapa a 0,78 m).
            DocumentItem letter = Paper("Carta_de_Grau", new Vector3(-0.05f, 0.781f, 0.02f), 0.21f, 0.28f, -8f, grau.facsimile);
            letter.Configure(grau);

            DocumentItem photo = Paper("Retrato_Carmela_Carvajal", new Vector3(0.32f, 0.782f, 0.12f), 0.065f, 0.105f, 14f, carmela);
            photo.Configure("Carmela Carvajal Briones", "Retrato de estudio · viuda de Arturo Prat", carmela, DocumentForm.Photograph,
                "Carmela Carvajal Briones, esposa del capitán de fragata Arturo Prat. Destinataria de la carta que Miguel Grau " +
                "escribió el 2 de junio de 1879 al devolver las pertenencias de su marido.\n\nArchivo Histórico: 02_Carmela_Carvajal_Briones.jpg",
                string.Empty);

            DocumentItem plan = Paper("Plano_de_Arica", new Vector3(-0.42f, 0.7815f, 0.18f), 0.36f, 0.33f, 5f, arica);
            plan.Configure("Plano militar de la batalla de Arica", "7 de junio de 1880", arica, DocumentForm.Map,
                "Plano militar de la toma del Morro de Arica (7 de junio de 1880).\n\nArchivo Histórico: 05_Plano_Militar_Batalla_de_Arica_1880.jpg",
                string.Empty);

            var viewer = new GameObject("Visor_de_Documentos").AddComponent<DocumentViewer>();
            new GameObject("Escritorio_Interaccion").AddComponent<DocumentDesk>().Configure(camera, viewer, letter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Escena del visor de documentos guardada en " + ScenePath);
        }

        /// <summary>Camarote: suelo, mamparos de madera, escritorio de caoba y un quinqué.</summary>
        private static Camera CreateRoom()
        {
            Material wood = PrototypeSceneKit.Material("Caoba", Mahogany, 0.45f);
            Material panel = PrototypeSceneKit.Material("Mamparo", Panelling);
            Material brass = PrototypeSceneKit.Material("Laton", Brass, 0.6f);

            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Suelo", null, new Vector3(0f, -0.05f, 0f), new Vector3(4f, 0.1f, 4f), panel);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Mamparo_Proa", null, new Vector3(0f, 1.1f, 1.2f), new Vector3(4f, 2.2f, 0.1f), panel);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Mamparo_Babor", null, new Vector3(-1.4f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 4f), panel);
            PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Tapa_Escritorio", null, new Vector3(0f, 0.755f, 0.1f), new Vector3(1.3f, 0.05f, 0.7f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.6f : 0.6f, z = i < 2 ? -0.2f : 0.4f;
                PrototypeSceneKit.Primitive(PrimitiveType.Cube, "Pata", null, new Vector3(x, 0.365f, z), new Vector3(0.06f, 0.73f, 0.06f), wood);
            }
            GameObject lamp = PrototypeSceneKit.Primitive(PrimitiveType.Cylinder, "Quinque", null, new Vector3(0.5f, 0.9f, 0.35f), new Vector3(0.1f, 0.12f, 0.1f), brass);
            var light = new GameObject("Luz_Quinque").AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.8f, 0.55f);
            light.shadows = LightShadows.Soft;
            light.transform.position = lamp.transform.position + Vector3.up * 0.2f;
            RenderSettings.ambientLight = new Color(0.12f, 0.1f, 0.08f);

            var cameraObject = new GameObject("Camara_Camarote");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.45f, -0.75f), Quaternion.Euler(52f, 0f, 0f));
            return camera;
        }

        /// <summary>Hoja o fotografía sobre la mesa, con su facsímil como textura y un colisionador para el clic.</summary>
        private static DocumentItem Paper(string name, Vector3 position, float width, float depth, float yawDeg, Texture2D texture)
        {
            Material material = PrototypeSceneKit.Material("Documento_" + name, Color.white, 0.1f);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                EditorUtility.SetDirty(material);
            }
            GameObject go = PrototypeSceneKit.Primitive(PrimitiveType.Cube, name, null, position, new Vector3(width, 0.002f, depth), material);
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            return go.AddComponent<DocumentItem>();
        }
    }
}
