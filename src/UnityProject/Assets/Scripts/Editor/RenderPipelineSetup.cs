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
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return false;

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
            AssetDatabase.SaveAssets();
            ReimportModels();
            Debug.Log("[Pacífico] URP activado (" + AssetPath + ").");
            return true;
#else
            return false;
#endif
        }

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
