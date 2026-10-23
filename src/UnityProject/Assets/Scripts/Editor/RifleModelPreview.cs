using System;
using System.IO;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fotos de comprobación de los fusiles en primera persona: monta el jugador con <see cref="PisaguaPrototypeBuilder.CreatePlayer"/>
    /// (el mismo que usan los capítulos) y fotografía el arma desde los ojos, en el encare y de costado.
    /// Modo batch (sin <c>-nographics</c>):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.RifleModelPreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class RifleModelPreview
    {
        // Pose de encare de IronSightViewModel (su valor por defecto).
        private static readonly Vector3 AimPosition = new Vector3(0f, -0.075f, 0.34f);

        public static void RunBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/RiflePreview");
                Render(folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            RenderPipelineSetup.Ensure();
            HistoricalDataAssetGenerator.GenerateAll();
            string[] weapons = { WeaponCatalog.ComblainId, WeaponCatalog.ChassepotId };
            for (int n = 0; n < weapons.Length; n++)
            {
                // Igual que los capítulos: el arma se carga antes de crear la escena (NewScene la descarga de memoria).
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(ProjectPaths.WeaponData + "/Weapon_" + weapons[n] + ".asset");
                if (weapon == null) throw new InvalidOperationException("Falta el WeaponDataSO " + weapons[n]);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
                PrototypeSceneKit.Primitive(PrimitiveType.Plane, "Suelo", null, Vector3.zero, new Vector3(20f, 1f, 20f),
                    PrototypeSceneKit.Material("Arena_Foto", new Color(0.78f, 0.69f, 0.52f)), keepCollider: false);

                FirstPersonController player = PisaguaPrototypeBuilder.CreatePlayer(weapon, null, Vector3.zero, 0f, 10);
                Transform eye = player.ViewCamera.transform;
                Transform view = eye.parent.Find("Fusil_Comblain_Vista");
                eye.gameObject.SetActive(false); // las fotos las hace una cámara aparte en el mismo sitio

                string prefix = (n + 1) + "_" + weapons[n];
                ShipModelPreview.Shoot(folder, prefix + "_a_cadera", eye.position, eye.position + eye.forward, 70f, 0.03f);
                Vector3 hip = view.localPosition;
                Quaternion hipRotation = view.localRotation;
                view.localPosition = AimPosition;
                view.localRotation = Quaternion.identity;
                ShipModelPreview.Shoot(folder, prefix + "_b_encare", eye.position, eye.position + eye.forward, 70f, 0.03f);
                view.localPosition = hip;
                view.localRotation = hipRotation;
                Vector3 center = view.position + view.forward * 0.25f;
                ShipModelPreview.Shoot(folder, prefix + "_c_costado", center + view.right * 1.6f + Vector3.up * 0.15f, center, 45f, 0.03f);
            }
            Debug.Log("[Pacífico] Fotos de los fusiles guardadas en " + folder);
        }
    }
}
