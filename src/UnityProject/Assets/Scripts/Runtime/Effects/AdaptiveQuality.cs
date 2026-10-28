using UnityEngine;
#if PACIFICO_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Pacifico.Effects
{
    /// <summary>
    /// Calidad adaptativa para que el juego vaya fluido también en gráficas integradas (medido en una Intel Iris Xe,
    /// 2026-10-27): en ellas empieza dibujando la imagen al 75 % de la resolución y sin oclusión ambiental; después,
    /// en cualquier equipo, baja la resolución interna (hasta el 60 %) si no llega a ~55 imágenes por segundo y la sube
    /// (hasta el 100 %) cuando sobra. Se crea sola al arrancar el juego y dura todas las escenas.
    /// </summary>
    public sealed class AdaptiveQuality : MonoBehaviour
    {
        private const float MinScale = 0.6f;
        private const float MaxScale = 1f;
        private const float Step = 0.05f;
        /// <summary>Segundos de cada medida (valor de ajuste).</summary>
        private const float Window = 2f;

        private float _elapsed;
        private int _frames;
        private float _originalScale = 1f;
#if PACIFICO_URP
        private readonly System.Collections.Generic.List<ScriptableRendererFeature> _disabled = new System.Collections.Generic.List<ScriptableRendererFeature>();
#endif

        /// <summary>¿Gráfica integrada (comparte la memoria del sistema)?</summary>
        public static bool IntegratedGpu()
        {
            string name = SystemInfo.graphicsDeviceName ?? string.Empty;
            return SystemInfo.graphicsMemorySize < 2048 || name.Contains("Intel") || name.Contains("UHD") || name.Contains("Iris")
                   || (name.Contains("Radeon") && name.Contains("Graphics") && !name.Contains("RX"));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("Calidad_Adaptativa");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveQuality>();
        }

        private void Start()
        {
            _originalScale = CurrentScale();
            if (!IntegratedGpu()) return;
            SetScale(0.75f);
#if PACIFICO_URP
            // La oclusión ambiental en pantalla es lo más caro del posproceso en una integrada.
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset asset)
            {
                foreach (ScriptableRendererData data in asset.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                    {
                        if (!(feature is ScreenSpaceAmbientOcclusion) || !feature.isActive) continue;
                        feature.SetActive(false);
                        _disabled.Add(feature);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Deja el asset de URP como estaba: en el editor los cambios hechos al jugar se quedarían guardados en el
        /// proyecto.
        /// </summary>
        private void OnDestroy()
        {
            SetScale(_originalScale);
#if PACIFICO_URP
            foreach (ScriptableRendererFeature feature in _disabled)
            {
                if (feature != null) feature.SetActive(true);
            }
            _disabled.Clear();
#endif
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < Window) return;
            float fps = _frames / _elapsed;
            _elapsed = 0f;
            _frames = 0;
            float scale = CurrentScale();
            if (fps < 52f) SetScale(scale - Step);
            else if (fps > 58.5f && scale < MaxScale) SetScale(scale + Step * 0.5f);
        }

        private static float CurrentScale()
        {
#if PACIFICO_URP
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset asset) return asset.renderScale;
#endif
            return 1f;
        }

        private static void SetScale(float scale)
        {
#if PACIFICO_URP
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset asset) asset.renderScale = Mathf.Clamp(scale, MinScale, MaxScale);
#endif
        }
    }
}
