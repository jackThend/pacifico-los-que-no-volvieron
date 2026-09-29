using System;
using Pacifico.Core.Campaign;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Un grupo de náufragos agarrados a restos de la Esmeralda (capítulo 1, cierre). Flotan con la marejada; si el
    /// buque de rescate se queda cerca y casi parado, se arría un bote y suben a bordo (<see cref="SurvivorRescue"/>).
    /// </summary>
    public sealed class SurvivorGroup : MonoBehaviour
    {
        [SerializeField] private int men = 4;
        [SerializeField] private float bobAmplitude = 0.25f;
        [SerializeField] private float bobPeriod = 4f;
        [SerializeField] private float labelRange = 700f;

        private readonly SurvivorRescue _rescue = new SurvivorRescue();
        private ShipController _rescuer;
        private Camera _camera;
        private float _baseY;
        private float _phase;
        private float _vanish;
        private GUIStyle _style;

        public int Men
        {
            get => men;
            set => men = Mathf.Max(1, value);
        }

        public SurvivorRescue Rescue => _rescue;

        /// <summary>Se emite una vez, al subir el grupo a bordo.</summary>
        public event Action<SurvivorGroup> Rescued;

        public void Configure(ShipController rescuer, int count)
        {
            _rescuer = rescuer;
            Men = count;
        }

        private void Start()
        {
            _camera = Camera.main;
            _baseY = transform.position.y;
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.11f) % 6.283f;
        }

        /// <summary>Distancia (m) de la borda del buque a los náufragos: al segmento de crujía menos media manga.</summary>
        public static float DistanceToHull(ShipController ship, Vector3 point)
        {
            Transform t = ship.transform;
            Vector3 local = t.InverseTransformPoint(point);
            float half = ship.Spec != null ? ship.Spec.LengthM * 0.5f : 30f;
            float beam = ship.Spec != null ? ship.Spec.BeamM * 0.5f : 5f;
            var closest = new Vector2(0f, Mathf.Clamp(local.z, -half, half));
            return Mathf.Max(0f, Vector2.Distance(new Vector2(local.x, local.z), closest) - beam);
        }

        private void Update()
        {
            Vector3 p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * 2f * Mathf.PI / bobPeriod + _phase) * bobAmplitude;
            transform.position = p;

            if (_rescue.Rescued)
            {
                // Suben al bote: el grupo se desvanece en un segundo y medio.
                _vanish += Time.deltaTime / 1.5f;
                transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - _vanish);
                if (_vanish >= 1f) gameObject.SetActive(false);
                return;
            }
            if (_rescuer == null || _rescuer.Motion == null) return;
            float distance = DistanceToHull(_rescuer, transform.position);
            if (_rescue.Step(Time.deltaTime, distance, _rescuer.Motion.Speed)) Rescued?.Invoke(this);
        }

        private void OnGUI()
        {
            if (_rescue.Rescued || _rescuer == null || !_rescuer.PlayerControlled || _rescuer.Motion == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector3 screen = _camera.WorldToScreenPoint(transform.position + Vector3.up * 4f);
            float distance = DistanceToHull(_rescuer, transform.position);
            if (screen.z <= 0f || distance > labelRange) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }

            string blocker = _rescue.Blocker(distance, _rescuer.Motion.Speed);
            string text = men + " náufragos · " + distance.ToString("0") + " m" +
                          (blocker != null ? "\n" + blocker : "\nArriando bote… " + (_rescue.Progress * 100f).ToString("0") + "%");
            var rect = new Rect(screen.x - 120f, Screen.height - screen.y - 24f, 240f, 40f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _style);
            GUI.color = blocker == null ? new Color(0.6f, 1f, 0.7f) : Color.white;
            GUI.Label(rect, text, _style);
            if (_rescue.Progress > 0f)
            {
                GUI.color = new Color(0.5f, 1f, 0.6f, 0.9f);
                GUI.DrawTexture(new Rect(rect.x + 60f, rect.yMax + 2f, 120f * _rescue.Progress, 4f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
