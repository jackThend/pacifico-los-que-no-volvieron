using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Melee
{
    public enum MeleePhase
    {
        Idle = 0,
        /// <summary>Preparación: se arma el golpe (no hiere).</summary>
        Windup = 1,
        /// <summary>Golpe: la hoja corta o penetra; es el único tramo que hiere.</summary>
        Active = 2,
        /// <summary>Recuperación: vuelta a la guardia (no hiere).</summary>
        Recovery = 3,
    }

    /// <summary>Una parte de un objetivo: cuerpo, cabeza… con su multiplicador de daño.</summary>
    public struct MeleeTargetShape
    {
        public int TargetId;
        public int PartId;
        public Capsule Shape;
        public float DamageMultiplier;

        public MeleeTargetShape(int targetId, int partId, Capsule shape, float damageMultiplier = 1f)
        {
            TargetId = targetId;
            PartId = partId;
            Shape = shape;
            DamageMultiplier = damageMultiplier;
        }
    }

    /// <summary>Impacto de un golpe cuerpo a cuerpo.</summary>
    public struct MeleeHit
    {
        public int TargetId;
        public int PartId;
        /// <summary>Punto de entrada de la hoja en la superficie.</summary>
        public Vec3 Point;
        /// <summary>Dirección del movimiento de la hoja en el contacto (para el retroceso del muñeco y los efectos).</summary>
        public Vec3 Direction;
        public float Depth;
        public float Damage;
        /// <summary>Instante del contacto desde el inicio del golpe (exacto a 1/480 s).</summary>
        public float Time;
        public MeleeAttackKind Kind;
    }

    /// <summary>
    /// Golpe cuerpo a cuerpo (ROADMAP 3.4) sin dependencias del motor. Recorre la trayectoria de la hoja de
    /// <see cref="MeleeAttackProfile"/> en pasos fijos de 1/480 s alineados con el inicio del golpe: el resultado
    /// (qué se toca, dónde y cuándo) es idéntico a 20, 60 o 144 FPS y la hoja no atraviesa un poste de 10 cm sin
    /// tocarlo aunque vaya a 20 m/s. En cada paso se comprueba el segmento empuñadura–punta y el recorrido de la
    /// punta contra las cápsulas de los objetivos; cada objetivo se hiere una sola vez por golpe.
    /// </summary>
    public sealed class MeleeAttackModel
    {
        public const float TickSeconds = 1f / 480f;

        private readonly List<MeleeHit> _hits = new List<MeleeHit>();
        private readonly HashSet<int> _struck = new HashSet<int>();
        private float _accumulator;
        private float _hitStop;
        private float _recoverFrom;
        private int _targetsHit;
        private bool _hasPreviousTip;
        private Vec3 _previousTip;

        public MeleeAttackProfile Profile { get; private set; }
        public MeleePhase Phase { get; private set; }
        public float PhaseElapsed { get; private set; }
        /// <summary>Tiempo desde el inicio del golpe, sin contar la congelación por impacto.</summary>
        public float AttackTime { get; private set; }
        /// <summary>Carrera σ actual (−1 armado, 0 guardia, +1 extensión o final del arco).</summary>
        public float Stroke { get; private set; }
        /// <summary>Queda congelado tras un impacto.</summary>
        public bool InHitStop => _hitStop > 0f;
        public bool IsBusy => Phase != MeleePhase.Idle;
        public int TargetsHit => _targetsHit;

        public float PhaseDuration
        {
            get
            {
                if (Profile == null) return 0f;
                switch (Phase)
                {
                    case MeleePhase.Windup: return Profile.WindupSeconds;
                    case MeleePhase.Active: return Profile.ActiveSeconds;
                    case MeleePhase.Recovery: return Profile.RecoverySeconds;
                    default: return 0f;
                }
            }
        }

        /// <summary>Empieza un golpe. Solo desde la guardia: un golpe no se interrumpe con otro.</summary>
        public bool Start(MeleeAttackProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (IsBusy) return false;
            Profile = profile;
            Phase = MeleePhase.Windup;
            PhaseElapsed = 0f;
            AttackTime = 0f;
            Stroke = 0f;
            _accumulator = 0f;
            _hitStop = 0f;
            _targetsHit = 0;
            _hasPreviousTip = false;
            _struck.Clear();
            return true;
        }

        /// <summary>Pose actual de la hoja en coordenadas de vista.</summary>
        public void CurrentPose(out Vec3 hilt, out Vec3 tip)
        {
            if (Profile == null)
            {
                hilt = tip = Vec3.Zero;
                return;
            }
            Profile.Pose(Stroke, out hilt, out tip);
        }

        /// <summary>
        /// Avanza el golpe. El ojo se interpola entre <paramref name="from"/> (inicio del fotograma) y
        /// <paramref name="to"/> (final); los objetivos se toman en su posición actual.
        /// </summary>
        public IReadOnlyList<MeleeHit> Step(float dt, ViewFrame from, ViewFrame to, IReadOnlyList<MeleeTargetShape> targets)
        {
            _hits.Clear();
            if (!IsBusy || dt <= 0f) return _hits;

            _accumulator += dt;
            float consumed = 0f;
            while (_accumulator >= TickSeconds && IsBusy)
            {
                _accumulator -= TickSeconds;
                consumed += TickSeconds;
                Tick(ViewFrame.Lerp(from, to, consumed / dt), targets);
            }
            if (!IsBusy) _accumulator = 0f;
            return _hits;
        }

        private void Tick(ViewFrame eye, IReadOnlyList<MeleeTargetShape> targets)
        {
            if (_hitStop > 0f)
            {
                _hitStop -= TickSeconds;
                return;
            }

            AttackTime += TickSeconds;
            PhaseElapsed += TickSeconds;
            // Margen de medio paso: evita que el redondeo de float deje una fase un paso de más o de menos.
            bool phaseEnds = PhaseElapsed >= PhaseDuration - TickSeconds * 0.5f;
            float progress = phaseEnds || PhaseDuration <= 0f ? 1f : PhaseElapsed / PhaseDuration;
            switch (Phase)
            {
                case MeleePhase.Windup:
                    Stroke = MeleeAttackProfile.WindupStroke(progress);
                    break;
                case MeleePhase.Active:
                    Stroke = Profile.ActiveStroke(progress);
                    break;
                case MeleePhase.Recovery:
                    Stroke = MeleeAttackProfile.RecoveryStroke(progress, _recoverFrom);
                    break;
            }

            Profile.Pose(Stroke, out Vec3 hiltLocal, out Vec3 tipLocal);
            Vec3 hilt = eye.ToWorld(hiltLocal);
            Vec3 tip = eye.ToWorld(tipLocal);
            MeleePhase phase = Phase;
            if (phase == MeleePhase.Active && targets != null) TestTargets(hilt, tip, targets);
            _previousTip = tip;
            _hasPreviousTip = true;
            // Se cambia de fase después de evaluar la pose final (la extensión completa también se comprueba). Si la
            // estocada se ha clavado, TestTargets ya ha pasado a la recuperación.
            if (phaseEnds && Phase == phase) AdvancePhase();
        }

        private void AdvancePhase()
        {
            PhaseElapsed = 0f;
            switch (Phase)
            {
                case MeleePhase.Windup:
                    Phase = MeleePhase.Active;
                    break;
                case MeleePhase.Active:
                    EnterRecovery(1f);
                    break;
                default:
                    Phase = MeleePhase.Idle;
                    Stroke = 0f;
                    break;
            }
        }

        private void EnterRecovery(float fromStroke)
        {
            Phase = MeleePhase.Recovery;
            PhaseElapsed = 0f;
            _recoverFrom = fromStroke;
        }

        private void TestTargets(Vec3 hilt, Vec3 tip, IReadOnlyList<MeleeTargetShape> targets)
        {
            Vec3 motion = _hasPreviousTip ? tip - _previousTip : tip - hilt;
            for (int i = 0; i < targets.Count; i++)
            {
                MeleeTargetShape target = targets[i];
                if (_struck.Contains(target.TargetId)) continue;

                bool hit = MeleeGeometry.SegmentHitsCapsule(hilt, tip, target.Shape, out Vec3 point, out float depth);
                // El recorrido de la punta entre pasos: nada queda entre dos posiciones de la hoja.
                if (!hit && _hasPreviousTip) hit = MeleeGeometry.SegmentHitsCapsule(_previousTip, tip, target.Shape, out point, out depth);
                if (!hit) continue;

                _struck.Add(target.TargetId);
                _targetsHit++;
                _hits.Add(new MeleeHit
                {
                    TargetId = target.TargetId,
                    PartId = target.PartId,
                    Point = point,
                    Direction = motion.Normalized,
                    Depth = depth,
                    Damage = Profile.Damage * target.DamageMultiplier,
                    Time = AttackTime,
                    Kind = Profile.Kind,
                });

                if (_targetsHit == 1) _hitStop = Profile.HitStopSeconds;
                if (_targetsHit >= Profile.MaxTargets)
                {
                    // La estocada se queda clavada en el primero: se retira desde donde llegó.
                    EnterRecovery(Stroke);
                    return;
                }
            }
        }
    }

    /// <summary>Proximidad al combate cuerpo a cuerpo: el arma se pone en guardia al acercarse un objetivo.</summary>
    public static class MeleeProximity
    {
        /// <summary>
        /// Distancia del ojo a la superficie del objetivo más cercano dentro de un cono de
        /// <paramref name="maxAngleDeg"/> grados alrededor de la mirada (infinito si no hay ninguno).
        /// </summary>
        public static float NearestInFront(ViewFrame eye, IReadOnlyList<MeleeTargetShape> targets, float maxAngleDeg, out int targetId)
        {
            targetId = -1;
            float best = float.PositiveInfinity;
            if (targets == null) return best;
            float cosLimit = (float)Math.Cos(maxAngleDeg * MathUtil.Deg2Rad);
            for (int i = 0; i < targets.Count; i++)
            {
                Capsule c = targets[i].Shape;
                Vec3 closest = MeleeGeometry.ClosestPointOnSegment(c.A, c.B, eye.Origin);
                Vec3 toTarget = closest - eye.Origin;
                float distance = toTarget.Magnitude;
                float surface = distance - c.Radius;
                if (surface >= best) continue;
                // Dentro del cono (o tan cerca que el cuerpo ocupa la vista).
                bool inCone = distance < 1e-4f || Vec3.Dot(toTarget / distance, eye.Forward) >= cosLimit || surface < 0.3f;
                if (!inCone) continue;
                best = Math.Max(0f, surface);
                targetId = targets[i].TargetId;
            }
            return best;
        }

        /// <summary>
        /// Peso de la guardia (0: fusil en posición normal; 1: en guardia): sube de forma suave desde
        /// alcance + 1,5 m hasta el alcance del arma.
        /// </summary>
        public static float GuardWeight(float surfaceDistance, float reach)
        {
            if (float.IsInfinity(surfaceDistance)) return 0f;
            return 1f - MathUtil.SmoothStep(reach, reach + 1.5f, surfaceDistance);
        }
    }
}
