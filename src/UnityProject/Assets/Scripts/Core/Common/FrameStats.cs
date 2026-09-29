using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pacifico.Core.Common
{
    /// <summary>
    /// Estadística de tiempos de fotograma (ROADMAP 6.6): media, percentiles, peor fotograma, tirones (fotogramas de
    /// más del doble del presupuesto) y veredicto frente al objetivo de 60 FPS. Guarda los tiempos en un búfer
    /// preasignado: medir no debe generar basura.
    /// </summary>
    public sealed class FrameStats
    {
        public const float TargetFps = 60f;
        public const float BudgetMs = 1000f / TargetFps;

        private readonly float[] _ms;
        private readonly float[] _sorted;
        private int _count;
        private int _next;

        public FrameStats(int capacity = 7200)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _ms = new float[capacity];
            _sorted = new float[capacity];
        }

        public int Count => _count;

        /// <summary>Anota un fotograma (segundos). Con el búfer lleno, se descartan los más antiguos.</summary>
        public void Add(float seconds)
        {
            _ms[_next] = seconds * 1000f;
            _next = (_next + 1) % _ms.Length;
            if (_count < _ms.Length) _count++;
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
        }

        public float MeanMs
        {
            get
            {
                if (_count == 0) return 0f;
                double sum = 0;
                for (int i = 0; i < _count; i++) sum += _ms[i];
                return (float)(sum / _count);
            }
        }

        public float MeanFps => MeanMs > 0f ? 1000f / MeanMs : 0f;

        public float MaxMs
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < _count; i++) max = Math.Max(max, _ms[i]);
                return max;
            }
        }

        /// <summary>Percentil (0–100) del tiempo de fotograma (ms), por rango más cercano.</summary>
        public float PercentileMs(float percentile)
        {
            if (_count == 0) return 0f;
            Array.Copy(_ms, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            int rank = (int)Math.Ceiling(Math.Max(0f, Math.Min(100f, percentile)) / 100f * _count) - 1;
            return _sorted[Math.Max(0, Math.Min(_count - 1, rank))];
        }

        /// <summary>Fotogramas de más del doble del presupuesto: los tirones que se notan.</summary>
        public int Hitches
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_ms[i] > 2f * BudgetMs) n++;
                }
                return n;
            }
        }

        /// <summary>Cumple si el percentil 95 está dentro del presupuesto de 60 FPS y los tirones son menos del 0,5 %.</summary>
        public bool MeetsTarget => _count > 0 && PercentileMs(95f) <= BudgetMs && Hitches <= _count * 0.005f;

        public const string CsvHeader = "escena;fotogramas;media_ms;fps_medio;p50_ms;p95_ms;p99_ms;max_ms;tirones;recolecciones_gc;cumple_60fps";

        public string CsvRow(string scene, int gcCollections)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(scene).Append(';').Append(_count).Append(';')
              .Append(MeanMs.ToString("0.00", c)).Append(';').Append(MeanFps.ToString("0.0", c)).Append(';')
              .Append(PercentileMs(50f).ToString("0.00", c)).Append(';').Append(PercentileMs(95f).ToString("0.00", c)).Append(';')
              .Append(PercentileMs(99f).ToString("0.00", c)).Append(';').Append(MaxMs.ToString("0.00", c)).Append(';')
              .Append(Hitches).Append(';').Append(gcCollections).Append(';').Append(MeetsTarget ? "sí" : "no");
            return sb.ToString();
        }
    }
}
