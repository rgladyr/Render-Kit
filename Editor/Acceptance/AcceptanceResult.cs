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
        /// Версию можно принять: ошибок нет.
        /// </summary>
        public bool CanAccept => this.ErrorCount == 0;
    }
}
