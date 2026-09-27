using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pacifico.Core.Weapons
{
    /// <summary>
    /// Alza graduada de un fusil de 1879 (ROADMAP 3.1, «miras de época»). Cada graduación, cada 100 m hasta el
    /// alcance máximo del alza de la ficha, eleva el cañón lo necesario para que la bala cruce la línea de mira
    /// a esa distancia. Las elevaciones se calculan con <see cref="SmallArmsBallistics"/> y se guardan en caché por
    /// arma: empuñar un fusil ya conocido (recoger un Comblain en Tarapacá) no repite la integración balística.
    /// </summary>
    public sealed class SightLadder
    {
        public const float DefaultStepM = 100f;
        /// <summary>Alza de combate: la graduación con la que se entra en fuego salvo orden en contrario.</summary>
        public const float DefaultBattleSightM = 200f;

        private static readonly Dictionary<string, float[]> ElevationCache = new Dictionary<string, float[]>();
        private static readonly object CacheLock = new object();

        private readonly float[] _ranges;
        private readonly float[] _elevations;

        public SightLadder(WeaponSpec weapon, float stepM = DefaultStepM, float sightHeightM = SmallArmsBallistics.DefaultSightHeightM)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (!weapon.IsFirearm) throw new ArgumentException(weapon.Id + " no es un arma de fuego", nameof(weapon));

            Weapon = weapon;
            SightHeightM = sightHeightM;
            DragFactor = SmallArmsBallistics.DragFactor(weapon);

            var ranges = new List<float>();
            for (float r = stepM; r <= weapon.MaxSightRangeM + 0.5f; r += stepM) ranges.Add(r);
            if (ranges.Count == 0) ranges.Add(weapon.MaxSightRangeM);
            _ranges = ranges.ToArray();

            _elevations = CachedElevations(weapon, stepM, sightHeightM, DragFactor, _ranges);

            Index = Math.Max(0, Array.IndexOf(_ranges, DefaultBattleSightM));
        }

        /// <summary>Número de tablas de elevación calculadas (para diagnóstico y pruebas).</summary>
        public static int CachedTables
        {
            get
            {
                lock (CacheLock) return ElevationCache.Count;
            }
        }

        private static float[] CachedElevations(WeaponSpec weapon, float stepM, float sightHeightM, float drag, float[] ranges)
        {
            // La clave incluye todo lo que influye en la balística: un ajuste de la ficha invalida la caché.
            string key = string.Join("|", weapon.Id,
                weapon.MuzzleVelocityMps.ToString(CultureInfo.InvariantCulture),
                weapon.BulletMassG.ToString(CultureInfo.InvariantCulture),
                weapon.CaliberMm.ToString(CultureInfo.InvariantCulture),
                weapon.MaxSightRangeM.ToString(CultureInfo.InvariantCulture),
                stepM.ToString(CultureInfo.InvariantCulture),
                sightHeightM.ToString(CultureInfo.InvariantCulture));
            lock (CacheLock)
            {
                if (ElevationCache.TryGetValue(key, out float[] cached)) return cached;
            }

            var elevations = new float[ranges.Length];
            for (int i = 0; i < ranges.Length; i++)
            {
                if (!SmallArmsBallistics.TrySolveElevation(ranges[i], weapon.MuzzleVelocityMps, drag, out elevations[i], sightHeightM))
                {
                    throw new InvalidOperationException(weapon.Id + ": no hay solución balística para " + ranges[i] + " m");
                }
            }
            lock (CacheLock) ElevationCache[key] = elevations;
            return elevations;
        }

        public WeaponSpec Weapon { get; }
        public float SightHeightM { get; }
        public float DragFactor { get; }
        public int Count => _ranges.Length;
        public int Index { get; private set; }

        public float RangeM => _ranges[Index];
        public float ElevationDeg => _elevations[Index];
        public float MaxRangeM => _ranges[_ranges.Length - 1];

        public float RangeAt(int index) => _ranges[index];
        public float ElevationAt(int index) => _elevations[index];

        public bool Raise() => SetIndex(Index + 1);
        public bool Lower() => SetIndex(Index - 1);

        public bool SetIndex(int index)
        {
            if (index < 0 || index >= _ranges.Length || index == Index) return false;
            Index = index;
            return true;
        }

        /// <summary>Selecciona la graduación más próxima a una distancia.</summary>
        public void SetRange(float rangeM)
        {
            int best = 0;
            for (int i = 1; i < _ranges.Length; i++)
            {
                if (Math.Abs(_ranges[i] - rangeM) < Math.Abs(_ranges[best] - rangeM)) best = i;
            }
            Index = best;
        }

        /// <summary>
        /// Altura del impacto respecto al punto apuntado (m) al tirar a <paramref name="targetDistanceM"/> con la graduación
        /// actual: positiva = alto, negativa = bajo. Enseña por qué hay que ajustar el alza.
        /// </summary>
        public float ImpactOffsetAt(float targetDistanceM)
        {
            TrajectorySample s = SmallArmsBallistics.Sample(targetDistanceM, ElevationDeg, Weapon.MuzzleVelocityMps, DragFactor, SightHeightM);
            return s.Reached ? s.Height : float.NegativeInfinity;
        }
    }
}
