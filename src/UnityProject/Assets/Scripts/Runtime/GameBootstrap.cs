using Pacifico.Core;
using UnityEngine;

namespace Pacifico.Runtime
{
    /// <summary>
    /// Punto de entrada de la escena de arranque. Persiste entre escenas y
    /// expone la modalidad de juego activa.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameMode initialMode = GameMode.Narrative;

        public static GameBootstrap Instance { get; private set; }
        public GameMode CurrentMode { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentMode = initialMode;
            Debug.Log($"[{ProjectInfo.Title}] v{ProjectInfo.Version} iniciado en modo {CurrentMode}.");
        }

        public void SetMode(GameMode mode)
        {
            CurrentMode = mode;
        }
    }
}
