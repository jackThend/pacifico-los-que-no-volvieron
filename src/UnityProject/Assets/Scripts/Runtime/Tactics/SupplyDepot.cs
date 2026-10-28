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
}
