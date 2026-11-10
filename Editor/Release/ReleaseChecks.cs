using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Validation;

using UnityEditor;

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

        private const string PackageRule = "Устройство пакета";

        private const string ReferenceRule = "Ссылка вне пакета";

        private const string ManifestRule = "Манифест выпуска";

        private const string PrepareHint = "Выполните «" + ReleasePreparation.MenuPath + "» и закоммитьте манифест.";

        private static LocationPackage ReadPackage(
            string manifestPath,
            List<string> errors)
        {
            var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));

            if (string.IsNullOrWhiteSpace(manifest.name))
            {
                AddError(
                    errors,
                    manifestPath,
                    PackageRule,
                    "Не указано имя пакета (name).");
            }

            if (string.IsNullOrWhiteSpace(manifest.version))
            {
                AddError(
                    errors,
                    manifestPath,
                    PackageRule,
                    "Не указана версия пакета (version).");
            }

            return new LocationPackage(
                ToUnityPath(Path.GetDirectoryName(manifestPath)),
                manifest.name,
                manifest.version);
        }

        // Unity при подключении пакета выкладывает только его папку, без корня репозитория.
        // Без своих правил LFS в этой папке модели и текстуры придут в игру файлами-указателями.
        private static void CheckLfsRules(
            string packageFolder,
            List<string> errors)
        {
            var attributesPath = $"{packageFolder}/{AttributesFileName}";

            if (!File.Exists(attributesPath))
            {
                AddError(
                    errors,
                    packageFolder,
                    PackageRule,
                    $"Нет файла {AttributesFileName} с правилами LFS: модели и текстуры придут в игру указателями.");

                return;
            }

            var attributes = File.ReadAllText(attributesPath);

            if (!attributes.Contains(
                    LfsFilter,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    errors,
                    attributesPath,
                    PackageRule,
                    $"Нет правил LFS ({LfsFilter}).");
            }
        }

        // В игру попадает только папка пакета. Ссылка на файл из остального проекта художников
        // в игре окажется пустой: у художника объект выглядит нормально, а в игре, например, станет розовым.
        private static void CheckExternalReferences(
            LocationPackage package,
            List<string> errors)
        {
            var assetPaths =
                AssetDatabase
                    .FindAssets(
                        string.Empty,
                        new[] { package.Folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct();

            foreach (var assetPath in assetPaths)
            {
                var outside =
                    AssetDatabase
                        .GetDependencies(
                            assetPath,
                            false)
                        .Where(dependency => dependency.StartsWith(AssetsPath + "/", StringComparison.OrdinalIgnoreCase))
                        .Where(dependency => !package.Contains(dependency));

                foreach (var dependency in outside)
                {
                    AddError(
                        errors,
                        assetPath,
                        ReferenceRule,
                        $"Ссылка на файл вне пакета: {dependency}. В игре она окажется пустой.");
                }
            }
        }

        private static void CheckManifest(
            LocationPackage package,
            List<string> errors)
        {
            var saved = ManifestBuilder.Load(package.Folder);

            if (saved == null)
            {
                AddError(
                    errors,
                    package.Folder,
                    ManifestRule,
                    $"Нет манифеста выпуска. {PrepareHint}");

                return;
            }

            var current = package.BuildManifest();

            var mismatches =
                ManifestComparer.FindMismatches(
                    saved,
                    current);

            foreach (var mismatch in mismatches)
            {
                AddError(
                    errors,
                    package.Folder,
                    ManifestRule,
                    $"Манифест выпуска устарел. {mismatch} {PrepareHint}");
            }
        }

        // Ошибка попадает и в вывод для push, и в окно отчёта, чтобы художник видел все ошибки в одном месте.
        private static void AddError(
            List<string> errors,
            string assetPath,
            string rule,
            string message)
        {
            var issue =
                new ValidationIssue(
                        ValidationSeverity.Error,
                        assetPath,
                        string.Empty,
                        message)
                    .WithRule(rule);

            ValidationReport.Add(issue);
            errors.Add(Describe(issue));
        }

        private static IEnumerable<string> FilterRuleErrors(
            IEnumerable<ValidationIssue> issues,
            IReadOnlyList<LocationPackage> packages)
            => issues
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Where(issue => packages.Any(package => package.Contains(issue.AssetPath)))
                .Select(Describe);

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
        /// Находит пакеты локаций в Assets и проверяет их устройство.
        /// </summary>
        /// <param name="errors">Список, в который добавляются ошибки устройства пакетов.</param>
        /// <returns>Найденные пакеты.</returns>
        public static List<LocationPackage> FindPackages(List<string> errors)
        {
            var manifestPaths =
                Directory.GetFiles(
                    AssetsPath,
                    PackageFileName,
                    SearchOption.AllDirectories);

            if (manifestPaths.Length == 0)
            {
                AddError(
                    errors,
                    AssetsPath,
                    PackageRule,
                    $"Нет пакета локации: не найден {PackageFileName}.");
            }

            var packages = new List<LocationPackage>();

            foreach (var manifestPath in manifestPaths.Select(ToUnityPath))
            {
                var package =
                    ReadPackage(
                        manifestPath,
                        errors);

                CheckLfsRules(
                    package.Folder,
                    errors);

                packages.Add(package);
            }

            return packages;
        }

        /// <summary>
        /// Проверяет пакеты локаций перед отправкой на сервер.
        /// </summary>
        /// <param name="requireManifest"><c>true</c> при выпуске версии: манифест должен быть записан и совпадать с файлами.</param>
        /// <returns>Ошибки, из-за которых отправка запрещена; пустой список, если отправлять можно.</returns>
        public static List<string> Run(bool requireManifest)
        {
            // Пересканирование очищает окно отчёта, поэтому идёт первым: остальные проверки дописывают в него свои ошибки.
            var issues = LocationScanner.Rescan();

            var errors = new List<string>();
            var packages = FindPackages(errors);

            errors.AddRange(
                FilterRuleErrors(
                    issues,
                    packages));

            foreach (var package in packages)
            {
                CheckExternalReferences(
                    package,
                    errors);

                if (requireManifest)
                {
                    CheckManifest(
                        package,
                        errors);
                }
            }

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
