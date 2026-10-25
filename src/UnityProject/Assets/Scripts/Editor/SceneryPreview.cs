using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fotos de comprobación del escenario con modelos (olivos, sauces, locomotoras, tiendas y cajones): abre las escenas
    /// ya construidas de la campaña y fotografía cada objeto en su sitio. Hay que construirlas antes
    /// (<see cref="GameBuilder.BuildAllScenesBatch"/>). Modo batch (sin <c>-nographics</c>):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.SceneryPreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class SceneryPreview
    {
        private static readonly (string scene, string target, string photo, float distance)[] Shots =
        {
            ("Capitulo8_Reductos_de_Miraflores", "Olivo_3", "1_olivo_miraflores", 1.8f),
            ("Capitulo8_Reductos_de_Miraflores", "Olivo_3", "2_olivar_miraflores", 5f),
            ("Capitulo4_Quebrada_de_Tarapaca", "Sauce_5", "3_sauce_tarapaca", 1.8f),
            ("Capitulo4_Quebrada_de_Tarapaca", "Sauce_5", "4_sauces_tarapaca", 5f),
            ("Capitulo3_Bahia_de_Pisagua", "Locomotoras_Visual", "5_locomotoras_pisagua", 1.6f),
            ("Capitulo5_Alto_de_la_Alianza", "Deposito_Aliado", "6_deposito_alto_de_la_alianza", 1.6f),
            ("Capitulo5_Alto_de_la_Alianza", "Parapeto_1_Sacos", "7_parapeto_alto_de_la_alianza", 0.9f),
            ("Capitulo3_Bahia_de_Pisagua", "Parapeto_Sacos", "8_parapeto_pisagua", 0.9f),
            ("Capitulo4_Quebrada_de_Tarapaca", "Parapeto_Sacos", "9_parapeto_krupp_tarapaca", 1.2f),
            ("Capitulo6_Morro_de_Arica", "Sacos_4_Sacos", "10_sacos_arica", 3f),
            ("Capitulo4_Quebrada_de_Tarapaca", "Krupp_1", "11_krupp_tarapaca", 1.4f),
            ("Capitulo5_Alto_de_la_Alianza", "Krupp_Chileno_1", "12_krupp_alto_de_la_alianza", 1.4f),
            ("Capitulo8_Reductos_de_Miraflores", "Pieza_del_Reducto_1", "13_pieza_miraflores", 1.4f),
            ("Capitulo4_Quebrada_de_Tarapaca", "Caceres_a_Caballo", "14_caceres_a_caballo", 1.2f),
            ("Capitulo6_Morro_de_Arica", "Ugarte_a_Caballo", "15_ugarte_a_caballo", 1.2f),
        };

        public static void RunBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/SceneryPreview");
                Directory.CreateDirectory(folder);
                foreach (var shot in Shots)
                {
                    EditorSceneManager.OpenScene(GameBuilder.ScenePath(shot.scene), OpenSceneMode.Single);
                    GameObject target = GameObject.Find(shot.target);
                    if (target == null) throw new InvalidOperationException("No está " + shot.target + " en " + shot.scene);
                    Debug.Log("[Pacífico] " + shot.target + ": Animation=" + target.GetComponentsInChildren<Animation>().Length +
                              " Animator=" + target.GetComponentsInChildren<Animator>().Length);
                    Bounds bounds = ShipModelLibrary.LocalBounds(target, null);
                    float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    // Los jinetes van entre casas: se les fotografía de costado y a ras de suelo.
                    Vector3 direction = shot.target.Contains("Caballo")
                        ? (target.transform.right + Vector3.up * 0.25f).normalized
                        : new Vector3(-0.8f, 0.45f, -1f).normalized;
                    Vector3 from = bounds.center + direction * (size * shot.distance + 2f);
                    ShipModelPreview.Shoot(folder, shot.photo, from, bounds.center, 50f);
                }
                Debug.Log("[Pacífico] Fotos del escenario guardadas en " + folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
