using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Fotos de comprobación del ambiente (<see cref="SceneAtmosphere"/>): abre cada capítulo ya construido y saca una
    /// foto desde la cámara del juego, con su posproceso, y otra hacia el horizonte. Modo batch (sin <c>-nographics</c>):
    /// <c>Unity -batchmode -projectPath src/UnityProject -executeMethod Pacifico.EditorTools.AtmospherePreview.RunBatch -pacifico-preview &lt;carpeta&gt;</c>
    /// </summary>
    public static class AtmospherePreview
    {
        private const int Width = 1600;
        private const int Height = 900;

        public static void RunBatch()
        {
            try
            {
                string folder = ShipModelPreview.ArgumentAfter("-pacifico-preview") ?? Path.GetFullPath("Temp/AtmospherePreview");
                Directory.CreateDirectory(folder);
                int n = 1;
                foreach (string scene in new[]
                {
                    "Capitulo1_Rada_de_Iquique", "Capitulo2_Punta_Angamos", "Capitulo3_Bahia_de_Pisagua", "Capitulo4_Quebrada_de_Tarapaca",
                    "Capitulo5_Alto_de_la_Alianza", "Capitulo6_Morro_de_Arica", "Capitulo7_Canaverales_de_Canete", "Capitulo8_Reductos_de_Miraflores",
                })
                {
                    string path = GameBuilder.ScenePath(scene);
                    if (!File.Exists(path)) continue;
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    DynamicGI.UpdateEnvironment();
                    Camera camera = Camera.main;
#if UNITY_2023_1_OR_NEWER
                    if (camera == null) camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
#else
                    if (camera == null) camera = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
                    if (camera == null) continue;
                    Shoot(camera, Path.Combine(folder, n + "_" + scene + "_juego.png"));
                    // Hacia el suelo, para ver texturas de terreno.
                    Quaternion view = camera.transform.rotation;
                    Vector3 ahead = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
                    if (ahead.sqrMagnitude < 1e-4f) ahead = Vector3.forward;
                    camera.transform.rotation = Quaternion.LookRotation(ahead.normalized) * Quaternion.Euler(35f, 0f, 0f);
                    Shoot(camera, Path.Combine(folder, n + "_" + scene + "_suelo.png"));
                    camera.transform.rotation = view;
                    // Hacia el horizonte, a la altura de los ojos y mirando al sol.
                    Light sun = RenderSettings.sun;
                    if (sun != null)
                    {
                        Vector3 toSun = Vector3.ProjectOnPlane(-sun.transform.forward, Vector3.up);
                        if (toSun.sqrMagnitude > 1e-4f) camera.transform.rotation = Quaternion.LookRotation(toSun.normalized + Vector3.up * 0.12f);
                        Shoot(camera, Path.Combine(folder, n + "_" + scene + "_horizonte.png"));
                    }
                    n++;
                }
                Debug.Log("[Pacífico] Fotos del ambiente guardadas en " + folder);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Shoot(Camera camera, string file)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            File.WriteAllBytes(file, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
        }
    }
}
