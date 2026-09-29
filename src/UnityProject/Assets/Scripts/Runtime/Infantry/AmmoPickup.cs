using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Cartuchos recogibles (GDD cap. 4, Tarapacá: «escasez crítica de cartuchos: necesidad de recoger munición del
    /// suelo»). Solo los aprovecha un fusil del mismo cartucho: los paquetes de Comblain no sirven para un Chassepot.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AmmoPickup : MonoBehaviour
    {
        [Tooltip("Cartucho exacto de la ficha (p. ej. «11×50R Comblain»). Vacío = sirve a cualquier fusil.")]
        [SerializeField] private string cartridge = string.Empty;
        [SerializeField] private int rounds = 20;

        public void Configure(string cartridgeName, int amount)
        {
            cartridge = cartridgeName;
            rounds = amount;
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var rifle = other.GetComponentInParent<RifleController>();
            if (rifle == null) return;
            if (rifle.AddAmmo(cartridge, rounds) > 0) Destroy(gameObject);
        }
    }
}
