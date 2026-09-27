using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Weapons
{
    /// <summary>ROADMAP 1.1 — valida el catálogo contra las especificaciones del GDD §3.1.</summary>
    public class WeaponCatalogTests
    {
        [TestCase(WeaponCatalog.ComblainId, 2.0f)]
        [TestCase(WeaponCatalog.ChassepotId, 2.2f)]
        [TestCase(WeaponCatalog.GrasId, 2.2f)]
        [TestCase(WeaponCatalog.RemingtonId, 2.1f)]
        public void TiempoDeRecarga_CoincideConElGDD(string id, float expectedSeconds)
        {
            Assert.That(WeaponCatalog.Get(id).ReloadSeconds, Is.EqualTo(expectedSeconds).Within(1e-4f));
        }

        [Test]
        public void TodoElCatalogo_EsValido()
        {
            foreach (var weapon in WeaponCatalog.All())
            {
                var result = weapon.Validate();
                Assert.That(result.IsValid, Is.True, result.ToString());
            }
        }

        [Test]
        public void Identificadores_SonUnicos()
        {
            var ids = WeaponCatalog.All().Select(w => w.Id).ToList();
            Assert.That(ids, Is.Unique);
        }

        [Test]
        public void Fusiles_SonMonotiroDeOnceMilimetros()
        {
            foreach (var weapon in WeaponCatalog.All().Where(w => w.Kind == WeaponKind.Rifle))
            {
                Assert.That(weapon.Feed, Is.EqualTo(AmmoFeed.SingleShot), weapon.Id);
                Assert.That(weapon.CaliberMm, Is.InRange(10.9f, 11.5f), weapon.Id);
            }
        }

        [Test]
        public void Remington_TieneLaMayorPotenciaDeParada()
        {
            var rifles = WeaponCatalog.All().Where(w => w.IsFirearm).ToList();
            var remington = WeaponCatalog.Get(WeaponCatalog.RemingtonId);
            Assert.That(rifles.Max(w => w.BaseDamage), Is.EqualTo(remington.BaseDamage));
        }

        [Test]
        public void CerrojosFranceses_SonLosMasPrecisos()
        {
            float comblain = WeaponCatalog.Get(WeaponCatalog.ComblainId).DispersionMoa;
            float remington = WeaponCatalog.Get(WeaponCatalog.RemingtonId).DispersionMoa;
            foreach (var id in new[] { WeaponCatalog.ChassepotId, WeaponCatalog.GrasId })
            {
                Assert.That(WeaponCatalog.Get(id).DispersionMoa, Is.LessThan(comblain), id);
                Assert.That(WeaponCatalog.Get(id).DispersionMoa, Is.LessThan(remington), id);
            }
        }

        [Test]
        public void Bandos_SegunElGDD()
        {
            Assert.That(WeaponCatalog.Get(WeaponCatalog.ComblainId).UsedBy, Does.Contain(Faction.Chile));
            Assert.That(WeaponCatalog.Get(WeaponCatalog.ChassepotId).UsedBy, Does.Contain(Faction.Peru));
            Assert.That(WeaponCatalog.Get(WeaponCatalog.RemingtonId).UsedBy, Does.Contain(Faction.Bolivia));
            Assert.That(WeaponCatalog.Get(WeaponCatalog.CorvoId).UsedBy, Does.Contain(Faction.Chile));
        }

        [Test]
        public void Catalogo_DevuelveCopiasIndependientes()
        {
            var first = WeaponCatalog.Get(WeaponCatalog.ComblainId);
            first.ReloadSeconds = 99f;
            first.UsedBy.Clear();
            var second = WeaponCatalog.Get(WeaponCatalog.ComblainId);
            Assert.That(second.ReloadSeconds, Is.EqualTo(2.0f));
            Assert.That(second.UsedBy, Is.Not.Empty);
        }

        [Test]
        public void Clone_CopiaProfundamenteLasListas()
        {
            var original = WeaponCatalog.Gras();
            int references = original.Source.References.Count;
            var copy = original.Clone();
            copy.Source.References.Add("otra");
            copy.UsedBy.Add(Faction.Bolivia);
            Assert.That(original.Source.References.Count, Is.EqualTo(references));
            Assert.That(original.UsedBy, Has.Count.EqualTo(1));
        }
    }

    public class WeaponSpecTests
    {
        [Test]
        public void Dano_EsPlenoHastaElAlcanceEficazYLuegoDecae()
        {
            var comblain = WeaponCatalog.Comblain();
            Assert.That(comblain.DamageAtDistance(0f), Is.EqualTo(comblain.BaseDamage));
            Assert.That(comblain.DamageAtDistance(comblain.EffectiveRangeM), Is.EqualTo(comblain.BaseDamage));
            Assert.That(comblain.DamageAtDistance(comblain.MaxSightRangeM),
                Is.EqualTo(comblain.BaseDamage * comblain.MinDamageFactor).Within(1e-3f));
            Assert.That(comblain.DamageAtDistance(comblain.MaxSightRangeM + 1f), Is.EqualTo(0f));
        }

        [Test]
        public void Dano_NoAumentaConLaDistancia()
        {
            foreach (var weapon in WeaponCatalog.All().Where(w => w.IsFirearm))
            {
                float previous = float.MaxValue;
                for (float d = 0f; d <= weapon.MaxSightRangeM; d += 25f)
                {
                    float damage = weapon.DamageAtDistance(d);
                    Assert.That(damage, Is.LessThanOrEqualTo(previous + 1e-4f), weapon.Id + " a " + d + " m");
                    previous = damage;
                }
            }
        }

        [Test]
        public void ArmaBlanca_SoloHiereEnSuAlcance()
        {
            var corvo = WeaponCatalog.Corvo();
            Assert.That(corvo.DamageAtDistance(1.0f), Is.EqualTo(corvo.MeleeDamage));
            Assert.That(corvo.DamageAtDistance(1.5f), Is.EqualTo(0f));
        }

        [Test]
        public void Dispersion_CrecePropocionalmenteALaDistancia()
        {
            var gras = WeaponCatalog.Gras();
            // 1 MOA ≈ 29,1 mm de diámetro a 100 m -> 2,5 MOA ≈ 36,4 mm de radio.
            Assert.That(gras.DispersionRadiusAt(100f), Is.EqualTo(0.0364f).Within(5e-4f));
            Assert.That(gras.DispersionRadiusAt(200f), Is.EqualTo(2f * gras.DispersionRadiusAt(100f)).Within(1e-5f));
        }

        [Test]
        public void Cadencia_MonotiroDependeSoloDeLaRecarga()
        {
            Assert.That(WeaponCatalog.Comblain().SustainedShotsPerMinute, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(WeaponCatalog.Winchester().SustainedShotsPerMinute, Is.GreaterThan(WeaponCatalog.Comblain().SustainedShotsPerMinute));
        }

        [Test]
        public void Validacion_DetectaDatosIncoherentes()
        {
            var broken = WeaponCatalog.Comblain();
            broken.EffectiveRangeM = broken.MaxSightRangeM + 100f;
            broken.MuzzleVelocityMps = 900f;
            broken.MagazineCapacity = 5;
            broken.Source.References.Clear();
            var result = broken.Validate();
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(4), result.ToString());
        }
    }
}
