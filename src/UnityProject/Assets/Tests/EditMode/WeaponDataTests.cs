using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests
{
    public class WeaponDataTests
    {
        // Tiempos de recarga fijados por el GDD §3.1 y el ROADMAP 1.1.
        [TestCase(HistoricalWeapons.ComblainId, 2.0f)]
        [TestCase(HistoricalWeapons.ChassepotId, 2.2f)]
        [TestCase(HistoricalWeapons.GrasId, 2.2f)]
        [TestCase(HistoricalWeapons.RemingtonId, 2.1f)]
        public void RecargaCoincideConGdd(string id, float expectedSeconds)
        {
            var spec = HistoricalWeapons.FindById(id);
            Assert.IsNotNull(spec, id);
            Assert.AreEqual(expectedSeconds, spec.ReloadSeconds, 1e-4f);
        }

        [Test]
        public void CatalogoCompletoEsValido()
        {
            foreach (var spec in HistoricalWeapons.All)
            {
                CollectionAssert.IsEmpty(WeaponValidator.Validate(spec), spec.Id);
            }
        }

        [Test]
        public void IdsSonUnicos()
        {
            var ids = HistoricalWeapons.All.Select(w => w.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void FusilesSonMonotiroDe11mm()
        {
            foreach (var spec in HistoricalWeapons.All.Where(w => w.Class == WeaponClass.SingleShotRifle))
            {
                Assert.AreEqual(1, spec.Capacity, spec.Id);
                Assert.AreEqual(11f, spec.CaliberMm, 1e-4f, spec.Id);
            }
        }

        [Test]
        public void RasgosDeDisenoDelGdd()
        {
            var rifles = HistoricalWeapons.All.Where(w => w.Class == WeaponClass.SingleShotRifle).ToList();
            // Remington: "potencia de parada brutal" -> mayor daño entre fusiles.
            Assert.AreEqual(HistoricalWeapons.RemingtonId, rifles.OrderByDescending(w => w.Damage).First().Id);
            // Chassepot / Gras: "gran precisión" -> menor dispersión.
            var minSpread = rifles.Min(w => w.SpreadDegrees);
            Assert.AreEqual(minSpread, HistoricalWeapons.Chassepot.SpreadDegrees);
            Assert.AreEqual(minSpread, HistoricalWeapons.Gras.SpreadDegrees);
            // Comblain: la recarga más rápida.
            Assert.AreEqual(HistoricalWeapons.ComblainId, rifles.OrderBy(w => w.ReloadSeconds).First().Id);
        }

        [Test]
        public void ArmasBlancasDisponiblesParaAmbosBandos()
        {
            Assert.Contains(Faction.Chile, HistoricalWeapons.Corvo.Factions.ToList());
            Assert.Contains(Faction.Peru, HistoricalWeapons.TriangularBayonet.Factions.ToList());
        }

        [Test]
        public void ValidadorRechazaDatosIncoherentes()
        {
            var invalid = new WeaponSpec("x", "X", WeaponClass.SingleShotRifle, ActionType.None,
                new Faction[0], 0f, 0f, -1f, 20f, 100f, 50f, 0f, 3, "");
            var errors = WeaponValidator.Validate(invalid);
            Assert.GreaterOrEqual(errors.Count, 8);
            Assert.IsNotEmpty(WeaponValidator.Validate(null));
        }
    }
}
