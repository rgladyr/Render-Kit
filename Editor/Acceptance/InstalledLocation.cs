using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Release;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Пакет локации, подключённый к игре через Packages/manifest.json: в нём лежит манифест выпуска.
    /// </summary>
    internal sealed class InstalledLocation
    {
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
        /// Манифест выпуска, пришедший с пакетом.
        /// </summary>
        public ReleaseManifest Manifest { get; }

        /// <summary>
        /// Находит все подключённые пакеты локаций.
        /// </summary>
        /// <returns>Пакеты, в которых есть манифест выпуска.</returns>
        public static List<InstalledLocation> FindAll()
            => PackageInfo
                .GetAllRegisteredPackages()
                .Where(package => File.Exists(Path.Combine(
                    package.resolvedPath,
                    ReleaseManifest.FileName)))
                .Select(package => new InstalledLocation(package))
                .ToList();
    }
}
