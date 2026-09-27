using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// HUD de prototipo para la fase FPS (IMGUI, sin assets): resistencia, postura, arma y graduación del alza.
    /// Sin cruz de mira al apuntar: la puntería se hace con las miras del arma, como en 1879.
    /// </summary>
    public sealed class InfantryHud : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private RifleController rifle;
        [SerializeField] private bool showDebug = true;

        private GUIStyle _label;
        private Texture2D _white;
        private float _smoothedFps = 60f;

        public FirstPersonController Player
        {
            get => player;
            set => player = value;
        }

        public RifleController Rifle
        {
            get => rifle;
            set => rifle = value;
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f) _smoothedFps = Mathf.Lerp(_smoothedFps, 1f / Time.unscaledDeltaTime, 0.05f);
        }

        private void OnGUI()
        {
            if (player == null || player.Motor == null) return;
            EnsureStyles();
            InfantryMotor m = player.Motor;

            // Punto central discreto solo a la cadera.
            float aim = player.Sights != null ? player.Sights.Eased : 0f;
            if (aim < 0.5f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f * (1f - aim * 2f));
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), _white);
            }

            // Resistencia.
            const float barWidth = 220f;
            float x = 20f, y = Screen.height - 70f;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x, y, barWidth, 10f), _white);
            GUI.color = m.IsExhausted ? new Color(0.85f, 0.3f, 0.2f) : new Color(0.9f, 0.82f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, barWidth * m.Stamina01, 10f), _white);
            GUI.color = Color.white;

            string stance = m.Stance == Stance.Sliding ? "Deslizándose" : m.Stance == Stance.Crouching ? "Agachado" : m.IsSprinting ? "Corriendo" : "De pie";
            GUI.Label(new Rect(x, y - 24f, 400f, 22f), stance + (m.IsExhausted ? " · AGOTADO" : string.Empty), _label);

            if (player.Weapon != null)
            {
                string sight = player.Ladder != null ? "   Alza: " + player.Ladder.RangeM.ToString("0") + " m [rueda al apuntar]" : string.Empty;
                GUI.Label(new Rect(x, y + 16f, 700f, 22f), player.Weapon.DisplayName + sight, _label);
            }

            if (rifle != null && rifle.Model != null)
            {
                RifleCycleModel r = rifle.Model;
                string rounds = (r.Chambered ? "1" : "0") + (r.MagazineCapacity > 0 ? " + " + r.MagazineRounds : string.Empty) + " | " + r.Reserve;
                string state = r.TotalRounds == 0 ? "SIN MUNICIÓN"
                    : r.IsReady && !r.Chambered ? "Vacío [R recargar]"
                    : StageName(r.Stage);
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 70f, 320f, 22f), "Cartuchos: " + rounds, _label);
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 48f, 320f, 22f), state, _label);
                string lastHit = ShootingTarget.DescribeLastHit();
                if (lastHit.Length > 0) GUI.Label(new Rect(Screen.width - 330f, Screen.height - 94f, 320f, 22f), "Impacto: " + lastHit, _label);
            }

            if (showDebug)
            {
                GUI.Label(new Rect(Screen.width - 260f, 16f, 250f, 22f),
                    _smoothedFps.ToString("0") + " FPS · " + m.HorizontalSpeed.ToString("0.0") + " m/s", _label);
            }
        }

        public static string StageName(RifleStage stage)
        {
            switch (stage)
            {
                case RifleStage.Firing: return "Disparo";
                case RifleStage.Cocking: return "Amartillando…";
                case RifleStage.OpeningAction: return "Abriendo el cierre…";
                case RifleStage.Extracting: return "Expulsando la vaina…";
                case RifleStage.InsertingCartridge: return "Cargando cartucho…";
                case RifleStage.ClosingAction: return "Cerrando…";
                case RifleStage.Shouldering: return "Encarando…";
                case RifleStage.LoadingMagazine: return "Cargando el depósito…";
                default: return "Listo";
            }
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _label.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
            _white = Texture2D.whiteTexture;
        }
    }
}
