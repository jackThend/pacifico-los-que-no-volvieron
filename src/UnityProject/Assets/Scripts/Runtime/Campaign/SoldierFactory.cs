using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Tactics;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>Uniformes de prototipo (greyboxing: colores planos, no reconstrucción de uniformes).</summary>
    [System.Serializable]
    public sealed class SoldierLook
    {
        public Material uniform;
        public Material trousers;
        public Material skin;
        public Material kepi;
    }

    /// <summary>
    /// Crea soldados de la IA en tiempo de ejecución con primitivas: cápsula del cuerpo y esfera de la cabeza (las
    /// formas exactas con las que se resuelven balas y bayonetazos), quepis y fusil. Se crean inactivos para que el
    /// <see cref="UnityEngine.AI.NavMeshAgent"/> use el tipo de agente del NavMesh generado al cargar la escena.
    /// </summary>
    public static class SoldierFactory
    {
        public static RiflemanAI Create(string name, Faction faction, Vector3 position, float heading, WeaponDataSO weapon, int cartridges,
                                        int seed, SoldierLook look, WeaponSpec bayonet)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetPositionAndRotation(NavMeshRuntimeBaker.Snap(position), Quaternion.Euler(0f, heading, 0f));

            var figure = new GameObject("Figura").transform;
            figure.SetParent(go.transform, false);
            GameObject legs = Part(PrimitiveType.Cube, "Piernas", figure, new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.9f, 0.22f), look?.trousers, false);
            GameObject body = Part(PrimitiveType.Capsule, "Cuerpo", figure, new Vector3(0f, 0.9f, 0f), new Vector3(0.46f, 0.85f, 0.46f), look?.uniform, true);
            GameObject head = Part(PrimitiveType.Sphere, "Cabeza", figure, new Vector3(0f, 1.6f, 0f), Vector3.one * 0.24f, look?.skin, true);
            Part(PrimitiveType.Cylinder, "Quepis", figure, new Vector3(0f, 1.74f, 0f), new Vector3(0.2f, 0.06f, 0.2f), look?.kepi, false);
            GameObject rifle = Part(PrimitiveType.Cube, "Fusil", figure, new Vector3(0.22f, 1.1f, 0.25f), new Vector3(0.05f, 0.05f, 1.3f), look?.trousers, false);
            rifle.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            legs.name = "Piernas";
            body.name = "Cuerpo";

            var combatant = go.AddComponent<Combatant>();
            combatant.Configure(faction, false, head.GetComponent<Collider>(), name);
            var ai = go.AddComponent<RiflemanAI>();
            ai.Configure(weapon, cartridges, seed, figure, bayonet);
            go.SetActive(true);
            return ai;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
