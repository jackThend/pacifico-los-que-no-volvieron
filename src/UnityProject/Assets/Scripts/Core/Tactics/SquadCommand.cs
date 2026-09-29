using System;
using System.Collections.Generic;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    /// <summary>
    /// Marcha del ancla de la escuadra por un camino (las esquinas que devuelve el NavMesh). El ancla es un punto
    /// virtual: los soldados siguen sus puestos alrededor de ella. Avanza a paso de marcha, frena si los hombres se
    /// quedan atrás (cohesión) y gira la formación con una velocidad angular que los extremos puedan seguir.
    /// </summary>
    public sealed class SquadMarch
    {
        /// <summary>Paso de marcha de la infantería cargada (~1,4 m/s, ≈ 110 pasos de 75 cm por minuto).</summary>
        public const float MarchSpeedMps = 1.4f;
        /// <summary>Carrera de un soldado que alcanza su puesto.</summary>
        public const float RunSpeedMps = 3.2f;
        public const float MaxTurnRateDeg = 60f;
        /// <summary>En los últimos metros la formación gira hacia la orientación ordenada.</summary>
        public const float FaceBlendDistanceM = 8f;
        /// <summary>Con un giro mayor que este al recibir la orden, la escuadra da media vuelta en el sitio.</summary>
        public const float AboutFaceThresholdDeg = 100f;
        /// <summary>
        /// Si girar la formación entera (rueda) costaría más que esto, los hombres cambian de frente cada uno por su
        /// lado hacia los nuevos puestos: así la guerrilla, con 55 m de frente, no tarda medio minuto en girar.
        /// </summary>
        public const float MaxWheelSeconds = 4f;

        private readonly List<Vec3> _path = new List<Vec3>();
        private int _next;
        private Vec3 _finalFacing;

        public SquadMarch(Vec3 anchor, Vec3 facing)
        {
            Anchor = anchor;
            Facing = Formation.Flatten(facing);
            _finalFacing = Facing;
        }

        public Vec3 Anchor { get; private set; }
        public Vec3 Facing { get; private set; }
        public bool Moving => _next < _path.Count;
        public float Speed { get; private set; }
        /// <summary>Anchura del frente (para limitar el giro: los extremos no pueden correr más que un hombre).</summary>
        public float Frontage { get; set; }

        /// <summary>Multiplicador de la velocidad de marcha (la supresión la reduce, ROADMAP 4.2).</summary>
        public float SpeedFactor { get; set; } = 1f;

        /// <summary>
        /// Aire de la marcha: 1 = paso ordinario; <see cref="TrotPace"/> = paso de carga (los Colorados «avanzan al
        /// trote»); <see cref="CavalryPace"/> = caballería al galope corto.
        /// </summary>
        public float Pace { get; set; } = 1f;

        public const float TrotPace = 2.1f;
        public const float CavalryPace = 4f;

        /// <summary>La formación ha cambiado de frente de golpe: hay que reasignar los puestos.</summary>
        public bool Reformed { get; private set; }

        public void ClearReformed() => Reformed = false;

        /// <summary>Velocidad angular máxima de la rueda con este frente y esta cohesión (°/s).</summary>
        public float TurnRateDeg(float cohesion)
        {
            float halfFront = Math.Max(0.5f, Frontage * 0.5f);
            float wingLimitDeg = (RunSpeedMps - MarchSpeedMps) / halfFront / MathUtil.Deg2Rad;
            return Math.Min(MaxTurnRateDeg, wingLimitDeg) * cohesion;
        }

        private bool NeedsSnap(float turnDeg)
        {
            return turnDeg > AboutFaceThresholdDeg || (turnDeg > 45f && turnDeg / TurnRateDeg(1f) > MaxWheelSeconds);
        }

        public float RemainingDistance
        {
            get
            {
                if (!Moving) return 0f;
                float total = Horizontal(_path[_next] - Anchor).Magnitude;
                for (int i = _next + 1; i < _path.Count; i++) total += Horizontal(_path[i] - _path[i - 1]).Magnitude;
                return total;
            }
        }

        /// <summary>
        /// Ordena marchar por <paramref name="path"/> (sin incluir, o incluyendo, la posición actual) y acabar mirando
        /// a <paramref name="finalFacing"/>. Devuelve true si la escuadra ha dado media vuelta (hay que reasignar
        /// puestos para que nadie cruce la formación).
        /// </summary>
        public bool Order(IReadOnlyList<Vec3> path, Vec3 finalFacing)
        {
            _path.Clear();
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    if (Horizontal(path[i] - (_path.Count > 0 ? _path[_path.Count - 1] : Anchor)).Magnitude > 0.05f) _path.Add(path[i]);
                }
            }
            _next = 0;
            _finalFacing = Formation.Flatten(finalFacing);

            Vec3 initial = DesiredFacing();
            float turn = Math.Abs(MathUtil.DeltaAngle(Formation.HeadingDeg(Facing), Formation.HeadingDeg(initial)));
            if (!NeedsSnap(turn)) return false;
            Facing = initial;
            return true;
        }

        /// <summary>Cambia la orientación en el sitio (media vuelta o encarar a un enemigo).</summary>
        public void FaceTowards(Vec3 facing, bool immediate)
        {
            _finalFacing = Formation.Flatten(facing);
            if (immediate) Facing = _finalFacing;
        }

        public void Halt()
        {
            _path.Clear();
            _next = 0;
            _finalFacing = Facing;
            Speed = 0f;
        }

        /// <summary>Cohesión según el rezagado más retrasado: 1 hasta 1,5 m de su puesto; 0,15 a 8 m o más.</summary>
        public static float CohesionFactor(float maxLagM)
        {
            return MathUtil.Lerp(1f, 0.15f, MathUtil.InverseLerp(1.5f, 8f, maxLagM));
        }

        public void Step(float dt, float maxLagM)
        {
            if (dt <= 0f) return;
            float cohesion = CohesionFactor(maxLagM);

            // 1) Giro limitado: el hombre del extremo recorre (frente/2)·ω; no puede ir más deprisa que corriendo.
            //    Si la rueda fuera demasiado larga (esquina del camino, frente muy ancho), se cambia de frente de golpe.
            float heading = Formation.HeadingDeg(Facing);
            float target = Formation.HeadingDeg(DesiredFacing());
            if (NeedsSnap(Math.Abs(MathUtil.DeltaAngle(heading, target))))
            {
                heading = target;
                Reformed = true;
            }
            float newHeading = MathUtil.MoveTowardsAngle(heading, target, TurnRateDeg(cohesion) * dt);
            Facing = Formation.FromHeadingDeg(newHeading);

            // 2) Avance: no se echa a andar hasta estar casi orientado (evita avanzar de lado con un frente ancho).
            if (!Moving)
            {
                Speed = 0f;
                return;
            }
            float misalignment = Math.Abs(MathUtil.DeltaAngle(newHeading, target));
            float alignFactor = MathUtil.Lerp(1f, 0f, MathUtil.InverseLerp(20f, 60f, misalignment));
            Speed = MarchSpeedMps * Math.Max(1f, Pace) * cohesion * alignFactor * MathUtil.Clamp01(SpeedFactor);
            float travel = Speed * dt;
            while (travel > 0f && Moving)
            {
                Vec3 to = Horizontal(_path[_next] - Anchor);
                float d = to.Magnitude;
                if (d <= travel)
                {
                    Anchor = new Vec3(_path[_next].X, _path[_next].Y, _path[_next].Z);
                    travel -= d;
                    _next++;
                }
                else
                {
                    Vec3 step = to / d * travel;
                    // La altura del ancla se interpola entre esquinas (el terreno la corrige en Unity).
                    float fraction = travel / d;
                    Anchor = new Vec3(Anchor.X + step.X, Anchor.Y + (_path[_next].Y - Anchor.Y) * fraction, Anchor.Z + step.Z);
                    travel = 0f;
                }
            }
        }

        /// <summary>Hacia dónde debe mirar la formación: la tangente del camino y, al final, la orientación ordenada.</summary>
        private Vec3 DesiredFacing()
        {
            if (!Moving) return _finalFacing;
            Vec3 tangent = Horizontal(_path[_next] - Anchor);
            if (tangent.Magnitude < 0.05f) return _finalFacing;
            tangent = tangent.Normalized;
            float remaining = RemainingDistance;
            if (remaining >= FaceBlendDistanceM) return tangent;
            float t = 1f - remaining / FaceBlendDistanceM;
            float a = Formation.HeadingDeg(tangent);
            float b = Formation.HeadingDeg(_finalFacing);
            return Formation.FromHeadingDeg(a + MathUtil.DeltaAngle(a, b) * t);
        }

        private static Vec3 Horizontal(Vec3 v) => new Vec3(v.X, 0f, v.Z);
    }

    /// <summary>
    /// Mando de una escuadra de 8 a 12 hombres (ROADMAP 4.1): formación, puestos, marcha y bajas. No mueve a los
    /// soldados (eso lo hace el NavMesh en Unity, o la simulación en las pruebas): les dice dónde deben estar y a
    /// qué velocidad ir. Los puestos se reasignan con <see cref="SlotAssignment"/> al cambiar de formación, al dar
    /// media vuelta y al cerrar filas tras una baja, de modo que nadie cruza la formación.
    /// </summary>
    public sealed class SquadCommand
    {
        private Vec3[] _local = Array.Empty<Vec3>();
        private int[] _slotOf = Array.Empty<int>();

        public SquadCommand(FormationType formation, IReadOnlyList<Vec3> soldiers, Vec3 anchor, Vec3 facing)
        {
            if (soldiers == null) throw new ArgumentNullException(nameof(soldiers));
            Formation = formation;
            March = new SquadMarch(anchor, facing);
            Rebuild(soldiers);
        }

        public FormationType Formation { get; private set; }
        public SquadMarch March { get; }
        public int Count => _slotOf.Length;

        /// <summary>Puesto del soldado <paramref name="soldier"/> en el mundo (altura del ancla).</summary>
        public Vec3 SlotOf(int soldier) => Tactics.Formation.ToWorld(March.Anchor, March.Facing, _local[_slotOf[soldier]]);

        /// <summary>Puesto final (con la escuadra ya en su destino y orientada).</summary>
        public Vec3 LocalSlotOf(int soldier) => _local[_slotOf[soldier]];

        public void SetFormation(FormationType formation, IReadOnlyList<Vec3> soldiers)
        {
            Formation = formation;
            Rebuild(soldiers);
        }

        public void MoveTo(IReadOnlyList<Vec3> path, Vec3 facing, IReadOnlyList<Vec3> soldiers)
        {
            bool aboutFace = March.Order(path, facing);
            if (aboutFace) Rebuild(soldiers);
        }

        public void Halt() => March.Halt();

        /// <summary>
        /// Baja: el soldado <paramref name="soldier"/> sale de la escuadra y los demás cierran filas.
        /// <paramref name="remaining"/> son las posiciones de los que quedan, en su nuevo orden.
        /// </summary>
        public void RemoveSoldier(IReadOnlyList<Vec3> remaining) => Rebuild(remaining);

        /// <summary>Distancia horizontal del soldado más alejado de su puesto.</summary>
        public float MaxLag(IReadOnlyList<Vec3> soldiers)
        {
            float worst = 0f;
            for (int i = 0; i < soldiers.Count && i < Count; i++)
            {
                Vec3 d = SlotOf(i) - soldiers[i];
                worst = Math.Max(worst, new Vec3(d.X, 0f, d.Z).Magnitude);
            }
            return worst;
        }

        public void Step(float dt, IReadOnlyList<Vec3> soldiers)
        {
            March.Step(dt, MaxLag(soldiers));
            if (!March.Reformed) return;
            March.ClearReformed();
            Rebuild(soldiers);
        }

        /// <summary>
        /// Velocidad con la que un soldado persigue su puesto: el paso de la escuadra más una corrección
        /// proporcional al retraso, hasta la carrera.
        /// </summary>
        public float FollowSpeed(float distanceToSlot)
        {
            // Bajo fuego nadie corre erguido: la carrera se limita con el mismo factor que la marcha (mínimo, arrastrarse).
            float run = Math.Max(SquadMarch.RunSpeedMps, SquadMarch.MarchSpeedMps * Math.Max(1f, March.Pace) * 1.3f);
            float cap = run * Math.Max(0.35f, MathUtil.Clamp01(March.SpeedFactor));
            return MathUtil.Clamp(March.Speed + distanceToSlot * 1.5f, 0f, cap);
        }

        private void Rebuild(IReadOnlyList<Vec3> soldiers)
        {
            _local = Tactics.Formation.LocalSlots(Formation, soldiers.Count);
            March.Frontage = Tactics.Formation.Frontage(Formation, soldiers.Count);
            var world = new Vec3[_local.Length];
            for (int i = 0; i < world.Length; i++) world[i] = Tactics.Formation.ToWorld(March.Anchor, March.Facing, _local[i]);
            _slotOf = SlotAssignment.Solve(soldiers, world);
        }
    }
}
