using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Modelo de maniobra en el plano (ROADMAP 2.1). Convenciones de Unity: X = este, Z = norte,
    /// rumbo en grados en sentido horario desde +Z (igual que la rotación en Y de un Transform).
    /// <para>
    /// Inercia: la velocidad tiende a la ordenada por el telégrafo con respuesta exponencial. La constante de
    /// tiempo al ganar arrancada (<see cref="ShipHandling.AccelerationTimeSeconds"/>) es mucho menor que al
    /// perderla por rozamiento (<see cref="ShipHandling.CoastDownTimeSeconds"/>): un buque que corta máquina
    /// sigue avanzando durante minutos.
    /// </para>
    /// <para>
    /// Gobierno: el timón necesita arrancada. Por encima de la velocidad de gobierno la velocidad angular máxima
    /// es constante, así que el radio de giro (v / ω) crece en proporción a la velocidad.
    /// </para>
    /// </summary>
    public sealed class ShipMotionModel
    {
        /// <summary>Fracción de la velocidad máxima a partir de la cual el timón gobierna plenamente.</summary>
        public const float SteerageSpeedFraction = 0.25f;
        /// <summary>Pérdida de velocidad con el timón a la banda (los cascos de 1879 «frenaban» al caer).</summary>
        public const float TurnSpeedLoss = 0.25f;
        /// <summary>Constante de tiempo de la guiñada (inercia rotacional del casco), en segundos.</summary>
        public const float YawResponseSeconds = 1.5f;
        /// <summary>Las constantes se definen para alcanzar el 95 % del cambio: 3 constantes de tiempo.</summary>
        private const float TimeConstantsTo95Percent = 3f;
        /// <summary>Invertir la máquina frena con menos eficacia de la que acelera la hélice avante.</summary>
        private const float AsternBrakingFactor = 1.5f;

        private readonly ShipHandling _handling;
        private readonly float _baseMaxSpeed;

        public ShipMotionModel(ShipSpec spec) : this(spec.Handling, spec.MaxSpeedMps)
        {
        }

        public ShipMotionModel(ShipHandling handling, float maxSpeedMps)
        {
            _handling = handling ?? throw new ArgumentNullException(nameof(handling));
            if (maxSpeedMps <= 0f) throw new ArgumentOutOfRangeException(nameof(maxSpeedMps));
            _baseMaxSpeed = maxSpeedMps;
        }

        public EngineTelegraph Telegraph { get; } = new EngineTelegraph();

        // --- Estado ------------------------------------------------------------------------------
        public float X { get; private set; }
        public float Z { get; private set; }
        public float HeadingDeg { get; private set; }
        /// <summary>Velocidad sobre el fondo en la dirección de la proa (m/s, negativa hacia atrás).</summary>
        public float Speed { get; private set; }
        /// <summary>Velocidad angular (°/s, positiva a estribor).</summary>
        public float YawRateDegPerSecond { get; private set; }
        /// <summary>Ángulo de timón real, normalizado [-1, 1] (negativo = babor).</summary>
        public float Rudder { get; private set; }

        // --- Mandos ------------------------------------------------------------------------------
        /// <summary>Timón ordenado [-1, 1] (A = -1 babor, D = +1 estribor).</summary>
        public float RudderCommand { get; set; }

        // --- Averías (ROADMAP 2.4) ---------------------------------------------------------------
        /// <summary>Multiplicador de la potencia de máquina por daños en calderas o inundación [0, 1].</summary>
        public float PropulsionFactor { get; set; } = 1f;
        /// <summary>Si el servomotor está dañado, el timón queda trabado en su ángulo actual.</summary>
        public bool RudderJammed { get; set; }

        public float MaxSpeed => _baseMaxSpeed;
        public float SpeedKnots => Units.MetersPerSecondToKnots(Speed);

        /// <summary>Velocidad que persigue la máquina con la orden y el timón actuales.</summary>
        public float TargetSpeed
        {
            get
            {
                float fraction = EngineTelegraph.SpeedFraction(Telegraph.Order);
                float turnLoss = 1f - TurnSpeedLoss * Math.Abs(Rudder) * SteerageFactor(Speed);
                return fraction * _baseMaxSpeed * MathUtil.Clamp01(PropulsionFactor) * turnLoss;
            }
        }

        /// <summary>Radio de giro instantáneo (m); infinito si no gira.</summary>
        public float TurnRadius
        {
            get
            {
                float omega = Math.Abs(YawRateDegPerSecond) * MathUtil.Deg2Rad;
                return omega < 1e-5f ? float.PositiveInfinity : Math.Abs(Speed) / omega;
            }
        }

        public void SetPose(float x, float z, float headingDeg)
        {
            X = x;
            Z = z;
            HeadingDeg = MathUtil.WrapAngle360(headingDeg);
        }

        /// <summary>Fija la velocidad (p. ej. al empezar una misión ya navegando).</summary>
        public void SetSpeed(float speedMps) => Speed = speedMps;

        /// <summary>Aplica una variación brusca de velocidad (espolonazo, varada).</summary>
        public void ApplySpeedImpulse(float deltaSpeedMps) => Speed += deltaSpeedMps;

        /// <summary>Avanza la simulación <paramref name="dt"/> segundos. Independiente de la tasa de fotogramas.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;

            // 1) Servomotor del timón.
            if (!RudderJammed)
            {
                float rudderRate = 1f / Math.Max(0.01f, _handling.RudderTimeSeconds);
                Rudder = MathUtil.MoveTowards(Rudder, MathUtil.Clamp(RudderCommand, -1f, 1f), rudderRate * dt);
            }

            // 2) Máquina e inercia hidrodinámica.
            float target = TargetSpeed;
            float tau = SpeedTimeConstant(Speed, target);
            Speed += (target - Speed) * (1f - (float)Math.Exp(-dt / tau));

            // 3) Guiñada: el timón solo actúa con arrancada; marcha atrás invierte el efecto.
            float targetYaw = _handling.MaxTurnRateDegPerSecond * Rudder * SteerageFactor(Speed) * Math.Sign(Speed);
            YawRateDegPerSecond += (targetYaw - YawRateDegPerSecond) * (1f - (float)Math.Exp(-dt / YawResponseSeconds));

            // 4) Integración de la posición (punto medio del rumbo para reducir la deriva numérica en giros).
            float midHeading = (HeadingDeg + YawRateDegPerSecond * dt * 0.5f) * MathUtil.Deg2Rad;
            X += (float)Math.Sin(midHeading) * Speed * dt;
            Z += (float)Math.Cos(midHeading) * Speed * dt;
            HeadingDeg = MathUtil.WrapAngle360(HeadingDeg + YawRateDegPerSecond * dt);
        }

        /// <summary>Eficacia del timón según la arrancada, en [0, 1].</summary>
        public float SteerageFactor(float speed)
        {
            return MathUtil.Clamp01(Math.Abs(speed) / (SteerageSpeedFraction * _baseMaxSpeed));
        }

        private float SpeedTimeConstant(float current, float target)
        {
            float accelerate = _handling.AccelerationTimeSeconds / TimeConstantsTo95Percent;
            float coast = _handling.CoastDownTimeSeconds / TimeConstantsTo95Percent;

            bool reversing = Math.Abs(current) > 0.05f && Math.Sign(target) != Math.Sign(current) && target != 0f;
            if (reversing) return accelerate * AsternBrakingFactor;

            bool gainingWay = Math.Abs(target) > Math.Abs(current);
            return gainingWay ? accelerate : coast;
        }
    }
}
