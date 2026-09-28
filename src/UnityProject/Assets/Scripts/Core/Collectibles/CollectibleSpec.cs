using System.Collections.Generic;

namespace Pacifico.Core.Collectibles
{
    /// <summary>
    /// Documento histórico coleccionable. Fuente de verdad para
    /// CollectibleDataSO; se construye a partir de los Markdown de
    /// Archivo_Historico mediante <see cref="CollectibleMarkdownParser"/>.
    /// </summary>
    public sealed class CollectibleSpec
    {
        public string Id { get; }
        public string Title { get; }
        public CollectibleType Type { get; }
        public Faction Faction { get; }
        public string Sender { get; }
        public string Recipient { get; }
        public string Location { get; }
        public string DateText { get; }
        public int Year { get; }
        public int Chapter { get; }
        public string Transcription { get; }
        public string Context { get; }
        public string SourceReference { get; }
        public string SourceDocumentPath { get; }
        public string FacsimileImagePath { get; }
        public string NarrationKey { get; }
        public IReadOnlyList<string> GameNotes { get; }

        public CollectibleSpec(
            string id,
            string title,
            CollectibleType type,
            Faction faction,
            string sender,
            string recipient,
            string location,
            string dateText,
            int year,
            int chapter,
            string transcription,
            string context,
            string sourceReference,
            string sourceDocumentPath,
            string facsimileImagePath,
            string narrationKey,
            IReadOnlyList<string> gameNotes)
        {
            Id = id;
            Title = title;
            Type = type;
            Faction = faction;
            Sender = sender;
            Recipient = recipient;
            Location = location;
            DateText = dateText;
            Year = year;
            Chapter = chapter;
            Transcription = transcription;
            Context = context;
            SourceReference = sourceReference;
            SourceDocumentPath = sourceDocumentPath;
            FacsimileImagePath = facsimileImagePath;
            NarrationKey = narrationKey;
            GameNotes = gameNotes;
        }
    }
}
