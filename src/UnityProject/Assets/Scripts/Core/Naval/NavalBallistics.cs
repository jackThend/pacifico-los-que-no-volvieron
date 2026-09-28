using System;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Balística simplificada de artillería naval: trayectoria parabólica sin
    /// rozamiento desde la altura de los muñones hasta la flotación.
    /// Suficiente para retícula y puntería; el rebote/penetración va en 2.3.
    /// </summary>
    public static class NavalBallistics
    {
        public const float Gravity = 9.81f;
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Alcance horizontal (m) al disparar con esa elevación desde <paramref name="heightM"/> sobre el agua.</summary>
        public static float RangeForElevation(float muzzleVelocityMs, float elevationDegrees, float heightM)
        {
            var angle = elevationDegrees * DegToRad;
            var vx = muzzleVelocityMs * Math.Cos(angle);
            var vy = muzzleVelocityMs * Math.Sin(angle);
            // y(t) = h + vy·t − g·t²/2 = 0  →  raíz positiva.
            var time = (vy + Math.Sqrt(vy * vy + 2.0 * Gravity * Math.Max(heightM, 0f))) / Gravity;
            return (float)(vx * time);
        }

        /// <summary>Ángulo de caída sobre la horizontal al llegar al agua (grados, positivo hacia abajo).</summary>
        public static float FallAngleDegrees(float muzzleVelocityMs, float elevationDegrees, float heightM)
        {
            var angle = elevationDegrees * DegToRad;
            var vx = muzzleVelocityMs * Math.Cos(angle);
            var vy = muzzleVelocityMs * Math.Sin(angle);
            var time = (vy + Math.Sqrt(vy * vy + 2.0 * Gravity * Math.Max(heightM, 0f))) / Gravity;
            var vyImpact = vy - Gravity * time;
            return (float)(Math.Atan2(-vyImpact, vx) / DegToRad);
        }

        /// <summary>
        /// Elevación necesaria para alcanzar <paramref name="rangeM"/>, limitada a
        /// [minElevation, maxElevation]. <paramref name="reachable"/> es false si el
        /// blanco queda fuera de ese intervalo (demasiado cerca o demasiado lejos).
        /// </summary>
        public static float ElevationForRange(float muzzleVelocityMs, float rangeM, float heightM,
            float minElevationDegrees, float maxElevationDegrees, out bool reachable)
        {
            var minRange = RangeForElevation(muzzleVelocityMs, minElevationDegrees, heightM);
            var maxRange = RangeForElevation(muzzleVelocityMs, maxElevationDegrees, heightM);
            if (rangeM <= minRange)
            {
                reachable = rangeM >= minRange - 1f;
                return minElevationDegrees;
            }
            if (rangeM >= maxRange)
            {
                reachable = rangeM <= maxRange + 1f;
                return maxElevationDegrees;
            }

            // El alcance crece monótonamente con la elevación por debajo de 45°: bisección.
            float low = minElevationDegrees, high = maxElevationDegrees;
            for (var i = 0; i < 40; i++)
            {
                var mid = 0.5f * (low + high);
                if (RangeForElevation(muzzleVelocityMs, mid, heightM) < rangeM) low = mid; else high = mid;
            }
            reachable = true;
            return 0.5f * (low + high);
        }
    }
}
