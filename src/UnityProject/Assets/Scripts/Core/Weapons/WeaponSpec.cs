using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    public enum WeaponKind
    {
        Rifle = 0,
        Carbine = 1,
        Blade = 2,
    }

    /// <summary>Mecanismo de cierre del arma de fuego.</summary>
    public enum FiringAction
    {
        None = 0,
        FallingBlock = 1,   // Comblain: bloque descendente accionado por palanca
        BoltAction = 2,     // Chassepot, Gras: cerrojo
        RollingBlock = 3,   // Remington: bloque rodante
        LeverAction = 4,    // Winchester 1873: palanca con depósito tubular
    }

    public enum AmmoFeed
    {
        None = 0,
        SingleShot = 1,
        TubeMagazine = 2,
    }

    /// <summary>
    /// Especificación de un arma. Los datos se dividen en dos bloques:
    /// <list type="bullet">
    /// <item><b>Históricos</b> (calibre, velocidad en boca, masa, alza máxima): documentados, con fuente.</item>
    /// <item><b>De juego</b> (recarga, daño, dispersión): licencias funcionales del GDD §1.2 (p. ej. recargas de ~2 s).</item>
    /// </list>
    /// </summary>
    public sealed class WeaponSpec
    {
        public const float MaxHealth = 100f;

        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public WeaponKind Kind { get; set; }
        public FiringAction Action { get; set; }
        public AmmoFeed Feed { get; set; }
        public List<Faction> UsedBy { get; set; } = new List<Faction>();

        // --- Datos históricos -------------------------------------------------
        public int YearAdopted { get; set; }
        public string Cartridge { get; set; } = string.Empty;
        public float CaliberMm { get; set; }
        public float MuzzleVelocityMps { get; set; }
        public float MassKg { get; set; }
        /// <summary>Alcance máximo del alza (o alcance máximo documentado).</summary>
        public float MaxSightRangeM { get; set; }
        public HistoricalSource Source { get; set; } = new HistoricalSource();

        // --- Ajustes de juego --------------------------------------------------
        /// <summary>Ciclo completo de recarga (monotiro) o tiempo por cartucho (depósito tubular).</summary>
        public float ReloadSeconds { get; set; }
        public int MagazineCapacity { get; set; }
        /// <summary>Daño por impacto sobre <see cref="MaxHealth"/> puntos, dentro del alcance eficaz.</summary>
        public float BaseDamage { get; set; }
        /// <summary>Dispersión del grupo de impactos en minutos de ángulo (MOA), apuntando con miras.</summary>
        public float DispersionMoa { get; set; }
        /// <summary>Distancia hasta la que el daño es pleno; luego decae linealmente hasta el alza máxima.</summary>
        public float EffectiveRangeM { get; set; }
        /// <summary>Fracción del daño base que conserva el proyectil en el alcance máximo.</summary>
        public float MinDamageFactor { get; set; } = 0.4f;
        /// <summary>Alcance cuerpo a cuerpo (hoja, o fusil con bayoneta calada). 0 si no aplica.</summary>
        public float MeleeReachM { get; set; }
        public float MeleeDamage { get; set; }
        public float MeleeCycleSeconds { get; set; }

        public bool IsFirearm => Kind != WeaponKind.Blade;

        /// <summary>Daño de un impacto a <paramref name="distanceM"/> metros.</summary>
        public float DamageAtDistance(float distanceM)
        {
            if (!IsFirearm) return distanceM <= MeleeReachM ? MeleeDamage : 0f;
            if (distanceM < 0f) distanceM = 0f;
            if (distanceM > MaxSightRangeM) return 0f;
            if (distanceM <= EffectiveRangeM) return BaseDamage;
            float t = MathUtil.InverseLerp(EffectiveRangeM, MaxSightRangeM, distanceM);
            return BaseDamage * MathUtil.Lerp(1f, MinDamageFactor, t);
        }

        /// <summary>Radio (m) del círculo de dispersión a <paramref name="distanceM"/> metros.</summary>
        public float DispersionRadiusAt(float distanceM)
        {
            float halfAngleRad = DispersionMoa / 60f * MathUtil.Deg2Rad * 0.5f;
            return distanceM * (float)Math.Tan(halfAngleRad);
        }

        /// <summary>Disparos por minuto sostenidos considerando solo la recarga.</summary>
        public float SustainedShotsPerMinute
        {
            get
            {
                if (!IsFirearm || ReloadSeconds <= 0f) return 0f;
                if (Feed == AmmoFeed.TubeMagazine)
                {
                    // Vaciar el depósito es rápido; el cuello de botella es reponer cartucho a cartucho.
                    const float leverCycleSeconds = 0.6f;
                    float cycle = MagazineCapacity * (leverCycleSeconds + ReloadSeconds);
                    return 60f * MagazineCapacity / cycle;
                }
                return 60f / ReloadSeconds;
            }
        }

        public ValidationResult Validate()
        {
            var r = new ValidationResult("Arma '" + Id + "'");
            r.RequireNotEmpty(Id, "Id");
            r.RequireNotEmpty(DisplayName, "DisplayName");
            r.Require(UsedBy.Count > 0, "debe tener al menos un bando usuario");
            r.Require(YearAdopted >= 1800 && YearAdopted <= 1884, "YearAdopted fuera del periodo (" + YearAdopted + ")");
            r.Require(Source.References.Count > 0, "requiere al menos una referencia histórica");
            r.RequireNonNegative(MassKg, "MassKg");

            if (IsFirearm)
            {
                r.Require(Action != FiringAction.None, "un arma de fuego necesita mecanismo de cierre");
                r.Require(Feed != AmmoFeed.None, "un arma de fuego necesita sistema de alimentación");
                r.RequireNotEmpty(Cartridge, "Cartridge");
                r.RequirePositive(CaliberMm, "CaliberMm");
                r.Require(MuzzleVelocityMps >= 250f && MuzzleVelocityMps <= 600f,
                    "MuzzleVelocityMps fuera del rango de la pólvora negra (" + MuzzleVelocityMps + ")");
                r.RequirePositive(MassKg, "MassKg");
                r.RequirePositive(MaxSightRangeM, "MaxSightRangeM");
                r.RequirePositive(ReloadSeconds, "ReloadSeconds");
                r.RequirePositive(BaseDamage, "BaseDamage");
                r.Require(BaseDamage <= MaxHealth, "BaseDamage no puede superar la salud máxima");
                r.RequirePositive(DispersionMoa, "DispersionMoa");
                r.RequirePositive(EffectiveRangeM, "EffectiveRangeM");
                r.Require(EffectiveRangeM <= MaxSightRangeM, "EffectiveRangeM no puede superar MaxSightRangeM");
                r.Require(MinDamageFactor > 0f && MinDamageFactor <= 1f, "MinDamageFactor debe estar en (0, 1]");
                r.Require(Feed == AmmoFeed.SingleShot ? MagazineCapacity == 1 : MagazineCapacity > 1,
                    "MagazineCapacity incoherente con el sistema de alimentación");
            }
            else
            {
                r.Require(Action == FiringAction.None && Feed == AmmoFeed.None, "un arma blanca no dispara");
            }

            if (MeleeReachM > 0f)
            {
                r.RequirePositive(MeleeDamage, "MeleeDamage");
                r.RequirePositive(MeleeCycleSeconds, "MeleeCycleSeconds");
            }
            else
            {
                r.Require(Kind != WeaponKind.Blade, "un arma blanca necesita alcance cuerpo a cuerpo");
            }

            return r;
        }

        public WeaponSpec Clone()
        {
            var copy = (WeaponSpec)MemberwiseClone();
            copy.UsedBy = new List<Faction>(UsedBy);
            copy.Source = Source.Clone();
            return copy;
        }

        public override string ToString() => DisplayName + " (" + Id + ")";
    }
}
