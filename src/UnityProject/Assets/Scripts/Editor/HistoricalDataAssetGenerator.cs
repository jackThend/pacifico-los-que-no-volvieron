using System;
using System.Collections.Generic;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Genera (o actualiza en su sitio) los ScriptableObjects de datos a partir de los catálogos de Pacifico.Core.
    /// Es idempotente: si el asset ya existe se sobrescriben sus valores conservando el GUID y las referencias.
    /// Modo batch:
    /// <c>Unity -batchmode -quit -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.HistoricalDataAssetGenerator.RunBatch</c>
    /// </summary>
    public static partial class HistoricalDataAssetGenerator
    {
        [MenuItem("Pacífico/Datos/Generar ScriptableObjects históricos")]
        public static void GenerateAll()
        {
            var report = new List<string>();
            GenerateWeapons(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pacífico] Datos generados:\n  " + string.Join("\n  ", report));
        }

        public static void RunBatch()
        {
            try
            {
                GenerateAll();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void GenerateWeapons(List<string> report)
        {
            foreach (WeaponSpec spec in WeaponCatalog.All())
            {
                var asset = LoadOrCreate<WeaponDataSO>(ProjectPaths.WeaponData, "Weapon_" + spec.Id);
                asset.CopyFrom(spec);
                Finish(asset, spec.Validate().IsValid, report);
            }
        }

        internal static T LoadOrCreate<T>(string folder, string assetName) where T : ScriptableObject
        {
            EnsureFolder(folder);
            string path = folder + "/" + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static void Finish(ScriptableObject asset, bool valid, List<string> report)
        {
            EditorUtility.SetDirty(asset);
            string path = AssetDatabase.GetAssetPath(asset);
            report.Add((valid ? "[OK] " : "[INVÁLIDO] ") + path);
            if (!valid) Debug.LogWarning("[Pacífico] Datos inválidos en " + path, asset);
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
