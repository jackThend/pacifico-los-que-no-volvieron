using System.Collections.Generic;
using System.Linq;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    public enum ShipType
    {
        Monitor = 0,                 // Huáscar: buque de torre con espolón
        Corvette = 1,                // Esmeralda
        Schooner = 2,                // Covadonga (goleta cañonera)
        BroadsideIronclad = 3,       // Independencia: fragata blindada de batería
        CentralBatteryIronclad = 4,  // Cochrane, Blanco Encalada
    }

    public enum HullMaterial
    {
        Wood = 0,
        Iron = 1,
    }

    /// <summary>Zonas de impacto que distingue el sistema de blindaje (ROADMAP 2.3).</summary>
    public enum ArmorZone
    {
        BeltMidships = 0,
        BeltEnds = 1,
        /// <summary>Torre giratoria (monitor) o reducto central de artillería (fragatas).</summary>
        MainBattery = 2,
        ConningTower = 3,
        Deck = 4,
        /// <summary>Superestructura, arboladura y obra muerta sin coraza.</summary>
        Unarmored = 5,
    }

    /// <summary>Espesores de coraza de hierro por zona (mm) y respaldo/casco.</summary>
    public sealed class ArmorLayout
    {
        public float BeltMidshipsMm { get; set; }
        public float BeltEndsMm { get; set; }
        public float MainBatteryMm { get; set; }
        public float ConningTowerMm { get; set; }
        public float DeckMm { get; set; }
        /// <summary>Almohadilla de teca tras las planchas (blindados) o grosor del costado (buques de madera).</summary>
        public float WoodBackingMm { get; set; }
        /// <summary>Chapa del casco de hierro sin blindar (0 en buques de madera).</summary>
        public float HullPlatingMm { get; set; }

        public float IronThicknessFor(ArmorZone zone)
        {
            switch (zone)
            {
                case ArmorZone.BeltMidships: return BeltMidshipsMm;
                case ArmorZone.BeltEnds: return BeltEndsMm;
                case ArmorZone.MainBattery: return MainBatteryMm;
                case ArmorZone.ConningTower: return ConningTowerMm;
                case ArmorZone.Deck: return DeckMm;
                default: return HullPlatingMm;
            }
        }

        /// <summary>Respaldo de madera detrás de la zona. La cubierta y la obra muerta no lo tienen.</summary>
        public float WoodFor(ArmorZone zone)
        {
            switch (zone)
            {
                case ArmorZone.BeltMidships:
                case ArmorZone.BeltEnds:
                case ArmorZone.MainBattery:
                    return WoodBackingMm;
                case ArmorZone.Unarmored:
                    return HullPlatingMm > 0f ? 0f : WoodBackingMm;
                default:
                    return 0f;
            }
        }

        public bool IsArmored => BeltMidshipsMm > 0f;

        public ArmorLayout Clone() => (ArmorLayout)MemberwiseClone();
    }

    public enum GunPlacement
    {
        Turret = 0,
        Broadside = 1,
        Pivot = 2,
        CentralBattery = 3,
    }

    /// <summary>Grupo de piezas de artillería idénticas.</summary>
    public sealed class GunMount
    {
        public string Designation { get; set; } = string.Empty;
        public int Count { get; set; }
        public float BoreMm { get; set; }
        /// <summary>Peso nominal del proyectil en libras (así se designaban: «cañón de 300»).</summary>
        public float ShellWeightLb { get; set; }
        public GunPlacement Placement { get; set; }
        public bool Rifled { get; set; } = true;
        public bool MuzzleLoading { get; set; } = true;
        public float MuzzleVelocityMps { get; set; }
        /// <summary>Tiempo de recarga de juego (s). Históricamente las piezas pesadas tardaban minutos.</summary>
        public float ReloadSeconds { get; set; }

        public float ShellMassKg => Units.PoundsToKilograms(ShellWeightLb);

        public GunMount Clone() => (GunMount)MemberwiseClone();
    }

    /// <summary>Torre giratoria tipo Coles (Huáscar).</summary>
    public sealed class TurretSpec
    {
        public string Designation { get; set; } = string.Empty;
        /// <summary>Velocidad de giro de juego (°/s). La torre original se giraba a mano con manivelas.</summary>
        public float TraverseDegPerSecond { get; set; }
        public float MinElevationDeg { get; set; }
        public float MaxElevationDeg { get; set; }
        public float ElevationDegPerSecond { get; set; }
        /// <summary>Centro del sector ciego respecto a la proa (°, 180 = popa): chimenea y superestructura.</summary>
        public float BlindArcCenterDeg { get; set; }
        /// <summary>Semiancho del sector ciego (°). 0 = sin sector ciego.</summary>
        public float BlindArcHalfWidthDeg { get; set; }

        public TurretSpec Clone() => (TurretSpec)MemberwiseClone();
    }

    /// <summary>Parámetros de maniobra (licencias de juego calibradas con la velocidad histórica).</summary>
    public sealed class ShipHandling
    {
        /// <summary>Segundos para pasar de parado a toda fuerza con el telégrafo en «Toda».</summary>
        public float AccelerationTimeSeconds { get; set; }
        /// <summary>Segundos para perder la arrancada de toda fuerza a parado por rozamiento del casco.</summary>
        public float CoastDownTimeSeconds { get; set; }
        /// <summary>Velocidad angular máxima con el timón a la banda y buena arrancada (°/s).</summary>
        public float MaxTurnRateDegPerSecond { get; set; }
        /// <summary>Segundos para llevar el timón de la vía a la banda.</summary>
        public float RudderTimeSeconds { get; set; }

        public ShipHandling Clone() => (ShipHandling)MemberwiseClone();
    }

    /// <summary>Especificación completa de un buque (ROADMAP 1.2).</summary>
    public sealed class ShipSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Faction Faction { get; set; }
        public ShipType Type { get; set; }
        public HullMaterial Hull { get; set; }
        public string Builder { get; set; } = string.Empty;
        public int YearLaunched { get; set; }

        public float DisplacementTonnes { get; set; }
        public float LengthM { get; set; }
        public float BeamM { get; set; }
        public float DraughtM { get; set; }
        public float DesignSpeedKnots { get; set; }
        /// <summary>Velocidad real en 1879 (calderas gastadas, fondos sucios...). Es la que usa el juego.</summary>
        public float SpeedKnots1879 { get; set; }
        public int Complement { get; set; }
        public bool HasRam { get; set; }

        public ArmorLayout Armor { get; set; } = new ArmorLayout();
        public List<GunMount> Guns { get; set; } = new List<GunMount>();
        /// <summary>Null si el buque no tiene torre giratoria.</summary>
        public TurretSpec Turret { get; set; }
        public ShipHandling Handling { get; set; } = new ShipHandling();
        public HistoricalSource Source { get; set; } = new HistoricalSource();

        public float MaxSpeedMps => Units.KnotsToMetersPerSecond(SpeedKnots1879);

        public int TotalGuns => Guns.Sum(g => g.Count);

        public GunMount HeaviestGun => Guns.OrderByDescending(g => g.ShellWeightLb).FirstOrDefault();

        /// <summary>Peso total de una andanada de todas las piezas, en libras.</summary>
        public float TotalShellWeightLb => Guns.Sum(g => g.Count * g.ShellWeightLb);

        public ValidationResult Validate()
        {
            var r = new ValidationResult("Buque '" + Id + "'");
            r.RequireNotEmpty(Id, "Id");
            r.RequireNotEmpty(Name, "Name");
            r.Require(Faction != Faction.Neutral, "debe pertenecer a un bando");
            r.Require(YearLaunched >= 1840 && YearLaunched <= 1880, "YearLaunched fuera de rango (" + YearLaunched + ")");
            r.RequirePositive(DisplacementTonnes, "DisplacementTonnes");
            r.RequirePositive(LengthM, "LengthM");
            r.RequirePositive(BeamM, "BeamM");
            r.RequirePositive(DraughtM, "DraughtM");
            r.Require(LengthM > BeamM, "la eslora debe superar a la manga");
            r.Require(BeamM > DraughtM, "la manga debe superar al calado");
            r.RequirePositive(DesignSpeedKnots, "DesignSpeedKnots");
            r.RequirePositive(SpeedKnots1879, "SpeedKnots1879");
            r.Require(SpeedKnots1879 <= DesignSpeedKnots, "la velocidad de 1879 no puede superar la de diseño");
            r.Require(Complement > 0, "Complement debe ser > 0");
            r.Require(Guns.Count > 0, "debe tener artillería");
            r.Require(Source.References.Count > 0, "requiere al menos una referencia histórica");

            ValidateArmor(r);
            ValidateGuns(r);
            ValidateHandling(r);
            return r;
        }

        private void ValidateArmor(ValidationResult r)
        {
            ArmorLayout a = Armor;
            r.Require(a != null, "Armor no puede ser nulo");
            if (a == null) return;
            foreach (ArmorZone zone in new[] { ArmorZone.BeltMidships, ArmorZone.BeltEnds, ArmorZone.MainBattery, ArmorZone.ConningTower, ArmorZone.Deck })
            {
                r.RequireNonNegative(a.IronThicknessFor(zone), "Armor." + zone);
            }
            r.Require(a.BeltEndsMm <= a.BeltMidshipsMm, "el cinturón en los extremos no puede superar al central");
            if (Hull == HullMaterial.Wood)
            {
                r.Require(!a.IsArmored && a.MainBatteryMm == 0f && a.HullPlatingMm == 0f, "un casco de madera no lleva coraza ni chapa");
                r.RequirePositive(a.WoodBackingMm, "Armor.WoodBackingMm (grosor del costado)");
            }
            else
            {
                r.RequirePositive(a.HullPlatingMm, "Armor.HullPlatingMm");
                if (a.IsArmored) r.RequirePositive(a.WoodBackingMm, "Armor.WoodBackingMm (almohadilla de teca)");
            }
        }

        private void ValidateGuns(ValidationResult r)
        {
            bool turretGuns = false;
            foreach (GunMount g in Guns)
            {
                string where = "Guns[" + g.Designation + "].";
                r.RequireNotEmpty(g.Designation, "Guns[].Designation");
                r.Require(g.Count > 0, where + "Count debe ser > 0");
                r.RequirePositive(g.BoreMm, where + "BoreMm");
                r.RequirePositive(g.ShellWeightLb, where + "ShellWeightLb");
                r.Require(g.MuzzleVelocityMps >= 250f && g.MuzzleVelocityMps <= 650f, where + "MuzzleVelocityMps fuera de rango");
                r.RequirePositive(g.ReloadSeconds, where + "ReloadSeconds");
                turretGuns |= g.Placement == GunPlacement.Turret;
            }
            r.Require(turretGuns == (Turret != null), "las piezas en torre requieren TurretSpec (y viceversa)");
            if (Turret != null)
            {
                r.RequirePositive(Turret.TraverseDegPerSecond, "Turret.TraverseDegPerSecond");
                r.Require(Turret.MinElevationDeg < Turret.MaxElevationDeg, "Turret: elevación mínima debe ser menor que la máxima");
                r.RequirePositive(Turret.ElevationDegPerSecond, "Turret.ElevationDegPerSecond");
                r.Require(Turret.BlindArcHalfWidthDeg >= 0f && Turret.BlindArcHalfWidthDeg < 180f, "Turret.BlindArcHalfWidthDeg fuera de rango");
            }
        }

        private void ValidateHandling(ValidationResult r)
        {
            ShipHandling h = Handling;
            r.Require(h != null, "Handling no puede ser nulo");
            if (h == null) return;
            r.RequirePositive(h.AccelerationTimeSeconds, "Handling.AccelerationTimeSeconds");
            r.RequirePositive(h.CoastDownTimeSeconds, "Handling.CoastDownTimeSeconds");
            r.Require(h.CoastDownTimeSeconds > h.AccelerationTimeSeconds, "un buque debe conservar la arrancada más tiempo del que tarda en ganarla");
            r.RequirePositive(h.MaxTurnRateDegPerSecond, "Handling.MaxTurnRateDegPerSecond");
            r.RequirePositive(h.RudderTimeSeconds, "Handling.RudderTimeSeconds");
        }

        public ShipSpec Clone()
        {
            var copy = (ShipSpec)MemberwiseClone();
            copy.Armor = Armor?.Clone();
            copy.Guns = Guns.Select(g => g.Clone()).ToList();
            copy.Turret = Turret?.Clone();
            copy.Handling = Handling?.Clone();
            copy.Source = Source.Clone();
            return copy;
        }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
