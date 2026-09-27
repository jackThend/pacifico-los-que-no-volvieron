using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    /// <summary>Lo que el planificador necesita saber de cada escuadra seleccionada.</summary>
    public struct SquadFootprint
    {
        public Vec3 Center;
        public Vec3 Facing;
        public int Count;
        public FormationType Formation;

        public SquadFootprint(Vec3 center, Vec3 facing, int count, FormationType formation)
        {
            Center = center;
            Facing = facing;
            Count = count;
            Formation = formation;
        }

        public float Frontage => Tactics.Formation.Frontage(Formation, Count);
    }

    /// <summary>Destino de una escuadra: ancla (centro de su primera fila) y orientación final.</summary>
    public struct SquadDestination
    {
        public Vec3 Anchor;
        public Vec3 Facing;
    }

    /// <summary>
    /// Órdenes de movimiento para varias escuadras a la vez (ROADMAP 4.1), como en los tácticos en tiempo real:
    /// <list type="bullet">
    /// <item><b>Clic:</b> las escuadras forman codo con codo, centradas en el punto y mirando en la dirección en que
    /// avanzan.</item>
    /// <item><b>Arrastre:</b> el trazo marca el frente. Las escuadras se reparten a lo largo de él y miran a su
    /// perpendicular (de izquierda a derecha, hacia delante). Si el trazo es más corto que el frente de las escuadras, el
    /// frente se alarga por igual a ambos lados.</item>
    /// </list>
    /// Qué escuadra ocupa qué tramo se decide con <see cref="SlotAssignment"/>: no se cruzan al desplegarse.
    /// </summary>
    public static class OrderPlanner
    {
        /// <summary>Por debajo de esta longitud el arrastre cuenta como clic.</summary>
        public const float DragThresholdM = 4f;

        public static SquadDestination[] PlanMove(IReadOnlyList<SquadFootprint> squads, Vec3 from, Vec3 to)
        {
            if (squads == null) throw new ArgumentNullException(nameof(squads));
            int n = squads.Count;
            var result = new SquadDestination[n];
            if (n == 0) return result;

            Vec3 drag = new Vec3(to.X - from.X, 0f, to.Z - from.Z);
            float dragLength = drag.Magnitude;
            Vec3 center, facing;
            float available;
            if (dragLength >= DragThresholdM)
            {
                Vec3 along = drag / dragLength;
                // Trazo de izquierda a derecha → mirar hacia delante: frente = dirección girada 90° a la izquierda.
                facing = new Vec3(-along.Z, 0f, along.X);
                center = (from + to) * 0.5f;
                available = dragLength;
            }
            else
            {
                Vec3 centroid = Vec3.Zero;
                for (int i = 0; i < n; i++) centroid = centroid + squads[i].Center;
                centroid = centroid / n;
                Vec3 advance = new Vec3(to.X - centroid.X, 0f, to.Z - centroid.Z);
                // Si el punto está encima del grupo, se conserva la orientación de la primera escuadra.
                facing = advance.Magnitude > 0.5f ? advance.Normalized : Formation.Flatten(squads[0].Facing);
                center = to;
                available = 0f;
            }

            // Tramos del frente de izquierda a derecha: anchura de cada escuadra más los intervalos.
            float needed = 0f;
            for (int i = 0; i < n; i++)
            {
                needed += squads[i].Frontage;
                if (i > 0) needed += Math.Max(Formation.IntervalM(squads[i - 1].Formation), Formation.IntervalM(squads[i].Formation));
            }
            float total = Math.Max(needed, available);
            // Con un trazo largo, el espacio sobrante se reparte entre los intervalos.
            float extraGap = n > 1 ? (total - needed) / (n - 1) : 0f;

            Vec3 right = Formation.RightOf(facing);
            var widths = new float[n];
            for (int i = 0; i < n; i++) widths[i] = squads[i].Frontage;

            // Primero se reparten los tramos (en el orden actual de izquierda a derecha, para que la asignación
            // parta de una solución razonable) y luego se decide quién va a cada uno.
            var order = SortedLeftToRight(squads, right);
            float cursor = -total * 0.5f;
            var slotCenters = new Vec3[n];
            var slotWidth = new float[n];
            for (int k = 0; k < n; k++)
            {
                SquadFootprint s = squads[order[k]];
                float w = s.Frontage;
                if (k > 0)
                {
                    SquadFootprint prev = squads[order[k - 1]];
                    cursor += Math.Max(Formation.IntervalM(prev.Formation), Formation.IntervalM(s.Formation)) + extraGap;
                }
                slotCenters[k] = center + right * (cursor + w * 0.5f);
                slotWidth[k] = w;
                cursor += w;
            }

            // Solo se intercambian tramos entre escuadras de la misma anchura (si no, el frente quedaría mal repartido).
            var from3 = new Vec3[n];
            for (int i = 0; i < n; i++) from3[i] = squads[i].Center;
            int[] assigned = AssignSameWidth(from3, slotCenters, slotWidth, order, widths);
            for (int i = 0; i < n; i++)
            {
                result[i] = new SquadDestination { Anchor = slotCenters[assigned[i]], Facing = facing };
            }
            return result;
        }

        private static int[] SortedLeftToRight(IReadOnlyList<SquadFootprint> squads, Vec3 right)
        {
            var order = new int[squads.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => Vec3.Dot(squads[a].Center, right).CompareTo(Vec3.Dot(squads[b].Center, right)));
            return order;
        }

        /// <summary>
        /// Asignación de mínima distancia total con coste infinito entre escuadras y tramos de anchura distinta.
        /// El orden inicial (izquierda → derecha) es siempre una solución factible.
        /// </summary>
        private static int[] AssignSameWidth(Vec3[] centers, Vec3[] slots, float[] slotWidth, int[] order, float[] widths)
        {
            int n = centers.Length;
            var cost = new double[n, n];
            const double Forbidden = 1e9;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double dx = centers[i].X - slots[j].X, dz = centers[i].Z - slots[j].Z;
                    cost[i, j] = Math.Abs(widths[i] - slotWidth[j]) < 1e-3f ? Math.Sqrt(dx * dx + dz * dz) : Forbidden;
                }
            }
            return SlotAssignment.Hungarian(cost);
        }
    }
}
