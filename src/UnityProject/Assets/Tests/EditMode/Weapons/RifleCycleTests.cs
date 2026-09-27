using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Weapons;

namespace Pacifico.Tests.Weapons
{
    /// <summary>ROADMAP 3.2 — ciclo percutir → abrir → expulsar → insertar → cerrar → apuntar, sincronizado.</summary>
    public class RifleCycleTests
    {
        /// <summary>Ejecuta el ciclo con pasos fijos y devuelve todos los eventos con su instante absoluto.</summary>
        private static List<(float Time, RifleEvent Event)> Run(RifleCycleModel rifle, float seconds, float dt)
        {
            var log = new List<(float, RifleEvent)>();
            float t = 0f;
            int steps = (int)Math.Ceiling(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                foreach (RifleEvent e in rifle.Step(dt)) log.Add((t + e.TimeOffset, e));
                t += dt;
            }
            return log;
        }

        private static RifleStage[] StagesOf(IEnumerable<(float Time, RifleEvent Event)> log) =>
            log.Where(x => x.Event.Type == RifleEventType.StageStarted).Select(x => x.Event.Stage).ToArray();

        [Test]
        public void Comblain_SecuenciaDelRoadmap()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 10);
            Assert.That(rifle.PullTrigger(), Is.EqualTo(TriggerResult.Fired));
            var log = Run(rifle, 3f, 1f / 60f);
            Assert.That(StagesOf(log), Is.EqualTo(new[]
            {
                RifleStage.Firing, RifleStage.OpeningAction, RifleStage.Extracting,
                RifleStage.InsertingCartridge, RifleStage.ClosingAction, RifleStage.Shouldering,
            }), "percutir → abrir → expulsar vaina → insertar → cerrar → apuntar");
        }

        [Test]
        public void Chassepot_CartuchoDePapel_SinVainaQueExpulsar_YAmartillaAMano()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Chassepot(), reserve: 10);
            rifle.PullTrigger();
            var log = Run(rifle, 3f, 1f / 60f);
            var stages = StagesOf(log);
            Assert.That(stages, Does.Not.Contain(RifleStage.Extracting));
            Assert.That(stages[1], Is.EqualTo(RifleStage.Cocking));
            Assert.That(log.Any(x => x.Event.Type == RifleEventType.CaseEjected), Is.False);
        }

        [Test]
        public void Remington_AmartillaAntesDeAbrirElBloque()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Remington(), reserve: 10);
            rifle.PullTrigger();
            var stages = StagesOf(Run(rifle, 3f, 1f / 60f));
            Assert.That(Array.IndexOf(stages, RifleStage.Cocking), Is.LessThan(Array.IndexOf(stages, RifleStage.OpeningAction)));
            Assert.That(stages, Does.Contain(RifleStage.Extracting));
        }

        [TestCase(WeaponCatalog.ComblainId, 2.0f)]
        [TestCase(WeaponCatalog.ChassepotId, 2.2f)]
        [TestCase(WeaponCatalog.GrasId, 2.2f)]
        [TestCase(WeaponCatalog.RemingtonId, 2.1f)]
        public void CicloCompleto_DuraExactamenteLaRecargaDeLaFicha(string id, float seconds)
        {
            var weapon = WeaponCatalog.Get(id);
            Assert.That(RifleCycleProfile.For(weapon).FireCycleSeconds, Is.EqualTo(seconds).Within(1e-4f));

            // Medido sobre la simulación: del gatillo a CycleCompleted.
            var rifle = new RifleCycleModel(weapon, reserve: 10);
            rifle.PullTrigger();
            var log = Run(rifle, seconds + 0.5f, 1f / 60f);
            float completed = log.First(x => x.Event.Type == RifleEventType.CycleCompleted).Time;
            Assert.That(completed, Is.EqualTo(seconds).Within(1e-4f));
            Assert.That(rifle.IsReady && rifle.Chambered, Is.True);
        }

        /// <summary>
        /// Criterio de 3.2: temporizador y animaciones sincronizados. Una animación que dure lo que anuncia cada
        /// StageStarted termina justo cuando empieza la etapa siguiente, y los instantes no dependen del paso:
        /// un fotograma de 250 ms que abarca varias etapas emite todos sus eventos, en orden y en su momento exacto.
        /// </summary>
        [Test]
        public void Sincronia_LosEventosCaenEnElMismoInstanteConCualquierPaso()
        {
            List<(float Time, RifleEvent Event)> Timeline(float dt)
            {
                var rifle = new RifleCycleModel(WeaponCatalog.Gras(), reserve: 10);
                rifle.PullTrigger();
                return Run(rifle, 3f, dt);
            }

            var reference = Timeline(1f / 240f);
            foreach (float dt in new[] { 1f / 144f, 1f / 60f, 1f / 30f, 0.25f })
            {
                var other = Timeline(dt);
                Assert.That(other.Select(x => x.Event.Type), Is.EqualTo(reference.Select(x => x.Event.Type)), "dt = " + dt);
                for (int i = 0; i < reference.Count; i++)
                {
                    Assert.That(other[i].Time, Is.EqualTo(reference[i].Time).Within(1e-4f), reference[i].Event + " con dt = " + dt);
                }
            }

            // Cada etapa empieza cuando termina la anterior: la suma de duraciones anunciadas encadena el ciclo.
            var starts = reference.Where(x => x.Event.Type == RifleEventType.StageStarted).ToList();
            for (int i = 1; i < starts.Count; i++)
            {
                Assert.That(starts[i].Time, Is.EqualTo(starts[i - 1].Time + starts[i - 1].Event.Duration).Within(1e-4f));
            }
        }

        [Test]
        public void ProgresoDeEtapa_EsMonotonoYCompleto()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 5);
            rifle.PullTrigger();
            rifle.Step(0f);
            RifleStage stage = rifle.Stage;
            float previous = -1f;
            for (int i = 0; i < 400 && !rifle.IsReady; i++) // 400 × 1/120 s = 3,3 s > ciclo de 2,0 s
            {
                rifle.Step(1f / 120f);
                if (rifle.Stage != stage)
                {
                    stage = rifle.Stage;
                    previous = -1f;
                }
                Assert.That(rifle.StageProgress, Is.GreaterThan(previous).Or.EqualTo(1f));
                previous = rifle.StageProgress;
            }
            Assert.That(rifle.IsReady, Is.True);
        }

        [Test]
        public void EventosDeMecanica_EnSuEtapa()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 10);
            rifle.PullTrigger();
            var log = Run(rifle, 3f, 1f / 60f);
            var ejected = log.Single(x => x.Event.Type == RifleEventType.CaseEjected);
            var chambered = log.Single(x => x.Event.Type == RifleEventType.CartridgeChambered);
            var insertStart = log.Single(x => x.Event.Type == RifleEventType.StageStarted && x.Event.Stage == RifleStage.InsertingCartridge);
            Assert.That(ejected.Time, Is.EqualTo(insertStart.Time).Within(1e-4f), "la vaina sale justo al acabar la extracción");
            Assert.That(chambered.Time, Is.EqualTo(insertStart.Time + insertStart.Event.Duration).Within(1e-4f));
            Assert.That(rifle.Reserve, Is.EqualTo(9));
        }

        [Test]
        public void NoSePuedeDispararHastaCompletarElCiclo()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 10);
            rifle.PullTrigger();
            rifle.Step(1.9f);
            Assert.That(rifle.PullTrigger(), Is.EqualTo(TriggerResult.Busy));
            rifle.Step(0.11f);
            Assert.That(rifle.PullTrigger(), Is.EqualTo(TriggerResult.Fired));
        }

        [Test]
        public void Pausa_CongelaLaRecarga()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 10);
            rifle.PullTrigger();
            rifle.Step(0.5f);
            RifleStage stage = rifle.Stage;
            float elapsed = rifle.StageElapsed;
            rifle.Step(5f, paused: true);
            Assert.That(rifle.Stage, Is.EqualTo(stage));
            Assert.That(rifle.StageElapsed, Is.EqualTo(elapsed));
        }

        [Test]
        public void SinMunicion_ElUltimoDisparoDejaElArmaVaciaConLaVaina()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 0);
            rifle.PullTrigger();
            var log = Run(rifle, 1f, 1f / 60f);
            Assert.That(log.Any(x => x.Event.Type == RifleEventType.OutOfAmmo), Is.True);
            Assert.That(rifle.Chambered, Is.False);
            Assert.That(rifle.SpentCaseInChamber, Is.True);
            Assert.That(rifle.PullTrigger(), Is.EqualTo(TriggerResult.DryFire), "clic");
            Assert.That(rifle.Reload(), Is.False);

            // Tarapacá: se recogen cartuchos y se recarga a mano; ahora sí hay que extraer la vaina.
            rifle.AddAmmo(5);
            Assert.That(rifle.Reload(), Is.True);
            var reload = Run(rifle, 3f, 1f / 60f);
            Assert.That(StagesOf(reload)[0], Is.EqualTo(RifleStage.OpeningAction));
            Assert.That(reload.Any(x => x.Event.Type == RifleEventType.CaseEjected), Is.True);
            Assert.That(rifle.Chambered, Is.True);
            Assert.That(rifle.Reserve, Is.EqualTo(4));
        }

        [Test]
        public void SinRecargaAutomatica_EsperaALaTeclaR()
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Gras(), reserve: 10) { AutoReload = false };
            rifle.PullTrigger();
            Run(rifle, 1f, 1f / 60f);
            Assert.That(rifle.IsReady && !rifle.Chambered, Is.True);
            Assert.That(rifle.Reload(), Is.True);
            Run(rifle, 3f, 1f / 60f);
            Assert.That(rifle.Chambered, Is.True);
        }

        [Test]
        public void Winchester_DisparaDelDeposito_YRecargaCartuchoACartucho()
        {
            var winchester = new RifleCycleModel(WeaponCatalog.Winchester(), reserve: 20);
            Assert.That(winchester.MagazineRounds, Is.EqualTo(12));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(winchester.PullTrigger(), Is.EqualTo(TriggerResult.Fired));
                Run(winchester, 1f, 1f / 60f);
            }
            Assert.That(winchester.MagazineRounds, Is.EqualTo(9));
            Assert.That(winchester.Chambered, Is.True, "la palanca alimenta desde el depósito");

            Assert.That(winchester.Reload(), Is.True);
            var log = Run(winchester, 3 * winchester.Weapon.ReloadSeconds + 0.01f, 1f / 60f);
            Assert.That(log.Count(x => x.Event.Type == RifleEventType.CartridgeLoaded), Is.EqualTo(3));
            Assert.That(winchester.MagazineRounds, Is.EqualTo(12));
            Assert.That(winchester.Reserve, Is.EqualTo(17));
        }

        [Test]
        public void Winchester_DispararInterrumpeLaCargaDelDeposito()
        {
            var winchester = new RifleCycleModel(WeaponCatalog.Winchester(), reserve: 20, startChambered: true, startMagazine: 5);
            winchester.Reload();
            winchester.Step(winchester.Weapon.ReloadSeconds * 1.5f); // un cartucho dentro y otro a medias
            Assert.That(winchester.MagazineRounds, Is.EqualTo(6));
            Assert.That(winchester.PullTrigger(), Is.EqualTo(TriggerResult.Fired), "recarga dinámica: se puede disparar");
            Assert.That(winchester.Stage, Is.EqualTo(RifleStage.Firing));
            Assert.That(winchester.Reserve, Is.EqualTo(19), "el cartucho a medio meter no se pierde ni se cuenta");
        }

        [Test]
        public void Winchester_ConLaRecamaraVacia_LaRecargaAccionaLaPalanca()
        {
            var winchester = new RifleCycleModel(WeaponCatalog.Winchester(), reserve: 5, startChambered: false, startMagazine: 0);
            Assert.That(winchester.PullTrigger(), Is.EqualTo(TriggerResult.DryFire));
            winchester.Reload();
            var log = Run(winchester, 5f, 1f / 60f);
            Assert.That(winchester.Chambered, Is.True);
            Assert.That(winchester.MagazineRounds, Is.EqualTo(4));
            Assert.That(log.Any(x => x.Event.Type == RifleEventType.CaseEjected), Is.False, "no había vaina disparada");
        }

        /// <summary>
        /// La pieza del cierre, el martillo y el retroceso visible se derivan de la etapa y su progreso: se muestrea el
        /// ciclo completo a 60 FPS y ninguna curva salta al pasar de una etapa a la siguiente.
        /// </summary>
        [TestCase(WeaponCatalog.ComblainId)]
        [TestCase(WeaponCatalog.RemingtonId)]
        [TestCase(WeaponCatalog.ChassepotId)]
        public void Animacion_ContinuaEntreEtapas(string id)
        {
            var rifle = new RifleCycleModel(WeaponCatalog.Get(id), reserve: 5);
            bool manual = rifle.Profile.NeedsManualCocking;
            rifle.PullTrigger();
            rifle.Step(0f);
            float previousOpen = 0f, previousHammer = 0f, maxOpenJump = 0f, maxHammerJump = 0f;
            bool first = true;
            for (int i = 0; i < 300 && !rifle.IsReady; i++)
            {
                rifle.Step(1f / 60f);
                float open = RifleAnimationCurves.ActionOpen(rifle.Stage, rifle.StageProgress, false);
                float hammer = RifleAnimationCurves.HammerCocked(rifle.Stage, rifle.StageProgress, manual, rifle.IsCocked);
                if (!first)
                {
                    maxOpenJump = Math.Max(maxOpenJump, Math.Abs(open - previousOpen));
                    maxHammerJump = Math.Max(maxHammerJump, Math.Abs(hammer - previousHammer));
                }
                first = false;
                previousOpen = open;
                previousHammer = hammer;
            }
            // La etapa más corta (amartillar, ~0,15 s) son 9 fotogramas: el paso máximo con smoothstep ronda 0,17.
            Assert.That(maxOpenJump, Is.LessThan(0.35f), id + ": el cierre no salta entre etapas");
            Assert.That(maxHammerJump, Is.LessThan(0.35f), id + ": el martillo no salta entre etapas");
            Assert.That(RifleAnimationCurves.ActionOpen(RifleStage.Ready, 0f, false), Is.EqualTo(0f), "arma lista = cierre cerrado");
        }

        [Test]
        public void Martillo_TrasElUltimoDisparo_QuedaAbatidoYSeMontaSinSaltos()
        {
            var remington = new RifleCycleModel(WeaponCatalog.Remington(), reserve: 0);
            Assert.That(remington.IsCocked, Is.True, "arma cargada y montada al empezar");
            remington.PullTrigger();
            Run(remington, 1f, 1f / 60f);
            Assert.That(remington.IsCocked, Is.False);
            Assert.That(RifleAnimationCurves.HammerCocked(remington.Stage, remington.StageProgress, true, remington.IsCocked), Is.EqualTo(0f),
                "sin munición el martillo se ve abatido");

            remington.AddAmmo(1);
            remington.Reload();
            remington.Step(1f / 60f);
            Assert.That(remington.Stage, Is.EqualTo(RifleStage.Cocking));
            Assert.That(RifleAnimationCurves.HammerCocked(remington.Stage, remington.StageProgress, true, remington.IsCocked), Is.LessThan(0.1f),
                "empieza a montarse desde abajo, sin saltar");
            Run(remington, 3f, 1f / 60f);
            Assert.That(remington.IsCocked, Is.True);
        }

        [Test]
        public void Encare_SoloSeBloqueaMientrasSeManipulaElFusil()
        {
            var comblain = new RifleCycleModel(WeaponCatalog.Comblain(), reserve: 5);
            Assert.That(comblain.BlocksAiming, Is.False);
            comblain.PullTrigger();
            var blocked = new List<RifleStage>();
            for (int i = 0; i < 200 && !(comblain.IsReady && i > 0); i++)
            {
                comblain.Step(1f / 60f);
                if (comblain.BlocksAiming && !blocked.Contains(comblain.Stage)) blocked.Add(comblain.Stage);
            }
            Assert.That(blocked, Is.EqualTo(new[]
            {
                RifleStage.OpeningAction, RifleStage.Extracting, RifleStage.InsertingCartridge, RifleStage.ClosingAction,
            }));
        }

        [Test]
        public void Winchester_ElCicloDePalancaNoBajaLasMiras()
        {
            var winchester = new RifleCycleModel(WeaponCatalog.Winchester(), reserve: 10);
            winchester.PullTrigger();
            for (int i = 0; i < 60; i++)
            {
                winchester.Step(1f / 60f);
                Assert.That(winchester.BlocksAiming, Is.False, winchester.Stage.ToString());
            }
        }

        [Test]
        public void TodosLosFusilesDelCatalogo_TienenPerfilValido()
        {
            foreach (var weapon in WeaponCatalog.All().Where(w => w.IsFirearm))
            {
                var profile = RifleCycleProfile.For(weapon);
                Assert.That(profile.FireCycle[0].Stage, Is.EqualTo(RifleStage.Firing), weapon.Id);
                Assert.That(profile.FireCycle.All(s => s.Seconds > 0f), Is.True, weapon.Id);
                Assert.That(profile.FireCycle.Last().Stage, Is.EqualTo(RifleStage.Shouldering), weapon.Id);
            }
            Assert.Throws<ArgumentException>(() => RifleCycleProfile.For(WeaponCatalog.Corvo()));
        }
    }
}
