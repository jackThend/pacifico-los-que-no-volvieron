using System.Collections.Generic;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Marcas sobre los últimos enemigos de un objetivo de «abatir a N» (IMGUI). Si uno queda fuera de la imagen, una
    /// flecha en el borde dice hacia dónde girarse. Solo se enseñan cuando quedan pocos y llevan un rato sin caer:
    /// para que el jugador no se quede buscando al último soldado escondido tras una tapia.
    /// </summary>
    public static class EnemyMarkers
    {
        private static GUIStyle s_label;

        /// <summary>Decide si toca mostrarlas (valores de balance: hasta 3 enemigos y 30 s sin bajas).</summary>
        public static bool ShouldShow(int remaining, float secondsSinceLastDown, int maxRemaining = 3, float quietSeconds = 30f) =>
            remaining > 0 && remaining <= maxRemaining && secondsSinceLastDown >= quietSeconds;

        public static void Draw(Camera camera, IReadOnlyList<Vector3> positions, Color color)
        {
            if (camera == null || positions == null || positions.Count == 0) return;
            if (s_label == null)
            {
                s_label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (Vector3 world in positions)
            {
                Vector3 s = camera.WorldToScreenPoint(world + Vector3.up * 2.3f);
                float distance = Vector3.Distance(camera.transform.position, world);
                var gui = new Vector2(s.x, Screen.height - s.y);
                bool onScreen = s.z > 0f && gui.x > 30f && gui.x < Screen.width - 30f && gui.y > 30f && gui.y < Screen.height - 30f;
                if (onScreen)
                {
                    Label(new Rect(gui.x - 60f, gui.y - 26f, 120f, 20f), "▼", color);
                    Label(new Rect(gui.x - 60f, gui.y - 10f, 120f, 20f), distance.ToString("0") + " m", color);
                    continue;
                }
                // Fuera de la imagen (o detrás): flecha en el borde, en la dirección del enemigo.
                Vector2 dir = gui - center;
                if (s.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                dir.Normalize();
                float scale = Mathf.Min((Screen.width * 0.5f - 40f) / Mathf.Max(1e-3f, Mathf.Abs(dir.x)),
                                        (Screen.height * 0.5f - 40f) / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
                Vector2 edge = center + dir * scale;
                string arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0f ? "►" : "◄") : (dir.y > 0f ? "▼" : "▲");
                Label(new Rect(edge.x - 60f, edge.y - 18f, 120f, 20f), arrow, color);
                Label(new Rect(edge.x - 60f, edge.y, 120f, 20f), distance.ToString("0") + " m", color);
            }
        }

        private static void Label(Rect rect, string text, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, s_label);
            GUI.color = color;
            GUI.Label(rect, text, s_label);
            GUI.color = Color.white;
        }
    }
}
