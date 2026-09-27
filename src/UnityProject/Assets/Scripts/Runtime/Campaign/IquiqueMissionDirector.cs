using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Naval;
using Pacifico.Data;
using Pacifico.Input;
using Pacifico.Narrative;
using Pacifico.Naval;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = Pacifico.Core.Campaign.IquiqueChapter.Facts;
using G = Pacifico.Core.Campaign.IquiqueChapter.Flags;
using P = Pacifico.Core.Campaign.IquiqueChapter.Perspectives;
using S = Pacifico.Core.Campaign.IquiqueChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 1 en escena (ROADMAP 6.1). La lógica narrativa es <see cref="IquiqueChapter"/>,
    /// probada fuera del motor; aquí se traducen los sucesos de la escena a hechos (andanadas, impactos,
    /// espolonazos, hundimientos, rescates) y las etapas a perspectiva: quién es el jugador, qué buque sigue la
    /// cámara, qué mandos tiene y qué hace la IA del otro bando. También dibuja objetivos y diálogos, echa a
    /// pique la Esmeralda tras el tercer espolonazo, reparte los náufragos y abre la carta de Grau al final.
    /// </summary>
    public sealed class IquiqueMissionDirector : MonoBehaviour
    {
        [Header("Guion (sección del capítulo 1, copiada por el constructor de la escena)")]
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;
        [SerializeField] private int survivorGroups = IquiqueChapter.DefaultSurvivorGroups;

        [Header("Esmeralda")]
        [SerializeField] private ShipController esmeralda;
        [SerializeField] private PlayerBroadsideGunner gunner;
        [SerializeField] private BroadsideShipAI esmeraldaAI;
        [Tooltip("Centro de la rada: la Esmeralda se aparta de las baterías de tierra hacia aquí.")]
        [SerializeField] private Vector3 bayCenter = new Vector3(300f, 0f, 100f);
        [SerializeField] private float bayRadius = 350f;

        [Header("Huáscar")]
        [SerializeField] private ShipController huascar;
        [SerializeField] private RammingShipAI huascarAI;
        [SerializeField] private ColesTurretController turret;
        [SerializeField] private RamBow ram;

        [Header("Escena")]
        [SerializeField] private ShoreBattery[] shoreBatteries = new ShoreBattery[0];
        [SerializeField] private ShipCameraRig cameraRig;
        [SerializeField] private NavalHud hud;
        [Tooltip("Plantilla inactiva de un grupo de náufragos.")]
        [SerializeField] private SurvivorGroup survivorTemplate;
        [SerializeField] private DocumentViewer viewer;
        [SerializeField] private CollectibleDataSO letter;

        private const float FadeSeconds = 0.7f;
        /// <summary>Dos contactos seguidos del mismo choque no son dos espolonazos.</summary>
        private const float MinSecondsBetweenRams = 20f;

        private MissionRunner _runner;
        private ShipDamageController _esmeraldaDamage;
        private ShipDamageController _huascarDamage;
        private string _perspective;
        private string _pendingPerspective;
        private float _fade = -1f;
        private bool _foundered;
        private float _lastRamTime = float.NegativeInfinity;
        private bool _letterOpened;
        private bool _recorded;
        private bool _newCollectible;
        private readonly List<SurvivorGroup> _survivors = new List<SurvivorGroup>();

        private readonly MissionHud _hud = new MissionHud();

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, ShipController esmeraldaShip, PlayerBroadsideGunner esmeraldaGunner, BroadsideShipAI esmeraldaBrain,
                              ShipController huascarShip, RammingShipAI huascarBrain, ColesTurretController coles, RamBow bow,
                              ShoreBattery[] batteries, ShipCameraRig rig, NavalHud navalHud, SurvivorGroup template,
                              DocumentViewer documentViewer, CollectibleDataSO grauLetter)
        {
            guionChapter = chapterMarkdown;
            esmeralda = esmeraldaShip;
            gunner = esmeraldaGunner;
            esmeraldaAI = esmeraldaBrain;
            huascar = huascarShip;
            huascarAI = huascarBrain;
            turret = coles;
            ram = bow;
            shoreBatteries = batteries;
            cameraRig = rig;
            hud = navalHud;
            survivorTemplate = template;
            viewer = documentViewer;
            letter = grauLetter;
        }

        // ------------------------------------------------------------------------------------------
        // Arranque: guion y sucesos de la escena → hechos
        // ------------------------------------------------------------------------------------------

        private void Start()
        {
            GuionQuotes quotes = GuionQuotes.Extract(guionChapter, IquiqueChapter.ChapterHeading);
            _runner = new MissionRunner(IquiqueChapter.Build(quotes, survivorGroups));

            _esmeraldaDamage = esmeralda.GetComponent<ShipDamageController>();
            _huascarDamage = huascar.GetComponent<ShipDamageController>();
            if (esmeraldaAI != null)
            {
                esmeraldaAI.Anchor = bayCenter;
                esmeraldaAI.LeashRadius = bayRadius;
            }
            if (gunner != null)
            {
                gunner.Target = huascar;
                gunner.PlayerFired += _ => _runner.Facts.Add(F.Broadsides);
            }
            var huascarHull = huascar.GetComponent<ArmoredHull>();
            if (huascarHull != null) huascarHull.ImpactResolved += OnHuascarHit;
            var esmeraldaHull = esmeralda.GetComponent<ArmoredHull>();
            if (esmeraldaHull != null) esmeraldaHull.ImpactResolved += OnEsmeraldaHit;
            if (ram != null) ram.Rammed += OnRammed;
            if (turret != null) turret.Fired += OnTurretFired;
            if (_esmeraldaDamage != null) _esmeraldaDamage.Sunk += OnEsmeraldaSunk;
            if (_huascarDamage != null) _huascarDamage.Sunk += _ => _runner.Facts.SetFlag(G.HuascarSunk);
            if (viewer != null) viewer.Closed += OnViewerClosed;
            foreach (ShoreBattery battery in shoreBatteries) battery.Target = esmeralda;

            Apply(_runner.CurrentStage.Perspective);
        }

        private void OnDestroy()
        {
            if (viewer != null) viewer.Closed -= OnViewerClosed;
            if (ram != null) ram.Rammed -= OnRammed;
            if (turret != null) turret.Fired -= OnTurretFired;
        }

        private void OnHuascarHit(ResolvedImpact impact)
        {
            if (impact.Hit.Shooter == esmeralda.gameObject) _runner.Facts.Add(F.BroadsideHits);
        }

        private void OnEsmeraldaHit(ResolvedImpact impact)
        {
            GameObject shooter = impact.Hit.Shooter;
            if (shooter == huascar.gameObject)
            {
                _runner.Facts.Add(F.TurretHits);
                if (impact.BelowWaterline) _runner.Facts.Add(F.WaterlineHits);
            }
            else if (shooter != null && shooter.GetComponent<ShoreBattery>() != null)
            {
                _runner.Facts.Add(F.ShoreHits);
            }
        }

        private void OnRammed(RamBow bow, ShipController victim, RamResult result)
        {
            // Solo cuenta un espolonazo de verdad (RamModel.Critical), no un roce al maniobrar junto a ella.
            if (victim != esmeralda || !result.Critical || Time.time - _lastRamTime < MinSecondsBetweenRams) return;
            _lastRamTime = Time.time;
            _runner.Facts.Add(F.Rams);
        }

        private void OnTurretFired(ColesTurretController coles)
        {
            _runner.Facts.Add(F.TurretShots);
            // Solo cuenta como desobediencia si dispara el jugador después del hundimiento.
            if (_runner.Facts.Flag(G.EsmeraldaSunk) && coles.PlayerControlled) _runner.Facts.Add(F.ShotsAfterSinking);
        }

        private void OnEsmeraldaSunk(ShipDamageController damage)
        {
            _runner.Facts.SetFlag(G.EsmeraldaSunk);
            SpawnSurvivors(esmeralda.transform.position);
        }

        private void OnViewerClosed() => _runner.Facts.SetFlag(G.DocumentClosed);

        /// <summary>Grupos de náufragos en espiral alrededor del pecio (ángulo áureo: repartidos sin amontonarse).</summary>
        private void SpawnSurvivors(Vector3 wreck)
        {
            if (survivorTemplate == null) return;
            for (int i = 0; i < survivorGroups; i++)
            {
                float angle = i * 137.5f;
                float radius = 45f + 22f * i;
                Vector3 p = wreck + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                p.y = 0.2f;
                SurvivorGroup group = Instantiate(survivorTemplate, p, Quaternion.Euler(0f, angle * 3f, 0f));
                group.name = "Naufragos_" + (i + 1);
                group.gameObject.SetActive(true);
                group.Configure(huascar, 3 + i % 3);
                group.Rescued += _ => _runner.Facts.Add(F.SurvivorsRescued);
                _survivors.Add(group);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Bucle
        // ------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_runner == null) return;
            float dt = Time.deltaTime;

            if (_runner.State != MissionState.Running)
            {
                if (_runner.State == MissionState.Complete && !_recorded)
                {
                    _recorded = true;
                    _newCollectible = CampaignSave.Record(_runner.Script);
                }
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            ProtectStory();
            ShipDamageState esmeraldaState = _esmeraldaDamage != null ? _esmeraldaDamage.State : null;
            // Hasta el tercer espolonazo, el guion la mantiene a flote (ShipDamageState.KeepAfloat).
            if (esmeraldaState != null && !_foundered) esmeraldaState.KeepAfloat = true;
            if (_runner.Facts.Flag(G.FounderEsmeralda) && !_foundered && _esmeraldaDamage != null)
            {
                _foundered = true;
                _esmeraldaDamage.Founder(); // tercer espolonazo: desenlace histórico
            }

            _runner.Step(dt);

            string wanted = _runner.CurrentStage.Perspective;
            if (wanted != _perspective && _pendingPerspective == null)
            {
                _pendingPerspective = wanted;
                _fade = 0f;
            }
            if (_fade >= 0f)
            {
                // Fundido a negro y vuelta: el cambio de bando se hace con la pantalla a oscuras.
                float before = _fade;
                _fade += Time.unscaledDeltaTime / FadeSeconds;
                if (before < 1f && _fade >= 1f && _pendingPerspective != null)
                {
                    Apply(_pendingPerspective);
                    _pendingPerspective = null;
                }
                if (_fade >= 2f) _fade = -1f;
            }
            else
            {
                AdjustHuascarAI();
            }
        }

        /// <summary>
        /// En el acto I la Esmeralda no debe hundirse antes del espolón: si está muy tocada, la torre del Huáscar y
        /// las baterías de tierra aguantan el fuego. Las baterías solo tiran mientras la tienen a su alcance.
        /// </summary>
        private void ProtectStory()
        {
            string stage = _runner.CurrentStage.Id;
            bool actOne = stage == S.WoodenDeck || stage == S.Ram;
            ShipDamageState state = _esmeraldaDamage != null ? _esmeraldaDamage.State : null;
            bool critical = state != null && (state.FloodFraction > 0.5f || state.IntegrityFraction < 0.5f);
            if (huascarAI != null) huascarAI.HoldFire = actOne && critical;
            foreach (ShoreBattery battery in shoreBatteries)
            {
                float range = Vector3.Distance(battery.transform.position, esmeralda.transform.position);
                battery.HoldFire = !actOne || critical || range > battery.MaxRangeM;
            }
        }

        private void AdjustHuascarAI()
        {
            if (huascarAI == null) return;
            string stage = _runner.CurrentStage.Id;
            // Primer espolonazo a media máquina (así lo dio Grau); los siguientes, a toda fuerza.
            if (_perspective == P.EsmeraldaBattery) huascarAI.Mode = stage == S.Ram ? RammingMode.Ram : RammingMode.StandOff;
            else if (_perspective == P.HuascarTurret) huascarAI.Mode = stage == S.ThirdRam ? RammingMode.Ram : RammingMode.StandOff;
            huascarAI.RamOrder = stage == S.Ram ? EngineOrder.HalfAhead : EngineOrder.FullAhead;
        }

        /// <summary>Traduce la perspectiva de la etapa a cámara, mandos e IA.</summary>
        private void Apply(string perspective)
        {
            _perspective = perspective;
            bool battery = perspective == P.EsmeraldaBattery;
            bool gunnery = perspective == P.HuascarTurret;
            bool bridge = perspective == P.HuascarBridge;
            bool document = perspective == P.Document;

            if (gunner != null) gunner.PlayerActive = battery;
            if (esmeraldaAI != null) esmeraldaAI.AutoFire = !battery; // en el acto II sigue disparando sola
            esmeralda.PlayerControlled = false;
            if (_esmeraldaDamage != null) _esmeraldaDamage.PlayerControlled = battery;

            huascar.PlayerControlled = bridge;
            if (_huascarDamage != null) _huascarDamage.PlayerControlled = gunnery || bridge;
            if (turret != null) turret.PlayerControlled = gunnery || bridge;
            if (huascarAI != null)
            {
                huascarAI.Mode = RammingMode.Idle;
                AdjustHuascarAI();
            }
            if (bridge || document) huascar.Command(EngineOrder.Stop, 0f);
            if (document)
            {
                huascar.PlayerControlled = false;
                if (turret != null) turret.PlayerControlled = false;
                OpenLetter();
            }

            ShipController followed = battery ? esmeralda : huascar;
            if (cameraRig != null) cameraRig.Target = followed.transform;
            if (hud != null) hud.Ship = followed;
        }

        private void OpenLetter()
        {
            if (_letterOpened) return;
            _letterOpened = true;
            if (viewer == null || letter == null)
            {
                _runner.Facts.SetFlag(G.DocumentClosed); // sin visor, la misión no se queda colgada
                return;
            }
            viewer.Open(DocumentSpec.From(letter));
        }

        // ------------------------------------------------------------------------------------------
        // Interfaz (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            bool viewerOpen = viewer != null && viewer.IsOpen;
            if (!viewerOpen && _runner.State == MissionState.Running)
            {
                _hud.DrawObjectives(_runner, "CAPÍTULO 1", Who());
                _hud.DrawDialogue(_runner, 300f);
            }
            if (_runner.State != MissionState.Running && !viewerOpen)
            {
                string detail = letter != null ? (_newCollectible ? "Coleccionable desbloqueado: " : "Coleccionable: ") + letter.title : string.Empty;
                _hud.DrawEnd(_runner, "CAPÍTULO 1", detail);
            }
            if (_fade >= 0f) MissionHud.DrawFade(1f - Mathf.Abs(_fade - 1f), Color.black);
        }

        private string Who()
        {
            switch (_perspective)
            {
                case P.EsmeraldaBattery: return "Wenceslao Vargas · grumete, batería de la Esmeralda";
                case P.HuascarTurret: return "Cabo Dámaso Antúnez · torre Coles del Huáscar";
                case P.HuascarBridge: return "Huáscar · a las órdenes de Grau";
                default: return string.Empty;
            }
        }
    }
}
