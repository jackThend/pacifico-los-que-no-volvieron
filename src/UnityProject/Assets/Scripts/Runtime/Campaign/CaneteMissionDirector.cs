using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Input;
using Pacifico.Tactics;
using UnityEngine;
using UnityEngine.SceneManagement;
using C = Pacifico.Core.Campaign.CaneteChapter;
using F = Pacifico.Core.Campaign.CaneteChapter.Facts;
using G = Pacifico.Core.Campaign.CaneteChapter.Flags;
using P = Pacifico.Core.Campaign.CaneteChapter.Perspectives;
using S = Pacifico.Core.Campaign.CaneteChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 7, «Cadenas rotas», en escena (ROADMAP 6.9). El guion es <see cref="CaneteChapter"/>. Acto I:
    /// los torpedos de tierra de la senda del cañaveral. Acto II: estalla la mina de la patrulla, los fusileros de la
    /// hacienda abren fuego, dos tiradores salen a rematar al herido y Quintín, con el machete, lo lleva a la acequia.
    /// Acto III: la Winchester, la dinamita, el portón y el nido, con los culíes y los soldados chilenos.
    /// </summary>
    public sealed class CaneteMissionDirector : MonoBehaviour
    {
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Texture2D plate;
        [SerializeField] private string plateCaption = string.Empty;

        [Header("Acto I")]
        [SerializeField] private TripwireMine[] trailMines = new TripwireMine[0];

        [Header("Acto II")]
        [SerializeField] private TripwireMine patrolMine;
        [SerializeField] private WoundedSoldier wounded;
        [SerializeField] private Transform[] finisherSpawns = new Transform[0];
        [SerializeField] private Transform[] wallPosts = new Transform[0];

        [Header("Acto III")]
        [SerializeField] private Transform winchesterSpot;
        [SerializeField] private InteractionPoint dynamite;
        [SerializeField] private InteractionPoint gateCharge;
        [SerializeField] private GameObject gate;
        [SerializeField] private Transform courtyard;
        [SerializeField] private Transform[] allySpawns = new Transform[0];
        [SerializeField] private Material blast;
        [SerializeField] private Light moon;

        [Header("Armas y uniformes")]
        [SerializeField] private WeaponDataSO winchester;
        [SerializeField] private WeaponDataSO comblain;
        [SerializeField] private WeaponDataSO remington;
        [SerializeField] private WeaponDataSO bayonet;
        [SerializeField] private SoldierLook chileLook = new SoldierLook();
        [SerializeField] private SoldierLook culiLook = new SoldierLook();
        [SerializeField] private SoldierLook peruLook = new SoldierLook();

        private const float GateBlastLethalM = 5f;
        private const float GateBlastHurtM = 9f;

        private MissionRunner _runner;
        private Combatant _playerCombatant;
        private WeaponSpec _bayonetSpec;
        private readonly MissionHud _hud = new MissionHud();
        private readonly List<RiflemanAI> _finishers = new List<RiflemanAI>();
        private readonly List<RiflemanAI> _allies = new List<RiflemanAI>();
        private string _stage;
        private int _seed = 1880;
        private bool _recorded;
        private bool _blastDone;
        private float _gateFuse = -1f;
        private float _dawn;
        private float _nextImpact;
        private GUIStyle _caption;
        private GUIStyle _small;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, FirstPersonController fps, Texture2D prologuePlate, string caption, TripwireMine[] mines,
                              TripwireMine patrol, WoundedSoldier woundedSoldier, Transform[] finishers, Transform[] posts, Transform winchesterPoint,
                              InteractionPoint dynamitePoint, InteractionPoint chargePoint, GameObject haciendaGate, Transform haciendaCourtyard,
                              Transform[] allies, Material blastMaterial, Light moonLight, WeaponDataSO carbine, WeaponDataSO chileRifle,
                              WeaponDataSO peruRifle, WeaponDataSO bayonetData, SoldierLook chilean, SoldierLook culi, SoldierLook peruvian)
        {
            guionChapter = chapterMarkdown;
            player = fps;
            plate = prologuePlate;
            plateCaption = caption;
            trailMines = mines;
            patrolMine = patrol;
            wounded = woundedSoldier;
            finisherSpawns = finishers;
            wallPosts = posts;
            winchesterSpot = winchesterPoint;
            dynamite = dynamitePoint;
            gateCharge = chargePoint;
            gate = haciendaGate;
            courtyard = haciendaCourtyard;
            allySpawns = allies;
            blast = blastMaterial;
            moon = moonLight;
            winchester = carbine;
            comblain = chileRifle;
            remington = peruRifle;
            bayonet = bayonetData;
            chileLook = chilean;
            culiLook = culi;
            peruLook = peruvian;
        }

        private void Start()
        {
            _runner = new MissionRunner(C.Build(GuionQuotes.Extract(guionChapter, C.ChapterHeading)));
            _playerCombatant = player.GetComponent<Combatant>();
            _bayonetSpec = bayonet != null ? bayonet.ToSpec() : null;
            Combatant.Downed += OnDowned;
            WeaponPickup.PickedUp += OnPickedUp;

            foreach (TripwireMine mine in trailMines)
            {
                mine.Bind(player);
                mine.Disarmed += _ => _runner.Facts.Add(F.MinesDisarmed);
                mine.Exploded += (_, byPlayer) =>
                {
                    if (byPlayer) _runner.Facts.SetFlag(G.MineTriggered);
                };
            }
            if (wounded != null)
            {
                wounded.NearestEnemy = NearestFinisher;
                wounded.PickedUp += _ => _runner.Facts.SetFlag(G.WoundedCarried);
                wounded.Saved += _ => _runner.Facts.SetFlag(G.WoundedSafe);
                wounded.Lost += w => _runner.Facts.SetFlag(w.Model.Killed ? G.WoundedKilled : G.WoundedBledOut);
            }
            if (dynamite != null)
            {
                dynamite.Configure("Recoger los cartuchos de dinamita", player.transform);
                dynamite.OnUsed += _ =>
                {
                    _runner.Facts.SetFlag(G.DynamiteTaken);
                    if (gateCharge != null) gateCharge.Active = true;
                };
            }
            if (gateCharge != null)
            {
                gateCharge.Configure("Poner la dinamita en el portón y encender la mecha", player.transform);
                gateCharge.OnUsed += _ => _gateFuse = C.GateFuseSeconds;
            }
            SetPlayerControl(false); // el prólogo: solo se mira y se escucha
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy()
        {
            Combatant.Downed -= OnDowned;
            WeaponPickup.PickedUp -= OnPickedUp;
        }

        private void SetPlayerControl(bool on)
        {
            player.enabled = on;
            var rifle = player.GetComponent<RifleController>();
            if (rifle != null) rifle.enabled = on;
            var melee = player.GetComponent<MeleeController>();
            if (melee != null) melee.enabled = on;
        }

        private void OnDowned(Combatant who, GameObject by)
        {
            if (_runner == null) return;
            if (who.IsPlayer)
            {
                _runner.Facts.SetFlag(G.PlayerDown);
                return;
            }
            if (who.Faction == Faction.Chile) return;
            RiflemanAI ai = who.GetComponent<RiflemanAI>();
            if (ai != null && _finishers.Contains(ai)) _runner.Facts.Add(F.FinishersDown);
            else _runner.Facts.Add(F.DefendersDown);
        }

        private void OnPickedUp(WeaponPickup pickup, FirstPersonController by)
        {
            if (by == player && pickup.Weapon == winchester) _runner.Facts.SetFlag(G.WinchesterTaken);
        }

        private float NearestFinisher(Vector3 at)
        {
            float best = float.PositiveInfinity;
            foreach (RiflemanAI f in _finishers)
            {
                if (f == null) continue;
                var c = f.GetComponent<Combatant>();
                if (c == null || !c.Alive) continue;
                best = Mathf.Min(best, Flat(f.transform.position, at));
            }
            return best;
        }

        private RiflemanAI Spawn(string name, Faction faction, Vector3 at, float heading, WeaponDataSO weapon, int cartridges, SoldierLook look) =>
            SoldierFactory.Create(name, faction, at, heading, weapon, cartridges, _seed++, look, _bayonetSpec);

        // ------------------------------------------------------------------------------------------
        // Bucle
        // ------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_runner == null) return;
            if (_runner.State != MissionState.Running)
            {
                if (_runner.State == MissionState.Complete && !_recorded)
                {
                    _recorded = true;
                    CampaignSave.Record(_runner.Script);
                }
                CampaignNavigator.HandleEndKeys(_runner.Script.Id, _runner.State == MissionState.Complete);
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (_stage == S.Assault) WeaponPickup.UpdatePlayer(player);
            if (_runner.Facts.Flag(G.PatrolBlast) && !_blastDone) PatrolBlast();
            if (_stage == S.Blood && _blastDone) Hail();
            if (_stage == S.Blood)
            {
                // Los tiradores van a por el herido, esté donde esté (también a cuestas de Quintín).
                foreach (RiflemanAI f in _finishers)
                {
                    if (f != null && wounded != null) f.Objective = wounded.Position;
                }
            }
            if (_gateFuse > 0f)
            {
                _gateFuse -= Time.deltaTime;
                if (_gateFuse <= 0f) BlowGate();
            }
            if (_stage == S.Dawn && moon != null)
            {
                _dawn = Mathf.Min(1f, _dawn + Time.deltaTime / 8f);
                moon.intensity = Mathf.Lerp(0.25f, 1.1f, _dawn);
                moon.color = Color.Lerp(new Color(0.55f, 0.62f, 0.85f), new Color(1f, 0.8f, 0.62f), _dawn);
            }

            _runner.Step(Time.deltaTime);
            if (_runner.CurrentStage.Id != _stage)
            {
                _stage = _runner.CurrentStage.Id;
                EnterStage(_stage);
            }
        }

        private void EnterStage(string stage)
        {
            if (stage == S.Silence) SetPlayerControl(true);
            else if (stage == S.Assault)
            {
                if (winchester != null && winchesterSpot != null) WeaponPickup.Spawn(winchester, 30, winchesterSpot.position);
                if (dynamite != null) dynamite.Active = true;
                for (int i = 0; i < allySpawns.Length; i++)
                {
                    bool culi = i % 2 == 1;
                    RiflemanAI ally = Spawn(culi ? "Culi_liberado_" + (i + 1) : "Soldado_chileno_" + (i + 1), Faction.Chile, allySpawns[i].position,
                                            allySpawns[i].eulerAngles.y, comblain, 40, culi ? culiLook : chileLook);
                    ally.Objective = gate != null ? gate.transform.position + new Vector3(Random.Range(-8f, 8f), 0f, -12f) : (Vector3?)null;
                    _allies.Add(ally);
                }
            }
        }

        /// <summary>La mina de la patrulla: el herido en el campo, la hacienda despierta y los dos tiradores.</summary>
        private void PatrolBlast()
        {
            _blastDone = true;
            if (patrolMine != null) patrolMine.Detonate();
            if (wounded != null) wounded.Activate(player, WoundedComrade.DefaultBleedSeconds);
            foreach (Transform post in wallPosts)
            {
                RiflemanAI defender = Spawn("Fusilero_peruano_" + _seed, Faction.Peru, post.position, post.eulerAngles.y, remington, 60, peruLook);
                defender.HoldPosition = true;
                defender.Brain.EngageRangeM = C.NightSightM; // de noche, solo ven a quien está cerca
            }
            foreach (Transform spawn in finisherSpawns)
            {
                RiflemanAI f = Spawn("Tirador_peruano_" + _seed, Faction.Peru, spawn.position, spawn.eulerAngles.y, remington, 20, peruLook);
                f.Brain.EngageRangeM = C.FinisherSightM; // van a por el herido: solo ven a quien tienen encima
                f.Objective = wounded != null ? wounded.Position : spawn.position;
                _finishers.Add(f);
            }
        }

        /// <summary>
        /// La «granizada de balas» del fuego cruzado: tierra que salta en el campo de tiro alrededor de Quintín. Es
        /// ambiente: el fuego que hiere es el de los fusileros que lo ven (<see cref="CaneteChapter.NightSightM"/>).
        /// </summary>
        private void Hail()
        {
            if (Time.time < _nextImpact || player == null || player.transform.position.z < 120f) return;
            _nextImpact = Time.time + Random.Range(0.25f, 0.6f);
            Vector3 p = player.transform.position + new Vector3(Random.Range(-9f, 9f), 0.05f, Random.Range(-9f, 9f));
            DustBurst.Spawn(p, blast, 0.9f);
        }

        /// <summary>El portón salta: la brecha se abre en el NavMesh y los aliados entran al patio.</summary>
        private void BlowGate()
        {
            _gateFuse = -1f;
            if (gate == null) return;
            Vector3 at = gate.transform.position;
            DustBurst.Spawn(at + Vector3.up * 1.5f, blast, 16f);
            gate.SetActive(false);
            if (NavMeshRuntimeBaker.Active != null) NavMeshRuntimeBaker.Active.Rebuild();
            Combatant[] nearby = new Combatant[Combatant.All.Count];
            for (int i = 0; i < nearby.Length; i++) nearby[i] = Combatant.All[i];
            foreach (Combatant c in nearby)
            {
                if (c == null || !c.Alive) continue;
                float d = Flat(c.transform.position, at);
                if (d <= GateBlastLethalM) c.Hurt(1000f, gameObject, at);
                else if (d <= GateBlastHurtM) c.Hurt(35f, gameObject, at);
            }
            _runner.Facts.SetFlag(G.GateBlown);
            foreach (RiflemanAI ally in _allies)
            {
                if (ally != null && courtyard != null) ally.Objective = courtyard.position + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            }
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ------------------------------------------------------------------------------------------
        // Interfaz (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            bool plateStage = _runner.CurrentStage.Perspective == P.Plate;
            if (plateStage && _runner.State == MissionState.Running) DrawPlate();
            else if (_playerCombatant != null && _playerCombatant.Vitality != null)
            {
                float hurt = Mathf.Clamp01(1f - (Time.time - _playerCombatant.LastHurtTime) / 0.8f);
                MissionHud.DrawFade(hurt * 0.45f, new Color(0.45f, 0f, 0f));
                DrawHealth();
            }

            if (_runner.State == MissionState.Running)
            {
                if (!plateStage) _hud.DrawObjectives(_runner, "CAPÍTULO 7", "Quintín Quintana · los culíes liberados");
                _hud.DrawDialogue(_runner, plateStage ? 40f : 190f);
                if (_gateFuse > 0f)
                {
                    EnsureStyles();
                    GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.35f, 400f, 30f), "¡Mecha encendida! Apártate: " + _gateFuse.ToString("0") + " s", _caption);
                }
            }
            else
            {
                _hud.DrawEnd(_runner, "CAPÍTULO 7", "Los candados de los galpones cuelgan rotos. Los culíes liberados marchan con la columna hacia Lima.");
            }
        }

        private void EnsureStyles()
        {
            if (_caption != null) return;
            _caption = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _caption.normal.textColor = new Color(0.92f, 0.88f, 0.78f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _small.normal.textColor = Color.white;
        }

        private void DrawPlate()
        {
            EnsureStyles();
            MissionHud.DrawFade(1f, Color.black);
            if (plate == null) return;
            float aspect = plate.width / (float)plate.height;
            float h = Screen.height * 0.66f, w = h * aspect;
            if (w > Screen.width * 0.9f)
            {
                w = Screen.width * 0.9f;
                h = w / aspect;
            }
            GUI.color = new Color(1f, 0.95f, 0.86f, Mathf.Clamp01(_runner.StageTime / 1.5f));
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.04f, w, h), plate, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.04f + h + 8f, Screen.width * 0.8f, 44f), plateCaption, _caption);
        }

        private void DrawHealth()
        {
            EnsureStyles();
            float f = _playerCombatant.Vitality.Fraction;
            var back = new Rect(Screen.width - 232f, 16f, 216f, 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.8f, 0.1f, 0.05f), new Color(0.85f, 0.8f, 0.6f), f);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (back.width - 4f) * f, back.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(back.x, back.yMax + 2f, 216f, 20f), "Salud " + (f * 100f).ToString("0"), _small);
        }
    }
}
