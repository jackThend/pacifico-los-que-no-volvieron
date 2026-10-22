using System.Collections.Generic;
using System.IO;
using Pacifico.Core.Art;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Modelos y texturas CC0 de Poly Haven (AGENTS.md §4.A; créditos en <c>Assets/Art/CC0/CREDITOS.md</c>) para
    /// vestir las escenas que los constructores levantan con primitivas. Cada recurso vive en
    /// <c>Assets/Art/CC0/PolyHaven/&lt;id&gt;/</c>: color, normales y ARM (oclusión, rugosidad, metal) a 2K y, si es un
    /// modelo, su FBX. Si falta un archivo, los métodos devuelven null o false y quien llama conserva la primitiva.
    /// </summary>
    internal static class ArtLibrary
    {
        public const string Root = ProjectPaths.ArtCC0 + "/PolyHaven";
        private const string Resolution = "2k";

        // Texturas.
        public const string BlackPaintedPlanks = "black_painted_planks";
        public const string BrownPlanks = "brown_planks_07";
        public const string MetalPlate = "metal_plate_02";
        public const string GravellySand = "gravelly_sand";
        public const string CliffSide = "cliff_side";

        // Modelos.
        public const string Cannon = "cannon_01";
        public const string WoodenCrate = "wooden_crate_02";
        public const string AridCliffSmall = "namaqualand_cliff_01";
        public const string AridCliffLarge = "namaqualand_cliff_02";
        public const string Mountainside = "mountainside";
        public const string Boulders = "namaqualand_boulders_01";

        /// <summary>Normal media de cada modelo en su espacio local (las rocas escaneadas tienen una cara).</summary>
        private static readonly Dictionary<string, Vector3> MeanNormals = new Dictionary<string, Vector3>();

        private static string Folder(string id) => Root + "/" + id;
        private static string TexturePath(string id, string map) => Folder(id) + "/" + id + "_" + map + "_" + Resolution + ".jpg";
        private static string MaskPath(string id) => Folder(id) + "/" + id + "_urp_mask_" + Resolution + ".png";
        private static string ModelPath(string id) => Folder(id) + "/" + id + ".fbx";

        /// <summary>Material URP Lit con las tres texturas del recurso, guardado como asset y reutilizado.</summary>
        public static Material Surface(string id, Color tint)
        {
            if (!File.Exists(TexturePath(id, "diff"))) return null;
            string key = tint == Color.white ? id : id + "_" + ColorUtility.ToHtmlStringRGB(tint);
            string path = ProjectPaths.Materials + "/CC0_" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            Texture2D albedo = LoadTexture(TexturePath(id, "diff"), srgb: true, normalMap: false);
            Texture2D normal = LoadTexture(TexturePath(id, "nor_gl"), srgb: false, normalMap: true);
            Texture2D mask = Mask(id);

            material.SetTexture(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (mask != null && material.HasProperty("_MetallicGlossMap"))
            {
                // Máscara URP: metal en R, oclusión en G y lisura en A (ver Mask).
                material.SetTexture("_MetallicGlossMap", mask);
                material.EnableKeyword(material.HasProperty("_BaseMap") ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
                if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", 1f);
                if (material.HasProperty("_OcclusionMap"))
                {
                    material.SetTexture("_OcclusionMap", mask);
                    material.EnableKeyword("_OCCLUSIONMAP");
                }
            }
            HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Viste una primitiva con la textura <paramref name="id"/> repetida cada <paramref name="tileMeters"/> metros
        /// (proyección en caja sobre una copia de la malla). False si la textura no está.
        /// </summary>
        public static bool Dress(GameObject primitive, string id, float tileMeters, Color tint)
        {
            Material material = Surface(id, tint);
            var filter = primitive.GetComponent<MeshFilter>();
            if (material == null || filter == null || filter.sharedMesh == null) return false;

            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = primitive.name + "_CC0";
            Vector3 scale = primitive.transform.lossyScale;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = Vector3.Scale(vertices[i], scale);
                Vector3 n = normals[i];
                ModelFitting.BoxUv(p.x, p.y, p.z, n.x, n.y, n.z, tileMeters, out float u, out float v);
                uv[i] = new Vector2(u, v);
            }
            mesh.uv = uv;
            mesh.RecalculateTangents();
            filter.sharedMesh = mesh;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return true;
        }

        /// <summary>Instancia el modelo <paramref name="id"/> con su material, sin colisionadores. Null si no está.</summary>
        public static GameObject Model(string id, string name, Transform parent)
        {
            string path = ModelPath(id);
            if (!File.Exists(path)) return null;
            ConfigureModelImporter(path);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Material material = Surface(id, Color.white);
            if (asset == null || material == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = name;
            if (parent != null) instance.transform.SetParent(parent, false);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            return instance;
        }

        /// <summary>Caja que envuelve todos los renderers del objeto, en coordenadas de mundo.</summary>
        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Escala uniforme para que el modelo mida <paramref name="height"/> metros de alto.</summary>
        public static void FitHeight(GameObject go, float height)
        {
            go.transform.localScale *= ModelFitting.ScaleToHeight(WorldBounds(go).size.y, height);
        }

        /// <summary>Gira el modelo sobre la vertical para que su dirección <paramref name="worldFrom"/> apunte a <paramref name="worldTo"/>.</summary>
        public static void Face(GameObject go, Vector3 worldFrom, Vector3 worldTo)
        {
            go.transform.Rotate(0f, ModelFitting.YawToFace(worldFrom.x, worldFrom.z, worldTo.x, worldTo.z), 0f, Space.World);
        }

        /// <summary>
        /// Dirección hacia la que mira el modelo, en mundo: la normal media de sus caras (la cara escaneada de una
        /// roca). Se calcula una vez por modelo.
        /// </summary>
        public static Vector3 FacingDirection(GameObject go, string id)
        {
            if (!MeanNormals.TryGetValue(id, out Vector3 local))
            {
                Vector3 sum = Vector3.zero;
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    Vector3[] v = mesh.vertices;
                    int[] t = mesh.triangles;
                    Matrix4x4 toRoot = go.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        Vector3 a = toRoot.MultiplyPoint3x4(v[t[i]]);
                        sum += Vector3.Cross(toRoot.MultiplyPoint3x4(v[t[i + 1]]) - a, toRoot.MultiplyPoint3x4(v[t[i + 2]]) - a);
                    }
                }
                local = sum;
                MeanNormals[id] = local;
            }
            return go.transform.TransformDirection(local);
        }

        /// <summary>Dirección de la boca del cañón, en mundo: del centro del modelo al centro del ánima.</summary>
        public static Vector3 MuzzleDirection(GameObject gun)
        {
            Bounds all = WorldBounds(gun);
            foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name.ToLowerInvariant().Contains("barrel")) return renderer.bounds.center - all.center;
            }
            return gun.transform.forward;
        }

        private static Texture2D LoadTexture(string path, bool srgb, bool normalMap)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
            bool changed = false;
            if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (!normalMap && importer.sRGBTexture != srgb)
            {
                importer.sRGBTexture = srgb;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Convierte el ARM de Poly Haven (R oclusión, G rugosidad, B metal) en la máscara que espera URP Lit (R metal,
        /// G oclusión, A lisura). Se genera una vez junto al original.
        /// </summary>
        private static Texture2D Mask(string id)
        {
            string maskPath = MaskPath(id);
            if (!File.Exists(maskPath))
            {
                string armPath = TexturePath(id, "arm");
                if (!(AssetImporter.GetAtPath(armPath) is TextureImporter arm)) return null;
                if (!arm.isReadable || arm.sRGBTexture || arm.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    arm.isReadable = true;
                    arm.sRGBTexture = false;
                    arm.textureCompression = TextureImporterCompression.Uncompressed;
                    arm.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(armPath);
                if (source == null) return null;
                Color32[] pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    pixels[i] = new Color32(p.b, p.r, 0, (byte)(255 - p.g));
                }
                var mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                mask.SetPixels32(pixels);
                mask.Apply();
                File.WriteAllBytes(maskPath, mask.EncodeToPNG());
                Object.DestroyImmediate(mask);
                AssetDatabase.ImportAsset(maskPath);
            }
            return LoadTexture(maskPath, srgb: false, normalMap: false);
        }

        /// <summary>Sin materiales importados (se asignan los nuestros) y con la malla legible para orientarla.</summary>
        private static void ConfigureModelImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && importer.isReadable) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
