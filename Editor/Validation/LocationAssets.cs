using System;
using System.Collections.Generic;
using System.IO;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Определяет, относится ли файл к локации, и находит папку пакета локации.
    /// </summary>
    /// <remarks>
    /// <para>Пакет локации — папка с package.json, в котором есть ключевое слово <see cref="Keyword"/>,
    /// или с манифестом выпуска. Просто package.json не годится: он есть и у сторонних библиотек, например у Zenject.</para>
    /// <para>Для пакетов, подключённых к игре через Packages, по-прежнему работает старый признак — папка Locations в пути.</para>
    /// </remarks>
    internal static class LocationAssets
    {
        /// <summary>
        /// Ключевое слово в package.json, которым помечается пакет локации.
        /// </summary>
        public const string Keyword = "paperfly-location";

        private const string AssetsPrefix = "Assets/";

        private const string PackageFileName = "package.json";

        private const string ReleaseManifestFileName = "release-manifest.json";

        private static readonly object SyncRoot = new object();

        // Папка → является ли она пакетом локации. Проверка идёт на каждый импортируемый файл, поэтому результат запоминается.
        private static readonly Dictionary<string, bool> PackageFolders = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static string Normalize(string path)
            => path.Replace(
                '\\',
                '/');

        private static string GetParent(string path)
        {
            var separator = path.LastIndexOf('/');

            return separator < 0
                ? string.Empty
                : path.Substring(
                    0,
                    separator);
        }

        private static bool CheckPackageFolder(string folder)
        {
            if (File.Exists($"{folder}/{ReleaseManifestFileName}"))
            {
                return true;
            }

            var packagePath = $"{folder}/{PackageFileName}";

            return File.Exists(packagePath)
                && File.ReadAllText(packagePath).Contains(
                    $"\"{Keyword}\"",
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, является ли папка пакетом локации.
        /// </summary>
        /// <param name="folder">Папка относительно проекта.</param>
        /// <returns><c>true</c>, если в папке package.json с ключевым словом локации или манифест выпуска.</returns>
        public static bool IsPackageFolder(string folder)
        {
            lock (SyncRoot)
            {
                if (!PackageFolders.TryGetValue(
                        folder,
                        out var isPackage))
                {
                    isPackage = CheckPackageFolder(folder);
                    PackageFolders[folder] = isPackage;
                }

                return isPackage;
            }
        }

        /// <summary>
        /// Находит все пакеты локаций, которые лежат в Assets исходниками.
        /// </summary>
        /// <returns>Пути к package.json пакетов локаций относительно проекта.</returns>
        public static List<string> FindSourcePackageFiles()
        {
            var packageFiles = new List<string>();

            foreach (var path in Directory.GetFiles(
                         AssetsPrefix.TrimEnd('/'),
                         PackageFileName,
                         SearchOption.AllDirectories))
            {
                var packageFile = Normalize(path);

                if (IsPackageFolder(GetParent(packageFile)))
                {
                    packageFiles.Add(packageFile);
                }
            }

            return packageFiles;
        }

        /// <summary>
        /// Находит пакет локации, в который входит файл из Assets.
        /// </summary>
        /// <param name="assetPath">Путь к файлу относительно проекта.</param>
        /// <returns>Папка пакета или <c>null</c>, если файл не входит в пакет локации.</returns>
        public static string FindPackageFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var path = Normalize(assetPath);

            if (!path.StartsWith(
                    AssetsPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            for (var folder = GetParent(path); folder.Length >= AssetsPrefix.Length; folder = GetParent(folder))
            {
                if (IsPackageFolder(folder))
                {
                    return folder;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет, входит ли файл в локацию.
        /// </summary>
        /// <param name="assetPath">Путь к файлу в проекте.</param>
        /// <returns><c>true</c> для файлов пакета локации в Assets и для файлов в папке Locations подключённых пакетов.</returns>
        public static bool IsLocationAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var path = Normalize(assetPath);

            return path.StartsWith(
                    AssetsPrefix,
                    StringComparison.OrdinalIgnoreCase)
                ? FindPackageFolder(path) != null
                : path.Contains(
                    Constants.Paths.LocationMarker,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
