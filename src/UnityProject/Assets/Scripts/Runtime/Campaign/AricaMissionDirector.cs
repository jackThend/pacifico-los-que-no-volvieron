using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = Pacifico.Core.Campaign.AricaChapter.Facts;
using G = Pacifico.Core.Campaign.AricaChapter.Flags;
using S = Pacifico.Core.Campaign.AricaChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 6 en escena (ROADMAP 6.4). El guion es <see cref="AricaChapter"/>. La junta se muestra
    /// sobre la lámina del Archivo con la respuesta de Bolognesi; después el jugador defiende el parapeto con los
    /// Artesanos de Tacna y la Gatling, prueba el detonador de las minas (los cables están cortados), retrocede hasta la
    /// explanada y resiste junto a la bandera mientras caen Bolognesi y sus oficiales y Ugarte salta al vacío.
    /// </summary>
    public sealed class AricaMissionDirector : MonoBehaviour
    {
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private WeaponDataSO peruRifle;
        [SerializeField] private WeaponDataSO chileRifle;
        [SerializeField] private WeaponDataSO bayonet;
        [SerializeField] private Texture2D councilImage;
        [SerializeField] private Transform[] defenderPosts = new Transform[0];
        [SerializeField] private Transform[] assaultSpawns = new Transform[0];
        [SerializeField] private Transform parapet;
        [SerializeField] private Transform summit;
        [SerializeField] private float summitRadius = 14f;
        [SerializeField] private GatlingGun gatling;
        [SerializeField] private InteractionPoint detonator;
        [SerializeField] private Transform[] officers = new Transform[0];
        [SerializeField] private ScriptedRider ugarte;
        [SerializeField] private SoldierLook chileLook = new SoldierLook();
        [SerializeField] private SoldierLook peruLook = new SoldierLook();

        private const float AssaultEngageRangeM = 60f;
        /// <summary>A esta distancia, el asaltante deja de tirar y carga a la bayoneta («combate a quemarropa»).</summary>
        private const float AssaultChargeWithinM = 40f;

        private MissionRunner _runner;
        private Combatant _playerCombatant;
        private WeaponSpec _bayonetSpec;
        private readonly List<RiflemanAI> _defenders = new List<RiflemanAI>();
        private readonly MissionHud _hud = new MissionHud();
        private string _stage;
        private int _wave;
        private int _seed = 1880;
        private bool _recorded;
        private float _officersFall;
        private GUIStyle _small;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, FirstPersonController fps, WeaponDataSO peru, WeaponDataSO chile, WeaponDataSO bayonetData,
                              Texture2D council, Transform[] posts, Transform[] spawns, Transform parapetPoint, Transform summitPoint,
                              GatlingGun gun, InteractionPoint mines, Transform[] staff, ScriptedRider rider, SoldierLook chilean, SoldierLook peruvian)
        {
            guionChapter = chapterMarkdown;
            player = fps;
            peruRifle = peru;
            chileRifle = chile;
            bayonet = bayonetData;
            councilImage = council;
            defenderPosts = posts;
            assaultSpawns = spawns;
            parapet = parapetPoint;
            summit = summitPoint;
            gatling = gun;
            detonator = mines;
            officers = staff;
            ugarte = rider;
            chileLook = chilean;
            peruLook = peruvian;
        }

        private void Start()
        {
            _runner = new MissionRunner(AricaChapter.Build(GuionQuotes.Extract(guionChapter, AricaChapter.ChapterHeading)));
            _playerCombatant = player.GetComponent<Combatant>();
            _bayonetSpec = bayonet != null ? bayonet.ToSpec() : null;
            Combatant.Downed += OnDowned;
            if (detonator != null) detonator.OnUsed += _ => _runner.Facts.SetFlag(G.DetonatorTried);
            SetPlayerControl(false); // la junta: solo se mira y se escucha
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy() => Combatant.Downed -= OnDowned;

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
            if (who.IsPlayer) _runner.Facts.SetFlag(G.PlayerDown);
            else if (who.Faction == Faction.Chile)
            {
                _runner.Facts.Add(F.EnemiesDown);
                // El asalto no se detiene: por cada tres que caen en el parapeto, sube otra oleada.
                if (_stage == S.Parapet && (int)_runner.Facts.Get(F.EnemiesDown) % 3 == 0) SpawnWave(3, parapet.position);
            }
        }

        private RiflemanAI Spawn(string name, Faction faction, Transform at, WeaponDataSO weapon, int cartridges, SoldierLook look) =>
            SoldierFactory.Create(name, faction, at.position, at.eulerAngles.y, weapon, cartridges, _seed++, look, _bayonetSpec);

        private void SpawnWave(int count, Vector3 objective)
        {
            for (int i = 0; i < count && assaultSpawns.Length > 0; i++)
            {
                _wave++;
                Transform at = assaultSpawns[_wave % assaultSpawns.Length];
                RiflemanAI soldier = Spawn((_wave % 2 == 0 ? "3.º de Línea_" : "4.º de Línea_") + _wave, Faction.Chile, at, chileRifle, 30, chileLook);
                soldier.Objective = objective + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                soldier.Brain.EngageRangeM = AssaultEngageRangeM;
                soldier.ChargeWithinM = AssaultChargeWithinM;
            }
        }

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
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (_stage != S.Council) WeaponPickup.UpdatePlayer(player);
            if (_stage == S.Withdraw && Flat(player.transform.position, summit.position) <= summitRadius) _runner.Facts.SetFlag(G.AtSummit);
            if (_runner.Facts.Flag(G.BolognesiFalls) && _officersFall < 1f)
            {
                _officersFall = Mathf.Min(1f, _officersFall + Time.deltaTime / 1.2f);
                foreach (Transform officer in officers) officer.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 88f, _officersFall), officer.localEulerAngles.y, 0f);
            }
            if (_runner.Facts.Flag(G.UgarteLeaps) && ugarte != null && !ugarte.Riding) ugarte.Riding = true;

            _runner.Step(Time.deltaTime);
            if (_runner.CurrentStage.Id != _stage)
            {
                _stage = _runner.CurrentStage.Id;
                EnterStage(_stage);
            }
        }

        private void EnterStage(string stage)
        {
            if (stage == S.Parapet)
            {
                SetPlayerControl(true);
                for (int i = 0; i < defenderPosts.Length; i++)
                {
                    RiflemanAI defender = Spawn("Artesanos_de_Tacna_" + (i + 1), Faction.Peru, defenderPosts[i], peruRifle, 40, peruLook);
                    defender.HoldPosition = true;
                    _defenders.Add(defender);
                }
                SpawnWave(10, parapet.position);
                if (gatling != null) gatling.Firing = true;
                if (detonator != null) detonator.Active = true;
            }
            else if (stage == S.Withdraw)
            {
                if (gatling != null) gatling.Firing = false; // rebasada
                if (detonator != null) detonator.Active = false;
                foreach (RiflemanAI d in _defenders)
                {
                    if (d == null) continue;
                    d.HoldPosition = false;
                    d.Objective = summit.position + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                }
                SpawnWave(6, summit.position);
            }
            else if (stage == S.Summit)
            {
                SpawnWave(8, summit.position);
            }
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            if (_stage == S.Council && _runner.State == MissionState.Running) DrawCouncil();
            else if (_playerCombatant != null && _playerCombatant.Vitality != null)
            {
                float hurt = Mathf.Clamp01(1f - (Time.time - _playerCombatant.LastHurtTime) / 0.8f);
                float low = Mathf.Clamp01((0.45f - _playerCombatant.Vitality.Fraction) / 0.45f) * 0.35f;
                MissionHud.DrawFade(Mathf.Max(hurt * 0.45f, low), new Color(0.45f, 0f, 0f));
                DrawHealth();
            }

            if (_runner.State == MissionState.Running)
            {
                if (_stage != S.Council) _hud.DrawObjectives(_runner, "CAPÍTULO 6", "Soldado Manuel Salazar · batallón Artesanos de Tacna");
                _hud.DrawDialogue(_runner, _stage == S.Council ? 140f : 190f);
            }
            else
            {
                _hud.DrawEnd(_runner, "CAPÍTULO 6", "Cincuenta y cinco minutos. La guarnición del Morro salvó el honor de sus armas.");
            }
        }

        /// <summary>La junta: la lámina de la respuesta de Bolognesi en sepia, acercándose despacio.</summary>
        private void DrawCouncil()
        {
            MissionHud.DrawFade(1f, Color.black);
            if (councilImage == null) return;
            float zoom = 1f + 0.08f * Mathf.Clamp01(_runner.StageTime / 30f);
            float aspect = councilImage.width / (float)councilImage.height;
            float h = Screen.height * 0.78f * zoom, w = h * aspect;
            if (w > Screen.width * 0.9f * zoom)
            {
                w = Screen.width * 0.9f * zoom;
                h = w / aspect;
            }
            GUI.color = new Color(1f, 0.9f, 0.74f, Mathf.Clamp01(_runner.StageTime / 1.5f));
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.4f, w, h), councilImage, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }

        private void DrawHealth()
        {
            if (_small == null)
            {
                _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _small.normal.textColor = Color.white;
            }
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
