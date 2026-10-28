using UnityEngine;

namespace Pacifico.Naval
{
    /// <summary>
    /// Espuma de la estela (en la popa) o de la ola de proa: manchas de espuma planas sobre el agua que suelta el buque
    /// según su velocidad y que se quedan en el mundo, ensanchándose y apagándose detrás de él. Parado no deja espuma.
    /// El editor (<c>ShipDressing</c>) lo coloca a la altura del agua y le da el material.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ShipWake : MonoBehaviour
    {
        [SerializeField] private Material material;
        /// <summary>Manga del buque (m): ancho de la franja de espuma.</summary>
        [SerializeField] private float beam = 10f;
        /// <summary>Segundos que dura una mancha (la estela larga; la ola de proa, poco).</summary>
        [SerializeField] private float lifetime = 18f;
        /// <summary>Manchas por segundo y por m/s de velocidad (valor de ajuste).</summary>
        [SerializeField] private float ratePerMps = 1.6f;

        private ParticleSystem _particles;
        private Vector3 _lastPosition;
        private float _speed;
        private Audio.SoundLoop _sound;

        public void Setup(Material foam, float shipBeam, float seconds, float rate)
        {
            material = foam;
            beam = shipBeam;
            lifetime = seconds;
            ratePerMps = rate;
        }

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(beam * 0.35f, beam * 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new Color(0.92f, 0.95f, 0.95f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(beam * 0.5f, 0.01f, 1.5f);

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.6f));

            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortMode = ParticleSystemSortMode.OldestInFront;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;

            _lastPosition = transform.position;
            _particles.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _particles == null) return;
            Vector3 moved = transform.position - _lastPosition;
            _lastPosition = transform.position;
            moved.y = 0f;
            _speed = Mathf.Lerp(_speed, moved.magnitude / dt, 1f - Mathf.Exp(-dt * 3f));
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = Mathf.Min(_speed, 15f) * ratePerMps;
            if (_sound == null) _sound = Audio.SoundLoop.Attach(gameObject, Audio.Sfx.Wake, 0.55f, 15f, 350f, 0f);
            _sound.Level = Mathf.Clamp01(_speed / 7f);
        }
    }
}
