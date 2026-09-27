using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Narrative;
using UnityEngine;

namespace Pacifico.Data
{
    /// <summary>
    /// Coleccionable de «La Memoria Rota» (ROADMAP 1.3, GDD §4): texto transcrito, facsímil 3D y voz en off.
    /// Se importa desde los Markdown del Archivo Histórico con «Pacífico/Datos/Generar ScriptableObjects históricos».
    /// </summary>
    [CreateAssetMenu(fileName = "Collectible_", menuName = "Pacífico/Datos/Coleccionable", order = 2)]
    public sealed class CollectibleDataSO : ScriptableObject
    {
        [Header("Identidad")]
        public string id = string.Empty;
        public CollectibleType type = CollectibleType.Letter;
        public string title = string.Empty;
        public string sender = string.Empty;
        public string recipient = string.Empty;
        public string dateLabel = string.Empty;
        [Range(0, 8)] public int chapter;
        public Faction faction = Faction.Neutral;

        [Header("Texto")]
        public string attribution = string.Empty;
        [TextArea(8, 30)] public string body = string.Empty;
        [TextArea(1, 4)] public string signature = string.Empty;
        [Tooltip("Dedicatoria del reverso, visible al girar una fotografía en el visor.")]
        [TextArea(1, 4)] public string reverseInscription = string.Empty;
        [Min(0)] public int wordCount;

        [Header("Facsímil y audio")]
        [Tooltip("Textura del documento original para el visor 3D.")]
        public Texture2D facsimile;
        [Tooltip("Voz en off para [Escuchar Carta]. Si falta, el visor muestra solo subtítulos.")]
        public AudioClip narration;
        public string audioClipKey = string.Empty;

        [Header("Procedencia")]
        public string sourceFile = string.Empty;
        public string facsimileSourceImage = string.Empty;
        public List<string> designNotes = new List<string>();

        /// <summary>Duración de la narración: la del clip si existe, o una estimación por número de palabras.</summary>
        public float NarrationSeconds => narration != null ? narration.length : wordCount / CollectibleRecord.NarrationWordsPerMinute * 60f;

        public CollectibleRecord ToRecord()
        {
            return new CollectibleRecord
            {
                Id = id,
                Type = type,
                Title = title,
                Sender = sender,
                Recipient = recipient,
                DateLabel = dateLabel,
                Chapter = chapter,
                Faction = faction,
                Attribution = attribution,
                Body = body,
                Signature = signature,
                ReverseInscription = reverseInscription,
                SourceFile = sourceFile,
                FacsimileImage = facsimileSourceImage,
                AudioClipKey = audioClipKey,
                DesignNotes = new List<string>(designNotes),
                WordCount = wordCount,
            };
        }

        /// <summary>Copia los datos textuales; las referencias a assets (textura, audio) se conservan.</summary>
        public void CopyFrom(CollectibleRecord record)
        {
            id = record.Id;
            type = record.Type;
            title = record.Title;
            sender = record.Sender;
            recipient = record.Recipient;
            dateLabel = record.DateLabel;
            chapter = record.Chapter;
            faction = record.Faction;
            attribution = record.Attribution;
            body = record.Body;
            signature = record.Signature;
            if (!string.IsNullOrEmpty(record.ReverseInscription)) reverseInscription = record.ReverseInscription;
            sourceFile = record.SourceFile;
            facsimileSourceImage = record.FacsimileImage;
            audioClipKey = record.AudioClipKey;
            designNotes = new List<string>(record.DesignNotes);
            wordCount = record.WordCount;
        }

        public ValidationResult Validate() => ToRecord().Validate();

        private void OnValidate()
        {
            var result = Validate();
            if (!result.IsValid) Debug.LogWarning(result.ToString(), this);
        }
    }
}
