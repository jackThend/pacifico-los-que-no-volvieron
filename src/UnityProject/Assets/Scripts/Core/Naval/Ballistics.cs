using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Balística parabólica sin rozamiento para la artillería naval (a las distancias de Iquique,
    /// 200–2.000 m, el tiro es casi tenso y el modelo es suficiente para la jugabilidad).
    /// </summary>
    public static class Ballistics
    {
        public const float Gravity = 9.81f;

        /// <summary>Alcance sobre el mar (misma cota de salida y llegada) para una elevación dada.</summary>
        public static float Range(float muzzleVelocity, float elevationDeg)
        {
            float twoTheta = 2f * elevationDeg * MathUtil.Deg2Rad;
            return Math.Max(0f, muzzleVelocity * muzzleVelocity * (float)Math.Sin(twoTheta) / Gravity);
        }

        public static float MaxRange(float muzzleVelocity) => muzzleVelocity * muzzleVelocity / Gravity;

        /// <summary>
        /// Elevación de tiro tenso (ángulo bajo) para alcanzar <paramref name="range"/> metros.
        /// Devuelve false si el blanco está más allá del alcance máximo teórico.
        /// </summary>
        public static bool TrySolveElevation(float muzzleVelocity, float range, out float elevationDeg)
        {
            float s = Gravity * range / (muzzleVelocity * muzzleVelocity);
            if (range < 0f || s > 1f)
            {
                elevationDeg = 45f;
                return false;
            }
            elevationDeg = 0.5f * (float)Math.Asin(s) * MathUtil.Rad2Deg;
            return true;
        }

        public static float FlightTime(float muzzleVelocity, float elevationDeg)
        {
            return 2f * muzzleVelocity * (float)Math.Sin(elevationDeg * MathUtil.Deg2Rad) / Gravity;
        }

        /// <summary>Velocidad remanente y ángulo de caída al llegar al agua (tiro sin rozamiento: simétrico).</summary>
        public static float ImpactAngleDeg(float elevationDeg) => elevationDeg;

        /// <summary>
        /// Punto de puntería con corrección por el movimiento del blanco (tiro con adelanto).
        /// Itera sobre el tiempo de vuelo; converge en pocas iteraciones porque el blanco es lento frente al proyectil.
        /// </summary>
        public static bool TryLead(float shooterX, float shooterZ, float targetX, float targetZ,
                                   float targetVelX, float targetVelZ, float muzzleVelocity,
                                   out float aimX, out float aimZ, out float flightTime)
        {
            aimX = targetX;
            aimZ = targetZ;
            flightTime = 0f;
            for (int i = 0; i < 8; i++)
            {
                float range = Distance(shooterX, shooterZ, aimX, aimZ);
                if (!TrySolveElevation(muzzleVelocity, range, out float elevation)) return false;
                flightTime = FlightTime(muzzleVelocity, elevation);
                aimX = targetX + targetVelX * flightTime;
                aimZ = targetZ + targetVelZ * flightTime;
            }
            return true;
        }

        /// <summary>Rumbo (°, horario desde +Z) de un punto a otro.</summary>
        public static float Bearing(float fromX, float fromZ, float toX, float toZ)
        {
            return MathUtil.WrapAngle360((float)Math.Atan2(toX - fromX, toZ - fromZ) * MathUtil.Rad2Deg);
        }

        public static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
