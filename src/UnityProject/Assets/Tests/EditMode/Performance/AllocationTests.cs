using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Effects;
using Pacifico.Core.Infantry;
using Pacifico.Core.Naval;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Performance
{
    /// <summary>
    /// ROADMAP 6.6 — lo que corre en cada fotograma no debe generar basura: en Unity (Mono/IL2CPP) cada asignación
    /// acaba en una pasada del recolector y en un tirón. Se mide en régimen estable (tras calentar) con el contador de
    /// bytes del hilo.
    /// </summary>
    public class AllocationTests
    {
        private const int Frames = 600;
        private const float Dt = 1f / 60f;

        private static long Measure(Action frame, int warmupFrames = 900)
        {
            for (int i = 0; i < warmupFrames; i++) frame(); // calentar 15 s: un ciclo completo de tiro y recarga, listas que crecen una vez
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Frames; i++) frame();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void Mision_SinBasuraPorFotograma()
        {
            // El capítulo 5 en su etapa más cargada de condiciones (tenaza: All/Any/Not anidados).
            var runner = new MissionRunner(AltoDeLaAlianzaChapter.Build(Campaign.MissionTests.Read));
            runner.Facts.Set(AltoDeLaAlianzaChapter.Facts.MenInField, 40);
            // Se calienta hasta pasar las líneas iniciales: el historial de eventos crece con lo que ocurre, no por fotograma.
            Assert.That(Measure(() => runner.Step(Dt), warmupFrames: 60 * 45), Is.Zero);
        }

        [Test]
        public void Fusilero_SinBasuraPorFotograma()
        {
            var brain = new RiflemanBrain(WeaponCatalog.Get(WeaponCatalog.ComblainId), 1);
            var p = new RiflemanPerception { HasTarget = true, TargetDistanceM = 120f, LineOfSight = true, Loaded = true, HasAmmo = true };
            var rifle = new RifleCycleModel(WeaponCatalog.Get(WeaponCatalog.ComblainId), 1000);
            Assert.That(Measure(() =>
            {
                if (brain.Step(Dt, p).Fire) rifle.PullTrigger();
                rifle.Step(Dt);
            }), Is.Zero);
        }

        [Test]
        public void Escuadra_SinBasuraPorFotograma()
        {
            var positions = new List<Vec3>();
            for (int i = 0; i < 12; i++) positions.Add(new Vec3(i, 0f, 0f));
            var command = new SquadCommand(FormationType.Line, positions, new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 1f));
            var fire = new SquadFireControl(WeaponCatalog.Get(WeaponCatalog.RemingtonId), 12, 3);
            var suppression = new SuppressionModel();
            var supply = new SquadSupply(12, new SupplySettings(), 3);
            Assert.That(Measure(() =>
            {
                command.Step(Dt, positions);
                VolleyResult v = fire.Step(Dt, true, 200f, Posture.Standing, 0.3f);
                suppression.ReceiveFire(v.Shots, v.Hits, 0, 200f, 450f, 0.5f);
                suppression.Step(Dt);
                supply.Step(Dt, Exertion.Fighting, 1f);
            }), Is.Zero);
        }

        [Test]
        public void Humo_Naval_Choque_SinBasuraPorFotograma()
        {
            var smoke = new BlackPowderSmokeModel();
            var ship = new ShipMotionModel(ShipCatalog.Huascar());
            ship.Telegraph.Set(EngineOrder.FullAhead);
            var roll = new DeckRollModel();
            int frame = 0;
            Assert.That(Measure(() =>
            {
                if (frame++ % 20 == 0) smoke.Emit(new Vec3(0f, 1.5f, 0f), new Vec3(0f, 0f, 1f), 5f);
                smoke.Step(Dt);
                ship.Step(Dt);
                roll.Step(Dt);
                var shock = default(ShockStep);
                shock.Outcome = ShockOutcome.Fighting;
            }), Is.Zero);
        }
    }
}
