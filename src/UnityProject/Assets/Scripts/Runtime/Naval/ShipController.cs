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
    /// Controles: W/S (o el stick hacia delante y atrás, mantenido) suben y bajan la máquina un punto cada poco;
    /// A/D (o el stick a los lados) llevan el timón. Un clic derecho corto en el mar marca un rumbo: el buque va
    /// solo hacia allí hasta llegar o hasta que el jugador toque el timón.
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
        private float _throttleRepeat;
        private int _throttleSign;
        private Vector3 _rightPress;
        private float _rightPressTime;
        private float _baseY;
        private float _sinkDepth;
        private float _sinkPitch;
        private float _sinkRoll;

        /// <summary>Balanceo impuesto desde fuera (°, + estribor arriba): el oleaje que sienten los artilleros.</summary>
        public float ExtraRollDeg { get; set; }

        /// <summary>Segundos entre un punto de máquina y el siguiente al mantener W/S o el stick (valor de jugabilidad).</summary>
        private const float ThrottleRepeatSeconds = 0.55f;
        /// <summary>Al llegar a esta distancia (m) del rumbo marcado se da por alcanzado.</summary>
        private const float CourseArrivalM = 140f;

        /// <summary>Punto del mar al que el jugador mandó ir con el ratón (null: ninguno).</summary>
        public Vector3? CourseTarget { get; private set; }

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
            _baseY = p.y;
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

            // Máquina: al empujar sube (o baja) un punto enseguida y, si se mantiene, otro cada poco.
            // Las pulsaciones se acumulan en Update para no perderlas entre pasos de FixedUpdate.
            float throttle = GameInput.Axis(GameKey.S, GameKey.W);
            int sign = throttle > 0.5f ? 1 : throttle < -0.5f ? -1 : 0;
            if (sign == 0) _throttleSign = 0;
            else if (sign != _throttleSign || (_throttleRepeat -= Time.deltaTime) <= 0f)
            {
                _pendingTelegraphSteps += sign;
                _throttleSign = sign;
                _throttleRepeat = ThrottleRepeatSeconds;
                CourseTarget = null;
            }

            ReadCourseClick();

            float axis = GameInput.Axis(GameKey.A, GameKey.D);
            if (Mathf.Abs(axis) > 0.15f) CourseTarget = null;
            if (CourseTarget.HasValue) _heldRudder = SteerToCourse();
            else if (axis != 0f || autoCenterRudder) _heldRudder = axis;
            Motion.RudderCommand = _heldRudder;
        }

        /// <summary>Clic derecho corto (sin arrastrar, que es girar la cámara) en el mar: rumbo hacia ese punto.</summary>
        private void ReadCourseClick()
        {
            if (GameInput.UsingGamepad) return;
            if (GameInput.MousePressed(1))
            {
                _rightPress = GameInput.MousePosition;
                _rightPressTime = Time.unscaledTime;
            }
            if (!GameInput.MouseReleased(1)) return;
            Vector3 mouse = GameInput.MousePosition;
            if ((mouse - _rightPress).sqrMagnitude > 12f * 12f || Time.unscaledTime - _rightPressTime > 0.35f) return;
            Camera view = Camera.main;
            if (view == null) return;
            Ray ray = view.ScreenPointToRay(mouse);
            var sea = new Plane(Vector3.up, new Vector3(0f, _baseY, 0f));
            if (!sea.Raycast(ray, out float enter)) return;
            CourseTarget = ray.GetPoint(enter);
            // Quien marca un rumbo quiere moverse: si el buque está parado o va atrás, avante.
            if (Motion.Telegraph.Order <= EngineOrder.Stop) _pendingTelegraphSteps += (int)EngineOrder.HalfAhead - (int)Motion.Telegraph.Order;
        }

        /// <summary>Timón proporcional hacia el rumbo marcado; lo olvida al llegar.</summary>
        private float SteerToCourse()
        {
            Vector3 to = CourseTarget.Value - transform.position;
            if (new Vector2(to.x, to.z).magnitude < CourseArrivalM)
            {
                CourseTarget = null;
                return 0f;
            }
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            return Mathf.Clamp(Mathf.DeltaAngle(Motion.HeadingDeg, bearing) / 25f, -1f, 1f);
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
            _body.MovePosition(new Vector3(Motion.X, _baseY - _sinkDepth, Motion.Z));
            _body.MoveRotation(Quaternion.Euler(_sinkPitch, Motion.HeadingDeg, _sinkRoll));
        }

        private void LateUpdate()
        {
            if (hullVisual == null || Motion == null) return;
            // Escora hacia fuera del giro, proporcional a la velocidad y a la guiñada.
            float targetHeel = Mathf.Clamp(Motion.YawRateDegPerSecond * Motion.Speed * heelPerTurn * 0.1f, -maxHeelDeg, maxHeelDeg);
            _heel = Mathf.Lerp(_heel, targetHeel, 1f - Mathf.Exp(-Time.deltaTime * 1.5f));
            float pitch = Mathf.Sin(Time.time * 2f * Mathf.PI / swellPeriodSeconds) * swellPitchDeg;
            hullVisual.localRotation = Quaternion.Euler(pitch, 0f, _heel + ExtraRollDeg);
        }

        /// <summary>Postura de hundimiento (la aplica el control de averías; 0 = a flote).</summary>
        public void SetSinkPose(float depth, float pitchDeg, float rollDeg)
        {
            _sinkDepth = depth;
            _sinkPitch = pitchDeg;
            _sinkRoll = rollDeg;
        }

        /// <summary>Sincroniza el modelo si otro sistema teletransporta el buque (p. ej. al reiniciar).</summary>
        public void Teleport(Vector3 position, float headingDeg)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, headingDeg, 0f));
            _baseY = position.y;
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
