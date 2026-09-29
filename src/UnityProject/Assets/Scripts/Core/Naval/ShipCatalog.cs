using System.Collections.Generic;
using System.Linq;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Buques de la campaña naval de 1879 (Capítulos 1 y 2). Fuente de verdad para los <c>ShipDataSO</c>.
    /// Cada llamada devuelve instancias nuevas.
    /// </summary>
    public static class ShipCatalog
    {
        public const string HuascarId = "huascar";
        public const string EsmeraldaId = "esmeralda";
        public const string CovadongaId = "covadonga";
        public const string IndependenciaId = "independencia";
        public const string CochraneId = "almirante_cochrane";
        public const string BlancoEncaladaId = "blanco_encalada";

        public static IReadOnlyList<ShipSpec> All()
        {
            return new[] { Huascar(), Esmeralda(), Covadonga(), Independencia(), Cochrane(), BlancoEncalada() };
        }

        public static ShipSpec Get(string id) => All().FirstOrDefault(s => s.Id == id);

        // -------------------------------------------------------------------------------------------
        // Piezas de artillería compartidas
        // -------------------------------------------------------------------------------------------

        private static GunMount Armstrong300(int count) => new GunMount
        {
            Designation = "Armstrong 10 pulg. (300 lb) de avancarga",
            Count = count, BoreMm = 254f, ShellWeightLb = 300f, Placement = GunPlacement.Turret,
            MuzzleVelocityMps = 400f, ReloadSeconds = 20f,
        };

        private static GunMount Armstrong250(int count) => new GunMount
        {
            Designation = "Armstrong 9 pulg. (250 lb) de avancarga",
            Count = count, BoreMm = 229f, ShellWeightLb = 250f, Placement = GunPlacement.CentralBattery,
            MuzzleVelocityMps = 430f, ReloadSeconds = 18f,
        };

        private static GunMount Armstrong70(int count, GunPlacement placement) => new GunMount
        {
            Designation = "Armstrong 6,4 pulg. (70 lb) de avancarga",
            Count = count, BoreMm = 163f, ShellWeightLb = 70f, Placement = placement,
            MuzzleVelocityMps = 370f, ReloadSeconds = 10f,
        };

        private static GunMount Armstrong40(int count, GunPlacement placement) => new GunMount
        {
            Designation = "Armstrong 4,75 pulg. (40 lb) de avancarga",
            Count = count, BoreMm = 120f, ShellWeightLb = 40f, Placement = placement,
            MuzzleVelocityMps = 360f, ReloadSeconds = 8f,
        };

        private static GunMount Light12(int count, bool muzzleLoading) => new GunMount
        {
            Designation = "Armstrong 3 pulg. (12 lb)",
            Count = count, BoreMm = 76f, ShellWeightLb = 12f, Placement = GunPlacement.Pivot,
            MuzzleLoading = muzzleLoading, MuzzleVelocityMps = 350f, ReloadSeconds = 5f,
        };

        private static readonly string[] EstimatedGunBallistics =
        {
            "Guns.MuzzleVelocityMps",
        };

        // -------------------------------------------------------------------------------------------
        // Buques
        // -------------------------------------------------------------------------------------------

        /// <summary>Monitor Huáscar (Perú). Torre Coles con dos Armstrong de 300 lb, coraza de 4,5" y espolón.</summary>
        public static ShipSpec Huascar()
        {
            var spec = new ShipSpec
            {
                Id = HuascarId,
                Name = "Monitor Huáscar",
                Faction = Faction.Peru,
                Type = ShipType.Monitor,
                Hull = HullMaterial.Iron,
                Builder = "Laird Brothers, Birkenhead (Inglaterra)",
                YearLaunched = 1865,
                DisplacementTonnes = 1745f,
                LengthM = 59.4f,
                BeamM = 10.6f,
                DraughtM = 4.6f,
                DesignSpeedKnots = 12f,
                SpeedKnots1879 = 11f,
                Complement = 200,
                HasRam = true,
                Armor = new ArmorLayout
                {
                    BeltMidshipsMm = 114.3f,  // 4,5"
                    BeltEndsMm = 63.5f,       // 2,5"
                    MainBatteryMm = 139.7f,   // 5,5" (torre)
                    ConningTowerMm = 76.2f,   // 3"
                    DeckMm = 0f,
                    WoodBackingMm = 254f,
                    HullPlatingMm = 25f,
                },
                Guns = { Armstrong300(2), Armstrong40(2, GunPlacement.Broadside), Light12(1, true) },
                Turret = new TurretSpec
                {
                    Designation = "Torre giratoria Coles",
                    TraverseDegPerSecond = 6f,
                    MinElevationDeg = -5f,
                    MaxElevationDeg = 10f,
                    ElevationDegPerSecond = 2f,
                    BlindArcCenterDeg = 180f,
                    BlindArcHalfWidthDeg = 30f,
                },
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = 45f,
                    CoastDownTimeSeconds = 150f,
                    MaxTurnRateDegPerSecond = 3.0f,
                    RudderTimeSeconds = 4f,
                },
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (es): «Monitor Huáscar» — eslora 59,4 m, manga 10,6 m; cinturón 114,3 mm (63,5 mm en extremos), torre 139,7 mm, torre de mando 76,2 mm; ~1.745 t a máxima carga.",
                        "Wikipedia (en): «Huáscar (ironclad)» — 2 × Armstrong de 10\" (300 lb) en torre Coles, 12 nudos.",
                        "Archivo Histórico: 01_Barcos_y_Combate_Naval/01 y 09 (fotografía de 1879 y daños tras Angamos).",
                    },
                    EstimatedFields =
                    {
                        nameof(ShipSpec.DraughtM), nameof(ShipSpec.SpeedKnots1879), nameof(ShipSpec.Complement),
                        "Armor.DeckMm", "Armor.WoodBackingMm", "Armor.HullPlatingMm",
                        "Turret.MinElevationDeg", "Turret.MaxElevationDeg",
                    },
                    Notes = "Las fuentes difieren en el desplazamiento (1.130 t en rosca, ~1.745 t a plena carga, 1.870 t largas en otras). " +
                            "La torre se giraba a mano: la velocidad de 6 °/s es una licencia de juego. El sector ciego de popa " +
                            "(chimenea y superestructura) es una abstracción de diseño.",
                },
            };
            spec.Source.EstimatedFields.AddRange(EstimatedGunBallistics);
            return spec;
        }

        /// <summary>Corbeta Esmeralda (Chile). Casco de madera de 1855; en 1879 sus calderas apenas daban 2–3 nudos.</summary>
        public static ShipSpec Esmeralda()
        {
            var spec = new ShipSpec
            {
                Id = EsmeraldaId,
                Name = "Corbeta Esmeralda",
                Faction = Faction.Chile,
                Type = ShipType.Corvette,
                Hull = HullMaterial.Wood,
                Builder = "Pitcher, Northfleet (Inglaterra)",
                YearLaunched = 1855,
                DisplacementTonnes = 850f,
                LengthM = 64f,
                BeamM = 9.75f,
                DraughtM = 4.0f,
                DesignSpeedKnots = 8f,
                SpeedKnots1879 = 3f,
                Complement = 201,
                HasRam = false,
                Armor = new ArmorLayout { WoodBackingMm = 180f },
                Guns = { Armstrong40(12, GunPlacement.Broadside), Light12(2, false) },
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = 60f,
                    CoastDownTimeSeconds = 200f,
                    MaxTurnRateDegPerSecond = 2.0f,
                    RudderTimeSeconds = 5f,
                },
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (es): «Corbeta Esmeralda» — 850 t, eslora 64 m, manga 9,75 m, 201 tripulantes, 8 nudos de diseño; 2 nudos en el combate.",
                        "Armada de Chile: «Corbeta Esmeralda 2°» (unidades históricas).",
                        "Archivo Histórico: 01_Barcos_y_Combate_Naval/02 y 03 (fotografía y óleo de Somerscales).",
                    },
                    EstimatedFields =
                    {
                        nameof(ShipSpec.Builder), nameof(ShipSpec.DraughtM), "Armor.WoodBackingMm",
                    },
                    Notes = "Armamento de 1879 según las fuentes: 12 piezas de 40 lb más 2 de 12 lb de retrocarga " +
                            "(algunas fuentes desglosan 4 de las de 40 lb como Whitworth de ánima lisa). " +
                            "Velocidad de juego 3 nudos (las fuentes citan 2–3) para que la maniobra sea jugable.",
                },
            };
            spec.Source.EstimatedFields.AddRange(EstimatedGunBallistics);
            return spec;
        }

        /// <summary>Goleta Covadonga (Chile). Persiguida por la Independencia hasta Punta Gruesa.</summary>
        public static ShipSpec Covadonga()
        {
            var spec = new ShipSpec
            {
                Id = CovadongaId,
                Name = "Goleta Covadonga",
                Faction = Faction.Chile,
                Type = ShipType.Schooner,
                Hull = HullMaterial.Wood,
                Builder = "Arsenal de La Carraca, Cádiz (España)",
                YearLaunched = 1859,
                DisplacementTonnes = 412f,
                LengthM = 48.5f,
                BeamM = 7.5f,
                DraughtM = 3.0f,
                DesignSpeedKnots = 7f,
                SpeedKnots1879 = 6f,
                Complement = 120,
                Armor = new ArmorLayout { WoodBackingMm = 150f },
                Guns = { Armstrong70(2, GunPlacement.Pivot) },
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = 35f,
                    CoastDownTimeSeconds = 120f,
                    MaxTurnRateDegPerSecond = 3.5f,
                    RudderTimeSeconds = 3f,
                },
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (es): «Goleta Covadonga» — botada en La Carraca en 1859, eslora 48,5 m, máquina de 160 CV y 7 nudos; " +
                        "en la Guerra del Pacífico, goleta cañonera de 412 t con 2 cañones Armstrong de 70 lb.",
                        "Archivo Histórico: 01_Barcos_y_Combate_Naval/06 y 07.",
                    },
                    EstimatedFields =
                    {
                        nameof(ShipSpec.BeamM), nameof(ShipSpec.DraughtM), nameof(ShipSpec.SpeedKnots1879),
                        nameof(ShipSpec.Complement), "Armor.WoodBackingMm",
                    },
                    Notes = "Capturada a España en Papudo (1865). Hundida en Chancay en 1880.",
                },
            };
            spec.Source.EstimatedFields.AddRange(EstimatedGunBallistics);
            return spec;
        }

        /// <summary>Fragata blindada Independencia (Perú). Varada en Punta Gruesa el 21 de mayo de 1879.</summary>
        public static ShipSpec Independencia()
        {
            var spec = new ShipSpec
            {
                Id = IndependenciaId,
                Name = "Fragata blindada Independencia",
                Faction = Faction.Peru,
                Type = ShipType.BroadsideIronclad,
                Hull = HullMaterial.Iron,
                Builder = "Samuda Brothers, Londres (Inglaterra)",
                YearLaunched = 1865,
                DisplacementTonnes = 2004f,
                LengthM = 72f,
                BeamM = 13.6f,
                DraughtM = 6.1f,
                DesignSpeedKnots = 11f,
                SpeedKnots1879 = 10f,
                Complement = 250,
                HasRam = true,
                Armor = new ArmorLayout
                {
                    BeltMidshipsMm = 114.3f,
                    BeltEndsMm = 114.3f,
                    MainBatteryMm = 114.3f,
                    ConningTowerMm = 0f,
                    DeckMm = 0f,
                    WoodBackingMm = 254f,
                    HullPlatingMm = 25.4f,
                },
                Guns =
                {
                    new GunMount
                    {
                        Designation = "Cañón rayado de 150 lb",
                        Count = 2, BoreMm = 203f, ShellWeightLb = 150f, Placement = GunPlacement.Pivot,
                        MuzzleVelocityMps = 350f, ReloadSeconds = 14f,
                    },
                    Armstrong70(12, GunPlacement.Broadside),
                },
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = 70f,
                    CoastDownTimeSeconds = 220f,
                    MaxTurnRateDegPerSecond = 1.8f,
                    RudderTimeSeconds = 5f,
                },
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (es): «Independencia (fragata blindada)» — 2.004 t, 72 m de eslora, casco de hierro de 1\" con planchas de 4,5\" " +
                        "sobre 10\" de teca en la sección central y la línea de agua; 2 cañones rayados de 150 y 12 de 70 en batería; 11 nudos.",
                        "Apuntamientos sobre la fragata blindada Independencia (García y García, 1866), Biblioteca Nacional del Perú.",
                        "Archivo Histórico: 01_Barcos_y_Combate_Naval/08 (plano de perfil).",
                    },
                    EstimatedFields =
                    {
                        nameof(ShipSpec.BeamM), nameof(ShipSpec.DraughtM), nameof(ShipSpec.SpeedKnots1879),
                        nameof(ShipSpec.Complement), nameof(ShipSpec.HasRam), "Guns.BoreMm",
                    },
                    Notes = "Otras fuentes dan 65,5 m entre perpendiculares. Se perdió al varar persiguiendo a la Covadonga en Punta Gruesa.",
                },
            };
            spec.Source.EstimatedFields.AddRange(EstimatedGunBallistics);
            return spec;
        }

        /// <summary>Fragata blindada Almirante Cochrane (Chile), de reducto central. Recién carenada en Angamos.</summary>
        public static ShipSpec Cochrane()
        {
            ShipSpec spec = CentralBatteryIronclad(CochraneId, "Fragata blindada Almirante Cochrane", 1874, 12f);
            spec.Source.References.Insert(0,
                "Wikipedia (en): «Chilean ironclad Almirante Cochrane» — 3.480 t largas (3.540 t), cinturón 4,5–9\", " +
                "6 × 9\" de avancarga, 12 nudos; Earle's Shipbuilding, Hull, botada el 23-01-1874.");
            spec.Source.References.Add("Archivo Histórico: 01_Barcos_y_Combate_Naval/04 y 10.");
            spec.Source.Notes = "Carenada en Valparaíso antes de Angamos: mantuvo su velocidad frente al Huáscar.";
            return spec;
        }

        /// <summary>Fragata blindada Blanco Encalada (Chile), gemela del Cochrane, con los fondos sucios en 1879.</summary>
        public static ShipSpec BlancoEncalada()
        {
            ShipSpec spec = CentralBatteryIronclad(BlancoEncaladaId, "Fragata blindada Blanco Encalada", 1875, 10f);
            spec.Source.References.Insert(0,
                "Wikipedia (en): «Chilean ironclad Blanco Encalada» — gemela del Almirante Cochrane (Earle's Shipbuilding, Hull).");
            spec.Source.References.Add("Archivo Histórico: 01_Barcos_y_Combate_Naval/05 y 10.");
            spec.Source.EstimatedFields.Add(nameof(ShipSpec.SpeedKnots1879));
            spec.Source.Notes = "Sin carenar en 1879: velocidad reducida por el casco sucio (valor de juego estimado).";
            return spec;
        }

        private static ShipSpec CentralBatteryIronclad(string id, string name, int yearLaunched, float speed1879)
        {
            var spec = new ShipSpec
            {
                Id = id,
                Name = name,
                Faction = Faction.Chile,
                Type = ShipType.CentralBatteryIronclad,
                Hull = HullMaterial.Iron,
                Builder = "Earle's Shipbuilding, Hull (Inglaterra)",
                YearLaunched = yearLaunched,
                DisplacementTonnes = 3540f,
                LengthM = 64f,
                BeamM = 13.9f,
                DraughtM = 6.7f,
                DesignSpeedKnots = 12f,
                SpeedKnots1879 = speed1879,
                Complement = 300,
                HasRam = true,
                Armor = new ArmorLayout
                {
                    BeltMidshipsMm = 228.6f,  // 9"
                    BeltEndsMm = 114.3f,      // 4,5"
                    MainBatteryMm = 203.2f,   // 8" (reducto central)
                    ConningTowerMm = 0f,
                    DeckMm = 0f,
                    WoodBackingMm = 254f,
                    HullPlatingMm = 25.4f,
                },
                Guns =
                {
                    Armstrong250(6),
                    new GunMount
                    {
                        Designation = "Cañón de 20 lb",
                        Count = 1, BoreMm = 95f, ShellWeightLb = 20f, Placement = GunPlacement.Pivot,
                        MuzzleVelocityMps = 360f, ReloadSeconds = 6f,
                    },
                },
                Handling = new ShipHandling
                {
                    AccelerationTimeSeconds = 75f,
                    CoastDownTimeSeconds = 250f,
                    MaxTurnRateDegPerSecond = 2.2f,
                    RudderTimeSeconds = 5f,
                },
                Source = new HistoricalSource
                {
                    References = { "Guion, Cap. 2: «dirigir las descargas de los cañones de 250 libras»." },
                    EstimatedFields =
                    {
                        nameof(ShipSpec.LengthM), nameof(ShipSpec.BeamM), nameof(ShipSpec.DraughtM), nameof(ShipSpec.Complement),
                        "Armor.MainBatteryMm", "Armor.WoodBackingMm", "Armor.HullPlatingMm",
                    },
                },
            };
            spec.Source.EstimatedFields.AddRange(EstimatedGunBallistics);
            return spec;
        }
    }
}
