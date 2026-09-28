using System;
using Pacifico.Campaign;
using Pacifico.Core.Campaign;
using Pacifico.Core.Common;
using Pacifico.Core.Tactics;
using UnityEngine;

namespace Pacifico.Tactics
{
    /// <summary>
    /// Pieza de artillería de montaña en el RTS (capítulo 5): dispara granadas de metralla contra la escuadra enemiga
    /// más cercana a su alcance (<see cref="ShellBurst"/>: bajas según la distancia al punto de caída y la cobertura)
    /// y la suprime. Se captura quedándose junto a ella sin enemigos cerca (<see cref="CapturePoint"/>) y entonces
    /// dispara para su nuevo dueño, como los cañones que recuperan los Colorados.
    /// </summary>
    public sealed class SquadArtillery : MonoBehaviour
    {
        [SerializeField] private Faction owner = Faction.Chile;
        [SerializeField] private float rangeM = 1800f;
        [Tooltip("Una granada cada 25 s por pieza: con 14 s, la metralla sola costaba al jugador un cuarto de sus hombres en la trinchera (simulación, DEV_LOG 6.3).")]
        [SerializeField] private float fireIntervalSeconds = 25f;
        [Tooltip("Dispersión del punto de caída (m, desviación típica).")]
        [SerializeField] private float dispersionM = 9f;
        [SerializeField] private float captureRadiusM = 12f;
        [SerializeField] private float contestRadiusM = 30f;
        [SerializeField] private Transform barrel;
        [SerializeField] private Material dust;

        private System.Random _random;
        private CapturePoint _capture = new CapturePoint(6f);
        private float _nextShot;
        private float _recoil;

        public Faction Owner => owner;
        public bool Firing { get; set; } = true;
        /// <summary>El bando contrario puede tomarla.</summary>
        public bool Capturable { get; set; }
        public float CaptureProgress => _capture.Progress;

        /// <summary>Cambia de manos (nuevo dueño).</summary>
        public event Action<SquadArtillery, Faction> Captured;

        public void Configure(Faction side, Transform barrelTransform, Material dustMaterial)
        {
            owner = side;
            barrel = barrelTransform;
            dust = dustMaterial;
        }

        /// <summary>Pasa a manos de <paramref name="side"/> (p. ej. el flanco que cede) y se reinicia la captura.</summary>
        public void SetOwner(Faction side)
        {
            owner = side;
            _capture = new CapturePoint(6f);
        }

        private void Awake()
        {
            _random = new System.Random(GetInstanceID());
            _nextShot = Time.time + fireIntervalSeconds * (float)_random.NextDouble();
        }

        private static bool Enemies(Faction a, Faction b) => (a == Faction.Chile) != (b == Faction.Chile);

        private void Update()
        {
            if (Capturable) UpdateCapture();
            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * 2f);
            if (barrel != null) barrel.localPosition = new Vector3(0f, barrel.localPosition.y, -_recoil * 0.4f);
            if (!Firing || Time.time < _nextShot) return;
            _nextShot = Time.time + fireIntervalSeconds * (0.8f + 0.4f * (float)_random.NextDouble());
            Shoot();
        }

        private void UpdateCapture()
        {
            int attackers = 0, defenders = 0;
            Vector3 here = transform.position;
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.IsAlive || squad.Routed) continue;
                bool enemy = Enemies(owner, squad.Faction);
                float radius = enemy ? captureRadiusM : contestRadiusM;
                foreach (SoldierUnit soldier in squad.Soldiers)
                {
                    if (Vector3.Distance(soldier.transform.position, here) > radius) continue;
                    if (enemy) attackers++;
                    else defenders++;
                }
            }
            if (!_capture.Step(Time.deltaTime, attackers, defenders)) return;
            Faction newOwner = owner == Faction.Chile ? Faction.Bolivia : Faction.Chile;
            SetOwner(newOwner);
            Capturable = false;
            Captured?.Invoke(this, newOwner);
        }

        private void Shoot()
        {
            SquadController target = null;
            float best = rangeM;
            foreach (SquadController squad in SquadController.All)
            {
                if (!squad.IsAlive || !Enemies(owner, squad.Faction)) continue;
                float d = Vector3.Distance(transform.position, squad.CenterOfMass());
                if (d < best)
                {
                    best = d;
                    target = squad;
                }
            }
            if (target == null) return;
            _recoil = 1f;

            Vector3 impact = target.CenterOfMass() + new Vector3(Gaussian() * dispersionM, 0f, Gaussian() * dispersionM);
            if (Physics.Raycast(impact + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                impact = hit.point;
            }
            DustBurst.Spawn(impact, dust, ShellBurst.LethalRadiusM * 0.8f);

            int casualties = 0;
            foreach (SoldierUnit soldier in target.Soldiers)
            {
                float d = Vector3.Distance(soldier.transform.position, impact);
                if (_random.NextDouble() < ShellBurst.CasualtyProbability(d, target.Cover)) casualties++;
            }
            if (casualties > 0) target.TakeCasualties(casualties, impact);
            if (target.IsAlive)
            {
                // La granada suprime como una descarga cerrada de media escuadra.
                var volley = new VolleyResult { Shots = 6, Hits = casualties, Casualties = casualties };
                target.ReceiveFire(volley, best, rangeM, transform.position);
            }
        }

        private float Gaussian()
        {
            double u1 = 1.0 - _random.NextDouble(), u2 = _random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
