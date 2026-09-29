using System.Collections.Generic;
using System.Linq;

namespace Pacifico.Core.Narrative
{
    /// <summary>Documento del Archivo Histórico (un archivo Markdown) ya analizado.</summary>
    public sealed class ArchiveDocument
    {
        public string Title { get; set; } = string.Empty;

        /// <summary>Campos de cabecera <c>**Clave:** valor</c> (p. ej. «Fecha», «Autor», «Destinatario»).</summary>
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        /// <summary>Cartas, despachos o transcripciones contenidos en el documento, en orden.</summary>
        public List<ArchiveEntry> Entries { get; } = new List<ArchiveEntry>();

        /// <summary>Viñetas de las secciones de uso en el juego («NOTAS…», «USO…»).</summary>
        public List<string> DesignNotes { get; } = new List<string>();

        public string GetMetadata(string key)
        {
            return Metadata.TryGetValue(key, out string value) ? value : string.Empty;
        }
    }

    /// <summary>Una carta o despacho dentro de un documento.</summary>
    public sealed class ArchiveEntry
    {
        /// <summary>Encabezado tal como aparece en el Markdown, sin almohadillas.</summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>Número de la carta o despacho («Carta 2» → 2); 0 si no está numerado.</summary>
        public int Number { get; set; }

        /// <summary>Título sin el prefijo numerado ni el paréntesis final.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Contenido del paréntesis final del encabezado (lugar y fecha).</summary>
        public string DateLabel { get; set; } = string.Empty;

        /// <summary>Línea de atribución previa al texto («Informe del Coronel Wood…»).</summary>
        public string Attribution { get; set; } = string.Empty;

        /// <summary>Párrafos del texto, limpios de marcas Markdown; los saltos de línea internos se conservan.</summary>
        public List<string> Paragraphs { get; } = new List<string>();

        /// <summary>Firma (p. ej. «MIGUEL GRAU\nComandante del Monitor "Huáscar"»).</summary>
        public string Signature { get; set; } = string.Empty;

        public string Body => string.Join("\n\n", Paragraphs);

        public int WordCount => Paragraphs.Sum(CountWords);

        private static int CountWords(string text)
        {
            int count = 0;
            bool inWord = false;
            foreach (char c in text)
            {
                bool letter = char.IsLetterOrDigit(c);
                if (letter && !inWord) count++;
                inWord = letter;
            }
            return count;
        }
    }
}
