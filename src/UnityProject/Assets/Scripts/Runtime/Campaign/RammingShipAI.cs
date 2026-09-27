using Pacifico.Core.Naval;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Campaign
{
    public enum RammingMode
    {
        /// <summary>Sin órdenes: manda el jugador o nadie.</summary>
        Idle,
        /// <summary>Se mantiene a distancia de tiro y dispara la torre.</summary>
        StandOff,
        /// <summary>Carrera de embestida; tras cada espolonazo, da atrás, abre distancia y vuelve a embestir.</summary>
        Ram,
    }

    /// <summary>
    /// El Huáscar de Grau en el capítulo 1 cuando no lo gobierna el jugador: primero cañonea a distancia y después
    /// embiste. La embestida apunta al punto de encuentro (adelanto por la marcha del blanco); tras el choque da
    /// atrás para desencajar el espolón, se abre y vuelve a la carga, como hizo tres veces el 21 de mayo.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class RammingShipAI : MonoBehaviour
    {
        [SerializeField] private ShipController target;
        [SerializeField] private RamBow ram;
        [SerializeField] private ColesTurretController turret;
        [SerializeField] private float standOffMinM = 300f;
        [SerializeField] private float standOffMaxM = 700f;
        [Tooltip("Distancia a la que se abre antes de cada nueva carrera de embestida.")]
        [SerializeField] private float runUpDistanceM = 200f;
        [Tooltip("Tras el choque da atrás al menos este tiempo…")]
        [SerializeField] private float backOffSeconds = 5f;
        [Tooltip("…y hasta separarse esta distancia (entre centros), para no volver a tocarla al virar.")]
        [SerializeField] private float backOffClearM = 100f;
        [SerializeField] private float backOffMaxSeconds = 25f;
        [Tooltip("Intervalo mínimo entre disparos de la torre cuando la sirve la IA (el tiro del Huáscar fue lento).")]
        [SerializeField] private float aiFireIntervalSeconds = 25f;
        [SerializeField] private float thinkIntervalSeconds = 0.25f;

        private ShipController _ship;
        private ShipDamageController _damage;
        private RammingMode _mode = RammingMode.StandOff;
        private float _backOff;
        private float _backOffElapsed;
        private bool _runningUp;
        private float _nextThink;
        private float _nextShot;

        public ShipController Target
        {
            get => target;
            set => target = value;
        }

        public RammingMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                _runningUp = false;
                _backOff = 0f;
            }
        }

        /// <summary>Máquina en la carrera de embestida (el primer espolonazo de Grau fue a poca velocidad).</summary>
        public EngineOrder RamOrder { get; set; } = EngineOrder.FullAhead;

        /// <summary>La torre no dispara (p. ej. para no decidir por el jugador un hundimiento que el guion no quiere aún).</summary>
        public bool HoldFire { get; set; }

        public void Configure(ShipController enemy, RamBow bow, ColesTurretController coles)
        {
            target = enemy;
            ram = bow;
            turret = coles;
        }

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            _damage = GetComponent<ShipDamageController>();
            if (ram == null) ram = GetComponentInChildren<RamBow>();
            if (turret == null) turret = GetComponent<ColesTurretController>();
        }

        private void OnEnable()
        {
            if (ram != null) ram.Rammed += OnRammed;
            _nextShot = Time.time + aiFireIntervalSeconds * 0.5f;
        }

        private void OnDisable()
        {
            if (ram != null) ram.Rammed -= OnRammed;
        }

        private void OnRammed(RamBow bow, ShipController victim, RamResult result)
        {
            if (victim != target || _mode != RammingMode.Ram) return;
            _backOff = backOffSeconds;
            _backOffElapsed = 0f;
            _runningUp = true;
        }

        private bool Sunk => _damage != null && _damage.State != null && _damage.State.IsSunk;

        private void Update()
        {
            if (_mode == RammingMode.Idle || target == null || _ship.Motion == null || Sunk) return;
            AimTurret();
            if (Time.time < _nextThink) return;
            _nextThink = Time.time + thinkIntervalSeconds;

            Vector3 to = target.transform.position - transform.position;
            float range = new Vector2(to.x, to.z).magnitude;
            float bearing = Ballistics.Bearing(0f, 0f, to.x, to.z);

            if (_backOff > 0f)
            {
                // Da atrás el tiempo mínimo y, después, hasta quedar separado (o hasta el máximo, por si se atasca).
                _backOffElapsed += thinkIntervalSeconds;
                _backOff -= thinkIntervalSeconds;
                if (_backOff <= 0f && range < backOffClearM && _backOffElapsed < backOffMaxSeconds) _backOff = thinkIntervalSeconds;
                _ship.Command(EngineOrder.HalfAstern, 0f); // desencajar el espolón
                return;
            }

            float desired;
            EngineOrder order;
            if (_mode == RammingMode.Ram)
            {
                if (_runningUp && range >= runUpDistanceM) _runningUp = false;
                if (_runningUp)
                {
                    // Abrirse por la banda que menos cueste, casi perpendicular al blanco.
                    float side = Mathf.Sign(Mathf.DeltaAngle(bearing, _ship.Motion.HeadingDeg));
                    desired = bearing + side * 110f;
                    order = EngineOrder.FullAhead;
                }
                else
                {
                    // Punto de encuentro: el blanco avanza mientras el espolón recorre la distancia.
                    float closing = Mathf.Max(1f, _ship.Motion.Speed);
                    Vector3 meet = target.transform.position + target.Velocity * (range / closing);
                    desired = Ballistics.Bearing(transform.position.x, transform.position.z, meet.x, meet.z);
                    order = RamOrder;
                }
            }
            else
            {
                if (range > standOffMaxM) desired = bearing;
                else if (range < standOffMinM) desired = bearing + 180f;
                else
                {
                    float side = Mathf.Sign(Mathf.DeltaAngle(bearing, _ship.Motion.HeadingDeg));
                    desired = bearing + side * 90f; // rodear al blanco con la torre libre
                }
                order = range > standOffMaxM ? EngineOrder.FullAhead : EngineOrder.HalfAhead;
            }

            float error = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, desired);
            _ship.Command(order, Mathf.Clamp(error / 20f, -1f, 1f));
        }

        private void AimTurret()
        {
            if (turret == null || turret.PlayerControlled || !turret.enabled || turret.Model == null) return;
            turret.AimAt(target.transform.position, target.Velocity);
            if (HoldFire || Time.time < _nextShot || !turret.Model.IsOnTarget(1f)) return;
            turret.RequestFire();
            _nextShot = Time.time + aiFireIntervalSeconds;
        }
    }
}
