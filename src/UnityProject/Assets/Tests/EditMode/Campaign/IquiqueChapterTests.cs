using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Narrative;
using Pacifico.Core.Naval;
using F = Pacifico.Core.Campaign.IquiqueChapter.Facts;
using G = Pacifico.Core.Campaign.IquiqueChapter.Flags;
using S = Pacifico.Core.Campaign.IquiqueChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.1 — Capítulo 1, rada de Iquique: el guion de la misión y el balanceo de la batería.</summary>
    public class IquiqueChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionScript Chapter() => IquiqueChapter.Build(MissionTests.Read);

        /// <summary>Avanza hasta que se cumple <paramref name="until"/> (o se agota el tiempo) y devuelve los segundos.</summary>
        private static float RunUntil(MissionRunner runner, System.Func<bool> until, float maxSeconds = 600f)
        {
            float t = 0f;
            while (!until() && t < maxSeconds && runner.State == MissionState.Running)
            {
                runner.Step(Dt);
                t += Dt;
            }
            return t;
        }

        [Test]
        public void Guion_EsValidoYSusCitasSonLiteralesDelGuion()
        {
            MissionScript script = Chapter();
            Assert.That(script.Validate(), Is.Empty);
            GuionQuotes guion = GuionQuotes.Load(MissionTests.Read, IquiqueChapter.ChapterHeading);
            var quotes = script.Stages.SelectMany(s => s.OnEnter.Concat(s.Triggers.SelectMany(t => t.Lines))).Where(l => l.Kind == LineKind.Quote).ToList();
            Assert.That(quotes.Count, Is.GreaterThanOrEqualTo(5));
            foreach (MissionLine line in quotes) Assert.That(guion.Contains(line.Text), Is.True, line.ToString());
            Assert.That(quotes.Count(l => l.Speaker == IquiqueChapter.Prat), Is.EqualTo(2));
            Assert.That(quotes.Count(l => l.Speaker == IquiqueChapter.Grau), Is.GreaterThanOrEqualTo(2));
            Assert.That(script.RewardCollectibleId, Is.EqualTo(CollectibleCatalog.GrauLetterId));
        }

        /// <summary>
        /// Partida completa con el desenlace histórico: tres andanadas de la Esmeralda, primer espolonazo (Prat al
        /// abordaje), la torre del Huáscar alcanza la flotación, tercer espolonazo y hundimiento, rescate y carta.
        /// </summary>
        [Test]
        public void Partida_DesenlaceHistorico()
        {
            var runner = new MissionRunner(Chapter());
            var perspectives = new List<string> { runner.CurrentStage.Perspective };
            var said = new List<string>();
            runner.Event += e =>
            {
                if (e.Kind == MissionEventKind.StageStarted) perspectives.Add(runner.CurrentStage.Perspective);
                if (e.Kind == MissionEventKind.LineStarted && e.Line.Kind == LineKind.Quote) said.Add(e.Line.Speaker + ": " + e.Line.Text);
            };

            // Acto I: tres andanadas, espaciadas por la recarga.
            for (int i = 0; i < 3; i++)
            {
                RunUntil(runner, () => false, 9f);
                runner.Facts.Add(F.Broadsides);
            }
            runner.Facts.Add(F.BroadsideHits);
            runner.Facts.Add(F.ShoreHits);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Ram);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Ram));
            Assert.That(said[0], Does.StartWith("Arturo Prat: ¡Muchachos"), "la arenga antes del espolón");

            // Primer espolonazo: «¡Al abordaje, muchachos!» y cambio de perspectiva al terminar el diálogo.
            RunUntil(runner, () => false, 20f);
            runner.Facts.Add(F.Rams);
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.PratBoards), Is.True);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Ram), "no se corta a Prat");
            float wait = RunUntil(runner, () => runner.CurrentStage.Id == S.Turret);
            Assert.That(wait, Is.InRange(8f, 30f));
            Assert.That(said.Last(), Is.EqualTo("Arturo Prat: ¡Al abordaje, muchachos!"));

            // Acto II: la torre alcanza la flotación → tercer espolonazo.
            RunUntil(runner, () => false, 15f);
            runner.Facts.Add(F.TurretShots);
            runner.Facts.Add(F.TurretHits);
            runner.Facts.Add(F.WaterlineHits);
            runner.Step(Dt);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.ThirdRam));
            Assert.That(said.Any(s => s.StartsWith("Miguel Grau: ¡Fuego a la línea de flotación!")), Is.True);

            runner.Facts.Add(F.Rams);
            RunUntil(runner, () => false, 20f);
            Assert.That(runner.Facts.Flag(G.FounderEsmeralda), Is.False, "dos espolonazos no bastan en el guion");
            runner.Facts.Add(F.Rams);
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.FounderEsmeralda), Is.True, "tercer espolonazo: la escena la echa a pique");
            RunUntil(runner, () => false, 3f);
            runner.Facts.SetFlag(G.EsmeraldaSunk);
            runner.Step(Dt);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Survivors));

            // Rescate.
            for (int i = 0; i < IquiqueChapter.DefaultSurvivorGroups; i++)
            {
                RunUntil(runner, () => false, 12f);
                runner.Facts.Add(F.SurvivorsRescued);
            }
            RunUntil(runner, () => runner.CurrentStage.Id == S.Letter);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Letter));
            Assert.That(said.Any(s => s.StartsWith("Miguel Grau: ¡Fuego no! ¡Arriad los botes")), Is.True);

            runner.Facts.SetFlag(G.DocumentClosed);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Complete));
            Assert.That(runner.History.Last().Detail, Is.EqualTo(CollectibleCatalog.GrauLetterId), "desbloquea la carta");
            Assert.That(runner.Visited, Is.EqualTo(new[] { S.WoodenDeck, S.Ram, S.Turret, S.ThirdRam, S.Survivors, S.Letter }));
            Assert.That(perspectives, Is.EqualTo(new[]
            {
                IquiqueChapter.Perspectives.EsmeraldaBattery, IquiqueChapter.Perspectives.EsmeraldaBattery,
                IquiqueChapter.Perspectives.HuascarTurret, IquiqueChapter.Perspectives.HuascarTurret,
                IquiqueChapter.Perspectives.HuascarBridge, IquiqueChapter.Perspectives.Document,
            }));
        }

        [Test]
        public void Partida_SinDisparar_LaHistoriaSigue()
        {
            var runner = new MissionRunner(Chapter());
            float t = RunUntil(runner, () => runner.CurrentStage.Id == S.Ram);
            Assert.That(t, Is.EqualTo(IquiqueChapter.BroadsideStageTimeoutSeconds).Within(0.1f));
            runner.Facts.Add(F.Rams);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Turret);
            t = RunUntil(runner, () => runner.CurrentStage.Id == S.ThirdRam);
            Assert.That(t, Is.EqualTo(IquiqueChapter.TurretStageTimeoutSeconds).Within(0.1f));
        }

        [Test]
        public void Partida_HundidaAntesDeTiempo_SaltaAlRescate()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.Add(F.Rams);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Turret);
            runner.Facts.SetFlag(G.EsmeraldaSunk); // la torre la hunde sin más espolonazos
            runner.Step(Dt);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Survivors));
            Assert.That(runner.State, Is.EqualTo(MissionState.Running));
        }

        [Test]
        public void Partida_DispararALosNaufragos_ReprimendaYRelevo()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.SetFlag(G.EsmeraldaSunk);
            runner.Step(Dt);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Survivors));

            runner.Facts.Add(F.ShotsAfterSinking);
            runner.Step(Dt);
            Assert.That(runner.Dialogue.Current.Speaker, Is.EqualTo(IquiqueChapter.Grau), "la reprimenda interrumpe");
            Assert.That(runner.Dialogue.Current.Text, Is.EqualTo("¡Fuego no!"));
            Assert.That(runner.State, Is.EqualTo(MissionState.Running), "un disparo no es relevo");

            runner.Facts.Add(F.ShotsAfterSinking, IquiqueChapter.ShotsAfterSinkingToFail - 1);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("náufragos", runner.FailureReason);
        }

        [Test]
        public void Partida_HuascarHundido_Fracaso()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.SetFlag(G.HuascarSunk);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
        }

        // ------------------------------------------------------------------------------------------
        // Balanceo de la cubierta de la Esmeralda
        // ------------------------------------------------------------------------------------------

        private const float V40 = 360f; // pieza Armstrong de 40 lb (ShipCatalog)

        [Test]
        public void Balanceo_ConCubiertaHorizontalSeAciertaYEnElExtremoNo()
        {
            var roll = new DeckRollModel();
            Assert.That(roll.AngleDeg, Is.EqualTo(0f), "empieza horizontal");
            float level = DeckRollModel.MissHeight(V40, 500f, roll.ElevationErrorDeg(BroadsideSide.Starboard));
            Assert.That(level, Is.EqualTo(0f).Within(0.05f));

            roll.Step(DeckRollModel.DefaultPeriodSeconds * 0.25f); // estribor arriba al máximo
            Assert.That(roll.AngleDeg, Is.EqualTo(DeckRollModel.DefaultAmplitudeDeg).Within(1e-3f));
            float high = DeckRollModel.MissHeight(V40, 500f, roll.ElevationErrorDeg(BroadsideSide.Starboard));
            float low = DeckRollModel.MissHeight(V40, 500f, roll.ElevationErrorDeg(BroadsideSide.Port));
            Assert.That(high, Is.GreaterThan(10f), "estribor alzado: pasa por encima");
            Assert.That(low, Is.LessThan(-10f), "babor hundido: cae corto");
            Assert.That(DeckRollModel.FallRange(V40, 500f, roll.ElevationErrorDeg(BroadsideSide.Starboard)), Is.GreaterThan(900f));
            Assert.That(DeckRollModel.FallRange(V40, 500f, roll.ElevationErrorDeg(BroadsideSide.Port)), Is.LessThan(100f));
        }

        [Test]
        public void Balanceo_VentanaDeTiroExigentePeroJugable()
        {
            var roll = new DeckRollModel();
            // Obra muerta del Huáscar: el blanco vertical son ~2 m bajo y ~3 m sobre la cota de puntería.
            float window = roll.HitWindowFraction(V40, 500f, 2f, 3f);
            float windowSeconds = window * DeckRollModel.DefaultPeriodSeconds / 2f; // dos pasos por la horizontal por ciclo
            Assert.That(window, Is.InRange(0.05f, 0.2f), "ni automático ni imposible");
            Assert.That(windowSeconds, Is.GreaterThan(0.25f), "más que el tiempo de reacción de una persona atenta");
            Assert.That(roll.HitWindowFraction(V40, 1500f, 2f, 3f), Is.LessThan(window), "de lejos, más difícil");
        }

        [Test]
        public void Balanceo_IndependienteDelPaso()
        {
            var a = new DeckRollModel(phase: 0.1f);
            var b = new DeckRollModel(phase: 0.1f);
            for (int i = 0; i < 600; i++) a.Step(1f / 60f);
            for (int i = 0; i < 70; i++) b.Step(10f / 70f);
            Assert.That(a.AngleDeg, Is.EqualTo(b.AngleDeg).Within(1e-3f));
            Assert.That(a.RateDegPerSecond, Is.EqualTo(b.RateDegPerSecond).Within(1e-3f));
        }
    

        // ------------------------------------------------------------------------------------------
        // Rescate, desenlace forzado y progreso
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Rescate_HayQueQuedarseCercaYCasiParado()
        {
            var rescue = new SurvivorRescue();
            Assert.That(rescue.Blocker(100f, 0f), Does.StartWith("Acércate"));
            Assert.That(rescue.Blocker(20f, 4f), Does.Contain("arrancada"));
            Assert.That(rescue.Blocker(20f, 1f), Is.Null);

            // A seis nudos al lado de los náufragos no se rescata a nadie.
            for (int i = 0; i < 300; i++) Assert.That(rescue.Step(Dt, 20f, 3f), Is.False);
            Assert.That(rescue.Progress, Is.Zero);

            int steps = 0;
            while (!rescue.Step(Dt, 20f, 0.5f)) steps++;
            Assert.That((steps + 1) * Dt, Is.EqualTo(SurvivorRescue.DefaultHoldSeconds).Within(Dt * 1.5f));
            Assert.That(rescue.Rescued, Is.True);
            Assert.That(rescue.Step(Dt, 20f, 0.5f), Is.False, "se completa una sola vez");
        }

        [Test]
        public void Rescate_AlejarseDeshaceLaManiobraPocoAPoco()
        {
            var rescue = new SurvivorRescue();
            rescue.Step(SurvivorRescue.DefaultHoldSeconds * 0.5f, 10f, 0f);
            rescue.Step(1f, 200f, 0f);
            float expected = 0.5f - 1f / SurvivorRescue.DefaultHoldSeconds * SurvivorRescue.DecayFactor;
            Assert.That(rescue.Progress, Is.EqualTo(expected).Within(1e-5f));
            rescue.Step(100f, 200f, 0f);
            Assert.That(rescue.Progress, Is.Zero);
        }

        [Test]
        public void Hundimiento_ForzadoPorElGuion()
        {
            var state = new ShipDamageState(ShipCatalog.Esmeralda());
            int sunk = 0;
            state.Sunk += () => sunk++;
            state.Founder();
            state.Founder();
            Assert.That(state.IsSunk, Is.True);
            Assert.That(sunk, Is.EqualTo(1));
            Assert.That(state.FloodFraction, Is.EqualTo(1f));
        }

        /// <summary>
        /// Sin el guion, el modelo de daños hunde la Esmeralda al primer espolonazo a toda máquina (la simulación de
        /// la IA lo mostró: 25 s de inundación). Con KeepAfloat aguanta dos espolonazos y se va a pique en el tercero.
        /// </summary>
        [Test]
        public void Hundimiento_ElGuionLaMantieneAFloteHastaElTercerEspolonazo()
        {
            ShipSpec huascar = ShipCatalog.Huascar(), esmeralda = ShipCatalog.Esmeralda();
            RamResult ram = RamModel.Resolve(huascar, 30f, 6.5f, esmeralda, 270f, 0.5f);
            Assert.That(ram.Critical, Is.True);

            var free = new ShipDamageState(esmeralda);
            free.ApplyRamReceived(ram);
            for (int i = 0; i < 90 * 30 && !free.IsSunk; i++) free.Step(Dt);
            Assert.That(free.IsSunk, Is.True, "sin guion se hunde en minuto y medio");

            var scripted = new ShipDamageState(esmeralda) { KeepAfloat = true };
            for (int r = 0; r < 2; r++)
            {
                scripted.ApplyRamReceived(ram);
                for (int i = 0; i < 180 * 30; i++) scripted.Step(Dt);
            }
            Assert.That(scripted.IsSunk, Is.False, "dos espolonazos y tres minutos después, sigue a flote");
            Assert.That(scripted.FloodFraction, Is.LessThanOrEqualTo(ShipDamageState.KeepAfloatMaxFraction + 1e-4f));
            Assert.That(scripted.FloodFraction, Is.GreaterThan(0.5f), "pero muy escorada");
            scripted.ApplyRamReceived(ram);
            scripted.Founder();
            Assert.That(scripted.IsSunk, Is.True);
            Assert.That(scripted.KeepAfloat, Is.False);
        }

        [Test]
        public void Progreso_SeGuardaYSeRecupera()
        {
            var progress = new CampaignProgress();
            MissionScript chapter = Chapter();
            Assert.That(progress.Complete(chapter), Is.True, "coleccionable nuevo");
            Assert.That(progress.Complete(chapter), Is.False, "ya lo tenía");
            string saved = progress.Serialize();
            CampaignProgress loaded = CampaignProgress.Parse(saved);
            Assert.That(loaded.IsCompleted(IquiqueChapter.Id), Is.True);
            Assert.That(loaded.IsUnlocked(CollectibleCatalog.GrauLetterId), Is.True);
            Assert.That(loaded.Serialize(), Is.EqualTo(saved));
            Assert.That(CampaignProgress.Parse("basura;;x:y").CompletedChapters, Is.Empty);
            Assert.That(CampaignProgress.Parse(null).UnlockedCollectibles, Is.Empty);
        }
    }
}
