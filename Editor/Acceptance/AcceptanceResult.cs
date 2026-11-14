using System;
using System.Collections.Generic;
using System.Linq;

using PaperFly.RenderKit.Release;
using PaperFly.RenderKit.Validation;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Результат проверки подключённой версии локации.
    /// </summary>
    internal sealed class AcceptanceResult
    {
        /// <summary>
        /// Создаёт пустой результат проверки пакета.
        /// </summary>
        /// <param name="location">Подключённый пакет.</param>
        /// <param name="accepted">Принятый ранее манифест; <c>null</c>, если пакет ещё не принимали.</param>
        public AcceptanceResult(
            InstalledLocation location,
            ReleaseManifest accepted)
        {
            this.Location = location;
            this.Accepted = accepted;
            this.IsAccepted = AcceptedManifests.IsAccepted(location);
        }

        /// <summary>
        /// Подключённый пакет.
        /// </summary>
        public InstalledLocation Location { get; }

        /// <summary>
        /// Принятый ранее манифест; <c>null</c>, если пакет ещё не принимали.
        /// </summary>
        public ReleaseManifest Accepted { get; }

        /// <summary>
        /// Подключённая версия уже принята.
        /// </summary>
        public bool IsAccepted { get; }

        /// <summary>
        /// Найденные проблемы.
        /// </summary>
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        /// <summary>
        /// Изменения относительно принятой версии.
        /// </summary>
        public List<string> Changes { get; } = new List<string>();

        /// <summary>
        /// Число ошибок.
        /// </summary>
        public int ErrorCount => this.Issues.Count(issue => issue.Severity == ValidationSeverity.Error);

        /// <summary>
        /// Число предупреждений.
        /// </summary>
        public int WarningCount => this.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);

        /// <summary>
        /// Файлы игры, в которых остались правки и ссылки на удалённые объекты локации.
        /// </summary>
        public List<string> LostLinkFiles
            => this.GetFiles(
                AcceptanceChecks.LostLinkRule,
                AcceptanceChecks.LostReferenceRule);

        /// <summary>
        /// Файлы игры, в которых остались объекты «_Потерянное …».
        /// </summary>
        public List<string> HolderFiles => this.GetFiles(AcceptanceChecks.HolderRule);

        /// <summary>
        /// Подключён коммит локации, а не выпуск: манифеста нет или он старого формата.
        /// </summary>
        public bool IsTrial
            => this.Location.Manifest == null
            || this.Location.Manifest.format != ReleaseManifest.CurrentFormat;

        /// <summary>
        /// Версию можно принять: это выпуск и ошибок нет.
        /// </summary>
        public bool CanAccept
            => !this.IsTrial
            && this.ErrorCount == 0;

        private List<string> GetFiles(params string[] rules)
            => this.Issues
                .Where(issue => rules.Contains(
                    issue.Rule,
                    StringComparer.Ordinal))
                .Select(issue => issue.AssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
