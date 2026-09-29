using Pacifico.Core.Naval;
using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Marca un colisionador del casco con su zona de blindaje (cinturón, torre, torre de mando...).
    /// Los colisionadores sin marcador se tratan como obra muerta sin coraza.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ArmorZoneMarker : MonoBehaviour
    {
        [SerializeField] private ArmorZone zone = ArmorZone.BeltMidships;
        [Tooltip("Impactos en esta zona bajo la flotación abren vías de agua.")]
        [SerializeField] private bool belowWaterline;

        public ArmorZone Zone
        {
            get => zone;
            set => zone = value;
        }

        public bool BelowWaterline
        {
            get => belowWaterline;
            set => belowWaterline = value;
        }
    }
}
