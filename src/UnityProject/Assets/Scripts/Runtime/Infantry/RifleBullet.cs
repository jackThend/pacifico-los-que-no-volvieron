using System;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>Impacto de una bala de fusil.</summary>
    public struct BulletHit
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Velocity;
        public float DistanceTravelled;
        public float Damage;
        public Collider Collider;
        public GameObject Shooter;
        public WeaponSpec Weapon;
    }

    /// <summary>Todo lo que puede recibir una bala (soldados, blancos de práctica, muñecos).</summary>
    public interface IBulletTarget
    {
        void ReceiveBullet(BulletHit hit);
    }

    /// <summary>
    /// Bala de fusil (ROADMAP 3.2). Se integra con <see cref="SmallArmsBallistics.StepBullet"/>, el mismo modelo con
    /// el que se calculan las graduaciones del alza, así que cruza la línea de mira donde dice el alza. Cada paso se
    /// barre con un raycast entre posiciones (sin «túneles» a 450 m/s) y se ignora el propio tirador.
    /// </summary>
    public sealed class RifleBullet : MonoBehaviour
    {
        private const float MaxLifetimeSeconds = 12f;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private Vec3 _position;
        private Vec3 _velocity;
        private float _dragFactor;
        private float _distance;
        private float _age;
        private WeaponSpec _weapon;
        private GameObject _shooter;
        private LayerMask _hitMask;

        /// <summary>Cada impacto, haya o no un <see cref="IBulletTarget"/> (polvo, astillas, sonido).</summary>
        public static event Action<BulletHit> Impacted;

        public static RifleBullet Fire(Vector3 origin, Vector3 velocity, WeaponSpec weapon, float dragFactor, GameObject shooter, LayerMask hitMask)
        {
            var go = new GameObject("Bala_" + weapon.Id);
            go.transform.position = origin;
            var bullet = go.AddComponent<RifleBullet>();
            bullet._position = new Vec3(origin.x, origin.y, origin.z);
            bullet._velocity = new Vec3(velocity.x, velocity.y, velocity.z);
            bullet._weapon = weapon;
            bullet._dragFactor = dragFactor;
            bullet._shooter = shooter;
            bullet._hitMask = hitMask;
            return bullet;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _age += dt;
            if (_age > MaxLifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 start = ToVector3(_position);
            Vector3 startVelocity = ToVector3(_velocity);
            SmallArmsBallistics.StepBullet(ref _position, ref _velocity, _dragFactor, dt);
            Vector3 end = ToVector3(_position);
            Vector3 segment = end - start;
            float length = segment.magnitude;

            if (length > 0f && TryFirstHit(start, segment / length, length, out RaycastHit hit))
            {
                float distance = _distance + hit.distance;
                var bulletHit = new BulletHit
                {
                    Point = hit.point,
                    Normal = hit.normal,
                    // Velocidad en el punto de impacto, no al final del paso de integración.
                    Velocity = Vector3.Lerp(startVelocity, ToVector3(_velocity), hit.distance / length),
                    DistanceTravelled = distance,
                    Damage = _weapon.DamageAtDistance(distance),
                    Collider = hit.collider,
                    Shooter = _shooter,
                    Weapon = _weapon,
                };
                Impacted?.Invoke(bulletHit);
                FindTarget(hit.collider)?.ReceiveBullet(bulletHit);
                Destroy(gameObject);
                return;
            }

            _distance += length;
            transform.position = end;
        }

        private bool TryFirstHit(Vector3 start, Vector3 direction, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(start, direction, HitBuffer, length, _hitMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = HitBuffer[i];
                if (_shooter != null && candidate.collider.transform.IsChildOf(_shooter.transform)) continue;
                if (!found || candidate.distance < best.distance)
                {
                    best = candidate;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Se recorre la jerarquía en lugar de GetComponentInParent&lt;Interfaz&gt;() para evitar el «null falso» de Unity.</summary>
        private static IBulletTarget FindTarget(Collider collider)
        {
            foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IBulletTarget target) return target;
            }
            return null;
        }

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
