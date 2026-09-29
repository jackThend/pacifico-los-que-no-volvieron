using System;
using System.Collections.Generic;
using System.IO;
using Pacifico.Core.Common;

namespace Pacifico.Core.Narrative
{
    /// <summary>
    /// Curaduría de los coleccionables textuales: qué entrada de qué documento Markdown es cada coleccionable,
    /// y los datos que el Markdown no expresa de forma estructurada (remitente, capítulo, facsímil, audio).
    /// El texto siempre se lee del Archivo Histórico: no se duplica en código.
    /// </summary>
    public static class CollectibleCatalog
    {
        public const string LettersFolder = "Archivo_Historico/05_Cartas_y_Documentos";
        public const string GrauLetterFile = LettersFolder + "/Carta_Miguel_Grau_a_Carmela_Carvajal_1879.md";
        public const string QuirozLettersFile = LettersFolder + "/Epistolario_Abraham_Quiroz_1879_1881.md";
        public const string DispatchesFile = LettersFolder + "/Cronicas_Spencer_St_John_The_Times_1879_1881.md";

        public const string GrauLetterId = "carta_grau_carmela_carvajal";

        /// <summary>Definición curada de un coleccionable.</summary>
        public sealed class Definition
        {
            public string Id;
            /// <summary>Título curado; si es null se usa el de la entrada del Markdown.</summary>
            public string Title;
            public string SourceFile;
            /// <summary>Índice de la entrada dentro del documento (0 = primera).</summary>
            public int EntryIndex;
            public CollectibleType Type;
            public string Sender;
            public string Recipient;
            public int Chapter;
            public Faction Faction;
            public string FacsimileImage = string.Empty;
            public string AudioClipKey;
        }

        /// <summary>
        /// Capítulos según la numeración del GDD §5 (1 Iquique, 2 Angamos, 3 Pisagua, 4 Tarapacá,
        /// 5 Tacna, 6 Arica, 7 Cañete, 8 Miraflores).
        /// </summary>
        public static IReadOnlyList<Definition> Definitions { get; } = new[]
        {
            new Definition
            {
                Id = GrauLetterId, Title = "Carta de Miguel Grau a Carmela Carvajal", SourceFile = GrauLetterFile, EntryIndex = 0, Type = CollectibleType.Letter,
                Sender = "Miguel Grau Seminario", Recipient = "Carmela Carvajal Briones, viuda de Arturo Prat",
                Chapter = 1, Faction = Faction.Peru,
                FacsimileImage = LettersFolder + "/01_Facsimil_Monumento_Carta_Grau_a_Carmela_Carvajal.jpg",
                AudioClipKey = "vo_carta_grau",
            },
            new Definition
            {
                Id = "carta_quiroz_1_enrolamiento", SourceFile = QuirozLettersFile, EntryIndex = 0, Type = CollectibleType.Letter,
                Sender = "Abraham Quiroz", Recipient = "Luciano Quiroz (su padre)", Chapter = 3, Faction = Faction.Chile,
                AudioClipKey = "vo_carta_quiroz_1",
            },
            new Definition
            {
                Id = "carta_quiroz_2_desierto", SourceFile = QuirozLettersFile, EntryIndex = 1, Type = CollectibleType.Letter,
                Sender = "Abraham Quiroz", Recipient = "Luciano Quiroz (su padre)", Chapter = 5, Faction = Faction.Chile,
                AudioClipKey = "vo_carta_quiroz_2",
            },
            new Definition
            {
                Id = "carta_quiroz_3_lima", SourceFile = QuirozLettersFile, EntryIndex = 2, Type = CollectibleType.Letter,
                Sender = "Abraham Quiroz", Recipient = "Luciano Quiroz (su padre)", Chapter = 8, Faction = Faction.Chile,
                AudioClipKey = "vo_carta_quiroz_3",
            },
            new Definition
            {
                Id = "despacho_1_prejuicio", Title = "El prejuicio inicial", SourceFile = DispatchesFile, EntryIndex = 0, Type = CollectibleType.Dispatch,
                Sender = "George F. Morice, The Times", Chapter = 0, Faction = Faction.Neutral, AudioClipKey = "vo_despacho_1",
            },
            new Definition
            {
                Id = "despacho_2_pisagua", Title = "La sorpresa de Pisagua y el silencio chileno", SourceFile = DispatchesFile, EntryIndex = 1, Type = CollectibleType.Dispatch,
                Sender = "Coronel Wood, observador militar británico", Chapter = 3, Faction = Faction.Neutral, AudioClipKey = "vo_despacho_2",
            },
            new Definition
            {
                Id = "despacho_3_arica", Title = "El coraje desesperado en Arica", SourceFile = DispatchesFile, EntryIndex = 2, Type = CollectibleType.Dispatch,
                Sender = "Sir Spenser St. John, Ministro británico en Lima", Chapter = 6, Faction = Faction.Neutral, AudioClipKey = "vo_despacho_3",
            },
            new Definition
            {
                Id = "despacho_4_miraflores", Title = "La caída de Lima y el horror de Miraflores", SourceFile = DispatchesFile, EntryIndex = 3, Type = CollectibleType.Dispatch,
                Sender = "Sir Spenser St. John, Ministro británico en Lima", Chapter = 8, Faction = Faction.Neutral, AudioClipKey = "vo_despacho_4",
            },
        };

        /// <summary>
        /// Construye los coleccionables leyendo los documentos con <paramref name="readRepositoryFile"/>
        /// (ruta relativa a la raíz del repositorio → contenido). Cada documento se analiza una sola vez.
        /// </summary>
        public static List<CollectibleRecord> Build(Func<string, string> readRepositoryFile)
        {
            if (readRepositoryFile == null) throw new ArgumentNullException(nameof(readRepositoryFile));
            var parsed = new Dictionary<string, ArchiveDocument>();
            var records = new List<CollectibleRecord>();

            foreach (Definition definition in Definitions)
            {
                if (!parsed.TryGetValue(definition.SourceFile, out ArchiveDocument document))
                {
                    document = ArchiveMarkdownParser.Parse(readRepositoryFile(definition.SourceFile));
                    parsed.Add(definition.SourceFile, document);
                }

                if (definition.EntryIndex >= document.Entries.Count)
                {
                    throw new InvalidDataException(definition.SourceFile + " tiene " + document.Entries.Count +
                                                   " entradas; '" + definition.Id + "' pide la " + definition.EntryIndex);
                }

                records.Add(CreateRecord(definition, document, document.Entries[definition.EntryIndex]));
            }

            return records;
        }

        /// <summary>Lee los documentos desde disco a partir de la raíz del repositorio.</summary>
        public static List<CollectibleRecord> BuildFromRepository(string repositoryRoot)
        {
            return Build(relative => File.ReadAllText(Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar))));
        }

        private static CollectibleRecord CreateRecord(Definition definition, ArchiveDocument document, ArchiveEntry entry)
        {
            return new CollectibleRecord
            {
                Id = definition.Id,
                Type = definition.Type,
                Title = definition.Title ?? entry.Title,
                Sender = definition.Sender,
                Recipient = definition.Recipient ?? string.Empty,
                DateLabel = !string.IsNullOrEmpty(entry.DateLabel) ? entry.DateLabel : document.GetMetadata("Fecha"),
                Chapter = definition.Chapter,
                Faction = definition.Faction,
                Attribution = entry.Attribution,
                Body = entry.Body,
                Signature = entry.Signature,
                SourceFile = definition.SourceFile,
                FacsimileImage = definition.FacsimileImage,
                AudioClipKey = definition.AudioClipKey ?? string.Empty,
                DesignNotes = new List<string>(document.DesignNotes),
                WordCount = entry.WordCount,
            };
        }
    }
}
