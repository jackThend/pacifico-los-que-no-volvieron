using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Pacifico.Core.Campaign
{
    // ==============================================================================================
    // Hechos: la pizarra que la escena alimenta y los guiones consultan
    // ==============================================================================================

    /// <summary>
    /// Hechos de una misión: contadores («andanadas disparadas», «náufragos rescatados») y marcas («Esmeralda
    /// hundida»). La escena solo escribe hechos; qué significan lo decide el guion de la misión. Así toda la lógica
    /// narrativa se prueba sin el motor.
    /// </summary>
    public sealed class MissionFacts
    {
        private readonly Dictionary<string, float> _numbers = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

        public float Get(string key) => _numbers.TryGetValue(key, out float v) ? v : 0f;
        public void Set(string key, float value) => _numbers[key] = value;
        public float Add(string key, float delta = 1f) => _numbers[key] = Get(key) + delta;
        public bool Flag(string key) => _flags.Contains(key);

        public void SetFlag(string key, bool value = true)
        {
            if (value) _flags.Add(key);
            else _flags.Remove(key);
        }

        public IEnumerable<string> Flags => _flags;
        public IEnumerable<KeyValuePair<string, float>> Numbers => _numbers;
    }

    /// <summary>Lo que ve una condición: hechos, reloj y estado del diálogo.</summary>
    public sealed class MissionContext
    {
        public MissionFacts Facts { get; internal set; } = new MissionFacts();
        public float MissionTime { get; internal set; }
        public float StageTime { get; internal set; }
        public bool DialogueIdle { get; internal set; } = true;
    }

    // ==============================================================================================
    // Condiciones
    // ==============================================================================================

    /// <summary>Condición declarativa de un guion de misión. Las fábricas estáticas cubren los casos del juego.</summary>
    public sealed class MissionCondition
    {
        private readonly Func<MissionContext, bool> _test;

        private MissionCondition(Func<MissionContext, bool> test, string description)
        {
            _test = test;
            Description = description;
        }

        public string Description { get; }

        public bool IsMet(MissionContext context) => _test(context);

        public override string ToString() => Description;

        public static MissionCondition AtLeast(string fact, float value) =>
            new MissionCondition(c => c.Facts.Get(fact) >= value, fact + " ≥ " + value.ToString(CultureInfo.InvariantCulture));

        public static MissionCondition Flag(string flag) => new MissionCondition(c => c.Facts.Flag(flag), flag);
        public static MissionCondition NotFlag(string flag) => new MissionCondition(c => !c.Facts.Flag(flag), "no " + flag);

        public static MissionCondition StageTime(float seconds) =>
            new MissionCondition(c => c.StageTime >= seconds, "etapa ≥ " + seconds.ToString(CultureInfo.InvariantCulture) + " s");

        /// <summary>No queda ninguna línea de diálogo por decir (para cambiar de escena sin cortar a nadie).</summary>
        public static MissionCondition DialogueIdle() => new MissionCondition(c => c.DialogueIdle, "diálogo terminado");

        public static MissionCondition Not(MissionCondition condition) =>
            new MissionCondition(c => !condition.IsMet(c), "no " + condition.Description);

        public static MissionCondition All(params MissionCondition[] all) =>
            new MissionCondition(c => all.All(x => x.IsMet(c)), "(" + string.Join(" y ", all.Select(x => x.Description)) + ")");

        public static MissionCondition Any(params MissionCondition[] any) =>
            new MissionCondition(c => any.Any(x => x.IsMet(c)), "(" + string.Join(" o ", any.Select(x => x.Description)) + ")");
    }

    // ==============================================================================================
    // Guion: líneas, objetivos, reacciones, etapas
    // ==============================================================================================

    public enum LineKind
    {
        /// <summary>Cita literal del guion (y de la historia): se muestra entre comillas latinas.</summary>
        Quote,
        /// <summary>Acotación narrativa: lo que el jugador ve suceder.</summary>
        Narration,
        /// <summary>Indicación de juego (controles, qué hacer).</summary>
        Hint,
    }

    /// <summary>Una línea de diálogo o de narración; su duración se calcula para poder leerse.</summary>
    public sealed class MissionLine
    {
        public const float CharactersPerSecond = 15f;
        public const float MinSeconds = 2.5f;
        public const float PauseSeconds = 0.6f;

        public MissionLine(string speaker, string text, LineKind kind = LineKind.Quote)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Kind = kind;
        }

        public string Speaker { get; }
        public string Text { get; }
        public LineKind Kind { get; }

        /// <summary>Tiempo en pantalla: a 15 caracteres por segundo (norma de subtítulos), nunca menos de 2,5 s.</summary>
        public float Seconds => Math.Max(MinSeconds, Text.Length / CharactersPerSecond) + PauseSeconds;

        public override string ToString() => Speaker.Length > 0 ? Speaker + ": " + Text : Text;
    }

    public sealed class MissionObjective
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public MissionCondition Complete { get; set; }
        /// <summary>Hecho que mide el progreso (opcional) y valor que lo completa, para mostrar «2/3».</summary>
        public string ProgressFact { get; set; }
        public float ProgressTarget { get; set; }
        public bool Optional { get; set; }
    }

    /// <summary>Reacción dentro de una etapa: cuando se cumple la condición, se dicen unas líneas (una sola vez).</summary>
    public sealed class MissionTrigger
    {
        public string Id { get; set; } = string.Empty;
        public MissionCondition When { get; set; }
        public List<MissionLine> Lines { get; } = new List<MissionLine>();
        /// <summary>Marca que se pone al dispararse (para que el guion o la escena reaccionen).</summary>
        public string SetsFlag { get; set; }
        /// <summary>Si es cierto, interrumpe el diálogo en curso (una orden urgente no espera turno).</summary>
        public bool Interrupts { get; set; }
    }

    public sealed class MissionTransition
    {
        public MissionTransition(MissionCondition when, string next)
        {
            When = when ?? throw new ArgumentNullException(nameof(when));
            Next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public MissionCondition When { get; }
        /// <summary>Etapa siguiente, o <see cref="MissionScript.CompleteStage"/> para terminar la misión.</summary>
        public string Next { get; }
    }

    public sealed class MissionStage
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Quién es el jugador y qué controla (la escena lo traduce a cámara y mandos).</summary>
        public string Perspective { get; set; } = string.Empty;
        public List<MissionLine> OnEnter { get; } = new List<MissionLine>();
        public List<MissionObjective> Objectives { get; } = new List<MissionObjective>();
        public List<MissionTrigger> Triggers { get; } = new List<MissionTrigger>();
        /// <summary>Se evalúan en orden; gana la primera que se cumple.</summary>
        public List<MissionTransition> Transitions { get; } = new List<MissionTransition>();
    }

    public sealed class MissionFailure
    {
        public MissionFailure(MissionCondition when, string reason)
        {
            When = when;
            Reason = reason;
        }

        public MissionCondition When { get; }
        public string Reason { get; }
    }

    public sealed class MissionScript
    {
        public const string CompleteStage = "#fin";

        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<MissionStage> Stages { get; } = new List<MissionStage>();
        public List<MissionFailure> Failures { get; } = new List<MissionFailure>();
        /// <summary>Coleccionable que se desbloquea al completar la misión (p. ej. la carta de Grau).</summary>
        public string RewardCollectibleId { get; set; }

        public MissionStage Stage(string id) => Stages.FirstOrDefault(s => s.Id == id);

        /// <summary>Comprueba la coherencia del guion: ids únicos y transiciones a etapas existentes.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Stages.Count == 0) errors.Add("La misión no tiene etapas.");
            foreach (var group in Stages.GroupBy(s => s.Id).Where(g => g.Count() > 1)) errors.Add("Etapa repetida: " + group.Key);
            foreach (MissionStage stage in Stages)
            {
                foreach (MissionTransition t in stage.Transitions)
                {
                    if (t.Next != CompleteStage && Stage(t.Next) == null) errors.Add(stage.Id + " → etapa inexistente " + t.Next);
                }
                foreach (var group in stage.Objectives.GroupBy(o => o.Id).Where(g => g.Count() > 1)) errors.Add(stage.Id + ": objetivo repetido " + group.Key);
            }
            // Toda etapa debe poder llegar al final.
            var reach = new HashSet<string> { CompleteStage };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (MissionStage stage in Stages)
                {
                    if (!reach.Contains(stage.Id) && stage.Transitions.Any(t => reach.Contains(t.Next)))
                    {
                        reach.Add(stage.Id);
                        changed = true;
                    }
                }
            }
            foreach (MissionStage stage in Stages.Where(s => !reach.Contains(s.Id))) errors.Add(stage.Id + " no lleva al final de la misión");
            return errors;
        }
    }

    // ==============================================================================================
    // Diálogo
    // ==============================================================================================

    /// <summary>Cola de líneas: una en pantalla cada vez, en orden, cada una el tiempo necesario para leerla.</summary>
    public sealed class DialogueQueue
    {
        private readonly Queue<MissionLine> _pending = new Queue<MissionLine>();

        public MissionLine Current { get; private set; }
        public float CurrentElapsed { get; private set; }
        public bool IsIdle => Current == null && _pending.Count == 0;
        public int PendingCount => _pending.Count;

        /// <summary>Línea que empieza (para el registro y el audio).</summary>
        public event Action<MissionLine> LineStarted;

        public void Enqueue(MissionLine line)
        {
            _pending.Enqueue(line ?? throw new ArgumentNullException(nameof(line)));
            if (Current == null) Advance();
        }

        /// <summary>Descarta lo pendiente y dice <paramref name="line"/> ya.</summary>
        public void Interrupt(MissionLine line)
        {
            _pending.Clear();
            Current = null;
            Enqueue(line);
        }

        public void Step(float dt)
        {
            if (Current == null) return;
            CurrentElapsed += dt;
            // Un paso largo puede consumir varias líneas cortas: se reparte el tiempo sobrante.
            while (Current != null && CurrentElapsed >= Current.Seconds)
            {
                float extra = CurrentElapsed - Current.Seconds;
                Current = null;
                Advance();
                if (Current != null) CurrentElapsed = extra;
            }
        }

        public void Clear()
        {
            _pending.Clear();
            Current = null;
            CurrentElapsed = 0f;
        }

        private void Advance()
        {
            CurrentElapsed = 0f;
            if (_pending.Count == 0) return;
            Current = _pending.Dequeue();
            LineStarted?.Invoke(Current);
        }
    }

    // ==============================================================================================
    // Ejecución
    // ==============================================================================================

    public enum MissionState
    {
        Running,
        Complete,
        Failed,
    }

    public enum MissionEventKind
    {
        StageStarted,
        ObjectiveCompleted,
        TriggerFired,
        LineStarted,
        MissionComplete,
        MissionFailed,
    }

    public struct MissionEvent
    {
        public MissionEventKind Kind;
        public float Time;
        public string StageId;
        /// <summary>Objetivo, reacción o motivo del fracaso, según el tipo.</summary>
        public string Detail;
        public MissionLine Line;

        public override string ToString() =>
            Time.ToString("0.0", CultureInfo.InvariantCulture) + " s  " + Kind + "  " + StageId + (Detail != null ? "  " + Detail : string.Empty) +
            (Line != null ? "  «" + Line + "»" : string.Empty);
    }

    /// <summary>
    /// Ejecuta un <see cref="MissionScript"/>: una máquina de estados determinista que, en cada paso, dice las
    /// reacciones que tocan, marca los objetivos cumplidos, comprueba los fracasos y avanza de etapa. La escena
    /// escribe hechos en <see cref="Facts"/> y escucha <see cref="Event"/> para cambiar de cámara, de mandos o
    /// mostrar documentos.
    /// </summary>
    public sealed class MissionRunner
    {
        /// <summary>Tope de cambios de etapa en un mismo paso (protege de guiones con ciclos instantáneos).</summary>
        public const int MaxStagesPerStep = 8;

        private readonly MissionContext _context = new MissionContext();
        private readonly HashSet<string> _completedObjectives = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _firedTriggers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<MissionEvent> _history = new List<MissionEvent>();
        private readonly List<string> _visited = new List<string>();

        public MissionRunner(MissionScript script)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            List<string> errors = script.Validate();
            if (errors.Count > 0) throw new ArgumentException("Guion de misión inválido: " + string.Join("; ", errors), nameof(script));
            _context.Facts = Facts;
            Dialogue.LineStarted += line => Emit(MissionEventKind.LineStarted, null, line);
            Enter(script.Stages[0]);
        }

        public MissionScript Script { get; }
        public MissionFacts Facts { get; } = new MissionFacts();
        public DialogueQueue Dialogue { get; } = new DialogueQueue();
        public MissionStage CurrentStage { get; private set; }
        public MissionState State { get; private set; } = MissionState.Running;
        public string FailureReason { get; private set; }
        public float MissionTime => _context.MissionTime;
        public float StageTime => _context.StageTime;
        public IReadOnlyList<MissionEvent> History => _history;
        /// <summary>Etapas recorridas, en orden.</summary>
        public IReadOnlyList<string> Visited => _visited;

        public event Action<MissionEvent> Event;

        public bool IsObjectiveComplete(MissionObjective objective) => _completedObjectives.Contains(Key(objective));

        /// <summary>Progreso de 0 a 1 de un objetivo (1 si está cumplido).</summary>
        public float Progress(MissionObjective objective)
        {
            if (IsObjectiveComplete(objective)) return 1f;
            if (objective.ProgressFact == null || objective.ProgressTarget <= 0f) return 0f;
            return Math.Min(1f, Math.Max(0f, Facts.Get(objective.ProgressFact) / objective.ProgressTarget));
        }

        /// <summary>«Dispara tres andanadas (2/3)».</summary>
        public string Describe(MissionObjective objective)
        {
            if (objective.ProgressFact == null || objective.ProgressTarget <= 1f) return objective.Text;
            float value = IsObjectiveComplete(objective) ? objective.ProgressTarget : Math.Min(objective.ProgressTarget, Facts.Get(objective.ProgressFact));
            return objective.Text + " (" + value.ToString("0", CultureInfo.InvariantCulture) + "/" +
                   objective.ProgressTarget.ToString("0", CultureInfo.InvariantCulture) + ")";
        }

        public void Step(float dt)
        {
            if (State != MissionState.Running) return;
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            _context.MissionTime += dt;
            _context.StageTime += dt;
            Dialogue.Step(dt);
            Evaluate();
        }

        /// <summary>Evalúa sin avanzar el reloj (tras escribir hechos, para reaccionar en el mismo fotograma).</summary>
        public void Evaluate()
        {
            for (int hops = 0; hops < MaxStagesPerStep && State == MissionState.Running; hops++)
            {
                _context.DialogueIdle = Dialogue.IsIdle;
                MissionStage stage = CurrentStage;

                foreach (MissionTrigger trigger in stage.Triggers)
                {
                    string key = stage.Id + "/" + trigger.Id;
                    if (_firedTriggers.Contains(key) || !trigger.When.IsMet(_context)) continue;
                    _firedTriggers.Add(key);
                    if (trigger.SetsFlag != null) Facts.SetFlag(trigger.SetsFlag);
                    Emit(MissionEventKind.TriggerFired, trigger.Id, null);
                    for (int i = 0; i < trigger.Lines.Count; i++)
                    {
                        if (i == 0 && trigger.Interrupts) Dialogue.Interrupt(trigger.Lines[i]);
                        else Dialogue.Enqueue(trigger.Lines[i]);
                    }
                }
                _context.DialogueIdle = Dialogue.IsIdle;

                foreach (MissionObjective objective in stage.Objectives)
                {
                    if (IsObjectiveComplete(objective) || objective.Complete == null || !objective.Complete.IsMet(_context)) continue;
                    _completedObjectives.Add(Key(objective));
                    Emit(MissionEventKind.ObjectiveCompleted, objective.Id, null);
                }

                foreach (MissionFailure failure in Script.Failures)
                {
                    if (!failure.When.IsMet(_context)) continue;
                    State = MissionState.Failed;
                    FailureReason = failure.Reason;
                    Emit(MissionEventKind.MissionFailed, failure.Reason, null);
                    return;
                }

                MissionTransition next = stage.Transitions.FirstOrDefault(t => t.When.IsMet(_context));
                if (next == null) return;
                if (next.Next == MissionScript.CompleteStage)
                {
                    State = MissionState.Complete;
                    Emit(MissionEventKind.MissionComplete, Script.RewardCollectibleId, null);
                    return;
                }
                Enter(Script.Stage(next.Next));
            }
        }

        private void Enter(MissionStage stage)
        {
            CurrentStage = stage;
            _context.StageTime = 0f;
            _visited.Add(stage.Id);
            Emit(MissionEventKind.StageStarted, stage.Title, null);
            foreach (MissionLine line in stage.OnEnter) Dialogue.Enqueue(line);
            _context.DialogueIdle = Dialogue.IsIdle;
        }

        private string Key(MissionObjective objective) => CurrentStageIdFor(objective) + "/" + objective.Id;

        private string CurrentStageIdFor(MissionObjective objective)
        {
            foreach (MissionStage stage in Script.Stages)
            {
                if (stage.Objectives.Contains(objective)) return stage.Id;
            }
            return string.Empty;
        }

        private void Emit(MissionEventKind kind, string detail, MissionLine line)
        {
            var e = new MissionEvent
            {
                Kind = kind,
                Time = _context.MissionTime,
                StageId = CurrentStage != null ? CurrentStage.Id : string.Empty,
                Detail = detail,
                Line = line,
            };
            _history.Add(e);
            Event?.Invoke(e);
        }
    }
}
