using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Narrative;

namespace Pacifico.Tests.Narrative
{
    /// <summary>ROADMAP 5.1 — visor 3D de documentos: interacción, proporciones y transcripción.</summary>
    public class DocumentViewerTests
    {
        private static CollectibleRecord Grau() =>
            CollectibleCatalog.BuildFromRepository(TestPaths.RepositoryRoot).Single(r => r.Id == CollectibleCatalog.GrauLetterId);

        private static string Repo(string relative) => Path.Combine(TestPaths.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        private static void Settle(DocumentViewerModel m, float seconds = 2f, float dt = 1f / 60f)
        {
            for (float t = 0f; t < seconds; t += dt) m.Step(dt);
        }

        // ------------------------------------------------------------------------------------------
        // Imágenes y medidas
        // ------------------------------------------------------------------------------------------

        [Test]
        public void TamanoDeImagen_JpegYPng()
        {
            Assert.That(ImageInfo.TryReadSize(Repo("Archivo_Historico/05_Cartas_y_Documentos/01_Facsimil_Monumento_Carta_Grau_a_Carmela_Carvajal.jpg"), out int w, out int h), Is.True);
            Assert.That((w, h), Is.EqualTo((2736, 3648)), "foto de cámara con cabecera EXIF");
            Assert.That(ImageInfo.TryReadSize(Repo("Archivo_Historico/05_Cartas_y_Documentos/05_Plano_Militar_Batalla_de_Arica_1880.jpg"), out w, out h), Is.True);
            Assert.That((w, h), Is.EqualTo((1259, 1165)));
            string png = Directory.GetFiles(TestPaths.HistoricalArchive, "*.png", SearchOption.AllDirectories).First();
            Assert.That(ImageInfo.TryReadSize(png, out w, out h), Is.True, png);
            Assert.That(w, Is.GreaterThan(0));
            Assert.That(ImageInfo.TryReadSize(new byte[40], out _, out _), Is.False, "no es una imagen");
            Assert.That(ImageInfo.TryReadSize(Repo("Archivo_Historico/no_existe.jpg"), out _, out _), Is.False);
        }

        [Test]
        public void Medidas_SegunElSoporte()
        {
            DocumentShape letter = DocumentShape.For(DocumentForm.Letter, 2736f / 3648f);
            Assert.That(letter.HeightM, Is.EqualTo(0.27f).Within(1e-5f));
            Assert.That(letter.WidthM / letter.HeightM, Is.EqualTo(0.75f).Within(1e-4f), "la proporción del facsímil");
            Assert.That(letter.BendM, Is.GreaterThan(0f), "el papel se curva");

            DocumentShape plate = DocumentShape.For(DocumentForm.Daguerreotype, 7f / 8.3f);
            Assert.That(plate.HeightM, Is.EqualTo(0.083f).Within(1e-5f), "sexto de placa");
            Assert.That(plate.BendM, Is.EqualTo(0f), "placa rígida");

            DocumentShape map = DocumentShape.For(DocumentForm.Map, 1259f / 1165f);
            Assert.That(map.WidthM, Is.EqualTo(0.6f).Within(1e-5f), "apaisado: el lado mayor es el ancho");
            Assert.That(DocumentShape.For(DocumentForm.Letter, float.NaN).WidthM, Is.GreaterThan(0f), "proporción inválida → la de una carta");
        }

        // ------------------------------------------------------------------------------------------
        // Interacción
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Girar_YVoltearMuestraElReverso()
        {
            var m = new DocumentViewerModel(DocumentShape.For(DocumentForm.Letter, 0.75f));
            m.Rotate(100f, 0f);
            Settle(m);
            Assert.That(m.Yaw, Is.EqualTo(35f).Within(0.01f));
            Assert.That(m.ShowingReverse, Is.False);
            m.Flip();
            Settle(m);
            Assert.That(m.ShowingReverse, Is.True, "tras voltear se ve el reverso");
            Assert.That(m.Pitch, Is.EqualTo(0f));
            m.Flip();
            Settle(m);
            Assert.That(m.ShowingReverse, Is.False, "y de nuevo el anverso");
            m.Rotate(0f, -10000f);
            Settle(m);
            Assert.That(m.Pitch, Is.EqualTo(DocumentViewerModel.MaxPitchDeg).Within(0.01f), "la inclinación está limitada");
        }

        [Test]
        public void Zoom_HaciaElCursor_ElPuntoBajoElCursorNoSeMueve()
        {
            var m = new DocumentViewerModel(DocumentShape.For(DocumentForm.Letter, 0.75f));
            // Cursor en la firma (abajo a la derecha de la vista).
            float cx = 0.3f, cy = -0.35f;
            float docX = m.TargetPanX + cx / m.TargetZoom, docY = m.TargetPanY + cy / m.TargetZoom;
            m.ZoomAt(3f, cx, cy);
            Assert.That(m.TargetZoom, Is.EqualTo((float)Math.Pow(1.2, 3)).Within(1e-4f));
            Assert.That(m.TargetPanX + cx / m.TargetZoom, Is.EqualTo(docX).Within(1e-4f));
            Assert.That(m.TargetPanY + cy / m.TargetZoom, Is.EqualTo(docY).Within(1e-4f));
        }

        [Test]
        public void LaVista_NuncaSeSaleDelDocumento()
        {
            var m = new DocumentViewerModel(DocumentShape.For(DocumentForm.Letter, 0.75f));
            var rng = new Random(5);
            for (int i = 0; i < 2000; i++)
            {
                switch (rng.Next(3))
                {
                    case 0: m.ZoomAt((float)(rng.NextDouble() * 6 - 3), (float)(rng.NextDouble() - 0.5), (float)(rng.NextDouble() - 0.5)); break;
                    case 1: m.Pan((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)); break;
                    default: m.Step(1f / 60f); break;
                }
                // Los bordes de la vista, en coordenadas del documento, quedan dentro de [−0,5, 0,5].
                foreach ((float pan, float zoom) in new[] { (m.PanX, m.Zoom), (m.PanY, m.Zoom), (m.TargetPanX, m.TargetZoom), (m.TargetPanY, m.TargetZoom) })
                {
                    Assert.That(Math.Abs(pan) + 0.5f / zoom, Is.LessThanOrEqualTo(0.5f + 1e-4f));
                }
                Assert.That(m.TargetZoom, Is.InRange(DocumentViewerModel.MinZoom, DocumentViewerModel.MaxZoom));
            }
        }

        [Test]
        public void Alejar_VuelveACentrar()
        {
            var m = new DocumentViewerModel(DocumentShape.For(DocumentForm.Map, 1.08f));
            m.ZoomAt(8f, 0.4f, 0.4f);
            m.Pan(-0.3f, -0.3f);
            m.ZoomAt(-20f, 0f, 0f);
            Assert.That(m.TargetZoom, Is.EqualTo(1f));
            Assert.That(m.TargetPanX, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(m.TargetPanY, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void Suavizado_IgualA30y144FPS()
        {
            DocumentViewerModel Run(float dt)
            {
                var m = new DocumentViewerModel(DocumentShape.For(DocumentForm.Letter, 0.75f));
                m.Rotate(200f, 50f);
                m.ZoomAt(4f, 0.2f, 0.1f);
                for (float t = 0f; t < 0.3f - 1e-4f; t += dt) m.Step(dt);
                return m;
            }
            DocumentViewerModel a = Run(1f / 30f), b = Run(1f / 144f);
            Assert.That(a.Yaw, Is.EqualTo(b.Yaw).Within(0.6f));
            Assert.That(a.Zoom, Is.EqualTo(b.Zoom).Within(0.03f));
        }

        // ------------------------------------------------------------------------------------------
        // Verificación de ROADMAP 5.1: el facsímil de la carta de Grau
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Inspección funcional de la carta de Grau a Carmela Carvajal, paso a paso como la haría el jugador: se abre
        /// con la proporción de su facsímil, se acerca a la firma, se da la vuelta, se alterna la transcripción y se
        /// comprueba que esta contiene la carta completa, y se restablece la vista.
        /// </summary>
        [Test]
        public void InspeccionDeLaCartaDeGrau()
        {
            CollectibleRecord grau = Grau();
            Assert.That(ImageInfo.TryReadSize(Repo(grau.FacsimileImage), out int w, out int h), Is.True);
            var viewer = new DocumentViewerModel(DocumentShape.For(DocumentForm.Letter, w / (float)h));

            // 1) Acercarse a la firma, abajo a la derecha, hasta el máximo.
            for (int i = 0; i < 12; i++) viewer.ZoomAt(1f, 0.25f, -0.4f);
            Settle(viewer);
            Assert.That(viewer.Zoom, Is.EqualTo(DocumentViewerModel.MaxZoom).Within(0.01f));
            Assert.That(viewer.PanX, Is.GreaterThan(0f));
            Assert.That(viewer.PanY, Is.LessThan(0f));

            // 2) Darle la vuelta.
            viewer.Flip();
            Settle(viewer);
            Assert.That(viewer.ShowingReverse, Is.True);

            // 3) Alternar la transcripción: aparece en menos de medio segundo.
            viewer.ToggleTranscription();
            Settle(viewer, 0.5f);
            Assert.That(viewer.TranscriptionBlend, Is.GreaterThan(0.95f));
            string full = TranscriptionLayout.FullText(grau);
            var pages = TranscriptionLayout.Paginate(TranscriptionLayout.Wrap(full, 46), 18);
            string joined = string.Join(" ", pages.SelectMany(p => p));
            string[] Words(string s) => s.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(Words(joined), Is.EqualTo(Words(full)), "la transcripción no pierde ni una palabra");
            StringAssert.Contains("Dignísima señora", joined);
            StringAssert.Contains("MIGUEL GRAU", joined, "con la firma");
            Assert.That(pages.Count, Is.InRange(2, 6));

            // 4) Ocultarla y restablecer la vista.
            viewer.ToggleTranscription();
            viewer.Reset();
            Settle(viewer);
            Assert.That(viewer.TranscriptionBlend, Is.LessThan(0.01f));
            Assert.That(viewer.Zoom, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(viewer.ShowingReverse, Is.False);
        }

        // ------------------------------------------------------------------------------------------
        // Transcripción
        // ------------------------------------------------------------------------------------------

        [Test]
        public void Ajuste_DeLineas_RespetaAnchoYParrafos()
        {
            string text = "Un sagrado deber me autoriza a dirigirme a usted.\n\nMIGUEL GRAU\nComandante del Monitor \"Huáscar\"";
            var lines = TranscriptionLayout.Wrap(text, 20);
            Assert.That(lines.All(l => l.Length <= 20), Is.True);
            Assert.That(lines, Does.Contain(string.Empty), "línea en blanco entre párrafos");
            Assert.That(lines, Does.Contain("MIGUEL GRAU"), "los saltos de línea de la firma se conservan");
            Assert.That(TranscriptionLayout.Wrap("Supercalifragilisticoespialidoso", 10).All(l => l.Length <= 10), Is.True);
            Assert.That(TranscriptionLayout.Wrap(string.Empty, 20), Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => TranscriptionLayout.Wrap("x", 3));
        }

        [Test]
        public void Paginas_SinEmpezarEnBlanco()
        {
            var lines = Enumerable.Range(0, 25).Select(i => i % 5 == 4 ? string.Empty : "línea " + i).ToList();
            var pages = TranscriptionLayout.Paginate(lines, 5);
            Assert.That(pages.All(p => p[0].Length > 0), Is.True);
            Assert.That(pages.All(p => p.Count <= 5), Is.True);
            Assert.That(pages.Sum(p => p.Count(l => l.Length > 0)), Is.EqualTo(20));
        }
    }
}
