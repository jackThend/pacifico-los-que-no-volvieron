using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    public enum ImpactResult
    {
        /// <summary>El proyectil no perfora: rebota o se rompe contra la coraza (chispas, daño mínimo).</summary>
        Ricochet = 0,
        /// <summary>Perfora la coraza con incidencia oblicua.</summary>
        Penetration = 1,
        /// <summary>Perfora de frente o atraviesa un casco de madera: daño crítico.</summary>
        CriticalPenetration = 2
    }

    /// <summary>Resultado detallado de un impacto, para daño, efectos y depuración.</summary>
    public sealed class ImpactReport
    {
        public ImpactResult Result { get; }
        public ArmorZone Zone { get; }
        public float IncidenceDegrees { get; }
        public float ImpactVelocityMs { get; }
        public float PenetrationInches { get; }
        public float ArmorInches { get; }
        public float EffectiveArmorInches { get; }
        public float Damage { get; }

        public ImpactReport(ImpactResult result, ArmorZone zone, float incidenceDegrees, float impactVelocityMs,
            float penetrationInches, float armorInches, float effectiveArmorInches, float damage)
        {
            Result = result;
            Zone = zone;
            IncidenceDegrees = incidenceDegrees;
            ImpactVelocityMs = impactVelocityMs;
            PenetrationInches = penetrationInches;
            ArmorInches = armorInches;
            EffectiveArmorInches = effectiveArmorInches;
            Damage = damage;
        }

        public bool Penetrated => Result != ImpactResult.Ricochet;
    }

    /// <summary>
    /// Blindaje angular (tarea 2.3). La penetración en hierro forjado sigue la
    /// regla de la época de "energía por pulgada de circunferencia" del
    /// proyectil, calibrada con dos anclas: Armstrong de 300 lb a 400 m/s ≈ 12"
    /// y de 40 lb a 360 m/s ≈ 3.5" (menos que los 4.5" del Huáscar, como en
    /// Iquique). El ángulo aumenta el espesor efectivo (t / cos θ) y por encima
    /// de <see cref="AutoRicochetDegrees"/> el proyectil siempre rebota.
    /// </summary>
    public static class ArmorImpact
    {
        public const float AutoRicochetDegrees = 60f;
        public const float CriticalIncidenceDegrees = 25f;
        /// <summary>Chapa de hierro sin coraza (superestructura, costados no blindados).</summary>
        public const float UnarmoredIronPlateInches = 0.75f;
        /// <summary>Metros de alcance por pulgada de calibre en los que la velocidad cae a 1/e.</summary>
        public const float VelocityDecayMetersPerInch = 900f;
        public const float DamagePerPound = 1f;
        public const float RicochetDamageFactor = 0.05f;
        public const float CriticalDamageFactor = 2f;

        private const float PenetrationCoefficient = 0.228f;
        private const float PenetrationExponent = 0.84f;
        private const float FeetPerMeter = 3.28084f;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Velocidad remanente a esa distancia: los calibres grandes la conservan mejor.</summary>
        public static float ImpactVelocity(float muzzleVelocityMs, float caliberInches, float rangeM)
        {
            return muzzleVelocityMs * (float)Math.Exp(-Math.Max(rangeM, 0f) / (VelocityDecayMetersPerInch * caliberInches));
        }

        /// <summary>Hierro forjado perforable a incidencia normal (pulgadas).</summary>
        public static float PenetrationInches(float projectileLbs, float caliberInches, float velocityMs)
        {
            var feetPerSecond = velocityMs * FeetPerMeter;
            var footTons = projectileLbs * feetPerSecond * feetPerSecond / (2.0 * 32.174 * 2240.0);
            var perInchOfCircumference = footTons / (Math.PI * caliberInches);
            return (float)(PenetrationCoefficient * Math.Pow(perInchOfCircumference, PenetrationExponent));
        }

        /// <summary>
        /// Ángulo entre la trayectoria y la normal de la plancha (0° = de frente).
        /// Cinturón y reducto: costado vertical (se elige la banda que mira al
        /// proyectil); torre y torre de mando: cilindro golpeado en su eje;
        /// cubierta: plancha horizontal.
        /// </summary>
        public static float IncidenceDegrees(float shellBearingDegrees, float fallAngleDegrees,
            float targetHeadingDegrees, ArmorZone zone)
        {
            var fall = Math.Abs(fallAngleDegrees) * DegToRad;
            switch (zone)
            {
                case ArmorZone.Deck:
                    return 90f - Math.Abs(fallAngleDegrees);
                case ArmorZone.Turret:
                case ArmorZone.ConningTower:
                    return Math.Abs(fallAngleDegrees);
                default:
                    var incoming = shellBearingDegrees + 180f;
                    var starboard = Math.Abs(Angles.DeltaDegrees(incoming, targetHeadingDegrees + 90f));
                    var port = Math.Abs(Angles.DeltaDegrees(incoming, targetHeadingDegrees - 90f));
                    var horizontal = Math.Min(starboard, port) * DegToRad;
                    var cos = Math.Cos(horizontal) * Math.Cos(fall);
                    return (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) / DegToRad);
            }
        }

        public static ImpactReport Resolve(GunBattery gun, float rangeM, float incidenceDegrees,
            ShipSpec target, ArmorZone zone)
        {
            if (gun == null) throw new ArgumentNullException(nameof(gun));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var velocity = ImpactVelocity(gun.muzzleVelocityMs, gun.caliberInches, rangeM);
            var penetration = PenetrationInches(gun.projectileLbs, gun.caliberInches, velocity);
            var baseDamage = gun.projectileLbs * DamagePerPound;
            var incidence = Math.Max(0f, Math.Min(90f, incidenceDegrees));

            if (target.Hull == HullMaterial.Wood)
            {
                // La madera no detiene ni desvía un proyectil de hierro: astillas y daño crítico.
                return new ImpactReport(ImpactResult.CriticalPenetration, zone, incidence, velocity,
                    penetration, 0f, 0f, baseDamage * CriticalDamageFactor);
            }

            var armor = target.ArmorInches(zone);
            if (armor <= 0f) armor = UnarmoredIronPlateInches;
            var effective = incidence >= 89.9f ? float.PositiveInfinity : armor / (float)Math.Cos(incidence * DegToRad);

            ImpactResult result;
            if (incidence >= AutoRicochetDegrees || penetration < effective) result = ImpactResult.Ricochet;
            else if (incidence <= CriticalIncidenceDegrees) result = ImpactResult.CriticalPenetration;
            else result = ImpactResult.Penetration;

            var factor = result == ImpactResult.Ricochet ? RicochetDamageFactor
                : result == ImpactResult.CriticalPenetration ? CriticalDamageFactor : 1f;
            return new ImpactReport(result, zone, incidence, velocity, penetration, armor, effective, baseDamage * factor);
        }

        /// <summary>
        /// Resuelve un disparo completo: distancia y marcación desde el tirador,
        /// elevación y ángulo de caída por balística, incidencia contra la zona
        /// y resultado. <paramref name="gunHeightM"/> es la altura del cañón.
        /// </summary>
        public static ImpactReport ResolveShot(GunBattery gun, float gunHeightM, SeaPoint shooter,
            ShipPose target, ShipSpec targetSpec, ArmorZone zone)
        {
            var dx = target.X - shooter.X;
            var dz = target.Z - shooter.Z;
            var range = (float)Math.Sqrt(dx * dx + dz * dz);
            var bearing = (float)(Math.Atan2(dx, dz) / DegToRad);
            var elevation = NavalBallistics.ElevationForRange(gun.muzzleVelocityMs, range, gunHeightM, -10f, 30f, out _);
            var fall = NavalBallistics.FallAngleDegrees(gun.muzzleVelocityMs, elevation, gunHeightM);
            var incidence = IncidenceDegrees(bearing, fall, target.HeadingDegrees, zone);
            return Resolve(gun, range, incidence, targetSpec, zone);
        }
    }
}
