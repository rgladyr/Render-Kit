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
        // Файл, путь и тип: у пересозданного объекта они те же, меняется только идентификатор.
        private static string GetIdentityKey(ReleaseManifest.ObjectEntry entry)
            => $"{entry.guid}|{entry.path}|{entry.type}";

        // Вложенные в удалённый объект удалены вместе с ним, поэтому перечисляются числом.
        private static List<string> DescribeTopmost(
            IReadOnlyCollection<ReleaseManifest.ObjectEntry> entries,
            string verb)
        {
            var changes = new List<string>();

            foreach (var entry in entries)
            {
                var hasParentInList =
                    entries.Any(other => !ReferenceEquals(
                                             other,
                                             entry)
                                      && string.Equals(
                                             other.guid,
                                             entry.guid,
                                             StringComparison.Ordinal)
                                      && entry.path.StartsWith(
                                             other.path + "/",
                                             StringComparison.Ordinal));

                if (hasParentInList)
                {
                    continue;
                }

                var nested =
                    entries.Count(other => !ReferenceEquals(
                                               other,
                                               entry)
                                        && string.Equals(
                                               other.guid,
                                               entry.guid,
                                               StringComparison.Ordinal)
                                        && IsInSubtree(
                                               other.path,
                                               entry.path));

                changes.Add(
                    nested == 0
                        ? $"{verb}: {Describe(entry)}"
                        : $"{verb}: {Describe(entry)} и {nested} вложенных");
            }

            return changes;
        }

        private static bool IsInSubtree(
            string path,
            string root)
            => string.Equals(
                   path,
                   root,
                   StringComparison.Ordinal)
            || path.StartsWith(
                   root + "/",
                   StringComparison.Ordinal);

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
        /// Описывает удалённые объекты основы и моделей: только верхние, вложенные сворачиваются в число.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Описания удалений; пустой список, если связи с правками в игре не пострадают.</returns>
        /// <remarks>
        /// Переименование в Blender для Unity — удаление и создание объекта, поэтому оно тоже попадает сюда.
        /// Угадывать новое имя не нужно: при нескольких удалениях догадка может ошибиться.
        /// Пересозданные объекты (тот же путь и тип) сюда не попадают: правки игры переносятся на них сами.
        /// </remarks>
        public static List<string> DescribeObjectChanges(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var recreated =
                FindRecreatedObjects(
                    previous,
                    current);

            var removed =
                Except(
                        GetGameObjects(previous),
                        GetGameObjects(current))
                    .Where(entry => !recreated.ContainsKey(GetKey(entry)))
                    .ToList();

            return DescribeTopmost(
                removed,
                "Удалён");
        }

        /// <summary>
        /// Описывает пересозданные объекты: у них новый идентификатор, но тот же путь и тип.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Описания; вложенные объекты сворачиваются в число.</returns>
        public static List<string> DescribeRecreated(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var recreatedKeys =
                new HashSet<string>(
                    FindRecreatedObjects(
                            previous,
                            current)
                        .Keys,
                    StringComparer.Ordinal);

            var recreated =
                GetGameObjects(previous)
                    .Where(entry => recreatedKeys.Contains(GetKey(entry)))
                    .ToList();

            return DescribeTopmost(
                    recreated,
                    "Пересоздан")
                .Select(change => change + " — правки игры перенесутся сами")
                .ToList();
        }

        /// <summary>
        /// Сопоставляет пропавшие объекты с новыми, у которых тот же файл, путь в иерархии и тип.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns>Ключ старого объекта → новый объект.</returns>
        /// <remarks>
        /// Так бывает, когда префаб основы или объект удалили и создали заново с теми же именами: это тот же объект.
        /// Если одинаковых путей несколько (соседи с одним именем), они сопоставляются только при равном числе, по порядку.
        /// </remarks>
        public static Dictionary<string, ReleaseManifest.ObjectEntry> FindRecreatedObjects(
            ReleaseManifest previous,
            ReleaseManifest current)
        {
            var map = new Dictionary<string, ReleaseManifest.ObjectEntry>(StringComparer.Ordinal);

            var added =
                Except(
                        current.objects,
                        previous.objects)
                    .ToLookup(
                        GetIdentityKey,
                        StringComparer.Ordinal);

            var removed =
                Except(
                        previous.objects,
                        current.objects)
                    .GroupBy(
                        GetIdentityKey,
                        StringComparer.Ordinal);

            foreach (var group in removed)
            {
                var candidates = added[group.Key].ToList();
                var lost = group.ToList();

                if (candidates.Count != lost.Count)
                {
                    continue;
                }

                for (var index = 0; index < lost.Count; index++)
                {
                    map[GetKey(lost[index])] = candidates[index];
                }
            }

            return map;
        }

        /// <summary>
        /// Проверяет, появились ли новые объекты с прошлого выпуска.
        /// </summary>
        /// <param name="previous">Манифест прошлого выпуска.</param>
        /// <param name="current">Манифест текущего состояния.</param>
        /// <returns><c>true</c>, если в текущем манифесте есть объекты, которых не было.</returns>
        public static bool HasAddedObjects(
            ReleaseManifest previous,
            ReleaseManifest current)
            => Except(
                    GetGameObjects(current),
                    GetGameObjects(previous))
                .Count > 0;

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
