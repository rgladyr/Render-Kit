using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Сравнивает манифесты выпуска: что изменилось в основе и совпадает ли записанный манифест с файлами.
    /// </summary>
    internal static class ManifestComparer
    {
        private static string GetParentPath(ReleaseManifest.ObjectEntry entry)
        {
            var separator = entry.path.LastIndexOf('/');

            return separator < 0
                ? string.Empty
                : entry.path.Substring(0, separator);
        }

        private static string GetGroupKey(ReleaseManifest.ObjectEntry entry)
            => $"{entry.prefab}|{GetParentPath(entry)}";

        private static HashSet<string> GetKeys(ReleaseManifest manifest)
            => new HashSet<string>(
                manifest.objects.Select(entry => $"{entry.prefab}|{entry.fileId}"),
                StringComparer.Ordinal);

        private static List<ReleaseManifest.ObjectEntry> Except(
            ReleaseManifest source,
            ReleaseManifest other)
        {
            var otherKeys = GetKeys(other);

            return source.objects
                .Where(entry => !otherKeys.Contains($"{entry.prefab}|{entry.fileId}"))
                .ToList();
        }

        /// <summary>
        /// Находит объекты основы, которые удалены, добавлены или, вероятно, переименованы с прошлого выпуска.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Описания изменений для художника; пустой список, если связи с вариантом не пострадают.</returns>
        /// <remarks>
        /// Для Unity переименование — это удаление и создание объекта с новым идентификатором.
        /// Если под одним родителем пропал ровно один объект и появился ровно один, это считается переименованием.
        /// </remarks>
        public static List<string> DescribeObjectChanges(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var removed =
                Except(
                    previous,
                    current);

            var added =
                Except(
                    current,
                    previous);

            var changes = new List<string>();

            var addedByGroup =
                added.ToLookup(
                    GetGroupKey,
                    StringComparer.Ordinal);

            foreach (var group in removed.GroupBy(GetGroupKey))
            {
                var addedInGroup = addedByGroup[group.Key].ToList();

                if (group.Count() == 1
                 && addedInGroup.Count == 1)
                {
                    changes.Add($"Переименован: {group.First().prefab} → {group.First().path} → {addedInGroup[0].path}");

                    continue;
                }

                changes.AddRange(group.Select(entry => $"Удалён: {entry.prefab} → {entry.path}"));
            }

            return changes;
        }

        /// <summary>
        /// Проверяет, что записанный манифест описывает текущие файлы и объекты пакета.
        /// </summary>
        /// <param name="saved">Манифест, записанный при подготовке выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Расхождения; пустой список, если манифест актуален.</returns>
        public static List<string> FindMismatches(
            ReleaseManifest saved,
            ReleaseManifest current)
        {
            var savedHashes =
                saved.files.ToDictionary(
                    entry => entry.path,
                    entry => entry.hash,
                    StringComparer.Ordinal);

            var mismatches = new List<string>();

            foreach (var entry in current.files)
            {
                if (!savedHashes.TryGetValue(entry.path, out var hash))
                {
                    mismatches.Add($"Файл не описан в манифесте: {entry.path}");
                }
                else if (!string.Equals(hash, entry.hash, StringComparison.Ordinal))
                {
                    mismatches.Add($"Файл изменён после подготовки выпуска: {entry.path}");
                }

                savedHashes.Remove(entry.path);
            }

            mismatches.AddRange(savedHashes.Keys.Select(path => $"Файл удалён после подготовки выпуска: {path}"));

            if (!string.Equals(saved.version, current.version, StringComparison.Ordinal))
            {
                mismatches.Add($"В манифесте версия {saved.version}, в package.json {current.version}.");
            }

            return mismatches;
        }
    }
}
