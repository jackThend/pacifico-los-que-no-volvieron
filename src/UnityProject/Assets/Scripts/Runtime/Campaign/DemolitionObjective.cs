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
