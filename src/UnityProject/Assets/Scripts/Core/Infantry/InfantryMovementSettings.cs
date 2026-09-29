using Pacifico.Core.Common;

namespace Pacifico.Core.Infantry
{
    /// <summary>
    /// Parámetros de movimiento del infante (GDD §3.1: respuesta moderna e inmediata). Las velocidades son de juego:
    /// más ágiles que las de un soldado real cargado con 20–25 kg de equipo, pero con la misma jerarquía
    /// (agachado &lt; caminar &lt; correr) y con fatiga al correr bajo el sol del desierto.
    /// </summary>
    public sealed class InfantryMovementSettings
    {
        // --- Velocidades (m/s) ---------------------------------------------------------------------
        public float WalkSpeed { get; set; } = 2.6f;
        public float SprintSpeed { get; set; } = 5.4f;
        public float CrouchSpeed { get; set; } = 1.5f;
        /// <summary>Multiplicador al apuntar con las miras.</summary>
        public float AimSpeedMultiplier { get; set; } = 0.55f;
        public float BackwardMultiplier { get; set; } = 0.7f;
        public float StrafeMultiplier { get; set; } = 0.85f;

        // --- Aceleraciones (m/s²) ------------------------------------------------------------------
        public float GroundAcceleration { get; set; } = 16f;
        public float GroundDeceleration { get; set; } = 20f;
        public float AirAcceleration { get; set; } = 3f;
        public float Gravity { get; set; } = 9.81f;
        /// <summary>Velocidad vertical base con la que se «pega» al suelo.</summary>
        public float GroundStickSpeed { get; set; } = 2f;
        /// <summary>Tangente de la pendiente máxima transitable (45°, igual que el slopeLimit del CharacterController).</summary>
        public float MaxWalkableSlopeTan { get; set; } = 1f;
        /// <summary>Altura de salto (m): modesta, el soldado carga mochila, fusil y cartucheras.</summary>
        public float JumpHeight { get; set; } = 0.55f;

        // --- Posturas (m) --------------------------------------------------------------------------
        public float StandingHeight { get; set; } = 1.75f;
        public float CrouchHeight { get; set; } = 1.15f;
        public float SlideHeight { get; set; } = 0.95f;
        /// <summary>Altura de los ojos respecto a la altura de la cápsula.</summary>
        public float EyeHeightRatio { get; set; } = 0.92f;
        /// <summary>Velocidad con la que cambia la altura de la cápsula entre posturas (m/s).</summary>
        public float StanceChangeSpeed { get; set; } = 5f;

        // --- Deslizamiento táctico ----------------------------------------------------------------
        public float SlideStartSpeed { get; set; } = 6.4f;
        /// <summary>Deceleración por rozamiento durante el deslizamiento (m/s²).</summary>
        public float SlideFriction { get; set; } = 5.5f;
        public float SlideMinSpeed { get; set; } = 2.2f;
        public float SlideMaxDuration { get; set; } = 0.9f;
        public float SlideCooldown { get; set; } = 1.0f;
        /// <summary>Fracción de la velocidad de carrera necesaria para iniciar un deslizamiento.</summary>
        public float SlideEntrySpeedRatio { get; set; } = 0.8f;
        /// <summary>Capacidad de corregir la dirección durante el deslizamiento (0 = ninguna).</summary>
        public float SlideSteering { get; set; } = 1.5f;

        // --- Resistencia --------------------------------------------------------------------------
        public float MaxStamina { get; set; } = 100f;
        public float SprintStaminaPerSecond { get; set; } = 11f;
        public float SlideStaminaCost { get; set; } = 15f;
        public float JumpStaminaCost { get; set; } = 8f;
        public float StaminaRegenPerSecond { get; set; } = 14f;
        public float StaminaRegenDelay { get; set; } = 1.2f;
        /// <summary>Tras agotarse, no se puede volver a correr hasta recuperar esta cantidad.</summary>
        public float StaminaRecoverThreshold { get; set; } = 30f;

        public ValidationResult Validate()
        {
            var r = new ValidationResult("InfantryMovementSettings");
            r.Require(CrouchSpeed < WalkSpeed && WalkSpeed < SprintSpeed, "debe cumplirse agachado < caminar < correr");
            r.Require(SlideStartSpeed >= SprintSpeed, "el deslizamiento debe arrancar al menos a velocidad de carrera");
            r.Require(SlideMinSpeed < SlideStartSpeed, "SlideMinSpeed debe ser menor que SlideStartSpeed");
            r.Require(SlideHeight < CrouchHeight && CrouchHeight < StandingHeight, "alturas de postura incoherentes");
            r.RequirePositive(GroundAcceleration, nameof(GroundAcceleration));
            r.RequirePositive(GroundDeceleration, nameof(GroundDeceleration));
            r.RequirePositive(Gravity, nameof(Gravity));
            r.RequirePositive(MaxStamina, nameof(MaxStamina));
            r.Require(StaminaRecoverThreshold < MaxStamina, "el umbral de recuperación debe ser menor que la resistencia máxima");
            return r;
        }
    }
}
