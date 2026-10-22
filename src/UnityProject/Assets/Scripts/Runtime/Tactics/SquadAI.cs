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

        /// <summary>
        /// Espera antes de romper el avance. Asignarla reinicia la cuenta: AddComponent ya ha ejecutado Awake, y sin esto
        /// las escuadras creadas por código (la guardia de los cañones del capítulo 5) arrancaban a los 20 s por defecto.
        /// </summary>
        public float StartDelaySeconds
        {
            get => startDelaySeconds;
            set
            {
                startDelaySeconds = value;
                _timer = value;
            }
        }

        /// <summary>Si es mayor que cero, carga a la bayoneta (o al sable) al enemigo que tenga a esta distancia (m).</summary>
        public float ChargeWithinM { get; set; }

        private void Awake()
        {
            _squad = GetComponent<SquadController>();
            _timer = startDelaySeconds;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f || _squad.Command == null || !_squad.IsAlive || _squad.Routed) return;
            _timer = thinkSeconds;

            if (_squad.Order == SquadOrderKind.Charge) return;
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
            if (nearest == null) return;
            if (ChargeWithinM > 0f && best <= ChargeWithinM * ChargeWithinM) _squad.IssueCharge(nearest);
            else if (_squad.Order != SquadOrderKind.Attack || _squad.Target == null || !_squad.Target.IsAlive) _squad.IssueAttack(nearest);
        }
    }
}
