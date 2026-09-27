using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    public enum BroadsideSide
    {
        None = 0,
        Port = -1,
        Starboard = 1,
    }

    /// <summary>
    /// Batería de costado (Esmeralda, Independencia, reductos centrales): las piezas se reparten por bandas y
    /// solo pueden batir un sector alrededor del través. Cada banda dispara en andanada y recarga por separado.
    /// </summary>
    public sealed class BroadsideBatteryModel
    {
        public const float DefaultArcHalfWidthDeg = 40f;
        public const float DefaultDispersionDeg = 0.6f;

        private readonly GunMount _gun;
        private readonly Random _random;
        private float _portReload;
        private float _starboardReload;

        public BroadsideBatteryModel(GunMount gun, int seed = 1855)
        {
            _gun = gun ?? throw new ArgumentNullException(nameof(gun));
            _random = new Random(seed);
            GunsPerSide = Math.Max(1, gun.Count / 2);
        }

        /// <summary>Batería con las piezas de costado más pesadas de la ficha.</summary>
        public static BroadsideBatteryModel FromShip(ShipSpec ship, int seed = 1855)
        {
            GunMount best = null;
            foreach (GunMount g in ship.Guns)
            {
                bool broadside = g.Placement == GunPlacement.Broadside || g.Placement == GunPlacement.CentralBattery;
                if (broadside && (best == null || g.ShellWeightLb > best.ShellWeightLb)) best = g;
            }
            if (best == null) throw new ArgumentException(ship.Id + " no tiene artillería de costado", nameof(ship));
            return new BroadsideBatteryModel(best, seed);
        }

        public GunMount Gun => _gun;
        public int GunsPerSide { get; }
        public float ArcHalfWidthDeg { get; set; } = DefaultArcHalfWidthDeg;
        public float DispersionDeg { get; set; } = DefaultDispersionDeg;

        /// <summary>Banda que puede batir una marcación relativa a la proa (°), o None.</summary>
        public BroadsideSide SideFor(float relativeBearingDeg)
        {
            float b = MathUtil.WrapAngle180(relativeBearingDeg);
            if (Math.Abs(MathUtil.DeltaAngle(90f, b)) <= ArcHalfWidthDeg) return BroadsideSide.Starboard;
            if (Math.Abs(MathUtil.DeltaAngle(-90f, b)) <= ArcHalfWidthDeg) return BroadsideSide.Port;
            return BroadsideSide.None;
        }

        public bool IsLoaded(BroadsideSide side) => ReloadRemaining(side) <= 0f;

        public float ReloadRemaining(BroadsideSide side)
        {
            return side == BroadsideSide.Port ? _portReload : side == BroadsideSide.Starboard ? _starboardReload : float.PositiveInfinity;
        }

        public void Step(float dt)
        {
            _portReload = Math.Max(0f, _portReload - dt);
            _starboardReload = Math.Max(0f, _starboardReload - dt);
        }

        /// <summary>
        /// Andanada contra un blanco a <paramref name="rangeM"/> metros en la marcación relativa dada.
        /// Devuelve una lista vacía si ninguna banda bate el blanco, si está descargada o fuera de alcance.
        /// </summary>
        public List<ShellLaunch> FireAt(float shipHeadingDeg, float relativeBearingDeg, float rangeM)
        {
            var launches = new List<ShellLaunch>();
            BroadsideSide side = SideFor(relativeBearingDeg);
            if (side == BroadsideSide.None || !IsLoaded(side)) return launches;
            if (!Ballistics.TrySolveElevation(_gun.MuzzleVelocityMps, rangeM, out float elevation)) return launches;

            if (side == BroadsideSide.Port) _portReload = _gun.ReloadSeconds;
            else _starboardReload = _gun.ReloadSeconds;

            for (int i = 0; i < GunsPerSide; i++)
            {
                launches.Add(new ShellLaunch
                {
                    GunIndex = i,
                    LateralOffsetM = (int)side * 4f,
                    AzimuthDeg = MathUtil.WrapAngle360(shipHeadingDeg + relativeBearingDeg + Noise() * DispersionDeg),
                    ElevationDeg = elevation + Noise() * DispersionDeg * 0.5f,
                    MuzzleVelocity = _gun.MuzzleVelocityMps,
                    ShellMassKg = _gun.ShellMassKg,
                    CaliberMm = _gun.BoreMm,
                });
            }
            return launches;
        }

        private float Noise()
        {
            // Suma de dos uniformes: distribución triangular en [-1, 1], barata y acotada.
            return (float)(_random.NextDouble() + _random.NextDouble() - 1.0);
        }
    }
}
