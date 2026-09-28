using System;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Simulación plana (2D) de propulsión, inercia hidrodinámica y timón.
    /// Determinista e independiente de Unity: el controlador de Runtime la
    /// avanza en FixedUpdate y copia posición y rumbo al Transform.
    /// Convención: rumbo 0° = +Z (norte), positivo en sentido horario (igual
    /// que la rotación Y de Unity); X = este.
    /// </summary>
    public sealed class ShipMotionModel
    {
        private const float MaxSubstepSeconds = 0.02f;
        private const float DegreesPerRadian = 57.29578f;

        private readonly float quadraticDrag;

        public ShipHandling Handling { get; }
        public EngineOrder Order { get; private set; }

        /// <summary>Potencia real entregada por las máquinas (-1..1), con retardo respecto al telégrafo.</summary>
        public float EngineOutput { get; private set; }

        /// <summary>Velocidad sobre el agua (m/s); negativa si se navega hacia atrás.</summary>
        public float SpeedMs { get; private set; }

        /// <summary>Posición de la pala del timón (-1 = babor a la banda, +1 = estribor a la banda).</summary>
        public float Rudder { get; private set; }

        /// <summary>Entrada de timón solicitada (-1..1).</summary>
        public float RudderCommand { get; private set; }

        public float HeadingDegrees { get; private set; }
        public float YawRateDegreesPerSecond { get; private set; }
        public float PositionX { get; private set; }
        public float PositionZ { get; private set; }

        /// <summary>
        /// Potencia máxima disponible (0..1): la reducen calderas averiadas e
        /// inundación. El telégrafo puede pedir más, pero las máquinas no la dan.
        /// </summary>
        public float PowerLimit { get; private set; } = 1f;

        public float SpeedKnots => SpeedMs / ShipHandling.KnotsToMetersPerSecond;
        public float RudderDegrees => Rudder * Handling.MaxRudderDegrees;

        public ShipMotionModel(ShipHandling handling, float x = 0f, float z = 0f, float headingDegrees = 0f)
        {
            Handling = handling ?? throw new ArgumentNullException(nameof(handling));
            // Arrastre cuadrático calibrado: a toda fuerza desde parado la aceleración es MaxSpeed / Inercia.
            quadraticDrag = 1f / (handling.MaxSpeedMs * handling.InertiaSeconds);
            PositionX = x;
            PositionZ = z;
            HeadingDegrees = Angles.Normalize360(headingDegrees);
            Order = EngineOrder.Stop;
        }

        public void SetOrder(EngineOrder order) => Order = order;

        public void SetPowerLimit(float limit) => PowerLimit = Clamp(limit, 0f, 1f);

        /// <summary>Choque (espolonazo): conserva solo <paramref name="keepFraction"/> de la velocidad.</summary>
        public void ApplySpeedLoss(float keepFraction)
        {
            SpeedMs *= Clamp(keepFraction, 0f, 1f);
        }

        /// <summary>Sube una posición del telégrafo (W). Devuelve true si cambió.</summary>
        public bool TelegraphUp()
        {
            if (Order == EngineOrder.Full) return false;
            Order = (EngineOrder)((int)Order + 1);
            return true;
        }

        /// <summary>Baja una posición del telégrafo (S). Devuelve true si cambió.</summary>
        public bool TelegraphDown()
        {
            if (Order == EngineOrder.Astern) return false;
            Order = (EngineOrder)((int)Order - 1);
            return true;
        }

        /// <summary>Entrada continua de timón (A = -1, D = +1, soltar = 0).</summary>
        public void SetRudderCommand(float command)
        {
            RudderCommand = Clamp(command, -1f, 1f);
        }

        /// <summary>Avanza la simulación. Pasos grandes se subdividen para mantener la estabilidad.</summary>
        public void Step(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            var steps = (int)Math.Ceiling(deltaSeconds / MaxSubstepSeconds);
            var dt = deltaSeconds / steps;
            for (var i = 0; i < steps; i++) Substep(dt);
        }

        /// <summary>Radio de giro actual con el timón a la banda (m); crece con la velocidad.</summary>
        public float TurnRadiusAtSpeed(float speedMs)
        {
            var speedRatio = Math.Min(Math.Abs(speedMs) / Handling.MaxSpeedMs, 1.5f);
            return Handling.MinTurnRadiusM * (1f + Handling.TurnRadiusSpeedFactor * speedRatio);
        }

        private void Substep(float dt)
        {
            // 1. Calderas: la potencia sigue al telégrafo con retardo.
            var requested = Clamp(Order.PowerFraction(), -PowerLimit, PowerLimit);
            EngineOutput = MoveTowards(EngineOutput, requested, Handling.EngineResponsePerSecond * dt);

            // 2. Propulsión vs. arrastre: velocidad terminal = potencia × velocidad máxima.
            var targetSpeed = EngineOutput * Handling.MaxSpeedMs;
            var thrust = quadraticDrag * targetSpeed * Math.Abs(targetSpeed) + Handling.LinearDragPerSecond * targetSpeed;
            var drag = quadraticDrag * SpeedMs * Math.Abs(SpeedMs) + Handling.LinearDragPerSecond * SpeedMs;
            var newSpeed = SpeedMs + (thrust - drag) * dt;
            // Sin empuje, el arrastre frena pero nunca invierte la marcha.
            if (thrust == 0f && Math.Sign(newSpeed) != Math.Sign(SpeedMs)) newSpeed = 0f;
            SpeedMs = newSpeed;

            // 3. Timón: la pala gira a velocidad limitada.
            Rudder = MoveTowards(Rudder, RudderCommand, Handling.RudderRatePerSecond * 2f * dt);

            // 4. Guiñada: sin arrancada no hay gobierno; el radio de giro crece con la velocidad.
            var targetYaw = Rudder * SpeedMs / TurnRadiusAtSpeed(SpeedMs) * DegreesPerRadian;
            var yawBlend = 1f - (float)Math.Exp(-dt / Handling.YawResponseSeconds);
            YawRateDegreesPerSecond += (targetYaw - YawRateDegreesPerSecond) * yawBlend;
            HeadingDegrees = Angles.Normalize360(HeadingDegrees + YawRateDegreesPerSecond * dt);

            // 5. Traslación sobre el rumbo actual.
            var headingRad = HeadingDegrees / DegreesPerRadian;
            PositionX += (float)Math.Sin(headingRad) * SpeedMs * dt;
            PositionZ += (float)Math.Cos(headingRad) * SpeedMs * dt;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
