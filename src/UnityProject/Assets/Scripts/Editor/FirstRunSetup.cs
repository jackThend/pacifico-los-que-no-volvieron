using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.Editor
{
    /// <summary>
    /// Preparación automática al abrir el proyecto: si aún no existe la escena
    /// del prototipo naval, genera los datos históricos y la crea; si existe y
    /// el editor está en una escena vacía sin guardar, la abre. Así basta con
    /// abrir el proyecto y pulsar Play.
    /// </summary>
    [InitializeOnLoad]
    public static class FirstRunSetup
    {
        static FirstRunSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            // Esperar a que termine la importación/compilación inicial.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            try
            {
                var active = EditorSceneManager.GetActiveScene();
                var sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(NavalPrototypeSceneBuilder.ScenePath) != null;

                if (!sceneExists)
                {
                    if (active.isDirty) return; // no pisar trabajo sin guardar
                    Debug.Log("[Pacífico] Primera apertura: generando datos históricos y la escena de Iquique…");
                    HistoricalDataGenerator.GenerateAll();
                    NavalPrototypeSceneBuilder.Create();
                    return;
                }

                if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                {
                    EditorSceneManager.OpenScene(NavalPrototypeSceneBuilder.ScenePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Pacífico] Falló la preparación automática: {exception.Message}\n" +
                               "Puedes crear la escena a mano con Pacífico → Escenas → Crear prototipo naval (Iquique).");
            }
        }
    }
}
