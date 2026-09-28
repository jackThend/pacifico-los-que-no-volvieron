using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    public enum ShockOutcome
    {
        /// <summary>Siguen trabados cuerpo a cuerpo.</summary>
        Fighting,
        /// <summary>Los defensores rompen y huyen.</summary>
        DefendersBreak,
        /// <summary>La carga se estrella: los atacantes retroceden.</summary>
        AttackersRepulsed,
    }

    public struct ShockStep
    {
        public int AttackerCasualties;
        public int DefenderCasualties;
        public ShockOutcome Outcome;
    }

    /// <summary>
    /// Choque a la bayoneta entre dos escuadras (ROADMAP 6.3, la carga de los Colorados). Cada hombre trabado
    /// produce bajas en el contrario a un ritmo fijo; el atacante tiene la ventaja del impulso durante los primeros
    /// segundos, y un defensor suprimido o flanqueado apenas responde. Casi nunca se lucha hasta el último hombre:
    /// quien pierde una parte de su fuerza o se ve muy superado en número rompe. Determinista con su semilla; las
    /// bajas se sortean hombre a hombre como un proceso de Poisson, así que su esperanza no depende del paso de tiempo.
    /// </summary>
    public sealed class ShockCombat
    {
        /// <summary>Bajas por segundo que causa cada hombre trabado (hombres/s). Estimación de diseño.</summary>
        public const float LethalityPerManPerSecond = 0.06f;
        /// <summary>Multiplicador del atacante mientras dura el impulso de la carga.</summary>
        public const float ChargeImpetus = 1.5f;
        public const float ImpetusSeconds = 8f;
        /// <summary>Un defensor suprimido responde a este ritmo relativo.</summary>
        public const float SuppressedDefence = 0.4f;
        /// <summary>Pérdidas (fracción de la fuerza al trabarse) a partir de las que se rompe.</summary>
        public const float DefenderBreakLosses = 0.5f;
        public const float AttackerBreakLosses = 0.5f;
        /// <summary>Superado en número por este factor, el defensor rompe.</summary>
        public const float OutnumberedBreakRatio = 3f;

        private readonly Random _random;
        private readonly int _attackersAtContact;
        private readonly int _defendersAtContact;

        public ShockCombat(int attackers, int defenders, int seed)
        {
            if (attackers < 0 || defenders < 0) throw new ArgumentOutOfRangeException();
            _attackersAtContact = Math.Max(1, attackers);
            _defendersAtContact = Math.Max(1, defenders);
            _random = new Random(seed);
        }

        public float Elapsed { get; private set; }
        public ShockOutcome Outcome { get; private set; } = ShockOutcome.Fighting;
        public int AttackerLosses { get; private set; }
        public int DefenderLosses { get; private set; }

        /// <param name="attackers">Atacantes en pie ahora.</param>
        /// <param name="defenders">Defensores en pie ahora.</param>
        /// <param name="defenderSuppressed">El defensor estaba suprimido al recibir la carga.</param>
        public ShockStep Step(float dt, int attackers, int defenders, bool defenderSuppressed)
        {
            var step = new ShockStep { Outcome = Outcome };
            if (Outcome != ShockOutcome.Fighting || dt <= 0f) return step;
            if (defenders <= 0)
            {
                Outcome = step.Outcome = ShockOutcome.DefendersBreak;
                return step;
            }
            if (attackers <= 0)
            {
                Outcome = step.Outcome = ShockOutcome.AttackersRepulsed;
                return step;
            }
            float impetus = Elapsed < ImpetusSeconds ? ChargeImpetus : 1f;
            float defence = defenderSuppressed ? SuppressedDefence : 1f;
            Elapsed += dt;

            // Cada hombre trabado derriba a un contrario con probabilidad λ·dt en este paso (proceso de Poisson: la
            // esperanza no depende del paso; el resultado de cada choque, sí del azar).
            step.DefenderCasualties = Math.Min(defenders, Sample(attackers, LethalityPerManPerSecond * impetus * dt));
            step.AttackerCasualties = Math.Min(attackers, Sample(defenders, LethalityPerManPerSecond * defence * dt));
            DefenderLosses += step.DefenderCasualties;
            AttackerLosses += step.AttackerCasualties;

            int defendersLeft = defenders - step.DefenderCasualties;
            int attackersLeft = attackers - step.AttackerCasualties;
            if (defendersLeft <= 0 || DefenderLosses >= DefenderBreakLosses * _defendersAtContact ||
                attackersLeft >= OutnumberedBreakRatio * defendersLeft)
            {
                Outcome = ShockOutcome.DefendersBreak;
            }
            else if (attackersLeft <= 0 || AttackerLosses >= AttackerBreakLosses * _attackersAtContact)
            {
                Outcome = ShockOutcome.AttackersRepulsed;
            }
            step.Outcome = Outcome;
            return step;
        }

        private int Sample(int men, float probability)
        {
            int count = 0;
            for (int i = 0; i < men; i++)
            {
                if (_random.NextDouble() < probability) count++;
            }
            return count;
        }

        /// <summary>Probabilidad de que la carga triunfe (Monte Carlo), para calibrar y para la interfaz.</summary>
        public static float WinProbability(int attackers, int defenders, bool suppressed, int trials = 400, int seed = 1880)
        {
            int wins = 0;
            for (int t = 0; t < trials; t++)
            {
                var c = new ShockCombat(attackers, defenders, seed + t);
                int a = attackers, d = defenders;
                while (c.Outcome == ShockOutcome.Fighting && c.Elapsed < 120f)
                {
                    ShockStep s = c.Step(0.1f, a, d, suppressed);
                    a -= s.AttackerCasualties;
                    d -= s.DefenderCasualties;
                }
                if (c.Outcome == ShockOutcome.DefendersBreak) wins++;
            }
            return wins / (float)trials;
        }
    }

    /// <summary>
    /// Granada de metralla de un Krupp de montaña sobre una escuadra: la probabilidad de que un hombre caiga
    /// decrece con el cuadrado de la distancia al punto de caída, y la cobertura (zanja, parapeto) la reduce.
    /// </summary>
    public static class ShellBurst
    {
        public const float LethalRadiusM = 12f;
        public const float CenterKillProbability = 0.5f;
        /// <summary>Fracción de la protección de la cobertura que también vale contra la metralla.</summary>
        public const float CoverEffectiveness = 0.8f;

        public static float CasualtyProbability(float distanceM, float cover)
        {
            if (distanceM >= LethalRadiusM) return 0f;
            float falloff = 1f - distanceM / LethalRadiusM;
            return CenterKillProbability * falloff * falloff * (1f - CoverEffectiveness * MathUtil.Clamp01(cover));
        }
    }
}
