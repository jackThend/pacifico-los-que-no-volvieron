using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Cámara en tercera persona sobre el buque (GDD §3.2): órbita con botón derecho, zoom con la rueda. Sigue la posición
    /// sin heredar la escora del casco para no marear al jugador. Con un <see cref="LockTarget"/> mira hacia el enemigo
    /// aunque el buque le dé el costado (la batería dispara por el través: mirando a proa no se le vería); Tab cambia a la
    /// vista por la popa. Mayús: catalejo.
    /// </summary>
    public sealed class ShipCameraRig : MonoBehaviour
    {
        /// <summary>El blanco queda a un lado del centro para que la arboladura propia no lo tape (°).</summary>
        private const float LockSideAngleDeg = 14f;
        private const float YawSharpness = 3f;
        private const float NormalFov = 60f;
        private const float SpyglassFov = 14f;

        [SerializeField] private Transform target;
        [SerializeField] private float distance = 90f;
        [SerializeField] private Vector2 distanceRange = new Vector2(25f, 400f);
        [SerializeField] private float height = 18f;
        [SerializeField] private float orbitSensitivity = 0.15f;
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private Vector2 pitchRange = new Vector2(5f, 60f);

        private Camera _camera;
        private bool _lockEnabled = true;
        private bool _yawReady;
        private float _yaw;
        private float _yawOffset;
        private float _pitch = 15f;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Hacia dónde mira la cámara en vez de hacia la proa (null: vista por la popa).</summary>
        public Transform LockTarget { get; set; }

        private void Awake() => _camera = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null) return;

            if (GameInput.Pressed(GameKey.Tab))
            {
                _lockEnabled = !_lockEnabled;
                _yawOffset = 0f;
            }
            if (GameInput.MouseHeld(1))
            {
                Vector2 delta = GameInput.MouseDelta * orbitSensitivity;
                _yawOffset += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, pitchRange.x, pitchRange.y);
            }
            distance = Mathf.Clamp(distance * (1f - GameInput.ScrollDelta * 0.1f), distanceRange.x, distanceRange.y);

            float wanted = target.eulerAngles.y;
            if (_lockEnabled && LockTarget != null)
            {
                Vector3 to = LockTarget.position - target.position;
                if (new Vector2(to.x, to.z).sqrMagnitude > 1f) wanted = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - LockSideAngleDeg;
            }
            _yaw = _yawReady ? Mathf.LerpAngle(_yaw, wanted, 1f - Mathf.Exp(-YawSharpness * Time.deltaTime)) : wanted;
            _yawReady = true;

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw + _yawOffset, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 desired = focus - orbit * Vector3.forward * distance;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);

            if (_camera != null)
            {
                float fov = GameInput.Held(GameKey.LeftShift) ? SpyglassFov : NormalFov;
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, fov, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }
    }
}
