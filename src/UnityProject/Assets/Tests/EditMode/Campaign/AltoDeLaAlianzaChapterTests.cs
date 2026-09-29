using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Tactics;
using C = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter;
using F = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Facts;
using G = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Flags;
using S = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.3 — Capítulo 5, «El trueno de Intiorko»: guion, choque a la bayoneta y metralla.</summary>
    public class AltoDeLaAlianzaChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionRunner NewRunner()
        {
            var r = new MissionRunner(C.Build(MissionTests.Read));
            r.Facts.Set(F.MenInField, 44);
            return r;
        }

        private static void Run(MissionRunner r, float seconds)
        {
            for (float t = 0f; t < seconds && r.State == MissionState.Running; t += Dt) r.Step(Dt);
        }

        [Test]
        public void Guion_ValidoYConElGritoDeLosColorados()
        {
            MissionScript script = C.Build(MissionTests.Read);
            Assert.That(script.Validate(), Is.Empty);
            MissionLine cry = script.Stage(S.Charge).Triggers.SelectMany(t => t.Lines).Single(l => l.Kind == LineKind.Quote);
            Assert.That(cry.Text, Is.EqualTo("¡Temblad, rotos, que aquí entran los Colorados de Bolivia!"));
            Assert.That(script.RewardCollectibleId, Is.EqualTo("carta_quiroz_2_desierto"));
            Assert.That(Pacifico.Core.Narrative.CollectibleCatalog.Definitions.Any(d => d.Id == script.RewardCollectibleId && d.Chapter == 5), Is.True);
        }

        [Test]
        public void Partida_Completa()
        {
            MissionRunner r = NewRunner();
            var said = new List<string>();
            r.Event += e => { if (e.Kind == MissionEventKind.LineStarted) said.Add(e.Line.Text); };

            r.Facts.Set(F.SquadsEntrenched, 4);
            Run(r, C.FogSeconds + 1f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Advance), "la niebla se levanta a su hora, se esté listo o no");
            Assert.That(r.Facts.Flag(G.FogLifted), Is.True);

            r.Facts.Set(F.EnemyDown, C.EnemyCasualtiesToStall);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Charge));
            Assert.That(said.Any(s => s.Contains("La izquierda aliada cede")), Is.True);

            r.Facts.SetFlag(G.ChargeOrdered);
            Run(r, 2f);
            Assert.That(r.Dialogue.Current.Speaker == C.Colorados || said.Any(s => s.StartsWith("Estalla un huayno")), Is.True);
            r.Facts.Set(F.GunsRetaken, 2);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Retreat));
            Assert.That(said, Does.Contain("¡Temblad, rotos, que aquí entran los Colorados de Bolivia!"));

            // Retirada: todos los supervivientes llegan a la retaguardia antes de que se cierre la tenaza.
            Run(r, 30f);
            r.Facts.Set(F.MenEvacuated, 22);
            r.Facts.Set(F.MenInField, 0);
            Run(r, 30f);
            Assert.That(r.State, Is.EqualTo(MissionState.Complete));
            Assert.That(said.Any(s => s.StartsWith("Es la última batalla de Bolivia")), Is.True);
        }

        [Test]
        public void Partida_SinContenerAlEnemigo_LaIzquierdaCedeIgual()
        {
            MissionRunner r = NewRunner();
            Run(r, C.FogSeconds + C.HoldSeconds + 20f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Charge));
            Run(r, C.ChargeStageTimeoutSeconds + 1f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Retreat), "sin carga, la tenaza llega igualmente");
        }

        [Test]
        public void Partida_LaTenazaSeCierraSinLosHeridos()
        {
            MissionRunner r = NewRunner();
            Run(r, C.FogSeconds + C.HoldSeconds + C.ChargeStageTimeoutSeconds + 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Retreat));
            r.Facts.Set(F.MenEvacuated, 5);
            Run(r, C.RetreatSeconds + 1f);
            Assert.That(r.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("heridos", r.FailureReason);
        }

        [Test]
        public void Partida_AniquiladosEsFracaso()
        {
            MissionRunner r = NewRunner();
            r.Facts.Set(F.MenInField, 0);
            r.Step(Dt);
            Assert.That(r.State, Is.EqualTo(MissionState.Failed));
        }

        // ------------------------------------------------------------------------------------------
        // Choque a la bayoneta
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Carga_ContraUnaEscuadraSuprimidaTriunfaCasiSiempre()
        {
            float suppressed = ShockCombat.WinProbability(12, 10, suppressed: true);
            float fresh = ShockCombat.WinProbability(12, 10, suppressed: false);
            float weak = ShockCombat.WinProbability(6, 12, suppressed: false);
            Assert.That(suppressed, Is.GreaterThan(0.9f));
            Assert.That(fresh, Is.InRange(0.5f, 0.95f), "contra una escuadra entera, la carga es arriesgada");
            Assert.That(weak, Is.LessThan(0.25f), "media escuadra contra una entera se estrella");
            Assert.That(suppressed, Is.GreaterThan(fresh));
        }

        [Test]
        public void Carga_EsBreveYNoHastaElUltimoHombre()
        {
            var c = new ShockCombat(12, 10, 3);
            int a = 12, d = 10;
            while (c.Outcome == ShockOutcome.Fighting && c.Elapsed < 60f)
            {
                ShockStep s = c.Step(1f / 30f, a, d, true);
                a -= s.AttackerCasualties;
                d -= s.DefenderCasualties;
            }
            Assert.That(c.Outcome, Is.EqualTo(ShockOutcome.DefendersBreak));
            Assert.That(c.Elapsed, Is.LessThan(20f), "se decide en segundos");
            Assert.That(d, Is.GreaterThan(0), "rompen y huyen antes de caer todos");
        }

        [Test]
        public void Carga_LaEsperanzaNoDependeDelPaso()
        {
            static float MeanLosses(float dt)
            {
                int total = 0;
                for (int seed = 0; seed < 400; seed++)
                {
                    var c = new ShockCombat(12, 12, seed);
                    for (float t = 0f; t < 4f; t += dt) total += c.Step(dt, 12, 12, false).DefenderCasualties;
                }
                return total / 400f;
            }
            float coarse = MeanLosses(1f / 20f), fine = MeanLosses(1f / 144f);
            Assert.That(coarse, Is.EqualTo(fine).Within(fine * 0.08f));
            // 12 hombres × 0,06 × 1,5 durante 4 s ≈ 4,3 bajas esperadas.
            Assert.That(fine, Is.EqualTo(12 * ShockCombat.LethalityPerManPerSecond * ShockCombat.ChargeImpetus * 4f).Within(0.5f));
        }

        [Test]
        public void AlTrote_LaEscuadraCubreElDobleDeTerreno()
        {
            static float Distance(float pace)
            {
                var march = new SquadMarch(new Pacifico.Core.Common.Vec3(0f, 0f, 0f), new Pacifico.Core.Common.Vec3(0f, 0f, 1f)) { Pace = pace, Frontage = 10f };
                march.Order(new[] { new Pacifico.Core.Common.Vec3(0f, 0f, 500f) }, new Pacifico.Core.Common.Vec3(0f, 0f, 1f));
                for (int i = 0; i < 300; i++) march.Step(1f / 30f, 0f);
                return march.Anchor.Z;
            }
            float walk = Distance(1f), trot = Distance(SquadMarch.TrotPace);
            Assert.That(walk, Is.EqualTo(SquadMarch.MarchSpeedMps * 10f).Within(0.5f));
            Assert.That(trot / walk, Is.EqualTo(SquadMarch.TrotPace).Within(0.05f));
        }

        [Test]
        public void Metralla_DecreceConLaDistanciaYLaCobertura()
        {
            Assert.That(ShellBurst.CasualtyProbability(0f, 0f), Is.EqualTo(ShellBurst.CenterKillProbability));
            Assert.That(ShellBurst.CasualtyProbability(6f, 0f), Is.EqualTo(ShellBurst.CenterKillProbability * 0.25f).Within(1e-5f));
            Assert.That(ShellBurst.CasualtyProbability(ShellBurst.LethalRadiusM, 0f), Is.Zero);
            Assert.That(ShellBurst.CasualtyProbability(0f, 1f), Is.EqualTo(ShellBurst.CenterKillProbability * 0.2f).Within(1e-5f), "en la zanja, una quinta parte");
        }
    }
}
