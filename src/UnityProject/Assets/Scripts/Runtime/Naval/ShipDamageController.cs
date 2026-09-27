using System;
using Pacifico.Core.Naval;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Control de averías en escena (ROADMAP 2.4). Aplica impactos y embestidas al <see cref="ShipDamageState"/>,
    /// traslada la pérdida de potencia al modelo de maniobra y ofrece la brigada al jugador:
    /// 1 = incendios, 2 = achique, 3 = reparar vapor. Al hundirse, el casco escora y desaparece bajo el agua.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class ShipDamageController : MonoBehaviour
    {
        [SerializeField] private bool playerControlled = true;
        [SerializeField] private float sinkDepth = 14f;
        [SerializeField] private float sinkDurationSeconds = 25f;

        private ShipController _ship;
        private ArmoredHull _hull;
        private float _sinkProgress;
        private float _listSide = 1f;
        private string _lastEvent = string.Empty;

        public ShipDamageState State { get; private set; }

        public bool PlayerControlled
        {
            get => playerControlled;
            set => playerControlled = value;
        }

        public event Action<ShipDamageController> Sunk;

        private void Start()
        {
            _ship = GetComponent<ShipController>();
            if (_ship.Spec == null)
            {
                enabled = false;
                return;
            }

            State = new ShipDamageState(_ship.Spec, GetInstanceID());
            State.Sunk += OnSunk;

            _hull = GetComponent<ArmoredHull>();
            if (_hull != null) _hull.ImpactResolved += OnImpact;

            if (playerControlled && NavalHud.Active != null)
            {
                NavalHud.Active.ExtraLines.Add(StatusLine);
                NavalHud.Active.ExtraLines.Add(BrigadeLine);
                NavalHud.Active.ExtraLines.Add(() => _lastEvent);
            }
        }

        private void OnDestroy()
        {
            if (_hull != null) _hull.ImpactResolved -= OnImpact;
            if (State != null) State.Sunk -= OnSunk;
        }

        private void OnImpact(ResolvedImpact impact)
        {
            State.ApplyImpact(impact.Result, impact.Zone, impact.BelowWaterline, impact.Hit.CaliberMm);
            _lastEvent = ArmoredHull.Describe(impact);
            _listSide = Mathf.Sign(Vector3.Dot(impact.Hit.Point - transform.position, transform.right));
        }

        /// <summary>Recibe una embestida (llamado por el <see cref="RamBow"/> del atacante).</summary>
        public void ReceiveRam(RamResult ram)
        {
            State?.ApplyRamReceived(ram);
            _lastEvent = ram.FramesBroken ? "¡ESPOLONAZO! Cuadernas partidas" : "Roce de espolón";
        }

        public void DealtRam(RamResult ram)
        {
            State?.ApplyRamDealt(ram);
            _lastEvent = ram.Critical ? "Espolonazo a " + (ram.ClosingSpeed * 1.944f).ToString("0.0") + " nudos" : "Roce";
        }

        private void Update()
        {
            if (State == null || State.IsSunk || !playerControlled) return;
            if (GameInput.Pressed(GameKey.Digit1)) State.Activate(DamageControlAction.FireFighting);
            if (GameInput.Pressed(GameKey.Digit2)) State.Activate(DamageControlAction.Pumping);
            if (GameInput.Pressed(GameKey.Digit3)) State.Activate(DamageControlAction.SteamRepair);
        }

        private void FixedUpdate()
        {
            if (State == null) return;
            State.Step(Time.fixedDeltaTime);
            if (_ship.Motion != null) _ship.Motion.PropulsionFactor = State.PropulsionFactor;

            // Escora progresiva por inundación y, una vez hundido, descenso bajo el agua.
            if (State.IsSunk) _sinkProgress = Mathf.Min(1f, _sinkProgress + Time.fixedDeltaTime / sinkDurationSeconds);
            float eased = _sinkProgress * _sinkProgress;
            float floodList = State.FloodFraction * 8f;
            _ship.SetSinkPose(sinkDepth * eased, eased * 12f, _listSide * (floodList + eased * 35f));
        }

        private void OnSunk()
        {
            _lastEvent = _ship.Spec.Name + " se hunde";
            _ship.PlayerControlled = false;
            _ship.Command(EngineOrder.Stop, 0f);
            foreach (var turret in GetComponentsInChildren<ColesTurretController>()) turret.enabled = false;
            Sunk?.Invoke(this);
        }

        private string StatusLine()
        {
            if (State == null) return string.Empty;
            return "Casco " + (State.IntegrityFraction * 100f).ToString("0") + "%  ·  Agua " + State.WaterTonnes.ToString("0") + "/" +
                   State.ReserveBuoyancy.ToString("0") + " t  ·  " + (State.OnFire ? "FUEGO" : "sin fuego") +
                   "  ·  Vapor " + ((1f - State.BoilerDamage) * 100f).ToString("0") + "%";
        }

        private string BrigadeLine()
        {
            if (State == null) return string.Empty;
            if (State.ActiveAction != null)
            {
                return "Brigada: " + ActionName(State.ActiveAction.Value) + " (" + State.ActiveRemaining.ToString("0") + " s)";
            }
            return State.CooldownRemaining > 0f
                ? "Brigada: descansando " + State.CooldownRemaining.ToString("0") + " s"
                : "Brigada lista: [1] incendios  [2] achique  [3] vapor";
        }

        private static string ActionName(DamageControlAction action)
        {
            switch (action)
            {
                case DamageControlAction.FireFighting: return "combatiendo incendios";
                case DamageControlAction.Pumping: return "achicando";
                default: return "reparando calderas";
            }
        }
    }
}
