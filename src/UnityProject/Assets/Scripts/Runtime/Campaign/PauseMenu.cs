using Pacifico.Core.Campaign;
using Pacifico.Input;
using Pacifico.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Pausa de las misiones (Esc): seguir, volver al último punto de control, empezar el capítulo de nuevo o salir al
    /// menú, con los controles del capítulo a la vista. Se crea sola al arrancar el juego y vive entre escenas, así que
    /// sirve a todos los capítulos sin tocarlos. También avisa cuando se guarda un punto de control.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float ToastSeconds = 3f;

        private ChapterEntry _chapter;
        private bool _paused;
        private float _previousScale = 1f;
        private CursorLockMode _previousLock;
        private bool _previousVisible;
        private float _toastUntil = -1f;
        private string _toast = string.Empty;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _button;

        /// <summary>El juego está en pausa (los directores no deben tocar la velocidad del tiempo).</summary>
        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("Menu_de_Pausa");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenu>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckpointStore.Saved += OnCheckpointSaved;
            FindChapter();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CheckpointStore.Saved -= OnCheckpointSaved;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_paused) Release(false);
            FindChapter();
        }

        private void FindChapter()
        {
            _chapter = null;
            string scene = SceneManager.GetActiveScene().name;
            foreach (ChapterEntry entry in CampaignCatalog.Chapters)
            {
                if (entry.Number >= 1 && entry.Scene == scene) _chapter = entry;
            }
        }

        private void OnCheckpointSaved(MissionCheckpoint checkpoint)
        {
            _toast = "Punto de control: " + checkpoint.StageTitle;
            _toastUntil = Time.unscaledTime + ToastSeconds;
        }

        private void Update()
        {
            if (_chapter == null || !GameInput.Pressed(GameKey.Escape)) return;
            if (_paused)
            {
                Release(true);
                return;
            }
            if (CanPause()) Pause();
        }

        /// <summary>Un documento abierto usa Esc para cerrarse, y el epílogo no se interrumpe.</summary>
        private static bool CanPause()
        {
            if (DocumentViewer.Instance != null && DocumentViewer.Instance.IsOpen) return false;
            EpiloguePlayer epilogue = FindAnyObjectByType<EpiloguePlayer>();
            return epilogue == null || !epilogue.IsPlaying;
        }

        private void Pause()
        {
            _paused = true;
            IsPaused = true;
            GameInput.Blocked = true;
            _previousScale = Time.timeScale;
            _previousLock = Cursor.lockState;
            _previousVisible = Cursor.visible;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Quita la pausa; <paramref name="restore"/> vuelve al ritmo y al cursor de antes (al seguir jugando).</summary>
        private void Release(bool restore)
        {
            _paused = false;
            IsPaused = false;
            GameInput.Blocked = false;
            AudioListener.pause = false;
            Time.timeScale = restore ? _previousScale : 1f;
            if (restore)
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousVisible;
            }
        }

        private void OnGUI()
        {
            GUI.depth = -1000; // por delante de todo: recibe el ratón antes que los botones del juego
            EnsureStyles();
            if (Time.unscaledTime < _toastUntil && !_paused)
            {
                var rect = new Rect(Screen.width - 380f, Screen.height - 54f, 360f, 34f);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = new Color(0.75f, 1f, 0.8f);
                GUI.Label(rect, _toast, _text);
                GUI.color = Color.white;
            }
            if (_paused) DrawMenu();
        }

        private void DrawMenu()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(520f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            float y = Mathf.Max(30f, Screen.height * 0.5f - 280f);
            GUI.Label(new Rect(x, y, width, 44f), "PAUSA", _title);
            GUI.Label(new Rect(x, y + 44f, width, 26f), "Capítulo " + _chapter.Number + " · " + _chapter.Title, _text);
            y += 84f;

            if (GUI.Button(new Rect(x, y, width, 44f), "Seguir jugando  [Esc]", _button)) Release(true);
            y += 52f;
            if (CheckpointStore.ExistsFor(_chapter.Id))
            {
                if (GUI.Button(new Rect(x, y, width, 44f), "Volver al punto de control: " + CheckpointStore.Current.StageTitle, _button))
                {
                    CampaignNavigator.ResumeCheckpoint();
                    return;
                }
                y += 52f;
            }
            if (GUI.Button(new Rect(x, y, width, 44f), "Empezar el capítulo de nuevo", _button))
            {
                CampaignNavigator.Load(_chapter);
                return;
            }
            y += 52f;
            if (GUI.Button(new Rect(x, y, width, 44f), "Salir al menú", _button))
            {
                CampaignNavigator.Menu();
                return;
            }
            y += 62f;
            GUI.Label(new Rect(x, y, width, Screen.height - y - 10f), ControlsText(_chapter.Mode), _text);

            // Nada de lo que queda debajo debe recibir el ratón mientras dura la pausa.
            if (Event.current.isMouse) Event.current.Use();
        }

        private static string ControlsText(ChapterMode mode)
        {
            switch (mode)
            {
                case ChapterMode.Naval:
                    return "Controles\nW / S: máquina  ·  A / D: timón  ·  Espacio o clic: fuego\n1 incendios  ·  2 achique  ·  3 vapor y timón\nTab: cámara hacia el enemigo o por la popa  ·  Mayús: catalejo\nBotón derecho: girar la cámara  ·  Rueda: acercar  ·  En la torre: el ratón apunta";
                case ChapterMode.Rts:
                    return "Controles\nClic o recuadro: seleccionar  ·  Clic derecho: mover o atacar\nW A S D: mover la cámara  ·  Q / E: girarla  ·  Rueda: altura\n1 línea  ·  2 guerrilla  ·  H alto  ·  C carga  ·  B flota";
                default:
                    return "Controles\nW A S D: moverse  ·  Ratón: mirar  ·  Clic: fuego\nClic derecho: encarar  ·  R: cargar  ·  F: bayoneta\nC: agacharse  ·  E: usar o recoger  ·  Espacio: saltar";
            }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(1f, 0.94f, 0.82f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _text.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
        }
    }
}
