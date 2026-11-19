using System.Collections.Generic;
using UnityEngine;

namespace Pacifico.Input
{
    /// <summary>
    /// Botones de menú (IMGUI) que también se manejan con el mando: la cruceta o el stick mueven el resaltado y A pulsa.
    /// Cada menú llama a <see cref="Begin"/> al principio de su OnGUI y usa <see cref="Button"/> en lugar de GUI.Button.
    /// Solo un menú debe usarlo a la vez (el principal o la pausa).
    /// </summary>
    public static class PadMenu
    {
        private static readonly List<bool> s_enabled = new List<bool>();
        private static readonly List<bool> s_previous = new List<bool>();
        private static int s_focus;
        private static int s_count;
        private static int s_navFrame = -1;
        private static bool s_confirm;
        private static object s_owner;

        /// <summary>Índice del botón resaltado (en el orden en que se dibujan).</summary>
        public static int Focus => s_focus;

        /// <summary>Empieza un pase de dibujo del menú de <paramref name="owner"/>.</summary>
        public static void Begin(object owner)
        {
            if (!ReferenceEquals(owner, s_owner))
            {
                s_owner = owner;
                s_focus = 0;
                s_previous.Clear();
            }
            if (Event.current.type == EventType.Layout)
            {
                s_previous.Clear();
                s_previous.AddRange(s_enabled);
            }
            s_enabled.Clear();
            s_count = 0;

            if (s_navFrame == Time.frameCount) return;
            s_navFrame = Time.frameCount;
            s_confirm = GameInput.PadPressedRaw(PadButton.A);
            int step = 0;
            if (GameInput.PadPressedRaw(PadButton.DUp) || GameInput.PadPressedRaw(PadButton.LUp)) step = -1;
            if (GameInput.PadPressedRaw(PadButton.DDown) || GameInput.PadPressedRaw(PadButton.LDown)) step = 1;
            if (step != 0) Move(step);
            else if (s_previous.Count > 0 && (s_focus >= s_previous.Count || !s_previous[s_focus])) Move(1);
        }

        /// <summary>Olvida el resaltado (al cambiar de pantalla dentro del mismo menú).</summary>
        public static void Reset()
        {
            s_focus = 0;
            s_previous.Clear();
        }

        private static void Move(int step)
        {
            int n = s_previous.Count;
            if (n == 0) return;
            int index = s_focus;
            for (int tries = 0; tries < n; tries++)
            {
                index = ((index + step) % n + n) % n;
                if (s_previous[index])
                {
                    s_focus = index;
                    return;
                }
            }
        }

        /// <summary>Como GUI.Button; con mando, el botón resaltado se pulsa con A.</summary>
        public static bool Button(Rect rect, string text, GUIStyle style)
        {
            int index = s_count++;
            bool enabled = GUI.enabled;
            s_enabled.Add(enabled);
            bool focused = GameInput.UsingGamepad && index == s_focus && enabled;
            if (focused && Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(1f, 0.82f, 0.25f);
                GUI.DrawTexture(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            bool clicked = GUI.Button(rect, focused ? "►  " + text : text, style);
            if (focused && s_confirm && Event.current.type == EventType.Repaint)
            {
                s_confirm = false;
                clicked = true;
            }
            return clicked;
        }
    }
}
