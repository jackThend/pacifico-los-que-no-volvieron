namespace Pacifico
{
    /// <summary>
    /// Rutas convencionales del proyecto (relativas a la raíz del proyecto de Unity).
    /// Centralizarlas evita cadenas mágicas repartidas entre herramientas de editor y runtime.
    /// </summary>
    public static class ProjectPaths
    {
        public const string Scripts = "Assets/Scripts";
        public const string Prefabs = "Assets/Prefabs";
        public const string Scenes = "Assets/Scenes";
        public const string ScriptableObjects = "Assets/ScriptableObjects";
        public const string Audio = "Assets/Audio";
        public const string Materials = "Assets/Materials";
        public const string UI = "Assets/UI";

        public const string WeaponData = ScriptableObjects + "/Weapons";
        public const string ShipData = ScriptableObjects + "/Ships";
        public const string CollectibleData = ScriptableObjects + "/Collectibles";

        /// <summary>Copias de imágenes del Archivo Histórico importadas como texturas (ignoradas por git).</summary>
        public const string ImportedArchive = "Assets/ArchivoImportado";

        /// <summary>Archivo Histórico del repositorio, relativo a la raíz del proyecto de Unity (src/UnityProject).</summary>
        public const string HistoricalArchiveFromProjectRoot = "../../Archivo_Historico";

        public static readonly string[] RequiredFolders =
        {
            Scripts, Prefabs, Scenes, ScriptableObjects, Audio, Materials, UI,
        };
    }
}
