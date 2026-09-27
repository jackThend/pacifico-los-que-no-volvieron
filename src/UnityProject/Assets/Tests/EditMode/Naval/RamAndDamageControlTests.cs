using System;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    /// <summary>ROADMAP 2.4 — espolonazo y control de averías.</summary>
    public class RamModelTests
    {
        private static RamResult HuascarRamsEsmeralda(float speedKnots, float crossingDeg, float esmeraldaKnots = 0f)
        {
            return RamModel.Resolve(ShipCatalog.Huascar(), 0f, Units.KnotsToMetersPerSecond(speedKnots),
                                    ShipCatalog.Esmeralda(), crossingDeg, Units.KnotsToMetersPerSecond(esmeraldaKnots));
        }

        [Test]
        public void Huascar_AVelocidadCritica_ParteLasCuadernasDeLaEsmeralda()
        {
            var ram = HuascarRamsEsmeralda(10f, 90f);
            Assert.That(ram.Critical, Is.True);
            Assert.That(ram.FramesBroken, Is.True);
            Assert.That(ram.TargetDamage, Is.GreaterThan(0.3f * ShipCatalog.Esmeralda().DisplacementTonnes), "daño masivo");
            Assert.That(ram.TargetFloodingRate, Is.GreaterThan(1f));
            Assert.That(ram.RammerDamage, Is.LessThan(0.05f * ShipCatalog.Huascar().DisplacementTonnes), "el espolón protege al atacante");
        }

        [Test]
        public void PorDebajoDeLaVelocidadCritica_SoloEsUnRoce()
        {
            var ram = HuascarRamsEsmeralda(2f, 90f);
            Assert.That(ram.Critical, Is.False);
            Assert.That(ram.FramesBroken, Is.False);
            Assert.That(ram.TargetFloodingRate, Is.EqualTo(0f));
        }

        [Test]
        public void EmbestidaParalela_NoTieneEfecto()
        {
            var ram = HuascarRamsEsmeralda(10f, 0f, esmeraldaKnots: 3f);
            Assert.That(ram.AngleFactor, Is.LessThan(0.01f));
            Assert.That(ram.Critical, Is.False);
            Assert.That(ram.TargetDamage, Is.LessThan(1f));
        }

        [Test]
        public void ElBlancoQueHuye_ReduceLaVelocidadDeCierre()
        {
            // Esmeralda navegando en el mismo sentido (cruce de 45°) resta velocidad de cierre.
            var fleeing = HuascarRamsEsmeralda(10f, 45f, esmeraldaKnots: 3f);
            var still = HuascarRamsEsmeralda(10f, 45f);
            Assert.That(fleeing.ClosingSpeed, Is.LessThan(still.ClosingSpeed));
        }

        [Test]
        public void CascoDeHierro_ResisteMejorQueElDeMadera()
        {
            var huascar = ShipCatalog.Huascar();
            float v = Units.KnotsToMetersPerSecond(10f);
            var vsWood = RamModel.Resolve(huascar, 0f, v, ShipCatalog.Esmeralda(), 90f, 0f);
            var vsIron = RamModel.Resolve(huascar, 0f, v, ShipCatalog.Independencia(), 90f, 0f);
            Assert.That(vsWood.TargetDamage, Is.EqualTo(vsIron.TargetDamage * RamModel.WoodenHullMultiplier).Within(0.01f));
        }

        [Test]
        public void SinEspolon_ElAtacanteSeDanaMas()
        {
            float v = Units.KnotsToMetersPerSecond(3f);
            var esmeraldaRams = RamModel.Resolve(ShipCatalog.Esmeralda(), 0f, v, ShipCatalog.Huascar(), 90f, 0f);
            Assert.That(esmeraldaRams.RammerDamage / esmeraldaRams.TargetDamage, Is.EqualTo(RamModel.NoRamSelfDamageFraction).Within(1e-4f));
        }

        [Test]
        public void TrasElChoque_ElAtacantePierdeLaArrancada()
        {
            Assert.That(HuascarRamsEsmeralda(10f, 90f).RammerSpeedRetained, Is.LessThan(0.5f));
        }
    }

    public class ShipDamageStateTests
    {
        private static void Run(ShipDamageState state, float seconds, float dt = 0.1f)
        {
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) state.Step(dt);
        }

        private static ArmorImpactResult Penetration(float fireChance = 0f) => new ArmorImpactResult
        {
            Outcome = ImpactOutcome.Penetration,
            StructuralDamage = 10f,
            FireChance = fireChance,
        };

        [Test]
        public void TercerEspolonazo_HundeLaEsmeraldaSinAchique()
        {
            var esmeralda = new ShipDamageState(ShipCatalog.Esmeralda());
            bool sunkEvent = false;
            esmeralda.Sunk += () => sunkEvent = true;

            var ram = RamModel.Resolve(ShipCatalog.Huascar(), 0f, Units.KnotsToMetersPerSecond(10f), ShipCatalog.Esmeralda(), 90f, 0f);
            esmeralda.ApplyRamReceived(ram);
            Assert.That(esmeralda.FramesBroken, Is.True);
            Assert.That(esmeralda.InflowRate, Is.GreaterThan(esmeralda.PumpRate), "las bombas no dan abasto");

            Run(esmeralda, 180f);
            Assert.That(esmeralda.IsSunk, Is.True);
            Assert.That(sunkEvent, Is.True);
            Assert.That(esmeralda.PropulsionFactor, Is.EqualTo(0f));
        }

        [Test]
        public void Achique_TaponaYVaciaUnaViaDeAguaModerada()
        {
            var huascar = new ShipDamageState(ShipCatalog.Huascar());
            huascar.ApplyImpact(Penetration(), ArmorZone.BeltEnds, belowWaterline: true, caliberMm: 229f);
            Run(huascar, 20f);
            float waterBefore = huascar.WaterTonnes;
            Assert.That(waterBefore, Is.GreaterThan(0f));

            Assert.That(huascar.Activate(DamageControlAction.Pumping), Is.True);
            Run(huascar, ShipDamageState.BrigadeActiveSeconds);
            Assert.That(huascar.WaterTonnes, Is.LessThan(waterBefore));
            Assert.That(huascar.InflowRate, Is.LessThan(2.29f));
        }

        [Test]
        public void Incendio_DanaLaEstructura_YLaBrigadaLoApaga()
        {
            var burning = new ShipDamageState(ShipCatalog.Huascar(), seed: 1);
            burning.ApplyImpact(Penetration(fireChance: 1f), ArmorZone.Unarmored, false, 254f);
            Assert.That(burning.OnFire, Is.True);

            float before = burning.StructuralDamage;
            Run(burning, 5f);
            Assert.That(burning.StructuralDamage, Is.GreaterThan(before), "el fuego consume el buque");

            burning.Activate(DamageControlAction.FireFighting);
            Run(burning, ShipDamageState.BrigadeActiveSeconds);
            Assert.That(burning.OnFire, Is.False);
        }

        [Test]
        public void IncendioSinAtender_SeMantieneOCrece()
        {
            var burning = new ShipDamageState(ShipCatalog.Huascar(), seed: 1);
            burning.ApplyImpact(Penetration(1f), ArmorZone.Unarmored, false, 254f);
            burning.ApplyImpact(Penetration(1f), ArmorZone.Unarmored, false, 254f);
            float intensity = burning.FireIntensity;
            Run(burning, 30f);
            Assert.That(burning.FireIntensity, Is.GreaterThanOrEqualTo(intensity));
        }

        [Test]
        public void Calderas_DanadasReducenPotencia_YSeReparanConVapor()
        {
            var state = new ShipDamageState(ShipCatalog.Huascar(), seed: 3);
            for (int i = 0; i < 20 && state.BoilerDamage <= 0f; i++)
            {
                state.ApplyImpact(new ArmorImpactResult { Outcome = ImpactOutcome.Penetration }, ArmorZone.BeltMidships, false, 229f);
            }
            Assert.That(state.BoilerDamage, Is.GreaterThan(0f));
            float damaged = state.PropulsionFactor;
            Assert.That(damaged, Is.LessThan(1f));

            state.Activate(DamageControlAction.SteamRepair);
            Run(state, ShipDamageState.BrigadeActiveSeconds);
            Assert.That(state.PropulsionFactor, Is.GreaterThan(damaged));
        }

        [Test]
        public void Brigada_UnaTareaALaVez_ConEnfriamiento()
        {
            var state = new ShipDamageState(ShipCatalog.Huascar());
            Assert.That(state.Activate(DamageControlAction.Pumping), Is.True);
            Assert.That(state.Activate(DamageControlAction.FireFighting), Is.False, "la brigada está ocupada");

            Run(state, ShipDamageState.BrigadeActiveSeconds + 0.1f);
            Assert.That(state.ActiveAction, Is.Null);
            Assert.That(state.Activate(DamageControlAction.FireFighting), Is.False, "en enfriamiento");

            Run(state, ShipDamageState.BrigadeCooldownSeconds);
            Assert.That(state.Activate(DamageControlAction.FireFighting), Is.True);
        }

        [Test]
        public void Rebotes_NoProvocanIncendiosNiViasDeAgua()
        {
            var state = new ShipDamageState(ShipCatalog.Huascar());
            state.ApplyImpact(new ArmorImpactResult { Outcome = ImpactOutcome.Ricochet, StructuralDamage = 0.5f, FireChance = 1f },
                              ArmorZone.BeltMidships, true, 120f);
            Assert.That(state.OnFire, Is.False);
            Assert.That(state.InflowRate, Is.EqualTo(0f));
        }

        [Test]
        public void Inundacion_ReducePropulsion()
        {
            var state = new ShipDamageState(ShipCatalog.Huascar());
            state.ApplyImpact(Penetration(), ArmorZone.BeltEnds, true, 800f);
            Run(state, 30f);
            Assert.That(state.FloodFraction, Is.GreaterThan(0f));
            Assert.That(state.PropulsionFactor, Is.LessThan(1f));
        }

        [Test]
        public void MismaSemilla_MismoResultado()
        {
            ShipDamageState Simulate()
            {
                var s = new ShipDamageState(ShipCatalog.Huascar(), seed: 42);
                for (int i = 0; i < 10; i++) s.ApplyImpact(Penetration(0.5f), ArmorZone.BeltMidships, i % 2 == 0, 229f);
                Run(s, 60f);
                return s;
            }
            var a = Simulate();
            var b = Simulate();
            Assert.That(a.StructuralDamage, Is.EqualTo(b.StructuralDamage));
            Assert.That(a.BoilerDamage, Is.EqualTo(b.BoilerDamage));
            Assert.That(a.WaterTonnes, Is.EqualTo(b.WaterTonnes));
        }
    }
}
