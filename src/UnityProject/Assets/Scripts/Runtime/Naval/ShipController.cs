using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Puente entre <see cref="ShipMotionModel"/> y la escena (ROADMAP 2.1). La física de maniobra vive en
    /// Pacifico.Core; aquí solo se leen mandos y se mueve un Rigidbody cinemático, de modo que el buque es
    /// determinista y a la vez dispara triggers (espolón) y recibe raycasts (artillería).
    /// Controles: W/S telégrafo, A/D timón.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ShipController : MonoBehaviour
    {
        [SerializeField] private ShipDataSO data;
        [Tooltip("Si está activo, lee W/S (telégrafo) y A/D (timón) del teclado.")]
        [SerializeField] private bool playerControlled = true;
        [Tooltip("Devuelve el timón a la vía al soltar A/D (ayuda arcade).")]
        [SerializeField] private bool autoCenterRudder = true;
        [SerializeField] private EngineOrder initialOrder = EngineOrder.Stop;
        [Tooltip("Arrancada inicial como fracción de la velocidad máxima.")]
        [Range(0f, 1f)] [SerializeField] private float initialSpeedFraction;

        [Header("Visual (no afecta a la simulación)")]
        [Tooltip("Malla del casco: escora en los giros y cabecea con el oleaje.")]
        [SerializeField] private Transform hullVisual;
        [SerializeField] private float maxHeelDeg = 6f;
        [SerializeField] private float heelPerTurn = 1.2f;
        [SerializeField] private float swellPitchDeg = 1.2f;
        [SerializeField] private float swellPeriodSeconds = 7f;

        private Rigidbody _body;
        private float _heel;
        private float _heldRudder;
        private int _pendingTelegraphSteps;

        public ShipDataSO Data => data;
        public ShipSpec Spec { get; private set; }
        public ShipMotionModel Motion { get; private set; }
        public bool PlayerControlled
        {
            get => playerControlled;
            set => playerControlled = value;
        }

        /// <summary>Velocidad del buque en el mundo (m/s), útil para la balística y el espolón.</summary>
        public Vector3 Velocity => transform.forward * (Motion != null ? Motion.Speed : 0f);

        /// <summary>Inicializa con datos por código (p. ej. escenas generadas por el editor).</summary>
        public void Initialize(ShipDataSO shipData)
        {
            data = shipData;
            Build();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            if (data != null) Build();
        }

        private void Build()
        {
            Spec = data.ToSpec();
            var validation = Spec.Validate();
            if (!validation.IsValid) Debug.LogWarning(validation.ToString(), this);

            Motion = new ShipMotionModel(Spec);
            Vector3 p = transform.position;
            Motion.SetPose(p.x, p.z, transform.eulerAngles.y);
            Motion.Telegraph.Set(initialOrder);
            Motion.SetSpeed(initialSpeedFraction * Motion.MaxSpeed);
        }

        /// <summary>Órdenes para IA o cinemáticas.</summary>
        public void Command(EngineOrder order, float rudder)
        {
            if (Motion == null) return;
            Motion.Telegraph.Set(order);
            Motion.RudderCommand = Mathf.Clamp(rudder, -1f, 1f);
        }

        private void Update()
        {
            if (Motion == null || !playerControlled) return;

            // Las pulsaciones se acumulan en Update para no perderlas entre pasos de FixedUpdate.
            if (GameInput.Pressed(GameKey.W)) _pendingTelegraphSteps++;
            if (GameInput.Pressed(GameKey.S)) _pendingTelegraphSteps--;

            float axis = GameInput.Axis(GameKey.A, GameKey.D);
            if (axis != 0f || autoCenterRudder) _heldRudder = axis;
            Motion.RudderCommand = _heldRudder;
        }

        private void FixedUpdate()
        {
            if (Motion == null) return;

            while (_pendingTelegraphSteps != 0)
            {
                int step = _pendingTelegraphSteps > 0 ? 1 : -1;
                Motion.Telegraph.Step(step);
                _pendingTelegraphSteps -= step;
            }

            Motion.Step(Time.fixedDeltaTime);
            _body.MovePosition(new Vector3(Motion.X, transform.position.y, Motion.Z));
            _body.MoveRotation(Quaternion.Euler(0f, Motion.HeadingDeg, 0f));
        }

        private void LateUpdate()
        {
            if (hullVisual == null || Motion == null) return;
            // Escora hacia fuera del giro, proporcional a la velocidad y a la guiñada.
            float targetHeel = Mathf.Clamp(Motion.YawRateDegPerSecond * Motion.Speed * heelPerTurn * 0.1f, -maxHeelDeg, maxHeelDeg);
            _heel = Mathf.Lerp(_heel, targetHeel, 1f - Mathf.Exp(-Time.deltaTime * 1.5f));
            float pitch = Mathf.Sin(Time.time * 2f * Mathf.PI / swellPeriodSeconds) * swellPitchDeg;
            hullVisual.localRotation = Quaternion.Euler(pitch, 0f, _heel);
        }

        /// <summary>Sincroniza el modelo si otro sistema teletransporta el buque (p. ej. al reiniciar).</summary>
        public void Teleport(Vector3 position, float headingDeg)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, headingDeg, 0f));
            Motion?.SetPose(position.x, position.z, headingDeg);
        }

        private void OnDrawGizmosSelected()
        {
            if (Motion == null) return;
            // Círculo de giro instantáneo: ayuda a calibrar la maniobra en el editor.
            float radius = Motion.TurnRadius;
            if (float.IsInfinity(radius) || radius > 5000f) return;
            Vector3 side = transform.right * Mathf.Sign(Motion.YawRateDegPerSecond);
            Vector3 center = transform.position + side * radius;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            const int segments = 48;
            Vector3 previous = center + Quaternion.Euler(0f, 0f, 0f) * Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = center + Quaternion.Euler(0f, 360f * i / segments, 0f) * Vector3.forward * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
