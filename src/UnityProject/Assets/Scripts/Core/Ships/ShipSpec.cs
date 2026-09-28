using System.Collections.Generic;

namespace Pacifico.Core.Ships
{
    /// <summary>
    /// Especificación inmutable de un buque. Fuente de verdad para ShipDataSO
    /// y para los tests. Unidades: toneladas, metros, nudos, pulgadas de
    /// coraza, libras de proyectil y segundos.
    /// </summary>
    public sealed class ShipSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Faction Faction { get; }
        public ShipType Type { get; }
        public HullMaterial Hull { get; }
        public float DisplacementTons { get; }
        public float LengthM { get; }
        public float BeamM { get; }
        public float MaxSpeedKnots { get; }
        public int Crew { get; }
        public bool HasRam { get; }
        public float HullIntegrity { get; }
        public IReadOnlyList<ArmorPlate> Armor { get; }
        public IReadOnlyList<GunBattery> Guns { get; }

        /// <summary>Montaje de torre giratoria; null si el buque no tiene torre.</summary>
        public TurretMount Turret { get; }
        public string HistoricalNote { get; }

        public ShipSpec(
            string id,
            string displayName,
            Faction faction,
            ShipType type,
            HullMaterial hull,
            float displacementTons,
            float lengthM,
            float beamM,
            float maxSpeedKnots,
            int crew,
            bool hasRam,
            float hullIntegrity,
            IReadOnlyList<ArmorPlate> armor,
            IReadOnlyList<GunBattery> guns,
            string historicalNote,
            TurretMount turret = null)
        {
            Id = id;
            DisplayName = displayName;
            Faction = faction;
            Type = type;
            Hull = hull;
            DisplacementTons = displacementTons;
            LengthM = lengthM;
            BeamM = beamM;
            MaxSpeedKnots = maxSpeedKnots;
            Crew = crew;
            HasRam = hasRam;
            HullIntegrity = hullIntegrity;
            Armor = armor;
            Guns = guns;
            HistoricalNote = historicalNote;
            Turret = turret;
        }

        public bool HasTurret
        {
            get
            {
                foreach (var battery in Guns)
                {
                    if (battery.mount == GunMount.Turret) return true;
                }
                return false;
            }
        }

        /// <summary>Espesor de coraza en una zona (0 si no está blindada).</summary>
        public float ArmorInches(ArmorZone zone)
        {
            foreach (var plate in Armor)
            {
                if (plate.zone == zone) return plate.thicknessInches;
            }
            return 0f;
        }

        /// <summary>Proyectil más pesado que dispara el buque (lb).</summary>
        public float HeaviestProjectileLbs
        {
            get
            {
                var max = 0f;
                foreach (var battery in Guns)
                {
                    if (battery.projectileLbs > max) max = battery.projectileLbs;
                }
                return max;
            }
        }
    }
}
