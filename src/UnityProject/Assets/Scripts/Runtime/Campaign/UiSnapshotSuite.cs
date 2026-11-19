using System;
using System.Collections;
using System.IO;
using Pacifico.Core.Campaign;
using Pacifico.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Fotos de la interfaz para revisarla sin jugar: con <c>-pacifico-fotos-ui carpeta</c> en la línea de órdenes, el
    /// juego compilado fotografía el menú y cada capítulo (con las ayudas de teclado y con las del mando, y la pausa en
    /// alguno), guarda los PNG en la carpeta y se cierra. Las fotos incluyen la IMGUI, que las cámaras no ven.
    /// </summary>
    public sealed class UiSnapshotSuite : MonoBehaviour
    {
        public const string Argument = "-pacifico-fotos-ui";
        private const float SettleSeconds = 7f;

        private string _folder;

        public static bool RequestedFromCommandLine() => Array.IndexOf(Environment.GetCommandLineArgs(), Argument) >= 0;

        private static bool s_launched;

        public static void Launch()
        {
            if (s_launched) return;
            s_launched = true;
            Application.runInBackground = true;
            var go = new GameObject("Fotos_de_Interfaz");
            DontDestroyOnLoad(go);
            go.AddComponent<UiSnapshotSuite>();
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, Argument);
            _folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "FotosUI");
            Directory.CreateDirectory(_folder);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2f);
            yield return Pair("00_menu");

            foreach (ChapterEntry chapter in CampaignCatalog.Chapters)
            {
                if (!chapter.Implemented || chapter.Number < 1 || !Application.CanStreamedLevelBeLoaded(chapter.Scene)) continue;
                SceneManager.LoadScene(chapter.Scene);
                yield return null;
                yield return new WaitForSecondsRealtime(SettleSeconds);
                string name = "cap" + chapter.Number;
                yield return Pair(name);
                if (chapter.Number == 1 || chapter.Number == 4 || chapter.Number == 5)
                {
                    PauseMenu pause = FindAnyObjectByType<PauseMenu>();
                    if (pause != null)
                    {
                        pause.SendMessage("Pause");
                        yield return Pair(name + "_pausa");
                        pause.SendMessage("Release", true);
                    }
                }
            }
            GameInput.ForceGamepadDisplay = null;
            Debug.Log("[Pacífico][FotosUI] Hecho: " + _folder);
            Application.Quit();
        }

        /// <summary>Una foto con las ayudas de teclado y otra con las del mando.</summary>
        private IEnumerator Pair(string name)
        {
            GameInput.ForceGamepadDisplay = false;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(name + "_teclado");
            GameInput.ForceGamepadDisplay = true;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(name + "_mando");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_folder, name + ".png");
            // Sin el módulo ScreenCapture: se leen los píxeles de la pantalla al final del fotograma.
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
            Debug.Log("[Pacífico][FotosUI] " + path);
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
