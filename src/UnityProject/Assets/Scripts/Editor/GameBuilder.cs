using System;
using System.IO;
using System.Linq;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Data;
using Pacifico.Narrative;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Empaquetado final (ROADMAP 6.6): construye el menú principal y todas las escenas de la campaña a partir del guion
    /// y del Archivo, deja la build con esas escenas en el orden de <see cref="CampaignCatalog"/> (sin los prototipos) y
    /// genera el ejecutable. Todo se puede lanzar en modo batch, p. ej.:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.GameBuilder.BuildLinuxBatch</c>
    /// Para medir el rendimiento de la build: <c>Pacifico.x86_64 -pacifico-perf [-pacifico-perf-seconds 60]</c>.
    /// </summary>
    public static class GameBuilder
    {
        public const string MenuScenePath = ProjectPaths.Scenes + "/" + CampaignCatalog.MenuScene + ".unity";
        public const string ProductName = "Pacífico: Los que no volvieron";
        public const string Version = "0.6.0";
        private const string MenuBackdrop = "Archivo_Historico/01_Barcos_y_Combate_Naval/03_Combate_Naval_de_Iquique_Somerscales.jpg";

        public static string ScenePath(string sceneName) => ProjectPaths.Scenes + "/" + sceneName + ".unity";

        [MenuItem("Pacífico/Construir/Todas las escenas de la campaña")]
        public static void BuildAllScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAllScenes();
            EditorUtility.DisplayDialog("Pacífico", "Menú y " + (CampaignCatalog.BuildScenes().Count() - 1) + " escenas de la campaña construidos; la build queda en el orden de la campaña.", "Aceptar");
        }

        [MenuItem("Pacífico/Construir/Build de Windows (64 bits)")]
        public static void BuildWindowsMenu() => BuildPlayer(BuildTarget.StandaloneWindows64);

        [MenuItem("Pacífico/Construir/Build de Linux (64 bits)")]
        public static void BuildLinuxMenu() => BuildPlayer(BuildTarget.StandaloneLinux64);

        [MenuItem("Pacífico/Construir/Build de macOS")]
        public static void BuildMacMenu() => BuildPlayer(BuildTarget.StandaloneOSX);

        public static void BuildAllScenesBatch() => Batch(BuildAllScenes);
        public static void BuildWindowsBatch() => Batch(() => { BuildAllScenes(); BuildPlayer(BuildTarget.StandaloneWindows64); });
        public static void BuildLinuxBatch() => Batch(() => { BuildAllScenes(); BuildPlayer(BuildTarget.StandaloneLinux64); });
        public static void BuildMacBatch() => Batch(() => { BuildAllScenes(); BuildPlayer(BuildTarget.StandaloneOSX); });

        private static void Batch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Construye el menú y cada capítulo, y fija la lista de escenas de la build.</summary>
        public static void BuildAllScenes()
        {
            HistoricalDataAssetGenerator.GenerateAll();
            PrologoPrototypeBuilder.BuildScene();
            Capitulo1IquiqueBuilder.BuildScene();
            Capitulo4TarapacaBuilder.BuildScene();
            Capitulo5AltoDeLaAlianzaBuilder.BuildScene();
            Capitulo6MorroDeAricaBuilder.BuildScene();
            Capitulo8MirafloresBuilder.BuildScene();
            BuildMenuScene();
            SetCampaignBuildSettings();
        }

        /// <summary>Solo las escenas de la campaña, en su orden: el menú primero (es la que abre el juego).</summary>
        public static void SetCampaignBuildSettings()
        {
            EditorBuildSettings.scenes = CampaignCatalog.BuildScenes()
                .Select(name => new EditorBuildSettingsScene(ScenePath(name), true))
                .ToArray();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!File.Exists(scene.path)) throw new InvalidOperationException("Falta la escena " + scene.path + ": construye primero las escenas de la campaña.");
            }
        }

        /// <summary>Menú principal: cámara, visor de documentos y los coleccionables de «La memoria rota».</summary>
        internal static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Camara_Menu");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            CollectibleDataSO[] collectibles = AssetDatabase.FindAssets("t:CollectibleDataSO", new[] { ProjectPaths.CollectibleData })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CollectibleDataSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null)
                .OrderBy(c => c.chapter).ThenBy(c => c.id, StringComparer.Ordinal)
                .ToArray();
            var viewer = new GameObject("Visor_de_Documentos").AddComponent<DocumentViewer>();
            Texture2D backdrop = HistoricalDataAssetGenerator.ImportArchiveTexture(HistoricalDataAssetGenerator.RepositoryRoot, MenuBackdrop);
            new GameObject("Menu_Principal").AddComponent<MainMenu>().Configure(collectibles, viewer, backdrop);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log("[Pacífico] Menú principal guardado en " + MenuScenePath);
        }

        /// <summary>Genera el ejecutable en <c>Builds/&lt;plataforma&gt;/</c> (fuera de <c>Assets</c>, ignorado por git).</summary>
        public static BuildReport BuildPlayer(BuildTarget target)
        {
            SetCampaignBuildSettings();
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            string folder = Path.Combine("Builds", target.ToString());
            string file = target == BuildTarget.StandaloneWindows64 ? "Pacifico.exe" : target == BuildTarget.StandaloneOSX ? "Pacifico.app" : "Pacifico.x86_64";
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(folder, file),
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[Pacífico] Build " + target + ": " + summary.result + " · " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB · " +
                      summary.totalTime.TotalSeconds.ToString("0") + " s · " + summary.totalErrors + " errores, " + summary.totalWarnings + " avisos");
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("La build de " + target + " ha fallado: " + summary.result);
            return report;
        }
    }
}
