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
    /// Medidor de rendimiento en cualquier escena (F3): FPS, media, percentil 95 y 99, tirones y recolecciones del GC
    /// de los últimos 10 s (<see cref="FrameStats"/>). Se instala solo al cargar el juego.
    /// </summary>
    public sealed class PerformanceProbe : MonoBehaviour
    {
        private readonly FrameStats _stats = new FrameStats(600);
        private bool _visible;
        private float _nextText;
        private string _text = string.Empty;
        private int _gcStart;
        private GUIStyle _style;

        public FrameStats Stats => _stats;

        private static bool s_installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Sin FindObjectOfType (obsoleto en Unity 6): una marca estática basta, el objeto sobrevive a las escenas.
            if (s_installed) return;
            s_installed = true;
            var go = new GameObject("Medidor_de_Rendimiento");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceProbe>();
        }

        private void Start() => _gcStart = GC.CollectionCount(0);

        private void Update()
        {
            _stats.Add(Time.unscaledDeltaTime);
            if (GameInput.Pressed(GameKey.F3)) _visible = !_visible;
            if (!_visible || Time.unscaledTime < _nextText) return;
            _nextText = Time.unscaledTime + 0.5f;
            var c = CultureInfo.InvariantCulture;
            _text = _stats.MeanFps.ToString("0", c) + " FPS · media " + _stats.MeanMs.ToString("0.0", c) + " ms · p95 " + _stats.PercentileMs(95f).ToString("0.0", c) +
                    " · p99 " + _stats.PercentileMs(99f).ToString("0.0", c) + " · tirones " + _stats.Hitches + " · GC " + (GC.CollectionCount(0) - _gcStart) +
                    (_stats.MeetsTarget ? " · cumple 60 FPS" : " · NO cumple 60 FPS");
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
                _style.normal.textColor = Color.white;
            }
            GUI.depth = -1000;
            GUI.Box(new Rect(Screen.width * 0.5f - 300f, 6f, 600f, 26f), _text, _style);
        }
    }
}
