using System;
using Pacifico.Core.Campaign;
using UnityEngine;

namespace Pacifico.Narrative
{
    /// <summary>
    /// Reproduce el epílogo «La memoria rota» (<see cref="EpilogueTimeline"/>, función pura del tiempo). Pantalla
    /// negra; el marco del retrato de Abraham Quiroz (el Archivo no tiene su fotografía: el marco queda vacío en vez
    /// de inventar un rostro) y su lápida escrita a máquina; la carta a su padre en subtítulos; el mosaico de retratos
    /// de época de los tres bandos; la cifra de muertos; la última cita del corresponsal; y el fundido final.
    /// </summary>
    public sealed class EpiloguePlayer : MonoBehaviour
    {
        [SerializeField] private Texture2D[] mosaic = new Texture2D[0];
        [SerializeField] private Color sepia = new Color(1f, 0.9f, 0.74f);

        private EpilogueTimeline _timeline;
        private float _time;
        private bool _playing;
        private GUIStyle _card;
        private GUIStyle _cardSmall;
        private GUIStyle _subtitle;
        private GUIStyle _quote;
        private GUIStyle _note;

        public event Action Finished;
        public bool IsPlaying => _playing;

        public Texture2D[] Mosaic
        {
            get => mosaic;
            set => mosaic = value;
        }

        public void Play(MemoriaRotaEpilogue epilogue)
        {
            _timeline = new EpilogueTimeline(epilogue);
            _time = 0f;
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            _time += Time.unscaledDeltaTime;
            if (_time < _timeline.Duration) return;
            _playing = false;
            Finished?.Invoke();
        }

        private void OnGUI()
        {
            if (_timeline == null) return;
            EnsureStyles();
            GUI.depth = -300;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            EpilogueFrame f = _timeline.Evaluate(_playing ? _time : _timeline.Duration);

            if (f.Mosaic > 0f) DrawMosaic(f.Mosaic, f.MosaicAlpha);
            if (f.Card > 0f) DrawCard(f);
            if (f.Cue >= 0)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.78f, Screen.width * 0.8f, Screen.height * 0.16f),
                          string.Join("\n", _timeline.Cues[f.Cue].Lines), _subtitle);
            }
            if (f.Statistic > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, f.Statistic);
                GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.42f, Screen.width * 0.8f, 60f), _timeline.Epilogue.Statistic, _quote);
            }
            if (f.Quote > 0f)
            {
                GUI.color = new Color(1f, 0.95f, 0.85f, f.Quote);
                GUI.Label(new Rect(Screen.width * 0.12f, Screen.height * 0.3f, Screen.width * 0.76f, Screen.height * 0.4f), "«" + _timeline.Epilogue.FinalQuote + "»", _quote);
            }
            if (f.Black > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, f.Black);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        private void DrawCard(EpilogueFrame f)
        {
            float a = f.Card;
            // Marco del retrato: vacío a propósito (no hay fotografía de Abraham Quiroz en el Archivo).
            float w = Screen.height * 0.22f, h = w * 1.3f;
            var frame = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.12f, w, h);
            GUI.color = new Color(sepia.r * 0.5f, sepia.g * 0.45f, sepia.b * 0.35f, a);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(frame.x + 6f, frame.y + 6f, frame.width - 12f, frame.height - 12f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a * 0.5f);
            GUI.Label(new Rect(frame.x, frame.center.y - 20f, frame.width, 40f), "Sin fotografía\nen el Archivo", _note);

            string[] lines = _timeline.Epilogue.Card;
            float y = frame.yMax + 24f;
            for (int i = 0; i < lines.Length; i++)
            {
                string text = i < f.CardLines ? lines[i] : i == f.CardLines ? lines[i].Substring(0, Math.Min(lines[i].Length, f.CardChars)) : string.Empty;
                if (text.Length == 0) continue;
                GUI.color = new Color(1f, 0.96f, 0.88f, a);
                GUI.Label(new Rect(0f, y, Screen.width, 34f), text, i == 0 ? _card : _cardSmall);
                y += i == 0 ? 40f : 28f;
            }
        }

        private void DrawMosaic(float reveal, float opacity)
        {
            if (mosaic == null || mosaic.Length == 0) return;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(mosaic.Length * 1.6f));
            int rows = Mathf.CeilToInt(mosaic.Length / (float)columns);
            float cellW = Screen.width * 0.86f / columns, cellH = Screen.height * 0.7f / rows;
            float x0 = Screen.width * 0.07f, y0 = Screen.height * 0.08f;
            for (int i = 0; i < mosaic.Length; i++)
            {
                if (mosaic[i] == null) continue;
                // Los retratos entran uno a uno.
                float alpha = Mathf.Clamp01(reveal * mosaic.Length - i);
                if (alpha <= 0f) continue;
                var cell = new Rect(x0 + (i % columns) * cellW + 4f, y0 + (i / columns) * cellH + 4f, cellW - 8f, cellH - 8f);
                GUI.color = new Color(sepia.r, sepia.g, sepia.b, alpha * 0.9f * opacity);
                GUI.DrawTexture(cell, mosaic[i], ScaleMode.ScaleAndCrop);
            }
        }

        private void EnsureStyles()
        {
            if (_card != null) return;
            _card = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(22, Screen.height / 30), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _card.normal.textColor = Color.white;
            _cardSmall = new GUIStyle(_card) { fontSize = Mathf.Max(15, Screen.height / 48), fontStyle = FontStyle.Italic };
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 34), alignment = TextAnchor.UpperCenter, wordWrap = true };
            _subtitle.normal.textColor = Color.white;
            _quote = new GUIStyle(_subtitle) { fontSize = Mathf.Max(20, Screen.height / 30), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            _note = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _note.normal.textColor = Color.white;
        }
    }
}
