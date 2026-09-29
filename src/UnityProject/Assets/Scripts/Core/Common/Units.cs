namespace Pacifico.Core.Common
{
    /// <summary>
    /// Conversiones de unidades de época (pulgadas, libras, nudos) al sistema métrico
    /// que usa la simulación. Las fuentes de 1879 citan casi todo en unidades imperiales.
    /// </summary>
    public static class Units
    {
        public const float MetersPerSecondPerKnot = 0.514444f;
        public const float MillimetersPerInch = 25.4f;
        public const float KilogramsPerPound = 0.45359237f;

        public static float KnotsToMetersPerSecond(float knots) => knots * MetersPerSecondPerKnot;

        public static float MetersPerSecondToKnots(float metersPerSecond) => metersPerSecond / MetersPerSecondPerKnot;

        public static float InchesToMillimeters(float inches) => inches * MillimetersPerInch;

        public static float PoundsToKilograms(float pounds) => pounds * KilogramsPerPound;
    }
}
