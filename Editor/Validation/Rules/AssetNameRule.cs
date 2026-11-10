using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет имя файла модели или текстуры: без пробелов и без символов вне ASCII.
    /// </summary>
    internal static class AssetNameRule
    {
        /// <summary>
        /// Название проверки в отчёте.
        /// </summary>
        public const string Title = "Имя файла";

        /// <summary>
        /// Проверяет имя файла и добавляет найденные проблемы.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        public static void Validate(
            string assetPath,
            ICollection<ValidationIssue> issues)
        {
            // Версия в имени (Childhood.v2) разрешена: по принятому правилу модели маркируются версиями.
            var fileName = Path.GetFileNameWithoutExtension(assetPath);

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
                ValidationSeverity.Error,
                assetPath,
                string.Empty,
                message);
    }
}
