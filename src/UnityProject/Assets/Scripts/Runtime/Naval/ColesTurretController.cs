using Pacifico.Core.Naval;
using UnityEngine;
using UnityEngine.InputSystem;

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

        public ColesTurretModel Turret => turret;

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
            if (aimCamera == null) aimCamera = Camera.main;
        }

        private void Update()
        {
            if (!playerControlled || aimCamera == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            var ray = aimCamera.ScreenPointToRay(mouse.position.ReadValue());
            var sea = new Plane(Vector3.up, new Vector3(0f, seaLevel, 0f));
            if (sea.Raycast(ray, out var distance))
            {
                var point = ray.GetPoint(distance);
                turret.SetTarget(point.x, point.z);
            }
        }

        /// <summary>Asigna blanco desde código (IA, cinemáticas).</summary>
        public void SetTarget(Vector3 worldPoint)
        {
            turret?.SetTarget(worldPoint.x, worldPoint.z);
        }

        private void FixedUpdate()
        {
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

            if (turret.HasTarget) DrawMarker(turret.Target, "◎");
            DrawMarker(turret.LeftImpact, "×");
            DrawMarker(turret.RightImpact, "×");

            string state;
            if (!turret.HasTarget) state = "sin blanco";
            else if (!turret.TargetInRange) state = "FUERA DE ALCANCE";
            else if (turret.IsMasked) state = $"ENMASCARADO: {turret.MaskingSector.reason}";
            else if (turret.CanFire) state = "EN PUNTERÍA";
            else state = "apuntando…";

            GUI.Label(new Rect(12f, 130f, 420f, 70f),
                $"Torre Coles: marcación {turret.TrainDegrees:+000;-000}°  elevación {turret.ElevationDegrees:0.0}°\n" +
                $"Alcance {turret.CurrentRangeM:0} m / blanco {turret.TargetRangeM:0} m (máx. {turret.MaxRangeM:0} m)\n" +
                state);
        }

        private void DrawMarker(SeaPoint point, string glyph)
        {
            var screen = aimCamera.WorldToScreenPoint(new Vector3(point.X, seaLevel, point.Z));
            if (screen.z <= 0f) return; // detrás de la cámara
            GUI.Label(new Rect(screen.x - 8f, Screen.height - screen.y - 10f, 20f, 20f), glyph);
        }
    }
}
