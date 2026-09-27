using System;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    /// <summary>ROADMAP 2.2 — torre Coles del Huáscar: giro independiente, topes, convergencia y recarga.</summary>
    public class ColesTurretTests
    {
        private static ColesTurretModel Turret() => ColesTurretModel.FromShip(ShipCatalog.Huascar());

        private static void Run(ColesTurretModel turret, float seconds, float dt = 0.02f)
        {
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) turret.Step(dt);
        }

        [Test]
        public void SoloElHuascarTieneTorreColes()
        {
            Assert.DoesNotThrow(() => ColesTurretModel.FromShip(ShipCatalog.Huascar()));
            Assert.Throws<ArgumentException>(() => ColesTurretModel.FromShip(ShipCatalog.Esmeralda()));
            Assert.That(Turret().GunCount, Is.EqualTo(2));
            Assert.That(Turret().Gun.ShellWeightLb, Is.EqualTo(300f));
        }

        [Test]
        public void Giro_NoSuperaLaVelocidadMaxima_YSeDetieneSinSobrepasar()
        {
            var turret = Turret();
            turret.Order(90f, 0f);
            float previous = turret.TrainDeg;
            float maxStep = 0f;
            float maxTrain = 0f;
            for (int i = 0; i < 2000; i++)
            {
                turret.Step(0.02f);
                maxStep = Math.Max(maxStep, Math.Abs(MathUtil.DeltaAngle(previous, turret.TrainDeg)));
                maxTrain = Math.Max(maxTrain, turret.TrainDeg);
                previous = turret.TrainDeg;
            }
            Assert.That(maxStep / 0.02f, Is.LessThanOrEqualTo(turret.Spec.TraverseDegPerSecond + 1e-3f));
            Assert.That(maxTrain, Is.LessThanOrEqualTo(90f + 1e-3f), "apuntado suave: sin sobrepasar la orden");
            Assert.That(turret.TrainDeg, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(turret.IsOnTarget(), Is.True);
        }

        [Test]
        public void Giro_ArrancaSuave()
        {
            var turret = Turret();
            turret.Order(120f, 0f);
            Run(turret, 0.5f);
            // Con aceleración de 4 °/s² en 0,5 s se recorre ~0,5 °, no los 3 ° de un arranque instantáneo a 6 °/s.
            Assert.That(turret.TrainDeg, Is.LessThan(1f));
        }

        [Test]
        public void Giro_EsIndependienteDelCasco_YTomaElCaminoMasCorto()
        {
            var turret = Turret();
            turret.Order(-100f, 0f);
            Run(turret, 60f);
            Assert.That(turret.TrainDeg, Is.EqualTo(-100f).Within(1e-3f));
            turret.Order(170f, 0f); // de -100 a 170 el camino corto pasa por 180 (90 °), no por 0 (270 °)
            Run(turret, 5f);
            Assert.That(turret.TrainDeg, Is.LessThan(-100f), "debe girar hacia babor/popa");
        }

        [Test]
        public void Elevacion_RespetaLosTopesHistoricos()
        {
            var turret = Turret();
            turret.Order(0f, 40f);
            Run(turret, 30f);
            Assert.That(turret.ElevationDeg, Is.EqualTo(turret.Spec.MaxElevationDeg));
            turret.Order(0f, -30f);
            Run(turret, 30f);
            Assert.That(turret.ElevationDeg, Is.EqualTo(turret.Spec.MinElevationDeg));
        }

        [Test]
        public void SectorCiegoDePopa_ImpideDisparar()
        {
            var turret = Turret();
            turret.Order(175f, 2f);
            Run(turret, 60f);
            Assert.That(turret.IsInBlindArc(turret.TrainDeg), Is.True);
            Assert.That(turret.CanFire, Is.False);
            Assert.That(turret.Fire(0f), Is.Empty);

            turret.Order(120f, 2f);
            Run(turret, 30f);
            Assert.That(turret.CanFire, Is.True);
        }

        [Test]
        public void Andanada_DisparaAmbasPiezas_YExigeRecargar()
        {
            var turret = Turret();
            turret.Order(90f, 3f);
            Run(turret, 30f);
            var salvo = turret.Fire(shipHeadingDeg: 0f);
            Assert.That(salvo, Has.Count.EqualTo(2));
            Assert.That(salvo[0].ShellMassKg, Is.EqualTo(Units.PoundsToKilograms(300f)).Within(0.01f));
            Assert.That(turret.Fire(0f), Is.Empty, "las piezas de avancarga necesitan recargarse");

            Run(turret, turret.Gun.ReloadSeconds * 0.5f);
            Assert.That(turret.ReloadProgress(0), Is.EqualTo(0.5f).Within(0.01f));
            Run(turret, turret.Gun.ReloadSeconds * 0.5f + 0.1f);
            Assert.That(turret.Fire(0f), Has.Count.EqualTo(2));
        }

        [Test]
        public void Disparo_CombinaRumboDelBuqueYMarcacionDeLaTorre()
        {
            var turret = Turret();
            turret.DispersionDeg = 0f;
            turret.ConvergenceRangeM = 5000f;
            turret.Order(90f, 3f);
            Run(turret, 30f);
            var salvo = turret.Fire(shipHeadingDeg: 45f);
            foreach (var shell in salvo)
            {
                Assert.That(MathUtil.DeltaAngle(135f, shell.AzimuthDeg), Is.EqualTo(0f).Within(0.05f));
                Assert.That(shell.ElevationDeg, Is.EqualTo(3f).Within(1e-3f));
            }
        }

        [Test]
        public void Convergencia_LasPiezasSeCruzanALaDistanciaAjustada()
        {
            var turret = Turret();
            turret.ConvergenceRangeM = 800f;
            float half = turret.GunSeparationM / 2f;
            Assert.That(turret.LateralMissAt(0, 800f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(turret.LateralMissAt(1, 800f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(turret.LateralMissAt(0, 400f), Is.EqualTo(-half / 2f).Within(1e-4f));
            Assert.That(turret.LateralMissAt(1, 1600f), Is.EqualTo(-half).Within(1e-4f), "más allá se cruzan");
            Assert.That(turret.ConvergenceYawDeg(0), Is.GreaterThan(0f), "la pieza de babor apunta hacia estribor");
            Assert.That(turret.ConvergenceYawDeg(1), Is.EqualTo(-turret.ConvergenceYawDeg(0)));
        }

        [Test]
        public void OrdenarAUnPunto_CalculaMarcacionYElevacion()
        {
            var turret = Turret();
            // Buque al norte (rumbo 0), blanco 1.000 m al este: marcación +90.
            turret.OrderAtPoint(0f, 0f, 0f, 1000f, 0f);
            Assert.That(turret.OrderedTrainDeg, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(Ballistics.Range(turret.Gun.MuzzleVelocityMps, turret.OrderedElevationDeg), Is.EqualTo(1000f).Within(1f));
            Assert.That(turret.TargetOutOfRange, Is.False);

            turret.OrderAtPoint(0f, 0f, 0f, 0f, 50000f);
            Assert.That(turret.TargetOutOfRange, Is.True);
            Assert.That(turret.OrderedElevationDeg, Is.EqualTo(turret.Spec.MaxElevationDeg));
        }

        [Test]
        public void Dispersion_EsDeterministaConLaMismaSemilla()
        {
            var a = ColesTurretModel.FromShip(ShipCatalog.Huascar(), seed: 7);
            var b = ColesTurretModel.FromShip(ShipCatalog.Huascar(), seed: 7);
            foreach (var t in new[] { a, b })
            {
                t.Order(60f, 4f);
                Run(t, 30f);
            }
            var sa = a.Fire(0f);
            var sb = b.Fire(0f);
            Assert.That(sa[0].AzimuthDeg, Is.EqualTo(sb[0].AzimuthDeg));
            Assert.That(sa[1].ElevationDeg, Is.EqualTo(sb[1].ElevationDeg));
        }
    }

    public class BallisticsTests
    {
        [Test]
        public void ElevacionYAlcance_SonInversos()
        {
            const float v = 400f;
            foreach (float range in new[] { 200f, 800f, 2000f, 5000f })
            {
                Assert.That(Ballistics.TrySolveElevation(v, range, out float elevation), Is.True);
                Assert.That(Ballistics.Range(v, elevation), Is.EqualTo(range).Within(range * 1e-3f));
            }
            Assert.That(Ballistics.TrySolveElevation(v, Ballistics.MaxRange(v) * 1.01f, out _), Is.False);
        }

        [Test]
        public void Rumbo_ConvencionDeUnity()
        {
            Assert.That(Ballistics.Bearing(0f, 0f, 0f, 10f), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(Ballistics.Bearing(0f, 0f, 10f, 0f), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(Ballistics.Bearing(0f, 0f, -10f, 0f), Is.EqualTo(270f).Within(1e-3f));
        }

        [Test]
        public void Adelanto_ElProyectilYElBlancoCoincidenEnElPuntoPredicho()
        {
            const float v = 400f;
            // Blanco a 1.500 m al norte navegando hacia el este a 3 m/s (≈6 nudos).
            Assert.That(Ballistics.TryLead(0f, 0f, 0f, 1500f, 3f, 0f, v, out float ax, out float az, out float t), Is.True);
            Assert.That(ax, Is.EqualTo(3f * t).Within(1e-3f));
            Assert.That(az, Is.EqualTo(1500f));
            Ballistics.TrySolveElevation(v, Ballistics.Distance(0f, 0f, ax, az), out float elevation);
            Assert.That(Ballistics.FlightTime(v, elevation), Is.EqualTo(t).Within(1e-3f));
        }
    }
}
