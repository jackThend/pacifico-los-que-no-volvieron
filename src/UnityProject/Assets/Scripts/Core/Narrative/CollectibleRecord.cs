using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Narrative
{
    public enum CollectibleType
    {
        Letter = 0,
        Dispatch = 1,
        Photograph = 2,
        Document = 3,
    }

    /// <summary>
    /// Coleccionable de «La Memoria Rota» (GDD §4) listo para el visor de documentos:
    /// texto transcrito, remitente, facsímil y pista de audio para [Escuchar Carta].
    /// </summary>
    public sealed class CollectibleRecord
    {
        /// <summary>Palabras por minuto de una lectura pausada en voz alta (para estimar subtítulos sin audio).</summary>
        public const float NarrationWordsPerMinute = 130f;

        public string Id { get; set; } = string.Empty;
        public CollectibleType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        /// <summary>Lugar y fecha tal como constan en la fuente.</summary>
        public string DateLabel { get; set; } = string.Empty;
        /// <summary>Capítulo del GDD §5 en que se obtiene (0 = prólogo).</summary>
        public int Chapter { get; set; }
        public Faction Faction { get; set; }
        public string Attribution { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        /// <summary>Texto manuscrito del reverso (fotografías), visible al rotar el objeto.</summary>
        public string ReverseInscription { get; set; } = string.Empty;
        /// <summary>Documento Markdown de origen, relativo a la raíz del repositorio.</summary>
        public string SourceFile { get; set; } = string.Empty;
        /// <summary>Imagen de facsímil relativa a la raíz del repositorio (opcional).</summary>
        public string FacsimileImage { get; set; } = string.Empty;
        /// <summary>Clave de la pista de voz (AudioClip) para [Escuchar Carta].</summary>
        public string AudioClipKey { get; set; } = string.Empty;
        public List<string> DesignNotes { get; set; } = new List<string>();
        public int WordCount { get; set; }

        public float EstimatedNarrationSeconds => WordCount / NarrationWordsPerMinute * 60f;

        public ValidationResult Validate()
        {
            var r = new ValidationResult("Coleccionable '" + Id + "'");
            r.RequireNotEmpty(Id, "Id");
            r.RequireNotEmpty(Title, "Title");
            r.RequireNotEmpty(Sender, "Sender");
            r.RequireNotEmpty(Body, "Body");
            r.RequireNotEmpty(SourceFile, "SourceFile");
            r.Require(Chapter >= 0 && Chapter <= 8, "Chapter debe estar entre 0 (prólogo) y 8");
            r.Require(Type != CollectibleType.Letter || !string.IsNullOrWhiteSpace(Recipient), "una carta necesita destinatario");
            r.Require(Body.IndexOf('*') < 0 && Body.IndexOf('#') < 0, "el cuerpo contiene marcas Markdown sin limpiar");
            return r;
        }
    }
}
