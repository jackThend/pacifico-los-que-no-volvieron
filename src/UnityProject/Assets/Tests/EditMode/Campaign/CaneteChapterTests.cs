using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using F = Pacifico.Core.Campaign.CaneteChapter.Facts;
using G = Pacifico.Core.Campaign.CaneteChapter.Flags;
using P = Pacifico.Core.Campaign.CaneteChapter.Perspectives;
using S = Pacifico.Core.Campaign.CaneteChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.9 — Capítulo 7, Cañete: los torpedos de tierra, el herido y el asalto a la hacienda.</summary>
    public class CaneteChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionScript Chapter() => CaneteChapter.Build(MissionTests.Read);

        private static float RunUntil(MissionRunner runner, Func<bool> until, float maxSeconds = 900f)
        {
            float t = 0f;
            while (!until() && t < maxSeconds && runner.State == MissionState.Running)
            {
                runner.Step(Dt);
                t += Dt;
            }
            return t;
        }

        private static void Wait(MissionRunner runner, float seconds) => RunUntil(runner, () => false, seconds);

        [Test]
        public void Guion_EsValidoYElPrologoEsLiteral()
        {
            MissionScript script = Chapter();
            Assert.That(script.Validate(), Is.Empty);
            Assert.That(script.Id, Is.EqualTo(CampaignCatalog.Get(CaneteChapter.Id).Id));
            Assert.That(script.RewardCollectibleId, Is.Null, "el contrato de Quintín no está en el Archivo");
            GuionQuotes guion = GuionQuotes.Load(MissionTests.Read, CaneteChapter.ChapterHeading);
            string prologue = guion.QuotedField("Prólogo del Corresponsal");
            StringAssert.StartsWith("En las haciendas costeñas del Perú", prologue);
            StringAssert.EndsWith("comprar su dignidad con sangre.", prologue);
            Assert.That(prologue.IndexOfAny(new[] { '"', '*' }), Is.EqualTo(-1));
            Assert.That(script.Stage(S.Prologue).OnEnter.Single(l => l.Speaker == CaneteChapter.Correspondent).Text, Is.EqualTo(prologue));
            // El mismo lector sirve para el prólogo de Pisagua.
            Assert.That(PisaguaChapter.Prologue(GuionQuotes.Load(MissionTests.Read, PisaguaChapter.ChapterHeading)), Does.StartWith("Pisagua parecía"));
        }

        [Test]
        public void Machete_EsUnArmaBlancaValidaMasLargaQueElCorvo()
        {
            WeaponSpec machete = WeaponCatalog.Get(WeaponCatalog.MacheteId);
            Assert.That(machete.Validate().IsValid, Is.True, machete.Validate().ToString());
            Assert.That(machete.IsFirearm, Is.False);
            Assert.That(machete.MeleeReachM, Is.GreaterThan(WeaponCatalog.Corvo().MeleeReachM));
        }

        [Test]
        public void Torpedo_SoloLoVeQuienTanteaAgachadoYSeCortaConPulsoFirme()
        {
            var wire = new Tripwire(-2f, 10f, 2f, 10f);
            wire.Probe(0f, 7f, crouched: false);
            Assert.That(wire.Revealed, Is.False, "de pie y de noche no se ve");
            Assert.That(wire.StepDisarm(5f, true, 0f, 10.5f, 0f, true), Is.False, "sin descubrir no se corta");
            wire.Probe(0f, 7f, crouched: true);
            Assert.That(wire.Revealed, Is.True);

            // Moverse o soltar la acción echa a perder el corte.
            wire.StepDisarm(2f, true, 0f, 10.5f, 0f, true);
            Assert.That(wire.Progress, Is.GreaterThan(0.5f));
            wire.StepDisarm(0.1f, true, 0f, 10.5f, 1.2f, true);
            Assert.That(wire.Progress, Is.Zero, "se le fue el pulso");
            float t = 0f;
            while (!wire.StepDisarm(0.1f, true, 0f, 10.5f, 0f, true)) t += 0.1f;
            Assert.That(t + 0.1f, Is.EqualTo(Tripwire.DisarmSeconds).Within(0.11f));
            Assert.That(wire.State, Is.EqualTo(TripwireState.Disarmed));
            Assert.That(wire.Crossed(0f, 8f, 0f, 12f), Is.False, "cortado, ya se puede pasar");

            // Cruzar un alambre armado lo hace estallar; pasar al lado, no.
            var live = new Tripwire(-2f, 0f, 2f, 0f);
            Assert.That(live.Crossed(3f, -1f, 3f, 1f), Is.False);
            Assert.That(live.Crossed(0f, -0.3f, 0.1f, 0.3f), Is.True);
            Assert.That(live.State, Is.EqualTo(TripwireState.Exploded));
            Assert.That(live.IsLethalAt(0f, 6f), Is.True);
            Assert.That(live.IsLethalAt(0f, 9f), Is.False);
        }

        [Test]
        public void Herido_SeCargaSeLlevaYSePuedeRematarOPerder()
        {
            var wounded = new WoundedComrade(100f);
            wounded.Step(1f, true, 5f, 99f);
            Assert.That(wounded.Carried, Is.False, "demasiado lejos");
            for (int i = 0; i < 20; i++) wounded.Step(0.1f, true, 1f, 99f);
            Assert.That(wounded.Carried, Is.True);
            float before = wounded.Remaining;
            wounded.Step(10f, false, 0f, 99f);
            Assert.That(before - wounded.Remaining, Is.EqualTo(10f * WoundedComrade.CarriedBleedFactor).Within(1e-3f), "cargado sangra menos");
            wounded.SetDownSafe();
            Assert.That(wounded.Safe, Is.True);

            var finished = new WoundedComrade();
            for (int i = 0; i < 40; i++) finished.Step(0.1f, false, 50f, 1f);
            Assert.That(finished.Killed, Is.True, "un enemigo encima lo remata");

            var bled = new WoundedComrade(5f);
            bled.Step(6f, false, 50f, 99f);
            Assert.That(bled.BledOut, Is.True);
        }

        /// <summary>Partida completa: cinco alambres, la patrulla, el herido a salvo, la Winchester, el portón y el amanecer.</summary>
        [Test]
        public void Partida_DesenlaceDelGuion()
        {
            var runner = new MissionRunner(Chapter());
            var perspectives = new List<string> { runner.CurrentStage.Perspective };
            runner.Event += e =>
            {
                if (e.Kind == MissionEventKind.StageStarted) perspectives.Add(runner.CurrentStage.Perspective);
            };
            RunUntil(runner, () => runner.CurrentStage.Id == S.Silence);
            for (int i = 0; i < CaneteChapter.Mines; i++)
            {
                Wait(runner, 25f);
                runner.Facts.Add(F.MinesDisarmed);
            }
            RunUntil(runner, () => runner.CurrentStage.Id == S.Blood);
            RunUntil(runner, () => runner.Facts.Flag(G.PatrolBlast), 5f);
            Assert.That(runner.Facts.Flag(G.PatrolBlast), Is.True);
            Wait(runner, 20f);
            runner.Facts.Add(F.FinishersDown, 2);
            runner.Facts.SetFlag(G.WoundedCarried);
            Wait(runner, 25f);
            runner.Facts.SetFlag(G.WoundedSafe);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Assault);
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.AlliesJoin), Is.True);
            runner.Facts.SetFlag(G.WinchesterTaken);
            runner.Facts.SetFlag(G.DynamiteTaken);
            Wait(runner, 30f);
            runner.Facts.SetFlag(G.GateBlown);
            Wait(runner, 40f);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Assault), "falta el nido");
            runner.Facts.Add(F.DefendersDown, CaneteChapter.HaciendaDefenders);
            RunUntil(runner, () => runner.State != MissionState.Running);
            Assert.That(runner.State, Is.EqualTo(MissionState.Complete));
            Assert.That(perspectives, Is.EqualTo(new[] { P.Plate, P.Quintin, P.Quintin, P.Quintin, P.Quintin }));
        }

        [TestCase(G.MineTriggered, "alambre")]
        [TestCase(G.WoundedKilled, "rematado")]
        [TestCase(G.WoundedBledOut, "desangrado")]
        [TestCase(G.PlayerDown, "caído")]
        public void Partida_Fracasos(string flag, string reason)
        {
            var runner = new MissionRunner(Chapter());
            Wait(runner, 2f);
            runner.Facts.SetFlag(flag);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains(reason, runner.FailureReason);
        }
    }
}
