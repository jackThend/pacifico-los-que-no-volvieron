using System;
using System.Collections;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.Campaign
{

    /// <summary>
    /// Batería de rendimiento de la build (ROADMAP 6.6): con <c>-pacifico-perf</c> en la línea de órdenes, carga cada
    /// capítulo jugable, espera a que se asiente, mide los fotogramas durante <c>-pacifico-perf-seconds</c> (60 por
    /// defecto) y escribe un informe CSV en <see cref="Application.persistentDataPath"/>; después cierra el juego.
    /// </summary>
    public sealed class PerformanceSuite : MonoBehaviour
    {
        public const string Argument = "-pacifico-perf";
        public const string SecondsArgument = "-pacifico-perf-seconds";
        /// <summary>Capítulos que se saltan al principio (para medir o diagnosticar uno concreto).</summary>
        public const string SkipArgument = "-pacifico-perf-skip";
        /// <summary>Medir solo esta escena (por nombre), esté o no en la campaña.</summary>
        public const string SceneArgument = "-pacifico-perf-scene";
        /// <summary>Apaga algo antes de medir, para saber cuánto cuesta: particulas, mar, posproceso, sombras o agentes.</summary>
        public const string WithoutArgument = "-pacifico-perf-sin";
        private const float SettleSeconds = 4f;

        public static bool RequestedFromCommandLine() => Array.IndexOf(Environment.GetCommandLineArgs(), Argument) >= 0;

        private static bool s_launched;

        public static void Launch()
        {
            if (s_launched) return;
            s_launched = true;
            // Que siga midiendo aunque la ventana pierda el foco (si no, el juego se pausa y la medida sale falseada).
            Application.runInBackground = true;
            var go = new GameObject("Bateria_de_Rendimiento");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceSuite>();
        }

        private static float Seconds() => Number(SecondsArgument, 60f);

        private static string Text(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, argument);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static T[] All<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>();
#else
            return FindObjectsOfType<T>();
#endif
        }

        /// <summary>Diagnóstico de rendimiento: apaga una familia de cosas de la escena recién cargada.</summary>
        private static void Disable(string what)
        {
            if (string.IsNullOrEmpty(what)) return;
            Debug.Log("[Pacífico][Rendimiento] Sin " + what);
            switch (what)
            {
                case "particulas":
                    foreach (ParticleSystem p in All<ParticleSystem>()) p.gameObject.SetActive(false);
                    break;
                case "mar":
                    GameObject sea = GameObject.Find("Mar");
                    if (sea != null) sea.GetComponent<Renderer>().enabled = false;
                    break;
                case "posproceso":
                    foreach (Camera c in All<Camera>()) c.allowHDR = false;
                    GameObject volume = GameObject.Find("Ambiente_Posproceso");
                    if (volume != null) volume.SetActive(false);
                    break;
                case "sombras":
                    QualitySettings.shadows = ShadowQuality.Disable;
                    foreach (Light l in All<Light>()) l.shadows = LightShadows.None;
                    break;
                case "soldados":
                    foreach (SkinnedMeshRenderer r in All<SkinnedMeshRenderer>()) r.enabled = false;
                    break;
                case "naturaleza":
                    GameObject nature = GameObject.Find("Naturaleza");
                    if (nature != null) nature.SetActive(false);
                    break;
                case "ia":
                    foreach (MonoBehaviour b in All<MonoBehaviour>())
                    {
                        string ns = b.GetType().Namespace ?? "";
                        if (ns == "Pacifico.Tactics" || ns == "Pacifico.Infantry") b.enabled = false;
                    }
                    break;
                case "agentes":
                    foreach (MonoBehaviour b in All<MonoBehaviour>())
                    {
                        if (b.GetType().Namespace == "Pacifico.Naval") b.enabled = false;
                    }
                    break;
            }
        }

        private static float Number(string argument, float fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, argument);
            return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : fallback;
        }

        private IEnumerator Start()
        {
            float seconds = Seconds();
            var report = new StringBuilder(FrameStats.CsvHeader).Append('\n');
            var stats = new FrameStats(Mathf.CeilToInt(seconds * 240f));
            int skip = Mathf.RoundToInt(Number(SkipArgument, 0f));
            string[] args = Environment.GetCommandLineArgs();
            int only = Array.IndexOf(args, SceneArgument);
            string[] scenes = only >= 0 && only + 1 < args.Length
                ? new[] { args[only + 1] }
                : CampaignCatalog.Playable.Select(c => c.Scene).ToArray();
            foreach (string scene in scenes)
            {
                if (skip-- > 0) continue;
                if (!Application.CanStreamedLevelBeLoaded(scene)) continue;
                Debug.Log("[Pacífico][Rendimiento] Cargando " + scene);
                yield return SceneManager.LoadSceneAsync(scene);
                string without = Text(WithoutArgument);
                Disable(without);
                float settle = Time.realtimeSinceStartup + SettleSeconds;
                while (Time.realtimeSinceStartup < settle) yield return null;
                Disable(without); // otra vez: lo que aparece después de cargar (soldados de las escuadras)
                stats.Clear();
                int gc = GC.CollectionCount(0);
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    stats.Add(Time.unscaledDeltaTime);
                }
                string row = stats.CsvRow(scene, GC.CollectionCount(0) - gc);
                report.Append(row).Append('\n');
                Debug.Log("[Pacífico][Rendimiento] " + row);
            }
            string path = Path.Combine(Application.persistentDataPath, "rendimiento_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv");
            File.WriteAllText(path, report.ToString());
            Debug.Log("[Pacífico][Rendimiento] Informe: " + path);
            Application.Quit();
        }
    }
}
