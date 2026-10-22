using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Narrative;
using Pacifico.Core.Tactics;
using F = Pacifico.Core.Campaign.PisaguaChapter.Facts;
using G = Pacifico.Core.Campaign.PisaguaChapter.Flags;
using P = Pacifico.Core.Campaign.PisaguaChapter.Perspectives;
using S = Pacifico.Core.Campaign.PisaguaChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.8 — Capítulo 3, Pisagua: el desembarco, la flota, el risco y los pozos.</summary>
    public class PisaguaChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionScript Chapter() => PisaguaChapter.Build(MissionTests.Read);

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
            Assert.That(script.Id, Is.EqualTo(CampaignCatalog.Get(PisaguaChapter.Id).Id));
            Assert.That(CollectibleCatalog.Definitions.Single(d => d.Id == script.RewardCollectibleId).Chapter, Is.EqualTo(3));

            GuionQuotes guion = GuionQuotes.Load(MissionTests.Read, PisaguaChapter.ChapterHeading);
            string prologue = PisaguaChapter.Prologue(guion);
            StringAssert.StartsWith("Pisagua parecía un anfiteatro natural", prologue);
            StringAssert.EndsWith("veteranos bolivianos y peruanos.", prologue);
            Assert.That(prologue.IndexOfAny(new[] { '"', '*', '«' }), Is.EqualTo(-1), "sin comillas ni marcas");
            MissionLine line = script.Stage(S.Prologue).OnEnter.Single(l => l.Speaker == PisaguaChapter.Correspondent);
            Assert.That(line.Text, Is.EqualTo(prologue));
            // Los fuertes y los buques son los que nombra el guion.
            string guionText = MissionTests.Read(GuionQuotes.ScriptFile);
            foreach (string name in new[] { "Pichalo", "Guacas", "O'Higgins", "Atacama", "Zapadores" }) StringAssert.Contains(name, guionText);
        }

        [Test]
        public void Botes_ElFuegoDeLaCostaSangraLaTravesia()
        {
            // Sin fuego, llegan todos y la travesía dura lo que dicta la velocidad.
            var calm = new LandingBoat(12, PisaguaChapter.BoatRunM);
            var random = new Random(1);
            int steps = 0;
            while (!calm.Beached) { calm.Step(0.1f, 0f, random); steps++; }
            Assert.That(calm.Men, Is.EqualTo(12));
            Assert.That(steps * 0.1f, Is.EqualTo(calm.TransitSeconds).Within(0.15f));

            // La media de bajas sigue la exponencial: dos fuertes y dos nidos (intensidad 4) se llevan casi la mitad.
            double Survivors(float intensity)
            {
                double total = 0;
                for (int seed = 0; seed < 400; seed++)
                {
                    var boat = new LandingBoat(12, PisaguaChapter.BoatRunM);
                    var r = new Random(seed);
                    while (!boat.Beached) boat.Step(0.1f, intensity, r);
                    total += boat.Men;
                }
                return total / 400.0;
            }
            double expected4 = 12 * Math.Exp(-LandingBoat.KillRatePerIntensity * 4f * calm.TransitSeconds);
            Assert.That(Survivors(4f), Is.EqualTo(expected4).Within(0.4));
            Assert.That(Survivors(4f), Is.InRange(5.5, 7.5));
            Assert.That(Survivors(1f), Is.GreaterThan(10));

            // Los fusileros: pleno a corta distancia, nada fuera de alcance, y la supresión les quita puntería.
            Assert.That(LandingBoat.RifleIntensity(12, 0f, 100f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(LandingBoat.RifleIntensity(12, 0f, 900f), Is.Zero);
            Assert.That(LandingBoat.RifleIntensity(8, 0.5f, 100f), Is.EqualTo(8f / 12f * 0.5f).Within(1e-4f));
        }

        [Test]
        public void Flota_CallaLosFuertesDondeSeLeMarca()
        {
            var fort = new CoastalFort(PisaguaChapter.Pichalo, -170f, 30f);
            var fleet = new NavalBombardment(7);
            var landed = new List<NavalImpact>();
            fleet.Step(1f, landed);
            Assert.That(fleet.SalvosFired, Is.Zero, "sin blanco no dispara");
            fleet.Designate(fort.X, fort.Z);
            float t = 0f;
            int shells = 0;
            while (!fort.Silenced && t < 600f)
            {
                fleet.Step(0.1f, landed);
                foreach (NavalImpact impact in landed)
                {
                    shells++;
                    fort.RegisterImpact(impact.X, impact.Z);
                }
                t += 0.1f;
            }
            Assert.That(fort.Silenced, Is.True);
            Assert.That(fort.IntensityOn(100f), Is.Zero, "callado, ya no tira a los botes");
            Assert.That(t, Is.InRange(30f, 300f));
            // La probabilidad de impacto es la de una dispersión circular de 18 m sobre un radio de 15 m.
            Assert.That(fleet.HitProbability(CoastalFort.HitRadiusM), Is.EqualTo(0.293f).Within(0.002f));

            // Determinista: la misma semilla, las mismas granadas.
            var a = new NavalBombardment(3);
            var b = new NavalBombardment(3);
            var la = new List<NavalImpact>();
            var lb = new List<NavalImpact>();
            a.Designate(0f, 0f);
            b.Designate(0f, 0f);
            for (int i = 0; i < 100; i++)
            {
                a.Step(0.1f, la);
                b.Step(0.1f, lb);
                Assert.That(la.Select(x => x.X), Is.EqualTo(lb.Select(x => x.X)));
            }
        }

        [Test]
        public void Demolicion_LaMechaSeApagaSiSeLlegaATiempo()
        {
            var water = new DemolitionCharge(PisaguaChapter.WaterFuseSeconds);
            water.Step(100f, false);
            Assert.That(water.Blown, Is.False);
            water.Step(1f, true);
            Assert.That(water.Defused, Is.True);
            Assert.That(water.Step(500f, false), Is.False, "apagada, ya no estalla");

            var engines = new DemolitionCharge(PisaguaChapter.LocomotiveFuseSeconds);
            Assert.That(engines.Step(PisaguaChapter.LocomotiveFuseSeconds - 0.01f, false), Is.False);
            Assert.That(engines.Step(0.02f, false), Is.True);
            Assert.That(engines.Blown, Is.True);
        }

        [Test]
        public void Risco_LosNidosSeReplieganDiezmadosOSuprimidosConElEnemigoEncima()
        {
            Assert.That(PisaguaChapter.NestWithdraws(PisaguaChapter.MenPerNest, true, 200f), Is.False, "suprimidos pero lejos: aguantan");
            Assert.That(PisaguaChapter.NestWithdraws(PisaguaChapter.MenPerNest, false, 10f), Is.False, "sin suprimir: se baten");
            Assert.That(PisaguaChapter.NestWithdraws(PisaguaChapter.MenPerNest, true, 40f), Is.True);
            Assert.That(PisaguaChapter.NestWithdraws(PisaguaChapter.WithdrawAtStrength, false, 300f), Is.True);
        }

        /// <summary>
        /// El relieve: de la playa a la pampa se sube a pie (el NavMesh admite hasta 40°), el terraplén es llano para las
        /// vías y las puntas de los fuertes asoman sobre el mar.
        /// </summary>
        [Test]
        public void Terreno_LaLaderaSeSubeYElTerraplenEsLlano()
        {
            float steepest = 0f;
            for (float x = -120f; x <= 120f; x += 1f)
            {
                for (float z = -60f; z <= 220f; z += 1f) steepest = Math.Max(steepest, PisaguaTerrain.SlopeDeg(x, z));
            }
            Assert.That(steepest, Is.LessThan(38f));
            Assert.That(PisaguaTerrain.HeightAt(0f, -8f), Is.GreaterThan(PisaguaTerrain.SeaLevel), "los botes varan en seco");
            Assert.That(PisaguaTerrain.HeightAt(0f, -60f), Is.LessThan(PisaguaTerrain.SeaLevel));
            for (float x = -60f; x <= 60f; x += 10f)
            {
                Assert.That(PisaguaTerrain.HeightAt(x, PisaguaTerrain.EmbankmentZ), Is.EqualTo(PisaguaTerrain.EmbankmentHeight).Within(0.01f));
            }
            Assert.That(PisaguaTerrain.HeightAt(0f, 160f), Is.EqualTo(PisaguaTerrain.PlateauHeight).Within(1f));
            Assert.That(PisaguaTerrain.HeightAt(-PisaguaTerrain.HeadlandX, -60f), Is.GreaterThan(20f), "Punta Pichalo");
            Assert.That(PisaguaTerrain.HeightAt(PisaguaTerrain.HeadlandX, -60f), Is.GreaterThan(20f), "Punta Guacas");
        }

        /// <summary>Partida completa: los fuertes callados, dos olas en la playa, el terraplén, los pozos y el despacho.</summary>
        [Test]
        public void Partida_DesenlaceHistorico()
        {
            var runner = new MissionRunner(Chapter());
            var perspectives = new List<string> { runner.CurrentStage.Perspective };
            runner.Event += e =>
            {
                if (e.Kind == MissionEventKind.StageStarted) perspectives.Add(runner.CurrentStage.Perspective);
            };
            RunUntil(runner, () => runner.CurrentStage.Id == S.Beachhead);

            Wait(runner, 120f);
            runner.Facts.Set(F.MenInField, 20f);
            runner.Facts.Set(F.BoatsLanded, 2f);
            runner.Facts.SetFlag(G.PichaloSilenced);
            runner.Facts.Set(F.FortsSilenced, 1f);
            RunUntil(runner, () => runner.Facts.Flag(G.SecondWave), 30f);
            Assert.That(runner.Facts.Flag(G.SecondWave), Is.True, "zarpa la segunda ola");
            Wait(runner, 100f);
            runner.Facts.SetFlag(G.GuacasSilenced);
            runner.Facts.Set(F.FortsSilenced, 2f);
            runner.Facts.Set(F.BoatsLanded, 4f);
            runner.Facts.Set(F.MenInField, 38f);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Cliff);
            Assert.That(runner.Facts.Flag(G.FleetFinishesForts), Is.False);

            Wait(runner, 90f);
            runner.Facts.SetFlag(G.EmbankmentTaken);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Wells);
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.FusesLit), Is.True);
            Wait(runner, 60f);
            runner.Facts.SetFlag(G.WaterSecured);
            Wait(runner, 10f);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Wells), "espera a las locomotoras");
            runner.Facts.SetFlag(G.LocomotivesBlown);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Defense);
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.CounterAttack), Is.True);
            Wait(runner, 40f);
            runner.Facts.SetFlag(G.CounterRepulsed);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Dispatch);
            runner.Facts.SetFlag(G.DocumentClosed);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Complete));
            MissionStage wells = runner.Script.Stage(S.Wells);
            Assert.That(runner.IsObjectiveComplete(wells.Objectives.Single(o => o.Id == "locomotoras")), Is.False, "volaron: opcional perdido");
            Assert.That(perspectives, Is.EqualTo(new[] { P.Plate, P.Command, P.Command, P.Command, P.Command, P.Document }));
        }

        [Test]
        public void Partida_SinFlotaLaCabezaDePlayaTerminaIgual()
        {
            var runner = new MissionRunner(Chapter());
            RunUntil(runner, () => runner.CurrentStage.Id == S.Beachhead);
            runner.Facts.Set(F.BoatsLanded, PisaguaChapter.DefaultBoats);
            runner.Facts.Set(F.MenInField, 25f);
            float t = RunUntil(runner, () => runner.Facts.Flag(G.FleetFinishesForts));
            Assert.That(t, Is.EqualTo(PisaguaChapter.BeachheadTimeoutSeconds).Within(1f));
            runner.Facts.Set(F.FortsSilenced, 2f); // la escena calla los fuertes que quedaban
            RunUntil(runner, () => runner.CurrentStage.Id == S.Cliff);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Cliff));
        }

        [Test]
        public void Partida_SiVuelanLosEstanquesSeFracasa()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.Set(F.MenInField, 30f);
            runner.Facts.SetFlag(G.WaterBlown);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("sin agua", runner.FailureReason);
        }

        [Test]
        public void Partida_SinNadieEnTierraSeFracasa()
        {
            var runner = new MissionRunner(Chapter());
            RunUntil(runner, () => runner.CurrentStage.Id == S.Beachhead);
            Wait(runner, 5f);
            Assert.That(runner.State, Is.EqualTo(MissionState.Running), "antes de desembarcar no cuenta");
            runner.Facts.Set(F.BoatsLanded, PisaguaChapter.DefaultBoats);
            runner.Facts.Set(F.MenInField, 0f);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
        }
    }
}
