using System;
using System.Collections.Generic;
using Pacifico.Core.Naval;
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
    /// Interfaz de los capítulos navales (IMGUI): panel del buque con barras de casco, agua y vapor; rosa de marcaciones
    /// con el sector de tiro de la batería; una marca sobre cada buque, con flecha si queda fuera de la imagen; y avisos
    /// de avería con la tecla que la remedia.
    /// </summary>
    public sealed class NavalHud : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float RoseWidth = 560f;
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
        private GUIStyle _center;
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
            DrawRose();
            string urgent = PriorityAlert != null ? PriorityAlert() : null;
            if (!string.IsNullOrEmpty(urgent)) DrawAlert(urgent, 84f);
            if (showDamage) DrawAlerts(damage.State, string.IsNullOrEmpty(urgent) ? 84f : 120f);
            DrawPanel(damage, showDamage);
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

            float inner = PanelWidth - 20f;
            float height = 30f + 22f * (ship.PlayerControlled ? 3f : 2f) + (showDamage ? 3f * 20f + 6f : 0f) + 26f;
            foreach (string line in _lines) height += _small.CalcHeight(new GUIContent(line), inner) + 3f;

            float x = 16f, y = Screen.height - height - 16f;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(x, y, PanelWidth, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            x += 10f;
            y += 6f;

            GUI.Label(new Rect(x, y, inner, 24f), ship.Spec.Name, _title);
            y += 26f;
            if (ship.PlayerControlled)
            {
                GUI.Label(new Rect(x, y, inner, 22f), "Máquina:  " + EngineTelegraph.DisplayName(m.Telegraph.Order) + "   [W/S]", _label);
                y += 22f;
                GUI.Label(new Rect(x, y, inner, 22f), "Timón:  " + RudderBar(m.Rudder) + "   [A/D]", _label);
                y += 22f;
            }
            else
            {
                GUI.Label(new Rect(x, y, inner, 22f), "Timón y máquina los lleva la dotación", _label);
                y += 22f;
            }
            GUI.Label(new Rect(x, y, inner, 22f), m.SpeedKnots.ToString("0.0") + " nudos  ·  rumbo " + m.HeadingDeg.ToString("000") + "°", _label);
            y += 22f;

            if (showDamage)
            {
                y += 4f;
                ShipDamageState s = damage.State;
                DrawBar(x, ref y, inner, "Casco", s.IntegrityFraction, Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.45f, 0.8f, 0.45f), s.IntegrityFraction));
                DrawBar(x, ref y, inner, "Agua", s.FloodFraction, s.FloodFraction > 0.6f ? new Color(0.9f, 0.35f, 0.2f) : new Color(0.35f, 0.65f, 0.95f));
                DrawBar(x, ref y, inner, "Vapor", 1f - s.BoilerDamage, new Color(0.85f, 0.75f, 0.45f));
                y += 2f;
            }

            foreach (string line in _lines)
            {
                float h = _small.CalcHeight(new GUIContent(line), inner);
                GUI.Label(new Rect(x, y, inner, h), line, _small);
                y += h + 3f;
            }
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(x, y + 2f, inner, 20f), "[Tab] cámara  ·  [Mayús] catalejo  ·  [Esc] pausa", _small);
            GUI.color = Color.white;
        }

        private void DrawBar(float x, ref float y, float width, string name, float fraction, Color color)
        {
            const float labelWidth = 54f;
            GUI.Label(new Rect(x, y - 2f, labelWidth, 20f), name, _small);
            var back = new Rect(x + labelWidth, y + 3f, width - labelWidth - 44f, 10f);
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * Mathf.Clamp01(fraction), back.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.xMax + 6f, y - 2f, 40f, 20f), (fraction * 100f).ToString("0") + " %", _small);
            y += 20f;
        }

        private void DrawAlert(string text, float y)
        {
            var rect = new Rect((Screen.width - 460f) * 0.5f, y, 460f, 32f);
            GUI.color = new Color(0.35f, 0.05f, 0.03f, 0.8f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, _alert);
        }

        private void DrawAlerts(ShipDamageState s, float y)
        {
            string text = null;
            if (s.SteeringJammed) text = "¡TIMÓN TRABADO!  Pulsa [3]";
            else if (s.FloodFraction > 0.25f || s.InflowRate > s.BasePumpRate) text = "¡VÍA DE AGUA!  Pulsa [2]";
            else if (s.FireIntensity > 0.4f) text = "¡INCENDIO A BORDO!  Pulsa [1]";
            if (text == null) return;
            if (s.ActiveAction != null) text = "La brigada está trabajando…";
            else if (s.CooldownRemaining > 0f) text = text.Substring(0, text.IndexOf('!') + 1) + "  Brigada lista en " + s.CooldownRemaining.ToString("0") + " s";
            DrawAlert(text, y);
        }

        private static string RudderBar(float rudder)
        {
            const int half = 5;
            int pos = Mathf.RoundToInt(rudder * half);
            var chars = new char[half * 2 + 1];
            for (int i = 0; i < chars.Length; i++) chars[i] = '·';
            chars[half] = '|';
            chars[half + pos] = '■';
            return "Br " + new string(chars) + " Er";
        }

        // ------------------------------------------------------------------------------------------
        // Rosa de marcaciones: la proa al centro, la popa en los extremos y, en verde, por dónde tira la batería
        // ------------------------------------------------------------------------------------------

        private void DrawRose()
        {
            if (ship.Motion == null || _contacts.Count == 0) return;
            float left = Mathf.Max(480f, (Screen.width - RoseWidth) * 0.5f);
            float width = Mathf.Min(RoseWidth, Screen.width - left - 16f);
            const float top = 12f, height = 28f;
            float X(float relative) => left + (Mathf.Clamp(relative, -180f, 180f) + 180f) / 360f * width;

            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(left, top, width, height + 20f), Texture2D.whiteTexture);

            var battery = ship.GetComponent<BroadsideBatteryController>();
            if (battery != null && battery.Model != null && ship.GetComponent<ColesTurretController>() == null)
            {
                float arc = battery.Model.ArcHalfWidthDeg;
                GUI.color = new Color(0.3f, 0.8f, 0.4f, 0.4f);
                GUI.DrawTexture(new Rect(X(-90f - arc), top + 8f, X(-90f + arc) - X(-90f - arc), 12f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(X(90f - arc), top + 8f, X(90f + arc) - X(90f - arc), 12f), Texture2D.whiteTexture);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(left, top + 13f, width, 2f), Texture2D.whiteTexture);
            GUI.color = Ink;
            GUI.Label(new Rect(X(0f) - 40f, top + height - 2f, 80f, 20f), "proa", _center);
            GUI.Label(new Rect(X(-90f) - 40f, top + height - 2f, 80f, 20f), "babor", _center);
            GUI.Label(new Rect(X(90f) - 40f, top + height - 2f, 80f, 20f), "estribor", _center);
            GUI.Label(new Rect(left, top + height - 2f, 40f, 20f), "popa", _center);
            GUI.Label(new Rect(left + width - 40f, top + height - 2f, 40f, 20f), "popa", _center);

            foreach (NavalContact contact in _contacts)
            {
                if (contact.Where == null) continue;
                Vector3 to = contact.Where.position - ship.transform.position;
                float relative = Mathf.DeltaAngle(ship.Motion.HeadingDeg, Ballistics.Bearing(0f, 0f, to.x, to.z));
                GUI.color = contact.Enemy ? EnemyColor : AllyColor;
                GUI.DrawTexture(new Rect(X(relative) - 4f, top + 2f, 8f, 24f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
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
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = Ink;
            _small = new GUIStyle(_label) { fontSize = 13, wordWrap = true };
            _title = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            _center = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _alert = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _alert.normal.textColor = Color.white;
            _mark = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mark.normal.textColor = Color.white;
        }
    }
}
