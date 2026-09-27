using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>Disparo de una pieza de la torre, en coordenadas del mundo.</summary>
    public struct ShellLaunch
    {
        public int GunIndex;
        /// <summary>Desplazamiento lateral de la boca respecto al eje de la torre (m, + estribor de la torre).</summary>
        public float LateralOffsetM;
        public float AzimuthDeg;
        public float ElevationDeg;
        public float MuzzleVelocity;
        public float ShellMassKg;
        public float CaliberMm;
    }

    /// <summary>
    /// Torre giratoria Coles del Huáscar (ROADMAP 2.2). Gira independiente del casco, con aceleración limitada
    /// para un apuntado suave, elevación entre los topes históricos y un sector ciego hacia popa en el que no
    /// puede disparar sin barrer la propia superestructura. Las dos piezas convergen a una distancia ajustable.
    /// </summary>
    public sealed class ColesTurretModel
    {
        /// <summary>Separación entre las dos piezas de la torre (m). Estimación a partir del diámetro de la torre.</summary>
        public const float DefaultGunSeparationM = 2.4f;
        public const float DefaultTraverseAcceleration = 4f;
        /// <summary>Dispersión angular de juego por disparo (desviación típica, grados).</summary>
        public const float DefaultDispersionDeg = 0.25f;

        private readonly TurretSpec _turret;
        private readonly GunMount _gun;
        private readonly float[] _reloadRemaining;
        private readonly Random _random;
        private float _trainVelocity;

        public ColesTurretModel(TurretSpec turret, GunMount gun, int seed = 1879)
        {
            _turret = turret ?? throw new ArgumentNullException(nameof(turret));
            _gun = gun ?? throw new ArgumentNullException(nameof(gun));
            _reloadRemaining = new float[Math.Max(1, gun.Count)];
            _random = new Random(seed);
            ConvergenceRangeM = 800f;
        }

        /// <summary>Crea la torre de un buque a partir de su ficha (pieza más pesada montada en torre).</summary>
        public static ColesTurretModel FromShip(ShipSpec ship, int seed = 1879)
        {
            if (ship.Turret == null) throw new ArgumentException(ship.Id + " no tiene torre giratoria", nameof(ship));
            GunMount gun = ship.Guns.Find(g => g.Placement == GunPlacement.Turret);
            return new ColesTurretModel(ship.Turret, gun, seed);
        }

        public TurretSpec Spec => _turret;
        public GunMount Gun => _gun;
        public int GunCount => _reloadRemaining.Length;

        public float GunSeparationM { get; set; } = DefaultGunSeparationM;
        public float TraverseAccelerationDegPerSecond2 { get; set; } = DefaultTraverseAcceleration;
        public float DispersionDeg { get; set; } = DefaultDispersionDeg;

        /// <summary>Marcación de la torre respecto a la proa (°, (-180, 180], positiva a estribor).</summary>
        public float TrainDeg { get; private set; }
        public float ElevationDeg { get; private set; }

        public float OrderedTrainDeg { get; private set; }
        public float OrderedElevationDeg { get; private set; }

        /// <summary>El blanco ordenado está más allá del alcance máximo con la elevación disponible.</summary>
        public bool TargetOutOfRange { get; private set; }

        private float _convergenceRange;

        /// <summary>Distancia a la que se cruzan los ejes de las dos piezas (retícula de convergencia).</summary>
        public float ConvergenceRangeM
        {
            get => _convergenceRange;
            set => _convergenceRange = MathUtil.Clamp(value, 100f, 5000f);
        }

        public float MaxRangeM => Ballistics.Range(_gun.MuzzleVelocityMps, Math.Min(_turret.MaxElevationDeg, 45f));

        public bool IsInBlindArc(float trainDeg)
        {
            if (_turret.BlindArcHalfWidthDeg <= 0f) return false;
            return Math.Abs(MathUtil.DeltaAngle(_turret.BlindArcCenterDeg, trainDeg)) < _turret.BlindArcHalfWidthDeg;
        }

        /// <summary>La torre ha llegado a la orden (dentro de la tolerancia).</summary>
        public bool IsOnTarget(float toleranceDeg = 0.5f)
        {
            return Math.Abs(MathUtil.DeltaAngle(TrainDeg, OrderedTrainDeg)) <= toleranceDeg &&
                   Math.Abs(ElevationDeg - OrderedElevationDeg) <= toleranceDeg;
        }

        public bool IsLoaded(int gunIndex) => _reloadRemaining[gunIndex] <= 0f;

        public float ReloadProgress(int gunIndex) => 1f - MathUtil.Clamp01(_reloadRemaining[gunIndex] / _gun.ReloadSeconds);

        public bool CanFire => !IsInBlindArc(TrainDeg) && Array.Exists(_reloadRemaining, r => r <= 0f);

        /// <summary>Ordena apuntar a una marcación y elevación concretas (se aplican los topes).</summary>
        public void Order(float trainDeg, float elevationDeg)
        {
            OrderedTrainDeg = MathUtil.WrapAngle180(trainDeg);
            OrderedElevationDeg = MathUtil.Clamp(elevationDeg, _turret.MinElevationDeg, _turret.MaxElevationDeg);
        }

        /// <summary>
        /// Ordena apuntar a un punto del mar desde la posición de la torre y el rumbo del buque.
        /// Calcula la elevación de tiro tenso; si el blanco excede el alcance, eleva al máximo y lo indica.
        /// </summary>
        public void OrderAtPoint(float turretX, float turretZ, float shipHeadingDeg, float targetX, float targetZ)
        {
            float bearing = Ballistics.Bearing(turretX, turretZ, targetX, targetZ);
            float range = Ballistics.Distance(turretX, turretZ, targetX, targetZ);
            bool solved = Ballistics.TrySolveElevation(_gun.MuzzleVelocityMps, range, out float elevation);
            TargetOutOfRange = !solved || elevation > _turret.MaxElevationDeg;
            Order(bearing - shipHeadingDeg, solved ? elevation : _turret.MaxElevationDeg);
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;

            // Giro: perfil de velocidad trapezoidal (acelera, crucero, frena para detenerse justo en la orden).
            float error = MathUtil.DeltaAngle(TrainDeg, OrderedTrainDeg);
            float accel = TraverseAccelerationDegPerSecond2;
            float stoppingSpeed = (float)Math.Sqrt(2f * accel * Math.Abs(error));
            float desiredVelocity = Math.Sign(error) * Math.Min(_turret.TraverseDegPerSecond, stoppingSpeed);
            _trainVelocity = MathUtil.MoveTowards(_trainVelocity, desiredVelocity, accel * dt);
            float move = _trainVelocity * dt;
            if (Math.Abs(move) >= Math.Abs(error) || Math.Abs(error) < 1e-3f)
            {
                TrainDeg = OrderedTrainDeg;
                _trainVelocity = 0f;
            }
            else
            {
                TrainDeg = MathUtil.WrapAngle180(TrainDeg + move);
            }

            ElevationDeg = MathUtil.MoveTowards(ElevationDeg, OrderedElevationDeg, _turret.ElevationDegPerSecond * dt);

            for (int i = 0; i < _reloadRemaining.Length; i++)
            {
                _reloadRemaining[i] = Math.Max(0f, _reloadRemaining[i] - dt);
            }
        }

        /// <summary>
        /// Guiñada (°) de cada pieza hacia el eje para que ambas converjan a <see cref="ConvergenceRangeM"/>.
        /// Índice 0 = pieza de babor de la torre (desplazada a la izquierda).
        /// </summary>
        public float ConvergenceYawDeg(int gunIndex)
        {
            float half = GunSeparationM * 0.5f;
            float angle = (float)Math.Atan2(half, ConvergenceRangeM) * MathUtil.Rad2Deg;
            return GunLateralOffset(gunIndex) < 0f ? angle : -angle;
        }

        /// <summary>Desviación lateral (m) del impacto de una pieza respecto al eje de la torre a <paramref name="distanceM"/>.</summary>
        public float LateralMissAt(int gunIndex, float distanceM)
        {
            float offset = GunLateralOffset(gunIndex);
            return offset * (1f - distanceM / ConvergenceRangeM);
        }

        public float GunLateralOffset(int gunIndex)
        {
            if (GunCount == 1) return 0f;
            return gunIndex == 0 ? -GunSeparationM * 0.5f : GunSeparationM * 0.5f;
        }

        /// <summary>
        /// Dispara las piezas cargadas (andanada de torre). Devuelve una lista vacía si la torre apunta al sector ciego.
        /// </summary>
        public List<ShellLaunch> Fire(float shipHeadingDeg)
        {
            var launches = new List<ShellLaunch>();
            if (IsInBlindArc(TrainDeg)) return launches;

            for (int i = 0; i < _reloadRemaining.Length; i++)
            {
                if (_reloadRemaining[i] > 0f) continue;
                _reloadRemaining[i] = _gun.ReloadSeconds;
                launches.Add(new ShellLaunch
                {
                    GunIndex = i,
                    LateralOffsetM = GunLateralOffset(i),
                    AzimuthDeg = MathUtil.WrapAngle360(shipHeadingDeg + TrainDeg + ConvergenceYawDeg(i) + Gaussian() * DispersionDeg),
                    ElevationDeg = ElevationDeg + Gaussian() * DispersionDeg * 0.5f,
                    MuzzleVelocity = _gun.MuzzleVelocityMps,
                    ShellMassKg = _gun.ShellMassKg,
                    CaliberMm = _gun.BoreMm,
                });
            }
            return launches;
        }

        /// <summary>Normal estándar (Box-Muller) truncada a ±3σ.</summary>
        private float Gaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (float)Math.Max(-3.0, Math.Min(3.0, z));
        }
    }
}
