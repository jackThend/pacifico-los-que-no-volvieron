using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>Único punto donde se instancian proyectiles navales (torres y baterías de costado).</summary>
    public static class ShellLauncher
    {
        /// <summary>Dispara <paramref name="launch"/> desde <paramref name="origin"/>; el proyectil hereda la velocidad del buque.</summary>
        public static NavalShell Launch(NavalShell prefab, Vector3 origin, ShellLaunch launch, Vector3 shipVelocity, GameObject shooter)
        {
            Quaternion direction = Quaternion.Euler(-launch.ElevationDeg, launch.AzimuthDeg, 0f);
            NavalShell shell = Object.Instantiate(prefab, origin, direction);
            shell.Launch(direction * Vector3.forward * launch.MuzzleVelocity + shipVelocity, launch.ShellMassKg, launch.CaliberMm, shooter);
            return shell;
        }

        /// <summary>Punto de puntería corregido por el movimiento del blanco (o el propio punto si no hay solución).</summary>
        public static Vector3 Lead(Vector3 shooter, Vector3 target, Vector3 targetVelocity, float muzzleVelocity)
        {
            return Ballistics.TryLead(shooter.x, shooter.z, target.x, target.z, targetVelocity.x, targetVelocity.z,
                                      muzzleVelocity, out float ax, out float az, out _)
                ? new Vector3(ax, target.y, az)
                : target;
        }
    }
}
