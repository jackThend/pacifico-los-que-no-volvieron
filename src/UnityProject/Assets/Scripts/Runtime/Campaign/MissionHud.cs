using Pacifico.Core.Campaign;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Interfaz común de las misiones (IMGUI de prototipo): panel de capítulo y objetivos, línea de diálogo (citas
    /// entre comillas latinas, acotaciones en cursiva, ayudas en azul), panel de fin y fundidos.
    /// </summary>
    public sealed class MissionHud
    {
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _speaker;
        private GUIStyle _line;
        private GUIStyle _center;

        public void DrawObjectives(MissionRunner runner, string chapterLabel, string who)
        {
            EnsureStyles();
            MissionStage stage = runner.CurrentStage;
            float height = 86f + stage.Objectives.Count * 22f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(12f, 12f, 450f, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(22f, 16f, 430f, 22f), chapterLabel + " · " + runner.Script.Title.ToUpperInvariant(), _title);
            GUI.Label(new Rect(22f, 38f, 430f, 20f), runner.Script.Date + " · " + runner.Script.Location + " — " + stage.Title, _text);
            GUI.Label(new Rect(22f, 58f, 430f, 20f), who, _text);
            float y = 82f;
            foreach (MissionObjective objective in stage.Objectives)
            {
                bool done = runner.IsObjectiveComplete(objective);
                GUI.color = done ? new Color(0.6f, 1f, 0.65f) : objective.Optional ? new Color(0.8f, 0.8f, 0.75f) : Color.white;
                GUI.Label(new Rect(22f, y, 430f, 20f), (done ? "☑ " : "☐ ") + runner.Describe(objective) + (objective.Optional ? " (opcional)" : string.Empty), _text);
                y += 22f;
            }
            GUI.color = Color.white;
        }

        public void DrawDialogue(MissionRunner runner, float bottomOffset)
        {
            MissionLine line = runner.Dialogue.Current;
            if (line == null) return;
            EnsureStyles();
            float width = Mathf.Min(900f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - bottomOffset, width, 76f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            float alpha = Mathf.Clamp01(runner.Dialogue.CurrentElapsed / 0.25f);
            switch (line.Kind)
            {
                case LineKind.Quote:
                    GUI.color = new Color(1f, 0.85f, 0.5f, alpha);
                    GUI.Label(new Rect(rect.x + 14f, rect.y + 6f, width - 28f, 20f), line.Speaker.ToUpperInvariant(), _speaker);
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.Label(new Rect(rect.x + 14f, rect.y + 26f, width - 28f, 46f), "«" + line.Text + "»", _line);
                    break;
                case LineKind.Hint:
                    GUI.color = new Color(0.75f, 0.9f, 1f, alpha);
                    GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, width - 28f, 56f), line.Text, _line);
                    break;
                default:
                    GUI.color = new Color(0.92f, 0.88f, 0.78f, alpha);
                    GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, width - 28f, 56f), line.Text, _center);
                    break;
            }
            GUI.color = Color.white;
        }

        public void DrawEnd(MissionRunner runner, string chapterLabel, string completeDetail)
        {
            DrawEnd(runner, chapterLabel, completeDetail, CampaignNavigator.EndHint(runner.Script.Id, runner.State == MissionState.Complete));
        }

        public void DrawEnd(MissionRunner runner, string chapterLabel, string completeDetail, string hint)
        {
            EnsureStyles();
            var rect = new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.5f - 90f, 600f, 180f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            bool complete = runner.State == MissionState.Complete;
            GUI.Label(new Rect(rect.x, rect.y + 20f, rect.width, 30f), complete ? chapterLabel + " COMPLETADO" : "MISIÓN FRACASADA", _title);
            GUI.Label(new Rect(rect.x + 20f, rect.y + 64f, rect.width - 40f, 60f), complete ? completeDetail : runner.FailureReason, _center);
            GUI.Label(new Rect(rect.x, rect.y + 130f, rect.width, 24f), hint, _center);
        }

        public static void DrawFade(float alpha, Color color)
        {
            if (alpha <= 0f) return;
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            _title.normal.textColor = new Color(1f, 0.93f, 0.8f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _text.normal.textColor = Color.white;
            _speaker = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _speaker.normal.textColor = Color.white;
            _line = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _line.normal.textColor = Color.white;
            _center = new GUIStyle(_line) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }
    }
}
