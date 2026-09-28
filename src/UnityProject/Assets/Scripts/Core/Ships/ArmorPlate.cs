using System;

namespace Pacifico.Core.Ships
{
    /// <summary>
    /// Coraza de una zona del buque. Clase [Serializable] con campos públicos
    /// para que Unity la serialice directamente dentro de ShipDataSO.
    /// </summary>
    [Serializable]
    public sealed class ArmorPlate
    {
        public const float MillimetersPerInch = 25.4f;

        public ArmorZone zone;
        public float thicknessInches;

        public ArmorPlate()
        {
        }

        public ArmorPlate(ArmorZone zone, float thicknessInches)
        {
            this.zone = zone;
            this.thicknessInches = thicknessInches;
        }

        public float ThicknessMm => thicknessInches * MillimetersPerInch;
    }
}
