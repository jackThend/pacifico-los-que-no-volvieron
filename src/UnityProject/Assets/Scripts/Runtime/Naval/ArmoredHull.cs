using System;
using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>Impacto ya resuelto contra el blindaje, con la zona afectada.</summary>
    public struct ResolvedImpact
    {
        public ShellHit Hit;
        public ArmorZone Zone;
        public bool BelowWaterline;
        public float ObliquityDeg;
        public ArmorImpactResult Result;
    }

    /// <summary>
    /// Resuelve en escena los impactos de proyectiles contra el casco (ROADMAP 2.3): determina la zona por el
    /// colisionador alcanzado, calcula la oblicuidad con la normal real de la superficie y la velocidad remanente
    /// por la distancia recorrida, y delega en <see cref="ArmorPenetrationModel"/>.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class ArmoredHull : MonoBehaviour, IShellTarget
    {
        private ShipController _ship;

        /// <summary>Se emite tras cada impacto (control de averías, HUD, efectos, audio).</summary>
        public event Action<ResolvedImpact> ImpactResolved;

        public ResolvedImpact? LastImpact { get; private set; }

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
        }

        public void ReceiveShell(ShellHit hit)
        {
            if (_ship == null || _ship.Spec == null) return;

            var marker = hit.Collider != null ? hit.Collider.GetComponent<ArmorZoneMarker>() : null;
            ArmorZone zone = marker != null ? marker.Zone : ArmorZone.Unarmored;
            float obliquity = Vector3.Angle(-hit.Velocity.normalized, hit.Normal);

            float striking = ArmorPenetrationModel.StrikingVelocity(hit.Velocity.magnitude, hit.ShellMassKg, hit.CaliberMm, hit.DistanceTravelled);
            ArmorImpactResult result = ArmorPenetrationModel.Resolve(new ArmorImpact
            {
                ShellMassKg = hit.ShellMassKg,
                CaliberMm = hit.CaliberMm,
                StrikingVelocity = striking,
                ObliquityDeg = obliquity,
                IronThicknessMm = _ship.Spec.Armor.IronThicknessFor(zone),
                WoodThicknessMm = _ship.Spec.Armor.WoodFor(zone),
                Hull = _ship.Spec.Hull,
            });

            var resolved = new ResolvedImpact
            {
                Hit = hit,
                Zone = zone,
                BelowWaterline = marker != null && marker.BelowWaterline,
                ObliquityDeg = obliquity,
                Result = result,
            };
            LastImpact = resolved;
            ImpactResolved?.Invoke(resolved);
        }

        /// <summary>Texto corto para el HUD.</summary>
        public static string Describe(ResolvedImpact impact)
        {
            string outcome;
            switch (impact.Result.Outcome)
            {
                case ImpactOutcome.Ricochet: outcome = "REBOTE"; break;
                case ImpactOutcome.NoPenetration: outcome = "SIN PERFORAR"; break;
                case ImpactOutcome.CriticalPenetration: outcome = "PERFORACIÓN CRÍTICA"; break;
                default: outcome = "PERFORACIÓN"; break;
            }
            return outcome + " · " + impact.Zone + " · " + impact.ObliquityDeg.ToString("0") + "° · " +
                   impact.Result.PenetrationMm.ToString("0") + "/" + impact.Result.EffectiveThicknessMm.ToString("0") + " mm";
        }
    }
}
