using System.Collections.Generic;

namespace Pacifico.Core.Collectibles
{
    /// <summary>
    /// Describe un Markdown de Archivo_Historico y los datos que el propio
    /// documento no declara (bando, facsímil, remitente por defecto).
    /// </summary>
    public sealed class DocumentSource
    {
        public string RelativePath { get; }
        public string IdPrefix { get; }
        public string Title { get; }
        public CollectibleType Type { get; }
        public Faction Faction { get; }
        public string DefaultSender { get; }
        public string DefaultRecipient { get; }
        public string FacsimileImagePath { get; }

        public DocumentSource(
            string relativePath,
            string idPrefix,
            string title,
            CollectibleType type,
            Faction faction,
            string defaultSender = null,
            string defaultRecipient = null,
            string facsimileImagePath = null)
        {
            RelativePath = relativePath;
            IdPrefix = idPrefix;
            Title = title;
            Type = type;
            Faction = faction;
            DefaultSender = defaultSender;
            DefaultRecipient = defaultRecipient;
            FacsimileImagePath = facsimileImagePath;
        }
    }

    /// <summary>Documentos del archivo histórico que se convierten en coleccionables.</summary>
    public static class HistoricalDocumentSources
    {
        private const string LettersFolder = "Archivo_Historico/05_Cartas_y_Documentos/";

        public static readonly DocumentSource GrauToCarmelaCarvajal = new DocumentSource(
            LettersFolder + "Carta_Miguel_Grau_a_Carmela_Carvajal_1879.md",
            "carta_grau_carmela_carvajal",
            "Carta de Miguel Grau a Carmela Carvajal",
            CollectibleType.Letter,
            Faction.Peru,
            defaultSender: "Miguel Grau Seminario",
            defaultRecipient: "Carmela Carvajal Briones",
            facsimileImagePath: LettersFolder + "01_Facsimil_Monumento_Carta_Grau_a_Carmela_Carvajal.jpg");

        public static readonly DocumentSource AbrahamQuirozLetters = new DocumentSource(
            LettersFolder + "Epistolario_Abraham_Quiroz_1879_1881.md",
            "carta_abraham_quiroz",
            "Abraham Quiroz",
            CollectibleType.Letter,
            Faction.Chile,
            defaultRecipient: "Luciano Quiroz");

        public static IReadOnlyList<DocumentSource> All { get; } = new[]
        {
            GrauToCarmelaCarvajal, AbrahamQuirozLetters
        };
    }
}
