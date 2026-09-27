using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Data
{
    /// <summary>
    /// Datos de un arma editables en el Inspector (ROADMAP 1.1). Se generan desde <see cref="WeaponCatalog"/>
    /// con «Pacífico/Datos/Generar ScriptableObjects históricos» y la simulación los consume vía <see cref="ToSpec"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Pacífico/Datos/Arma", order = 0)]
    public sealed class WeaponDataSO : ScriptableObject
    {
        [Header("Identidad")]
        public string id = string.Empty;
        public string displayName = string.Empty;
        public WeaponKind kind = WeaponKind.Rifle;
        public FiringAction action = FiringAction.BoltAction;
        public AmmoFeed feed = AmmoFeed.SingleShot;
        public List<Faction> usedBy = new List<Faction>();

        [Header("Datos históricos (documentados)")]
        [Range(1800, 1884)] public int yearAdopted = 1870;
        public string cartridge = string.Empty;
        [Min(0f)] public float caliberMm;
        [Tooltip("Velocidad en boca (m/s).")]
        [Min(0f)] public float muzzleVelocityMps;
        [Tooltip("Masa de la bala (g).")]
        [Min(0f)] public float bulletMassG;
        [Min(0f)] public float massKg;
        [Tooltip("Alcance máximo del alza (m).")]
        [Min(0f)] public float maxSightRangeM;
        public SerializableHistoricalSource source = new SerializableHistoricalSource();

        [Header("Ajustes de juego (licencias del GDD)")]
        [Tooltip("Ciclo completo de recarga (monotiro) o tiempo por cartucho (depósito tubular).")]
        [Min(0f)] public float reloadSeconds = 2f;
        [Min(1)] public int magazineCapacity = 1;
        [Range(0f, 100f)] public float baseDamage = 60f;
        [Tooltip("Dispersión del grupo en minutos de ángulo (MOA).")]
        [Min(0f)] public float dispersionMoa = 3f;
        [Min(0f)] public float effectiveRangeM = 300f;
        [Range(0.01f, 1f)] public float minDamageFactor = 0.4f;

        [Header("Cuerpo a cuerpo")]
        [Min(0f)] public float meleeReachM;
        [Range(0f, 100f)] public float meleeDamage;
        [Min(0f)] public float meleeCycleSeconds;

        public WeaponSpec ToSpec()
        {
            return new WeaponSpec
            {
                Id = id,
                DisplayName = displayName,
                Kind = kind,
                Action = action,
                Feed = feed,
                UsedBy = new List<Faction>(usedBy),
                YearAdopted = yearAdopted,
                Cartridge = cartridge,
                CaliberMm = caliberMm,
                MuzzleVelocityMps = muzzleVelocityMps,
                BulletMassG = bulletMassG,
                MassKg = massKg,
                MaxSightRangeM = maxSightRangeM,
                Source = source != null ? source.ToCore() : new HistoricalSource(),
                ReloadSeconds = reloadSeconds,
                MagazineCapacity = magazineCapacity,
                BaseDamage = baseDamage,
                DispersionMoa = dispersionMoa,
                EffectiveRangeM = effectiveRangeM,
                MinDamageFactor = minDamageFactor,
                MeleeReachM = meleeReachM,
                MeleeDamage = meleeDamage,
                MeleeCycleSeconds = meleeCycleSeconds,
            };
        }

        public void CopyFrom(WeaponSpec spec)
        {
            id = spec.Id;
            displayName = spec.DisplayName;
            kind = spec.Kind;
            action = spec.Action;
            feed = spec.Feed;
            usedBy = new List<Faction>(spec.UsedBy);
            yearAdopted = spec.YearAdopted;
            cartridge = spec.Cartridge;
            caliberMm = spec.CaliberMm;
            muzzleVelocityMps = spec.MuzzleVelocityMps;
            bulletMassG = spec.BulletMassG;
            massKg = spec.MassKg;
            maxSightRangeM = spec.MaxSightRangeM;
            source = SerializableHistoricalSource.FromCore(spec.Source);
            reloadSeconds = spec.ReloadSeconds;
            magazineCapacity = spec.MagazineCapacity;
            baseDamage = spec.BaseDamage;
            dispersionMoa = spec.DispersionMoa;
            effectiveRangeM = spec.EffectiveRangeM;
            minDamageFactor = spec.MinDamageFactor;
            meleeReachM = spec.MeleeReachM;
            meleeDamage = spec.MeleeDamage;
            meleeCycleSeconds = spec.MeleeCycleSeconds;
        }

        public ValidationResult Validate() => ToSpec().Validate();

        private void OnValidate()
        {
            var result = Validate();
            if (!result.IsValid) Debug.LogWarning(result.ToString(), this);
        }
    }
}
