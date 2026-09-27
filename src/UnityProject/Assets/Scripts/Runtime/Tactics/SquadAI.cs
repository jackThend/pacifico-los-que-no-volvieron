using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Mando automático de una escuadra enemiga (prototipo de ROADMAP 4.2): cada pocos segundos elige la escuadra
    /// contraria más cercana y la ataca. El avance se frena solo bajo fuego (la supresión la detiene y la tiende) y
    /// se reanuda cuando afloja: así se ve la mecánica del GDD, en que el fuego concentrado inmoviliza al asaltante.
    /// </summary>
    [RequireComponent(typeof(SquadController))]
    public sealed class SquadAI : MonoBehaviour
    {
        [Tooltip("Segundos antes de romper el avance (para dar tiempo al jugador a desplegar).")]
        [SerializeField] private float startDelaySeconds = 20f;
        [SerializeField] private float thinkSeconds = 3f;

        private SquadController _squad;
        private float _timer;

        public float StartDelaySeconds
        {
            get => startDelaySeconds;
            set => startDelaySeconds = value;
        }

        private void Awake()
        {
            _squad = GetComponent<SquadController>();
            _timer = startDelaySeconds;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f || _squad.Command == null || !_squad.IsAlive) return;
            _timer = thinkSeconds;

            if (_squad.Order == SquadOrderKind.Attack && _squad.Target != null && _squad.Target.IsAlive) return;
            SquadController nearest = null;
            float best = float.PositiveInfinity;
            Vector3 here = _squad.CenterOfMass();
            foreach (SquadController other in SquadController.All)
            {
                if (!other.IsAlive || !_squad.IsEnemyOf(other)) continue;
                float d = (other.CenterOfMass() - here).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = other;
                }
            }
            if (nearest != null) _squad.IssueAttack(nearest);
        }
    }
}
