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
        [SerializeField] private MeleeController melee;
        [SerializeField] private bool showDebug = true;

        private GUIStyle _label;
        private GUIStyle _center;
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

        public MeleeController Melee
        {
            get => melee;
            set => melee = value;
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f) _smoothedFps = Mathf.Lerp(_smoothedFps, 1f / Time.unscaledDeltaTime, 0.05f);
        }

        private float _hitTime = -10f;
        private bool _hitKill;

        private void OnEnable() => Combatant.Hit += OnCombatantHit;
        private void OnDisable() => Combatant.Hit -= OnCombatantHit;

        /// <summary>Un enemigo herido por el jugador: marca en la mira (roja y más grande si cae).</summary>
        private void OnCombatantHit(Combatant victim, GameObject attacker, bool down)
        {
            if (player == null || attacker == null || victim == null || victim.IsPlayer) return;
            if (attacker.transform.root != player.transform.root) return;
            _hitTime = Time.time;
            _hitKill = down;
            Pacifico.Input.GameInput.Rumble(0.15f, 0.35f, 0.08f);
        }

        /// <summary>Mira en cruz, bien visible sobre la arena; se aparta al encarar (ahí mandan las miras del fusil).</summary>
        private void DrawCrosshair(float aim)
        {
            float alpha = 1f - Mathf.Clamp01(aim * 2f);
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            if (alpha > 0.01f)
            {
                const float gap = 6f, length = 10f, thick = 2f;
                for (int pass = 0; pass < 2; pass++)
                {
                    GUI.color = pass == 0 ? new Color(0f, 0f, 0f, 0.6f * alpha) : new Color(1f, 1f, 1f, 0.9f * alpha);
                    float o = pass == 0 ? 1f : 0f, t = thick + (pass == 0 ? 2f : 0f);
                    GUI.DrawTexture(new Rect(cx - gap - length - o, cy - t * 0.5f, length + 2f * o, t), _white);
                    GUI.DrawTexture(new Rect(cx + gap - o, cy - t * 0.5f, length + 2f * o, t), _white);
                    GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - gap - length - o, t, length + 2f * o), _white);
                    GUI.DrawTexture(new Rect(cx - t * 0.5f, cy + gap - o, t, length + 2f * o), _white);
                }
            }
            // Aviso de impacto: una X que se desvanece en medio segundo.
            float since = Time.time - _hitTime;
            if (since < 0.5f)
            {
                float a = 1f - since / 0.5f, size = _hitKill ? 30f : 22f;
                GUI.color = _hitKill ? new Color(1f, 0.3f, 0.25f, a) : new Color(1f, 1f, 1f, a);
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
                GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - 1.5f, size, 3f), _white);
                GUI.DrawTexture(new Rect(cx - 1.5f, cy - size * 0.5f, 3f, size), _white);
                GUI.matrix = m;
                if (_hitKill) GUI.Label(new Rect(cx - 100f, cy + 24f, 200f, 24f), "¡Abatido!", _center);
            }
            GUI.color = Color.white;
        }

        private void OnGUI()
        {
            if (player == null || player.Motor == null) return;
            EnsureStyles();
            InfantryMotor m = player.Motor;

            float aim = player.Sights != null ? player.Sights.Eased : 0f;
            DrawCrosshair(aim);

            // Fondos oscuros: sobre la arena del desierto el texto blanco no se lee.
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(10f, Screen.height - 142f, 420f, 76f), _white);
            if (rifle != null && rifle.Model != null) GUI.DrawTexture(new Rect(Screen.width - 340f, Screen.height - 114f, 330f, 52f), _white);
            GUI.color = Color.white;

            // Resistencia.
            const float barWidth = 220f;
            float x = 20f, y = Screen.height - 110f;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x, y, barWidth, 10f), _white);
            GUI.color = m.IsExhausted ? new Color(0.85f, 0.3f, 0.2f) : new Color(0.9f, 0.82f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, barWidth * m.Stamina01, 10f), _white);
            GUI.color = Color.white;

            string stance = m.Stance == Stance.Sliding ? "Deslizándose" : m.Stance == Stance.Crouching ? "Agachado" : m.IsSprinting ? "Corriendo" : "De pie";
            GUI.Label(new Rect(x, y - 24f, 400f, 22f), stance + (m.IsExhausted ? " · AGOTADO" : string.Empty), _label);

            if (player.Weapon != null)
            {
                string sight = player.Ladder != null ? "   Alza: " + player.Ladder.RangeM.ToString("0") + " m" : string.Empty;
                GUI.Label(new Rect(x, y + 16f, 700f, 22f), player.Weapon.DisplayName + sight, _label);
            }

            if (rifle != null && rifle.Model != null)
            {
                RifleCycleModel r = rifle.Model;
                string rounds = (r.Chambered ? "1" : "0") + (r.MagazineCapacity > 0 ? " + " + r.MagazineRounds : string.Empty) + " | " + r.Reserve;
                string state = r.TotalRounds == 0 ? "¡SIN BALAS! Busca más"
                    : r.IsReady && !r.Chambered ? "Vacío: " + Pacifico.Input.ControlPrompts.Key(Pacifico.Input.Act.Reload) + " recargar"
                    : StageName(r.Stage);
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 110f, 320f, 22f), "Balas: " + rounds, _label);
                GUI.Label(new Rect(Screen.width - 330f, Screen.height - 86f, 320f, 22f), state, _label);
                string lastHit = ShootingTarget.DescribeLastHit();
                if (lastHit.Length > 0) GUI.Label(new Rect(Screen.width - 330f, Screen.height - 136f, 320f, 22f), "Impacto: " + lastHit, _label);
            }

            if (melee != null)
            {
                // Cuerpo a cuerpo: aviso de guardia bajo el centro de la pantalla y último golpe asestado.
                if (melee.Guard > 0.5f && !melee.Model.IsBusy && (melee.ThrustProfile != null || melee.SlashProfile != null))
                {
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((melee.Guard - 0.5f) * 2f));
                    string keys = (melee.ThrustProfile != null ? Pacifico.Input.ControlPrompts.Key(Pacifico.Input.Act.Bayonet) + " estocada" : string.Empty) +
                                  (melee.ThrustProfile != null && melee.SlashProfile != null ? " · " : string.Empty) +
                                  (melee.SlashProfile != null ? Pacifico.Input.ControlPrompts.Key(Pacifico.Input.Act.Slash) + " tajo" : string.Empty);
                    GUI.Label(new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.5f + 40f, 260f, 22f), "EN GUARDIA  " + keys, _label);
                    GUI.color = Color.white;
                }
                string strike = MeleeDummy.DescribeLastStrike();
                if (strike.Length > 0) GUI.Label(new Rect(Screen.width - 330f, Screen.height - 162f, 320f, 22f), "Golpe: " + strike, _label);
            }

            Pacifico.Input.ControlPrompts.DrawBar(12f, Pacifico.Input.Act.Move, Pacifico.Input.Act.Fire, Pacifico.Input.Act.Aim,
                                                  Pacifico.Input.Act.Reload, Pacifico.Input.Act.Use, Pacifico.Input.Act.Pause);

            if (showDebug && Debug.isDebugBuild)
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
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _label.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
            _center = new GUIStyle(_label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _white = Texture2D.whiteTexture;
        }
    }
}
