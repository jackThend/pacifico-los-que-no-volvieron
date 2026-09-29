using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Effects
{
    /// <summary>
    /// Banco de pruebas del humo en el editor (ROADMAP 3.3 y 0.3): con <c>V</c> dispara una descarga de una línea
    /// de tiradores invisibles a ambos lados del jugador, y en pantalla muestra la claridad del centro de la vista
    /// (la métrica de la prueba de estrés) y cuántas bocanadas hay vivas. Sirve para juzgar a ojo lo que los tests
    /// miden: que el humo se vea, que no ciegue y que no se acumule.
    /// </summary>
    public sealed class SmokeVolleyTest : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField, Range(1, 40)] private int riflemen = 20;
        [SerializeField] private float spacingM = 0.9f;
        [Tooltip("Carga de pólvora de cada fusil (Comblain: 4,9 g).")]
        [SerializeField] private float powderChargeG = 4.9f;
        [Tooltip("Dispersión temporal de la descarga: a la voz de «¡fuego!» no todos disparan en el mismo instante.")]
        [SerializeField] private float spreadSeconds = 0.25f;
        [SerializeField] private bool showOverlay = true;

        private float[] _pending;
        private Vector3 _lineOrigin;
        private Vector3 _lineRight;
        private Vector3 _lineForward;
        private float _worstClarity = 1f;
        private float _clarity = 1f;
        private GUIStyle _style;

        private void Update()
        {
            BlackPowderSmoke smoke = BlackPowderSmoke.Active;
            Transform view = eye != null ? eye : (Camera.main != null ? Camera.main.transform : null);
            if (smoke == null || view == null) return;

            if (GameInput.Pressed(GameKey.V)) StartVolley(view);
            FirePending(smoke);

            _clarity = smoke.ViewClarity(view);
            _worstClarity = Mathf.Min(_worstClarity, _clarity);
        }

        private void StartVolley(Transform view)
        {
            // La línea se fija al dar la orden: si el jugador se gira después, el humo sigue donde se disparó.
            _lineForward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (_lineForward.sqrMagnitude < 0.5f) _lineForward = Vector3.forward;
            _lineRight = Vector3.Cross(Vector3.up, _lineForward);
            _lineOrigin = view.position + _lineForward * 1.23f - Vector3.up * 0.165f;

            _pending = new float[riflemen];
            for (int i = 0; i < riflemen; i++) _pending[i] = Random.Range(0f, spreadSeconds);
            _worstClarity = 1f;
        }

        private void FirePending(BlackPowderSmoke smoke)
        {
            if (_pending == null) return;
            bool any = false;
            for (int i = 0; i < _pending.Length; i++)
            {
                if (_pending[i] < 0f) continue;
                _pending[i] -= Time.deltaTime;
                if (_pending[i] >= 0f)
                {
                    any = true;
                    continue;
                }
                _pending[i] = -1f;
                // Tiradores alternos a derecha e izquierda del jugador: ±1, ±2, …
                int slot = i / 2 + 1;
                float side = (i & 1) == 0 ? 1f : -1f;
                Vector3 muzzle = _lineOrigin + _lineRight * (side * slot * spacingM);
                smoke.EmitShot(muzzle, _lineForward, powderChargeG, Vector3.zero);
            }
            if (!any) _pending = null;
        }

        private void OnGUI()
        {
            BlackPowderSmoke smoke = BlackPowderSmoke.Active;
            if (!showOverlay || smoke == null || smoke.Model == null) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            string text = "Humo — claridad de la vista " + (_clarity * 100f).ToString("0") + " % (mín. " +
                          (_worstClarity * 100f).ToString("0") + " %) · bocanadas " + smoke.Model.Count + "/" +
                          smoke.Model.Settings.MaxPuffs + " · [V] descarga de " + riflemen + " fusiles";
            // Arriba a la izquierda: abajo están los indicadores del HUD de infantería.
            GUI.Label(new Rect(12f, 12f, 900f, 24f), text, _style);
        }
    }
}
