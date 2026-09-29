using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Effects
{
    /// <summary>
    /// Una bocanada de humo de un disparo: una nube gaussiana que se desplaza, se expande y se diluye.
    /// </summary>
    public struct SmokePuff
    {
        /// <summary>Identificador creciente (estable mientras la bocanada vive): semilla visual del renderizador.</summary>
        public int Id;
        public Vec3 Position;
        public Vec3 Velocity;
        public float Age;
        /// <summary>Masa de humo (residuos sólidos en suspensión) en el instante de la emisión, en gramos.</summary>
        public float InitialMassG;
        /// <summary>Masa de humo que todavía se ve.</summary>
        public float MassG;
        /// <summary>Radio característico r de la nube: la densidad cae a 1/e a esta distancia del centro.</summary>
        public float RadiusM;
        /// <summary>Radio del chorro en la boca y radio al que llega el chorro al frenarse (varía por disparo).</summary>
        public float StartRadiusM;
        public float JetRadiusM;
        /// <summary>Profundidad óptica por el centro de la nube (0: transparente; 3: prácticamente opaca).</summary>
        public float CenterOpticalDepth;
    }

    /// <summary>
    /// Parámetros del humo. Los que se apoyan en la química de la pólvora negra llevan fuente; el resto son decisiones
    /// de dirección («rapidez cinematográfica») o estimaciones razonables, marcadas en <see cref="Source"/>.
    /// </summary>
    public sealed class BlackPowderSmokeSettings
    {
        /// <summary>
        /// Fracción de la carga que acaba como residuo sólido (K₂CO₃, K₂SO₄, K₂S…): ~56 % en masa frente a ~43 % de gases.
        /// Es lo que forma la nube blanca característica de la pólvora negra.
        /// </summary>
        public float SmokeMassFraction { get; set; } = 0.56f;

        /// <summary>Coeficiente de extinción másico del humo (m²/g).</summary>
        public float MassExtinctionM2PerG { get; set; } = 0.5f;

        /// <summary>Velocidad inicial del chorro de humo y constante de frenado por arrastre del aire.</summary>
        public float JetSpeedMps { get; set; } = 14f;
        public float JetDecaySeconds { get; set; } = 0.08f;

        /// <summary>Radio en la boca, y radio del chorro ya frenado por gramo de carga (más un mínimo).</summary>
        public float MuzzleRadiusM { get; set; } = 0.1f;
        public float JetRadiusBaseM { get; set; } = 0.3f;
        public float JetRadiusPerGramM { get; set; } = 0.06f;
        public float JetExpansionSeconds { get; set; } = 0.2f;

        /// <summary>Difusividad turbulenta: r² crece 2·K·t después del chorro.</summary>
        public float TurbulentDiffusivityM2PerS { get; set; } = 0.15f;

        /// <summary>
        /// Constante de desvanecimiento. En 1879 el humo tardaba muchos segundos en abrirse (los combates navales y
        /// de infantería se libraban entre nubes blancas); el juego lo acelera para no cegar al jugador, que es el
        /// criterio de ROADMAP 3.3. Es una licencia de diseño, no un dato.
        /// </summary>
        public float FadeSeconds { get; set; } = 0.9f;

        /// <summary>Ascenso de la nube caliente.</summary>
        public float BuoyancyMps { get; set; } = 0.35f;

        /// <summary>Variación aleatoria por disparo: dirección del chorro (grados), velocidad y tamaño (fracción).</summary>
        public float DirectionJitterDeg { get; set; } = 5f;
        public float SpeedJitter { get; set; } = 0.15f;
        public float SizeJitter { get; set; } = 0.1f;

        /// <summary>Presupuesto: al superarlo se retira la bocanada más antigua (la más tenue).</summary>
        public int MaxPuffs { get; set; } = 64;

        /// <summary>Por debajo de esta profundidad óptica central la bocanada ya no se distingue y se retira.</summary>
        public float MinOpticalDepth { get; set; } = 0.02f;

        /// <summary>
        /// Desvanecimiento cercano a la cámara (distancia del centro de la bocanada al ojo): por debajo de
        /// <see cref="NearFadeStartM"/> no se dibuja; a partir de <see cref="NearFadeEndM"/>, completa. Evita que
        /// el humo recién salido tape toda la pantalla, como hacen los sistemas de partículas con «near fade».
        /// </summary>
        public float NearFadeStartM { get; set; } = 0.6f;
        public float NearFadeEndM { get; set; } = 2.4f;

        public HistoricalSource Source { get; set; } = new HistoricalSource
        {
            References =
            {
                "Composición de los productos de combustión de la pólvora negra (≈56 % sólidos, ≈43 % gases, ≈1 % agua): " +
                "«Black VS Smokeless Powder», The Firearm Blog; «Smokeless powder», Wikipedia (sección de la pólvora negra).",
            },
            EstimatedFields =
            {
                nameof(MassExtinctionM2PerG), nameof(JetSpeedMps), nameof(JetDecaySeconds), nameof(JetRadiusBaseM),
                nameof(JetRadiusPerGramM), nameof(TurbulentDiffusivityM2PerS), nameof(BuoyancyMps), nameof(FadeSeconds),
            },
            Notes = "FadeSeconds es una licencia cinematográfica (ROADMAP 3.3). El resto se ajusta para que una bocanada " +
                    "de un fusil de 5 g de carga forme una nube opaca de ~1 m a 1–2 m de la boca, como en las fotografías " +
                    "y recreaciones con pólvora negra.",
        };
    }

    /// <summary>
    /// Humo de pólvora negra (ROADMAP 3.3) sin dependencias del motor. Cada disparo emite una bocanada gaussiana
    /// cuya masa sale de la carga del cartucho; el chorro se frena con el aire (solución exponencial exacta, así que
    /// no depende de la frecuencia de fotogramas), la nube se expande, sube, deriva con el viento y se desvanece.
    /// <para>
    /// La densidad de una nube de masa M y radio r es ρ(x) = M/(π^{3/2} r³)·exp(−|x−c|²/r²); su profundidad óptica a
    /// lo largo de un rayo a distancia b del centro es τ = κ·M/(π r²)·exp(−b²/r²). Eso permite medir cuánto tapa el
    /// humo la vista (<see cref="Transmittance"/>, <see cref="ViewClarity"/>) y verificar que 20 disparos seguidos no
    /// saturan la imagen. El renderizador de Unity dibuja exactamente estas nubes.
    /// </para>
    /// </summary>
    public sealed class BlackPowderSmokeModel
    {
        private static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        private readonly SmokePuff[] _puffs;
        private readonly Random _random;
        private int _count;
        private int _nextId;

        public BlackPowderSmokeModel(BlackPowderSmokeSettings settings = null, int seed = 1879)
        {
            Settings = settings ?? new BlackPowderSmokeSettings();
            if (Settings.MaxPuffs < 1) throw new ArgumentOutOfRangeException(nameof(settings), "MaxPuffs debe ser al menos 1.");
            _puffs = new SmokePuff[Settings.MaxPuffs];
            _random = new Random(seed);
        }

        public BlackPowderSmokeSettings Settings { get; }

        /// <summary>Viento (m/s). La nube se acelera hacia él con la misma constante con la que se frena el chorro.</summary>
        public Vec3 Wind { get; set; }

        public int Count => _count;

        /// <summary>Bocanadas vivas, de la más antigua a la más reciente.</summary>
        public SmokePuff this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                return _puffs[index];
            }
        }

        /// <summary>Gramos de humo que produce una carga de pólvora negra.</summary>
        public float SmokeMassFor(float powderChargeG) => Math.Max(0f, powderChargeG) * Settings.SmokeMassFraction;

        /// <summary>
        /// Emite la bocanada de un disparo.
        /// </summary>
        /// <param name="muzzle">Boca del cañón.</param>
        /// <param name="direction">Dirección del ánima (se normaliza).</param>
        /// <param name="powderChargeG">Carga de pólvora del cartucho.</param>
        /// <param name="inheritedVelocity">Velocidad del tirador (el humo sale con ella).</param>
        /// <returns>Identificador de la bocanada, o −1 si la carga es nula.</returns>
        public int Emit(Vec3 muzzle, Vec3 direction, float powderChargeG, Vec3 inheritedVelocity = default)
        {
            float mass = SmokeMassFor(powderChargeG);
            if (mass <= 0f) return -1;

            Vec3 axis = direction.Normalized;
            if (axis.SqrMagnitude < 0.5f) axis = new Vec3(0f, 0f, 1f);
            axis = Jitter(axis, Settings.DirectionJitterDeg);

            float speed = Settings.JetSpeedMps * (1f + Settings.SpeedJitter * Signed());
            float size = Math.Max(0.1f, 1f + Settings.SizeJitter * Signed());

            if (_count == _puffs.Length) RemoveAt(0);
            var puff = new SmokePuff
            {
                Id = _nextId++,
                Position = muzzle,
                Velocity = inheritedVelocity + axis * speed,
                Age = 0f,
                InitialMassG = mass,
                MassG = mass,
                StartRadiusM = Settings.MuzzleRadiusM * size,
                JetRadiusM = (Settings.JetRadiusBaseM + Settings.JetRadiusPerGramM * Math.Max(0f, powderChargeG)) * size,
            };
            UpdateShape(ref puff);
            _puffs[_count++] = puff;
            return puff.Id;
        }

        /// <summary>Avanza la simulación. Exacta para viento constante durante el paso: no depende de dt.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            BlackPowderSmokeSettings s = Settings;
            // Velocidad de equilibrio: el aire que arrastra la nube más el ascenso por flotación.
            Vec3 drift = Wind + Up * s.BuoyancyMps;
            float decay = (float)Math.Exp(-dt / s.JetDecaySeconds);
            float travel = s.JetDecaySeconds * (1f - decay);

            int write = 0;
            for (int i = 0; i < _count; i++)
            {
                SmokePuff p = _puffs[i];
                // v(t) = drift + (v₀ − drift)·e^{−t/τ}  ⇒  x(t+dt) = x + drift·dt + (v − drift)·τ·(1 − e^{−dt/τ})
                Vec3 relative = p.Velocity - drift;
                p.Position = p.Position + drift * dt + relative * travel;
                p.Velocity = drift + relative * decay;
                p.Age += dt;
                UpdateShape(ref p);
                if (p.CenterOpticalDepth < s.MinOpticalDepth) continue;
                _puffs[write++] = p;
            }
            for (int i = write; i < _count; i++) _puffs[i] = default;
            _count = write;
        }

        public void Clear()
        {
            Array.Clear(_puffs, 0, _count);
            _count = 0;
        }

        /// <summary>Radio característico de la nube a una edad dada (chorro que se abre + difusión turbulenta).</summary>
        public float RadiusAt(float startRadius, float jetRadius, float age)
        {
            BlackPowderSmokeSettings s = Settings;
            float jet = 1f - (float)Math.Exp(-age / s.JetExpansionSeconds);
            float r2 = startRadius * startRadius + (jetRadius * jetRadius - startRadius * startRadius) * jet
                       + 2f * s.TurbulentDiffusivityM2PerS * age;
            // Nunca radio nulo (la profundidad óptica divide por r²).
            return Math.Max(1e-3f, (float)Math.Sqrt(r2));
        }

        private void UpdateShape(ref SmokePuff p)
        {
            BlackPowderSmokeSettings s = Settings;
            p.RadiusM = RadiusAt(p.StartRadiusM, p.JetRadiusM, p.Age);
            p.MassG = p.InitialMassG * (float)Math.Exp(-p.Age / s.FadeSeconds);
            p.CenterOpticalDepth = s.MassExtinctionM2PerG * p.MassG / ((float)Math.PI * p.RadiusM * p.RadiusM);
        }

        // ------------------------------------------------------------------------------------------
        // Oclusión de la vista
        // ------------------------------------------------------------------------------------------

        /// <summary>Factor de visibilidad de una bocanada según su distancia a la cámara (0: invisible; 1: completa).</summary>
        public float NearFade(float distanceM) => MathUtil.SmoothStep(Settings.NearFadeStartM, Settings.NearFadeEndM, distanceM);

        /// <summary>
        /// Profundidad óptica que ve un ojo en <paramref name="eye"/> mirando en <paramref name="direction"/>
        /// (normalizada). Integra cada nube gaussiana solo por delante del ojo y aplica el desvanecimiento cercano.
        /// </summary>
        public float OpticalDepth(Vec3 eye, Vec3 direction)
        {
            float total = 0f;
            for (int i = 0; i < _count; i++)
            {
                SmokePuff p = _puffs[i];
                Vec3 toCenter = p.Position - eye;
                float along = Vec3.Dot(toCenter, direction);
                float b2 = Math.Max(0f, toCenter.SqrMagnitude - along * along);
                float r = p.RadiusM;
                // Fracción de la columna gaussiana que queda delante del ojo: ½·(1 + erf(s/r)).
                float ahead = 0.5f * (1f + MathUtil.Erf(along / r));
                total += p.CenterOpticalDepth * (float)Math.Exp(-b2 / (r * r)) * ahead * NearFade(toCenter.Magnitude);
            }
            return total;
        }

        /// <summary>Fracción de luz que atraviesa el humo en esa dirección (1: vista limpia).</summary>
        public float Transmittance(Vec3 eye, Vec3 direction) => (float)Math.Exp(-OpticalDepth(eye, direction));

        /// <summary>
        /// Claridad media en un cono de <paramref name="halfAngleDeg"/> alrededor de la mirada: el centro y
        /// <paramref name="rings"/> anillos de 8 rayos. Mide si el humo satura la zona útil de la pantalla.
        /// </summary>
        public float ViewClarity(Vec3 eye, Vec3 forward, float halfAngleDeg = 12f, int rings = 2)
        {
            Vec3 f = forward.Normalized;
            Vec3 right = Vec3.Cross(Up, f);
            if (right.SqrMagnitude < 1e-6f) right = new Vec3(1f, 0f, 0f);
            right = right.Normalized;
            Vec3 up = Vec3.Cross(f, right);

            float sum = Transmittance(eye, f);
            int samples = 1;
            for (int ring = 1; ring <= rings; ring++)
            {
                double tilt = halfAngleDeg * ring / rings * MathUtil.Deg2Rad;
                for (int k = 0; k < 8; k++)
                {
                    double around = (k + 0.5 * (ring & 1)) * Math.PI / 4.0;
                    float sinTilt = (float)Math.Sin(tilt);
                    Vec3 d = f * (float)Math.Cos(tilt) + right * (sinTilt * (float)Math.Cos(around)) + up * (sinTilt * (float)Math.Sin(around));
                    sum += Transmittance(eye, d);
                    samples++;
                }
            }
            return sum / samples;
        }

        // ------------------------------------------------------------------------------------------

        private void RemoveAt(int index)
        {
            Array.Copy(_puffs, index + 1, _puffs, index, _count - index - 1);
            _puffs[--_count] = default;
        }

        private float Signed() => (float)(_random.NextDouble() * 2.0 - 1.0);

        /// <summary>Desvía el eje un ángulo aleatorio de hasta <paramref name="maxDeg"/> grados.</summary>
        private Vec3 Jitter(Vec3 axis, float maxDeg)
        {
            if (maxDeg <= 0f) return axis;
            Vec3 side = Vec3.Cross(Math.Abs(axis.Y) < 0.95f ? Up : new Vec3(1f, 0f, 0f), axis).Normalized;
            Vec3 other = Vec3.Cross(axis, side);
            double angle = maxDeg * MathUtil.Deg2Rad * Math.Sqrt(_random.NextDouble());
            double around = _random.NextDouble() * 2.0 * Math.PI;
            float sin = (float)Math.Sin(angle);
            return (axis * (float)Math.Cos(angle) + side * (sin * (float)Math.Cos(around)) + other * (sin * (float)Math.Sin(around))).Normalized;
        }
    }
}
