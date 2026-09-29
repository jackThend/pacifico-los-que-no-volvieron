using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using A = Pacifico.Core.Campaign.AricaChapter;
using F = Pacifico.Core.Campaign.AricaChapter.Facts;
using G = Pacifico.Core.Campaign.AricaChapter.Flags;
using S = Pacifico.Core.Campaign.AricaChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.4 — Capítulo 6, «Hasta el último cartucho» (Morro de Arica).</summary>
    public class AricaChapterTests
    {
        private const float Dt = 1f / 30f;

        private static void Run(MissionRunner r, float seconds)
        {
            for (float t = 0f; t < seconds && r.State == MissionState.Running; t += Dt) r.Step(Dt);
        }

        [Test]
        public void Guion_ValidoYConLaRespuestaDeBolognesi()
        {
            MissionScript script = A.Build(MissionTests.Read);
            Assert.That(script.Validate(), Is.Empty);
            MissionLine answer = script.Stage(S.Council).OnEnter.Single(l => l.Kind == LineKind.Quote);
            Assert.That(answer.Speaker, Is.EqualTo(A.Bolognesi));
            Assert.That(answer.Text, Is.EqualTo("Tengo deberes sagrados que cumplir y los cumpliré hasta quemar el último cartucho."));
            Assert.That(Pacifico.Core.Narrative.CollectibleCatalog.Definitions.Any(d => d.Id == script.RewardCollectibleId && d.Chapter == 6), Is.True);
        }

        [Test]
        public void Partida_Completa()
        {
            var r = new MissionRunner(A.Build(MissionTests.Read));
            var said = new List<string>();
            r.Event += e => { if (e.Kind == MissionEventKind.LineStarted) said.Add(e.Line.Text); };
            Assert.That(r.CurrentStage.Perspective, Is.EqualTo(A.Perspectives.Council));
            Run(r, 40f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Parapet), "la junta termina al acabar la respuesta");

            Run(r, 36f);
            r.Facts.SetFlag(G.DetonatorTried);
            r.Step(Dt);
            Assert.That(r.Dialogue.Current.Text, Does.Contain("cortado los cables"));
            r.Facts.Set(F.EnemiesDown, A.ParapetEnemies);
            r.Step(Dt);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Withdraw));

            Run(r, 20f);
            r.Facts.SetFlag(G.AtSummit);
            r.Step(Dt);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Summit));
            Run(r, A.SummitSeconds + 30f);
            Assert.That(r.Facts.Flag(G.BolognesiFalls) && r.Facts.Flag(G.UgarteLeaps), Is.True);
            Assert.That(said.Any(s => s.Contains("se lanza al vacío")), Is.True);
            Assert.That(r.State, Is.EqualTo(MissionState.Complete));
        }

        [Test]
        public void Partida_ElParapetoCaeIgualSiNoSeRechazaElAsalto()
        {
            var r = new MissionRunner(A.Build(MissionTests.Read));
            Run(r, 40f);
            Run(r, A.ParapetHoldSeconds + 1f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Withdraw));
        }

        [Test]
        public void Partida_CaerEnLasEscarpasEsFracaso_EnLaCimaEsElFinal()
        {
            var early = new MissionRunner(A.Build(MissionTests.Read));
            Run(early, 40f);
            early.Facts.SetFlag(G.PlayerDown);
            early.Step(Dt);
            Assert.That(early.State, Is.EqualTo(MissionState.Failed));

            var late = new MissionRunner(A.Build(MissionTests.Read));
            Run(late, 40f);
            late.Facts.Set(F.EnemiesDown, A.ParapetEnemies);
            late.Step(Dt);
            late.Facts.SetFlag(G.AtSummit);
            late.Step(Dt);
            late.Facts.SetFlag(G.PlayerDown);
            late.Step(Dt);
            Assert.That(late.State, Is.EqualTo(MissionState.Complete), "en la cima, caer es el destino de la guarnición");
        }
    }
}
