using Pacifico.Core.Naval;
using Pacifico.Core.Ships;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>
    /// Torre Coles en escena: el jugador apunta con el ratón sobre el mar,
    /// <see cref="ColesTurretModel"/> gira la torre (independiente del casco)
    /// y eleva los cañones con las velocidades del montaje, y se dibuja la
    /// retícula de convergencia de los dos Armstrong.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColesTurretController : MonoBehaviour
    {
        [SerializeField] private ShipNavigationController ship;
        [SerializeField, Tooltip("Pivote de giro horizontal (hijo del buque).")] private Transform turretPivot;
        [SerializeField, Tooltip("Pivote de elevación de los cañones (hijo de la torre).")] private Transform gunPivot;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private bool playerControlled = true;
        [SerializeField] private float seaLevel;
        [SerializeField] private bool showReticle = true;

        private ColesTurretModel turret;
        private GunBattery turretGuns;
        private float reloadRemaining;
        private string lastSalvo = string.Empty;

        public ColesTurretModel Turret => turret;
        public float ReloadRemaining => reloadRemaining;
        public bool IsLoaded => reloadRemaining <= 0f;

        /// <summary>Se dispara por cada proyectil de la salva que alcanza a un buque.</summary>
        public event System.Action<ShipDamageReceiver, ImpactReport> ShellHit;

        public void Configure(ShipNavigationController owner, Transform yawPivot, Transform pitchPivot,
            Camera camera, bool isPlayerControlled)
        {
            ship = owner;
            turretPivot = yawPivot;
            gunPivot = pitchPivot;
            aimCamera = camera;
            playerControlled = isPlayerControlled;
        }

        // Start (no Awake): el ShipNavigationController debe haber creado su modelo.
        private void Start()
        {
            if (ship == null) ship = GetComponent<ShipNavigationController>();
            var spec = ship != null && ship.ShipData != null ? ship.ShipData.ToSpec() : null;
            if (spec == null || spec.Turret == null)
            {
                Debug.LogError($"[ColesTurretController] {name}: el buque no tiene montaje de torre.", this);
                enabled = false;
                return;
            }
            turret = new ColesTurretModel(spec.Turret);
            foreach (var battery in spec.Guns)
            {
                if (battery.mount == GunMount.Turret) turretGuns = battery;
            }
            if (aimCamera == null) aimCamera = Camera.main;
        }

        private void Update()
        {
            if (!playerControlled || aimCamera == null) return;
            if (!GameInput.TryGetPointer(out var pointer)) return;

            var ray = aimCamera.ScreenPointToRay(pointer);
            var sea = new Plane(Vector3.up, new Vector3(0f, seaLevel, 0f));
            if (sea.Raycast(ray, out var distance))
            {
                var point = ray.GetPoint(distance);
                turret.SetTarget(point.x, point.z);
            }

            if (GameInput.PrimaryClickDown()) TryFire();
        }

        /// <summary>
        /// Dispara la salva de los dos Armstrong si están cargados y la torre
        /// puede hacer fuego. Cada proyectil cae en su punto de la retícula y se
        /// resuelve contra la silueta y el blindaje del buque alcanzado.
        /// </summary>
        public bool TryFire()
        {
            if (turret == null || turretGuns == null || !IsLoaded || !turret.CanFire) return false;
            reloadRemaining = turretGuns.reloadSeconds;

            var hits = 0;
            foreach (var impact in new[] { turret.LeftImpact, turret.RightImpact })
            {
                foreach (var receiver in ShipDamageReceiver.All)
                {
                    if (receiver.Ship == ship || receiver.Spec == null || receiver.Hull.IsSunk) continue;
                    var pose = receiver.Pose;
                    if (!NavalHitTest.TryHit(impact, pose, receiver.Spec, out var zone)) continue;

                    var report = ArmorImpact.ResolveShot(turretGuns, turret.Mount.gunHeightM, turret.TurretPosition,
                        pose, receiver.Spec, zone);
                    receiver.ApplyImpact(report);
                    ShellHit?.Invoke(receiver, report);
                    lastSalvo = $"{receiver.Spec.DisplayName}: {Describe(report)}";
                    hits++;
                    break;
                }
            }
            if (hits == 0) lastSalvo = "Agua: la salva no alcanza ningún buque";
            return true;
        }

        private static string Describe(ImpactReport report)
        {
            var result = report.Result == ImpactResult.Ricochet ? "REBOTE"
                : report.Result == ImpactResult.CriticalPenetration ? "PERFORACIÓN CRÍTICA" : "PERFORACIÓN";
            return $"{result} ({report.Zone}, {report.IncidenceDegrees:0}°, perfora {report.PenetrationInches:0.0}\" " +
                   $"vs {report.EffectiveArmorInches:0.0}\" efectivas) −{report.Damage:0}";
        }

        /// <summary>Asigna blanco desde código (IA, cinemáticas).</summary>
        public void SetTarget(Vector3 worldPoint)
        {
            turret?.SetTarget(worldPoint.x, worldPoint.z);
        }

        private void FixedUpdate()
        {
            if (reloadRemaining > 0f) reloadRemaining -= Time.fixedDeltaTime;
            var motion = ship.Motion;
            if (motion == null) return;
            turret.Step(Time.fixedDeltaTime, new ShipPose(motion.PositionX, motion.PositionZ, motion.HeadingDegrees));
        }

        private void LateUpdate()
        {
            if (turretPivot != null) turretPivot.localRotation = Quaternion.Euler(0f, turret.TrainDegrees, 0f);
            if (gunPivot != null) gunPivot.localRotation = Quaternion.Euler(-turret.ElevationDegrees, 0f, 0f);
        }

        private void OnGUI()
        {
            if (!showReticle || !playerControlled || turret == null || aimCamera == null) return;

            if (turret.HasTarget) DrawMarker(turret.Target, "+");
            DrawMarker(turret.LeftImpact, "x");
            DrawMarker(turret.RightImpact, "x");

            string state;
            if (!turret.HasTarget) state = "sin blanco";
            else if (!turret.TargetInRange) state = "FUERA DE ALCANCE";
            else if (turret.IsMasked) state = $"ENMASCARADO: {turret.MaskingSector.reason}";
            else if (turret.CanFire) state = IsLoaded ? "EN PUNTERÍA — clic para disparar" : "EN PUNTERÍA";
            else state = "apuntando…";
            var loading = IsLoaded ? "cargados" : $"cargando {reloadRemaining:0.0} s";

            GUI.Label(new Rect(12f, 130f, 560f, 90f),
                $"Torre Coles: marcación {turret.TrainDegrees:+000;-000}°  elevación {turret.ElevationDegrees:0.0}°  ({loading})\n" +
                $"Alcance {turret.CurrentRangeM:0} m / blanco {turret.TargetRangeM:0} m (máx. {turret.MaxRangeM:0} m)\n" +
                state + "\n" + lastSalvo);
        }

        private void DrawMarker(SeaPoint point, string glyph)
        {
            var screen = aimCamera.WorldToScreenPoint(new Vector3(point.X, seaLevel, point.Z));
            if (screen.z <= 0f) return; // detrás de la cámara
            GUI.Label(new Rect(screen.x - 8f, Screen.height - screen.y - 10f, 20f, 20f), glyph);
        }
    }
}
