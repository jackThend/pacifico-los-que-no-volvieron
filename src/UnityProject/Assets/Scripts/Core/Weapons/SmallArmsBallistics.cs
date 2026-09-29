using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    /// <summary>Estado de una bala a cierta distancia horizontal.</summary>
    public struct TrajectorySample
    {
        public bool Reached;
        /// <summary>Altura respecto a la línea de mira horizontal (m).</summary>
        public float Height;
        public float TimeSeconds;
        public float Speed;
    }

    /// <summary>
    /// Balística exterior de las balas de plomo de pólvora negra: masa puntual con resistencia cuadrática
    /// (F = ½·ρ·Cd(M)·A·v²) integrada con Runge-Kutta de 2.º orden. El coeficiente de arrastre depende del número de
    /// Mach: alto en supersónico y mucho menor en subsónico, como en toda bala roma.
    /// <para>
    /// Calibración: el cartucho Gras de 1874 (bala de 25 g a 450 m/s) caía a 430 m/s a 25 m (Wikipedia, «11×59mmR Gras»),
    /// lo que implica Cd ≈ 0,8 en supersónico. El valor subsónico (0,20) se ajustó para que el alcance máximo se logre
    /// con 25–35° de elevación, como en los ensayos de Versalles del Gras (~3.000 m a ~35°); con 0,35 el alcance máximo
    /// apenas superaría los 2.200 m, inverosímil para un fusil con alza hasta 1.800 m.
    /// </para>
    /// </summary>
    public static class SmallArmsBallistics
    {
        public const float Gravity = 9.81f;
        /// <summary>Densidad del aire a nivel del mar, costa del Pacífico (kg/m³).</summary>
        public const float SeaLevelAirDensity = 1.2f;
        public const float SpeedOfSound = 340f;
        /// <summary>Cd supersónico de una bala roma de plomo (calibrado con el dato del Gras).</summary>
        public const float SupersonicDragCoefficient = 0.8f;
        /// <summary>Cd subsónico (estimación calibrada por alcance máximo; ver resumen de la clase).</summary>
        public const float SubsonicDragCoefficient = 0.2f;
        /// <summary>Altura típica de la línea de mira sobre el eje del cañón (m).</summary>
        public const float DefaultSightHeightM = 0.035f;

        private const float IntegrationStep = 0.004f;
        /// <summary>Suficiente para el tiro por elevación máxima (a 35° una bala de 11 mm vuela más de 20 s).</summary>
        private const float MaxFlightSeconds = 40f;

        /// <summary>
        /// Factor de arrastre sin Cd, k₀ = ρ·A / (2·m) en 1/m: la deceleración por rozamiento es k₀·Cd(M)·v².
        /// </summary>
        public static float DragFactor(float caliberMm, float bulletMassG, float airDensity = SeaLevelAirDensity)
        {
            if (caliberMm <= 0f || bulletMassG <= 0f) return 0f;
            double radius = caliberMm / 2000.0;
            double area = Math.PI * radius * radius;
            return (float)(airDensity * area / (2.0 * bulletMassG / 1000.0));
        }

        public static float DragFactor(WeaponSpec weapon) => DragFactor(weapon.CaliberMm, weapon.BulletMassG);

        /// <summary>Cd en función de la velocidad: subsónico hasta Mach 0,85, rampa transónica y supersónico desde Mach 1,2.</summary>
        public static float DragCoefficient(float speed)
        {
            float mach = speed / SpeedOfSound;
            float t = MathUtil.InverseLerp(0.85f, 1.2f, mach);
            return MathUtil.Lerp(SubsonicDragCoefficient, SupersonicDragCoefficient, t);
        }

        /// <summary>
        /// Simula el disparo con elevación <paramref name="elevationDeg"/> desde <paramref name="sightHeightM"/> bajo la
        /// línea de mira y devuelve el estado de la bala al alcanzar la distancia horizontal indicada.
        /// </summary>
        public static TrajectorySample Sample(float distanceM, float elevationDeg, float muzzleVelocity, float dragFactor,
                                              float sightHeightM = DefaultSightHeightM)
        {
            double theta = elevationDeg * MathUtil.Deg2Rad;
            double x = 0.0, y = -sightHeightM, t = 0.0;
            double vx = muzzleVelocity * Math.Cos(theta);
            double vy = muzzleVelocity * Math.Sin(theta);
            double h = IntegrationStep;

            while (t < MaxFlightSeconds)
            {
                // RK2 (punto medio).
                Accel(vx, vy, dragFactor, out double ax1, out double ay1);
                double mvx = vx + ax1 * h * 0.5, mvy = vy + ay1 * h * 0.5;
                Accel(mvx, mvy, dragFactor, out double ax2, out double ay2);
                double nx = x + mvx * h, ny = y + mvy * h;
                double nvx = vx + ax2 * h, nvy = vy + ay2 * h;

                if (nx >= distanceM)
                {
                    double f = (distanceM - x) / (nx - x);
                    return new TrajectorySample
                    {
                        Reached = true,
                        Height = (float)(y + (ny - y) * f),
                        TimeSeconds = (float)(t + h * f),
                        Speed = (float)Math.Sqrt(Sq(vx + (nvx - vx) * f) + Sq(vy + (nvy - vy) * f)),
                    };
                }
                if (nvx <= 0.0) break;
                x = nx; y = ny; vx = nvx; vy = nvy; t += h;
            }
            return new TrajectorySample { Reached = false, Height = (float)y, TimeSeconds = (float)t };
        }

        /// <summary>
        /// Elevación del cañón (°) para que la bala cruce la línea de mira a <paramref name="distanceM"/>:
        /// es lo que «hace» una graduación del alza. Devuelve false si no hay solución por debajo de 20°.
        /// </summary>
        public static bool TrySolveElevation(float distanceM, float muzzleVelocity, float dragFactor, out float elevationDeg,
                                             float sightHeightM = DefaultSightHeightM)
        {
            float lo = 0f, hi = 20f;
            TrajectorySample top = Sample(distanceM, hi, muzzleVelocity, dragFactor, sightHeightM);
            if (!top.Reached || top.Height < 0f)
            {
                elevationDeg = hi;
                return false;
            }
            for (int i = 0; i < 24; i++) // 20° / 2^24 ≈ 1e-6°: el límite útil de la precisión de float
            {
                float mid = 0.5f * (lo + hi);
                TrajectorySample s = Sample(distanceM, mid, muzzleVelocity, dragFactor, sightHeightM);
                if (s.Reached && s.Height >= 0f) hi = mid;
                else lo = mid;
            }
            elevationDeg = hi;
            return true;
        }

        /// <summary>
        /// Avanza una bala en 3D <paramref name="dt"/> segundos con el mismo integrador (RK2, subpasos de 4 ms) que
        /// usan <see cref="Sample"/> y el alza: la bala del juego cruza la línea de mira donde dice la graduación.
        /// </summary>
        public static void StepBullet(ref Vec3 position, ref Vec3 velocity, float dragFactor, float dt)
        {
            double px = position.X, py = position.Y, pz = position.Z;
            double vx = velocity.X, vy = velocity.Y, vz = velocity.Z;
            double remaining = dt;
            while (remaining > 1e-9)
            {
                double h = Math.Min(IntegrationStep, remaining);
                Accel3(vx, vy, vz, dragFactor, out double ax1, out double ay1, out double az1);
                double mvx = vx + ax1 * h * 0.5, mvy = vy + ay1 * h * 0.5, mvz = vz + az1 * h * 0.5;
                Accel3(mvx, mvy, mvz, dragFactor, out double ax2, out double ay2, out double az2);
                px += mvx * h; py += mvy * h; pz += mvz * h;
                vx += ax2 * h; vy += ay2 * h; vz += az2 * h;
                remaining -= h;
            }
            position = new Vec3((float)px, (float)py, (float)pz);
            velocity = new Vec3((float)vx, (float)vy, (float)vz);
        }

        private static void Accel3(double vx, double vy, double vz, double k, out double ax, out double ay, out double az)
        {
            double speed = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            double drag = k * DragCoefficient((float)speed) * speed;
            ax = -drag * vx;
            ay = -drag * vy - Gravity;
            az = -drag * vz;
        }

        private static void Accel(double vx, double vy, double k, out double ax, out double ay)
        {
            double speed = Math.Sqrt(vx * vx + vy * vy);
            double drag = k * DragCoefficient((float)speed) * speed;
            ax = -drag * vx;
            ay = -drag * vy - Gravity;
        }

        private static double Sq(double v) => v * v;
    }
}
