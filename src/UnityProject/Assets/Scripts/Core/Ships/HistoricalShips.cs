using System.Collections.Generic;

namespace Pacifico.Core.Ships
{
    /// <summary>
    /// Catálogo de buques del módulo naval (GDD §3.2, Guion caps. 1 y 3).
    /// Coraza, calibres y dimensiones son aproximaciones históricas; la
    /// velocidad refleja el estado en 1879; integridad de casco y tiempos de
    /// recarga de artillería son valores de balance (acelerados para jugar).
    /// </summary>
    public static class HistoricalShips
    {
        public const string HuascarId = "huascar";
        public const string EsmeraldaId = "esmeralda";
        public const string CochraneId = "cochrane";
        public const string IndependenciaId = "independencia";

        /// <summary>
        /// Torre Coles del Huáscar. Gira 360° (a brazo, con su dotación), pero
        /// el castillo de proa y la chimenea/palo/puente enmascaran el tiro.
        /// Velocidades de puntería, sectores y elevación: aproximación de balance;
        /// velocidad de boca aproximada del Armstrong de 10".
        /// </summary>
        public static readonly TurretMount HuascarColesTurret = new TurretMount(
            traverseDegreesPerSecond: 4f,
            elevationDegreesPerSecond: 1.5f,
            minElevationDegrees: -5f,
            maxElevationDegrees: 12f,
            muzzleVelocityMs: 400f,
            barrelSeparationM: 2.2f,
            gunHeightM: 3f,
            offsetForwardM: 6f,
            blindSectors: new[]
            {
                new FiringArcBlock(0f, 12f, "Castillo de proa"),
                new FiringArcBlock(180f, 35f, "Chimenea, palo y puente")
            });

        public static readonly ShipSpec Huascar = new ShipSpec(
            HuascarId, "Monitor Huáscar", Faction.Peru, ShipType.Monitor, HullMaterial.Iron,
            displacementTons: 1130f, lengthM: 59f, beamM: 10.7f, maxSpeedKnots: 11f, crew: 200,
            hasRam: true, hullIntegrity: 3000f,
            armor: new[]
            {
                new ArmorPlate(ArmorZone.BeltMidship, 4.5f),
                new ArmorPlate(ArmorZone.BeltEnds, 2.5f),
                new ArmorPlate(ArmorZone.Turret, 5.5f),
                new ArmorPlate(ArmorZone.ConningTower, 3f)
            },
            guns: new[]
            {
                new GunBattery("Armstrong 10\" de 300 lb", 2, 300f, GunMount.Turret, 15f),
                new GunBattery("Armstrong de 40 lb", 2, 40f, GunMount.Broadside, 8f),
                new GunBattery("Cañón de 12 lb", 1, 12f, GunMount.Pivot, 5f)
            },
            historicalNote: "Monitor de hierro (Laird, 1865). Torre giratoria Coles movida a mano con dos Armstrong de 300 lb y espolón de proa.",
            turret: HuascarColesTurret);

        public static readonly ShipSpec Esmeralda = new ShipSpec(
            EsmeraldaId, "Corbeta Esmeralda", Faction.Chile, ShipType.Corvette, HullMaterial.Wood,
            displacementTons: 850f, lengthM: 61f, beamM: 10f, maxSpeedKnots: 4f, crew: 200,
            hasRam: false, hullIntegrity: 1500f,
            armor: new ArmorPlate[0],
            guns: new[]
            {
                new GunBattery("Cañón de 40 lb", 16, 40f, GunMount.Broadside, 6f)
            },
            historicalNote: "Corbeta de madera (1855). En Iquique sus calderas averiadas apenas le daban unos 3–4 nudos.");

        public static readonly ShipSpec Cochrane = new ShipSpec(
            CochraneId, "Fragata blindada Cochrane", Faction.Chile, ShipType.CentralBatteryIronclad, HullMaterial.Iron,
            displacementTons: 3560f, lengthM: 64f, beamM: 13.9f, maxSpeedKnots: 12.8f, crew: 300,
            hasRam: true, hullIntegrity: 5000f,
            armor: new[]
            {
                new ArmorPlate(ArmorZone.BeltMidship, 9f),
                new ArmorPlate(ArmorZone.BeltEnds, 4.5f),
                new ArmorPlate(ArmorZone.CentralBattery, 8f)
            },
            guns: new[]
            {
                new GunBattery("Armstrong 9\" de 250 lb", 6, 250f, GunMount.CentralBattery, 12f)
            },
            historicalNote: "Blindado de reducto central (Earle's, 1874). Recién carenado, cerró la trampa de Angamos junto al Blanco Encalada.");

        public static readonly ShipSpec Independencia = new ShipSpec(
            IndependenciaId, "Fragata blindada Independencia", Faction.Peru, ShipType.ArmouredFrigate, HullMaterial.Iron,
            displacementTons: 2004f, lengthM: 65.8f, beamM: 13.4f, maxSpeedKnots: 12f, crew: 250,
            hasRam: true, hullIntegrity: 4000f,
            armor: new[]
            {
                new ArmorPlate(ArmorZone.BeltMidship, 4.5f),
                new ArmorPlate(ArmorZone.CentralBattery, 4.5f)
            },
            guns: new[]
            {
                new GunBattery("Vavasseur de 150 lb", 2, 150f, GunMount.Pivot, 10f),
                new GunBattery("Armstrong de 70 lb", 12, 70f, GunMount.Broadside, 8f)
            },
            historicalNote: "Fragata blindada (Samuda, 1865). Encalló en Punta Gruesa persiguiendo a la goleta Covadonga.");

        public static IReadOnlyList<ShipSpec> All { get; } = new[]
        {
            Huascar, Esmeralda, Cochrane, Independencia
        };

        public static ShipSpec FindById(string id)
        {
            foreach (var spec in All)
            {
                if (spec.Id == id) return spec;
            }
            return null;
        }
    }
}
