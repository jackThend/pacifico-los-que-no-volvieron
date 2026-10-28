using System.Collections.Generic;
using UnityEngine;

namespace Pacifico.Audio
{
    /// <summary>
    /// Sonidos sueltos del juego (disparos, cañonazos, explosiones, impactos, pasos). Los clips son grabaciones
    /// profesionales de los paquetes gratuitos GameAudioGDC de Sonniss (ver <c>Assets/Resources/Sonido/CREDITOS.md</c>),
    /// guardadas en <c>Resources/Sonido/&lt;grupo&gt;</c>; cada vez se elige uno al azar del grupo con un poco de
    /// variación de tono para que no suenen repetidos.
    /// <para>
    /// Los sonidos lejanos llegan con el retraso del sonido en el aire (343 m/s): el fogonazo de un cañón a un kilómetro
    /// se ve tres segundos antes de oírse, como en 1879. Cada grupo tiene un máximo de voces simultáneas (una descarga de
    /// cien fusiles no son cien sonidos) y un volumen propio. Si un grupo no tiene clips, no suena nada.
    /// </para>
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Velocidad del sonido en el aire (m/s).</summary>
        public const float SpeedOfSound = 343f;
        private const int PoolSize = 40;

        /// <summary>Ajuste de cada grupo: volumen, voces a la vez, distancia a la que empieza a bajar y alcance (valores de ajuste).</summary>
        private struct Group
        {
            public float Volume;
            public int Voices;
            public float MinDistance;
            public float MaxDistance;
            public float Pitch;
            public AudioClip[] Clips;
            public int Last;
        }

        private struct Pending
        {
            public string Group;
            public Vector3 Position;
            public float Volume;
            public float Time;
            public bool Flat;
        }

        private static readonly Dictionary<string, (float volume, int voices, float min, float max, float pitch)> Settings =
            new Dictionary<string, (float, int, float, float, float)>
            {
                { Sfx.Rifle, (0.9f, 10, 4f, 900f, 0.06f) },
                { Sfx.FieldGun, (1f, 4, 15f, 4000f, 0.05f) },
                { Sfx.NavalGun, (1f, 6, 25f, 6000f, 0.05f) },
                { Sfx.Explosion, (1f, 6, 12f, 3500f, 0.06f) },
                { Sfx.Splash, (0.8f, 6, 10f, 1500f, 0.08f) },
                { Sfx.ArmorHit, (0.9f, 4, 10f, 1500f, 0.05f) },
                { Sfx.BulletImpact, (0.5f, 6, 2f, 60f, 0.1f) },
                { Sfx.BodyHit, (0.6f, 3, 2f, 40f, 0.08f) },
                { Sfx.Flyby, (0.55f, 3, 1f, 25f, 0.1f) },
                { Sfx.Blade, (0.8f, 3, 1.5f, 40f, 0.08f) },
                { Sfx.Scream, (0.55f, 3, 4f, 160f, 0.06f) },
                { Sfx.Footstep, (0.32f, 2, 1f, 25f, 0.08f) },
                { Sfx.Gear, (0.3f, 2, 1f, 15f, 0.06f) },
                { Sfx.Horse, (0.7f, 2, 4f, 120f, 0.05f) },
                { Sfx.Steam, (0.6f, 2, 8f, 400f, 0.05f) },
                { Sfx.Creak, (0.45f, 2, 4f, 80f, 0.06f) },
                { Sfx.BattleCry, (0.7f, 2, 10f, 600f, 0.03f) },
                { Sfx.Bolt, (0.6f, 2, 1f, 10f, 0.05f) },
            };

        private static GameAudio s_instance;
        private readonly Dictionary<string, Group> _groups = new Dictionary<string, Group>();
        private readonly List<Pending> _pending = new List<Pending>();
        private readonly Dictionary<string, int> _playing = new Dictionary<string, int>();
        private AudioSource[] _pool;
        private string[] _poolGroup;
        private int _next;

        /// <summary>Reproduce un sonido del grupo en un punto del mundo (con el retraso del sonido según la distancia).</summary>
        public static void Play(string group, Vector3 position, float volume = 1f) => Instance?.Queue(group, position, volume, false);

        /// <summary>Reproduce un sonido del grupo sin posición (pegado al oído del jugador).</summary>
        public static void PlayFlat(string group, float volume = 1f) => Instance?.Queue(group, Vector3.zero, volume, true);

        /// <summary>Un clip al azar del grupo, para sonidos en bucle (estela, galope, ambiente).</summary>
        public static AudioClip Clip(string group)
        {
            GameAudio audio = Instance;
            if (audio == null) return null;
            Group g = audio.Get(group);
            return g.Clips.Length == 0 ? null : g.Clips[Random.Range(0, g.Clips.Length)];
        }

        private static GameAudio Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject("Sonido");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<GameAudio>();
                return s_instance;
            }
        }

        private void Awake()
        {
            _pool = new AudioSource[PoolSize];
            _poolGroup = new string[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Voz_" + i);
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                _pool[i] = source;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private Group Get(string name)
        {
            if (_groups.TryGetValue(name, out Group g)) return g;
            Settings.TryGetValue(name, out var s);
            g = new Group
            {
                Volume = s.volume > 0f ? s.volume : 0.8f,
                Voices = s.voices > 0 ? s.voices : 3,
                MinDistance = s.min > 0f ? s.min : 3f,
                MaxDistance = s.max > 0f ? s.max : 200f,
                Pitch = s.pitch,
                Clips = Resources.LoadAll<AudioClip>("Sonido/" + name),
                Last = -1,
            };
            _groups[name] = g;
            return g;
        }

        private void Queue(string group, Vector3 position, float volume, bool flat)
        {
            float delay = 0f;
            if (!flat)
            {
                AudioListener listener = Listener();
                if (listener != null) delay = Vector3.Distance(listener.transform.position, position) / SpeedOfSound;
            }
            if (delay < 0.02f) Begin(group, position, volume, flat);
            else _pending.Add(new Pending { Group = group, Position = position, Volume = volume, Time = Time.time + delay, Flat = flat });
        }

        private void Update()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Time > Time.time) continue;
                Pending p = _pending[i];
                _pending.RemoveAt(i);
                Begin(p.Group, p.Position, p.Volume, p.Flat);
            }
        }

        private void Begin(string name, Vector3 position, float volume, bool flat)
        {
            Group g = Get(name);
            if (g.Clips.Length == 0) return;
            // Voces del grupo que aún suenan: si ya están todas ocupadas, este disparo se funde con los demás.
            int busy = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_poolGroup[i] == name && _pool[i].isPlaying) busy++;
            }
            if (busy >= g.Voices) return;

            int clip = Random.Range(0, g.Clips.Length);
            if (g.Clips.Length > 1 && clip == g.Last) clip = (clip + 1) % g.Clips.Length;
            g.Last = clip;
            _groups[name] = g;

            AudioSource source = FreeSource();
            _poolGroup[System.Array.IndexOf(_pool, source)] = name;
            source.transform.position = position;
            source.spatialBlend = flat ? 0f : 1f;
            source.minDistance = g.MinDistance;
            source.maxDistance = g.MaxDistance;
            source.pitch = 1f + Random.Range(-g.Pitch, g.Pitch);
            source.clip = g.Clips[clip];
            source.volume = Mathf.Clamp01(g.Volume * volume);
            source.Play();
        }

        private AudioSource FreeSource()
        {
            for (int n = 0; n < PoolSize; n++)
            {
                int i = (_next + n) % PoolSize;
                if (_pool[i].isPlaying) continue;
                _next = (i + 1) % PoolSize;
                return _pool[i];
            }
            // Todas ocupadas: se corta la más antigua.
            AudioSource oldest = _pool[_next];
            _next = (_next + 1) % PoolSize;
            oldest.Stop();
            return oldest;
        }

        /// <summary>Posición del oído (el AudioListener de la escena); NaN si no hay.</summary>
        public static Vector3 ListenerPosition
        {
            get
            {
                AudioListener listener = Listener();
                return listener != null ? listener.transform.position : new Vector3(float.NaN, 0f, 0f);
            }
        }

        /// <summary>¿El oído va montado en este objeto (el jugador que dispara)?</summary>
        public static bool ListenerBelongsTo(GameObject owner)
        {
            AudioListener listener = Listener();
            return listener != null && listener.transform.IsChildOf(owner.transform);
        }

        private static AudioListener s_listener;

        private static AudioListener Listener()
        {
            if (s_listener != null && s_listener.isActiveAndEnabled) return s_listener;
#if UNITY_2023_1_OR_NEWER
            s_listener = FindAnyObjectByType<AudioListener>();
#else
            s_listener = FindObjectOfType<AudioListener>();
#endif
            return s_listener;
        }
    }

    /// <summary>Nombres de los grupos de sonidos (carpetas de <c>Resources/Sonido</c>).</summary>
    public static class Sfx
    {
        public const string Rifle = "Fusil";
        public const string FieldGun = "Canon";
        public const string NavalGun = "CanonNaval";
        public const string Explosion = "Explosion";
        public const string Splash = "Pique";
        public const string ArmorHit = "ImpactoBlindaje";
        public const string BulletImpact = "ImpactoBala";
        public const string BodyHit = "ImpactoCuerpo";
        public const string Flyby = "Silbido";
        public const string Blade = "Bayoneta";
        public const string Scream = "Grito";
        public const string Footstep = "Paso";
        public const string Gear = "Correaje";
        public const string Horse = "Caballo";
        public const string Gallop = "Galope";
        public const string Steam = "Vapor";
        public const string Creak = "Crujido";
        public const string BattleCry = "GritoCarga";
        public const string Wake = "Estela";
        public const string Engine = "Maquina";
        public const string Bolt = "Cerrojo";
    }
}
