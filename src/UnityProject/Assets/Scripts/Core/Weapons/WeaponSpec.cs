using System.Collections.Generic;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Especificación inmutable de un arma de época. Es la fuente de verdad
    /// que alimenta a los ScriptableObjects (WeaponDataSO) y a los tests.
    /// Unidades: segundos, metros, metros/segundo, grados y puntos de vida.
    /// </summary>
    public sealed class WeaponSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public WeaponClass Class { get; }
        public ActionType Action { get; }
        public IReadOnlyList<Faction> Factions { get; }
        public float CaliberMm { get; }
        public float ReloadSeconds { get; }
        public float Damage { get; }
        public float SpreadDegrees { get; }
        public float EffectiveRangeM { get; }
        public float MaxRangeM { get; }
        public float MuzzleVelocityMs { get; }
        public int Capacity { get; }
        public string HistoricalNote { get; }

        public WeaponSpec(
            string id,
            string displayName,
            WeaponClass weaponClass,
            ActionType action,
            IReadOnlyList<Faction> factions,
            float caliberMm,
            float reloadSeconds,
            float damage,
            float spreadDegrees,
            float effectiveRangeM,
            float maxRangeM,
            float muzzleVelocityMs,
            int capacity,
            string historicalNote)
        {
            Id = id;
            DisplayName = displayName;
            Class = weaponClass;
            Action = action;
            Factions = factions;
            CaliberMm = caliberMm;
            ReloadSeconds = reloadSeconds;
            Damage = damage;
            SpreadDegrees = spreadDegrees;
            EffectiveRangeM = effectiveRangeM;
            MaxRangeM = maxRangeM;
            MuzzleVelocityMs = muzzleVelocityMs;
            Capacity = capacity;
            HistoricalNote = historicalNote;
        }

        public bool IsFirearm => Class != WeaponClass.Melee;
    }
}
