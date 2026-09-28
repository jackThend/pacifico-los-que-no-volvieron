using System;

namespace Pacifico.Core.Ships
{
    /// <summary>
    /// Grupo de cañones idénticos con el mismo montaje (p. ej. "2 × Armstrong
    /// de 300 lb en torre Coles"). Serializable por Unity.
    /// </summary>
    [Serializable]
    public sealed class GunBattery
    {
        public string gunName;
        public int count;
        public float projectileLbs;
        public float caliberInches;
        public float muzzleVelocityMs;
        public GunMount mount;
        public float reloadSeconds;

        public GunBattery()
        {
        }

        public GunBattery(string gunName, int count, float projectileLbs, float caliberInches,
            float muzzleVelocityMs, GunMount mount, float reloadSeconds)
        {
            this.gunName = gunName;
            this.count = count;
            this.projectileLbs = projectileLbs;
            this.caliberInches = caliberInches;
            this.muzzleVelocityMs = muzzleVelocityMs;
            this.mount = mount;
            this.reloadSeconds = reloadSeconds;
        }

        /// <summary>Peso total de una andanada completa de esta batería (lb).</summary>
        public float BroadsideWeightLbs => count * projectileLbs;
    }
}
