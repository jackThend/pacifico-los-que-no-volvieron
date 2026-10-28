using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Humo de carbón de la chimenea de un buque de vapor: penacho oscuro que sube, se ensancha y, al quedarse en el
    /// mundo (no en el buque), traza la estela de humo detrás del barco y se lo lleva la brisa. El emisor apunta hacia
    /// arriba desde la boca de la chimenea; el editor (<c>ShipDressing</c>) lo coloca y le da el material.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FunnelSmoke : MonoBehaviour
    {
        [SerializeField] private Material material;
        /// <summary>Bocanadas por segundo (valor de ajuste).</summary>
        [SerializeField] private float rate = 5f;
        /// <summary>Brisa que arrastra el humo (m/s, mundo).</summary>
        [SerializeField] private Vector3 wind = new Vector3(2.5f, 0f, 0.8f);

        public void Setup(Material smokeMaterial, float puffsPerSecond)
        {
            material = smokeMaterial;
            rate = puffsPerSecond;
        }

        private void Awake() => Configure(GetComponent<ParticleSystem>(), material, rate, wind);

        public static void Configure(ParticleSystem particles, Material material, float rate, Vector3 wind)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.095f, 0.09f, 0.8f), new Color(0.22f, 0.21f, 0.2f, 0.65f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 320;
            main.gravityModifier = -0.015f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.6f;

            // La brisa arrastra el humo a velocidad constante (una fuerza lo aceleraría sin fin y lo tumbaría de lado).
            ParticleSystem.VelocityOverLifetimeModule drift = particles.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = wind.x * 0.5f;
            drift.y = 0f;
            drift.z = wind.z * 0.5f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 6f));

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1.6f, 1.6f, 1.6f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            if (material != null) renderer.sharedMaterial = material;
            particles.Play();
        }
    }
}
