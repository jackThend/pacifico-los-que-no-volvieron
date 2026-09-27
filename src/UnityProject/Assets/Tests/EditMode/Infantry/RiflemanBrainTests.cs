using System;
using NUnit.Framework;
using Pacifico.Core.Infantry;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Infantry
{
    /// <summary>ROADMAP 6.2 — el fusilero de la IA en primera persona y la salud de los combatientes.</summary>
    public class RiflemanBrainTests
    {
        private const float Dt = 1f / 60f;
        private static WeaponSpec Comblain => WeaponCatalog.Get(WeaponCatalog.ComblainId);

        private static RiflemanPerception Seen(float distance, bool loaded = true) => new RiflemanPerception
        {
            HasTarget = true, TargetDistanceM = distance, LineOfSight = true, Loaded = loaded, HasAmmo = true,
        };

        /// <summary>Segundos hasta el primer disparo (o infinito).</summary>
        private static float TimeToFirstShot(RiflemanBrain brain, RiflemanPerception p, float max = 20f)
        {
            for (float t = 0f; t < max; t += Dt)
            {
                if (brain.Step(Dt, p).Fire) return t + Dt;
            }
            return float.PositiveInfinity;
        }

        [Test]
        public void Dispara_TrasReaccionarYApuntar()
        {
            var brain = new RiflemanBrain(Comblain, 7);
            float t = TimeToFirstShot(brain, Seen(150f));
            Assert.That(t, Is.InRange(RiflemanBrain.ReactionSeconds + FireModel.AimSeconds,
                                      RiflemanBrain.ReactionSeconds + FireModel.AimSeconds + FireModel.AimJitterSeconds + 2 * Dt));
            Assert.That(brain.State, Is.EqualTo(RiflemanState.Engage));
            // Sin cargar no vuelve a disparar: carga, de rodillas.
            RiflemanDecision d = brain.Step(Dt, Seen(150f, loaded: false));
            Assert.That(d.Fire, Is.False);
            Assert.That(d.State, Is.EqualTo(RiflemanState.Reload));
            Assert.That(d.Kneel, Is.True);
        }

        [Test]
        public void FueraDeAlcanceOSinVerlo_Avanza()
        {
            var brain = new RiflemanBrain(Comblain, 7);
            RiflemanDecision far = brain.Step(Dt, Seen(900f));
            Assert.That(far.State, Is.EqualTo(RiflemanState.Advance));
            Assert.That(far.MoveToObjective, Is.True);
            var hidden = Seen(80f);
            hidden.LineOfSight = false;
            RiflemanDecision d = brain.Step(Dt, hidden);
            Assert.That(d.MoveToTarget, Is.True, "lo busca rodeando la tapia");
            Assert.That(TimeToFirstShot(brain, hidden, 10f), Is.EqualTo(float.PositiveInfinity));
            hidden.HoldPosition = true;
            d = brain.Step(Dt, hidden);
            Assert.That(d.MoveToTarget || d.MoveToObjective, Is.False, "el sirviente de la pieza no se mueve");
        }

        [Test]
        public void Asalto_NoSeParaATirarHastaEntrarEnElPueblo()
        {
            var brain = new RiflemanBrain(Comblain, 5) { EngageRangeM = 70f };
            RiflemanDecision d = brain.Step(Dt, Seen(120f));
            Assert.That(d.State, Is.EqualTo(RiflemanState.Advance), "a 120 m sigue bajando la ladera");
            Assert.That(d.MoveToObjective, Is.True);
            Assert.That(brain.Step(Dt, Seen(60f)).State, Is.EqualTo(RiflemanState.Engage));
            Assert.That(new RiflemanBrain(Comblain, 5).EngageRangeM, Is.EqualTo(Comblain.EffectiveRangeM), "por defecto, el alcance eficaz");
        }

        [Test]
        public void Bayoneta_CargaYGolpeaConPausa()
        {
            var brain = new RiflemanBrain(Comblain, 7);
            var p = Seen(40f);
            p.ChargeOrdered = true;
            RiflemanDecision d = brain.Step(Dt, p);
            Assert.That(d.State, Is.EqualTo(RiflemanState.Charge));
            Assert.That(d.MoveToTarget, Is.True);

            // Descargado y cerca: carga en vez de recargar.
            var brain2 = new RiflemanBrain(Comblain, 8);
            Assert.That(brain2.Step(Dt, Seen(12f, loaded: false)).State, Is.EqualTo(RiflemanState.Charge));
            // Sin cartuchos y a la vista: también.
            var empty = Seen(100f, loaded: false);
            empty.HasAmmo = false;
            Assert.That(brain2.Step(Dt, empty).State, Is.EqualTo(RiflemanState.Charge));

            int strikes = 0;
            for (float t = 0f; t < 3f; t += Dt)
            {
                if (brain.Step(Dt, Seen(1.5f)).Strike) strikes++;
            }
            Assert.That(strikes, Is.EqualTo(3), "un golpe cada 1,1 s");
        }

        [Test]
        public void Supresion_RetrasaElDisparoYLoHaceArrodillarse()
        {
            float calm = TimeToFirstShot(new RiflemanBrain(Comblain, 3), Seen(150f));
            var pinned = Seen(150f);
            pinned.Suppression = 0.8f;
            var brain = new RiflemanBrain(Comblain, 3);
            Assert.That(brain.Step(Dt, pinned).Kneel, Is.True);
            float slow = TimeToFirstShot(brain, pinned);
            Assert.That(slow, Is.GreaterThan(calm * 1.4f));
            Assert.That(RiflemanBrain.SigmaDeg(Comblain, false, 0.8f), Is.GreaterThan(RiflemanBrain.SigmaDeg(Comblain, false, 0f) * 2f));
        }

        /// <summary>El tiro de la IA (bala a bala) acierta con la probabilidad del modelo de fuego de escuadras.</summary>
        [Test]
        public void Punteria_CoincideConElModeloDeFuego()
        {
            var brain = new RiflemanBrain(Comblain, 11);
            const float distance = 100f;
            float sigma = RiflemanBrain.SigmaDeg(Comblain, false, 0f);
            int hits = 0;
            const int n = 40000;
            float halfW = FireModel.SilhouetteWidthM(Posture.Standing) / 2f, halfH = FireModel.SilhouetteHeightM(Posture.Standing) / 2f;
            for (int i = 0; i < n; i++)
            {
                ShotDirection s = brain.SampleShot(sigma);
                float x = distance * (float)Math.Tan(s.YawDeg * Math.PI / 180.0);
                float y = distance * (float)Math.Tan(s.PitchDeg * Math.PI / 180.0);
                if (Math.Abs(x) <= halfW && Math.Abs(y) <= halfH) hits++;
            }
            float expected = FireModel.HitProbability(Comblain, distance, Posture.Standing);
            Assert.That(hits / (float)n, Is.EqualTo(expected).Within(0.02f));
        }

        [Test]
        public void Determinista()
        {
            var a = new RiflemanBrain(Comblain, 42);
            var b = new RiflemanBrain(Comblain, 42);
            Assert.That(TimeToFirstShot(a, Seen(200f)), Is.EqualTo(TimeToFirstShot(b, Seen(200f))));
            Assert.That(a.SampleShot(1f).YawDeg, Is.EqualTo(b.SampleShot(1f).YawDeg));
        }

        // ------------------------------------------------------------------------------------------
        // Salud
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Salud_BalazoDeCercaDejaFueraDeCombate()
        {
            var v = new Vitality();
            int downed = 0;
            v.Downed += () => downed++;
            float damage = Comblain.DamageAtDistance(50f);
            v.ApplyDamage(damage * Vitality.HeadMultiplier);
            Assert.That(v.IsDown, Is.True, "a la cabeza, a 50 m");
            Assert.That(v.ApplyDamage(10f), Is.False);
            Assert.That(downed, Is.EqualTo(1));
        }

        [Test]
        public void Salud_ElJugadorSeRecobraSoloHastaUnTope()
        {
            var v = new Vitality { RecoveryPerSecond = 5f };
            v.ApplyDamage(80f);
            for (int i = 0; i < 5 * 60; i++) v.Step(Dt);
            Assert.That(v.Health, Is.EqualTo(20f).Within(1e-3f), "antes de 6 s sin fuego, nada");
            for (int i = 0; i < 30 * 60; i++) v.Step(Dt);
            Assert.That(v.Fraction, Is.EqualTo(v.RecoveryCap).Within(1e-3f));
            v.ApplyDamage(1f);
            Assert.That(v.SinceDamageSeconds, Is.Zero);
        }
    }
}
