using System;
using System.Collections.Generic;
using Pacifico.Core.Naval;
using Pacifico.Core.Ships;
using UnityEngine;

namespace Pacifico.Runtime.Naval
{
    /// <summary>
    /// Integridad, cuadernas y control de averías de un buque en escena. Se
    /// registra en una lista global para que cañones y espolones puedan
    /// resolver impactos contra él. El jugador asigna la cuadrilla con 1/2/3
    /// (fuego / achique / calderas); la IA la asigna sola. Al hundirse se
    /// detienen las máquinas y el casco desciende.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipDamageReceiver : MonoBehaviour
    {
        private static readonly List<ShipDamageReceiver> Registered = new List<ShipDamageReceiver>();

        [SerializeField] private ShipNavigationController ship;
        [SerializeField, Min(0f), Tooltip("Metros por segundo que desciende el casco al hundirse.")] private float sinkSpeed = 0.6f;
        [SerializeField] private bool showDamageHud = true;
        [SerializeField, Tooltip("Semilla de las averías aleatorias por impacto (0 = aleatoria).")] private int randomSeed;

        private HullIntegrity hull;
        private HullFrames frames;
        private DamageControlSystem damageControl;
        private System.Random random;

        public static IReadOnlyList<ShipDamageReceiver> All => Registered;
        public HullIntegrity Hull => hull;
        public HullFrames Frames => frames;
        public DamageControlSystem DamageControl => damageControl;
        public ShipSpec Spec { get; private set; }
        public ShipNavigationController Ship => ship;
        public ImpactReport LastImpact { get; private set; }

        public event Action<ImpactReport> Impacted;

        public void Configure(ShipNavigationController owner) => ship = owner;

        public ShipPose Pose
        {
            get
            {
                var motion = ship.Motion;
                return new ShipPose(motion.PositionX, motion.PositionZ, motion.HeadingDegrees);
            }
        }

        private void Start()
        {
            if (ship == null) ship = GetComponent<ShipNavigationController>();
            if (ship == null || ship.ShipData == null)
            {
                Debug.LogError($"[ShipDamageReceiver] {name}: falta ShipNavigationController con ShipDataSO.", this);
                enabled = false;
                return;
            }
            Spec = ship.ShipData.ToSpec();
            hull = new HullIntegrity(Spec.HullIntegrity);
            hull.Sunk += OnSunk;
            frames = HullFrames.ForShip(Spec);
            damageControl = new DamageControlSystem(Spec, hull);
            random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
        }

        private void OnEnable() => Registered.Add(this);
        private void OnDisable() => Registered.Remove(this);

        public void ApplyImpact(ImpactReport report)
        {
            if (hull == null || hull.IsSunk) return;
            LastImpact = report;
            hull.ApplyImpact(report);
            damageControl.ApplyShellImpact(report, random.NextDouble);
            Impacted?.Invoke(report);
        }

        /// <summary>Recibe un espolonazo ya resuelto contra <see cref="Frames"/>.</summary>
        public void ApplyRam(RamReport report)
        {
            if (hull == null || hull.IsSunk) return;
            hull.ApplyDamage(report.TargetDamage);
            damageControl.ApplyRam(report);
        }

        /// <summary>Daño propio al embestir.</summary>
        public void ApplyStructuralDamage(float amount)
        {
            if (hull == null) return;
            hull.ApplyDamage(amount);
        }

        private void FixedUpdate()
        {
            if (damageControl == null || hull.IsSunk) return;
            if (!ship.PlayerControlled) damageControl.AssignTask(damageControl.SuggestTask());
            damageControl.Step(Time.fixedDeltaTime);
            ship.Motion.SetPowerLimit(damageControl.PowerLimit);
        }

        private void OnSunk()
        {
            ship.SetOrder(EngineOrder.Stop);
            ship.SetRudderCommand(0f);
            Debug.Log($"[Pacífico] {Spec.DisplayName} se hunde.");
        }

        private void Update()
        {
            if (hull == null) return;
            if (!hull.IsSunk && ship.PlayerControlled) ReadDamageControlKeys();
            if (!hull.IsSunk) return;
            var position = transform.position;
            if (position.y > -Spec.LengthM) transform.position = new Vector3(position.x, position.y - sinkSpeed * Time.deltaTime, position.z);
        }

        private void ReadDamageControlKeys()
        {
            if (GameInput.KeyDown(GameKey.Digit1)) damageControl.ToggleTask(DamageControlTask.Firefighting);
            if (GameInput.KeyDown(GameKey.Digit2)) damageControl.ToggleTask(DamageControlTask.Pumping);
            if (GameInput.KeyDown(GameKey.Digit3)) damageControl.ToggleTask(DamageControlTask.BoilerRepair);
        }

        private static string TaskName(DamageControlTask task)
        {
            switch (task)
            {
                case DamageControlTask.Firefighting: return "extinguiendo incendio";
                case DamageControlTask.Pumping: return "achicando y apuntalando";
                case DamageControlTask.BoilerRepair: return "reparando calderas";
                default: return "sin asignar";
            }
        }

        private void OnGUI()
        {
            if (!showDamageHud || damageControl == null) return;
            var dc = damageControl;
            var text =
                $"{Spec.DisplayName}: casco {hull.Fraction * 100f:0}%  cuadernas rotas {frames.BrokenCount}/{frames.Count}\n" +
                $"Incendio {dc.FireIntensity * 100f:0}%  Agua {dc.WaterTons:0}/{dc.ReserveBuoyancyTons:0} t (+{dc.InflowTonsPerMinute:0} t/min)  " +
                $"Calderas {dc.BoilerHealth * 100f:0}%  Potencia {dc.PowerLimit * 100f:0}%\n" +
                $"Cuadrilla: {TaskName(dc.Task)}" + (ship.PlayerControlled ? "   [1] fuego  [2] achique  [3] calderas" : string.Empty) +
                (hull.IsSunk ? "\nSE HUNDE" : string.Empty);
            GUI.Label(new Rect(12f, ship.PlayerControlled ? 230f : 300f, 620f, 70f), text);
        }
    }
}
