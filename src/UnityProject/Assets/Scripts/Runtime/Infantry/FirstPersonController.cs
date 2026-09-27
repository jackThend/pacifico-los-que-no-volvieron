using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Controlador de infantería en primera persona (ROADMAP 3.1). La lógica vive en Pacifico.Core
    /// (<see cref="InfantryMotor"/>, <see cref="HeadBobModel"/>, <see cref="IronSightModel"/>, <see cref="SightLadder"/>);
    /// aquí solo se leen mandos, se mueve el CharacterController y se coloca la cámara.
    /// <para>
    /// Sin tirones: el movimiento y la cámara se actualizan en Update, al ritmo de la pantalla, con un
    /// CharacterController (no un Rigidbody). Así no hay desfase entre el paso de física y el fotograma
    /// —la causa habitual del «jitter» en primera persona— ni hace falta interpolación.
    /// </para>
    /// Controles: WASD mover · ratón mirar · Mayús correr · C/Ctrl agacharse (corriendo: deslizarse) ·
    /// Espacio saltar · botón derecho apuntar con las miras · rueda o RePág/AvPág: graduar el alza.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Pivote de la cámara (hijo del jugador). Recibe el cabeceo, la altura de los ojos y el balanceo.")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Arma inicial (sus miras y peso determinan el encare y el alza).")]
        [SerializeField] private WeaponDataSO weapon;

        [Header("Mirada")]
        [Tooltip("Grados por píxel de ratón.")]
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private bool invertY;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private float baseFov = 70f;
        [Tooltip("Apertura extra del campo visual a la carrera (sensación de velocidad).")]
        [SerializeField] private float sprintFovBonus = 4f;
        [SerializeField] private bool lockCursor = true;

        [Header("Entorno")]
        [Tooltip("Capas que cuentan como techo al comprobar si hay altura para ponerse de pie.")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        private CharacterController _controller;
        private float _yaw;
        private float _pitch;
        private int _seenLandings;
        private float _fovVelocity;
        private bool _grounded = true;
        private float _scrollAccumulator;
        private readonly Collider[] _overlapBuffer = new Collider[16];

        public InfantryMotor Motor { get; private set; }
        public HeadBobModel HeadBob { get; private set; }
        public IronSightModel Sights { get; private set; }
        public SightLadder Ladder { get; private set; }
        public WeaponSpec Weapon { get; private set; }
        public WeaponDataSO WeaponData => weapon;
        public Camera ViewCamera => viewCamera;
        public RecoilModel Recoil { get; private set; }

        /// <summary>
        /// El fusil está en plena manipulación (recarga): no se puede encarar. Lo fija <see cref="RifleController"/>,
        /// que se ejecuta antes que este componente (<c>DefaultExecutionOrder</c>) para que el valor sea el del fotograma.
        /// </summary>
        public bool AimBlocked { get; set; }

        /// <summary>El jugador controla el personaje: ratón capturado, o captura desactivada (pruebas en el Editor).</summary>
        public bool HasInputFocus => Cursor.lockState == CursorLockMode.Locked || !lockCursor;

        /// <summary>Se emite al cambiar de arma (el fusil reconstruye su ciclo y su munición).</summary>
        public event System.Action<FirstPersonController> WeaponChanged;

        /// <summary>Rayo de puntería: dirección de la mirada más la deriva de la respiración al apuntar.</summary>
        public Ray AimRay
        {
            get
            {
                Transform t = viewCamera != null ? viewCamera.transform : cameraPivot;
                return new Ray(t.position, t.forward);
            }
        }

        /// <summary>Configura referencias por código (escenas generadas por el editor).</summary>
        public void Configure(Transform pivot, Camera cameraComponent, WeaponDataSO weaponData)
        {
            cameraPivot = pivot;
            viewCamera = cameraComponent;
            weapon = weaponData;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Motor = new InfantryMotor();
            HeadBob = new HeadBobModel { ReferenceSpeed = Motor.Settings.SprintSpeed };
            Recoil = new RecoilModel(GetInstanceID());
            EquipWeapon(weapon);
            _yaw = transform.eulerAngles.y;
            ApplyCapsule(Motor.Height);
        }

        /// <summary>Cambia el arma empuñada (p. ej. recoger un Comblain del suelo en Tarapacá).</summary>
        public void EquipWeapon(WeaponDataSO weaponData)
        {
            weapon = weaponData;
            Weapon = weaponData != null ? weaponData.ToSpec() : null;
            bool firearm = Weapon != null && Weapon.IsFirearm;
            Sights = firearm ? new IronSightModel(Weapon) : null;
            Ladder = firearm ? new SightLadder(Weapon) : null;
            WeaponChanged?.Invoke(this);
        }

        private void OnEnable()
        {
            if (lockCursor) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (GameInput.Pressed(GameKey.Escape)) SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            bool hasFocus = HasInputFocus;

            if (hasFocus) UpdateLook();
            var input = ReadInput(hasFocus);

            // 1) Simulación del movimiento y desplazamiento real con colisiones.
            Vec3 displacement = Motor.Step(input, dt, _grounded, CanStandUp());
            CollisionFlags flags = _controller.Move(new Vector3(displacement.X, displacement.Y, displacement.Z));
            _grounded = _controller.isGrounded;
            if ((flags & CollisionFlags.Above) != 0) Motor.OnCeilingHit();
            ApplyCapsule(Motor.Height);

            // 2) Miras y alza.
            if (Sights != null)
            {
                bool moving = Motor.HorizontalSpeed > 0.3f;
                Sights.Step(Motor.IsAiming, dt, Motor.Stamina01, moving, Motor.Stance != Stance.Standing);
                UpdateSightLadder(hasFocus);
            }

            // 3) Cámara: cabeceo, aterrizajes y campo visual.
            if (Motor.LandingCount != _seenLandings)
            {
                _seenLandings = Motor.LandingCount;
                HeadBob.Land(Motor.LastLandingSpeed);
            }
            float aim01 = Sights != null ? Sights.Eased : 0f;
            // Deslizándose no hay pasos: el cabeceo se apaga (solo queda el alabeo por desplazamiento lateral).
            float stepSpeed = Motor.Stance == Stance.Sliding ? 0f : Motor.HorizontalSpeed;
            HeadBob.Step(stepSpeed, Motor.IsGrounded, aim01, input.MoveX, dt);
            Recoil.Step(dt);
            ApplyCamera(aim01, dt);
        }

        private void UpdateLook()
        {
            Vector2 mouse = GameInput.MouseDelta * mouseSensitivity;
            _yaw = MathUtil.WrapAngle360(_yaw + mouse.x);
            _pitch = Mathf.Clamp(_pitch + (invertY ? mouse.y : -mouse.y), -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private InfantryInput ReadInput(bool hasFocus)
        {
            if (!hasFocus) return new InfantryInput { YawDeg = _yaw };
            return new InfantryInput
            {
                MoveX = GameInput.Axis(GameKey.A, GameKey.D),
                MoveZ = GameInput.Axis(GameKey.S, GameKey.W),
                YawDeg = _yaw,
                Sprint = GameInput.Held(GameKey.LeftShift),
                Crouch = GameInput.Held(GameKey.C) || GameInput.Held(GameKey.LeftControl),
                Jump = GameInput.Held(GameKey.Space),
                Aim = GameInput.MouseHeld(1) && Sights != null && !AimBlocked,
            };
        }

        private void UpdateSightLadder(bool hasFocus)
        {
            if (!hasFocus || Ladder == null) return;
            // La rueda solo gradúa el alza con el arma encarada, para no cambiarla por accidente. Se acumula el
            // desplazamiento y se avanza una graduación por «muesca» completa: un trackpad o una rueda de alta
            // resolución envían fracciones en cada fotograma y, sin acumular, recorrerían toda el alza de un gesto.
            if (Motor.IsAiming) _scrollAccumulator += GameInput.ScrollDelta;
            else _scrollAccumulator = 0f;
            while (_scrollAccumulator >= 1f)
            {
                Ladder.Raise();
                _scrollAccumulator -= 1f;
            }
            while (_scrollAccumulator <= -1f)
            {
                Ladder.Lower();
                _scrollAccumulator += 1f;
            }
            if (GameInput.Pressed(GameKey.PageUp)) Ladder.Raise();
            if (GameInput.Pressed(GameKey.PageDown)) Ladder.Lower();
        }

        private bool CanStandUp()
        {
            if (Motor.Stance == Stance.Standing) return true;
            float radius = _controller.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
            Vector3 top = transform.position + Vector3.up * (Motor.Settings.StandingHeight - radius);
            // No se usa Physics.CheckCapsule: detectaría el propio CharacterController y nunca dejaría levantarse.
            // La versión NonAlloc reutiliza un búfer: agachado se comprueba cada fotograma y no debe generar basura.
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlapBuffer, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlapBuffer[i];
                if (c != _controller && !c.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        /// <summary>La cápsula crece y encoge desde los pies (el origen del jugador está en el suelo).</summary>
        private void ApplyCapsule(float height)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private void ApplyCamera(float aim01, float dt)
        {
            if (cameraPivot == null) return;

            float swayYaw = Sights != null ? Sights.SwayYawDeg * aim01 : 0f;
            float swayPitch = Sights != null ? Sights.SwayPitchDeg * aim01 : 0f;
            cameraPivot.localPosition = new Vector3(HeadBob.OffsetX, Motor.EyeHeight + HeadBob.OffsetY, 0f);
            // El retroceso levanta la boca (pitch negativo en Unity) y la desvía un poco de lado.
            float pitch = _pitch + HeadBob.PitchDeg + swayPitch - Recoil.PitchDeg;
            cameraPivot.localRotation = Quaternion.Euler(pitch, swayYaw + Recoil.YawDeg, HeadBob.RollDeg);

            if (viewCamera != null)
            {
                float fov = baseFov * (Sights != null ? Sights.FovMultiplier : 1f) + (Motor.IsSprinting ? sprintFovBonus : 0f);
                viewCamera.fieldOfView = MathUtil.SmoothDamp(viewCamera.fieldOfView, fov, ref _fovVelocity, 0.12f, dt);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Paredes (normales casi horizontales): se anula la velocidad contra ellas para no «pegarse».
            if (Mathf.Abs(hit.normal.y) < 0.3f) Motor.OnWallHit(new Vec3(hit.normal.x, hit.normal.y, hit.normal.z));
        }
    }
}
