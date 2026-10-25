using UnityEngine;

namespace Pacifico.Effects
{
    /// <summary>
    /// Recalcula al cargar la escena la luz ambiente y los reflejos a partir del cielo (HDRI de Poly Haven): las escenas
    /// se construyen por código y no tienen iluminación horneada, así que sin esto el ambiente quedaría con el valor
    /// por defecto en lugar del color del cielo de cada capítulo.
    /// </summary>
    public sealed class EnvironmentRefresh : MonoBehaviour
    {
        private void Start() => DynamicGI.UpdateEnvironment();
    }
}
