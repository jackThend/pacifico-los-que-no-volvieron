using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Citas de un capítulo del guion (<c>Historia_Completa_Guion.md</c>): todo lo que va entre comillas latinas
    /// «…». Las misiones no copian las frases: las buscan aquí por su comienzo, de modo que lo que dice Prat o Grau
    /// en el juego es siempre, letra por letra, lo que fija el guion.
    /// </summary>
    public sealed class GuionQuotes
    {
        public const string ScriptFile = "Historia_Completa_Guion.md";

        private static readonly Regex Quote = new Regex("«(?<q>[^»]+)»");

        private GuionQuotes(string chapterTitle, List<string> quotes)
        {
            ChapterTitle = chapterTitle;
            All = quotes;
        }

        public string ChapterTitle { get; }
        public IReadOnlyList<string> All { get; }

        /// <summary>
        /// Texto de la sección «## …» cuyo título contiene <paramref name="headingContains"/>, con su encabezado
        /// (lo que guarda la escena para no depender del fichero del guion en una build).
        /// </summary>
        public static string Section(string markdown, string headingContains)
        {
            if (markdown == null) throw new ArgumentNullException(nameof(markdown));
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, l => l.StartsWith("## ") && l.IndexOf(headingContains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (start < 0) throw new ArgumentException("No se encontró la sección «" + headingContains + "» en el guion.", nameof(headingContains));
            int end = start + 1;
            while (end < lines.Length && !lines[end].StartsWith("## ")) end++;
            return string.Join("\n", lines, start, end - start);
        }

        /// <summary>Citas de la sección «## …» cuyo título contiene <paramref name="headingContains"/>.</summary>
        public static GuionQuotes Extract(string markdown, string headingContains)
        {
            string[] lines = Section(markdown, headingContains).Split('\n');
            var quotes = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                foreach (Match m in Quote.Matches(lines[i]))
                {
                    string q = Regex.Replace(m.Groups["q"].Value.Replace("*", string.Empty), @"\s+", " ").Trim();
                    if (q.Length > 0) quotes.Add(q);
                }
            }
            return new GuionQuotes(lines[0].Substring(3).Trim(), quotes);
        }

        public static GuionQuotes Load(Func<string, string> readRepositoryFile, string headingContains) =>
            Extract(readRepositoryFile(ScriptFile), headingContains);

        /// <summary>La cita que empieza por <paramref name="prefix"/> (debe haber exactamente una).</summary>
        public string Find(string prefix)
        {
            List<string> found = All.Where(q => q.StartsWith(prefix, StringComparison.Ordinal)).Distinct().ToList();
            if (found.Count == 0) throw new KeyNotFoundException("El guion (" + ChapterTitle + ") no contiene una cita que empiece por «" + prefix + "».");
            if (found.Count > 1) throw new InvalidOperationException("Hay varias citas que empiezan por «" + prefix + "» en " + ChapterTitle + ".");
            return found[0];
        }

        /// <summary>Una frase (hasta el primer signo de cierre) de la cita que empieza por <paramref name="prefix"/>.</summary>
        public string FirstSentence(string prefix)
        {
            string q = Find(prefix);
            int end = q.IndexOfAny(new[] { '!', '.', '?' });
            return end < 0 ? q : q.Substring(0, end + 1);
        }

        public bool Contains(string text) => All.Any(q => q.IndexOf(text, StringComparison.Ordinal) >= 0);
    }
}
