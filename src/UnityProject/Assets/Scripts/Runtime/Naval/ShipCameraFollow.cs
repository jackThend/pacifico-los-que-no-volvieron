using System;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>Cámara en tercera persona que sigue al buque por la popa (GDD §3.2).</summary>
    public sealed class ShipCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float distance = 120f;
        [SerializeField, Min(0f)] private float height = 45f;
        [SerializeField, Min(0f)] private float lookAtHeight = 5f;
        [SerializeField, Min(0.01f), Tooltip("Mayor = sigue al buque con menos retraso.")] private float sharpness = 3f;

        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = target.position - target.forward * distance + Vector3.up * height;
            var blend = 1f - (float)Math.Exp(-sharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}
