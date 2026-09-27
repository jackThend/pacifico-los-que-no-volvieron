using System;
using Pacifico.Core.Narrative;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Narrative
{
    /// <summary>
    /// Reproductor de cinemáticas de corresponsal (ROADMAP 5.2): grabados y fotografías del Archivo en sepia con
    /// movimiento lento de cámara, fundidos encadenados, bandas de cine, rótulo y la voz en off con subtítulos en
    /// español. Qué se ve en cada instante lo decide <see cref="CinematicTimeline"/>, una función pura del tiempo
    /// probada fuera del motor. Si hay pista de voz, su reloj manda (los subtítulos no se desincronizan aunque el
    /// juego vaya a tirones). Espacio: pausa; Esc: saltar.
    /// </summary>
    public sealed class CinematicPlayer : MonoBehaviour
    {
        [SerializeField] private CinematicDataSO cinematic;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private Color sepia = new Color(1f, 0.9f, 0.74f);
        [SerializeField, Range(0f, 0.2f)] private float letterbox = 0.1f;
        [SerializeField] private bool showSubtitles = true;

        private CinematicTimeline _timeline;
        private AudioSource _voice;
        private AudioSource _music;
        private float _time;
        private bool _playing;
        private bool _paused;
        private bool _voiceStarted;
        private float _skipFade = -1f;

        private Texture2D _white;
        private Texture2D _vignette;
        private GUIStyle _subtitle;
        private GUIStyle _titleSmall;
        private GUIStyle _titleBig;
        private GUIStyle _credit;

        public event Action Finished;
        public CinematicTimeline Timeline => _timeline;
        public float Time01 => _timeline != null && _timeline.Duration > 0f ? _time / _timeline.Duration : 0f;
        public bool IsPlaying => _playing;

        public CinematicDataSO Cinematic
        {
            get => cinematic;
            set => cinematic = value;
        }

        private void Awake()
        {
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = 0.6f;
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        private void OnDestroy()
        {
            if (_vignette != null) Destroy(_vignette);
        }

        public void Play()
        {
            if (cinematic == null) return;
            float narration = cinematic.narration != null ? cinematic.narration.length : 0f;
            _timeline = PrologueCinematic.Build(cinematic.ToScript(), narration);
            _time = 0f;
            _playing = true;
            _paused = false;
            _voiceStarted = false;
            _skipFade = -1f;
            if (cinematic.music != null)
            {
                _music.clip = cinematic.music;
                _music.Play();
            }
        }

        private void Update()
        {
            if (!_playing) return;
            float dt = UnityEngine.Time.unscaledDeltaTime;

            if (GameInput.Pressed(GameKey.Space)) TogglePause();
            if (GameInput.Pressed(GameKey.Escape) && _skipFade < 0f) _skipFade = 0f;

            if (_skipFade >= 0f)
            {
                // Saltar: fundido a negro de medio segundo y fin.
                _skipFade += dt / 0.5f;
                _music.volume = 0.6f * (1f - Mathf.Clamp01(_skipFade));
                _voice.volume = 1f - Mathf.Clamp01(_skipFade);
                if (_skipFade >= 1f) Finish();
                return;
            }
            if (_paused) return;

            // La voz empieza tras el rótulo; desde entonces su reloj es la referencia del tiempo.
            if (!_voiceStarted && cinematic.narration != null && _time >= _timeline.NarrationStart)
            {
                _voice.clip = cinematic.narration;
                _voice.Play();
                _voiceStarted = true;
            }
            if (_voiceStarted && _voice.isPlaying) _time = _timeline.NarrationStart + _voice.time;
            else _time += dt;

            float tail = _timeline.Duration - _time;
            if (tail < CinematicTimeline.FadeOutSeconds) _music.volume = 0.6f * Mathf.Clamp01(tail / CinematicTimeline.FadeOutSeconds);
            if (_time >= _timeline.Duration) Finish();
        }

        private void TogglePause()
        {
            _paused = !_paused;
            if (_paused)
            {
                _voice.Pause();
                _music.Pause();
            }
            else
            {
                _voice.UnPause();
                _music.UnPause();
            }
        }

        private void Finish()
        {
            _playing = false;
            _voice.Stop();
            _music.Stop();
            Finished?.Invoke();
        }

        // ------------------------------------------------------------------------------------------
        // Dibujo (IMGUI: sin assets de interfaz)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_timeline == null) return;
            EnsureStyles();
            GUI.depth = -200;
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(screen, _white);
            if (!_playing) return;

            CinematicFrame f = _timeline.Evaluate(_time);
            float bar = Screen.height * letterbox;
            var picture = new Rect(0f, bar, Screen.width, Screen.height - 2f * bar);
            if (f.ShotA >= 0) DrawShot(picture, _timeline.Shots[f.ShotA], f.FrameA, 1f);
            if (f.ShotB >= 0) DrawShot(picture, _timeline.Shots[f.ShotB], f.FrameB, f.Blend);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(picture, _vignette);

            // Bajo el rótulo, la imagen se oscurece para que el título se lea sobre cualquier grabado.
            float black = Mathf.Max(Mathf.Max(f.Black, f.Title * PrologueCinematic.TitleDim), _skipFade >= 0f ? Mathf.Clamp01(_skipFade) : 0f);
            if (black > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, black);
                GUI.DrawTexture(screen, _white);
            }

            if (f.Title > 0.001f) DrawTitle(picture, f.Title);
            if (showSubtitles && f.Cue >= 0) DrawSubtitle(picture, _timeline.Cues[f.Cue]);

            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(12f, Screen.height - bar * 0.5f - 10f, 400f, 20f), _paused ? "En pausa · Espacio: seguir · Esc: saltar" : "Espacio: pausa · Esc: saltar", _credit);
            GUI.color = Color.white;
        }

        /// <summary>Imagen a pantalla completa (recortada, sin deformar) con el encuadre del movimiento de cámara.</summary>
        private void DrawShot(Rect area, CinematicShot shot, KenBurnsFrame frame, float alpha)
        {
            Texture2D texture = cinematic.Image(shot.Image);
            if (texture == null || alpha <= 0f) return;
            float screenAspect = area.width / area.height;
            float textureAspect = texture.width / (float)texture.height;
            // Fracción de la imagen visible con ampliación 1 (cubrir el cuadro), dividida por la ampliación.
            float w = textureAspect > screenAspect ? screenAspect / textureAspect : 1f;
            float h = textureAspect > screenAspect ? 1f : textureAspect / screenAspect;
            w /= Mathf.Max(1f, frame.Zoom);
            h /= Mathf.Max(1f, frame.Zoom);
            float x = Mathf.Clamp(frame.CenterX - w * 0.5f, 0f, 1f - w);
            float y = Mathf.Clamp(1f - frame.CenterY - h * 0.5f, 0f, 1f - h);
            GUI.color = new Color(sepia.r, sepia.g, sepia.b, alpha);
            GUI.DrawTextureWithTexCoords(area, texture, new Rect(x, y, w, h));
        }

        private void DrawTitle(Rect area, float alpha)
        {
            float cy = area.center.y;
            GUI.color = new Color(1f, 0.95f, 0.85f, alpha);
            GUI.Label(new Rect(0f, cy - 70f, Screen.width, 30f), "PRÓLOGO", _titleSmall);
            GUI.Label(new Rect(0f, cy - 40f, Screen.width, 60f), _timeline.Script.Title, _titleBig);
            GUI.Label(new Rect(0f, cy + 26f, Screen.width, 24f), _timeline.Script.Location, _credit);
            GUI.Label(new Rect(0f, cy + 50f, Screen.width, 24f), "Voz: " + _timeline.Script.Narrator, _credit);
        }

        private void DrawSubtitle(Rect area, SubtitleCue cue)
        {
            string text = string.Join("\n", cue.Lines);
            float lineHeight = _subtitle.fontSize * 1.35f;
            float height = lineHeight * cue.Lines.Length + 8f;
            var rect = new Rect(area.x + area.width * 0.08f, area.yMax - height - area.height * 0.04f, area.width * 0.84f, height);
            // Contorno negro de un píxel para leerse sobre cualquier grabado.
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            for (int dx = -2; dx <= 2; dx += 2)
            {
                for (int dy = -2; dy <= 2; dy += 2)
                {
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), text, _subtitle);
                }
            }
            GUI.color = Color.white;
            GUI.Label(rect, text, _subtitle);
        }

        private void EnsureStyles()
        {
            if (_subtitle != null) return;
            _white = Texture2D.whiteTexture;
            _vignette = CreateVignette(128);
            int size = Mathf.Max(18, Screen.height / 28);
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.LowerCenter, wordWrap = false };
            _subtitle.normal.textColor = Color.white;
            _titleSmall = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45), alignment = TextAnchor.MiddleCenter };
            _titleSmall.normal.textColor = Color.white;
            _titleBig = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(28, Screen.height / 16), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _titleBig.normal.textColor = Color.white;
            _credit = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 60), alignment = TextAnchor.MiddleCenter };
            _credit.normal.textColor = Color.white;
        }

        /// <summary>Viñeta de los daguerrotipos: los bordes se oscurecen.</summary>
        private static Texture2D CreateVignette(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vineta" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float a = Mathf.SmoothStep(0.55f, 1.25f, r);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
