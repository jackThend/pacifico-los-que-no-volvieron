using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Naval;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Torre Coles en escena (ROADMAP 2.2). Apunta con el ratón sobre el mar, dispara con clic izquierdo o Espacio,
    /// ajusta la convergencia con R/F y amplía el telémetro manteniendo Mayús. Dibuja la retícula de convergencia:
    /// los puntos de caída previstos de cada pieza y la dispersión esperada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColesTurretController : MonoBehaviour
    {
        [SerializeField] private ShipController ship;
        [Tooltip("Pivote de giro de la torre (rota en Y local).")]
        [SerializeField] private Transform turretPivot;
        [Tooltip("Pivote de elevación de los cañones (rota en X local).")]
        [SerializeField] private Transform gunCradle;
        [Tooltip("Bocas de las piezas: 0 = babor de la torre, 1 = estribor.")]
        [SerializeField] private Transform[] muzzles = new Transform[0];
        [SerializeField] private NavalShell shellPrefab;
        [SerializeField] private bool playerControlled = true;
        [SerializeField] private float seaLevel;

        [Header("Telémetro")]
        [SerializeField] private float normalFov = 60f;
        [SerializeField] private float rangefinderFov = 14f;
        [SerializeField] private float convergenceStepM = 100f;

        private ColesTurretModel _model;
        private Camera _camera;
        private bool _fireRequested;
        private GUIStyle _style;

        public ColesTurretModel Model => _model;

        public bool PlayerControlled
        {
            get => playerControlled;
            set => playerControlled = value;
        }

        /// <summary>Configura la torre por código (escenas de prototipo generadas por el editor).</summary>
        public void Configure(ShipController owner, Transform pivot, Transform cradle, Transform[] gunMuzzles, NavalShell prefab)
        {
            ship = owner;
            turretPivot = pivot;
            gunCradle = cradle;
            muzzles = gunMuzzles;
            shellPrefab = prefab;
        }

        private void Start()
        {
            _camera = Camera.main;
            if (ship == null || ship.Spec == null || ship.Spec.Turret == null)
            {
                Debug.LogError("[Pacífico] ColesTurretController necesita un ShipController con torre en su ficha.", this);
                enabled = false;
                return;
            }
            _model = ColesTurretModel.FromShip(ship.Spec, GetInstanceID());

            NavalHud hud = NavalHud.Active;
            if (hud != null)
            {
                hud.ExtraLines.Add(() => "Torre:      " + _model.TrainDeg.ToString("+000;-000") + "°  elev " + _model.ElevationDeg.ToString("0.0") + "°");
                hud.ExtraLines.Add(() => "Convergencia: " + _model.ConvergenceRangeM.ToString("0") + " m   [R/F]");
            }
        }

        /// <summary>Orden de puntería para IA: punto del mundo con corrección por movimiento del blanco.</summary>
        public void AimAt(Vector3 worldPoint, Vector3 targetVelocity)
        {
            if (_model == null) return;
            Vector3 origin = PivotPosition;
            Vector3 aim = ShellLauncher.Lead(origin, worldPoint, targetVelocity, _model.Gun.MuzzleVelocityMps);
            _model.OrderAtPoint(origin.x, origin.z, ship.Motion.HeadingDeg, aim.x, aim.z);
        }

        public void RequestFire() => _fireRequested = true;

        private Vector3 PivotPosition => turretPivot != null ? turretPivot.position : ship.transform.position;

        private void Update()
        {
            if (_model == null) return;

            if (playerControlled)
            {
                if (_camera != null && TryMouseOnSea(out Vector3 point))
                {
                    Vector3 origin = PivotPosition;
                    _model.OrderAtPoint(origin.x, origin.z, ship.Motion.HeadingDeg, point.x, point.z);
                }
                if (GameInput.MousePressed(0) || GameInput.Pressed(GameKey.Space)) _fireRequested = true;
                if (GameInput.Pressed(GameKey.R)) _model.ConvergenceRangeM += convergenceStepM;
                if (GameInput.Pressed(GameKey.F)) _model.ConvergenceRangeM -= convergenceStepM;

                if (_camera != null)
                {
                    float fov = GameInput.Held(GameKey.LeftShift) ? rangefinderFov : normalFov;
                    _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, fov, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
            }

            // Visual: la torre y la cuna siguen al modelo (la simulación manda, la malla obedece).
            if (turretPivot != null) turretPivot.localRotation = Quaternion.Euler(0f, _model.TrainDeg, 0f);
            if (gunCradle != null) gunCradle.localRotation = Quaternion.Euler(-_model.ElevationDeg, 0f, 0f);
        }

        private void FixedUpdate()
        {
            if (_model == null) return;
            _model.Step(Time.fixedDeltaTime);
            if (_fireRequested)
            {
                _fireRequested = false;
                Fire();
            }
        }

        private void Fire()
        {
            List<ShellLaunch> launches = _model.Fire(ship.Motion.HeadingDeg);
            if (launches.Count == 0 || shellPrefab == null) return;

            foreach (ShellLaunch launch in launches)
            {
                ShellLauncher.Launch(shellPrefab, MuzzlePosition(launch.GunIndex, launch.LateralOffsetM), launch, ship.Velocity, ship.gameObject);
            }
        }

        private Vector3 MuzzlePosition(int gunIndex, float lateralOffset)
        {
            if (gunIndex < muzzles.Length && muzzles[gunIndex] != null) return muzzles[gunIndex].position;
            Transform basis = turretPivot != null ? turretPivot : ship.transform;
            return basis.position + basis.right * lateralOffset + Vector3.up * 2f;
        }

        private bool TryMouseOnSea(out Vector3 point)
        {
            Vector3 mouse = GameInput.MousePosition;
            Ray ray = _camera.ScreenPointToRay(mouse);
            var sea = new Plane(Vector3.up, new Vector3(0f, seaLevel, 0f));
            if (sea.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        // ------------------------------------------------------------------------------------------
        // Retícula de convergencia (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_model == null || !playerControlled || _camera == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            }

            Vector3 origin = PivotPosition;
            float azimuth = ship.Motion.HeadingDeg + _model.TrainDeg;
            float range = Ballistics.Range(_model.Gun.MuzzleVelocityMps, Mathf.Max(0.01f, _model.ElevationDeg));
            Vector3 forward = Quaternion.Euler(0f, azimuth, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, azimuth, 0f) * Vector3.right;
            Vector3 center = new Vector3(origin.x, seaLevel, origin.z) + forward * range;

            bool blind = _model.IsInBlindArc(_model.TrainDeg);
            GUI.color = blind ? new Color(1f, 0.3f, 0.2f) : _model.IsOnTarget() ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.3f);

            for (int i = 0; i < _model.GunCount; i++)
            {
                Vector3 impact = center + right * _model.LateralMissAt(i, range);
                DrawMarker(impact, _model.IsLoaded(i) ? "●" : "○");
            }

            float spread = range * Mathf.Tan(_model.DispersionDeg * Mathf.Deg2Rad);
            DrawMarker(center + right * spread, "·");
            DrawMarker(center - right * spread, "·");

            string status = blind ? "SECTOR CIEGO" : _model.TargetOutOfRange ? "FUERA DE ALCANCE" : range.ToString("0") + " m";
            DrawMarker(center + Vector3.up * 8f, status);
            GUI.color = Color.white;
        }

        private void DrawMarker(Vector3 world, string text)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 20f);
            GUI.Label(rect, text, _style);
        }
    }
}
