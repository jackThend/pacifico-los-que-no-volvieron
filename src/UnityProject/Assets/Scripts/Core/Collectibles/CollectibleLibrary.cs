using System.Collections.Generic;
using System.IO;

namespace Pacifico.Core.Collectibles
{
    /// <summary>Carga los coleccionables desde los Markdown del repositorio.</summary>
    public static class CollectibleLibrary
    {
        public const string ArchiveFolderName = "Archivo_Historico";

        public static IReadOnlyList<CollectibleSpec> LoadAll(string repositoryRoot)
        {
            var result = new List<CollectibleSpec>();
            foreach (var source in HistoricalDocumentSources.All)
            {
                var path = Path.Combine(repositoryRoot, source.RelativePath);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"No se encuentra el documento histórico {source.RelativePath}", path);
                }
                result.AddRange(CollectibleMarkdownParser.Parse(File.ReadAllText(path), source));
            }
            return result;
        }

        /// <summary>
        /// Sube por los directorios desde <paramref name="startDirectory"/> hasta
        /// encontrar la carpeta Archivo_Historico. Devuelve null si no existe.
        /// </summary>
        public static string FindRepositoryRoot(string startDirectory)
        {
            var current = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ArchiveFolderName))) return current.FullName;
                current = current.Parent;
            }
            return null;
        }
    }
}
