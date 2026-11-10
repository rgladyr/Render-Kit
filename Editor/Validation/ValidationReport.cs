using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Хранит найденные проблемы по файлам до перезагрузки скриптов.
    /// </summary>
    internal static class ValidationReport
    {
        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<string, List<ValidationIssue>> IssuesByAsset =
            new Dictionary<string, List<ValidationIssue>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Вызывается после любого изменения отчёта.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// Заменяет проблемы файла новыми.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        /// <param name="issues">Найденные проблемы.</param>
        public static void Set(
            string assetPath,
            IEnumerable<ValidationIssue> issues)
        {
            lock (SyncRoot)
            {
                IssuesByAsset[assetPath] = issues.ToList();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Добавляет проблему к уже найденным проблемам файла.
        /// </summary>
        /// <param name="issue">Найденная проблема.</param>
        public static void Add(ValidationIssue issue)
        {
            lock (SyncRoot)
            {
                if (!IssuesByAsset.TryGetValue(
                        issue.AssetPath,
                        out var issues))
                {
                    issues = new List<ValidationIssue>();
                    IssuesByAsset[issue.AssetPath] = issues;
                }

                issues.Add(issue);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Удаляет проблемы, найденные указанными правилами, во всех файлах.
        /// </summary>
        /// <param name="rules">Названия правил.</param>
        public static void RemoveRules(ICollection<string> rules)
        {
            lock (SyncRoot)
            {
                foreach (var issues in IssuesByAsset.Values)
                {
                    issues.RemoveAll(issue => rules.Contains(issue.Rule));
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Удаляет проблемы файла.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        public static void Remove(string assetPath)
        {
            lock (SyncRoot)
            {
                IssuesByAsset.Remove(assetPath);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Удаляет все проблемы.
        /// </summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                IssuesByAsset.Clear();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Проверяет, есть ли проблемы у файла.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        /// <returns><c>true</c>, если у файла есть хотя бы одна проблема.</returns>
        public static bool HasIssues(string assetPath)
        {
            lock (SyncRoot)
            {
                return IssuesByAsset.TryGetValue(
                        assetPath,
                        out var issues)
                    && issues.Count > 0;
            }
        }

        /// <summary>
        /// Возвращает все проблемы, отсортированные по уровню и файлу.
        /// </summary>
        /// <returns>Копия списка проблем.</returns>
        public static List<ValidationIssue> GetAll()
        {
            lock (SyncRoot)
            {
                return IssuesByAsset.Values
                    .SelectMany(issues => issues)
                    .OrderByDescending(issue => issue.Severity)
                    .ThenBy(
                        issue => issue.AssetPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(
                        issue => issue.ObjectPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }
}
