using NUnit.Framework;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    public class BroadsideBatteryTests
    {
        [Test]
        public void Esmeralda_BateSoloPorElTraves()
        {
            var battery = BroadsideBatteryModel.FromShip(ShipCatalog.Esmeralda());
            Assert.That(battery.Gun.ShellWeightLb, Is.EqualTo(40f));
            Assert.That(battery.GunsPerSide, Is.EqualTo(6));
            Assert.That(battery.SideFor(90f), Is.EqualTo(BroadsideSide.Starboard));
            Assert.That(battery.SideFor(-100f), Is.EqualTo(BroadsideSide.Port));
            Assert.That(battery.SideFor(0f), Is.EqualTo(BroadsideSide.None), "no bate por la proa");
            Assert.That(battery.SideFor(180f), Is.EqualTo(BroadsideSide.None), "ni por la popa");
        }

        [Test]
        public void CadaBanda_RecargaPorSeparado()
        {
            var battery = BroadsideBatteryModel.FromShip(ShipCatalog.Esmeralda());
            Assert.That(battery.FireAt(0f, 90f, 600f), Has.Count.EqualTo(6));
            Assert.That(battery.FireAt(0f, 90f, 600f), Is.Empty, "estribor descargada");
            Assert.That(battery.FireAt(0f, -90f, 600f), Has.Count.EqualTo(6), "babor sigue cargada");

            battery.Step(battery.Gun.ReloadSeconds);
            Assert.That(battery.IsLoaded(BroadsideSide.Starboard), Is.True);
        }

        [Test]
        public void Andanada_ApuntaAlBlancoConLaElevacionCorrecta()
        {
            var battery = BroadsideBatteryModel.FromShip(ShipCatalog.Esmeralda());
            battery.DispersionDeg = 0f;
            var salvo = battery.FireAt(30f, 90f, 800f);
            foreach (var shell in salvo)
            {
                Assert.That(shell.AzimuthDeg, Is.EqualTo(120f).Within(1e-3f));
                Assert.That(Ballistics.Range(shell.MuzzleVelocity, shell.ElevationDeg), Is.EqualTo(800f).Within(1f));
            }
        }

        [Test]
        public void SinArtilleriaDeCostado_Falla()
        {
            Assert.Throws<System.ArgumentException>(() => BroadsideBatteryModel.FromShip(ShipCatalog.Covadonga()));
        }
    }
}
