using System.Collections.Generic;
using System.IO;
using Pacifico.Core.Narrative;
using Pacifico.Data;
using UnityEditor;
using UnityEngine;

namespace Pacifico.EditorTools
{
    public static partial class HistoricalDataAssetGenerator
    {
        private const string ArchiveRootPrefix = "Archivo_Historico/";

        /// <summary>Raíz del repositorio (el proyecto de Unity vive en src/UnityProject).</summary>
        private static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ProjectPaths.HistoricalArchiveFromProjectRoot, ".."));

        private static void GenerateCollectibles(List<string> report)
        {
            string root = RepositoryRoot;
            if (!Directory.Exists(Path.Combine(root, "Archivo_Historico")))
            {
                report.Add("[OMITIDO] Coleccionables: no se encontró el Archivo Histórico en " + root);
                return;
            }

            foreach (CollectibleRecord record in CollectibleCatalog.BuildFromRepository(root))
            {
                var asset = LoadOrCreate<CollectibleDataSO>(ProjectPaths.CollectibleData, "Collectible_" + record.Id);
                asset.CopyFrom(record);
                if (!string.IsNullOrEmpty(record.FacsimileImage))
                {
                    asset.facsimile = ImportArchiveTexture(root, record.FacsimileImage) ?? asset.facsimile;
                }
                Finish(asset, record.Validate().IsValid, report);
            }
        }

        /// <summary>
        /// Copia una imagen del Archivo Histórico a Assets/ArchivoImportado (ignorado por git) y la importa como textura.
        /// </summary>
        private static Texture2D ImportArchiveTexture(string repositoryRoot, string repositoryRelativePath)
        {
            string source = Path.Combine(repositoryRoot, repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                Debug.LogWarning("[Pacífico] Falta el facsímil " + repositoryRelativePath);
                return null;
            }

            string relative = repositoryRelativePath.StartsWith(ArchiveRootPrefix)
                ? repositoryRelativePath.Substring(ArchiveRootPrefix.Length)
                : Path.GetFileName(repositoryRelativePath);
            string assetPath = ProjectPaths.ImportedArchive + "/" + relative;
            EnsureFolder(assetPath.Substring(0, assetPath.LastIndexOf('/')));

            string destination = Path.GetFullPath(assetPath);
            if (!File.Exists(destination) || new FileInfo(destination).Length != new FileInfo(source).Length)
            {
                File.Copy(source, destination, true);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
