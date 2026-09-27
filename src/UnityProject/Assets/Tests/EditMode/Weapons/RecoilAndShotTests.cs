using System;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Weapons
{
    /// <summary>ROADMAP 3.2 — retroceso físico, dispersión del disparo y coherencia de la bala con el alza.</summary>
    public class RecoilTests
    {
        [Test]
        public void RetrocesoLibre_PorConservacionDelMomento()
        {
            var gras = WeaponCatalog.Gras();
            // (0,025·450 + 0,0052·1200) / 4,2 = 4,16 m/s
            Assert.That(RecoilModel.FreeRecoilVelocity(gras), Is.EqualTo(4.164f).Within(0.01f));
            Assert.That(RecoilModel.FreeRecoilEnergy(gras), Is.InRange(30f, 45f), "del orden de los fusiles militares de pólvora negra");
        }

        [Test]
        public void SinArma_NoHayRetroceso()
        {
            Assert.That(RecoilModel.FreeRecoilVelocity(null), Is.EqualTo(0f));
            Assert.That(RecoilModel.FreeRecoilEnergy(null), Is.EqualTo(0f));
        }

        [Test]
        public void LaCarabinaDePistola_PateaMenosQueLosFusilesDeGuerra()
        {
            float winchester = RecoilModel.FreeRecoilEnergy(WeaponCatalog.Winchester());
            foreach (var id in new[] { WeaponCatalog.ComblainId, WeaponCatalog.ChassepotId, WeaponCatalog.GrasId, WeaponCatalog.RemingtonId })
            {
                Assert.That(winchester, Is.LessThan(RecoilModel.FreeRecoilEnergy(WeaponCatalog.Get(id)) * 0.5f), id);
            }
        }

        [Test]
        public void Salto_AlcanzaElPicoYSeRecupera()
        {
            var gras = WeaponCatalog.Gras();
            var recoil = new RecoilModel();
            recoil.Kick(gras, aim01: 0f, crouched: false);
            float peak = 0f, peakTime = 0f;
            for (float t = 0f; t < 1f; t += 1f / 240f)
            {
                recoil.Step(1f / 240f);
                if (recoil.PitchDeg > peak)
                {
                    peak = recoil.PitchDeg;
                    peakTime = t;
                }
            }
            Assert.That(peak, Is.EqualTo(RecoilModel.KickDeg(gras)).Within(RecoilModel.KickDeg(gras) * 0.03f));
            Assert.That(peakTime, Is.EqualTo(1f / RecoilModel.RecoveryOmega).Within(0.01f));
            Assert.That(Math.Abs(recoil.PitchDeg), Is.LessThan(0.05f * peak), "recuperado al cabo de un segundo");
        }

        [Test]
        public void Salto_IgualA30y144FPS()
        {
            float PeakAt(float dt)
            {
                var recoil = new RecoilModel();
                recoil.Kick(WeaponCatalog.Comblain(), 0f, false);
                float peak = 0f;
                for (float t = 0f; t < 0.5f; t += dt)
                {
                    recoil.Step(dt);
                    peak = Math.Max(peak, recoil.PitchDeg);
                }
                return peak;
            }
            // Con la solución exacta la curva es la misma; solo cambia dónde se muestrea el pico.
            Assert.That(PeakAt(1f / 30f), Is.EqualTo(PeakAt(1f / 144f)).Within(0.1f));
        }

        [Test]
        public void Encarado_YAgachado_PateaMenos()
        {
            float PeakWith(float aim, bool crouched)
            {
                var recoil = new RecoilModel();
                recoil.Kick(WeaponCatalog.Remington(), aim, crouched);
                float peak = 0f;
                for (int i = 0; i < 60; i++)
                {
                    recoil.Step(1f / 120f);
                    peak = Math.Max(peak, recoil.PitchDeg);
                }
                return peak;
            }
            Assert.That(PeakWith(1f, true), Is.LessThan(PeakWith(1f, false)));
            Assert.That(PeakWith(1f, false), Is.LessThan(PeakWith(0f, false)));
        }
    }

    public class ShotTests
    {
        [Test]
        public void Encarado_LaDispersionEsElGrupoDeLaFicha()
        {
            var chassepot = WeaponCatalog.Chassepot();
            var rng = new Random(1866);
            var shots = Enumerable.Range(0, 4000).Select(_ => RifleShotSolver.Solve(chassepot, 0f, 1f, false, rng)).ToList();
            double sigmaMoa = Math.Sqrt(shots.Average(s => s.YawDeg * (double)s.YawDeg)) * 60.0;
            Assert.That(sigmaMoa, Is.EqualTo(chassepot.DispersionMoa / 4.0).Within(chassepot.DispersionMoa / 4.0 * 0.08));
            Assert.That(Math.Abs(shots.Average(s => s.PitchDeg)), Is.LessThan(0.01), "sin sesgo");
        }

        [Test]
        public void ALaCadera_YEnMovimiento_DisparaMuchoPeor()
        {
            var gras = WeaponCatalog.Gras();
            float aimed = RifleShotSolver.SigmaDeg(gras, 1f, false);
            Assert.That(RifleShotSolver.SigmaDeg(gras, 0f, false), Is.GreaterThan(aimed * 20f));
            Assert.That(RifleShotSolver.SigmaDeg(gras, 1f, true), Is.GreaterThan(aimed * 5f));
            Assert.That(RifleShotSolver.SigmaDeg(gras, 0.5f, false), Is.GreaterThan(MathUtil.Lerp(RifleShotSolver.HipSigmaDeg, aimed, 0.5f)),
                "disparar a medio encare penaliza");
        }

        [Test]
        public void LaElevacionDelAlza_SeSumaAlDisparo()
        {
            var rng = new Random(1);
            var comblain = WeaponCatalog.Comblain();
            double mean = Enumerable.Range(0, 2000).Average(_ => RifleShotSolver.Solve(comblain, 1.5f, 1f, false, rng).PitchDeg);
            Assert.That(mean, Is.EqualTo(1.5).Within(0.01));
        }

        /// <summary>
        /// La bala del juego (integrada paso a paso como en Unity, a 50 Hz) cruza la línea de mira a la distancia
        /// graduada en el alza: el alza, la balística y el proyectil usan el mismo modelo.
        /// </summary>
        [TestCase(200f)]
        [TestCase(500f)]
        [TestCase(1000f)]
        public void BalaDelJuego_CruzaLaMiraALaDistanciaDelAlza(float range)
        {
            var comblain = WeaponCatalog.Comblain();
            var ladder = new SightLadder(comblain);
            ladder.SetRange(range);

            double theta = ladder.ElevationDeg * Math.PI / 180.0;
            var position = new Vec3(0f, -ladder.SightHeightM, 0f);
            var velocity = new Vec3(0f, (float)(Math.Sin(theta) * comblain.MuzzleVelocityMps), (float)(Math.Cos(theta) * comblain.MuzzleVelocityMps));
            Vec3 previous = position;
            while (position.Z < range)
            {
                previous = position;
                SmallArmsBallistics.StepBullet(ref position, ref velocity, ladder.DragFactor, 0.02f);
            }
            float f = (range - previous.Z) / (position.Z - previous.Z);
            float heightAtRange = previous.Y + (position.Y - previous.Y) * f;
            Assert.That(heightAtRange, Is.EqualTo(0f).Within(0.03f), "±3 cm respecto al punto apuntado");
        }
    }
}
