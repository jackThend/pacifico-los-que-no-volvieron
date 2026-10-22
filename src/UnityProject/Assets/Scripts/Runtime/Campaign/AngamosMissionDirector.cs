using Pacifico.Core.Campaign;
using Pacifico.Core.Naval;
using Pacifico.Input;
using Pacifico.Naval;
using UnityEngine;
using UnityEngine.SceneManagement;
using C = Pacifico.Core.Campaign.AngamosChapter;
using F = Pacifico.Core.Campaign.AngamosChapter.Facts;
using G = Pacifico.Core.Campaign.AngamosChapter.Flags;
using P = Pacifico.Core.Campaign.AngamosChapter.Perspectives;
using S = Pacifico.Core.Campaign.AngamosChapter.Stages;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Director del capítulo 2, «Caza en alta mar», en escena (ROADMAP 6.7). La lógica narrativa es
    /// <see cref="AngamosChapter"/>, probada fuera del motor; aquí se traducen los sucesos de la escena a hechos
    /// (distancia, andanadas, perforaciones, granadas desviadas, espolonazos, abordaje) y las etapas a perspectiva y a
    /// órdenes para la IA: el Huáscar huye y vira, embiste una vez, se para y abre las válvulas; la Unión escapa; el
    /// Blanco Encalada entra en combate. También dibuja la distancia de perforación, el ángulo de la coraza, la cuenta
    /// atrás de las válvulas y las láminas del Archivo con las que se cierra el capítulo.
    /// </summary>
    public sealed class AngamosMissionDirector : MonoBehaviour
    {
        [Header("Guion (sección del capítulo 2, copiada por el constructor de la escena)")]
        [SerializeField, TextArea(3, 8)] private string guionChapter = string.Empty;

        [Header("Cochrane (jugador)")]
        [SerializeField] private ShipController cochrane;
        [SerializeField] private PlayerBroadsideGunner gunner;
        [Tooltip("Gobierna el Cochrane cuando el jugador no lo hace (intercalado del Huáscar). Empieza desactivada.")]
        [SerializeField] private BroadsideShipAI cochraneAI;

        [Header("Huáscar")]
        [SerializeField] private ShipController huascar;
        [SerializeField] private RammingShipAI huascarAI;
        [SerializeField] private ColesTurretController turret;
        [SerializeField] private RamBow ram;
        [Tooltip("Torre de mando del Huáscar (donde estalla el proyectil que mata a Grau).")]
        [SerializeField] private Transform conningTower;

        [Header("Escena")]
        [SerializeField] private ShipController blanco;
        [SerializeField] private BroadsideShipAI blancoAI;
        [SerializeField] private EscapingShip union;
        [SerializeField] private ShipCameraRig cameraRig;
        [SerializeField] private NavalHud hud;
        [SerializeField] private Material burstMaterial;

        [Header("Láminas de cierre (Archivo Histórico)")]
        [SerializeField] private Texture2D[] plates = new Texture2D[0];
        [SerializeField] private string[] plateCaptions = new string[0];

        private const float FadeSeconds = 0.7f;
        private const float MinSecondsBetweenRams = 20f;
        /// <summary>Rumbos de la huida del Huáscar y de la Unión (hacia el norte, a Arica).</summary>
        private const float FleeHeadingDeg = 0f;
        private const float UnionEscapeHeadingDeg = 345f;

        private MissionRunner _runner;
        private ShipDamageController _cochraneDamage;
        private ShipDamageController _huascarDamage;
        private SurvivorRescue _boarding;
        private float _openingFireRangeM;
        private string _perspective;
        private string _pendingPerspective;
        private float _fade = -1f;
        private float _ramWindowStart = -1f;
        private bool _ramDone;
        private float _lastRamTime = float.NegativeInfinity;
        private bool _unionReleased;
        private bool _foundered;
        private bool _blancoBrought;
        private int _plate;
        private bool _recorded;
        private GUIStyle _caption;

        private readonly MissionHud _hud = new MissionHud();

        public MissionRunner Runner => _runner;

        public void Configure(string chapterMarkdown, ShipController cochraneShip, PlayerBroadsideGunner cochraneGunner, BroadsideShipAI cochraneBrain,
                              ShipController huascarShip, RammingShipAI huascarBrain, ColesTurretController coles, RamBow bow, Transform tower,
                              ShipController blancoShip, BroadsideShipAI blancoBrain, EscapingShip unionShip, ShipCameraRig rig, NavalHud navalHud,
                              Material burst, Texture2D[] archivePlates, string[] captions)
        {
            guionChapter = chapterMarkdown;
            cochrane = cochraneShip;
            gunner = cochraneGunner;
            cochraneAI = cochraneBrain;
            huascar = huascarShip;
            huascarAI = huascarBrain;
            turret = coles;
            ram = bow;
            conningTower = tower;
            blanco = blancoShip;
            blancoAI = blancoBrain;
            union = unionShip;
            cameraRig = rig;
            hud = navalHud;
            burstMaterial = burst;
            plates = archivePlates;
            plateCaptions = captions;
        }

        // ------------------------------------------------------------------------------------------
        // Arranque: guion y sucesos de la escena → hechos
        // ------------------------------------------------------------------------------------------

        private void Start()
        {
            GuionQuotes quotes = GuionQuotes.Extract(guionChapter, C.ChapterHeading);
            _runner = new MissionRunner(C.Build(quotes));
            _openingFireRangeM = C.OpeningFireRangeM();
            _boarding = new SurvivorRescue(C.BoardingRadiusM, C.BoardingMaxSpeedMps, C.BoardingHoldSeconds);

            _cochraneDamage = cochrane.GetComponent<ShipDamageController>();
            _huascarDamage = huascar.GetComponent<ShipDamageController>();

            if (gunner != null)
            {
                gunner.Target = huascar;
                gunner.PlayerFired += OnPlayerFired;
            }
            var huascarHull = huascar.GetComponent<ArmoredHull>();
            if (huascarHull != null) huascarHull.ImpactResolved += OnHuascarHit;
            var cochraneHull = cochrane.GetComponent<ArmoredHull>();
            if (cochraneHull != null) cochraneHull.ImpactResolved += OnCochraneHit;
            if (ram != null) ram.Rammed += OnRammed;
            if (_cochraneDamage != null) _cochraneDamage.Sunk += _ => _runner.Facts.SetFlag(G.CochraneSunk);
            if (_huascarDamage != null) _huascarDamage.Sunk += _ => _runner.Facts.SetFlag(G.HuascarSunk);

            // El Huáscar nunca lo gobierna el jugador en este capítulo.
            huascar.PlayerControlled = false;
            if (turret != null) turret.PlayerControlled = false;
            if (_huascarDamage != null) _huascarDamage.PlayerControlled = false;
            if (huascarAI != null)
            {
                huascarAI.Target = cochrane;
                huascarAI.FleeHeadingDeg = FleeHeadingDeg;
                huascarAI.SetStandOff(400f, 1100f); // el combate se cerró hasta pocos cientos de metros
                huascarAI.AiFireIntervalSeconds = 20f;
                huascarAI.Mode = RammingMode.Flee;
                huascarAI.HoldFire = true;
            }
            if (cochraneAI != null)
            {
                cochraneAI.Enemy = huascar;
                cochraneAI.LeashRadius = 1e6f;
                cochraneAI.enabled = false;
            }
            if (blancoAI != null)
            {
                blancoAI.Enemy = huascar;
                blancoAI.LeashRadius = 1e6f;
                blancoAI.AutoFire = false;
            }
            if (hud != null)
            {
                hud.ExtraLines.Add(RangeLine);
                hud.ExtraLines.Add(ArmorAngleLine);
                hud.ExtraLines.Add(ValvesLine);
            }

            Apply(_runner.CurrentStage.Perspective);
        }

        private void OnDestroy()
        {
            if (ram != null) ram.Rammed -= OnRammed;
            if (gunner != null) gunner.PlayerFired -= OnPlayerFired;
        }

        private float Range => Vector3.Distance(Flat(cochrane.transform.position), Flat(huascar.transform.position));

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private string Stage => _runner.CurrentStage.Id;

        private void OnPlayerFired(int guns)
        {
            _runner.Facts.Add(F.Broadsides);
            if (Range > _openingFireRangeM) _runner.Facts.Add(F.BroadsidesBeyondRange);
        }

        private void OnHuascarHit(ResolvedImpact impact)
        {
            GameObject shooter = impact.Hit.Shooter;
            bool penetrated = impact.Result.Penetrated;
            if (shooter == cochrane.gameObject && Stage == S.Duel)
            {
                if (!penetrated)
                {
                    _runner.Facts.Add(F.ArmorStops);
                    return;
                }
                _runner.Facts.Add(F.PenetratingHits);
                if (impact.Zone == ArmorZone.ConningTower) _runner.Facts.Add(F.ConningTowerHits);
                // La popa: los guardines del timón corren por ahí.
                bool stern = huascar.transform.InverseTransformPoint(impact.Hit.Point).z < 0f;
                if (impact.Zone == ArmorZone.BeltEnds && stern && _huascarDamage != null && _huascarDamage.State != null) _huascarDamage.State.JamSteering();
            }
            else if (penetrated && Stage == S.Relief && (shooter == cochrane.gameObject || (blanco != null && shooter == blanco.gameObject)))
            {
                _runner.Facts.Add(F.HitsSinceGrau);
            }
        }

        private void OnCochraneHit(ResolvedImpact impact)
        {
            if (impact.Hit.Shooter != huascar.gameObject) return;
            _runner.Facts.Add(F.HitsOnCochrane);
            if (!impact.Result.Penetrated) _runner.Facts.Add(F.Deflected);
        }

        private void OnRammed(RamBow bow, ShipController victim, RamResult result)
        {
            if (victim != cochrane || !result.Critical || Time.time - _lastRamTime < MinSecondsBetweenRams) return;
            _lastRamTime = Time.time;
            _runner.Facts.Add(F.RamsReceived);
            EndRamRun();
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
                    CampaignSave.Record(_runner.Script);
                }
                CampaignNavigator.HandleEndKeys(_runner.Script.Id, _runner.State == MissionState.Complete);
                if (GameInput.Pressed(GameKey.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            ReadScene(dt);
            _runner.Step(dt);
            Direct();

            string wanted = _runner.CurrentStage.Perspective;
            if (wanted != _perspective && _pendingPerspective == null)
            {
                _pendingPerspective = wanted;
                _fade = 0f;
            }
            if (_fade >= 0f)
            {
                float before = _fade;
                _fade += Time.unscaledDeltaTime / FadeSeconds;
                if (before < 1f && _fade >= 1f && _pendingPerspective != null)
                {
                    Apply(_pendingPerspective);
                    _pendingPerspective = null;
                }
                if (_fade >= 2f) _fade = -1f;
            }

            if (_perspective == P.Plates && _fade < 0f && (GameInput.Pressed(GameKey.Space) || GameInput.MousePressed(0)))
            {
                _plate++;
                if (_plate >= plates.Length) _runner.Facts.SetFlag(G.PlatesClosed);
            }
        }

        /// <summary>Lo que la escena sabe y el guion no: distancias, gobierno del Huáscar, abordaje.</summary>
        private void ReadScene(float dt)
        {
            float range = Range;
            MissionFacts facts = _runner.Facts;
            if (range <= C.ChaseRangeM) facts.SetFlag(G.WithinRange);
            if (Stage == S.Trap && !facts.Flag(G.HuascarTurns) && range > C.EscapeRangeM) facts.SetFlag(G.HuascarEscaped);

            ShipDamageState huascarState = _huascarDamage != null ? _huascarDamage.State : null;
            if (huascarState != null)
            {
                // El modelo no decide el desenlace: el Huáscar no se hunde por el cañoneo (fue capturado a flote).
                if (!_foundered) huascarState.KeepAfloat = true;
                if (huascarState.SteeringJammed) facts.SetFlag(G.HuascarSteeringJammed);
            }

            if (Stage == S.Valves && facts.Flag(G.ValvesOpen) && !facts.Flag(G.Boarded))
            {
                float distance = range;
                float speed = cochrane.Motion != null ? cochrane.Motion.Speed : 0f;
                if (_boarding.Step(dt, distance, speed)) facts.SetFlag(G.Boarded);
                facts.Set(F.Boarding, _boarding.Progress);
            }
            // La carrera de espolón: tras la primera perforación (o un rato de duelo), en cuanto lo tiene cerca.
            if (Stage == S.Duel && !facts.Flag(G.RamAttempt) && range < C.RamStartRangeM &&
                (facts.Get(F.PenetratingHits) >= 1f || _runner.StageTime >= C.RamAfterSeconds))
            {
                facts.SetFlag(G.RamAttempt);
            }
            if (facts.Flag(G.ScuttleComplete) && !_foundered && _huascarDamage != null)
            {
                _foundered = true;
                _huascarDamage.Founder();
            }
        }

        /// <summary>Órdenes a la IA según la etapa y las marcas que pone el guion.</summary>
        private void Direct()
        {
            MissionFacts facts = _runner.Facts;
            if (!_unionReleased && facts.Flag(G.UnionBreaksAway) && union != null)
            {
                _unionReleased = true;
                union.BreakAway(UnionEscapeHeadingDeg);
            }
            if (huascarAI == null) return;

            if (facts.Flag(G.ValvesOpen))
            {
                // Rendido: máquina parada, la torre callada.
                huascarAI.Mode = RammingMode.Idle;
                huascarAI.HoldFire = true;
                huascar.Command(EngineOrder.Stop, 0f);
                if (gunner != null) gunner.PlayerActive = false; // no se dispara sobre un buque rendido
                if (blancoAI != null) blancoAI.AutoFire = false;
                if (cochraneAI != null) cochraneAI.AutoFire = false;
                return;
            }

            if (!facts.Flag(G.HuascarTurns))
            {
                huascarAI.Mode = RammingMode.Flee;
                huascarAI.HoldFire = true;
                return;
            }

            huascarAI.HoldFire = false;
            if (Stage == S.Duel && facts.Flag(G.RamAttempt) && !_ramDone)
            {
                if (_ramWindowStart < 0f) _ramWindowStart = Time.time;
                huascarAI.Mode = RammingMode.Ram;
                huascarAI.RamOrder = EngineOrder.FullAhead;
                if (Time.time - _ramWindowStart > C.RamRunSeconds) EndRamRun();
                return;
            }
            huascarAI.Mode = RammingMode.StandOff;
        }

        private void EndRamRun()
        {
            if (_ramDone || _ramWindowStart < 0f) return;
            _ramDone = true;
            if (_runner.Facts.Get(F.RamsReceived) < 1f) _runner.Facts.SetFlag(G.RamAvoided);
            if (huascarAI != null) huascarAI.Mode = RammingMode.StandOff;
        }

        /// <summary>Traduce la perspectiva de la etapa a cámara, mandos e IA.</summary>
        private void Apply(string perspective)
        {
            _perspective = perspective;
            bool bridge = perspective == P.CochraneBridge;
            bool deck = perspective == P.HuascarDeck;

            if (gunner != null) gunner.PlayerActive = bridge;
            if (cochraneAI != null)
            {
                cochraneAI.enabled = !bridge;
                cochraneAI.AutoFire = deck;
            }
            if (bridge) cochrane.PlayerControlled = true; // la IA lo desactiva en su Start al relevar al jugador
            else
            {
                cochrane.PlayerControlled = false;
                cochrane.Command(EngineOrder.HalfAhead, 0f);
            }
            if (_cochraneDamage != null) _cochraneDamage.PlayerControlled = bridge;

            if (deck) ConningTowerBurst();
            if (bridge && Stage == S.Relief) BringBlanco();

            ShipController followed = deck ? huascar : cochrane;
            if (cameraRig != null) cameraRig.Target = followed.transform;
            if (hud != null) hud.Ship = followed;
        }

        /// <summary>El proyectil en la torre de mando: una nube de humo y fuego sobre el Huáscar.</summary>
        private void ConningTowerBurst()
        {
            Vector3 at = conningTower != null ? conningTower.position : huascar.transform.position + Vector3.up * 4f;
            DustBurst.Spawn(at, burstMaterial, 9f);
            DustBurst.Spawn(at + Vector3.up * 2f, burstMaterial, 6f);
        }

        /// <summary>El Blanco Encalada se suma al combate: si aún está lejos, aparece a la vista (tras el fundido).</summary>
        private void BringBlanco()
        {
            if (_blancoBrought || blanco == null) return;
            _blancoBrought = true;
            if (blancoAI != null) blancoAI.AutoFire = true;
            Vector3 h = Flat(huascar.transform.position);
            if (Vector3.Distance(Flat(blanco.transform.position), h) <= 3000f) return;
            Vector3 from = Flat(blanco.transform.position) - h;
            if (from.sqrMagnitude < 1f) from = Vector3.back;
            Vector3 p = h + from.normalized * 2200f;
            blanco.Teleport(new Vector3(p.x, blanco.transform.position.y, p.z), Ballistics.Bearing(p.x, p.z, h.x, h.z));
        }

        // ------------------------------------------------------------------------------------------
        // Interfaz (IMGUI de prototipo)
        // ------------------------------------------------------------------------------------------

        private string RangeLine()
        {
            if (_runner == null || _perspective != P.CochraneBridge || Stage == S.Valves) return string.Empty;
            float range = Range;
            return "Huáscar a " + C.Meters(range) + " m  ·  " + (range <= _openingFireRangeM
                ? "a distancia de perforar su coraza"
                : "fuera de perforación (" + C.Meters(_openingFireRangeM) + " m)");
        }

        private string ArmorAngleLine()
        {
            if (_runner == null || _perspective != P.CochraneBridge || Stage == S.Trap || Stage == S.Valves || cochrane.Motion == null) return string.Empty;
            Vector3 to = huascar.transform.position - cochrane.transform.position;
            float obliquity = Ballistics.SideObliquityDeg(cochrane.Motion.HeadingDeg, Ballistics.Bearing(0f, 0f, to.x, to.z));
            // La batería bate hasta 40° a cada lado del través; la coraza protege los extremos a partir de ~35°.
            // La oblicuidad coincide con el apartamiento del través: la batería bate mientras no pase de su semisector.
            float fireArc = gunner != null && gunner.Battery != null ? gunner.Battery.ArcHalfWidthDeg : BroadsideBatteryModel.DefaultArcHalfWidthDeg;
            string advice = obliquity < 30f ? "de través: andanada lista, coraza expuesta"
                : obliquity <= fireArc ? "en ángulo y aún con la batería por el través"
                : "en ángulo: la batería no lo bate";
            return "Coraza a " + obliquity.ToString("0") + "° del fuego enemigo  ·  " + advice;
        }

        private string ValvesLine()
        {
            if (_runner == null || Stage != S.Valves || !_runner.Facts.Flag(G.ValvesOpen) || _runner.Facts.Flag(G.Boarded)) return string.Empty;
            float left = Mathf.Max(0f, C.ScuttleSeconds - _runner.StageTime);
            float speedKn = cochrane.Motion != null ? cochrane.Motion.SpeedKnots : 0f;
            string blocker = _boarding.Blocker(Range, cochrane.Motion != null ? cochrane.Motion.Speed : 0f);
            return "VÁLVULAS ABIERTAS: se hunde en " + left.ToString("0") + " s  ·  " +
                   (blocker ?? "arriando los botes " + (_boarding.Progress * 100f).ToString("0") + "%") + "  ·  " + speedKn.ToString("0.0") + " nudos";
        }

        private void OnGUI()
        {
            if (_runner == null) return;
            GUI.depth = -100;
            if (_perspective == P.Plates && _runner.State == MissionState.Running) DrawPlate();
            if (_runner.State == MissionState.Running)
            {
                if (_perspective != P.Plates) _hud.DrawObjectives(_runner, "CAPÍTULO 2", Who());
                _hud.DrawDialogue(_runner, _perspective == P.Plates ? 40f : 300f);
            }
            else
            {
                _hud.DrawEnd(_runner, "CAPÍTULO 2", "El Huáscar, capturado a flote. Grau ha muerto en su torre de mando.");
            }
            if (_fade >= 0f) MissionHud.DrawFade(1f - Mathf.Abs(_fade - 1f), Color.black);
        }

        /// <summary>Lámina de época a pantalla completa con su pie.</summary>
        private void DrawPlate()
        {
            MissionHud.DrawFade(1f, Color.black);
            if (plates == null || plates.Length == 0)
            {
                _runner.Facts.SetFlag(G.PlatesClosed); // sin láminas, la misión no se queda colgada
                return;
            }
            int index = Mathf.Clamp(_plate, 0, plates.Length - 1);
            Texture2D plate = plates[index];
            if (plate != null)
            {
                float aspect = plate.width / (float)plate.height;
                float h = Screen.height * 0.68f, w = h * aspect;
                if (w > Screen.width * 0.9f)
                {
                    w = Screen.width * 0.9f;
                    h = w / aspect;
                }
                GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.06f, w, h), plate, ScaleMode.ScaleToFit);
            }
            if (_caption == null)
            {
                _caption = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
                _caption.normal.textColor = new Color(0.92f, 0.88f, 0.78f);
            }
            string caption = plateCaptions != null && index < plateCaptions.Length ? plateCaptions[index] : string.Empty;
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.76f, Screen.width * 0.8f, 60f), caption, _caption);
        }

        private string Who()
        {
            switch (_perspective)
            {
                case P.CochraneBridge: return "Almirante Cochrane · a las órdenes del comandante " + C.Latorre;
                case P.HuascarDeck: return "A bordo del Huáscar";
                default: return string.Empty;
            }
        }
    }
}
