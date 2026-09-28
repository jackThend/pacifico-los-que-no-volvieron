namespace Pacifico.Core.Ships
{
    /// <summary>Tipología naval de 1879.</summary>
    public enum ShipType
    {
        Monitor = 0,
        Corvette = 1,
        CentralBatteryIronclad = 2,
        ArmouredFrigate = 3,
        Schooner = 4
    }

    /// <summary>Material del casco: determina la respuesta a impactos y espolonazos.</summary>
    public enum HullMaterial
    {
        Wood = 0,
        Iron = 1
    }

    /// <summary>Zonas del buque que pueden llevar coraza.</summary>
    public enum ArmorZone
    {
        BeltMidship = 0,
        BeltEnds = 1,
        Turret = 2,
        CentralBattery = 3,
        ConningTower = 4,
        Deck = 5
    }

    /// <summary>Tipo de montaje de una batería de artillería.</summary>
    public enum GunMount
    {
        Turret = 0,
        Broadside = 1,
        Pivot = 2,
        CentralBattery = 3
    }
}
