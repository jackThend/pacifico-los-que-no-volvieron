using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Infantry
{
    /// <summary>
    /// Cabeceo de cámara (ROADMAP 3.1). Realista pero sin marear:
    /// <list type="bullet">
    /// <item>La fase avanza con la <b>distancia recorrida</b> (un paso = medio ciclo), no con el tiempo:
    /// al frenar, el balanceo se detiene donde está en lugar de seguir oscilando en el sitio. El paso se alarga con
    /// la velocidad, como en la marcha humana (≈0,9 m caminando, ≈1,5 m a la carrera: 3–3,7 pasos/s).</item>
    /// <item>Solo funciones seno (derivada continua): no hay picos ni saltos de un fotograma a otro.</item>
    /// <item>La intensidad sigue a la velocidad con suavizado exponencial y se reduce al apuntar.</item>
    /// <item>El golpe de aterrizaje es un muelle amortiguado integrado en subpasos fijos: se comporta igual a 30 o a 144 FPS.</item>
    /// </list>
    /// </summary>
    public sealed class HeadBobModel
    {
        /// <summary>Longitud del paso a velocidad nula (m); crece con <see cref="StepLengthPerSpeed"/>.</summary>
        public float BaseStepLength { get; set; } = 0.45f;
        /// <summary>Alargamiento del paso por cada m/s de velocidad (s).</summary>
        public float StepLengthPerSpeed { get; set; } = 0.19f;
        public float VerticalAmplitude { get; set; } = 0.045f;
        public float LateralAmplitude { get; set; } = 0.028f;
        public float RollAmplitudeDeg { get; set; } = 0.7f;
        /// <summary>Fracción del cabeceo que se conserva al apuntar con las miras.</summary>
        public float AimFactor { get; set; } = 0.2f;
        /// <summary>Velocidad a la que el cabeceo alcanza su intensidad máxima (m/s).</summary>
        public float ReferenceSpeed { get; set; } = 5.4f;
        public float IntensitySmoothingSeconds { get; set; } = 0.12f;
        public float StrafeRollDeg { get; set; } = 1.2f;

        // Muelle del aterrizaje.
        public float LandingSpringStiffness { get; set; } = 140f;
        public float LandingDampingRatio { get; set; } = 0.75f;
        /// <summary>Metros de hundimiento por cada m/s de velocidad de impacto.</summary>
        public float LandingDipPerSpeed { get; set; } = 0.018f;
        public float MaxLandingDip { get; set; } = 0.12f;

        private const float SpringSubstep = 1f / 240f;

        private float _phase;
        private float _intensity;
        private float _smoothedSpeed;
        private float _roll;
        private float _landingOffset;
        private float _landingVelocity;

        /// <summary>Desplazamiento lateral de la cámara (m, + derecha).</summary>
        public float OffsetX { get; private set; }
        /// <summary>Desplazamiento vertical de la cámara (m).</summary>
        public float OffsetY { get; private set; }
        public float RollDeg { get; private set; }
        /// <summary>Cabeceo adicional por el aterrizaje (°, + mira abajo).</summary>
        public float PitchDeg { get; private set; }
        public float Intensity => _intensity;
        public float Phase => _phase;

        /// <summary>Longitud del paso (m) a una velocidad dada.</summary>
        public float StepLengthAt(float speed) => BaseStepLength + StepLengthPerSpeed * Math.Max(0f, speed);

        /// <summary>Se llama al tocar suelo con la velocidad vertical (negativa) del impacto.</summary>
        public void Land(float verticalSpeed)
        {
            float dip = Math.Min(MaxLandingDip, Math.Abs(verticalSpeed) * LandingDipPerSpeed);
            // Impulso hacia abajo: velocidad inicial que produce aproximadamente ese hundimiento.
            float omega = (float)Math.Sqrt(LandingSpringStiffness);
            _landingVelocity -= dip * omega;
        }

        /// <param name="horizontalSpeed">Velocidad horizontal (m/s).</param>
        /// <param name="grounded">En el aire no hay paso.</param>
        /// <param name="aim01">Progreso del apuntado con miras [0, 1].</param>
        /// <param name="strafe">Entrada lateral [-1, 1] para inclinar la cabeza al desplazarse de lado.</param>
        public void Step(float horizontalSpeed, bool grounded, float aim01, float strafe, float dt)
        {
            if (dt <= 0f) return;

            // Fase e intensidad siguen a una velocidad suavizada: un frenazo en seco no congela el paso de golpe
            // (lo que produciría un quiebro visible), sino que el último paso se completa con naturalidad.
            float smoothing = 1f - (float)Math.Exp(-dt / IntensitySmoothingSeconds);
            float effectiveSpeed = grounded ? Math.Max(0f, horizontalSpeed) : 0f;
            _smoothedSpeed += (effectiveSpeed - _smoothedSpeed) * smoothing;
            _intensity = MathUtil.Clamp01(_smoothedSpeed / ReferenceSpeed);

            if (grounded)
            {
                _phase += _smoothedSpeed * dt / StepLengthAt(_smoothedSpeed) * (float)Math.PI;
                if (_phase > 1000f * Math.PI) _phase -= (float)(1000.0 * Math.PI); // evita perder precisión
            }

            float amount = _intensity * MathUtil.Lerp(1f, AimFactor, aim01);
            float sin = (float)Math.Sin(_phase);
            // Vertical: un valle por paso (periodo π). Lateral y alabeo: un vaivén por zancada (periodo 2π).
            float vertical = -VerticalAmplitude * amount * sin * sin;
            OffsetX = LateralAmplitude * amount * sin;

            float targetRoll = -MathUtil.Clamp(strafe, -1f, 1f) * StrafeRollDeg * MathUtil.Lerp(1f, AimFactor, aim01);
            _roll += (targetRoll - _roll) * (1f - (float)Math.Exp(-dt / 0.15f));
            RollDeg = RollAmplitudeDeg * amount * sin + _roll;

            StepLandingSpring(dt);
            OffsetY = vertical + _landingOffset;
            PitchDeg = -_landingOffset * 25f;
        }

        private void StepLandingSpring(float dt)
        {
            float k = LandingSpringStiffness;
            float c = 2f * LandingDampingRatio * (float)Math.Sqrt(k);
            float remaining = dt;
            while (remaining > 1e-6f)
            {
                float h = Math.Min(SpringSubstep, remaining);
                // Euler semi-implícito: estable para muelles con este paso.
                _landingVelocity += (-k * _landingOffset - c * _landingVelocity) * h;
                _landingOffset += _landingVelocity * h;
                remaining -= h;
            }
            if (Math.Abs(_landingOffset) < 1e-5f && Math.Abs(_landingVelocity) < 1e-4f)
            {
                _landingOffset = 0f;
                _landingVelocity = 0f;
            }
        }
    }
}
