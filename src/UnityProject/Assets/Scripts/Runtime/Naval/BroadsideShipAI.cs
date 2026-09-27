using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// IA mínima de un buque de costado para el prototipo de Iquique: procura presentar el través al enemigo
    /// a la distancia deseada, sin alejarse de su fondeadero, y dispara cuando una banda lo bate.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class BroadsideShipAI : MonoBehaviour
    {
        [SerializeField] private ShipController enemy;
        [SerializeField] private EngineOrder cruiseOrder = EngineOrder.FullAhead;
        [SerializeField] private float preferredRangeM = 600f;
        [SerializeField] private float maxRangeM = 2500f;
        [Tooltip("Radio máximo alrededor de la posición inicial (la Esmeralda se mantenía en la rada).")]
        [SerializeField] private float leashRadiusM = 900f;
        [SerializeField] private float thinkIntervalSeconds = 0.5f;

        private ShipController _ship;
        private BroadsideBatteryController _battery;
        private ShipDamageController _damage;
        private Vector3 _anchor;
        private float _nextThink;

        public ShipController Enemy
        {
            get => enemy;
            set => enemy = value;
        }

        private void Start()
        {
            _ship = GetComponent<ShipController>();
            _battery = GetComponent<BroadsideBatteryController>();
            _damage = GetComponent<ShipDamageController>();
            _ship.PlayerControlled = false;
            _anchor = transform.position;
        }

        private void Update()
        {
            if (Time.time < _nextThink || _ship.Motion == null || enemy == null) return;
            _nextThink = Time.time + thinkIntervalSeconds;
            if (_damage != null && _damage.State != null && _damage.State.IsSunk) return;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            float range = toEnemy.magnitude;
            float enemyBearing = Ballistics.Bearing(0f, 0f, toEnemy.x, toEnemy.z);

            // Rumbo deseado: través al enemigo; se cierra o abre el ángulo para corregir la distancia.
            float side = Mathf.Sign(Mathf.DeltaAngle(_ship.Motion.HeadingDeg, enemyBearing));
            float rangeCorrection = Mathf.Clamp((range - preferredRangeM) / preferredRangeM, -1f, 1f) * 30f;
            float desired = enemyBearing - side * (90f - rangeCorrection);

            // Correa al fondeadero: si se aleja demasiado, vuelve hacia él.
            Vector3 fromAnchor = transform.position - _anchor;
            if (fromAnchor.magnitude > leashRadiusM)
            {
                desired = Ballistics.Bearing(transform.position.x, transform.position.z, _anchor.x, _anchor.z);
            }

            float error = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, desired);
            _ship.Command(cruiseOrder, Mathf.Clamp(error / 25f, -1f, 1f));

            if (_battery != null && range <= maxRangeM) _battery.TryFireAt(enemy);
        }
    }
}
