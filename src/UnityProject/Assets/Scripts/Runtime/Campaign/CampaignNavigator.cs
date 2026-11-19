using Pacifico.Core.Campaign;
using Pacifico.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Navegación de la campaña (ROADMAP 6.6): cargar un capítulo, pasar al siguiente jugable o volver al menú. Las
    /// escenas se cargan por nombre (<see cref="CampaignCatalog"/>): deben estar en la build, como las deja
    /// <c>GameBuilder</c>.
    /// </summary>
    public static class CampaignNavigator
    {
        public static void Load(ChapterEntry chapter)
        {
            if (chapter == null || !chapter.Implemented) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(chapter.Scene);
        }

        /// <summary>Vuelve al último punto de control guardado, sea del capítulo que sea.</summary>
        public static void ResumeCheckpoint()
        {
            MissionCheckpoint checkpoint = CheckpointStore.Current;
            if (checkpoint == null) return;
            foreach (ChapterEntry chapter in CampaignCatalog.Chapters)
            {
                if (chapter.Id != checkpoint.ChapterId) continue;
                CheckpointStore.RequestResume();
                Load(chapter);
                return;
            }
        }

        public static void Menu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(CampaignCatalog.MenuScene)) SceneManager.LoadScene(CampaignCatalog.MenuScene);
        }

        /// <summary>Al acabar una misión: N (completada) pasa al siguiente capítulo; M vuelve al menú.</summary>
        public static void HandleEndKeys(string chapterId, bool completed)
        {
            if (completed && GameInput.Pressed(GameKey.N))
            {
                ChapterEntry next = CampaignCatalog.Next(chapterId);
                if (next != null) Load(next);
                else Menu();
            }
            // Con mando, A vuelve al punto de control (C en el teclado).
            if (!completed && CheckpointStore.ExistsFor(chapterId) && (GameInput.Pressed(GameKey.C) || GameInput.PadPressed(PadButton.A))) ResumeCheckpoint();
            if (GameInput.Pressed(GameKey.M)) Menu();
        }

        /// <summary>Texto de ayuda del panel final.</summary>
        public static string EndHint(string chapterId, bool completed)
        {
            string retry = ControlPrompts.Key(Act.Retry), menu = ControlPrompts.Key(Act.Menu);
            if (!completed)
            {
                return CheckpointStore.ExistsFor(chapterId)
                    ? ControlPrompts.Key(Act.Checkpoint) + " seguir desde el punto de control   " + retry + " desde el principio   " + menu + " menú"
                    : retry + " intentarlo otra vez   " + menu + " menú";
            }
            return CampaignCatalog.Next(chapterId) != null
                ? ControlPrompts.Key(Act.Next) + " siguiente capítulo   " + retry + " volver a jugar   " + menu + " menú"
                : retry + " volver a jugar   " + menu + " menú";
        }
    }
}
