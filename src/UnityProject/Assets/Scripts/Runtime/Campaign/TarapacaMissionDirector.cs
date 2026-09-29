using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;
using Pacifico.Data;
using Pacifico.Infantry;
using Pacifico.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = Pacifico.Core.Campaign.TarapacaChapter.Facts;
using G = Pacifico.Core.Campaign.TarapacaChapter.Flags;
using S = Pacifico.Core.Campaign.TarapacaChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 4 en escena (ROADMAP 6.2). La lógica narrativa es <see cref="TarapacaChapter"/>; aquí se
    /// crean los soldados de cada momento (el Zepita, la vanguardia chilena que baja las laderas, los sirvientes de los
    /// Krupp), se traducen los sucesos a hechos (caídos, cartuchos, fusil cambiado, piezas tomadas, el tambor, la
    /// columna) y se dibujan objetivos, diálogos, la salud y el daño del jugador.
    /// </summary>
    public sealed class TarapacaMissionDirector : MonoBehaviour
    {
        [Header("Guion (sección del capítulo 4, copiada por el constructor de la escena)")]
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;

        [Header("Jugador")]
        [SerializeField] private FirstPersonController player;

        [Header("Armas")]
        [SerializeField] private WeaponDataSO chassepot;
        [SerializeField] private WeaponDataSO comblain;
        [SerializeField] private WeaponDataSO bayonet;

        [Header("Lugares")]
        [SerializeField] private Transform plaza;
        [SerializeField] private float plazaRadius = 10f;
        [SerializeField] private Transform[] allySpawns = new Transform[0];
        [SerializeField] private Transform[] chileSpawns = new Transform[0];
        [SerializeField] private Transform[] kruppEscortSpawns = new Transform[0];
        [SerializeField] private KruppGun[] kruppGuns = new KruppGun[0];
        [SerializeField] private WoundedDrummer drummer;
        [SerializeField] private Transform column;
        [SerializeField] private float columnRadius = 12f;

        [Header("Aspecto")]
        [SerializeField] private SoldierLook chileLook = new SoldierLook();
        [SerializeField] private SoldierLook peruLook = new SoldierLook();

        /// <summary>Soldados de la vanguardia chilena en el primer empuje y en el segundo.</summary>
        private const int FirstWave = 6;
        private const int ChileanCartridges = 40;
        private const int AllyCartridges = 25;
        /// <summary>
        /// La vanguardia no se para a tirar desde lo alto: baja la ladera y entra en el pueblo antes de detenerse (m).
        /// Ajustado con una simulación de la escaramuza (ver DEV_LOG, tarea 6.2).
        /// </summary>
        private const float AssaultEngageRangeM = 70f;

        private MissionRunner _runner;
        private Combatant _playerCombatant;
        private RifleController _rifle;
        private WeaponSpec _bayonetSpec;
        private readonly List<RiflemanAI> _allies = new List<RiflemanAI>();
        private readonly List<RiflemanAI> _chileans = new List<RiflemanAI>();
        private readonly MissionHud _hud = new MissionHud();
        private string _stage;
        private bool _secondWave;
        private bool _recorded;
        private int _seed = 1879;
        private float _fadeIn = 1f;
        private GUIStyle _small;

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, FirstPersonController fps, WeaponDataSO peruRifle, WeaponDataSO chileRifle, WeaponDataSO bayonetData,
                              Transform plazaPoint, Transform[] allies, Transform[] chile, Transform[] escort, KruppGun[] guns,
                              WoundedDrummer wounded, Transform columnPoint, SoldierLook chilean, SoldierLook allied)
        {
            guionChapter = chapterMarkdown;
            player = fps;
            chassepot = peruRifle;
            comblain = chileRifle;
            bayonet = bayonetData;
            plaza = plazaPoint;
            allySpawns = allies;
            chileSpawns = chile;
            kruppEscortSpawns = escort;
            kruppGuns = guns;
            drummer = wounded;
            column = columnPoint;
            chileLook = chilean;
            peruLook = allied;
        }

        private void Start()
        {
            _runner = new MissionRunner(TarapacaChapter.Build(GuionQuotes.Extract(guionChapter, TarapacaChapter.ChapterHeading)));
            _playerCombatant = player.GetComponent<Combatant>();
            _rifle = player.GetComponent<RifleController>();
            _bayonetSpec = bayonet != null ? bayonet.ToSpec() : null;

            Combatant.Downed += OnDowned;
            WeaponPickup.PickedUp += OnPickedUp;
            foreach (KruppGun gun in kruppGuns) gun.Captured += _ => _runner.Facts.Add(F.GunsTaken);
            if (drummer != null)
            {
                drummer.OnFound += _ => _runner.Facts.SetFlag(G.DrummerFound);
                drummer.OnWatered += _ => _runner.Facts.SetFlag(G.WaterGiven);
            }

            for (int i = 0; i < allySpawns.Length; i++)
            {
                RiflemanAI ally = Spawn("Zepita_" + (i + 1), Faction.Peru, allySpawns[i], chassepot, AllyCartridges, peruLook);
                ally.Objective = plaza.position + Offset(i, 4f);
                _allies.Add(ally);
            }
            SpawnWave(0, FirstWave);
            _stage = _runner.CurrentStage.Id;
        }

        private void OnDestroy()
        {
            Combatant.Downed -= OnDowned;
            WeaponPickup.PickedUp -= OnPickedUp;
        }

        private RiflemanAI Spawn(string name, Faction faction, Transform at, WeaponDataSO weapon, int cartridges, SoldierLook look) =>
            SoldierFactory.Create(name, faction, at.position, at.eulerAngles.y, weapon, cartridges, _seed++, look, _bayonetSpec);

        private void SpawnWave(int from, int count)
        {
            for (int i = from; i < from + count && chileSpawns.Length > 0; i++)
            {
                Transform at = chileSpawns[i % chileSpawns.Length];
                RiflemanAI soldier = Spawn("Segundo_de_Linea_" + (i + 1), Faction.Chile, at, comblain, ChileanCartridges, chileLook);
                soldier.Objective = plaza.position + Offset(i, 8f);
                soldier.Brain.EngageRangeM = AssaultEngageRangeM;
                _chileans.Add(soldier);
            }
        }

        /// <summary>Puestos repartidos alrededor de un punto (para que no se amontonen en el mismo metro).</summary>
        private static Vector3 Offset(int i, float radius)
        {
            float a = i * 2.39996f; // ángulo áureo
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Mathf.Sqrt((i % 5 + 1) / 5f);
        }

        private void OnDowned(Combatant who, GameObject by)
        {
            if (who.IsPlayer) _runner.Facts.SetFlag(G.PlayerDown);
            else if (who.Faction == Faction.Chile) _runner.Facts.Add(F.EnemiesDown);
        }

        private void OnPickedUp(WeaponPickup pickup, FirstPersonController fps)
        {
            if (fps == player && fps.Weapon != null && fps.Weapon.Id != WeaponCatalog.ChassepotId) _runner.Facts.SetFlag(G.SwappedRifle);
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
                CampaignNavigator.HandleEndKeys(_runner.Script.Id, _runner.State == MissionState.Complete);
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            WeaponPickup.UpdatePlayer(player);
            if (_rifle != null && _rifle.Model != null) _runner.Facts.Set(F.Rounds, _rifle.Model.TotalRounds);
            if (!_runner.Facts.Flag(G.AtRally) && Flat(player.transform.position, plaza.position) <= plazaRadius) _runner.Facts.SetFlag(G.AtRally);
            if (!_secondWave && _runner.Facts.Get(F.EnemiesDown) >= 3)
            {
                _secondWave = true;
                SpawnWave(FirstWave, TarapacaChapter.DefaultVillageEnemies - FirstWave);
            }
            if (_stage == S.Aftermath && !_runner.Facts.Flag(G.ReachedColumn) && Flat(player.transform.position, column.position) <= columnRadius)
            {
                _runner.Facts.SetFlag(G.ReachedColumn);
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
            if (stage == S.Village)
            {
                foreach (KruppGun gun in kruppGuns) gun.Firing = true; // desde la pampa baten la quebrada
            }
            else if (stage == S.Krupp)
            {
                // Sirvientes de cada pieza (no se mueven) y la escolta que baja a contener la carga.
                int n = 0;
                foreach (KruppGun gun in kruppGuns)
                {
                    gun.Capturable = true;
                    for (int k = 0; k < 2; k++)
                    {
                        var at = new GameObject("Puesto").transform;
                        at.position = gun.transform.position + gun.transform.right * (k == 0 ? -2.5f : 2.5f) - gun.transform.forward * 1.5f;
                        at.rotation = gun.transform.rotation;
                        RiflemanAI crew = Spawn("Artillero_" + (++n), Faction.Chile, at, comblain, 10, chileLook);
                        crew.HoldPosition = true;
                        Destroy(at.gameObject);
                    }
                }
                for (int i = 0; i < kruppEscortSpawns.Length; i++)
                {
                    RiflemanAI escort = Spawn("Escolta_" + (i + 1), Faction.Chile, kruppEscortSpawns[i], comblain, ChileanCartridges, chileLook);
                    escort.Objective = kruppGuns.Length > 0 ? kruppGuns[i % kruppGuns.Length].transform.position : escort.transform.position;
                }
                // Cáceres: «¡a la bayoneta!». El Zepita sube con el jugador.
                for (int i = 0; i < _allies.Count; i++)
                {
                    if (_allies[i] == null) continue;
                    _allies[i].ChargeOrdered = true;
                    if (kruppGuns.Length > 0) _allies[i].Objective = kruppGuns[i % kruppGuns.Length].transform.position + Offset(i, 3f);
                }
            }
            else if (stage == S.Aftermath)
            {
                foreach (KruppGun gun in kruppGuns) gun.Firing = false;
                foreach (RiflemanAI ally in _allies)
                {
                    if (ally == null) continue;
                    ally.ChargeOrdered = false;
                    ally.Objective = column.position + Offset(_allies.IndexOf(ally), 5f);
                }
                if (drummer != null) drummer.Active = true;
            }
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        // ------------------------------------------------------------------------------------------
        // Interfaz
        // ------------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;

            // Daño: el borde de la pantalla se enrojece al recibirlo y, con poca salud, se queda teñido.
            if (_playerCombatant != null && _playerCombatant.Vitality != null)
            {
                float hurt = Mathf.Clamp01(1f - (Time.time - _playerCombatant.LastHurtTime) / 0.8f);
                float low = Mathf.Clamp01((0.45f - _playerCombatant.Vitality.Fraction) / 0.45f) * 0.35f;
                MissionHud.DrawFade(Mathf.Max(hurt * 0.45f, low), new Color(0.45f, 0f, 0f));
                DrawHealth();
            }

            if (_runner.State == MissionState.Running)
            {
                _hud.DrawObjectives(_runner, "CAPÍTULO 4", "Soldado Mariano Santos · batallón Zepita (Cáceres)");
                _hud.DrawDialogue(_runner, 190f);
            }
            else
            {
                string detail = _runner.Facts.Flag(G.WaterGiven)
                    ? "El tambor bebió de tu caramayola antes de morir. La columna sigue a pie hacia Arica."
                    : "La columna sigue a pie hacia Arica.";
                _hud.DrawEnd(_runner, "CAPÍTULO 4", detail);
            }
            MissionHud.DrawFade(_fadeIn, Color.black);
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
            string canteen = _runner.Facts.Flag(G.WaterGiven) ? "Caramayola: vacía" : "Caramayola: un resto de agua";
            GUI.Label(new Rect(back.x, back.yMax + 2f, 216f, 20f), "Salud " + (f * 100f).ToString("0") + " · " + canteen, _small);
        }
    }
}
