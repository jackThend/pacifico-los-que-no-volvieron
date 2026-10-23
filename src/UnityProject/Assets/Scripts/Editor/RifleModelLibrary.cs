using System.Collections.Generic;
using System.IO;
using Pacifico.Core.Weapons;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fusiles CC-BY de Sketchfab (créditos en <c>Assets/Art/CCBY/CREDITOS.md</c>) para la vista en primera persona.
    /// El modelo sustituye a la culata, el cajón, el guardamano y el cañón de primitivas; la boca, el alza, el punto de
    /// mira, la palanca, el cartucho y la bayoneta siguen siendo los de siempre porque el juego los mueve o apunta con
    /// ellos. Sin el archivo o sin glTFast, el fusil se queda con sus primitivas.
    /// </summary>
    internal static class RifleModelLibrary
    {
        /// <summary>Encaje de un modelo de fusil en el nodo «Mecanica» de la vista.</summary>
        public struct Fit
        {
            public string Model;
            /// <summary>Longitud total del arma sin bayoneta (m).</summary>
            public float LengthM;
            /// <summary>True si, alineado a lo largo de Z, la boca del modelo queda hacia −Z y hay que girarlo 180°.</summary>
            public bool MuzzleTowardsMinusZ;
            /// <summary>Altura del ánima junto a la boca, medida desde lo más alto del modelo, en fracción de su alto.</summary>
            public float BoreFromTop;
        }

        // Longitudes aproximadas sin bayoneta. BoreFromTop medido en los GLB junto a la boca (valores de ajuste).
        private static readonly Dictionary<string, Fit> ByWeapon = new Dictionary<string, Fit>
        {
            // El Martini-Henry tiene el mismo bloque basculante accionado por palanca que el Comblain II (1,18 m).
            [WeaponCatalog.ComblainId] = new Fit { Model = "fusil_martini_henry", LengthM = 1.18f, MuzzleTowardsMinusZ = false, BoreFromTop = 0.103f },
            [WeaponCatalog.ChassepotId] = new Fit { Model = "fusil_chassepot_1866", LengthM = 1.31f, MuzzleTowardsMinusZ = false, BoreFromTop = 0.127f },
            // El Gras de 1874 es la conversión del Chassepot a cartucho metálico: misma silueta.
            [WeaponCatalog.GrasId] = new Fit { Model = "fusil_chassepot_1866", LengthM = 1.305f, MuzzleTowardsMinusZ = false, BoreFromTop = 0.127f },
        };

        /// <summary>
        /// Instancia bajo <paramref name="body"/> el modelo del arma <paramref name="weaponId"/>, a su longitud real, con
        /// la boca en <paramref name="muzzleLocal"/> y el ánima a esa altura. Null si el arma no tiene modelo.
        /// </summary>
        public static GameObject Dress(Transform body, string weaponId, Vector3 muzzleLocal)
        {
            if (weaponId == null || !ByWeapon.TryGetValue(weaponId, out Fit fit)) return null;
            string path = ShipModelLibrary.Root + "/" + fit.Model + ".glb";
            if (!File.Exists(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "Modelo_" + fit.Model;
            model.transform.SetParent(body, false);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);

            Bounds bounds = ShipModelLibrary.LocalBounds(model, body);
            if (bounds.size.x > bounds.size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            if (fit.MuzzleTowardsMinusZ) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;

            bounds = ShipModelLibrary.LocalBounds(model, body);
            if (bounds.size.z > 0f) model.transform.localScale *= fit.LengthM / bounds.size.z;

            bounds = ShipModelLibrary.LocalBounds(model, body);
            float boreY = bounds.max.y - fit.BoreFromTop * bounds.size.y;
            model.transform.localPosition += new Vector3(muzzleLocal.x - bounds.center.x, muzzleLocal.y - boreY, muzzleLocal.z - bounds.max.z);

            // Pegado a la cámara: sin sombras propias que manchen el suelo lejano.
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return model;
        }
    }
}
