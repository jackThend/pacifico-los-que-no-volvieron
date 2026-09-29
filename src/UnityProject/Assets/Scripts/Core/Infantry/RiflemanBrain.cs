using System;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Core.Weapons;

namespace Pacifico.Core.Infantry
{
    public enum RiflemanState
    {
        /// <summary>Camina hacia su objetivo de maniobra (bajar la ladera, entrar en el pueblo).</summary>
        Advance,
        /// <summary>Tiene un enemigo a la vista y al alcance: se para, apunta y dispara.</summary>
        Engage,
        /// <summary>Carga el fusil (de rodillas si puede).</summary>
        Reload,
        /// <summary>Carrera a la bayoneta contra el enemigo.</summary>
        Charge,
        /// <summary>Al alcance del arma blanca: golpea.</summary>
        Melee,
        Down,
    }

    /// <summary>Lo que percibe el fusilero en este instante (lo rellena la escena).</summary>
    public struct RiflemanPerception
    {
        public bool HasTarget;
        public float TargetDistanceM;
        public bool LineOfSight;
        public bool TargetMoving;
        /// <summary>El fusil tiene cartucho en la recámara.</summary>
        public bool Loaded;
        /// <summary>Quedan cartuchos para cargar.</summary>
        public bool HasAmmo;
        /// <summary>Su jefe ordena cargar a la bayoneta.</summary>
        public bool ChargeOrdered;
        /// <summary>Debe quedarse en su puesto (sirvientes de una pieza, defensores de una tapia).</summary>
        public bool HoldPosition;
        /// <summary>Supresión recibida (0–1, <see cref="SuppressionModel"/>).</summary>
        public float Suppression;
    }

    public struct RiflemanDecision
    {
        public RiflemanState State;
        /// <summary>Disparar en este paso.</summary>
        public bool Fire;
        /// <summary>Golpear en este paso.</summary>
        public bool Strike;
        /// <summary>Hacia dónde moverse: su objetivo de maniobra o el enemigo.</summary>
        public bool MoveToObjective;
        public bool MoveToTarget;
        public bool Kneel;
    }

    /// <summary>
    /// Decisiones de un fusilero de la IA en primera persona (capítulos FPS): avanzar, apuntar y disparar con la
    /// cadencia real de su fusil, cargar, cargar a la bayoneta y golpear. Determinista con su semilla y probada sin el
    /// motor; la escena solo percibe (distancias, línea de visión) y ejecuta (NavMesh, balas, golpes).
    /// </summary>
    public sealed class RiflemanBrain
    {
        /// <summary>Tiempo de reacción al ver a un enemigo nuevo (s).</summary>
        public const float ReactionSeconds = 0.6f;
        /// <summary>Por debajo de esta distancia, un soldado con la bayoneta calada prefiere cargar a recargar.</summary>
        public const float ChargeWithinM = 18f;
        public const float StrikeIntervalSeconds = 1.1f;

        private readonly Random _random;
        private readonly WeaponSpec _weapon;
        private float _aimTimer;
        private float _aimNeeded;
        private float _strikeCooldown;
        private bool _sawTarget;

        public RiflemanBrain(WeaponSpec weapon, int seed)
        {
            _weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            _random = new Random(seed);
            _aimNeeded = NextAimTime();
        }

        public RiflemanState State { get; private set; } = RiflemanState.Advance;
        /// <summary>
        /// Alcance al que se para a hacer fuego (m): por defecto el eficaz de su fusil, sin pasar de 400. Las tropas de
        /// asalto lo tienen corto: bajan la ladera y entran en el pueblo antes de detenerse a tirar.
        /// </summary>
        public float EngageRangeM
        {
            get => _engageRangeM > 0f ? _engageRangeM : Math.Min(400f, _weapon.EffectiveRangeM);
            set => _engageRangeM = value;
        }

        private float _engageRangeM;
        public float MeleeReachM { get; set; } = 1.9f;
        public float AimProgress => _aimNeeded > 0f ? MathUtil.Clamp01(_aimTimer / _aimNeeded) : 1f;

        public void Kill() => State = RiflemanState.Down;

        public RiflemanDecision Step(float dt, RiflemanPerception p)
        {
            var d = new RiflemanDecision();
            if (State == RiflemanState.Down)
            {
                d.State = State;
                return d;
            }
            _strikeCooldown = Math.Max(0f, _strikeCooldown - dt);

            bool visible = p.HasTarget && p.LineOfSight;
            if (!visible)
            {
                _sawTarget = false;
                _aimTimer = 0f;
            }

            if (visible && p.TargetDistanceM <= MeleeReachM)
            {
                State = RiflemanState.Melee;
                if (_strikeCooldown <= 0f)
                {
                    d.Strike = true;
                    _strikeCooldown = StrikeIntervalSeconds;
                }
            }
            else if (visible && (p.ChargeOrdered || (!p.Loaded && p.TargetDistanceM <= ChargeWithinM)) && !p.HoldPosition)
            {
                State = RiflemanState.Charge;
                d.MoveToTarget = true;
            }
            else if (!p.Loaded)
            {
                State = p.HasAmmo ? RiflemanState.Reload : visible && !p.HoldPosition ? RiflemanState.Charge : RiflemanState.Advance;
                d.MoveToTarget = State == RiflemanState.Charge;
                d.MoveToObjective = State == RiflemanState.Advance && !p.HoldPosition;
                d.Kneel = State == RiflemanState.Reload;
            }
            else if (visible && p.TargetDistanceM <= EngageRangeM)
            {
                State = RiflemanState.Engage;
                d.Kneel = p.Suppression > SuppressionModel.PinEnter;
                if (!_sawTarget)
                {
                    // Enemigo nuevo: reacción antes de empezar a apuntar.
                    _sawTarget = true;
                    _aimTimer = -ReactionSeconds;
                }
                // La supresión hace apuntar peor y más despacio (SuppressionModel: presionado → la mitad de ritmo).
                _aimTimer += dt * (1f - 0.5f * MathUtil.Clamp01(p.Suppression));
                if (_aimTimer >= _aimNeeded)
                {
                    d.Fire = true;
                    _aimTimer = 0f;
                    _aimNeeded = NextAimTime();
                }
            }
            else
            {
                State = RiflemanState.Advance;
                d.MoveToObjective = !p.HoldPosition;
                d.MoveToTarget = p.HoldPosition ? false : p.HasTarget && !p.LineOfSight && p.TargetDistanceM < EngageRangeM;
                if (d.MoveToTarget) d.MoveToObjective = false;
            }
            d.State = State;
            return d;
        }

        private float NextAimTime() => FireModel.AimSeconds + (float)_random.NextDouble() * FireModel.AimJitterSeconds;

        /// <summary>
        /// Desviación típica angular (°) del disparo de la IA: la de combate de su fusil (<see cref="FireModel"/>),
        /// peor si el blanco corre y mucho peor suprimido.
        /// </summary>
        public static float SigmaDeg(WeaponSpec weapon, bool targetMoving, float suppression)
        {
            float sigma = FireModel.SigmaDeg(weapon);
            if (targetMoving) sigma *= 1.4f;
            return sigma * (1f + 1.5f * MathUtil.Clamp01(suppression));
        }

        /// <summary>Error angular del disparo (cabeceo, guiñada en grados), gaussiano con la desviación dada.</summary>
        public ShotDirection SampleShot(float sigmaDeg) =>
            new ShotDirection { PitchDeg = RifleShotSolver.Gaussian(_random) * sigmaDeg, YawDeg = RifleShotSolver.Gaussian(_random) * sigmaDeg };
    }
}
