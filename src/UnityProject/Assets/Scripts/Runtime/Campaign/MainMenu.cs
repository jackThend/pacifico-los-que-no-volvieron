using Pacifico.Core.Campaign;
using Pacifico.Data;
using Pacifico.Input;
using Pacifico.Narrative;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Menú principal (ROADMAP 6.6, IMGUI de prototipo): continuar la campaña, elegir un capítulo abierto, ver los
    /// capítulos aún en desarrollo, leer los coleccionables desbloqueados («La memoria rota») en el visor 3D y salir.
    /// Si se arranca con <c>-pacifico-perf</c>, lanza la batería de rendimiento en vez de esperar al jugador.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private CollectibleDataSO[] collectibles = new CollectibleDataSO[0];
        [SerializeField] private DocumentViewer viewer;
        [SerializeField] private Texture2D backdrop;

        private CampaignProgress _progress;
        private bool _showCollectibles;
        private Vector2 _scroll;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;
        private GUIStyle _small;
        private GUIStyle _play;
        private GUIStyle _hint;

        public void Configure(CollectibleDataSO[] items, DocumentViewer documentViewer, Texture2D background)
        {
            collectibles = items;
            viewer = documentViewer;
            backdrop = background;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _progress = CampaignSave.Load();
            if (PerformanceSuite.RequestedFromCommandLine()) PerformanceSuite.Launch();
            if (UiSnapshotSuite.RequestedFromCommandLine()) UiSnapshotSuite.Launch();
        }

        private void OnGUI()
        {
            if (viewer != null && viewer.IsOpen) return;
            EnsureStyles();
            PadMenu.Begin(this);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (backdrop != null)
            {
                GUI.color = new Color(1f, 0.9f, 0.74f, 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backdrop, ScaleMode.ScaleAndCrop);
            }
            GUI.color = Color.white;

            float x = Mathf.Max(24f, Screen.width * 0.08f), width = Mathf.Min(620f, Screen.width - 2f * x);
            GUI.Label(new Rect(x, 30f, Screen.width - 2f * x, 60f), "PACÍFICO", _title);
            GUI.Label(new Rect(x, 90f, Screen.width - 2f * x, 30f), "Los que no volvieron · La Guerra del Pacífico, 1879–1884", _subtitle);

            float y = 140f;
            // Con muchos capítulos, en pantallas bajas los botones se encogen para que quepa todo.
            float row = Mathf.Clamp((Screen.height - 140f - 230f) / Mathf.Max(1, CampaignCatalog.Chapters.Count), 34f, 46f);
            if (_showCollectibles)
            {
                DrawCollectibles(x, y, width);
                return;
            }
            ChapterEntry next = CampaignCatalog.Continue(_progress);
            MissionCheckpoint checkpoint = CheckpointStore.Current;
            ChapterEntry saved = null;
            foreach (ChapterEntry chapter in CampaignCatalog.Chapters)
            {
                if (checkpoint != null && chapter.Id == checkpoint.ChapterId) saved = chapter;
            }
            if (saved != null)
            {
                if (PadMenu.Button(new Rect(x, y, width, 56f), "JUGAR  ·  seguir en el Capítulo " + saved.Number, _play)) CampaignNavigator.ResumeCheckpoint();
            }
            else if (next != null && PadMenu.Button(new Rect(x, y, width, 56f), "JUGAR  ·  " + Label(next), _play)) CampaignNavigator.Load(next);
            y += 70f;
            foreach (ChapterEntry chapter in CampaignCatalog.Chapters)
            {
                bool open = CampaignCatalog.IsUnlocked(chapter, _progress);
                bool done = _progress.IsCompleted(chapter.Id);
                string state = !chapter.Implemented ? "   (próximamente)" : done ? "   ☑" : open ? string.Empty : "   (bloqueado)";
                GUI.enabled = open;
                if (PadMenu.Button(new Rect(x, y, width, row - 6f), Label(chapter) + state, _button)) CampaignNavigator.Load(chapter);
                GUI.enabled = true;
                y += row;
            }
            y += 10f;
            if (PadMenu.Button(new Rect(x, y, width * 0.49f, 40f), "Recuerdos de la guerra", _button))
            {
                _showCollectibles = true;
                PadMenu.Reset();
            }
            if (PadMenu.Button(new Rect(x + width * 0.51f, y, width * 0.49f, 40f), "Salir", _button)) Application.Quit();
            y += 54f;
            if (y + 96f < Screen.height - 70f) GameSettings.DrawSliders(new Rect(x, y, width, 60f));
            DrawFooter(x);
        }

        /// <summary>Cómo se maneja el menú y la versión, para que los jugadores de prueba digan cuál jugaron.</summary>
        private void DrawFooter(float x)
        {
            string how = GameInput.UsingGamepad ? "Mando: cruceta o stick para elegir · [A] aceptar"
                : "Ratón: haz clic en lo que quieras" + (GameInput.PadConnected ? " · o usa el mando" : string.Empty);
            GUI.Label(new Rect(x, Screen.height - 62f, Screen.width - 2f * x, 26f), how, _hint);
            GUI.Label(new Rect(x, Screen.height - 32f, Screen.width - 2f * x, 24f), "Versión de prueba " + Application.version, _small);
        }

        private static string Label(ChapterEntry c) => (c.Number == 0 ? "Prólogo" : "Capítulo " + c.Number) + " · " + c.Title;

        private void DrawCollectibles(float x, float y, float width)
        {
            if (PadMenu.Button(new Rect(x, y, 180f, 40f), "← Volver", _button) || GameInput.PadPressedRaw(PadButton.B))
            {
                _showCollectibles = false;
                PadMenu.Reset();
                return;
            }
            y += 46f;
            int unlocked = 0;
            _scroll = GUI.BeginScrollView(new Rect(x, y, width + 20f, Screen.height - y - 40f), _scroll, new Rect(0f, 0f, width, collectibles.Length * 40f));
            for (int i = 0; i < collectibles.Length; i++)
            {
                CollectibleDataSO item = collectibles[i];
                if (item == null) continue;
                bool open = _progress.IsUnlocked(item.id);
                if (open) unlocked++;
                GUI.enabled = open && viewer != null;
                if (PadMenu.Button(new Rect(0f, i * 40f, width, 34f), open ? item.title : "— sin descubrir —", _button)) viewer.Open(DocumentSpec.From(item));
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            // Con mando, la lista sigue al resaltado (el botón 0 es «Volver»).
            if (GameInput.UsingGamepad && PadMenu.Focus > 0)
            {
                float top = (PadMenu.Focus - 1) * 40f, visible = Screen.height - y - 40f;
                if (top < _scroll.y) _scroll.y = top;
                else if (top + 40f > _scroll.y + visible) _scroll.y = top + 40f - visible;
            }
            GUI.Label(new Rect(x, Screen.height - 34f, width, 24f), unlocked + " de " + collectibles.Length + " documentos recuperados", _small);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.94f, 0.82f);
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Italic };
            _subtitle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 16, 4, 4) };
            _play = new GUIStyle(_button) { fontSize = 24, fontStyle = FontStyle.Bold };
            _play.normal.textColor = new Color(1f, 0.93f, 0.7f);
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            _hint.normal.textColor = new Color(1f, 0.93f, 0.75f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }
    }
}
