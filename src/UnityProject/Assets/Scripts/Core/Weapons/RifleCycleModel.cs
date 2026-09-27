using System;
using System.Collections.Generic;

namespace Pacifico.Core.Weapons
{
    public enum RifleEventType
    {
        /// <summary>Empieza una etapa: la animación debe durar exactamente <see cref="RifleEvent.Duration"/>.</summary>
        StageStarted = 0,
        /// <summary>Sale despedida la vaina vacía.</summary>
        CaseEjected = 1,
        /// <summary>Un cartucho queda en la recámara: el arma podrá disparar al terminar el ciclo.</summary>
        CartridgeChambered = 2,
        /// <summary>Un cartucho entra en el depósito tubular.</summary>
        CartridgeLoaded = 3,
        /// <summary>Fin del ciclo: arma lista (o vacía si no quedaba munición).</summary>
        CycleCompleted = 4,
        /// <summary>No quedan cartuchos para recargar.</summary>
        OutOfAmmo = 5,
    }

    public struct RifleEvent
    {
        public RifleEventType Type;
        public RifleStage Stage;
        /// <summary>Duración de la etapa (solo en <see cref="RifleEventType.StageStarted"/>).</summary>
        public float Duration;
        /// <summary>Segundos transcurridos dentro del paso en que ocurrió (permite ordenar sonidos y efectos).</summary>
        public float TimeOffset;

        public override string ToString() => Type + "(" + Stage + ", " + Duration.ToString("0.000") + " s, +" + TimeOffset.ToString("0.000") + ")";
    }

    public enum TriggerResult
    {
        Fired = 0,
        /// <summary>Recámara vacía: «clic» del percutor.</summary>
        DryFire = 1,
        /// <summary>El arma está en mitad de un ciclo.</summary>
        Busy = 2,
    }

    /// <summary>
    /// Ciclo de disparo y recarga de un arma de 1879 (ROADMAP 3.2): percutir → [amartillar] → abrir el cierre →
    /// [expulsar la vaina] → insertar el cartucho → cerrar → encarar. Las etapas dependen del mecanismo real
    /// (<see cref="RifleCycleProfile"/>) y suman exactamente el tiempo de recarga de la ficha.
    /// <para>
    /// Sincronía con las animaciones: cada etapa se anuncia con su duración exacta (<see cref="RifleEventType.StageStarted"/>)
    /// y expone su progreso normalizado; una animación que se reproduzca a <c>duración_clip / Duration</c> o que se
    /// muestree con <see cref="StageProgress"/> termina a la vez que la etapa. Si un fotograma largo abarca varias
    /// etapas, el tiempo sobrante pasa a la siguiente y todos los eventos se emiten en orden, con su desfase.
    /// </para>
    /// </summary>
    public sealed class RifleCycleModel
    {
        private readonly List<RifleEvent> _events = new List<RifleEvent>();
        private readonly List<StageSpec> _sequence = new List<StageSpec>();
        private int _index;

        public RifleCycleModel(WeaponSpec weapon, int reserve, bool startChambered = true, int startMagazine = -1)
        {
            Profile = RifleCycleProfile.For(weapon);
            Reserve = Math.Max(0, reserve);
            Chambered = startChambered;
            if (Profile.UsesMagazine)
            {
                MagazineCapacity = weapon.MagazineCapacity;
                MagazineRounds = startMagazine < 0 ? MagazineCapacity : Math.Min(startMagazine, MagazineCapacity);
            }
        }

        public RifleCycleProfile Profile { get; }
        public WeaponSpec Weapon => Profile.Weapon;

        /// <summary>Recarga automática tras cada disparo (los fusiles monotiro siempre la necesitan).</summary>
        public bool AutoReload { get; set; } = true;

        public RifleStage Stage { get; private set; } = RifleStage.Ready;
        public float StageElapsed { get; private set; }
        public float StageDuration { get; private set; }
        public float StageProgress => StageDuration > 0f ? Math.Min(1f, StageElapsed / StageDuration) : 1f;
        public bool IsReady => Stage == RifleStage.Ready;

        public bool Chambered { get; private set; }
        /// <summary>Martillo o percutor montado. Cae al disparar; lo monta el cierre al abrirse o el tirador a mano.</summary>
        public bool IsCocked { get; private set; } = true;
        /// <summary>Hay una vaina disparada en la recámara (debe extraerse antes de cargar).</summary>
        public bool SpentCaseInChamber { get; private set; }
        public int MagazineRounds { get; private set; }
        public int MagazineCapacity { get; }
        /// <summary>Cartuchos en las cartucheras.</summary>
        public int Reserve { get; private set; }

        public int TotalRounds => (Chambered ? 1 : 0) + MagazineRounds + Reserve;

        /// <summary>
        /// El fusil está bajado para manipularlo y no se puede encarar. No lo está al percutir, al volver a encarar ni
        /// al cargar el depósito; tampoco durante el ciclo de palanca de la Winchester, que se acciona sin dejar de
        /// apuntar (su gran ventaja en el fuego rápido).
        /// </summary>
        public bool BlocksAiming
        {
            get
            {
                switch (Stage)
                {
                    case RifleStage.Ready:
                    case RifleStage.Firing:
                    case RifleStage.Shouldering:
                    case RifleStage.LoadingMagazine:
                        return false;
                    case RifleStage.OpeningAction:
                    case RifleStage.ClosingAction:
                        return !Profile.UsesMagazine;
                    default:
                        return true;
                }
            }
        }

        /// <summary>Se puede disparar: cartucho en la recámara y arma lista (o cargando el depósito, que se interrumpe).</summary>
        public bool CanFire => Chambered && (Stage == RifleStage.Ready || Stage == RifleStage.LoadingMagazine);

        public bool CanReload
        {
            get
            {
                if (Stage != RifleStage.Ready || Reserve <= 0) return false;
                return Profile.UsesMagazine ? MagazineRounds < MagazineCapacity : !Chambered;
            }
        }

        /// <summary>Eventos pendientes que se entregarán en el próximo <see cref="Step"/> (los genera el disparo).</summary>
        private readonly List<RifleEvent> _pending = new List<RifleEvent>();

        /// <summary>Aprieta el gatillo. Si dispara, el ciclo arranca y sus eventos llegan en el próximo <see cref="Step"/>.</summary>
        public TriggerResult PullTrigger()
        {
            if (Stage != RifleStage.Ready && Stage != RifleStage.LoadingMagazine) return TriggerResult.Busy;
            if (!Chambered) return TriggerResult.DryFire;

            // Disparar mientras se carga el depósito interrumpe la carga (el cartucho a medio meter no cuenta).
            Chambered = false;
            IsCocked = false;
            SpentCaseInChamber = Profile.EjectsCase;

            _sequence.Clear();
            foreach (StageSpec s in Profile.FireCycle)
            {
                // Sin munición no hay recarga: tras percutir, el arma queda vacía con la vaina dentro.
                if (s.Stage != RifleStage.Firing && !Profile.UsesMagazine && (!AutoReload || Reserve <= 0)) break;
                _sequence.Add(s);
            }
            Begin(0f, _pending);
            return TriggerResult.Fired;
        }

        /// <summary>Recarga manual (tecla R). Devuelve false si no procede.</summary>
        public bool Reload()
        {
            if (!CanReload) return false;
            _sequence.Clear();
            if (Profile.UsesMagazine)
            {
                int rounds = Math.Min(MagazineCapacity - MagazineRounds, Reserve);
                for (int i = 0; i < rounds; i++) _sequence.Add(new StageSpec(RifleStage.LoadingMagazine, Weapon.ReloadSeconds));
                if (!Chambered)
                {
                    // Con la recámara vacía hay que accionar la palanca para alimentar desde el depósito.
                    _sequence.Add(new StageSpec(RifleStage.OpeningAction, RifleCycleProfile.LeverOpenSeconds));
                    _sequence.Add(new StageSpec(RifleStage.ClosingAction, RifleCycleProfile.LeverCloseSeconds));
                    _sequence.Add(new StageSpec(RifleStage.Shouldering, RifleCycleProfile.LeverShoulderSeconds));
                }
            }
            else
            {
                _sequence.AddRange(Profile.ReloadCycle(SpentCaseInChamber));
            }
            Begin(0f, _pending);
            return true;
        }

        /// <summary>Recoge cartuchos (p. ej. de un caído en Tarapacá). Devuelve cuántos se aceptaron.</summary>
        public int AddAmmo(int rounds)
        {
            if (rounds <= 0) return 0;
            Reserve += rounds;
            return rounds;
        }

        /// <summary>
        /// Avanza el ciclo. <paramref name="paused"/> congela la manipulación (correr o deslizarse con el fusil).
        /// Devuelve los eventos ocurridos, en orden; la lista se reutiliza en la siguiente llamada.
        /// </summary>
        public IReadOnlyList<RifleEvent> Step(float dt, bool paused = false)
        {
            _events.Clear();
            _events.AddRange(_pending);
            _pending.Clear();
            if (dt <= 0f || paused || Stage == RifleStage.Ready) return _events;

            float offset = 0f;
            float remaining = dt;
            while (remaining > 0f && Stage != RifleStage.Ready)
            {
                float left = StageDuration - StageElapsed;
                if (remaining < left)
                {
                    StageElapsed += remaining;
                    break;
                }
                remaining -= left;
                offset += left;
                StageElapsed = StageDuration;
                CompleteStage(offset);
            }
            return _events;
        }

        // ------------------------------------------------------------------------------------------

        private void Begin(float offset, List<RifleEvent> sink)
        {
            _index = 0;
            if (_sequence.Count == 0)
            {
                Finish(offset, sink);
                return;
            }
            Enter(_sequence[0], offset, sink);
        }

        private void Enter(StageSpec spec, float offset, List<RifleEvent> sink)
        {
            Stage = spec.Stage;
            StageDuration = spec.Seconds;
            StageElapsed = 0f;
            sink.Add(new RifleEvent { Type = RifleEventType.StageStarted, Stage = spec.Stage, Duration = spec.Seconds, TimeOffset = offset });
        }

        private void CompleteStage(float offset)
        {
            RifleStage done = Stage;
            if (done == RifleStage.Cocking || (done == RifleStage.OpeningAction && !Profile.NeedsManualCocking)) IsCocked = true;
            switch (done)
            {
                case RifleStage.Extracting:
                    EjectCase(offset);
                    break;
                case RifleStage.OpeningAction when Profile.UsesMagazine:
                    // La palanca de la Winchester extrae y expulsa al bajar.
                    if (SpentCaseInChamber) EjectCase(offset);
                    break;
                case RifleStage.InsertingCartridge:
                    Reserve--;
                    Chambered = true;
                    Emit(RifleEventType.CartridgeChambered, done, offset);
                    break;
                case RifleStage.ClosingAction when Profile.UsesMagazine:
                    if (MagazineRounds > 0)
                    {
                        MagazineRounds--;
                        Chambered = true;
                        Emit(RifleEventType.CartridgeChambered, done, offset);
                    }
                    break;
                case RifleStage.LoadingMagazine:
                    Reserve--;
                    MagazineRounds++;
                    Emit(RifleEventType.CartridgeLoaded, done, offset);
                    break;
            }

            _index++;
            if (_index < _sequence.Count) Enter(_sequence[_index], offset, _events);
            else Finish(offset, _events);
        }

        private void EjectCase(float offset)
        {
            SpentCaseInChamber = false;
            Emit(RifleEventType.CaseEjected, Stage, offset);
        }

        private void Finish(float offset, List<RifleEvent> sink)
        {
            Stage = RifleStage.Ready;
            StageElapsed = 0f;
            StageDuration = 0f;
            _sequence.Clear();
            sink.Add(new RifleEvent { Type = RifleEventType.CycleCompleted, Stage = RifleStage.Ready, TimeOffset = offset });
            if (!Chambered && MagazineRounds == 0 && Reserve == 0)
            {
                sink.Add(new RifleEvent { Type = RifleEventType.OutOfAmmo, Stage = RifleStage.Ready, TimeOffset = offset });
            }
        }

        private void Emit(RifleEventType type, RifleStage stage, float offset)
        {
            _events.Add(new RifleEvent { Type = type, Stage = stage, TimeOffset = offset });
        }
    }
}
