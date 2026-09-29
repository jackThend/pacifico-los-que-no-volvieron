using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using UnityEngine;
using UnityEngine.AI;

namespace Pacifico.Tactics
{
    /// <summary>Depósito de retaguardia de un bando: donde los carros cargan agua y cartuchos.</summary>
    public sealed class SupplyDepot : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Bolivia;
        [SerializeField] private float radius = 15f;

        private static readonly List<SupplyDepot> s_all = new List<SupplyDepot>();

        public Faction Faction
        {
            get => faction;
            set => faction = value;
        }

        public float Radius => radius;

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        /// <summary>Depósito más cercano del mismo bando (Perú y Bolivia comparten intendencia).</summary>
        public static SupplyDepot NearestFor(Faction side, Vector3 position)
        {
            SupplyDepot best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (SupplyDepot d in s_all)
            {
                if ((d.faction == Faction.Chile) != (side == Faction.Chile)) continue;
                float distance = (d.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = d;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Carro de vituallas (ROADMAP 4.3): sale del depósito con dos pipas de agua y seis cajones de cartuchos, va a la
    /// escuadra propia más necesitada (<see cref="SupplyDispatcher"/>), reparte a todas las que tenga a menos de 20 m
    /// y vuelve a cargar cuando se vacía. Cuanto más lejos está el frente de la retaguardia, más tarda en volver:
    /// mantener los carros conectados es la mecánica del GDD (§3.3).
    /// </summary>
    public sealed class SupplyCart : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Bolivia;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material barrelMaterial;
        [SerializeField] private float speed = 1.8f;

        private const float ThinkSeconds = 2f;

        private static readonly List<SupplyCart> s_all = new List<SupplyCart>();

        private NavMeshAgent _agent;
        private readonly List<SupplyRequest> _requests = new List<SupplyRequest>();
        private readonly List<SquadController> _squads = new List<SquadController>();
        private float _thinkTimer;
        private bool _built;

        public SupplyCartModel Model { get; } = new SupplyCartModel();
        public static IReadOnlyList<SupplyCart> All => s_all;
        public Faction Faction => faction;

        /// <summary>«Cargando», «Repartiendo a …», «Hacia …» o «En espera» (para el HUD).</summary>
        public string Status { get; private set; } = "En espera";

        public void Configure(Faction side, Material wood, Material barrels)
        {
            faction = side;
            woodMaterial = wood;
            barrelMaterial = barrels;
        }

        private void OnEnable() => s_all.Add(this);

        private void OnDisable() => s_all.Remove(this);

        private void Start()
        {
            // El agente se crea en tiempo de ejecución, con el objeto inactivo mientras se configura: así no intenta
            // colocarse con el tipo de agente por defecto, para el que no hay NavMesh (igual que los soldados).
            gameObject.SetActive(false);
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.agentTypeID = NavMeshRuntimeBaker.AgentTypeId;
            _agent.speed = speed;
            _agent.radius = 0.9f;
            _agent.height = 2f;
            _agent.acceleration = 3f;
            _agent.angularSpeed = 90f;
            _agent.stoppingDistance = 6f;
            transform.position = NavMeshRuntimeBaker.Snap(transform.position);
            gameObject.SetActive(true);
            BuildVisual();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _agent == null) return;

            // Reparte o carga en cada fotograma; decide adónde ir cada pocos segundos.
            SupplyDepot depot = SupplyDepot.NearestFor(faction, transform.position);
            bool atDepot = depot != null && Vector3.Distance(depot.transform.position, transform.position) <= depot.Radius;
            if (atDepot && (Model.WaterFraction < 1f || Model.CartridgeFraction < 1f))
            {
                Model.Reload(dt);
                Status = "Cargando en el depósito";
            }
            GiveToSquadsInReach(dt);

            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = ThinkSeconds;
            Think(depot, atDepot);
        }

        private void GiveToSquadsInReach(float dt)
        {
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.IsAlive || squad.Supply == null || !SameSide(squad)) continue;
                if (Vector3.Distance(squad.CenterOfMass(), transform.position) > SupplyCartModel.ReachM) continue;
                if (squad.Supply.WaterNeededLiters < 0.01f && squad.Supply.CartridgesNeeded == 0) continue;
                Model.Resupply(squad.Supply, dt);
                Status = "Repartiendo a " + squad.DisplayName;
            }
        }

        private void Think(SupplyDepot depot, bool atDepot)
        {
            _requests.Clear();
            _squads.Clear();
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.IsAlive || squad.Supply == null || !SameSide(squad)) continue;
                SquadSupply s = squad.Supply;
                Vector3 p = squad.CenterOfMass();
                _requests.Add(new SupplyRequest(_squads.Count, new Vec3(p.x, p.y, p.z), s.WaterFraction, s.AmmoFraction, s.DehydrationPercent));
                _squads.Add(squad);
            }

            // En el depósito, no se sale hasta haber cargado (salvo que no quede nada por cargar).
            bool loading = atDepot && (Model.WaterFraction < 0.95f || Model.CartridgeFraction < 0.95f);
            int choice = loading ? SupplyDispatcher.GoToDepot : SupplyDispatcher.Choose(Model, new Vec3(transform.position.x, 0f, transform.position.z), _requests);
            if (choice == SupplyDispatcher.GoToDepot && depot != null)
            {
                if (!atDepot) Status = "Vuelve al depósito";
                _agent.SetDestination(NavMeshRuntimeBaker.Snap(depot.transform.position));
            }
            else if (choice >= 0)
            {
                SquadController squad = _squads[choice];
                // Se detiene a retaguardia de la escuadra, del lado contrario al enemigo si lo hay.
                Vector3 target = squad.CenterOfMass() - squad.Facing * 8f;
                if (Vector3.Distance(target, transform.position) > SupplyCartModel.ReachM * 0.5f) Status = "Hacia " + squad.DisplayName;
                _agent.SetDestination(NavMeshRuntimeBaker.Snap(target));
            }
            else if (!atDepot)
            {
                Status = "En espera";
            }
        }

        private bool SameSide(SquadController squad) => (squad.Faction == Faction.Chile) == (faction == Faction.Chile);

        /// <summary>Carreta de dos ejes con dos pipas y los cajones (primitivas, AGENTS.md §4.A).</summary>
        private void BuildVisual()
        {
            if (_built) return;
            _built = true;
            Part(PrimitiveType.Cube, "Caja", new Vector3(0f, 0.9f, 0f), new Vector3(1.4f, 0.5f, 3f), woodMaterial);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.8f : 0.8f, z = i < 2 ? -1f : 1f;
                GameObject wheel = Part(PrimitiveType.Cylinder, "Rueda", new Vector3(x, 0.55f, z), new Vector3(1.1f, 0.06f, 1.1f), woodMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            for (int i = 0; i < 2; i++)
            {
                GameObject pipa = Part(PrimitiveType.Cylinder, "Pipa", new Vector3(0f, 1.55f, i == 0 ? -0.7f : 0.3f), new Vector3(0.8f, 0.45f, 0.8f), barrelMaterial);
                pipa.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Part(PrimitiveType.Cube, "Cajones", new Vector3(0f, 1.3f, 1.1f), new Vector3(1.1f, 0.35f, 0.6f), barrelMaterial);
            Part(PrimitiveType.Cube, "Varas", new Vector3(0f, 0.8f, 2.2f), new Vector3(0.9f, 0.08f, 1.6f), woodMaterial);
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.layer = SoldierUnit.Layer; // no tapa la línea de visión ni el NavMesh
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
