using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Apuntado con miras abiertas (ROADMAP 3.1): transición cadera ↔ miras, ligero acercamiento del campo visual
    /// (las miras de hierro no amplían: es solo la concentración del tirador) y deriva de la puntería por la
    /// respiración, que crece con la fatiga y el movimiento y se reduce agachado.
    /// </summary>
    public sealed class IronSightModel
    {
        /// <summary>Acercamiento del campo visual al apuntar (1 = ninguno).</summary>
        public const float ZoomFactor = 1.25f;
        /// <summary>Frecuencia de la respiración en reposo (Hz).</summary>
        public const float BreathingHz = 0.25f;
        public const float BaseSwayDeg = 0.12f;

        private float _swayAmplitude;
        private double _breathPhase;
        private float _breathRate = (float)(2.0 * Math.PI * BreathingHz);
        private float _swayVelocity;
        private float _breathRateVelocity;

        public IronSightModel(WeaponSpec weapon)
        {
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            // Un fusil más pesado tarda más en encararse.
            AimTimeSeconds = 0.18f + 0.03f * Math.Max(0f, weapon.MassKg);
            _swayAmplitude = BaseSwayDeg;
        }

        public WeaponSpec Weapon { get; }
        public float AimTimeSeconds { get; }

        /// <summary>Progreso lineal del encare [0, 1].</summary>
        public float Progress { get; private set; }

        /// <summary>Progreso suavizado (smoothstep) para animar el arma y la cámara sin tirones.</summary>
        public float Eased => Progress * Progress * (3f - 2f * Progress);

        public bool IsFullyAimed => Progress >= 1f;

        public float FovMultiplier => MathUtil.Lerp(1f, 1f / ZoomFactor, Eased);

        public float SwayAmplitudeDeg => _swayAmplitude;
        public float SwayYawDeg { get; private set; }
        public float SwayPitchDeg { get; private set; }

        public void Step(bool aimHeld, float dt, float stamina01, bool moving, bool crouched)
        {
            if (dt <= 0f) return;

            float rate = dt / AimTimeSeconds;
            Progress = MathUtil.MoveTowards(Progress, aimHeld ? 1f : 0f, rate);

            float fatigue = 1f + 2.5f * (1f - MathUtil.Clamp01(stamina01));
            float target = BaseSwayDeg * fatigue * (moving ? 2f : 1f) * (crouched ? 0.65f : 1f);
            _swayAmplitude = MathUtil.SmoothDamp(_swayAmplitude, target, ref _swayVelocity, 0.6f, dt);

            // Lissajous 1:2: el punto de mira dibuja un «ocho», como al respirar con el fusil encarado. La fatiga
            // acelera la respiración; la fase se acumula paso a paso para que un cambio de ritmo no provoque saltos.
            float targetRate = (float)(2.0 * Math.PI * BreathingHz) * (1f + 0.6f * (fatigue - 1f));
            _breathRate = MathUtil.SmoothDamp(_breathRate, targetRate, ref _breathRateVelocity, 1.2f, dt);
            _breathPhase = (_breathPhase + _breathRate * dt) % (2.0 * Math.PI * 1000.0);
            SwayYawDeg = _swayAmplitude * (float)Math.Sin(_breathPhase);
            SwayPitchDeg = _swayAmplitude * 0.7f * (float)Math.Sin(2.0 * _breathPhase + 0.6);
        }
    }
}
