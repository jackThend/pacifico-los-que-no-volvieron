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
                    if (battery.caliberInches <= 0f) errors.Add($"{spec.Id}: batería {battery.gunName} con calibre inválido.");
                    if (battery.muzzleVelocityMs <= 0f) errors.Add($"{spec.Id}: batería {battery.gunName} con velocidad de boca inválida.");
                    if (battery.reloadSeconds <= 0f) errors.Add($"{spec.Id}: batería {battery.gunName} con recarga inválida.");
                }
            }

            if (spec.HasTurret && spec.ArmorInches(ArmorZone.Turret) <= 0f)
                errors.Add($"{spec.Id}: torreta sin blindaje declarado.");

            if (spec.Guns != null)
            {
                if (spec.HasTurret && spec.Turret == null)
                    errors.Add($"{spec.Id}: tiene cañones en torre pero no declara el montaje.");
                if (!spec.HasTurret && spec.Turret != null)
                    errors.Add($"{spec.Id}: declara montaje de torre sin cañones en torre.");
            }
            if (spec.Turret != null) ValidateTurret(spec.Id, spec.Turret, errors);

            return errors;
        }

        private static void ValidateTurret(string id, TurretMount turret, List<string> errors)
        {
            if (turret.traverseDegreesPerSecond <= 0f) errors.Add($"{id}: velocidad de giro de torre inválida.");
            if (turret.elevationDegreesPerSecond <= 0f) errors.Add($"{id}: velocidad de elevación inválida.");
            if (turret.minElevationDegrees >= turret.maxElevationDegrees) errors.Add($"{id}: límites de elevación incoherentes.");
            if (turret.maxElevationDegrees <= 0f || turret.maxElevationDegrees >= 45f) errors.Add($"{id}: elevación máxima fuera de (0, 45)°.");
            if (turret.minElevationDegrees < -15f) errors.Add($"{id}: depresión mínima por debajo de -15°.");
            if (turret.muzzleVelocityMs <= 0f) errors.Add($"{id}: velocidad de boca de torre inválida.");
            if (turret.barrelSeparationM < 0f) errors.Add($"{id}: separación de cañones negativa.");
            if (turret.gunHeightM < 0f) errors.Add($"{id}: altura de cañones negativa.");
            if (turret.blindSectors == null)
            {
                errors.Add($"{id}: lista de sectores enmascarados nula.");
                return;
            }
            var blocked = 0f;
            foreach (var sector in turret.blindSectors)
            {
                if (sector == null) { errors.Add($"{id}: sector enmascarado nulo."); continue; }
                if (sector.halfWidthDegrees <= 0f || sector.halfWidthDegrees >= 180f)
                    errors.Add($"{id}: sector enmascarado con semiancho inválido.");
                blocked += 2f * sector.halfWidthDegrees;
            }
            if (blocked >= 360f) errors.Add($"{id}: los sectores enmascarados bloquean todo el horizonte.");
        }
    }
}
