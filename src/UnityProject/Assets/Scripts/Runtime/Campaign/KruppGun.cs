using System;
using Pacifico.Core.Campaign;
using Pacifico.Infantry;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Cañón Krupp de montaña en la pampa superior (capítulo 4). Mientras está en manos chilenas cañonea la quebrada:
    /// cada disparo levanta tierra en un punto de la zona batida y la metralla hiere en unos metros a la redonda. Se
    /// toma quedándose junto a la pieza sin chilenos en pie cerca (<see cref="CapturePoint"/>).
    /// </summary>
    public sealed class KruppGun : MonoBehaviour
    {
        [SerializeField] private Transform barrel;
        [SerializeField] private Vector3 targetCenter;
        [SerializeField] private float targetRadius = 40f;
        [SerializeField] private float fireIntervalSeconds = 11f;
        [SerializeField] private float captureRadius = 7f;
        [SerializeField] private float contestRadius = 16f;
        [Tooltip("Radio de la metralla (m) y daño en el centro (cae linealmente con la distancia).")]
        [SerializeField] private float blastRadius = 5f;
        [SerializeField] private float blastDamage = 45f;
        [SerializeField] private Material dust;

        private readonly CapturePoint _capture = new CapturePoint(4f);
        private System.Random _random;
        private float _nextShot;
        private float _recoil;
        private GUIStyle _style;

        public bool Firing { get; set; }
        public bool Capturable { get; set; }
        public CapturePoint Capture => _capture;
        public event Action<KruppGun> Captured;

        public void Configure(Transform barrelTransform, Vector3 zoneCenter, float zoneRadius, Material dustMaterial)
        {
            barrel = barrelTransform;
            targetCenter = zoneCenter;
            targetRadius = zoneRadius;
            dust = dustMaterial;
        }

        private void Awake()
        {
            _random = new System.Random(GetInstanceID());
            _nextShot = Time.time + fireIntervalSeconds * (0.5f + (float)_random.NextDouble());
        }

        private void Update()
        {
            if (_capture.Captured) return;
            if (Capturable)
            {
                int friends = 0, enemies = 0;
                foreach (Combatant c in Combatant.All)
                {
                    if (!c.Alive) continue;
                    float d = Vector3.Distance(c.transform.position, transform.position);
                    if (c.Faction == Core.Common.Faction.Chile) { if (d <= contestRadius) enemies++; }
                    else if (d <= captureRadius) friends++;
                }
                if (_capture.Step(Time.deltaTime, friends, enemies))
                {
                    Firing = false;
                    Captured?.Invoke(this);
                }
            }
            if (Firing && Time.time >= _nextShot)
            {
                _nextShot = Time.time + fireIntervalSeconds * (0.8f + 0.4f * (float)_random.NextDouble());
                Shoot();
            }
            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * 2f);
            if (barrel != null) barrel.localPosition = new Vector3(0f, barrel.localPosition.y, -_recoil * 0.4f);
        }

        private void Shoot()
        {
            _recoil = 1f;
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)_random.NextDouble()) * targetRadius;
            Vector3 impact = targetCenter + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            if (Physics.Raycast(impact + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                impact = hit.point;
            }
            DustBurst.Spawn(impact, dust, blastRadius * 1.2f);
            foreach (Combatant c in Combatant.All)
            {
                if (!c.Alive || c.Faction == Core.Common.Faction.Chile) continue;
                float d = Vector3.Distance(c.transform.position, impact);
                if (d < blastRadius) c.Hurt(blastDamage * (1f - d / blastRadius), gameObject, impact);
            }
        }

        private void OnGUI()
        {
            if (!Capturable || _capture.Captured || _capture.Progress <= 0f) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
            if (screen.z <= 0f) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 24f, 200f, 20f), "Tomando la pieza… " + (_capture.Progress * 100f).ToString("0") + "%", _style);
        }
    }

    /// <summary>Tierra levantada por una granada: una esfera que crece y se desvanece.</summary>
    public sealed class DustBurst : MonoBehaviour
    {
        private float _age;
        private float _size;

        public static void Spawn(Vector3 position, Material material, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Polvo_Granada";
            Destroy(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.1f;
            go.AddComponent<DustBurst>()._size = size;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / 1.2f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, _size, 1f - (1f - Mathf.Min(1f, t * 2f)) * (1f - Mathf.Min(1f, t * 2f)));
            transform.position += Vector3.up * Time.deltaTime * 1.5f;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
