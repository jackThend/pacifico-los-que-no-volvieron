using System;
using NUnit.Framework;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;

namespace Pacifico.Tests
{
    public class ShipMotionModelTests
    {
        private const float Dt = 1f / 50f; // FixedUpdate por defecto de Unity

        private static ShipMotionModel NewHuascar() =>
            new ShipMotionModel(ShipHandling.FromSpec(HistoricalShips.Huascar));

        private static void Run(ShipMotionModel model, float seconds)
        {
            for (var t = 0f; t < seconds; t += Dt) model.Step(Dt);
        }

        [Test]
        public void TelegrafoRecorreLasPosicionesYSeLimita()
        {
            var model = NewHuascar();
            Assert.AreEqual(EngineOrder.Stop, model.Order);
            Assert.IsTrue(model.TelegraphUp());
            Assert.AreEqual(EngineOrder.Quarter, model.Order);
            model.TelegraphUp();
            model.TelegraphUp();
            Assert.AreEqual(EngineOrder.Full, model.Order);
            Assert.IsFalse(model.TelegraphUp());

            for (var i = 0; i < 4; i++) model.TelegraphDown();
            Assert.AreEqual(EngineOrder.Astern, model.Order);
            Assert.IsFalse(model.TelegraphDown());
            Assert.AreEqual("Toda fuerza", EngineOrder.Full.DisplayName());
        }

        [TestCase(EngineOrder.Quarter, 0.25f)]
        [TestCase(EngineOrder.Half, 0.5f)]
        [TestCase(EngineOrder.Full, 1f)]
        public void VelocidadTerminalSegunTelegrafo(EngineOrder order, float fraction)
        {
            var model = NewHuascar();
            model.SetOrder(order);
            Run(model, 600f);
            Assert.AreEqual(fraction * HistoricalShips.Huascar.MaxSpeedKnots, model.SpeedKnots, 0.05f);
        }

        [Test]
        public void AceleracionGradualPorCalderas()
        {
            var model = NewHuascar();
            model.SetOrder(EngineOrder.Full);
            Run(model, 1f);
            Assert.Greater(model.SpeedMs, 0f);
            Assert.Less(model.SpeedMs, 0.05f * model.Handling.MaxSpeedMs, "no debe saltar a toda velocidad");
            Run(model, 20f);
            Assert.Less(model.SpeedMs, 0.9f * model.Handling.MaxSpeedMs);
        }

        [Test]
        public void MantieneInerciaAlCortarPropulsion()
        {
            var model = NewHuascar();
            model.SetOrder(EngineOrder.Full);
            Run(model, 600f);
            var cruise = model.SpeedMs;

            model.SetOrder(EngineOrder.Stop);
            var zBefore = model.PositionZ;
            Run(model, 5f);
            Assert.Greater(model.SpeedMs, 0.7f * cruise, "tras 5 s sin máquinas sigue con arrancada");
            Assert.Greater(model.PositionZ - zBefore, 20f, "sigue avanzando por inercia");

            var previous = model.SpeedMs;
            for (var i = 0; i < 100; i++)
            {
                Run(model, 1f);
                Assert.LessOrEqual(model.SpeedMs, previous, "la velocidad decrece monótonamente");
                Assert.GreaterOrEqual(model.SpeedMs, 0f, "el arrastre nunca invierte la marcha");
                previous = model.SpeedMs;
            }
            Run(model, 900f);
            Assert.Less(model.SpeedMs, 0.02f * cruise, "acaba deteniéndose");
        }

        [Test]
        public void SinArrancadaNoHayGobierno()
        {
            var model = NewHuascar();
            model.SetRudderCommand(1f);
            Run(model, 30f);
            Assert.AreEqual(1f, model.Rudder, 1e-4f, "la pala sí gira");
            Assert.AreEqual(0f, model.HeadingDegrees, 1e-3f, "pero el buque no cae");
        }

        [Test]
        public void TimonGiraHaciaEstriborYBabor()
        {
            var starboard = NewHuascar();
            starboard.SetOrder(EngineOrder.Half);
            Run(starboard, 120f);
            starboard.SetRudderCommand(1f);
            Run(starboard, 20f);
            Assert.That(starboard.HeadingDegrees, Is.InRange(1f, 180f), "D cae a estribor (rumbo creciente)");
            Assert.Greater(starboard.PositionX, 0f);

            var port = NewHuascar();
            port.SetOrder(EngineOrder.Half);
            Run(port, 120f);
            port.SetRudderCommand(-1f);
            Run(port, 20f);
            Assert.That(port.HeadingDegrees, Is.InRange(180f, 359f), "A cae a babor");
        }

        [Test]
        public void PalaDelTimonTieneVelocidadLimitada()
        {
            var model = NewHuascar();
            model.SetRudderCommand(1f);
            model.Step(0.5f);
            Assert.AreEqual(0.5f, model.Rudder, 1e-3f);
            Assert.AreEqual(17.5f, model.RudderDegrees, 1e-2f);
        }

        [Test]
        public void RadioDeGiroCreceConLaVelocidad()
        {
            float MeasuredRadius(EngineOrder order)
            {
                var model = NewHuascar();
                model.SetOrder(order);
                Run(model, 600f);
                model.SetRudderCommand(1f);
                Run(model, 60f);
                var yawRad = model.YawRateDegreesPerSecond * (float)Math.PI / 180f;
                return model.SpeedMs / yawRad;
            }

            var slow = MeasuredRadius(EngineOrder.Quarter);
            var fast = MeasuredRadius(EngineOrder.Full);
            Assert.Greater(fast, slow * 1.3f);
            Assert.Greater(slow, HistoricalShips.Huascar.LengthM, "radio mayor que la eslora");
        }

        [Test]
        public void AtrasRetrocede()
        {
            var model = NewHuascar();
            model.SetOrder(EngineOrder.Astern);
            Run(model, 120f);
            Assert.Less(model.SpeedMs, 0f);
            Assert.Less(model.PositionZ, 0f);
        }

        [Test]
        public void BuqueMasPesadoTieneMasInercia()
        {
            var huascar = ShipHandling.FromSpec(HistoricalShips.Huascar);
            var cochrane = ShipHandling.FromSpec(HistoricalShips.Cochrane);
            Assert.Greater(cochrane.InertiaSeconds, huascar.InertiaSeconds);
            Assert.Less(cochrane.EngineResponsePerSecond, huascar.EngineResponsePerSecond);
        }

        [Test]
        public void EsmeraldaApenasAlcanzaSusCuatroNudos()
        {
            var model = new ShipMotionModel(ShipHandling.FromSpec(HistoricalShips.Esmeralda));
            model.SetOrder(EngineOrder.Full);
            Run(model, 600f);
            Assert.AreEqual(4f, model.SpeedKnots, 0.05f);
        }

        [Test]
        public void PasoGrandeEquivaleAPasosPequenos()
        {
            var fine = NewHuascar();
            var coarse = NewHuascar();
            foreach (var m in new[] { fine, coarse })
            {
                m.SetOrder(EngineOrder.Full);
                m.SetRudderCommand(0.5f);
            }
            Run(fine, 10f);
            coarse.Step(10f);
            Assert.AreEqual(fine.SpeedMs, coarse.SpeedMs, 0.01f);
            Assert.AreEqual(fine.HeadingDegrees, coarse.HeadingDegrees, 0.1f);
        }

        [Test]
        public void RumboSiempreNormalizado()
        {
            var model = new ShipMotionModel(ShipHandling.FromSpec(HistoricalShips.Huascar), headingDegrees: -90f);
            Assert.AreEqual(270f, model.HeadingDegrees, 1e-3f);
            model.SetOrder(EngineOrder.Full);
            model.SetRudderCommand(1f);
            Run(model, 900f);
            Assert.That(model.HeadingDegrees, Is.InRange(0f, 360f));
        }

        [Test]
        public void ParametrosInvalidosSeRechazan()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShipHandling(0f, 10f, 0.1f, 100f, 1f));
            Assert.Throws<ArgumentNullException>(() => ShipHandling.FromSpec(null));
            Assert.Throws<ArgumentNullException>(() => new ShipMotionModel(null));
        }
    }
}
