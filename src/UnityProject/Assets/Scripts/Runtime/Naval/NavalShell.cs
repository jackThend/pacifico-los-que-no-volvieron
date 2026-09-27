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

        private Vector3 _velocity;
        private float _massKg;
        private float _caliberMm;
        private GameObject _shooter;
        private float _age;

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

            if (Physics.Raycast(start, segment.normalized, out RaycastHit hit, segment.magnitude, hitMask, QueryTriggerInteraction.Ignore)
                && !IsShooter(hit.collider))
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
                };
                Impacted?.Invoke(shellHit);
                hit.collider.GetComponentInParent<IShellTarget>()?.ReceiveShell(shellHit);
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

            transform.SetPositionAndRotation(end, Quaternion.LookRotation(nextVelocity));
            _velocity = nextVelocity;
        }

        private bool IsShooter(Collider other)
        {
            return _shooter != null && other.transform.IsChildOf(_shooter.transform);
        }
    }
}
