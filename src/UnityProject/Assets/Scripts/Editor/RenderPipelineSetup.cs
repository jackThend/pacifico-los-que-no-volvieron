using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if PACIFICO_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Activa URP: crea el asset del pipeline y su renderer si faltan y los asigna en Graphics Settings. Sin esto Unity
    /// usa el pipeline integrado y todos los materiales URP Lit del juego se ven de color magenta. Tras el cambio
    /// reimporta los GLB, porque glTFast elige sus shaders según el pipeline activo al importar.
    /// </summary>
    internal static class RenderPipelineSetup
    {
        public const string Folder = "Assets/Settings";
        public const string AssetPath = Folder + "/Pacifico_URP.asset";
        public const string RendererPath = Folder + "/Pacifico_URP_Renderer.asset";

        /// <summary>True si ha cambiado el pipeline activo.</summary>
        public static bool Ensure()
        {
#if PACIFICO_URP
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset current)
            {
                ConfigureQuality(current);
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                HistoricalDataAssetGenerator.EnsureFolder(Folder);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            ConfigureQuality(asset);
            AssetDatabase.SaveAssets();
            ReimportModels();
            Debug.Log("[Pacífico] URP activado (" + AssetPath + ").");
            return true;
#else
            return false;
#endif
        }

#if PACIFICO_URP
        /// <summary>
        /// Calidad de imagen para un juego de PC realista que también vaya fluido en gráficas integradas: HDR, sombras
        /// hasta 150 m en dos cascadas y oclusión ambiental en pantalla (sombra en rincones y bajo los objetos). Se aplica siempre, también
        /// a un asset ya creado, para que los proyectos abiertos antes reciban los cambios.
        /// </summary>
        private static void ConfigureQuality(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            void Set(string name, System.Action<SerializedProperty> apply)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p != null) apply(p);
            }
            Set("m_SupportsHDR", p => p.boolValue = true);
            // Sin MSAA: el posproceso ya suaviza los bordes (SMAA) y el MSAA ×4 costaba mucho en gráficas integradas.
            Set("m_MSAA", p => p.intValue = 1);
            Set("m_MainLightShadowsSupported", p => p.boolValue = true);
            Set("m_MainLightShadowmapResolution", p => p.intValue = 2048);
            Set("m_AdditionalLightShadowsSupported", p => p.boolValue = true);
            // Medido en una Intel Iris Xe (2026-10-27): 4 cascadas de 4096 a 200 m dejaban Angamos a 23 fps.
            Set("m_ShadowDistance", p => p.floatValue = 150f);
            Set("m_ShadowCascadeCount", p => p.intValue = 2);
            Set("m_SoftShadowsSupported", p => p.boolValue = true);
            // El mar lee la hondura de lo que hay bajo él (espuma de orilla, transparencia).
            Set("m_RequireDepthTexture", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Filtrado anisótropo en todos los niveles de calidad (los suelos se ven casi de canto). ProjectSettings no
            // va en el repositorio, así que se fija aquí.
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable) QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            }
            QualitySettings.SetQualityLevel(level, false);

            // La oclusión ambiental es una «renderer feature» de URP; su clase no es pública en todas las versiones.
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            System.Type ssao = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (renderer != null && ssao != null && !renderer.rendererFeatures.Exists(f => f != null && f.GetType() == ssao))
            {
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssao);
                feature.name = "Oclusion_Ambiental";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                var rso = new SerializedObject(renderer);
                SerializedProperty map = rso.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    map.arraySize++;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id);
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(renderer);
            }
            // Oclusión ambiental a media resolución y con menos muestras (lo más caro del posproceso en gráficas integradas).
            if (renderer != null && ssao != null)
            {
                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature == null || feature.GetType() != ssao) continue;
                    var settings = new SerializedObject(feature);
                    SerializedProperty downsample = settings.FindProperty("m_Settings.Downsample");
                    if (downsample != null) downsample.boolValue = true;
                    SerializedProperty samples = settings.FindProperty("m_Settings.Samples");
                    if (samples != null) samples.enumValueIndex = Mathf.Min(2, samples.enumNames.Length - 1); // baja
                    SerializedProperty blur = settings.FindProperty("m_Settings.BlurQuality");
                    if (blur != null) blur.enumValueIndex = Mathf.Min(1, blur.enumNames.Length - 1); // media
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(asset);
        }
#endif

        private static void ReimportModels()
        {
            if (!Directory.Exists(ProjectPaths.ArtCCBY)) return;
            foreach (string file in Directory.GetFiles(ProjectPaths.ArtCCBY, "*.glb", SearchOption.AllDirectories))
            {
                AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
