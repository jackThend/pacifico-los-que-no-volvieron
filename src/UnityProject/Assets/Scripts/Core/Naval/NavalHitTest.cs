using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>Comprobación de impacto contra la silueta del buque (rectángulo eslora × manga).</summary>
    public static class NavalHitTest
    {
        private const double DegToRad = Math.PI / 180.0;
        /// <summary>Fracción de la eslora a cada extremo considerada "extremos del cinturón".</summary>
        public const float EndsFraction = 0.2f;
        public const float TurretRadiusM = 3.5f;

        /// <summary>
        /// Devuelve true si el punto cae sobre el buque e indica la zona golpeada:
        /// torre (si la tiene y el punto cae en su radio), extremos o centro del cinturón.
        /// </summary>
        public static bool TryHit(SeaPoint point, ShipPose ship, ShipSpec spec, out ArmorZone zone)
        {
            zone = ArmorZone.BeltMidship;
            var dx = point.X - ship.X;
            var dz = point.Z - ship.Z;
            var rad = ship.HeadingDegrees * DegToRad;
            var forward = (float)(dx * Math.Sin(rad) + dz * Math.Cos(rad));
            var right = (float)(dx * Math.Cos(rad) - dz * Math.Sin(rad));

            if (Math.Abs(forward) > spec.LengthM * 0.5f || Math.Abs(right) > spec.BeamM * 0.5f) return false;

            if (spec.Turret != null)
            {
                var tf = forward - spec.Turret.offsetForwardM;
                if (tf * tf + right * right <= TurretRadiusM * TurretRadiusM)
                {
                    zone = ArmorZone.Turret;
                    return true;
                }
            }
            if (Math.Abs(forward) > spec.LengthM * (0.5f - EndsFraction)) zone = ArmorZone.BeltEnds;
            return true;
        }
    }
}
