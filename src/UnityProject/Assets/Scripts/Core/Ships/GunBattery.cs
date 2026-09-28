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
        public GunMount mount;
        public float reloadSeconds;

        public GunBattery()
        {
        }

        public GunBattery(string gunName, int count, float projectileLbs, GunMount mount, float reloadSeconds)
        {
            this.gunName = gunName;
            this.count = count;
            this.projectileLbs = projectileLbs;
            this.mount = mount;
            this.reloadSeconds = reloadSeconds;
        }

        /// <summary>Peso total de una andanada completa de esta batería (lb).</summary>
        public float BroadsideWeightLbs => count * projectileLbs;
    }
}
