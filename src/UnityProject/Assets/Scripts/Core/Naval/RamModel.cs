using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>Resultado de una embestida.</summary>
    public struct RamResult
    {
        /// <summary>Velocidad de cierre a lo largo de la proa del atacante (m/s).</summary>
        public float ClosingSpeed;
        /// <summary>Seno del ángulo de cruce: 1 = embestida perpendicular, 0 = roce paralelo.</summary>
        public float AngleFactor;
        public float ImpactEnergyJoules;
        /// <summary>Embestida a velocidad crítica (daño masivo) frente a un simple roce.</summary>
        public bool Critical;
        public float TargetDamage;
        /// <summary>Vía de agua abierta en el blanco (t/s).</summary>
        public float TargetFloodingRate;
        /// <summary>Se parten las cuadernas: el casco pierde su integridad (ROADMAP 2.4).</summary>
        public bool FramesBroken;
        public float RammerDamage;
        /// <summary>Velocidad que conserva el atacante tras el choque (fracción).</summary>
        public float RammerSpeedRetained;
    }

    /// <summary>
    /// Espolonazo (ROADMAP 2.4). La energía de la embestida es ½·m·v² con la masa del atacante (su desplazamiento)
    /// y la velocidad de cierre sobre su proa, ponderada por el ángulo de cruce. Un casco de madera sufre mucho más
    /// que uno de hierro; un buque sin espolón se daña a sí mismo al embestir.
    /// </summary>
    public static class RamModel
    {
        /// <summary>Velocidad de cierre mínima para una embestida demoledora (≈3 nudos).</summary>
        public const float CriticalClosingSpeed = 1.5f;
        /// <summary>Julios por tonelada equivalente de daño estructural.</summary>
        public const float JoulesPerDamageTonne = 130000f;
        public const float WoodenHullMultiplier = 2.5f;
        public const float GlancingMultiplier = 0.2f;
        /// <summary>Fracción del desplazamiento del blanco cuyo daño en un solo golpe parte las cuadernas.</summary>
        public const float FramesBrokenThreshold = 0.15f;
        /// <summary>Toneladas por segundo de vía de agua por tonelada de daño de embestida.</summary>
        public const float FloodingPerDamageTonne = 0.02f;
        public const float RamSelfDamageFraction = 0.08f;
        public const float NoRamSelfDamageFraction = 0.4f;

        /// <summary>
        /// Calcula una embestida. Rumbos en grados (convención de Unity); velocidades en m/s sobre la proa de cada buque.
        /// </summary>
        public static RamResult Resolve(ShipSpec rammer, float rammerHeadingDeg, float rammerSpeed,
                                        ShipSpec target, float targetHeadingDeg, float targetSpeed)
        {
            float rh = rammerHeadingDeg * MathUtil.Deg2Rad;
            float th = targetHeadingDeg * MathUtil.Deg2Rad;
            float fwdX = (float)Math.Sin(rh);
            float fwdZ = (float)Math.Cos(rh);

            float relX = fwdX * rammerSpeed - (float)Math.Sin(th) * targetSpeed;
            float relZ = fwdZ * rammerSpeed - (float)Math.Cos(th) * targetSpeed;
            float closing = Math.Max(0f, relX * fwdX + relZ * fwdZ);

            float crossing = MathUtil.DeltaAngle(rammerHeadingDeg, targetHeadingDeg);
            float angleFactor = Math.Abs((float)Math.Sin(crossing * MathUtil.Deg2Rad));

            float massKg = rammer.DisplacementTonnes * 1000f;
            float energy = 0.5f * massKg * closing * closing * angleFactor;
            bool critical = closing >= CriticalClosingSpeed && angleFactor >= 0.3f;

            float damage = energy / JoulesPerDamageTonne;
            if (target.Hull == HullMaterial.Wood) damage *= WoodenHullMultiplier;
            if (!rammer.HasRam) damage *= 0.5f;
            if (!critical) damage *= GlancingMultiplier;

            float selfFraction = rammer.HasRam ? RamSelfDamageFraction : NoRamSelfDamageFraction;
            return new RamResult
            {
                ClosingSpeed = closing,
                AngleFactor = angleFactor,
                ImpactEnergyJoules = energy,
                Critical = critical,
                TargetDamage = damage,
                TargetFloodingRate = critical ? damage * FloodingPerDamageTonne : 0f,
                FramesBroken = critical && damage >= FramesBrokenThreshold * target.DisplacementTonnes,
                RammerDamage = damage * selfFraction,
                RammerSpeedRetained = critical ? 0.2f : 0.7f,
            };
        }
    }
}
