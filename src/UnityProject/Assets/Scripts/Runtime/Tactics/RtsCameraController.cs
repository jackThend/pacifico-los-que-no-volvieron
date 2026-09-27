using Pacifico.Core.Common;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Cámara táctica (ROADMAP 4.1): WASD o flechas para desplazarse, Q/E para girar, rueda para la altura. Se
    /// inclina más cuanto más alta está (de 35° a ras de duna a 65° en vista de mapa) y nunca atraviesa el terreno.
    /// Todo con suavizado críticamente amortiguado, igual a cualquier tasa de fotogramas.
    /// </summary>
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private float minHeight = 14f;
        [SerializeField] private float maxHeight = 140f;
        [SerializeField] private float panSpeedAtMinHeight = 18f;
        [SerializeField] private float panSpeedAtMaxHeight = 120f;
        [SerializeField] private float rotateSpeedDeg = 90f;
        [SerializeField] private float zoomStep = 0.12f;
        [SerializeField] private Vector2 pitchRange = new Vector2(35f, 65f);
        [SerializeField] private Vector2 boundsMin = new Vector2(-250f, -250f);
        [SerializeField] private Vector2 boundsMax = new Vector2(250f, 250f);
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        private Vector3 _focus;
        private float _yaw;
        private float _height01 = 0.35f;
        private float _smoothedHeight01;
        private float _heightVelocity;
        private Vector3 _smoothedFocus;
        private Vector3 _focusVelocity;

        private void Start()
        {
            _yaw = transform.eulerAngles.y;
            // Foco inicial: el punto del suelo que la cámara mira.
            _focus = Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2000f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 60f;
            _smoothedFocus = _focus;
            _smoothedHeight01 = _height01;
            Apply();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            float panSpeed = Mathf.Lerp(panSpeedAtMinHeight, panSpeedAtMaxHeight, _smoothedHeight01);
            float x = GameInput.Axis(GameKey.A, GameKey.D);
            float z = GameInput.Axis(GameKey.S, GameKey.W);
            Quaternion yaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 pan = yaw * new Vector3(x, 0f, z);
            if (pan.sqrMagnitude > 1f) pan.Normalize();
            _focus += pan * (panSpeed * dt);
            _focus.x = Mathf.Clamp(_focus.x, boundsMin.x, boundsMax.x);
            _focus.z = Mathf.Clamp(_focus.z, boundsMin.y, boundsMax.y);

            _yaw += GameInput.Axis(GameKey.Q, GameKey.E) * rotateSpeedDeg * dt;
            float scroll = GameInput.ScrollDelta;
            if (Mathf.Abs(scroll) > 0.01f) _height01 = Mathf.Clamp01(_height01 - scroll * zoomStep);

            _smoothedHeight01 = MathUtil.SmoothDamp(_smoothedHeight01, _height01, ref _heightVelocity, 0.15f, dt);
            _smoothedFocus.x = MathUtil.SmoothDamp(_smoothedFocus.x, _focus.x, ref _focusVelocity.x, 0.08f, dt);
            _smoothedFocus.z = MathUtil.SmoothDamp(_smoothedFocus.z, _focus.z, ref _focusVelocity.z, 0.08f, dt);
            Apply();
        }

        private void Apply()
        {
            float height = Mathf.Lerp(minHeight, maxHeight, _smoothedHeight01);
            float pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, _smoothedHeight01);
            float ground = GroundHeight(_smoothedFocus);
            Vector3 focus = new Vector3(_smoothedFocus.x, ground, _smoothedFocus.z);
            Quaternion rotation = Quaternion.Euler(pitch, _yaw, 0f);
            // La cámara queda a «height» metros sobre el foco, a lo largo del eje de la vista.
            float distance = height / Mathf.Sin(pitch * Mathf.Deg2Rad);
            Vector3 position = focus - rotation * Vector3.forward * distance;
            // Sin atravesar dunas entre la cámara y el foco.
            float below = GroundHeight(position) + 4f;
            if (position.y < below) position.y = below;
            transform.SetPositionAndRotation(position, rotation);
        }

        private float GroundHeight(Vector3 at)
        {
            var origin = new Vector3(at.x, 1000f, at.z);
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f, groundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
        }
    }
}
