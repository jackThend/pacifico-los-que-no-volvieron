using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Melee;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Input;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Combate cuerpo a cuerpo del infante (ROADMAP 3.4): <c>F</c> estocada con la bayoneta calada (o con el corvo si
    /// el arma no admite bayoneta) y <c>G</c> tajo con el corvo. Al acercarse un objetivo, el arma se pone en guardia.
    /// <para>
    /// La colisión la resuelve <see cref="MeleeAttackModel"/> en pasos fijos de 1/480 s contra la forma exacta de los
    /// colisionadores de los objetivos (cápsula, esfera o caja), con la cámara interpolada entre fotogramas. Solo
    /// cuentan los objetivos a la vista: una tapia entre el soldado y el muñeco para la hoja.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController))]
    // Antes que RifleController (−50) y FirstPersonController (0): MeleeBusy ya vale lo que debe en este fotograma.
    [DefaultExecutionOrder(-60)]
    public sealed class MeleeController : MonoBehaviour
    {
        [Tooltip("Arma blanca que lleva el soldado además del fusil (corvo chileno).")]
        [SerializeField] private WeaponDataSO sidearm;
        [Tooltip("Capas de los objetivos y de lo que bloquea la hoja.")]
        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;
        [Tooltip("Sacudida de la vista al clavar la bayoneta (°).")]
        [SerializeField] private float impactPunchDeg = 1.6f;

        private const int MaxColliders = 32;

        private FirstPersonController _fps;
        private readonly Collider[] _overlap = new Collider[MaxColliders];
        private readonly RaycastHit[] _sightHits = new RaycastHit[16];
        private readonly List<MeleeTargetShape> _shapes = new List<MeleeTargetShape>(MaxColliders);
        /// <summary>Colisionador por PartId (su GetInstanceID) y objetivo por TargetId, válidos durante el fotograma.</summary>
        private readonly Dictionary<int, Collider> _parts = new Dictionary<int, Collider>();
        private readonly Dictionary<int, IMeleeTarget> _targets = new Dictionary<int, IMeleeTarget>();

        private WeaponSpec _sidearmSpec;
        private MeleeAttackProfile _thrust;
        private MeleeAttackProfile _slash;
        private WeaponSpec _profilesFor;
        private bool _profilesBuilt;
        private ViewFrame _previousFrame;
        private bool _hasPreviousFrame;
        private float _guardVelocity;

        public MeleeAttackModel Model { get; } = new MeleeAttackModel();

        /// <summary>Peso de la guardia suavizado (0: normal; 1: en guardia ante un objetivo al alcance).</summary>
        public float Guard { get; private set; }

        /// <summary>El golpe en curso lo da el arma blanca (el fusil se aparta) y no la bayoneta.</summary>
        public bool UsingSidearm => Model.IsBusy && Model.Profile != null && !Model.Profile.Weapon.IsFirearm;

        public MeleeAttackProfile ThrustProfile => _thrust;
        public MeleeAttackProfile SlashProfile => _slash;

        /// <summary>Alcance del golpe principal (estocada) con el equipo actual.</summary>
        public float Reach => _thrust != null ? _thrust.ReachM : 0f;

        public WeaponDataSO Sidearm
        {
            get => sidearm;
            set
            {
                sidearm = value;
                _sidearmSpec = null;
                _profilesBuilt = false;
            }
        }

        private void Awake()
        {
            _fps = GetComponent<FirstPersonController>();
        }

        private void OnDisable()
        {
            _fps.MeleeBusy = false;
            _hasPreviousFrame = false;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            RefreshProfiles();

            if (!Model.IsBusy && _fps.HasInputFocus && _fps.Motor.Stance != Stance.Sliding)
            {
                if (GameInput.Pressed(GameKey.F) && _thrust != null) Model.Start(_thrust);
                else if (GameInput.Pressed(GameKey.G) && _slash != null) Model.Start(_slash);
            }
            _fps.MeleeBusy = Model.IsBusy;
        }

        /// <summary>
        /// En LateUpdate: la cámara ya está colocada, así que el golpe recorre el movimiento real de la vista en
        /// este fotograma (del marco anterior al actual).
        /// </summary>
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Camera camera = _fps.ViewCamera;
            if (dt <= 0f || camera == null) return;

            Transform eye = camera.transform;
            ViewFrame frame = new ViewFrame(ToVec3(eye.position), ToVec3(eye.right), ToVec3(eye.up), ToVec3(eye.forward));
            ViewFrame from = _hasPreviousFrame ? _previousFrame : frame;
            _previousFrame = frame;
            _hasPreviousFrame = true;

            GatherTargets(eye.position, Mathf.Max(Reach, _slash != null ? _slash.ReachM : 0f) + 2f);

            float surface = MeleeProximity.NearestInFront(frame, _shapes, 35f, out _);
            float guardReach = _thrust != null ? _thrust.ReachM : _slash != null ? _slash.ReachM : 0f;
            float guardTarget = Model.IsBusy ? 1f : guardReach > 0f ? MeleeProximity.GuardWeight(surface, guardReach) : 0f;
            Guard = MathUtil.SmoothDamp(Guard, guardTarget, ref _guardVelocity, 0.12f, dt);

            if (!Model.IsBusy) return;
            IReadOnlyList<MeleeHit> hits = Model.Step(dt, from, frame, _shapes);
            for (int i = 0; i < hits.Count; i++) Deliver(hits[i]);
            _fps.MeleeBusy = Model.IsBusy;
        }

        private void RefreshProfiles()
        {
            WeaponSpec rifle = _fps.Weapon;
            if (_sidearmSpec == null && sidearm != null) _sidearmSpec = sidearm.ToSpec();
            if (_profilesBuilt && _profilesFor == rifle) return;
            if (Model.IsBusy) return; // no se cambia la trayectoria a mitad de golpe
            _profilesFor = rifle;
            _profilesBuilt = true;

            bool blade = _sidearmSpec != null && _sidearmSpec.MeleeReachM > 0f;
            if (rifle != null && rifle.MeleeReachM > 0f)
                _thrust = rifle.IsFirearm ? MeleeAttackProfile.BayonetThrust(rifle) : MeleeAttackProfile.BladeThrust(rifle);
            else
                _thrust = blade ? MeleeAttackProfile.BladeThrust(_sidearmSpec) : null;

            if (rifle != null && !rifle.IsFirearm && rifle.MeleeReachM > 0f) _slash = MeleeAttackProfile.BladeSlash(rifle);
            else _slash = blade ? MeleeAttackProfile.BladeSlash(_sidearmSpec) : null;
        }

        // ------------------------------------------------------------------------------------------
        // Objetivos
        // ------------------------------------------------------------------------------------------

        private void GatherTargets(Vector3 eye, float radius)
        {
            _shapes.Clear();
            _parts.Clear();
            _targets.Clear();
            int count = Physics.OverlapSphereNonAlloc(eye, radius, _overlap, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlap[i];
                if (c.transform.IsChildOf(transform)) continue;
                IMeleeTarget target = FindTarget(c, out MonoBehaviour owner);
                if (target == null) continue;
                if (!TryGetCapsule(c, out Capsule capsule)) continue;
                if (!InSight(eye, c, owner)) continue;

                int targetId = owner.GetInstanceID();
                int partId = c.GetInstanceID();
                _targets[targetId] = target;
                _parts[partId] = c;
                _shapes.Add(new MeleeTargetShape(targetId, partId, capsule, target.DamageMultiplier(c)));
            }
        }

        /// <summary>¿Hay línea libre del ojo a la parte? Lo primero que se cruce (sin contar al propio soldado) debe ser el objetivo.</summary>
        private bool InSight(Vector3 eye, Collider part, MonoBehaviour owner)
        {
            Vector3 aim = part.bounds.center - eye;
            float distance = aim.magnitude;
            if (distance < 1e-3f) return true;
            int count = Physics.RaycastNonAlloc(eye, aim / distance, _sightHits, distance, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider first = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _sightHits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    first = h.collider;
                }
            }
            return first == null || first.transform.IsChildOf(owner.transform);
        }

        private void Deliver(MeleeHit hit)
        {
            if (!_targets.TryGetValue(hit.TargetId, out IMeleeTarget target)) return;
            _parts.TryGetValue(hit.PartId, out Collider part);
            var point = new Vector3(hit.Point.X, hit.Point.Y, hit.Point.Z);
            var direction = new Vector3(hit.Direction.X, hit.Direction.Y, hit.Direction.Z);
            target.ReceiveMelee(new MeleeStrike
            {
                Point = point,
                Direction = direction,
                Damage = hit.Damage,
                Depth = hit.Depth,
                Kind = hit.Kind,
                AttackName = Model.Profile.Name,
                Part = part,
                Attacker = gameObject,
            });
            MeleeImpactEffect.Play(point, direction, target.ImpactColor, hit.Kind == MeleeAttackKind.Slash ? 26 : 18);

            // Sacudida de la vista: la estocada frena en seco (hacia abajo); el tajo arrastra la vista en su sentido.
            Transform eye = _fps.ViewCamera.transform;
            float lateral = Vector3.Dot(direction, eye.right);
            if (hit.Kind == MeleeAttackKind.Thrust) _fps.Recoil.Punch(-impactPunchDeg, 0f);
            else _fps.Recoil.Punch(-0.3f * impactPunchDeg, impactPunchDeg * Mathf.Sign(lateral));
        }

        /// <summary>Se recorre la jerarquía en lugar de GetComponentInParent&lt;Interfaz&gt;() para evitar el «null falso» de Unity.</summary>
        private static IMeleeTarget FindTarget(Collider collider, out MonoBehaviour owner)
        {
            foreach (MonoBehaviour behaviour in collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IMeleeTarget target)
                {
                    owner = behaviour;
                    return target;
                }
            }
            owner = null;
            return null;
        }

        /// <summary>
        /// Forma del colisionador en el mundo como cápsula: exacta para cápsulas y esferas (con la escala del objeto);
        /// una caja se aproxima por la cápsula inscrita en su eje mayor. Las mallas no se admiten.
        /// </summary>
        public static bool TryGetCapsule(Collider collider, out Capsule capsule)
        {
            Transform t = collider.transform;
            Vector3 scale = t.lossyScale;
            switch (collider)
            {
                case SphereCollider sphere:
                {
                    float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    capsule = Capsule.Sphere(ToVec3(t.TransformPoint(sphere.center)), r);
                    return true;
                }
                case CapsuleCollider cap:
                {
                    Vector3 axis = cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward;
                    float axisScale = Mathf.Abs(cap.direction == 0 ? scale.x : cap.direction == 1 ? scale.y : scale.z);
                    float radiusScale = cap.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                        : cap.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
                        : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    float r = cap.radius * radiusScale;
                    float half = Mathf.Max(0f, cap.height * axisScale * 0.5f - r);
                    Vector3 center = t.TransformPoint(cap.center);
                    Vector3 worldAxis = t.TransformDirection(axis).normalized;
                    capsule = new Capsule(ToVec3(center - worldAxis * half), ToVec3(center + worldAxis * half), r);
                    return true;
                }
                case BoxCollider box:
                {
                    Vector3 size = Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    int major = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                    Vector3 axis = major == 0 ? t.right : major == 1 ? t.up : t.forward;
                    float length = major == 0 ? size.x : major == 1 ? size.y : size.z;
                    float r = 0.5f * (major == 0 ? Mathf.Min(size.y, size.z) : major == 1 ? Mathf.Min(size.x, size.z) : Mathf.Min(size.x, size.y));
                    float half = Mathf.Max(0f, length * 0.5f - r);
                    Vector3 center = t.TransformPoint(box.center);
                    capsule = new Capsule(ToVec3(center - axis * half), ToVec3(center + axis * half), r);
                    return true;
                }
                default:
                    capsule = default;
                    return false;
            }
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);
    }
}
