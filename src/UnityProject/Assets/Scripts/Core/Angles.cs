namespace Pacifico.Core
{
    /// <summary>Utilidades de ángulos en grados (sin dependencias de UnityEngine.Mathf).</summary>
    public static class Angles
    {
        /// <summary>Normaliza a [0, 360).</summary>
        public static float Normalize360(float degrees)
        {
            var result = degrees % 360f;
            return result < 0f ? result + 360f : result;
        }

        /// <summary>Normaliza a (-180, 180].</summary>
        public static float Normalize180(float degrees)
        {
            var result = Normalize360(degrees);
            return result > 180f ? result - 360f : result;
        }

        /// <summary>Diferencia más corta de <paramref name="from"/> a <paramref name="to"/>, en (-180, 180].</summary>
        public static float DeltaDegrees(float from, float to) => Normalize180(to - from);
    }
}
