using System;
using System.IO;
using NUnit.Framework;

namespace Pacifico.Tests
{
    /// <summary>
    /// Localiza la raíz del repositorio tanto desde el Unity Test Runner (cwd = src/UnityProject)
    /// como desde el arnés dotnet (cwd = carpeta bin del proyecto de pruebas).
    /// </summary>
    public static class TestPaths
    {
        private const string Marker = "Archivo_Historico";

        public static string RepositoryRoot
        {
            get
            {
                string found = FindUpwards(TestContext.CurrentContext.TestDirectory) ?? FindUpwards(Directory.GetCurrentDirectory());
                if (found == null) throw new DirectoryNotFoundException("No se encontró la carpeta '" + Marker + "' en ningún directorio ancestro.");
                return found;
            }
        }

        public static string HistoricalArchive => Path.Combine(RepositoryRoot, Marker);

        public static string LettersFolder => Path.Combine(HistoricalArchive, "05_Cartas_y_Documentos");

        private static string FindUpwards(string start)
        {
            if (string.IsNullOrEmpty(start)) return null;
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, Marker))) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
