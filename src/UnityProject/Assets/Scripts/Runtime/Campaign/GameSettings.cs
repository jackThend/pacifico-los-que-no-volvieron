using Pacifico.Input;
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
        private const string VibrationKey = "pacifico.ajustes.vibracion";
        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;

        private static bool s_loaded;
        private static float s_volume = 1f;
        private static float s_sensitivity = 1f;
        private static bool s_vibration = true;
        private static GUIStyle s_label;
        private static GUIStyle s_button;

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

        /// <summary>El mando vibra al disparar y al recibir daño.</summary>
        public static bool Vibration
        {
            get { Load(); return s_vibration; }
            set
            {
                Load();
                s_vibration = value;
                PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
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
            s_vibration = PlayerPrefs.GetInt(VibrationKey, 1) != 0;
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
            if (s_button == null) s_button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            float labelWidth = Mathf.Min(220f, rect.width * 0.45f);
            float sliderX = rect.x + labelWidth + 10f;
            float sliderWidth = rect.width - labelWidth - 10f;

            GUI.Label(new Rect(rect.x, rect.y, labelWidth, 24f), "Volumen: " + Mathf.RoundToInt(Volume * 100f) + " %", s_label);
            float volume = PadMenu.Slider(new Rect(sliderX, rect.y + 8f, sliderWidth, 16f), Volume, 0f, 1f, 0.1f);
            if (!Mathf.Approximately(volume, Volume)) { Volume = volume; if (GameInput.UsingGamepad) PlayerPrefs.Save(); }

            float y = rect.y + 32f;
            GUI.Label(new Rect(rect.x, y, labelWidth, 24f), "Sensibilidad de la cámara: " + MouseSensitivity.ToString("0.0") + "×", s_label);
            float sensitivity = PadMenu.Slider(new Rect(sliderX, y + 8f, sliderWidth, 16f), MouseSensitivity, MinSensitivity, MaxSensitivity, 0.25f);
            if (!Mathf.Approximately(sensitivity, MouseSensitivity)) { MouseSensitivity = sensitivity; if (GameInput.UsingGamepad) PlayerPrefs.Save(); }

            if (Event.current.type == EventType.MouseUp) PlayerPrefs.Save();

            // La vibración solo se ofrece con un mando conectado.
            if (!GameInput.PadConnected) return 60f;
            y += 34f;
            if (PadMenu.Button(new Rect(rect.x, y, rect.width, 30f), "Vibración del mando: " + (Vibration ? "sí" : "no"), s_button)) Vibration = !Vibration;
            return 96f;
        }
    }
}
