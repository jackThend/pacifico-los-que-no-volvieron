using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Arma en primera persona (ROADMAP 3.1): interpola entre la posición de cadera y la de encare (miras alineadas
    /// con el centro de la cámara), la baja al correr, la inclina al deslizarse y la retrasa al girar (inercia del
    /// fusil de 4 kg).
    /// <para>
    /// Alza de escalera: para tirar a más distancia la corredera sube <c>R·tan θ</c> (R = radio de mira, distancia
    /// entre alza y punto de mira). Al alinear la corredera con el punto de mira, el fusil queda inclinado θ hacia
    /// arriba: exactamente lo que se ve al encarar con el alza graduada a 1.000 m.
    /// </para>
    /// </summary>
    public sealed class IronSightViewModel : MonoBehaviour
    {
        [SerializeField] private FirstPersonController owner;

        [Header("Poses (locales al pivote de cámara)")]
        [SerializeField] private Vector3 hipPosition = new Vector3(0.22f, -0.2f, 0.45f);
        [SerializeField] private Vector3 hipEuler = new Vector3(0f, -2f, 0f);
        [Tooltip("Posición con las miras alineadas en el centro de la pantalla.")]
        [SerializeField] private Vector3 aimPosition = new Vector3(0f, -0.075f, 0.34f);
        [SerializeField] private Vector3 sprintPosition = new Vector3(0.28f, -0.3f, 0.35f);
        [SerializeField] private Vector3 sprintEuler = new Vector3(18f, -35f, 10f);
        [SerializeField] private Vector3 slideEuler = new Vector3(0f, 0f, 22f);

        [Header("Alza")]
        [Tooltip("Corredera del alza: sube R·tan(θ) según la graduación.")]
        [SerializeField] private Transform rearSightLeaf;
        [Tooltip("Radio de mira (m): distancia entre el alza y el punto de mira.")]
        [SerializeField] private float sightRadiusM = 0.7f;

        [Header("Inercia")]
        [Tooltip("Metros de retraso por cada °/s de giro de la cámara.")]
        [SerializeField] private float lagPerDegreePerSecond = 0.00003f;
        [SerializeField] private float maxSway = 0.03f;

        private float _sprintBlend;
        private float _sprintVelocity;
        private float _slideBlend;
        private float _slideVelocity;
        private Vector3 _lag;
        private Quaternion _lastCameraRotation;
        private Vector3 _leafBasePosition;

        public void Configure(FirstPersonController controller, Transform leaf)
        {
            owner = controller;
            rearSightLeaf = leaf;
        }

        private void Start()
        {
            _lastCameraRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            if (rearSightLeaf != null) _leafBasePosition = rearSightLeaf.localPosition;
        }

        private void LateUpdate()
        {
            if (owner == null || owner.Motor == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float aim = owner.Sights != null ? owner.Sights.Eased : 0f;
            _sprintBlend = MathUtil.SmoothDamp(_sprintBlend, owner.Motor.IsSprinting ? 1f : 0f, ref _sprintVelocity, 0.12f, dt);
            _slideBlend = MathUtil.SmoothDamp(_slideBlend, owner.Motor.Stance == Stance.Sliding ? 1f : 0f, ref _slideVelocity, 0.1f, dt);

            // Retraso del arma respecto al giro de la cámara: el fusil «pesa».
            if (transform.parent != null)
            {
                Quaternion delta = Quaternion.Inverse(_lastCameraRotation) * transform.parent.rotation;
                _lastCameraRotation = transform.parent.rotation;
                Vector3 euler = delta.eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, euler.y);
                float pitch = Mathf.DeltaAngle(0f, euler.x);
                // Velocidad angular (°/s), no grados por fotograma: el retraso es el mismo a 30 que a 144 FPS.
                Vector3 target = new Vector3(-yaw, pitch, 0f) / dt * lagPerDegreePerSecond * (1f - 0.7f * aim);
                target = Vector3.ClampMagnitude(target, maxSway);
                _lag = Vector3.Lerp(_lag, target, 1f - Mathf.Exp(-12f * dt));
            }

            Vector3 position = Vector3.Lerp(hipPosition, aimPosition, aim);
            position = Vector3.Lerp(position, sprintPosition, _sprintBlend) + _lag;
            float elevation = owner.Ladder != null ? owner.Ladder.ElevationDeg : 0f;
            Quaternion aimRotation = Quaternion.Euler(-elevation, 0f, 0f); // boca arriba θ al alinear alza y punto de mira
            Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(hipEuler), aimRotation, aim);
            rotation = Quaternion.Slerp(rotation, Quaternion.Euler(sprintEuler), _sprintBlend);
            rotation = Quaternion.Slerp(rotation, Quaternion.Euler(slideEuler), _slideBlend);
            transform.localPosition = position;
            transform.localRotation = rotation;

            if (rearSightLeaf != null)
            {
                rearSightLeaf.localPosition = _leafBasePosition + Vector3.up * (sightRadiusM * Mathf.Tan(elevation * Mathf.Deg2Rad));
            }
        }
    }
}
