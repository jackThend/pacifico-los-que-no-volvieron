using System.Collections.Generic;

namespace Pacifico.Core.Collectibles
{
    /// <summary>Reglas de coherencia para cualquier CollectibleSpec.</summary>
    public static class CollectibleValidator
    {
        // Restos de sintaxis Markdown que no deben llegar al texto mostrado en pantalla.
        private static readonly string[] MarkdownArtifacts = { "*", "> ", "#", "«", "»" };

        public static IReadOnlyList<string> Validate(CollectibleSpec spec)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("La especificación es nula.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(spec.Id)) errors.Add("Id vacío.");
            if (string.IsNullOrWhiteSpace(spec.Title)) errors.Add($"{spec.Id}: título vacío.");
            if (string.IsNullOrWhiteSpace(spec.Sender)) errors.Add($"{spec.Id}: remitente vacío.");
            if (spec.Type == CollectibleType.Letter && string.IsNullOrWhiteSpace(spec.Recipient))
                errors.Add($"{spec.Id}: una carta necesita destinatario.");
            if (string.IsNullOrWhiteSpace(spec.DateText)) errors.Add($"{spec.Id}: fecha vacía.");
            if (!ProjectInfo.IsWithinWarPeriod(spec.Year))
                errors.Add($"{spec.Id}: año {spec.Year} fuera del periodo {ProjectInfo.WarStartYear}-{ProjectInfo.WarEndYear}.");
            if (spec.Chapter < 1 || spec.Chapter > 8) errors.Add($"{spec.Id}: capítulo {spec.Chapter} fuera de rango [1, 8].");
            if (string.IsNullOrWhiteSpace(spec.NarrationKey)) errors.Add($"{spec.Id}: sin clave de narración.");

            if (string.IsNullOrWhiteSpace(spec.Transcription))
            {
                errors.Add($"{spec.Id}: transcripción vacía.");
            }
            else
            {
                foreach (var artifact in MarkdownArtifacts)
                {
                    if (spec.Transcription.Contains(artifact))
                        errors.Add($"{spec.Id}: la transcripción conserva sintaxis Markdown '{artifact}'.");
                }
            }

            return errors;
        }
    }
}
