using System;
using NUnit.Framework;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;

namespace Pacifico.Tests
{
    public class RamTests
    {
        private static readonly ShipSpec Huascar = HistoricalShips.Huascar;
        private static readonly ShipSpec Esmeralda = HistoricalShips.Esmeralda;
        private static float Knots(float k) => k * ShipHandling.KnotsToMetersPerSecond;

        private static RamReport RamEsmeralda(float knots, float rammerHeading, HullFrames frames, float contact = 0f) =>
            RamImpact.Resolve(Huascar, Knots(knots), rammerHeading, Esmeralda, 0f, 0f, frames, contact);

        // ROADMAP 2.4 – verificación: el Huáscar embiste y parte cuadernas simuladas.
        [Test]
        public void HuascarEmbisteYParteCuadernasDeLaEsmeralda()
        {
            var frames = HullFrames.ForShip(Esmeralda);
            var report = RamEsmeralda(6f, 90f, frames, contact: 5f);

            Assert.IsTrue(report.IsCritical);
            Assert.AreEqual(90f, report.ImpactAngleDegrees, 1e-3f);
            Assert.GreaterOrEqual(report.FramesBroken, 5);
            Assert.AreEqual(report.FramesBroken, frames.BrokenCount);
            Assert.IsTrue(frames.IsBroken(frames.IndexAt(5f)), "se parte la cuaderna del punto de impacto");
            Assert.IsFalse(frames.IsBroken(0), "la popa queda intacta");
            Assert.Greater(report.FloodingTonsPerMinute, 60f);
            Assert.Greater(report.TargetDamage, 300f);
            Assert.Less(report.RammerDamage, report.TargetDamage * 0.1f, "el espolón protege al Huáscar");
        }

        [Test]
        public void ADobleVelocidadPracticamenteCuadruplicaLaEnergia()
        {
            var slow = RamEsmeralda(5f, 90f, HullFrames.ForShip(Esmeralda));
            var fast = RamEsmeralda(10f, 90f, HullFrames.ForShip(Esmeralda));
            Assert.AreEqual(4f, fast.EnergyMJ / slow.EnergyMJ, 1e-3f);
            Assert.Greater(fast.FramesBroken, slow.FramesBroken * 3);
        }

        [Test]
        public void PorDebajoDeLaVelocidadCriticaSoloRoza()
        {
            var frames = HullFrames.ForShip(Esmeralda);
            var report = RamEsmeralda(3f, 90f, frames);
            Assert.IsFalse(report.IsCritical);
            Assert.AreEqual(0, report.FramesBroken);
            Assert.AreEqual(0, frames.BrokenCount);
            Assert.AreEqual(0f, report.FloodingTonsPerMinute);
        }

        [Test]
        public void EmbestidaOblicuaRompeMenosQuePerpendicular()
        {
            var perpendicular = RamEsmeralda(8f, 90f, HullFrames.ForShip(Esmeralda));
            var glancing = RamEsmeralda(8f, 20f, HullFrames.ForShip(Esmeralda));
            Assert.AreEqual(20f, glancing.ImpactAngleDegrees, 1e-3f);
            Assert.Less(glancing.FramesBroken, perpendicular.FramesBroken / 2);
        }

        [Test]
        public void VelocidadDeCierreDescuentaLaDelBlanco()
        {
            // Blanco que huye en el mismo rumbo: el cierre es la diferencia de velocidades.
            var chase = RamImpact.Resolve(Huascar, Knots(11f), 0f, Esmeralda, Knots(4f), 0f,
                HullFrames.ForShip(Esmeralda), 0f);
            Assert.AreEqual(7f, chase.ClosingSpeedKnots, 1e-2f);
            // Blanco que viene de frente: se suman.
            var headOn = RamImpact.Resolve(Huascar, Knots(6f), 0f, Esmeralda, Knots(4f), 180f,
                HullFrames.ForShip(Esmeralda), 0f);
            Assert.AreEqual(10f, headOn.ClosingSpeedKnots, 1e-2f);
        }

        [Test]
        public void CascoDeHierroResisteMasQueElDeMadera()
        {
            var wood = RamEsmeralda(10f, 90f, HullFrames.ForShip(Esmeralda));
            var iron = RamImpact.Resolve(Huascar, Knots(10f), 90f, HistoricalShips.Independencia, 0f, 0f,
                HullFrames.ForShip(HistoricalShips.Independencia), 0f);
            Assert.Less(iron.FramesBroken, wood.FramesBroken);
        }

        [Test]
        public void SinEspolonElAtacanteSufreMas()
        {
            var withRam = RamImpact.Resolve(Huascar, Knots(8f), 90f, HistoricalShips.Independencia, 0f, 0f,
                HullFrames.ForShip(HistoricalShips.Independencia), 0f);
            var noRamSpec = new ShipSpec("sin_espolon", "Sin espolón", Huascar.Faction, Huascar.Type, Huascar.Hull,
                Huascar.DisplacementTons, Huascar.LengthM, Huascar.BeamM, Huascar.MaxSpeedKnots, Huascar.Crew,
                false, Huascar.HullIntegrity, Huascar.Armor, Huascar.Guns, "", Huascar.Turret);
            var withoutRam = RamImpact.Resolve(noRamSpec, Knots(8f), 90f, HistoricalShips.Independencia, 0f, 0f,
                HullFrames.ForShip(HistoricalShips.Independencia), 0f);

            Assert.Less(withoutRam.FramesBroken, withRam.FramesBroken);
            Assert.Greater(withoutRam.RammerDamage / withoutRam.TargetDamage, withRam.RammerDamage / withRam.TargetDamage);
        }

        [Test]
        public void AtacanteConservaVelocidadSegunMasas()
        {
            var report = RamEsmeralda(8f, 90f, HullFrames.ForShip(Esmeralda));
            Assert.AreEqual(1130f / (1130f + 850f), report.RammerSpeedKept, 1e-4f);

            var motion = new ShipMotionModel(ShipHandling.FromSpec(Huascar));
            motion.SetOrder(EngineOrder.Full);
            for (var i = 0; i < 30000; i++) motion.Step(0.02f);
            var before = motion.SpeedMs;
            motion.ApplySpeedLoss(report.RammerSpeedKept);
            Assert.AreEqual(before * report.RammerSpeedKept, motion.SpeedMs, 1e-4f);
        }

        [Test]
        public void TresEspolonazosLentosHundenLaEsmeraldaSinAchique()
        {
            // Iquique: tres embestidas. Sin cuadrilla de achique, la vía de agua la hunde.
            var frames = HullFrames.ForShip(Esmeralda);
            var hull = new HullIntegrity(Esmeralda.HullIntegrity);
            var control = new DamageControlSystem(Esmeralda, hull);
            foreach (var contact in new[] { -10f, 0f, 10f })
            {
                var ram = RamEsmeralda(5f, 90f, frames, contact);
                Assert.IsTrue(ram.IsCritical);
                hull.ApplyDamage(ram.TargetDamage);
                control.ApplyRam(ram);
                for (var t = 0f; t < 30f; t += 0.1f) control.Step(0.1f);
            }
            for (var t = 0f; t < 600f && !control.IsFoundered; t += 0.1f) control.Step(0.1f);
            Assert.IsTrue(control.IsFoundered);
            Assert.IsTrue(hull.IsSunk);
        }

        [Test]
        public void CuadernasSeParteEnAlrededorDelImpactoSinRepetir()
        {
            var frames = new HullFrames(HullMaterial.Wood, 15f);
            Assert.AreEqual(10, frames.Count);
            Assert.AreEqual(0, frames.IndexAt(-100f));
            Assert.AreEqual(9, frames.IndexAt(100f));
            Assert.AreEqual(3, frames.BreakAround(5, 3));
            Assert.IsTrue(frames.IsBroken(4) && frames.IsBroken(5) && frames.IsBroken(6));
            Assert.AreEqual(2, frames.BreakAround(5, 2), "salta las ya rotas");
            Assert.IsTrue(frames.IsBroken(3) && frames.IsBroken(7));
            Assert.AreEqual(5, frames.BreakAround(0, 50), "no puede romper más de las que quedan");
            Assert.AreEqual(10, frames.BrokenCount);
        }

        [Test]
        public void DetectorDeContactoPorLaRoda()
        {
            // Esmeralda al norte, rumbo este; el Huáscar llega desde el sur con rumbo norte.
            var target = new ShipPose(0f, 100f, 90f);
            var bowShort = Huascar.LengthM * 0.5f;
            Assert.IsFalse(RamDetector.TryDetect(new ShipPose(0f, 100f - 30f - bowShort, 0f), Huascar, target, Esmeralda, out _));
            Assert.IsTrue(RamDetector.TryDetect(new ShipPose(8f, 100f - 2f - bowShort, 0f), Huascar, target, Esmeralda, out var contact));
            Assert.AreEqual(8f, contact, 0.01f, "contacto 8 m a proa del centro de la Esmeralda");
        }
    }

    public class DamageControlTests
    {
        private static DamageControlSystem NewControl(out HullIntegrity hull, ShipSpec spec = null)
        {
            spec = spec ?? HistoricalShips.Huascar;
            hull = new HullIntegrity(spec.HullIntegrity);
            return new DamageControlSystem(spec, hull);
        }

        private static void Run(DamageControlSystem control, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.1f) control.Step(0.1f);
        }

        [Test]
        public void IncendioCreceYDanaElCascoSiNadieLoApaga()
        {
            var control = NewControl(out var hull);
            control.StartFire(0.3f);
            Run(control, 30f);
            Assert.Greater(control.FireIntensity, 0.55f);
            Assert.Less(hull.Current, hull.Max);
        }

        [Test]
        public void CuadrillaApagaElIncendio()
        {
            var control = NewControl(out _);
            control.StartFire(0.8f);
            control.ToggleTask(DamageControlTask.Firefighting);
            Run(control, 15f);
            Assert.IsFalse(control.OnFire);
        }

        [Test]
        public void IncendioIntensoAveriaLasCalderas()
        {
            var control = NewControl(out _);
            control.StartFire(1f);
            Run(control, 20f);
            Assert.Less(control.BoilerHealth, 1f);
        }

        [Test]
        public void AchiqueYApuntalamientoContienenLaInundacion()
        {
            var sinPumping = NewControl(out _, HistoricalShips.Esmeralda);
            var withPumping = NewControl(out _, HistoricalShips.Esmeralda);
            foreach (var c in new[] { sinPumping, withPumping }) c.AddBreach(90f);
            withPumping.AssignTask(DamageControlTask.Pumping);

            Run(sinPumping, 300f);
            Run(withPumping, 300f);
            Assert.IsTrue(sinPumping.IsFoundered, "sin achique se va a pique");
            Assert.IsFalse(withPumping.IsFoundered);
            Assert.AreEqual(0f, withPumping.InflowTonsPerMinute, 1e-3f, "vía de agua apuntalada");
            Assert.AreEqual(0f, withPumping.WaterTons, 1e-3f, "agua achicada");
        }

        [Test]
        public void ReparacionDeCalderasRestauraPotencia()
        {
            var control = NewControl(out _);
            control.DamageBoilers(0.6f);
            Assert.AreEqual(0.4f, control.PowerLimit, 1e-4f);
            control.AssignTask(DamageControlTask.BoilerRepair);
            Run(control, 30f);
            Assert.AreEqual(1f, control.BoilerHealth, 1e-4f);
        }

        [Test]
        public void InundacionReducePotencia()
        {
            var control = NewControl(out _);
            control.AddBreach(60f);
            Run(control, 60f);
            Assert.Greater(control.FloodFraction, 0f);
            Assert.Less(control.PowerLimit, 1f);
        }

        [Test]
        public void LimiteDePotenciaFrenaAlBuque()
        {
            var motion = new ShipMotionModel(ShipHandling.FromSpec(HistoricalShips.Huascar));
            motion.SetOrder(EngineOrder.Full);
            motion.SetPowerLimit(0.5f);
            for (var i = 0; i < 30000; i++) motion.Step(0.02f);
            Assert.AreEqual(0.5f * HistoricalShips.Huascar.MaxSpeedKnots, motion.SpeedKnots, 0.05f);
        }

        [Test]
        public void TeclaDeLaTareaActivaLaDesasigna()
        {
            var control = NewControl(out _);
            control.ToggleTask(DamageControlTask.Pumping);
            Assert.AreEqual(DamageControlTask.Pumping, control.Task);
            control.ToggleTask(DamageControlTask.BoilerRepair);
            Assert.AreEqual(DamageControlTask.BoilerRepair, control.Task);
            control.ToggleTask(DamageControlTask.BoilerRepair);
            Assert.AreEqual(DamageControlTask.None, control.Task);
        }

        [Test]
        public void ImpactosDeArtilleriaProvocanAverias()
        {
            var critical = ArmorImpact.Resolve(HistoricalShips.Huascar.Guns[0], 500f, 0f,
                HistoricalShips.Esmeralda, ArmorZone.BeltMidship);
            var ricochet = ArmorImpact.Resolve(HistoricalShips.Esmeralda.Guns[0], 500f, 0f,
                HistoricalShips.Huascar, ArmorZone.BeltMidship);

            var unlucky = NewControl(out _, HistoricalShips.Esmeralda);
            unlucky.ApplyShellImpact(critical, () => 0.0);
            Assert.IsTrue(unlucky.OnFire);
            Assert.Less(unlucky.BoilerHealth, 1f);
            Assert.Greater(unlucky.InflowTonsPerMinute, 0f);

            var lucky = NewControl(out _, HistoricalShips.Esmeralda);
            lucky.ApplyShellImpact(critical, () => 0.99);
            Assert.IsFalse(lucky.OnFire);
            Assert.AreEqual(1f, lucky.BoilerHealth);

            var armored = NewControl(out _);
            armored.ApplyShellImpact(ricochet, () => 0.0);
            Assert.IsFalse(armored.OnFire, "un rebote no provoca averías");
            Assert.AreEqual(0f, armored.InflowTonsPerMinute);
        }

        [Test]
        public void IaPriorizaInundacionLuegoFuegoLuegoCalderas()
        {
            var control = NewControl(out _);
            Assert.AreEqual(DamageControlTask.None, control.SuggestTask());
            control.DamageBoilers(0.2f);
            Assert.AreEqual(DamageControlTask.BoilerRepair, control.SuggestTask());
            control.StartFire(0.2f);
            Assert.AreEqual(DamageControlTask.Firefighting, control.SuggestTask());
            control.AddBreach(5f);
            Assert.AreEqual(DamageControlTask.Pumping, control.SuggestTask());
        }

        [Test]
        public void HundimientoPorInundacionAvisaUnaVez()
        {
            var control = NewControl(out var hull, HistoricalShips.Esmeralda);
            var foundered = 0;
            control.Foundered += () => foundered++;
            control.AddBreach(600f);
            Run(control, 120f);
            Assert.AreEqual(1, foundered);
            Assert.IsTrue(hull.IsSunk);
        }
    }
}
