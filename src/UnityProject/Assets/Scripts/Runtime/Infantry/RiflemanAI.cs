using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using Pacifico.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Fusilero de la IA en los capítulos FPS. Decide con <see cref="RiflemanBrain"/> (probado sin el motor) y aquí
    /// solo percibe y ejecuta: busca al enemigo vivo más cercano con línea de visión, camina por el NavMesh, dispara
    /// balas reales (<see cref="RifleBullet"/>, con la elevación que pide la distancia y el error de combate de su
    /// fusil), recarga con el ciclo de su arma (<see cref="RifleCycleModel"/>) y carga a la bayoneta.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public sealed class RiflemanAI : MonoBehaviour
    {
        private const float SenseIntervalSeconds = 0.4f;
        private const float WalkSpeed = 1.6f;
        private const float ChargeSpeed = 4f;
        private const float MaxSenseRangeM = 350f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private Combatant _self;
        private NavMeshAgent _agent;
        private RiflemanBrain _brain;
        private RifleCycleModel _rifle;
        private WeaponDataSO _weaponData;
        private WeaponSpec _weapon;
        private float _drag;
        private float _bayonetDamage;
        private Combatant _target;
        private bool _lineOfSight;
        private float _nextSense;
        private Vector3 _lastTargetPosition;
        private bool _targetMoving;
        private Transform _figure;

        /// <summary>Punto al que avanza cuando no tiene a quién disparar.</summary>
        public Vector3? Objective { get; set; }
        public bool HoldPosition { get; set; }
        public bool ChargeOrdered { get; set; }

        /// <summary>Si es mayor que cero, carga a la bayoneta al enemigo que vea a esta distancia (asalto del Morro).</summary>
        public float ChargeWithinM { get; set; }
        public RiflemanBrain Brain => _brain;
        public Combatant Target => _target;

        /// <summary>
        /// Configura un soldado recién creado (el objeto debe estar inactivo: así el <see cref="NavMeshAgent"/> se crea
        /// con el tipo de agente del NavMesh generado en tiempo de ejecución).
        /// </summary>
        public void Configure(WeaponDataSO weaponData, int cartridges, int seed, Transform figure, WeaponSpec bayonet)
        {
            _self = GetComponent<Combatant>();
            _weaponData = weaponData;
            _weapon = weaponData.ToSpec();
            _drag = SmallArmsBallistics.DragFactor(_weapon);
            _rifle = new RifleCycleModel(_weapon, cartridges) { AutoReload = true };
            _brain = new RiflemanBrain(_weapon, seed);
            _bayonetDamage = _weapon.MeleeDamage > 0f ? _weapon.MeleeDamage : bayonet != null ? bayonet.MeleeDamage : 35f;
            _figure = figure;

            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.agentTypeID = NavMeshRuntimeBaker.AgentTypeId;
            _agent.radius = 0.3f;
            _agent.height = 1.75f;
            _agent.speed = WalkSpeed;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 6f;
            _agent.stoppingDistance = 0.5f;
            _agent.autoBraking = true;
            _nextSense = Random.value * SenseIntervalSeconds; // no todos piensan en el mismo fotograma

            _self.DropProvider = () => (_weaponData, _rifle.TotalRounds);
        }

        private void Update()
        {
            if (_brain == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!_self.Alive)
            {
                _brain.Kill();
                enabled = false;
                return;
            }
            _rifle.Step(dt);

            if (Time.time >= _nextSense)
            {
                _nextSense = Time.time + SenseIntervalSeconds;
                Sense();
            }

            var p = new RiflemanPerception
            {
                HasTarget = _target != null && _target.Alive,
                TargetDistanceM = _target != null ? Vector3.Distance(transform.position, _target.transform.position) : float.PositiveInfinity,
                LineOfSight = _lineOfSight,
                TargetMoving = _targetMoving,
                Loaded = _rifle.CanFire,
                HasAmmo = _rifle.TotalRounds > 0,
                ChargeOrdered = ChargeOrdered || (ChargeWithinM > 0f && _target != null && _lineOfSight &&
                                                  Vector3.Distance(transform.position, _target.transform.position) <= ChargeWithinM),
                HoldPosition = HoldPosition,
            };
            RiflemanDecision d = _brain.Step(dt, p);
            Act(d);
        }

        private void Sense()
        {
            Combatant best = null;
            float bestDistance = MaxSenseRangeM;
            bool bestVisible = false;
            foreach (Combatant c in Combatant.All)
            {
                if (!c.Alive || !Combatant.AreEnemies(_self, c)) continue;
                float distance = Vector3.Distance(transform.position, c.transform.position);
                if (distance > MaxSenseRangeM) continue;
                bool visible = CanSee(c);
                // Prefiere al que ve; entre iguales, al más cercano.
                if (best == null || (visible && !bestVisible) || (visible == bestVisible && distance < bestDistance))
                {
                    best = c;
                    bestDistance = distance;
                    bestVisible = visible;
                }
            }
            if (best != null)
            {
                _targetMoving = best == _target && Vector3.Distance(best.transform.position, _lastTargetPosition) > 0.8f * SenseIntervalSeconds;
                _lastTargetPosition = best.transform.position;
            }
            _target = best;
            _lineOfSight = bestVisible;
        }

        private bool CanSee(Combatant other)
        {
            Vector3 eye = _self.EyePoint;
            Vector3 to = other.AimPoint - eye;
            float distance = to.magnitude;
            int count = Physics.RaycastNonAlloc(eye, to / distance, Hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(other.transform)) continue;
                // Los cuerpos de otros soldados no tapan del todo la vista (se dispara entre ellos).
                if (t.GetComponentInParent<Combatant>() != null) continue;
                return false;
            }
            return true;
        }

        private void Act(RiflemanDecision d)
        {
            bool onMesh = _agent.enabled && _agent.isOnNavMesh;
            if (onMesh)
            {
                _agent.speed = d.State == RiflemanState.Charge ? ChargeSpeed : WalkSpeed;
                if (d.MoveToTarget && _target != null) _agent.SetDestination(_target.transform.position);
                else if (d.MoveToObjective && Objective.HasValue) _agent.SetDestination(Objective.Value);
                _agent.isStopped = !(d.MoveToTarget || d.MoveToObjective);
            }
            if (_target != null && (d.State == RiflemanState.Engage || d.State == RiflemanState.Melee || d.State == RiflemanState.Reload))
            {
                Vector3 look = Vector3.ProjectOnPlane(_target.transform.position - transform.position, Vector3.up);
                if (look.sqrMagnitude > 1e-3f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 240f * Time.deltaTime);
                }
            }
            if (_figure != null)
            {
                float height = d.Kneel ? 0.65f : 1f;
                Vector3 s = _figure.localScale;
                _figure.localScale = new Vector3(1f, Mathf.MoveTowards(s.y, height, Time.deltaTime / 0.3f), 1f);
            }
            if (d.Fire) Fire();
            if (d.Strike && _target != null) _target.Hurt(_bayonetDamage, gameObject, transform.position);
        }

        private void Fire()
        {
            if (_target == null || _rifle.PullTrigger() != TriggerResult.Fired) return;
            Vector3 eye = _self.EyePoint;
            Vector3 aim = _target.AimPoint;
            float distance = Vector3.Distance(eye, aim);
            // Elevación para que la bala llegue a esa distancia (el tirador de la IA gradúa bien el alza).
            SmallArmsBallistics.TrySolveElevation(distance, _weapon.MuzzleVelocityMps, _drag, out float elevation, 0f);
            float sigma = RiflemanBrain.SigmaDeg(_weapon, _targetMoving, 0f);
            var error = _brain.SampleShot(sigma);
            Quaternion look = Quaternion.LookRotation(aim - eye);
            Vector3 direction = look * Quaternion.Euler(-(elevation + error.PitchDeg), error.YawDeg, 0f) * Vector3.forward;
            RifleBullet.Fire(eye + direction * 0.6f, direction * _weapon.MuzzleVelocityMps, _weapon, _drag, gameObject, Physics.DefaultRaycastLayers);
            BlackPowderSmoke.Emit(eye + direction * 1.1f, direction, _weapon.PowderChargeG, Vector3.zero);
        }
    }
}
