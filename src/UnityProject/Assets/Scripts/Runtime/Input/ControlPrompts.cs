using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pacifico.Input
{
    /// <summary>Acciones que se le enseñan al jugador en pantalla.</summary>
    public enum Act
    {
        Move, Look, Fire, Aim, Reload, Use, Jump, Crouch, Sprint, Bayonet, Slash,
        ShipFire, Faster, Slower, Steer, Camera, Spyglass, Pause, FixFire, FixWater, FixSteam, CourseClick,
        Select, Order, Charge, Halt, Line, Skirmish, Mark, CameraTurn,
        TurretAim, Convergence, Next, Retry, Menu, Checkpoint, Confirm, Back, NextPage,
    }

    /// <summary>
    /// Nombres de teclas y botones para las ayudas en pantalla: con mando enseña sus botones (A, RT, stick…) y con
    /// teclado las teclas. Los textos del guion y de las misiones llevan marcas como <c>{disparar}</c> que
    /// <see cref="Resolve"/> cambia por el botón que toca. También dibuja la barra de controles de abajo.
    /// </summary>
    public static class ControlPrompts
    {
        private struct Entry
        {
            public string Token, Keyboard, Pad, Verb;
            public Entry(string token, string keyboard, string pad, string verb)
            {
                Token = token;
                Keyboard = keyboard;
                Pad = pad;
                Verb = verb;
            }
        }

        private static readonly Dictionary<Act, Entry> Table = new Dictionary<Act, Entry>
        {
            { Act.Move, new Entry("mover", "W A S D", "Stick izq.", "Moverse") },
            { Act.Look, new Entry("mirar", "Ratón", "Stick der.", "Mirar") },
            { Act.Fire, new Entry("disparar", "Clic", "RT", "Disparar") },
            { Act.Aim, new Entry("apuntar", "Clic der.", "LT", "Apuntar") },
            { Act.Reload, new Entry("recargar", "R", "X", "Recargar") },
            { Act.Use, new Entry("usar", "E", "X", "Usar") },
            { Act.Jump, new Entry("saltar", "Espacio", "A", "Saltar") },
            { Act.Crouch, new Entry("agacharse", "C", "B", "Agacharse") },
            { Act.Sprint, new Entry("correr", "Mayús", "L3", "Correr") },
            { Act.Bayonet, new Entry("bayoneta", "F", "RB", "Bayoneta") },
            { Act.Slash, new Entry("tajo", "G", "LB", "Machete") },
            { Act.ShipFire, new Entry("cañones", "Espacio", "A", "¡Fuego!") },
            { Act.Faster, new Entry("acelerar", "W", "Stick ↑", "Más rápido") },
            { Act.Slower, new Entry("frenar", "S", "Stick ↓", "Más lento") },
            { Act.Steer, new Entry("timon", "A / D", "Stick ← →", "Girar") },
            { Act.Camera, new Entry("camara", "Tab", "View", "Cámara") },
            { Act.Spyglass, new Entry("catalejo", "Mayús", "LT", "Catalejo") },
            { Act.Pause, new Entry("pausa", "Esc", "Start", "Pausa") },
            { Act.FixFire, new Entry("incendio", "1", "Cruceta ←", "Apagar fuego") },
            { Act.FixWater, new Entry("achique", "2", "Cruceta ↑", "Sacar agua") },
            { Act.FixSteam, new Entry("vapor", "3", "Cruceta →", "Arreglar timón") },
            { Act.CourseClick, new Entry("rumbo", "Clic der. en el mar", "", "Ir allí") },
            { Act.Select, new Entry("seleccionar", "Clic", "RT", "Elegir tropa") },
            { Act.Order, new Entry("ordenar", "Clic der.", "LT", "Ir / atacar") },
            { Act.Charge, new Entry("carga", "C", "B", "¡A la carga!") },
            { Act.Halt, new Entry("alto", "H", "Cruceta ↓", "Alto") },
            { Act.Line, new Entry("linea", "1", "Cruceta ←", "En línea") },
            { Act.Skirmish, new Entry("guerrilla", "2", "Cruceta ↑", "Dispersos") },
            { Act.Mark, new Entry("marcar", "B", "Y", "Marcar blanco") },
            { Act.CameraTurn, new Entry("girar", "Q / E", "LB / RB", "Girar cámara") },
            { Act.TurretAim, new Entry("torre", "Ratón", "Stick der.", "Apuntar la torre") },
            { Act.Convergence, new Entry("convergencia", "R / F", "X / RB", "Más lejos / cerca") },
            { Act.Next, new Entry("siguiente", "N", "A", "Siguiente capítulo") },
            { Act.Retry, new Entry("reintentar", "R", "X", "Otra vez") },
            { Act.Menu, new Entry("menu", "M", "Y", "Menú") },
            { Act.Checkpoint, new Entry("control", "C", "A", "Punto de control") },
            { Act.Confirm, new Entry("aceptar", "Clic", "A", "Aceptar") },
            { Act.Back, new Entry("volver", "Esc", "B", "Volver") },
            { Act.NextPage, new Entry("pasar", "Espacio", "A", "Seguir") },
        };

        private static GUIStyle s_key;
        private static GUIStyle s_verb;

        /// <summary>Nombre de la tecla o botón, sin corchetes (p. ej. «RT» o «Espacio»).</summary>
        public static string Name(Act act)
        {
            Entry e = Table[act];
            if (GameInput.UsingGamepad && e.Pad.Length > 0) return e.Pad;
            return e.Keyboard;
        }

        /// <summary>Tecla o botón entre corchetes: «[RT]».</summary>
        public static string Key(Act act) => "[" + Name(act) + "]";

        /// <summary>«[RT] Disparar».</summary>
        public static string Hint(Act act) => Key(act) + " " + Table[act].Verb;

        /// <summary>Cambia cada <c>{marca}</c> del texto por el botón entre corchetes. Las desconocidas se quedan igual.</summary>
        public static string Resolve(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                int close = text.IndexOf('}', open + 1);
                if (close < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }
                sb.Append(text, i, open - i);
                string token = text.Substring(open + 1, close - open - 1);
                bool found = false;
                foreach (KeyValuePair<Act, Entry> pair in Table)
                {
                    if (pair.Value.Token != token) continue;
                    sb.Append(Key(pair.Key));
                    found = true;
                    break;
                }
                if (!found) sb.Append(text, open, close - open + 1);
                i = close + 1;
            }
            return sb.ToString();
        }

        /// <summary>Color del botón del mando (los de un mando Xbox); gris para el resto y para el teclado.</summary>
        private static Color KeyColor(Act act)
        {
            if (!GameInput.UsingGamepad) return new Color(0.92f, 0.88f, 0.78f);
            switch (Table[act].Pad)
            {
                case "A": return new Color(0.45f, 0.85f, 0.35f);
                case "B": return new Color(0.95f, 0.4f, 0.35f);
                case "X": return new Color(0.4f, 0.65f, 1f);
                case "Y": return new Color(1f, 0.85f, 0.3f);
                default: return new Color(0.92f, 0.88f, 0.78f);
            }
        }

        /// <summary>Barra centrada abajo con los controles del momento: «[RT] Disparar   [X] Recargar …».</summary>
        public static void DrawBar(float bottomMargin, params Act[] acts) => DrawBarAt(Screen.height - bottomMargin - 34f, acts);

        /// <summary>Como <see cref="DrawBar"/>, pero con el borde de arriba en <paramref name="top"/>.</summary>
        public static void DrawBarAt(float top, params Act[] acts)
        {
            if (acts == null || acts.Length == 0) return;
            EnsureStyles();
            const float gap = 18f, pad = 8f, height = 34f;
            float total = 0f;
            var widths = new float[acts.Length * 2];
            for (int i = 0; i < acts.Length; i++)
            {
                widths[2 * i] = s_key.CalcSize(new GUIContent(Name(acts[i]))).x + 2f * pad;
                widths[2 * i + 1] = s_verb.CalcSize(new GUIContent(Table[acts[i]].Verb)).x;
                total += widths[2 * i] + 6f + widths[2 * i + 1] + (i > 0 ? gap : 0f);
            }
            float x = (Screen.width - total) * 0.5f, y = top;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 14f, y - 6f, total + 28f, height + 12f), Texture2D.whiteTexture);
            for (int i = 0; i < acts.Length; i++)
            {
                if (i > 0) x += gap;
                var keyRect = new Rect(x, y + 3f, widths[2 * i], height - 6f);
                GUI.color = KeyColor(acts[i]);
                GUI.DrawTexture(keyRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(keyRect, Name(acts[i]), s_key);
                x += widths[2 * i] + 6f;
                GUI.Label(new Rect(x, y, widths[2 * i + 1] + 4f, height), Table[acts[i]].Verb, s_verb);
                x += widths[2 * i + 1];
            }
            GUI.color = Color.white;
        }

        private static void EnsureStyles()
        {
            if (s_key != null) return;
            s_key = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s_key.normal.textColor = new Color(0.08f, 0.07f, 0.06f);
            s_verb = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            s_verb.normal.textColor = Color.white;
        }
    }
}
