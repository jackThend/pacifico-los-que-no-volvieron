using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    /// <summary>ROADMAP 1.2 — buques y blindajes.</summary>
    public class ShipCatalogTests
    {
        [Test]
        public void TodoElCatalogo_EsValido()
        {
            foreach (var ship in ShipCatalog.All())
            {
                var result = ship.Validate();
                Assert.That(result.IsValid, Is.True, result.ToString());
            }
        }

        [Test]
        public void Identificadores_SonUnicos()
        {
            Assert.That(ShipCatalog.All().Select(s => s.Id), Is.Unique);
        }

        [Test]
        public void IncluyeLosBuquesDelRoadmap()
        {
            foreach (var id in new[] { ShipCatalog.HuascarId, ShipCatalog.EsmeraldaId, ShipCatalog.CochraneId, ShipCatalog.IndependenciaId })
            {
                Assert.That(ShipCatalog.Get(id), Is.Not.Null, id);
            }
        }

        [Test]
        public void Huascar_TorreColesBlindajeDeCuatroYMedioPulgadasYEspolon()
        {
            var huascar = ShipCatalog.Huascar();
            Assert.That(huascar.Faction, Is.EqualTo(Faction.Peru));
            Assert.That(huascar.Type, Is.EqualTo(ShipType.Monitor));
            Assert.That(huascar.Armor.BeltMidshipsMm, Is.EqualTo(Units.InchesToMillimeters(4.5f)).Within(0.01f));
            Assert.That(huascar.Turret, Is.Not.Null);
            Assert.That(huascar.Turret.Designation, Does.Contain("Coles"));
            Assert.That(huascar.HasRam, Is.True);

            var main = huascar.HeaviestGun;
            Assert.That(main.ShellWeightLb, Is.EqualTo(300f));
            Assert.That(main.Count, Is.EqualTo(2));
            Assert.That(main.Placement, Is.EqualTo(GunPlacement.Turret));
        }

        [Test]
        public void Esmeralda_CascoDeMaderaSinCorazaYCanonesDeCuarentaLibras()
        {
            var esmeralda = ShipCatalog.Esmeralda();
            Assert.That(esmeralda.Hull, Is.EqualTo(HullMaterial.Wood));
            Assert.That(esmeralda.Armor.IsArmored, Is.False);
            Assert.That(esmeralda.HeaviestGun.ShellWeightLb, Is.EqualTo(40f));
            Assert.That(esmeralda.SpeedKnots1879, Is.LessThan(esmeralda.DesignSpeedKnots / 2f), "calderas desgastadas en 1879");
        }

        [Test]
        public void FragatasChilenas_TienenElCinturonMasGruesoDeLaGuerra()
        {
            float cochrane = ShipCatalog.Cochrane().Armor.BeltMidshipsMm;
            Assert.That(cochrane, Is.EqualTo(Units.InchesToMillimeters(9f)).Within(0.01f));
            Assert.That(ShipCatalog.All().Max(s => s.Armor.BeltMidshipsMm), Is.EqualTo(cochrane));
            Assert.That(ShipCatalog.BlancoEncalada().Armor.BeltMidshipsMm, Is.EqualTo(cochrane));
        }

        [Test]
        public void Angamos_ElCochraneCarenadoEsMasRapidoQueElBlancoYQueElHuascar()
        {
            float cochrane = ShipCatalog.Cochrane().SpeedKnots1879;
            Assert.That(cochrane, Is.GreaterThan(ShipCatalog.BlancoEncalada().SpeedKnots1879));
            Assert.That(cochrane, Is.GreaterThan(ShipCatalog.Huascar().SpeedKnots1879));
        }

        [Test]
        public void Iquique_ElHuascarSuperaAmpliamenteEnPotenciaDeFuegoPorPieza()
        {
            Assert.That(ShipCatalog.Huascar().HeaviestGun.ShellWeightLb,
                Is.GreaterThan(7f * ShipCatalog.Esmeralda().HeaviestGun.ShellWeightLb));
        }

        [Test]
        public void TodoBuqueConEstimaciones_LasDeclara()
        {
            foreach (var ship in ShipCatalog.All())
            {
                Assert.That(ship.Source.References, Is.Not.Empty, ship.Id);
                Assert.That(ship.Source.IsEstimated("Guns.MuzzleVelocityMps"), Is.True,
                    ship.Id + ": las velocidades en boca de la artillería naval son estimaciones y deben declararse");
            }
        }

        [Test]
        public void Clone_EsProfundo()
        {
            var original = ShipCatalog.Huascar();
            var copy = original.Clone();
            copy.Armor.BeltMidshipsMm = 1f;
            copy.Guns[0].Count = 9;
            copy.Turret.TraverseDegPerSecond = 99f;
            copy.Source.References.Clear();
            Assert.That(original.Armor.BeltMidshipsMm, Is.EqualTo(114.3f).Within(0.01f));
            Assert.That(original.Guns[0].Count, Is.EqualTo(2));
            Assert.That(original.Turret.TraverseDegPerSecond, Is.EqualTo(6f));
            Assert.That(original.Source.References, Is.Not.Empty);
        }

        [Test]
        public void Validacion_DetectaIncoherencias()
        {
            var ship = ShipCatalog.Esmeralda();
            ship.Armor.BeltMidshipsMm = 100f;        // madera con coraza
            ship.SpeedKnots1879 = 20f;              // más rápida que su diseño
            ship.Guns[0].Placement = GunPlacement.Turret; // piezas en torre sin TurretSpec
            var result = ship.Validate();
            Assert.That(result.Errors, Has.Count.EqualTo(3), result.ToString());
        }

        [Test]
        public void Blindaje_EspesorYRespaldoPorZona()
        {
            var armor = ShipCatalog.Huascar().Armor;
            Assert.That(armor.IronThicknessFor(ArmorZone.MainBattery), Is.EqualTo(139.7f).Within(0.01f));
            Assert.That(armor.IronThicknessFor(ArmorZone.Unarmored), Is.EqualTo(armor.HullPlatingMm));
            Assert.That(armor.WoodFor(ArmorZone.BeltMidships), Is.EqualTo(armor.WoodBackingMm));
            Assert.That(armor.WoodFor(ArmorZone.Unarmored), Is.EqualTo(0f));

            var wood = ShipCatalog.Esmeralda().Armor;
            Assert.That(wood.IronThicknessFor(ArmorZone.BeltMidships), Is.EqualTo(0f));
            Assert.That(wood.WoodFor(ArmorZone.Unarmored), Is.EqualTo(wood.WoodBackingMm));
        }
    }
}
