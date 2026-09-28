using System;

namespace Pacifico.Core.Ships
{
    /// <summary>
    /// Sector en el que la torre no puede disparar porque la propia
    /// superestructura del buque bloquea la línea de tiro.
    /// </summary>
    [Serializable]
    public sealed class FiringArcBlock
    {
        /// <summary>Marcación relativa a la proa del centro del sector (0 = proa, 180 = popa).</summary>
        public float centerDegrees;
        public float halfWidthDegrees;
        public string reason;

        public FiringArcBlock()
        {
        }

        public FiringArcBlock(float centerDegrees, float halfWidthDegrees, string reason)
        {
            this.centerDegrees = centerDegrees;
            this.halfWidthDegrees = halfWidthDegrees;
            this.reason = reason;
        }

        public bool Contains(float trainDegrees)
        {
            return Math.Abs(Angles.DeltaDegrees(centerDegrees, trainDegrees)) <= halfWidthDegrees;
        }
    }

    /// <summary>
    /// Montaje de torre giratoria: velocidades de puntería, límites de
    /// elevación, balística de sus cañones y sectores enmascarados.
    /// </summary>
    [Serializable]
    public sealed class TurretMount
    {
        public float traverseDegreesPerSecond;
        public float elevationDegreesPerSecond;
        public float minElevationDegrees;
        public float maxElevationDegrees;
        public float muzzleVelocityMs;
        /// <summary>Distancia entre los ejes de los dos cañones (m).</summary>
        public float barrelSeparationM;
        /// <summary>Altura de los muñones sobre la flotación (m).</summary>
        public float gunHeightM;
        /// <summary>Posición de la torre respecto al centro del buque, hacia proa (m).</summary>
        public float offsetForwardM;
        public FiringArcBlock[] blindSectors = new FiringArcBlock[0];

        public TurretMount()
        {
        }

        public TurretMount(float traverseDegreesPerSecond, float elevationDegreesPerSecond,
            float minElevationDegrees, float maxElevationDegrees, float muzzleVelocityMs,
            float barrelSeparationM, float gunHeightM, float offsetForwardM, FiringArcBlock[] blindSectors)
        {
            this.traverseDegreesPerSecond = traverseDegreesPerSecond;
            this.elevationDegreesPerSecond = elevationDegreesPerSecond;
            this.minElevationDegrees = minElevationDegrees;
            this.maxElevationDegrees = maxElevationDegrees;
            this.muzzleVelocityMs = muzzleVelocityMs;
            this.barrelSeparationM = barrelSeparationM;
            this.gunHeightM = gunHeightM;
            this.offsetForwardM = offsetForwardM;
            this.blindSectors = blindSectors ?? new FiringArcBlock[0];
        }

        /// <summary>Devuelve el sector que enmascara esa marcación, o null si el tiro está libre.</summary>
        public FiringArcBlock MaskingSector(float trainDegrees)
        {
            foreach (var sector in blindSectors)
            {
                if (sector != null && sector.Contains(trainDegrees)) return sector;
            }
            return null;
        }

        public TurretMount Clone()
        {
            var sectors = new FiringArcBlock[blindSectors.Length];
            for (var i = 0; i < sectors.Length; i++)
            {
                var s = blindSectors[i];
                sectors[i] = new FiringArcBlock(s.centerDegrees, s.halfWidthDegrees, s.reason);
            }
            return new TurretMount(traverseDegreesPerSecond, elevationDegreesPerSecond, minElevationDegrees,
                maxElevationDegrees, muzzleVelocityMs, barrelSeparationM, gunHeightM, offsetForwardM, sectors);
        }
    }
}
