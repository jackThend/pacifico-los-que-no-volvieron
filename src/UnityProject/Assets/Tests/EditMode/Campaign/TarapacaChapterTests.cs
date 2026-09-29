using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using F = Pacifico.Core.Campaign.TarapacaChapter.Facts;
using G = Pacifico.Core.Campaign.TarapacaChapter.Flags;
using S = Pacifico.Core.Campaign.TarapacaChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.2 — Capítulo 4, «Sed en la quebrada» (Tarapacá): guion de la misión y captura de posiciones.</summary>
    public class TarapacaChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionRunner NewRunner()
        {
            var runner = new MissionRunner(TarapacaChapter.Build(MissionTests.Read));
            runner.Facts.Set(F.Rounds, 30);
            return runner;
        }

        private static void Run(MissionRunner r, float seconds)
        {
            for (float t = 0f; t < seconds && r.State == MissionState.Running; t += Dt) r.Step(Dt);
        }

        [Test]
        public void Guion_ValidoYConLaFraseDeCaceres()
        {
            MissionScript script = TarapacaChapter.Build(MissionTests.Read);
            Assert.That(script.Validate(), Is.Empty);
            MissionLine caceres = script.Stages[0].OnEnter.Single(l => l.Kind == LineKind.Quote);
            Assert.That(caceres.Speaker, Is.EqualTo(TarapacaChapter.Caceres));
            Assert.That(caceres.Text, Is.EqualTo("¡Hijos del Zepita! ¡No hay retirada posible! ¡O vencemos aquí o morimos de sed en el desierto!"));
            Assert.That(script.RewardCollectibleId, Is.Null, "no se inventa una carta que el Archivo no tiene");
        }

        [Test]
        public void Partida_Completa()
        {
            MissionRunner r = NewRunner();
            var said = new List<string>();
            r.Event += e => { if (e.Kind == MissionEventKind.LineStarted) said.Add(e.Line.Text); };

            Run(r, 10f);
            r.Facts.SetFlag(G.AtRally);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Village));

            // Combate en el pueblo: se gastan los cartuchos del Chassepot.
            for (int i = 0; i < 6; i++) { r.Facts.Add(F.EnemiesDown); r.Facts.Add(F.Rounds, -5); Run(r, 5f); }
            Assert.That(said.Any(s => s.StartsWith("Quedan pocos cartuchos")), Is.True, "aviso al quedar 5 o menos");
            r.Facts.SetFlag(G.SwappedRifle);
            r.Facts.Set(F.Rounds, 15);
            for (int i = 6; i < TarapacaChapter.DefaultVillageEnemies; i++) { r.Facts.Add(F.EnemiesDown); Run(r, 5f); }
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Krupp));
            Run(r, Dt * 2);
            Assert.That(r.Facts.Flag(G.ChargeOrdered), Is.True, "la escena ordena cargar a la bayoneta");

            r.Facts.Add(F.GunsTaken);
            Run(r, 5f);
            r.Facts.Add(F.GunsTaken);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Aftermath));

            r.Facts.SetFlag(G.DrummerFound);
            Run(r, 3f);
            r.Facts.SetFlag(G.WaterGiven);
            Run(r, 1f);
            Assert.That(r.Dialogue.Current.Text, Does.StartWith("Bebe despacio"), "interrumpe al resto");
            r.Facts.SetFlag(G.ReachedColumn);
            Run(r, 30f);
            Assert.That(r.State, Is.EqualTo(MissionState.Complete));
            Assert.That(r.Visited, Is.EqualTo(new[] { S.Surprise, S.Village, S.Krupp, S.Aftermath }));
            Assert.That(r.IsObjectiveComplete(r.Script.Stage(S.Aftermath).Objectives[0]), Is.True, "el agua al tambor queda anotada");
        }

        [Test]
        public void Partida_SiLosChilenosLleganAntes_EmpiezaElCombate()
        {
            MissionRunner r = NewRunner();
            r.Facts.Add(F.EnemiesDown);
            r.Step(Dt);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Village));
        }

        [Test]
        public void Partida_ElJugadorCae()
        {
            MissionRunner r = NewRunner();
            r.Facts.SetFlag(G.PlayerDown);
            r.Step(Dt);
            Assert.That(r.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("Mariano Santos", r.FailureReason);
        }

        [Test]
        public void Captura_SoloSinEnemigosYSinVueltaAtras()
        {
            var point = new CapturePoint(4f);
            for (int i = 0; i < 300; i++) Assert.That(point.Step(Dt, 1, 1), Is.False, "disputada: no avanza");
            Assert.That(point.Progress, Is.Zero);
            for (int i = 0; i < 60; i++) point.Step(Dt, 2, 0);
            Assert.That(point.Progress, Is.EqualTo(0.5f).Within(0.01f));
            for (int i = 0; i < 60; i++) point.Step(Dt, 0, 0);
            Assert.That(point.Progress, Is.EqualTo(0.5f - 0.25f * 0.25f * 2f).Within(0.01f), "abandonada: retrocede despacio");
            bool captured = false;
            for (int i = 0; i < 300 && !captured; i++) captured = point.Step(Dt, 1, 0);
            Assert.That(point.Captured, Is.True);
            point.Step(10f, 0, 5);
            Assert.That(point.Captured, Is.True);
        }
    }
}
