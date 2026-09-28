namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Órdenes del telégrafo de máquinas (GDD §3.2: Detener, 1/4, Media, Toda
    /// fuerza). Se añade "Atrás" para desengancharse tras un espolonazo.
    /// </summary>
    public enum EngineOrder
    {
        Astern = -1,
        Stop = 0,
        Quarter = 1,
        Half = 2,
        Full = 3
    }

    public static class EngineOrderExtensions
    {
        /// <summary>Fracción de potencia de máquinas solicitada (negativa = atrás).</summary>
        public static float PowerFraction(this EngineOrder order)
        {
            switch (order)
            {
                case EngineOrder.Astern: return -0.25f;
                case EngineOrder.Quarter: return 0.25f;
                case EngineOrder.Half: return 0.5f;
                case EngineOrder.Full: return 1f;
                default: return 0f;
            }
        }

        public static string DisplayName(this EngineOrder order)
        {
            switch (order)
            {
                case EngineOrder.Astern: return "Atrás";
                case EngineOrder.Quarter: return "1/4";
                case EngineOrder.Half: return "Media";
                case EngineOrder.Full: return "Toda fuerza";
                default: return "Detener";
            }
        }
    }
}
