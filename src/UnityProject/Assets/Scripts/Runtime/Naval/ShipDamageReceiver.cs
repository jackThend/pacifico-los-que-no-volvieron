using System;
using System.Collections.Generic;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>
    /// Integridad del casco de un buque en escena. Se registra en una lista
    /// global para que los cañones puedan resolver impactos contra él.
    /// Al hundirse se detienen las máquinas y el casco desciende.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipDamageReceiver : MonoBehaviour
    {
        private static readonly List<ShipDamageReceiver> Registered = new List<ShipDamageReceiver>();

        [SerializeField] private ShipNavigationController ship;
        [SerializeField, Min(0f), Tooltip("Metros por segundo que desciende el casco al hundirse.")] private float sinkSpeed = 0.6f;

        private HullIntegrity hull;

        public static IReadOnlyList<ShipDamageReceiver> All => Registered;
        public HullIntegrity Hull => hull;
        public ShipSpec Spec { get; private set; }
        public ShipNavigationController Ship => ship;
        public ImpactReport LastImpact { get; private set; }

        public event Action<ImpactReport> Impacted;

        public void Configure(ShipNavigationController owner) => ship = owner;

        public ShipPose Pose
        {
            get
            {
                var motion = ship.Motion;
                return new ShipPose(motion.PositionX, motion.PositionZ, motion.HeadingDegrees);
            }
        }

        private void Start()
        {
            if (ship == null) ship = GetComponent<ShipNavigationController>();
            if (ship == null || ship.ShipData == null)
            {
                Debug.LogError($"[ShipDamageReceiver] {name}: falta ShipNavigationController con ShipDataSO.", this);
                enabled = false;
                return;
            }
            Spec = ship.ShipData.ToSpec();
            hull = new HullIntegrity(Spec.HullIntegrity);
            hull.Sunk += OnSunk;
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        public void ApplyImpact(ImpactReport report)
        {
            if (hull == null || hull.IsSunk) return;
            LastImpact = report;
            hull.ApplyImpact(report);
            Impacted?.Invoke(report);
        }

        private void OnSunk()
        {
            ship.SetOrder(EngineOrder.Stop);
            ship.SetRudderCommand(0f);
            Debug.Log($"[Pacífico] {Spec.DisplayName} se hunde.");
        }

        private void Update()
        {
            if (hull == null || !hull.IsSunk) return;
            var position = transform.position;
            if (position.y > -Spec.LengthM) transform.position = new Vector3(position.x, position.y - sinkSpeed * Time.deltaTime, position.z);
        }
    }
}
