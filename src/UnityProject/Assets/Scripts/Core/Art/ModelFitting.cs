using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Art
{
    /// <summary>
    /// Cálculos para colocar en escena los modelos y texturas CC0 (AGENTS.md §4.A): escala, orientación y
    /// coordenadas de textura. Sin dependencias del motor para poder probarlos.
    /// </summary>
    public static class ModelFitting
    {
        /// <summary>Escala uniforme que da al modelo la altura pedida (1 si el modelo no tiene altura).</summary>
        public static float ScaleToHeight(float height, float targetHeight) => height > 1e-4f ? targetHeight / height : 1f;

        /// <summary>
        /// Giro en grados alrededor del eje vertical (sentido de Unity: de +Z hacia +X) que lleva la dirección
        /// horizontal (fromX, fromZ) a (toX, toZ). 0 si alguna de las dos es nula.
        /// </summary>
        public static float YawToFace(float fromX, float fromZ, float toX, float toZ)
        {
            if (fromX * fromX + fromZ * fromZ < 1e-12f || toX * toX + toZ * toZ < 1e-12f) return 0f;
            float from = (float)Math.Atan2(fromX, fromZ) * MathUtil.Rad2Deg;
            float to = (float)Math.Atan2(toX, toZ) * MathUtil.Rad2Deg;
            return MathUtil.WrapAngle180(to - from);
        }

        /// <summary>
        /// Proyección en caja: la textura se repite cada <paramref name="tileMeters"/> metros sobre el plano
        /// perpendicular al eje dominante de la normal, para que una primitiva estirada (una costa de 5 km, un casco
        /// de 60 m) no deforme la textura.
        /// </summary>
        public static void BoxUv(float px, float py, float pz, float nx, float ny, float nz, float tileMeters, out float u, out float v)
        {
            float ax = Math.Abs(nx), ay = Math.Abs(ny), az = Math.Abs(nz);
            if (ay >= ax && ay >= az)
            {
                u = px;
                v = pz;
            }
            else if (ax >= az)
            {
                u = pz;
                v = py;
            }
            else
            {
                u = px;
                v = py;
            }
            float tile = tileMeters > 1e-4f ? tileMeters : 1f;
            u /= tile;
            v /= tile;
        }
    }
}
