using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Modelos de buques CC-BY de Sketchfab (créditos en <c>Assets/Art/CCBY/CREDITOS.md</c>), importados como GLB con
    /// glTFast. Sustituyen al casco de primitivas de un buque sin tocar su jugabilidad: las zonas de blindaje, la torre
    /// giratoria y el espolón siguen siendo los objetos de siempre. Si falta el archivo o el importador, los métodos
    /// devuelven null y el buque conserva sus primitivas.
    /// </summary>
    internal static class ShipModelLibrary
    {
        public const string Root = ProjectPaths.ArtCCBY + "/Sketchfab";

        public const string RolfKrake = "rolf_krake_1863";
        public const string Constitution = "uss_constitution_1798";
        public const string ArmstrongGun = "canon_armstrong_40lb";

        /// <summary>Encaje de un modelo en el casco: qué parte de su longitud es casco y hacia dónde mira la proa.</summary>
        public struct Fit
        {
            /// <summary>Fracción de la longitud total del modelo que ocupa el casco (el resto, bauprés y botalones).</summary>
            public float HullFraction;
            /// <summary>Desplazamiento a popa del casco respecto al centro del modelo, en fracción de la longitud del casco.</summary>
            public float HullOffset;
            /// <summary>True si la proa del modelo, una vez alineado, queda hacia −Z y hay que girarlo 180°.</summary>
            public bool BowTowardsMinusZ;
            /// <summary>Calado (m): la quilla queda a esta profundidad bajo la flotación (y = 0).</summary>
            public float DraftM;
            /// <summary>
            /// Nodos del modelo que no corresponden al buque histórico (p. ej. una bandera ajena) y se ocultan. También
            /// valen para las copias que glTFast renombra con sufijo cuando un nombre se repite (<c>lamp_low_0</c>).
            /// </summary>
            public string[] HiddenParts;
        }

        /// <summary>
        /// Instancia el modelo <paramref name="id"/> bajo <paramref name="hull"/>, a la eslora <paramref name="lengthM"/>
        /// y con la quilla a <see cref="Fit.DraftM"/> bajo el agua. Null si no está importado.
        /// </summary>
        public static GameObject Place(string id, string name, Transform hull, float lengthM, Fit fit)
        {
            string path = Root + "/" + id + ".glb";
            if (!File.Exists(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null; // sin glTFast el GLB no es un modelo

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = name;
            model.transform.SetParent(hull, false);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            if (fit.HiddenParts != null)
            {
                foreach (Transform part in model.GetComponentsInChildren<Transform>())
                {
                    if (!IsHidden(part.name, fit.HiddenParts)) continue;
                    foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                }
            }

            // Eslora a lo largo de Z (la proa de los buques del juego es +Z).
            Bounds bounds = LocalBounds(model, hull);
            if (bounds.size.x > bounds.size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            if (fit.BowTowardsMinusZ) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;

            bounds = LocalBounds(model, hull);
            float hullLength = bounds.size.z * Mathf.Clamp(fit.HullFraction, 0.1f, 1f);
            if (hullLength > 0f) model.transform.localScale *= lengthM / hullLength;

            bounds = LocalBounds(model, hull);
            float hullCenterZ = bounds.center.z - fit.HullOffset * lengthM;
            model.transform.localPosition += new Vector3(-bounds.center.x, -fit.DraftM - bounds.min.y, -hullCenterZ);
            return model;
        }

        internal static bool IsHidden(string name, string[] parts)
        {
            foreach (string part in parts)
            {
                if (name == part) return true;
                if (name.StartsWith(part + "_") && int.TryParse(name.Substring(part.Length + 1), out _)) return true;
            }
            return false;
        }

        /// <summary>
        /// Oculta las primitivas hijas de <paramref name="hull"/> que el modelo sustituye, aunque varias compartan nombre
        /// (sin borrarlas: algunas llevan componentes de juego).
        /// </summary>
        public static void HideStandIns(Transform hull, params string[] names)
        {
            foreach (Transform part in hull)
            {
                if (System.Array.IndexOf(names, part.name) >= 0 && part.TryGetComponent(out Renderer renderer)) renderer.enabled = false;
            }
        }

        // Encajes de los modelos de martyn169 y del bote (valores de ajuste a ojo, comprobados con ShipModelPreview).
        // DraftM es lo que se hunde cada modelo para que su obra viva roja quede bajo el agua.

        /// <summary>HDMS Danmark (1864) como Independencia: el 87 % de su longitud es casco; el resto, bauprés a proa.</summary>
        public static readonly Fit DanmarkAsIndependencia = new Fit
        {
            HullFraction = 0.87f, HullOffset = 0.046f, BowTowardsMinusZ = false, DraftM = 4.7f,
        };

        /// <summary>
        /// HDMS Odin (1872), de reducto central, como Cochrane y Blanco Encalada; sin las banderas danesas de proa y popa
        /// (group_247 y group_248).
        /// </summary>
        public static readonly Fit OdinAsCochrane = new Fit
        {
            HullFraction = 0.955f, HullOffset = 0f, BowTowardsMinusZ = false, DraftM = 4.3f,
            HiddenParts = new[] { "group_247", "group_248" },
        };

        /// <summary>
        /// Bote de remos victoriano: sin el farol con su poste; la regala queda a 0,55 m sobre la raíz del bote y las
        /// cabezas de la tropa (a 0,75 m) asoman como de hombres sentados.
        /// </summary>
        public static readonly Fit RowBoat = new Fit
        {
            HullFraction = 0.93f, HullOffset = 0f, BowTowardsMinusZ = false, DraftM = 1.19f,
            HiddenParts = new[] { "lamp_low", "lamppost_low", "lamppostdowel_low", "lampbracket_low" },
        };

        public const string Danmark = "hdms_danmark_1864";
        public const string Odin = "hdms_odin_1872";
        public const string Boat = "bote_victoriano";

        /// <summary>Caja de todas las mallas del modelo en el espacio local de <paramref name="space"/> (null: el mundo).</summary>
        public static Bounds LocalBounds(GameObject model, Transform space)
        {
            bool any = false;
            var bounds = new Bounds();
            Matrix4x4 worldToSpace = space != null ? space.worldToLocalMatrix : Matrix4x4.identity;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = worldToSpace * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x,
                                             (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                                             (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
