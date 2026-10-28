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
            _random = new System.Random(this.StableId());
        }

        private void Update()
        {
            if (_boat == null || _announced) return;
            float intensity = FireAt != null ? FireAt(transform.position) : 0f;
            int fallen = _boat.Step(Time.deltaTime, intensity, _random);
            if (fallen > 0) HideHeads();
            Vector3 p = Vector3.Lerp(_start, beachPoint, _boat.Progress);
            p.y = _start.y + Mathf.Sin(Time.time * 1.7f + this.StableId()) * 0.15f; // cabeceo del bote
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
}
