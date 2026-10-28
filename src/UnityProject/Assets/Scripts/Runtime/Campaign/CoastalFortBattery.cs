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
}
