using System;
using System.Collections.Generic;
using Pacifico.Core.Common;
using UnityEngine;

namespace Pacifico.Data
{
    /// <summary>Versión serializable por Unity de <see cref="HistoricalSource"/>.</summary>
    [Serializable]
    public sealed class SerializableHistoricalSource
    {
        [Tooltip("Referencias bibliográficas o archivísticas.")]
        public List<string> references = new List<string>();

        [Tooltip("Campos cuyo valor es una estimación sin fuente directa.")]
        public List<string> estimatedFields = new List<string>();

        [TextArea(2, 6)]
        public string notes = string.Empty;

        public HistoricalSource ToCore()
        {
            return new HistoricalSource
            {
                References = new List<string>(references ?? new List<string>()),
                EstimatedFields = new List<string>(estimatedFields ?? new List<string>()),
                Notes = notes ?? string.Empty,
            };
        }

        public static SerializableHistoricalSource FromCore(HistoricalSource source)
        {
            return new SerializableHistoricalSource
            {
                references = new List<string>(source.References),
                estimatedFields = new List<string>(source.EstimatedFields),
                notes = source.Notes,
            };
        }
    }
}
