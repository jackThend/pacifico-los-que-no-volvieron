using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Infantry
{
    public enum Stance
    {
        Standing = 0,
        Crouching = 1,
        Sliding = 2,
    }

    /// <summary>Mandos de un paso de simulación (ya leídos del teclado o de una IA).</summary>
    public struct InfantryInput
    {
        /// <summary>Movimiento lateral local [-1, 1] (A/D).</summary>
        public float MoveX;
        /// <summary>Movimiento frontal local [-1, 1] (W/S).</summary>
        public float MoveZ;
        /// <summary>Guiñada de la mirada (°), convención de Unity.</summary>
        public float YawDeg;
        public bool Sprint;
        public bool Crouch;
        public bool Jump;
        public bool Aim;
    }

    /// <summary>
    /// Motor de movimiento en primera persona (ROADMAP 3.1). No conoce el motor: calcula el desplazamiento de cada
    /// paso y lo entrega a un CharacterController, que devuelve si quedó apoyado en el suelo o tocó un techo.
    /// <para>
    /// Es independiente de la tasa de fotogramas: aceleración lineal hacia la velocidad deseada (exacta con
    /// objetivo constante) e integración vertical de Verlet (exacta con gravedad constante), sin sobrepasar nunca
    /// la velocidad objetivo; por eso no hay oscilaciones ni tirones a 30, 60 o 144 FPS.
    /// </para>
    /// </summary>
    public sealed class InfantryMotor
    {
        private readonly InfantryMovementSettings _s;
        private Vec3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _slideTimer;
        private float _slideCooldown;
        private float _staminaRegenTimer;
        private bool _previousCrouch;
        private bool _previousJump;
        private bool _wasGrounded = true;
        private float _airTime;

        /// <summary>
        /// Tiempo mínimo en el aire para contar un aterrizaje. El isGrounded de un CharacterController parpadea en
        /// pendientes y escalones; sin este filtro cada parpadeo produciría una sacudida de cámara.
        /// </summary>
        public const float MinAirTimeForLanding = 0.2f;
        private Vec3 _slideDirection;

        public InfantryMotor(InfantryMovementSettings settings = null)
        {
            _s = settings ?? new InfantryMovementSettings();
            Stamina = _s.MaxStamina;
            Height = _s.StandingHeight;
        }

        public InfantryMovementSettings Settings => _s;

        public Stance Stance { get; private set; } = Stance.Standing;
        public bool IsGrounded { get; private set; } = true;
        public bool IsSprinting { get; private set; }
        public bool IsAiming { get; private set; }
        /// <summary>Tras agotar la resistencia no se puede correr hasta recuperar el umbral.</summary>
        public bool IsExhausted { get; private set; }
        public float Stamina { get; private set; }
        public float Stamina01 => Stamina / _s.MaxStamina;
        /// <summary>Altura actual de la cápsula (transición suave entre posturas).</summary>
        public float Height { get; private set; }
        public float EyeHeight => Height * _s.EyeHeightRatio;

        public Vec3 Velocity => new Vec3(_horizontalVelocity.X, _verticalVelocity, _horizontalVelocity.Z);
        public float HorizontalSpeed => _horizontalVelocity.HorizontalMagnitude;

        /// <summary>Velocidad vertical (m/s, negativa) del último aterrizaje; la consume el cabeceo de cámara.</summary>
        public float LastLandingSpeed { get; private set; }
        /// <summary>Incrementa en cada aterrizaje (permite detectar el evento sin callbacks).</summary>
        public int LandingCount { get; private set; }

        /// <summary>Altura objetivo de la postura actual.</summary>
        public float TargetHeight
        {
            get
            {
                switch (Stance)
                {
                    case Stance.Crouching: return _s.CrouchHeight;
                    case Stance.Sliding: return _s.SlideHeight;
                    default: return _s.StandingHeight;
                }
            }
        }

        /// <summary>
        /// Avanza <paramref name="dt"/> segundos y devuelve el desplazamiento a aplicar.
        /// </summary>
        /// <param name="grounded">Si el personaje estaba apoyado tras el movimiento anterior.</param>
        /// <param name="canStand">Si hay altura libre para ponerse de pie (no hay techo bajo).</param>
        public Vec3 Step(InfantryInput input, float dt, bool grounded, bool canStand = true)
        {
            if (dt <= 0f) return Vec3.Zero;

            UpdateGrounding(grounded, dt);
            bool crouchPressed = input.Crouch && !_previousCrouch;
            bool jumpPressed = input.Jump && !_previousJump;
            _previousCrouch = input.Crouch;
            _previousJump = input.Jump;
            _slideCooldown = Math.Max(0f, _slideCooldown - dt);

            UpdateStance(input, crouchPressed, jumpPressed, canStand);
            IsAiming = input.Aim && Stance != Stance.Sliding;
            IsSprinting = Stance == Stance.Standing && IsGrounded && input.Sprint && input.MoveZ > 0.5f && !IsAiming && !IsExhausted;

            if (Stance == Stance.Sliding) UpdateSlide(input, dt);
            else UpdateWalk(input, dt);

            float verticalDisplacement = UpdateVertical(jumpPressed, dt);
            UpdateStamina(dt);
            Height = MathUtil.MoveTowards(Height, TargetHeight, _s.StanceChangeSpeed * dt);

            return new Vec3(_horizontalVelocity.X * dt, verticalDisplacement, _horizontalVelocity.Z * dt);
        }

        /// <summary>El CharacterController tocó un techo: se anula la subida.</summary>
        public void OnCeilingHit()
        {
            if (_verticalVelocity > 0f) _verticalVelocity = 0f;
        }

        /// <summary>Choque lateral: se elimina la componente de velocidad contra la pared (normal horizontal).</summary>
        public void OnWallHit(Vec3 normal)
        {
            Vec3 n = normal.Horizontal.Normalized;
            float into = Vec3.Dot(_horizontalVelocity, n);
            if (into < 0f) _horizontalVelocity -= n * into;
            // En un deslizamiento la dirección también se desvía: si no, se reconstruiría contra la pared cada fotograma.
            if (Stance == Stance.Sliding && _horizontalVelocity.HorizontalMagnitude > 1e-4f)
            {
                _slideDirection = _horizontalVelocity.Normalized;
            }
        }

        // ------------------------------------------------------------------------------------------

        private void UpdateGrounding(bool grounded, float dt)
        {
            if (grounded && !_wasGrounded && _airTime >= MinAirTimeForLanding)
            {
                LastLandingSpeed = _verticalVelocity;
                LandingCount++;
            }
            // Al abandonar el suelo sin saltar (borde de un parapeto) se anula el empuje hacia abajo del «pegado»,
            // para empezar a caer desde velocidad vertical nula y no a varios m/s.
            if (!grounded && _wasGrounded && _verticalVelocity < 0f) _verticalVelocity = 0f;
            _airTime = grounded ? 0f : _airTime + dt;
            _wasGrounded = grounded;
            IsGrounded = grounded;
        }

        private void UpdateStance(InfantryInput input, bool crouchPressed, bool jumpPressed, bool canStand)
        {
            if (Stance == Stance.Sliding)
            {
                bool expired = _slideTimer >= _s.SlideMaxDuration || HorizontalSpeed < _s.SlideMinSpeed;
                // Saltar desde el deslizamiento endereza al soldado (si hay altura) para que el salto no se pierda.
                if (expired || jumpPressed || !IsGrounded) EndSlide(input.Crouch && !jumpPressed, canStand);
                return;
            }

            bool fastEnough = HorizontalSpeed >= _s.SprintSpeed * _s.SlideEntrySpeedRatio;
            bool canSlide = crouchPressed && IsSprinting && IsGrounded && fastEnough &&
                            _slideCooldown <= 0f && Stamina >= _s.SlideStaminaCost;
            if (canSlide)
            {
                StartSlide();
                return;
            }

            if (input.Crouch) Stance = Stance.Crouching;
            else if (canStand) Stance = Stance.Standing;
            // Sin altura libre se sigue agachado aunque se suelte la tecla.
        }

        private void StartSlide()
        {
            Stance = Stance.Sliding;
            _slideTimer = 0f;
            _slideDirection = _horizontalVelocity.Normalized;
            _horizontalVelocity = _slideDirection * Math.Max(HorizontalSpeed, _s.SlideStartSpeed);
            Stamina -= _s.SlideStaminaCost;
            _staminaRegenTimer = _s.StaminaRegenDelay;
            IsSprinting = false;
        }

        private void EndSlide(bool crouchHeld, bool canStand)
        {
            Stance = crouchHeld || !canStand ? Stance.Crouching : Stance.Standing;
            _slideCooldown = _s.SlideCooldown;
        }

        private void UpdateSlide(InfantryInput input, float dt)
        {
            _slideTimer += dt;
            float speed = Math.Max(0f, HorizontalSpeed - _s.SlideFriction * dt);

            // Ligera corrección de rumbo hacia donde se mira (no hay giros bruscos en un deslizamiento).
            Vec3 look = Vec3.RotateYaw(0f, 1f, input.YawDeg);
            if (Vec3.Dot(look, _slideDirection) > 0f)
            {
                _slideDirection = Vec3.MoveTowards(_slideDirection, look, _s.SlideSteering * dt).Normalized;
            }
            _horizontalVelocity = _slideDirection * speed;
        }

        private void UpdateWalk(InfantryInput input, float dt)
        {
            float x = MathUtil.Clamp(input.MoveX, -1f, 1f);
            float z = MathUtil.Clamp(input.MoveZ, -1f, 1f);
            float magnitude = (float)Math.Sqrt(x * x + z * z);
            if (magnitude > 1f)
            {
                x /= magnitude;
                z /= magnitude;
                magnitude = 1f;
            }

            float baseSpeed = Stance == Stance.Crouching ? _s.CrouchSpeed : IsSprinting ? _s.SprintSpeed : _s.WalkSpeed;
            if (IsAiming) baseSpeed *= _s.AimSpeedMultiplier;

            // Retroceder y desplazarse de lado es más lento que avanzar.
            float directional = z < 0f ? _s.BackwardMultiplier : 1f;
            if (magnitude > 1e-4f)
            {
                float lateralShare = Math.Abs(x) / magnitude;
                directional *= MathUtil.Lerp(1f, _s.StrafeMultiplier, lateralShare);
            }

            Vec3 wish = Vec3.RotateYaw(x, z, input.YawDeg) * (baseSpeed * directional);

            if (IsGrounded)
            {
                bool accelerating = wish.HorizontalMagnitude > 1e-4f && Vec3.Dot(wish, _horizontalVelocity) >= 0f;
                float rate = accelerating ? _s.GroundAcceleration : _s.GroundDeceleration;
                _horizontalVelocity = Vec3.MoveTowards(_horizontalVelocity, wish, rate * dt);
            }
            else if (wish.HorizontalMagnitude > 1e-4f)
            {
                // En el aire solo hay un leve control; no se frena ni se acelera más allá del impulso del salto.
                Vec3 steered = Vec3.MoveTowards(_horizontalVelocity, wish, _s.AirAcceleration * dt);
                float cap = Math.Max(HorizontalSpeed, wish.HorizontalMagnitude);
                _horizontalVelocity = steered.HorizontalMagnitude > cap ? steered.Normalized * cap : steered;
            }
        }

        private float UpdateVertical(bool jumpPressed, float dt)
        {
            bool canJump = IsGrounded && Stance != Stance.Crouching && Stamina >= _s.JumpStaminaCost;
            if (jumpPressed && canJump)
            {
                _verticalVelocity = (float)Math.Sqrt(2f * _s.Gravity * _s.JumpHeight);
                Stamina -= _s.JumpStaminaCost;
                _staminaRegenTimer = _s.StaminaRegenDelay;
                IsGrounded = false;
                _wasGrounded = false;
            }
            else if (IsGrounded)
            {
                // Apoyado: se empuja hacia abajo lo bastante para seguir cualquier pendiente transitable
                // (hasta 45°, bajar h metros por cada metro avanzado) sin despegarse del suelo al correr cuesta abajo.
                _verticalVelocity = -(_s.GroundStickSpeed + HorizontalSpeed * _s.MaxWalkableSlopeTan);
                return _verticalVelocity * dt;
            }

            float before = _verticalVelocity;
            _verticalVelocity -= _s.Gravity * dt;
            return (before + _verticalVelocity) * 0.5f * dt;
        }

        private void UpdateStamina(float dt)
        {
            if (IsSprinting)
            {
                Stamina = Math.Max(0f, Stamina - _s.SprintStaminaPerSecond * dt);
                _staminaRegenTimer = _s.StaminaRegenDelay;
                if (Stamina <= 0f) IsExhausted = true;
            }
            else if (_staminaRegenTimer > 0f)
            {
                _staminaRegenTimer -= dt;
            }
            else
            {
                Stamina = Math.Min(_s.MaxStamina, Stamina + _s.StaminaRegenPerSecond * dt);
            }

            if (IsExhausted && Stamina >= _s.StaminaRecoverThreshold) IsExhausted = false;
        }
    }
}
