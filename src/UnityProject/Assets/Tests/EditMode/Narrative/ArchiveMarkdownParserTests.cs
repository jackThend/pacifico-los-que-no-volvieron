using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core.Narrative;

namespace Pacifico.Tests.Narrative
{
    /// <summary>ROADMAP 1.3 — carga de las cartas desde los Markdown reales del Archivo Histórico.</summary>
    public class ArchiveMarkdownParserTests
    {
        private static ArchiveDocument ParseRepositoryFile(string relative)
        {
            string path = Path.Combine(TestPaths.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            return ArchiveMarkdownParser.Parse(File.ReadAllText(path));
        }

        [Test]
        public void CartaDeGrau_SeLeeConCabeceraCuerpoYFirma()
        {
            var doc = ParseRepositoryFile(CollectibleCatalog.GrauLetterFile);

            Assert.That(doc.Title, Does.StartWith("CARTA AUTÉNTICA DE DON MIGUEL GRAU"));
            Assert.That(doc.GetMetadata("Fecha"), Does.Contain("2 de junio de 1879"));
            Assert.That(doc.GetMetadata("Contexto"), Does.Contain("Combate Naval de Iquique"));
            Assert.That(doc.Entries, Has.Count.EqualTo(1));

            var letter = doc.Entries[0];
            Assert.That(letter.Paragraphs[0], Is.EqualTo("Monitor \"Huáscar\", Pisagua, Junio 2 de 1879."));
            Assert.That(letter.Paragraphs[1], Is.EqualTo("Dignísima señora:"));
            Assert.That(letter.Body, Does.Contain("capitán de fragata don Arturo Prat"));
            Assert.That(letter.Body, Does.Contain("su espada, su uniforme, su escapulario"));
            Assert.That(letter.Body, Does.Not.Contain("*"));
            Assert.That(letter.Body, Does.Not.Contain("Firmado"));
            Assert.That(letter.Signature, Is.EqualTo("MIGUEL GRAU\nComandante del Monitor \"Huáscar\""));
            Assert.That(doc.DesignNotes, Has.Count.EqualTo(3));
        }

        [Test]
        public void EpistolarioDeQuiroz_TresCartasNumeradasConFechaYFirma()
        {
            var doc = ParseRepositoryFile(CollectibleCatalog.QuirozLettersFile);

            Assert.That(doc.GetMetadata("Destinatario"), Does.Contain("Luciano Quiroz"));
            Assert.That(doc.GetMetadata("Unidad"), Does.Contain("3° de Línea"));
            Assert.That(doc.Entries.Select(e => e.Number), Is.EqualTo(new[] { 1, 2, 3 }));

            var first = doc.Entries[0];
            Assert.That(first.Title, Is.EqualTo("Enrolamiento y Partida al Norte"));
            Assert.That(first.DateLabel, Is.EqualTo("Septiembre de 1879"));
            Assert.That(first.Body, Does.StartWith("Señor don Luciano Quiroz."), "se retira la comilla que envuelve la carta");
            Assert.That(first.Body, Does.EndWith("tan pronto termine esta guerra..."));
            Assert.That(first.Signature, Is.Empty);

            var last = doc.Entries[2];
            Assert.That(last.DateLabel, Is.EqualTo("Lurín, Enero de 1881 - Días antes de Miraflores"));
            Assert.That(last.Body, Does.StartWith("Señor don Luciano Quiroz."));
            Assert.That(last.Body, Does.Contain("Pídale a Dios por mí, que yo no lo olvido."));
            Assert.That(last.Body, Does.EndWith("Su hijo que de corazón lo ama y respeta,"));
            Assert.That(last.Signature, Is.EqualTo("Abraham Quiroz"));
            Assert.That(last.Body, Does.Not.Contain("«").And.Not.Contain("»"));
        }

        [Test]
        public void Cronicas_CuatroDespachosConAtribucion()
        {
            var doc = ParseRepositoryFile(CollectibleCatalog.DispatchesFile);

            Assert.That(doc.Entries, Has.Count.EqualTo(4));
            Assert.That(doc.GetMetadata("Fuentes Históricas").Split('\n'), Has.Length.EqualTo(3));

            var pisagua = doc.Entries[1];
            Assert.That(pisagua.Title, Is.EqualTo("LA SORPRESA DE PISAGUA Y EL SILENCIO CHILENO"));
            Assert.That(pisagua.DateLabel, Is.EqualTo("Noviembre de 1879"));
            Assert.That(pisagua.Attribution, Is.EqualTo("Informe del Coronel Wood, observador militar británico adjunto"));
            Assert.That(pisagua.Body, Does.StartWith("He asistido al desembarco en Pisagua"));

            // Las comillas internas se conservan: solo se retiran las que envuelven todo el despacho.
            Assert.That(doc.Entries[0].Attribution, Is.EqualTo("«The Times» de Londres — Corresponsalía de Sudamérica"));
        }

        [Test]
        public void ComillasInternas_NoSeConfundenConLasEnvolventes()
        {
            const string md = "# T\n\n---\n\n### Carta 1: Prueba (1880)\n> *«El Mercurio» publicó hoy la noticia y dijo «basta».*\n";
            var entry = ArchiveMarkdownParser.Parse(md).Entries.Single();
            Assert.That(entry.Body, Is.EqualTo("«El Mercurio» publicó hoy la noticia y dijo «basta»."));
        }

        [Test]
        public void FinDeLineaWindows_SeAceptan()
        {
            const string md = "# T\r\n**Fecha:** 1879\r\n\r\n---\r\n\r\n### Despacho 2: Uno (Lima)\r\n> *«Texto.»*\r\n";
            var doc = ArchiveMarkdownParser.Parse(md);
            Assert.That(doc.GetMetadata("Fecha"), Is.EqualTo("1879"));
            Assert.That(doc.Entries.Single().Number, Is.EqualTo(2));
            Assert.That(doc.Entries.Single().Body, Is.EqualTo("Texto."));
        }

        [Test]
        public void DocumentoSinEntradas_NoFalla()
        {
            var doc = ArchiveMarkdownParser.Parse("# Solo título\n\nTexto suelto.\n");
            Assert.That(doc.Title, Is.EqualTo("Solo título"));
            Assert.That(doc.Entries, Is.Empty);
        }
    }

    public class CollectibleCatalogTests
    {
        private static System.Collections.Generic.List<CollectibleRecord> Build() =>
            CollectibleCatalog.BuildFromRepository(TestPaths.RepositoryRoot);

        [Test]
        public void TodosLosColeccionables_SeConstruyenYSonValidos()
        {
            var records = Build();
            Assert.That(records, Has.Count.EqualTo(CollectibleCatalog.Definitions.Count));
            Assert.That(records.Select(r => r.Id), Is.Unique);
            foreach (var record in records)
            {
                var result = record.Validate();
                Assert.That(result.IsValid, Is.True, result.ToString());
            }
        }

        [Test]
        public void CartaDeGrau_TieneFacsimilQueExisteEnElArchivo()
        {
            var grau = Build().Single(r => r.Id == CollectibleCatalog.GrauLetterId);
            Assert.That(grau.Chapter, Is.EqualTo(1));
            Assert.That(grau.Recipient, Does.Contain("Carmela Carvajal"));
            Assert.That(grau.DateLabel, Does.Contain("2 de junio de 1879"));
            Assert.That(grau.Signature, Does.StartWith("MIGUEL GRAU"));
            string facsimile = Path.Combine(TestPaths.RepositoryRoot, grau.FacsimileImage.Replace('/', Path.DirectorySeparatorChar));
            Assert.That(File.Exists(facsimile), Is.True, facsimile);
        }

        [Test]
        public void UltimaCartaDeQuiroz_EsElClimaxDelCapitulo8()
        {
            var last = Build().Single(r => r.Id == "carta_quiroz_3_lima");
            Assert.That(last.Chapter, Is.EqualTo(8));
            Assert.That(last.Signature, Is.EqualTo("Abraham Quiroz"));
            Assert.That(last.EstimatedNarrationSeconds, Is.InRange(40f, 180f));
        }

        [Test]
        public void DefinicionFueraDeRango_DaUnErrorClaro()
        {
            Assert.Throws<InvalidDataException>(() =>
                CollectibleCatalog.Build(_ => "# Vacío\n\n---\n"));
        }
    }
}
