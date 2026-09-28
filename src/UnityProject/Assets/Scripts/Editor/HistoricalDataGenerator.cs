using System;
using System.Collections.Generic;
using Pacifico.Core.Ships;
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
        private const string Root = "Assets/ScriptableObjects";

        [MenuItem("Pacífico/Datos/Generar armas históricas")]
        public static void GenerateWeapons()
        {
            Generate<WeaponSpec, WeaponDataSO>("Weapons", "Weapon", HistoricalWeapons.All,
                s => s.Id, (asset, spec) => asset.ApplySpec(spec));
        }

        [MenuItem("Pacífico/Datos/Generar buques históricos")]
        public static void GenerateShips()
        {
            Generate<ShipSpec, ShipDataSO>("Ships", "Ship", HistoricalShips.All,
                s => s.Id, (asset, spec) => asset.ApplySpec(spec));
        }

        [MenuItem("Pacífico/Datos/Generar todo")]
        public static void GenerateAll()
        {
            GenerateWeapons();
            GenerateShips();
        }

        private static void Generate<TSpec, TAsset>(
            string folder,
            string prefix,
            IReadOnlyList<TSpec> specs,
            Func<TSpec, string> idOf,
            Action<TAsset, TSpec> apply)
            where TAsset : ScriptableObject
        {
            if (!AssetDatabase.IsValidFolder($"{Root}/{folder}"))
            {
                AssetDatabase.CreateFolder(Root, folder);
            }

            foreach (var spec in specs)
            {
                var path = $"{Root}/{folder}/{prefix}_{idOf(spec)}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<TAsset>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                apply(asset, spec);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Pacífico] {specs.Count} assets generados en {Root}/{folder}.");
        }
    }
}
