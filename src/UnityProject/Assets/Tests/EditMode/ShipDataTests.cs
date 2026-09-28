using System.Linq;
using NUnit.Framework;
using Pacifico.Core;
using Pacifico.Core.Ships;

namespace Pacifico.Tests
{
    public class ShipDataTests
    {
        [Test]
        public void CatalogoCompletoEsValido()
        {
            foreach (var spec in HistoricalShips.All)
            {
                CollectionAssert.IsEmpty(ShipValidator.Validate(spec), spec.Id);
            }
        }

        [Test]
        public void CatalogoIncluyeBuquesDelRoadmap()
        {
            var ids = HistoricalShips.All.Select(s => s.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            CollectionAssert.IsSubsetOf(
                new[] { HistoricalShips.HuascarId, HistoricalShips.EsmeraldaId, HistoricalShips.CochraneId, HistoricalShips.IndependenciaId },
                ids);
        }

        [Test]
        public void HuascarEsMonitorConTorreColesYCoraza45()
        {
            var huascar = HistoricalShips.Huascar;
            Assert.AreEqual(Faction.Peru, huascar.Faction);
            Assert.AreEqual(ShipType.Monitor, huascar.Type);
            Assert.AreEqual(HullMaterial.Iron, huascar.Hull);
            Assert.IsTrue(huascar.HasTurret);
            Assert.IsTrue(huascar.HasRam);
            Assert.AreEqual(4.5f, huascar.ArmorInches(ArmorZone.BeltMidship), 1e-4f);
            Assert.AreEqual(300f, huascar.HeaviestProjectileLbs, 1e-4f);

            var turretGuns = huascar.Guns.Single(g => g.mount == GunMount.Turret);
            Assert.AreEqual(2, turretGuns.count);
            StringAssert.Contains("Armstrong", turretGuns.gunName);
        }

        [Test]
        public void EsmeraldaEsCorbetaDeMaderaSinCorazaNiEspolon()
        {
            var esmeralda = HistoricalShips.Esmeralda;
            Assert.AreEqual(Faction.Chile, esmeralda.Faction);
            Assert.AreEqual(HullMaterial.Wood, esmeralda.Hull);
            Assert.IsEmpty(esmeralda.Armor);
            Assert.IsFalse(esmeralda.HasRam);
            Assert.IsFalse(esmeralda.HasTurret);
            Assert.AreEqual(40f, esmeralda.HeaviestProjectileLbs, 1e-4f);
            Assert.Less(esmeralda.MaxSpeedKnots, HistoricalShips.Huascar.MaxSpeedKnots);
            Assert.Less(esmeralda.HullIntegrity, HistoricalShips.Huascar.HullIntegrity);
        }

        [Test]
        public void CochraneSuperaEnCorazaAlHuascarYDisparaDe250()
        {
            var cochrane = HistoricalShips.Cochrane;
            Assert.AreEqual(Faction.Chile, cochrane.Faction);
            Assert.Greater(cochrane.ArmorInches(ArmorZone.BeltMidship), HistoricalShips.Huascar.ArmorInches(ArmorZone.BeltMidship));
            Assert.AreEqual(250f, cochrane.HeaviestProjectileLbs, 1e-4f);
        }

        [Test]
        public void IndependenciaEsFragataBlindadaPeruana()
        {
            var independencia = HistoricalShips.Independencia;
            Assert.AreEqual(Faction.Peru, independencia.Faction);
            Assert.AreEqual(ShipType.ArmouredFrigate, independencia.Type);
            Assert.AreEqual(HullMaterial.Iron, independencia.Hull);
        }

        [Test]
        public void ConversionDePulgadasAMilimetros()
        {
            Assert.AreEqual(114.3f, new ArmorPlate(ArmorZone.BeltMidship, 4.5f).ThicknessMm, 1e-3f);
        }

        [Test]
        public void AndanadaSumaTodosLosCanones()
        {
            Assert.AreEqual(640f, HistoricalShips.Esmeralda.Guns[0].BroadsideWeightLbs, 1e-4f);
        }

        [Test]
        public void ValidadorRechazaDatosIncoherentes()
        {
            var madera = new ShipSpec("x", "X", Faction.Chile, ShipType.Corvette, HullMaterial.Wood,
                100f, 10f, 20f, 30f, 0, false, 0f,
                new[] { new ArmorPlate(ArmorZone.Deck, 1f), new ArmorPlate(ArmorZone.Deck, 0f) },
                new[] { new GunBattery("", 0, 0f, 0f, 0f, GunMount.Turret, 0f) }, "");
            var errors = ShipValidator.Validate(madera);
            // manga>eslora, velocidad, dotación, integridad, espesor 0, zona duplicada,
            // madera con coraza, batería sin nombre/cañones/proyectil/recarga, torreta sin coraza.
            Assert.GreaterOrEqual(errors.Count, 12, string.Join("\n", errors));

            var hierroSinCoraza = new ShipSpec("y", "Y", Faction.Peru, ShipType.Monitor, HullMaterial.Iron,
                100f, 30f, 5f, 10f, 10, true, 100f, new ArmorPlate[0],
                new[] { new GunBattery("G", 1, 10f, 3f, 300f, GunMount.Broadside, 1f) }, "");
            CollectionAssert.IsNotEmpty(ShipValidator.Validate(hierroSinCoraza));
            CollectionAssert.IsNotEmpty(ShipValidator.Validate(null));
        }
    }
}
