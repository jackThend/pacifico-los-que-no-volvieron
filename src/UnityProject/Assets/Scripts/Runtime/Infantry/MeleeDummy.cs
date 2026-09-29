using Pacifico.Core.Common;
using Pacifico.Core.Melee;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Muñeco de práctica de esgrima de bayoneta (ROADMAP 3.4): un saco de paja en un poste, con torso y cabeza como
    /// colisionadores. Registra cada golpe (tipo, parte, daño y profundidad), se balancea en la dirección del golpe
    /// con un muelle amortiguado y suelta paja. También recibe balas.
    /// </summary>
    public sealed class MeleeDummy : MonoBehaviour, IMeleeTarget, IBulletTarget
    {
        [SerializeField] private Collider head;
        [SerializeField] private float headMultiplier = 1.5f;
        [SerializeField] private Color strawColor = new Color(0.86f, 0.74f, 0.42f);
        [Tooltip("Inclinación máxima por golpe (°) y rigidez del poste.")]
        [SerializeField] private float swayPerHitDeg = 14f;
        [SerializeField] private float swayFrequencyHz = 1.6f;
        [SerializeField, Range(0.05f, 1f)] private float swayDamping = 0.25f;

        private const float SwayStep = 1f / 240f;

        private Quaternion _restRotation;
        private Vector2 _tilt;
        private Vector2 _tiltVelocity;
        private float _accumulator;

        public int Hits { get; private set; }
        public float DamageTaken { get; private set; }

        /// <summary>Último muñeco golpeado y descripción del golpe (para el HUD).</summary>
        public static MeleeDummy LastStruck { get; private set; }
        public string LastDescription { get; private set; } = string.Empty;

        public Color ImpactColor => strawColor;

        public void Configure(Collider headCollider)
        {
            head = headCollider;
        }

        private void Awake()
        {
            _restRotation = transform.localRotation;
        }

        public float DamageMultiplier(Collider part) => part != null && part == head ? headMultiplier : 1f;

        public void ReceiveMelee(MeleeStrike strike)
        {
            Hits++;
            DamageTaken += strike.Damage;
            string part = strike.Part != null && strike.Part == head ? "cabeza" : "torso";
            LastDescription = strike.AttackName + " · " + part + " · " + strike.Damage.ToString("0") + " · " +
                              (strike.Depth * 100f).ToString("0") + " cm";
            LastStruck = this;
            Push(strike.Direction, strike.Kind == MeleeAttackKind.Slash ? 1.2f : 1f);
        }

        public void ReceiveBullet(BulletHit hit)
        {
            Hits++;
            DamageTaken += hit.Damage;
            LastDescription = "Bala · " + hit.DistanceTravelled.ToString("0") + " m · " + hit.Damage.ToString("0");
            LastStruck = this;
            Push(hit.Velocity.normalized, 0.4f);
        }

        /// <summary>Impulso angular: el muñeco se inclina hacia donde lo empuja el golpe.</summary>
        private void Push(Vector3 direction, float strength)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (horizontal.sqrMagnitude < 1e-6f) return;
            horizontal.Normalize();
            // Inclinar hacia +X local es girar sobre −Z; hacia +Z, sobre +X.
            Vector3 local = transform.parent != null ? transform.parent.InverseTransformDirection(horizontal) : horizontal;
            float omega = 2f * Mathf.PI * swayFrequencyHz;
            _tiltVelocity += new Vector2(local.z, -local.x) * (swayPerHitDeg * strength * omega);
        }

        private void Update()
        {
            _accumulator += Time.deltaTime;
            // Muelle amortiguado en pasos fijos: igual a cualquier tasa de fotogramas.
            float omega = 2f * Mathf.PI * swayFrequencyHz;
            while (_accumulator >= SwayStep)
            {
                _accumulator -= SwayStep;
                Vector2 acceleration = -omega * omega * _tilt - 2f * swayDamping * omega * _tiltVelocity;
                _tiltVelocity += acceleration * SwayStep;
                _tilt += _tiltVelocity * SwayStep;
            }
            transform.localRotation = _restRotation * Quaternion.Euler(_tilt.x, 0f, _tilt.y);
        }

        public static string DescribeLastStrike() => LastStruck != null ? LastStruck.LastDescription : string.Empty;
    }
}
