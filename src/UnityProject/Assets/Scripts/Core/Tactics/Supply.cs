using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    /// <summary>Lo que hace la escuadra: determina cuánto suda.</summary>
    public enum Exertion
    {
        Resting = 0,
        Marching = 1,
        Fighting = 2,
    }

    /// <summary>
    /// Parámetros de agua y munición (ROADMAP 4.3). Los fisiológicos siguen los estudios militares de operaciones en
    /// el desierto; los de dotación son estimaciones razonables para 1880, marcadas en <see cref="Source"/>.
    /// </summary>
    public sealed class SupplySettings
    {
        /// <summary>Masa corporal media del soldado (kg). Un litro de sudor perdido es un kilo.</summary>
        public float BodyMassKg { get; set; } = 65f;

        /// <summary>Caramayola (cantimplora) del soldado chileno: ~1 litro.</summary>
        public float CanteenLiters { get; set; } = 1f;

        /// <summary>Sudoración (L/h) cargado y al sol: en reposo, marchando y combatiendo.</summary>
        public float SweatRestingLph { get; set; } = 0.35f;
        public float SweatMarchingLph { get; set; } = 1.2f;
        public float SweatFightingLph { get; set; } = 1.5f;

        /// <summary>Se bebe cuando el déficit supera este volumen, y nunca más deprisa de lo que el cuerpo absorbe.</summary>
        public float DrinkThresholdLiters { get; set; } = 0.4f;
        public float MaxAbsorptionLph { get; set; } = 1.2f;

        /// <summary>Dotación de cartuchos por hombre al empezar.</summary>
        public int CartridgesPerMan { get; set; } = 100;
        /// <summary>Por debajo de esta fracción de la dotación, los oficiales imponen economía de fuego.</summary>
        public float LowAmmoFraction { get; set; } = 0.2f;
        public float LowAmmoRateFactor { get; set; } = 0.6f;

        /// <summary>
        /// Horas de fisiología por hora de juego. Una batalla de un cuarto de hora de juego abarca así casi cuatro
        /// horas de sol, lo bastante para que la sed decida (licencia de diseño, como el humo de 3.3).
        /// </summary>
        public float TimeScale { get; set; } = 15f;

        public HistoricalSource Source { get; set; } = new HistoricalSource
        {
            References =
            {
                "Sudoración de 1,5–2,5 L/h de soldados aclimatados en operaciones intensas en el desierto; con equipo, un " +
                "30–50 % más; perder el 2 % del peso corporal ya compromete la tolerancia al calor y la puntería: " +
                "«Nutritional Needs in Hot Environments», National Academies Press (1993), cap. 3; «Commander's Guide for Heat», USMC.",
                "La caramayola y el agua con té de los soldados chilenos en la campaña de Tacna: F. Machuca, «Las cuatro " +
                "campañas de la Guerra del Pacífico», citado en La Tercera, 25-5-2022 («Sangre, charqui y la retirada de Bolivia»).",
                "Distribución de munición en cascada (puerto, depósito mayor, depósitos menores, soldado): Academia de Historia Militar de Chile, «Batalla de Tacna».",
            },
            EstimatedFields =
            {
                nameof(CanteenLiters), nameof(SweatRestingLph), nameof(CartridgesPerMan), nameof(DrinkThresholdLiters), nameof(TimeScale),
            },
            Notes = "TimeScale es una licencia de diseño: comprime las horas de sol de una batalla en minutos de juego.",
        };
    }

    /// <summary>
    /// Agua y cartuchos de una escuadra (ROADMAP 4.3), por hombre medio. Cada hombre suda según lo que hace y el
    /// calor; bebe de su caramayola cuando la sed aprieta, al ritmo que el cuerpo absorbe. Con la caramayola vacía el
    /// déficit crece y la escuadra pierde efectividad: marcha más despacio, apunta peor y dispara menos; pasado el
    /// 7 % del peso, los hombres empiezan a caer por el calor.
    /// </summary>
    public sealed class SquadSupply
    {
        private readonly Random _random;
        private double _collapseAccumulator;

        public SquadSupply(int men, SupplySettings settings = null, int seed = 1880)
        {
            Settings = settings ?? new SupplySettings();
            Men = Math.Max(0, men);
            WaterPerManLiters = Settings.CanteenLiters;
            Cartridges = Men * Settings.CartridgesPerMan;
            _random = new Random(seed);
        }

        public SupplySettings Settings { get; }
        public int Men { get; private set; }
        public float WaterPerManLiters { get; private set; }
        /// <summary>Déficit de agua por hombre (litros de sudor no repuestos).</summary>
        public float DeficitPerManLiters { get; private set; }
        public int Cartridges { get; private set; }

        public float DehydrationPercent => DeficitPerManLiters / Settings.BodyMassKg * 100f;
        public float WaterFraction => Settings.CanteenLiters > 0f ? WaterPerManLiters / Settings.CanteenLiters : 0f;
        public float AmmoFraction => Men > 0 ? Cartridges / (float)(Men * Settings.CartridgesPerMan) : 0f;
        public bool HasAmmo => Cartridges > 0;

        public float SweatRateLph(Exertion exertion, float heat)
        {
            float rate;
            switch (exertion)
            {
                case Exertion.Marching: rate = Settings.SweatMarchingLph; break;
                case Exertion.Fighting: rate = Settings.SweatFightingLph; break;
                default: rate = Settings.SweatRestingLph; break;
            }
            return rate * Math.Max(0f, heat);
        }

        /// <summary>
        /// Avanza <paramref name="gameSeconds"/> de juego (se multiplican por <see cref="SupplySettings.TimeScale"/>).
        /// <paramref name="heat"/>: 1 a pleno sol del mediodía, ~0,4 con camanchaca. Devuelve cuántos hombres caen por
        /// agotamiento por calor en el paso.
        /// </summary>
        public int Step(float gameSeconds, Exertion exertion, float heat)
        {
            if (gameSeconds <= 0f || Men == 0) return 0;
            double hours = gameSeconds * Settings.TimeScale / 3600.0;

            DeficitPerManLiters += (float)(SweatRateLph(exertion, heat) * hours);
            // Bebe lo que haga falta para volver al umbral, sin pasar de lo que absorbe el cuerpo ni de lo que queda.
            float wanted = DeficitPerManLiters - Settings.DrinkThresholdLiters;
            if (wanted > 0f)
            {
                float drink = Math.Min(wanted, Math.Min(WaterPerManLiters, (float)(Settings.MaxAbsorptionLph * hours)));
                WaterPerManLiters -= drink;
                DeficitPerManLiters -= drink;
            }

            // Golpe de calor: por encima del 7 %, una fracción creciente de hombres cae cada hora.
            float excess = DehydrationPercent - 7f;
            if (excess <= 0f) return 0;
            _collapseAccumulator += Men * Math.Min(1f, excess * 0.25f) * hours;
            int collapsed = 0;
            while (_collapseAccumulator >= 1.0 && Men > 0)
            {
                _collapseAccumulator -= 1.0;
                // Un poco de azar para que no caigan a intervalos regulares.
                if (_random.NextDouble() < 0.85) collapsed++;
            }
            Men -= collapsed;
            return collapsed;
        }

        /// <summary>Bajas por el enemigo: los cartuchos de los caídos se recogen; su agua se pierde.</summary>
        public void SetMen(int men) => Men = Math.Max(0, men);

        /// <summary>Gasta cartuchos; devuelve cuántos disparos se pudieron hacer.</summary>
        public int ConsumeCartridges(int shots)
        {
            int used = Math.Min(Math.Max(0, shots), Cartridges);
            Cartridges -= used;
            return used;
        }

        // ------------------------------------------------------------------------------------------
        // Reposición
        // ------------------------------------------------------------------------------------------

        /// <summary>Agua que cabe en las caramayolas más la que los hombres beberían ya (hasta saciarse).</summary>
        public float WaterNeededLiters => Men * (Settings.CanteenLiters - WaterPerManLiters + DeficitPerManLiters);

        public int CartridgesNeeded => Math.Max(0, Men * Settings.CartridgesPerMan - Cartridges);

        /// <summary>Recibe agua de un carro: primero beben (saciar la sed), luego llenan las caramayolas. Devuelve los litros usados.</summary>
        public float ReceiveWater(float liters)
        {
            if (Men == 0 || liters <= 0f) return 0f;
            float perMan = liters / Men;
            float drink = Math.Min(perMan, DeficitPerManLiters);
            DeficitPerManLiters -= drink;
            perMan -= drink;
            float fill = Math.Min(perMan, Settings.CanteenLiters - WaterPerManLiters);
            WaterPerManLiters += fill;
            return (drink + fill) * Men;
        }

        public int ReceiveCartridges(int rounds)
        {
            int taken = Math.Min(Math.Max(0, rounds), CartridgesNeeded);
            Cartridges += taken;
            return taken;
        }

        // ------------------------------------------------------------------------------------------
        // Efectividad
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Efectividad según la deshidratación (fracción del peso perdido): plena hasta el 2 %, 0,85 al 3 %, 0,55 al
        /// 5 %, 0,3 al 7 % y 0,15 desde el 9 %.
        /// </summary>
        public static float EffectivenessAt(float dehydrationPercent)
        {
            float[] pct = { 2f, 3f, 5f, 7f, 9f };
            float[] eff = { 1f, 0.85f, 0.55f, 0.3f, 0.15f };
            if (dehydrationPercent <= pct[0]) return eff[0];
            for (int i = 1; i < pct.Length; i++)
            {
                if (dehydrationPercent <= pct[i]) return MathUtil.Lerp(eff[i - 1], eff[i], MathUtil.InverseLerp(pct[i - 1], pct[i], dehydrationPercent));
            }
            return eff[eff.Length - 1];
        }

        public float Effectiveness => EffectivenessAt(DehydrationPercent);

        /// <summary>Velocidad de marcha relativa por la sed.</summary>
        public float SpeedFactor => 0.4f + 0.6f * Effectiveness;

        /// <summary>Multiplicador de la dispersión del fuego (pulso, vista, atención).</summary>
        public float DispersionScale => 1f / (float)Math.Sqrt(Math.Max(0.25f, Effectiveness));

        /// <summary>Cadencia relativa: la sed y la economía de cartuchos.</summary>
        public float RateFactor
        {
            get
            {
                float rate = Math.Max(0.3f, Effectiveness);
                if (AmmoFraction < Settings.LowAmmoFraction) rate *= Settings.LowAmmoRateFactor;
                return rate;
            }
        }
    }

    /// <summary>
    /// Carro de vituallas (ROADMAP 4.3): lleva pipas de agua y cajones de cartuchos desde el depósito de retaguardia
    /// hasta las escuadras. Reparte a las que están a menos de <see cref="ReachM"/>, a un ritmo limitado (se llenan las
    /// caramayolas por turnos), y cuando se vacía vuelve al depósito a cargar.
    /// </summary>
    public sealed class SupplyCartModel
    {
        public const float ReachM = 20f;
        /// <summary>Dos pipas de ~200 L.</summary>
        public const float WaterCapacityLiters = 400f;
        /// <summary>Seis cajones de 1.000 cartuchos.</summary>
        public const int CartridgeCapacity = 6000;
        public const float WaterRateLps = 1f;
        public const float CartridgeRatePerSecond = 40f;
        /// <summary>En el depósito se carga más deprisa (con toda la intendencia).</summary>
        public const float DepotLoadFactor = 4f;

        private double _cartridgeCarry;

        public SupplyCartModel()
        {
            WaterLiters = WaterCapacityLiters;
            Cartridges = CartridgeCapacity;
        }

        public float WaterLiters { get; private set; }
        public int Cartridges { get; private set; }
        public float WaterFraction => WaterLiters / WaterCapacityLiters;
        public float CartridgeFraction => Cartridges / (float)CartridgeCapacity;
        public bool NeedsReload => (WaterFraction < 0.15f && CartridgeFraction < 0.15f) || WaterFraction < 0.05f || CartridgeFraction < 0.05f;

        /// <summary>Reparte durante <paramref name="dt"/> segundos de juego a una escuadra al alcance.</summary>
        public void Resupply(SquadSupply squad, float dt)
        {
            if (squad == null || dt <= 0f) return;
            float water = Math.Min(WaterLiters, WaterRateLps * dt);
            WaterLiters -= squad.ReceiveWater(water);

            _cartridgeCarry += CartridgeRatePerSecond * dt;
            int batch = Math.Min(Cartridges, (int)_cartridgeCarry);
            int taken = squad.ReceiveCartridges(batch);
            Cartridges -= taken;
            _cartridgeCarry -= batch;
            if (taken < batch) _cartridgeCarry = 0.0; // la escuadra está llena: no se acumula
        }

        /// <summary>Carga en el depósito de retaguardia.</summary>
        public void Reload(float dt)
        {
            if (dt <= 0f) return;
            WaterLiters = Math.Min(WaterCapacityLiters, WaterLiters + WaterRateLps * DepotLoadFactor * dt);
            Cartridges = Math.Min(CartridgeCapacity, Cartridges + (int)Math.Ceiling(CartridgeRatePerSecond * DepotLoadFactor * dt));
        }
    }

    /// <summary>Una escuadra vista por el carro: dónde está y cuánto le falta.</summary>
    public struct SupplyRequest
    {
        public int SquadId;
        public Vec3 Position;
        public float WaterFraction;
        public float AmmoFraction;
        public float DehydrationPercent;

        public SupplyRequest(int squadId, Vec3 position, float waterFraction, float ammoFraction, float dehydrationPercent)
        {
            SquadId = squadId;
            Position = position;
            WaterFraction = waterFraction;
            AmmoFraction = ammoFraction;
            DehydrationPercent = dehydrationPercent;
        }

        /// <summary>Urgencia: falta de agua (y sed ya acumulada) y de cartuchos.</summary>
        public float Urgency => (1f - WaterFraction) + DehydrationPercent / 4f + (1f - AmmoFraction) * 0.8f;
    }

    /// <summary>
    /// Decide adónde va el carro: al depósito si se ha vaciado; si no, a la escuadra más necesitada, descontando la
    /// distancia (un carro no cruza el campo por un poco de agua). Sin nadie que lo necesite, espera.
    /// </summary>
    public static class SupplyDispatcher
    {
        /// <summary>Urgencia mínima para ponerse en marcha.</summary>
        public const float MinUrgency = 0.35f;
        /// <summary>Metros que equivalen a un punto de urgencia.</summary>
        public const float MetersPerUrgency = 250f;

        public const int GoToDepot = -2;
        public const int Wait = -1;

        public static int Choose(SupplyCartModel cart, Vec3 cartPosition, IReadOnlyList<SupplyRequest> squads)
        {
            if (cart.NeedsReload) return GoToDepot;
            int best = Wait;
            float bestScore = 0f;
            if (squads == null) return best;
            for (int i = 0; i < squads.Count; i++)
            {
                SupplyRequest r = squads[i];
                float urgency = r.Urgency;
                if (urgency < MinUrgency) continue;
                Vec3 d = r.Position - cartPosition;
                float distance = new Vec3(d.X, 0f, d.Z).Magnitude;
                float score = urgency - distance / MetersPerUrgency;
                if (best == Wait || score > bestScore)
                {
                    best = r.SquadId;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
