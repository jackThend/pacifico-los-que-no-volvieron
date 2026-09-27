using Pacifico.Core.Campaign;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>Guarda el <see cref="CampaignProgress"/> en PlayerPrefs (una línea de texto).</summary>
    public static class CampaignSave
    {
        public const string Key = "pacifico.campana";

        public static CampaignProgress Load() => CampaignProgress.Parse(PlayerPrefs.GetString(Key, string.Empty));

        /// <summary>Anota la misión completada. Devuelve true si su coleccionable es nuevo.</summary>
        public static bool Record(MissionScript mission)
        {
            CampaignProgress progress = Load();
            bool isNew = progress.Complete(mission);
            PlayerPrefs.SetString(Key, progress.Serialize());
            PlayerPrefs.Save();
            return isNew;
        }
    }
}
