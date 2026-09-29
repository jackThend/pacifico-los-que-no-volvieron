using Pacifico.Core.Melee;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>Un golpe cuerpo a cuerpo que alcanza a un objetivo.</summary>
    public struct MeleeStrike
    {
        public Vector3 Point;
        /// <summary>Dirección del movimiento de la hoja en el contacto.</summary>
        public Vector3 Direction;
        public float Damage;
        /// <summary>Cuánto se hundió la hoja (m).</summary>
        public float Depth;
        public MeleeAttackKind Kind;
        /// <summary>«Estocada» o «Tajo».</summary>
        public string AttackName;
        /// <summary>Parte alcanzada (colisionador del torso, de la cabeza…).</summary>
        public Collider Part;
        public GameObject Attacker;
    }

    /// <summary>
    /// Todo lo que puede recibir un golpe de bayoneta o de corvo. Sus colisionadores de tipo cápsula, esfera o caja
    /// son la forma exacta con la que se calcula el contacto.
    /// </summary>
    public interface IMeleeTarget
    {
        void ReceiveMelee(MeleeStrike strike);

        /// <summary>Multiplicador de daño de una parte (cabeza ×1,5, etc.).</summary>
        float DamageMultiplier(Collider part);

        /// <summary>Color de las partículas del impacto (paja, astillas…).</summary>
        Color ImpactColor { get; }
    }
}
