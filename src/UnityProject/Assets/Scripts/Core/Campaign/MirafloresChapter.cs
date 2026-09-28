using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pacifico.Core.Narrative;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 8, «Los que no volvieron» (15 de enero de 1881, reductos de Miraflores), según
    /// <c>Historia_Completa_Guion.md</c> y el GDD: el soldado Abraham Quiroz, del 3.º de Línea. El prólogo del
    /// corresponsal, el avance por los jardines bajo el bombardeo, el asalto al Reducto N.º 3 y el rostro del enemigo
    /// (un oficinista y un muchacho de 14 años), la artillería silenciada y el alto el fuego, y el desenlace: Abraham se
    /// sienta contra el parapeto con la carta para su padre y cae por un disparo rezagado. Sigue el epílogo
    /// «La memoria rota» (<see cref="MemoriaRotaEpilogue"/>). Los textos del corresponsal se leen del guion.
    /// </summary>
    public static class MirafloresChapter
    {
        public const string Id = "cap8_miraflores";
        public const string ChapterHeading = "CAPÍTULO 8";
        public const int RedoubtDefenders = 6;
        public const int RedoubtGuns = 2;

        public static class Facts
        {
            public const string DefendersDown = "defensores_fuera_de_combate";
            public const string RedoubtDown = "defensores_del_reducto_caidos";
            public const string GunsSilenced = "piezas_silenciadas";
        }

        public static class Flags
        {
            public const string AtRedoubt = "ante_el_reducto";
            public const string EnemyFace = "el_rostro_del_enemigo";
            public const string Seated = "sentado_contra_el_parapeto";
            public const string Shot = "el_disparo";
            public const string EpilogueFinished = "epilogo_terminado";
            public const string PlayerDown = "abraham_quiroz_cae";
        }

        public static class Stages
        {
            public const string Prologue = "q0_prologo";
            public const string Gardens = "q1_jardines";
            public const string Redoubt = "q2_reducto_3";
            public const string Guns = "q3_artilleria";
            public const string Ending = "q4_desenlace";
        }

        public static class Perspectives
        {
            public const string Correspondent = "corresponsal";
            public const string Soldier = "abraham_quiroz";
            /// <summary>Sentado contra el parapeto: sin mandos, vista íntima.</summary>
            public const string Intimate = "intima";
        }

        public const string Correspondent = "El corresponsal";

        /// <summary>Divide un texto en frases (para mostrarlo en líneas legibles).</summary>
        public static IEnumerable<string> Sentences(string text) =>
            Regex.Split(text, @"(?<=[.!?…])\s+").Select(s => s.Trim()).Where(s => s.Length > 0);

        public static MissionScript Build(Func<string, string> readRepositoryFile) => Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading));

        public static MissionScript Build(GuionQuotes guion)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);

            var script = new MissionScript
            {
                Id = Id, Title = "Los que no volvieron", Date = "15 de enero de 1881", Location = "Reductos de Miraflores (Lima)",
                RewardCollectibleId = "carta_quiroz_3_lima",
            };

            var prologue = new MissionStage { Id = Stages.Prologue, Title = "Prólogo del corresponsal", Perspective = Perspectives.Correspondent };
            foreach (string sentence in Sentences(string.Join(" ", guion.FindBlock("Miraflores no era"))))
            {
                prologue.OnEnter.Add(new MissionLine(Correspondent, sentence));
            }
            prologue.Transitions.Add(new MissionTransition(MissionCondition.DialogueIdle(), Stages.Gardens));
            script.Stages.Add(prologue);

            var gardens = new MissionStage { Id = Stages.Gardens, Title = "Los jardines de Miraflores", Perspective = Perspectives.Soldier };
            gardens.OnEnter.Add(Narrate("15 de enero de 1881. Soldado Abraham Quiroz, 3.º de Línea, natural de Quillota."));
            gardens.OnEnter.Add(Narrate("Los Krupp chilenos y las fragatas desde el mar bombardean los balnearios de Chorrillos y Miraflores."));
            gardens.OnEnter.Add(Hint("Avanza con tu escuadra entre muros de adobe derrumbados y jardines destrozados. Clic derecho: encarar. Clic: fuego. F: bayoneta."));
            gardens.Objectives.Add(new MissionObjective { Id = "avanzar", Text = "Avanza con tu escuadra hasta el Reducto N.º 3", Complete = MissionCondition.Flag(Flags.AtRedoubt) });
            var shouts = new MissionTrigger { Id = "gritos", When = MissionCondition.AtLeast(Facts.DefendersDown, 2) };
            shouts.Lines.Add(Narrate("Entre las tapias se oyen gritos de hombres que defienden sus casas."));
            gardens.Triggers.Add(shouts);
            gardens.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.AtRedoubt), Stages.Redoubt));
            script.Stages.Add(gardens);

            var redoubt = new MissionStage { Id = Stages.Redoubt, Title = "El rostro del enemigo", Perspective = Perspectives.Soldier };
            redoubt.OnEnter.Add(Narrate("La escuadra asalta el Reducto N.º 3."));
            redoubt.Objectives.Add(new MissionObjective
            {
                Id = "reducto", Text = "Asalta la trinchera del reducto",
                Complete = MissionCondition.AtLeast(Facts.RedoubtDown, RedoubtDefenders), ProgressFact = Facts.RedoubtDown, ProgressTarget = RedoubtDefenders,
            });
            var face = new MissionTrigger { Id = "rostro", When = MissionCondition.Flag(Flags.EnemyFace), Interrupts = true };
            face.Lines.Add(Narrate("No es un soldado. Es un hombre de anteojos y manos finas de oficinista, con chaleco civil."));
            face.Lines.Add(Narrate("A su lado, un muchacho voluntario que no pasa de los 14 años, abrazado a un fusil descargado."));
            face.Lines.Add(Narrate("Abraham retrocede un paso. El peso de la carnicería lo aplasta."));
            redoubt.Triggers.Add(face);
            redoubt.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.AtLeast(Facts.RedoubtDown, RedoubtDefenders), MissionCondition.DialogueIdle()), Stages.Guns));
            script.Stages.Add(redoubt);

            var guns = new MissionStage { Id = Stages.Guns, Title = "El cumplimiento del objetivo", Perspective = Perspectives.Soldier };
            guns.OnEnter.Add(Hint("Silencia la artillería del reducto: quédate junto a cada pieza sin defensores en pie."));
            guns.Objectives.Add(new MissionObjective
            {
                Id = "piezas", Text = "Silencia la artillería del reducto",
                Complete = MissionCondition.AtLeast(Facts.GunsSilenced, RedoubtGuns), ProgressFact = Facts.GunsSilenced, ProgressTarget = RedoubtGuns,
            });
            var ceasefire = new MissionTrigger { Id = "alto_el_fuego", When = MissionCondition.AtLeast(Facts.GunsSilenced, RedoubtGuns) };
            ceasefire.Lines.Add(Narrate("La bandera peruana del parapeto cae al suelo."));
            ceasefire.Lines.Add(Narrate("Clarines lejanos tocan alto el fuego en las colinas cercanas. El camino hacia Lima está abierto."));
            guns.Triggers.Add(ceasefire);
            guns.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.AtLeast(Facts.GunsSilenced, RedoubtGuns), MissionCondition.DialogueIdle()), Stages.Ending));
            script.Stages.Add(guns);

            var ending = new MissionStage { Id = Stages.Ending, Title = "El desenlace", Perspective = Perspectives.Soldier };
            ending.OnEnter.Add(Narrate("Los soldados de la escuadra se dejan caer al suelo, exhaustos, y se desabrochan los correajes empapados."));
            ending.OnEnter.Add(Hint("E: sentarte contra el parapeto de tierra."));
            ending.Objectives.Add(new MissionObjective { Id = "descanso", Text = "Siéntate contra el parapeto", Complete = MissionCondition.Flag(Flags.Seated) });
            var letter = new MissionTrigger { Id = "la_carta", When = MissionCondition.Flag(Flags.Seated), Interrupts = true };
            letter.Lines.Add(Narrate("Solo se oye tu respiración y el viento de la costa en las ramas de un olivo partido por un cañonazo."));
            letter.Lines.Add(Narrate("Del bolsillo interior de la guerrera, un sobre arrugado: una carta a tu padre, don Luciano Quiroz, en Quillota."));
            letter.Lines.Add(Narrate("La guerra ha terminado. Podrás volver al valle verde de tu casa."));
            ending.Triggers.Add(letter);
            ending.Triggers.Add(new MissionTrigger
            {
                Id = "disparo", SetsFlag = Flags.Shot,
                When = MissionCondition.All(MissionCondition.Flag(Flags.Seated), MissionCondition.DialogueIdle()),
            });
            ending.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.EpilogueFinished), MissionScript.CompleteStage));
            script.Stages.Add(ending);

            script.Failures.Add(new MissionFailure(MissionCondition.All(MissionCondition.Flag(Flags.PlayerDown), MissionCondition.NotFlag(Flags.Seated)),
                "Abraham Quiroz cae antes de llegar al final de la batalla."));
            return script;
        }
    }

    /// <summary>
    /// Epílogo «La memoria rota» leído del guion (sección «EPÍLOGO»): la lápida de Abraham Quiroz, la carta a su padre,
    /// el mosaico de retratos de época, la cifra de muertos y la última cita del corresponsal.
    /// </summary>
    public sealed class MemoriaRotaEpilogue
    {
        public const string SectionHeading = "EPÍLOGO";

        /// <summary>
        /// Retratos de época del Archivo para el mosaico (de los tres bandos). Se excluyen a propósito las imágenes
        /// modernas del Archivo (el desfile de recreación de los Colorados, el monumento de Intiorko, la vitrina de museo).
        /// </summary>
        public static readonly string[] MosaicImages =
        {
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/01_Arturo_Prat_Chacon.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/02_Miguel_Grau_Seminario.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/03_Francisco_Bolognesi.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/05_Coronel_Alfonso_Ugarte.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/04_Andres_Avelino_Caceres.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/10_Soldado_Boliviano_Uniforme_Campania.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/11_Soldados_Chilenos_Regimiento_Aconcagua_Comblain.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/08_Cantinera_Irene_Morales.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/09_Culi_Chino_Esclavizado_Peru_1881.jpg",
            "Archivo_Historico/02_Fotos_Soldados_y_Personajes/12_Mutilado_de_Guerra_1880.jpg",
        };

        public static readonly string[] ExcludedModernImages =
        {
            "06_Batallon_Colorados_de_Bolivia.jpg", "05_Campo_de_la_Alianza_Tacna_Intiorko.jpg", "06_Armamento_Aliado_Alto_Alianza.jpg",
        };

        public string[] Card { get; private set; } = new string[0];
        public string[] Letter { get; private set; } = new string[0];
        public string Statistic { get; private set; } = string.Empty;
        public string FinalQuote { get; private set; } = string.Empty;

        public static MemoriaRotaEpilogue Parse(string markdown)
        {
            string section = GuionQuotes.Section(markdown, SectionHeading);
            GuionQuotes quotes = GuionQuotes.Extract(section, SectionHeading);
            var e = new MemoriaRotaEpilogue
            {
                Card = quotes.Fenced.ToArray(),
                Letter = quotes.FindBlock("Señor don Luciano Quiroz"),
                FinalQuote = string.Join(" ", quotes.FindBlock("El honor militar")),
            };
            Match stat = Regex.Match(section, @"\*(?<s>Más de [\d.]+ seres humanos[^*]*)\*");
            if (!stat.Success) throw new ArgumentException("El epílogo del guion no tiene la cifra de muertos («Más de …»).");
            e.Statistic = stat.Groups["s"].Value.Trim();
            if (e.Card.Length == 0) throw new ArgumentException("El epílogo del guion no tiene la lápida (bloque ```).");
            return e;
        }

        public static MemoriaRotaEpilogue Load(Func<string, string> readRepositoryFile) => Parse(readRepositoryFile(GuionQuotes.ScriptFile));
    }

    public struct EpilogueFrame
    {
        /// <summary>Negro por encima de todo (fundidos de entrada y salida).</summary>
        public float Black;
        /// <summary>Opacidad del marco del retrato y la lápida.</summary>
        public float Card;
        /// <summary>Líneas de la lápida completas y caracteres escritos de la siguiente (máquina de escribir).</summary>
        public int CardLines;
        public int CardChars;
        /// <summary>Subtítulo de la carta visible (-1: ninguno).</summary>
        public int Cue;
        /// <summary>Retratos del mosaico ya aparecidos (fracción de 0 a 1: entran uno a uno y no se retiran).</summary>
        public float Mosaic;
        /// <summary>Opacidad del mosaico: baja bajo la cifra y la cita final para que se lean (el mosaico no desaparece).</summary>
        public float MosaicAlpha;
        public float Statistic;
        public float Quote;
        public bool Finished;
    }

    /// <summary>
    /// Línea de tiempo del epílogo: una función pura del tiempo, como el prólogo (5.2). Negro, lápida escrita a máquina,
    /// la carta subtitulada al ritmo de lectura, el mosaico que se va llenando, la cifra, la cita final y el fundido.
    /// </summary>
    public sealed class EpilogueTimeline
    {
        public const float BlackSeconds = 2.5f;
        public const float CharsPerSecond = 18f;
        public const float LinePauseSeconds = 0.6f;
        public const float HoldSeconds = 2f;
        public const float MosaicSeconds = 8f;
        public const float StatisticSeconds = 6f;
        public const float QuoteSeconds = 11f;
        public const float FadeOutSeconds = 3.5f;

        private readonly float[] _lineStarts;

        public EpilogueTimeline(MemoriaRotaEpilogue epilogue, SubtitleSettings settings = null)
        {
            Epilogue = epilogue ?? throw new ArgumentNullException(nameof(epilogue));
            _lineStarts = new float[epilogue.Card.Length + 1];
            float t = BlackSeconds;
            for (int i = 0; i < epilogue.Card.Length; i++)
            {
                _lineStarts[i] = t;
                t += epilogue.Card[i].Length / CharsPerSecond + LinePauseSeconds;
            }
            _lineStarts[epilogue.Card.Length] = t;
            LetterStart = t + HoldSeconds;
            Cues = SubtitleBuilder.Build(epilogue.Letter, settings, LetterStart);
            MosaicStart = (Cues.Count > 0 ? Cues[Cues.Count - 1].End : LetterStart) + 1.5f;
            StatisticStart = MosaicStart + MosaicSeconds;
            QuoteStart = StatisticStart + StatisticSeconds;
            Duration = QuoteStart + QuoteSeconds + FadeOutSeconds;
        }

        public MemoriaRotaEpilogue Epilogue { get; }
        public IReadOnlyList<SubtitleCue> Cues { get; }
        public float LetterStart { get; }
        public float MosaicStart { get; }
        public float StatisticStart { get; }
        public float QuoteStart { get; }
        public float Duration { get; }

        private static float Ramp(float t, float start, float seconds) => Math.Max(0f, Math.Min(1f, (t - start) / seconds));

        public EpilogueFrame Evaluate(float t)
        {
            var f = new EpilogueFrame { Cue = -1 };
            f.Black = Math.Max(1f - Ramp(t, 0.5f, BlackSeconds - 0.5f), Ramp(t, Duration - FadeOutSeconds, FadeOutSeconds));
            // Lápida: visible desde el negro hasta que el mosaico la sustituye.
            f.Card = Ramp(t, BlackSeconds - 0.5f, 1f) * (1f - Ramp(t, MosaicStart, 1.5f));
            string[] card = Epilogue.Card;
            f.CardLines = 0;
            for (int i = 0; i < card.Length; i++)
            {
                if (t >= _lineStarts[i + 1] - LinePauseSeconds) f.CardLines = i + 1;
                else
                {
                    f.CardChars = t < _lineStarts[i] ? 0 : Math.Min(card[i].Length, (int)((t - _lineStarts[i]) * CharsPerSecond));
                    break;
                }
            }
            f.Cue = SubtitleBuilder.CueAt(Cues, t);
            f.Mosaic = Ramp(t, MosaicStart, MosaicSeconds * 0.8f);
            f.MosaicAlpha = f.Mosaic > 0f ? 1f - 0.78f * Ramp(t, StatisticStart, 1.5f) : 0f;
            f.Statistic = Ramp(t, StatisticStart, 1.5f) * (1f - Ramp(t, QuoteStart, 1f));
            f.Quote = Ramp(t, QuoteStart, 1.5f);
            f.Finished = t >= Duration;
            return f;
        }
    }
}
