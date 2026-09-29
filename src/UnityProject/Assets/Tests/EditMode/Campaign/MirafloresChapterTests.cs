using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Campaign;
using M = Pacifico.Core.Campaign.MirafloresChapter;
using F = Pacifico.Core.Campaign.MirafloresChapter.Facts;
using G = Pacifico.Core.Campaign.MirafloresChapter.Flags;
using S = Pacifico.Core.Campaign.MirafloresChapter.Stages;

namespace Pacifico.Tests.Campaign
{
    /// <summary>ROADMAP 6.5 — Capítulo 8, «Los que no volvieron» (Miraflores), y el epílogo «La memoria rota».</summary>
    public class MirafloresChapterTests
    {
        private const float Dt = 1f / 30f;

        private static void Run(MissionRunner r, float seconds)
        {
            for (float t = 0f; t < seconds && r.State == MissionState.Running; t += Dt) r.Step(Dt);
        }

        private static string[] Words(string s) => s.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        [Test]
        public void Guion_ValidoYConElPrologoDelCorresponsalLiteral()
        {
            MissionScript script = M.Build(MissionTests.Read);
            Assert.That(script.Validate(), Is.Empty);
            var prologue = script.Stage(S.Prologue).OnEnter;
            Assert.That(prologue[0].Text, Is.EqualTo("Miraflores no era una fortaleza militar."));
            Assert.That(prologue.Last().Text, Does.EndWith("los ojos vacíos de piedad."));
            string guion = MissionTests.Read(GuionQuotes.ScriptFile).Replace("*", string.Empty);
            string joined = string.Join(" ", prologue.Select(l => l.Text));
            Assert.That(guion.Contains(joined), Is.True, "el prólogo es el texto del guion, entero y en orden");
            Assert.That(Pacifico.Core.Narrative.CollectibleCatalog.Definitions.Any(d => d.Id == script.RewardCollectibleId && d.Chapter == 8), Is.True);
        }

        [Test]
        public void Partida_Completa_HastaElDisparoYElEpilogo()
        {
            var r = new MissionRunner(M.Build(MissionTests.Read));
            Run(r, 60f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Gardens));
            r.Facts.Add(F.DefendersDown, 3);
            r.Facts.SetFlag(G.AtRedoubt);
            r.Step(Dt);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Redoubt));

            r.Facts.Add(F.RedoubtDown);
            r.Facts.SetFlag(G.EnemyFace);
            r.Step(Dt);
            Assert.That(r.Dialogue.Current.Text, Does.StartWith("No es un soldado"));
            r.Facts.Set(F.RedoubtDown, M.RedoubtDefenders);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Guns));

            r.Facts.Set(F.GunsSilenced, M.RedoubtGuns);
            Run(r, 30f);
            Assert.That(r.CurrentStage.Id, Is.EqualTo(S.Ending));
            Run(r, 15f);
            Assert.That(r.Facts.Flag(G.Shot), Is.False, "no hay disparo hasta que se sienta");
            r.Facts.SetFlag(G.Seated);
            r.Step(Dt);
            Assert.That(r.Facts.Flag(G.Shot), Is.False, "primero, la carta");
            Run(r, 30f);
            Assert.That(r.Facts.Flag(G.Shot), Is.True);
            r.Facts.SetFlag(G.PlayerDown); // el disparo: no es un fracaso
            r.Step(Dt);
            Assert.That(r.State, Is.EqualTo(MissionState.Running));
            r.Facts.SetFlag(G.EpilogueFinished);
            r.Step(Dt);
            Assert.That(r.State, Is.EqualTo(MissionState.Complete));
        }

        [Test]
        public void Partida_CaerEnElCombateEsFracaso()
        {
            var r = new MissionRunner(M.Build(MissionTests.Read));
            Run(r, 60f);
            r.Facts.SetFlag(G.PlayerDown);
            r.Step(Dt);
            Assert.That(r.State, Is.EqualTo(MissionState.Failed));
        }

        // ------------------------------------------------------------------------------------------
        // Epílogo
        // ------------------------------------------------------------------------------------------

        private static MemoriaRotaEpilogue Epilogue() => MemoriaRotaEpilogue.Load(MissionTests.Read);

        [Test]
        public void Epilogo_SeLeeDelGuion()
        {
            MemoriaRotaEpilogue e = Epilogue();
            Assert.That(e.Card, Is.EqualTo(new[]
            {
                "SOLDADO ABRAHAM QUIROZ", "Regimiento 3° de Línea / Batallón Cazadores del Desierto",
                "Natural de Quillota, Chile.", "Fallecido en las líneas de Miraflores, Enero de 1881.",
            }));
            Assert.That(e.Letter.First(), Is.EqualTo("Señor don Luciano Quiroz."));
            Assert.That(e.Letter.Last(), Does.EndWith("recuerde a su hijo que tanto lo quiso."));
            Assert.That(e.Statistic, Does.StartWith("Más de 20.000 seres humanos"));
            Assert.That(e.FinalQuote, Does.EndWith("y los que nunca volvieron."));
        }

        [Test]
        public void Epilogo_MosaicoSoloConRetratosDeEpoca()
        {
            foreach (string path in MemoriaRotaEpilogue.MosaicImages)
            {
                Assert.That(File.Exists(Path.Combine(TestPaths.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar))), Is.True, path);
                Assert.That(MemoriaRotaEpilogue.ExcludedModernImages.Any(m => path.EndsWith(m)), Is.False, path);
            }
            Assert.That(MemoriaRotaEpilogue.MosaicImages.Length, Is.GreaterThanOrEqualTo(8));
        }

        /// <summary>El epílogo entero a 60 FPS: en orden, sin saltos, la carta entera y un final en negro.</summary>
        [Test]
        public void Epilogo_ReproduccionFluidaA60FPS()
        {
            var timeline = new EpilogueTimeline(Epilogue());
            Assert.That(timeline.Duration, Is.InRange(60f, 180f));
            EpilogueFrame previous = timeline.Evaluate(0f);
            Assert.That(previous.Black, Is.EqualTo(1f));
            int lastCue = -1, shown = 0, lastLines = 0;
            for (float t = 1f / 60f; t <= timeline.Duration + 0.1f; t += 1f / 60f)
            {
                EpilogueFrame f = timeline.Evaluate(t);
                Assert.That(Math.Abs(f.Black - previous.Black), Is.LessThan(0.02f), "sin saltos a negro");
                Assert.That(Math.Abs(f.Mosaic - previous.Mosaic), Is.LessThan(0.02f));
                Assert.That(f.CardLines, Is.GreaterThanOrEqualTo(lastLines), "la lápida se escribe hacia delante");
                lastLines = f.CardLines;
                Assert.That(f.Quote > 0f && f.Card > 0f, Is.False, "la cita final no se superpone a la lápida");
                if (f.Cue >= 0 && f.Cue != lastCue)
                {
                    Assert.That(f.Cue, Is.EqualTo(lastCue + 1), "la carta, en orden");
                    lastCue = f.Cue;
                    shown++;
                }
                previous = f;
            }
            Assert.That(shown, Is.EqualTo(timeline.Cues.Count));
            // Regresión (previsualización de 6.5): bajo la cifra y la cita están todos los retratos, atenuados, no la mitad.
            EpilogueFrame underQuote = timeline.Evaluate(timeline.QuoteStart + 2f);
            Assert.That(underQuote.Mosaic, Is.EqualTo(1f));
            Assert.That(underQuote.MosaicAlpha, Is.InRange(0.15f, 0.35f));
            Assert.That(lastLines, Is.EqualTo(4));
            Assert.That(Words(string.Join(" ", timeline.Cues.Select(c => c.Text))), Is.EqualTo(Words(string.Join(" ", Epilogue().Letter))));
            EpilogueFrame end = timeline.Evaluate(timeline.Duration);
            Assert.That(end.Finished && end.Black >= 0.999f, Is.True, "acaba en negro");
        }
    }
}
