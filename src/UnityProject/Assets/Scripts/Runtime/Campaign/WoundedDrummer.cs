using System;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// El tambor chileno herido de muerte contra una pared de barro (capítulo 4, «momento humano»). Se le encuentra al
    /// acercarse; con E, a su lado, el jugador le da su caramayola. Después se desploma despacio.
    /// </summary>
    public sealed class WoundedDrummer : MonoBehaviour
    {
        [SerializeField] private Transform figure;
        [SerializeField] private float noticeRadius = 10f;
        [SerializeField] private float reach = 2.5f;

        private Transform _player;
        private float _slump;
        private GUIStyle _style;

        public bool Active { get; set; }
        public bool Found { get; private set; }
        public bool Watered { get; private set; }
        public event Action<WoundedDrummer> OnFound;
        public event Action<WoundedDrummer> OnWatered;

        public void Configure(Transform figureTransform, Transform player)
        {
            figure = figureTransform;
            _player = player;
        }

        private void Update()
        {
            if (Watered && figure != null && _slump < 1f)
            {
                _slump = Mathf.Min(1f, _slump + Time.deltaTime / 6f);
                figure.localRotation = Quaternion.Euler(0f, 0f, Mathf.SmoothStep(0f, 70f, _slump));
            }
            if (!Active || _player == null || Watered) return;
            float d = Vector3.Distance(_player.position, transform.position);
            if (!Found && d <= noticeRadius)
            {
                Found = true;
                OnFound?.Invoke(this);
            }
            if (Found && d <= reach && GameInput.Pressed(GameKey.E))
            {
                Watered = true;
                OnWatered?.Invoke(this);
            }
        }

        private void OnGUI()
        {
            if (!Active || !Found || Watered || _player == null) return;
            if (Vector3.Distance(_player.position, transform.position) > reach) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.95f, 0.8f);
            }
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f + 40f, 400f, 24f), "[E] Darle tu caramayola", _style);
        }
    }

    /// <summary>Un jinete que recorre un camino una vez (Cáceres pasando a caballo al empezar el capítulo 4).</summary>
    public sealed class ScriptedRider : MonoBehaviour
    {
        [SerializeField] private Vector3[] waypoints = new Vector3[0];
        [SerializeField] private float speed = 5f;

        private int _index;

        public void Configure(Vector3[] path, float metersPerSecond)
        {
            waypoints = path;
            speed = metersPerSecond;
        }

        private void Update()
        {
            if (_index >= waypoints.Length) return;
            Vector3 target = waypoints[_index];
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.5f)
            {
                _index++;
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 180f * Time.deltaTime);
            Vector3 step = to.normalized * Mathf.Min(to.magnitude, speed * Time.deltaTime);
            Vector3 p = transform.position + step;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.position = p;
        }
    }
}
