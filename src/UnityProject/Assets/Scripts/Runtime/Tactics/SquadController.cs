using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace Pacifico.Tactics
{
    public enum SquadOrderKind
    {
        Hold = 0,
        Move = 1,
        Attack = 2,
    }

    /// <summary>
    /// Escuadra RTS de 8 a 12 hombres (ROADMAP 4.1). El mando (formación, puestos, marcha, cohesión, bajas) es
    /// <see cref="SquadCommand"/> y el fuego, <see cref="SquadFireControl"/>, ambos probados fuera del motor; aquí se
    /// crean los soldados, se calculan los caminos por el NavMesh y cada <see cref="NavMeshAgent"/> persigue su puesto
    /// a la velocidad que le pide la escuadra.
    /// <para>
    /// Órdenes: moverse (con orientación final), atacar a otra escuadra (se acerca hasta su alcance eficaz, se detiene,
    /// encara y rompe el fuego) y alto. Parada, responde por su cuenta al enemigo que tenga a tiro.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadController : MonoBehaviour
    {
        [SerializeField] private string displayName = "Escuadra";
        [SerializeField] private Faction faction = Faction.Bolivia;
        [SerializeField] private WeaponDataSO weapon;
        [SerializeField, Range(1, 16)] private int soldierCount = 12;
        [SerializeField] private FormationType formation = FormationType.Line;
        [SerializeField] private bool playerControlled;
        [SerializeField] private Material uniformMaterial;
        [SerializeField] private Material trimMaterial;

        private const float DestinationTolerance = 0.3f;
        private const float RetargetSeconds = 1f;
        private const float SightCheckSeconds = 0.5f;
        private const float RepathSeconds = 2f;

        private static readonly List<SquadController> s_all = new List<SquadController>();

        private readonly List<SoldierUnit> _soldiers = new List<SoldierUnit>();
        private readonly List<Vec3> _positions = new List<Vec3>();
        private readonly List<Vector3> _lastDestination = new List<Vector3>();
        private readonly List<Vec3> _pathBuffer = new List<Vec3>();

        private SquadController _target;
        private SquadController _autoTarget;
        private float _retargetTimer;
        private float _sightTimer;
        private bool _lineOfSight;
        private SquadController _sightTarget;
        private float _repathTimer;

        /// <summary>Todas las escuadras vivas de la escena.</summary>
        public static IReadOnlyList<SquadController> All => s_all;

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public Faction Faction
        {
            get => faction;
            set => faction = value;
        }

        public bool PlayerControlled
        {
            get => playerControlled;
            set => playerControlled = value;
        }

        public SquadCommand Command { get; private set; }
        public SquadFireControl Fire { get; private set; }
        public WeaponSpec Weapon { get; private set; }
        public SquadOrderKind Order { get; private set; }
        public SquadController Target => Order == SquadOrderKind.Attack ? _target : _autoTarget;
        public int InitialStrength { get; private set; }
        public int Strength => _soldiers.Count;
        public bool IsAlive => _soldiers.Count > 0;
        public IReadOnlyList<SoldierUnit> Soldiers => _soldiers;
        public FormationType Formation => Command != null ? Command.Formation : formation;

        /// <summary>Postura que ofrece al enemigo (ROADMAP 4.2 la cambia bajo fuego).</summary>
        public Posture Posture { get; set; } = Posture.Standing;
        /// <summary>Fracción de la silueta tapada por la cobertura (ROADMAP 4.2).</summary>
        public float Cover { get; set; }

        /// <summary>Último resultado del fuego propio (para el HUD).</summary>
        public VolleyResult LastVolley { get; private set; }
        public int ShotsFired { get; private set; }
        public int EnemyCasualties { get; private set; }

        public Vector3 Anchor => ToVector3(Command.March.Anchor);
        public Vector3 Facing => ToVector3(Command.March.Facing);

        public void Configure(string name, Faction side, WeaponDataSO weaponData, int count, FormationType type, bool player,
                              Material uniform, Material trim)
        {
            displayName = name;
            faction = side;
            weapon = weaponData;
            soldierCount = count;
            formation = type;
            playerControlled = player;
            uniformMaterial = uniform;
            trimMaterial = trim;
        }

        /// <summary>¿Son enemigos? Chile contra la alianza de Perú y Bolivia.</summary>
        public bool IsEnemyOf(SquadController other)
        {
            if (other == null || other.faction == Faction.Neutral || faction == Faction.Neutral) return false;
            return (faction == Faction.Chile) != (other.faction == Faction.Chile);
        }

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        private void Start()
        {
            Weapon = weapon != null ? weapon.ToSpec() : WeaponCatalog.Remington();
            Vector3 anchor = NavMeshRuntimeBaker.Snap(transform.position);
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            Vec3[] local = Pacifico.Core.Tactics.Formation.LocalSlots(formation, soldierCount);
            for (int i = 0; i < local.Length; i++)
            {
                Vec3 slot = Pacifico.Core.Tactics.Formation.ToWorld(ToVec3(anchor), ToVec3(facing), local[i]);
                Vector3 position = NavMeshRuntimeBaker.Snap(ToVector3(slot));
                _soldiers.Add(CreateSoldier(i, position, facing));
                _positions.Add(ToVec3(position));
                _lastDestination.Add(position);
            }
            InitialStrength = _soldiers.Count;
            Command = new SquadCommand(formation, _positions, ToVec3(anchor), ToVec3(facing));
            Fire = new SquadFireControl(Weapon, _soldiers.Count, GetInstanceID());
        }

        // ------------------------------------------------------------------------------------------
        // Órdenes
        // ------------------------------------------------------------------------------------------

        public SquadFootprint Footprint => new SquadFootprint(Command.March.Anchor, Command.March.Facing, Strength, Command.Formation);

        public void IssueMove(Vector3 anchor, Vector3 facing)
        {
            if (!IsAlive) return;
            Order = SquadOrderKind.Move;
            _target = null;
            MarchTo(anchor, facing);
        }

        public void IssueAttack(SquadController enemy)
        {
            if (!IsAlive || enemy == null || !IsEnemyOf(enemy)) return;
            Order = SquadOrderKind.Attack;
            _target = enemy;
            _repathTimer = 0f;
        }

        public void IssueHalt()
        {
            if (!IsAlive) return;
            Order = SquadOrderKind.Hold;
            _target = null;
            Command.Halt();
        }

        public void SetFormation(FormationType type)
        {
            if (!IsAlive || Command.Formation == type) return;
            SyncPositions();
            Command.SetFormation(type, _positions);
        }

        private void MarchTo(Vector3 anchor, Vector3 facing)
        {
            SyncPositions();
            Vector3[] corners = NavMeshRuntimeBaker.Path(Anchor, anchor);
            _pathBuffer.Clear();
            foreach (Vector3 c in corners) _pathBuffer.Add(ToVec3(c));
            Command.MoveTo(_pathBuffer, ToVec3(facing), _positions);
        }

        // ------------------------------------------------------------------------------------------
        // Simulación
        // ------------------------------------------------------------------------------------------

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Command == null || !IsAlive) return;

            SyncPositions();
            // Llegada: la escuadra queda a la espera y responde al fuego por su cuenta.
            if (Order == SquadOrderKind.Move && !Command.March.Moving) Order = SquadOrderKind.Hold;
            SquadController target = UpdateEngagement(dt, out bool canFire, out float distance);
            Command.Step(dt, _positions);
            DriveSoldiers();

            VolleyResult volley = Fire.Step(dt, canFire, distance, target != null ? target.Posture : Posture.Standing, target != null ? target.Cover : 0f);
            LastVolley = volley;
            if (volley.Shots > 0)
            {
                ShotsFired += volley.Shots;
                EmitShots(volley.Shots, target);
            }
            if (volley.Casualties > 0 && target != null)
            {
                EnemyCasualties += target.TakeCasualties(volley.Casualties, CenterOfMass());
            }
        }

        /// <summary>Decide a quién se dispara y si se puede: a tiro, con línea de visión y parados.</summary>
        private SquadController UpdateEngagement(float dt, out bool canFire, out float distance)
        {
            canFire = false;
            distance = 0f;

            if (Order == SquadOrderKind.Attack && (_target == null || !_target.IsAlive))
            {
                // Objetivo aniquilado o desbandado: alto en el sitio.
                IssueHalt();
            }

            SquadController target;
            if (Order == SquadOrderKind.Attack)
            {
                target = _target;
            }
            else
            {
                _retargetTimer -= dt;
                if (_retargetTimer <= 0f || _autoTarget == null || !_autoTarget.IsAlive)
                {
                    _retargetTimer = RetargetSeconds;
                    _autoTarget = Order == SquadOrderKind.Hold ? NearestEnemyInRange() : null;
                }
                target = _autoTarget;
            }
            if (target == null) return null;

            Vector3 from = CenterOfMass(), to = target.CenterOfMass();
            Vector3 toTarget = Vector3.ProjectOnPlane(to - from, Vector3.up);
            distance = toTarget.magnitude;
            Vector3 direction = distance > 0.1f ? toTarget / distance : Facing;
            bool sight = HasLineOfSight(target);
            float range = Weapon.EffectiveRangeM;

            if (Order == SquadOrderKind.Attack)
            {
                _repathTimer -= dt;
                bool inPosition = distance <= range * 0.95f && sight;
                if (!inPosition)
                {
                    if (_repathTimer <= 0f)
                    {
                        _repathTimer = RepathSeconds;
                        // Hasta el 80 % del alcance eficaz; sin línea de visión (una duna), más cerca.
                        float standOff = sight ? range * 0.8f : Mathf.Max(30f, distance * 0.6f);
                        Vector3 destination = to - direction * Mathf.Min(standOff, distance);
                        MarchTo(destination, direction);
                    }
                }
                else if (Command.March.Moving)
                {
                    Command.Halt();
                }
            }

            // Parados, encaran al enemigo.
            if (!Command.March.Moving) Command.March.FaceTowards(ToVec3(direction), false);
            canFire = distance <= range && sight && !Command.March.Moving && Command.March.Speed < 0.1f;
            return target;
        }

        private SquadController NearestEnemyInRange()
        {
            SquadController best = null;
            float bestDistance = Weapon.EffectiveRangeM;
            Vector3 here = CenterOfMass();
            foreach (SquadController other in s_all)
            {
                if (other == this || !other.IsAlive || !IsEnemyOf(other)) continue;
                float d = Vector3.Distance(here, other.CenterOfMass());
                if (d > bestDistance || !HasLineOfSight(other)) continue;
                bestDistance = d;
                best = other;
            }
            return best;
        }

        /// <summary>Línea de visión de ojos a ojos (el terreno y los parapetos la cortan; los soldados no).</summary>
        public bool HasLineOfSight(SquadController other)
        {
            if (other == _sightTarget && _sightTimer > 0f) return _lineOfSight;
            Vector3 eye = CenterOfMass() + Vector3.up * SoldierUnit.EyeHeightM;
            Vector3 targetEye = other.CenterOfMass() + Vector3.up * EyeHeightOf(other);
            bool clear = !Physics.Linecast(eye, targetEye, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            _sightTarget = other;
            _lineOfSight = clear;
            _sightTimer = SightCheckSeconds;
            return clear;
        }

        private static float EyeHeightOf(SquadController squad)
        {
            switch (squad.Posture)
            {
                case Posture.Prone: return 0.3f;
                case Posture.Kneeling: return 1.0f;
                default: return SoldierUnit.EyeHeightM;
            }
        }

        private void LateUpdate()
        {
            _sightTimer -= Time.deltaTime;
        }

        /// <summary>Cada soldado persigue su puesto (ajustado al NavMesh) a la velocidad que le pide la escuadra.</summary>
        private void DriveSoldiers()
        {
            Vector3 facing = Facing;
            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierUnit soldier = _soldiers[i];
                NavMeshAgent agent = soldier.Agent;
                if (agent == null || !agent.isOnNavMesh) continue;
                Vector3 slot = ToVector3(Command.SlotOf(i));
                Vector3 position = soldier.transform.position;
                float distance = Vector3.ProjectOnPlane(slot - position, Vector3.up).magnitude;
                agent.speed = Mathf.Max(0.2f, Command.FollowSpeed(distance));
                if ((slot - _lastDestination[i]).sqrMagnitude > DestinationTolerance * DestinationTolerance)
                {
                    Vector3 snapped = NavMeshRuntimeBaker.Snap(slot, 4f);
                    agent.SetDestination(snapped);
                    _lastDestination[i] = slot;
                }
                // Andando miran adonde van; en su puesto, al frente de la escuadra.
                Vector3 velocity = Vector3.ProjectOnPlane(agent.velocity, Vector3.up);
                Vector3 look = velocity.sqrMagnitude > 0.09f ? velocity.normalized : facing;
                soldier.transform.rotation = Quaternion.RotateTowards(soldier.transform.rotation, Quaternion.LookRotation(look, Vector3.up), 240f * Time.deltaTime);
            }
        }

        /// <summary>Bajas: caen hombres al azar; los demás cierran filas. Devuelve cuántos cayeron.</summary>
        public int TakeCasualties(int count, Vector3 fireFrom)
        {
            int fallen = 0;
            for (int n = 0; n < count && _soldiers.Count > 0; n++)
            {
                int index = Random.Range(0, _soldiers.Count);
                _soldiers[index].Fall(fireFrom);
                _soldiers.RemoveAt(index);
                _lastDestination.RemoveAt(index);
                fallen++;
            }
            if (fallen == 0) return 0;
            SyncPositions();
            if (_soldiers.Count > 0)
            {
                Command.RemoveSoldier(_positions);
                Fire.SetSoldiers(_soldiers.Count);
            }
            else
            {
                Order = SquadOrderKind.Hold;
                enabled = false;
                s_all.Remove(this);
            }
            return fallen;
        }

        private void EmitShots(int shots, SquadController target)
        {
            // Humo y fogonazo de algunos tiradores (no de todos: el presupuesto de humo es de la escena entera).
            int visible = Mathf.Min(shots, 3);
            for (int i = 0; i < visible; i++)
            {
                SoldierUnit shooter = _soldiers[Random.Range(0, _soldiers.Count)];
                Vector3 forward = target != null
                    ? (target.CenterOfMass() - shooter.transform.position).normalized
                    : shooter.transform.forward;
                Vector3 muzzle = shooter.transform.position + Vector3.up * 1.4f + forward * 1.1f;
                BlackPowderSmoke.Emit(muzzle, forward, Weapon.PowderChargeG, Vector3.zero);
            }
        }

        public Vector3 CenterOfMass()
        {
            if (_soldiers.Count == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            foreach (SoldierUnit s in _soldiers) sum += s.transform.position;
            return sum / _soldiers.Count;
        }

        private void SyncPositions()
        {
            _positions.Clear();
            foreach (SoldierUnit s in _soldiers) _positions.Add(ToVec3(s.transform.position));
        }

        // ------------------------------------------------------------------------------------------
        // Soldados (primitivas: AGENTS.md §4.A)
        // ------------------------------------------------------------------------------------------

        private SoldierUnit CreateSoldier(int index, Vector3 position, Vector3 facing)
        {
            var go = new GameObject(displayName + "_" + (index + 1));
            // Inactivo mientras se configura: el NavMeshAgent no intenta colocarse con el tipo de agente por defecto
            // (para el que no hay NavMesh) antes de recibir el del campo de batalla.
            go.SetActive(false);
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
            go.layer = SoldierUnit.Layer;

            Part(go.transform, PrimitiveType.Capsule, "Cuerpo", new Vector3(0f, 0.85f, 0f), new Vector3(0.5f, 0.85f, 0.4f), uniformMaterial, keepCollider: true);
            Part(go.transform, PrimitiveType.Cylinder, "Quepis", new Vector3(0f, 1.78f, 0f), new Vector3(0.24f, 0.07f, 0.24f), trimMaterial, keepCollider: false);
            Part(go.transform, PrimitiveType.Cube, "Fusil", new Vector3(0.22f, 1.1f, 0.25f), new Vector3(0.04f, 0.04f, 1.2f), trimMaterial, keepCollider: false)
                .transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.agentTypeID = NavMeshRuntimeBaker.AgentTypeId;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = SquadMarch.MarchSpeedMps;
            agent.acceleration = 8f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.05f;
            agent.updateRotation = false; // la orientación la pone la escuadra
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 40 + index;

            var soldier = go.AddComponent<SoldierUnit>();
            soldier.Initialize(this);
            go.SetActive(true);
            if (agent.isOnNavMesh) agent.Warp(position);
            return soldier;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale,
                                       Material material, bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.layer = SoldierUnit.Layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            if (!keepCollider) Destroy(part.GetComponent<Collider>());
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
