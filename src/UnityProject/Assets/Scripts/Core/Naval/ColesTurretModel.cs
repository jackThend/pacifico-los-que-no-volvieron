using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>Posición y rumbo del buque que porta la torre.</summary>
    public readonly struct ShipPose
    {
        public readonly float X;
        public readonly float Z;
        public readonly float HeadingDegrees;

        public ShipPose(float x, float z, float headingDegrees)
        {
            X = x;
            Z = z;
            HeadingDegrees = headingDegrees;
        }
    }

    /// <summary>Punto sobre el agua (coordenadas de mundo X/Z).</summary>
    public readonly struct SeaPoint
    {
        public readonly float X;
        public readonly float Z;

        public SeaPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float DistanceTo(SeaPoint other)
        {
            var dx = other.X - X;
            var dz = other.Z - Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// Torre giratoria con puntería independiente del casco (tarea 2.2).
    /// La marcación (<see cref="TrainDegrees"/>) es relativa a la proa, así que
    /// al caer el buque la torre compensa sola para seguir al blanco, limitada
    /// por su velocidad de giro. Los dos cañones convergen a la distancia del
    /// blanco: <see cref="LeftImpact"/>/<see cref="RightImpact"/> forman la
    /// retícula de convergencia.
    /// </summary>
    public sealed class ColesTurretModel
    {
        public const float OnTargetTrainToleranceDegrees = 0.5f;
        public const float OnTargetElevationToleranceDegrees = 0.1f;
        public const float DefaultConvergenceM = 1000f;
        private const double DegToRad = Math.PI / 180.0;

        public TurretMount Mount { get; }

        /// <summary>Marcación actual de la torre respecto a la proa, en (-180, 180].</summary>
        public float TrainDegrees { get; private set; }
        public float ElevationDegrees { get; private set; }

        public bool HasTarget { get; private set; }
        public SeaPoint Target { get; private set; }
        public float DesiredTrainDegrees { get; private set; }
        public float DesiredElevationDegrees { get; private set; }
        public float TargetRangeM { get; private set; }
        public bool TargetInRange { get; private set; }

        /// <summary>Distancia a la que convergen los ejes de ambos cañones (m).</summary>
        public float ConvergenceM { get; private set; } = DefaultConvergenceM;

        public SeaPoint TurretPosition { get; private set; }
        public SeaPoint AimPoint { get; private set; }
        public SeaPoint LeftImpact { get; private set; }
        public SeaPoint RightImpact { get; private set; }

        public ColesTurretModel(TurretMount mount, float initialTrainDegrees = 0f)
        {
            Mount = mount ?? throw new ArgumentNullException(nameof(mount));
            TrainDegrees = Angles.Normalize180(initialTrainDegrees);
            ElevationDegrees = Clamp(0f, mount.minElevationDegrees, mount.maxElevationDegrees);
        }

        /// <summary>Alcance que tendrían los disparos con la elevación actual (m).</summary>
        public float CurrentRangeM =>
            NavalBallistics.RangeForElevation(Mount.muzzleVelocityMs, ElevationDegrees, Mount.gunHeightM);

        public float MaxRangeM =>
            NavalBallistics.RangeForElevation(Mount.muzzleVelocityMs, Mount.maxElevationDegrees, Mount.gunHeightM);

        public float TrainErrorDegrees => HasTarget ? Angles.DeltaDegrees(TrainDegrees, DesiredTrainDegrees) : 0f;
        public float ElevationErrorDegrees => HasTarget ? DesiredElevationDegrees - ElevationDegrees : 0f;

        /// <summary>Sector de superestructura que bloquea el tiro con la marcación actual, o null.</summary>
        public FiringArcBlock MaskingSector => Mount.MaskingSector(TrainDegrees);
        public bool IsMasked => MaskingSector != null;

        public bool IsOnTarget =>
            HasTarget && TargetInRange &&
            Math.Abs(TrainErrorDegrees) <= OnTargetTrainToleranceDegrees &&
            Math.Abs(ElevationErrorDegrees) <= OnTargetElevationToleranceDegrees;

        /// <summary>La torre apunta al blanco y ninguna superestructura se interpone.</summary>
        public bool CanFire => IsOnTarget && !IsMasked;

        public void SetTarget(float x, float z)
        {
            HasTarget = true;
            Target = new SeaPoint(x, z);
        }

        public void ClearTarget() => HasTarget = false;

        /// <summary>Avanza la puntería hacia el blanco con las velocidades de giro y elevación del montaje.</summary>
        public void Step(float deltaSeconds, ShipPose ship)
        {
            if (deltaSeconds < 0f) deltaSeconds = 0f;
            TurretPosition = Offset(new SeaPoint(ship.X, ship.Z), ship.HeadingDegrees, Mount.offsetForwardM, 0f);

            if (HasTarget)
            {
                var dx = Target.X - TurretPosition.X;
                var dz = Target.Z - TurretPosition.Z;
                var worldBearing = (float)(Math.Atan2(dx, dz) / DegToRad);
                DesiredTrainDegrees = Angles.Normalize180(worldBearing - ship.HeadingDegrees);
                TargetRangeM = (float)Math.Sqrt(dx * dx + dz * dz);
                DesiredElevationDegrees = NavalBallistics.ElevationForRange(Mount.muzzleVelocityMs, TargetRangeM,
                    Mount.gunHeightM, Mount.minElevationDegrees, Mount.maxElevationDegrees, out var inRange);
                TargetInRange = inRange;
                ConvergenceM = Math.Max(TargetRangeM, 1f);

                // Giro por el camino más corto: la torre Coles da la vuelta completa.
                TrainDegrees = Angles.Normalize180(TrainDegrees +
                    ClampMagnitude(TrainErrorDegrees, Mount.traverseDegreesPerSecond * deltaSeconds));
                ElevationDegrees += ClampMagnitude(DesiredElevationDegrees - ElevationDegrees,
                    Mount.elevationDegreesPerSecond * deltaSeconds);
            }

            UpdateReticle(ship.HeadingDegrees);
        }

        private void UpdateReticle(float shipHeadingDegrees)
        {
            var bearing = shipHeadingDegrees + TrainDegrees;
            var range = CurrentRangeM;
            AimPoint = Offset(TurretPosition, bearing, range, 0f);

            // Cada cañón está desplazado media separación a su banda y tiene los ejes
            // convergentes (toe-in) hacia ConvergenceM: sus impactos coinciden solo
            // cuando el alcance actual iguala la distancia de convergencia.
            var half = Mount.barrelSeparationM * 0.5f;
            var toeIn = (float)(Math.Atan2(half, ConvergenceM) / DegToRad);
            var leftOrigin = Offset(TurretPosition, bearing, 0f, -half);
            var rightOrigin = Offset(TurretPosition, bearing, 0f, half);
            LeftImpact = Offset(leftOrigin, bearing + toeIn, range, 0f);
            RightImpact = Offset(rightOrigin, bearing - toeIn, range, 0f);
        }

        /// <summary>Desplaza un punto <paramref name="forward"/> m según el rumbo y <paramref name="right"/> m a estribor.</summary>
        private static SeaPoint Offset(SeaPoint origin, float headingDegrees, float forward, float right)
        {
            var rad = headingDegrees * DegToRad;
            var sin = Math.Sin(rad);
            var cos = Math.Cos(rad);
            return new SeaPoint(
                (float)(origin.X + sin * forward + cos * right),
                (float)(origin.Z + cos * forward - sin * right));
        }

        private static float ClampMagnitude(float value, float max) =>
            value > max ? max : value < -max ? -max : value;

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
