using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Punto de control de una misión: la etapa a la que se vuelve, los hechos del guion en ese momento y los datos que
    /// el director de la escena necesita para recolocar el mundo (<see cref="Data"/>: posiciones, hombres, averías).
    /// Es una línea de texto por valor, para guardarse en las preferencias del jugador.
    /// </summary>
    public sealed class MissionCheckpoint
    {
        private const string Header = "pacifico-punto-de-control-1";

        public string ChapterId { get; set; } = string.Empty;
        public string StageId { get; set; } = string.Empty;
        public string StageTitle { get; set; } = string.Empty;
        public Dictionary<string, float> Numbers { get; } = new Dictionary<string, float>(StringComparer.Ordinal);
        public HashSet<string> Flags { get; } = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<string, float> Data { get; } = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>Foto del guion en la etapa actual. Los datos de la escena los añade el director.</summary>
        public static MissionCheckpoint Capture(MissionRunner runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            var checkpoint = new MissionCheckpoint
            {
                ChapterId = runner.Script.Id,
                StageId = runner.CurrentStage.Id,
                StageTitle = runner.CurrentStage.Title,
            };
            foreach (KeyValuePair<string, float> number in runner.Facts.Numbers) checkpoint.Numbers[number.Key] = number.Value;
            foreach (string flag in runner.Facts.Flags) checkpoint.Flags.Add(flag);
            return checkpoint;
        }

        public float Get(string key, float fallback = 0f) => Data.TryGetValue(key, out float value) ? value : fallback;

        public string Serialize()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            text.Append("capitulo\t").Append(ChapterId).Append('\n');
            text.Append("etapa\t").Append(StageId).Append('\n');
            text.Append("titulo\t").Append(StageTitle).Append('\n');
            foreach (KeyValuePair<string, float> n in Numbers) Line(text, "n", n.Key, n.Value);
            foreach (string flag in Flags) text.Append("m\t").Append(flag).Append('\n');
            foreach (KeyValuePair<string, float> d in Data) Line(text, "d", d.Key, d.Value);
            return text.ToString();
        }

        private static void Line(StringBuilder text, string kind, string key, float value)
        {
            text.Append(kind).Append('\t').Append(key).Append('\t').Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        /// <summary>Lee un punto de control guardado; null si está vacío o no se entiende (nunca falla por datos viejos).</summary>
        public static MissionCheckpoint Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] lines = text.Split('\n');
            if (lines[0] != Header) return null;
            var checkpoint = new MissionCheckpoint();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length == 2)
                {
                    switch (parts[0])
                    {
                        case "capitulo": checkpoint.ChapterId = parts[1]; break;
                        case "etapa": checkpoint.StageId = parts[1]; break;
                        case "titulo": checkpoint.StageTitle = parts[1]; break;
                        case "m": checkpoint.Flags.Add(parts[1]); break;
                    }
                }
                else if (parts.Length == 3 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    if (parts[0] == "n") checkpoint.Numbers[parts[1]] = value;
                    else if (parts[0] == "d") checkpoint.Data[parts[1]] = value;
                }
            }
            return checkpoint.ChapterId.Length > 0 && checkpoint.StageId.Length > 0 ? checkpoint : null;
        }
    }
}
