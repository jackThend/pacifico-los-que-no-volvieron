using System.Collections.Generic;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Runtime.Data
{
    /// <summary>
    /// Asset editable de un arma. Los valores iniciales se generan desde
    /// <see cref="HistoricalWeapons"/> (menú Pacífico/Datos/Generar armas históricas).
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Pacífico/Datos/Arma", order = 0)]
    public sealed class WeaponDataSO : ScriptableObject
    {
        [Header("Identidad")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private WeaponClass weaponClass;
        [SerializeField] private ActionType action;
        [SerializeField] private Faction[] factions = new Faction[0];
        [SerializeField, TextArea] private string historicalNote;

        [Header("Balística")]
        [SerializeField, Min(0f)] private float caliberMm;
        [SerializeField, Min(0f), Tooltip("Segundos del ciclo completo de recarga.")] private float reloadSeconds;
        [SerializeField, Min(0f)] private float damage;
        [SerializeField, Range(0f, 10f), Tooltip("Cono de dispersión en grados.")] private float spreadDegrees;
        [SerializeField, Min(0f)] private float effectiveRangeM;
        [SerializeField, Min(0f)] private float maxRangeM;
        [SerializeField, Min(0f)] private float muzzleVelocityMs;
        [SerializeField, Min(0)] private int capacity;

        public string Id => id;
        public float ReloadSeconds => reloadSeconds;
        public float Damage => damage;
        public float SpreadDegrees => spreadDegrees;
        public float EffectiveRangeM => effectiveRangeM;

        public WeaponSpec ToSpec()
        {
            return new WeaponSpec(id, displayName, weaponClass, action, (Faction[])factions.Clone(),
                caliberMm, reloadSeconds, damage, spreadDegrees,
                effectiveRangeM, maxRangeM, muzzleVelocityMs, capacity, historicalNote);
        }

        public void ApplySpec(WeaponSpec spec)
        {
            id = spec.Id;
            displayName = spec.DisplayName;
            weaponClass = spec.Class;
            action = spec.Action;
            factions = new List<Faction>(spec.Factions).ToArray();
            historicalNote = spec.HistoricalNote;
            caliberMm = spec.CaliberMm;
            reloadSeconds = spec.ReloadSeconds;
            damage = spec.Damage;
            spreadDegrees = spec.SpreadDegrees;
            effectiveRangeM = spec.EffectiveRangeM;
            maxRangeM = spec.MaxRangeM;
            muzzleVelocityMs = spec.MuzzleVelocityMs;
            capacity = spec.Capacity;
        }

        private void OnValidate()
        {
            foreach (var error in WeaponValidator.Validate(ToSpec()))
            {
                Debug.LogWarning($"[WeaponDataSO] {name}: {error}", this);
            }
        }
    }
}
