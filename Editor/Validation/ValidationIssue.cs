using System;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проблема, найденная проверкой.
    /// </summary>
    internal sealed class ValidationIssue
    {
        /// <summary>
        /// Создаёт проблему с текущим временем и без названия проверки.
        /// </summary>
        /// <param name="severity">Уровень проблемы.</param>
        /// <param name="assetPath">Путь к файлу, в котором найдена проблема.</param>
        /// <param name="objectPath">Путь к объекту внутри файла или пустая строка.</param>
        /// <param name="message">Описание проблемы для художника.</param>
        public ValidationIssue(
            ValidationSeverity severity,
            string assetPath,
            string objectPath,
            string message)
            : this(
                severity,
                assetPath,
                objectPath,
                message,
                string.Empty,
                DateTime.Now)
        {
        }

        private ValidationIssue(
            ValidationSeverity severity,
            string assetPath,
            string objectPath,
            string message,
            string rule,
            DateTime detectedAt)
        {
            this.Severity = severity;
            this.AssetPath = assetPath;
            this.ObjectPath = objectPath;
            this.Message = message;
            this.Rule = rule;
            this.DetectedAt = detectedAt;
        }

        /// <summary>
        /// Уровень проблемы.
        /// </summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Путь к файлу, в котором найдена проблема.
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// Путь к объекту внутри файла или пустая строка.
        /// </summary>
        public string ObjectPath { get; }

        /// <summary>
        /// Описание проблемы для художника.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Название проверки, которая нашла проблему.
        /// </summary>
        public string Rule { get; }

        /// <summary>
        /// Время проверки файла, в которой найдена проблема.
        /// </summary>
        public DateTime DetectedAt { get; }

        /// <summary>
        /// Возвращает копию проблемы с названием проверки.
        /// </summary>
        /// <param name="rule">Название проверки.</param>
        /// <returns>Та же проблема с заполненным <see cref="Rule"/>.</returns>
        public ValidationIssue WithRule(string rule)
            => new ValidationIssue(
                this.Severity,
                this.AssetPath,
                this.ObjectPath,
                this.Message,
                rule,
                this.DetectedAt);
    }
}
