using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Narrative;

namespace Pacifico.Tests.Narrative
{
    /// <summary>ROADMAP 5.2 — cinemáticas de corresponsal: guion, subtítulos sincronizados y el prólogo «El Ojo de Europa».</summary>
    public class CinematicTests
    {
        private static string Read(string relative) => File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        private static CinematicScript Prologue() => PrologueCinematic.LoadScript(Read);

        private static string[] Words(string s) =>
            s.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // ------------------------------------------------------------------------------------------
        // Guion
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Guion_ElPrologoSeLeeDelGuion()
        {
            CinematicScript s = Prologue();
            Assert.That(s.Title, Is.EqualTo("EL OJO DE EUROPA"));
            StringAssert.Contains("George F. Morice", s.Narrator);
            StringAssert.Contains("The Times", s.Narrator);
            StringAssert.Contains("Valparaíso", s.Location);
            StringAssert.Contains("chelo", s.MusicCue);
            Assert.That(s.Paragraphs.Count, Is.EqualTo(4));
            StringAssert.StartsWith("Londres nos envió", s.Paragraphs[0]);
            Assert.That(s.Paragraphs[1], Is.EqualTo("Cuán equivocados estábamos."));
            StringAssert.EndsWith("jamás regresaron.", s.Paragraphs[3]);
            Assert.That(s.Paragraphs.All(p => p.IndexOf('*') < 0 && p.IndexOf('"') < 0), Is.True, "sin marcas ni comillas");
        }

        [Test]
        public void Guion_OtrasSeccionesYErrores()
        {
            const string md = "## CAPÍTULO X: PRUEBA\n**Voz en Off:** *Alguien*\n> *(Música: tambores).*\n\n**NARRADOR:**\n> *\"Uno dos.*\n>\n> *Tres.\"*\n\n---\n## OTRO";
            CinematicScript s = CinematicScriptParser.Parse(md, "PRUEBA");
            Assert.That(s.Title, Is.EqualTo("PRUEBA"));
            Assert.That(s.Narrator, Is.EqualTo("Alguien"));
            Assert.That(s.MusicCue, Is.EqualTo("Música: tambores"));
            Assert.That(s.Paragraphs, Is.EqualTo(new[] { "Uno dos.", "Tres." }));
            Assert.Throws<ArgumentException>(() => CinematicScriptParser.Parse(md, "NO EXISTE"));
        }

        // ------------------------------------------------------------------------------------------
        // Subtítulos
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Subtitulos_CumplenLasNormasDeLectura()
        {
            var settings = new SubtitleSettings();
            var cues = SubtitleBuilder.Build(Prologue().Paragraphs, settings);
            Assert.That(cues.Count, Is.GreaterThan(10));
            for (int i = 0; i < cues.Count; i++)
            {
                SubtitleCue c = cues[i];
                Assert.That(c.Lines.Length, Is.InRange(1, settings.MaxLines), c.Text);
                Assert.That(c.Lines.All(l => l.Length <= settings.MaxCharsPerLine), Is.True, c.Text);
                Assert.That(c.CharactersPerSecond, Is.LessThanOrEqualTo(settings.MaxCharsPerSecond + 1e-3f), c.Text);
                Assert.That(c.Duration, Is.InRange(settings.MinSeconds, settings.MaxSeconds), c.Text);
                if (i > 0) Assert.That(c.Start - cues[i - 1].End, Is.GreaterThanOrEqualTo(settings.MinGapSeconds - 1e-4f), "sin solaparse");
            }
        }

        [Test]
        public void Subtitulos_SonElTextoDelGuionPalabraPorPalabra()
        {
            CinematicScript s = Prologue();
            var cues = SubtitleBuilder.Build(s.Paragraphs);
            Assert.That(Words(string.Join(" ", cues.Select(c => c.Text))), Is.EqualTo(Words(string.Join(" ", s.Paragraphs))));
            // El golpe de efecto va solo, en su propio subtítulo.
            Assert.That(cues.Count(c => c.Text == "Cuán equivocados estábamos."), Is.EqualTo(1));
        }

        [Test]
        public void Subtitulos_NoSeparanArticuloYNombre()
        {
            string[] lines = SubtitleBuilder.BreakLines("Y sobre las aguas del Pacífico sur se preparaban", 42);
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0].EndsWith(" del"), Is.False, string.Join(" / ", lines));
            Assert.That(SubtitleBuilder.BreakLines("Cuán equivocados estábamos.", 42), Has.Length.EqualTo(1));
        }

        [Test]
        public void Subtitulos_SeAjustanALaPistaDeVoz()
        {
            var cues = SubtitleBuilder.Build(Prologue().Paragraphs, null, 5f);
            // Una locución grabada algo más lenta que la estimada (el actor se toma su tiempo).
            var fitted = SubtitleBuilder.FitTo(cues, 5f, 5f + 90f);
            Assert.That(fitted[0].Start, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(fitted[fitted.Count - 1].End, Is.EqualTo(95f).Within(1e-3f));
            for (int i = 1; i < fitted.Count; i++) Assert.That(fitted[i].Start, Is.GreaterThan(fitted[i - 1].End));
            Assert.That(fitted.Max(c => c.CharactersPerSecond), Is.LessThanOrEqualTo(17f), "más lenta: más fácil de leer");
        }

        [Test]
        public void BusquedaDelSubtituloVisible()
        {
            var cues = SubtitleBuilder.Build(Prologue().Paragraphs, null, 5f);
            Assert.That(SubtitleBuilder.CueAt(cues, 0f), Is.EqualTo(-1), "antes de la voz");
            for (int i = 0; i < cues.Count; i++)
            {
                Assert.That(SubtitleBuilder.CueAt(cues, (cues[i].Start + cues[i].End) * 0.5f), Is.EqualTo(i));
                Assert.That(SubtitleBuilder.CueAt(cues, cues[i].End + 0.01f), Is.EqualTo(-1).Or.EqualTo(i + 1));
            }
        }

        // ------------------------------------------------------------------------------------------
        // Verificación de ROADMAP 5.2: reproducción fluida del prólogo
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Prologo_LasImagenesExistenEnElArchivo()
        {
            CinematicTimeline t = PrologueCinematic.Build(Prologue());
            foreach (CinematicShot shot in t.Shots)
            {
                Assert.That(File.Exists(Path.Combine(TestPaths.RepositoryRoot, shot.Image.Replace('/', Path.DirectorySeparatorChar))), Is.True, shot.Image);
            }
        }

        /// <summary>
        /// Se reproduce el prólogo entero a 60 fotogramas por segundo y se comprueba cada fotograma: siempre hay imagen
        /// (salvo el negro de entrada y de salida), los fundidos y los movimientos de cámara no saltan, cada subtítulo
        /// aparece sobre la imagen de su párrafo y el título se retira antes de la primera palabra.
        /// </summary>
        [Test]
        public void Prologo_ReproduccionFluidaA60FPS()
        {
            CinematicScript script = Prologue();
            CinematicTimeline t = PrologueCinematic.Build(script);
            Assert.That(t.Duration, Is.InRange(60f, 150f), "entre uno y dos minutos y medio");

            const float dt = 1f / 60f;
            CinematicFrame previous = t.Evaluate(0f);
            int shownCues = 0, lastCue = -1;
            for (float time = dt; time <= t.Duration + dt; time += dt)
            {
                CinematicFrame f = t.Evaluate(time);
                bool inFades = time < CinematicTimeline.FadeInSeconds || time > t.Duration - CinematicTimeline.FadeOutSeconds;
                Assert.That(f.ShotA, Is.GreaterThanOrEqualTo(0), "siempre hay un plano");
                if (!inFades) Assert.That(f.Black, Is.EqualTo(0f), "sin negros a mitad");
                Assert.That(Math.Abs(f.Black - previous.Black), Is.LessThan(0.02f), "los fundidos a negro no saltan");
                Assert.That(Math.Abs(f.Title - previous.Title), Is.LessThan(0.05f), "el rótulo no salta");

                // La imagen visible cambia de forma continua: o sigue el mismo plano, o el fundido lo sustituye.
                if (f.ShotA == previous.ShotA)
                {
                    Assert.That(Math.Abs(f.FrameA.Zoom - previous.FrameA.Zoom), Is.LessThan(0.01f), "movimiento de cámara suave");
                    Assert.That(Math.Abs(f.FrameA.CenterX - previous.FrameA.CenterX), Is.LessThan(0.01f));
                }
                else
                {
                    Assert.That(previous.ShotB, Is.EqualTo(f.ShotA), "el plano nuevo entra por fundido");
                    Assert.That(previous.Blend, Is.GreaterThan(0.99f), "el fundido había terminado");
                }
                if (f.ShotB >= 0 && previous.ShotB == f.ShotB) Assert.That(Math.Abs(f.Blend - previous.Blend), Is.LessThan(0.05f));

                if (f.Cue >= 0)
                {
                    Assert.That(f.Title, Is.EqualTo(0f), "el rótulo no tapa los subtítulos");
                    string image = t.Shots[f.ShotA].Image;
                    string[] expected = PrologueCinematic.ImagesOf(t.Cues[f.Cue].Paragraph);
                    bool matches = expected.Contains(image) || (f.ShotB >= 0 && expected.Contains(t.Shots[f.ShotB].Image));
                    Assert.That(matches, Is.True, "subtítulo «" + t.Cues[f.Cue].Text + "» sobre " + image);
                    if (f.Cue != lastCue)
                    {
                        Assert.That(f.Cue, Is.EqualTo(lastCue + 1), "en orden, sin saltarse ninguno");
                        shownCues++;
                        lastCue = f.Cue;
                    }
                }
                previous = f;
            }
            Assert.That(shownCues, Is.EqualTo(t.Cues.Count), "se ven todos los subtítulos");
            Assert.That(t.Evaluate(t.Duration).Finished, Is.True);
            Assert.That(t.Evaluate(t.Duration).Black, Is.EqualTo(1f), "acaba en negro");
        }

        [Test]
        public void Prologo_ConPistaDeVoz_SubtitulosSincronizados()
        {
            CinematicTimeline t = PrologueCinematic.Build(Prologue(), narrationSeconds: 100f);
            Assert.That(t.Cues[0].Start, Is.EqualTo(t.NarrationStart));
            Assert.That(t.Cues[t.Cues.Count - 1].End - t.Cues[0].Start, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(t.Duration, Is.EqualTo(t.NarrationStart + 100f + CinematicTimeline.TailSeconds + CinematicTimeline.FadeOutSeconds).Within(1e-3f));
        }

        [Test]
        public void Prologo_SeEvaluaIgualAlSaltarORebobinar()
        {
            CinematicTimeline t = PrologueCinematic.Build(Prologue());
            CinematicFrame a = t.Evaluate(42.5f);
            t.Evaluate(80f);
            t.Evaluate(3f);
            CinematicFrame b = t.Evaluate(42.5f);
            Assert.That(b.ShotA, Is.EqualTo(a.ShotA));
            Assert.That(b.Cue, Is.EqualTo(a.Cue));
            Assert.That(b.FrameA.Zoom, Is.EqualTo(a.FrameA.Zoom));
        }
    }
}
