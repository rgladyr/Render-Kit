using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Находит в файле игры ссылки на объекты основы, которых больше нет: поля компонентов и правки свойств.
    /// </summary>
    /// <remarks>
    /// Ссылка на компонент художника внутри экземпляра хранится как ссылка на «заглушку» (stripped-документ),
    /// а заглушка указывает на объект основы. Когда объект основы удалён, Unity при сохранении молча
    /// записывает в такое поле пустую ссылку, поэтому найти его можно только до сохранения.
    /// </remarks>
    internal static class LostReferenceFinder
    {
        private const int PrefabInstanceClassId = 1001;

        private const int ModificationIndent = 4;

        private const string ModificationsList = "m_Modifications";

        private const string ListElement = "элемент списка";

        private const string EmptyReference = "{fileID: 0}";

        // Служебные поля описывают устройство экземпляра, а не ссылки логики. Компоненты и дочерние объекты
        // на удалённом объекте обрабатываются как потерянные правки.
        private static readonly string[] ServiceKeys =
        {
            "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Father", "component",
        };

        private static readonly Regex KeyPattern =
            new Regex(
                @"^\s*(?:- )?(?<key>[A-Za-z_][A-Za-z0-9_.\[\]]*):",
                RegexOptions.Compiled);

        private static readonly Regex ReferencePattern =
            new Regex(
                @"\{fileID: [^}]*\}",
                RegexOptions.Compiled);

        private static string GetKey(string line)
        {
            var match = KeyPattern.Match(line);

            return match.Success
                ? match.Groups["key"].Value
                : null;
        }

        private static bool IsElement(string line)
            => line
                .TrimStart()
                .StartsWith(
                    "- ",
                    StringComparison.Ordinal);

        private static bool TryGetLost(
            string line,
            IReadOnlyDictionary<long, YamlReference> stubs,
            ILostTargets targets,
            out YamlReference lost)
        {
            lost = default;

            if (!YamlReference.TryFind(
                    line,
                    out var reference))
            {
                return false;
            }

            if (string.IsNullOrEmpty(reference.Guid))
            {
                return stubs.TryGetValue(
                    reference.FileId,
                    out lost);
            }

            lost = reference;

            return targets.IsLost(
                reference.Guid,
                reference.FileId);
        }

        private static Dictionary<long, YamlReference> FindLostStubs(
            UnityYamlFile file,
            ILostTargets targets)
        {
            var stubs = new Dictionary<long, YamlReference>();

            foreach (var document in file.Documents.Where(document => document.IsStripped))
            {
                if (YamlReference.TryFind(
                        document.GetValue("m_CorrespondingSourceObject"),
                        out var source)
                 && targets.IsLost(
                        source.Guid,
                        source.FileId))
                {
                    stubs[document.FileId] = source;
                }
            }

            return stubs;
        }

        private static string DescribeOwner(
            UnityYamlFile file,
            UnityYamlDocument component,
            ILostTargets targets)
        {
            var name = LostOverrideRescue.GetComponentName(component);

            if (!YamlReference.TryFind(
                    component.GetValue("m_GameObject"),
                    out var owner))
            {
                return name;
            }

            var gameObject = file.Find(owner.FileId);

            if (gameObject == null)
            {
                return name;
            }

            if (!gameObject.IsStripped)
            {
                var ownerName = LostOverrideRescue.ReadName(gameObject.GetValue("m_Name") ?? "?");

                return $"{ownerName} ({name})";
            }

            YamlReference.TryFind(
                gameObject.GetValue("m_CorrespondingSourceObject"),
                out var source);

            var ownerDescription =
                targets.Describe(
                    source.Guid,
                    source.FileId);

            return $"{ownerDescription} ({name})";
        }

        private static IEnumerable<LostReference> FindInComponent(
            UnityYamlFile file,
            UnityYamlDocument component,
            IReadOnlyDictionary<long, YamlReference> stubs,
            ILostTargets targets)
        {
            string lastKey = null;

            for (var index = 0; index < component.Lines.Count; index++)
            {
                var line = component.Lines[index];
                var key = GetKey(line);
                var isElement = IsElement(line);

                if (key != null
                 && !isElement)
                {
                    lastKey = key;
                }

                var field =
                    isElement
                        ? key ?? lastKey ?? ListElement
                        : key;

                if (field == null
                 || ServiceKeys.Contains(field)
                 || !TryGetLost(
                        line,
                        stubs,
                        targets,
                        out var lost))
                {
                    continue;
                }

                var owner =
                    DescribeOwner(
                        file,
                        component,
                        targets);

                var target =
                    targets.Describe(
                        lost.Guid,
                        lost.FileId);

                yield return new LostReference(
                    component,
                    index,
                    $"{owner}, поле {field} → {target}");
            }
        }

        private static IEnumerable<LostReference> FindInModifications(
            UnityYamlDocument instance,
            IReadOnlyDictionary<long, YamlReference> stubs,
            ILostTargets targets)
        {
            var entries =
                instance.GetEntries(
                    ModificationsList,
                    ModificationIndent);

            foreach (var entry in entries)
            {
                // Правка на удалённый объект сама потеряна: о ней сообщает проверка потерянных правок.
                if (!YamlReference.TryFind(
                        entry[0],
                        out var target)
                 || targets.IsLost(
                        target.Guid,
                        target.FileId))
                {
                    continue;
                }

                var referenceLine =
                    entry.FirstOrDefault(line => string.Equals(
                        GetKey(line),
                        "objectReference",
                        StringComparison.Ordinal));

                if (referenceLine == null
                 || !TryGetLost(
                        referenceLine,
                        stubs,
                        targets,
                        out var lost))
                {
                    continue;
                }

                var property =
                    entry
                        .Select(line => line.Trim())
                        .FirstOrDefault(line => line.StartsWith(
                            "propertyPath:",
                            StringComparison.Ordinal))
                        ?.Substring("propertyPath:".Length)
                        .Trim();

                var owner =
                    targets.Describe(
                        target.Guid,
                        target.FileId);

                var lostTarget =
                    targets.Describe(
                        lost.Guid,
                        lost.FileId);

                // Строки элемента — копия: номер строки ищется в самом документе по тексту.
                var index =
                    instance.Lines.FindIndex(line => ReferenceEquals(
                        line,
                        referenceLine));

                yield return new LostReference(
                    instance,
                    index,
                    $"{owner}, свойство {property} → {lostTarget}");
            }
        }

        /// <summary>
        /// Находит ссылки на удалённые объекты основы.
        /// </summary>
        /// <param name="file">Разобранный файл сцены или префаба.</param>
        /// <param name="targets">Сведения об объектах основы.</param>
        /// <returns>Найденные ссылки с описаниями вида «объект (компонент), поле → удалённый объект».</returns>
        public static List<LostReference> Find(
            UnityYamlFile file,
            ILostTargets targets)
        {
            var stubs =
                FindLostStubs(
                    file,
                    targets);

            var components =
                file.Documents
                    .Where(document => !document.IsStripped)
                    .Where(document => document.ClassId != PrefabInstanceClassId)
                    .SelectMany(document => FindInComponent(
                        file,
                        document,
                        stubs,
                        targets));

            var modifications =
                file.Documents
                    .Where(document => document.ClassId == PrefabInstanceClassId)
                    .SelectMany(instance => FindInModifications(
                        instance,
                        stubs,
                        targets));

            return components
                .Concat(modifications)
                .ToList();
        }

        /// <summary>
        /// Записывает пустую ссылку вместо ссылок на удалённые объекты: то же сделала бы Unity при сохранении, но молча.
        /// </summary>
        /// <param name="references">Ссылки из <see cref="Find"/>.</param>
        /// <returns><c>true</c>, если файл изменён.</returns>
        public static bool Clear(IEnumerable<LostReference> references)
        {
            var changed = false;

            foreach (var reference in references.Where(reference => reference.LineIndex >= 0))
            {
                var lines = reference.Document.Lines;

                lines[reference.LineIndex] =
                    ReferencePattern.Replace(
                        lines[reference.LineIndex],
                        EmptyReference,
                        1);

                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Ссылка на удалённый объект основы.
        /// </summary>
        internal sealed class LostReference
        {
            /// <summary>
            /// Создаёт описание ссылки.
            /// </summary>
            /// <param name="document">Документ, в котором записана ссылка.</param>
            /// <param name="lineIndex">Номер строки в документе; -1, если строка не найдена.</param>
            /// <param name="description">Описание для отчёта.</param>
            public LostReference(
                UnityYamlDocument document,
                int lineIndex,
                string description)
            {
                this.Document = document;
                this.LineIndex = lineIndex;
                this.Description = description;
            }

            /// <summary>
            /// Документ, в котором записана ссылка.
            /// </summary>
            public UnityYamlDocument Document { get; }

            /// <summary>
            /// Номер строки в документе.
            /// </summary>
            public int LineIndex { get; }

            /// <summary>
            /// Описание для отчёта.
            /// </summary>
            public string Description { get; }
        }
    }
}
