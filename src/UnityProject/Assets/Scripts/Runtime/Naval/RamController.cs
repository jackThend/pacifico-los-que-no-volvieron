using System.Collections.Generic;
using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>
    /// Detecta cuándo la roda de este buque entra en la silueta de otro y
    /// resuelve el espolonazo: cuadernas partidas y vía de agua en el blanco,
    /// daño propio y pérdida de velocidad. Un mismo contacto se resuelve una
    /// sola vez; hay que separarse (p. ej. "Atrás") para volver a embestir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamController : MonoBehaviour
    {
        [SerializeField] private ShipDamageReceiver self;

        private readonly HashSet<ShipDamageReceiver> inContact = new HashSet<ShipDamageReceiver>();

        public RamReport LastRam { get; private set; }

        public event System.Action<ShipDamageReceiver, RamReport> Rammed;

        public void Configure(ShipDamageReceiver owner) => self = owner;

        private void Start()
        {
            if (self == null) self = GetComponent<ShipDamageReceiver>();
        }

        private void FixedUpdate()
        {
            if (self == null || self.Spec == null || self.Hull.IsSunk) return;
            var myPose = self.Pose;
            var myMotion = self.Ship.Motion;

            foreach (var target in ShipDamageReceiver.All)
            {
                if (target == self || target.Spec == null) continue;

                if (!RamDetector.TryDetect(myPose, self.Spec, target.Pose, target.Spec, out var contact))
                {
                    inContact.Remove(target);
                    continue;
                }
                if (!inContact.Add(target) || target.Hull.IsSunk) continue;

                var targetMotion = target.Ship.Motion;
                var report = RamImpact.Resolve(self.Spec, myMotion.SpeedMs, myMotion.HeadingDegrees,
                    target.Spec, targetMotion.SpeedMs, targetMotion.HeadingDegrees, target.Frames, contact);

                target.ApplyRam(report);
                self.ApplyStructuralDamage(report.RammerDamage);
                myMotion.ApplySpeedLoss(report.RammerSpeedKept);
                LastRam = report;
                Rammed?.Invoke(target, report);
                Debug.Log($"[Pacífico] {self.Spec.DisplayName} embiste a {target.Spec.DisplayName}: " +
                          $"{report.ClosingSpeedKnots:0.0} nudos, {report.ImpactAngleDegrees:0}°, " +
                          $"{report.FramesBroken} cuadernas partidas, vía de agua {report.FloodingTonsPerMinute:0} t/min.");
            }
        }
    }
}
