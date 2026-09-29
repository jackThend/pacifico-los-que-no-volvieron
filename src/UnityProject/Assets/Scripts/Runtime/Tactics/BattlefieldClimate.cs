using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Clima del campo de batalla (ROADMAP 4.3): el calor que hace sudar a las tropas. En Tacna, la mañana del 26 de
    /// mayo de 1880 empezó con camanchaca (neblina) y el sol del desierto apretó al levantarse: el calor sube de
    /// <see cref="MorningHeat"/> a pleno sol en <see cref="clearingSeconds"/> de juego, y la niebla se disipa a la vez.
    /// </summary>
    public sealed class BattlefieldClimate : MonoBehaviour
    {
        [Tooltip("Calor relativo con camanchaca (1 = sol del mediodía en el desierto).")]
        [SerializeField, Range(0f, 1.5f)] private float morningHeat = 0.45f;
        [SerializeField, Range(0f, 1.5f)] private float noonHeat = 1f;
        [Tooltip("Segundos de juego hasta que se levanta la niebla.")]
        [SerializeField] private float clearingSeconds = 300f;
        [Tooltip("Horas de fisiología por hora de juego (compresión del tiempo de la sed).")]
        [SerializeField] private float timeScale = 15f;
        [SerializeField] private bool driveFog = true;
        [SerializeField] private float morningFogDensity = 0.0035f;
        [SerializeField] private float noonFogDensity = 0.0008f;

        private float _elapsed;

        public float MorningHeat => morningHeat;
        public static BattlefieldClimate Active { get; private set; }

        /// <summary>Calor actual, o el del mediodía si la escena no tiene clima.</summary>
        public static float Heat => Active != null ? Active.CurrentHeat : 1f;

        public static float TimeScale => Active != null ? Active.timeScale : 15f;

        public float CurrentHeat => Mathf.Lerp(morningHeat, noonHeat, Mathf.SmoothStep(0f, 1f, _elapsed / Mathf.Max(1f, clearingSeconds)));

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (!driveFog || !RenderSettings.fog) return;
            float t = Mathf.SmoothStep(0f, 1f, _elapsed / Mathf.Max(1f, clearingSeconds));
            RenderSettings.fogDensity = Mathf.Lerp(morningFogDensity, noonFogDensity, t);
        }
    }
}
