using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Validation;

using UnityEngine;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Проверки пакета локации, без которых изменения нельзя отправить на сервер.
    /// </summary>
    internal static class ReleaseChecks
    {
        private const string AssetsPath = "Assets";

        private const string PackageFileName = "package.json";

        private const string AttributesFileName = ".gitattributes";

        private const string LfsFilter = "filter=lfs";

        private static void CheckManifest(
            string manifestPath,
            List<string> errors)
        {
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));

            if (string.IsNullOrWhiteSpace(manifest.name))
            {
                errors.Add($"{manifestPath}: не указано имя пакета (name).");
            }

            if (string.IsNullOrWhiteSpace(manifest.version))
            {
                errors.Add($"{manifestPath}: не указана версия пакета (version).");
            }
        }

        // Unity при подключении пакета выкладывает только его папку, без корня репозитория.
        // Без своих правил LFS в этой папке модели и текстуры придут в игру файлами-указателями.
        private static void CheckLfsRules(
            string packageFolder,
            List<string> errors)
        {
            var attributesPath =
                Path.Combine(
                    packageFolder,
                    AttributesFileName);

            if (!File.Exists(attributesPath))
            {
                errors.Add($"{packageFolder}: нет файла {AttributesFileName} с правилами LFS.");

                return;
            }

            var attributes = File.ReadAllText(attributesPath);

            if (!attributes.Contains(
                    LfsFilter,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{ToUnityPath(attributesPath)}: нет правил LFS ({LfsFilter}).");
            }
        }

        private static void CheckPackages(List<string> errors)
        {
            var manifestPaths =
                Directory.GetFiles(
                    AssetsPath,
                    PackageFileName,
                    SearchOption.AllDirectories);

            if (manifestPaths.Length == 0)
            {
                errors.Add($"В папке {AssetsPath} нет пакета локации: не найден {PackageFileName}.");

                return;
            }

            foreach (var manifestPath in manifestPaths.Select(ToUnityPath))
            {
                CheckManifest(
                    manifestPath,
                    errors);

                CheckLfsRules(
                    ToUnityPath(Path.GetDirectoryName(manifestPath)),
                    errors);
            }
        }

        private static string Describe(ValidationIssue issue)
        {
            var location =
                string.IsNullOrEmpty(issue.ObjectPath)
                    ? issue.AssetPath
                    : $"{issue.AssetPath} → {issue.ObjectPath}";

            return $"{location}: [{issue.Rule}] {issue.Message}";
        }

        private static string ToUnityPath(string path)
            => path.Replace(
                '\\',
                '/');

        /// <summary>
        /// Проверяет устройство пакетов локаций и прогоняет правила импорта по всем их файлам.
        /// </summary>
        /// <returns>Ошибки, из-за которых отправка запрещена; пустой список, если отправлять можно.</returns>
        public static List<string> Run()
        {
            var errors = new List<string>();

            CheckPackages(errors);

            errors.AddRange(
                LocationScanner
                    .Rescan()
                    .Where(issue => issue.Severity == ValidationSeverity.Error)
                    .Select(Describe));

            return errors;
        }

        /// <summary>
        /// Поля package.json, которые нужны проверке. Имена совпадают с ключами JSON, как требует JsonUtility.
        /// </summary>
        [Serializable]
        private sealed class PackageManifest
        {
            public string name;

            public string version;
        }
    }
}
