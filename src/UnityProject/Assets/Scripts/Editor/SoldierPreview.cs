using System;
using System.IO;
using Pacifico.Core.Common;
using Pacifico.Infantry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fotos de comprobación de los soldados con esqueleto (<see cref="SoldierModelLibrary"/>) en sus poses: de pie
    /// con el fusil terciado, dos momentos del paso, encarando de pie y rodilla en tierra, y cuerpo a tierra. Modo batch
    /// (sin <c>-nographics</c>):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.SoldierPreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class SoldierPreview
    {
        public static void RunBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/SoldierPreview");
                Directory.CreateDirectory(folder);
                RenderPipelineSetup.Ensure();
                SoldierModelLibrary.RebuildAll();

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.localScale = Vector3.one * 10f;
                ground.GetComponent<Renderer>().sharedMaterial = PrototypeSceneKit.Material("Arena_Foto", new Color(0.62f, 0.55f, 0.42f));

                var factions = new[] { (Faction.Chile, "chileno", 0f), (Faction.Peru, "peruano", 6f) };
                foreach (var (faction, label, z) in factions)
                {
                    GameObject prefab = SoldierModelLibrary.Prefab(faction);
                    if (prefab == null) throw new InvalidOperationException("No hay soldado " + label);
                    Pose(prefab, new Vector3(-4.5f, 0f, z), 0f, 0f, 0f, 0f, 0f);
                    Pose(prefab, new Vector3(-2.7f, 0f, z), 0.9f, 1f, 0f, 0f, 0f);
                    Pose(prefab, new Vector3(-0.9f, 0f, z), 0.9f + Mathf.PI, 1f, 0f, 0f, 0f);
                    Pose(prefab, new Vector3(0.9f, 0f, z), 0f, 0f, 0f, 0f, 1f);
                    Pose(prefab, new Vector3(2.7f, 0f, z), 0f, 0f, 1f, 0f, 1f);
                    // Cuerpo a tierra, como lo tumba el juego: la figura gira 88° hacia delante sobre los pies.
                    var lying = new GameObject("Tumbado").transform;
                    lying.SetPositionAndRotation(new Vector3(4.6f, 0.18f, z - 0.9f), Quaternion.Euler(88f, 0f, 0f));
                    Pose(prefab, Vector3.zero, 0f, 0f, 0f, 1f, 1f, lying);
                    // Paso y carrera, apartados para fotografiarlos de lado.
                    Pose(prefab, new Vector3(9f, 0f, z), 0.9f, 1f, 0f, 0f, 0f);
                    Pose(prefab, new Vector3(9f, 0f, z + 2f), 0.9f + Mathf.PI, 1.8f, 0f, 0f, 0f);

                    Vector3 c = new Vector3(0f, 1f, z);
                    ShipModelPreview.Shoot(folder, "soldado_" + label + "_1_poses", c + new Vector3(-1.5f, 1.6f, 9.5f), c + new Vector3(0f, -0.2f, 0f), 50f, 0.1f);
                    ShipModelPreview.Shoot(folder, "soldado_" + label + "_2_de_cerca", new Vector3(-3.6f, 1.6f, z + 2.6f), new Vector3(-4.5f, 1.05f, z), 40f, 0.1f);
                    ShipModelPreview.Shoot(folder, "soldado_" + label + "_3_paso_y_carrera_de_lado", new Vector3(14f, 1.1f, z + 1f), new Vector3(9f, 0.9f, z + 1f), 45f, 0.1f);
                    ShipModelPreview.Shoot(folder, "soldado_" + label + "_4_encarando", new Vector3(1.8f, 1.5f, z + 3.2f), new Vector3(1.8f, 1f, z), 45f, 0.1f);
                    ShipModelPreview.Shoot(folder, "soldado_" + label + "_5_espalda", new Vector3(-4.2f, 1.7f, z - 2.6f), new Vector3(-4.5f, 1.05f, z), 40f, 0.1f);
                }
                // Uniformes de cada unidad y civiles, con los colores de los capítulos.
                var units = new (Faction faction, Color coat, Color trousers, Color cap, bool civilian, string label)[]
                {
                    (Faction.Chile, new Color(0.14f, 0.17f, 0.3f), new Color(0.55f, 0.18f, 0.14f), new Color(0.12f, 0.14f, 0.25f), false, "chileno"),
                    (Faction.Peru, new Color(0.82f, 0.8f, 0.72f), new Color(0.78f, 0.76f, 0.68f), new Color(0.35f, 0.12f, 0.1f), false, "peruano de blanco"),
                    (Faction.Bolivia, TacnaPrototypeBuilder.ColoradoRed, TacnaPrototypeBuilder.ColoradoTrim, TacnaPrototypeBuilder.ColoradoRed, false, "colorado"),
                    (Faction.Peru, new Color(0.18f, 0.17f, 0.16f), new Color(0.35f, 0.33f, 0.3f), new Color(0.12f, 0.11f, 0.1f), true, "civil"),
                    (Faction.Peru, new Color(0.42f, 0.4f, 0.36f), new Color(0.3f, 0.28f, 0.25f), new Color(0.72f, 0.62f, 0.4f), true, "culi"),
                };
                for (int i = 0; i < units.Length; i++)
                {
                    GameObject variant = SoldierModelLibrary.Prefab(units[i].faction, units[i].coat, units[i].trousers, units[i].cap, units[i].civilian);
                    Pose(variant, new Vector3(-4f + i * 2f, 0f, 14f), units[i].civilian ? 0.9f : 0f, units[i].civilian ? 1f : 0f, 0f, 0f, 0f);
                }
                ShipModelPreview.Shoot(folder, "soldado_unidades", new Vector3(0f, 1.4f, 20.5f), new Vector3(0f, 0.95f, 14f), 45f, 0.1f);
                Debug.Log("[Pacífico] Fotos de los soldados guardadas en " + folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Pose(GameObject prefab, Vector3 position, float phase, float gait, float kneel, float prone, float aim,
                                 Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<SoldierFigure>().ApplyPose(phase, gait, kneel, prone, aim);
            Log(go);
        }

        private static void Log(GameObject go)
        {
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            Debug.Log("[Pacífico] " + go.name + " en " + go.transform.position + ": malla " + skin.sharedMesh.bounds + " horneada " + baked.bounds +
                      " cadera " + skin.rootBone.position);
            foreach (Renderer r in go.transform.Find("Fusil").GetComponentsInChildren<Renderer>()) Debug.Log("[Pacífico]   fusil " + r.name + " " + r.bounds);
        }
    }
}
