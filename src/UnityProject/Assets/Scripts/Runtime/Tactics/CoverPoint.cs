using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Puesto a cubierto para un hombre (ROADMAP 4.2): un tramo de zanja, el pie de un parapeto o una roca. Protege
    /// hacia su eje Z (hacia el enemigo). Las escuadras bajo fuego, o paradas frente a una amenaza, los ocupan; cada
    /// puesto lo ocupa una sola escuadra a la vez.
    /// </summary>
    public sealed class CoverPoint : MonoBehaviour
    {
        [Tooltip("Fracción de la silueta que tapa frente al fuego de frente (zanja ≈ 0,8; parapeto de sacos ≈ 0,65; roca ≈ 0,5).")]
        [SerializeField, Range(0f, 1f)] private float protection = 0.65f;

        private static readonly List<CoverPoint> s_points = new List<CoverPoint>();
        private static readonly List<CoverSpot> s_spots = new List<CoverSpot>();
        private static readonly Dictionary<int, SquadController> s_claims = new Dictionary<int, SquadController>();
        private static bool s_dirty = true;

        public float Protection
        {
            get => protection;
            set => protection = value;
        }

        public static IReadOnlyList<CoverPoint> All => s_points;

        /// <summary>Los puestos como datos del núcleo, en el mismo orden que <see cref="All"/>.</summary>
        public static IReadOnlyList<CoverSpot> Spots
        {
            get
            {
                if (!s_dirty) return s_spots;
                s_spots.Clear();
                foreach (CoverPoint p in s_points)
                {
                    Vector3 pos = p.transform.position, f = p.transform.forward;
                    s_spots.Add(new CoverSpot(new Vec3(pos.x, pos.y, pos.z), new Vec3(f.x, 0f, f.z), p.protection));
                }
                s_dirty = false;
                return s_spots;
            }
        }

        private void OnEnable()
        {
            s_points.Add(this);
            s_dirty = true;
            s_claims.Clear(); // los índices cambian: las escuadras vuelven a pedir sus puestos
        }

        private void OnDisable()
        {
            s_points.Remove(this);
            s_dirty = true;
            s_claims.Clear();
        }

        /// <summary>Puestos ocupados por escuadras distintas de <paramref name="squad"/>.</summary>
        public static HashSet<int> TakenByOthers(SquadController squad, HashSet<int> into)
        {
            into.Clear();
            foreach (KeyValuePair<int, SquadController> claim in s_claims)
            {
                if (claim.Value != squad && claim.Value != null && claim.Value.IsAlive) into.Add(claim.Key);
            }
            return into;
        }

        public static void Claim(int index, SquadController squad) => s_claims[index] = squad;

        /// <summary>Libera todos los puestos de una escuadra.</summary>
        public static void Release(SquadController squad)
        {
            var mine = new List<int>();
            foreach (KeyValuePair<int, SquadController> claim in s_claims) if (claim.Value == squad) mine.Add(claim.Key);
            foreach (int i in mine) s_claims.Remove(i);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.25f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, transform.position + Vector3.up * 0.2f + transform.forward * 0.8f);
        }
    }
}
