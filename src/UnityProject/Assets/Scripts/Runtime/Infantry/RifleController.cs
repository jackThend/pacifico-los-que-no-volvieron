using System;
using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Effects;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Fusil del infante (ROADMAP 3.2): gatillo (clic izquierdo), recarga manual (R), ciclo de disparo y recarga
    /// (<see cref="RifleCycleModel"/>), retroceso y disparo balístico con la elevación del alza y la dispersión.
    /// Correr o deslizarse detiene la manipulación del arma; durante la recarga no se puede encarar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController))]
    // Antes que FirstPersonController: así AimBlocked ya refleja la etapa de este fotograma cuando se leen los mandos.
    [DefaultExecutionOrder(-50)]
    public sealed class RifleController : MonoBehaviour
    {
        [Tooltip("Cartuchos en las cartucheras al empezar (la dotación chilena rondaba los 100–150).")]
        [SerializeField] private int startingReserve = 100;
        [SerializeField] private bool autoReload = true;
        [Tooltip("Capas que pueden recibir balas (por defecto, todas salvo «Ignore Raycast»: vainas, efectos).")]
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("Boca del cañón del fusil en primera persona (humo y fogonazo). Si falta, se estima delante del ojo.")]
        [SerializeField] private Transform muzzle;

        /// <summary>Distancia del ojo a la boca con el fusil encarado cuando no hay <see cref="Muzzle"/> (≈ 1,2 m).</summary>
        private const float DefaultMuzzleDistanceM = 1.2f;

        private FirstPersonController _fps;
        private System.Random _random;
        private float _dragFactor;

        /// <summary>
        /// Cartucheras por tipo de cartucho: al cambiar de arma, los cartuchos (incluidos el de la recámara y los del
        /// depósito) se guardan y vuelven a estar disponibles si se empuña de nuevo un arma de ese calibre.
        /// </summary>
        private readonly Dictionary<string, int> _pouches = new Dictionary<string, int>();
        private bool _started;

        public RifleCycleModel Model { get; private set; }

        /// <summary>Cada evento del ciclo (animación procedimental, sonidos, vainas).</summary>
        public event Action<RifleEvent> CycleEvent;

        /// <summary>Resultado de cada pulsación del gatillo (disparo, «clic» en vacío).</summary>
        public event Action<TriggerResult> TriggerPulled;

        public int StartingReserve
        {
            get => startingReserve;
            set => startingReserve = value;
        }

        public Transform Muzzle
        {
            get => muzzle;
            set => muzzle = value;
        }

        private void Awake()
        {
            _fps = GetComponent<FirstPersonController>();
            _random = new System.Random(GetInstanceID());
        }

        private void OnEnable()
        {
            _fps.WeaponChanged += OnWeaponChanged;
            // Si se cambió de arma mientras el componente estaba desactivado, el ciclo sería el del arma anterior.
            if (_started && ModelIsStale()) OnWeaponChanged(_fps);
        }

        private bool ModelIsStale()
        {
            WeaponSpec weapon = _fps.Weapon;
            bool firearm = weapon != null && weapon.IsFirearm;
            if (!firearm) return Model != null;
            return Model == null || Model.Weapon.Id != weapon.Id;
        }

        private void OnDisable()
        {
            _fps.WeaponChanged -= OnWeaponChanged;
            _fps.AimBlocked = false;
        }

        private void Start()
        {
            // Dotación inicial: arma cargada y cartucheras llenas.
            WeaponSpec weapon = _fps.Weapon;
            if (weapon != null && weapon.IsFirearm && Model == null)
            {
                Model = new RifleCycleModel(weapon, startingReserve) { AutoReload = autoReload };
                _dragFactor = SmallArmsBallistics.DragFactor(weapon);
            }
            _started = true;
        }

        private void OnWeaponChanged(FirstPersonController fps)
        {
            // Por DefaultExecutionOrder(-50) este componente se suscribe antes del Awake de FirstPersonController, que
            // equipa el arma inicial y emite este evento: la dotación inicial la construye Start, no este cambio.
            if (!_started) return;

            // 1) Se guardan todos los cartuchos del arma que se deja (recámara, depósito y cartucheras).
            if (Model != null) Deposit(Model.Weapon.Cartridge, Model.TotalRounds);
            Model = null;

            WeaponSpec weapon = fps.Weapon;
            if (weapon == null || !weapon.IsFirearm) return;

            // 2) La nueva arma se empuña descargada y se carga con lo que haya de su calibre; nada se regala.
            int reserve = Withdraw(weapon.Cartridge);
            Model = new RifleCycleModel(weapon, reserve, startChambered: false, startMagazine: 0) { AutoReload = autoReload };
            _dragFactor = SmallArmsBallistics.DragFactor(weapon);
            Model.Reload();
        }

        private void Deposit(string cartridge, int rounds)
        {
            if (rounds <= 0) return;
            _pouches.TryGetValue(cartridge, out int stored);
            _pouches[cartridge] = stored + rounds;
        }

        private int Withdraw(string cartridge)
        {
            if (!_pouches.TryGetValue(cartridge, out int stored)) return 0;
            _pouches.Remove(cartridge);
            return stored;
        }

        /// <summary>Munición recogida del suelo o de un caído. Solo sirve si es del mismo cartucho.</summary>
        public int AddAmmo(string cartridge, int rounds)
        {
            if (Model == null) return 0;
            if (!string.IsNullOrEmpty(cartridge) && cartridge != Model.Weapon.Cartridge) return 0;
            return Model.AddAmmo(rounds);
        }

        private void Update()
        {
            if (Model == null) return;
            float dt = Time.deltaTime;
            // En pausa (timeScale 0) no se lee el gatillo: el disparo se resolvería de golpe al reanudar.
            if (dt <= 0f) return;

            InfantryMotor motor = _fps.Motor;
            bool handling = motor.IsSprinting || motor.Stance == Stance.Sliding;

            if (_fps.HasInputFocus && !handling)
            {
                if (GameInput.MousePressed(0)) PullTrigger();
                if (GameInput.Pressed(GameKey.R)) Model.Reload();
            }

            // Correr o deslizarse detiene la manipulación del arma (se retoma al volver a caminar).
            // Bucle for sobre la lista: un foreach sobre IReadOnlyList encajonaría el enumerador en cada fotograma.
            IReadOnlyList<RifleEvent> events = Model.Step(dt, paused: handling);
            for (int i = 0; i < events.Count; i++) CycleEvent?.Invoke(events[i]);

            _fps.AimBlocked = Model.BlocksAiming;
        }

        private void PullTrigger()
        {
            TriggerResult result = Model.PullTrigger();
            TriggerPulled?.Invoke(result);
            if (result != TriggerResult.Fired) return;

            float aim01 = _fps.Sights != null ? _fps.Sights.Eased : 0f;
            bool moving = _fps.Motor.HorizontalSpeed > 0.3f;
            float elevation = _fps.Ladder != null ? _fps.Ladder.ElevationDeg : 0f;
            ShotDirection shot = RifleShotSolver.Solve(Model.Weapon, elevation, aim01, moving, _random);

            // La bala sale del ánima, un poco por debajo de la línea de mira; la elevación del alza la hace cruzarla
            // a la distancia graduada.
            Transform eye = _fps.ViewCamera != null ? _fps.ViewCamera.transform : transform;
            float sightHeight = _fps.Ladder != null ? _fps.Ladder.SightHeightM : SmallArmsBallistics.DefaultSightHeightM;
            Vector3 origin = eye.position - eye.up * sightHeight;
            Vector3 direction = eye.rotation * Quaternion.Euler(-shot.PitchDeg, shot.YawDeg, 0f) * Vector3.forward;
            RifleBullet.Fire(origin, direction * Model.Weapon.MuzzleVelocityMps, Model.Weapon, _dragFactor, gameObject, hitMask);

            // Humo de pólvora negra (ROADMAP 3.3): sale de la boca en la dirección del ánima, con la velocidad del tirador.
            Vector3 muzzlePosition = muzzle != null ? muzzle.position : origin + eye.forward * DefaultMuzzleDistanceM;
            Vec3 v = _fps.Motor.Velocity;
            BlackPowderSmoke.Emit(muzzlePosition, eye.forward, Model.Weapon.PowderChargeG, new Vector3(v.X, v.Y, v.Z));

            _fps.Recoil.Kick(Model.Weapon, aim01, _fps.Motor.Stance == Stance.Crouching);
        }
    }
}
