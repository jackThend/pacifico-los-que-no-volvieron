using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pacifico.Core.Narrative
{
    /// <summary>
    /// Analiza los documentos Markdown de <c>Archivo_Historico/05_Cartas_y_Documentos</c>.
    /// Reconoce tres formatos presentes en el archivo:
    /// <list type="bullet">
    /// <item>Transcripción única bajo «Texto Original…», en cursiva y con bloque «(Firmado)» (carta de Grau).</item>
    /// <item>Cartas numeradas «Carta N: Título (fecha)» en cita <c>&gt;</c>, con firma en negrita (epistolario de Quiroz).</item>
    /// <item>Despachos «DESPACHO N: TÍTULO (lugar, fecha)» con línea de atribución en cursiva (crónicas).</item>
    /// </list>
    /// </summary>
    public static class ArchiveMarkdownParser
    {
        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.*?)\s*#*\s*$");
        private static readonly Regex MetadataRegex = new Regex(@"^\*\*(?<key>[^*]+?):\*\*\s*(?<value>.*)$");
        private static readonly Regex NumberedEntryRegex = new Regex(@"^(?<kind>carta|despacho)\s+(?<n>\d+)\s*:\s*(?<rest>.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex TrailingParenRegex = new Regex(@"^(?<title>.*?)\s*\((?<paren>[^()]*)\)\s*$");
        private static readonly Regex BoldLineRegex = new Regex(@"^\*\*(?<text>[^*]+)\*\*[»”""*\s]*$");
        private static readonly Regex ListItemRegex = new Regex(@"^\s*(?:[*\-+]|\d+\.)\s+(?<text>.+)$");

        private enum SectionKind
        {
            None,
            Entry,
            Notes,
        }

        public static ArchiveDocument Parse(string markdown)
        {
            if (markdown == null) throw new ArgumentNullException(nameof(markdown));

            var document = new ArchiveDocument();
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            bool inHeader = true;
            string lastMetadataKey = null;
            SectionKind section = SectionKind.None;
            ArchiveEntry current = null;
            var buffer = new List<string>();

            void FlushEntry()
            {
                if (current != null)
                {
                    FillEntry(current, buffer);
                    document.Entries.Add(current);
                }
                current = null;
                buffer.Clear();
            }

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                Match heading = HeadingRegex.Match(line);

                if (heading.Success)
                {
                    int level = heading.Groups[1].Value.Length;
                    string text = heading.Groups[2].Value.Trim();
                    if (level == 1 && string.IsNullOrEmpty(document.Title))
                    {
                        document.Title = CleanInline(text);
                        continue;
                    }

                    inHeader = false;
                    FlushEntry();
                    if (IsNotesHeading(text))
                    {
                        section = SectionKind.Notes;
                    }
                    else if (TryCreateEntry(text, out ArchiveEntry entry))
                    {
                        section = SectionKind.Entry;
                        current = entry;
                    }
                    else
                    {
                        section = SectionKind.None; // encabezado contenedor («Fragmentos Seleccionados…»)
                    }
                    continue;
                }

                if (IsHorizontalRule(line))
                {
                    inHeader = false;
                    if (section == SectionKind.Entry) FlushEntry();
                    section = SectionKind.None;
                    continue;
                }

                if (inHeader)
                {
                    ReadHeaderLine(document, line, ref lastMetadataKey);
                    continue;
                }

                switch (section)
                {
                    case SectionKind.Entry:
                        buffer.Add(line);
                        break;
                    case SectionKind.Notes:
                        Match item = ListItemRegex.Match(line);
                        if (item.Success) document.DesignNotes.Add(CleanInline(item.Groups["text"].Value));
                        break;
                }
            }

            FlushEntry();
            return document;
        }

        // ------------------------------------------------------------------------------------------
        // Cabecera
        // ------------------------------------------------------------------------------------------

        private static void ReadHeaderLine(ArchiveDocument document, string line, ref string lastKey)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            Match meta = MetadataRegex.Match(line.Trim());
            if (meta.Success)
            {
                lastKey = CleanInline(meta.Groups["key"].Value);
                document.Metadata[lastKey] = CleanInline(meta.Groups["value"].Value);
                return;
            }

            // Continuación en forma de lista («**Fuentes Históricas:**» seguido de 1., 2., 3.).
            Match item = ListItemRegex.Match(line);
            if (item.Success && lastKey != null)
            {
                string previous = document.Metadata[lastKey];
                string value = CleanInline(item.Groups["text"].Value);
                document.Metadata[lastKey] = string.IsNullOrEmpty(previous) ? value : previous + "\n" + value;
            }
        }

        // ------------------------------------------------------------------------------------------
        // Secciones
        // ------------------------------------------------------------------------------------------

        private static bool IsNotesHeading(string heading)
        {
            string upper = heading.ToUpperInvariant();
            return upper.StartsWith("NOTAS", StringComparison.Ordinal) || upper.StartsWith("USO", StringComparison.Ordinal);
        }

        private static bool TryCreateEntry(string heading, out ArchiveEntry entry)
        {
            entry = null;
            string clean = CleanInline(heading);
            Match numbered = NumberedEntryRegex.Match(clean);
            if (numbered.Success)
            {
                entry = new ArchiveEntry { Heading = clean, Number = int.Parse(numbered.Groups["n"].Value) };
                SplitTitle(numbered.Groups["rest"].Value, entry);
                return true;
            }

            if (clean.IndexOf("Texto Original", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                entry = new ArchiveEntry { Heading = clean, Title = clean };
                return true;
            }

            return false;
        }

        private static void SplitTitle(string text, ArchiveEntry entry)
        {
            Match paren = TrailingParenRegex.Match(text);
            if (paren.Success)
            {
                entry.Title = paren.Groups["title"].Value.Trim();
                entry.DateLabel = paren.Groups["paren"].Value.Trim();
            }
            else
            {
                entry.Title = text.Trim();
            }
        }

        private static bool IsHorizontalRule(string line)
        {
            string t = line.Trim();
            return t.Length >= 3 && (t.All(c => c == '-') || t.All(c => c == '*') || t.All(c => c == '_'));
        }

        // ------------------------------------------------------------------------------------------
        // Cuerpo de una carta o despacho
        // ------------------------------------------------------------------------------------------

        private static void FillEntry(ArchiveEntry entry, List<string> rawLines)
        {
            // 1) Quitar el marcador de cita y agrupar en párrafos por líneas en blanco.
            var groups = new List<List<string>>();
            var group = new List<string>();
            var quotedFlags = new List<bool>();
            bool groupQuoted = false;
            foreach (string raw in rawLines)
            {
                bool quoted = raw.TrimStart().StartsWith(">", StringComparison.Ordinal);
                string text = quoted ? raw.TrimStart().Substring(1).TrimStart() : raw.Trim();
                if (text.Length == 0)
                {
                    if (group.Count > 0)
                    {
                        groups.Add(group);
                        quotedFlags.Add(groupQuoted);
                        group = new List<string>();
                    }
                    continue;
                }
                // Pasar de texto normal a cita (o viceversa) también separa párrafos,
                // aunque no haya línea en blanco (atribución seguida de «> …»).
                if (group.Count > 0 && quoted != groupQuoted)
                {
                    groups.Add(group);
                    quotedFlags.Add(groupQuoted);
                    group = new List<string>();
                }
                if (group.Count == 0) groupQuoted = quoted;
                group.Add(text.Trim());
            }
            if (group.Count > 0)
            {
                groups.Add(group);
                quotedFlags.Add(groupQuoted);
            }

            // 2) Línea de atribución: un único renglón en cursiva, fuera de la cita, que termina en «:».
            if (groups.Count > 1 && !quotedFlags[0] && groups[0].Count == 1)
            {
                string candidate = CleanInline(groups[0][0]);
                if (candidate.EndsWith(":", StringComparison.Ordinal) && groups[0][0].StartsWith("*", StringComparison.Ordinal))
                {
                    entry.Attribution = candidate.TrimEnd(':').Trim();
                    groups.RemoveAt(0);
                }
            }

            // 3) Firma estilo «(Firmado)».
            int signedIndex = groups.FindIndex(g => CleanInline(g[0]).Equals("(Firmado)", StringComparison.OrdinalIgnoreCase));
            if (signedIndex >= 0)
            {
                entry.Signature = string.Join("\n", groups[signedIndex].Skip(1).Select(CleanInline).Where(s => s.Length > 0));
                groups.RemoveRange(signedIndex, groups.Count - signedIndex);
            }

            // 4) Firma en negrita al final de la cita («**Abraham Quiroz**»*).
            if (entry.Signature.Length == 0 && groups.Count > 0)
            {
                List<string> last = groups[groups.Count - 1];
                Match bold = BoldLineRegex.Match(last[last.Count - 1]);
                if (bold.Success)
                {
                    entry.Signature = bold.Groups["text"].Value.Trim();
                    last.RemoveAt(last.Count - 1);
                    if (last.Count == 0) groups.RemoveAt(groups.Count - 1);
                }
            }

            // 5) Limpiar marcas Markdown. Dentro de una cita cada renglón con salto duro es su propio verso.
            foreach (List<string> g in groups)
            {
                string paragraph = string.Join("\n", g.Select(CleanInline).Where(s => s.Length > 0));
                if (paragraph.Length > 0) entry.Paragraphs.Add(paragraph);
            }

            StripOuterGuillemets(entry.Paragraphs);
        }

        /// <summary>Elimina las comillas angulares que envuelven todo el texto citado, conservando las internas.</summary>
        private static void StripOuterGuillemets(List<string> paragraphs)
        {
            if (paragraphs.Count == 0) return;
            string joined = string.Join("\u0001", paragraphs);

            if (joined.Length >= 2 && joined[0] == '«' && joined[joined.Length - 1] == '»' && ClosesAtEnd(joined))
            {
                joined = joined.Substring(1, joined.Length - 2);
            }
            else if (joined[0] == '«' && Count(joined, '«') > Count(joined, '»'))
            {
                joined = joined.Substring(1); // la comilla de cierre iba en la línea de la firma
            }
            else if (joined[joined.Length - 1] == '»' && Count(joined, '»') > Count(joined, '«'))
            {
                joined = joined.Substring(0, joined.Length - 1);
            }

            paragraphs.Clear();
            paragraphs.AddRange(joined.Split('\u0001').Select(p => p.Trim()).Where(p => p.Length > 0));
        }

        /// <summary>True si la comilla que abre en la posición 0 es la que cierra en la última posición.</summary>
        private static bool ClosesAtEnd(string text)
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '«') depth++;
                else if (text[i] == '»')
                {
                    depth--;
                    if (depth == 0) return i == text.Length - 1;
                }
            }
            return false;
        }

        private static int Count(string text, char c) => text.Count(x => x == c);

        /// <summary>Quita énfasis Markdown (<c>*</c>, <c>**</c>, <c>_</c> envolventes) y espacios sobrantes.</summary>
        public static string CleanInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string t = text.Replace("**", string.Empty).Replace("*", string.Empty);
            t = Regex.Replace(t, @"(?<!\w)_(.+?)_(?!\w)", "$1");
            t = Regex.Replace(t, @"[ \t]{2,}", " ");
            return t.Trim();
        }
    }
}
