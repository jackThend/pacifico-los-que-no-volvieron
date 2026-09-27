using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Cámara en tercera persona sobre el buque (GDD §3.2): órbita con botón derecho, zoom con la rueda.
    /// Sigue la posición y el rumbo sin heredar la escora del casco para no marear al jugador.
    /// </summary>
    public sealed class ShipCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 90f;
        [SerializeField] private Vector2 distanceRange = new Vector2(25f, 400f);
        [SerializeField] private float height = 18f;
        [SerializeField] private float orbitSensitivity = 0.15f;
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private Vector2 pitchRange = new Vector2(5f, 60f);

        private float _yawOffset;
        private float _pitch = 15f;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (GameInput.MouseHeld(1))
            {
                Vector2 delta = GameInput.MouseDelta * orbitSensitivity;
                _yawOffset += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, pitchRange.x, pitchRange.y);
            }
            distance = Mathf.Clamp(distance * (1f - GameInput.ScrollDelta * 0.1f), distanceRange.x, distanceRange.y);

            Quaternion orbit = Quaternion.Euler(_pitch, target.eulerAngles.y + _yawOffset, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 desired = focus - orbit * Vector3.forward * distance;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }
    }
}
