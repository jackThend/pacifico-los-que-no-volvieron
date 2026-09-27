using Pacifico.Core.Common;
using Pacifico.Core.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pacifico.Effects
{
    /// <summary>
    /// Humo de pólvora negra de la escena (ROADMAP 3.3). Simula las bocanadas con <see cref="BlackPowderSmokeModel"/>
    /// (el mismo modelo que verifica la prueba de estrés de 20 disparos) y las dibuja con un único
    /// <see cref="ParticleSystem"/> cuyas partículas se escriben cada fotograma: varias láminas por bocanada con una
    /// textura gaussiana ruidosa generada al vuelo, sin assets externos.
    /// <para>
    /// La opacidad de cada lámina sale de la profundidad óptica del modelo, y las bocanadas pegadas a la cámara se
    /// desvanecen igual que en la medición (<see cref="BlackPowderSmokeModel.NearFade"/>): lo que se ve es lo que se
    /// ha medido. Cualquier arma llama a <see cref="Emit"/>; si no hay humo en la escena, no pasa nada.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BlackPowderSmoke : MonoBehaviour
    {
        [Header("Simulación")]
        [Tooltip("Viento (m/s, espacio del mundo). En Pisagua, la brisa del mar.")]
        [SerializeField] private Vector3 wind = Vector3.zero;
        [Tooltip("Constante de desvanecimiento (s). Licencia cinematográfica: el humo de 1879 tardaba mucho más en abrirse.")]
        [SerializeField, Range(0.3f, 4f)] private float fadeSeconds = 0.9f;
        [SerializeField, Range(8, 256)] private int maxPuffs = 64;

        [Header("Aspecto")]
        [Tooltip("Humo blanco grisáceo de las sales de potasio.")]
        [SerializeField] private Color smokeColor = new Color(0.86f, 0.85f, 0.82f, 1f);
        [Tooltip("Tono de la bocanada recién salida (más densa y oscura).")]
        [SerializeField] private Color freshColor = new Color(0.62f, 0.6f, 0.57f, 1f);
        [SerializeField, Range(1, 8)] private int spritesPerPuff = 4;
        [Tooltip("Material de partículas transparente. Si falta, se crea uno (URP Particles/Unlit o equivalente).")]
        [SerializeField] private Material material;

        [Header("Fogonazo")]
        [SerializeField] private bool muzzleFlash = true;
        [SerializeField] private Color flashColor = new Color(1f, 0.72f, 0.4f);
        [SerializeField] private float flashIntensity = 8f;
        [SerializeField] private float flashRange = 7f;
        [SerializeField] private float flashSeconds = 0.035f;

        /// <summary>Semiextensión de la lámina en radios de la sub-nube: la textura gaussiana cae a e⁻⁴ en el borde.</summary>
        private const float SpriteExtent = 2f;
        /// <summary>Radio de cada lámina respecto al de la bocanada y dispersión de sus centros.</summary>
        private const float SubRadius = 0.8f;
        private const float SubOffset = 0.5f;

        private static BlackPowderSmoke s_active;

        private ParticleSystem _particles;
        private ParticleSystem.Particle[] _buffer;
        private Light _flash;
        private float _flashAge = float.PositiveInfinity;
        private Material _runtimeMaterial;
        private Texture2D _texture;
        private Camera _camera;
        /// <summary>Láminas por bocanada fijadas en Awake: el búfer se dimensiona con ellas.</summary>
        private int _sprites;

        /// <summary>El humo activo de la escena (el último habilitado).</summary>
        public static BlackPowderSmoke Active => s_active;

        public BlackPowderSmokeModel Model { get; private set; }

        public Vector3 Wind
        {
            get => wind;
            set => wind = value;
        }

        /// <summary>
        /// Emite la bocanada de un disparo en el humo activo. Devuelve false si la escena no tiene humo.
        /// </summary>
        public static bool Emit(Vector3 muzzle, Vector3 direction, float powderChargeG, Vector3 shooterVelocity)
        {
            if (s_active == null) return false;
            s_active.EmitShot(muzzle, direction, powderChargeG, shooterVelocity);
            return true;
        }

        public void EmitShot(Vector3 muzzle, Vector3 direction, float powderChargeG, Vector3 shooterVelocity)
        {
            if (Model == null) return;
            if (Model.Emit(ToVec3(muzzle), ToVec3(direction), powderChargeG, ToVec3(shooterVelocity)) < 0) return;
            if (muzzleFlash && _flash != null)
            {
                _flash.transform.position = muzzle + direction.normalized * 0.15f;
                _flashAge = 0f;
            }
        }

        /// <summary>Claridad media del cono central de la vista (1: limpia), la métrica de la prueba de estrés.</summary>
        public float ViewClarity(Transform eye, float halfAngleDeg = 12f)
        {
            if (Model == null || eye == null) return 1f;
            return Model.ViewClarity(ToVec3(eye.position), ToVec3(eye.forward), halfAngleDeg);
        }

        private void Awake()
        {
            var settings = new BlackPowderSmokeSettings { MaxPuffs = maxPuffs, FadeSeconds = fadeSeconds };
            Model = new BlackPowderSmokeModel(settings, GetInstanceID());
            _sprites = spritesPerPuff;
            _buffer = new ParticleSystem.Particle[maxPuffs * _sprites];
            _particles = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
            if (muzzleFlash) CreateFlash();
        }

        private void OnEnable()
        {
            s_active = this;
        }

        private void OnDisable()
        {
            if (s_active == this) s_active = null;
        }

        private void OnDestroy()
        {
            // Material y textura creados al vuelo: no los libera nadie más.
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Model.Settings.FadeSeconds = fadeSeconds;
            Model.Wind = ToVec3(wind);
            Model.Step(dt);

            if (_flash != null)
            {
                _flashAge += dt;
                float k = Mathf.Exp(-_flashAge / flashSeconds);
                _flash.enabled = k > 0.02f;
                _flash.intensity = flashIntensity * k;
            }
        }

        /// <summary>En LateUpdate: la cámara ya está colocada para este fotograma (desvanecimiento cercano exacto).</summary>
        private void LateUpdate()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            Vector3 eye = _camera != null ? _camera.transform.position : transform.position;

            int n = 0;
            int count = Model.Count;
            for (int i = 0; i < count; i++)
            {
                SmokePuff puff = Model[i];
                Vector3 center = ToVector3(puff.Position);
                float fade = _camera != null ? Model.NearFade(Vector3.Distance(center, eye)) : 1f;
                if (fade <= 0f) continue;

                // K láminas superpuestas con opacidad a cada una suman, en el centro, 1 − (1 − a)^K = 1 − e^{−τ}.
                float alpha = 1f - Mathf.Exp(-puff.CenterOpticalDepth * fade / _sprites);
                Color tint = Color.Lerp(freshColor, smokeColor, Mathf.Clamp01(puff.Age / 0.6f));
                tint.a = alpha;
                float size = 2f * SpriteExtent * SubRadius * puff.RadiusM;

                for (int j = 0; j < _sprites; j++)
                {
                    uint h = Hash((uint)puff.Id * 16u + (uint)j);
                    var offset = new Vector3(Signed(h), Signed(Hash(h)), Signed(Hash(h + 1u))) * (SubOffset * puff.RadiusM);
                    float spin = (Unit(Hash(h + 2u)) - 0.5f) * 40f;
                    _buffer[n++] = new ParticleSystem.Particle
                    {
                        position = center + offset,
                        startSize = size * (0.85f + 0.3f * Unit(Hash(h + 3u))),
                        startColor = tint,
                        rotation = Unit(Hash(h + 4u)) * 360f + spin * puff.Age,
                        startLifetime = 100f,
                        remainingLifetime = 100f,
                        velocity = Vector3.zero,
                    };
                }
            }
            _particles.SetParticles(_buffer, n);
        }

        // ------------------------------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------------------------------

        private void ConfigureParticleSystem()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = _buffer.Length;
            main.startLifetime = 100f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            // Por defecto una partícula no pasa de media pantalla; el humo cercano puede ocupar más (y se desvanece).
            renderer.maxParticleSize = 3f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _texture = CreateSmokeTexture(64, GetInstanceID());
            _runtimeMaterial = material != null ? new Material(material) : CreateMaterial();
            if (_runtimeMaterial.HasProperty("_BaseMap")) _runtimeMaterial.SetTexture("_BaseMap", _texture);
            if (_runtimeMaterial.HasProperty("_MainTex")) _runtimeMaterial.SetTexture("_MainTex", _texture);
            renderer.sharedMaterial = _runtimeMaterial;

            _particles.Play();
        }

        private void CreateFlash()
        {
            var go = new GameObject("Fogonazo");
            go.transform.SetParent(transform, false);
            _flash = go.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = flashColor;
            _flash.range = flashRange;
            _flash.shadows = LightShadows.None;
            _flash.intensity = 0f;
            _flash.enabled = false;
        }

        /// <summary>
        /// Material transparente de partículas (mezcla alfa, sin escribir profundidad). Prueba URP y, si no está,
        /// los sombreadores integrados. En una build, el sombreador debe estar referenciado por un material de la
        /// escena (el constructor de Pisagua lo guarda como asset) o Shader.Find no lo encontrará.
        /// </summary>
        public static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "Humo_Polvora_Negra" };
            MakeTransparent(m);
            return m;
        }

        public static void MakeTransparent(Material m)
        {
            // URP: superficie transparente con mezcla alfa.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            // Particles/Standard Unlit: modo «Fade».
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            // URP 17 separa la mezcla del canal alfa.
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Textura de humo: perfil gaussiano (el de la nube del modelo) modulado por ruido fractal para romper la
        /// silueta circular, con el borde llevado a cero para que no se vea el cuadrado de la lámina.
        /// </summary>
        public static Texture2D CreateSmokeTexture(int size, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Humo_Polvora_Negra",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            float offset = (seed & 1023) * 0.37f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float rho = Mathf.Sqrt(u * u + v * v);
                    float noise = 0f, amplitude = 0.5f, frequency = 3f;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        noise += amplitude * Mathf.PerlinNoise(u * frequency + offset, v * frequency - offset);
                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }
                    // noise ∈ [0, ~0,94]: la densidad varía ±35 % y el contorno se deforma.
                    float warped = rho * (0.85f + 0.35f * noise);
                    float gauss = Mathf.Exp(-(SpriteExtent * warped) * (SpriteExtent * warped));
                    float edge = 1f - Mathf.SmoothStep(0.85f, 1f, rho);
                    float alpha = Mathf.Clamp01(gauss * (0.7f + 0.6f * noise) * edge);
                    byte shade = (byte)(215 + 40 * Mathf.Clamp01(noise));
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // ------------------------------------------------------------------------------------------

        /// <summary>Hash entero (Wang): variación visual estable por bocanada y lámina.</summary>
        private static uint Hash(uint x)
        {
            x = (x ^ 61u) ^ (x >> 16);
            x *= 9u;
            x ^= x >> 4;
            x *= 0x27d4eb2du;
            x ^= x >> 15;
            return x;
        }

        private static float Unit(uint h) => (h & 0xFFFFFFu) / 16777215f;

        private static float Signed(uint h) => Unit(h) * 2f - 1f;

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
