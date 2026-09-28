using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Parámetros de maniobra de un buque. Se derivan de su ShipSpec
    /// (velocidad, desplazamiento, eslora) con constantes de balance.
    /// </summary>
    public sealed class ShipHandling
    {
        public const float KnotsToMetersPerSecond = 0.514444f;

        /// <summary>Velocidad máxima avante (m/s).</summary>
        public float MaxSpeedMs { get; }

        /// <summary>
        /// Constante de tiempo de inercia (s): a toda fuerza desde parado
        /// acelera a MaxSpeed/Tau, y al cortar máquinas pierde la mitad de su
        /// velocidad en ~Tau segundos.
        /// </summary>
        public float InertiaSeconds { get; }

        /// <summary>Velocidad (fracción de potencia por segundo) a la que las calderas responden al telégrafo.</summary>
        public float EngineResponsePerSecond { get; }

        /// <summary>Radio de giro mínimo con el timón a la banda y a baja velocidad (m).</summary>
        public float MinTurnRadiusM { get; }

        /// <summary>Crecimiento del radio de giro con la velocidad (a toda fuerza el radio es MinTurnRadius × (1 + factor)).</summary>
        public float TurnRadiusSpeedFactor { get; }

        /// <summary>Ángulo máximo de la pala del timón (grados).</summary>
        public float MaxRudderDegrees { get; }

        /// <summary>Velocidad de giro de la pala del timón (fracción de banda a banda por segundo).</summary>
        public float RudderRatePerSecond { get; }

        /// <summary>Constante de tiempo con la que la guiñada alcanza la velocidad de giro objetivo (s).</summary>
        public float YawResponseSeconds { get; }

        /// <summary>Arrastre lineal residual (1/s): garantiza que el buque acabe deteniéndose.</summary>
        public float LinearDragPerSecond { get; }

        public ShipHandling(
            float maxSpeedMs,
            float inertiaSeconds,
            float engineResponsePerSecond,
            float minTurnRadiusM,
            float turnRadiusSpeedFactor,
            float maxRudderDegrees = 35f,
            float rudderRatePerSecond = 0.5f,
            float yawResponseSeconds = 2.5f,
            float linearDragPerSecond = 0.01f)
        {
            if (maxSpeedMs <= 0f) throw new ArgumentOutOfRangeException(nameof(maxSpeedMs));
            if (inertiaSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(inertiaSeconds));
            if (engineResponsePerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(engineResponsePerSecond));
            if (minTurnRadiusM <= 0f) throw new ArgumentOutOfRangeException(nameof(minTurnRadiusM));
            if (yawResponseSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(yawResponseSeconds));

            MaxSpeedMs = maxSpeedMs;
            InertiaSeconds = inertiaSeconds;
            EngineResponsePerSecond = engineResponsePerSecond;
            MinTurnRadiusM = minTurnRadiusM;
            TurnRadiusSpeedFactor = turnRadiusSpeedFactor;
            MaxRudderDegrees = maxRudderDegrees;
            RudderRatePerSecond = rudderRatePerSecond;
            YawResponseSeconds = yawResponseSeconds;
            LinearDragPerSecond = linearDragPerSecond;
        }

        /// <summary>
        /// Deriva la maniobrabilidad de la ficha del buque: más desplazamiento
        /// = más inercia y calderas más lentas; más eslora = radio de giro mayor.
        /// </summary>
        public static ShipHandling FromSpec(ShipSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            var massScale = (float)Math.Pow(spec.DisplacementTons / 1000f, 1.0 / 3.0);
            return new ShipHandling(
                maxSpeedMs: spec.MaxSpeedKnots * KnotsToMetersPerSecond,
                inertiaSeconds: 25f * massScale,
                engineResponsePerSecond: 0.2f / massScale,
                minTurnRadiusM: 3f * spec.LengthM,
                turnRadiusSpeedFactor: 1f);
        }
    }
}
