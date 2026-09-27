using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Genera el NavMesh del campo de batalla al cargar la escena (ROADMAP 4.1), a partir de los colisionadores
    /// (terreno de dunas, parapetos, tapias). Usa la API del motor (<see cref="NavMeshBuilder"/>), la misma que emplea
    /// NavMeshSurface, así que no depende de ningún paquete ni de un NavMesh horneado en el Editor: si se cambia el
    /// terreno, el NavMesh sigue siendo correcto. Se ejecuta antes que las escuadras, que crean a sus soldados
    /// sobre él.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NavMeshRuntimeBaker : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(520f, 120f, 520f);
        [SerializeField] private LayerMask geometry = Physics.DefaultRaycastLayers;
        [Tooltip("Radio del soldado para el NavMesh: con 0,3 m caben codo con codo (1 m entre hombres).")]
        [SerializeField] private float agentRadius = 0.3f;
        [SerializeField] private float agentHeight = 1.8f;
        [SerializeField] private float maxSlopeDeg = 40f;
        [SerializeField] private float stepHeight = 0.45f;
        [SerializeField, Range(0.05f, 0.5f)] private float voxelSize = 0.2f;

        private NavMeshDataInstance _instance;
        private bool _hasSettings;
        private int _agentTypeId;

        /// <summary>Tipo de agente creado para este NavMesh (los NavMeshAgent de los soldados deben usarlo).</summary>
        public static int AgentTypeId { get; private set; }
        public static bool Ready { get; private set; }

        private void Awake()
        {
            NavMeshBuildSettings settings = NavMesh.CreateSettings();
            _hasSettings = true;
            _agentTypeId = settings.agentTypeID;
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentSlope = maxSlopeDeg;
            settings.agentClimb = stepHeight;
            // Vóxel de 20 cm (el valor por defecto sería radio/3 = 10 cm): un campo de 500 × 500 m se genera en
            // una cuarta parte del tiempo y sigue resolviendo zanjas de 1 m y huecos entre parapetos.
            settings.overrideVoxelSize = true;
            settings.voxelSize = voxelSize;

            var bounds = new Bounds(transform.position, size);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, geometry, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("[Pacífico] No se pudo generar el NavMesh del campo de batalla.");
                return;
            }
            _instance = NavMesh.AddNavMeshData(data);
            AgentTypeId = settings.agentTypeID;
            Ready = true;
        }

        private void OnDestroy()
        {
            if (_instance.valid) _instance.Remove();
            if (_hasSettings) NavMesh.RemoveSettings(_agentTypeId);
            if (AgentTypeId == _agentTypeId) Ready = false;
        }

        /// <summary>Punto del NavMesh más cercano (o el mismo punto si no hay ninguno a <paramref name="maxDistance"/>).</summary>
        public static Vector3 Snap(Vector3 point, float maxDistance = 6f)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };
            return NavMesh.SamplePosition(point, out NavMeshHit hit, maxDistance, filter) ? hit.position : point;
        }

        /// <summary>Camino por el NavMesh; si no lo hay, la recta.</summary>
        public static Vector3[] Path(Vector3 from, Vector3 to)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            if (Ready && NavMesh.CalculatePath(Snap(from), Snap(to), filter, path) && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 0)
            {
                return path.corners;
            }
            return new[] { from, to };
        }
    }
}
