using System.Collections.Generic;
using System.Linq;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Catálogo canónico del armamento de 1879–1881 (GDD §3.1). Es la fuente de verdad a partir de la cual
    /// el editor genera los <c>WeaponDataSO</c>; cada llamada devuelve copias nuevas para que nadie
    /// modifique el catálogo por accidente.
    /// </summary>
    public static class WeaponCatalog
    {
        public const string ComblainId = "comblain_ii_1871";
        public const string ChassepotId = "chassepot_1866";
        public const string GrasId = "gras_1874";
        public const string RemingtonId = "remington_rolling_block";
        public const string WinchesterId = "winchester_1873_carbine";
        public const string CorvoId = "corvo";
        public const string TriangularBayonetId = "bayoneta_triangular";

        // Bayoneta calada: alcance del fusil más la hoja. Común a los fusiles de infantería.
        private const float BayonetReachM = 1.9f;
        private const float BayonetDamage = 90f;
        private const float BayonetCycleSeconds = 0.9f;

        public static IReadOnlyList<WeaponSpec> All()
        {
            return new[]
            {
                Comblain(), Chassepot(), Gras(), Remington(), Winchester(), Corvo(), TriangularBayonet(),
            };
        }

        public static WeaponSpec Get(string id)
        {
            return All().FirstOrDefault(w => w.Id == id);
        }

        /// <summary>Fusil Comblain II (Chile). GDD: «daño contundente a media distancia», recarga 2,0 s.</summary>
        public static WeaponSpec Comblain()
        {
            return new WeaponSpec
            {
                Id = ComblainId,
                DisplayName = "Fusil Comblain II Mod. 1871",
                Kind = WeaponKind.Rifle,
                Action = FiringAction.FallingBlock,
                Feed = AmmoFeed.SingleShot,
                UsedBy = { Faction.Chile },
                YearAdopted = 1874,
                Cartridge = "11×50R Comblain",
                CaliberMm = 11f,
                MuzzleVelocityMps = 440f,
                BulletMassG = 24.7f,
                PowderChargeG = 4.9f,
                MassKg = 4.3f,
                MaxSightRangeM = 1300f,
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Greve Moller, P. «Fusil Chileno Comblain II Modelo 1871, Calibre 11x50 R mm».",
                        "The Black Powder Cartridge News / IAA: cartucho 11×50R Comblain con bala de 386 gr y 76 gr (≈4,9 g) de pólvora negra.",
                        "Wikipedia (en): «M1870 Belgian Comblain» — masa 4,3 kg sin bayoneta, alcance máximo 1.300 m.",
                    },
                    EstimatedFields = { nameof(WeaponSpec.MuzzleVelocityMps), nameof(WeaponSpec.BulletMassG), nameof(WeaponSpec.YearAdopted) },
                    Notes = "Velocidad y masa de bala tomadas de la munición belga 11×51R (proyectil 24,7 g, ~440 m/s). " +
                            "Arma reglamentaria de la infantería chilena (foto 11 del Archivo Histórico).",
                },
                ReloadSeconds = 2.0f,
                MagazineCapacity = 1,
                BaseDamage = 70f,
                DispersionMoa = 4.0f,
                EffectiveRangeM = 350f,
                MeleeReachM = BayonetReachM,
                MeleeDamage = BayonetDamage,
                MeleeCycleSeconds = BayonetCycleSeconds,
            };
        }

        /// <summary>Fusil Chassepot 1866 (Perú). GDD: cerrojo de 11 mm, «gran precisión», recarga 2,2 s.</summary>
        public static WeaponSpec Chassepot()
        {
            return new WeaponSpec
            {
                Id = ChassepotId,
                DisplayName = "Fusil Chassepot Mod. 1866",
                Kind = WeaponKind.Rifle,
                Action = FiringAction.BoltAction,
                Feed = AmmoFeed.SingleShot,
                UsedBy = { Faction.Peru },
                YearAdopted = 1866,
                Cartridge = "11 mm Chassepot (cartucho combustible de papel)",
                CaliberMm = 11f,
                MuzzleVelocityMps = 410f,
                BulletMassG = 25f,
                PowderChargeG = 5.6f,
                Case = CartridgeCase.Combustible,
                MassKg = 4.635f,
                MaxSightRangeM = 1200f,
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (en): «Chassepot» — velocidad en boca 410 m/s, bala de plomo de 25 g con 5,6 g de pólvora negra en cartucho de papel, masa 4,635 kg, alcance máximo 1.200 m.",
                        "Archivo Histórico: 04_Uniformes_y_Armamento/01 y 02 (planos de cerrojo y cañón).",
                    },
                    Notes = "Aguja percutora sobre cartucho de papel; el juego abstrae el ciclo como recarga de 2,2 s.",
                },
                ReloadSeconds = 2.2f,
                MagazineCapacity = 1,
                BaseDamage = 60f,
                DispersionMoa = 2.5f,
                EffectiveRangeM = 450f,
                MeleeReachM = BayonetReachM,
                MeleeDamage = BayonetDamage,
                MeleeCycleSeconds = BayonetCycleSeconds,
            };
        }

        /// <summary>Fusil Gras 1874 (Perú). GDD: cerrojo de 11 mm, «gran precisión», recarga 2,2 s.</summary>
        public static WeaponSpec Gras()
        {
            return new WeaponSpec
            {
                Id = GrasId,
                DisplayName = "Fusil Gras Mod. 1874",
                Kind = WeaponKind.Rifle,
                Action = FiringAction.BoltAction,
                Feed = AmmoFeed.SingleShot,
                UsedBy = { Faction.Peru },
                YearAdopted = 1874,
                Cartridge = "11×59R Gras (vaina metálica)",
                CaliberMm = 11f,
                MuzzleVelocityMps = 450f,
                BulletMassG = 25f,
                PowderChargeG = 5.2f,
                MassKg = 4.2f,
                MaxSightRangeM = 1800f,
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (en): «Fusil Gras mle 1874» — velocidad en boca 450 m/s, masa 4,2 kg, cañón 795 mm.",
                        "Wikipedia (en): «11×59mmR Gras» — bala de plomo encamisada en papel de 25,0 g con 5,2 g de pólvora negra F1.",
                        "Archivo Histórico: 04_Uniformes_y_Armamento/03 y 04 (cerrojo y cartucho metálico).",
                    },
                    EstimatedFields = { nameof(WeaponSpec.MaxSightRangeM) },
                },
                ReloadSeconds = 2.2f,
                MagazineCapacity = 1,
                BaseDamage = 62f,
                DispersionMoa = 2.5f,
                EffectiveRangeM = 500f,
                MeleeReachM = BayonetReachM,
                MeleeDamage = BayonetDamage,
                MeleeCycleSeconds = BayonetCycleSeconds,
            };
        }

        /// <summary>Remington Rolling Block (Bolivia/Perú). GDD: «potencia de parada brutal», recarga 2,1 s.</summary>
        public static WeaponSpec Remington()
        {
            return new WeaponSpec
            {
                Id = RemingtonId,
                DisplayName = "Fusil Remington Rolling Block",
                Kind = WeaponKind.Rifle,
                Action = FiringAction.RollingBlock,
                Feed = AmmoFeed.SingleShot,
                UsedBy = { Faction.Bolivia, Faction.Peru },
                YearAdopted = 1867,
                Cartridge = ".43 Spanish (11,15×58R)",
                CaliberMm = 11.15f,
                MuzzleVelocityMps = 389f,
                BulletMassG = 25.7f,
                PowderChargeG = 5.0f,
                MassKg = 4.2f,
                MaxSightRangeM = 1000f,
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Wikipedia (en): «.43 Spanish» — bala de 396 gr y 77 gr de pólvora negra, ~1.275 ft/s (≈389 m/s).",
                        "Archivo Histórico: 04_Uniformes_y_Armamento/05 y 06.",
                    },
                    EstimatedFields = { nameof(WeaponSpec.MassKg), nameof(WeaponSpec.MaxSightRangeM) },
                    Notes = "Los ejércitos aliados usaron varios calibres Remington; se toma el .43 Spanish como referencia.",
                },
                ReloadSeconds = 2.1f,
                MagazineCapacity = 1,
                BaseDamage = 80f,
                DispersionMoa = 4.5f,
                EffectiveRangeM = 300f,
                MeleeReachM = BayonetReachM,
                MeleeDamage = BayonetDamage,
                MeleeCycleSeconds = BayonetCycleSeconds,
            };
        }

        /// <summary>Carabina Winchester 1873 — arma de Quintín Quintana en el Capítulo 7 (GDD §5).</summary>
        public static WeaponSpec Winchester()
        {
            return new WeaponSpec
            {
                Id = WinchesterId,
                DisplayName = "Carabina Winchester Mod. 1873",
                Kind = WeaponKind.Carbine,
                Action = FiringAction.LeverAction,
                Feed = AmmoFeed.TubeMagazine,
                UsedBy = { Faction.Chile },
                YearAdopted = 1873,
                Cartridge = ".44-40 WCF",
                CaliberMm = 10.8f,
                MuzzleVelocityMps = 379f,
                BulletMassG = 13f,
                PowderChargeG = 2.6f,
                MassKg = 3.3f,
                MaxSightRangeM = 500f,
                Source = new HistoricalSource
                {
                    References =
                    {
                        "Especificación comercial Winchester Model 1873 (carabina, depósito tubular de 12 cartuchos).",
                        "Wikipedia (en): «.44-40 Winchester» — bala de 200 gr (13 g) a ~1.245 ft/s (379 m/s) con pólvora negra.",
                    },
                    EstimatedFields = { nameof(WeaponSpec.MassKg), nameof(WeaponSpec.MaxSightRangeM) },
                    Notes = "Carabina ligera citada en el GDD para la Compañía Vulcano. Recarga por cartucho.",
                },
                ReloadSeconds = 0.45f,
                MagazineCapacity = 12,
                BaseDamage = 45f,
                DispersionMoa = 6f,
                EffectiveRangeM = 150f,
            };
        }

        /// <summary>Corvo chileno: cuchillo curvo de combate cuerpo a cuerpo.</summary>
        public static WeaponSpec Corvo()
        {
            return new WeaponSpec
            {
                Id = CorvoId,
                DisplayName = "Corvo chileno",
                Kind = WeaponKind.Blade,
                UsedBy = { Faction.Chile },
                YearAdopted = 1879,
                MassKg = 0.4f,
                Source = new HistoricalSource
                {
                    References = { "GDD §3.1 — «el emblemático Corvo chileno» para remates cuerpo a cuerpo." },
                    EstimatedFields = { nameof(WeaponSpec.MassKg), nameof(WeaponSpec.YearAdopted) },
                    Notes = "Herramienta campesina de hoja curva, llevada por iniciativa de los soldados, no de dotación.",
                },
                MeleeReachM = 1.1f,
                MeleeDamage = 85f,
                MeleeCycleSeconds = 0.55f,
            };
        }

        /// <summary>Bayoneta triangular aliada, calada en el fusil (el alcance incluye el arma).</summary>
        public static WeaponSpec TriangularBayonet()
        {
            return new WeaponSpec
            {
                Id = TriangularBayonetId,
                DisplayName = "Bayoneta triangular",
                Kind = WeaponKind.Blade,
                UsedBy = { Faction.Peru, Faction.Bolivia },
                YearAdopted = 1866,
                MassKg = 0.35f,
                Source = new HistoricalSource
                {
                    References = { "GDD §3.1 — «Bayoneta triangular aliada para remates cuerpo a cuerpo»." },
                    EstimatedFields = { nameof(WeaponSpec.MassKg), nameof(WeaponSpec.YearAdopted) },
                },
                MeleeReachM = BayonetReachM,
                MeleeDamage = BayonetDamage,
                MeleeCycleSeconds = BayonetCycleSeconds,
            };
        }
    }
}
