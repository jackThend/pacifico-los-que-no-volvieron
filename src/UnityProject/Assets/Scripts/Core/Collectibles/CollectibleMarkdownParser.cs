using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Pacifico.Core.Collectibles
{
    /// <summary>
    /// Convierte los Markdown de Archivo_Historico en CollectibleSpec.
    /// Admite dos formatos:
    /// <list type="bullet">
    /// <item>Documento único: sección "### ... Transcrito" con fecha en el metadato **Fecha:**.</item>
    /// <item>Epistolario: varias secciones "#### Carta N: Título (Lugar, Fecha)" con el texto en citas "&gt;".</item>
    /// </list>
    /// El capítulo se toma del encabezado de notas "... (CAPÍTULO N)".
    /// </summary>
    public static class CollectibleMarkdownParser
    {
        private static readonly Regex MetadataLine = new Regex(@"^\*\*(?<key>[^*:]+):\*\*\s*(?<value>.*)$");
        private static readonly Regex ChapterHeading = new Regex(@"CAP[ÍI]TULO\s+(?<n>\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex LetterHeading = new Regex(@"^####\s+(?<name>[^(]+?)\s*(?:\((?<when>[^)]*)\))?\s*$");
        private static readonly Regex NumberedName = new Regex(@"^\S+\s+(?<n>\d+)\s*:\s*(?<title>.+)$");
        private static readonly Regex YearPattern = new Regex(@"\b(?<y>18\d{2})\b");

        public static IReadOnlyList<CollectibleSpec> Parse(string markdown, DocumentSource source)
        {
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var metadata = ReadMetadata(lines);
            var chapter = ReadChapter(lines, out var notes);

            var sender = CleanPerson(Get(metadata, "Autor")) ?? source.DefaultSender;
            var recipient = CleanPerson(Get(metadata, "Destinatario")) ?? source.DefaultRecipient;
            var context = Get(metadata, "Contexto") ?? PrefixOrNull("Unidad: ", Get(metadata, "Unidad")) ?? string.Empty;
            var reference = Get(metadata, "Fuente Histórica") ?? source.RelativePath;

            var result = new List<CollectibleSpec>();
            var letters = ReadLetterSections(lines);
            if (letters.Count > 0)
            {
                for (var i = 0; i < letters.Count; i++)
                {
                    var section = letters[i];
                    var number = section.Number > 0 ? section.Number : i + 1;
                    var id = $"{source.IdPrefix}_{number:00}";
                    SplitPlaceAndDate(section.When, out var location, out var date);
                    result.Add(Build(source, id, $"{source.Title}: {section.Title}", sender, recipient,
                        location, date, chapter, section.Text, context, reference, notes));
                }
                return result;
            }

            var body = ReadTranscribedSection(lines);
            if (body != null)
            {
                SplitPlaceAndDate(Get(metadata, "Fecha"), out var location, out var date);
                result.Add(Build(source, source.IdPrefix, source.Title, sender, recipient,
                    location, date, chapter, body, context, reference, notes));
            }
            return result;
        }

        private static CollectibleSpec Build(DocumentSource source, string id, string title, string sender,
            string recipient, string location, string date, int chapter, string text, string context,
            string reference, IReadOnlyList<string> notes)
        {
            var yearMatch = YearPattern.Match(date ?? string.Empty);
            if (!yearMatch.Success) yearMatch = YearPattern.Match(text);
            var year = yearMatch.Success ? int.Parse(yearMatch.Groups["y"].Value) : 0;

            return new CollectibleSpec(id, title, source.Type, source.Faction, sender, recipient,
                location ?? string.Empty, date ?? string.Empty, year, chapter, text, context, reference,
                source.RelativePath, source.FacsimileImagePath ?? string.Empty, "vo_" + id, notes);
        }

        private static Dictionary<string, string> ReadMetadata(string[] lines)
        {
            var metadata = new Dictionary<string, string>();
            foreach (var raw in lines)
            {
                var match = MetadataLine.Match(raw.Trim());
                if (match.Success && !metadata.ContainsKey(match.Groups["key"].Value.Trim()))
                {
                    metadata[match.Groups["key"].Value.Trim()] = CleanInline(match.Groups["value"].Value);
                }
            }
            return metadata;
        }

        private static int ReadChapter(string[] lines, out IReadOnlyList<string> notes)
        {
            var collected = new List<string>();
            var chapter = 0;
            var inNotes = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("#"))
                {
                    var match = ChapterHeading.Match(line);
                    inNotes = match.Success;
                    if (match.Success && chapter == 0) chapter = int.Parse(match.Groups["n"].Value);
                    continue;
                }
                if (inNotes && (line.StartsWith("* ") || line.StartsWith("- ")))
                {
                    collected.Add(CleanInline(line.Substring(2)));
                }
            }
            notes = collected;
            return chapter;
        }

        private sealed class LetterSection
        {
            public int Number;
            public string Title;
            public string When;
            public string Text;
        }

        private static List<LetterSection> ReadLetterSections(string[] lines)
        {
            var sections = new List<LetterSection>();
            for (var i = 0; i < lines.Length; i++)
            {
                var heading = LetterHeading.Match(lines[i].Trim());
                if (!heading.Success) continue;

                var body = new List<string>();
                var j = i + 1;
                for (; j < lines.Length; j++)
                {
                    var line = lines[j].Trim();
                    if (line.StartsWith("#") || line == "---") break;
                    if (line.StartsWith(">")) body.Add(line.Substring(1));
                    else if (line.Length == 0) body.Add(string.Empty);
                }

                var text = JoinParagraphs(body);
                if (text.Length == 0) continue;

                var name = heading.Groups["name"].Value.Trim();
                var numbered = NumberedName.Match(name);
                sections.Add(new LetterSection
                {
                    Number = numbered.Success ? int.Parse(numbered.Groups["n"].Value) : 0,
                    Title = numbered.Success ? numbered.Groups["title"].Value.Trim() : name,
                    When = heading.Groups["when"].Success ? heading.Groups["when"].Value : null,
                    Text = text
                });
                i = j - 1;
            }
            return sections;
        }

        private static string ReadTranscribedSection(string[] lines)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("###") || line.IndexOf("Transcrit", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                var body = new List<string>();
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var current = lines[j].Trim();
                    if (current.StartsWith("#") || current == "---") break;
                    body.Add(current);
                }
                var text = JoinParagraphs(body);
                return text.Length > 0 ? text : null;
            }
            return null;
        }

        /// <summary>Une líneas limpias; una línea vacía separa párrafos.</summary>
        private static string JoinParagraphs(List<string> rawLines)
        {
            var builder = new StringBuilder();
            var pendingBreak = false;
            foreach (var raw in rawLines)
            {
                var line = CleanInline(raw);
                if (line.Length == 0)
                {
                    pendingBreak = builder.Length > 0;
                    continue;
                }
                if (builder.Length > 0) builder.Append(pendingBreak ? "\n\n" : "\n");
                builder.Append(line);
                pendingBreak = false;
            }
            return builder.ToString();
        }

        /// <summary>Elimina énfasis Markdown y comillas angulares de apertura/cierre.</summary>
        private static string CleanInline(string text)
        {
            return text.Replace("*", string.Empty).Replace("«", string.Empty).Replace("»", string.Empty).Trim();
        }

        private static void SplitPlaceAndDate(string when, out string location, out string date)
        {
            location = string.Empty;
            date = string.Empty;
            if (string.IsNullOrWhiteSpace(when)) return;

            var value = when.Trim().TrimEnd('.');
            var dash = value.IndexOf(" - ", System.StringComparison.Ordinal);
            if (dash >= 0) value = value.Substring(0, dash).Trim();

            var comma = value.LastIndexOf(',');
            if (comma >= 0)
            {
                location = value.Substring(0, comma).Trim();
                date = value.Substring(comma + 1).Trim();
            }
            else
            {
                date = value;
            }
        }

        /// <summary>"Don Luciano Quiroz (su padre), residente..." → "Don Luciano Quiroz".</summary>
        private static string CleanPerson(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var cut = value.IndexOfAny(new[] { '(', ',' });
            return (cut >= 0 ? value.Substring(0, cut) : value).Trim();
        }

        private static string Get(Dictionary<string, string> metadata, string key)
        {
            return metadata.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
        }

        private static string PrefixOrNull(string prefix, string value)
        {
            return value == null ? null : prefix + value;
        }
    }
}
