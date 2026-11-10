using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Сравнивает манифесты выпуска: что изменилось в пакете и совпадает ли записанный манифест с файлами.
    /// </summary>
    internal static class ManifestComparer
    {
        private static string GetParentPath(ReleaseManifest.ObjectEntry entry)
        {
            var separator = entry.path.LastIndexOf('/');

            return separator < 0
                ? string.Empty
                : entry.path.Substring(
                    0,
                    separator);
        }

        private static string GetGroupKey(ReleaseManifest.ObjectEntry entry)
            => $"{entry.guid}|{GetParentPath(entry)}";

        private static List<ReleaseManifest.ObjectEntry> GetGameObjects(ReleaseManifest manifest)
            => manifest.objects
                .Where(entry => string.Equals(
                    entry.type,
                    ReleaseManifest.GameObjectType,
                    StringComparison.Ordinal))
                .ToList();

        private static List<ReleaseManifest.ObjectEntry> Except(
            IEnumerable<ReleaseManifest.ObjectEntry> source,
            IEnumerable<ReleaseManifest.ObjectEntry> other)
        {
            var otherKeys =
                new HashSet<string>(
                    other.Select(GetKey),
                    StringComparer.Ordinal);

            return source
                .Where(entry => !otherKeys.Contains(GetKey(entry)))
                .ToList();
        }

        /// <summary>
        /// Ключ объекта: GUID файла и идентификатор объекта внутри него.
        /// </summary>
        /// <param name="entry">Объект манифеста.</param>
        /// <returns>Ключ, одинаковый у художника и в игре.</returns>
        public static string GetKey(ReleaseManifest.ObjectEntry entry)
            => GetKey(
                entry.guid,
                entry.fileId);

        /// <summary>
        /// Ключ объекта по GUID файла и идентификатору объекта внутри него.
        /// </summary>
        /// <param name="guid">GUID файла.</param>
        /// <param name="fileId">Идентификатор объекта внутри файла.</param>
        /// <returns>Ключ объекта.</returns>
        public static string GetKey(
            string guid,
            long fileId)
            => $"{guid}|{fileId}";

        /// <summary>
        /// Описывает объект для сообщения: файл, путь в иерархии и тип, если это не GameObject.
        /// </summary>
        /// <param name="entry">Объект манифеста.</param>
        /// <returns>Например «Location-Art.prefab → Root/Tree (MeshRenderer)».</returns>
        public static string Describe(ReleaseManifest.ObjectEntry entry)
            => string.Equals(
                    entry.type,
                    ReleaseManifest.GameObjectType,
                    StringComparison.Ordinal)
                ? $"{entry.asset} → {entry.path}"
                : $"{entry.asset} → {entry.path} ({entry.type})";

        /// <summary>
        /// Находит объекты, которые удалены или, вероятно, переименованы с прошлого выпуска.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Описания изменений; пустой список, если связи с правками в игре не пострадают.</returns>
        /// <remarks>
        /// Для Unity переименование в Blender — это удаление и создание объекта с новым идентификатором.
        /// Если под одним родителем пропал ровно один объект и появился ровно один, это считается переименованием.
        /// Компоненты пропадают вместе со своим объектом, поэтому сравниваются только объекты.
        /// </remarks>
        public static List<string> DescribeObjectChanges(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var previousObjects = GetGameObjects(previous);
            var currentObjects = GetGameObjects(current);

            var removed =
                Except(
                    previousObjects,
                    currentObjects);

            var added =
                Except(
                    currentObjects,
                    previousObjects);

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
                    changes.Add($"Переименован: {Describe(group.First())} → {addedInGroup[0].path}");

                    continue;
                }

                changes.AddRange(group.Select(entry => $"Удалён: {Describe(entry)}"));
            }

            return changes;
        }

        /// <summary>
        /// Описывает, какие файлы добавлены, удалены и изменены между версиями.
        /// </summary>
        /// <param name="previous">Манифест прошлой версии.</param>
        /// <param name="current">Манифест новой версии.</param>
        /// <returns>Описания изменений файлов.</returns>
        public static List<string> DescribeFileChanges(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var previousHashes =
                previous.files.ToDictionary(
                    entry => entry.path,
                    entry => entry.hash,
                    StringComparer.Ordinal);

            var changes = new List<string>();

            foreach (var entry in current.files)
            {
                if (!previousHashes.TryGetValue(
                        entry.path,
                        out var hash))
                {
                    changes.Add($"Добавлен файл: {entry.path}");
                }
                else if (!string.Equals(
                             hash,
                             entry.hash,
                             StringComparison.Ordinal))
                {
                    changes.Add($"Изменён файл: {entry.path}");
                }

                previousHashes.Remove(entry.path);
            }

            changes.AddRange(previousHashes.Keys.Select(path => $"Удалён файл: {path}"));

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
            var mismatches = new List<string>();

            if (saved.format != current.format)
            {
                mismatches.Add($"Манифест старого формата ({saved.format}, нужен {current.format}).");
            }

            var savedHashes =
                saved.files.ToDictionary(
                    entry => entry.path,
                    entry => entry.hash,
                    StringComparer.Ordinal);

            foreach (var entry in current.files)
            {
                if (!savedHashes.TryGetValue(
                        entry.path,
                        out var hash))
                {
                    mismatches.Add($"Файл не описан в манифесте: {entry.path}");
                }
                else if (!string.Equals(
                             hash,
                             entry.hash,
                             StringComparison.Ordinal))
                {
                    mismatches.Add($"Файл изменён после подготовки выпуска: {entry.path}");
                }

                savedHashes.Remove(entry.path);
            }

            mismatches.AddRange(savedHashes.Keys.Select(path => $"Файл удалён после подготовки выпуска: {path}"));

            if (!string.Equals(
                    saved.version,
                    current.version,
                    StringComparison.Ordinal))
            {
                mismatches.Add($"В манифесте версия {saved.version}, в package.json {current.version}.");
            }

            return mismatches;
        }
    }
}
