using System.Collections.Generic;

namespace Pacifico.Core.Common
{
    /// <summary>
    /// Trazabilidad de un dato histórico: de dónde sale y qué campos son estimaciones.
    /// El pilar de fidelidad histórica exige distinguir el dato documentado de la licencia de diseño.
    /// </summary>
    public sealed class HistoricalSource
    {
        /// <summary>Referencias bibliográficas o archivísticas (texto libre, una por entrada).</summary>
        public List<string> References { get; set; } = new List<string>();

        /// <summary>Nombres de campos cuyo valor es una estimación razonable sin fuente directa.</summary>
        public List<string> EstimatedFields { get; set; } = new List<string>();

        /// <summary>Notas de contexto (discrepancias entre fuentes, estado en 1879, etc.).</summary>
        public string Notes { get; set; } = string.Empty;

        public bool IsEstimated(string field) => EstimatedFields.Contains(field);

        public HistoricalSource Clone()
        {
            return new HistoricalSource
            {
                References = new List<string>(References),
                EstimatedFields = new List<string>(EstimatedFields),
                Notes = Notes,
            };
        }
    }
}
