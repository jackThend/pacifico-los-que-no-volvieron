using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>Resultado de un espolonazo o colisión entre buques.</summary>
    public sealed class RamReport
    {
        public float ClosingSpeedKnots { get; }
        /// <summary>Ángulo entre las quillas (90° = embestida perpendicular al costado).</summary>
        public float ImpactAngleDegrees { get; }
        public bool IsCritical { get; }
        public float EnergyMJ { get; }
        public int FramesBroken { get; }
        public float TargetDamage { get; }
        public float FloodingTonsPerMinute { get; }
        public float RammerDamage { get; }
        /// <summary>Fracción de velocidad que conserva el atacante tras el choque.</summary>
        public float RammerSpeedKept { get; }

        public RamReport(float closingSpeedKnots, float impactAngleDegrees, bool isCritical, float energyMJ,
            int framesBroken, float targetDamage, float floodingTonsPerMinute, float rammerDamage, float rammerSpeedKept)
        {
            ClosingSpeedKnots = closingSpeedKnots;
            ImpactAngleDegrees = impactAngleDegrees;
            IsCritical = isCritical;
            EnergyMJ = energyMJ;
            FramesBroken = framesBroken;
            TargetDamage = targetDamage;
            FloodingTonsPerMinute = floodingTonsPerMinute;
            RammerDamage = rammerDamage;
            RammerSpeedKept = rammerSpeedKept;
        }
    }

    /// <summary>
    /// Espolonazo (tarea 2.4). La energía es la cinética del atacante en la
    /// dirección de embestida (velocidad de cierre); se aprovecha entera con
    /// espolón y de forma perpendicular, y se reparte en cuadernas partidas.
    /// Por debajo de <see cref="CriticalClosingKnots"/> solo hay un roce.
    /// </summary>
    public static class RamImpact
    {
        public const float CriticalClosingKnots = 4f;
        public const float NoRamEfficiency = 0.35f;
        public const float DamagePerFrame = 60f;
        public const float GrazeDamage = 20f;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>
        /// Resuelve el choque y parte las cuadernas del blanco en
        /// <paramref name="targetFrames"/> alrededor de <paramref name="contactForwardM"/>.
        /// </summary>
        public static RamReport Resolve(ShipSpec rammer, float rammerSpeedMs, float rammerHeadingDegrees,
            ShipSpec target, float targetSpeedMs, float targetHeadingDegrees,
            HullFrames targetFrames, float contactForwardM)
        {
            if (rammer == null) throw new ArgumentNullException(nameof(rammer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetFrames == null) throw new ArgumentNullException(nameof(targetFrames));

            // Velocidad de cierre: componente de la velocidad relativa en la dirección del atacante.
            var relative = Angles.DeltaDegrees(rammerHeadingDegrees, targetHeadingDegrees) * DegToRad;
            var closing = Math.Max(0.0, rammerSpeedMs - targetSpeedMs * Math.Cos(relative));
            var closingKnots = (float)(closing / ShipHandling.KnotsToMetersPerSecond);

            var keelAngle = Math.Abs(Angles.DeltaDegrees(rammerHeadingDegrees, targetHeadingDegrees));
            if (keelAngle > 90f) keelAngle = 180f - keelAngle;
            var angleEfficiency = (float)Math.Sin(keelAngle * DegToRad);

            var massKg = rammer.DisplacementTons * 1000.0;
            var energy = (float)(0.5 * massKg * closing * closing / 1e6);
            var keep = rammer.DisplacementTons / (rammer.DisplacementTons + target.DisplacementTons);

            if (closingKnots < CriticalClosingKnots)
            {
                return new RamReport(closingKnots, keelAngle, false, energy, 0,
                    closingKnots > 0.5f ? GrazeDamage : 0f, 0f, closingKnots > 0.5f ? GrazeDamage : 0f,
                    closingKnots > 0.5f ? keep : 1f);
            }

            var efficiency = angleEfficiency * (rammer.HasRam ? 1f : NoRamEfficiency);
            var framesToBreak = (int)Math.Floor(energy * efficiency / targetFrames.FrameStrengthMJ);
            var broken = targetFrames.BreakAround(targetFrames.IndexAt(contactForwardM), framesToBreak);

            var targetDamage = broken * DamagePerFrame + GrazeDamage;
            // Con espolón el atacante apenas sufre; sin él la proa propia se destroza.
            var rammerDamage = targetDamage * (rammer.HasRam ? 0.05f : 0.5f);
            return new RamReport(closingKnots, keelAngle, true, energy, broken, targetDamage,
                broken * targetFrames.InflowPerFrameTonsPerMinute, rammerDamage, keep);
        }
    }

    /// <summary>Detección de contacto: la roda del atacante dentro de la silueta del blanco.</summary>
    public static class RamDetector
    {
        private const double DegToRad = Math.PI / 180.0;

        public static SeaPoint BowPoint(ShipPose ship, ShipSpec spec)
        {
            var rad = ship.HeadingDegrees * DegToRad;
            var half = spec.LengthM * 0.5f;
            return new SeaPoint((float)(ship.X + Math.Sin(rad) * half), (float)(ship.Z + Math.Cos(rad) * half));
        }

        /// <summary>
        /// True si la roda del atacante toca al blanco; <paramref name="contactForwardM"/>
        /// es la posición del contacto a lo largo de la eslora del blanco (+ = hacia su proa).
        /// </summary>
        public static bool TryDetect(ShipPose rammer, ShipSpec rammerSpec, ShipPose target, ShipSpec targetSpec,
            out float contactForwardM)
        {
            contactForwardM = 0f;
            var bow = BowPoint(rammer, rammerSpec);
            if (!NavalHitTest.TryHit(bow, target, targetSpec, out _)) return false;

            var rad = target.HeadingDegrees * DegToRad;
            contactForwardM = (float)((bow.X - target.X) * Math.Sin(rad) + (bow.Z - target.Z) * Math.Cos(rad));
            return true;
        }
    }
}
