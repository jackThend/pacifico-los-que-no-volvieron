using System;
using System.Collections.Generic;
using System.Linq;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Progreso de la campaña: capítulos completados y coleccionables desbloqueados. Se guarda como una línea de
    /// texto («cap:…;col:…») para que cualquier almacén (PlayerPrefs, fichero) sirva sin serializadores.
    /// </summary>
    public sealed class CampaignProgress
    {
        private readonly SortedSet<string> _chapters = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> _collectibles = new SortedSet<string>(StringComparer.Ordinal);

        public IEnumerable<string> CompletedChapters => _chapters;
        public IEnumerable<string> UnlockedCollectibles => _collectibles;

        public bool IsCompleted(string chapterId) => _chapters.Contains(chapterId);
        public bool IsUnlocked(string collectibleId) => _collectibles.Contains(collectibleId);

        /// <summary>Anota una misión completada y su recompensa. Devuelve true si el coleccionable es nuevo.</summary>
        public bool Complete(MissionScript mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            _chapters.Add(mission.Id);
            return !string.IsNullOrEmpty(mission.RewardCollectibleId) && _collectibles.Add(mission.RewardCollectibleId);
        }

        public bool Unlock(string collectibleId) => !string.IsNullOrEmpty(collectibleId) && _collectibles.Add(collectibleId);

        public string Serialize() => "cap:" + string.Join(",", _chapters) + ";col:" + string.Join(",", _collectibles);

        public static CampaignProgress Parse(string text)
        {
            var progress = new CampaignProgress();
            if (string.IsNullOrEmpty(text)) return progress;
            foreach (string part in text.Split(';'))
            {
                int colon = part.IndexOf(':');
                if (colon < 0) continue;
                string key = part.Substring(0, colon);
                IEnumerable<string> ids = part.Substring(colon + 1).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
                SortedSet<string> target = key == "cap" ? progress._chapters : key == "col" ? progress._collectibles : null;
                if (target == null) continue;
                foreach (string id in ids) target.Add(id);
            }
            return progress;
        }
    }
}
