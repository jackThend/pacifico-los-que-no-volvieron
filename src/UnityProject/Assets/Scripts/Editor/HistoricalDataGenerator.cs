using Pacifico.Core.Weapons;
using Pacifico.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Pacifico.Editor
{
    /// <summary>
    /// Genera (o actualiza) los ScriptableObjects de datos históricos a partir
    /// de los catálogos de Pacifico.Core, evitando editar YAML a mano.
    /// </summary>
    public static class HistoricalDataGenerator
    {
        private const string WeaponsFolder = "Assets/ScriptableObjects/Weapons";

        [MenuItem("Pacífico/Datos/Generar armas históricas")]
        public static void GenerateWeapons()
        {
            EnsureFolder("Assets/ScriptableObjects", "Weapons");
            foreach (var spec in HistoricalWeapons.All)
            {
                var path = $"{WeaponsFolder}/Weapon_{spec.Id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<WeaponDataSO>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.ApplySpec(spec);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Pacífico] {HistoricalWeapons.All.Count} armas generadas en {WeaponsFolder}.");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
