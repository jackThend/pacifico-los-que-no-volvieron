using Pacifico.Core.Campaign;
using Pacifico.Data;
using Pacifico.Narrative;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>Al acabar el prólogo, lo anota como visto y pasa al capítulo 1.</summary>
    [RequireComponent(typeof(CinematicPlayer))]
    public sealed class PrologueCampaignLink : MonoBehaviour
    {
        private void Start() => GetComponent<CinematicPlayer>().Finished += OnFinished;

        private void OnFinished()
        {
            CampaignSave.RecordChapter(CampaignCatalog.PrologueId);
            ChapterEntry next = CampaignCatalog.Next(CampaignCatalog.PrologueId);
            if (next != null && Application.CanStreamedLevelBeLoaded(next.Scene)) CampaignNavigator.Load(next);
        }
    }
}
