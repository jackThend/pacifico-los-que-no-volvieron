using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Bombardeo de ambientación (capítulo 8: los Krupp chilenos y las fragatas desde el mar sobre Chorrillos y
    /// Miraflores): granadas que levantan tierra en una zona, sin daño, para el estruendo y el caos del avance.
    /// </summary>
    public sealed class AmbientBombardment : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(200f, 0f, 120f);
        [SerializeField] private Vector2 intervalSeconds = new Vector2(1.2f, 3.5f);
        [SerializeField] private float burstSize = 9f;
        [SerializeField] private Material dust;

        private float _next;

        public bool Firing { get; set; } = true;

        public void Configure(Vector3 area, Material dustMaterial)
        {
            size = area;
            dust = dustMaterial;
        }

        private void Update()
        {
            if (!Firing || Time.time < _next) return;
            _next = Time.time + Random.Range(intervalSeconds.x, intervalSeconds.y);
            Vector3 p = transform.position + new Vector3(Random.Range(-0.5f, 0.5f) * size.x, 0f, Random.Range(-0.5f, 0.5f) * size.z);
            if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            DustBurst.Spawn(p, dust, burstSize * Random.Range(0.7f, 1.3f));
        }
    }
}
