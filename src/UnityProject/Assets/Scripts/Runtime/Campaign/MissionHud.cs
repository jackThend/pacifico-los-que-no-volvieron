using Pacifico.Core.Campaign;
using Pacifico.Input;
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
        private GUIStyle _small;
        private GUIStyle _goal;
        private GUIStyle _endTitle;
        private GUIStyle _endHint;

        public void DrawObjectives(MissionRunner runner, string chapterLabel, string who)
        {
            EnsureStyles();
            MissionStage stage = runner.CurrentStage;
            const float width = 500f, inner = width - 24f;
            float height = 96f;
            foreach (MissionObjective objective in stage.Objectives) height += _goal.CalcHeight(new GUIContent(GoalText(runner, objective)), inner - 26f) + 6f;
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(12f, 12f, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 16f, inner, 28f), chapterLabel + " · " + runner.Script.Title.ToUpperInvariant(), _title);
            GUI.Label(new Rect(24f, 44f, inner, 24f), stage.Title, _text);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(24f, 66f, inner, 22f), who, _small);
            GUI.color = Color.white;
            float y = 94f;
            foreach (MissionObjective objective in stage.Objectives)
            {
                bool done = runner.IsObjectiveComplete(objective);
                string text = GoalText(runner, objective);
                float h = _goal.CalcHeight(new GUIContent(text), inner - 26f);
                GUI.color = done ? new Color(0.6f, 1f, 0.65f) : objective.Optional ? new Color(0.8f, 0.8f, 0.75f) : new Color(1f, 0.9f, 0.55f);
                GUI.Label(new Rect(24f, y, 24f, 24f), done ? "☑" : "►", _goal);
                GUI.color = done ? new Color(0.6f, 1f, 0.65f) : Color.white;
                GUI.Label(new Rect(50f, y, inner - 26f, h), text, _goal);
                y += h + 6f;
            }
            GUI.color = Color.white;
        }

        private static string GoalText(MissionRunner runner, MissionObjective objective) =>
            ControlPrompts.Resolve(runner.Describe(objective)) + (objective.Optional ? " (si puedes)" : string.Empty);

        public void DrawDialogue(MissionRunner runner, float bottomOffset)
        {
            MissionLine line = runner.Dialogue.Current;
            if (line == null) return;
            EnsureStyles();
            // Deja 380 px a cada lado para los paneles del buque, de las escuadras y de la munición.
            float width = Mathf.Clamp(Screen.width - 760f, Mathf.Min(520f, Screen.width - 40f), 980f);
            float inner = width - 28f;
            string said = ControlPrompts.Resolve(line.Text);
            bool quote = line.Kind == LineKind.Quote;
            string text = quote ? "«" + said + "»" : said;
            GUIStyle style = line.Kind == LineKind.Quote || line.Kind == LineKind.Hint ? _line : _center;
            float header = quote ? 24f : 0f;
            float textHeight = style.CalcHeight(new GUIContent(text), inner);
            // El recuadro crece hacia arriba con textos largos: el borde de abajo no se mueve y nada se corta.
            float height = Mathf.Max(90f, header + textHeight + 18f);
            float bottom = Screen.height - Mathf.Max(bottomOffset, 100f) + 76f;
            var rect = new Rect((Screen.width - width) * 0.5f, bottom - height, width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            float alpha = Mathf.Clamp01(runner.Dialogue.CurrentElapsed / 0.25f);
            if (quote)
            {
                GUI.color = new Color(1f, 0.85f, 0.5f, alpha);
                GUI.Label(new Rect(rect.x + 14f, rect.y + 6f, inner, 20f), line.Speaker.ToUpperInvariant(), _speaker);
            }
            GUI.color = quote ? new Color(1f, 1f, 1f, alpha)
                : line.Kind == LineKind.Hint ? new Color(0.75f, 0.9f, 1f, alpha) : new Color(0.92f, 0.88f, 0.78f, alpha);
            float top = rect.y + 8f + header;
            GUI.Label(new Rect(rect.x + 14f, top, inner, rect.yMax - top - 6f), text, style);
            GUI.color = Color.white;
        }

        public void DrawEnd(MissionRunner runner, string chapterLabel, string completeDetail)
        {
            DrawEnd(runner, chapterLabel, completeDetail, CampaignNavigator.EndHint(runner.Script.Id, runner.State == MissionState.Complete));
        }

        public void DrawEnd(MissionRunner runner, string chapterLabel, string completeDetail, string hint)
        {
            EnsureStyles();
            float w = Mathf.Min(760f, Screen.width - 40f);
            var rect = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - 110f, w, 220f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            bool complete = runner.State == MissionState.Complete;
            GUI.color = complete ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.6f, 0.5f);
            GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 40f), complete ? "¡" + chapterLabel + " SUPERADO!" : "¡OH, NO! INTÉNTALO OTRA VEZ", _endTitle);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 20f, rect.y + 66f, rect.width - 40f, 80f), complete ? completeDetail : runner.FailureReason, _center);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 160f, rect.width - 20f, 44f), hint, _endHint);
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
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            _title.normal.textColor = new Color(1f, 0.93f, 0.8f);
            _text = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            _text.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Italic };
            _small.normal.textColor = Color.white;
            _goal = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _goal.normal.textColor = Color.white;
            _endTitle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _endTitle.normal.textColor = Color.white;
            _endHint = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _endHint.normal.textColor = new Color(1f, 0.9f, 0.55f);
            _speaker = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _speaker.normal.textColor = Color.white;
            _line = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            _line.normal.textColor = Color.white;
            _center = new GUIStyle(_line) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }
    }
}
