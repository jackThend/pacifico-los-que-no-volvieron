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
            if (GameInput.Pressed(GameKey.M)) Menu();
        }

        /// <summary>Texto de ayuda del panel final.</summary>
        public static string EndHint(string chapterId, bool completed)
        {
            if (!completed) return "[R] reintentar · [M] menú";
            return CampaignCatalog.Next(chapterId) != null ? "[N] siguiente capítulo · [R] volver a jugar · [M] menú" : "[R] volver a jugar · [M] menú";
        }
    }
}
