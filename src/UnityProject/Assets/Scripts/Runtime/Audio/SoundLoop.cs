using UnityEngine;

namespace Pacifico.Audio
{
    /// <summary>
    /// Sonido en bucle pegado a un objeto (estela de un buque, máquina de vapor, galope de un caballo). Su volumen
    /// sigue a <see cref="Level"/> (0–1), que actualiza quien lo usa, con un fundido suave; a nivel cero se calla y deja
    /// de gastar. El clip sale al azar del grupo de <c>Resources/Sonido</c> y empieza en un punto al azar para que dos
    /// buques no suenen a la vez igual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundLoop : MonoBehaviour
    {
        [SerializeField] private string group = Sfx.Wake;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 300f;
        /// <summary>Segundos del fundido al cambiar de nivel.</summary>
        [SerializeField] private float fade = 0.8f;

        private AudioSource _source;
        private float _current;

        /// <summary>Intensidad deseada (0 = callado, 1 = volumen completo).</summary>
        public float Level { get; set; } = 1f;

        /// <summary>Añade (o reutiliza) un bucle en <paramref name="host"/>.</summary>
        public static SoundLoop Attach(GameObject host, string group, float volume, float minDistance, float maxDistance, float level = 1f)
        {
            if (!host.TryGetComponent(out SoundLoop loop)) loop = host.AddComponent<SoundLoop>();
            loop.group = group;
            loop.volume = volume;
            loop.minDistance = minDistance;
            loop.maxDistance = maxDistance;
            loop.Level = level;
            return loop;
        }

        private void Start()
        {
            AudioClip clip = GameAudio.Clip(group);
            if (clip == null)
            {
                enabled = false;
                return;
            }
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = minDistance;
            _source.maxDistance = maxDistance;
            _source.dopplerLevel = 0f;
            _source.volume = 0f;
            _source.time = Random.Range(0f, clip.length * 0.9f);
        }

        private void Update()
        {
            if (_source == null) return;
            float target = Mathf.Clamp01(Level);
            _current = Mathf.MoveTowards(_current, target, Time.deltaTime / Mathf.Max(0.05f, fade));
            _source.volume = _current * volume;
            if (_current > 0.001f && !_source.isPlaying) _source.Play();
            else if (_current <= 0.001f && _source.isPlaying) _source.Pause();
        }
    }
}
