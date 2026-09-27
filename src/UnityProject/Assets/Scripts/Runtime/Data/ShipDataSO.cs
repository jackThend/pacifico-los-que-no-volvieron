using System;
using System.Collections.Generic;
using System.Linq;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Data
{
    /// <summary>
    /// Datos de un buque editables en el Inspector (ROADMAP 1.2). Generados desde <see cref="ShipCatalog"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Ship_", menuName = "Pacífico/Datos/Buque", order = 1)]
    public sealed class ShipDataSO : ScriptableObject
    {
        [Serializable]
        public sealed class Armor
        {
            [Min(0f)] public float beltMidshipsMm;
            [Min(0f)] public float beltEndsMm;
            [Tooltip("Torre giratoria o reducto central.")]
            [Min(0f)] public float mainBatteryMm;
            [Min(0f)] public float conningTowerMm;
            [Min(0f)] public float deckMm;
            [Tooltip("Almohadilla de teca (blindados) o grosor del costado (madera).")]
            [Min(0f)] public float woodBackingMm;
            [Min(0f)] public float hullPlatingMm;
        }

        [Serializable]
        public sealed class Gun
        {
            public string designation = string.Empty;
            [Min(1)] public int count = 1;
            [Min(0f)] public float boreMm;
            [Min(0f)] public float shellWeightLb;
            public GunPlacement placement = GunPlacement.Broadside;
            public bool rifled = true;
            public bool muzzleLoading = true;
            [Min(0f)] public float muzzleVelocityMps;
            [Min(0f)] public float reloadSeconds;
        }

        [Serializable]
        public sealed class Turret
        {
            public string designation = string.Empty;
            [Min(0f)] public float traverseDegPerSecond;
            public float minElevationDeg;
            public float maxElevationDeg;
            [Min(0f)] public float elevationDegPerSecond;
            [Range(0f, 360f)] public float blindArcCenterDeg = 180f;
            [Range(0f, 179f)] public float blindArcHalfWidthDeg;
        }

        [Serializable]
        public sealed class Handling
        {
            [Min(0.1f)] public float accelerationTimeSeconds = 45f;
            [Min(0.1f)] public float coastDownTimeSeconds = 150f;
            [Min(0f)] public float maxTurnRateDegPerSecond = 3f;
            [Min(0.1f)] public float rudderTimeSeconds = 4f;
        }

        [Header("Identidad")]
        public string id = string.Empty;
        public string shipName = string.Empty;
        public Faction faction = Faction.Chile;
        public ShipType type = ShipType.Corvette;
        public HullMaterial hull = HullMaterial.Wood;
        public string builder = string.Empty;
        public int yearLaunched = 1865;

        [Header("Dimensiones y máquina")]
        [Min(0f)] public float displacementTonnes;
        [Min(0f)] public float lengthM;
        [Min(0f)] public float beamM;
        [Min(0f)] public float draughtM;
        [Min(0f)] public float designSpeedKnots;
        [Tooltip("Velocidad real en 1879; es la que usa la simulación.")]
        [Min(0f)] public float speedKnots1879;
        [Min(1)] public int complement = 1;
        public bool hasRam;

        [Header("Blindaje y artillería")]
        public Armor armor = new Armor();
        public List<Gun> guns = new List<Gun>();
        public bool hasTurret;
        public Turret turret = new Turret();

        [Header("Maniobra (juego)")]
        public Handling handling = new Handling();

        [Header("Fuentes")]
        public SerializableHistoricalSource source = new SerializableHistoricalSource();

        public ShipSpec ToSpec()
        {
            return new ShipSpec
            {
                Id = id,
                Name = shipName,
                Faction = faction,
                Type = type,
                Hull = hull,
                Builder = builder,
                YearLaunched = yearLaunched,
                DisplacementTonnes = displacementTonnes,
                LengthM = lengthM,
                BeamM = beamM,
                DraughtM = draughtM,
                DesignSpeedKnots = designSpeedKnots,
                SpeedKnots1879 = speedKnots1879,
                Complement = complement,
                HasRam = hasRam,
                Armor = new ArmorLayout
                {
                    BeltMidshipsMm = armor.beltMidshipsMm,
                    BeltEndsMm = armor.beltEndsMm,
                    MainBatteryMm = armor.mainBatteryMm,
                    ConningTowerMm = armor.conningTowerMm,
                    DeckMm = armor.deckMm,
                    WoodBackingMm = armor.woodBackingMm,
                    HullPlatingMm = armor.hullPlatingMm,
                },
                Guns = guns.Select(g => new GunMount
                {
                    Designation = g.designation,
                    Count = g.count,
                    BoreMm = g.boreMm,
                    ShellWeightLb = g.shellWeightLb,
                    Placement = g.placement,
                    Rifled = g.rifled,
                    MuzzleLoading = g.muzzleLoading,
                    MuzzleVelocityMps = g.muzzleVelocityMps,
                    ReloadSeconds = g.reloadSeconds,
                }).ToList(),
                Turret = hasTurret
                    ? new TurretSpec
                    {
                        Designation = turret.designation,
                        TraverseDegPerSecond = turret.traverseDegPerSecond,
                        MinElevationDeg = turret.minElevationDeg,
                        MaxElevationDeg = turret.maxElevationDeg,
                        ElevationDegPerSecond = turret.elevationDegPerSecond,
                        BlindArcCenterDeg = turret.blindArcCenterDeg,
                        BlindArcHalfWidthDeg = turret.blindArcHalfWidthDeg,
                    }
                    : null,
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = handling.accelerationTimeSeconds,
                    CoastDownTimeSeconds = handling.coastDownTimeSeconds,
                    MaxTurnRateDegPerSecond = handling.maxTurnRateDegPerSecond,
                    RudderTimeSeconds = handling.rudderTimeSeconds,
                },
                Source = source != null ? source.ToCore() : new HistoricalSource(),
            };
        }

        public void CopyFrom(ShipSpec spec)
        {
            id = spec.Id;
            shipName = spec.Name;
            faction = spec.Faction;
            type = spec.Type;
            hull = spec.Hull;
            builder = spec.Builder;
            yearLaunched = spec.YearLaunched;
            displacementTonnes = spec.DisplacementTonnes;
            lengthM = spec.LengthM;
            beamM = spec.BeamM;
            draughtM = spec.DraughtM;
            designSpeedKnots = spec.DesignSpeedKnots;
            speedKnots1879 = spec.SpeedKnots1879;
            complement = spec.Complement;
            hasRam = spec.HasRam;
            armor = new Armor
            {
                beltMidshipsMm = spec.Armor.BeltMidshipsMm,
                beltEndsMm = spec.Armor.BeltEndsMm,
                mainBatteryMm = spec.Armor.MainBatteryMm,
                conningTowerMm = spec.Armor.ConningTowerMm,
                deckMm = spec.Armor.DeckMm,
                woodBackingMm = spec.Armor.WoodBackingMm,
                hullPlatingMm = spec.Armor.HullPlatingMm,
            };
            guns = spec.Guns.Select(g => new Gun
            {
                designation = g.Designation,
                count = g.Count,
                boreMm = g.BoreMm,
                shellWeightLb = g.ShellWeightLb,
                placement = g.Placement,
                rifled = g.Rifled,
                muzzleLoading = g.MuzzleLoading,
                muzzleVelocityMps = g.MuzzleVelocityMps,
                reloadSeconds = g.ReloadSeconds,
            }).ToList();
            hasTurret = spec.Turret != null;
            turret = hasTurret
                ? new Turret
                {
                    designation = spec.Turret.Designation,
                    traverseDegPerSecond = spec.Turret.TraverseDegPerSecond,
                    minElevationDeg = spec.Turret.MinElevationDeg,
                    maxElevationDeg = spec.Turret.MaxElevationDeg,
                    elevationDegPerSecond = spec.Turret.ElevationDegPerSecond,
                    blindArcCenterDeg = spec.Turret.BlindArcCenterDeg,
                    blindArcHalfWidthDeg = spec.Turret.BlindArcHalfWidthDeg,
                }
                : new Turret();
            handling = new Handling
            {
                accelerationTimeSeconds = spec.Handling.AccelerationTimeSeconds,
                coastDownTimeSeconds = spec.Handling.CoastDownTimeSeconds,
                maxTurnRateDegPerSecond = spec.Handling.MaxTurnRateDegPerSecond,
                rudderTimeSeconds = spec.Handling.RudderTimeSeconds,
            };
            source = SerializableHistoricalSource.FromCore(spec.Source);
        }

        public ValidationResult Validate() => ToSpec().Validate();

        private void OnValidate()
        {
            var result = Validate();
            if (!result.IsValid) Debug.LogWarning(result.ToString(), this);
        }
    }
}
