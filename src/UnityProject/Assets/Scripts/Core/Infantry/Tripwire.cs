using System;

namespace Pacifico.Core.Infantry
{
    public enum TripwireState
    {
        Armed = 0,
        Disarmed = 1,
        Exploded = 2,
    }

    /// <summary>
    /// «Torpedo de tierra» del capítulo 7: una caja de dinamita enterrada con un alambre tendido a ras de suelo entre dos
    /// estacas. De noche el alambre no se ve: solo lo descubre quien avanza agachado tanteando con el gancho de caña
    /// (a <see cref="RevealRadiusM"/>). Se corta quieto y agachado junto a la carga, con el pulso firme durante
    /// <see cref="DisarmSeconds"/>; si se mueve, hay que empezar de nuevo. Quien cruza el alambre armado lo hace estallar.
    /// Coordenadas en el plano (x, z), en metros.
    /// </summary>
    public sealed class Tripwire
    {
        public const float RevealRadiusM = 4f;
        public const float DisarmReachM = 1.6f;
        /// <summary>Tiempo de pulso firme para cortar el alambre sin tensarlo. Diseño.</summary>
        public const float DisarmSeconds = 3.5f;
        /// <summary>Más deprisa que esto (m/s) no se tiene el pulso firme.</summary>
        public const float SteadySpeedMps = 0.25f;
        /// <summary>Radio en el que la explosión mata (m).</summary>
        public const float LethalRadiusM = 7f;

        public Tripwire(float ax, float az, float bx, float bz)
        {
            Ax = ax;
            Az = az;
            Bx = bx;
            Bz = bz;
        }

        /// <summary>Estacas del alambre; la carga está en el punto medio.</summary>
        public float Ax { get; }
        public float Az { get; }
        public float Bx { get; }
        public float Bz { get; }
        public float ChargeX => 0.5f * (Ax + Bx);
        public float ChargeZ => 0.5f * (Az + Bz);

        public TripwireState State { get; private set; }
        public bool Armed => State == TripwireState.Armed;
        public bool Revealed { get; private set; }
        /// <summary>Progreso del corte (0 a 1).</summary>
        public float Progress { get; private set; }

        /// <summary>Distancia del punto al alambre.</summary>
        public float DistanceToWire(float x, float z)
        {
            float dx = Bx - Ax, dz = Bz - Az;
            float length2 = dx * dx + dz * dz;
            float t = length2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((x - Ax) * dx + (z - Az) * dz) / length2));
            float px = Ax + t * dx - x, pz = Az + t * dz - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }

        /// <summary>El gancho de caña lo descubre si se avanza agachado cerca del alambre.</summary>
        public void Probe(float x, float z, bool crouched)
        {
            if (Armed && crouched && DistanceToWire(x, z) <= RevealRadiusM) Revealed = true;
        }

        /// <summary>
        /// Corte del alambre: avanza mientras se mantiene la acción, quieto y agachado junto a la carga, con el alambre ya
        /// descubierto; si no, el progreso se pierde. Devuelve true en el paso en que queda desactivado.
        /// </summary>
        public bool StepDisarm(float dt, bool holding, float x, float z, float speedMps, bool crouched)
        {
            if (!Armed || dt <= 0f) return false;
            float dx = x - ChargeX, dz = z - ChargeZ;
            bool steady = holding && Revealed && crouched && speedMps <= SteadySpeedMps && dx * dx + dz * dz <= DisarmReachM * DisarmReachM;
            Progress = steady ? Math.Min(1f, Progress + dt / DisarmSeconds) : 0f;
            if (Progress < 1f) return false;
            State = TripwireState.Disarmed;
            return true;
        }

        /// <summary>
        /// Un cuerpo va de (x0, z0) a (x1, z1) en este paso: si cruza el alambre armado, estalla. Devuelve true si estalla.
        /// </summary>
        public bool Crossed(float x0, float z0, float x1, float z1)
        {
            if (!Armed || !SegmentsIntersect(x0, z0, x1, z1, Ax, Az, Bx, Bz)) return false;
            State = TripwireState.Exploded;
            return true;
        }

        /// <summary>Estalla ya (la patrulla chilena que se sale de la senda).</summary>
        public void Detonate()
        {
            if (Armed) State = TripwireState.Exploded;
        }

        public bool IsLethalAt(float x, float z)
        {
            float dx = x - ChargeX, dz = z - ChargeZ;
            return dx * dx + dz * dz <= LethalRadiusM * LethalRadiusM;
        }

        /// <summary>¿Se cortan los segmentos PQ y RS (en el plano)? Incluye el contacto en un extremo.</summary>
        public static bool SegmentsIntersect(float px, float pz, float qx, float qz, float rx, float rz, float sx, float sz)
        {
            float d1 = Cross(rx, rz, sx, sz, px, pz);
            float d2 = Cross(rx, rz, sx, sz, qx, qz);
            float d3 = Cross(px, pz, qx, qz, rx, rz);
            float d4 = Cross(px, pz, qx, qz, sx, sz);
            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f))) return true;
            return (d1 == 0f && OnSegment(rx, rz, sx, sz, px, pz)) || (d2 == 0f && OnSegment(rx, rz, sx, sz, qx, qz)) ||
                   (d3 == 0f && OnSegment(px, pz, qx, qz, rx, rz)) || (d4 == 0f && OnSegment(px, pz, qx, qz, sx, sz));
        }

        private static float Cross(float ax, float az, float bx, float bz, float cx, float cz) => (bx - ax) * (cz - az) - (bz - az) * (cx - ax);

        private static bool OnSegment(float ax, float az, float bx, float bz, float cx, float cz) =>
            Math.Min(ax, bx) <= cx && cx <= Math.Max(ax, bx) && Math.Min(az, bz) <= cz && cz <= Math.Max(az, bz);
    }

    /// <summary>
    /// Soldado herido en el campo de tiro (capítulo 7, acto II): se desangra; quien llega a su lado puede cargarlo (manteniendo
    /// la acción un momento) y llevarlo a cubierto, más despacio; si un enemigo se queda junto a él, lo remata.
    /// </summary>
    public sealed class WoundedComrade
    {
        /// <summary>Lo que aguanta tendido sin que nadie le contenga la hemorragia (s). Diseño.</summary>
        public const float DefaultBleedSeconds = 180f;
        /// <summary>Cargado, el que lo lleva le aprieta la herida: se desangra a la mitad de ritmo.</summary>
        public const float CarriedBleedFactor = 0.5f;
        public const float PickUpSeconds = 1.5f;
        public const float PickUpReachM = 1.8f;
        /// <summary>Velocidad de quien lo carga, como fracción de la normal (sin correr).</summary>
        public const float CarrySpeedFactor = 0.45f;
        /// <summary>Un enemigo a esta distancia durante <see cref="FinishSeconds"/> lo remata.</summary>
        public const float FinishReachM = 2.5f;
        public const float FinishSeconds = 3f;

        private float _pickUp;
        private float _finish;

        public WoundedComrade(float bleedSeconds = DefaultBleedSeconds)
        {
            if (bleedSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(bleedSeconds));
            BleedSeconds = bleedSeconds;
            Remaining = bleedSeconds;
        }

        public float BleedSeconds { get; }
        public float Remaining { get; private set; }
        public bool Carried { get; private set; }
        public bool Safe { get; private set; }
        public bool BledOut { get; private set; }
        public bool Killed { get; private set; }
        public bool Dead => BledOut || Killed;
        public float PickUpProgress => _pickUp / PickUpSeconds;

        /// <summary>
        /// Avanza: hemorragia, carga (mantener la acción a su lado) y remate (un enemigo encima). Si se le deja de cargar
        /// en un sitio seguro, queda a salvo.
        /// </summary>
        public void Step(float dt, bool holdingAction, float carrierDistanceM, float nearestEnemyM)
        {
            if (Safe || Dead || dt <= 0f) return;
            Remaining = Math.Max(0f, Remaining - dt * (Carried ? CarriedBleedFactor : 1f));
            if (Remaining <= 0f)
            {
                BledOut = true;
                return;
            }

            _finish = nearestEnemyM <= FinishReachM ? _finish + dt : 0f;
            if (_finish >= FinishSeconds)
            {
                Killed = true;
                return;
            }

            if (Carried) return;
            _pickUp = holdingAction && carrierDistanceM <= PickUpReachM ? _pickUp + dt : 0f;
            if (_pickUp >= PickUpSeconds) Carried = true;
        }

        /// <summary>Lo deja en un sitio a cubierto: queda a salvo.</summary>
        public void SetDownSafe()
        {
            if (!Carried || Dead) return;
            Carried = false;
            Safe = true;
        }
    }
}
