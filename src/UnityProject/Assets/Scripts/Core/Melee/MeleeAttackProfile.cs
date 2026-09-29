using System;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;

namespace Pacifico.Core.Melee
{
    public enum MeleeAttackKind
    {
        /// <summary>Estocada: la punta avanza en línea recta hacia donde se mira.</summary>
        Thrust = 0,
        /// <summary>Tajo: la hoja barre un arco en diagonal, de arriba a la derecha hacia abajo a la izquierda.</summary>
        Slash = 1,
    }

    /// <summary>
    /// Un golpe cuerpo a cuerpo: tiempos (preparación, golpe, recuperación) y trayectoria de la hoja en coordenadas
    /// de vista. La trayectoria depende de un único parámetro de carrera σ: −1 armado (preparación completa), 0 en
    /// guardia, +1 extensión completa o final del arco. La misma función sirve para detectar impactos y para
    /// animar el arma en primera persona, de modo que lo que se ve es exactamente lo que golpea.
    /// </summary>
    public sealed class MeleeAttackProfile
    {
        private Vec3 _restTip;
        private Vec3 _cockedTip;
        private Vec3 _extendedTip;
        private Vec3 _thrustAxis;

        private Vec3 _shoulder;
        private Vec3 _arcSide;
        private float _tipRadius;
        private float _thetaMidDeg;
        private float _thetaSpanDeg;

        private MeleeAttackProfile() { }

        public string Name { get; private set; } = string.Empty;
        public MeleeAttackKind Kind { get; private set; }
        public WeaponSpec Weapon { get; private set; }
        /// <summary>Distancia máxima del ojo a la punta de la hoja: el <see cref="WeaponSpec.MeleeReachM"/>.</summary>
        public float ReachM { get; private set; }
        public float Damage { get; private set; }
        public float BladeLengthM { get; private set; }
        public float WindupSeconds { get; private set; }
        public float ActiveSeconds { get; private set; }
        public float RecoverySeconds { get; private set; }
        /// <summary>Cuántos cuerpos puede atravesar: la estocada se queda en el primero; el tajo puede alcanzar dos.</summary>
        public int MaxTargets { get; private set; }
        /// <summary>Congelación del golpe al impactar («hit-stop»): da peso al contacto.</summary>
        public float HitStopSeconds { get; private set; } = 0.07f;

        public float CycleSeconds => WindupSeconds + ActiveSeconds + RecoverySeconds;

        // ------------------------------------------------------------------------------------------
        // Catálogo de golpes
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Estocada con la bayoneta calada. La punta parte de la guardia (fusil a la cintura, ~1,55 m del ojo), se
        /// retrae para tomar impulso y se proyecta hasta el alcance del arma, en el centro de la mira.
        /// </summary>
        public static MeleeAttackProfile BayonetThrust(WeaponSpec rifle)
        {
            if (rifle == null) throw new ArgumentNullException(nameof(rifle));
            if (rifle.MeleeReachM <= 0f) throw new ArgumentException("El arma " + rifle.Id + " no admite bayoneta.", nameof(rifle));
            return Thrust("Estocada", rifle, bladeLength: 0.5f, restDistance: 1.55f, restOffset: new Vec3(0.1f, -0.12f, 0f),
                windupFraction: 0.3f, activeFraction: 0.2f);
        }

        /// <summary>Estocada con el corvo: el brazo se estira desde la guardia hasta el alcance de la hoja.</summary>
        public static MeleeAttackProfile BladeThrust(WeaponSpec blade)
        {
            if (blade == null) throw new ArgumentNullException(nameof(blade));
            if (blade.MeleeReachM <= 0f) throw new ArgumentException("El arma " + blade.Id + " no tiene alcance cuerpo a cuerpo.", nameof(blade));
            return Thrust("Estocada", blade, bladeLength: 0.3f, restDistance: 0.45f, restOffset: new Vec3(0.1f, -0.16f, 0f),
                windupFraction: 0.3f, activeFraction: 0.2f);
        }

        /// <summary>
        /// Tajo con el corvo: arco diagonal de 180° alrededor del hombro derecho. El radio de la punta se elige para
        /// que su distancia máxima al ojo sea exactamente el alcance del arma.
        /// </summary>
        public static MeleeAttackProfile BladeSlash(WeaponSpec blade)
        {
            if (blade == null) throw new ArgumentNullException(nameof(blade));
            if (blade.MeleeReachM <= 0f) throw new ArgumentException("El arma " + blade.Id + " no tiene alcance cuerpo a cuerpo.", nameof(blade));

            var p = new MeleeAttackProfile
            {
                Name = "Tajo",
                Kind = MeleeAttackKind.Slash,
                Weapon = blade,
                ReachM = blade.MeleeReachM,
                Damage = blade.MeleeDamage,
                BladeLengthM = 0.3f,
                MaxTargets = 2,
                _shoulder = new Vec3(0.18f, -0.2f, 0f),
                // Plano del corte: inclinado ~24° (de arriba a la derecha a abajo a la izquierda).
                _arcSide = new Vec3(1f, 0.45f, 0f).Normalized,
                // σ = −1 → 100° (armado junto a la cabeza, sin llegar a la espalda); σ = +1 → −80° (terminado abajo a
                // la izquierda). Así el tajo no alcanza lo que el soldado tiene detrás.
                _thetaMidDeg = 10f,
                _thetaSpanDeg = -90f,
            };
            // Radio de la punta tal que la distancia máxima de la punta al ojo en todo el arco sea el alcance
            // (bisección: la distancia crece con el radio).
            float lo = 0.05f, hi = p.ReachM * 2f;
            for (int i = 0; i < 40; i++)
            {
                p._tipRadius = 0.5f * (lo + hi);
                if (p.MaxTipDistance(400) > p.ReachM) hi = p._tipRadius;
                else lo = p._tipRadius;
            }
            p._tipRadius = lo;
            SetTiming(p, blade.MeleeCycleSeconds, 0.3f, 0.2f);
            return p;
        }

        private static MeleeAttackProfile Thrust(string name, WeaponSpec weapon, float bladeLength, float restDistance, Vec3 restOffset,
                                                 float windupFraction, float activeFraction)
        {
            float reach = weapon.MeleeReachM;
            var p = new MeleeAttackProfile
            {
                Name = name,
                Kind = MeleeAttackKind.Thrust,
                Weapon = weapon,
                ReachM = reach,
                Damage = weapon.MeleeDamage,
                BladeLengthM = bladeLength,
                MaxTargets = 1,
            };
            // La guardia deja al menos 25 cm de recorrido hasta el alcance.
            float rest = Math.Min(restDistance, reach - 0.25f);
            p._restTip = new Vec3(restOffset.X, restOffset.Y, rest);
            p._cockedTip = new Vec3(restOffset.X * 1.3f, restOffset.Y * 1.1f, rest - Math.Min(0.2f, rest * 0.3f));
            // Extensión completa: la punta en el centro de la mira (2 cm por debajo), a la distancia del alcance.
            const float below = 0.02f;
            p._extendedTip = new Vec3(0f, -below, (float)Math.Sqrt(reach * reach - below * below));
            // La hoja apunta de la guardia a la extensión (de la cadera hacia el centro).
            p._thrustAxis = (p._extendedTip - p._restTip + new Vec3(0f, 0f, 0.5f)).Normalized;
            SetTiming(p, weapon.MeleeCycleSeconds, windupFraction, activeFraction);
            return p;
        }

        private static void SetTiming(MeleeAttackProfile p, float cycle, float windupFraction, float activeFraction)
        {
            p.WindupSeconds = cycle * windupFraction;
            p.ActiveSeconds = cycle * activeFraction;
            p.RecoverySeconds = cycle - p.WindupSeconds - p.ActiveSeconds;
        }

        // ------------------------------------------------------------------------------------------
        // Trayectoria
        // ------------------------------------------------------------------------------------------

        /// <summary>Carrera σ durante el golpe (fase activa): de −1 a +1.</summary>
        public float ActiveStroke(float progress)
        {
            float p = MathUtil.Clamp01(progress);
            // Estocada: arranque explosivo que se frena al llegar a la extensión (la punta llega exacta al alcance).
            // Tajo: acelera y frena (el arco no se detiene de golpe en el cuerpo).
            float eased = Kind == MeleeAttackKind.Thrust ? 1f - (1f - p) * (1f - p) : p * p * (3f - 2f * p);
            return -1f + 2f * eased;
        }

        /// <summary>Carrera σ durante la preparación: de 0 a −1.</summary>
        public static float WindupStroke(float progress)
        {
            float p = MathUtil.Clamp01(progress);
            return -(p * p * (3f - 2f * p));
        }

        /// <summary>Carrera σ durante la recuperación, desde <paramref name="from"/> hasta la guardia.</summary>
        public static float RecoveryStroke(float progress, float from)
        {
            float p = MathUtil.Clamp01(progress);
            return from * (1f - p * p * (3f - 2f * p));
        }

        /// <summary>Empuñadura y punta de la hoja en coordenadas de vista para una carrera σ ∈ [−1, 1].</summary>
        public void Pose(float stroke, out Vec3 hilt, out Vec3 tip)
        {
            float s = MathUtil.Clamp(stroke, -1f, 1f);
            if (Kind == MeleeAttackKind.Thrust)
            {
                tip = s < 0f ? Lerp(_restTip, _cockedTip, -s) : Lerp(_restTip, _extendedTip, s);
                hilt = tip - _thrustAxis * BladeLengthM;
                return;
            }

            double theta = (_thetaMidDeg + _thetaSpanDeg * s) * MathUtil.Deg2Rad;
            // El brazo se recoge al armar el golpe y se estira del todo en el corte.
            float radius = _tipRadius * (s < 0f ? 1f - 0.15f * -s : 1f);
            Vec3 forward = new Vec3(0f, 0f, 1f);
            Vec3 u = _arcSide * (float)Math.Sin(theta) + forward * (float)Math.Cos(theta);
            tip = _shoulder + u * radius;
            hilt = _shoulder + u * (radius - BladeLengthM);
        }

        /// <summary>Distancia máxima del ojo a la punta muestreando la carrera completa.</summary>
        public float MaxTipDistance(int samples = 400)
        {
            float best = 0f;
            for (int i = 0; i <= samples; i++)
            {
                Pose(-1f + 2f * i / samples, out _, out Vec3 tip);
                best = Math.Max(best, tip.Magnitude);
            }
            return best;
        }

        private static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;
    }
}
