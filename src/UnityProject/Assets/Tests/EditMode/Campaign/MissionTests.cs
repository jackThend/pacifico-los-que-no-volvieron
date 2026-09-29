using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.1 — motor de misiones: hechos, etapas, objetivos, reacciones, diálogo y fracasos.</summary>
    public class MissionTests
    {
        private static MissionScript TwoStages()
        {
            var script = new MissionScript { Id = "prueba", Title = "Prueba" };
            var a = new MissionStage { Id = "a", Title = "A", Perspective = "p1" };
            a.OnEnter.Add(new MissionLine("X", "Hola."));
            a.Objectives.Add(new MissionObjective { Id = "tres", Text = "Tres disparos", Complete = MissionCondition.AtLeast("disparos", 3), ProgressFact = "disparos", ProgressTarget = 3 });
            var t = new MissionTrigger { Id = "primero", When = MissionCondition.AtLeast("disparos", 1), SetsFlag = "dijo" };
            t.Lines.Add(new MissionLine("X", "¡Bien!"));
            a.Triggers.Add(t);
            a.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.AtLeast("disparos", 3), MissionCondition.DialogueIdle()), "b"));
            var b = new MissionStage { Id = "b", Title = "B", Perspective = "p2" };
            b.Transitions.Add(new MissionTransition(MissionCondition.StageTime(5f), MissionScript.CompleteStage));
            script.Stages.Add(a);
            script.Stages.Add(b);
            script.Failures.Add(new MissionFailure(MissionCondition.Flag("desastre"), "Desastre"));
            return script;
        }

        private static void Run(MissionRunner runner, float seconds, float dt = 1f / 30f)
        {
            for (float t = 0f; t < seconds; t += dt) runner.Step(dt);
        }

        [Test]
        public void Runner_ObjetivosReaccionesYTransiciones()
        {
            var runner = new MissionRunner(TwoStages());
            Assert.That(runner.CurrentStage.Id, Is.EqualTo("a"));
            Assert.That(runner.Dialogue.Current.Text, Is.EqualTo("Hola."));
            MissionObjective objective = runner.CurrentStage.Objectives[0];

            runner.Facts.Add("disparos");
            runner.Step(0.1f);
            Assert.That(runner.Facts.Flag("dijo"), Is.True);
            Assert.That(runner.Describe(objective), Is.EqualTo("Tres disparos (1/3)"));
            Assert.That(runner.Progress(objective), Is.EqualTo(1f / 3f).Within(1e-5f));

            runner.Facts.Add("disparos", 2);
            runner.Step(0.1f);
            Assert.That(runner.IsObjectiveComplete(objective), Is.True);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo("a"), "espera a que termine el diálogo");
            Run(runner, 15f);
            Assert.That(runner.Visited, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(runner.State, Is.EqualTo(MissionState.Complete));
            Assert.That(runner.History.Count(e => e.Kind == MissionEventKind.TriggerFired), Is.EqualTo(1), "las reacciones se disparan una vez");
            Assert.That(runner.History.Count(e => e.Kind == MissionEventKind.ObjectiveCompleted), Is.EqualTo(1));
        }

        /// <summary>Regresión (6.5): una reacción que espera al diálogo no se dispara en el mismo paso en que otra lo empieza.</summary>
        [Test]
        public void Runner_LasReaccionesVenElDialogoRecienEmpezado()
        {
            var script = new MissionScript { Id = "x" };
            var stage = new MissionStage { Id = "a" };
            var speak = new MissionTrigger { Id = "habla", When = MissionCondition.Flag("señal") };
            speak.Lines.Add(new MissionLine("X", "Una línea que hay que dejar terminar."));
            stage.Triggers.Add(speak);
            stage.Triggers.Add(new MissionTrigger { Id = "despues", When = MissionCondition.All(MissionCondition.Flag("señal"), MissionCondition.DialogueIdle()), SetsFlag = "hecho" });
            stage.Transitions.Add(new MissionTransition(MissionCondition.Flag("hecho"), MissionScript.CompleteStage));
            script.Stages.Add(stage);
            var runner = new MissionRunner(script);
            runner.Facts.SetFlag("señal");
            runner.Step(0.1f);
            Assert.That(runner.Facts.Flag("hecho"), Is.False);
            Run(runner, 10f);
            Assert.That(runner.Facts.Flag("hecho"), Is.True);
        }

        [Test]
        public void Runner_Fracaso()
        {
            var runner = new MissionRunner(TwoStages());
            runner.Facts.SetFlag("desastre");
            runner.Step(0.1f);
            Assert.That(runner.State, Is.EqualTo(MissionState.Failed));
            Assert.That(runner.FailureReason, Is.EqualTo("Desastre"));
            runner.Step(10f);
            Assert.That(runner.CurrentStage.Id, Is.EqualTo("a"), "una misión fracasada no avanza");
        }

        [Test]
        public void Guion_InvalidoSeRechaza()
        {
            MissionScript bad = TwoStages();
            bad.Stages[0].Transitions.Add(new MissionTransition(MissionCondition.StageTime(1f), "no_existe"));
            var isolated = new MissionStage { Id = "isla" };
            bad.Stages.Add(isolated);
            List<string> errors = bad.Validate();
            Assert.That(errors.Any(e => e.Contains("no_existe")), Is.True);
            Assert.That(errors.Any(e => e.Contains("isla")), Is.True, "etapa sin salida");
            Assert.Throws<ArgumentException>(() => new MissionRunner(bad));
        }

        [Test]
        public void Dialogo_EnOrdenYConTiempoDeLectura()
        {
            var queue = new DialogueQueue();
            var said = new List<string>();
            queue.LineStarted += l => said.Add(l.Text);
            var shortLine = new MissionLine("A", "Uno.");
            var longLine = new MissionLine("B", new string('x', 150));
            Assert.That(shortLine.Seconds, Is.EqualTo(MissionLine.MinSeconds + MissionLine.PauseSeconds));
            Assert.That(longLine.Seconds, Is.EqualTo(150f / MissionLine.CharactersPerSecond + MissionLine.PauseSeconds).Within(1e-4f));

            queue.Enqueue(shortLine);
            queue.Enqueue(shortLine);
            queue.Enqueue(longLine);
            // Un paso enorme consume las dos líneas cortas y reparte el sobrante.
            queue.Step(shortLine.Seconds * 2f + 1f);
            Assert.That(queue.Current, Is.SameAs(longLine));
            Assert.That(queue.CurrentElapsed, Is.EqualTo(1f).Within(1e-4f));
            queue.Interrupt(new MissionLine("C", "¡Alto!"));
            Assert.That(queue.Current.Text, Is.EqualTo("¡Alto!"));
            Assert.That(queue.PendingCount, Is.Zero);
            queue.Step(10f);
            Assert.That(queue.IsIdle, Is.True);
            Assert.That(said, Is.EqualTo(new[] { "Uno.", "Uno.", longLine.Text, "¡Alto!" }));
        }

        [Test]
        public void Citas_SeLeenDelCapituloDelGuion()
        {
            GuionQuotes q = GuionQuotes.Load(Read, IquiqueChapter.ChapterHeading);
            StringAssert.Contains("MADERA Y BLINDAJE", q.ChapterTitle);
            Assert.That(q.Find("¡Muchachos"), Does.EndWith("espero que no sea esta la ocasión de hacerlo!"));
            Assert.That(q.Find("¡Al abordaje"), Is.EqualTo("¡Al abordaje, muchachos!"));
            Assert.That(q.Find("¡Fuego a la línea"), Is.EqualTo("¡Fuego a la línea de flotación! Que termine pronto su agonía."));
            Assert.That(q.FirstSentence("¡Fuego no!"), Is.EqualTo("¡Fuego no!"));
            Assert.That(q.All.Any(s => s.StartsWith("Dignísima señora")), Is.True, "la carta de Grau");
            Assert.Throws<KeyNotFoundException>(() => q.Find("¡Viva el rey"));
            // Las citas de otro capítulo no se mezclan.
            Assert.That(q.All.Any(s => s.Contains("Hijos del Zepita")), Is.False);
        }

        internal static string Read(string relative) =>
            File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
    }
}
