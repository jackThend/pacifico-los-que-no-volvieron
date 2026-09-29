using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Preparación automática al abrir el proyecto: si aún no existe el menú principal, genera los datos históricos,
    /// construye el menú y todas las escenas de la campaña (<see cref="GameBuilder.BuildAllScenes"/>) y lo abre; si ya
    /// existe y el editor está en una escena vacía sin guardar, abre el menú. Así basta con abrir el proyecto y pulsar Play.
    /// </summary>
    [InitializeOnLoad]
    public static class FirstRunSetup
    {
        private const string DoneThisSession = "Pacifico.FirstRunSetup.Done";

        static FirstRunSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(DoneThisSession, false)) return;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            // Esperar a que termine la importación y la compilación inicial.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(DoneThisSession, true);

            try
            {
                Scene active = EditorSceneManager.GetActiveScene();
                bool menuExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameBuilder.MenuScenePath) != null;
                if (!menuExists)
                {
                    if (active.isDirty) return; // no pisar trabajo sin guardar
                    Debug.Log("[Pacífico] Primera apertura: construyendo el menú y los capítulos de la campaña…");
                    GameBuilder.BuildAllScenes();
                    EditorSceneManager.OpenScene(GameBuilder.MenuScenePath);
                    return;
                }
                if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(GameBuilder.MenuScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Pacífico] Falló la preparación automática: " + exception.Message +
                               "\nPuedes construirla a mano con Pacífico → Construir → Todas las escenas de la campaña.");
            }
        }
    }
}
