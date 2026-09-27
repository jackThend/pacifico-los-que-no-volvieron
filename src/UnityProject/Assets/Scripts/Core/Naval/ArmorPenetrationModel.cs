using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    public enum ImpactOutcome
    {
        /// <summary>El proyectil rebota en la coraza por la oblicuidad del impacto.</summary>
        Ricochet = 0,
        /// <summary>Impacta de frente pero no atraviesa: sacude la estructura y afloja pernos.</summary>
        NoPenetration = 1,
        /// <summary>Atraviesa la coraza (o el costado) y estalla dentro.</summary>
        Penetration = 2,
        /// <summary>Perforación en casco de madera con astillazo devastador (GDD: «daño crítico»).</summary>
        CriticalPenetration = 3,
    }

    /// <summary>Datos de entrada de un impacto.</summary>
    public struct ArmorImpact
    {
        public float ShellMassKg;
        public float CaliberMm;
        /// <summary>Velocidad remanente en el impacto (m/s).</summary>
        public float StrikingVelocity;
        /// <summary>Ángulo entre la trayectoria y la normal de la plancha (0 = impacto perpendicular).</summary>
        public float ObliquityDeg;
        public float IronThicknessMm;
        public float WoodThicknessMm;
        public HullMaterial Hull;
    }

    public struct ArmorImpactResult
    {
        public ImpactOutcome Outcome;
        public float PenetrationMm;
        /// <summary>Espesor equivalente de hierro que opone la zona a lo largo de la trayectoria.</summary>
        public float EffectiveThicknessMm;
        public float RicochetAngleDeg;
        /// <summary>Daño estructural en toneladas equivalentes (se compara con el desplazamiento del buque).</summary>
        public float StructuralDamage;
        /// <summary>Probabilidad [0, 1] de iniciar un incendio (control de averías, ROADMAP 2.4).</summary>
        public float FireChance;

        public bool Penetrated => Outcome == ImpactOutcome.Penetration || Outcome == ImpactOutcome.CriticalPenetration;
    }

    /// <summary>
    /// Blindaje angular y perforación (ROADMAP 2.3) para hierro forjado sobre almohadilla de teca y cascos de madera.
    /// <para>
    /// Perforación de hierro forjado con una fórmula empírica tipo De Marre:
    /// <c>T = (v · √m / (K · d^0,75))^(1/0,7)</c> (T y d en decímetros, m en kg, v en m/s), con K calibrada para que
    /// un proyectil Palliser de 10" y 400 lb a ~416 m/s perfore ~11" de hierro, orden de magnitud de las pruebas
    /// británicas de la década de 1870.
    /// </para>
    /// <para>
    /// Oblicuidad: el espesor efectivo crece con 1/cos(θ); por encima del ángulo de rebote la ojiva resbala.
    /// La madera opone ~1/10 de la resistencia del hierro y no hace rebotar los proyectiles.
    /// </para>
    /// </summary>
    public static class ArmorPenetrationModel
    {
        public const float WroughtIronK = 1350f;
        public const float WoodToIronEquivalence = 0.1f;
        /// <summary>Longitud característica de pérdida de velocidad por metro de densidad seccional (kg/cm²).</summary>
        public const float VelocityDecayLengthPerSectionalDensity = 28000f;

        /// <summary>Ángulo de rebote en coraza delgada respecto al calibre.</summary>
        public const float BaseRicochetAngleDeg = 65f;
        /// <summary>Ángulo de rebote cuando la plancha iguala o supera el calibre (rebota antes).</summary>
        public const float ThickPlateRicochetAngleDeg = 55f;
        /// <summary>En madera solo rebotan los impactos casi rasantes.</summary>
        public const float WoodRicochetAngleDeg = 82f;
        /// <summary>Calibre mínimo que astilla un costado de madera de forma crítica.</summary>
        public const float CriticalSplinterCaliberMm = 150f;

        private const float RicochetDamageFactor = 0.03f;
        private const float NoPenetrationDamageFactor = 0.15f;
        private const float PenetrationDamageFactor = 1f;
        private const float CriticalDamageFactor = 1.6f;
        /// <summary>Toneladas equivalentes de daño por kg de proyectil que estalla dentro.</summary>
        private const float DamagePerShellKg = 1.5f;

        /// <summary>Perforación en hierro forjado (mm) de un impacto perpendicular.</summary>
        public static float PenetrationMm(float shellMassKg, float caliberMm, float velocity)
        {
            if (shellMassKg <= 0f || caliberMm <= 0f || velocity <= 0f) return 0f;
            float dDm = caliberMm / 100f;
            double ratio = velocity * Math.Sqrt(shellMassKg) / (WroughtIronK * Math.Pow(dDm, 0.75));
            return (float)(Math.Pow(ratio, 1.0 / 0.7) * 100.0);
        }

        /// <summary>Velocidad remanente tras recorrer <paramref name="rangeM"/> metros (pérdida exponencial por rozamiento).</summary>
        public static float StrikingVelocity(float muzzleVelocity, float shellMassKg, float caliberMm, float rangeM)
        {
            float caliberCm = caliberMm / 10f;
            float sectionalDensity = shellMassKg / (caliberCm * caliberCm);
            float decayLength = VelocityDecayLengthPerSectionalDensity * sectionalDensity;
            return muzzleVelocity * (float)Math.Exp(-Math.Max(0f, rangeM) / decayLength);
        }

        public static float RicochetAngle(float ironThicknessMm, float caliberMm, HullMaterial hull)
        {
            if (hull == HullMaterial.Wood || ironThicknessMm <= 0f) return WoodRicochetAngleDeg;
            float t = MathUtil.InverseLerp(0.5f, 1f, ironThicknessMm / Math.Max(1f, caliberMm));
            return MathUtil.Lerp(BaseRicochetAngleDeg, ThickPlateRicochetAngleDeg, t);
        }

        public static ArmorImpactResult Resolve(ArmorImpact impact)
        {
            float obliquity = MathUtil.Clamp(Math.Abs(impact.ObliquityDeg), 0f, 89.9f);
            float cos = (float)Math.Cos(obliquity * MathUtil.Deg2Rad);
            float equivalent = impact.IronThicknessMm + impact.WoodThicknessMm * WoodToIronEquivalence;

            var result = new ArmorImpactResult
            {
                PenetrationMm = PenetrationMm(impact.ShellMassKg, impact.CaliberMm, impact.StrikingVelocity),
                EffectiveThicknessMm = equivalent / cos,
                RicochetAngleDeg = RicochetAngle(impact.IronThicknessMm, impact.CaliberMm, impact.Hull),
            };

            float fullDamage = impact.ShellMassKg * DamagePerShellKg;

            if (obliquity >= result.RicochetAngleDeg)
            {
                result.Outcome = ImpactOutcome.Ricochet;
                result.StructuralDamage = fullDamage * RicochetDamageFactor;
            }
            else if (result.PenetrationMm < result.EffectiveThicknessMm)
            {
                result.Outcome = ImpactOutcome.NoPenetration;
                result.StructuralDamage = fullDamage * NoPenetrationDamageFactor;
            }
            else if (impact.Hull == HullMaterial.Wood && impact.CaliberMm >= CriticalSplinterCaliberMm)
            {
                result.Outcome = ImpactOutcome.CriticalPenetration;
                result.StructuralDamage = fullDamage * CriticalDamageFactor;
                result.FireChance = 0.45f;
            }
            else
            {
                result.Outcome = ImpactOutcome.Penetration;
                result.StructuralDamage = fullDamage * PenetrationDamageFactor;
                result.FireChance = impact.Hull == HullMaterial.Wood ? 0.3f : 0.15f;
            }

            return result;
        }

        /// <summary>
        /// Resuelve un impacto sobre una zona de un buque a una distancia dada (atajo usado por el juego y las pruebas).
        /// </summary>
        public static ArmorImpactResult ResolveAgainstShip(GunMount gun, float rangeM, float obliquityDeg, ShipSpec target, ArmorZone zone)
        {
            return Resolve(new ArmorImpact
            {
                ShellMassKg = gun.ShellMassKg,
                CaliberMm = gun.BoreMm,
                StrikingVelocity = StrikingVelocity(gun.MuzzleVelocityMps, gun.ShellMassKg, gun.BoreMm, rangeM),
                ObliquityDeg = obliquityDeg,
                IronThicknessMm = target.Armor.IronThicknessFor(zone),
                WoodThicknessMm = target.Armor.WoodFor(zone),
                Hull = target.Hull,
            });
        }
    }
}
