using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Retroceso (ROADMAP 3.2). Parte de la física: por conservación del momento, la velocidad de retroceso libre del
    /// fusil es <c>(m_bala·v_boca + m_pólvora·v_gases) / M_fusil</c>, con v_gases ≈ 1.200 m/s para pólvora negra
    /// (valor habitual en los cálculos de retroceso libre). El salto de la boca en pantalla es proporcional a esa
    /// velocidad y se recupera con un muelle críticamente amortiguado, avanzado con su solución exacta en forma cerrada
    /// (no una integración numérica): el resultado es idéntico a cualquier tasa de fotogramas.
    /// </summary>
    public sealed class RecoilModel
    {
        public const float PowderGasVelocity = 1200f;
        /// <summary>Grados de salto de la boca por m/s de velocidad de retroceso libre (ajuste de juego).</summary>
        public const float KickDegPerMps = 0.85f;
        /// <summary>Frecuencia natural del muelle de recuperación (rad/s): pico a 1/ω ≈ 80 ms.</summary>
        public const float RecoveryOmega = 12f;
        /// <summary>Fracción del salto con el arma encarada (el hombro y la mejilla sujetan el fusil).</summary>
        public const float AimedFactor = 0.8f;
        public const float CrouchedFactor = 0.85f;
        /// <summary>Desviación lateral aleatoria respecto al salto vertical.</summary>
        public const float LateralRatio = 0.25f;

        private readonly Random _random;
        private float _pitch, _pitchVelocity;
        private float _yaw, _yawVelocity;

        public RecoilModel(int seed = 1879)
        {
            _random = new Random(seed);
        }

        /// <summary>Salto vertical actual de la boca (°, positivo = arriba).</summary>
        public float PitchDeg => _pitch;
        /// <summary>Desviación lateral actual (°, positivo = derecha).</summary>
        public float YawDeg => _yaw;

        public static float FreeRecoilVelocity(WeaponSpec weapon)
        {
            if (weapon == null || !weapon.IsFirearm || weapon.MassKg <= 0f) return 0f;
            float momentum = weapon.BulletMassG / 1000f * weapon.MuzzleVelocityMps + weapon.PowderChargeG / 1000f * PowderGasVelocity;
            return momentum / weapon.MassKg;
        }

        /// <summary>Energía de retroceso libre (J), la medida con que se comparaban los fusiles.</summary>
        public static float FreeRecoilEnergy(WeaponSpec weapon)
        {
            if (weapon == null) return 0f;
            float v = FreeRecoilVelocity(weapon);
            return 0.5f * weapon.MassKg * v * v;
        }

        /// <summary>Salto nominal de la boca (°) de un disparo con el arma en la mano, sin apoyo.</summary>
        public static float KickDeg(WeaponSpec weapon) => FreeRecoilVelocity(weapon) * KickDegPerMps;

        /// <summary>Aplica el retroceso de un disparo.</summary>
        public void Kick(WeaponSpec weapon, float aim01, bool crouched)
        {
            float kick = KickDeg(weapon) * MathUtil.Lerp(1f, AimedFactor, aim01) * (crouched ? CrouchedFactor : 1f);
            // Muelle críticamente amortiguado: x(t) = v0·t·e^(−ωt) alcanza su máximo v0/(ω·e) en t = 1/ω.
            float impulse = kick * RecoveryOmega * (float)Math.E;
            float lateral = (float)(_random.NextDouble() * 2.0 - 1.0) * LateralRatio;
            _pitchVelocity += impulse;
            _yawVelocity += impulse * lateral;
        }

        /// <summary>
        /// Sacudida de la vista con picos exactos de <paramref name="pitchDeg"/> y <paramref name="yawDeg"/> grados
        /// (p. ej. el choque de la bayoneta contra un cuerpo). Usa el mismo muelle que el retroceso.
        /// </summary>
        public void Punch(float pitchDeg, float yawDeg)
        {
            float gain = RecoveryOmega * (float)Math.E;
            _pitchVelocity += pitchDeg * gain;
            _yawVelocity += yawDeg * gain;
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            Advance(ref _pitch, ref _pitchVelocity, dt);
            Advance(ref _yaw, ref _yawVelocity, dt);
        }

        /// <summary>
        /// Solución exacta de x'' + 2ωx' + ω²x = 0: x(t) = (x₀ + (v₀ + ωx₀)·t)·e^(−ωt).
        /// </summary>
        private static void Advance(ref float x, ref float v, float dt)
        {
            float w = RecoveryOmega;
            float decay = (float)Math.Exp(-w * dt);
            float c = v + w * x;
            float nx = (x + c * dt) * decay;
            float nv = (v - w * c * dt) * decay;
            x = nx;
            v = nv;
        }
    }
}
