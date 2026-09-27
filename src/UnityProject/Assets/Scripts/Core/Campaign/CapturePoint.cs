using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Posición que se toma quedándose en ella sin enemigos vivos cerca (los cañones Krupp de la pampa en
    /// Tarapacá). El progreso avanza con amigos dentro y ningún enemigo, se congela si hay disputa y retrocede
    /// despacio si se abandona. Una vez tomada, queda tomada.
    /// </summary>
    public sealed class CapturePoint
    {
        public CapturePoint(float secondsToCapture = 4f)
        {
            if (secondsToCapture <= 0f) throw new ArgumentOutOfRangeException(nameof(secondsToCapture));
            SecondsToCapture = secondsToCapture;
        }

        public float SecondsToCapture { get; }
        public float Progress { get; private set; }
        public bool Captured { get; private set; }

        public bool Contested(int friendsInside, int enemiesInside) => friendsInside > 0 && enemiesInside > 0;

        /// <summary>Avanza; devuelve true en el paso en que se toma.</summary>
        public bool Step(float dt, int friendsInside, int enemiesInside)
        {
            if (Captured || dt <= 0f) return false;
            float rate = 1f / SecondsToCapture;
            if (friendsInside > 0 && enemiesInside == 0) Progress = Math.Min(1f, Progress + rate * dt);
            else if (friendsInside == 0) Progress = Math.Max(0f, Progress - rate * 0.25f * dt);
            if (Progress < 1f) return false;
            Captured = true;
            return true;
        }
    }
}
