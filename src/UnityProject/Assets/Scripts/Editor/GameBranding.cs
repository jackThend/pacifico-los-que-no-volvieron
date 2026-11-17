using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Pacifico.EditorTools
{
    /// <summary>
    /// Imagen del juego en Windows: icono del ejecutable, nombre del estudio y logo de la pantalla de arranque. El logo
    /// y el icono los dibuja <c>tools/arte/generar_logo.py</c> (medallón con ancla y fusiles cruzados) en
    /// <c>Assets/Art/Logo</c>. <see cref="GameBuilder.BuildPlayer"/> lo aplica antes de cada compilación.
    /// </summary>
    public static class GameBranding
    {
        public const string IconPath = "Assets/Art/Logo/Icono.png";
        public const string LogoPath = "Assets/Art/Logo/Logo.png";
        /// <summary>Carpeta de partidas guardadas en Windows: AppData/LocalLow/&lt;estudio&gt;/&lt;juego&gt;.</summary>
        public const string CompanyName = "Proyecto Pacífico";

        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;

            Texture2D icon = Import(IconPath, TextureImporterType.Default, 1024);
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                // Windows pide un icono por cada tamaño; Unity reduce el mismo dibujo a cada uno.
                int sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length;
                var standalone = new Texture2D[sizes];
                for (int i = 0; i < sizes; i++) standalone[i] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standalone, IconKind.Any);
            }
            else Debug.LogWarning("[Pacífico] Falta " + IconPath + ": ejecuta tools/arte/generar_logo.py. El juego saldrá con el icono de Unity.");

            Import(LogoPath, TextureImporterType.Sprite, 2048);
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
                PlayerSettings.SplashScreen.backgroundColor = new Color(0.07f, 0.08f, 0.09f);
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, logo) };
            }
            AssetDatabase.SaveAssets();
        }

        private static Texture2D Import(string path, TextureImporterType type, int maxSize)
        {
            if (!File.Exists(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) return null;
            }
            bool changed = importer.textureType != type || importer.maxTextureSize != maxSize || importer.mipmapEnabled ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (changed)
            {
                importer.textureType = type;
                if (type == TextureImporterType.Sprite) importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
