using System;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;

namespace Pacifico.Core.Tactics
{
    /// <summary>Postura del soldado: cuánto blanco ofrece.</summary>
    public enum Posture
    {
        Standing = 0,
        Kneeling = 1,
        Prone = 2,
    }

    /// <summary>
    /// Probabilidad de impacto del fuego de fusilería a escala táctica. Cada disparo se modela como una distribución
    /// normal circular alrededor del punto apuntado, con la dispersión del arma multiplicada por el estrés del
    /// combate más un error fijo (distancia mal estimada, prisa, humo). La probabilidad de acertar a una silueta de
    /// anchura w y altura h a distancia d es erf(w / (2√2·σ))·erf(h / (2√2·σ)), con σ = d·tan(σ_ang).
    /// <para>
    /// Calibración (Remington, blanco de pie): ~29 % a 100 m, ~10 % a 200 m, ~3 % a 400 m; tendido, unas cuatro
    /// veces menos. Del orden de los porcentajes de las guerras de 1866–1880, en que se necesitaban cientos de
    /// cartuchos por baja.
    /// </para>
    /// </summary>
    public static class FireModel
    {
        /// <summary>El estrés del combate triplica la dispersión del grupo de tiro de la ficha.</summary>
        public const float CombatDispersionFactor = 3f;
        /// <summary>Error angular fijo del tirador en combate (grados).</summary>
        public const float CombatBaseErrorDeg = 0.25f;
        /// <summary>Tiempo de apuntar tras recargar, más una variación aleatoria de hasta un segundo.</summary>
        public const float AimSeconds = 1.5f;
        public const float AimJitterSeconds = 1f;

        public static float SilhouetteWidthM(Posture posture) => posture == Posture.Prone ? 0.5f : 0.45f;

        public static float SilhouetteHeightM(Posture posture)
        {
            switch (posture)
            {
                case Posture.Kneeling: return 1.1f;
                case Posture.Prone: return 0.35f;
                default: return 1.7f;
            }
        }

        /// <summary>Desviación típica angular del disparo en combate (grados).</summary>
        public static float SigmaDeg(WeaponSpec weapon)
        {
            // Encarado, la ficha da el grupo como ≈ 4σ (igual que RifleShotSolver en primera persona).
            float aimed = weapon.DispersionMoa / 60f / 4f;
            return aimed * CombatDispersionFactor + CombatBaseErrorDeg;
        }

        /// <summary>
        /// Probabilidad de que un disparo alcance a un hombre a <paramref name="distanceM"/>, con la fracción
        /// <paramref name="coverFraction"/> de su silueta tapada (parapeto, zanja).
        /// </summary>
        public static float HitProbability(WeaponSpec weapon, float distanceM, Posture posture, float coverFraction = 0f, float dispersionScale = 1f)
        {
            if (weapon == null || !weapon.IsFirearm) return 0f;
            if (distanceM > weapon.MaxSightRangeM) return 0f;
            float d = Math.Max(1f, distanceM);
            double sigma = d * Math.Tan(SigmaDeg(weapon) * Math.Max(0.1f, dispersionScale) * MathUtil.Deg2Rad);
            float w = SilhouetteWidthM(posture);
            float h = SilhouetteHeightM(posture) * (1f - MathUtil.Clamp01(coverFraction));
            if (h <= 0f) return 0f;
            double k = 2.0 * Math.Sqrt(2.0) * sigma;
            return MathUtil.Erf((float)(w / k)) * MathUtil.Erf((float)(h / k));
        }

        /// <summary>
        /// Probabilidad de que un impacto deje al hombre fuera de combate (muerto o herido): el daño de la ficha a esa
        /// distancia sobre la salud máxima.
        /// </summary>
        public static float IncapacitationProbability(WeaponSpec weapon, float distanceM)
        {
            if (weapon == null) return 0f;
            return MathUtil.Clamp01(weapon.DamageAtDistance(distanceM) / WeaponSpec.MaxHealth);
        }
    }

    /// <summary>Resultado del fuego de una escuadra en un paso.</summary>
    public struct VolleyResult
    {
        public int Shots;
        public int Hits;
        /// <summary>Hombres puestos fuera de combate.</summary>
        public int Casualties;
    }

    /// <summary>
    /// Fuego a discreción de una escuadra (ROADMAP 4.1): cada hombre recarga (el tiempo de la ficha), apunta y
    /// dispara cuando está listo, así que la escuadra dispara un goteo continuo, no descargas a la vez. Determinista
    /// con semilla.
    /// </summary>
    public sealed class SquadFireControl
    {
        private readonly Random _random;
        private float[] _cooldown;

        public SquadFireControl(WeaponSpec weapon, int soldiers, int seed = 1880)
        {
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            _random = new Random(seed);
            _cooldown = new float[Math.Max(0, soldiers)];
            // Al romper el fuego no todos están listos a la vez.
            for (int i = 0; i < _cooldown.Length; i++) _cooldown[i] = (float)_random.NextDouble() * CycleSeconds;
        }

        public WeaponSpec Weapon { get; }

        /// <summary>Tiempo medio entre dos disparos de un mismo hombre.</summary>
        public float CycleSeconds => Weapon.ReloadSeconds + FireModel.AimSeconds + FireModel.AimJitterSeconds * 0.5f;

        /// <summary>Ajusta el número de tiradores (bajas o refuerzos).</summary>
        public void SetSoldiers(int soldiers)
        {
            if (soldiers == _cooldown.Length) return;
            var resized = new float[Math.Max(0, soldiers)];
            for (int i = 0; i < resized.Length; i++) resized[i] = i < _cooldown.Length ? _cooldown[i] : CycleSeconds;
            _cooldown = resized;
        }

        /// <summary>
        /// Avanza el fuego. Solo se dispara si <paramref name="canFire"/> (a tiro, con línea de visión, parados); si
        /// no, los hombres siguen recargando y quedan listos. Bajo fuego (ROADMAP 4.2) la escuadra apunta peor
        /// (<paramref name="dispersionScale"/>) y dispara menos (<paramref name="rateFactor"/>).
        /// </summary>
        public VolleyResult Step(float dt, bool canFire, float distanceM, Posture targetPosture, float targetCover = 0f,
                                 float dispersionScale = 1f, float rateFactor = 1f)
        {
            var result = new VolleyResult();
            if (dt <= 0f) return result;
            float p = FireModel.HitProbability(Weapon, distanceM, targetPosture, targetCover, dispersionScale);
            float incapacitate = FireModel.IncapacitationProbability(Weapon, distanceM);
            float elapsed = dt * MathUtil.Clamp(rateFactor, 0f, 1f);
            for (int i = 0; i < _cooldown.Length; i++)
            {
                _cooldown[i] -= elapsed;
                if (_cooldown[i] > 0f) continue;
                if (!canFire)
                {
                    _cooldown[i] = 0f; // cargado y esperando
                    continue;
                }
                result.Shots++;
                if (_random.NextDouble() < p)
                {
                    result.Hits++;
                    if (_random.NextDouble() < incapacitate) result.Casualties++;
                }
                _cooldown[i] += Weapon.ReloadSeconds + FireModel.AimSeconds + (float)_random.NextDouble() * FireModel.AimJitterSeconds;
            }
            return result;
        }
    }
}
