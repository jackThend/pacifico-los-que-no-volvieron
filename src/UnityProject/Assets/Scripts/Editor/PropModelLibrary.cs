using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Modelos CC-BY de Sketchfab para el escenario (créditos en <c>Assets/Art/CCBY/CREDITOS.md</c>): árboles, tiendas,
    /// pertrechos y locomotoras. Son solo decorado: la primitiva a la que sustituyen se queda con su colisionador (para
    /// los disparos, la navegación y los puestos a cubierto) y solo se oculta su renderer. Sin el archivo o sin glTFast,
    /// la escena conserva sus primitivas.
    /// </summary>
    internal static class PropModelLibrary
    {
        public const string Olive = "olivo_viejo";
        public const string Willow = "sauce";
        public const string CratesAndBarrels = "cajas_y_barriles";
        public const string Locomotive = "locomotora_1840";
        /// <summary>Tramo recto de sacos de arpillera de 4,6 × 1 × 0,75 m (Sandbag Barrier, G4AGamingLabs).</summary>
        public const string SandbagWall = "sacos_de_arena_muro";

        /// <summary>Un tramo no se estira ni se encoge más de esto para cuadrar con el parapeto.</summary>
        private const float MaxStretch = 0.3f;

        /// <summary>Bidones metálicos del lote de cajones: no son de 1879 (se dejan los cajones de madera y las lonas).</summary>
        public static readonly string[] ModernDrums = { "Barrel", "Barrel.001", "Barrel.002" };

        /// <summary>
        /// Instancia el modelo <paramref name="id"/> bajo <paramref name="parent"/> apoyado en <paramref name="groundLocal"/>
        /// (centro de su planta, base en el suelo), girado <paramref name="yawDeg"/> y escalado a <paramref name="height"/>
        /// metros de alto (0: su tamaño original). Con <paramref name="lengthAlongZ"/>, su lado más largo queda a lo
        /// largo de Z antes del giro. Los nodos de <paramref name="hiddenParts"/> se ocultan (piezas impropias de 1879).
        /// Null si no está.
        /// </summary>
        public static GameObject Place(string id, string name, Transform parent, Vector3 groundLocal, float height, float yawDeg,
                                       bool lengthAlongZ = false, string[] hiddenParts = null)
        {
            string path = ShipModelLibrary.Root + "/" + id + ".glb";
            if (!File.Exists(path)) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = name;
            model.transform.SetParent(parent, false);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            if (hiddenParts != null)
            {
                foreach (Transform part in model.GetComponentsInChildren<Transform>())
                {
                    if (!ShipModelLibrary.IsHidden(part.name, hiddenParts)) continue;
                    foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                }
            }

            Bounds bounds = ShipModelLibrary.LocalBounds(model, parent);
            float turn = lengthAlongZ && bounds.size.x > bounds.size.z ? 90f : 0f;
            model.transform.localRotation = Quaternion.Euler(0f, yawDeg + turn, 0f) * model.transform.localRotation;

            bounds = ShipModelLibrary.LocalBounds(model, parent);
            if (height > 0f && bounds.size.y > 0f) model.transform.localScale *= height / bounds.size.y;

            bounds = ShipModelLibrary.LocalBounds(model, parent);
            model.transform.localPosition += new Vector3(groundLocal.x - bounds.center.x, groundLocal.y - bounds.min.y, groundLocal.z - bounds.center.z);
            return model;
        }

        /// <summary>
        /// Tiñe los materiales del modelo cuyo nombre contiene <paramref name="materialName"/>, multiplicando su color
        /// base por <paramref name="tint"/> (p. ej. hojas verdes → secas). La copia teñida se guarda como asset
        /// (<c>Materials/CCBY_&lt;key&gt;.mat</c>) para que la escena la conserve.
        /// </summary>
        public static void Tint(GameObject model, string materialName, Color tint, string key)
        {
            string path = ProjectPaths.Materials + "/CCBY_" + key + ".mat";
            Material tinted = AssetDatabase.LoadAssetAtPath<Material>(path);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.Contains(materialName)) continue;
                    if (tinted == null)
                    {
                        tinted = new Material(materials[i]) { name = "CCBY_" + key };
                        foreach (string property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                        {
                            if (tinted.HasProperty(property)) tinted.SetColor(property, tinted.GetColor(property) * tint);
                        }
                        HistoricalDataAssetGenerator.EnsureFolder(ProjectPaths.Materials);
                        AssetDatabase.CreateAsset(tinted, path);
                    }
                    materials[i] = tinted;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Viste un parapeto de primitiva (un cubo sin girar respecto a su padre) con tramos de <paramref name="id"/> a lo
        /// largo de su lado mayor: cada tramo, a la altura del parapeto; tantos como quepan, estirados o encogidos a lo
        /// largo (como mucho un 30 %) para cubrirlo justo. Oculta el cubo, que conserva su colisionador. False si no hay
        /// modelo.
        /// </summary>
        public static bool Wall(string id, GameObject standIn)
        {
            Transform parent = standIn.transform.parent;
            Vector3 size = standIn.transform.localScale;
            bool alongZ = size.z > size.x;
            float length = alongZ ? size.z : size.x;
            Vector3 axis = alongZ ? Vector3.forward : Vector3.right;
            Vector3 bottom = standIn.transform.localPosition - Vector3.up * (size.y * 0.5f);

            var wall = new GameObject(standIn.name + "_Sacos").transform;
            wall.SetParent(parent, false);
            wall.localPosition = bottom;

            // Primer tramo para medir lo que ocupa a la altura del parapeto.
            var first = new GameObject("Tramo_1").transform;
            first.SetParent(wall, false);
            GameObject model = Place(id, "Modelo_" + id, first, Vector3.zero, size.y, alongZ ? 0f : 90f, lengthAlongZ: true);
            if (model == null)
            {
                Object.DestroyImmediate(wall.gameObject);
                return false;
            }
            Bounds bounds = ShipModelLibrary.LocalBounds(model, first);
            float module = alongZ ? bounds.size.z : bounds.size.x;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / module));
            float stretch = Mathf.Clamp(length / (count * module), 1f - MaxStretch, 1f + MaxStretch);
            float step = module * stretch;

            for (int i = 0; i < count; i++)
            {
                Transform section = i == 0 ? first : new GameObject("Tramo_" + (i + 1)).transform;
                if (i > 0)
                {
                    section.SetParent(wall, false);
                    Place(id, "Modelo_" + id, section, Vector3.zero, size.y, alongZ ? 0f : 90f, lengthAlongZ: true);
                }
                section.localPosition = axis * ((i + 0.5f) * step - count * step * 0.5f);
                section.localScale = alongZ ? new Vector3(1f, 1f, stretch) : new Vector3(stretch, 1f, 1f);
            }
            Hide(standIn);
            return true;
        }

        /// <summary>Cañón de campaña de ruedas grandes (Field Artilery Cannon, iedalton) como Krupp de montaña.</summary>
        public const string FieldGun = "canon_de_campana";
        /// <summary>Caballo con esqueleto (Horse Rigged, abhayexe).</summary>
        public const string Horse = "caballo_animado";

        /// <summary>
        /// Viste una pieza de artillería de primitivas (raíz con «Cureña» y «Rueda», y el pivote del tubo que el juego
        /// eleva y hace retroceder) con el cañón de campaña: el tubo del modelo pasa al pivote para que se siga moviendo.
        /// La boca mira a +Z, como la pieza. False si no hay modelo.
        /// </summary>
        public static bool DressFieldGun(Transform root, Transform barrelPivot)
        {
            // Ruedas de 1 m, como las de un Krupp de montaña de 75 mm (valor de ajuste); el modelo trae la boca hacia −Z.
            GameObject model = Place(FieldGun, "Modelo_Canon", root, Vector3.zero, 1.05f, 180f);
            if (model == null) return false;

            // El tubo es la pieza más estrecha respecto a su largo.
            Renderer tube = null;
            float best = float.MaxValue;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Bounds b = ShipModelLibrary.LocalBounds(renderer.gameObject, root);
                float slenderness = b.size.x / Mathf.Max(0.001f, b.size.z);
                if (slenderness < best)
                {
                    best = slenderness;
                    tube = renderer;
                }
            }
            // Primero se oculta el tubo de primitiva y después entra el del modelo, que hereda elevación y retroceso.
            foreach (Transform part in barrelPivot)
            {
                if (part.TryGetComponent(out Renderer primitive)) primitive.enabled = false;
            }
            if (tube != null) tube.transform.SetParent(barrelPivot, true);
            ShipModelLibrary.HideStandIns(root, "Cureña", "Rueda");
            return true;
        }

        /// <summary>
        /// Pone el caballo bajo <paramref name="rider"/> (que lo mueve el juego) en lugar del de primitivas («Caballo»,
        /// «Cuello», «Pata»), mirando a +Z, y baja al jinete para que vaya sentado en la silla. El caballo queda en su pose
        /// de pie: su única animación es una tira de 60 s con todos los aires seguidos (parado, paso, trote, galope,
        /// tumbado, encabritado) y aún no hay un tramo de paso recortado que se pueda repetir.
        /// </summary>
        public static GameObject DressHorse(Transform rider)
        {
            // 2,1 m hasta las orejas: unos 1,55 m a la cruz, un caballo de silla (valor de ajuste).
            GameObject horse = Place(Horse, "Modelo_Caballo", rider, Vector3.zero, 2.1f, 0f);
            if (horse == null) return null;
            foreach (Animator animator in horse.GetComponentsInChildren<Animator>()) animator.enabled = false;

            var standIns = new[] { "Caballo", "Cuello", "Pata" };
            ShipModelLibrary.HideStandIns(rider, standIns);
            // El jinete de primitivas iba sobre un lomo a 1,6 m; la silla del modelo queda unos 0,3 m más abajo.
            foreach (Transform part in rider)
            {
                if (part.gameObject == horse || System.Array.IndexOf(standIns, part.name) >= 0) continue;
                part.localPosition += Vector3.down * 0.3f;
            }
            return horse;
        }

        /// <summary>Oculta el aspecto de las primitivas sustituidas; sus colisionadores siguen funcionando.</summary>
        public static void Hide(params GameObject[] standIns)
        {
            foreach (GameObject standIn in standIns)
            {
                if (standIn != null && standIn.TryGetComponent(out Renderer renderer)) renderer.enabled = false;
            }
        }
    }
}
