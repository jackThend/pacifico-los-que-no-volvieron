namespace Pacifico.Core.Weapons
{
    /// <summary>Tipo de arma según su mecánica de uso.</summary>
    public enum WeaponClass
    {
        SingleShotRifle = 0,
        RepeatingCarbine = 1,
        Melee = 2
    }

    /// <summary>Mecanismo de cierre del arma de fuego.</summary>
    public enum ActionType
    {
        None = 0,
        RollingBlock = 1,
        FallingBlockLever = 2,
        BoltAction = 3,
        LeverAction = 4
    }

    /// <summary>Bando que emplea el arma en la campaña.</summary>
    public enum Faction
    {
        Chile = 0,
        Peru = 1,
        Bolivia = 2,
        Alliance = 3
    }
}
