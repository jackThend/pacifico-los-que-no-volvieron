using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Data;
using Pacifico.Input;
using Pacifico.Tactics;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Facts;
using G = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Flags;
using S = Pacifico.Core.Campaign.AltoDeLaAlianzaChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 5 en escena (ROADMAP 6.3). El guion es <see cref="AltoDeLaAlianzaChapter"/>; aquí se crean
    /// las oleadas chilenas de cada momento, se entregan los cañones de la izquierda al enemigo cuando el flanco cede, se
    /// da la carga de los Colorados (tecla C o el botón) y se cuentan las bajas, los cañones recuperados y los hombres
    /// que llegan a la retaguardia.
    /// </summary>
    public sealed class AltoDeLaAlianzaDirector : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Uniform
        {
            public Material coat;
            public Material trim;
        }

        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;
        [SerializeField] private SquadController[] playerSquads = new SquadController[0];
        [SerializeField] private WeaponDataSO comblain;
        [SerializeField] private WeaponDataSO winchester;
        [SerializeField] private Uniform chile = new Uniform();
        [SerializeField] private SquadArtillery[] chileanBattery = new SquadArtillery[0];
        [Tooltip("Cañones de la izquierda aliada: pasan a manos chilenas cuando el flanco cede y hay que recuperarlos.")]
        [SerializeField] private SquadArtillery[] leftFlankGuns = new SquadArtillery[0];
        [SerializeField] private Transform[] firstWave = new Transform[0];
        [SerializeField] private Transform[] gunGuards = new Transform[0];
        [SerializeField] private Transform[] cavalry = new Transform[0];
        [SerializeField] private Transform[] reserves = new Transform[0];
        [SerializeField] private Transform rearGuard;
        [SerializeField] private float rearRadius = 30f;
        [SerializeField] private AudioClip huayno;

        private MissionRunner _runner;
        private readonly MissionHud _hud = new MissionHud();
        private readonly List<SquadController> _chileans = new List<SquadController>();
        private string _stage;
        private int _evacuated;
        private bool _recorded;
        private bool _charged;
        private float _fadeIn = 1f;
        private AudioSource _music;
        private GUIStyle _button;
        private GUIStyle _label;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, SquadController[] player, WeaponDataSO chileRifle, WeaponDataSO chileCarbine, Uniform chileUniform,
                              SquadArtillery[] battery, SquadArtillery[] flankGuns, Transform[] wave, Transform[] guards, Transform[] horse,
                              Transform[] reserve, Transform rear)
        {
            guionChapter = chapterMarkdown;
            playerSquads = player;
            comblain = chileRifle;
            winchester = chileCarbine;
            chile = chileUniform;
            chileanBattery = battery;
            leftFlankGuns = flankGuns;
            firstWave = wave;
            gunGuards = guards;
            cavalry = horse;
            reserves = reserve;
            rearGuard = rear;
        }

        private void Start()
        {
            _runner = new MissionRunner(AltoDeLaAlianzaChapter.Build(GuionQuotes.Extract(guionChapter, AltoDeLaAlianzaChapter.ChapterHeading), playerSquads.Length));
            SquadController.CasualtiesTaken += OnCasualties;
            foreach (SquadArtillery gun in leftFlankGuns) gun.Captured += OnGunCaptured;
            foreach (SquadArtillery gun in chileanBattery) gun.Firing = false; // bajo la camanchaca no se ve a quién tirar
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            UpdateFacts();
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy() => SquadController.CasualtiesTaken -= OnCasualties;

        private void OnCasualties(SquadController squad, int count)
        {
            if (squad.Faction == Faction.Chile && _runner != null) _runner.Facts.Add(F.EnemyDown, count);
        }

        private void OnGunCaptured(SquadArtillery gun, Faction newOwner)
        {
            if (newOwner != Faction.Chile) _runner.Facts.Add(F.GunsRetaken);
        }

        private SquadController Spawn(string name, Transform at, WeaponDataSO weapon, int men, FormationType formation, float delay, float pace, float chargeWithin)
        {
            var go = new GameObject("Escuadra_" + name);
            Vector3 p = at.position;
            if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p = hit.point;
            go.transform.SetPositionAndRotation(p, at.rotation);
            var squad = go.AddComponent<SquadController>();
            squad.Configure(name, Faction.Chile, weapon, men, formation, false, chile.coat, chile.trim);
            squad.BasePace = pace;
            var ai = go.AddComponent<SquadAI>();
            ai.StartDelaySeconds = delay;
            ai.ChargeWithinM = chargeWithin;
            _chileans.Add(squad);
            return squad;
        }

        private void Update()
        {
            if (_runner == null) return;
            _fadeIn = Mathf.Max(0f, _fadeIn - Time.unscaledDeltaTime / 2.5f);
            if (_runner.State != MissionState.Running)
            {
                if (_runner.State == MissionState.Complete && !_recorded)
                {
                    _recorded = true;
                    CampaignSave.Record(_runner.Script);
                }
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (_stage == S.Charge && !_charged && GameInput.Pressed(GameKey.C)) OrderCharge();
            if (_stage == S.Retreat) EvacuateArrivals();
            UpdateFacts();

            _runner.Step(Time.deltaTime);
            if (_runner.Facts.Flag(G.LeftFlankBroken) && leftFlankGuns.Length > 0 && leftFlankGuns[0].Owner != Faction.Chile && _stage == S.Advance) FlankGives();
            if (_runner.CurrentStage.Id != _stage)
            {
                _stage = _runner.CurrentStage.Id;
                EnterStage(_stage);
            }
        }

        private void UpdateFacts()
        {
            int entrenched = 0, men = 0;
            foreach (SquadController squad in playerSquads)
            {
                if (squad == null || !squad.IsAlive || squad.Evacuated) continue;
                men += squad.Strength;
                if (squad.InCover * 2 >= squad.Strength) entrenched++;
            }
            _runner.Facts.Set(F.SquadsEntrenched, entrenched);
            _runner.Facts.Set(F.MenInField, men);
            _runner.Facts.Set(F.MenEvacuated, _evacuated);
        }

        private void EnterStage(string stage)
        {
            if (stage == S.Advance)
            {
                // La niebla se levanta: la primera línea chilena rompe el avance y su artillería abre fuego.
                for (int i = 0; i < firstWave.Length; i++) Spawn(i % 2 == 0 ? "2.º de Línea · " + (i / 2 + 1) : "Atacama · " + (i / 2 + 1), firstWave[i], comblain, 12, i % 2 == 0 ? FormationType.Line : FormationType.Skirmish, 0f, 1f, 0f);
                foreach (SquadArtillery gun in chileanBattery) gun.Firing = true;
            }
            else if (stage == S.Charge)
            {
                foreach (SquadArtillery gun in leftFlankGuns) gun.Capturable = true;
            }
            else if (stage == S.Retreat)
            {
                // La tenaza: caballería por el flanco izquierdo y reservas de frente.
                for (int i = 0; i < cavalry.Length; i++) Spawn("Cazadores a Caballo · " + (i + 1), cavalry[i], winchester, 8, FormationType.Skirmish, 0f, SquadMarch.CavalryPace, 90f);
                for (int i = 0; i < reserves.Length; i++) Spawn("Reserva chilena · " + (i + 1), reserves[i], comblain, 12, FormationType.Line, 5f, 1f, 0f);
                foreach (SquadArtillery gun in leftFlankGuns) gun.Capturable = false;
            }
        }

        /// <summary>La izquierda aliada cede: sus cañones pasan a manos chilenas, con una escuadra de guardia en cada uno.</summary>
        private void FlankGives()
        {
            foreach (SquadArtillery gun in leftFlankGuns) gun.SetOwner(Faction.Chile);
            for (int i = 0; i < gunGuards.Length; i++)
            {
                // Se quedan junto a las piezas: su IA no rompe nunca el avance (responden al fuego desde su sitio).
                Spawn("Guardia de los cañones · " + (i + 1), gunGuards[i], comblain, 10, FormationType.Line, 1e6f, 1f, 0f);
            }
        }

        private void OrderCharge()
        {
            _charged = true;
            _runner.Facts.SetFlag(G.ChargeOrdered);
            if (huayno != null)
            {
                _music.clip = huayno;
                _music.Play();
            }
            foreach (SquadController squad in playerSquads)
            {
                if (squad == null || !squad.IsAlive || squad.Evacuated || squad.Routed) continue;
                SquadController nearest = null;
                float best = float.PositiveInfinity;
                foreach (SquadController enemy in SquadController.All)
                {
                    if (!enemy.IsAlive || !squad.IsEnemyOf(enemy)) continue;
                    float d = Vector3.Distance(enemy.CenterOfMass(), squad.CenterOfMass());
                    if (d < best)
                    {
                        best = d;
                        nearest = enemy;
                    }
                }
                if (nearest != null) squad.IssueCharge(nearest);
            }
        }

        private void EvacuateArrivals()
        {
            if (rearGuard == null) return;
            foreach (SquadController squad in playerSquads)
            {
                if (squad == null || !squad.IsAlive || squad.Evacuated) continue;
                Vector3 c = squad.CenterOfMass();
                if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(rearGuard.position.x, rearGuard.position.z)) <= rearRadius) _evacuated += squad.Evacuate();
            }
        }

        // ------------------------------------------------------------------------------------------
        // Interfaz
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            if (_button == null)
            {
                _button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _label.normal.textColor = Color.white;
            }
            if (_runner.State == MissionState.Running)
            {
                _hud.DrawObjectives(_runner, "CAPÍTULO 5", "Subteniente Daniel Ballivián · Colorados de Bolivia");
                _hud.DrawDialogue(_runner, 200f);
                if (_stage == S.Charge && !_charged)
                {
                    GUI.color = new Color(0.85f, 0.2f, 0.15f);
                    if (GUI.Button(new Rect(Screen.width * 0.5f - 130f, Screen.height - 110f, 260f, 56f), "¡A LA CARGA!  [C]", _button)) OrderCharge();
                    GUI.color = Color.white;
                }
                DrawMarkers();
            }
            else
            {
                string detail = "Hombres salvados en la retaguardia: " + _evacuated + ".";
                _hud.DrawEnd(_runner, "CAPÍTULO 5", detail);
            }
            MissionHud.DrawFade(_fadeIn, Color.black);
        }

        private void DrawMarkers()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            if (_stage == S.Retreat && rearGuard != null) Marker(camera, rearGuard.position, "RETAGUARDIA · heridos a salvo: " + _evacuated);
            if (_stage == S.Charge)
            {
                foreach (SquadArtillery gun in leftFlankGuns)
                {
                    string state = gun.Owner == Faction.Chile ? "Cañón en manos chilenas" + (gun.CaptureProgress > 0f ? " · " + (gun.CaptureProgress * 100f).ToString("0") + "%" : string.Empty) : "Cañón recuperado";
                    Marker(camera, gun.transform.position, state);
                }
            }
        }

        private void Marker(Camera camera, Vector3 world, string text)
        {
            Vector3 screen = camera.WorldToScreenPoint(world + Vector3.up * 6f);
            if (screen.z <= 0f) return;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(screen.x - 159f, Screen.height - screen.y - 11f, 320f, 22f), text, _label);
            GUI.color = Color.white;
            GUI.Label(new Rect(screen.x - 160f, Screen.height - screen.y - 12f, 320f, 22f), text, _label);
        }
    }
}
