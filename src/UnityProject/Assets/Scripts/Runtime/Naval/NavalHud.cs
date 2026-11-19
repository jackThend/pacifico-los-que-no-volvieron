using System;
using System.Collections.Generic;
using Pacifico.Core.Naval;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>Buque u objetivo que el jugador debe poder localizar siempre (marca en pantalla y en la rosa).</summary>
    public sealed class NavalContact
    {
        public NavalContact(Transform where, string label, bool enemy)
        {
            Where = where;
            Label = label;
            Enemy = enemy;
        }

        public Transform Where { get; }
        public string Label { get; }
        public bool Enemy { get; }
    }

    /// <summary>
    /// Interfaz de los capítulos navales (IMGUI), pensada para escolares: panel del buque con la velocidad en palabras
    /// y barras de vida, agua y motor; una marca sobre cada buque, con flecha si queda fuera de la imagen; avisos de
    /// avería con el botón que la remedia; y abajo, la barra con los controles (teclado o mando, según lo que se use).
    /// </summary>
    public sealed class NavalHud : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float EdgeInset = 70f;

        private static readonly Color EnemyColor = new Color(1f, 0.45f, 0.35f);
        private static readonly Color AllyColor = new Color(0.55f, 0.85f, 1f);
        private static readonly Color Ink = new Color(0.93f, 0.9f, 0.8f);

        [SerializeField] private ShipController ship;

        private readonly List<NavalContact> _contacts = new List<NavalContact>();
        private readonly List<string> _lines = new List<string>();
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _alert;
        private GUIStyle _mark;

        /// <summary>HUD activo en la escena (uno por escena de prototipo).</summary>
        public static NavalHud Active { get; private set; }

        private void Awake()
        {
            // Awake precede a los Start de los sistemas que se registran en ExtraLines.
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public ShipController Ship
        {
            get => ship;
            set => ship = value;
        }

        /// <summary>Otros componentes (torre, averías) añaden aquí sus líneas de estado.</summary>
        public List<Func<string>> ExtraLines { get; } = new List<Func<string>>();

        /// <summary>Aviso urgente que da el capítulo, por encima de los de avería (vacío: ninguno).</summary>
        public Func<string> PriorityAlert { get; set; }

        /// <summary>Lo que hay que mostrar en pantalla y en la rosa. El director lo cambia según la etapa.</summary>
        public void SetContacts(params NavalContact[] contacts)
        {
            _contacts.Clear();
            _contacts.AddRange(contacts);
        }

        private void OnGUI()
        {
            if (ship == null || ship.Motion == null) return;
            EnsureStyles();
            Camera view = Camera.main;
            var damage = ship.GetComponent<ShipDamageController>();
            bool showDamage = damage != null && damage.PlayerControlled && damage.State != null;

            if (view != null)
            {
                foreach (NavalContact contact in _contacts) DrawContact(view, contact);
            }
            string urgent = PriorityAlert != null ? PriorityAlert() : null;
            if (!string.IsNullOrEmpty(urgent)) DrawAlert(urgent, 20f);
            if (showDamage) DrawAlerts(damage.State, string.IsNullOrEmpty(urgent) ? 20f : 64f);
            DrawPanel(damage, showDamage);
            DrawControls();
            DrawCourse(view);
        }

        // ------------------------------------------------------------------------------------------
        // Panel del buque
        // ------------------------------------------------------------------------------------------

        private void DrawPanel(ShipDamageController damage, bool showDamage)
        {
            ShipMotionModel m = ship.Motion;
            _lines.Clear();
            foreach (Func<string> line in ExtraLines)
            {
                string text = line();
                if (!string.IsNullOrEmpty(text)) _lines.Add(text); // las de otro buque o de un sistema inactivo vienen vacías
            }

            float inner = PanelWidth - 24f;
            float height = 42f + (ship.PlayerControlled ? 56f : 28f) + (showDamage ? 3f * 28f + 6f : 0f) + 8f;
            foreach (string line in _lines) height += _small.CalcHeight(new GUIContent(line), inner) + 4f;

            float x = 16f, y = Screen.height - height - 70f;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(x, y, PanelWidth, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            x += 12f;
            y += 8f;

            // Los nombres largos («Fragata blindada Almirante Cochrane») bajan de tamaño para caber.
            GUIStyle nameStyle = _title.CalcSize(new GUIContent(ship.Spec.Name)).x <= inner ? _title : _label;
            GUI.Label(new Rect(x, y, inner, 30f), ship.Spec.Name, nameStyle);
            y += 34f;
            if (ship.PlayerControlled)
            {
                GUI.Label(new Rect(x, y, inner, 24f), "Velocidad: " + SpeedName(m.Telegraph.Order), _label);
                y += 26f;
                DrawThrottle(x, y, inner, m.Telegraph.Order);
                y += 30f;
            }
            else
            {
                GUI.Label(new Rect(x, y, inner, 24f), "El barco lo maneja la tripulación", _label);
                y += 28f;
            }

            if (showDamage)
            {
                y += 4f;
                ShipDamageState s = damage.State;
                DrawBar(x, ref y, inner, "Vida", s.IntegrityFraction, Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.45f, 0.8f, 0.45f), s.IntegrityFraction));
                DrawBar(x, ref y, inner, "Agua", s.FloodFraction, s.FloodFraction > 0.6f ? new Color(0.9f, 0.35f, 0.2f) : new Color(0.35f, 0.65f, 0.95f));
                DrawBar(x, ref y, inner, "Motor", 1f - s.BoilerDamage, new Color(0.85f, 0.75f, 0.45f));
                y += 2f;
            }

            foreach (string line in _lines)
            {
                float h = _small.CalcHeight(new GUIContent(line), inner);
                GUI.Label(new Rect(x, y, inner, h), line, _small);
                y += h + 4f;
            }
        }

        /// <summary>Barra de abajo: solo los controles que sirven ahora (timón si el jugador lleva el buque, fuego si sirve un cañón).</summary>
        private void DrawControls()
        {
            var turret = ship.GetComponent<ColesTurretController>();
            var gunner = ship.GetComponent<Campaign.PlayerBroadsideGunner>();
            bool turretActive = turret != null && turret.enabled && turret.PlayerControlled;
            bool canFire = turretActive || (gunner != null && gunner.PlayerActive);
            if (ship.PlayerControlled && turretActive)
                ControlPrompts.DrawBar(12f, Act.Faster, Act.Slower, Act.Steer, Act.TurretAim, Act.Fire, Act.Pause);
            else if (ship.PlayerControlled && canFire)
                ControlPrompts.DrawBar(12f, Act.Faster, Act.Slower, Act.Steer, Act.ShipFire, Act.Camera, Act.Pause);
            else if (ship.PlayerControlled)
                ControlPrompts.DrawBar(12f, Act.Faster, Act.Slower, Act.Steer, Act.Camera, Act.Pause);
            else if (turretActive)
                ControlPrompts.DrawBar(12f, Act.TurretAim, Act.Fire, Act.Convergence, Act.Pause);
            else if (canFire)
                ControlPrompts.DrawBar(12f, Act.ShipFire, Act.Camera, Act.Spyglass, Act.Pause);
        }

        /// <summary>La orden de máquina en palabras de todos los días.</summary>
        private static string SpeedName(EngineOrder order)
        {
            switch (order)
            {
                case EngineOrder.HalfAstern: return "marcha atrás";
                case EngineOrder.QuarterAhead: return "despacio";
                case EngineOrder.HalfAhead: return "media";
                case EngineOrder.FullAhead: return "¡a toda máquina!";
                default: return "parado";
            }
        }

        /// <summary>Cinco casillas, de marcha atrás a toda máquina, con la actual encendida.</summary>
        private static void DrawThrottle(float x, float y, float width, EngineOrder order)
        {
            const int steps = 5;
            float cell = (width - (steps - 1) * 4f) / steps;
            int lit = (int)order - (int)EngineTelegraph.MinOrder;
            for (int i = 0; i < steps; i++)
            {
                Color on = i == 0 ? new Color(0.95f, 0.55f, 0.3f) : i == 1 ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.45f, 0.85f, 0.45f);
                GUI.color = i == lit ? on : i < lit && i > 1 ? new Color(on.r, on.g, on.b, 0.45f) : new Color(1f, 1f, 1f, 0.14f);
                GUI.DrawTexture(new Rect(x + i * (cell + 4f), y + 2f, cell, 20f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        /// <summary>Marca en el mar del rumbo que el jugador pidió con el ratón.</summary>
        private void DrawCourse(Camera view)
        {
            if (view == null || !ship.CourseTarget.HasValue) return;
            Vector3 screen = view.WorldToScreenPoint(ship.CourseTarget.Value + Vector3.up * 4f);
            if (screen.z <= 0f) return;
            Label(new Rect(screen.x - 100f, Screen.height - screen.y - 30f, 200f, 24f), "▼ vamos aquí", _mark, new Color(1f, 0.9f, 0.4f));
        }

        private void DrawBar(float x, ref float y, float width, string name, float fraction, Color color)
        {
            const float labelWidth = 64f;
            GUI.Label(new Rect(x, y, labelWidth, 24f), name, _label);
            var back = new Rect(x + labelWidth, y + 5f, width - labelWidth - 50f, 14f);
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * Mathf.Clamp01(fraction), back.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.xMax + 6f, y, 50f, 24f), (fraction * 100f).ToString("0") + " %", _label);
            y += 28f;
        }

        private void DrawAlert(string text, float y)
        {
            float width = Mathf.Min(620f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, y, width, 40f);
            GUI.color = new Color(0.35f, 0.05f, 0.03f, 0.8f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, _alert);
        }

        private void DrawAlerts(ShipDamageState s, float y)
        {
            string text = null;
            if (s.SteeringJammed) text = "¡EL TIMÓN NO GIRA!  Pulsa " + ControlPrompts.Key(Act.FixSteam);
            else if (s.FloodFraction > 0.25f || s.InflowRate > s.BasePumpRate) text = "¡ENTRA AGUA!  Pulsa " + ControlPrompts.Key(Act.FixWater);
            else if (s.FireIntensity > 0.4f) text = "¡FUEGO A BORDO!  Pulsa " + ControlPrompts.Key(Act.FixFire);
            if (text == null) return;
            if (s.ActiveAction != null) text = "La brigada está trabajando…";
            else if (s.CooldownRemaining > 0f) text = text.Substring(0, text.IndexOf('!') + 1) + "  Los marineros vuelven en " + s.CooldownRemaining.ToString("0") + " s";
            DrawAlert(text, y);
        }

        // ------------------------------------------------------------------------------------------
        // Marcas sobre los buques
        // ------------------------------------------------------------------------------------------

        private void DrawContact(Camera view, NavalContact contact)
        {
            if (contact.Where == null) return;
            Vector3 to = contact.Where.position - ship.transform.position;
            string text = contact.Label + "  ·  " + DistanceText(new Vector2(to.x, to.z).magnitude);
            Color color = contact.Enemy ? EnemyColor : AllyColor;

            Vector3 screen = view.WorldToScreenPoint(contact.Where.position + Vector3.up * 14f);
            bool inFront = screen.z > 0f;
            float sx = screen.x, sy = Screen.height - screen.y;
            if (inFront && sx > EdgeInset && sx < Screen.width - EdgeInset && sy > EdgeInset && sy < Screen.height - EdgeInset)
            {
                Label(new Rect(sx - 8f, sy - 18f, 16f, 22f), "▼", _mark, color);
                Label(new Rect(sx - 150f, sy - 40f, 300f, 22f), text, _mark, color);
                return;
            }

            // Fuera de la imagen: flecha en el borde, en la dirección en la que queda.
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 direction = new Vector2(sx, sy) - center;
            if (!inFront) direction = -direction;
            if (direction.sqrMagnitude < 1f) direction = Vector2.up;
            direction.Normalize();
            float halfW = Screen.width * 0.5f - EdgeInset, halfH = Screen.height * 0.5f - EdgeInset;
            float scale = Mathf.Min(halfW / Mathf.Max(0.001f, Mathf.Abs(direction.x)), halfH / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            Vector2 at = center + direction * scale;
            string arrow = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0f ? "►" : "◄") : (direction.y > 0f ? "▼" : "▲");
            float labelX = Mathf.Clamp(at.x - 150f, 8f, Screen.width - 308f);
            Label(new Rect(labelX, at.y - 22f, 300f, 22f), arrow, _mark, color);
            Label(new Rect(labelX, at.y, 300f, 22f), text, _mark, color);
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        private static string DistanceText(float meters) =>
            ((int)Mathf.Round(meters)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') + " m";

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _label.normal.textColor = Ink;
            _small = new GUIStyle(_label) { fontSize = 16, wordWrap = true };
            _title = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false };
            _alert = new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _alert.normal.textColor = Color.white;
            _mark = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mark.normal.textColor = Color.white;
        }
    }
}
