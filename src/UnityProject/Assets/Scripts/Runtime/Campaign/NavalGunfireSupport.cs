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
            _fleet = new NavalBombardment(this.StableId());
            _random = new System.Random(this.StableId() + 1);
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
}
