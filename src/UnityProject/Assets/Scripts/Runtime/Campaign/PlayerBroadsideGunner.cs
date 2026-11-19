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
    /// Espacio, clic, A o RT: fuego. Una aguja que se mueve con la cubierta y un aviso grande («¡AHORA!») ayudan a
    /// elegir el momento; si el enemigo no queda de costado, el panel dice hacia dónde girar.
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

        /// <summary>Parte del balanceo que llega al tiro (1 = todo). Por debajo de 1, además, hay andanada perfecta.</summary>
        public float RollForgiveness { get; set; } = 1f;

        /// <summary>Con la cubierta tan horizontal (°), la andanada sale agrupada (solo si <see cref="RollForgiveness"/> es menor que 1).</summary>
        public float PerfectRollDeg { get; set; }

        private const float NormalDispersionDeg = BroadsideBatteryModel.DefaultDispersionDeg;

        /// <summary>Modelo de la batería que sirve el jugador (sectores de tiro y recarga).</summary>
        public BroadsideBatteryModel Battery => _battery != null ? _battery.Model : null;

        /// <summary>Balanceo propio del buque (el Cochrane de Angamos es más estable que la Esmeralda de Iquique).</summary>
        public void SetRoll(float amplitudeDeg, float periodSeconds)
        {
            rollAmplitudeDeg = amplitudeDeg;
            rollPeriodSeconds = periodSeconds;
            _roll = new DeckRollModel(rollAmplitudeDeg, rollPeriodSeconds, 0.3f);
        }

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
            error = side == BroadsideSide.None ? 0f : _roll.ElevationErrorDeg(side) * RollForgiveness;
            return side != BroadsideSide.None;
        }

        private void Fire()
        {
            if (!Solve(out BroadsideSide side, out _, out float error))
            {
                Say("No lo tienes de lado: gira el barco");
                return;
            }
            if (!_battery.Model.IsLoaded(side))
            {
                Say("Los cañones aún se cargan… " + _battery.Model.ReloadRemaining(side).ToString("0") + " s");
                return;
            }
            // Las bandas cargan por separado: el balanceo del instante del disparo decide la caída.
            int before = _battery.Model.GunsPerSide;
            bool perfect = RollForgiveness < 1f && Mathf.Abs(_roll.AngleDeg) <= PerfectRollDeg;
            _battery.Model.DispersionDeg = perfect ? NormalDispersionDeg * 0.5f : NormalDispersionDeg;
            if (_battery.TryFireAt(target, error))
            {
                Say((perfect ? "¡Disparo perfecto! " : "¡Fuego! ") + before + " cañones");
                PlayerFired?.Invoke(before);
                GameInput.Rumble(1f, 0.5f, 0.45f);
            }
            else
            {
                Say("Está muy lejos: acércate");
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
                _label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = new Color(0.95f, 0.92f, 0.82f);
                _big = new GUIStyle(_label) { fontSize = 28, fontStyle = FontStyle.Bold };
            }

            const float width = 460f, height = 150f;
            float cx = Screen.width * 0.5f, top = Screen.height - height - 70f;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(cx - width * 0.5f, top, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool solved = Solve(out BroadsideSide side, out float range, out float error);
            float v = _battery.Model.Gun.MuzzleVelocityMps;
            float fallHeight = solved ? DeckRollModel.MissHeight(v, range, error) : float.NaN;
            bool onTarget = solved && fallHeight >= -targetBand.x && fallHeight <= targetBand.y;
            bool loaded = solved && _battery.Model.IsLoaded(side);

            // Lo más importante, en grande: qué hacer ahora.
            string say;
            Color sayColor = new Color(1f, 0.85f, 0.4f);
            if (Time.time < _statusUntil) say = _status;
            else if (!solved) say = _ship.PlayerControlled ? TurnAdvice() : "Espera: el barco está girando";
            else if (float.IsNaN(fallHeight)) say = "Está muy lejos: acércate";
            else if (!loaded) say = "Cargando cañones… " + _battery.Model.ReloadRemaining(side).ToString("0") + " s";
            else if (onTarget)
            {
                say = "¡AHORA!  Pulsa " + ControlPrompts.Key(Act.ShipFire);
                sayColor = new Color(0.5f, 1f, 0.55f);
            }
            else say = "Espera a que la raya entre en lo verde";
            GUI.color = sayColor;
            GUI.Label(new Rect(cx - width * 0.5f, top + 10f, width, 40f), say, _big);
            GUI.color = Color.white;

            // La raya se mueve con el balanceo; en la zona verde, los cañones apuntan al enemigo.
            float scale = 190f / Mathf.Max(0.5f, _roll.AmplitudeDeg);
            float good = solved ? BandHalfWidthDeg(v, range) * scale : 0f;
            float barY = top + 66f;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(cx - 200f, barY, 400f, 30f), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.9f, 0.4f, 0.75f);
            GUI.DrawTexture(new Rect(cx - good, barY, 2f * good, 30f), Texture2D.whiteTexture);
            float needle = Mathf.Clamp((side == BroadsideSide.Port ? -_roll.AngleDeg : _roll.AngleDeg) * scale, -200f, 200f);
            GUI.color = onTarget ? Color.white : new Color(1f, 0.75f, 0.25f);
            GUI.DrawTexture(new Rect(cx + needle - 4f, barY - 6f, 8f, 42f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string where = !solved ? "El enemigo: " + DistanceWords(range)
                : "Cañones de la " + (side == BroadsideSide.Port ? "izquierda" : "derecha") + "  ·  enemigo " + DistanceWords(range);
            GUI.Label(new Rect(cx - width * 0.5f, top + 108f, width, 30f), where, _label);
        }

        private static string DistanceWords(float meters) => meters < 900f ? "cerca" : meters < 2500f ? "a media distancia" : "lejos";

        /// <summary>Hacia dónde girar para poner al enemigo de costado.</summary>
        private string TurnAdvice()
        {
            Vector3 to = target.transform.position - transform.position;
            float relative = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, Ballistics.Bearing(0f, 0f, to.x, to.z));
            bool ahead = Mathf.Abs(relative) < 90f;
            // Por la proa se le aparta la proa; por la popa, se le acerca.
            bool toPort = ahead ? relative > 0f : relative < 0f;
            return toPort ? "◄ Gira a la izquierda" : "Gira a la derecha ►";
        }

        /// <summary>Semiancho (°) del balanceo con el que la andanada pasa por el blanco (para la zona verde).</summary>
        private float BandHalfWidthDeg(float v, float range)
        {
            // La altura en el blanco crece casi linealmente con el error: basta la pendiente.
            float slope = DeckRollModel.MissHeight(v, range, 0.1f) - DeckRollModel.MissHeight(v, range, 0f);
            if (float.IsNaN(slope) || slope <= 0f) return 0f;
            return Mathf.Min(targetBand.x, targetBand.y) / slope * 0.1f / Mathf.Max(0.05f, RollForgiveness);
        }
    }
}
