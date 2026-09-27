using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>Batería de costado en escena: dispara andanadas contra un blanco cuando lo bate una banda.</summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class BroadsideBatteryController : MonoBehaviour
    {
        [SerializeField] private NavalShell shellPrefab;
        [SerializeField] private float muzzleHeight = 3f;

        private ShipController _ship;

        public BroadsideBatteryModel Model { get; private set; }

        public NavalShell ShellPrefab
        {
            get => shellPrefab;
            set => shellPrefab = value;
        }

        private void Start()
        {
            _ship = GetComponent<ShipController>();
            if (_ship.Spec != null) Model = BroadsideBatteryModel.FromShip(_ship.Spec, GetInstanceID());
        }

        private void FixedUpdate()
        {
            Model?.Step(Time.fixedDeltaTime);
        }

        /// <summary>Intenta una andanada contra <paramref name="target"/> con adelanto por su movimiento.</summary>
        public bool TryFireAt(ShipController target)
        {
            if (Model == null || target == null || target.Motion == null || shellPrefab == null) return false;

            Vector3 from = transform.position;
            Vector3 aim = target.transform.position;
            Vector3 targetVelocity = target.Velocity;
            if (Ballistics.TryLead(from.x, from.z, aim.x, aim.z, targetVelocity.x, targetVelocity.z,
                    Model.Gun.MuzzleVelocityMps, out float ax, out float az, out _))
            {
                aim = new Vector3(ax, aim.y, az);
            }

            float bearing = Ballistics.Bearing(from.x, from.z, aim.x, aim.z);
            float relative = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, bearing);
            float range = Ballistics.Distance(from.x, from.z, aim.x, aim.z);

            var launches = Model.FireAt(_ship.Motion.HeadingDeg, relative, range);
            if (launches.Count == 0) return false;

            float spacing = _ship.Spec.LengthM * 0.5f / Mathf.Max(1, launches.Count);
            for (int i = 0; i < launches.Count; i++)
            {
                ShellLaunch launch = launches[i];
                float along = (i - (launches.Count - 1) * 0.5f) * spacing;
                Vector3 origin = transform.position + transform.forward * along +
                                 transform.right * (Mathf.Sign(relative) * _ship.Spec.BeamM * 0.55f) + Vector3.up * muzzleHeight;
                Quaternion direction = Quaternion.Euler(-launch.ElevationDeg, launch.AzimuthDeg, 0f);
                NavalShell shell = Instantiate(shellPrefab, origin, direction);
                shell.Launch(direction * Vector3.forward * launch.MuzzleVelocity + _ship.Velocity, launch.ShellMassKg, launch.CaliberMm, gameObject);
            }
            return true;
        }
    }
}
