using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет имя файла модели или текстуры: без версии, без пробелов и без символов вне ASCII.
    /// </summary>
    internal static class AssetNameRule
    {
        /// <summary>
        /// Название проверки в отчёте.
        /// </summary>
        public const string Title = "Имя файла";

        // Unity компилирует на C# 9 без генераторов регулярных выражений, поэтому GeneratedRegex недоступен.
        private static readonly Regex VersionName =
            new Regex(
                Constants.Names.VersionPattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Проверяет имя файла и добавляет найденные проблемы.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        public static void Validate(
            string assetPath,
            ICollection<ValidationIssue> issues)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath);

            if (VersionName.IsMatch(fileName))
            {
                issues.Add(
                    CreateIssue(
                        assetPath,
                        $"Версия в имени файла «{fileName}». Версию задаёт выпуск, а не имя."));
            }

            if (fileName.Any(char.IsWhiteSpace))
            {
                issues.Add(
                    CreateIssue(
                        assetPath,
                        $"Пробел в имени файла «{fileName}»."));
            }

            if (fileName.Any(symbol => symbol > Constants.Names.MaxAsciiCode))
            {
                issues.Add(
                    CreateIssue(
                        assetPath,
                        $"Кириллица или другие символы вне латиницы в имени файла «{fileName}»."));
            }
        }

        private static ValidationIssue CreateIssue(
            string assetPath,
            string message)
            => new ValidationIssue(
                ValidationSeverity.Warning,
                assetPath,
                string.Empty,
                message);
    }
}
