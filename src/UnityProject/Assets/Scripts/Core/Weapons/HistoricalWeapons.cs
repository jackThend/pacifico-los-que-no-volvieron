using System.Collections.Generic;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Catálogo del armamento de infantería de la Guerra del Pacífico
    /// (GDD §3.1). Los tiempos de recarga son los fijados por el GDD
    /// (acelerados para jugabilidad); calibre y velocidad de boca son
    /// aproximaciones históricas; daño, dispersión y alcances son valores
    /// de balance iniciales.
    /// </summary>
    public static class HistoricalWeapons
    {
        public const string ComblainId = "comblain";
        public const string ChassepotId = "chassepot";
        public const string GrasId = "gras";
        public const string RemingtonId = "remington_rolling_block";
        public const string CorvoId = "corvo";
        public const string TriangularBayonetId = "bayoneta_triangular";

        public static readonly WeaponSpec Comblain = new WeaponSpec(
            ComblainId, "Fusil Comblain", WeaponClass.SingleShotRifle, ActionType.FallingBlockLever,
            new[] { Faction.Chile },
            caliberMm: 11f, reloadSeconds: 2.0f, damage: 90f, spreadDegrees: 0.8f,
            effectiveRangeM: 400f, maxRangeM: 1100f, muzzleVelocityMs: 440f, capacity: 1,
            historicalNote: "Monotiro belga de palanca (bloque descendente). Daño contundente a media distancia.");

        public static readonly WeaponSpec Chassepot = new WeaponSpec(
            ChassepotId, "Fusil Chassepot", WeaponClass.SingleShotRifle, ActionType.BoltAction,
            new[] { Faction.Peru },
            caliberMm: 11f, reloadSeconds: 2.2f, damage: 80f, spreadDegrees: 0.5f,
            effectiveRangeM: 500f, maxRangeM: 1200f, muzzleVelocityMs: 410f, capacity: 1,
            historicalNote: "Cerrojo francés Mle 1866 de 11 mm. Gran precisión.");

        public static readonly WeaponSpec Gras = new WeaponSpec(
            GrasId, "Fusil Gras", WeaponClass.SingleShotRifle, ActionType.BoltAction,
            new[] { Faction.Peru, Faction.Chile },
            caliberMm: 11f, reloadSeconds: 2.2f, damage: 85f, spreadDegrees: 0.5f,
            effectiveRangeM: 500f, maxRangeM: 1200f, muzzleVelocityMs: 450f, capacity: 1,
            historicalNote: "Cerrojo francés Mle 1874 de 11 mm con cartucho metálico. Gran precisión.");

        public static readonly WeaponSpec Remington = new WeaponSpec(
            RemingtonId, "Fusil Remington Rolling Block", WeaponClass.SingleShotRifle, ActionType.RollingBlock,
            new[] { Faction.Bolivia, Faction.Alliance },
            caliberMm: 11f, reloadSeconds: 2.1f, damage: 100f, spreadDegrees: 0.9f,
            effectiveRangeM: 350f, maxRangeM: 1000f, muzzleVelocityMs: 420f, capacity: 1,
            historicalNote: "Monotiro de bloque rodante. Potencia de parada brutal, sonido cavernoso.");

        public static readonly WeaponSpec Corvo = new WeaponSpec(
            CorvoId, "Corvo chileno", WeaponClass.Melee, ActionType.None,
            new[] { Faction.Chile },
            caliberMm: 0f, reloadSeconds: 0f, damage: 120f, spreadDegrees: 0f,
            effectiveRangeM: 1.2f, maxRangeM: 1.5f, muzzleVelocityMs: 0f, capacity: 0,
            historicalNote: "Cuchillo curvo de hoja ancha, emblemático del soldado chileno.");

        public static readonly WeaponSpec TriangularBayonet = new WeaponSpec(
            TriangularBayonetId, "Bayoneta triangular", WeaponClass.Melee, ActionType.None,
            new[] { Faction.Peru, Faction.Bolivia, Faction.Alliance },
            caliberMm: 0f, reloadSeconds: 0f, damage: 110f, spreadDegrees: 0f,
            effectiveRangeM: 1.8f, maxRangeM: 2.2f, muzzleVelocityMs: 0f, capacity: 0,
            historicalNote: "Bayoneta de cubo de sección triangular calada en el fusil aliado.");

        public static IReadOnlyList<WeaponSpec> All { get; } = new[]
        {
            Comblain, Chassepot, Gras, Remington, Corvo, TriangularBayonet
        };

        public static WeaponSpec FindById(string id)
        {
            foreach (var spec in All)
            {
                if (spec.Id == id) return spec;
            }
            return null;
        }
    }
}
