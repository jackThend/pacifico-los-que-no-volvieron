using System;

namespace Pacifico.Core.Common
{
    /// <summary>
    /// Utilidades matemáticas sin dependencias del motor, equivalentes a las de UnityEngine.Mathf,
    /// para que la simulación pueda probarse fuera de Unity.
    /// </summary>
    public static class MathUtil
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float InverseLerp(float a, float b, float value)
        {
            if (Math.Abs(b - a) < 1e-6f) return 0f;
            return Clamp01((value - a) / (b - a));
        }

        /// <summary>Avanza <paramref name="current"/> hacia <paramref name="target"/> sin superar <paramref name="maxDelta"/>.</summary>
        /// <summary>Interpolación de Hermite entre dos umbrales (0 antes de <paramref name="edge0"/>, 1 después de <paramref name="edge1"/>).</summary>
        public static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Función error (Abramowitz y Stegun 7.1.26, error absoluto &lt; 1,5·10⁻⁷).</summary>
        public static float Erf(float x)
        {
            double sign = x < 0f ? -1.0 : 1.0;
            double ax = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.3275911 * ax);
            double y = 1.0 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-ax * ax);
            return (float)(sign * y);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + Math.Sign(delta) * maxDelta;
        }

        /// <summary>Normaliza un ángulo al rango (-180, 180].</summary>
        public static float WrapAngle180(float degrees)
        {
            float a = degrees % 360f;
            if (a > 180f) a -= 360f;
            else if (a <= -180f) a += 360f;
            return a;
        }

        /// <summary>Normaliza un ángulo al rango [0, 360).</summary>
        public static float WrapAngle360(float degrees)
        {
            float a = degrees % 360f;
            if (a < 0f) a += 360f;
            return a;
        }

        /// <summary>Diferencia angular más corta de <paramref name="from"/> a <paramref name="to"/>, en (-180, 180].</summary>
        public static float DeltaAngle(float from, float to) => WrapAngle180(to - from);

        /// <summary>Gira <paramref name="current"/> hacia <paramref name="target"/> por el camino más corto.</summary>
        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + Math.Sign(delta) * maxDelta;
        }

        /// <summary>
        /// Suavizado críticamente amortiguado (Game Programming Gems 4, §1.10; el mismo que Mathf.SmoothDamp):
        /// continuo en posición y velocidad, sin sobreimpulso apreciable y estable con cualquier paso de tiempo.
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float dt)
        {
            smoothTime = Math.Max(1e-4f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            // Evita sobrepasar el objetivo por errores numéricos.
            if ((target - current > 0f) == (output > target))
            {
                output = target;
                velocity = 0f;
            }
            return output;
        }

        public static bool Approximately(float a, float b, float epsilon = 1e-4f) => Math.Abs(a - b) <= epsilon;
    }
}
