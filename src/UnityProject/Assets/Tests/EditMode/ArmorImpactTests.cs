using System;
using NUnit.Framework;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;

namespace Pacifico.Tests
{
    public class ArmorImpactTests
    {
        private static readonly GunBattery Armstrong300 = HistoricalShips.Huascar.Guns[0];
        private static readonly GunBattery Gun40 = HistoricalShips.Esmeralda.Guns[0];
        private const float GunHeight = 3f;

        /// <summary>Blanco al norte del tirador, a la distancia dada, con el rumbo indicado.</summary>
        private static ShipPose TargetAt(float range, float heading) => new ShipPose(0f, range, heading);
        private static readonly SeaPoint Shooter = new SeaPoint(0f, 0f);

        [Test]
        public void PenetracionCalibradaConLasAnclasHistoricas()
        {
            Assert.AreEqual(12.2f, ArmorImpact.PenetrationInches(300f, 10f, 400f), 0.5f);
            Assert.AreEqual(3.5f, ArmorImpact.PenetrationInches(40f, 4.75f, 360f), 0.3f);
            Assert.Less(ArmorImpact.PenetrationInches(40f, 4.75f, 360f), HistoricalShips.Huascar.ArmorInches(ArmorZone.BeltMidship),
                "ni a quemarropa el 40 lb perfora el cinturón del Huáscar");
        }

        [Test]
        public void VelocidadRemanenteCaeConLaDistanciaYMenosEnCalibresGrandes()
        {
            Assert.AreEqual(400f, ArmorImpact.ImpactVelocity(400f, 10f, 0f), 1e-3f);
            var big = ArmorImpact.ImpactVelocity(400f, 10f, 2000f) / 400f;
            var small = ArmorImpact.ImpactVelocity(400f, 4.75f, 2000f) / 400f;
            Assert.Less(big, 1f);
            Assert.Greater(big, small);
        }

        // ROADMAP 2.3 – verificación: 40 lb rebota en el Huáscar, 300 lb perfora la Esmeralda.
        [TestCase(200f)]
        [TestCase(500f)]
        [TestCase(1000f)]
        public void ProyectilDe40LibrasRebotaEnElHuascar(float range)
        {
            foreach (var zone in new[] { ArmorZone.BeltMidship, ArmorZone.Turret })
            {
                var report = ArmorImpact.ResolveShot(Gun40, GunHeight, Shooter, TargetAt(range, 90f),
                    HistoricalShips.Huascar, zone);
                Assert.AreEqual(ImpactResult.Ricochet, report.Result, $"{zone} a {range} m");
                Assert.Less(report.Damage, 5f, "rebote: daño mínimo");
                Assert.Less(report.IncidenceDegrees, 5f, "incluso de frente");
            }
        }

        [TestCase(200f, 0f)]
        [TestCase(1000f, 60f)]
        [TestCase(2000f, 80f)]
        public void ProyectilDe300LibrasPerforaLaEsmeralda(float range, float targetHeading)
        {
            var report = ArmorImpact.ResolveShot(Armstrong300, GunHeight, Shooter, TargetAt(range, targetHeading),
                HistoricalShips.Esmeralda, ArmorZone.BeltMidship);
            Assert.AreEqual(ImpactResult.CriticalPenetration, report.Result);
            Assert.AreEqual(600f, report.Damage, 1e-3f);
        }

        [Test]
        public void MaderaSufreDanoCriticoInclusoCon40Libras()
        {
            var report = ArmorImpact.ResolveShot(Gun40, GunHeight, Shooter, TargetAt(800f, 45f),
                HistoricalShips.Esmeralda, ArmorZone.BeltMidship);
            Assert.AreEqual(ImpactResult.CriticalPenetration, report.Result);
            Assert.AreEqual(80f, report.Damage, 1e-3f);
        }

        [Test]
        public void AnguloDecideEntrePerforarYRebotarContraElCochrane()
        {
            // De través (0°) el 300 lb perfora los 9" del Cochrane a 1000 m; presentando
            // el casco a 30–45° (táctica de Angamos) el espesor efectivo lo detiene.
            var broadside = ArmorImpact.ResolveShot(Armstrong300, GunHeight, Shooter, TargetAt(1000f, 90f),
                HistoricalShips.Cochrane, ArmorZone.BeltMidship);
            Assert.AreEqual(ImpactResult.CriticalPenetration, broadside.Result);

            foreach (var heading in new[] { 60f, 45f })
            {
                var angled = ArmorImpact.ResolveShot(Armstrong300, GunHeight, Shooter, TargetAt(1000f, heading),
                    HistoricalShips.Cochrane, ArmorZone.BeltMidship);
                Assert.AreEqual(ImpactResult.Ricochet, angled.Result, $"rumbo {heading}");
                Assert.Greater(angled.EffectiveArmorInches, angled.ArmorInches);
            }
        }

        [Test]
        public void PerforacionOblicuaNoEsCritica()
        {
            var report = ArmorImpact.Resolve(Armstrong300, 500f, 40f, HistoricalShips.Independencia, ArmorZone.BeltMidship);
            Assert.AreEqual(ImpactResult.Penetration, report.Result);
            Assert.AreEqual(300f, report.Damage, 1e-3f);
        }

        [Test]
        public void MasAllaDelAnguloLimiteSiempreRebota()
        {
            var report = ArmorImpact.Resolve(Armstrong300, 0f, 65f, HistoricalShips.Independencia, ArmorZone.BeltMidship);
            Assert.AreEqual(ImpactResult.Ricochet, report.Result);
            Assert.Greater(report.PenetrationInches, report.EffectiveArmorInches, "rebota aunque tendría energía de sobra");
        }

        [Test]
        public void ZonaSinCorazaEnBuqueDeHierroUsaChapaFina()
        {
            var report = ArmorImpact.Resolve(Gun40, 500f, 10f, HistoricalShips.Cochrane, ArmorZone.Deck);
            Assert.AreEqual(ArmorImpact.UnarmoredIronPlateInches, report.ArmorInches, 1e-4f);
            Assert.IsTrue(report.Penetrated);
        }

        [Test]
        public void GeometriaDeIncidencia()
        {
            // Proyectil hacia el este contra un buque con rumbo norte: impacto de través.
            Assert.AreEqual(0f, ArmorImpact.IncidenceDegrees(90f, 0f, 0f, ArmorZone.BeltMidship), 1e-3f);
            Assert.AreEqual(0f, ArmorImpact.IncidenceDegrees(90f, 0f, 180f, ArmorZone.BeltMidship), 1e-3f, "la otra banda");
            Assert.AreEqual(45f, ArmorImpact.IncidenceDegrees(90f, 0f, 45f, ArmorZone.BeltMidship), 1e-3f);
            Assert.AreEqual(90f, ArmorImpact.IncidenceDegrees(90f, 0f, 90f, ArmorZone.BeltMidship), 1e-3f, "costado rasante");
            // La caída del proyectil se suma a la oblicuidad horizontal.
            Assert.AreEqual(10f, ArmorImpact.IncidenceDegrees(90f, 10f, 0f, ArmorZone.BeltMidship), 1e-3f);
            Assert.Greater(ArmorImpact.IncidenceDegrees(90f, 10f, 45f, ArmorZone.BeltMidship), 45f);
            // Torre cilíndrica y cubierta.
            Assert.AreEqual(8f, ArmorImpact.IncidenceDegrees(0f, 8f, 37f, ArmorZone.Turret), 1e-3f);
            Assert.AreEqual(82f, ArmorImpact.IncidenceDegrees(0f, 8f, 37f, ArmorZone.Deck), 1e-3f);
        }

        [Test]
        public void AnguloDeCaidaCreceConLaDistancia()
        {
            float FallAt(float range)
            {
                var elevation = NavalBallistics.ElevationForRange(400f, range, GunHeight, -10f, 30f, out _);
                return NavalBallistics.FallAngleDegrees(400f, elevation, GunHeight);
            }
            Assert.Less(FallAt(300f), 2f);
            Assert.Greater(FallAt(3000f), FallAt(1000f));
        }

        [Test]
        public void IntegridadDelCascoAcumulaDanoYSeHundeUnaVez()
        {
            var hull = new HullIntegrity(HistoricalShips.Esmeralda.HullIntegrity);
            var hits = 0;
            var sunk = 0;
            hull.Hit += _ => hits++;
            hull.Sunk += () => sunk++;

            var shot = ArmorImpact.Resolve(Armstrong300, 500f, 0f, HistoricalShips.Esmeralda, ArmorZone.BeltMidship);
            hull.ApplyImpact(shot);
            Assert.AreEqual(900f, hull.Current, 1e-3f);
            hull.ApplyImpact(shot);
            hull.ApplyImpact(shot);
            Assert.IsTrue(hull.IsSunk);
            Assert.AreEqual(0f, hull.Current);
            hull.ApplyImpact(shot);
            Assert.AreEqual(3, hits);
            Assert.AreEqual(1, sunk);
            Assert.Throws<ArgumentOutOfRangeException>(() => new HullIntegrity(0f));
        }

        [Test]
        public void ImpactoSobreLaSiluetaYZonaGolpeada()
        {
            var huascar = HistoricalShips.Huascar;
            var pose = new ShipPose(100f, 200f, 90f); // proa al este

            Assert.IsTrue(NavalHitTest.TryHit(new SeaPoint(100f + huascar.Turret.offsetForwardM, 200f), pose, huascar, out var zone));
            Assert.AreEqual(ArmorZone.Turret, zone);
            Assert.IsTrue(NavalHitTest.TryHit(new SeaPoint(100f - 10f, 202f), pose, huascar, out zone));
            Assert.AreEqual(ArmorZone.BeltMidship, zone);
            Assert.IsTrue(NavalHitTest.TryHit(new SeaPoint(100f + 27f, 200f), pose, huascar, out zone));
            Assert.AreEqual(ArmorZone.BeltEnds, zone);
            Assert.IsFalse(NavalHitTest.TryHit(new SeaPoint(100f, 200f + 20f), pose, huascar, out _), "fuera de la manga");
            Assert.IsFalse(NavalHitTest.TryHit(new SeaPoint(100f + 40f, 200f), pose, huascar, out _), "más allá de la proa");
        }
    }
}
