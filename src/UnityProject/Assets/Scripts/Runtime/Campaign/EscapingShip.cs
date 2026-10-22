using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Buque de decorado que navega a rumbo y velocidad fijos y, a la orden, se separa a toda máquina (la corbeta Unión
    /// en Angamos). No tiene ficha ni simulación naval: no combate y no recibe impactos, solo se ve alejarse.
    /// </summary>
    public sealed class EscapingShip : MonoBehaviour
    {
        [SerializeField] private float headingDeg;
        [SerializeField] private float speedMps = 5f;
        [Tooltip("Velocidad al separarse (m/s).")]
        [SerializeField] private float escapeSpeedMps = 7f;
        [Tooltip("Grados por segundo al cambiar de rumbo.")]
        [SerializeField] private float turnRateDegPerSecond = 2f;

        private float _targetHeading;
        private float _targetSpeed;
        private float _speed;

        public bool Escaping { get; private set; }

        public void Configure(float heading, float speed, float escapeSpeed)
        {
            headingDeg = heading;
            speedMps = speed;
            escapeSpeedMps = escapeSpeed;
        }

        private void Start()
        {
            _targetHeading = headingDeg;
            _targetSpeed = speedMps;
            _speed = speedMps;
            transform.rotation = Quaternion.Euler(0f, headingDeg, 0f);
        }

        /// <summary>Se separa a toda máquina por el rumbo dado.</summary>
        public void BreakAway(float heading)
        {
            Escaping = true;
            _targetHeading = heading;
            _targetSpeed = escapeSpeedMps;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            headingDeg = Mathf.MoveTowardsAngle(headingDeg, _targetHeading, turnRateDegPerSecond * dt);
            _speed = Mathf.MoveTowards(_speed, _targetSpeed, 0.05f * dt);
            transform.rotation = Quaternion.Euler(0f, headingDeg, 0f);
            transform.position += transform.forward * (_speed * dt);
        }
    }
}
