using System;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>Datos de un impacto de proyectil, pasados al blanco para resolver blindaje y daños.</summary>
    public struct ShellHit
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Velocity;
        public float ShellMassKg;
        public float CaliberMm;
        public Collider Collider;
        public GameObject Shooter;
        /// <summary>Metros recorridos desde la boca (la pérdida de velocidad por rozamiento se calcula con esto).</summary>
        public float DistanceTravelled;
    }

    /// <summary>Todo lo que puede recibir un proyectil naval (casco blindado, batería costera, blanco de prácticas).</summary>
    public interface IShellTarget
    {
        void ReceiveShell(ShellHit hit);
    }

    /// <summary>
    /// Proyectil de artillería naval: trayectoria balística integrada en FixedUpdate con detección continua por
    /// raycast entre posiciones (sin «túneles» a 400 m/s). Al tocar el agua levanta un pique y desaparece.
    /// </summary>
    public sealed class NavalShell : MonoBehaviour
    {
        [SerializeField] private float maxLifetimeSeconds = 20f;
        [SerializeField] private float seaLevel;
        [SerializeField] private LayerMask hitMask = ~0;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private Vector3 _velocity;
        private float _massKg;
        private float _caliberMm;
        private GameObject _shooter;
        private float _age;
        private float _distance;

        /// <summary>Se dispara cuando el proyectil cae al agua (para piques y corrección del tiro).</summary>
        public static event Action<Vector3> Splashed;

        /// <summary>Se dispara en cada impacto sobre un colisionador, haya o no un <see cref="IShellTarget"/>.</summary>
        public static event Action<ShellHit> Impacted;

        public void Launch(Vector3 velocity, float massKg, float caliberMm, GameObject shooter)
        {
            _velocity = velocity;
            _massKg = massKg;
            _caliberMm = caliberMm;
            _shooter = shooter;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _age += dt;
            if (_age > maxLifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 start = transform.position;
            Vector3 nextVelocity = _velocity + Physics.gravity * dt;
            Vector3 end = start + (_velocity + nextVelocity) * 0.5f * dt;
            Vector3 segment = end - start;

            if (TryFirstHit(start, segment, out RaycastHit hit))
            {
                var shellHit = new ShellHit
                {
                    Point = hit.point,
                    Normal = hit.normal,
                    Velocity = _velocity,
                    ShellMassKg = _massKg,
                    CaliberMm = _caliberMm,
                    Collider = hit.collider,
                    Shooter = _shooter,
                    DistanceTravelled = _distance + hit.distance,
                };
                Impacted?.Invoke(shellHit);
                FindTarget(hit.collider)?.ReceiveShell(shellHit);
                Destroy(gameObject);
                return;
            }

            if (end.y <= seaLevel)
            {
                float t = Mathf.InverseLerp(start.y, end.y, seaLevel);
                Splashed?.Invoke(Vector3.Lerp(start, end, t));
                Destroy(gameObject);
                return;
            }

            _distance += segment.magnitude;
            transform.SetPositionAndRotation(end, Quaternion.LookRotation(nextVelocity));
            _velocity = nextVelocity;
        }

        /// <summary>
        /// Impacto más cercano del segmento que no pertenezca al propio buque. Se usa RaycastNonAlloc en lugar de
        /// Raycast porque este devuelve solo el primer colisionador: si fuera el del tirador, el blanco que hubiera
        /// detrás en el mismo paso se perdería.
        /// </summary>
        private bool TryFirstHit(Vector3 start, Vector3 segment, out RaycastHit best)
        {
            best = default;
            float length = segment.magnitude;
            if (length <= 0f) return false;
            int count = Physics.RaycastNonAlloc(start, segment / length, HitBuffer, length, hitMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = HitBuffer[i];
                if (IsShooter(candidate.collider)) continue;
                if (!found || candidate.distance < best.distance)
                {
                    best = candidate;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Busca un <see cref="IShellTarget"/> en la jerarquía. Se evita GetComponentInParent&lt;Interfaz&gt;() con «?.»
        /// porque en el Editor Unity puede devolver un objeto «null falso» que no es null para C#.
        /// </summary>
        private static IShellTarget FindTarget(Collider collider)
        {
            foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IShellTarget target) return target;
            }
            return null;
        }

        private bool IsShooter(Collider other)
        {
            return _shooter != null && other.transform.IsChildOf(_shooter.transform);
        }
    }
}
