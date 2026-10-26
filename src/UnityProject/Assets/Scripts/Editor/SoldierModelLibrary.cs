using System;
using System.Collections.Generic;
using System.IO;
using Pacifico.Core.Common;
using Pacifico.Infantry;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Soldados CC-BY de Sketchfab (créditos en <c>Assets/Art/CCBY/CREDITOS.md</c>) con esqueleto propio: los modelos
    /// vienen sin huesos, así que aquí se unen sus piezas en una malla, se aligera, se le pone un esqueleto humano en
    /// los puntos medidos de cada modelo (cadera, rodillas, hombros, codos…) y se reparte el peso de cada hueso sobre
    /// los vértices según su distancia. El resultado es un prefab con <see cref="SoldierFigure"/>, que lo mueve por
    /// código, y el fusil de su bando. Sin el archivo o sin glTFast no hay prefab y el soldado sigue de primitivas.
    /// </summary>
    internal static class SoldierModelLibrary
    {
        public const string Folder = ProjectPaths.ArtCCBY + "/Soldados";

        /// <summary>Triángulos como mucho por soldado: hay decenas a la vez en pantalla (valor de ajuste).</summary>
        private const int TriangleBudget = 24000;

        /// <summary>
        /// Encaje de un modelo. Las alturas (Y) y separaciones laterales (X) van en fracción de la altura total, con
        /// los pies en 0 y el quepis en 1, medidas sobre el modelo.
        /// </summary>
        private sealed class Fit
        {
            public string Model;
            public string Prefab;
            public float HeightM;
            /// <summary>Pieza que está delante del cuerpo (para saber hacia dónde mira el modelo).</summary>
            public string ForwardPart;
            /// <summary>Triángulos de esa pieza que se quitan (p. ej. el fusil que trae en la mano).</summary>
            public Func<string, Vector3, bool> Drop;
            public string Rifle;
            public float RifleLengthM;
            public float HipY, HipX, KneeY, AnkleY, ToeY, ToeZ;
            public float SpineY, ChestY, NeckY, HeadY;
            public float ShoulderX, ShoulderY, ElbowX, ElbowY, WristX, WristY, HandX, HandY;
            /// <summary>Prenda de un triángulo (material, altura en fracción, hueso dominante): ver <see cref="SoldierFigure.PartCoat"/>.</summary>
            public Func<string, float, SoldierFigure.Bone, int> Part = (material, y, bone) => 0;
            /// <summary>Materiales del equipo militar que se quitan para vestir civiles (mochila, correaje, charreteras…).</summary>
            public string[] Gear = new string[0];
        }

        // Puntos medidos sobre los GLB (vistas de frente y de perfil); valores de ajuste.
        private static readonly Fit Chilean = new Fit
        {
            // Soldado de la Unión: quepis, casaca y pantalón azules, como la infantería chilena de línea.
            Model = "soldado_union", Prefab = "Soldado_Chileno", HeightM = 1.78f, ForwardPart = "Kit1",
            // El lote «Kit1» trae la mochila (detrás) y un fusil cogido por la derecha: el fusil se quita.
            Drop = (part, p) => part == "Kit1" && (p.z > 0.12f || p.z < -0.18f || Mathf.Abs(p.x) > 0.27f),
            Rifle = "fusil_martini_henry", RifleLengthM = 1.18f,
            // Casaca y pantalón comparten material: se separan por hueso; el quepis, por altura en la cabeza.
            Part = (material, y, bone) =>
                material.StartsWith("Union_fig", StringComparison.Ordinal)
                    ? (IsLeg(bone) && !bone.ToString().EndsWith("Foot", StringComparison.Ordinal) && y > 0.07f ? SoldierFigure.PartTrousers
                       : !IsLeg(bone) && !bone.ToString().EndsWith("Hand", StringComparison.Ordinal) ? SoldierFigure.PartCoat : 0)
                    : material.StartsWith("Head_hands", StringComparison.Ordinal) && y > 0.925f ? SoldierFigure.PartCap : 0,
            HipY = 0.52f, HipX = 0.06f, KneeY = 0.275f, AnkleY = 0.054f, ToeY = 0.02f, ToeZ = 0.07f,
            SpineY = 0.61f, ChestY = 0.72f, NeckY = 0.83f, HeadY = 0.88f,
            ShoulderX = 0.097f, ShoulderY = 0.8f, ElbowX = 0.216f, ElbowY = 0.692f, WristX = 0.324f, WristY = 0.592f, HandX = 0.39f, HandY = 0.514f,
        };

        private static readonly Fit Peruvian = new Fit
        {
            // Infante francés de 1870: el ejército peruano vestía y se instruía a la francesa.
            Model = "soldado_frances_1870", Prefab = "Soldado_Peruano", HeightM = 1.8f, ForwardPart = "Eyeball1_1_Eyeball1_10_0",
            Drop = null,
            Rifle = "fusil_remington_rolling_block", RifleLengthM = 1.25f,
            // Cada prenda es su propio material.
            Part = (material, y, bone) =>
                material.StartsWith("Jacket0", StringComparison.Ordinal) ? SoldierFigure.PartCoat
                : material.StartsWith("Trousers", StringComparison.Ordinal) ? SoldierFigure.PartTrousers
                : material.StartsWith("Cap0", StringComparison.Ordinal) ? SoldierFigure.PartCap
                : material.StartsWith("Jacket1", StringComparison.Ordinal) ? SoldierFigure.PartFacings : 0,
            Gear = new[] { "Cap1", "Epps", "Belt", "Blanket", "Knapsack", "Pouches", "Cross_strap" },
            HipY = 0.475f, HipX = 0.07f, KneeY = 0.225f, AnkleY = 0.05f, ToeY = 0.012f, ToeZ = 0.07f,
            SpineY = 0.57f, ChestY = 0.687f, NeckY = 0.838f, HeadY = 0.9f,
            ShoulderX = 0.113f, ShoulderY = 0.814f, ElbowX = 0.217f, ElbowY = 0.715f, WristX = 0.311f, WristY = 0.611f, HandX = 0.377f, HandY = 0.536f,
        };

        private static Fit FitFor(Faction faction) =>
            faction == Faction.Chile ? Chilean : faction == Faction.Peru || faction == Faction.Bolivia ? Peruvian : null;

        /// <summary>Prefab del soldado de ese bando (lo crea si falta). Null para neutrales o si falta el modelo.</summary>
        public static GameObject Prefab(Faction faction)
        {
            Fit fit = FitFor(faction);
            if (fit == null) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(fit));
            return prefab != null ? prefab : Build(fit);
        }

        /// <summary>Rehace los prefabs de todos los bandos (al reconstruir la campaña, para recoger cambios de ajuste).</summary>
        public static void RebuildAll()
        {
            Variants.Clear();
            AssetDatabase.DeleteAsset(Folder + "/Variantes");
            Build(Chilean);
            Build(Peruvian);
        }

        private static string PrefabPath(Fit fit) => Folder + "/" + fit.Prefab + ".prefab";

        // --------------------------------------------------------------------------------------------------------
        // Malla
        // --------------------------------------------------------------------------------------------------------

        private sealed class Merged
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector4> Tangents = new List<Vector4>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<List<int>> Triangles = new List<List<int>>();
        }

        private static GameObject Build(Fit fit)
        {
            string path = ShipModelLibrary.Root + "/" + fit.Model + ".glb";
            if (!File.Exists(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            Merged merged = Merge(asset, fit);
            Mesh mesh = ToMesh(merged, fit.Prefab + "_Malla");
            mesh = Simplify(mesh, TriangleBudget);

            HistoricalDataAssetGenerator.EnsureFolder(Folder);
            var root = new GameObject(fit.Prefab);
            try
            {
                Transform[] bones = CreateSkeleton(root.transform, fit);
                BoneWeight[] weights = Weigh(mesh.vertices, fit, bones, root.transform);
                mesh.boneWeights = weights;
                int[] parts = SplitParts(mesh, merged.Materials, weights, fit, out List<Material> partMaterials);
                var bindposes = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
                mesh.bindposes = bindposes;
                mesh.RecalculateBounds();

                string meshPath = Folder + "/" + fit.Prefab + "_Malla.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var skin = new GameObject("Cuerpo").AddComponent<SkinnedMeshRenderer>();
                skin.transform.SetParent(root.transform, false);
                skin.sharedMesh = mesh;
                skin.sharedMaterials = partMaterials.ToArray();
                skin.bones = bones;
                skin.rootBone = bones[(int)SoldierFigure.Bone.Hips];
                skin.updateWhenOffscreen = false;
                // Margen para las poses (rodilla en tierra, brazos encarando).
                Bounds local = mesh.bounds;
                local.center -= bones[(int)SoldierFigure.Bone.Hips].position;
                local.Expand(0.8f);
                skin.localBounds = local;

                Transform rifle = CreateRifle(root.transform, fit);
                root.AddComponent<SoldierFigure>().Setup(bones, rifle, skin, parts);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(fit));
                Debug.Log("[Pacífico] Soldado con esqueleto: " + PrefabPath(fit) + " (" + mesh.vertexCount + " vértices, " + TriangleCount(mesh) + " triángulos)");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Separa cada submalla en prendas (casaca, pantalón, quepis y resto) para poder teñir el uniforme de cada
        /// unidad sin tocar la cara, las manos, las botas ni el equipo. Devuelve la prenda de cada submalla nueva.
        /// </summary>
        private static int[] SplitParts(Mesh mesh, List<Material> materials, BoneWeight[] weights, Fit fit, out List<Material> partMaterials)
        {
            Vector3[] vertices = mesh.vertices;
            var groups = new List<(int sub, int part, List<int> triangles)>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                string name = materials[s] != null ? materials[s].name : "";
                int[] triangles = mesh.GetTriangles(s);
                var byPart = new Dictionary<int, List<int>>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    float y = (vertices[a].y + vertices[b].y + vertices[c].y) / 3f / fit.HeightM;
                    int part = fit.Part(name, y, (SoldierFigure.Bone)weights[a].boneIndex0);
                    if (!byPart.TryGetValue(part, out List<int> list)) byPart[part] = list = new List<int>();
                    list.Add(a);
                    list.Add(b);
                    list.Add(c);
                }
                foreach (KeyValuePair<int, List<int>> entry in byPart) groups.Add((s, entry.Key, entry.Value));
            }
            mesh.subMeshCount = groups.Count;
            partMaterials = new List<Material>();
            var parts = new int[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                mesh.SetTriangles(groups[i].triangles, i, false);
                partMaterials.Add(materials[groups[i].sub]);
                parts[i] = groups[i].part;
            }
            return parts;
        }

        // --------------------------------------------------------------------------------------------------------
        // Uniformes de cada unidad y civiles
        // --------------------------------------------------------------------------------------------------------

        private static readonly Dictionary<string, GameObject> Variants = new Dictionary<string, GameObject>();

        /// <summary>
        /// Pone en <paramref name="look"/> el soldado de ese bando con el uniforme teñido de sus colores (casaca, pantalón
        /// y quepis). Con <paramref name="civilian"/>, el modelo peruano sin equipo militar ni fusil, con la ropa y la gorra de
        /// esos colores (civiles de Miraflores, culíes de Cañete).
        /// </summary>
        public static void Dress(Pacifico.Campaign.SoldierLook look, Faction faction, bool civilian = false)
        {
            if (look == null) return;
            look.model = Prefab(civilian ? Faction.Peru : faction, ColorOf(look.uniform), ColorOf(look.trousers), ColorOf(look.kepi), civilian);
        }

        /// <summary>Variante del soldado del bando con esos colores de uniforme (la crea si falta en esta construcción).</summary>
        public static GameObject Prefab(Faction faction, Color coat, Color trousers, Color cap, bool civilian = false)
        {
            Fit fit = FitFor(faction);
            GameObject basePrefab = Prefab(faction);
            if (fit == null || basePrefab == null) return basePrefab;
            string key = fit.Prefab + (civilian ? "_Civil_" : "_") + ColorUtility.ToHtmlStringRGB(coat) + ColorUtility.ToHtmlStringRGB(trousers) + ColorUtility.ToHtmlStringRGB(cap);
            if (Variants.TryGetValue(key, out GameObject cached) && cached != null) return cached;

            HistoricalDataAssetGenerator.EnsureFolder(Folder + "/Variantes");
            string path = Folder + "/Variantes/" + key + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath(fit));
            try
            {
                var figure = root.GetComponent<SoldierFigure>();
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                int[] parts = figure.UniformParts;
                Material[] original = skin.sharedMaterials;
                var materials = (Material[])original.Clone();
                for (int i = 0; i < materials.Length && i < parts.Length; i++)
                {
                    if (parts[i] == SoldierFigure.PartCoat) materials[i] = Recolor(original[i], coat);
                    else if (parts[i] == SoldierFigure.PartTrousers) materials[i] = Recolor(original[i], trousers);
                    else if (parts[i] == SoldierFigure.PartCap) materials[i] = Recolor(original[i], cap);
                    // Los civiles no llevan los vivos de color del uniforme: van del color de la chaqueta.
                    else if (parts[i] == SoldierFigure.PartFacings && civilian) materials[i] = Recolor(original[i], coat);
                }
                skin.sharedMaterials = materials;
                if (civilian)
                {
                    // Sin equipo militar (la gorra se queda, teñida del color del sombrero): esas submallas quedan vacías.
                    Mesh mesh = Object.Instantiate(skin.sharedMesh);
                    mesh.name = key + "_Malla";
                    for (int i = 0; i < mesh.subMeshCount && i < original.Length; i++)
                    {
                        string source = original[i] != null ? original[i].name : "";
                        bool gear = false;
                        foreach (string prefix in fit.Gear) gear |= source.StartsWith(prefix, StringComparison.Ordinal);
                        if (gear) mesh.SetTriangles(new int[0], i, false);
                    }
                    string meshPath = Folder + "/Variantes/" + key + "_Malla.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    skin.sharedMesh = mesh;
                    Transform rifle = root.transform.Find("Fusil");
                    if (rifle != null) rifle.gameObject.SetActive(false);
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Variants[key] = prefab;
                return prefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Coloca en la escena un personaje con modelo bajo <paramref name="parent"/> (el tamborcillo, los oficiales, los
        /// jinetes): oculta las primitivas que lo representaban (<paramref name="standIns"/>, por nombre) y fija su postura.
        /// A caballo, la cadera queda a <paramref name="seatHeight"/> metros. Null si no hay modelo (se quedan las primitivas).
        /// </summary>
        public static GameObject Attach(Transform parent, GameObject prefab, SoldierFigure.Rest rest, string[] standIns,
                                        float scale = 1f, float seatHeight = 0f, Vector3 offset = default, float yaw = 0f)
        {
            if (parent == null || prefab == null) return null;
            foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>())
            {
                if (Array.IndexOf(standIns, renderer.gameObject.name) >= 0) renderer.enabled = false;
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Modelo_Personaje";
            model.transform.SetParent(parent, false);
            model.transform.localScale = Vector3.one * scale;
            Vector3 position = offset;
            if (rest == SoldierFigure.Rest.Mounted)
            {
                Transform hips = model.transform.Find("Hips");
                float hipsHeight = hips != null ? hips.localPosition.y * scale : 0.9f * scale;
                position.y += seatHeight - hipsHeight;
            }
            model.transform.localPosition = position;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var figure = model.GetComponent<SoldierFigure>();
            figure.SetRest(rest);
            // La postura queda puesta en la escena (y en el primer fotograma); al jugar la recalcula cada fotograma.
            figure.ApplyPose(0f, 0f, 0f, 0f, 0f);
            return model;
        }

        private static Color ColorOf(Material material) => material != null ? material.color : Color.gray;

        /// <summary>
        /// Material de la prenda teñida: la tela original pasada a escala de grises (conserva pliegues, costuras y
        /// desgaste) multiplicada por el color del uniforme.
        /// </summary>
        private static Material Recolor(Material source, Color color)
        {
            if (source == null) return null;
            string path = Folder + "/Variantes/Tela_" + source.name + "_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return source;
            Texture texture = source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : source.mainTexture;
            Texture2D gray = Gray(texture, source.name);
            material = new Material(shader) { name = "Tela_" + source.name };
            if (gray != null) material.SetTexture("_BaseMap", gray);
            // La tela gris tiene el brillo medio en 0,5: el color se dobla para que el tono medio sea el del uniforme.
            material.SetColor("_BaseColor", gray != null ? new Color(color.r * 2f, color.g * 2f, color.b * 2f, 1f) : color);
            material.SetFloat("_Smoothness", 0.12f);
            Texture normal = source.HasProperty("normalTexture") ? source.GetTexture("normalTexture") : null;
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Versión en escala de grises de la tela, con el brillo medio en 0,5 (se guarda como PNG).</summary>
        private static Texture2D Gray(Texture source, string name)
        {
            if (source == null) return null;
            string path = Folder + "/Variantes/Gris_" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color32[] pixels = read.GetPixels32();
            double sum = 0;
            var lum = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                lum[i] = (0.299f * pixels[i].r + 0.587f * pixels[i].g + 0.114f * pixels[i].b) / 255f;
                sum += lum[i];
            }
            float mean = Mathf.Max(0.02f, (float)(sum / pixels.Length));
            for (int i = 0; i < pixels.Length; i++)
            {
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(lum[i] / mean * 0.5f * 255f), 0, 255);
                pixels[i] = new Color32(g, g, g, 255);
            }
            read.SetPixels32(pixels);
            File.WriteAllBytes(path, read.EncodeToPNG());
            Object.DestroyImmediate(read);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Une las piezas del modelo, de pie en el origen, mirando a +Z y a su altura real; quita lo que sobra.</summary>
        private static Merged Merge(GameObject asset, Fit fit)
        {
            var instance = (GameObject)Object.Instantiate(asset);
            try
            {
                // Se conserva el giro y la escala de la raíz: en los GLB de Sketchfab pasa el modelo de Z arriba a Y arriba.
                MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();

                // Medidas en el espacio del modelo: alto total, centro de las piernas y lado de la pieza delantera.
                Bounds all = default, legs = default, front = default;
                bool any = false, anyLeg = false, anyFront = false;
                foreach (MeshFilter f in filters)
                {
                    if (f.sharedMesh == null) continue;
                    Matrix4x4 m = f.transform.localToWorldMatrix;
                    foreach (Vector3 v in f.sharedMesh.vertices)
                    {
                        Vector3 p = m.MultiplyPoint3x4(v);
                        if (!any) { all = new Bounds(p, Vector3.zero); any = true; }
                        else all.Encapsulate(p);
                    }
                }
                foreach (MeshFilter f in filters)
                {
                    if (f.sharedMesh == null) continue;
                    Matrix4x4 m = f.transform.localToWorldMatrix;
                    bool isFront = PartName(f) == fit.ForwardPart;
                    foreach (Vector3 v in f.sharedMesh.vertices)
                    {
                        Vector3 p = m.MultiplyPoint3x4(v);
                        if (p.y < all.min.y + 0.3f * all.size.y)
                        {
                            if (!anyLeg) { legs = new Bounds(p, Vector3.zero); anyLeg = true; }
                            else legs.Encapsulate(p);
                        }
                        if (isFront)
                        {
                            if (!anyFront) { front = new Bounds(p, Vector3.zero); anyFront = true; }
                            else front.Encapsulate(p);
                        }
                    }
                }
                bool turn = anyFront && front.center.z < legs.center.z;
                float scale = fit.HeightM / all.size.y;
                Quaternion facing = turn ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                Vector3 origin = new Vector3(legs.center.x, all.min.y, legs.center.z);
                Matrix4x4 normalize = Matrix4x4.TRS(Vector3.zero, facing, Vector3.one * scale) * Matrix4x4.Translate(-origin);

                var merged = new Merged();
                foreach (MeshFilter f in filters)
                {
                    Mesh source = f.sharedMesh;
                    var renderer = f.GetComponent<MeshRenderer>();
                    if (source == null || renderer == null) continue;
                    string part = PartName(f);
                    Matrix4x4 m = normalize * f.transform.localToWorldMatrix;
                    Vector3[] vertices = source.vertices;
                    Vector3[] normals = source.normals;
                    Vector4[] tangents = source.tangents;
                    Vector2[] uvs = source.uv;
                    int baseIndex = merged.Positions.Count;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        merged.Positions.Add(m.MultiplyPoint3x4(vertices[i]));
                        merged.Normals.Add(normals.Length == vertices.Length ? m.MultiplyVector(normals[i]).normalized : Vector3.up);
                        if (tangents.Length == vertices.Length)
                        {
                            Vector3 t = m.MultiplyVector(tangents[i]).normalized;
                            merged.Tangents.Add(new Vector4(t.x, t.y, t.z, tangents[i].w));
                        }
                        else merged.Tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                        merged.Uvs.Add(uvs.Length == vertices.Length ? uvs[i] : Vector2.zero);
                    }
                    Material[] materials = renderer.sharedMaterials;
                    for (int s = 0; s < source.subMeshCount; s++)
                    {
                        Material material = materials[Mathf.Min(s, materials.Length - 1)];
                        int slot = merged.Materials.IndexOf(material);
                        if (slot < 0)
                        {
                            merged.Materials.Add(material);
                            merged.Triangles.Add(new List<int>());
                            slot = merged.Materials.Count - 1;
                        }
                        int[] triangles = source.GetTriangles(s);
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            int a = baseIndex + triangles[t], b = baseIndex + triangles[t + 1], c = baseIndex + triangles[t + 2];
                            if (fit.Drop != null && (fit.Drop(part, merged.Positions[a] / fit.HeightM) ||
                                                     fit.Drop(part, merged.Positions[b] / fit.HeightM) ||
                                                     fit.Drop(part, merged.Positions[c] / fit.HeightM))) continue;
                            merged.Triangles[slot].Add(a);
                            merged.Triangles[slot].Add(b);
                            merged.Triangles[slot].Add(c);
                        }
                    }
                }
                return merged;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Nombre de la pieza: el nodo con nombre propio (glTFast cuelga la malla de un hijo genérico).</summary>
        private static string PartName(MeshFilter filter)
        {
            Transform t = filter.transform;
            return t.name.StartsWith("defaultMaterial", StringComparison.Ordinal) && t.parent != null ? t.parent.name : t.name;
        }

        private static Mesh ToMesh(Merged merged, string name)
        {
            // Solo los vértices que usa algún triángulo (los del fusil quitado se descartan).
            var remap = new int[merged.Positions.Count];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var submeshes = new List<int[]>();
            foreach (List<int> triangles in merged.Triangles)
            {
                var indices = new int[triangles.Count];
                for (int i = 0; i < triangles.Count; i++)
                {
                    int v = triangles[i];
                    if (remap[v] < 0)
                    {
                        remap[v] = positions.Count;
                        positions.Add(merged.Positions[v]);
                        normals.Add(merged.Normals[v]);
                        tangents.Add(merged.Tangents[v]);
                        uvs.Add(merged.Uvs[v]);
                    }
                    indices[i] = remap[v];
                }
                submeshes.Add(indices);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int TriangleCount(Mesh mesh)
        {
            long count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) count += mesh.GetIndexCount(s) / 3;
            return (int)count;
        }

        /// <summary>
        /// Aligera la malla hasta unos <paramref name="budget"/> triángulos con los niveles de detalle que calcula Unity
        /// (6.2 o posterior): se queda con el nivel adecuado de cada submalla y descarta los vértices sin usar. En
        /// versiones anteriores la malla queda entera.
        /// </summary>
        private static Mesh Simplify(Mesh mesh, int budget)
        {
            int total = TriangleCount(mesh);
            if (total <= budget) return mesh;
#if UNITY_6000_2_OR_NEWER
            MeshLodUtility.GenerateMeshLods(mesh, 16);
            float share = (float)budget / total;
            var data = Mesh.AcquireReadOnlyMeshData(mesh);
            try
            {
                var indexBuffer = data[0].GetIndexData<int>();
                var merged = new Merged();
                mesh.GetVertices(merged.Positions);
                mesh.GetNormals(merged.Normals);
                mesh.GetTangents(merged.Tangents);
                mesh.GetUVs(0, merged.Uvs);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    SubMeshDescriptor sub = mesh.GetSubMesh(s);
                    MeshLodRange[] lods = mesh.GetLods(s);
                    var triangles = new List<int>();
                    if (lods == null || lods.Length == 0)
                    {
                        triangles.AddRange(mesh.GetTriangles(s));
                    }
                    else
                    {
                        // Los rangos de nivel pueden ir referidos al búfer entero o a la submalla: el nivel 0 lo aclara.
                        int offset = lods[0].indexStart == sub.indexStart ? 0 : sub.indexStart;
                        int target = Mathf.CeilToInt(sub.indexCount / 3f * share);
                        MeshLodRange chosen = lods[0];
                        foreach (MeshLodRange lod in lods)
                        {
                            if (lod.indexCount / 3 >= target * 0.8f) chosen = lod;
                        }
                        for (int i = 0; i < chosen.indexCount; i++)
                        {
                            triangles.Add(indexBuffer[offset + (int)chosen.indexStart + i] + sub.baseVertex);
                        }
                    }
                    merged.Triangles.Add(triangles);
                    merged.Materials.Add(null);
                }
                Mesh simplified = ToMesh(merged, mesh.name);
                Object.DestroyImmediate(mesh);
                return simplified;
            }
            finally
            {
                data.Dispose();
            }
#else
            return mesh;
#endif
        }

        // --------------------------------------------------------------------------------------------------------
        // Esqueleto
        // --------------------------------------------------------------------------------------------------------

        private static Vector3 P(Fit fit, float x, float y, float z = 0f) => new Vector3(x, y, z) * fit.HeightM;

        /// <summary>Huesos en el orden de <see cref="SoldierFigure.Bone"/>, sin girar; la izquierda del soldado es −X.</summary>
        private static Transform[] CreateSkeleton(Transform root, Fit fit)
        {
            var bones = new Transform[SoldierFigure.BoneCount];
            Transform Make(SoldierFigure.Bone bone, SoldierFigure.Bone? parent, Vector3 position)
            {
                var t = new GameObject(bone.ToString()).transform;
                t.SetParent(parent.HasValue ? bones[(int)parent.Value] : root, false);
                t.position = root.TransformPoint(position);
                bones[(int)bone] = t;
                return t;
            }
            Make(SoldierFigure.Bone.Hips, null, P(fit, 0f, fit.HipY));
            Make(SoldierFigure.Bone.Spine, SoldierFigure.Bone.Hips, P(fit, 0f, fit.SpineY));
            Make(SoldierFigure.Bone.Chest, SoldierFigure.Bone.Spine, P(fit, 0f, fit.ChestY));
            Make(SoldierFigure.Bone.Neck, SoldierFigure.Bone.Chest, P(fit, 0f, fit.NeckY));
            Make(SoldierFigure.Bone.Head, SoldierFigure.Bone.Neck, P(fit, 0f, fit.HeadY));
            foreach (float side in new[] { -1f, 1f })
            {
                bool left = side < 0f;
                Make(left ? SoldierFigure.Bone.LeftUpperLeg : SoldierFigure.Bone.RightUpperLeg, SoldierFigure.Bone.Hips, P(fit, side * fit.HipX, fit.HipY - 0.02f));
                Make(left ? SoldierFigure.Bone.LeftLowerLeg : SoldierFigure.Bone.RightLowerLeg, left ? SoldierFigure.Bone.LeftUpperLeg : SoldierFigure.Bone.RightUpperLeg, P(fit, side * fit.HipX, fit.KneeY, 0.01f));
                Make(left ? SoldierFigure.Bone.LeftFoot : SoldierFigure.Bone.RightFoot, left ? SoldierFigure.Bone.LeftLowerLeg : SoldierFigure.Bone.RightLowerLeg, P(fit, side * fit.HipX, fit.AnkleY, -0.01f));
                Make(left ? SoldierFigure.Bone.LeftUpperArm : SoldierFigure.Bone.RightUpperArm, SoldierFigure.Bone.Chest, P(fit, side * fit.ShoulderX, fit.ShoulderY));
                Make(left ? SoldierFigure.Bone.LeftLowerArm : SoldierFigure.Bone.RightLowerArm, left ? SoldierFigure.Bone.LeftUpperArm : SoldierFigure.Bone.RightUpperArm, P(fit, side * fit.ElbowX, fit.ElbowY));
                Make(left ? SoldierFigure.Bone.LeftHand : SoldierFigure.Bone.RightHand, left ? SoldierFigure.Bone.LeftLowerArm : SoldierFigure.Bone.RightLowerArm, P(fit, side * fit.WristX, fit.WristY));
            }
            return bones;
        }

        /// <summary>Tramos de cada hueso (de su articulación a la siguiente), en el espacio del soldado.</summary>
        private static List<(Vector3 a, Vector3 b)>[] Segments(Fit fit)
        {
            var segments = new List<(Vector3, Vector3)>[SoldierFigure.BoneCount];
            for (int i = 0; i < segments.Length; i++) segments[i] = new List<(Vector3, Vector3)>();
            void Add(SoldierFigure.Bone bone, Vector3 a, Vector3 b) => segments[(int)bone].Add((a, b));
            Add(SoldierFigure.Bone.Hips, P(fit, 0f, fit.HipY), P(fit, 0f, fit.SpineY));
            Add(SoldierFigure.Bone.Hips, P(fit, -fit.HipX, fit.HipY), P(fit, fit.HipX, fit.HipY));
            Add(SoldierFigure.Bone.Spine, P(fit, 0f, fit.SpineY), P(fit, 0f, fit.ChestY));
            Add(SoldierFigure.Bone.Chest, P(fit, 0f, fit.ChestY), P(fit, 0f, fit.NeckY));
            Add(SoldierFigure.Bone.Chest, P(fit, -fit.ShoulderX * 0.7f, fit.ShoulderY - 0.02f), P(fit, fit.ShoulderX * 0.7f, fit.ShoulderY - 0.02f));
            Add(SoldierFigure.Bone.Neck, P(fit, 0f, fit.NeckY), P(fit, 0f, fit.HeadY));
            Add(SoldierFigure.Bone.Head, P(fit, 0f, fit.HeadY), P(fit, 0f, 0.98f));
            foreach (float side in new[] { -1f, 1f })
            {
                bool left = side < 0f;
                Add(left ? SoldierFigure.Bone.LeftUpperLeg : SoldierFigure.Bone.RightUpperLeg, P(fit, side * fit.HipX, fit.HipY - 0.02f), P(fit, side * fit.HipX, fit.KneeY, 0.01f));
                Add(left ? SoldierFigure.Bone.LeftLowerLeg : SoldierFigure.Bone.RightLowerLeg, P(fit, side * fit.HipX, fit.KneeY, 0.01f), P(fit, side * fit.HipX, fit.AnkleY, -0.01f));
                Add(left ? SoldierFigure.Bone.LeftFoot : SoldierFigure.Bone.RightFoot, P(fit, side * fit.HipX, fit.AnkleY, -0.01f), P(fit, side * fit.HipX, fit.ToeY, fit.ToeZ));
                Add(left ? SoldierFigure.Bone.LeftUpperArm : SoldierFigure.Bone.RightUpperArm, P(fit, side * fit.ShoulderX, fit.ShoulderY), P(fit, side * fit.ElbowX, fit.ElbowY));
                Add(left ? SoldierFigure.Bone.LeftLowerArm : SoldierFigure.Bone.RightLowerArm, P(fit, side * fit.ElbowX, fit.ElbowY), P(fit, side * fit.WristX, fit.WristY));
                Add(left ? SoldierFigure.Bone.LeftHand : SoldierFigure.Bone.RightHand, P(fit, side * fit.WristX, fit.WristY), P(fit, side * fit.HandX, fit.HandY));
            }
            return segments;
        }

        private static bool IsLeft(SoldierFigure.Bone b) => b.ToString().StartsWith("Left", StringComparison.Ordinal);
        private static bool IsRight(SoldierFigure.Bone b) => b.ToString().StartsWith("Right", StringComparison.Ordinal);
        private static bool IsLeg(SoldierFigure.Bone b) => b.ToString().EndsWith("Leg", StringComparison.Ordinal) || b.ToString().EndsWith("Foot", StringComparison.Ordinal);
        private static bool IsArm(SoldierFigure.Bone b) => b.ToString().EndsWith("Arm", StringComparison.Ordinal) || b.ToString().EndsWith("Hand", StringComparison.Ordinal);

        /// <summary>
        /// Peso de cada hueso sobre cada vértice: inverso de la distancia a sus tramos (a la sexta potencia, para que
        /// solo se mezclen huesos vecinos), con los cuatro mayores. Un vértice de un lado no lo mueven los huesos del
        /// otro; el tronco no lo mueven brazos ni piernas, ni la cabeza lo de debajo del cuello.
        /// </summary>
        private static BoneWeight[] Weigh(Vector3[] vertices, Fit fit, Transform[] bones, Transform root)
        {
            List<(Vector3 a, Vector3 b)>[] segments = Segments(fit);
            float h = fit.HeightM;
            float band = 0.012f * h;
            float eps = 0.01f * h;
            var kinds = (SoldierFigure.Bone[])Enum.GetValues(typeof(SoldierFigure.Bone));
            var result = new BoneWeight[vertices.Length];
            var scores = new float[kinds.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = vertices[v];
                for (int i = 0; i < kinds.Length; i++)
                {
                    SoldierFigure.Bone bone = kinds[i];
                    scores[i] = 0f;
                    if (IsLeft(bone) && p.x > band) continue;
                    if (IsRight(bone) && p.x < -band) continue;
                    if (IsLeg(bone) && p.y > (fit.HipY + 0.03f) * h) continue;
                    if (IsArm(bone) && Mathf.Abs(p.x) < fit.ShoulderX * 0.75f * h) continue;
                    if ((bone == SoldierFigure.Bone.Head || bone == SoldierFigure.Bone.Neck) && p.y < (fit.NeckY - 0.04f) * h) continue;
                    float d = float.MaxValue;
                    foreach (var (a, b) in segments[i]) d = Mathf.Min(d, DistanceToSegment(p, a, b));
                    float w = 1f / (d + eps);
                    w *= w * w;
                    scores[i] = w * w;
                }
                result[v] = Top4(scores);
            }
            return result;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private static BoneWeight Top4(float[] scores)
        {
            var best = new int[] { -1, -1, -1, -1 };
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] <= 0f) continue;
                for (int k = 0; k < 4; k++)
                {
                    if (best[k] < 0 || scores[i] > scores[best[k]])
                    {
                        for (int j = 3; j > k; j--) best[j] = best[j - 1];
                        best[k] = i;
                        break;
                    }
                }
            }
            float sum = 0f;
            for (int k = 0; k < 4; k++) if (best[k] >= 0) sum += scores[best[k]];
            if (sum <= 0f) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            float W(int k) => best[k] >= 0 ? scores[best[k]] / sum : 0f;
            int I(int k) => Mathf.Max(0, best[k]);
            return new BoneWeight
            {
                boneIndex0 = I(0), weight0 = W(0), boneIndex1 = I(1), weight1 = W(1),
                boneIndex2 = I(2), weight2 = W(2), boneIndex3 = I(3), weight3 = W(3),
            };
        }

        // --------------------------------------------------------------------------------------------------------
        // Fusil
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Fusil del bando bajo un nodo «Fusil» con el origen en la garganta de la culata (donde lo coge la mano
        /// derecha) y la boca hacia +Z. La boca se reconoce por ser el extremo más delgado.
        /// </summary>
        private static Transform CreateRifle(Transform root, Fit fit)
        {
            var holder = new GameObject("Fusil").transform;
            holder.SetParent(root, false);
            string path = ShipModelLibrary.Root + "/" + fit.Rifle + ".glb";
            var asset = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (asset == null) return holder;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "Modelo_" + fit.Rifle;
            model.transform.SetParent(holder, false);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);

            Bounds bounds = ShipModelLibrary.LocalBounds(model, holder);
            if (bounds.size.x > bounds.size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            bounds = ShipModelLibrary.LocalBounds(model, holder);
            if (bounds.size.y > bounds.size.z) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * model.transform.localRotation;
            bounds = ShipModelLibrary.LocalBounds(model, holder);
            if (bounds.size.z > 0f) model.transform.localScale *= fit.RifleLengthM / bounds.size.z;

            if (EndHeight(model, holder, true) > EndHeight(model, holder, false))
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            }
            bounds = ShipModelLibrary.LocalBounds(model, holder);
            // La garganta de la culata queda a unos 0,3 m de la cantonera, algo por debajo del eje del arma.
            model.transform.localPosition += new Vector3(-bounds.center.x, -bounds.center.y + 0.02f, -(bounds.min.z + 0.3f));
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
            return holder;
        }

        /// <summary>Alto de la sección del arma en su extremo +Z (o −Z): la culata es más alta que la boca.</summary>
        private static float EndHeight(GameObject model, Transform space, bool plusZ)
        {
            Bounds all = ShipModelLibrary.LocalBounds(model, space);
            float cut = all.size.z * 0.12f;
            float min = float.MaxValue, max = float.MinValue;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    bool inEnd = plusZ ? p.z > all.max.z - cut : p.z < all.min.z + cut;
                    if (!inEnd) continue;
                    min = Mathf.Min(min, p.y);
                    max = Mathf.Max(max, p.y);
                }
            }
            return max > min ? max - min : 0f;
        }
    }
}
