using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Input;
using Pacifico.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = Pacifico.Core.Campaign.MirafloresChapter.Facts;
using G = Pacifico.Core.Campaign.MirafloresChapter.Flags;
using S = Pacifico.Core.Campaign.MirafloresChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 8 en escena (ROADMAP 6.5). El guion es <see cref="MirafloresChapter"/>. Crea la escuadra de
    /// Abraham Quiroz, los defensores civiles de los jardines y del Reducto N.º 3 y el bombardeo; marca el momento del
    /// rostro del enemigo (el primer defensor que el jugador derriba dentro del reducto: la acción se ralentiza y aparece
    /// el muchacho); y conduce el desenlace: sentarse contra el parapeto, la carta, el disparo rezagado, la vista que se
    /// nubla y cae, la cámara que se eleva, el negro y el epílogo «La memoria rota».
    /// </summary>
    public sealed class MirafloresMissionDirector : MonoBehaviour
    {
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;
        [SerializeField, TextArea(3, 8)] private string epilogueSection = string.Empty;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private WeaponDataSO comblain;
        [SerializeField] private WeaponDataSO defenderRifle;
        [SerializeField] private WeaponDataSO bayonet;
        [SerializeField] private Texture2D prologueImage;
        [SerializeField] private Transform[] squadSpawns = new Transform[0];
        [SerializeField] private Transform[] gardenPosts = new Transform[0];
        [SerializeField] private Transform[] redoubtPosts = new Transform[0];
        [SerializeField] private Transform redoubt;
        [SerializeField] private float redoubtRadius = 22f;
        [SerializeField] private KruppGun[] redoubtGuns = new KruppGun[0];
        [SerializeField] private AmbientBombardment bombardment;
        [SerializeField] private InteractionPoint seat;
        [SerializeField] private GameObject boy;
        [SerializeField] private Transform flag;
        [SerializeField] private EpiloguePlayer epilogue;
        [SerializeField] private SoldierLook chileLook = new SoldierLook();
        [SerializeField] private SoldierLook civilLook = new SoldierLook();

        private MissionRunner _runner;
        private Combatant _playerCombatant;
        private WeaponSpec _bayonetSpec;
        private readonly HashSet<Combatant> _redoubtDefenders = new HashSet<Combatant>();
        private readonly MissionHud _hud = new MissionHud();
        private string _stage;
        private int _seed = 1881;
        private bool _recorded;
        private float _slowUntil = -1f;
        private float _flagFall;
        private float _seatedTime = -1f;
        private float _shotTime = -1f;
        private Transform _pivot;
        private Vector3 _pivotStart;
        private GUIStyle _paper;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, string epilogueMarkdown, FirstPersonController fps, WeaponDataSO chile, WeaponDataSO peru, WeaponDataSO bayonetData,
                              Texture2D prologue, Transform[] squad, Transform[] garden, Transform[] redoubtDefence, Transform redoubtPoint, KruppGun[] guns,
                              AmbientBombardment shells, InteractionPoint sitDown, GameObject volunteerBoy, Transform flagPole, EpiloguePlayer epiloguePlayer,
                              SoldierLook chilean, SoldierLook civilian)
        {
            guionChapter = chapterMarkdown;
            epilogueSection = epilogueMarkdown;
            player = fps;
            comblain = chile;
            defenderRifle = peru;
            bayonet = bayonetData;
            prologueImage = prologue;
            squadSpawns = squad;
            gardenPosts = garden;
            redoubtPosts = redoubtDefence;
            redoubt = redoubtPoint;
            redoubtGuns = guns;
            bombardment = shells;
            seat = sitDown;
            boy = volunteerBoy;
            flag = flagPole;
            epilogue = epiloguePlayer;
            chileLook = chilean;
            civilLook = civilian;
        }

        private void Start()
        {
            _runner = new MissionRunner(MirafloresChapter.Build(GuionQuotes.Extract(guionChapter, MirafloresChapter.ChapterHeading)));
            _playerCombatant = player.GetComponent<Combatant>();
            _bayonetSpec = bayonet != null ? bayonet.ToSpec() : null;
            _pivot = player.ViewCamera != null ? player.ViewCamera.transform.parent : null;
            if (_pivot != null) _pivotStart = _pivot.localPosition;
            Combatant.Downed += OnDowned;
            foreach (KruppGun gun in redoubtGuns) gun.Captured += OnGunSilenced;
            if (seat != null) seat.OnUsed += _ => _runner.Facts.SetFlag(G.Seated);
            if (epilogue != null) epilogue.Finished += () => _runner.Facts.SetFlag(G.EpilogueFinished);
            if (boy != null) boy.SetActive(false);
            if (bombardment != null) bombardment.Firing = false;
            SetPlayerControl(false); // el prólogo del corresponsal
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy()
        {
            Combatant.Downed -= OnDowned;
            Time.timeScale = 1f;
        }

        private void SetPlayerControl(bool on)
        {
            player.enabled = on;
            var rifle = player.GetComponent<RifleController>();
            if (rifle != null) rifle.enabled = on;
            var melee = player.GetComponent<MeleeController>();
            if (melee != null) melee.enabled = on;
        }

        private RiflemanAI Spawn(string name, Faction faction, Transform at, WeaponDataSO weapon, int cartridges, SoldierLook look) =>
            SoldierFactory.Create(name, faction, at.position, at.eulerAngles.y, weapon, cartridges, _seed++, look, _bayonetSpec);

        private void OnDowned(Combatant who, GameObject by)
        {
            if (_runner == null) return;
            if (who.IsPlayer)
            {
                _runner.Facts.SetFlag(G.PlayerDown);
                return;
            }
            if (who.Faction == Faction.Chile) return;
            _runner.Facts.Add(F.DefendersDown);
            if (!_redoubtDefenders.Contains(who)) return;
            _runner.Facts.Add(F.RedoubtDown);
            // El rostro del enemigo: el primero que Abraham derriba dentro del reducto.
            if (by == player.gameObject && !_runner.Facts.Flag(G.EnemyFace))
            {
                _runner.Facts.SetFlag(G.EnemyFace);
                _slowUntil = Time.unscaledTime + 3f;
                Time.timeScale = 0.35f;
                if (boy != null)
                {
                    boy.transform.position = who.transform.position + who.transform.right * 1.2f;
                    boy.SetActive(true);
                }
            }
        }

        private void OnGunSilenced(KruppGun gun)
        {
            _runner.Facts.Add(F.GunsSilenced);
        }

        private void Update()
        {
            if (_runner == null) return;
            if (_slowUntil > 0f && Time.unscaledTime >= _slowUntil)
            {
                _slowUntil = -1f;
                Time.timeScale = 1f;
            }
            if (_runner.State != MissionState.Running)
            {
                if (_runner.State == MissionState.Complete && !_recorded)
                {
                    _recorded = true;
                    CampaignSave.Record(_runner.Script);
                }
                CampaignNavigator.HandleEndKeys(_runner.Script.Id, _runner.State == MissionState.Complete);
                if (GameInput.Pressed(GameKey.R))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                return;
            }

            if (_stage == S.Gardens || _stage == S.Redoubt || _stage == S.Guns) WeaponPickup.UpdatePlayer(player);
            if (_stage == S.Gardens && redoubt != null && Flat(player.transform.position, redoubt.position) <= redoubtRadius + 10f) _runner.Facts.SetFlag(G.AtRedoubt);
            if (_runner.Facts.Get(F.GunsSilenced) >= MirafloresChapter.RedoubtGuns && flag != null && _flagFall < 1f)
            {
                _flagFall = Mathf.Min(1f, _flagFall + Time.deltaTime / 2f);
                flag.localRotation = Quaternion.Euler(0f, 0f, Mathf.SmoothStep(0f, 88f, _flagFall));
                if (bombardment != null) bombardment.Firing = false;
            }
            UpdateEnding();

            _runner.Step(Time.deltaTime);
            if (_runner.CurrentStage.Id != _stage)
            {
                _stage = _runner.CurrentStage.Id;
                EnterStage(_stage);
            }
        }

        private void EnterStage(string stage)
        {
            if (stage == S.Gardens)
            {
                SetPlayerControl(true);
                if (bombardment != null) bombardment.Firing = true;
                for (int i = 0; i < squadSpawns.Length; i++)
                {
                    RiflemanAI mate = Spawn("3.º de Línea_" + (i + 1), Faction.Chile, squadSpawns[i], comblain, 60, chileLook);
                    mate.Objective = redoubt.position + new Vector3((i - 2) * 3f, 0f, -12f);
                    mate.Brain.EngageRangeM = 120f;
                }
                for (int i = 0; i < gardenPosts.Length; i++)
                {
                    RiflemanAI defender = Spawn("Reservista_" + (i + 1), Faction.Peru, gardenPosts[i], defenderRifle, 25, civilLook);
                    defender.HoldPosition = true;
                }
                foreach (KruppGun gun in redoubtGuns) gun.Firing = true;
            }
            else if (stage == S.Redoubt)
            {
                for (int i = 0; i < redoubtPosts.Length; i++)
                {
                    RiflemanAI defender = Spawn("Defensor_del_Reducto_" + (i + 1), Faction.Peru, redoubtPosts[i], defenderRifle, 20, civilLook);
                    defender.HoldPosition = true;
                    defender.ChargeWithinM = 6f; // forcejeo a la bayoneta dentro de la trinchera
                    _redoubtDefenders.Add(defender.GetComponent<Combatant>());
                }
            }
            else if (stage == S.Guns)
            {
                foreach (KruppGun gun in redoubtGuns) gun.Capturable = true;
            }
            else if (stage == S.Ending)
            {
                foreach (KruppGun gun in redoubtGuns) gun.Firing = false;
                if (seat != null) seat.Active = true;
            }
        }

        /// <summary>Sentado: sin mandos, la vista baja; tras el disparo, tiembla, se nubla, cae de lado y se eleva.</summary>
        private void UpdateEnding()
        {
            if (!_runner.Facts.Flag(G.Seated) || _pivot == null) return;
            if (_seatedTime < 0f)
            {
                _seatedTime = Time.time;
                SetPlayerControl(false);
                if (seat != null) player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(seat.transform.forward, Vector3.up));
            }
            float sit = Mathf.Clamp01((Time.time - _seatedTime) / 1.5f);
            _pivot.localPosition = Vector3.Lerp(_pivotStart, _pivotStart + Vector3.down * 0.75f, Mathf.SmoothStep(0f, 1f, sit));

            if (!_runner.Facts.Flag(G.Shot)) return;
            if (_shotTime < 0f)
            {
                _shotTime = Time.time;
                if (_playerCombatant != null) _playerCombatant.Hurt(1000f, null, player.transform.position + player.transform.forward * 300f);
            }
            float t = Time.time - _shotTime;
            float shake = t < 0.5f ? (0.5f - t) * 6f : 0f;
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1f) / 3f));
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 5f) / 7f));
            _pivot.localRotation = Quaternion.Euler(Random.Range(-shake, shake), Random.Range(-shake, shake), fall * 80f);
            if (rise > 0f)
            {
                // Plano cenital: la cámara se separa del cuerpo y sube mirando hacia abajo.
                Transform cam = player.ViewCamera.transform;
                Vector3 body = player.transform.position;
                cam.position = Vector3.Lerp(_pivot.position, body + Vector3.up * 22f, rise);
                cam.rotation = Quaternion.Slerp(_pivot.rotation, Quaternion.LookRotation(Vector3.down, player.transform.forward), rise);
            }
            if (t >= 13f && epilogue != null && !epilogue.IsPlaying && !_runner.Facts.Flag(G.EpilogueFinished) && !_epilogueStarted)
            {
                _epilogueStarted = true;
                epilogue.Play(MemoriaRotaEpilogue.Parse(epilogueSection));
            }
        }

        private bool _epilogueStarted;

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ------------------------------------------------------------------------------------------
        // Interfaz
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            if (_epilogueStarted)
            {
                // Tras el epílogo, en negro, solo la salida discreta al menú.
                if (_runner.State == MissionState.Complete && epilogue != null && !epilogue.IsPlaying)
                {
                    GUI.depth = -400;
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    GUI.Label(new Rect(0f, Screen.height - 40f, Screen.width - 20f, 24f), "[M] menú   ", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight });
                    GUI.color = Color.white;
                }
                return;
            }
            GUI.depth = -100;
            bool seated = _runner.Facts.Flag(G.Seated);
            if (_stage == S.Prologue && _runner.State == MissionState.Running)
            {
                DrawImage(prologueImage);
                _hud.DrawDialogue(_runner, 140f);
                return;
            }
            if (!seated && _playerCombatant != null && _playerCombatant.Vitality != null)
            {
                float hurt = Mathf.Clamp01(1f - (Time.time - _playerCombatant.LastHurtTime) / 0.8f);
                MissionHud.DrawFade(hurt * 0.45f, new Color(0.45f, 0f, 0f));
            }
            if (_runner.State == MissionState.Running)
            {
                if (!seated) _hud.DrawObjectives(_runner, "CAPÍTULO 8", "Soldado Abraham Quiroz · 3.º de Línea");
                _hud.DrawDialogue(_runner, 190f);
                if (seated && _shotTime < 0f) DrawLetter();
            }
            else if (_runner.State == MissionState.Failed)
            {
                _hud.DrawEnd(_runner, "CAPÍTULO 8", string.Empty);
            }

            if (_shotTime >= 0f)
            {
                // El disparo: destello rojo, túnel que se cierra y fundido a negro.
                float t = Time.time - _shotTime;
                MissionHud.DrawFade(Mathf.Clamp01(1f - t / 1.5f) * 0.55f, new Color(0.6f, 0f, 0f));
                DrawTunnel(Mathf.Clamp01(t / 5f));
                MissionHud.DrawFade(Mathf.Clamp01((t - 9f) / 4f), Color.black);
            }
        }

        private void DrawImage(Texture2D image)
        {
            MissionHud.DrawFade(1f, Color.black);
            if (image == null) return;
            float zoom = 1f + 0.08f * Mathf.Clamp01(_runner.StageTime / 40f);
            GUI.color = new Color(1f, 0.9f, 0.74f, Mathf.Clamp01(_runner.StageTime / 1.5f));
            float h = Screen.height * 0.72f * zoom, w = Mathf.Min(Screen.width * 0.9f * zoom, h * image.width / (float)image.height);
            GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.35f, w, h), image, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }

        private void DrawLetter()
        {
            if (_paper == null)
            {
                _paper = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = true };
                _paper.normal.textColor = new Color(0.25f, 0.18f, 0.1f);
            }
            float a = Mathf.Clamp01((Time.time - _seatedTime - 3f) / 1.5f);
            var rect = new Rect(Screen.width * 0.5f - 170f, Screen.height - 150f, 340f, 110f);
            GUI.color = new Color(0.88f, 0.82f, 0.68f, a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(rect, "Señor don Luciano Quiroz\nQuillota", _paper);
            GUI.color = Color.white;
        }

        /// <summary>Visión de túnel: el borde de la pantalla se oscurece hacia el centro.</summary>
        private static void DrawTunnel(float amount)
        {
            if (amount <= 0f) return;
            float border = Mathf.Lerp(0f, 0.42f, amount);
            GUI.color = new Color(0f, 0f, 0f, 0.85f * amount);
            float w = Screen.width, h = Screen.height;
            GUI.DrawTexture(new Rect(0f, 0f, w, h * border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, h * (1f - border), w, h * border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, 0f, w * border, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(w * (1f - border), 0f, w * border, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
