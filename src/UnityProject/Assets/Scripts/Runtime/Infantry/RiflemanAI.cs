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
        /// <summary>
        /// Segundos queriendo andar sin avanzar un metro antes de recolocarlo (revisión del 2026-11-07: en Tarapacá
        /// quedaban soldados en una isla del NavMesh con camino parcial y el capítulo no terminaba). Valor de balance.
        /// </summary>
        private const float StuckSeconds = 12f;
        /// <summary>Más cerca que esto del jugador y en su campo de visión, no se recoloca (se vería el salto).</summary>
        private const float VisibleToPlayerM = 90f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private Vector3 _progressPoint;
        private float _progressTime;
        private Vector3 _requestedDestination = new Vector3(float.MaxValue, 0f, 0f);
        private float _requestedAt = float.NegativeInfinity;

        /// <summary>Veces que la IA se ha recolocado por estar atascada (para el registro de pruebas).</summary>
        public static int Unstuck { get; private set; }
        /// <summary>Veces que estaba atascado pero a la vista del jugador (no se recoloca: el jugador puede ir a por él).</summary>
        public static int StuckInSight { get; private set; }
        /// <summary>Veces que estaba atascado y no se encontró ningún sitio libre donde ponerlo.</summary>
        public static int StuckNoSpot { get; private set; }
        /// <summary>Veces que la vigilancia llegó a comprobar un atasco (para el registro de pruebas).</summary>
        public static int StuckChecks { get; private set; }

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
        private SoldierFigure _pose;

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
            _pose = figure != null ? figure.GetComponentInChildren<SoldierFigure>(true) : null;

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
            WatchStuck(d);
        }

        /// <summary>
        /// Si lleva <see cref="StuckSeconds"/> queriendo andar sin avanzar porque su destino queda fuera de su trozo de
        /// NavMesh (o él mismo quedó fuera del NavMesh), lo recoloca donde sí hay camino, siempre fuera de la vista.
        /// </summary>
        private void WatchStuck(RiflemanDecision d)
        {
            bool wantsToMove = _agent.enabled && !HoldPosition && (d.MoveToTarget || d.MoveToObjective);
            if (!wantsToMove || (transform.position - _progressPoint).sqrMagnitude > 1f)
            {
                _progressPoint = transform.position;
                _progressTime = Time.time;
                return;
            }
            if (Time.time - _progressTime < StuckSeconds) return;
            _progressPoint = transform.position;
            _progressTime = Time.time;
            StuckChecks++;

            var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit near, 15f, filter) && !SeenByPlayer(near.position)) Warp(near.position);
                return;
            }
            // No se mira el estado del camino: con el robot, dos chilenos quedaron 10 minutos parados a 40 m de su
            // blanco con un camino «completo» y nada por recorrer (el blanco, fuera del NavMesh, sobre una tapia).
            Vector3 goal = d.MoveToTarget && _target != null ? _target.transform.position : Objective ?? transform.position;
            if (Vector3.Distance(transform.position, goal) < 5f) return;
            if (SeenByPlayer(transform.position))
            {
                StuckInSight++;
                return;
            }
            // El destino puede estar fuera del NavMesh (un soldado subido a una tapia): se busca el punto del NavMesh
            // más cercano en un radio amplio.
            Vector3 anchor = NavMesh.SamplePosition(goal, out NavMeshHit goalHit, 15f, filter) ? goalHit.position : goal;
            // Un punto unido al destino, lo más atrás posible en dirección a donde estaba (no aparecer encima de nadie).
            Vector3 back = Vector3.ProjectOnPlane(transform.position - anchor, Vector3.up);
            back = back.sqrMagnitude > 1e-3f ? back.normalized : -transform.forward;
            var path = new NavMeshPath();
            Vector3? fallback = null;
            foreach (float m in new[] { 30f, 22f, 15f, 8f })
            {
                if (!NavMesh.SamplePosition(anchor + back * m, out NavMeshHit candidate, 5f, filter)) continue;
                if (SeenByPlayer(candidate.position)) continue;
                if (NavMesh.CalculatePath(candidate.position, anchor, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    Warp(candidate.position);
                    return;
                }
                fallback = candidate.position; // el más cercano que no se ve: si no hay camino a ningún sitio, allí
            }
            if (fallback.HasValue) Warp(fallback.Value);
            else StuckNoSpot++;
        }

        private void Warp(Vector3 position)
        {
            if (!_agent.Warp(position)) return;
            Unstuck++;
            _progressPoint = position;
            _requestedDestination = new Vector3(float.MaxValue, 0f, 0f); // pedir camino de nuevo desde el sitio nuevo
        }

        /// <summary>El jugador lo tiene delante, sin nada en medio y a una distancia a la que se notaría un salto.</summary>
        private static bool SeenByPlayer(Vector3 point)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 chest = point + Vector3.up * 1.2f;
            Vector3 v = camera.WorldToViewportPoint(chest);
            bool inView = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
            if (!inView || v.z >= VisibleToPlayerM) return false;
            // Una tapia o una casa en medio lo tapa: los soldados no cuentan como obstáculo.
            Vector3 eye = camera.transform.position;
            Vector3 to = chest - eye;
            int count = Physics.RaycastNonAlloc(eye, to.normalized, Hits, to.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider.GetComponentInParent<Combatant>() == null) return false;
            }
            return true;
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
                if (d.MoveToTarget && _target != null) MoveTo(_target.transform.position);
                else if (d.MoveToObjective && Objective.HasValue) MoveTo(Objective.Value);
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
            if (_pose != null) _pose.AimTarget = d.State == RiflemanState.Engage || d.State == RiflemanState.Reload ? 1f : 0f;
            if (d.Fire) Fire();
            if (d.Strike && _target != null) _target.Hurt(_bayonetDamage, gameObject, transform.position);
        }

        /// <summary>
        /// Pide camino solo si el destino cambió de verdad. Antes se pedía en cada fotograma: con un camino difícil,
        /// Unity no llegaba a terminar de calcularlo y el soldado se quedaba quieto «esperando» (robot, Tarapacá).
        /// </summary>
        private void MoveTo(Vector3 destination)
        {
            float moved = (destination - _requestedDestination).sqrMagnitude;
            if (_agent.pathPending && moved < 9f) return;
            if (_agent.hasPath && moved < 1f && Time.time - _requestedAt < 2f) return;
            _agent.SetDestination(destination);
            _requestedDestination = destination;
            _requestedAt = Time.time;
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
