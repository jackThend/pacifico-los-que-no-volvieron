using System;
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

        /// <summary>Andanada disparada (batería y número de piezas).</summary>
        public event Action<BroadsideBatteryController, int> Fired;

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

        /// <summary>
        /// Intenta una andanada contra <paramref name="target"/> con adelanto por su movimiento.
        /// <paramref name="elevationErrorDeg"/> suma un error de elevación común (el balanceo de la cubierta).
        /// </summary>
        public bool TryFireAt(ShipController target, float elevationErrorDeg = 0f)
        {
            if (Model == null || target == null || target.Motion == null || shellPrefab == null) return false;

            Vector3 from = transform.position;
            Vector3 aim = ShellLauncher.Lead(from, target.transform.position, target.Velocity, Model.Gun.MuzzleVelocityMps);

            float bearing = Ballistics.Bearing(from.x, from.z, aim.x, aim.z);
            float relative = Mathf.DeltaAngle(_ship.Motion.HeadingDeg, bearing);
            float range = Ballistics.Distance(from.x, from.z, aim.x, aim.z);

            var launches = Model.FireAt(_ship.Motion.HeadingDeg, relative, range);
            if (launches.Count == 0) return false;

            float spacing = _ship.Spec.LengthM * 0.5f / Mathf.Max(1, launches.Count);
            for (int i = 0; i < launches.Count; i++)
            {
                ShellLaunch launch = launches[i];
                launch.ElevationDeg += elevationErrorDeg;
                float along = (i - (launches.Count - 1) * 0.5f) * spacing;
                // Las piezas se reparten a lo largo de la banda que dispara (launch.Side).
                Vector3 origin = transform.position + transform.forward * along +
                                 transform.right * ((int)launch.Side * _ship.Spec.BeamM * 0.55f) + Vector3.up * muzzleHeight;
                ShellLauncher.Launch(shellPrefab, origin, launch, _ship.Velocity, gameObject);
            }
            Fired?.Invoke(this, launches.Count);
            return true;
        }
    }
}
