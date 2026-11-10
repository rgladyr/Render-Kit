using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Release;

using UnityEditor.PackageManager;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Пакет локации, подключённый к игре через Packages/manifest.json.
    /// </summary>
    internal sealed class InstalledLocation
    {
        private const string LocationsFolder = "Locations";

        private InstalledLocation(PackageInfo package)
        {
            this.Name = package.name;
            this.Version = package.version;
            this.AssetFolder = package.assetPath;
            this.DiskFolder = package.resolvedPath;
            this.ManifestPath =
                Path.Combine(
                    package.resolvedPath,
                    ReleaseManifest.FileName);

            this.Manifest = ManifestBuilder.LoadFile(this.ManifestPath);
        }

        /// <summary>
        /// Имя пакета.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Версия из package.json.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Папка пакета для AssetDatabase: Packages/имя.
        /// </summary>
        public string AssetFolder { get; }

        /// <summary>
        /// Папка пакета на диске.
        /// </summary>
        public string DiskFolder { get; }

        /// <summary>
        /// Путь к манифесту выпуска на диске.
        /// </summary>
        public string ManifestPath { get; }

        /// <summary>
        /// Манифест выпуска, пришедший с пакетом; <c>null</c>, если версию выпустили без подготовки выпуска.
        /// </summary>
        public ReleaseManifest Manifest { get; }

        // Пакет без манифеста тоже считается локацией, если в нём есть папка Locations, как на этапе импорта:
        // иначе версия, выпущенная без подготовки, просто пропала бы из приёма.
        private static bool IsLocationPackage(PackageInfo package)
            => package.source != PackageSource.Registry
            && package.source != PackageSource.BuiltIn
            && !string.IsNullOrEmpty(package.resolvedPath)
            && Directory.Exists(package.resolvedPath)
            && (File.Exists(
                    Path.Combine(
                        package.resolvedPath,
                        ReleaseManifest.FileName))
             || Directory
                    .EnumerateDirectories(
                        package.resolvedPath,
                        LocationsFolder,
                        SearchOption.AllDirectories)
                    .Any());

        /// <summary>
        /// Находит все подключённые пакеты локаций.
        /// </summary>
        /// <returns>Пакеты с манифестом выпуска или папкой Locations.</returns>
        public static List<InstalledLocation> FindAll()
            => PackageInfo
                .GetAllRegisteredPackages()
                .Where(IsLocationPackage)
                .Select(package => new InstalledLocation(package))
                .ToList();
    }
}
