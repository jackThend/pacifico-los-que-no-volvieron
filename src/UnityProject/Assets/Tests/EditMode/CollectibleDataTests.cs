using System.IO;
using System.Linq;
using NUnit.Framework;
using Pacifico.Core;
using Pacifico.Core.Collectibles;

namespace Pacifico.Tests
{
    public class CollectibleParserTests
    {
        private static readonly DocumentSource SingleSource = new DocumentSource(
            "doc.md", "carta_prueba", "Carta de prueba", CollectibleType.Letter, Faction.Peru,
            defaultSender: "Remitente", defaultRecipient: "Destinataria", facsimileImagePath: "img.jpg");

        private static readonly DocumentSource MultiSource = new DocumentSource(
            "epistolario.md", "carta_soldado", "Soldado", CollectibleType.Letter, Faction.Chile,
            defaultRecipient: "Padre");

        private const string SingleMarkdown =
            "# CARTA\r\n" +
            "**Fecha:** Monitor \"Huáscar\", Pisagua, 2 de junio de 1879.  \r\n" +
            "**Contexto:** Tras el combate.\r\n\r\n---\r\n\r\n" +
            "### Texto Original Transcrito\r\n\r\n" +
            "*Dignísima señora:*\r\n\r\n" +
            "*Primer párrafo con **énfasis**.*\r\n\r\n" +
            "**(Firmado)**  \r\n**FIRMA**\r\n\r\n---\r\n\r\n" +
            "### NOTAS PARA EL JUEGO (CAPÍTULO 1)\r\n" +
            "* **Visualización:** escritorio de caoba.\r\n";

        private const string MultiMarkdown =
            "# EPISTOLARIO\n" +
            "**Autor:** Soldado Juan Pérez  \n" +
            "**Unidad:** Regimiento 3° de Línea  \n" +
            "**Destinatario:** Don Pedro Pérez (su padre), residente en Quillota.\n" +
            "**Fuente Histórica:** *Libro de cartas*.\n\n---\n\n" +
            "#### Carta 1: La Partida (Septiembre de 1879)\n" +
            "> *«Querido padre:*  \n> *Parto al norte.»*\n\n" +
            "#### Carta 2: El Desierto (Dunas de Tacna, Mayo de 1880)\n" +
            "> *«Hay sed.*\n>\n> *Mucha sed.»*\n\n" +
            "#### Carta 3: Lima (Lurín, Enero de 1881 - Días antes de Miraflores)\n" +
            "> *«Adiós.*  \n> **Juan**»*\n\n---\n\n" +
            "### USO EN EL JUEGO (CAPÍTULO 8)\n" +
            "* Se encuentra en el bolsillo.\n";

        [Test]
        public void DocumentoUnico_ExtraeMetadatosYTexto()
        {
            var letters = CollectibleMarkdownParser.Parse(SingleMarkdown, SingleSource);
            Assert.AreEqual(1, letters.Count);
            var letter = letters[0];

            Assert.AreEqual("carta_prueba", letter.Id);
            Assert.AreEqual("Carta de prueba", letter.Title);
            Assert.AreEqual("Monitor \"Huáscar\", Pisagua", letter.Location);
            Assert.AreEqual("2 de junio de 1879", letter.DateText);
            Assert.AreEqual(1879, letter.Year);
            Assert.AreEqual(1, letter.Chapter);
            Assert.AreEqual("Remitente", letter.Sender);
            Assert.AreEqual("Destinataria", letter.Recipient);
            Assert.AreEqual("Tras el combate.", letter.Context);
            Assert.AreEqual("img.jpg", letter.FacsimileImagePath);
            Assert.AreEqual("vo_carta_prueba", letter.NarrationKey);
            Assert.AreEqual("Dignísima señora:\n\nPrimer párrafo con énfasis.\n\n(Firmado)\nFIRMA", letter.Transcription);
            CollectionAssert.AreEqual(new[] { "Visualización: escritorio de caoba." }, letter.GameNotes);
            CollectionAssert.IsEmpty(CollectibleValidator.Validate(letter));
        }

        [Test]
        public void Epistolario_GeneraUnaCartaPorSeccion()
        {
            var letters = CollectibleMarkdownParser.Parse(MultiMarkdown, MultiSource);
            CollectionAssert.AreEqual(new[] { "carta_soldado_01", "carta_soldado_02", "carta_soldado_03" },
                letters.Select(l => l.Id));

            Assert.AreEqual("Soldado: La Partida", letters[0].Title);
            Assert.AreEqual(string.Empty, letters[0].Location);
            Assert.AreEqual("Septiembre de 1879", letters[0].DateText);
            Assert.AreEqual("Dunas de Tacna", letters[1].Location);
            Assert.AreEqual(1880, letters[1].Year);
            Assert.AreEqual("Lurín", letters[2].Location);
            Assert.AreEqual("Enero de 1881", letters[2].DateText);

            Assert.AreEqual("Querido padre:\nParto al norte.", letters[0].Transcription);
            Assert.AreEqual("Hay sed.\n\nMucha sed.", letters[1].Transcription);
            Assert.AreEqual("Adiós.\nJuan", letters[2].Transcription);

            foreach (var letter in letters)
            {
                Assert.AreEqual("Soldado Juan Pérez", letter.Sender);
                Assert.AreEqual("Don Pedro Pérez", letter.Recipient);
                Assert.AreEqual("Unidad: Regimiento 3° de Línea", letter.Context);
                Assert.AreEqual("Libro de cartas.", letter.SourceReference);
                Assert.AreEqual(8, letter.Chapter);
                CollectionAssert.IsEmpty(CollectibleValidator.Validate(letter), letter.Id);
            }
        }

        [Test]
        public void DocumentoSinCartasDevuelveListaVacia()
        {
            CollectionAssert.IsEmpty(CollectibleMarkdownParser.Parse("# Solo título\n\nTexto suelto.", SingleSource));
        }

        [Test]
        public void ValidadorDetectaRestosDeMarkdownYDatosIncompletos()
        {
            var bad = new CollectibleSpec("x", "", CollectibleType.Letter, Faction.Chile, "", "", "", "",
                1900, 9, "*texto* > cita", "", "", "", "", "", new string[0]);
            var errors = CollectibleValidator.Validate(bad);
            // título, remitente, destinatario, fecha, año, capítulo, narración, '*', '> '
            Assert.GreaterOrEqual(errors.Count, 9, string.Join("\n", errors));
            CollectionAssert.IsNotEmpty(CollectibleValidator.Validate(null));
        }
    }

    /// <summary>Integración: carga los Markdown reales de Archivo_Historico.</summary>
    public class HistoricalLettersTests
    {
        private static string RepositoryRoot()
        {
            var root = CollectibleLibrary.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory)
                       ?? CollectibleLibrary.FindRepositoryRoot(Directory.GetCurrentDirectory());
            Assert.IsNotNull(root, "No se encontró Archivo_Historico.");
            return root;
        }

        [Test]
        public void CargaCartasDeGrauYQuiroz()
        {
            var all = CollectibleLibrary.LoadAll(RepositoryRoot());
            CollectionAssert.AreEquivalent(
                new[] { "carta_grau_carmela_carvajal", "carta_abraham_quiroz_01", "carta_abraham_quiroz_02", "carta_abraham_quiroz_03" },
                all.Select(c => c.Id));
            foreach (var spec in all)
            {
                CollectionAssert.IsEmpty(CollectibleValidator.Validate(spec), spec.Id);
                Assert.AreEqual(CollectibleType.Letter, spec.Type);
            }
        }

        [Test]
        public void CartaDeGrauACarmelaCarvajal()
        {
            var grau = CollectibleLibrary.LoadAll(RepositoryRoot()).Single(c => c.Id == "carta_grau_carmela_carvajal");

            Assert.AreEqual(Faction.Peru, grau.Faction);
            Assert.AreEqual("Miguel Grau Seminario", grau.Sender);
            Assert.AreEqual("Carmela Carvajal Briones", grau.Recipient);
            Assert.AreEqual(1879, grau.Year);
            Assert.AreEqual("2 de junio de 1879", grau.DateText);
            StringAssert.Contains("Pisagua", grau.Location);
            Assert.AreEqual(1, grau.Chapter);
            StringAssert.StartsWith("Monitor \"Huáscar\", Pisagua, Junio 2 de 1879.", grau.Transcription);
            StringAssert.Contains("Dignísima señora:", grau.Transcription);
            StringAssert.Contains("su espada, su uniforme, su escapulario", grau.Transcription);
            StringAssert.EndsWith("MIGUEL GRAU\nComandante del Monitor \"Huáscar\"", grau.Transcription);
            StringAssert.Contains("devolución de la espada", grau.Context);
            Assert.IsNotEmpty(grau.GameNotes);
            Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot(), grau.FacsimileImagePath)), grau.FacsimileImagePath);
        }

        [Test]
        public void EpistolarioDeAbrahamQuiroz()
        {
            var letters = CollectibleLibrary.LoadAll(RepositoryRoot())
                .Where(c => c.Id.StartsWith("carta_abraham_quiroz")).OrderBy(c => c.Id).ToList();

            Assert.AreEqual(3, letters.Count);
            CollectionAssert.AreEqual(new[] { 1879, 1880, 1881 }, letters.Select(l => l.Year));
            CollectionAssert.AreEqual(new[] { "", "Dunas de Tacna", "Lurín" }, letters.Select(l => l.Location));
            foreach (var letter in letters)
            {
                Assert.AreEqual(Faction.Chile, letter.Faction);
                Assert.AreEqual("Soldado Abraham Quiroz", letter.Sender);
                Assert.AreEqual("Don Luciano Quiroz", letter.Recipient);
                Assert.AreEqual(8, letter.Chapter);
                StringAssert.Contains("Cazadores del Desierto", letter.Context);
                StringAssert.Contains("Feliú Cruz", letter.SourceReference);
            }
            StringAssert.StartsWith("Señor don Luciano Quiroz.\nMi querido padre:", letters[0].Transcription);
            StringAssert.Contains("El desierto es una cosa que no se puede pintar con palabras.", letters[1].Transcription);
            Assert.AreEqual(3, letters[1].Transcription.Split('\n').Length, "saludo + 2 párrafos");
            StringAssert.EndsWith("Pídale a Dios por mí, que yo no lo olvido.\nSu hijo que de corazón lo ama y respeta,\nAbraham Quiroz",
                letters[2].Transcription);
        }
    }
}
