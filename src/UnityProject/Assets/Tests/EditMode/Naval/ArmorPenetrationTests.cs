using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;

namespace Pacifico.Tests.Naval
{
    /// <summary>ROADMAP 2.3 — blindaje angular: rebote en el Huáscar, perforación en la Esmeralda.</summary>
    public class ArmorPenetrationTests
    {
        private static GunMount Gun(ShipSpec ship, float shellLb) => ship.Guns.Find(g => g.ShellWeightLb == shellLb);

        private static GunMount Esmeralda40 => Gun(ShipCatalog.Esmeralda(), 40f);
        private static GunMount Huascar300 => Gun(ShipCatalog.Huascar(), 300f);
        private static GunMount Cochrane250 => Gun(ShipCatalog.Cochrane(), 250f);

        [Test]
        public void Calibracion_Palliser10PulgadasPerforaUnasOncePulgadas()
        {
            float penetration = ArmorPenetrationModel.PenetrationMm(Units.PoundsToKilograms(400f), 254f, 416f);
            Assert.That(penetration, Is.EqualTo(Units.InchesToMillimeters(11f)).Within(10f));
        }

        [TestCase(200f)]
        [TestCase(600f)]
        [TestCase(1200f)]
        public void Iquique_Los40LibrasDeLaEsmeraldaNoPerforanElCinturonDelHuascar(float range)
        {
            foreach (float obliquity in new[] { 0f, 30f, 60f, 75f })
            {
                var result = ArmorPenetrationModel.ResolveAgainstShip(Esmeralda40, range, obliquity, ShipCatalog.Huascar(), ArmorZone.BeltMidships);
                Assert.That(result.Penetrated, Is.False, "a " + range + " m y " + obliquity + "°");
            }
        }

        [Test]
        public void Iquique_ImpactoOblicuoDe40LibrasRebotaEnLaCoraza()
        {
            var result = ArmorPenetrationModel.ResolveAgainstShip(Esmeralda40, 400f, 70f, ShipCatalog.Huascar(), ArmorZone.BeltMidships);
            Assert.That(result.Outcome, Is.EqualTo(ImpactOutcome.Ricochet));
            Assert.That(result.StructuralDamage, Is.LessThan(1.5f), "rebotes «con estruendos metálicos y chispas», sin daño real");
        }

        [Test]
        public void Iquique_Los300LibrasDelHuascarPerforanLaEsmeraldaConDanoCritico()
        {
            var esmeralda = ShipCatalog.Esmeralda();
            foreach (float obliquity in new[] { 0f, 45f, 70f })
            {
                var result = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 800f, obliquity, esmeralda, ArmorZone.BeltMidships);
                Assert.That(result.Outcome, Is.EqualTo(ImpactOutcome.CriticalPenetration), obliquity + "°");
                Assert.That(result.FireChance, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Esmeralda_ResistiriaMuyPocosImpactosPerforantes()
        {
            var esmeralda = ShipCatalog.Esmeralda();
            var hit = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 500f, 10f, esmeralda, ArmorZone.BeltMidships);
            float hitsToDestroy = esmeralda.DisplacementTonnes / hit.StructuralDamage;
            Assert.That(hitsToDestroy, Is.InRange(2f, 6f));
        }

        [Test]
        public void Angamos_Los250LibrasDelCochranePerforanElHuascarAMenosDeDosMilMetros()
        {
            var huascar = ShipCatalog.Huascar();
            Assert.That(ArmorPenetrationModel.ResolveAgainstShip(Cochrane250, 1000f, 20f, huascar, ArmorZone.BeltMidships).Penetrated, Is.True);
            Assert.That(ArmorPenetrationModel.ResolveAgainstShip(Cochrane250, 1000f, 0f, huascar, ArmorZone.ConningTower).Penetrated, Is.True,
                "el impacto en la torre de mando que mató a Grau");
        }

        [Test]
        public void ElHuascarNoPerforaElCinturonCentralDelCochrane()
        {
            var result = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 300f, 0f, ShipCatalog.Cochrane(), ArmorZone.BeltMidships);
            Assert.That(result.Penetrated, Is.False);
            var ends = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 300f, 0f, ShipCatalog.Cochrane(), ArmorZone.BeltEnds);
            Assert.That(ends.Penetrated, Is.True, "los extremos, más delgados, sí son vulnerables");
        }

        [Test]
        public void Oblicuidad_AumentaElEspesorEfectivo()
        {
            var huascar = ShipCatalog.Huascar();
            var normal = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 500f, 0f, huascar, ArmorZone.BeltMidships);
            var angled = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 500f, 45f, huascar, ArmorZone.BeltMidships);
            Assert.That(angled.EffectiveThicknessMm, Is.EqualTo(normal.EffectiveThicknessMm * 1.4142f).Within(0.5f));
        }

        [Test]
        public void PosicionarA45Grados_ConvierteUnaPerforacionEnRebote()
        {
            // Guion, cap. 2: «posicionar el blindado a 45 grados para maximizar el rebote».
            var cochrane = ShipCatalog.Cochrane();
            var square = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 300f, 0f, cochrane, ArmorZone.BeltEnds);
            var angled = ArmorPenetrationModel.ResolveAgainstShip(Huascar300, 300f, 60f, cochrane, ArmorZone.BeltEnds);
            Assert.That(square.Penetrated, Is.True);
            Assert.That(angled.Penetrated, Is.False);
        }

        [Test]
        public void Distancia_ReduceVelocidadYPerforacion()
        {
            GunMount gun = Huascar300;
            float near = ArmorPenetrationModel.StrikingVelocity(gun.MuzzleVelocityMps, gun.ShellMassKg, gun.BoreMm, 100f);
            float far = ArmorPenetrationModel.StrikingVelocity(gun.MuzzleVelocityMps, gun.ShellMassKg, gun.BoreMm, 2000f);
            Assert.That(far, Is.LessThan(near));
            Assert.That(far / gun.MuzzleVelocityMps, Is.InRange(0.6f, 0.85f));
            Assert.That(ArmorPenetrationModel.PenetrationMm(gun.ShellMassKg, gun.BoreMm, far),
                Is.LessThan(ArmorPenetrationModel.PenetrationMm(gun.ShellMassKg, gun.BoreMm, near)));
        }

        [Test]
        public void Madera_NoHaceRebotarSalvoImpactosRasantes()
        {
            Assert.That(ArmorPenetrationModel.RicochetAngle(0f, 120f, HullMaterial.Wood), Is.EqualTo(ArmorPenetrationModel.WoodRicochetAngleDeg));
            var grazing = ArmorPenetrationModel.ResolveAgainstShip(Esmeralda40, 300f, 85f, ShipCatalog.Covadonga(), ArmorZone.BeltMidships);
            Assert.That(grazing.Outcome, Is.EqualTo(ImpactOutcome.Ricochet));
        }

        [Test]
        public void PlanchaGruesaRespectoAlCalibre_RebotaAntes()
        {
            float thin = ArmorPenetrationModel.RicochetAngle(50f, 254f, HullMaterial.Iron);
            float thick = ArmorPenetrationModel.RicochetAngle(260f, 254f, HullMaterial.Iron);
            Assert.That(thin, Is.EqualTo(ArmorPenetrationModel.BaseRicochetAngleDeg));
            Assert.That(thick, Is.EqualTo(ArmorPenetrationModel.ThickPlateRicochetAngleDeg));
        }

        [Test]
        public void DanoPorResultado_EstaOrdenado()
        {
            var huascar = ShipCatalog.Huascar();
            var gun = Cochrane250;
            float ricochet = ArmorPenetrationModel.ResolveAgainstShip(gun, 1000f, 80f, huascar, ArmorZone.BeltMidships).StructuralDamage;
            float bounce = ArmorPenetrationModel.ResolveAgainstShip(gun, 6000f, 0f, huascar, ArmorZone.MainBattery).StructuralDamage;
            float pen = ArmorPenetrationModel.ResolveAgainstShip(gun, 800f, 0f, huascar, ArmorZone.BeltMidships).StructuralDamage;
            Assert.That(ricochet, Is.LessThan(bounce));
            Assert.That(bounce, Is.LessThan(pen));
        }
    }
}
