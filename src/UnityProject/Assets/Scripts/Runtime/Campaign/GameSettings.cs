using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Ajustes del jugador que se guardan entre partidas (PlayerPrefs): volumen general y sensibilidad del ratón. Se
    /// cambian en el menú principal y en la pausa (<see cref="DrawSliders"/>). La sensibilidad multiplica la de cada
    /// cámara (primera persona y cámara de los buques).
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "pacifico.ajustes.volumen";
        private const string SensitivityKey = "pacifico.ajustes.raton";
        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;

        private static bool s_loaded;
        private static float s_volume = 1f;
        private static float s_sensitivity = 1f;
        private static GUIStyle s_label;

        public static float Volume
        {
            get { Load(); return s_volume; }
            set
            {
                Load();
                s_volume = Mathf.Clamp01(value);
                AudioListener.volume = s_volume;
                PlayerPrefs.SetFloat(VolumeKey, s_volume);
            }
        }

        public static float MouseSensitivity
        {
            get { Load(); return s_sensitivity; }
            set
            {
                Load();
                s_sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, s_sensitivity);
            }
        }

        /// <summary>El volumen guardado vale desde la primera escena.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            s_loaded = false;
            Load();
        }

        private static void Load()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            s_sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
            AudioListener.volume = s_volume;
        }

        /// <summary>Dos deslizadores (volumen y ratón) en <paramref name="rect"/>; guarda al soltar. Devuelve la altura usada.</summary>
        public static float DrawSliders(Rect rect)
        {
            if (s_label == null)
            {
                s_label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
                s_label.normal.textColor = Color.white;
            }
            float labelWidth = Mathf.Min(220f, rect.width * 0.45f);
            float sliderX = rect.x + labelWidth + 10f;
            float sliderWidth = rect.width - labelWidth - 10f;

            GUI.Label(new Rect(rect.x, rect.y, labelWidth, 24f), "Volumen: " + Mathf.RoundToInt(Volume * 100f) + " %", s_label);
            float volume = GUI.HorizontalSlider(new Rect(sliderX, rect.y + 8f, sliderWidth, 16f), Volume, 0f, 1f);
            if (!Mathf.Approximately(volume, Volume)) Volume = volume;

            float y = rect.y + 32f;
            GUI.Label(new Rect(rect.x, y, labelWidth, 24f), "Sensibilidad del ratón: " + MouseSensitivity.ToString("0.0") + "×", s_label);
            float sensitivity = GUI.HorizontalSlider(new Rect(sliderX, y + 8f, sliderWidth, 16f), MouseSensitivity, MinSensitivity, MaxSensitivity);
            if (!Mathf.Approximately(sensitivity, MouseSensitivity)) MouseSensitivity = sensitivity;

            if (Event.current.type == EventType.MouseUp) PlayerPrefs.Save();
            return 60f;
        }
    }
}
