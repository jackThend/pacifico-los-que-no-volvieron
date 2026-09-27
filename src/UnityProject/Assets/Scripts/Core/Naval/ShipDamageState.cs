using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Naval
{
    /// <summary>Tareas de la brigada de control de averías (GDD §3.2: incendios, achique, vapor).</summary>
    public enum DamageControlAction
    {
        FireFighting = 0,
        Pumping = 1,
        SteamRepair = 2,
    }

    /// <summary>
    /// Estado de averías de un buque (ROADMAP 2.4): estructura, incendios, inundación y calderas, con una única
    /// brigada de control de averías que atiende una tarea a la vez (duración activa + enfriamiento).
    /// Determinista: el azar (inicio de incendios, calderas alcanzadas) usa un generador con semilla.
    /// </summary>
    public sealed class ShipDamageState
    {
        // --- Parámetros de juego -------------------------------------------------------------------
        /// <summary>Flotabilidad de reserva como fracción del desplazamiento: agua embarcada que hunde el buque.</summary>
        public const float ReserveBuoyancyFraction = 0.3f;
        /// <summary>Bombas de achique siempre en marcha (t/s por cada 1.000 t de desplazamiento).</summary>
        public const float BasePumpRatePer1000t = 0.4f;
        public const float BrigadePumpMultiplier = 6f;
        /// <summary>Taponamiento de vías de agua por la brigada (fracción del caudal por segundo).</summary>
        public const float BrigadePatchRate = 0.05f;
        public const float FireDamagePerSecond = 0.6f;
        /// <summary>Tasa de crecimiento logístico del fuego (1/s).</summary>
        public const float FireGrowthPerSecond = 0.02f;
        /// <summary>Intensidad máxima: el fuego no puede crecer sin límite (se queda sin combustible y aire).</summary>
        public const float MaxFireIntensity = 2f;
        /// <summary>Extinción espontánea: solo apaga focos pequeños (por debajo de ~0,23 de intensidad).</summary>
        public const float FireSelfExtinguishPerSecond = 0.004f;
        public const float BrigadeExtinguishPerSecond = 0.12f;
        public const float BrigadeSteamRepairPerSecond = 0.05f;
        /// <summary>Con las cuadernas partidas la brigada apenas logra taponar (fracción de su eficacia normal).</summary>
        public const float BrokenFramesPatchFactor = 0.2f;
        /// <summary>Probabilidad de que una perforación en los extremos trabe el servomotor del timón.</summary>
        public const float SteeringHitChance = 0.1f;
        /// <summary>Segundos de trabajo de la brigada de vapor para liberar el timón.</summary>
        public const float SteeringRepairSeconds = 5f;
        public const float BoilerHitChance = 0.25f;
        public const float BoilerHitDamage = 0.35f;
        /// <summary>Potencia mínima con las calderas destrozadas (siempre queda algo de vapor).</summary>
        public const float MinPropulsion = 0.15f;
        public const float BrigadeActiveSeconds = 15f;
        public const float BrigadeCooldownSeconds = 25f;

        private readonly Random _random;
        private float _steeringRepairProgress;

        public ShipDamageState(ShipSpec spec, int seed = 1879)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _random = new Random(seed);
        }

        public ShipSpec Spec { get; }

        public float StructuralDamage { get; private set; }
        public float WaterTonnes { get; private set; }
        /// <summary>Caudal total de las vías de agua abiertas (t/s).</summary>
        public float InflowRate { get; private set; }
        /// <summary>Intensidad de incendio acumulada: 0 = sin fuego, 1 = un foco pleno (puede superar 1).</summary>
        public float FireIntensity { get; private set; }
        /// <summary>Daño de calderas [0, 1].</summary>
        public float BoilerDamage { get; private set; }
        public bool FramesBroken { get; private set; }
        /// <summary>Servomotor del timón averiado: el timón queda trabado (se repara con la brigada de vapor).</summary>
        public bool SteeringJammed { get; private set; }
        public bool IsSunk { get; private set; }

        public DamageControlAction? ActiveAction { get; private set; }
        public float ActiveRemaining { get; private set; }
        public float CooldownRemaining { get; private set; }

        public event Action Sunk;

        /// <summary>Intensidad que añade cada foco nuevo.</summary>
        public const float FireIntensityPerIgnition = 0.5f;

        public float StructureCapacity => Spec.DisplacementTonnes;
        public float ReserveBuoyancy => Spec.DisplacementTonnes * ReserveBuoyancyFraction;
        public float IntegrityFraction => MathUtil.Clamp01(1f - StructuralDamage / StructureCapacity);
        public float FloodFraction => MathUtil.Clamp01(WaterTonnes / ReserveBuoyancy);
        public bool OnFire => FireIntensity > 0.01f;

        public float BasePumpRate => BasePumpRatePer1000t * Spec.DisplacementTonnes / 1000f;
        public float PumpRate => BasePumpRate * (ActiveAction == DamageControlAction.Pumping ? BrigadePumpMultiplier : 1f);

        /// <summary>Potencia disponible por calderas e inundación (alimenta <see cref="ShipMotionModel.PropulsionFactor"/>).</summary>
        public float PropulsionFactor
        {
            get
            {
                if (IsSunk) return 0f;
                float boilers = Math.Max(MinPropulsion, 1f - BoilerDamage);
                float flooding = 1f - 0.5f * FloodFraction;
                return boilers * flooding;
            }
        }

        public bool CanActivate => !IsSunk && ActiveAction == null && CooldownRemaining <= 0f;

        /// <summary>Ordena a la brigada una tarea. Devuelve false si está ocupada o en enfriamiento.</summary>
        public bool Activate(DamageControlAction action)
        {
            if (!CanActivate) return false;
            ActiveAction = action;
            ActiveRemaining = BrigadeActiveSeconds;
            return true;
        }

        /// <summary>Aplica un impacto de artillería ya resuelto contra el blindaje.</summary>
        public void ApplyImpact(ArmorImpactResult impact, ArmorZone zone, bool belowWaterline, float caliberMm)
        {
            if (IsSunk) return;
            AddStructuralDamage(impact.StructuralDamage);

            if (impact.Penetrated)
            {
                if (_random.NextDouble() < impact.FireChance) FireIntensity = Math.Min(MaxFireIntensity, FireIntensity + FireIntensityPerIgnition);
                if (belowWaterline) InflowRate += caliberMm / 100f;
                bool machinerySpace = zone == ArmorZone.BeltMidships || zone == ArmorZone.Deck;
                if (machinerySpace && _random.NextDouble() < BoilerHitChance)
                {
                    BoilerDamage = MathUtil.Clamp01(BoilerDamage + BoilerHitDamage);
                }
                if (zone == ArmorZone.BeltEnds && _random.NextDouble() < SteeringHitChance)
                {
                    SteeringJammed = true;
                    _steeringRepairProgress = 0f;
                }
            }
            CheckSunk();
        }

        /// <summary>Aplica una embestida recibida.</summary>
        public void ApplyRamReceived(RamResult ram)
        {
            if (IsSunk) return;
            AddStructuralDamage(ram.TargetDamage);
            InflowRate += ram.TargetFloodingRate;
            FramesBroken |= ram.FramesBroken;
            CheckSunk();
        }

        /// <summary>Aplica el daño propio de haber embestido.</summary>
        public void ApplyRamDealt(RamResult ram)
        {
            if (IsSunk) return;
            AddStructuralDamage(ram.RammerDamage);
            CheckSunk();
        }

        public void Step(float dt)
        {
            if (dt <= 0f || IsSunk) return;

            UpdateBrigade(dt);
            bool brigade(DamageControlAction a) => ActiveAction == a;

            // Incendios: crecimiento logístico acotado por MaxFireIntensity. Solo los focos pequeños se apagan
            // solos; los demás arden hasta que actúa la brigada, cuya tasa siempre supera al crecimiento máximo.
            if (FireIntensity > 0f)
            {
                float growth = FireGrowthPerSecond * FireIntensity * (1f - FireIntensity / MaxFireIntensity);
                float extinguish = FireSelfExtinguishPerSecond + (brigade(DamageControlAction.FireFighting) ? BrigadeExtinguishPerSecond : 0f);
                FireIntensity = MathUtil.Clamp(FireIntensity + (growth - extinguish) * dt, 0f, MaxFireIntensity);
                AddStructuralDamage(FireDamagePerSecond * FireIntensity * dt);
            }

            // Inundación: entra agua por las vías, las bombas achican; la brigada además tapona.
            if (brigade(DamageControlAction.Pumping))
            {
                float patch = BrigadePatchRate * (FramesBroken ? BrokenFramesPatchFactor : 1f);
                InflowRate = Math.Max(0f, InflowRate * (1f - patch * dt));
            }
            WaterTonnes = Math.Max(0f, WaterTonnes + (InflowRate - PumpRate) * dt);

            // Vapor: reparación de calderas y del servomotor.
            if (brigade(DamageControlAction.SteamRepair))
            {
                BoilerDamage = Math.Max(0f, BoilerDamage - BrigadeSteamRepairPerSecond * dt);
                if (SteeringJammed)
                {
                    _steeringRepairProgress += dt;
                    if (_steeringRepairProgress >= SteeringRepairSeconds) SteeringJammed = false;
                }
            }

            CheckSunk();
        }

        private void UpdateBrigade(float dt)
        {
            if (ActiveAction != null)
            {
                ActiveRemaining -= dt;
                if (ActiveRemaining <= 0f)
                {
                    ActiveAction = null;
                    ActiveRemaining = 0f;
                    CooldownRemaining = BrigadeCooldownSeconds;
                }
            }
            else if (CooldownRemaining > 0f)
            {
                CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);
            }
        }

        private void AddStructuralDamage(float amount)
        {
            StructuralDamage = Math.Min(StructureCapacity, StructuralDamage + Math.Max(0f, amount));
        }

        private void CheckSunk()
        {
            if (IsSunk) return;
            if (WaterTonnes >= ReserveBuoyancy || StructuralDamage >= StructureCapacity)
            {
                IsSunk = true;
                ActiveAction = null;
                Sunk?.Invoke();
            }
        }
    }
}
