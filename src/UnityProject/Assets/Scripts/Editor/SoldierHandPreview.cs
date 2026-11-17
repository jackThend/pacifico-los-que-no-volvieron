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
    /// Fotos de cerca de las manos de los soldados en cada postura, y sus medidas. Modo batch (sin <c>-nographics</c>):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.SoldierHandPreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class SoldierHandPreview
    {
        public static void RunBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/SoldierHands");
                Directory.CreateDirectory(folder);
                RenderPipelineSetup.Ensure();
                SoldierModelLibrary.RebuildAll();

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.62f);
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

                var factions = new[] { (Faction.Chile, "chileno"), (Faction.Peru, "peruano") };
                foreach (var (faction, label) in factions)
                {
                    GameObject prefab = SoldierModelLibrary.Prefab(faction);
                    if (prefab == null) throw new InvalidOperationException("No hay soldado " + label);
                    Rest(prefab, folder, label);
                    Shots(prefab, folder, label, "terciado", 0f, 0f, 0f, 0f);
                    Shots(prefab, folder, label, "encarando", 0f, 0f, 0f, 1f);
                    Shots(prefab, folder, label, "rodilla", 1f, 0f, 0f, 1f);
                    Shots(prefab, folder, label, "marcha", 0f, 1f, 0.9f, 0f);
                    Shots(prefab, folder, label, "tumbado", 0f, 0f, 0f, 1f, 1f);
                }
                // Civil sin fusil: la mano relajada, colgando.
                GameObject civilian = SoldierModelLibrary.Prefab(Faction.Peru, new Color(0.42f, 0.4f, 0.36f), new Color(0.3f, 0.28f, 0.25f), new Color(0.72f, 0.62f, 0.4f), true);
                Shots(civilian, folder, "civil", "marcha", 0f, 1f, 0.9f, 0f);
                Debug.Log("[Pacífico] Fotos de las manos guardadas en " + folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>La mano en la pose de reposo del modelo, sin deformar: de frente, de lado y desde abajo.</summary>
        private static void Rest(GameObject prefab, string folder, string label)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var figure = go.GetComponent<SoldierFigure>();
            Transform rifle = go.transform.Find("Fusil");
            if (rifle != null) rifle.gameObject.SetActive(false);
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            Transform hand = FindBone(skin, "RightHand");
            Vector3 c = hand.position + (hand.position - FindBone(skin, "RightLowerArm").position).normalized * 0.07f;
            ShipModelPreview.Shoot(folder, label + "_reposo_frente", c + new Vector3(0f, 0f, 0.32f), c, 35f, 0.02f);
            ShipModelPreview.Shoot(folder, label + "_reposo_lado", c + new Vector3(0.32f, 0f, 0f), c, 35f, 0.02f);
            ShipModelPreview.Shoot(folder, label + "_reposo_abajo", c + new Vector3(0.1f, -0.3f, 0.02f), c, 35f, 0.02f);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void Shots(GameObject prefab, string folder, string label, string pose, float kneel, float gait, float phase, float aim, float prone = 0f)
        {
            var holder = new GameObject("Figura").transform;
            // Cuerpo a tierra, como lo tumba el juego: la figura gira 88° hacia delante sobre los pies.
            holder.rotation = Quaternion.Euler(88f * prone, 0f, 0f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            var figure = go.GetComponent<SoldierFigure>();
            figure.ApplyPose(phase, gait, kneel, prone, aim);
            Debug.Log("[Pacífico] Error de agarre " + label + " " + pose + ": " + (figure.GripErrorM * 100f).ToString("F1") + " cm");
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (string side in new[] { "Right", "Left" })
            {
                Transform hand = FindBone(skin, side + "Hand");
                Vector3 c = hand.position + (hand.position - FindBone(skin, side + "LowerArm").position).normalized * 0.06f;
                string name = label + "_" + pose + "_" + (side == "Right" ? "derecha" : "izquierda");
                ShipModelPreview.Shoot(folder, name + "_a", c + new Vector3(side == "Right" ? 0.28f : -0.28f, 0.08f, 0.14f), c, 38f, 0.02f);
                ShipModelPreview.Shoot(folder, name + "_b", c + new Vector3(0f, -0.26f, 0.2f), c, 38f, 0.02f);
            }
            // El cuerpo entero, para ver el brazo que lleva a la mano.
            Vector3 chest = FindBone(skin, "Chest").position;
            ShipModelPreview.Shoot(folder, label + "_" + pose + "_cuerpo", chest + new Vector3(0.5f, 0.15f, 1.7f), chest, 40f, 0.05f);
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
        }

        private static Transform FindBone(SkinnedMeshRenderer skin, string name)
        {
            foreach (Transform b in skin.bones)
            {
                if (b.name == name) return b;
            }
            throw new InvalidOperationException("Falta el hueso " + name);
        }
    }
}
