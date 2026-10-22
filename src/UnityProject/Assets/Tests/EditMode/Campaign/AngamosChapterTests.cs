using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Naval;
using F = Pacifico.Core.Campaign.AngamosChapter.Facts;
using G = Pacifico.Core.Campaign.AngamosChapter.Flags;
using P = Pacifico.Core.Campaign.AngamosChapter.Perspectives;
using S = Pacifico.Core.Campaign.AngamosChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.7 — Capítulo 2, Punta Angamos: el guion de la misión y la artillería que lo sostiene.</summary>
    public class AngamosChapterTests
    {
        private const float Dt = 1f / 30f;

        private static MissionScript Chapter() => AngamosChapter.Build(MissionTests.Read);

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

        private static void Wait(MissionRunner runner, float seconds) => RunUntil(runner, () => false, seconds);

        [Test]
        public void Guion_EsValidoYElPrologoSeLeeDelGuion()
        {
            MissionScript script = Chapter();
            Assert.That(script.Validate(), Is.Empty);
            Assert.That(script.Id, Is.EqualTo(CampaignCatalog.Get(AngamosChapter.Id).Id));
            Assert.That(script.RewardCollectibleId, Is.Null, "el libro de guardia del Huáscar no está en el Archivo");

            GuionQuotes guion = GuionQuotes.Load(MissionTests.Read, AngamosChapter.ChapterHeading);
            StringAssert.Contains("CAZA EN ALTA MAR", guion.ChapterTitle);
            string prologue = guion.Field("Prólogo");
            StringAssert.StartsWith("Durante cinco meses, el Huáscar", prologue);
            StringAssert.DoesNotContain("*", prologue);
            Assert.That(script.Stage(S.Trap).OnEnter.Any(l => l.Text == prologue), Is.True);
            Assert.That(guion.Field("Coleccionable 3D"), Does.Contain("libro de guardia"));
            Assert.Throws<KeyNotFoundException>(() => guion.Field("Jugabilidad"), "viñeta sin texto");
            // El guion no cita a nadie en este capítulo: ninguna línea se atribuye a un personaje histórico.
            var lines = script.Stages.SelectMany(s => s.OnEnter.Concat(s.Triggers.SelectMany(t => t.Lines))).ToList();
            Assert.That(lines.Where(l => l.Kind == LineKind.Quote), Is.Empty);
            // El relevo del mando, en el orden del guion (con Palacios, que el guion omite).
            string all = string.Join("\n", lines.Select(l => l.Text));
            int[] order = new[] { "Miguel Grau", "Elías Aguirre", "José Melitón Rodríguez", "Enrique Palacios", "Pedro Gárezon" }.Select(n => all.IndexOf(n)).ToArray();
            Assert.That(order.All(i => i >= 0), Is.True);
            Assert.That(order, Is.Ordered);
        }

        /// <summary>
        /// La distancia de apertura de fuego no es un número de diseño: es donde las piezas de 250 lb del Cochrane
        /// empiezan a perforar el cinturón del Huáscar según el modelo de blindaje (≈2.200 m, como en las crónicas).
        /// </summary>
        [Test]
        public void Artilleria_LatorreAbreFuegoDondeEmpiezaAPerforar()
        {
            float open = AngamosChapter.OpeningFireRangeM();
            Assert.That(open, Is.EqualTo(2200f));
            ShipSpec cochrane = ShipCatalog.Cochrane(), huascar = ShipCatalog.Huascar();
            GunMount gun = BroadsideBatteryModel.FromShip(cochrane).Gun;
            Assert.That(gun.ShellWeightLb, Is.EqualTo(250f));
            Assert.That(ArmorPenetrationModel.ResolveAgainstShip(gun, open, 0f, huascar, ArmorZone.BeltMidships).Penetrated, Is.True);
            Assert.That(ArmorPenetrationModel.ResolveAgainstShip(gun, open + 100f, 0f, huascar, ArmorZone.BeltMidships).Penetrated, Is.False);
            StringAssert.Contains("2.200 m", string.Join("\n", Chapter().Stage(S.Duel).OnEnter.Select(l => l.Text)));
            // La torre de mando (3") se perfora desde mucho más lejos: el proyectil que mató a Grau.
            Assert.That(ArmorPenetrationModel.PenetrationRangeM(gun, huascar, ArmorZone.ConningTower), Is.GreaterThan(4000f));
        }

        /// <summary>
        /// «Posicionar el blindado a 45 grados para maximizar el rebote»: las granadas de 300 lb del Huáscar no pasan nunca
        /// el cinturón central del Cochrane, pero atraviesan sus extremos de frente hasta ~1.800 m; a 45°, solo a quemarropa.
        /// </summary>
        [Test]
        public void Artilleria_LaCorazaEnAnguloSalvaLosExtremos()
        {
            ShipSpec cochrane = ShipCatalog.Cochrane(), huascar = ShipCatalog.Huascar();
            GunMount turret = huascar.Guns.First(g => g.Placement == GunPlacement.Turret);
            Assert.That(ArmorPenetrationModel.PenetrationRangeM(turret, cochrane, ArmorZone.BeltMidships), Is.Zero);
            float square = ArmorPenetrationModel.PenetrationRangeM(turret, cochrane, ArmorZone.BeltEnds);
            float angled = ArmorPenetrationModel.PenetrationRangeM(turret, cochrane, ArmorZone.BeltEnds, 45f);
            Assert.That(square, Is.InRange(1500f, 2100f));
            Assert.That(angled, Is.LessThan(500f));

            // El precio: a 45° la batería de costado (±40° del través) no bate al Huáscar. Se gira para recargar y se
            // vuelve al través para la andanada.
            BroadsideBatteryModel battery = BroadsideBatteryModel.FromShip(cochrane);
            Assert.That(battery.SideFor(45f), Is.EqualTo(BroadsideSide.None));
            Assert.That(battery.SideFor(60f), Is.EqualTo(BroadsideSide.Starboard));
            Assert.That(Ballistics.SideObliquityDeg(0f, 60f), Is.LessThanOrEqualTo(battery.ArcHalfWidthDeg));
            Assert.That(ArmorPenetrationModel.PenetrationRangeM(turret, cochrane, ArmorZone.BeltEnds, 30f), Is.InRange(angled, square));

            Assert.That(Ballistics.SideObliquityDeg(0f, 90f), Is.EqualTo(0f).Within(1e-4f), "enemigo por el través");
            Assert.That(Ballistics.SideObliquityDeg(0f, 0f), Is.EqualTo(90f).Within(1e-4f), "por la proa");
            Assert.That(Ballistics.SideObliquityDeg(350f, 35f), Is.EqualTo(45f).Within(1e-4f));
            Assert.That(Ballistics.SideObliquityDeg(10f, 235f), Is.EqualTo(45f).Within(1e-4f), "por la aleta de babor");
        }

        [Test]
        public void Averias_UnImpactoEnLaPopaTrabaElTimon()
        {
            var state = new ShipDamageState(ShipCatalog.Huascar());
            state.JamSteering();
            Assert.That(state.SteeringJammed, Is.True);
            state.Activate(DamageControlAction.SteamRepair);
            for (int i = 0; i < 300; i++) state.Step(0.1f);
            Assert.That(state.SteeringJammed, Is.False, "la brigada de vapor lo libera");
        }

        /// <summary>
        /// Partida completa con el desenlace histórico: el cerco, el fuego retenido, la coraza en ángulo, el espolón
        /// esquivado, la torre de mando, el relevo de los comandantes, el abordaje y las láminas.
        /// </summary>
        [Test]
        public void Partida_DesenlaceHistorico()
        {
            var runner = new MissionRunner(Chapter());
            var perspectives = new List<string> { runner.CurrentStage.Perspective };
            runner.Event += e =>
            {
                if (e.Kind == MissionEventKind.StageStarted) perspectives.Add(runner.CurrentStage.Perspective);
            };

            // La trampa: la Unión escapa por orden de Grau y el Huáscar vira cuando el Cochrane le corta el paso.
            Wait(runner, 30f);
            Assert.That(runner.Facts.Flag(G.UnionBreaksAway), Is.False, "espera a que termine el prólogo");
            RunUntil(runner, () => runner.Facts.Flag(G.UnionBreaksAway));
            Assert.That(runner.Facts.Flag(G.HuascarTurns), Is.False, "aún no está a tiro");
            runner.Facts.SetFlag(G.WithinRange);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Duel);
            Assert.That(runner.Facts.Flag(G.HuascarTurns), Is.True);

            // El duelo: fuego retenido, coraza en ángulo, espolón esquivado y tres perforaciones.
            Wait(runner, 20f);
            runner.Facts.Add(F.Deflected, 2);
            runner.Facts.Add(F.Broadsides);
            runner.Facts.Add(F.PenetratingHits);
            Wait(runner, 10f);
            runner.Facts.SetFlag(G.RamAttempt); // la escena: el Huáscar se lanza al espolón a menos de 900 m
            runner.Step(Dt);
            Assert.That(runner.History.Any(e => e.Kind == MissionEventKind.TriggerFired && e.Detail == "al_espolon"), Is.True);
            Wait(runner, 30f);
            runner.Facts.SetFlag(G.RamAvoided);
            runner.Facts.Add(F.PenetratingHits, 2);
            RunUntil(runner, () => runner.CurrentStage.Id == S.ConningTower);
            MissionStage duel = runner.Script.Stage(S.Duel);
            foreach (string id in new[] { "perforar", "retener_fuego", "coraza_en_angulo", "esquivar_espolon" })
            {
                Assert.That(runner.IsObjectiveComplete(duel.Objectives.First(o => o.Id == id)), Is.True, id);
            }

            // El intercalado a bordo del Huáscar y el relevo.
            RunUntil(runner, () => runner.CurrentStage.Id == S.Relief);
            for (int i = 0; i < AngamosChapter.HitsToSilence; i++)
            {
                Wait(runner, 12f);
                runner.Facts.Add(F.HitsSinceGrau);
            }
            RunUntil(runner, () => runner.CurrentStage.Id == S.Valves);
            Assert.That(runner.History.Count(e => e.Kind == MissionEventKind.TriggerFired && e.Detail.StartsWith("cae_")), Is.EqualTo(2));

            // Las válvulas: el abordaje llega a tiempo.
            runner.Step(Dt);
            Assert.That(runner.Facts.Flag(G.ValvesOpen), Is.True);
            Wait(runner, 60f);
            runner.Facts.SetFlag(G.Boarded);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Plates);
            Assert.That(runner.Facts.Flag(G.ValvesClosed), Is.True);
            Assert.That(runner.Facts.Flag(G.ScuttleComplete), Is.False);
            Wait(runner, AngamosChapter.ScuttleSeconds);
            Assert.That(runner.State, Is.EqualTo(MissionState.Running), "cerradas las válvulas, ya no hay cuenta atrás");
            runner.Facts.SetFlag(G.PlatesClosed);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Complete));

            Assert.That(perspectives, Is.EqualTo(new[] { P.CochraneBridge, P.CochraneBridge, P.HuascarDeck, P.CochraneBridge, P.CochraneBridge, P.Plates }));
        }

        [Test]
        public void Partida_ElFuegoLejanoNoCuentaComoRetenido()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.SetFlag(G.WithinRange);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Duel);
            runner.Facts.Add(F.BroadsidesBeyondRange);
            runner.Facts.Add(F.PenetratingHits);
            Wait(runner, 1f);
            MissionStage duel = runner.Script.Stage(S.Duel);
            Assert.That(runner.IsObjectiveComplete(duel.Objectives.First(o => o.Id == "retener_fuego")), Is.False);
        }

        [Test]
        public void Partida_SiNadieAbordaElHuascarSeVaAPique()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.SetFlag(G.WithinRange);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Duel);
            runner.Facts.Add(F.ConningTowerHits); // el proyectil en la torre de mando adelanta el desenlace
            RunUntil(runner, () => runner.CurrentStage.Id == S.Relief);
            runner.Facts.Add(F.HitsSinceGrau, AngamosChapter.HitsToSilence);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Valves);
            float t = RunUntil(runner, () => runner.Facts.Flag(G.ScuttleComplete));
            Assert.That(t, Is.EqualTo(AngamosChapter.ScuttleSeconds).Within(0.1f));
            RunUntil(runner, () => runner.State != MissionState.Running);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("válvulas abiertas", runner.FailureReason);
        }

        [Test]
        public void Partida_ElHuascarEscapa()
        {
            var runner = new MissionRunner(Chapter());
            Wait(runner, 5f);
            runner.Facts.SetFlag(G.HuascarEscaped);
            runner.Step(Dt);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            StringAssert.Contains("escapado", runner.FailureReason);
        }

        /// <summary>Si el jugador no logra perforar, la historia sigue: el duelo y el relevo terminan por tiempo.</summary>
        [Test]
        public void Partida_SinPunteriaLaHistoriaSigue()
        {
            var runner = new MissionRunner(Chapter());
            runner.Facts.SetFlag(G.WithinRange);
            RunUntil(runner, () => runner.CurrentStage.Id == S.Duel);
            float duel = RunUntil(runner, () => runner.CurrentStage.Id != S.Duel);
            Assert.That(duel, Is.InRange(AngamosChapter.DuelTimeoutSeconds - 1f, AngamosChapter.DuelTimeoutSeconds + 15f));
            RunUntil(runner, () => runner.CurrentStage.Id == S.Relief);
            float relief = RunUntil(runner, () => runner.CurrentStage.Id != S.Relief);
            Assert.That(relief, Is.InRange(AngamosChapter.ReliefTimeoutSeconds - 1f, AngamosChapter.ReliefTimeoutSeconds + 15f));
            Assert.That(runner.CurrentStage.Id, Is.EqualTo(S.Valves));
        }
    }
}
