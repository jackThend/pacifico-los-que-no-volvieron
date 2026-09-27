using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Tactics
{
    /// <summary>ROADMAP 4.2 — supresión por volumen de fuego y cobertura.</summary>
    public class SuppressionTests
    {
        private static readonly WeaponSpec Comblain = WeaponCatalog.Comblain();

        /// <summary>Disparos por segundo de una escuadra de 12 Comblain a discreción.</summary>
        private static float SquadRate(int soldiers = 12) => soldiers / new SquadFireControl(Comblain, soldiers).CycleSeconds;

        /// <summary>Fuego de «squads» escuadras (solo balas que silban, sin impactos) durante «seconds».</summary>
        private static List<SuppressionState> Barrage(SuppressionModel model, int squads, float distance, float seconds, float cover = 0f)
        {
            var states = new List<SuppressionState>();
            var fire = Enumerable.Range(0, squads).Select(i => new SquadFireControl(Comblain, 12, seed: 100 + i)).ToList();
            for (float t = 0f; t < seconds; t += 0.05f)
            {
                int shots = fire.Sum(f => f.Step(0.05f, true, distance, Posture.Standing).Shots);
                model.ReceiveFire(shots, 0, 0, distance, Comblain.EffectiveRangeM, cover);
                model.Step(0.05f);
                states.Add(model.State);
            }
            return states;
        }

        [Test]
        public void NivelDeEquilibrio_UnaEscuadraPresiona_DosSuprimen()
        {
            float one = SuppressionModel.SteadyLevel(SquadRate(), 200f, Comblain.EffectiveRangeM);
            float two = SuppressionModel.SteadyLevel(2f * SquadRate(), 200f, Comblain.EffectiveRangeM);
            Assert.That(one, Is.InRange(SuppressionModel.PinEnter, SuppressionModel.SuppressEnter), "una escuadra: presionada");
            Assert.That(two, Is.GreaterThan(SuppressionModel.SuppressEnter), "fuego concentrado: suprimida");
        }

        /// <summary>Verificación del roadmap: bajo volumen de fuego, el estado pasa a «Suprimido».</summary>
        [Test]
        public void FuegoConcentrado_PasaASuprimido()
        {
            var model = new SuppressionModel();
            List<SuppressionState> states = Barrage(model, squads: 2, distance: 200f, seconds: 30f);
            int first = states.IndexOf(SuppressionState.Suppressed);
            Assert.That(first, Is.GreaterThanOrEqualTo(0), "llega a Suprimido");
            Assert.That(first * 0.05f, Is.LessThan(15f), "en segundos, no en minutos");
            Assert.That(states.IndexOf(SuppressionState.Pinned), Is.LessThan(first), "antes pasa por Presionado");
            Assert.That(states.Skip(first + 100).All(s => s == SuppressionState.Suppressed), Is.True, "y sigue suprimida mientras dura el fuego");
        }

        [Test]
        public void UnaSolaEscuadra_PresionaPeroNoSuprime()
        {
            var model = new SuppressionModel();
            List<SuppressionState> states = Barrage(model, squads: 1, distance: 200f, seconds: 60f);
            Assert.That(states.Contains(SuppressionState.Suppressed), Is.False);
            Assert.That(states.Skip(states.Count / 2).All(s => s == SuppressionState.Pinned), Is.True, "presionada de forma estable");
        }

        [Test]
        public void LaZanja_Protege()
        {
            var open = new SuppressionModel();
            var trench = new SuppressionModel();
            Barrage(open, 2, 250f, 30f);
            Barrage(trench, 2, 250f, 30f, cover: 0.8f);
            Assert.That(trench.Level, Is.LessThan(open.Level * 0.7f));
        }

        [Test]
        public void Atrincherada_AguantaDosEscuadras_NoTres()
        {
            var two = new SuppressionModel();
            var three = new SuppressionModel();
            Assert.That(Barrage(two, 2, 200f, 120f, cover: 0.8f).Contains(SuppressionState.Suppressed), Is.False, "dos escuadras solo la presionan");
            Assert.That(Barrage(three, 3, 200f, 60f, cover: 0.8f).Contains(SuppressionState.Suppressed), Is.True, "hay que concentrar tres");
        }

        [Test]
        public void AlCesarElFuego_SeRecuperaPorEtapas()
        {
            var model = new SuppressionModel();
            model.ReceiveFire(40, 0, 0, 50f, 350f); // ráfaga intensa
            Assert.That(model.State, Is.EqualTo(SuppressionState.Suppressed));
            float level = model.Level;
            float t = 0f, toPinned = -1f, toNormal = -1f;
            while (t < 60f)
            {
                model.Step(0.05f);
                t += 0.05f;
                if (toPinned < 0f && model.State == SuppressionState.Pinned) toPinned = t;
                if (toNormal < 0f && model.State == SuppressionState.Normal) toNormal = t;
            }
            float tau = SuppressionModel.RecoverySeconds;
            Assert.That(toPinned, Is.EqualTo(tau * (float)Math.Log(level / SuppressionModel.SuppressExit)).Within(0.1f));
            Assert.That(toNormal, Is.EqualTo(tau * (float)Math.Log(level / SuppressionModel.PinExit)).Within(0.1f));
        }

        [Test]
        public void Histeresis_SinParpadeoEnElUmbral()
        {
            var model = new SuppressionModel();
            model.ReceiveFire(1, 0, 0, 0f, 350f);
            // Se sube justo por encima del umbral de supresión y se deja oscilar ±0,05 alrededor de él.
            while (model.Level < SuppressionModel.SuppressEnter) model.ReceiveFire(1, 0, 0, 0f, 350f);
            int changes = 0;
            SuppressionState last = model.State;
            var rng = new Random(4);
            for (int i = 0; i < 2000; i++)
            {
                if (model.Level < SuppressionModel.SuppressEnter - 0.05f) model.ReceiveFire(2, 0, 0, 0f, 350f);
                model.Step(0.05f + (float)rng.NextDouble() * 0.05f);
                if (model.State != last) changes++;
                last = model.State;
            }
            Assert.That(changes, Is.EqualTo(0));
        }

        [Test]
        public void LasBajas_PesanMasQueLasBalas()
        {
            var a = new SuppressionModel();
            var b = new SuppressionModel();
            a.ReceiveFire(3, 0, 0, 200f, 350f);
            b.ReceiveFire(3, 1, 1, 200f, 350f);
            Assert.That(b.Level - a.Level, Is.EqualTo(SuppressionModel.PerHit + SuppressionModel.PerCasualty).Within(1e-5f));
            Assert.That(SuppressionModel.ProximityFactor(0f, 350f), Is.GreaterThan(SuppressionModel.ProximityFactor(600f, 350f)));
        }

        [Test]
        public void Efectos_DelEstado()
        {
            Assert.That(SuppressionModel.SpeedFactor(SuppressionState.Normal), Is.EqualTo(1f));
            Assert.That(SuppressionModel.SpeedFactor(SuppressionState.Suppressed), Is.LessThan(SuppressionModel.SpeedFactor(SuppressionState.Pinned)));
            Assert.That(SuppressionModel.PostureFor(SuppressionState.Suppressed), Is.EqualTo(Posture.Prone), "se tiende");
            Assert.That(SuppressionModel.PostureFor(SuppressionState.Pinned), Is.EqualTo(Posture.Kneeling));
            float calm = FireModel.HitProbability(Comblain, 200f, Posture.Standing);
            float shaken = FireModel.HitProbability(Comblain, 200f, Posture.Standing, 0f, SuppressionModel.AccuracyPenalty(SuppressionState.Suppressed));
            Assert.That(shaken, Is.LessThan(calm * 0.5f), "suprimida, su fuego apenas hace blanco");
        }

        /// <summary>
        /// Duelo completo con impactos y bajas: una escuadra enemiga que avanza a descubierto bajo el fuego de dos
        /// escuadras a 250 m queda suprimida, se tiende y apenas avanza.
        /// </summary>
        [Test]
        public void AvanceBajoElFuego_SeFrenaYSeTiende()
        {
            SquadCommand Advance()
            {
                var soldiers = Formation.LocalSlots(FormationType.Line, 12).ToList();
                var c = new SquadCommand(FormationType.Line, soldiers, Vec3.Zero, new Vec3(0f, 0f, 1f));
                c.MoveTo(new List<Vec3> { new Vec3(0f, 0f, 200f) }, new Vec3(0f, 0f, 1f), soldiers);
                return c;
            }

            SquadCommand calm = Advance(), underFire = Advance();
            var suppression = new SuppressionModel();
            var shooters = new[] { new SquadFireControl(Comblain, 12, 1), new SquadFireControl(Comblain, 12, 2) };
            var soldiersCalm = Formation.LocalSlots(FormationType.Line, 12).ToList();
            var soldiersFire = Formation.LocalSlots(FormationType.Line, 12).ToList();
            float suppressedAt = -1f;
            for (float t = 0f; t < 40f; t += 0.05f)
            {
                Posture posture = SuppressionModel.PostureFor(suppression.State);
                int shots = 0, hits = 0, casualties = 0;
                foreach (SquadFireControl f in shooters)
                {
                    VolleyResult r = f.Step(0.05f, true, 250f, posture);
                    shots += r.Shots;
                    hits += r.Hits;
                    casualties += r.Casualties;
                }
                suppression.ReceiveFire(shots, hits, casualties, 250f, Comblain.EffectiveRangeM);
                suppression.Step(0.05f);
                if (suppressedAt < 0f && suppression.State == SuppressionState.Suppressed) suppressedAt = t;

                underFire.March.SpeedFactor = SuppressionModel.SpeedFactor(suppression.State);
                Step(calm, soldiersCalm, 0.05f);
                Step(underFire, soldiersFire, 0.05f);
            }
            Assert.That(suppressedAt, Is.InRange(0f, 15f), "suprimida en " + suppressedAt.ToString("0.0") + " s");
            Assert.That(underFire.March.Anchor.Z, Is.LessThan(calm.March.Anchor.Z * 0.5f), "avanza menos de la mitad");
            Assert.That(SuppressionModel.PostureFor(suppression.State), Is.EqualTo(Posture.Prone));
        }

        private static void Step(SquadCommand c, List<Vec3> soldiers, float dt)
        {
            c.Step(dt, soldiers);
            for (int i = 0; i < soldiers.Count; i++)
            {
                Vec3 slot = c.SlotOf(i);
                Vec3 to = slot - soldiers[i];
                float d = to.Magnitude, step = c.FollowSpeed(d) * dt;
                soldiers[i] = d <= step ? slot : soldiers[i] + to / d * step;
            }
        }

        [Test]
        public void Suprimida_DisparaMenos()
        {
            int Shots(float rate)
            {
                var f = new SquadFireControl(Comblain, 12, 5);
                int n = 0;
                for (float t = 0f; t < 300f; t += 0.05f) n += f.Step(0.05f, true, 200f, Posture.Standing, 0f, 1f, rate).Shots;
                return n;
            }
            int normal = Shots(1f);
            int suppressed = Shots(SuppressionModel.RateFactor(SuppressionState.Suppressed));
            Assert.That(suppressed, Is.EqualTo(normal * 0.4f).Within(normal * 0.05f));
        }
    }

    /// <summary>ROADMAP 4.2 — reparto de puestos a cubierto (zanjas y parapetos).</summary>
    public class CoverTests
    {
        private static readonly Vec3 South = new Vec3(0f, 0f, -1f);

        /// <summary>Parapeto de 8 m con puestos cada metro, protegiendo hacia el sur.</summary>
        private static List<CoverSpot> Parapet(float z = 0f, int count = 8, float protection = 0.7f)
        {
            return Enumerable.Range(0, count).Select(i => new CoverSpot(new Vec3(i - (count - 1) / 2f, 0f, z), South, protection)).ToList();
        }

        [Test]
        public void SeRepartenLosPuestosQueProtegenDelEnemigo()
        {
            var soldiers = Formation.LocalSlots(FormationType.Line, 8).Select(s => s + new Vec3(0f, 0f, 6f)).ToList();
            List<CoverSpot> spots = Parapet();
            int[] a = CoverSelector.Assign(soldiers, new Vec3(0f, 0f, 6f), spots, South);
            Assert.That(a.All(i => i >= 0), Is.True, "hay puesto para todos");
            Assert.That(a.Distinct().Count(), Is.EqualTo(8), "uno por puesto");
            // Cada uno al puesto que tiene delante (sin cruzarse).
            for (int i = 0; i < 8; i++) Assert.That(spots[a[i]].Position.X, Is.EqualTo(soldiers[i].X).Within(1e-4f));
            Assert.That(CoverSelector.SquadCover(a, spots, South), Is.EqualTo(0.7f).Within(1e-4f));
        }

        [Test]
        public void UnParapetoNoProtegeDelFuegoQueLlegaPorDetras()
        {
            var soldiers = new List<Vec3> { new Vec3(0f, 0f, 3f) };
            int[] a = CoverSelector.Assign(soldiers, soldiers[0], Parapet(), new Vec3(0f, 0f, 1f));
            Assert.That(a[0], Is.EqualTo(-1));
            Assert.That(Parapet()[0].ProtectionAgainst(new Vec3(1f, 0f, 0f)), Is.EqualTo(0f), "ni de lado");
        }

        [Test]
        public void MasHombresQuePuestos_LosDemasSeTiendenDondeEstan()
        {
            var soldiers = Formation.LocalSlots(FormationType.Line, 12).Select(s => s + new Vec3(0f, 0f, 4f)).ToList();
            int[] a = CoverSelector.Assign(soldiers, new Vec3(0f, 0f, 4f), Parapet(count: 5), South);
            Assert.That(a.Count(i => i >= 0), Is.EqualTo(5));
            Assert.That(a.Where(i => i >= 0).Distinct().Count(), Is.EqualTo(5));
            Assert.That(CoverSelector.SquadCover(a, Parapet(count: 5), South), Is.EqualTo(0.7f * 5f / 12f).Within(1e-4f));
        }

        [Test]
        public void PuestosLejanosUOcupados_NoCuentan()
        {
            var soldiers = new List<Vec3> { Vec3.Zero, new Vec3(1f, 0f, 0f) };
            Assert.That(CoverSelector.Assign(soldiers, Vec3.Zero, Parapet(z: -40f), South).All(i => i == -1), Is.True, "a 40 m");
            List<CoverSpot> spots = Parapet(z: -3f, count: 3);
            int[] a = CoverSelector.Assign(soldiers, Vec3.Zero, spots, South, new HashSet<int> { 0, 1 });
            Assert.That(a.Count(i => i == 2), Is.EqualTo(1), "solo queda el puesto libre");
            Assert.That(a.Count(i => i == -1), Is.EqualTo(1));
        }

        [Test]
        public void AIgualdad_ElMejorPuesto()
        {
            var soldiers = new List<Vec3> { Vec3.Zero };
            var spots = new List<CoverSpot>
            {
                new CoverSpot(new Vec3(-3f, 0f, -2f), South, 0.4f), // parapeto bajo
                new CoverSpot(new Vec3(3f, 0f, -2f), South, 0.85f),  // zanja, a la misma distancia
            };
            Assert.That(CoverSelector.Assign(soldiers, Vec3.Zero, spots, South)[0], Is.EqualTo(1));
        }
    }
}
