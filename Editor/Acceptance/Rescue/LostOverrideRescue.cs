using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Находит в файле игры правки на удалённые объекты основы и переносит добавленное на них в «карман».
    /// </summary>
    /// <remarks>
    /// Добавленный компонент на удалённом объекте Unity теряет уже при загрузке файла, а очистка неиспользуемых правок
    /// удаляет его окончательно. Поэтому перенос делается в тексте файла до того, как его откроет Unity:
    /// для каждого удалённого объекта под корнем экземпляра создаётся объект «_Потерянное имя», и на него
    /// переносятся добавленные компоненты (с настроенными полями) и дочерние объекты.
    /// </remarks>
    internal static class LostOverrideRescue
    {
        /// <summary>
        /// Начало имени «кармана»: по нему проверка приёма напоминает разобрать перенесённое.
        /// </summary>
        public const string HolderPrefix = "_Потерянное";

        private const int PrefabInstanceClassId = 1001;

        private const int GameObjectClassId = 1;

        private const int TransformClassId = 4;

        private const int RectTransformClassId = 224;

        private const int ModificationIndent = 4;

        private const string ModificationsList = "m_Modifications";

        private const string RemovedComponentsList = "m_RemovedComponents";

        private const string RemovedGameObjectsList = "m_RemovedGameObjects";

        private const string AddedGameObjectsList = "m_AddedGameObjects";

        private const string AddedComponentsList = "m_AddedComponents";

        private const string MonoBehaviourType = "MonoBehaviour";

        private const char Quote = '"';

        // Не латинские буквы Unity записывает в кавычках через \uXXXX.
        private static readonly Regex NamePattern =
            new Regex(
                @"^  m_Name: (?<name>.*?)\r?$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static string ReadName(string value)
            => value.Length > 1
            && value[0] == Quote
            && value[value.Length - 1] == Quote
                ? Regex.Unescape(value.Substring(
                    1,
                    value.Length - 2))
                : value;

        /// <summary>
        /// Находит в тексте сцены или префаба «карманы», созданные при переносе.
        /// </summary>
        /// <param name="text">Содержимое файла.</param>
        /// <returns>Имена «карманов».</returns>
        public static List<string> FindHolders(string text)
            => NamePattern
                .Matches(text)
                .Cast<Match>()
                .Select(match => ReadName(match.Groups["name"].Value))
                .Where(name => name.StartsWith(
                    HolderPrefix,
                    StringComparison.Ordinal))
                .ToList();

        private static string FindField(
            IEnumerable<string> entry,
            string key)
        {
            var prefix = key + ":";

            var line =
                entry
                    .Select(text => text.TrimStart(
                        ' ',
                        '-'))
                    .FirstOrDefault(text => text.StartsWith(
                        prefix,
                        StringComparison.Ordinal));

            return line?.Substring(prefix.Length).Trim();
        }

        private static string GetComponentName(UnityYamlDocument component)
        {
            if (!string.Equals(
                    component.TypeName,
                    MonoBehaviourType,
                    StringComparison.Ordinal)
             || !YamlReference.TryFind(
                    component.GetValue("m_Script"),
                    out var script))
            {
                return component.TypeName;
            }

            var path = AssetDatabase.GUIDToAssetPath(script.Guid);

            return string.IsNullOrEmpty(path)
                ? MonoBehaviourType
                : Path.GetFileNameWithoutExtension(path);
        }

        private static string GetGameObjectName(
            UnityYamlFile file,
            UnityYamlDocument transform)
        {
            YamlReference.TryFind(
                transform.GetValue("m_GameObject"),
                out var gameObject);

            return file.Find(gameObject.FileId)?.GetValue("m_Name") ?? "?";
        }

        private static bool IsTransform(UnityYamlDocument document)
            => document.ClassId == TransformClassId
            || document.ClassId == RectTransformClassId;

        private static string DescribeProperty(
            LostItem item,
            string target)
        {
            var entry = item.Entry;

            var property =
                FindField(
                    entry,
                    "propertyPath");

            var value =
                FindField(
                    entry,
                    "value");

            var referenceText =
                FindField(
                    entry,
                    "objectReference");

            var reference =
                YamlReference.TryFind(
                    referenceText,
                    out var objectReference)
             && objectReference.FileId != 0
                    ? $", ссылка {referenceText}"
                    : string.Empty;

            // Многострочное значение (длинная строка) в отчёте сокращается до первой строки.
            var more =
                entry.Count > 4
                    ? "…"
                    : string.Empty;

            return $"{target}: свойство {property} = {value}{more}{reference}";
        }

        private static string DescribeKind(LostItem item)
        {
            switch (item.Kind)
            {
                case LostItemKind.Property:
                    var property =
                        FindField(
                            item.Entry,
                            "propertyPath");

                    return $"свойство {property}";

                case LostItemKind.RemovedComponent:
                case LostItemKind.RemovedGameObject:
                    return "удалён в игре";

                case LostItemKind.AddedComponent:
                    return item.Added == null
                        ? "добавлен компонент"
                        : $"добавлен компонент {GetComponentName(item.Added)}";

                default:
                    return "добавлен дочерний объект";
            }
        }

        private static void Describe(
            UnityYamlFile file,
            LostItem item,
            ILostTargets targets)
        {
            var target =
                targets.Describe(
                    item.Target.Guid,
                    item.Target.FileId);

            var destination =
                item.HolderName == null
                    ? null
                    : $" → будет перенесён в «{item.HolderName}»";

            // Объект переименован: любая правка переходит на новый объект, остальные варианты не нужны.
            if (item.RenamedPath != null)
            {
                item.Description = $"{target}: {DescribeKind(item)} → объект переименован, правка перейдёт на {item.RenamedPath}";

                return;
            }

            switch (item.Kind)
            {
                case LostItemKind.Property:
                    item.Description =
                        DescribeProperty(
                            item,
                            target);

                    break;

                case LostItemKind.RemovedComponent:
                case LostItemKind.RemovedGameObject:
                    item.Description = $"{target}: был удалён в игре, правка больше не нужна";

                    break;

                case LostItemKind.AddedComponent:
                    var component =
                        item.Added == null
                            ? "?"
                            : GetComponentName(item.Added);

                    item.Description = $"{target}: добавлен компонент {component}{destination ?? " → будет удалён: перенесите его вручную"}";

                    break;

                case LostItemKind.AddedGameObject:
                    var name =
                        item.Added == null
                            ? "?"
                            : GetGameObjectName(
                                file,
                                item.Added);

                    item.Description = $"{target}: добавлен дочерний объект {name}{destination ?? " → останется на корне экземпляра"}";

                    break;
            }
        }

        private static string GetHolderName(
            LostItem item,
            string sourceGuid,
            ILostTargets targets)
        {
            if (item.Added == null
             || !string.Equals(
                    item.Target.Guid,
                    sourceGuid,
                    StringComparison.Ordinal)
             || !targets.TryGetRootTransform(
                    sourceGuid,
                    out _))
            {
                return null;
            }

            if (item.Kind == LostItemKind.AddedGameObject
             && !IsTransform(item.Added))
            {
                return null;
            }

            var name =
                targets.GetName(
                    item.Target.Guid,
                    item.Target.FileId);

            return $"{HolderPrefix} {name}";
        }

        private static IEnumerable<LostItem> FindInList(
            UnityYamlFile file,
            UnityYamlDocument instance,
            string list,
            LostItemKind kind,
            ILostTargets targets)
        {
            YamlReference.TryFind(
                instance.GetValue("m_SourcePrefab"),
                out var source);

            var entries =
                instance.GetEntries(
                    list,
                    ModificationIndent);

            foreach (var entry in entries)
            {
                if (!YamlReference.TryFind(
                        entry[0],
                        out var target)
                 || !targets.IsLost(
                        target.Guid,
                        target.FileId))
                {
                    continue;
                }

                var item =
                    new LostItem(
                        kind,
                        instance,
                        entry,
                        target);

                if (kind == LostItemKind.AddedComponent
                 || kind == LostItemKind.AddedGameObject)
                {
                    YamlReference.TryFind(
                        FindField(
                            entry,
                            "addedObject"),
                        out var added);

                    item.Added = file.Find(added.FileId);
                }

                // Переименованный объект получает правки сам, «карман» ему не нужен.
                if (targets.TryGetRenamed(
                        target.Guid,
                        target.FileId,
                        out _,
                        out var renamedPath))
                {
                    item.RenamedPath = renamedPath;
                }
                else
                {
                    item.HolderName =
                        GetHolderName(
                            item,
                            source.Guid,
                            targets);
                }

                Describe(
                    file,
                    item,
                    targets);

                yield return item;
            }
        }

        private static List<string> CreateGameObjectLines(
            string name,
            IEnumerable<long> components)
        {
            var lines = new List<string>
            {
                "GameObject:",
                "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}",
                "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}",
                "  serializedVersion: 6",
                "  m_Component:",
            };

            lines.AddRange(components.Select(component => "  - component: " + YamlReference.Local(component)));

            lines.AddRange(
                new[]
                {
                    "  m_Layer: 0",
                    "  m_Name: " + name,
                    "  m_TagString: Untagged",
                    "  m_Icon: {fileID: 0}",
                    "  m_NavMeshLayer: 0",
                    "  m_StaticEditorFlags: 0",
                    "  m_IsActive: 1",
                });

            return lines;
        }

        private static List<string> CreateTransformLines(
            long gameObject,
            long father,
            IReadOnlyCollection<long> children)
        {
            var lines = new List<string>
            {
                "Transform:",
                "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}",
                "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}",
                "  m_GameObject: " + YamlReference.Local(gameObject),
                "  serializedVersion: 2",
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
                "  m_LocalPosition: {x: 0, y: 0, z: 0}",
                "  m_LocalScale: {x: 1, y: 1, z: 1}",
                "  m_ConstrainProportionsScale: 0",
                children.Count == 0
                    ? "  m_Children: []"
                    : "  m_Children:",
            };

            lines.AddRange(children.Select(child => "  - " + YamlReference.Local(child)));

            lines.AddRange(
                new[]
                {
                    "  m_Father: " + YamlReference.Local(father),
                    "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
                });

            return lines;
        }

        private static UnityYamlDocument CreateDocument(
            int classId,
            long fileId,
            bool isStripped,
            List<string> lines)
        {
            var stripped =
                isStripped
                    ? " stripped"
                    : string.Empty;

            return new UnityYamlDocument(
                classId,
                fileId,
                isStripped,
                $"--- !u!{classId} &{fileId}{stripped}",
                lines);
        }

        // Объекты экземпляра в файле получают идентификатор «экземпляр XOR объект основы», так их находит Unity.
        private static long GetStrippedRoot(
            UnityYamlFile file,
            UnityYamlDocument instance,
            string sourceGuid,
            long rootTransform)
        {
            var fileId =
                (instance.FileId
               ^ rootTransform)
              & long.MaxValue;

            if (file.Find(fileId) != null)
            {
                return fileId;
            }

            var source =
                YamlReference.External(
                    rootTransform,
                    sourceGuid);

            file.Documents.Add(
                CreateDocument(
                    TransformClassId,
                    fileId,
                    true,
                    new List<string>
                    {
                        "Transform:",
                        "  m_CorrespondingSourceObject: " + source,
                        "  m_PrefabInstance: " + YamlReference.Local(instance.FileId),
                        "  m_PrefabAsset: {fileID: 0}",
                    }));

            return fileId;
        }

        private static void RemoveEntries(
            UnityYamlDocument instance,
            string list,
            ICollection<List<string>> removed)
        {
            var kept =
                instance
                    .GetEntries(
                        list,
                        ModificationIndent)
                    .Where(entry => !removed.Any(item => item.SequenceEqual(entry)))
                    .Cast<IReadOnlyCollection<string>>()
                    .ToList();

            instance.SetEntries(
                list,
                ModificationIndent,
                kept);
        }

        private static void AddHolderEntry(
            UnityYamlDocument instance,
            string sourceGuid,
            long rootTransform,
            long holderTransform)
        {
            var entries =
                instance
                    .GetEntries(
                        AddedGameObjectsList,
                        ModificationIndent)
                    .Cast<IReadOnlyCollection<string>>()
                    .ToList();

            var root =
                YamlReference.External(
                    rootTransform,
                    sourceGuid);

            entries.Add(
                new[]
                {
                    "    - targetCorrespondingSourceObject: " + root,
                    "      insertIndex: -1",
                    "      addedObject: " + YamlReference.Local(holderTransform),
                });

            instance.SetEntries(
                AddedGameObjectsList,
                ModificationIndent,
                entries);
        }

        private static void RescueGroup(
            UnityYamlFile file,
            UnityYamlDocument instance,
            string sourceGuid,
            long rootTransform,
            IGrouping<string, LostItem> group)
        {
            var holder = file.CreateFileId();
            var holderTransform = file.CreateFileId();

            var components =
                group
                    .Where(item => item.Kind == LostItemKind.AddedComponent)
                    .Select(item => item.Added)
                    .ToList();

            var children =
                group
                    .Where(item => item.Kind == LostItemKind.AddedGameObject)
                    .Select(item => item.Added)
                    .ToList();

            foreach (var component in components)
            {
                component.SetValue(
                    "m_GameObject",
                    YamlReference.Local(holder));
            }

            foreach (var child in children)
            {
                child.SetValue(
                    "m_Father",
                    YamlReference.Local(holderTransform));
            }

            var strippedRoot =
                GetStrippedRoot(
                    file,
                    instance,
                    sourceGuid,
                    rootTransform);

            file.Documents.Add(
                CreateDocument(
                    GameObjectClassId,
                    holder,
                    false,
                    CreateGameObjectLines(
                        group.Key,
                        new[] { holderTransform }.Concat(components.Select(component => component.FileId)))));

            file.Documents.Add(
                CreateDocument(
                    TransformClassId,
                    holderTransform,
                    false,
                    CreateTransformLines(
                        holder,
                        strippedRoot,
                        children
                            .Select(child => child.FileId)
                            .ToList())));

            AddHolderEntry(
                instance,
                sourceGuid,
                rootTransform,
                holderTransform);
        }

        private static void RescueInstance(
            UnityYamlFile file,
            UnityYamlDocument instance,
            List<LostItem> items,
            ILostTargets targets)
        {
            YamlReference.TryFind(
                instance.GetValue("m_SourcePrefab"),
                out var source);

            if (!targets.TryGetRootTransform(
                    source.Guid,
                    out var rootTransform))
            {
                return;
            }

            var groups =
                items
                    .Where(item => item.HolderName != null)
                    .GroupBy(
                        item => item.HolderName,
                        StringComparer.Ordinal)
                    .ToList();

            foreach (var group in groups)
            {
                RescueGroup(
                    file,
                    instance,
                    source.Guid,
                    rootTransform,
                    group);
            }

            var moved =
                groups
                    .SelectMany(group => group)
                    .ToList();

            RemoveEntries(
                instance,
                AddedComponentsList,
                moved
                    .Where(item => item.Kind == LostItemKind.AddedComponent)
                    .Select(item => item.Entry)
                    .ToList());

            RemoveEntries(
                instance,
                AddedGameObjectsList,
                moved
                    .Where(item => item.Kind == LostItemKind.AddedGameObject)
                    .Select(item => item.Entry)
                    .ToList());
        }

        /// <summary>
        /// Находит потерянные правки во всех экземплярах префабов файла.
        /// </summary>
        /// <param name="file">Разобранный файл сцены или префаба.</param>
        /// <param name="targets">Сведения об объектах основы.</param>
        /// <returns>Потерянные правки с описаниями для отчёта.</returns>
        public static List<LostItem> Find(
            UnityYamlFile file,
            ILostTargets targets)
        {
            var lists = new[]
            {
                (ModificationsList, LostItemKind.Property),
                (RemovedComponentsList, LostItemKind.RemovedComponent),
                (RemovedGameObjectsList, LostItemKind.RemovedGameObject),
                (AddedComponentsList, LostItemKind.AddedComponent),
                (AddedGameObjectsList, LostItemKind.AddedGameObject),
            };

            return file.Documents
                .Where(document => document.ClassId == PrefabInstanceClassId)
                .SelectMany(instance => lists.SelectMany(list => FindInList(
                    file,
                    instance,
                    list.Item1,
                    list.Item2,
                    targets)))
                .ToList();
        }

        /// <summary>
        /// Переносит добавленное на удалённые объекты в «карманы» под корнями экземпляров,
        /// а все правки переименованных объектов — на их новые объекты.
        /// </summary>
        /// <param name="file">Разобранный файл; меняется на месте.</param>
        /// <param name="items">Потерянные правки этого файла из <see cref="Find"/>.</param>
        /// <param name="targets">Сведения об объектах основы.</param>
        /// <returns><c>true</c>, если что-то перенесено и файл нужно записать.</returns>
        public static bool Rescue(
            UnityYamlFile file,
            IReadOnlyCollection<LostItem> items,
            ILostTargets targets)
        {
            var instances =
                items
                    .Where(item => item.HolderName != null)
                    .GroupBy(item => item.Instance)
                    .ToList();

            foreach (var instance in instances)
            {
                RescueInstance(
                    file,
                    instance.Key,
                    instance.ToList(),
                    targets);
            }

            // После «карманов»: перенос в них сверяет элементы списков правок с исходными строками.
            var retargeted =
                RenameRetargeter.Apply(
                    file,
                    targets);

            return instances.Count > 0
                || retargeted;
        }
    }
}
