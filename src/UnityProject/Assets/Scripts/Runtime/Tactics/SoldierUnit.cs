using Pacifico.Core.Tactics;
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

        private Transform _figure;
        private float _targetKneel;
        private float _targetProne;
        private float _kneel;
        private float _prone;

        public Posture Posture { get; private set; } = Posture.Standing;

        /// <summary><paramref name="figure"/>: nodo con las piezas visibles (pivote en los pies).</summary>
        public void Initialize(SquadController squad, Transform figure)
        {
            Squad = squad;
            Agent = GetComponent<NavMeshAgent>();
            _figure = figure;
        }

        /// <summary>De pie, rodilla en tierra o cuerpo a tierra (ROADMAP 4.2); la figura pasa de una a otra en ~0,3 s.</summary>
        public void SetPosture(Posture posture)
        {
            Posture = posture;
            _targetKneel = posture == Posture.Kneeling ? 1f : 0f;
            _targetProne = posture == Posture.Prone ? 1f : 0f;
        }

        private void Update()
        {
            if (!Alive || _figure == null) return;
            float step = Time.deltaTime / 0.3f;
            _kneel = Mathf.MoveTowards(_kneel, _targetKneel, step);
            _prone = Mathf.MoveTowards(_prone, _targetProne, step);
            // Rodilla en tierra: la figura se acorta un 35 %. Cuerpo a tierra: se tumba hacia delante sobre los pies.
            float height = Mathf.Lerp(1f, 0.65f, _kneel);
            _figure.localScale = new Vector3(1f, height, 1f);
            _figure.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 88f, _prone), 0f, 0f);
            _figure.localPosition = new Vector3(0f, Mathf.Lerp(0f, 0.18f, _prone), Mathf.Lerp(0f, -0.9f, _prone));
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
