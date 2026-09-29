using Pacifico.Core.Naval;
using Pacifico.Naval;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Batería de tierra de Iquique (capítulo 1): piezas de campaña que cañonean a la Esmeralda mientras está cerca
    /// de la costa y la obligan a salir al centro de la rada. Tiro con adelanto y dispersión; las piezas se turnan.
    /// </summary>
    public sealed class ShoreBattery : MonoBehaviour
    {
        [SerializeField] private ShipController target;
        [SerializeField] private NavalShell shellPrefab;
        [SerializeField] private int guns = 2;
        [Header("Pieza de campaña (estimación: cañón de montaña/campaña de ~4 kg, 1879)")]
        [SerializeField] private float muzzleVelocityMps = 400f;
        [SerializeField] private float shellMassKg = 4f;
        [SerializeField] private float caliberMm = 80f;
        [SerializeField] private float reloadSeconds = 14f;
        [SerializeField] private float maxRangeM = 1500f;
        [SerializeField] private float dispersionDeg = 0.6f;

        private System.Random _random;
        private float _nextShot;

        public ShipController Target
        {
            get => target;
            set => target = value;
        }

        public bool HoldFire { get; set; }
        public float MaxRangeM => maxRangeM;

        public void Configure(ShipController enemy, NavalShell prefab)
        {
            target = enemy;
            shellPrefab = prefab;
        }

        private void Awake()
        {
            _random = new System.Random(GetInstanceID());
            _nextShot = Time.time + 4f;
        }

        private void Update()
        {
            if (HoldFire || target == null || target.Motion == null || shellPrefab == null || Time.time < _nextShot) return;
            Vector3 from = transform.position + Vector3.up * 1.5f + transform.forward * 4.5f; // boca, delante del parapeto
            Vector3 aim = ShellLauncher.Lead(from, target.transform.position, target.Velocity, muzzleVelocityMps);
            float range = Ballistics.Distance(from.x, from.z, aim.x, aim.z);
            if (range > maxRangeM || !Ballistics.TrySolveElevation(muzzleVelocityMps, range, out float elevation)) return;
            // Desde lo alto del acantilado se tira hacia abajo: regla del fusilero (alza a nivel + ángulo de situación).
            float drop = target.transform.position.y + 2f - from.y;
            elevation += Mathf.Atan2(drop, range) * Mathf.Rad2Deg;

            var launch = new ShellLaunch
            {
                AzimuthDeg = Ballistics.Bearing(from.x, from.z, aim.x, aim.z) + Noise() * dispersionDeg,
                ElevationDeg = elevation + Noise() * dispersionDeg * 0.5f,
                MuzzleVelocity = muzzleVelocityMps,
                ShellMassKg = shellMassKg,
                CaliberMm = caliberMm,
            };
            ShellLauncher.Launch(shellPrefab, from, launch, Vector3.zero, gameObject);
            _nextShot = Time.time + reloadSeconds / Mathf.Max(1, guns);
        }

        private float Noise() => (float)(_random.NextDouble() + _random.NextDouble() - 1.0);
    }
}
