using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Pacifico.Core.Common;

namespace Pacifico.Core.Narrative
{
    // ==============================================================================================
    // Guion
    // ==============================================================================================

    /// <summary>Una crónica narrada leída del guion: la voz en off, su ambientación y sus párrafos.</summary>
    public sealed class CinematicScript
    {
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        /// <summary>Voz en off («Sir George F. Morice, corresponsal británico…»).</summary>
        public string Narrator { get; set; } = string.Empty;
        /// <summary>Indicación musical y de ambiente del guion.</summary>
        public string MusicCue { get; set; } = string.Empty;
        /// <summary>Párrafos de la narración, en español (la voz es en inglés con subtítulos en español).</summary>
        public List<string> Paragraphs { get; } = new List<string>();
    }

    /// <summary>
    /// Lee una secuencia de <c>Historia_Completa_Guion.md</c> (ROADMAP 5.2): la sección cuyo encabezado contiene el
    /// título pedido, sus campos «Ubicación», «Formato» y «Voz en Off», la cita con la música y los párrafos del
    /// narrador (las citas en cursiva tras «NARRADOR»). El texto no se copia en el código: se lee del guion.
    /// </summary>
    public static class CinematicScriptParser
    {
        private static readonly Regex Field = new Regex(@"^\*\*(?<key>[^*:]+):\*\*\s*(?<value>.+)$");

        public static CinematicScript Parse(string markdown, string headingContains)
        {
            if (markdown == null) throw new ArgumentNullException(nameof(markdown));
            var script = new CinematicScript();
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            int start = Array.FindIndex(lines, l => l.StartsWith("## ") && l.IndexOf(headingContains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (start < 0) throw new ArgumentException("No se encontró la sección «" + headingContains + "» en el guion.", nameof(headingContains));
            string heading = lines[start].Substring(3).Trim();
            int colon = heading.IndexOf(':');
            script.Title = colon >= 0 ? heading.Substring(colon + 1).Trim() : heading;

            bool narrating = false;
            var paragraph = new StringBuilder();
            void Flush()
            {
                string text = Clean(paragraph.ToString());
                if (text.Length > 0) script.Paragraphs.Add(text);
                paragraph.Clear();
            }

            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("## ") || line == "---")
                {
                    if (line.StartsWith("## ") || script.Paragraphs.Count > 0 || paragraph.Length > 0) break;
                    continue;
                }

                Match field = Field.Match(line);
                if (field.Success)
                {
                    string key = field.Groups["key"].Value.Trim().ToLowerInvariant();
                    string value = Clean(field.Groups["value"].Value);
                    if (key.StartsWith("ubicaci")) script.Location = value;
                    else if (key.StartsWith("formato")) script.Format = value;
                    else if (key.StartsWith("voz")) script.Narrator = value;
                    continue;
                }
                if (line.StartsWith("**NARRADOR", StringComparison.OrdinalIgnoreCase))
                {
                    narrating = true;
                    continue;
                }
                if (!line.StartsWith(">"))
                {
                    if (narrating && line.Length == 0) continue;
                    continue;
                }

                string quote = line.Substring(1).Trim();
                if (!narrating)
                {
                    // Cita de ambientación: «(Música: …)».
                    string cue = Clean(quote).Trim('(', ')', '.', ' ');
                    if (cue.Length > 0) script.MusicCue = cue;
                    continue;
                }
                if (quote.Length == 0)
                {
                    Flush(); // «>» vacío separa párrafos dentro de la cita
                    continue;
                }
                if (paragraph.Length > 0) paragraph.Append(' ');
                paragraph.Append(quote);
            }
            Flush();
            return script;
        }

        /// <summary>Quita marcas Markdown y las comillas de apertura y cierre de la cita.</summary>
        private static string Clean(string text)
        {
            string t = text.Replace("*", string.Empty).Replace("_", string.Empty).Trim();
            t = t.Trim('"', '“', '”', '«', '»').Trim();
            return Regex.Replace(t, @"\s+", " ");
        }
    }

    // ==============================================================================================
    // Subtítulos
    // ==============================================================================================

    /// <summary>Un subtítulo: una o dos líneas visibles entre dos instantes.</summary>
    public struct SubtitleCue
    {
        public float Start;
        public float End;
        public string[] Lines;
        /// <summary>Párrafo del guion al que pertenece.</summary>
        public int Paragraph;

        public float Duration => End - Start;
        public string Text => string.Join(" ", Lines);
        public int Characters => Lines.Sum(l => l.Length);
        /// <summary>Velocidad de lectura exigida (caracteres por segundo).</summary>
        public float CharactersPerSecond => Duration > 0f ? Characters / Duration : float.PositiveInfinity;
    }

    /// <summary>
    /// Normas de subtitulado (las habituales para el castellano: 42 caracteres por línea, dos líneas, hasta 17
    /// caracteres por segundo, entre 1 y 7 s por subtítulo y un mínimo de dos fotogramas entre subtítulos).
    /// </summary>
    public sealed class SubtitleSettings
    {
        public int MaxCharsPerLine { get; set; } = 42;
        public int MaxLines { get; set; } = 2;
        public float MaxCharsPerSecond { get; set; } = 17f;
        public float MinSeconds { get; set; } = 1f;
        public float MaxSeconds { get; set; } = 7f;
        public float MinGapSeconds { get; set; } = 0.083f;

        /// <summary>
        /// Ritmo de la voz en off (caracteres del subtítulo por segundo de locución). Una lectura pausada y grave,
        /// como pide el guion, va algo por debajo del límite de lectura.
        /// </summary>
        public float NarrationCharsPerSecond { get; set; } = 13.5f;
        public float SentencePauseSeconds { get; set; } = 0.35f;
        public float ParagraphPauseSeconds { get; set; } = 1.4f;
        /// <summary>Un párrafo de una sola frase corta es un golpe de efecto: se deja respirar.</summary>
        public float DramaticPauseSeconds { get; set; } = 1.8f;
    }

    /// <summary>
    /// Genera los subtítulos sincronizados de una narración (ROADMAP 5.2): parte cada párrafo en frases y las frases
    /// largas en unidades de sentido (comas, dos puntos, conjunciones), reparte cada unidad en líneas equilibradas y
    /// les da la duración de su locución. Si se conoce la duración de la pista de voz, todo se reescala para
    /// terminar con ella. El texto de los subtítulos es, palabra por palabra, el del guion.
    /// </summary>
    public static class SubtitleBuilder
    {
        private static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?…])\s+");

        public static List<SubtitleCue> Build(IReadOnlyList<string> paragraphs, SubtitleSettings settings = null, float startSeconds = 0f)
        {
            settings = settings ?? new SubtitleSettings();
            var cues = new List<SubtitleCue>();
            float t = startSeconds;
            int maxChars = settings.MaxCharsPerLine * settings.MaxLines;
            for (int p = 0; p < paragraphs.Count; p++)
            {
                string[] sentences = SentenceEnd.Split(paragraphs[p].Trim()).Where(s => s.Length > 0).ToArray();
                bool dramatic = sentences.Length == 1 && paragraphs[p].Length <= settings.MaxCharsPerLine;
                if (dramatic && p > 0) t += settings.DramaticPauseSeconds - settings.ParagraphPauseSeconds;
                for (int s = 0; s < sentences.Length; s++)
                {
                    foreach (string unit in SplitUnits(sentences[s], maxChars))
                    {
                        string[] lines = BreakLines(unit, settings.MaxCharsPerLine);
                        int chars = lines.Sum(l => l.Length);
                        float spoken = chars / settings.NarrationCharsPerSecond;
                        float shown = MathUtil.Clamp(Math.Max(spoken, chars / settings.MaxCharsPerSecond), settings.MinSeconds, settings.MaxSeconds);
                        cues.Add(new SubtitleCue { Start = t, End = t + shown, Lines = lines, Paragraph = p });
                        t += shown + settings.MinGapSeconds;
                    }
                    t += settings.SentencePauseSeconds;
                }
                t += dramatic ? settings.DramaticPauseSeconds : settings.ParagraphPauseSeconds;
            }
            return cues;
        }

        /// <summary>
        /// Reescala los subtítulos para que la narración ocupe [<paramref name="start"/>, <paramref name="end"/>] (la
        /// pista de voz grabada manda sobre la estimación).
        /// </summary>
        public static List<SubtitleCue> FitTo(IReadOnlyList<SubtitleCue> cues, float start, float end)
        {
            var result = new List<SubtitleCue>(cues.Count);
            if (cues.Count == 0) return result;
            float from = cues[0].Start, to = cues[cues.Count - 1].End;
            float scale = (end - start) / Math.Max(1e-3f, to - from);
            foreach (SubtitleCue c in cues)
            {
                result.Add(new SubtitleCue
                {
                    Start = start + (c.Start - from) * scale,
                    End = start + (c.End - from) * scale,
                    Lines = c.Lines,
                    Paragraph = c.Paragraph,
                });
            }
            return result;
        }

        /// <summary>Subtítulo visible en el instante <paramref name="t"/> (búsqueda binaria), o −1.</summary>
        public static int CueAt(IReadOnlyList<SubtitleCue> cues, float t)
        {
            int lo = 0, hi = cues.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (t < cues[mid].Start) hi = mid - 1;
                else if (t >= cues[mid].End) lo = mid + 1;
                else return mid;
            }
            return -1;
        }

        /// <summary>Parte una frase que no cabe en un subtítulo por el corte más natural cerca de la mitad.</summary>
        public static List<string> SplitUnits(string sentence, int maxChars)
        {
            var result = new List<string>();
            string rest = sentence.Trim();
            while (rest.Length > maxChars)
            {
                int cut = BestBreak(rest, maxChars);
                result.Add(rest.Substring(0, cut).Trim());
                rest = rest.Substring(cut).Trim();
            }
            if (rest.Length > 0) result.Add(rest);
            return result;
        }

        /// <summary>
        /// Posición de corte: tras un signo de puntuación (coma, punto y coma, dos puntos) o antes de una conjunción
        /// o preposición, lo más cerca posible del final permitido sin dejar un resto ridículo; si no, en un espacio.
        /// </summary>
        private static int BestBreak(string text, int maxChars)
        {
            int limit = Math.Min(maxChars, text.Length - 1);
            int minimum = Math.Max(12, limit / 3);
            string[] weakWords = { " y ", " e ", " o ", " ni ", " pero ", " sino ", " que ", " como ", " de ", " en ", " con ", " para ", " por ", " bajo ", " sobre " };
            for (int i = limit; i >= minimum; i--)
            {
                char c = text[i - 1];
                if ((c == ',' || c == ';' || c == ':') && i < text.Length && text[i] == ' ') return i;
            }
            for (int i = limit; i >= minimum; i--)
            {
                foreach (string w in weakWords)
                {
                    if (i + w.Length <= text.Length && string.CompareOrdinal(text, i, w, 0, w.Length) == 0) return i + 1;
                }
            }
            int space = text.LastIndexOf(' ', limit);
            return space > 0 ? space + 1 : limit;
        }

        private static readonly string[] FunctionWords =
        {
            "a", "al", "de", "del", "el", "la", "las", "los", "lo", "un", "una", "unos", "unas", "y", "e", "o", "u", "ni",
            "en", "con", "por", "para", "que", "se", "su", "sus", "sin", "sobre", "bajo", "tan", "como",
        };

        /// <summary>¿La palabra que termina en <paramref name="end"/> es un artículo, preposición o conjunción?</summary>
        private static bool EndsWithFunctionWord(string text, int end)
        {
            int start = text.LastIndexOf(' ', end - 1) + 1;
            string word = text.Substring(start, end - start).ToLowerInvariant();
            return Array.IndexOf(FunctionWords, word) >= 0;
        }

        /// <summary>Reparte un subtítulo en una o dos líneas lo más iguales posible (cortando en un espacio).</summary>
        public static string[] BreakLines(string text, int maxCharsPerLine)
        {
            text = text.Trim();
            if (text.Length <= maxCharsPerLine) return new[] { text };
            int best = -1;
            int bestScore = int.MaxValue;
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (text[i] != ' ') continue;
                int left = i, right = text.Length - i - 1;
                if (left > maxCharsPerLine || right > maxCharsPerLine) continue;
                // Líneas parejas; mejor si la primera acaba en puntuación y nunca en un artículo o una preposición
                // («las aguas del / Pacífico»): la línea no debe separar palabras que se leen juntas.
                int score = Math.Abs(left - right) - (",;:".IndexOf(text[i - 1]) >= 0 ? 6 : 0) + (EndsWithFunctionWord(text, i) ? 12 : 0);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0) best = text.LastIndexOf(' ', Math.Min(maxCharsPerLine, text.Length - 1));
            if (best <= 0) return new[] { text };
            return new[] { text.Substring(0, best).Trim(), text.Substring(best + 1).Trim() };
        }
    }

    // ==============================================================================================
    // Cinemática
    // ==============================================================================================

    /// <summary>Encuadre de un plano «Ken Burns»: centro (0–1) y ampliación sobre la imagen.</summary>
    public struct KenBurnsFrame
    {
        public float CenterX;
        public float CenterY;
        public float Zoom;

        public KenBurnsFrame(float centerX, float centerY, float zoom)
        {
            CenterX = centerX;
            CenterY = centerY;
            Zoom = zoom;
        }

        public static KenBurnsFrame Lerp(KenBurnsFrame a, KenBurnsFrame b, float t)
        {
            return new KenBurnsFrame(MathUtil.Lerp(a.CenterX, b.CenterX, t), MathUtil.Lerp(a.CenterY, b.CenterY, t), MathUtil.Lerp(a.Zoom, b.Zoom, t));
        }
    }

    /// <summary>Un plano: una imagen del Archivo Histórico con un movimiento lento de cámara sobre ella.</summary>
    public sealed class CinematicShot
    {
        /// <summary>Ruta de la imagen, relativa a la raíz del repositorio.</summary>
        public string Image { get; set; } = string.Empty;
        public float Start { get; set; }
        public float End { get; set; }
        public KenBurnsFrame From { get; set; } = new KenBurnsFrame(0.5f, 0.5f, 1f);
        public KenBurnsFrame To { get; set; } = new KenBurnsFrame(0.5f, 0.5f, 1.15f);
        public string Caption { get; set; } = string.Empty;

        /// <summary>Encuadre en el instante <paramref name="t"/> (suavizado en los extremos para que no arranque en seco).</summary>
        public KenBurnsFrame FrameAt(float t)
        {
            float u = MathUtil.Clamp01((t - Start) / Math.Max(1e-3f, End - Start));
            return KenBurnsFrame.Lerp(From, To, MathUtil.SmoothStep(0f, 1f, u));
        }
    }

    /// <summary>Lo que se ve en un instante de la cinemática.</summary>
    public struct CinematicFrame
    {
        /// <summary>Plano principal y, durante un fundido encadenado, el siguiente (−1 si no hay).</summary>
        public int ShotA;
        public int ShotB;
        /// <summary>Peso del plano B (0–1).</summary>
        public float Blend;
        public KenBurnsFrame FrameA;
        public KenBurnsFrame FrameB;
        /// <summary>Negro por encima de todo (fundidos de entrada y salida): 0 transparente, 1 negro.</summary>
        public float Black;
        /// <summary>Opacidad del rótulo de título.</summary>
        public float Title;
        /// <summary>Subtítulo visible (−1 si ninguno).</summary>
        public int Cue;
        public bool Finished;
    }

    /// <summary>
    /// Cinemática de corresponsal (ROADMAP 5.2): rótulo, planos del Archivo con movimiento lento y fundidos
    /// encadenados, voz en off con subtítulos sincronizados y fundido final a negro. La evaluación es una función pura
    /// del tiempo: se puede pausar, saltar o rebobinar y siempre da el mismo fotograma, a cualquier tasa.
    /// </summary>
    public sealed class CinematicTimeline
    {
        public const float FadeInSeconds = 1.5f;
        public const float TitleSeconds = 4.5f;
        public const float CrossfadeSeconds = 1.2f;
        public const float FadeOutSeconds = 2.5f;
        /// <summary>Tras la última palabra, la imagen se sostiene antes de fundir a negro.</summary>
        public const float TailSeconds = 1.5f;

        public CinematicTimeline(CinematicScript script, IReadOnlyList<CinematicShot> shots, IReadOnlyList<SubtitleCue> cues, float duration)
        {
            Script = script;
            Shots = shots;
            Cues = cues;
            Duration = duration;
        }

        public CinematicScript Script { get; }
        public IReadOnlyList<CinematicShot> Shots { get; }
        public IReadOnlyList<SubtitleCue> Cues { get; }
        public float Duration { get; }
        public float NarrationStart => Cues.Count > 0 ? Cues[0].Start : 0f;

        public CinematicFrame Evaluate(float t)
        {
            var frame = new CinematicFrame { ShotA = -1, ShotB = -1, Cue = SubtitleBuilder.CueAt(Cues, t), Finished = t >= Duration };

            // Plano principal: el último que ha empezado; si el siguiente empieza dentro del fundido, se mezcla.
            for (int i = 0; i < Shots.Count; i++)
            {
                if (t >= Shots[i].Start) frame.ShotA = i;
            }
            if (frame.ShotA >= 0)
            {
                frame.FrameA = Shots[frame.ShotA].FrameAt(t);
                int next = frame.ShotA + 1;
                if (next < Shots.Count)
                {
                    float into = t - (Shots[next].Start - CrossfadeSeconds);
                    if (into > 0f)
                    {
                        frame.ShotB = next;
                        frame.Blend = MathUtil.SmoothStep(0f, 1f, into / CrossfadeSeconds);
                        frame.FrameB = Shots[next].FrameAt(t);
                    }
                }
            }

            float fadeIn = 1f - MathUtil.Clamp01(t / FadeInSeconds);
            float fadeOut = MathUtil.Clamp01((t - (Duration - FadeOutSeconds)) / FadeOutSeconds);
            frame.Black = Math.Max(fadeIn, fadeOut);
            // Rótulo: aparece tras el fundido de entrada y se va antes de la primera palabra.
            float titleIn = MathUtil.SmoothStep(FadeInSeconds * 0.5f, FadeInSeconds + 0.8f, t);
            float titleOut = 1f - MathUtil.SmoothStep(TitleSeconds - 1f, TitleSeconds, t);
            frame.Title = Math.Min(titleIn, titleOut);
            return frame;
        }
    }

    /// <summary>Plano previsto en el montaje: imagen del Archivo y movimiento de cámara sobre ella.</summary>
    public struct ShotSpec
    {
        public string Image;
        public KenBurnsFrame From;
        public KenBurnsFrame To;

        public ShotSpec(string image, KenBurnsFrame from, KenBurnsFrame to)
        {
            Image = image;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// El prólogo «El Ojo de Europa» (ROADMAP 5.2, verificación): la crónica de Sir George F. Morice para The Times,
    /// leída del guion, sobre grabados y fotografías del Archivo Histórico.
    /// </summary>
    public static class PrologueCinematic
    {
        public const string ScriptFile = "Historia_Completa_Guion.md";
        public const string Heading = "EL OJO DE EUROPA";

        /// <summary>
        /// Imágenes por párrafo, en el orden del texto, todas de época (fotografías, grabados y óleos del siglo XIX):
        /// las «costas desoladas» y las «rocas estériles» del prejuicio londinense (el Morro de Arica fotografiado en
        /// 1880 y el grabado del bombardeo de Pisagua); el golpe de efecto sobre el óleo del combate de Iquique de
        /// Somerscales; el salitre y las máquinas de hierro (el Huáscar y el Cochrane); y los muchachos que marcharon
        /// (un soldado chileno del Aconcagua con su Comblain y un soldado boliviano en uniforme de campaña).
        /// Se descartaron el mapa general (posterior a la guerra, con la leyenda de los tratados) y tres imágenes
        /// modernas del Archivo: el monumento del Alto de la Alianza, el desfile de recreación del Batallón Colorados y
        /// la vitrina de armamento.
        /// </summary>
        public static readonly ShotSpec[][] ShotsByParagraph =
        {
            new[]
            {
                // Costa desierta: la cámara se acerca despacio al pueblo al pie del Morro.
                new ShotSpec("Archivo_Historico/03_Lugares_y_Campos_de_Batalla/07_Morro_de_Arica_Foto_Historica_1880.jpg", new KenBurnsFrame(0.5f, 0.55f, 1f), new KenBurnsFrame(0.45f, 0.5f, 1.15f)),
                new ShotSpec("Archivo_Historico/03_Lugares_y_Campos_de_Batalla/03_Bombardeo_de_Pisagua_1879.jpg", new KenBurnsFrame(0.55f, 0.4f, 1.12f), new KenBurnsFrame(0.5f, 0.45f, 1f)),
            },
            new[]
            {
                new ShotSpec("Archivo_Historico/01_Barcos_y_Combate_Naval/03_Combate_Naval_de_Iquique_Somerscales.jpg", new KenBurnsFrame(0.5f, 0.5f, 1f), new KenBurnsFrame(0.55f, 0.45f, 1.3f)),
            },
            new[]
            {
                new ShotSpec("Archivo_Historico/01_Barcos_y_Combate_Naval/01_Monitor_Huascar_1879.jpg", new KenBurnsFrame(0.45f, 0.55f, 1.05f), new KenBurnsFrame(0.5f, 0.5f, 1.2f)),
                new ShotSpec("Archivo_Historico/01_Barcos_y_Combate_Naval/04_Fragata_Blindada_Cochrane.jpg", new KenBurnsFrame(0.5f, 0.5f, 1.2f), new KenBurnsFrame(0.5f, 0.55f, 1f)),
            },
            new[]
            {
                // Retratos verticales: en un cuadro panorámico se ve una franja; la cámara sube del pecho al rostro.
                new ShotSpec("Archivo_Historico/02_Fotos_Soldados_y_Personajes/11_Soldados_Chilenos_Regimiento_Aconcagua_Comblain.jpg", new KenBurnsFrame(0.5f, 0.55f, 1f), new KenBurnsFrame(0.5f, 0.18f, 1.05f)),
                new ShotSpec("Archivo_Historico/02_Fotos_Soldados_y_Personajes/10_Soldado_Boliviano_Uniforme_Campania.jpg", new KenBurnsFrame(0.5f, 0.5f, 1f), new KenBurnsFrame(0.5f, 0.12f, 1.1f)),
            },
        };

        /// <summary>Imágenes de cada párrafo (las de <see cref="ShotsByParagraph"/>).</summary>
        public static string[] ImagesOf(int paragraph) =>
            ShotsByParagraph[Math.Min(paragraph, ShotsByParagraph.Length - 1)].Select(s => s.Image).ToArray();

        /// <summary>Oscurecimiento de la imagen bajo el rótulo para que el título se lea sobre cualquier grabado.</summary>
        public const float TitleDim = 0.6f;

        public static CinematicScript LoadScript(Func<string, string> readRepositoryFile) =>
            CinematicScriptParser.Parse(readRepositoryFile(ScriptFile), Heading);

        /// <summary>
        /// Monta el prólogo. <paramref name="narrationSeconds"/> es la duración de la pista de voz grabada si existe
        /// (los subtítulos se ajustan a ella); si no, se usa el ritmo estimado de la locución.
        /// </summary>
        public static CinematicTimeline Build(CinematicScript script, float narrationSeconds = 0f, SubtitleSettings settings = null)
        {
            float narrationStart = CinematicTimeline.TitleSeconds + 0.5f;
            List<SubtitleCue> cues = SubtitleBuilder.Build(script.Paragraphs, settings, narrationStart);
            if (narrationSeconds > 0f && cues.Count > 0) cues = SubtitleBuilder.FitTo(cues, narrationStart, narrationStart + narrationSeconds);
            float narrationEnd = cues.Count > 0 ? cues[cues.Count - 1].End : narrationStart;
            float duration = narrationEnd + CinematicTimeline.TailSeconds + CinematicTimeline.FadeOutSeconds;

            // Cada párrafo ocupa desde su primer subtítulo hasta el primero del siguiente; el primero empieza con el
            // rótulo (tras el negro) y el último llega hasta el final.
            var shots = new List<CinematicShot>();
            int paragraphs = script.Paragraphs.Count;
            for (int p = 0; p < paragraphs; p++)
            {
                float from = p == 0 ? 0f : cues.First(c => c.Paragraph == p).Start - 0.4f;
                float to = p == paragraphs - 1 ? duration : cues.First(c => c.Paragraph == p + 1).Start - 0.4f;
                ShotSpec[] specs = ShotsByParagraph[Math.Min(p, ShotsByParagraph.Length - 1)];
                float each = (to - from) / specs.Length;
                for (int k = 0; k < specs.Length; k++)
                {
                    shots.Add(new CinematicShot
                    {
                        Image = specs[k].Image,
                        Start = from + each * k,
                        End = from + each * (k + 1) + CinematicTimeline.CrossfadeSeconds,
                        From = specs[k].From,
                        To = specs[k].To,
                    });
                }
            }
            return new CinematicTimeline(script, shots, cues, duration);
        }
    }
}
