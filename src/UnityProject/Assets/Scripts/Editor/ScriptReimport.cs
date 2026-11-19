using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Reparación: vuelve a importar todos los scripts. Sirve cuando Unity avisa «No script asset for …» tras una
    /// compilación fallida y guarda los ScriptableObjects con <c>m_Script: {fileID: 0}</c> (le pasó el 2026-11-19).
    /// En modo batch: <c>-executeMethod Pacifico.EditorTools.ScriptReimport.RunBatch</c>.
    /// </summary>
    public static class ScriptReimport
    {
        [MenuItem("Pacífico/Mantenimiento/Reimportar scripts")]
        public static void Run()
        {
            AssetDatabase.ImportAsset("Assets/Scripts", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Pacífico] Scripts reimportados.");
        }

        public static void RunBatch()
        {
            Run();
            EditorApplication.Exit(0);
        }
    }
}
