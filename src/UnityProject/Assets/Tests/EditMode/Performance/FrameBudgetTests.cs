using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Effects;
using Pacifico.Core.Infantry;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Performance
{
    /// <summary>
    /// ROADMAP 6.6 — presupuesto de CPU de la simulación. A 60 FPS un fotograma dura 16,7 ms; la lógica del juego
    /// (sin render, física ni NavMesh) no debe pasar de un 10 % en la escena más cargada. Se reproduce el pico del
    /// capítulo 5 (16 escuadras de 12 hombres trabadas en fuego, con cobertura reasignada cada 3 s, humo al máximo y el
    /// guion de la misión) y el del capítulo 4/6/8 (40 fusileros de la IA). El tiempo se imprime en la salida de la
    /// prueba para seguirlo entre versiones.
    /// </summary>
    public class FrameBudgetTests
    {
        private const float Dt = 1f / 60f;
        private const double BudgetMs = 1.67; // el 10 % de un fotograma a 60 FPS

        private static double MeanFrameMs(Action frame, int frames = 1800)
        {
            for (int i = 0; i < 300; i++) frame();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++) frame();
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds / frames;
        }

        [Test]
        public void RTS_16Escuadras_HumoYMision_CabenEnElPresupuesto()
        {
            var weapon = WeaponCatalog.Get(WeaponCatalog.RemingtonId);
            var squads = new List<(SquadCommand command, SquadFireControl fire, SuppressionModel suppression, SquadSupply supply, List<Vec3> men)>();
            for (int s = 0; s < 16; s++)
            {
                var men = new List<Vec3>();
                for (int i = 0; i < 12; i++) men.Add(new Vec3(s * 30f + i, 0f, s % 2 == 0 ? 0f : 300f));
                var command = new SquadCommand(FormationType.Line, men, men[6], new Vec3(0f, 0f, s % 2 == 0 ? 1f : -1f));
                squads.Add((command, new SquadFireControl(weapon, 12, s), new SuppressionModel(), new SquadSupply(12, new SupplySettings(), s), men));
            }
            var spots = new List<CoverSpot>();
            for (int i = 0; i < 160; i++) spots.Add(new CoverSpot(new Vec3(i * 1.2f - 90f, 0f, 90f), new Vec3(0f, 0f, -1f), 0.65f));
            var smoke = new BlackPowderSmokeModel(new BlackPowderSmokeSettings { MaxPuffs = 160 });
            var runner = new MissionRunner(AltoDeLaAlianzaChapter.Build(Campaign.MissionTests.Read));
            runner.Facts.Set(AltoDeLaAlianzaChapter.Facts.MenInField, 44);
            int frame = 0;

            double ms = MeanFrameMs(() =>
            {
                frame++;
                for (int s = 0; s < squads.Count; s++)
                {
                    var q = squads[s];
                    q.command.Step(Dt, q.men);
                    VolleyResult v = q.fire.Step(Dt, true, 280f, Posture.Kneeling, 0.4f);
                    q.suppression.ReceiveFire(v.Shots, v.Hits, 0, 280f, 450f, 0.5f);
                    q.suppression.Step(Dt);
                    q.supply.Step(Dt, Exertion.Fighting, 1f);
                    // Cobertura: cada escuadra reasigna puestos cada 3 s (repartidas en fotogramas distintos).
                    if ((frame + s * 11) % 180 == 0) CoverSelector.Assign(q.men, q.men[6], spots, new Vec3(0f, 0f, 1f));
                }
                if (frame % 4 == 0) smoke.Emit(new Vec3(frame % 90, 1.4f, 0f), new Vec3(0f, 0f, 1f), 5f);
                smoke.Step(Dt);
                runner.Step(Dt);
            });
            TestContext.WriteLine("Capítulo 5, pico de combate: " + ms.ToString("0.000") + " ms por fotograma (presupuesto " + BudgetMs + " ms)");
            Assert.That(ms, Is.LessThan(BudgetMs));
        }

        [Test]
        public void FPS_40Fusileros_CabenEnElPresupuesto()
        {
            var weapon = WeaponCatalog.Get(WeaponCatalog.ComblainId);
            var brains = new List<(RiflemanBrain brain, RifleCycleModel rifle, Vitality vitality)>();
            for (int i = 0; i < 40; i++) brains.Add((new RiflemanBrain(weapon, i), new RifleCycleModel(weapon, 1000), new Vitality()));
            var runner = new MissionRunner(TarapacaChapter.Build(Campaign.MissionTests.Read));
            runner.Facts.Set(TarapacaChapter.Facts.Rounds, 10);
            double ms = MeanFrameMs(() =>
            {
                for (int i = 0; i < brains.Count; i++)
                {
                    var b = brains[i];
                    var p = new RiflemanPerception { HasTarget = true, TargetDistanceM = 40f + i, LineOfSight = i % 3 != 0, Loaded = b.rifle.CanFire, HasAmmo = true };
                    RiflemanDecision d = b.brain.Step(Dt, p);
                    if (d.Fire)
                    {
                        b.rifle.PullTrigger();
                        b.brain.SampleShot(RiflemanBrain.SigmaDeg(weapon, false, 0f));
                    }
                    b.rifle.Step(Dt);
                    b.vitality.Step(Dt);
                }
                runner.Step(Dt);
            });
            TestContext.WriteLine("FPS, 40 fusileros: " + ms.ToString("0.000") + " ms por fotograma (presupuesto " + BudgetMs + " ms)");
            Assert.That(ms, Is.LessThan(BudgetMs));
        }
    }
}
