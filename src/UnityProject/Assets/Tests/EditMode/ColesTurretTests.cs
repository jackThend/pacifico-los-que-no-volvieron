using System;
using NUnit.Framework;
using Pacifico.Core;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;

namespace Pacifico.Tests
{
    public class ColesTurretTests
    {
        private const float Dt = 1f / 50f;
        private static readonly TurretMount Mount = HistoricalShips.HuascarColesTurret;
        private static readonly ShipPose NorthAtOrigin = new ShipPose(0f, 0f, 0f);

        private static void Run(ColesTurretModel turret, ShipPose pose, float seconds)
        {
            for (var t = 0f; t < seconds; t += Dt) turret.Step(Dt, pose);
        }

        /// <summary>Punto a <paramref name="range"/> m de la torre en la marcación relativa dada (buque al norte en el origen).</summary>
        private static (float x, float z) PointAt(float relativeBearing, float range)
        {
            var rad = relativeBearing * Math.PI / 180.0;
            return ((float)(Math.Sin(rad) * range), (float)(Mount.offsetForwardM + Math.Cos(rad) * range));
        }

        [Test]
        public void HuascarDeclaraTorreColesYLosDemasNo()
        {
            Assert.AreSame(Mount, HistoricalShips.Huascar.Turret);
            Assert.IsNull(HistoricalShips.Esmeralda.Turret);
            Assert.IsNull(HistoricalShips.Cochrane.Turret);
            CollectionAssert.IsEmpty(ShipValidator.Validate(HistoricalShips.Huascar));
        }

        [Test]
        public void ValidadorExigeCoherenciaEntreTorreYBaterias()
        {
            var sinMontaje = new ShipSpec("x", "X", Faction.Peru, ShipType.Monitor, HullMaterial.Iron,
                1000f, 50f, 10f, 10f, 100, true, 1000f,
                new[] { new ArmorPlate(ArmorZone.Turret, 5f) },
                new[] { new GunBattery("G", 2, 300f, 10f, 400f, GunMount.Turret, 10f) }, "");
            StringAssert.Contains("no declara el montaje", string.Join("\n", ShipValidator.Validate(sinMontaje)));

            var montajeRoto = new TurretMount(0f, 0f, 10f, 5f, 0f, -1f, -1f, 0f,
                new[] { new FiringArcBlock(0f, 179f, "a"), new FiringArcBlock(180f, 179f, "b") });
            var conMontajeRoto = new ShipSpec("y", "Y", Faction.Peru, ShipType.Monitor, HullMaterial.Iron,
                1000f, 50f, 10f, 10f, 100, true, 1000f,
                new[] { new ArmorPlate(ArmorZone.Turret, 5f) },
                new[] { new GunBattery("G", 2, 300f, 10f, 400f, GunMount.Turret, 10f) }, "", montajeRoto);
            Assert.GreaterOrEqual(ShipValidator.Validate(conMontajeRoto).Count, 7);
        }

        [Test]
        public void AngulosSeNormalizanPorElCaminoCorto()
        {
            Assert.AreEqual(-170f, Angles.Normalize180(190f), 1e-4f);
            Assert.AreEqual(20f, Angles.DeltaDegrees(170f, -170f), 1e-4f);
            Assert.AreEqual(350f, Angles.Normalize360(-10f), 1e-4f);
        }

        [Test]
        public void BalisticaIdaYVuelta()
        {
            foreach (var range in new[] { 300f, 800f, 2000f, 3500f })
            {
                var elevation = NavalBallistics.ElevationForRange(Mount.muzzleVelocityMs, range, Mount.gunHeightM,
                    Mount.minElevationDegrees, Mount.maxElevationDegrees, out var reachable);
                Assert.IsTrue(reachable, range.ToString());
                Assert.AreEqual(range, NavalBallistics.RangeForElevation(Mount.muzzleVelocityMs, elevation, Mount.gunHeightM), 1f);
            }
        }

        [Test]
        public void GiroLimitadoPorLaVelocidadDeLaTorre()
        {
            var turret = new ColesTurretModel(Mount);
            var (x, z) = PointAt(90f, 800f);
            turret.SetTarget(x, z);
            Run(turret, NorthAtOrigin, 1f);
            Assert.AreEqual(Mount.traverseDegreesPerSecond, turret.TrainDegrees, 0.2f);
            Assert.IsFalse(turret.IsOnTarget);

            Run(turret, NorthAtOrigin, 30f);
            Assert.AreEqual(90f, turret.TrainDegrees, 0.1f);
            Assert.IsTrue(turret.CanFire);
        }

        [Test]
        public void GiraPorElCaminoMasCortoCruzandoPopa()
        {
            var turret = new ColesTurretModel(Mount, initialTrainDegrees: 170f);
            var (x, z) = PointAt(-170f, 800f);
            turret.SetTarget(x, z);
            Run(turret, NorthAtOrigin, 1f);
            Assert.That(Math.Abs(turret.TrainDegrees), Is.GreaterThan(170f), "debe cruzar ±180° en vez de dar la vuelta");
            Run(turret, NorthAtOrigin, 10f);
            Assert.AreEqual(-170f, turret.TrainDegrees, 0.1f);
        }

        [Test]
        public void SectoresEnmascaradosImpidenDisparar()
        {
            foreach (var (bearing, reason) in new[] { (0f, "Castillo de proa"), (180f, "Chimenea, palo y puente"), (-160f, "Chimenea, palo y puente") })
            {
                var turret = new ColesTurretModel(Mount, initialTrainDegrees: bearing);
                var (x, z) = PointAt(bearing, 800f);
                turret.SetTarget(x, z);
                Run(turret, NorthAtOrigin, 20f);
                Assert.IsTrue(turret.IsOnTarget, $"{bearing}: la torre sí apunta");
                Assert.IsTrue(turret.IsMasked, $"{bearing}: pero la superestructura lo impide");
                Assert.AreEqual(reason, turret.MaskingSector.reason);
                Assert.IsFalse(turret.CanFire);
            }

            var beam = new ColesTurretModel(Mount, initialTrainDegrees: 45f);
            var (bx, bz) = PointAt(45f, 800f);
            beam.SetTarget(bx, bz);
            Run(beam, NorthAtOrigin, 20f);
            Assert.IsFalse(beam.IsMasked);
            Assert.IsTrue(beam.CanFire);
        }

        [Test]
        public void ElevacionLimitadaYFueraDeAlcance()
        {
            var turret = new ColesTurretModel(Mount, initialTrainDegrees: 90f);
            var tooFar = turret.MaxRangeM + 500f;
            var (x, z) = PointAt(90f, tooFar);
            turret.SetTarget(x, z);
            Run(turret, NorthAtOrigin, 30f);
            Assert.AreEqual(Mount.maxElevationDegrees, turret.ElevationDegrees, 1e-3f);
            Assert.IsFalse(turret.TargetInRange);
            Assert.IsFalse(turret.CanFire);
        }

        [Test]
        public void ElevacionConVelocidadLimitada()
        {
            var turret = new ColesTurretModel(Mount, initialTrainDegrees: 90f);
            var (x, z) = PointAt(90f, turret.MaxRangeM - 10f);
            turret.SetTarget(x, z);
            Run(turret, NorthAtOrigin, 1f);
            Assert.AreEqual(Mount.elevationDegreesPerSecond, turret.ElevationDegrees, 0.05f);
        }

        [Test]
        public void TorreCompensaElGiroDelCasco()
        {
            var turret = new ColesTurretModel(Mount);
            var target = PointAt(90f, 1000f);
            turret.SetTarget(target.x, target.z);
            Run(turret, NorthAtOrigin, 30f);
            Assert.IsTrue(turret.CanFire);

            // El buque cae 20° a estribor sin moverse: la torre debe girar -20° respecto a la proa.
            var turned = new ShipPose(0f, 0f, 20f);
            Run(turret, turned, 10f);
            Assert.AreEqual(70f, turret.TrainDegrees, 1.5f, "la marcación relativa compensa el rumbo");
            Assert.IsTrue(turret.IsOnTarget);
        }

        [Test]
        public void ReticulaConvergeSobreElBlancoAlAsentarse()
        {
            var turret = new ColesTurretModel(Mount, initialTrainDegrees: 60f);
            var (x, z) = PointAt(100f, 900f);
            turret.SetTarget(x, z);
            Run(turret, NorthAtOrigin, 0.5f);
            var spreadWhileSlewing = turret.LeftImpact.DistanceTo(turret.RightImpact);
            var errorWhileSlewing = turret.AimPoint.DistanceTo(turret.Target);

            Run(turret, NorthAtOrigin, 30f);
            Assert.IsTrue(turret.CanFire);
            Assert.Less(turret.LeftImpact.DistanceTo(turret.RightImpact), 0.5f, "ambos cañones convergen");
            Assert.Less(turret.AimPoint.DistanceTo(turret.Target), 3f, "la retícula cae sobre el blanco");
            Assert.Less(turret.LeftImpact.DistanceTo(turret.Target), 3f);
            Assert.Greater(spreadWhileSlewing, turret.LeftImpact.DistanceTo(turret.RightImpact));
            Assert.Greater(errorWhileSlewing, 100f, "mientras gira la retícula está lejos del blanco");
        }

        [Test]
        public void SinBlancoLaTorreQuedaQuieta()
        {
            var turret = new ColesTurretModel(Mount, initialTrainDegrees: 30f);
            Run(turret, NorthAtOrigin, 5f);
            Assert.AreEqual(30f, turret.TrainDegrees, 1e-4f);
            Assert.IsFalse(turret.CanFire);

            var (x, z) = PointAt(90f, 800f);
            turret.SetTarget(x, z);
            turret.ClearTarget();
            Run(turret, NorthAtOrigin, 5f);
            Assert.AreEqual(30f, turret.TrainDegrees, 1e-4f);
        }

        [Test]
        public void MontajeNuloSeRechaza()
        {
            Assert.Throws<ArgumentNullException>(() => new ColesTurretModel(null));
        }
    }
}
