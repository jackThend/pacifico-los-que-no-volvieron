using System.Collections.Generic;
using Pacifico.Core;
using Pacifico.Core.Ships;
using UnityEngine;

namespace Pacifico.Runtime.Data
{
    /// <summary>
    /// Asset editable de un buque. Los valores iniciales se generan desde
    /// <see cref="HistoricalShips"/> (menú Pacífico/Datos/Generar buques históricos).
    /// </summary>
    [CreateAssetMenu(fileName = "Ship_", menuName = "Pacífico/Datos/Buque", order = 1)]
    public sealed class ShipDataSO : ScriptableObject
    {
        [Header("Identidad")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Faction faction;
        [SerializeField] private ShipType shipType;
        [SerializeField] private HullMaterial hull;
        [SerializeField, TextArea] private string historicalNote;

        [Header("Casco y propulsión")]
        [SerializeField, Min(0f)] private float displacementTons;
        [SerializeField, Min(0f)] private float lengthM;
        [SerializeField, Min(0f)] private float beamM;
        [SerializeField, Min(0f), Tooltip("Velocidad máxima a toda fuerza, en nudos.")] private float maxSpeedKnots;
        [SerializeField, Min(0)] private int crew;
        [SerializeField] private bool hasRam;
        [SerializeField, Min(0f)] private float hullIntegrity;

        [Header("Blindaje y artillería")]
        [SerializeField] private ArmorPlate[] armor = new ArmorPlate[0];
        [SerializeField] private GunBattery[] guns = new GunBattery[0];
        [SerializeField, Tooltip("Solo se usa si alguna batería va montada en torre.")]
        private TurretMount turret = new TurretMount();

        public string Id => id;
        public HullMaterial Hull => hull;
        public float MaxSpeedKnots => maxSpeedKnots;
        public bool HasRam => hasRam;
        public float HullIntegrity => hullIntegrity;

        public ShipSpec ToSpec()
        {
            return new ShipSpec(id, displayName, faction, shipType, hull,
                displacementTons, lengthM, beamM, maxSpeedKnots, crew, hasRam, hullIntegrity,
                CloneArmor(armor), CloneGuns(guns), historicalNote,
                HasTurretGuns() ? turret.Clone() : null);
        }

        public void ApplySpec(ShipSpec spec)
        {
            id = spec.Id;
            displayName = spec.DisplayName;
            faction = spec.Faction;
            shipType = spec.Type;
            hull = spec.Hull;
            historicalNote = spec.HistoricalNote;
            displacementTons = spec.DisplacementTons;
            lengthM = spec.LengthM;
            beamM = spec.BeamM;
            maxSpeedKnots = spec.MaxSpeedKnots;
            crew = spec.Crew;
            hasRam = spec.HasRam;
            hullIntegrity = spec.HullIntegrity;
            armor = CloneArmor(spec.Armor);
            guns = CloneGuns(spec.Guns);
            turret = spec.Turret != null ? spec.Turret.Clone() : new TurretMount();
        }

        private bool HasTurretGuns()
        {
            foreach (var battery in guns)
            {
                if (battery != null && battery.mount == GunMount.Turret) return true;
            }
            return false;
        }

        // Copias profundas: el asset nunca comparte instancias con el catálogo estático.
        private static ArmorPlate[] CloneArmor(IReadOnlyList<ArmorPlate> source)
        {
            var copy = new ArmorPlate[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = new ArmorPlate(source[i].zone, source[i].thicknessInches);
            }
            return copy;
        }

        private static GunBattery[] CloneGuns(IReadOnlyList<GunBattery> source)
        {
            var copy = new GunBattery[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var g = source[i];
                copy[i] = new GunBattery(g.gunName, g.count, g.projectileLbs, g.caliberInches, g.muzzleVelocityMs, g.mount, g.reloadSeconds);
            }
            return copy;
        }

        private void OnValidate()
        {
            foreach (var error in ShipValidator.Validate(ToSpec()))
            {
                Debug.LogWarning($"[ShipDataSO] {name}: {error}", this);
            }
        }
    }
}
