using System;
using NUnit.Framework;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    /// <summary>ROADMAP 2.1 — telégrafo, inercia hidrodinámica y respuesta del timón.</summary>
    public class ShipMotionModelTests
    {
        private const float Dt = 0.02f; // FixedUpdate por defecto de Unity

        private static ShipMotionModel Huascar() => new ShipMotionModel(ShipCatalog.Huascar());

        private static void Run(ShipMotionModel ship, float seconds, float dt = Dt)
        {
            int steps = (int)Math.Round(seconds / dt);
            for (int i = 0; i < steps; i++) ship.Step(dt);
        }

        [Test]
        public void Telegrafo_RecorreLasOrdenesConWyS_YSeDetieneEnLosExtremos()
        {
            var telegraph = new EngineTelegraph();
            Assert.That(telegraph.Order, Is.EqualTo(EngineOrder.Stop));
            Assert.That(telegraph.Step(+1), Is.True);
            Assert.That(telegraph.Order, Is.EqualTo(EngineOrder.QuarterAhead));
            telegraph.Step(+1);
            telegraph.Step(+1);
            Assert.That(telegraph.Order, Is.EqualTo(EngineOrder.FullAhead));
            Assert.That(telegraph.Step(+1), Is.False, "no hay posición por encima de «Toda»");
            telegraph.Set(EngineOrder.HalfAstern);
            Assert.That(telegraph.Step(-1), Is.False);
        }

        [Test]
        public void Aceleracion_EsGradual_YAlcanzaLaVelocidadMaximaEnElTiempoDeLaFicha()
        {
            var spec = ShipCatalog.Huascar();
            var ship = Huascar();
            ship.Telegraph.Set(EngineOrder.FullAhead);

            Run(ship, 1f);
            Assert.That(ship.Speed, Is.LessThan(0.1f * ship.MaxSpeed), "no hay arrancadas instantáneas");

            Run(ship, spec.Handling.AccelerationTimeSeconds - 1f);
            Assert.That(ship.Speed, Is.EqualTo(0.95f * ship.MaxSpeed).Within(0.01f * ship.MaxSpeed));
            Assert.That(ship.SpeedKnots, Is.EqualTo(spec.SpeedKnots1879 * 0.95f).Within(0.2f));
        }

        [Test]
        public void CortarMaquina_ConservaLaArrancadaDuranteMinutos()
        {
            var spec = ShipCatalog.Huascar();
            var ship = Huascar();
            ship.SetSpeed(ship.MaxSpeed);
            ship.Telegraph.Set(EngineOrder.Stop);

            Run(ship, 10f);
            Assert.That(ship.Speed, Is.GreaterThan(0.8f * ship.MaxSpeed), "inercia al cortar propulsión");

            Run(ship, spec.Handling.CoastDownTimeSeconds - 10f);
            Assert.That(ship.Speed, Is.LessThan(0.051f * ship.MaxSpeed));
            Assert.That(ship.Speed, Is.GreaterThanOrEqualTo(0f), "detener no hace retroceder");
        }

        [Test]
        public void MarchaAtras_FrenaAntesQueDejarloDerivar()
        {
            var coasting = Huascar();
            var braking = Huascar();
            coasting.SetSpeed(coasting.MaxSpeed);
            braking.SetSpeed(braking.MaxSpeed);
            coasting.Telegraph.Set(EngineOrder.Stop);
            braking.Telegraph.Set(EngineOrder.HalfAstern);

            Run(coasting, 30f);
            Run(braking, 30f);
            Assert.That(braking.Speed, Is.LessThan(coasting.Speed * 0.5f));
        }

        [Test]
        public void SinArrancada_ElTimonNoGobierna()
        {
            var ship = Huascar();
            ship.RudderCommand = 1f;
            Run(ship, 20f);
            Assert.That(ship.Rudder, Is.EqualTo(1f));
            Assert.That(ship.HeadingDeg, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Timon_TardaEnIrALaBanda()
        {
            var spec = ShipCatalog.Huascar();
            var ship = Huascar();
            ship.RudderCommand = -1f;
            Run(ship, spec.Handling.RudderTimeSeconds / 2f);
            Assert.That(ship.Rudder, Is.EqualTo(-0.5f).Within(0.02f));
            Run(ship, spec.Handling.RudderTimeSeconds);
            Assert.That(ship.Rudder, Is.EqualTo(-1f));
        }

        [Test]
        public void RadioDeGiro_EsProporcionalALaVelocidad()
        {
            float RadiusAt(EngineOrder order)
            {
                var ship = Huascar();
                ship.Telegraph.Set(order);
                ship.SetSpeed(EngineTelegraph.SpeedFraction(order) * ship.MaxSpeed);
                ship.RudderCommand = 1f;
                Run(ship, 300f); // régimen estacionario
                return ship.TurnRadius;
            }

            float half = RadiusAt(EngineOrder.HalfAhead);
            float full = RadiusAt(EngineOrder.FullAhead);
            Assert.That(full / half, Is.EqualTo(2f).Within(0.05f));
        }

        [Test]
        public void GiroAEstribor_AumentaElRumboYDesplazaAlEste()
        {
            var ship = Huascar();
            ship.SetSpeed(ship.MaxSpeed);
            ship.Telegraph.Set(EngineOrder.FullAhead);
            ship.RudderCommand = 1f;
            Run(ship, 10f);
            Assert.That(ship.HeadingDeg, Is.InRange(1f, 90f));
            Assert.That(ship.X, Is.GreaterThan(0f));
            Assert.That(ship.Z, Is.GreaterThan(0f));
        }

        [Test]
        public void TimonALaBanda_HacePerderVelocidad()
        {
            var straight = Huascar();
            var turning = Huascar();
            foreach (var ship in new[] { straight, turning })
            {
                ship.SetSpeed(ship.MaxSpeed);
                ship.Telegraph.Set(EngineOrder.FullAhead);
            }
            turning.RudderCommand = 1f;
            Run(straight, 120f);
            Run(turning, 120f);
            Assert.That(turning.Speed, Is.LessThan(straight.Speed * 0.85f));
        }

        [Test]
        public void Simulacion_EsIndependienteDeLaTasaDeFotogramas()
        {
            var fine = Huascar();
            var coarse = Huascar();
            foreach (var ship in new[] { fine, coarse })
            {
                ship.Telegraph.Set(EngineOrder.FullAhead);
                ship.RudderCommand = 0.6f;
            }
            Run(fine, 60f, 0.01f);
            Run(coarse, 60f, 0.1f);

            Assert.That(coarse.Speed, Is.EqualTo(fine.Speed).Within(0.02f * fine.MaxSpeed));
            Assert.That(coarse.HeadingDeg, Is.EqualTo(fine.HeadingDeg).Within(3f));
            double distance = Math.Sqrt(Math.Pow(coarse.X - fine.X, 2) + Math.Pow(coarse.Z - fine.Z, 2));
            Assert.That(distance, Is.LessThan(5.0), "menos de 5 m de discrepancia tras un minuto");
        }

        [Test]
        public void Averias_ReducenPotenciaYTrabanElTimon()
        {
            var ship = Huascar();
            ship.PropulsionFactor = 0.5f;
            ship.Telegraph.Set(EngineOrder.FullAhead);
            Run(ship, 200f);
            Assert.That(ship.Speed, Is.EqualTo(0.5f * ship.MaxSpeed).Within(0.01f));

            ship.RudderCommand = 1f;
            Run(ship, 1f);
            float jammedAt = ship.Rudder;
            ship.RudderJammed = true;
            ship.RudderCommand = -1f;
            Run(ship, 10f);
            Assert.That(ship.Rudder, Is.EqualTo(jammedAt));
        }

        [Test]
        public void Esmeralda_EsMuchoMasLentaQueElHuascar()
        {
            var esmeralda = new ShipMotionModel(ShipCatalog.Esmeralda());
            var huascar = Huascar();
            foreach (var ship in new[] { esmeralda, huascar }) ship.Telegraph.Set(EngineOrder.FullAhead);
            Run(esmeralda, 120f);
            Run(huascar, 120f);
            Assert.That(huascar.Speed, Is.GreaterThan(3f * esmeralda.Speed));
        }
    }
}
