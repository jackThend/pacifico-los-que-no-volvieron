using System.Collections.Generic;

namespace Pacifico.Core.Ships
{
    /// <summary>Reglas de coherencia para cualquier ShipSpec.</summary>
    public static class ShipValidator
    {
        public static IReadOnlyList<string> Validate(ShipSpec spec)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("La especificación es nula.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(spec.Id)) errors.Add("Id vacío.");
            if (string.IsNullOrWhiteSpace(spec.DisplayName)) errors.Add($"{spec.Id}: nombre vacío.");
            if (spec.DisplacementTons <= 0f) errors.Add($"{spec.Id}: desplazamiento inválido.");
            if (spec.LengthM <= 0f || spec.BeamM <= 0f) errors.Add($"{spec.Id}: dimensiones inválidas.");
            if (spec.BeamM >= spec.LengthM) errors.Add($"{spec.Id}: la manga no puede superar a la eslora.");
            if (spec.MaxSpeedKnots <= 0f || spec.MaxSpeedKnots > 20f) errors.Add($"{spec.Id}: velocidad fuera de rango (0, 20] nudos.");
            if (spec.Crew <= 0) errors.Add($"{spec.Id}: dotación inválida.");
            if (spec.HullIntegrity <= 0f) errors.Add($"{spec.Id}: integridad de casco inválida.");

            if (spec.Armor == null)
            {
                errors.Add($"{spec.Id}: lista de blindaje nula.");
            }
            else
            {
                var zones = new HashSet<ArmorZone>();
                foreach (var plate in spec.Armor)
                {
                    if (plate == null) { errors.Add($"{spec.Id}: placa de blindaje nula."); continue; }
                    if (plate.thicknessInches <= 0f) errors.Add($"{spec.Id}: blindaje {plate.zone} con espesor no positivo.");
                    if (!zones.Add(plate.zone)) errors.Add($"{spec.Id}: zona de blindaje duplicada {plate.zone}.");
                }
                if (spec.Hull == HullMaterial.Wood && spec.Armor.Count > 0)
                    errors.Add($"{spec.Id}: un casco de madera no lleva coraza.");
                if (spec.Hull == HullMaterial.Iron && spec.Armor.Count == 0)
                    errors.Add($"{spec.Id}: un casco de hierro debe declarar su coraza.");
            }

            if (spec.Guns == null || spec.Guns.Count == 0)
            {
                errors.Add($"{spec.Id}: sin artillería.");
            }
            else
            {
                foreach (var battery in spec.Guns)
                {
                    if (battery == null) { errors.Add($"{spec.Id}: batería nula."); continue; }
                    if (string.IsNullOrWhiteSpace(battery.gunName)) errors.Add($"{spec.Id}: batería sin nombre.");
                    if (battery.count < 1) errors.Add($"{spec.Id}: batería {battery.gunName} sin cañones.");
                    if (battery.projectileLbs <= 0f) errors.Add($"{spec.Id}: batería {battery.gunName} con proyectil inválido.");
                    if (battery.reloadSeconds <= 0f) errors.Add($"{spec.Id}: batería {battery.gunName} con recarga inválida.");
                }
            }

            if (spec.HasTurret && spec.ArmorInches(ArmorZone.Turret) <= 0f)
                errors.Add($"{spec.Id}: torreta sin blindaje declarado.");

            return errors;
        }
    }
}
