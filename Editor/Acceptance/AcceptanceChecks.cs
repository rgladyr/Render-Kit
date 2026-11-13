using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Acceptance.Conflicts;
using PaperFly.RenderKit.Acceptance.Rescue;
using PaperFly.RenderKit.Release;
using PaperFly.RenderKit.Validation;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Проверяет подключённые к игре версии локаций: всё ли пришло и не потеряли ли правки игры связь с объектами пакета.
    /// </summary>
    internal static class AcceptanceChecks
    {
        /// <summary>
        /// Пункт меню окна приёма.
        /// </summary>
        public const string MenuPath = "PaperFly/Приём/Приём локаций";

        private const string DeliveryRule = "Поставка";

        private const string RenderKitRule = "Версия Render Kit";

        /// <summary>
        /// Правило потерянных правок: по нему окно приёма находит файлы для кнопки «Убрать потерянные правки».
        /// </summary>
        public const string LostLinkRule = "Потерянная правка";

        // Ссылка на префаб или модель целиком (m_SourcePrefab). Такого объекта нет среди содержимого файла, но он есть всегда.
        private const long PrefabAssetFileId = 100100000;

        // Unity при подключении дописывает в package.json служебные поля (_fingerprint), поэтому его хеш всегда другой.
        // Имя и версия из него сверяются отдельно.
        private const string PackageFileName = "package.json";

        private const string GuidConflictRule = "Конфликт GUID";

        private const string HolderRule = "Потерянное";

        private const string AssetsFolder = "Assets";

        private static readonly string[] HolderFileExtensions = { ".prefab", ".unity" };

        private static readonly string[] Rules = { DeliveryRule, RenderKitRule, LostLinkRule, GuidConflictRule, HolderRule };

        private static bool CanHoldLost(string path)
            => HolderFileExtensions.Any(extension => path.EndsWith(
                extension,
                StringComparison.OrdinalIgnoreCase));

        // Предупреждение остаётся, пока разработчик не разложит перенесённое из «кармана» и не удалит его.
        private static void CheckHolders(IReadOnlyCollection<AcceptanceResult> results)
        {
            var files =
                Directory
                    .GetFiles(
                        AssetsFolder,
                        "*",
                        SearchOption.AllDirectories)
                    .Where(CanHoldLost);

            foreach (var path in files)
            {
                var text = File.ReadAllText(path);
                var holders = LostOverrideRescue.FindHolders(text);

                if (holders.Count == 0)
                {
                    continue;
                }

                // «Карман» висит на экземпляре локации, поэтому в файле есть GUID одного из её файлов.
                var result =
                    results.FirstOrDefault(candidate => GetFiles(candidate.Location.Manifest)
                        .Where(entry => !string.IsNullOrEmpty(entry.guid))
                        .Any(entry => text.Contains(
                            entry.guid,
                            StringComparison.Ordinal)))
                 ?? results.FirstOrDefault();

                if (result == null)
                {
                    continue;
                }

                var assetPath =
                    path.Replace(
                        '\\',
                        '/');

                foreach (var holder in holders)
                {
                    AddIssue(
                        result,
                        ValidationSeverity.Warning,
                        assetPath,
                        HolderRule,
                        $"«{holder}»: сюда перенесено то, что было добавлено на удалённый объект локации. Разложите содержимое по нужным объектам и удалите «{holder}».");
                }
            }
        }

        // Пока у файла игры и файла пакета один GUID, Unity видит только один из них, и неизвестно, какой попадёт в сборку.
        private static void CheckGuidConflicts(
            AcceptanceResult result,
            IEnumerable<GuidConflict> conflicts)
        {
            var files =
                conflicts
                    .Where(conflict => !conflict.IsFolder)
                    .Where(conflict => string.Equals(
                        conflict.PackageName,
                        result.Location.Name,
                        StringComparison.Ordinal));

            foreach (var conflict in files)
            {
                AddIssue(
                    result,
                    ValidationSeverity.Error,
                    conflict.GamePath,
                    GuidConflictRule,
                    conflict.IsIdentical
                        ? $"Копия файла локации {conflict.PackagePath} с тем же GUID, содержимое совпадает. Удалите её из игры: ссылки игры сами перейдут на файл пакета."
                        : $"Копия файла локации {conflict.PackagePath} с тем же GUID, содержимое отличается. Если в ней игровые правки, перенесите их в локацию (через художника) или вынесите поверх, затем удалите копию из игры.");
            }
        }

        private static void AddIssue(
            AcceptanceResult result,
            ValidationSeverity severity,
            string assetPath,
            string rule,
            string message)
        {
            var issue =
                new ValidationIssue(
                        severity,
                        assetPath,
                        string.Empty,
                        message)
                    .WithRule(rule);

            result.Issues.Add(issue);
            ValidationReport.Add(issue);
        }

        private static void CheckVersions(AcceptanceResult result)
        {
            var location = result.Location;
            var manifest = location.Manifest;

            if (manifest.format != ReleaseManifest.CurrentFormat)
            {
                AddIssue(
                    result,
                    ValidationSeverity.Warning,
                    location.AssetFolder,
                    DeliveryRule,
                    $"Проверочная версия: манифест выпуска старого формата ({manifest.format}, нужен {ReleaseManifest.CurrentFormat}), по нему нельзя найти потерянные правки. Смотреть можно, принять нельзя: художнику нужно заново подготовить и выпустить версию.");
            }

            if (!string.Equals(
                    manifest.version,
                    location.Version,
                    StringComparison.Ordinal))
            {
                AddIssue(
                    result,
                    ValidationSeverity.Error,
                    location.AssetFolder,
                    DeliveryRule,
                    $"В манифесте выпуска версия {manifest.version}, в package.json {location.Version}: версию выпустили без подготовки выпуска.");
            }

            var renderKitVersion = PackageInfo.FindForAssembly(typeof(AcceptanceChecks).Assembly)?.version;

            if (string.Equals(
                    manifest.renderKitVersion,
                    renderKitVersion,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Первые две цифры — рендеринг и импорт: локацию нужно перевыпустить и проверить с новыми настройками.
            // Последняя — только инструменты: на локацию не влияет.
            var affectsLocation =
                !string.Equals(
                    GetRenderingVersion(manifest.renderKitVersion),
                    GetRenderingVersion(renderKitVersion),
                    StringComparison.Ordinal);

            AddIssue(
                result,
                affectsLocation
                    ? ValidationSeverity.Error
                    : ValidationSeverity.Warning,
                location.AssetFolder,
                RenderKitRule,
                affectsLocation
                    ? $"Локация выпущена с Render Kit {manifest.renderKitVersion}, в игре стоит {renderKitVersion}: изменились рендеринг или импорт. Художнику нужно обновить Render Kit, проверить локацию и выпустить её заново."
                    : $"Локация выпущена с Render Kit {manifest.renderKitVersion}, в игре стоит {renderKitVersion}: изменились только инструменты, на локацию это не влияет.");
        }

        // «0.2.1» → «0.2»: часть версии Render Kit, которая отвечает за рендеринг и импорт.
        private static string GetRenderingVersion(string version)
        {
            var parts = (version ?? string.Empty).Split('.');

            return parts.Length < 2
                ? version ?? string.Empty
                : $"{parts[0]}.{parts[1]}";
        }

        // Unity при подключении молча пропускает ошибки LFS, поэтому пустые модели и текстуры видны только при сверке файлов.
        private static void CheckDelivery(AcceptanceResult result)
        {
            var location = result.Location;
            var expected = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in location.Manifest.files)
            {
                expected.Add(entry.path);

                var diskPath =
                    Path.Combine(
                        location.DiskFolder,
                        entry.path);

                var assetPath = $"{location.AssetFolder}/{entry.path}";

                if (!File.Exists(diskPath))
                {
                    AddIssue(
                        result,
                        ValidationSeverity.Error,
                        location.AssetFolder,
                        DeliveryRule,
                        $"Не пришёл файл {entry.path}.");
                }
                else if (ManifestBuilder.IsLfsPointer(diskPath))
                {
                    AddIssue(
                        result,
                        ValidationSeverity.Error,
                        assetPath,
                        DeliveryRule,
                        $"Вместо файла {entry.path} пришёл указатель LFS: проверьте .gitattributes в папке пакета и что файл отправлен в LFS.");
                }
                else if (!string.Equals(
                             entry.path,
                             PackageFileName,
                             StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(
                             ManifestBuilder.Hash(diskPath),
                             entry.hash,
                             StringComparison.Ordinal))
                {
                    AddIssue(
                        result,
                        ValidationSeverity.Error,
                        assetPath,
                        DeliveryRule,
                        $"Файл {entry.path} отличается от выпущенного.");
                }
            }

            var extraFiles =
                Directory
                    .GetFiles(
                        location.DiskFolder,
                        "*",
                        SearchOption.AllDirectories)
                    .Select(path => path
                        .Substring(location.DiskFolder.Length + 1)
                        .Replace(
                            '\\',
                            '/'))
                    .Where(path => !string.Equals(
                        path,
                        ReleaseManifest.FileName,
                        StringComparison.OrdinalIgnoreCase))
                    .Where(path => !expected.Contains(path));

            foreach (var path in extraFiles)
            {
                AddIssue(
                    result,
                    ValidationSeverity.Warning,
                    $"{location.AssetFolder}/{path}",
                    DeliveryRule,
                    $"Файл {path} не описан в манифесте выпуска.");
            }
        }

        private static string DescribeLostTarget(
            AcceptanceResult result,
            ReferenceScanner.Reference reference,
            string currentAsset)
        {
            var previous =
                result.Accepted?.objects.FirstOrDefault(entry => string.Equals(
                    ManifestComparer.GetKey(entry),
                    ManifestComparer.GetKey(
                        reference.Guid,
                        reference.FileId),
                    StringComparison.Ordinal));

            return previous == null
                ? $"объект {reference.FileId} в {currentAsset}"
                : ManifestComparer.Describe(previous);
        }

        // Правка в игре хранит GUID файла пакета и идентификатор объекта в нём. Если объект пропал или получил
        // новый идентификатор (переименование в Blender), правка молча перестаёт применяться.
        private static void CheckLostLinks(
            AcceptanceResult result,
            IReadOnlyList<ReferenceScanner.Reference> references)
        {
            var manifest = result.Location.Manifest;

            if (manifest.format != ReleaseManifest.CurrentFormat)
            {
                return;
            }

            var currentFiles =
                manifest.files
                    .Where(entry => !string.IsNullOrEmpty(entry.guid))
                    .ToDictionary(
                        entry => entry.guid,
                        entry => entry.path,
                        StringComparer.Ordinal);

            var previousFiles =
                GetFiles(result.Accepted)
                    .Where(entry => !string.IsNullOrEmpty(entry.guid))
                    .GroupBy(
                        entry => entry.guid,
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().path,
                        StringComparer.Ordinal);

            var currentObjects =
                new HashSet<string>(
                    manifest.objects.Select(ManifestComparer.GetKey),
                    StringComparer.Ordinal);

            var assetsWithObjects =
                new HashSet<string>(
                    manifest.objects.Select(entry => entry.guid),
                    StringComparer.Ordinal);

            foreach (var reference in references)
            {
                if (!currentFiles.TryGetValue(
                        reference.Guid,
                        out var currentAsset))
                {
                    if (previousFiles.TryGetValue(
                            reference.Guid,
                            out var previousAsset))
                    {
                        AddIssue(
                            result,
                            ValidationSeverity.Error,
                            reference.Source,
                            LostLinkRule,
                            $"Ссылается на файл {previousAsset}, которого нет в версии {manifest.version}.");
                    }

                    continue;
                }

                if (reference.FileId == PrefabAssetFileId
                 || !assetsWithObjects.Contains(reference.Guid)
                 || currentObjects.Contains(ManifestComparer.GetKey(
                        reference.Guid,
                        reference.FileId)))
                {
                    continue;
                }

                AddIssue(
                    result,
                    ValidationSeverity.Error,
                    reference.Source,
                    LostLinkRule,
                    $"Правка указывает на {DescribeLostTarget(result, reference, currentAsset)}, которого нет в версии {manifest.version}. Нажмите «Убрать потерянные правки» в окне приёма: добавленное на этот объект перейдёт в «{LostOverrideRescue.HolderPrefix} …», остальные правки будут удалены, список сохранится в отчёт.");
            }
        }

        private static void DescribeChanges(AcceptanceResult result)
        {
            if (result.Accepted == null)
            {
                result.Changes.Add("Первый приём этой локации.");

                return;
            }

            result.Changes.AddRange(
                ManifestComparer.DescribeFileChanges(
                    result.Accepted,
                    result.Location.Manifest));

            if (result.Accepted.format == ReleaseManifest.CurrentFormat
             && result.Location.Manifest.format == ReleaseManifest.CurrentFormat)
            {
                result.Changes.AddRange(
                    ManifestComparer.DescribeObjectChanges(
                        result.Accepted,
                        result.Location.Manifest));
            }
        }

        private static IEnumerable<ReleaseManifest.FileEntry> GetFiles(ReleaseManifest manifest)
            => manifest?.files ?? Enumerable.Empty<ReleaseManifest.FileEntry>();

        private static string Describe(ValidationIssue issue)
            => $"{issue.AssetPath}: [{issue.Rule}] {issue.Message}";

        /// <summary>
        /// Проверяет все подключённые пакеты локаций и выводит проблемы в окно отчёта.
        /// </summary>
        /// <returns>Результаты по каждому пакету.</returns>
        public static List<AcceptanceResult> Run()
        {
            ValidationReport.RemoveRules(Rules);

            var locations = InstalledLocation.FindAll();

            var results =
                locations
                    .Select(location => new AcceptanceResult(
                        location,
                        AcceptedManifests.Load(location.Name)))
                    .ToList();

            // Удалённый файл ищется и по GUID из принятой версии: в новой его уже нет.
            var guids =
                new HashSet<string>(
                    results
                        .SelectMany(result => GetFiles(result.Location.Manifest).Concat(GetFiles(result.Accepted)))
                        .Select(entry => entry.guid)
                        .Where(guid => !string.IsNullOrEmpty(guid)),
                    StringComparer.Ordinal);

            var references =
                guids.Count == 0
                    ? new List<ReferenceScanner.Reference>()
                    : ReferenceScanner.FindAll(guids);

            var conflicts = GuidConflicts.Find();

            foreach (var result in results)
            {
                CheckGuidConflicts(
                    result,
                    conflicts);

                if (result.Location.Manifest == null)
                {
                    // Разработчик может подключить любой коммит локации, чтобы разобраться в проблеме вместе с художником.
                    // Такая версия не ошибка, но принять и закоммитить её нельзя.
                    AddIssue(
                        result,
                        ValidationSeverity.Warning,
                        result.Location.AssetFolder,
                        DeliveryRule,
                        $"Проверочная версия: в {result.Location.Version} нет манифеста выпуска, значит это не выпуск, а коммит локации. Смотреть можно, принять и закоммитить нельзя.");

                    continue;
                }

                CheckVersions(result);
                CheckDelivery(result);

                CheckLostLinks(
                    result,
                    references);

                DescribeChanges(result);
            }

            CheckHolders(results);

            return results;
        }

        /// <summary>
        /// Проверка для хука pre-commit: подключённые версии должны быть проверены и приняты.
        /// </summary>
        /// <returns>Ошибки, из-за которых коммит запрещён; пустой список, если коммитить можно.</returns>
        public static List<string> RunForGate()
        {
            var errors = new List<string>();

            foreach (var result in Run())
            {
                errors.AddRange(
                    result.Issues
                        .Where(issue => issue.Severity == ValidationSeverity.Error)
                        .Select(Describe));

                if (result.IsTrial)
                {
                    errors.Add($"{result.Location.Name} {result.Location.Version}: подключён коммит локации, а не выпуск. Перед коммитом верните выпущенную версию пакета.");
                }
                else if (!result.IsAccepted)
                {
                    errors.Add($"{result.Location.Name} {result.Location.Version}: версия не принята. Откройте «{MenuPath}», проверьте изменения и нажмите «Принять», затем добавьте {AcceptedManifests.Folder} в коммит.");
                }
            }

            return errors;
        }
    }
}
