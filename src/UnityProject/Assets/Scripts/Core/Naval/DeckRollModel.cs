using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Balanceo de la cubierta de un buque de costado (capítulo 1, «calcula el balanceo de las olas»). Las piezas
    /// de costado van fijas al casco: al escorar hacia una banda, sus cañones se deprimen y los de la otra se
    /// elevan en el mismo ángulo. Los artilleros de 1879 disparaban «a la cubierta horizontal»; a 500 m, un grado
    /// de más o de menos son unos nueve metros de altura en el blanco, más que la obra muerta del Huáscar.
    /// El balanceo es una oscilación armónica exacta (independiente del paso de tiempo).
    /// </summary>
    public sealed class DeckRollModel
    {
        /// <summary>Amplitud de juego (°). Licencia de diseño: mar llana de Iquique, balanceo suave pero perceptible.</summary>
        public const float DefaultAmplitudeDeg = 2f;
        /// <summary>Periodo de balanceo (s). Estimación para una corbeta de 850 t y 9,75 m de manga.</summary>
        public const float DefaultPeriodSeconds = 9f;

        public DeckRollModel(float amplitudeDeg = DefaultAmplitudeDeg, float periodSeconds = DefaultPeriodSeconds, float phase = 0f)
        {
            if (periodSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(periodSeconds));
            AmplitudeDeg = Math.Abs(amplitudeDeg);
            PeriodSeconds = periodSeconds;
            Time = phase * periodSeconds;
        }

        public float AmplitudeDeg { get; }
        public float PeriodSeconds { get; }
        public float Time { get; private set; }

        /// <summary>Escora actual (°): positiva con la banda de estribor alzada.</summary>
        public float AngleDeg => AngleAt(Time);

        /// <summary>Velocidad angular (°/s): el signo dice si la cubierta sube o baja.</summary>
        public float RateDegPerSecond =>
            AmplitudeDeg * 2f * (float)Math.PI / PeriodSeconds * (float)Math.Cos(2.0 * Math.PI * Time / PeriodSeconds);

        public float AngleAt(float t) => AmplitudeDeg * (float)Math.Sin(2.0 * Math.PI * t / PeriodSeconds);

        public void Step(float dt) => Time += dt;

        /// <summary>Error de elevación que el balanceo suma a las piezas de una banda (°).</summary>
        public float ElevationErrorDeg(BroadsideSide side) => (int)side * AngleDeg;

        /// <summary>Altura (m) de la trayectoria sobre el agua a <paramref name="distance"/> metros, tiro sin rozamiento.</summary>
        public static float HeightAt(float muzzleVelocity, float elevationDeg, float distance)
        {
            double e = elevationDeg * MathUtil.Deg2Rad;
            double cos = Math.Cos(e);
            return (float)(distance * Math.Tan(e) - Ballistics.Gravity * distance * distance / (2.0 * muzzleVelocity * muzzleVelocity * cos * cos));
        }

        /// <summary>
        /// Altura a la que pasa el proyectil por el blanco cuando la pieza, apuntada para <paramref name="range"/>
        /// metros, dispara con <paramref name="errorDeg"/> grados de más (positivo: pasa por encima; negativo: cae corto).
        /// </summary>
        public static float MissHeight(float muzzleVelocity, float range, float errorDeg)
        {
            if (!Ballistics.TrySolveElevation(muzzleVelocity, range, out float elevation)) return float.NaN;
            return HeightAt(muzzleVelocity, elevation + errorDeg, range);
        }

        /// <summary>
        /// Dónde cae el tiro con el error de balanceo: distancia sobre el agua (m) con la elevación corregida.
        /// Para la retícula del jugador («CORTO / BLANCO / LARGO»).
        /// </summary>
        public static float FallRange(float muzzleVelocity, float range, float errorDeg)
        {
            if (!Ballistics.TrySolveElevation(muzzleVelocity, range, out float elevation)) return float.NaN;
            return Ballistics.Range(muzzleVelocity, Math.Max(0f, elevation + errorDeg));
        }

        /// <summary>
        /// Fracción del ciclo de balanceo en la que un disparo pasaría por el blanco (altura entre −
        /// <paramref name="belowM"/> y + <paramref name="aboveM"/>). Útil para calibrar la dificultad.
        /// </summary>
        public float HitWindowFraction(float muzzleVelocity, float range, float belowM, float aboveM, int samples = 3600)
        {
            int hits = 0;
            for (int i = 0; i < samples; i++)
            {
                float error = AngleAt(PeriodSeconds * i / samples);
                float h = MissHeight(muzzleVelocity, range, error);
                if (h >= -belowM && h <= aboveM) hits++;
            }
            return hits / (float)samples;
        }
    }
}
