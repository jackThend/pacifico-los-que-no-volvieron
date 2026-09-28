using System.Collections.Generic;

namespace Pacifico.Core.Weapons
{
    /// <summary>Reglas de coherencia para cualquier WeaponSpec.</summary>
    public static class WeaponValidator
    {
        public static IReadOnlyList<string> Validate(WeaponSpec spec)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("La especificación es nula.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(spec.Id)) errors.Add("Id vacío.");
            if (string.IsNullOrWhiteSpace(spec.DisplayName)) errors.Add($"{spec.Id}: nombre vacío.");
            if (spec.Factions == null || spec.Factions.Count == 0) errors.Add($"{spec.Id}: sin bando asignado.");
            if (spec.Damage <= 0f) errors.Add($"{spec.Id}: el daño debe ser positivo.");
            if (spec.EffectiveRangeM <= 0f) errors.Add($"{spec.Id}: alcance efectivo debe ser positivo.");
            if (spec.MaxRangeM < spec.EffectiveRangeM) errors.Add($"{spec.Id}: alcance máximo menor que el efectivo.");

            if (spec.IsFirearm)
            {
                if (spec.Action == ActionType.None) errors.Add($"{spec.Id}: arma de fuego sin mecanismo.");
                if (spec.CaliberMm <= 0f) errors.Add($"{spec.Id}: calibre inválido.");
                if (spec.ReloadSeconds <= 0f) errors.Add($"{spec.Id}: tiempo de recarga inválido.");
                if (spec.SpreadDegrees < 0f || spec.SpreadDegrees > 10f) errors.Add($"{spec.Id}: dispersión fuera de rango [0, 10]°.");
                if (spec.MuzzleVelocityMs <= 0f) errors.Add($"{spec.Id}: velocidad de boca inválida.");
                if (spec.Capacity < 1) errors.Add($"{spec.Id}: capacidad debe ser al menos 1.");
                if (spec.Class == WeaponClass.SingleShotRifle && spec.Capacity != 1)
                    errors.Add($"{spec.Id}: un fusil monotiro debe tener capacidad 1.");
            }
            else
            {
                if (spec.Action != ActionType.None) errors.Add($"{spec.Id}: arma blanca con mecanismo de fuego.");
                if (spec.Capacity != 0) errors.Add($"{spec.Id}: arma blanca no admite munición.");
            }

            return errors;
        }
    }
}
