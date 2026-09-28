namespace Pacifico.Core
{
    /// <summary>
    /// Metadatos globales del proyecto. Lógica pura en C# (sin UnityEngine)
    /// para poder compilarse y probarse fuera del editor.
    /// </summary>
    public static class ProjectInfo
    {
        public const string Title = "Pacífico: Los que no volvieron";
        public const string Version = "0.1.0";
        public const int WarStartYear = 1879;
        public const int WarEndYear = 1884;

        /// <summary>Indica si un año pertenece al periodo histórico cubierto por el juego.</summary>
        public static bool IsWithinWarPeriod(int year)
        {
            return year >= WarStartYear && year <= WarEndYear;
        }
    }
}
