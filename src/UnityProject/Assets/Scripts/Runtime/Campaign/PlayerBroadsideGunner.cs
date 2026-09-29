using System;
using Pacifico.Core.Naval;
using Pacifico.Input;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// El jugador sirve la batería de costado (capítulo 1, acto I: el grumete Wenceslao Vargas en las piezas de
    /// 40 libras de la Esmeralda). La maniobra la lleva el buque; el jugador elige el instante del disparo: la
    /// cubierta balancea y la andanada sale con el error de elevación del momento (<see cref="DeckRollModel"/>).
    /// Espacio o clic: fuego. Un clinómetro y la caída prevista («CORTO / AL BLANCO / LARGO») ayudan a elegir.
    /// </summary>
    [RequireComponent(typeof(BroadsideBatteryController))]
    public sealed class PlayerBroadsideGunner : MonoBehaviour
    {
        [SerializeField] private ShipController target;
        [SerializeField] private float rollAmplitudeDeg = DeckRollModel.DefaultAmplitudeDeg;
        [SerializeField] private float rollPeriodSeconds = DeckRollModel.DefaultPeriodSeconds;
        [Tooltip("Altura útil del blanco sobre y bajo la cota de puntería (m): la obra muerta del Huáscar.")]
        [SerializeField] private Vector2 targetBand = new Vector2(2f, 3f);

        private ShipController _ship;
        private BroadsideBatteryController _battery;
        private ShipDamageController _damage;
        private DeckRollModel _roll;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _label;
        private GUIStyle _big;

        public DeckRollModel Roll => _roll;

        /// <summary>El jugador está en la batería (si no, el mar sigue balanceando el buque, pero no hay mandos).</summary>
        public bool PlayerActive { get; set; }

        public ShipController Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Andanada disparada por el jugador (número de piezas).</summary>
        public event Action<int> PlayerFired;

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            _battery = GetComponent<BroadsideBatteryController>();
            _damage = GetComponent<ShipDamageController>();
            _roll = new DeckRollModel(rollAmplitudeDeg, rollPeriodSeconds, 0.3f);
        }

        private bool Sunk => _damage != null && _damage.State != null && _damage.State.IsSunk;

        private void Update()
        {
            _roll.Step(Time.deltaTime);
            _ship.ExtraRollDeg = Sunk ? 0f : _roll.AngleDeg;
            if (!PlayerActive || Sunk || target == null || _battery.Model == null) return;
            if (GameInput.Pressed(GameKey.Space) || GameInput.MousePressed(0)) Fire();
        }

        private void OnDisable()
        {
            if (_ship != null) _ship.ExtraRollDeg = 0f;
        }

        private bool Solve(out BroadsideSide side, out float range, out float error)
        {
            Vector3 to = target.transform.position - transform.position;
            float bearing = Ballistics.Bearing(0f, 0f, to.x, to.z);
            float relative = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, bearing);
            side = _battery.Model.SideFor(relative);
            range = new Vector2(to.x, to.z).magnitude;
            error = side == BroadsideSide.None ? 0f : _roll.ElevationErrorDeg(side);
            return side != BroadsideSide.None;
        }

        private void Fire()
        {
            if (!Solve(out BroadsideSide side, out _, out float error))
            {
                Say("El Huáscar no está por el través");
                return;
            }
            if (!_battery.Model.IsLoaded(side))
            {
                Say("Cargando… " + _battery.Model.ReloadRemaining(side).ToString("0.0") + " s");
                return;
            }
            // Las bandas cargan por separado: el balanceo del instante del disparo decide la caída.
            int before = _battery.Model.GunsPerSide;
            if (_battery.TryFireAt(target, error))
            {
                Say("¡Fuego! " + before + " piezas de " + (side == BroadsideSide.Starboard ? "estribor" : "babor"));
                PlayerFired?.Invoke(before);
            }
            else
            {
                Say("Fuera de alcance");
            }
        }

        private void Say(string text)
        {
            _status = text;
            _statusUntil = Time.time + 2f;
        }

        // ------------------------------------------------------------------------------------------
        // Clinómetro (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!PlayerActive || Sunk || target == null || _battery.Model == null || _ship.Motion == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = new Color(0.95f, 0.92f, 0.82f);
                _big = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold };
            }

            float cx = Screen.width * 0.5f, top = Screen.height - 190f;
            GUI.Box(new Rect(cx - 170f, top, 340f, 150f), GUIContent.none);

            // Clinómetro: una aguja que oscila con la cubierta; la zona verde es el balanceo con el que se acierta.
            float angle = _roll.AngleDeg;
            float scale = 150f / Mathf.Max(0.5f, _roll.AmplitudeDeg);
            bool solved = Solve(out BroadsideSide side, out float range, out float error);
            float v = _battery.Model.Gun.MuzzleVelocityMps;
            float height = solved ? DeckRollModel.MissHeight(v, range, error) : float.NaN;
            bool onTarget = solved && height >= -targetBand.x && height <= targetBand.y;

            GUI.color = new Color(0.3f, 0.9f, 0.4f, 0.5f);
            float good = solved ? BandHalfWidthDeg(v, range) * scale : 0f;
            GUI.DrawTexture(new Rect(cx - good, top + 20f, 2f * good, 16f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(cx - 150f, top + 27f, 300f, 2f), Texture2D.whiteTexture);
            GUI.color = onTarget ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.8f, 0.3f);
            float needle = (side == BroadsideSide.Port ? -angle : angle) * scale;
            GUI.DrawTexture(new Rect(cx + needle - 2f, top + 14f, 4f, 28f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 170f, top + 42f, 340f, 20f), "Balanceo " + angle.ToString("+0.0;-0.0") + "°  " + (_roll.RateDegPerSecond > 0f ? "▲" : "▼"), _label);

            string fall;
            if (!solved) fall = "Sin blanco por el través";
            else if (float.IsNaN(height)) fall = "FUERA DE ALCANCE";
            else if (onTarget) fall = "AL BLANCO";
            else
            {
                float delta = DeckRollModel.FallRange(v, range, error) - range;
                fall = (delta < 0f ? "CORTO " : "LARGO ") + Mathf.Abs(delta).ToString("0") + " m";
            }
            GUI.color = onTarget ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(cx - 170f, top + 64f, 340f, 30f), fall, _big);
            GUI.color = Color.white;

            string loaded = side == BroadsideSide.None ? "—"
                : _battery.Model.IsLoaded(side) ? "cargada" : "cargando " + _battery.Model.ReloadRemaining(side).ToString("0") + " s";
            GUI.Label(new Rect(cx - 170f, top + 96f, 340f, 20f),
                "Batería de " + (side == BroadsideSide.Port ? "babor" : "estribor") + ": " + loaded + " · " + range.ToString("0") + " m", _label);
            GUI.Label(new Rect(cx - 170f, top + 120f, 340f, 20f), Time.time < _statusUntil ? _status : "[Espacio] ¡Fuego!", _label);
        }

        /// <summary>Semiancho (°) del balanceo con el que la andanada pasa por el blanco (para la zona verde).</summary>
        private float BandHalfWidthDeg(float v, float range)
        {
            // La altura en el blanco crece casi linealmente con el error: basta la pendiente.
            float slope = DeckRollModel.MissHeight(v, range, 0.1f) - DeckRollModel.MissHeight(v, range, 0f);
            if (float.IsNaN(slope) || slope <= 0f) return 0f;
            return Mathf.Min(targetBand.x, targetBand.y) / slope * 0.1f;
        }
    }
}
