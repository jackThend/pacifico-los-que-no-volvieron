using System;
using System.IO;
using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Naval;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fotos de comprobación de los modelos de los buques (tarea 7.2): monta el Huáscar y la Esmeralda con los mismos
    /// constructores que los capítulos, en una escena sin guardar, y los renderiza desde varios ángulos a PNG.
    /// Modo batch (sin <c>-nographics</c>, hace falta la GPU):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.ShipModelPreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class ShipModelPreview
    {
        private const int Width = 1600;
        private const int Height = 900;

        public static void RunBatch()
        {
            try
            {
                string folder = ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/ShipPreview");
                Render(folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Pacífico/Arte/Fotos de comprobación de los buques")]
        public static void RenderMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = Path.GetFullPath("Temp/ShipPreview");
            Render(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            RenderPipelineSetup.Ensure();
            HistoricalDataAssetGenerator.GenerateAll();
            var huascarData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.HuascarId + ".asset");
            var esmeraldaData = AssetDatabase.LoadAssetAtPath<ShipDataSO>(ProjectPaths.ShipData + "/Ship_" + ShipCatalog.EsmeraldaId + ".asset");
            if (huascarData == null || esmeraldaData == null) throw new InvalidOperationException("No se generaron los ShipDataSO.");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavalShell shellPrefab = IquiquePrototypeBuilder.CreateShellPrefab();
            IquiquePrototypeBuilder.CreateEnvironment();
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            ShipController huascar = IquiquePrototypeBuilder.CreateHuascar(huascarData, shellPrefab, new Vector3(0f, 0f, 0f), 0f);
            ShipController esmeralda = IquiquePrototypeBuilder.CreateEsmeralda(esmeraldaData, shellPrefab, huascar, new Vector3(160f, 0f, 0f), 0f);

            Vector3 h = huascar.transform.position;
            Vector3 e = esmeralda.transform.position;
            Shoot(folder, "1_huascar_costado", h + new Vector3(-75f, 8f, 0f), h + new Vector3(0f, 4f, 0f), 50f);
            Shoot(folder, "2_huascar_tres_cuartos", h + new Vector3(-55f, 22f, 60f), h + new Vector3(0f, 3f, 0f), 50f);
            Shoot(folder, "3_huascar_camara_de_juego", h + new Vector3(0f, 30f, -90f), h + new Vector3(0f, 2f, 20f), 60f);
            Shoot(folder, "4_esmeralda_costado", e + new Vector3(110f, 18f, 0f), e + new Vector3(0f, 14f, 0f), 50f);
            Shoot(folder, "5_esmeralda_tres_cuartos", e + new Vector3(80f, 30f, 80f), e + new Vector3(0f, 10f, 0f), 50f);
            Shoot(folder, "6_los_dos", (h + e) * 0.5f + new Vector3(0f, 45f, -170f), (h + e) * 0.5f + new Vector3(0f, 5f, 0f), 55f);
            Debug.Log("[Pacífico] Fotos de los buques guardadas en " + folder);
        }

        private static void Shoot(string folder, string name, Vector3 from, Vector3 lookAt, float fieldOfView)
        {
            var go = new GameObject("Camara_Foto");
            var camera = go.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from));
            camera.fieldOfView = fieldOfView;
            camera.farClipPlane = 6000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.72f, 0.78f, 0.82f);

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
        }

        private static string ArgumentAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag) return args[i + 1];
            }
            return null;
        }
    }
}
