using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// HUD de prototipo (IMGUI, sin assets): telégrafo, velocidad, rumbo y timón del buque del jugador.
    /// Se sustituirá por UI Toolkit en la fase de pulido; su valor es dar lectura inmediata al calibrar la maniobra.
    /// </summary>
    public sealed class NavalHud : MonoBehaviour
    {
        [SerializeField] private ShipController ship;

        private GUIStyle _label;
        private GUIStyle _title;

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
        public System.Collections.Generic.List<System.Func<string>> ExtraLines { get; } =
            new System.Collections.Generic.List<System.Func<string>>();

        private void OnGUI()
        {
            if (ship == null || ship.Motion == null) return;
            EnsureStyles();

            ShipMotionModel m = ship.Motion;
            const float width = 290f;
            var lines = new System.Collections.Generic.List<string>(ExtraLines.Count);
            foreach (var line in ExtraLines)
            {
                string text = line();
                if (!string.IsNullOrEmpty(text)) lines.Add(text); // las líneas de otro buque o de un sistema inactivo vienen vacías
            }
            float height = 150f + lines.Count * 22f;
            GUILayout.BeginArea(new Rect(16f, Screen.height - height - 16f, width, height), GUI.skin.box);
            GUILayout.Label(ship.Spec.Name, _title);
            GUILayout.Label("Telégrafo:  " + EngineTelegraph.DisplayName(m.Telegraph.Order) + "   [W/S]", _label);
            GUILayout.Label("Velocidad:  " + m.SpeedKnots.ToString("0.0") + " nudos", _label);
            GUILayout.Label("Rumbo:      " + m.HeadingDeg.ToString("000") + "°", _label);
            GUILayout.Label("Timón:      " + RudderBar(m.Rudder) + "   [A/D]", _label);
            foreach (string line in lines) GUILayout.Label(line, _label);
            GUILayout.EndArea();
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

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = new Color(0.93f, 0.9f, 0.8f);
            _title = new GUIStyle(_label) { fontSize = 17, fontStyle = FontStyle.Bold };
        }
    }
}
