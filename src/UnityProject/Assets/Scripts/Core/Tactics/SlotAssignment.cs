using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    /// <summary>
    /// Asignación de puestos: qué soldado va a qué puesto de la formación (o qué escuadra a qué tramo del frente).
    /// Minimiza la suma de distancias en línea recta con el método húngaro (Kuhn–Munkres, O(n³)). Con esa suma
    /// mínima dos trayectorias rectas nunca se cruzan: si se cruzaran, intercambiar los destinos acortaría el total
    /// (desigualdad triangular). Así los soldados no se atraviesan al cambiar de formación.
    /// </summary>
    public static class SlotAssignment
    {
        /// <summary>
        /// Asigna cada origen a un destino distinto. Devuelve, para cada origen i, el índice de su destino.
        /// Requiere tantos destinos como orígenes o más.
        /// </summary>
        public static int[] Solve(IReadOnlyList<Vec3> from, IReadOnlyList<Vec3> to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (to.Count < from.Count) throw new ArgumentException("Hay menos puestos que soldados.", nameof(to));
            var cost = new double[from.Count, to.Count];
            for (int i = 0; i < from.Count; i++)
            {
                for (int j = 0; j < to.Count; j++)
                {
                    cost[i, j] = HorizontalDistance(from[i], to[j]);
                }
            }
            return Hungarian(cost);
        }

        public static float TotalDistance(IReadOnlyList<Vec3> from, IReadOnlyList<Vec3> to, int[] assignment)
        {
            double total = 0.0;
            for (int i = 0; i < from.Count; i++) total += HorizontalDistance(from[i], to[assignment[i]]);
            return (float)total;
        }

        private static double HorizontalDistance(Vec3 a, Vec3 b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Método húngaro con potenciales para una matriz n×m (n ≤ m). Devuelve la columna asignada a cada fila.
        /// </summary>
        public static int[] Hungarian(double[,] cost)
        {
            int n = cost.GetLength(0);
            int m = cost.GetLength(1);
            if (n > m) throw new ArgumentException("La matriz necesita al menos tantas columnas como filas.", nameof(cost));
            var u = new double[n + 1];
            var v = new double[m + 1];
            var p = new int[m + 1];   // p[j]: fila asignada a la columna j (1-based; 0 = libre)
            var way = new int[m + 1];

            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                var minv = new double[m + 1];
                var used = new bool[m + 1];
                for (int j = 0; j <= m; j++) minv[j] = double.PositiveInfinity;
                do
                {
                    used[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    double delta = double.PositiveInfinity;
                    for (int j = 1; j <= m; j++)
                    {
                        if (used[j]) continue;
                        double cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                    for (int j = 0; j <= m; j++)
                    {
                        if (used[j])
                        {
                            u[p[j]] += delta;
                            v[j] -= delta;
                        }
                        else
                        {
                            minv[j] -= delta;
                        }
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }

            var result = new int[n];
            for (int j = 1; j <= m; j++)
            {
                if (p[j] != 0) result[p[j] - 1] = j - 1;
            }
            return result;
        }
    }
}
