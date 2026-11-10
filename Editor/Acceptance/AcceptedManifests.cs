using System;
using System.IO;

using PaperFly.RenderKit.Release;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Принятые версии локаций: копии манифестов выпуска в ProjectSettings игры.
    /// </summary>
    /// <remarks>
    /// Файлы лежат вне Assets, чтобы Unity их не импортировала, и коммитятся в репозиторий игры вместе с новой версией пакета.
    /// </remarks>
    internal static class AcceptedManifests
    {
        /// <summary>
        /// Папка принятых манифестов относительно проекта.
        /// </summary>
        public const string Folder = "ProjectSettings/PaperFly/AcceptedLocations";

        private const string Extension = ".json";

        private static string GetPath(string packageName)
            => $"{Folder}/{packageName}{Extension}";

        /// <summary>
        /// Читает принятый манифест пакета.
        /// </summary>
        /// <param name="packageName">Имя пакета.</param>
        /// <returns>Манифест или <c>null</c>, если пакет ещё не принимали.</returns>
        public static ReleaseManifest Load(string packageName)
            => ManifestBuilder.LoadFile(GetPath(packageName));

        /// <summary>
        /// Проверяет, принята ли подключённая версия пакета.
        /// </summary>
        /// <param name="location">Подключённый пакет.</param>
        /// <returns><c>true</c>, если принятый манифест совпадает с пришедшим.</returns>
        public static bool IsAccepted(InstalledLocation location)
        {
            var path = GetPath(location.Name);

            return File.Exists(path)
                && string.Equals(
                    ManifestBuilder.Hash(path),
                    ManifestBuilder.Hash(location.ManifestPath),
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// Запоминает подключённую версию пакета как принятую.
        /// </summary>
        /// <param name="location">Подключённый пакет.</param>
        public static void Accept(InstalledLocation location)
        {
            Directory.CreateDirectory(Folder);

            File.Copy(
                location.ManifestPath,
                GetPath(location.Name),
                true);
        }
    }
}
