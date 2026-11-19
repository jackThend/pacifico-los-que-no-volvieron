using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Marca dorada de «adónde ir» en las misiones a pie (IMGUI): un rombo con el nombre y la distancia sobre el
    /// objetivo o, si queda fuera de la imagen, una flecha grande en el borde que dice hacia dónde girarse. Pensada para
    /// que un escolar no se pierda; cada director elige el punto según la etapa. Se oculta al llegar.
    /// </summary>
    public static class ObjectiveMarker
    {
        private static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        private const float Inset = 60f;

        private static GUIStyle s_label;
        private static GUIStyle s_arrow;

        /// <summary>Dibuja la marca de <paramref name="world"/>; no hace nada a menos de <paramref name="hideWithinM"/> metros.</summary>
        public static void Draw(Camera camera, Vector3 world, string label, float hideWithinM = 6f)
        {
            if (camera == null || Event.current.type != EventType.Repaint) return;
            Vector3 eye = camera.transform.position;
            float distance = Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(world.x, world.z));
            if (distance < hideWithinM) return;
            EnsureStyles();

            string text = label + "  ·  " + distance.ToString("0") + " m";
            Vector3 s = camera.WorldToScreenPoint(world + Vector3.up * 2.5f);
            var gui = new Vector2(s.x, Screen.height - s.y);
            bool onScreen = s.z > 0f && gui.x > Inset && gui.x < Screen.width - Inset && gui.y > Inset && gui.y < Screen.height - Inset;
            // Que respire un poco para que se note sin molestar.
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f);
            if (onScreen)
            {
                Shadowed(new Rect(gui.x - 20f, gui.y - 44f, 40f, 40f), "◆", s_arrow, Gold * pulse);
                Shadowed(new Rect(gui.x - 160f, gui.y - 4f, 320f, 26f), text, s_label, Gold);
                return;
            }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = gui - center;
            if (s.z <= 0f) dir = -dir; // detrás: la proyección sale invertida
            if (dir.sqrMagnitude < 1f) dir = Vector2.down;
            dir.Normalize();
            float scale = Mathf.Min((Screen.width * 0.5f - Inset) / Mathf.Max(0.001f, Mathf.Abs(dir.x)),
                                    (Screen.height * 0.5f - Inset) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
            Vector2 at = center + dir * scale;
            string arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0f ? "►" : "◄") : (dir.y > 0f ? "▼" : "▲");
            Shadowed(new Rect(at.x - 25f, at.y - 25f, 50f, 50f), arrow, s_arrow, Gold * pulse);
            float labelX = Mathf.Clamp(at.x - 160f, 8f, Screen.width - 328f);
            float labelY = Mathf.Clamp(at.y + (dir.y > 0.5f ? -54f : 26f), 8f, Screen.height - 34f);
            Shadowed(new Rect(labelX, labelY, 320f, 26f), text, s_label, Gold);
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            color.a = 1f;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        private static void EnsureStyles()
        {
            if (s_label != null) return;
            s_label = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s_label.normal.textColor = Color.white;
            s_arrow = new GUIStyle(s_label) { fontSize = 34 };
        }
    }
}
