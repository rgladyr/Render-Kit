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

        /// <summary>
        /// Проверяет имя файла модели и имена папок по соглашению о наименованиях: PascalCase, разделитель — точка.
        /// </summary>
        /// <param name="assetPath">Путь к файлу.</param>
        /// <param name="checkFileName"><c>false</c> для текстур: их имя проверяет отдельное правило.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        /// <remarks>
        /// Пробелы и символы вне латиницы уже ошибки (<see cref="Validate"/>), здесь только предупреждения.
        /// </remarks>
        public static void ValidateConvention(
            string assetPath,
            bool checkFileName,
            ICollection<ValidationIssue> issues)
        {
            var packageFolder = LocationAssets.FindPackageFolder(assetPath) ?? string.Empty;

            var relative =
                assetPath.Length > packageFolder.Length
                    ? assetPath.Substring(packageFolder.Length).TrimStart('/')
                    : assetPath;

            var parts = relative.Split('/').ToList();
            var folders = parts.Take(parts.Count - 1).ToList();

            var names =
                checkFileName
                    ? folders.Append(Path.GetFileNameWithoutExtension(assetPath))
                    : folders;

            foreach (var name in names)
            {
                var problems =
                    NameGrammar
                        .FindSymbolProblems(name)
                        .Where(problem => !problem.StartsWith(
                                              "пробел",
                                              System.StringComparison.Ordinal)
                                       && !problem.Contains("не латиниц"))
                        .ToList();

                if (name.Length > 0
                 && char.IsLower(name[0]))
                {
                    problems.Add("слова пишутся с заглавной (PascalCase)");
                }

                if (problems.Count > 0)
                {
                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Warning,
                            assetPath,
                            string.Empty,
                            $"Имя «{name}» не по соглашению: {string.Join("; ", problems)}."));
                }
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
