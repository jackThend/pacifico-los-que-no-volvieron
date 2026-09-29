using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Rescate de un grupo de náufragos (capítulo 1, «¡Arriad los botes salvavidas!»): el buque debe quedarse cerca
    /// y casi parado el tiempo de arriar un bote y subirlos por la borda. Si se aleja o toma arrancada, la maniobra
    /// se deshace poco a poco (el bote vuelve a por ellos), no de golpe.
    /// </summary>
    public sealed class SurvivorRescue
    {
        /// <summary>Distancia máxima de la borda a los náufragos para arriar un bote (m). Diseño.</summary>
        public const float DefaultRadiusM = 45f;
        /// <summary>Arrancada máxima para arriar botes con seguridad: unos tres nudos.</summary>
        public const float DefaultMaxSpeedMps = 1.6f;
        /// <summary>Tiempo de juego para arriar, recoger y subir a bordo (s). Comprimido respecto a la realidad.</summary>
        public const float DefaultHoldSeconds = 4f;
        /// <summary>Velocidad con la que se pierde el progreso fuera de condiciones (fracción de la de avance).</summary>
        public const float DecayFactor = 0.5f;

        public SurvivorRescue(float radiusM = DefaultRadiusM, float maxSpeedMps = DefaultMaxSpeedMps, float holdSeconds = DefaultHoldSeconds)
        {
            if (radiusM <= 0f || maxSpeedMps <= 0f || holdSeconds <= 0f) throw new ArgumentOutOfRangeException();
            RadiusM = radiusM;
            MaxSpeedMps = maxSpeedMps;
            HoldSeconds = holdSeconds;
        }

        public float RadiusM { get; }
        public float MaxSpeedMps { get; }
        public float HoldSeconds { get; }
        public float Progress { get; private set; }
        public bool Rescued { get; private set; }

        public static bool InRange(float distanceM, float radiusM) => distanceM <= radiusM;

        /// <summary>Por qué no avanza el rescate (para la interfaz), o null si avanza.</summary>
        public string Blocker(float distanceM, float speedMps)
        {
            if (Rescued) return null;
            if (distanceM > RadiusM) return "Acércate a " + RadiusM.ToString("0") + " m";
            if (Math.Abs(speedMps) > MaxSpeedMps) return "Demasiada arrancada para arriar los botes";
            return null;
        }

        /// <summary>Avanza el rescate. Devuelve true en el paso en que se completa.</summary>
        public bool Step(float dt, float distanceM, float speedMps)
        {
            if (Rescued || dt <= 0f) return false;
            bool ok = Blocker(distanceM, speedMps) == null;
            float rate = 1f / HoldSeconds;
            Progress = ok ? Math.Min(1f, Progress + dt * rate) : Math.Max(0f, Progress - dt * rate * DecayFactor);
            if (Progress < 1f) return false;
            Rescued = true;
            return true;
        }
    }
}
