using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor.PackageManager;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Git-пакет локации, подключённый к проекту. Список берётся из Package Manager, поэтому отдельная настройка не нужна.
    /// </summary>
    internal sealed class GitLocation
    {
        private GitLocation(
            string name,
            string packageAddress,
            string repositoryUrl,
            string currentHash)
        {
            this.Name = name;
            this.PackageAddress = packageAddress;
            this.RepositoryUrl = repositoryUrl;
            this.CurrentHash = currentHash;
        }

        /// <summary>
        /// Имя пакета.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Адрес пакета без версии: для Package Manager, вместе с подпапкой ?path=.
        /// </summary>
        public string PackageAddress { get; }

        /// <summary>
        /// Адрес репозитория без подпапки и версии: для git.
        /// </summary>
        public string RepositoryUrl { get; }

        /// <summary>
        /// Полный хеш подключённого коммита.
        /// </summary>
        public string CurrentHash { get; }

        /// <summary>
        /// Возвращает все подключённые git-пакеты PaperFly, кроме самого Render Kit.
        /// </summary>
        /// <returns>Пакеты, отсортированные по имени.</returns>
        public static List<GitLocation> GetAll()
        {
            var renderKitName = PackageInfo.FindForAssembly(typeof(GitLocation).Assembly)?.name;

            return PackageInfo
                .GetAllRegisteredPackages()
                .Where(package => package.source == PackageSource.Git)
                .Where(package => package.name.StartsWith(
                    Constants.PackagePrefix,
                    StringComparison.Ordinal))
                .Where(package => !string.Equals(
                    package.name,
                    renderKitName,
                    StringComparison.Ordinal))
                .Select(Create)
                .OrderBy(
                    location => location.Name,
                    StringComparer.Ordinal)
                .ToList();
        }

        // Вид packageId: com.paperfly.childhood@https://github.com/org/repo.git?path=/папка#хеш
        private static GitLocation Create(PackageInfo package)
        {
            var address = package.packageId.Substring(package.name.Length + 1);

            var packageAddress =
                address.Split(
                    '#',
                    2)[0];

            var repositoryUrl =
                packageAddress.Split(
                    '?',
                    2)[0];

            return new GitLocation(
                package.name,
                packageAddress,
                repositoryUrl,
                package.git?.hash ?? string.Empty);
        }
    }
}
