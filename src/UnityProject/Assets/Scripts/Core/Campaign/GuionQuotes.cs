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
        /// <summary>Viñeta con etiqueta en negrita: «* **Prólogo:** Durante cinco meses…».</summary>
        private static readonly Regex LabeledBullet = new Regex(@"^\s*[*-]\s+\*\*(?<k>[^*:]+):\*\*\s*(?<v>.*)$");

        private readonly Dictionary<string, string> _fields;

        private GuionQuotes(string chapterTitle, List<string> quotes, List<string[]> blocks, string[] fenced, Dictionary<string, string> fields)
        {
            ChapterTitle = chapterTitle;
            All = quotes;
            Blocks = blocks;
            Fenced = fenced;
            _fields = fields;
        }

        /// <summary>
        /// Texto de la viñeta etiquetada «* **<paramref name="label"/>:** …» de la sección (la primera, sin marcas
        /// Markdown): el prólogo de un capítulo cuyo guion no lo pone entre comillas, o la descripción del coleccionable.
        /// </summary>
        public string Field(string label)
        {
            if (_fields.TryGetValue(label, out string value) && value.Length > 0) return value;
            throw new KeyNotFoundException("El guion (" + ChapterTitle + ") no tiene la viñeta «**" + label + ":**» con texto.");
        }

        /// <summary>
        /// Como <see cref="Field"/>, sin las comillas que envuelven la cita (los prólogos del corresponsal de los
        /// capítulos 3 y 7 van entre comillas inglesas dentro de la viñeta).
        /// </summary>
        public string QuotedField(string label) => Field(label).Trim().Trim('"', '“', '”', '«', '»').Trim();

        /// <summary>
        /// Bloques citados («&gt; …») de varias líneas: el prólogo del corresponsal, una carta, la cita final. Cada
        /// bloque es la lista de sus líneas no vacías, sin marcas Markdown ni las comillas de apertura y cierre.
        /// </summary>
        public IReadOnlyList<string[]> Blocks { get; }

        /// <summary>Líneas del primer bloque de código («```») de la sección: la lápida del epílogo.</summary>
        public IReadOnlyList<string> Fenced { get; }

        /// <summary>El bloque citado cuya primera línea empieza por <paramref name="prefix"/>.</summary>
        public string[] FindBlock(string prefix)
        {
            foreach (string[] block in Blocks)
            {
                if (block.Length > 0 && block[0].StartsWith(prefix, StringComparison.Ordinal)) return block;
            }
            throw new KeyNotFoundException("El guion (" + ChapterTitle + ") no contiene un bloque citado que empiece por «" + prefix + "».");
        }

        private static string CleanBlockLine(string line)
        {
            string t = line.Trim();
            if (t.StartsWith(">")) t = t.Substring(1);
            t = Regex.Replace(t.Replace("*", string.Empty), @"\s+", " ").Trim();
            return t.Trim('"', '“', '”', '«', '»').Trim();
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
            var blocks = new List<string[]>();
            var current = new List<string>();
            var fenced = new List<string>();
            var fields = new Dictionary<string, string>();
            bool inFence = false, fenceDone = false;
            for (int i = 1; i < lines.Length; i++)
            {
                foreach (Match m in Quote.Matches(lines[i]))
                {
                    string q = Regex.Replace(m.Groups["q"].Value.Replace("*", string.Empty), @"\s+", " ").Trim();
                    if (q.Length > 0) quotes.Add(q);
                }

                Match field = LabeledBullet.Match(lines[i]);
                if (field.Success)
                {
                    string key = field.Groups["k"].Value.Trim();
                    if (!fields.ContainsKey(key)) fields.Add(key, Regex.Replace(field.Groups["v"].Value.Replace("*", string.Empty), @"\s+", " ").Trim());
                }

                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("```"))
                {
                    if (inFence) fenceDone = true;
                    inFence = !inFence && !fenceDone;
                    continue;
                }
                if (inFence)
                {
                    if (trimmed.Length > 0) fenced.Add(trimmed);
                    continue;
                }
                if (trimmed.StartsWith(">"))
                {
                    string text = CleanBlockLine(trimmed);
                    if (text.Length > 0) current.Add(text);
                }
                else if (current.Count > 0)
                {
                    blocks.Add(current.ToArray());
                    current.Clear();
                }
            }
            if (current.Count > 0) blocks.Add(current.ToArray());
            return new GuionQuotes(lines[0].Substring(3).Trim(), quotes, blocks, fenced.ToArray(), fields);
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
