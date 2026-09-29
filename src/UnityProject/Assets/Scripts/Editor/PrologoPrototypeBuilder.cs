using System;
using System.IO;
using System.Linq;
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
    /// Genera la cinemática del prólogo «El Ojo de Europa» (ROADMAP 5.2) y una escena que la reproduce. El texto se
    /// lee de <c>Historia_Completa_Guion.md</c>; las imágenes se importan del Archivo Histórico. Si en
    /// <c>Assets/Audio</c> hay una voz <c>vo_prologo_morice</c> o una música <c>mus_prologo_chelo</c>, se asignan.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.PrologoPrototypeBuilder.RunBatch</c>
    /// </summary>
    public static class PrologoPrototypeBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/Proto_Prologo_Ojo_de_Europa.unity";
        public const string AssetPath = ProjectPaths.ScriptableObjects + "/Cinematics/Cinematic_Prologo_Ojo_de_Europa.asset";

        [MenuItem("Pacífico/Prototipos/Construir prólogo «El Ojo de Europa»")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
            EditorUtility.DisplayDialog("Pacífico", "Escena creada en " + ScenePath + "\n\nEspacio: pausa · Esc: saltar.", "Aceptar");
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

        /// <summary>Crea o actualiza el asset de la cinemática a partir del guion y del Archivo.</summary>
        public static CinematicDataSO GenerateAsset()
        {
            string root = HistoricalDataAssetGenerator.RepositoryRoot;
            CinematicScript script = PrologueCinematic.LoadScript(relative => File.ReadAllText(Path.Combine(root, relative)));

            HistoricalDataAssetGenerator.EnsureFolder(AssetPath.Substring(0, AssetPath.LastIndexOf('/')));
            var asset = AssetDatabase.LoadAssetAtPath<CinematicDataSO>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CinematicDataSO>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.CopyFrom(script, PrologueCinematic.ScriptFile);
            asset.images.Clear();
            foreach (string path in PrologueCinematic.ShotsByParagraph.SelectMany(p => p).Select(s => s.Image).Distinct())
            {
                asset.images.Add(new CinematicDataSO.ArchiveImage { path = path, texture = HistoricalDataAssetGenerator.ImportArchiveTexture(root, path) });
            }
            asset.narration = FindAudio("vo_prologo_morice") ?? asset.narration;
            asset.music = FindAudio("mus_prologo_chelo") ?? asset.music;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static AudioClip FindAudio(string name)
        {
            string guid = AssetDatabase.FindAssets(name + " t:AudioClip", new[] { ProjectPaths.Audio }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
        }

        internal static void BuildScene()
        {
            CinematicDataSO asset = GenerateAsset();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Camara");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            var cinematic = new GameObject("Cinematica_Prologo");
            cinematic.AddComponent<CinematicPlayer>().Cinematic = asset;
            cinematic.AddComponent<Pacifico.Campaign.PrologueCampaignLink>(); // al terminar, pasa al capítulo 1

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSceneKit.AddToBuildSettings(ScenePath);
            Debug.Log("[Pacífico] Prólogo guardado en " + ScenePath);
        }
    }
}
