using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Data;
using Pacifico.Input;
using Pacifico.Narrative;
using Pacifico.Tactics;
using UnityEngine;
using UnityEngine.SceneManagement;
using C = Pacifico.Core.Campaign.PisaguaChapter;
using F = Pacifico.Core.Campaign.PisaguaChapter.Facts;
using G = Pacifico.Core.Campaign.PisaguaChapter.Flags;
using P = Pacifico.Core.Campaign.PisaguaChapter.Perspectives;
using S = Pacifico.Core.Campaign.PisaguaChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 3, «Sangre en el salitre», en escena (ROADMAP 6.8). El guion es <see cref="PisaguaChapter"/>;
    /// aquí se botan las olas de desembarco y se calcula el fuego que reciben (fuertes activos y nidos de fusileros
    /// según su supresión), se crean en la playa las escuadras que llegan, se atiende la artillería de la flota (B y clic,
    /// o los botones de los fuertes), se repliegan los nidos del terraplén cuando ceden, se encienden las mechas de los
    /// estanques y las locomotoras, se lanza el contraataque y se abre el despacho del coronel Wood al final.
    /// </summary>
    public sealed class PisaguaMissionDirector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Uniform
        {
            public Material coat;
            public Material trim;
            /// <summary>Soldado con esqueleto; sin él, se ven las primitivas.</summary>
            public GameObject model;
        }

        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;

        [Header("Desembarco")]
        [SerializeField] private LandingCraft[] boats = new LandingCraft[0];
        [SerializeField] private CoastalFortBattery[] forts = new CoastalFortBattery[0];
        [SerializeField] private NavalGunfireSupport fleet;
        [SerializeField] private WeaponDataSO comblain;
        [SerializeField] private Uniform chile = new Uniform();

        [Header("Defensores")]
        [Tooltip("Nidos de fusileros del terraplén del ferrocarril (tiran a los botes y defienden la ladera).")]
        [SerializeField] private SquadController[] nests = new SquadController[0];
        [SerializeField] private Transform embankment;
        [SerializeField] private float embankmentRadiusM = 45f;
        [Tooltip("Hacia dónde se repliegan los defensores (la estación).")]
        [SerializeField] private Transform rally;
        [SerializeField] private Transform[] counterSpawns = new Transform[0];
        [SerializeField] private WeaponDataSO remington;
        [SerializeField] private Uniform allies = new Uniform();

        [Header("Fase 3")]
        [SerializeField] private DemolitionObjective water;
        [SerializeField] private DemolitionObjective locomotives;

        [Header("Escena")]
        [SerializeField] private RtsCommander commander;
        [SerializeField] private Texture2D plate;
        [SerializeField] private string plateCaption = string.Empty;
        [SerializeField] private DocumentViewer viewer;
        [SerializeField] private CollectibleDataSO dispatch;

        private MissionRunner _runner;
        private readonly MissionHud _hud = new MissionHud();
        private readonly List<SquadController> _player = new List<SquadController>();
        private readonly List<SquadController> _counter = new List<SquadController>();
        private readonly HashSet<SquadController> _withdrawn = new HashSet<SquadController>();
        private readonly CapturePoint _embankment = new CapturePoint(5f);
        private CoastalFort[] _fortStates = new CoastalFort[0];
        private string _stage;
        private int _boatsLanded;
        private int _menLanded;
        private int _menLost;
        private bool _secondWave;
        private bool _designating;
        private bool _recorded;
        private bool _newCollectible;
        private bool _documentOpened;
        private float _nextNestCheck;
        private float _fade = 1f;
        private GUIStyle _label;
        private GUIStyle _caption;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, LandingCraft[] landingBoats, CoastalFortBattery[] coastalForts, NavalGunfireSupport support,
                              WeaponDataSO chileRifle, Uniform chileUniform, SquadController[] rifleNests, Transform embankmentCentre, Transform rallyPoint,
                              Transform[] counterAttack, WeaponDataSO alliedRifle, Uniform alliedUniform, DemolitionObjective tanks, DemolitionObjective engines,
                              RtsCommander rts, Texture2D prologuePlate, string caption, DocumentViewer documentViewer, CollectibleDataSO woodDispatch)
        {
            guionChapter = chapterMarkdown;
            boats = landingBoats;
            forts = coastalForts;
            fleet = support;
            comblain = chileRifle;
            chile = chileUniform;
            nests = rifleNests;
            embankment = embankmentCentre;
            rally = rallyPoint;
            counterSpawns = counterAttack;
            remington = alliedRifle;
            allies = alliedUniform;
            water = tanks;
            locomotives = engines;
            commander = rts;
            plate = prologuePlate;
            plateCaption = caption;
            viewer = documentViewer;
            dispatch = woodDispatch;
        }

        private void Start()
        {
            _runner = new MissionRunner(C.Build(GuionQuotes.Extract(guionChapter, C.ChapterHeading), boats.Length));
            foreach (LandingCraft boat in boats)
            {
                boat.FireAt = FireOnBoat;
                boat.Landed += OnLanded;
            }
            foreach (CoastalFortBattery fort in forts) fort.Silenced += OnFortSilenced;
            _fortStates = new CoastalFort[forts.Length];
            for (int i = 0; i < forts.Length; i++) _fortStates[i] = forts[i].Fort;
            if (water != null)
            {
                water.Secured += _ => _runner.Facts.SetFlag(G.WaterSecured);
                water.Blown += _ => _runner.Facts.SetFlag(G.WaterBlown);
            }
            if (locomotives != null)
            {
                locomotives.Secured += _ => _runner.Facts.SetFlag(G.LocomotivesSecured);
                locomotives.Blown += _ => _runner.Facts.SetFlag(G.LocomotivesBlown);
            }
            if (viewer != null) viewer.Closed += OnViewerClosed;
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy()
        {
            if (viewer != null) viewer.Closed -= OnViewerClosed;
        }

        private void OnViewerClosed() => _runner.Facts.SetFlag(G.DocumentClosed);

        // ------------------------------------------------------------------------------------------
        // Desembarco
        // ------------------------------------------------------------------------------------------

        /// <summary>Fuego sobre un bote: los fuertes activos y los nidos de fusileros, según su fuerza y su supresión.</summary>
        private float FireOnBoat(Vector3 boat)
        {
            float nestFire = 0f;
            foreach (SquadController nest in nests)
            {
                if (nest == null || !nest.IsAlive || nest.Routed || _withdrawn.Contains(nest)) continue;
                float d = Vector3.Distance(Flat(nest.CenterOfMass()), Flat(boat));
                nestFire += LandingBoat.RifleIntensity(nest.Strength, Mathf.Clamp01(nest.Suppression.Level), d, nest.Weapon != null ? nest.Weapon.EffectiveRangeM : 800f);
            }
            return C.FireOnBoat(_fortStates, boat.x, boat.z, nestFire);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnLanded(LandingCraft boat, int survivors)
        {
            _boatsLanded++;
            _menLanded += survivors;
            _menLost += boat.StartMen - survivors;
            _runner.Facts.Set(F.BoatsLanded, _boatsLanded);
            _runner.Facts.Set(F.MenLanded, _menLanded);
            _runner.Facts.Set(F.MenLostAtSea, _menLost);
            if (survivors <= 0) return;

            var go = new GameObject("Escuadra_" + boat.SquadName);
            Vector3 p = boat.BeachPoint;
            if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, 0f)); // de cara al risco
            var squad = go.AddComponent<SquadController>();
            squad.Configure(boat.SquadName, Faction.Chile, comblain, survivors, FormationType.Skirmish, true, chile.coat, chile.trim, chile.model);
            _player.Add(squad);
        }

        private void OnFortSilenced(CoastalFortBattery fort)
        {
            int silenced = 0;
            foreach (CoastalFortBattery f in forts)
            {
                if (f.Fort.Silenced) silenced++;
            }
            _runner.Facts.Set(F.FortsSilenced, silenced);
            _runner.Facts.SetFlag(fort.FortName == C.Pichalo ? G.PichaloSilenced : G.GuacasSilenced);
        }

        // ------------------------------------------------------------------------------------------
        // Bucle
        // ------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_runner == null) return;
            _fade = Mathf.Max(0f, _fade - Time.unscaledDeltaTime / 2.5f);
            if (_runner.State != MissionState.Running)
            {
                if (_runner.State == MissionState.Complete && !_recorded)
                {
                    _recorded = true;
                    _newCollectible = CampaignSave.Record(_runner.Script);
                }
                CampaignNavigator.HandleEndKeys(_runner.Script.Id, _runner.State == MissionState.Complete);
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            HandleDesignation();
            ReadScene();
            _runner.Step(Time.deltaTime);
            Direct();
            if (_runner.CurrentStage.Id != _stage)
            {
                _stage = _runner.CurrentStage.Id;
                EnterStage(_stage);
            }
        }

        /// <summary>B y clic: la flota tira donde se marque. Mientras se marca, el clic no selecciona escuadras.</summary>
        private void HandleDesignation()
        {
            if (fleet == null || _runner.CurrentStage.Perspective != P.Command) return;
            if (GameInput.Pressed(GameKey.B)) SetDesignating(!_designating);
            if (!_designating) return;
            if (GameInput.MousePressed(1) || GameInput.Pressed(GameKey.Escape))
            {
                SetDesignating(false);
                return;
            }
            if (!GameInput.MousePressed(0)) return;
            Camera camera = Camera.main;
            if (camera != null && Physics.Raycast(camera.ScreenPointToRay(GameInput.MousePosition), out RaycastHit hit, 3000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                fleet.Designate(hit.point);
            }
            SetDesignating(false);
        }

        private void SetDesignating(bool on)
        {
            _designating = on;
            if (commander != null) commander.enabled = !on;
        }

        private void ReadScene()
        {
            int men = 0;
            foreach (SquadController squad in _player)
            {
                if (squad != null && squad.IsAlive) men += squad.Strength;
            }
            _runner.Facts.Set(F.MenInField, men);
            if (fleet != null) _runner.Facts.Set(F.Salvos, fleet.Fleet.SalvosFired);

            // Los nidos ceden: diezmados, o suprimidos con los chilenos encima. Se repliegan a la estación.
            if (Time.time >= _nextNestCheck)
            {
                _nextNestCheck = Time.time + 0.5f;
                foreach (SquadController nest in nests)
                {
                    if (nest == null || !nest.IsAlive || _withdrawn.Contains(nest)) continue;
                    bool suppressed = nest.Suppression.State == SuppressionState.Suppressed;
                    if (!C.NestWithdraws(nest.Strength, suppressed, NearestChilean(nest.CenterOfMass()))) continue;
                    _withdrawn.Add(nest);
                    if (rally != null) nest.Rout(rally.position);
                }
            }

            // El terraplén: chilenos encima y ningún defensor que no se haya replegado.
            if (_stage == S.Cliff && embankment != null && !_runner.Facts.Flag(G.EmbankmentTaken))
            {
                int friends = 0, enemies = 0;
                foreach (SquadController squad in SquadController.All)
                {
                    if (!squad.IsAlive || squad.Routed || _withdrawn.Contains(squad)) continue;
                    foreach (SoldierUnit soldier in squad.Soldiers)
                    {
                        if (Vector3.Distance(Flat(soldier.transform.position), Flat(embankment.position)) > embankmentRadiusM) continue;
                        if (squad.Faction == Faction.Chile) friends++;
                        else enemies++;
                    }
                }
                if (_embankment.Step(Time.deltaTime, friends, enemies)) _runner.Facts.SetFlag(G.EmbankmentTaken);
            }

            if (_stage == S.Defense && _runner.Facts.Flag(G.CounterAttack) && _counter.Count > 0)
            {
                bool any = false;
                foreach (SquadController squad in _counter)
                {
                    if (squad == null || !squad.IsAlive || squad.Routed) continue;
                    if (squad.Strength <= 3 && rally != null)
                    {
                        squad.Rout(rally.position + rally.forward * 150f); // se pierden hacia el interior
                        continue;
                    }
                    any = true;
                }
                if (!any) _runner.Facts.SetFlag(G.CounterRepulsed);
            }
        }

        private float NearestChilean(Vector3 from)
        {
            float best = float.PositiveInfinity;
            foreach (SquadController squad in _player)
            {
                if (squad == null || !squad.IsAlive) continue;
                best = Mathf.Min(best, Vector3.Distance(Flat(squad.CenterOfMass()), Flat(from)));
            }
            return best;
        }

        /// <summary>Órdenes que dependen de marcas del guion.</summary>
        private void Direct()
        {
            if (!_secondWave && _runner.Facts.Flag(G.SecondWave))
            {
                _secondWave = true;
                for (int i = boats.Length / 2; i < boats.Length; i++) boats[i].Launch();
            }
            if (_runner.Facts.Flag(G.FleetFinishesForts))
            {
                foreach (CoastalFortBattery fort in forts) fort.SilenceNow();
            }
        }

        private void EnterStage(string stage)
        {
            if (stage == S.Beachhead)
            {
                for (int i = 0; i < boats.Length / 2; i++) boats[i].Launch();
            }
            else if (stage == S.Wells)
            {
                if (water != null) water.Light(C.WaterFuseSeconds);
                if (locomotives != null) locomotives.Light(C.LocomotiveFuseSeconds);
            }
            else if (stage == S.Defense)
            {
                for (int i = 0; i < counterSpawns.Length; i++)
                {
                    var go = new GameObject("Escuadra_Retaguardia_aliada_" + (i + 1));
                    go.transform.SetPositionAndRotation(counterSpawns[i].position, counterSpawns[i].rotation);
                    var squad = go.AddComponent<SquadController>();
                    squad.Configure("Retaguardia aliada · " + (i + 1), i % 2 == 0 ? Faction.Bolivia : Faction.Peru, remington, C.MenPerNest,
                                    FormationType.Skirmish, false, allies.coat, allies.trim,
                                    allies.model);
                    var ai = go.AddComponent<SquadAI>();
                    ai.StartDelaySeconds = 2f * i;
                    _counter.Add(squad);
                }
            }
            else if (stage == S.Dispatch)
            {
                SetDesignating(false);
                if (fleet != null) fleet.Cancel();
                OpenDispatch();
            }
        }

        private void OpenDispatch()
        {
            if (_documentOpened) return;
            _documentOpened = true;
            if (viewer == null || dispatch == null)
            {
                _runner.Facts.SetFlag(G.DocumentClosed); // sin visor, la misión no se queda colgada
                return;
            }
            viewer.Open(DocumentSpec.From(dispatch));
        }

        // ------------------------------------------------------------------------------------------
        // Interfaz (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            EnsureStyles();
            bool viewerOpen = viewer != null && viewer.IsOpen;
            if (_runner.State == MissionState.Running)
            {
                if (_runner.CurrentStage.Perspective == P.Plate) DrawPlate();
                else if (!viewerOpen)
                {
                    _hud.DrawObjectives(_runner, "CAPÍTULO 3", "Comandancia de desembarco · Atacama y Zapadores");
                    DrawFleetControls();
                    DrawMarkers();
                }
                if (!viewerOpen) _hud.DrawDialogue(_runner, _runner.CurrentStage.Perspective == P.Plate ? 40f : 120f);
            }
            else if (!viewerOpen)
            {
                string detail = "Desembarcaron " + _menLanded + " hombres; " + _menLost + " cayeron en los botes." +
                                (dispatch != null ? "\n" + (_newCollectible ? "Coleccionable desbloqueado: " : "Coleccionable: ") + dispatch.title : string.Empty);
                _hud.DrawEnd(_runner, "CAPÍTULO 3", detail);
            }
            MissionHud.DrawFade(_fade, Color.black);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _caption = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _caption.normal.textColor = new Color(0.92f, 0.88f, 0.78f);
        }

        private void DrawPlate()
        {
            MissionHud.DrawFade(1f, Color.black);
            if (plate == null) return;
            float aspect = plate.width / (float)plate.height;
            float h = Screen.height * 0.62f, w = h * aspect;
            if (w > Screen.width * 0.9f)
            {
                w = Screen.width * 0.9f;
                h = w / aspect;
            }
            GUI.color = new Color(1f, 0.93f, 0.8f, Mathf.Clamp01(_runner.StageTime / 1.5f));
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.05f, w, h), plate, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.05f + h + 8f, Screen.width * 0.8f, 44f), plateCaption, _caption);
        }

        /// <summary>Botones de la flota (arriba a la derecha): un fuerte o un punto marcado con B y clic.</summary>
        private void DrawFleetControls()
        {
            if (fleet == null) return;
            var box = new Rect(Screen.width - 300f, 16f, 284f, 40f + 34f * forts.Length + 40f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            NavalBombardment model = fleet.Fleet;
            string state = model.HasTarget ? "salva en " + model.ReloadRemaining.ToString("0") + " s" : "sin blanco";
            GUI.Label(new Rect(box.x, box.y + 6f, box.width, 24f), "Flota (Cochrane y O'Higgins): " + state, _label);
            for (int i = 0; i < forts.Length; i++)
            {
                CoastalFortBattery fort = forts[i];
                GUI.enabled = !fort.Fort.Silenced;
                string text = fort.FortName + (fort.Fort.Silenced ? " · callado" : " · " + fort.Fort.Hits + "/" + fort.Fort.HitsToSilence + " impactos");
                if (GUI.Button(new Rect(box.x + 10f, box.y + 34f + 34f * i, box.width - 20f, 28f), text)) fleet.Designate(fort.transform.position);
            }
            GUI.enabled = true;
            string mark = _designating ? "Clic en el mapa para marcar (clic derecho: cancelar)" : "[B] Marcar otro blanco";
            if (GUI.Button(new Rect(box.x + 10f, box.y + 34f + 34f * forts.Length, box.width - 20f, 28f), mark)) SetDesignating(!_designating);
        }

        private void DrawMarkers()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            foreach (LandingCraft boat in boats)
            {
                if (boat != null && boat.Launched && !boat.Beached) Marker(camera, boat.transform.position, boat.SquadName + " · " + boat.Men + " a bordo");
            }
            if (fleet != null && fleet.Fleet.HasTarget) Marker(camera, fleet.Target, "✕ blanco de la flota");
            if (_stage == S.Cliff && embankment != null)
            {
                Marker(camera, embankment.position, "TERRAPLÉN DEL FERROCARRIL" + (_embankment.Progress > 0f ? " · " + (_embankment.Progress * 100f).ToString("0") + "%" : string.Empty));
            }
            if (_stage == S.Wells || _stage == S.Defense)
            {
                Objective(camera, water, "Estanques de agua dulce");
                Objective(camera, locomotives, "Locomotoras");
            }
        }

        private void Objective(Camera camera, DemolitionObjective objective, string name)
        {
            if (objective == null || !objective.Lit) return;
            DemolitionCharge charge = objective.Charge;
            string state = charge.Defused ? "a salvo" : charge.Blown ? "VOLADAS" : "mecha: " + charge.Remaining.ToString("0") + " s" +
                (objective.CaptureProgress > 0f ? " · " + (objective.CaptureProgress * 100f).ToString("0") + "%" : string.Empty);
            Marker(camera, objective.transform.position, name + " · " + state);
        }

        private void Marker(Camera camera, Vector3 world, string text)
        {
            Vector3 screen = camera.WorldToScreenPoint(world + Vector3.up * 6f);
            if (screen.z <= 0f) return;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(screen.x - 179f, Screen.height - screen.y - 11f, 360f, 22f), text, _label);
            GUI.color = Color.white;
            GUI.Label(new Rect(screen.x - 180f, Screen.height - screen.y - 12f, 360f, 22f), text, _label);
        }
    }
}
