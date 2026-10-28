using System;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>Un jinete que recorre un camino una vez (Cáceres pasando a caballo al empezar el capítulo 4).</summary>
    public sealed class ScriptedRider : MonoBehaviour
    {
        [SerializeField] private Vector3[] waypoints = new Vector3[0];
        [SerializeField] private float speed = 5f;
        [Tooltip("Al acabar el camino sigue al galope y, si no hay suelo, cae (Ugarte en el Morro de Arica).")]
        [SerializeField] private bool rideOffEdge;
        [SerializeField] private bool startOnAwake = true;

        private int _index;
        private float _fallSpeed;
        private float _fallTime;

        /// <summary>Echa a andar (si se creó parado).</summary>
        public bool Riding { get; set; }

        public void Configure(Vector3[] path, float metersPerSecond, bool offEdge = false, bool startNow = true)
        {
            waypoints = path;
            speed = metersPerSecond;
            rideOffEdge = offEdge;
            startOnAwake = startNow;
        }

        private void Awake() => Riding = startOnAwake;

        private void Update()
        {
            if (!Riding) return;
            if (_index >= waypoints.Length)
            {
                if (rideOffEdge) Leap();
                return;
            }
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

        /// <summary>Sigue al frente: sobre el suelo, al galope; sin suelo, caída libre (y desaparece al cabo de un rato).</summary>
        private void Leap()
        {
            float dt = Time.deltaTime;
            Vector3 p = transform.position + transform.forward * speed * dt;
            bool ground = Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (ground && _fallSpeed <= 0f) p.y = hit.point.y;
            else
            {
                _fallSpeed += 9.81f * dt;
                _fallTime += dt;
                p.y -= _fallSpeed * dt;
                if (_fallTime > 8f) gameObject.SetActive(false);
            }
            transform.position = p;
        }
    }
}
