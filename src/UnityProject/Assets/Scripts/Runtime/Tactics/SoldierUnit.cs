using UnityEngine;
using UnityEngine.AI;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Un soldado de una escuadra RTS: su <see cref="NavMeshAgent"/> lo lleva al puesto que le indica la escuadra.
    /// Está en la capa «Ignore Raycast» para no tapar la línea de visión de los demás; el ratón lo encuentra con una
    /// máscara que incluye esa capa.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SoldierUnit : MonoBehaviour
    {
        public const int Layer = 2; // «Ignore Raycast»

        public SquadController Squad { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public bool Alive { get; private set; } = true;

        /// <summary>Altura de los ojos sobre los pies (de pie).</summary>
        public const float EyeHeightM = 1.55f;

        public void Initialize(SquadController squad)
        {
            Squad = squad;
            Agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>Cae: se aparta del NavMesh, se tumba y se retira al cabo de un rato.</summary>
        public void Fall(Vector3 awayFrom)
        {
            if (!Alive) return;
            Alive = false;
            if (Agent != null) Agent.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - awayFrom, Vector3.up);
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            // Cae hacia atrás, alejándose del fuego (el pivote está en los pies: +90° en X lleva la cabeza hacia +Z).
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            Destroy(gameObject, 30f);
        }
    }
}
