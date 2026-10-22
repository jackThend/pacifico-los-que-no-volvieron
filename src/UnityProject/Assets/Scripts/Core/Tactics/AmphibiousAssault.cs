using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    /// <summary>
    /// Bote de desembarco (capítulo 3, Pisagua): una lancha plana remolcada desde los transportes hasta la playa con una
    /// escuadra a bordo. Durante la travesía no hay cobertura posible: cada hombre cae con una probabilidad por segundo
    /// proporcional a la intensidad del fuego que recibe el bote (fuertes y fusileros de la costa). Determinista con la
    /// semilla del generador que se le pase.
    /// </summary>
    public sealed class LandingBoat
    {
        /// <summary>
        /// Bajas por hombre y segundo por unidad de intensidad de fuego. Calibrado (DEV_LOG 6.8) para que una travesía de
        /// 2 minutos bajo dos fuertes y dos nidos de fusileros intactos cueste casi la mitad del bote, y bajo nidos
        /// suprimidos y fuertes callados, uno o dos hombres.
        /// </summary>
        public const float KillRatePerIntensity = 0.00125f;
        /// <summary>Velocidad de un bote remolcado por una lancha de vapor (m/s). Diseño.</summary>
        public const float DefaultSpeedMps = 2.5f;

        public LandingBoat(int men, float distanceM, float speedMps = DefaultSpeedMps)
        {
            if (men < 1 || distanceM <= 0f || speedMps <= 0f) throw new ArgumentOutOfRangeException();
            StartMen = men;
            Men = men;
            DistanceM = distanceM;
            SpeedMps = speedMps;
        }

        public int StartMen { get; }
        public int Men { get; private set; }
        public float DistanceM { get; }
        public float SpeedMps { get; }
        /// <summary>Fracción recorrida (0 en el costado del transporte, 1 en la playa).</summary>
        public float Progress { get; private set; }
        public bool Beached => Progress >= 1f;
        public float RemainingM => (1f - Progress) * DistanceM;
        public float TransitSeconds => DistanceM / SpeedMps;

        /// <summary>
        /// Contribución de un nido de fusileros a la intensidad de fuego sobre el bote: su fuerza relativa (12 hombres =
        /// 1), lo que la supresión le deja tirar y la distancia (pleno a 200 m o menos; nada más allá del alcance).
        /// </summary>
        public static float RifleIntensity(int strength, float suppression01, float distanceM, float effectiveRangeM = 800f)
        {
            if (strength <= 0 || distanceM >= effectiveRangeM) return 0f;
            float range = 1f - MathUtil.SmoothStep(200f, effectiveRangeM, distanceM);
            return strength / 12f * (1f - MathUtil.Clamp01(suppression01)) * range;
        }

        /// <summary>Avanza la travesía bajo un fuego de intensidad dada. Devuelve las bajas de este paso.</summary>
        public int Step(float dt, float fireIntensity, Random random)
        {
            if (Beached || dt <= 0f) return 0;
            if (random == null) throw new ArgumentNullException(nameof(random));
            int casualties = 0;
            if (fireIntensity > 0f && Men > 0)
            {
                double p = 1.0 - Math.Exp(-KillRatePerIntensity * fireIntensity * dt);
                for (int i = 0; i < Men; i++)
                {
                    if (random.NextDouble() < p) casualties++;
                }
                Men -= casualties;
            }
            Progress = Math.Min(1f, Progress + SpeedMps * dt / DistanceM);
            return casualties;
        }
    }

    /// <summary>
    /// Fuerte costero (Punta Pichalo, Punta Guacas): calla cuando la artillería de la flota le acierta las veces
    /// necesarias. Mientras dispara, su metralla barre los botes y la playa.
    /// </summary>
    public sealed class CoastalFort
    {
        /// <summary>Impactos de la flota que acallan un fuerte de tierra con sus piezas. Diseño.</summary>
        public const int DefaultHitsToSilence = 3;
        /// <summary>Radio en el que una granada naval alcanza las piezas del fuerte (m).</summary>
        public const float HitRadiusM = 15f;
        /// <summary>Alcance de su fuego sobre los botes (m).</summary>
        public const float RangeM = 1500f;

        public CoastalFort(string name, float x, float z, int hitsToSilence = DefaultHitsToSilence)
        {
            if (hitsToSilence < 1) throw new ArgumentOutOfRangeException(nameof(hitsToSilence));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            X = x;
            Z = z;
            HitsToSilence = hitsToSilence;
        }

        public string Name { get; }
        public float X { get; }
        public float Z { get; }
        public int HitsToSilence { get; }
        public int Hits { get; private set; }
        public bool Silenced => Hits >= HitsToSilence;

        /// <summary>Intensidad de su fuego sobre un bote a esa distancia (1 si está activo y a su alcance).</summary>
        public float IntensityOn(float distanceM) => !Silenced && distanceM <= RangeM ? 1f : 0f;

        /// <summary>Una granada cae en (x, z). Devuelve true si es la que lo hace callar.</summary>
        public bool RegisterImpact(float x, float z)
        {
            if (Silenced) return false;
            float dx = x - X, dz = z - Z;
            if (dx * dx + dz * dz > HitRadiusM * HitRadiusM) return false;
            Hits++;
            return Silenced;
        }

        /// <summary>Lo calla ya (si el desembarco se alarga, la flota lo acaba de batir por su cuenta).</summary>
        public void Silence() => Hits = Math.Max(Hits, HitsToSilence);
    }

    /// <summary>Granada de la flota en vuelo.</summary>
    public struct NavalImpact
    {
        public float X;
        public float Z;
    }

    /// <summary>
    /// Artillería de la flota a demanda del jugador («marcar objetivos para la artillería naval»): los blindados disparan
    /// salvas regulares contra el último punto marcado, con la dispersión de un tiro naval a unos 1.500 m. Las granadas
    /// tardan en llegar; <see cref="Step"/> devuelve las que caen en cada paso.
    /// </summary>
    public sealed class NavalBombardment
    {
        /// <summary>Una salva cada 15 s entre los dos blindados (Cochrane y O'Higgins). Diseño.</summary>
        public const float DefaultSalvoIntervalSeconds = 15f;
        public const int DefaultShellsPerSalvo = 2;
        /// <summary>Desviación típica del punto de caída (m). Diseño.</summary>
        public const float DefaultDispersionM = 18f;
        public const float FlightSeconds = 4f;

        private readonly Random _random;
        private readonly List<float> _inFlightTime = new List<float>();
        private readonly List<NavalImpact> _inFlight = new List<NavalImpact>();
        private float _reload;

        public NavalBombardment(int seed = 1879)
        {
            _random = new Random(seed);
        }

        public float SalvoIntervalSeconds { get; set; } = DefaultSalvoIntervalSeconds;
        public int ShellsPerSalvo { get; set; } = DefaultShellsPerSalvo;
        public float DispersionM { get; set; } = DefaultDispersionM;

        public bool HasTarget { get; private set; }
        public float TargetX { get; private set; }
        public float TargetZ { get; private set; }
        public int SalvosFired { get; private set; }
        public float ReloadRemaining => _reload;
        public int InFlight => _inFlight.Count;

        /// <summary>Marca el blanco (la siguiente salva va ahí).</summary>
        public void Designate(float x, float z)
        {
            HasTarget = true;
            TargetX = x;
            TargetZ = z;
        }

        public void Cancel() => HasTarget = false;

        /// <summary>Avanza el tiempo: dispara cuando toca y deja en <paramref name="landed"/> las granadas que caen.</summary>
        public void Step(float dt, List<NavalImpact> landed)
        {
            if (landed == null) throw new ArgumentNullException(nameof(landed));
            landed.Clear();
            if (dt <= 0f) return;

            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                _inFlightTime[i] -= dt;
                if (_inFlightTime[i] > 0f) continue;
                landed.Add(_inFlight[i]);
                _inFlight.RemoveAt(i);
                _inFlightTime.RemoveAt(i);
            }

            _reload = Math.Max(0f, _reload - dt);
            if (!HasTarget || _reload > 0f) return;
            _reload = SalvoIntervalSeconds;
            SalvosFired++;
            for (int i = 0; i < ShellsPerSalvo; i++)
            {
                _inFlight.Add(new NavalImpact { X = TargetX + Gaussian() * DispersionM, Z = TargetZ + Gaussian() * DispersionM });
                _inFlightTime.Add(FlightSeconds + 0.4f * i);
            }
        }

        /// <summary>Probabilidad de que una granada caiga a menos de <paramref name="radiusM"/> del blanco (dispersión circular).</summary>
        public float HitProbability(float radiusM) => 1f - (float)Math.Exp(-radiusM * radiusM / (2f * DispersionM * DispersionM));

        private float Gaussian()
        {
            double u1 = 1.0 - _random.NextDouble(), u2 = _random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }

    /// <summary>
    /// Carga de dinamita con la mecha encendida (los estanques y las locomotoras de Pisagua): estalla si nadie la
    /// alcanza antes de que se consuma; se apaga si el objetivo se toma a tiempo.
    /// </summary>
    public sealed class DemolitionCharge
    {
        public DemolitionCharge(float fuseSeconds)
        {
            if (fuseSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(fuseSeconds));
            FuseSeconds = fuseSeconds;
            Remaining = fuseSeconds;
        }

        public float FuseSeconds { get; }
        public float Remaining { get; private set; }
        public bool Defused { get; private set; }
        public bool Blown { get; private set; }
        public bool Resolved => Defused || Blown;

        /// <summary>Consume la mecha; si el objetivo ya está tomado, la apaga. Devuelve true en el paso en que estalla.</summary>
        public bool Step(float dt, bool secured)
        {
            if (Resolved) return false;
            if (secured)
            {
                Defused = true;
                return false;
            }
            Remaining = Math.Max(0f, Remaining - Math.Max(0f, dt));
            if (Remaining > 0f) return false;
            Blown = true;
            return true;
        }
    }
}
