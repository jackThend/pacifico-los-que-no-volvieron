using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    /// <summary>Dirección de salida de una bala respecto a la línea de mira (grados).</summary>
    public struct ShotDirection
    {
        /// <summary>Elevación sobre la línea de mira (incluye la del alza).</summary>
        public float PitchDeg;
        /// <summary>Desviación lateral (+ derecha).</summary>
        public float YawDeg;
    }

    /// <summary>
    /// Dirección de cada disparo (ROADMAP 3.2): la elevación del alza más la dispersión del arma. Con el arma encarada
    /// la dispersión es la de la ficha (<see cref="WeaponSpec.DispersionMoa"/> se interpreta como el diámetro del grupo,
    /// ≈ 4 desviaciones típicas); a la cadera y en movimiento es mucho mayor.
    /// </summary>
    public static class RifleShotSolver
    {
        /// <summary>Desviación típica a la cadera (°): sin miras, el tiro es instintivo.</summary>
        public const float HipSigmaDeg = 1.2f;
        /// <summary>Desviación típica adicional al moverse (°), reducida a la mitad con el arma encarada.</summary>
        public const float MovingSigmaDeg = 0.5f;
        /// <summary>Desviación típica adicional durante el encare incompleto (disparar mientras se levanta el fusil).</summary>
        public const float UnsteadySigmaDeg = 0.4f;

        /// <summary>Desviación típica angular (°) del disparo en las condiciones dadas.</summary>
        public static float SigmaDeg(WeaponSpec weapon, float aim01, bool moving)
        {
            float aimed = weapon.DispersionMoa / 60f / 4f;
            float sigma = MathUtil.Lerp(HipSigmaDeg, aimed, aim01);
            if (aim01 > 0f && aim01 < 1f) sigma += UnsteadySigmaDeg * (1f - aim01) * aim01 * 4f;
            if (moving) sigma += MovingSigmaDeg * MathUtil.Lerp(1f, 0.5f, aim01);
            return sigma;
        }

        public static ShotDirection Solve(WeaponSpec weapon, float ladderElevationDeg, float aim01, bool moving, Random random)
        {
            float sigma = SigmaDeg(weapon, aim01, moving);
            return new ShotDirection
            {
                PitchDeg = ladderElevationDeg + Gaussian(random) * sigma,
                YawDeg = Gaussian(random) * sigma,
            };
        }

        /// <summary>Normal estándar (Box-Muller) truncada a ±3σ.</summary>
        public static float Gaussian(Random random)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (float)Math.Max(-3.0, Math.Min(3.0, z));
        }
    }
}
