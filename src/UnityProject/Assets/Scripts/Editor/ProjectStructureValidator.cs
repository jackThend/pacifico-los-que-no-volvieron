using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Verifica la estructura de carpetas del proyecto (ROADMAP 0.1) y la presencia del Archivo Histórico.
    /// Invocable desde el menú o en modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.ProjectStructureValidator.RunBatch</c>
    /// </summary>
    public static class ProjectStructureValidator
    {
        [MenuItem("Pacífico/Proyecto/Validar estructura")]
        public static void ValidateFromMenu()
        {
            int problems = Validate();
            if (problems == 0) Debug.Log("[Pacífico] Estructura del proyecto correcta.");
            else Debug.LogError("[Pacífico] Estructura del proyecto con " + problems + " problema(s). Revisa la consola.");
        }

        public static void RunBatch()
        {
            int problems = Validate();
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        private static int Validate()
        {
            int problems = 0;
            foreach (string folder in ProjectPaths.RequiredFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogError("[Pacífico] Falta la carpeta requerida: " + folder);
                    problems++;
                }
            }

            string archive = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ProjectPaths.HistoricalArchiveFromProjectRoot));
            if (!Directory.Exists(archive))
            {
                Debug.LogWarning("[Pacífico] No se encontró el Archivo Histórico en " + archive +
                                 ". Los importadores de coleccionables no tendrán fuentes.");
            }

            return problems;
        }
    }
}
