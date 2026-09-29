using Pacifico.Core.Campaign;
using Pacifico.Data;
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
        }

        private void OnGUI()
        {
            if (viewer != null && viewer.IsOpen) return;
            EnsureStyles();
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (backdrop != null)
            {
                GUI.color = new Color(1f, 0.9f, 0.74f, 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backdrop, ScaleMode.ScaleAndCrop);
            }
            GUI.color = Color.white;

            float x = Mathf.Max(24f, Screen.width * 0.08f), width = Mathf.Min(560f, Screen.width - 2f * x);
            GUI.Label(new Rect(x, 36f, Screen.width - 2f * x, 50f), "PACÍFICO", _title);
            GUI.Label(new Rect(x, 88f, Screen.width - 2f * x, 30f), "Los que no volvieron · La Guerra del Pacífico, 1879–1884", _subtitle);

            float y = 140f;
            if (_showCollectibles)
            {
                DrawCollectibles(x, y, width);
                return;
            }
            ChapterEntry next = CampaignCatalog.Continue(_progress);
            if (next != null && GUI.Button(new Rect(x, y, width, 44f), "Continuar: " + Label(next), _button)) CampaignNavigator.Load(next);
            y += 60f;
            foreach (ChapterEntry chapter in CampaignCatalog.Chapters)
            {
                bool open = CampaignCatalog.IsUnlocked(chapter, _progress);
                bool done = _progress.IsCompleted(chapter.Id);
                string state = !chapter.Implemented ? "  · en desarrollo" : done ? "  · completado" : open ? string.Empty : "  · bloqueado";
                GUI.enabled = open;
                if (GUI.Button(new Rect(x, y, width, 34f), Label(chapter) + state, _button)) CampaignNavigator.Load(chapter);
                GUI.enabled = true;
                y += 40f;
            }
            y += 10f;
            if (GUI.Button(new Rect(x, y, width * 0.49f, 34f), "La memoria rota (coleccionables)", _button)) _showCollectibles = true;
            if (GUI.Button(new Rect(x + width * 0.51f, y, width * 0.49f, 34f), "Salir", _button)) Application.Quit();
            GUI.Label(new Rect(x, Screen.height - 34f, Screen.width - 2f * x, 24f), "F3 en cualquier escena: medidor de rendimiento", _small);
        }

        private static string Label(ChapterEntry c) => (c.Number == 0 ? "Prólogo" : "Capítulo " + c.Number) + " · " + c.Title + " · " + c.Date;

        private void DrawCollectibles(float x, float y, float width)
        {
            if (GUI.Button(new Rect(x, y, 160f, 32f), "← Volver", _button)) _showCollectibles = false;
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
                if (GUI.Button(new Rect(0f, i * 40f, width, 34f), open ? item.title : "— sin descubrir —", _button)) viewer.Open(DocumentSpec.From(item));
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(x, Screen.height - 34f, width, 24f), unlocked + " de " + collectibles.Length + " documentos recuperados", _small);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.94f, 0.82f);
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Italic };
            _subtitle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 4, 4) };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }
    }

    /// <summary>Al acabar el prólogo, lo anota como visto y pasa al capítulo 1.</summary>
    [RequireComponent(typeof(CinematicPlayer))]
    public sealed class PrologueCampaignLink : MonoBehaviour
    {
        private void Start() => GetComponent<CinematicPlayer>().Finished += OnFinished;

        private void OnFinished()
        {
            CampaignSave.RecordChapter(CampaignCatalog.PrologueId);
            ChapterEntry next = CampaignCatalog.Next(CampaignCatalog.PrologueId);
            if (next != null && Application.CanStreamedLevelBeLoaded(next.Scene)) CampaignNavigator.Load(next);
        }
    }
}
