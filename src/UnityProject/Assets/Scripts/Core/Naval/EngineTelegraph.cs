using System;

namespace Pacifico.Core.Naval
{
    /// <summary>Órdenes del telégrafo de máquinas (GDD §3.2: W/S recorre Detener, 1/4, Media, Toda fuerza).</summary>
    public enum EngineOrder
    {
        HalfAstern = -1,
        Stop = 0,
        QuarterAhead = 1,
        HalfAhead = 2,
        FullAhead = 3,
    }

    /// <summary>Telégrafo de calderas: traduce la orden a fracción de la velocidad máxima.</summary>
    public sealed class EngineTelegraph
    {
        public const EngineOrder MinOrder = EngineOrder.HalfAstern;
        public const EngineOrder MaxOrder = EngineOrder.FullAhead;

        public EngineOrder Order { get; private set; } = EngineOrder.Stop;

        public event Action<EngineOrder> OrderChanged;

        /// <summary>Sube (+1, W) o baja (-1, S) una posición. Devuelve true si la orden cambió.</summary>
        public bool Step(int direction)
        {
            if (direction == 0) return false;
            int next = (int)Order + Math.Sign(direction);
            if (next < (int)MinOrder || next > (int)MaxOrder) return false;
            Set((EngineOrder)next);
            return true;
        }

        public void Set(EngineOrder order)
        {
            if (order == Order) return;
            Order = order;
            OrderChanged?.Invoke(order);
        }

        /// <summary>Fracción (con signo) de la velocidad máxima que persigue la máquina.</summary>
        public static float SpeedFraction(EngineOrder order)
        {
            switch (order)
            {
                case EngineOrder.HalfAstern: return -0.3f;
                case EngineOrder.QuarterAhead: return 0.25f;
                case EngineOrder.HalfAhead: return 0.5f;
                case EngineOrder.FullAhead: return 1f;
                default: return 0f;
            }
        }

        public static string DisplayName(EngineOrder order)
        {
            switch (order)
            {
                case EngineOrder.HalfAstern: return "Atrás media";
                case EngineOrder.QuarterAhead: return "Avante 1/4";
                case EngineOrder.HalfAhead: return "Avante media";
                case EngineOrder.FullAhead: return "Avante toda";
                default: return "Detener";
            }
        }
    }
}
