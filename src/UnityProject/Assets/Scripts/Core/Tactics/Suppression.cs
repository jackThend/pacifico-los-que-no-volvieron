using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    public enum SuppressionState
    {
        /// <summary>Sin presión: marcha y dispara con normalidad, de pie.</summary>
        Normal = 0,
        /// <summary>Presionado: se agacha (rodilla en tierra), avanza más despacio y apunta peor.</summary>
        Pinned = 1,
        /// <summary>Suprimido: cuerpo a tierra o a cubierto; apenas avanza y su fuego es ineficaz.</summary>
        Suppressed = 2,
    }

    /// <summary>
    /// Supresión de una escuadra (ROADMAP 4.2). Cada bala que le llega (acierte o no) suma presión, más cuanto
    /// más cerca silba y cuanto más próximo está el tirador; cada baja suma mucho más. La presión se disipa
    /// exponencialmente cuando el fuego cesa.
    /// <para>
    /// Con una tasa de llegada constante R el nivel tiende a S* = R·Δ·τ: el fuego de una sola escuadra a 200 m
    /// (≈3 disparos/s) la deja «presionada», pero el fuego <b>concentrado</b> de dos escuadras la suprime. Es la
    /// mecánica del GDD (§3.3): el fuego continuo inmoviliza y hay que flanquear o concentrar para romper una defensa.
    /// </para>
    /// Los estados tienen histéresis para que no parpadeen en el umbral.
    /// </summary>
    public sealed class SuppressionModel
    {
        /// <summary>Presión que suma cada disparo recibido a quemarropa (se atenúa con la distancia del tirador).</summary>
        public const float PerShot = 0.035f;
        /// <summary>Presión extra de cada impacto y de cada compañero que cae a su lado.</summary>
        public const float PerHit = 0.04f;
        public const float PerCasualty = 0.12f;
        /// <summary>Constante de recuperación (s).</summary>
        public const float RecoverySeconds = 6f;

        public const float PinEnter = 0.35f;
        public const float PinExit = 0.22f;
        public const float SuppressEnter = 0.7f;
        public const float SuppressExit = 0.5f;
        public const float Max = 1.5f;

        public float Level { get; private set; }
        public SuppressionState State { get; private set; }

        /// <summary>
        /// Multiplicador de la presión recibida con cobertura total (una zanja o un parapeto tranquilizan). Calibrado
        /// para que una escuadra en la zanja (cobertura 0,8) aguante sin suprimirse el fuego de dos escuadras a 200 m
        /// y haga falta concentrar tres (o flanquearla): «romper una defensa» exige más que el fuego frontal.
        /// </summary>
        public const float CoverDamping = 0.35f;

        /// <summary>
        /// Registra el fuego recibido. <paramref name="distanceM"/> es la distancia al tirador,
        /// <paramref name="effectiveRangeM"/> el alcance eficaz de su arma y <paramref name="coverFraction"/> cuánto
        /// protege la cobertura que ocupa la escuadra.
        /// </summary>
        public void ReceiveFire(int shots, int hits, int casualties, float distanceM, float effectiveRangeM, float coverFraction = 0f)
        {
            if (shots <= 0 && casualties <= 0) return;
            float proximity = ProximityFactor(distanceM, effectiveRangeM);
            float damping = MathUtil.Lerp(1f, CoverDamping, MathUtil.Clamp01(coverFraction));
            float added = (shots * PerShot * proximity + hits * PerHit) * damping + casualties * PerCasualty;
            Level = Math.Min(Max, Level + added);
            UpdateState();
        }

        /// <summary>Balas que silban cerca desde un tirador próximo asustan más: ×1,2 a quemarropa, ×0,4 al doble del alcance eficaz.</summary>
        public static float ProximityFactor(float distanceM, float effectiveRangeM)
        {
            if (effectiveRangeM <= 0f) return 1f;
            return MathUtil.Lerp(1.2f, 0.4f, MathUtil.Clamp01(distanceM / (2f * effectiveRangeM)));
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            Level *= (float)Math.Exp(-dt / RecoverySeconds);
            if (Level < 1e-4f) Level = 0f;
            UpdateState();
        }

        private void UpdateState()
        {
            switch (State)
            {
                case SuppressionState.Normal:
                    if (Level >= SuppressEnter) State = SuppressionState.Suppressed;
                    else if (Level >= PinEnter) State = SuppressionState.Pinned;
                    break;
                case SuppressionState.Pinned:
                    if (Level >= SuppressEnter) State = SuppressionState.Suppressed;
                    else if (Level < PinExit) State = SuppressionState.Normal;
                    break;
                default:
                    if (Level < PinExit) State = SuppressionState.Normal;
                    else if (Level < SuppressExit) State = SuppressionState.Pinned;
                    break;
            }
        }

        /// <summary>Nivel de equilibrio con fuego constante: S* = tasa·Δ·τ (sin impactos ni bajas).</summary>
        public static float SteadyLevel(float shotsPerSecond, float distanceM, float effectiveRangeM, float coverFraction = 0f)
        {
            float damping = MathUtil.Lerp(1f, CoverDamping, MathUtil.Clamp01(coverFraction));
            return shotsPerSecond * PerShot * ProximityFactor(distanceM, effectiveRangeM) * damping * RecoverySeconds;
        }

        // ------------------------------------------------------------------------------------------
        // Efectos del estado
        // ------------------------------------------------------------------------------------------

        /// <summary>Velocidad de marcha relativa: el fuego ralentiza a la escuadra (suprimida, se arrastra).</summary>
        public static float SpeedFactor(SuppressionState state)
        {
            switch (state)
            {
                case SuppressionState.Pinned: return 0.6f;
                case SuppressionState.Suppressed: return 0.25f;
                default: return 1f;
            }
        }

        /// <summary>Postura que adopta sin cobertura.</summary>
        public static Posture PostureFor(SuppressionState state)
        {
            switch (state)
            {
                case SuppressionState.Pinned: return Posture.Kneeling;
                case SuppressionState.Suppressed: return Posture.Prone;
                default: return Posture.Standing;
            }
        }

        /// <summary>Multiplicador de la dispersión del propio fuego (apuntar con la cabeza agachada).</summary>
        public static float AccuracyPenalty(SuppressionState state)
        {
            switch (state)
            {
                case SuppressionState.Pinned: return 1.4f;
                case SuppressionState.Suppressed: return 2.5f;
                default: return 1f;
            }
        }

        /// <summary>Cadencia relativa del propio fuego.</summary>
        public static float RateFactor(SuppressionState state)
        {
            switch (state)
            {
                case SuppressionState.Pinned: return 0.8f;
                case SuppressionState.Suppressed: return 0.4f;
                default: return 1f;
            }
        }
    }

    /// <summary>Un puesto a cubierto: un tramo de zanja o el pie de un parapeto.</summary>
    public struct CoverSpot
    {
        public Vec3 Position;
        /// <summary>Hacia dónde protege (hacia el enemigo), horizontal.</summary>
        public Vec3 Facing;
        /// <summary>Fracción de la silueta que tapa frente al fuego que llega de frente (0–1).</summary>
        public float Protection;

        public CoverSpot(Vec3 position, Vec3 facing, float protection)
        {
            Position = position;
            Facing = facing;
            Protection = protection;
        }

        /// <summary>
        /// Protección frente a una amenaza que llega en <paramref name="threatDirection"/> (del puesto hacia el
        /// enemigo): plena de frente, nula si el enemigo está de lado o detrás del parapeto.
        /// </summary>
        public float ProtectionAgainst(Vec3 threatDirection)
        {
            float alignment = Vec3.Dot(Formation.Flatten(Facing), Formation.Flatten(threatDirection));
            return Protection * MathUtil.InverseLerp(0.3f, 0.8f, alignment);
        }
    }

    /// <summary>
    /// Reparto de puestos a cubierto entre los hombres de una escuadra (ROADMAP 4.2): solo cuentan los puestos a
    /// menos de <see cref="SearchRadiusM"/> del ancla, libres y que protejan del lado por el que llega el fuego. La
    /// asignación minimiza la carrera total (método húngaro) y a igualdad prefiere la mejor protección. A quien no le
    /// toca puesto se echa cuerpo a tierra donde está.
    /// </summary>
    public static class CoverSelector
    {
        public const float SearchRadiusM = 25f;
        /// <summary>Protección mínima para que merezca la pena ir a un puesto.</summary>
        public const float MinProtection = 0.3f;

        /// <summary>
        /// Devuelve, para cada soldado, el índice del puesto asignado en <paramref name="spots"/> o −1.
        /// <paramref name="taken"/> marca los puestos ocupados por otras escuadras (puede ser null).
        /// </summary>
        public static int[] Assign(IReadOnlyList<Vec3> soldiers, Vec3 anchor, IReadOnlyList<CoverSpot> spots, Vec3 threatDirection, ISet<int> taken = null)
        {
            var result = new int[soldiers.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            if (spots == null || spots.Count == 0 || soldiers.Count == 0) return result;

            var candidates = new List<int>();
            for (int j = 0; j < spots.Count; j++)
            {
                if (taken != null && taken.Contains(j)) continue;
                Vec3 d = spots[j].Position - anchor;
                if (new Vec3(d.X, 0f, d.Z).Magnitude > SearchRadiusM) continue;
                if (spots[j].ProtectionAgainst(threatDirection) < MinProtection) continue;
                candidates.Add(j);
            }
            if (candidates.Count == 0) return result;

            // Más hombres que puestos: se completan con puestos «ficticios» (quedarse donde está) de coste alto.
            int n = soldiers.Count;
            int m = Math.Max(candidates.Count, n);
            var cost = new double[n, m];
            const double StayPenalty = 1e4;
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < m; k++)
                {
                    if (k >= candidates.Count)
                    {
                        cost[i, k] = StayPenalty;
                        continue;
                    }
                    CoverSpot spot = spots[candidates[k]];
                    Vec3 d = spot.Position - soldiers[i];
                    double run = Math.Sqrt(d.X * d.X + d.Z * d.Z);
                    // Un metro de carrera equivale a un 10 % de protección: a igualdad, el mejor puesto.
                    cost[i, k] = run - 10.0 * spot.ProtectionAgainst(threatDirection);
                }
            }
            int[] assignment = SlotAssignment.Hungarian(cost);
            for (int i = 0; i < n; i++)
            {
                int k = assignment[i];
                result[i] = k < candidates.Count ? candidates[k] : -1;
            }
            return result;
        }

        /// <summary>
        /// Cobertura efectiva de la escuadra frente a una amenaza: la media, por hombre, de la protección de su puesto
        /// (los que están al descubierto cuentan 0).
        /// </summary>
        public static float SquadCover(IReadOnlyList<int> assigned, IReadOnlyList<CoverSpot> spots, Vec3 threatDirection)
        {
            if (assigned == null || assigned.Count == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < assigned.Count; i++)
            {
                if (assigned[i] >= 0) sum += spots[assigned[i]].ProtectionAgainst(threatDirection);
            }
            return sum / assigned.Count;
        }
    }
}
