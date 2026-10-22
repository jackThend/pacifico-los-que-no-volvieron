using System;
using System.Collections.Generic;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using Pacifico.Tactics;
using UnityEngine;

namespace Pacifico.Campaign
{
    /// <summary>
    /// Bote de desembarco en escena (capítulo 3): recorre en línea recta la distancia del transporte a la playa con la
    /// escuadra a bordo. Las bajas las decide <see cref="LandingBoat"/> con la intensidad de fuego que le pasa el
    /// director; cada hombre que cae desaparece de la bancada. Al varar avisa con el número de supervivientes.
    /// </summary>
    public sealed class LandingCraft : MonoBehaviour
    {
        [SerializeField] private string squadName = "Atacama";
        [SerializeField] private int men = PisaguaChapter.MenPerBoat;
        [SerializeField] private Vector3 beachPoint;
        [Tooltip("Cabezas de los hombres a bordo (se ocultan al caer).")]
        [SerializeField] private Transform[] heads = new Transform[0];

        private LandingBoat _boat;
        private System.Random _random;
        private Vector3 _start;
        private bool _announced;

        public string SquadName => squadName;
        public bool Launched => _boat != null;
        public bool Beached => _boat != null && _boat.Beached;
        public int Men => _boat != null ? _boat.Men : men;
        public int StartMen => men;
        public Vector3 BeachPoint => beachPoint;

        /// <summary>Intensidad del fuego que recibe el bote en una posición (la pone el director).</summary>
        public Func<Vector3, float> FireAt { get; set; }

        /// <summary>El bote ha varado (supervivientes a bordo).</summary>
        public event Action<LandingCraft, int> Landed;

        public void Configure(string name, int count, Vector3 beach, Transform[] headVisuals)
        {
            squadName = name;
            men = count;
            beachPoint = beach;
            heads = headVisuals;
        }

        /// <summary>Se separa del transporte.</summary>
        public void Launch()
        {
            if (_boat != null) return;
            _start = transform.position;
            Vector3 run = beachPoint - _start;
            run.y = 0f;
            _boat = new LandingBoat(men, Mathf.Max(1f, run.magnitude));
            _random = new System.Random(GetInstanceID());
        }

        private void Update()
        {
            if (_boat == null || _announced) return;
            float intensity = FireAt != null ? FireAt(transform.position) : 0f;
            int fallen = _boat.Step(Time.deltaTime, intensity, _random);
            if (fallen > 0) HideHeads();
            Vector3 p = Vector3.Lerp(_start, beachPoint, _boat.Progress);
            p.y = _start.y + Mathf.Sin(Time.time * 1.7f + GetInstanceID()) * 0.15f; // cabeceo del bote
            transform.position = p;
            if (!_boat.Beached) return;
            _announced = true;
            Landed?.Invoke(this, _boat.Men);
            gameObject.SetActive(false); // la escuadra salta a tierra (la crea el director)
        }

        private void HideHeads()
        {
            for (int i = 0; i < heads.Length; i++)
            {
                if (heads[i] != null) heads[i].gameObject.SetActive(i < _boat.Men);
            }
        }
    }

    /// <summary>
    /// Fuerte costero en escena (Punta Pichalo, Punta Guacas): su estado es <see cref="CoastalFort"/>; sus piezas son
    /// una <see cref="SquadArtillery"/> que barre la playa mientras no lo acalle la flota.
    /// </summary>
    public sealed class CoastalFortBattery : MonoBehaviour
    {
        [SerializeField] private string fortName = PisaguaChapter.Pichalo;
        [SerializeField] private SquadArtillery guns;
        [SerializeField] private Material smoke;

        public CoastalFort Fort { get; private set; }
        public string FortName => fortName;

        /// <summary>La flota lo ha hecho callar.</summary>
        public event Action<CoastalFortBattery> Silenced;

        public void Configure(string name, SquadArtillery fortGuns, Material smokeMaterial)
        {
            fortName = name;
            guns = fortGuns;
            smoke = smokeMaterial;
        }

        private void Awake()
        {
            Fort = new CoastalFort(fortName, transform.position.x, transform.position.z);
        }

        /// <summary>Una granada de la flota cae en <paramref name="impact"/>.</summary>
        public void RegisterImpact(Vector3 impact)
        {
            if (Fort.RegisterImpact(impact.x, impact.z)) OnSilenced();
        }

        /// <summary>La flota termina de batirlo por su cuenta.</summary>
        public void SilenceNow()
        {
            if (Fort.Silenced) return;
            Fort.Silence();
            OnSilenced();
        }

        private void OnSilenced()
        {
            if (guns != null) guns.Firing = false;
            DustBurst.Spawn(transform.position + Vector3.up * 2f, smoke, 16f);
            Silenced?.Invoke(this);
        }
    }

    /// <summary>
    /// La artillería de la flota a demanda (Cochrane y O'Higgins): dispara <see cref="NavalBombardment"/> contra el punto
    /// marcado; cada granada levanta una columna de tierra, cuenta contra los fuertes y hiere y suprime a cualquier
    /// escuadra cercana, sea del bando que sea (el fuego naval no distingue).
    /// </summary>
    public sealed class NavalGunfireSupport : MonoBehaviour
    {
        [SerializeField] private CoastalFortBattery[] forts = new CoastalFortBattery[0];
        [SerializeField] private Transform[] ships = new Transform[0];
        [SerializeField] private Material dust;
        [SerializeField] private Material muzzleSmoke;

        private NavalBombardment _fleet;
        private System.Random _random;
        private readonly List<NavalImpact> _landed = new List<NavalImpact>();
        private int _salvos;

        public NavalBombardment Fleet => _fleet;
        public Vector3 Target { get; private set; }

        public void Configure(CoastalFortBattery[] coastalForts, Transform[] fleetShips, Material dustMaterial, Material smokeMaterial)
        {
            forts = coastalForts;
            ships = fleetShips;
            dust = dustMaterial;
            muzzleSmoke = smokeMaterial;
        }

        private void Awake()
        {
            _fleet = new NavalBombardment(GetInstanceID());
            _random = new System.Random(GetInstanceID() + 1);
        }

        public void Designate(Vector3 point)
        {
            Target = point;
            _fleet.Designate(point.x, point.z);
        }

        public void Cancel() => _fleet.Cancel();

        private void Update()
        {
            _fleet.Step(Time.deltaTime, _landed);
            if (_fleet.SalvosFired != _salvos)
            {
                _salvos = _fleet.SalvosFired;
                foreach (Transform ship in ships)
                {
                    if (ship != null) DustBurst.Spawn(ship.position + Vector3.up * 4f + ship.right * 8f, muzzleSmoke, 10f);
                }
            }
            foreach (NavalImpact impact in _landed) Explode(impact);
        }

        private void Explode(NavalImpact impact)
        {
            Vector3 point = new Vector3(impact.X, 200f, impact.Z);
            point.y = Physics.Raycast(point, Vector3.down, out RaycastHit hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            point = new Vector3(impact.X, point.y, impact.Z);
            DustBurst.Spawn(point, dust, ShellBurst.LethalRadiusM * 1.4f);
            foreach (CoastalFortBattery fort in forts)
            {
                if (fort != null) fort.RegisterImpact(point);
            }

            // Una granada de 250 lb: metralla y supresión para quien esté cerca, amigo o enemigo.
            SquadController[] squads = new SquadController[SquadController.All.Count];
            for (int i = 0; i < squads.Length; i++) squads[i] = SquadController.All[i];
            foreach (SquadController squad in squads)
            {
                if (squad == null || !squad.IsAlive) continue;
                float centre = Vector3.Distance(squad.CenterOfMass(), point);
                if (centre > ShellBurst.LethalRadiusM + 20f) continue;
                int casualties = 0;
                foreach (SoldierUnit soldier in squad.Soldiers)
                {
                    float d = Vector3.Distance(soldier.transform.position, point);
                    if (_random.NextDouble() < ShellBurst.CasualtyProbability(d, squad.Cover)) casualties++;
                }
                if (casualties > 0) squad.TakeCasualties(casualties, point);
                if (squad.IsAlive) squad.ReceiveFire(new VolleyResult { Shots = 8, Hits = casualties, Casualties = casualties }, centre, 400f, point);
            }
        }
    }

    /// <summary>
    /// Objetivo con dinamita (los estanques de agua dulce, las locomotoras): la mecha arde desde que se enciende y se
    /// apaga si una escuadra chilena se queda junto a él sin enemigos cerca (<see cref="CapturePoint"/>) antes de que
    /// llegue a la carga (<see cref="DemolitionCharge"/>).
    /// </summary>
    public sealed class DemolitionObjective : MonoBehaviour
    {
        [SerializeField] private string objectiveName = "Estanques";
        [SerializeField] private float captureRadiusM = 15f;
        [SerializeField] private float contestRadiusM = 30f;
        [SerializeField] private Material blast;
        [Tooltip("Lo que se ve destruido si estalla (se hunde y se oscurece).")]
        [SerializeField] private Transform visual;

        private readonly CapturePoint _capture = new CapturePoint(5f);
        private DemolitionCharge _charge;

        public string ObjectiveName => objectiveName;
        public bool Lit => _charge != null;
        public DemolitionCharge Charge => _charge;
        public float CaptureProgress => _capture.Progress;

        public event Action<DemolitionObjective> Secured;
        public event Action<DemolitionObjective> Blown;

        public void Configure(string name, Transform target, Material blastMaterial)
        {
            objectiveName = name;
            visual = target;
            blast = blastMaterial;
        }

        /// <summary>Enciende la mecha.</summary>
        public void Light(float fuseSeconds)
        {
            if (_charge == null) _charge = new DemolitionCharge(fuseSeconds);
        }

        private void Update()
        {
            if (_charge == null || _charge.Resolved) return;
            int chileans = 0, allies = 0;
            Vector3 here = transform.position;
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.IsAlive || squad.Routed) continue;
                bool chile = squad.Faction == Faction.Chile;
                float radius = chile ? captureRadiusM : contestRadiusM;
                foreach (SoldierUnit soldier in squad.Soldiers)
                {
                    if (Vector3.Distance(soldier.transform.position, here) > radius) continue;
                    if (chile) chileans++;
                    else allies++;
                }
            }
            _capture.Step(Time.deltaTime, chileans, allies);
            bool blown = _charge.Step(Time.deltaTime, _capture.Captured);
            if (_charge.Defused) Secured?.Invoke(this);
            if (!blown) return;
            DustBurst.Spawn(here + Vector3.up * 3f, blast, 22f);
            if (visual != null)
            {
                visual.localScale = Vector3.Scale(visual.localScale, new Vector3(1f, 0.35f, 1f));
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = blast;
            }
            Blown?.Invoke(this);
        }
    }
}
