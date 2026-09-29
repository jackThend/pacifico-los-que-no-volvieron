using System;
using NUnit.Framework;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Tactics
{
    /// <summary>ROADMAP 4.1 — fuego de escuadra para la orden de ataque.</summary>
    public class SquadFireTests
    {
        [Test]
        public void ProbabilidadDeImpacto_DelOrdenDeLaEpoca()
        {
            WeaponSpec remington = WeaponCatalog.Remington();
            Assert.That(FireModel.HitProbability(remington, 100f, Posture.Standing), Is.InRange(0.22f, 0.36f));
            Assert.That(FireModel.HitProbability(remington, 200f, Posture.Standing), Is.InRange(0.07f, 0.13f));
            Assert.That(FireModel.HitProbability(remington, 400f, Posture.Standing), Is.InRange(0.015f, 0.04f));
            Assert.That(FireModel.HitProbability(remington, 5000f, Posture.Standing), Is.EqualTo(0f), "más allá del alza");
            Assert.That(FireModel.HitProbability(WeaponCatalog.Corvo(), 1f, Posture.Standing), Is.EqualTo(0f));
        }

        [Test]
        public void Tendido_OCubierto_EsMuchoMasDificilDeAlcanzar()
        {
            WeaponSpec gras = WeaponCatalog.Gras();
            float standing = FireModel.HitProbability(gras, 250f, Posture.Standing);
            Assert.That(FireModel.HitProbability(gras, 250f, Posture.Kneeling), Is.LessThan(standing));
            Assert.That(FireModel.HitProbability(gras, 250f, Posture.Prone), Is.LessThan(standing / 3f));
            Assert.That(FireModel.HitProbability(gras, 250f, Posture.Standing, 0.6f), Is.LessThan(standing * 0.5f));
            Assert.That(FireModel.HitProbability(gras, 250f, Posture.Standing, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void ElArmaMasPrecisa_AciertaMas()
        {
            Assert.That(FireModel.HitProbability(WeaponCatalog.Gras(), 300f, Posture.Standing),
                Is.GreaterThan(FireModel.HitProbability(WeaponCatalog.Remington(), 300f, Posture.Standing)));
        }

        [Test]
        public void FuegoADiscrecion_CadenciaYAciertos()
        {
            WeaponSpec comblain = WeaponCatalog.Comblain();
            var fire = new SquadFireControl(comblain, 12, seed: 3);
            int shots = 0, hits = 0, casualties = 0;
            const float seconds = 600f, dt = 1f / 20f;
            for (float t = 0f; t < seconds; t += dt)
            {
                VolleyResult r = fire.Step(dt, true, 200f, Posture.Standing);
                shots += r.Shots;
                hits += r.Hits;
                casualties += r.Casualties;
            }
            float expectedShots = 12f * seconds / fire.CycleSeconds;
            Assert.That(shots, Is.EqualTo(expectedShots).Within(expectedShots * 0.04f), "≈ un disparo cada " + fire.CycleSeconds.ToString("0.0") + " s por hombre");
            float p = FireModel.HitProbability(comblain, 200f, Posture.Standing);
            double sd = Math.Sqrt(shots * p * (1 - p));
            Assert.That(hits, Is.EqualTo(shots * p).Within(4 * sd));
            Assert.That(casualties, Is.LessThanOrEqualTo(hits));
            Assert.That(casualties, Is.EqualTo(hits * FireModel.IncapacitationProbability(comblain, 200f)).Within(4 * Math.Sqrt(hits) + 1));
        }

        [Test]
        public void SinPoderDisparar_EsperanCargados()
        {
            var fire = new SquadFireControl(WeaponCatalog.Gras(), 10);
            for (int i = 0; i < 200; i++) Assert.That(fire.Step(0.05f, false, 100f, Posture.Standing).Shots, Is.EqualTo(0));
            // Al llegar a tiro, los diez rompen el fuego a la vez.
            Assert.That(fire.Step(0.05f, true, 100f, Posture.Standing).Shots, Is.EqualTo(10));
        }

        [Test]
        public void Bajas_ReducenElVolumenDeFuego()
        {
            var fire = new SquadFireControl(WeaponCatalog.Gras(), 12, seed: 9);
            int Shots(float seconds)
            {
                int n = 0;
                for (float t = 0f; t < seconds; t += 0.05f) n += fire.Step(0.05f, true, 150f, Posture.Standing).Shots;
                return n;
            }
            Shots(10f);
            int full = Shots(300f);
            fire.SetSoldiers(6);
            int half = Shots(300f);
            Assert.That(half, Is.EqualTo(full / 2f).Within(full * 0.06f));
            Assert.That(fire.Step(0.05f, true, 150f, Posture.Standing).Shots, Is.LessThanOrEqualTo(6));
        }
    }
}
