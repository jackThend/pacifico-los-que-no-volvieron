using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>Tarea asignada a la cuadrilla de control de averías (teclas 1/2/3).</summary>
    public enum DamageControlTask
    {
        None = 0,
        Firefighting = 1,
        Pumping = 2,
        BoilerRepair = 3
    }

    /// <summary>
    /// Control de averías (GDD §3.2): incendio, inundación y calderas. Una sola
    /// cuadrilla, asignable a una tarea; las bombas de achique básicas siempre
    /// funcionan. El fuego daña el casco; el agua embarcada y las calderas
    /// averiadas limitan la potencia; si el agua supera la reserva de
    /// flotabilidad el buque se va a pique.
    /// </summary>
    public sealed class DamageControlSystem
    {
        public const float ReserveBuoyancyFraction = 0.35f;
        public const float FireGrowthPerSecond = 0.01f;
        public const float FireHullDamagePerSecond = 4f;
        public const float FireBoilerDamagePerSecond = 0.01f;
        public const float FirefightingPerSecond = 0.08f;
        public const float PassivePumpTonsPerMinute = 5f;
        public const float CrewPumpTonsPerMinute = 30f;
        /// <summary>Apuntalamiento: la cuadrilla de achique reduce la vía de agua (t/min por segundo).</summary>
        public const float ShoringTonsPerMinutePerSecond = 0.5f;
        public const float BoilerRepairPerSecond = 0.03f;
        public const float FloodPowerPenalty = 0.6f;

        // Probabilidades de avería por impacto de artillería que perfora.
        public const float FireChanceOnPenetration = 0.2f;
        public const float FireChanceOnCritical = 0.4f;
        public const float BoilerChanceOnCritical = 0.2f;
        public const float BreachTonsPerMinuteOnCritical = 6f;

        private readonly HullIntegrity hull;

        public float ReserveBuoyancyTons { get; }
        public float FireIntensity { get; private set; }
        public float WaterTons { get; private set; }
        public float InflowTonsPerMinute { get; private set; }
        public float BoilerHealth { get; private set; } = 1f;
        public DamageControlTask Task { get; private set; }

        public bool OnFire => FireIntensity > 0f;
        public float FloodFraction => Math.Min(1f, WaterTons / ReserveBuoyancyTons);
        public bool IsFoundered { get; private set; }

        /// <summary>Potencia máxima disponible para <see cref="ShipMotionModel.SetPowerLimit"/>.</summary>
        public float PowerLimit => Math.Max(0f, BoilerHealth * (1f - FloodPowerPenalty * FloodFraction));

        public event Action Foundered;

        public DamageControlSystem(ShipSpec spec, HullIntegrity hull)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            this.hull = hull ?? throw new ArgumentNullException(nameof(hull));
            ReserveBuoyancyTons = spec.DisplacementTons * ReserveBuoyancyFraction;
        }

        public void AssignTask(DamageControlTask task) => Task = task;

        /// <summary>Pulsar la tecla de la tarea activa la desasigna; otra tecla la cambia.</summary>
        public void ToggleTask(DamageControlTask task) => Task = Task == task ? DamageControlTask.None : task;

        /// <summary>Prioridad para buques de IA: inundación, luego incendio, luego calderas.</summary>
        public DamageControlTask SuggestTask()
        {
            if (InflowTonsPerMinute > 0f || WaterTons > 0f) return DamageControlTask.Pumping;
            if (OnFire) return DamageControlTask.Firefighting;
            if (BoilerHealth < 1f) return DamageControlTask.BoilerRepair;
            return DamageControlTask.None;
        }

        public void StartFire(float intensity) => FireIntensity = Clamp01(Math.Max(FireIntensity, intensity));
        public void AddBreach(float tonsPerMinute) => InflowTonsPerMinute += Math.Max(0f, tonsPerMinute);
        public void DamageBoilers(float amount) => BoilerHealth = Clamp01(BoilerHealth - amount);

        /// <summary>Efectos secundarios de un proyectil. <paramref name="roll"/> devuelve valores en [0, 1).</summary>
        public void ApplyShellImpact(ImpactReport report, Func<double> roll)
        {
            if (report == null || !report.Penetrated) return;
            var critical = report.Result == ImpactResult.CriticalPenetration;

            if (roll() < (critical ? FireChanceOnCritical : FireChanceOnPenetration)) StartFire(0.3f);
            if (critical && report.Zone == ArmorZone.BeltMidship && roll() < BoilerChanceOnCritical) DamageBoilers(0.4f);
            if (critical && report.Zone != ArmorZone.Turret) AddBreach(BreachTonsPerMinuteOnCritical);
        }

        public void ApplyRam(RamReport report)
        {
            if (report == null) return;
            AddBreach(report.FloodingTonsPerMinute);
        }

        public void Step(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || IsFoundered) return;
            var dt = deltaSeconds;

            // Incendio: crece solo, daña casco y, si es intenso, las calderas.
            if (OnFire)
            {
                var fighting = Task == DamageControlTask.Firefighting ? FirefightingPerSecond : 0f;
                FireIntensity = Clamp01(FireIntensity + (FireGrowthPerSecond - fighting) * dt);
                hull.ApplyDamage(FireIntensity * FireHullDamagePerSecond * dt);
                if (FireIntensity > 0.7f) DamageBoilers(FireBoilerDamagePerSecond * dt);
            }

            // Inundación: vías de agua contra bombas; la cuadrilla achica y apuntala.
            var pumps = PassivePumpTonsPerMinute;
            if (Task == DamageControlTask.Pumping)
            {
                pumps += CrewPumpTonsPerMinute;
                InflowTonsPerMinute = Math.Max(0f, InflowTonsPerMinute - ShoringTonsPerMinutePerSecond * dt);
            }
            WaterTons = Math.Max(0f, WaterTons + (InflowTonsPerMinute - pumps) / 60f * dt);

            if (Task == DamageControlTask.BoilerRepair) BoilerHealth = Clamp01(BoilerHealth + BoilerRepairPerSecond * dt);

            if (WaterTons >= ReserveBuoyancyTons)
            {
                IsFoundered = true;
                hull.Founder();
                Foundered?.Invoke();
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
