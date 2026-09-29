using System;
using System.Collections.Generic;

namespace Pacifico.Core.Tactics
{
    public enum SelectionMode
    {
        /// <summary>La selección nueva sustituye a la anterior.</summary>
        Replace = 0,
        /// <summary>Mayús: se añade a la selección.</summary>
        Add = 1,
        /// <summary>Ctrl: se alterna (lo seleccionado se quita, lo demás se añade).</summary>
        Toggle = 2,
    }

    /// <summary>Rectángulo de selección en píxeles de pantalla, normalizado sea cual sea el sentido del arrastre.</summary>
    public struct ScreenRect
    {
        public float XMin;
        public float YMin;
        public float XMax;
        public float YMax;

        public static ScreenRect FromCorners(float x0, float y0, float x1, float y1)
        {
            return new ScreenRect
            {
                XMin = Math.Min(x0, x1),
                YMin = Math.Min(y0, y1),
                XMax = Math.Max(x0, x1),
                YMax = Math.Max(y0, y1),
            };
        }

        public float Width => XMax - XMin;
        public float Height => YMax - YMin;

        public bool Contains(float x, float y) => x >= XMin && x <= XMax && y >= YMin && y <= YMax;
    }

    /// <summary>Un soldado proyectado en pantalla: a qué escuadra pertenece y si está delante de la cámara.</summary>
    public struct ScreenPoint
    {
        public int SquadId;
        public float X;
        public float Y;
        public bool InFront;

        public ScreenPoint(int squadId, float x, float y, bool inFront = true)
        {
            SquadId = squadId;
            X = x;
            Y = y;
            InFront = inFront;
        }
    }

    /// <summary>
    /// Selección de escuadras con el ratón (ROADMAP 4.1). La unidad de mando es la escuadra: basta con que uno de sus
    /// hombres quede dentro del recuadro para seleccionarla entera. Un arrastre de menos de
    /// <see cref="ClickThresholdPx"/> píxeles es un clic, que selecciona la escuadra del hombre más cercano al cursor
    /// (dentro de <see cref="ClickRadiusPx"/>).
    /// </summary>
    public static class SelectionLogic
    {
        public const float ClickThresholdPx = 6f;
        public const float ClickRadiusPx = 24f;

        public static bool IsClick(ScreenRect rect) => rect.Width < ClickThresholdPx && rect.Height < ClickThresholdPx;

        /// <summary>Escuadras alcanzadas por un recuadro (o por un clic, si el recuadro es diminuto).</summary>
        public static List<int> Pick(ScreenRect rect, IReadOnlyList<ScreenPoint> soldiers)
        {
            var hits = new List<int>();
            if (soldiers == null) return hits;
            if (IsClick(rect))
            {
                float cx = (rect.XMin + rect.XMax) * 0.5f, cy = (rect.YMin + rect.YMax) * 0.5f;
                float best = ClickRadiusPx * ClickRadiusPx;
                int bestSquad = -1;
                for (int i = 0; i < soldiers.Count; i++)
                {
                    ScreenPoint p = soldiers[i];
                    if (!p.InFront) continue;
                    float d2 = (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy);
                    if (d2 <= best)
                    {
                        best = d2;
                        bestSquad = p.SquadId;
                    }
                }
                if (bestSquad >= 0) hits.Add(bestSquad);
                return hits;
            }

            for (int i = 0; i < soldiers.Count; i++)
            {
                ScreenPoint p = soldiers[i];
                if (p.InFront && rect.Contains(p.X, p.Y) && !hits.Contains(p.SquadId)) hits.Add(p.SquadId);
            }
            return hits;
        }

        /// <summary>Aplica lo alcanzado a la selección actual según el modo.</summary>
        public static void Apply(ISet<int> selection, IEnumerable<int> hits, SelectionMode mode)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (mode == SelectionMode.Replace) selection.Clear();
            if (hits == null) return;
            foreach (int id in hits)
            {
                if (mode == SelectionMode.Toggle && selection.Contains(id)) selection.Remove(id);
                else selection.Add(id);
            }
        }
    }
}
