using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет имя и формат файла текстуры по соглашению: ИмяМодели[.Вариант].Канал.png.
    /// </summary>
    /// <remarks>
    /// Пока правило выдаёт только предупреждения: существующие локации ещё не приведены к соглашению.
    /// </remarks>
    internal static class TextureNameRule
    {
        /// <summary>
        /// Название правила в отчёте.
        /// </summary>
        public const string Title = "Имя текстуры";

        private const string PngExtension = ".png";

        /// <summary>
        /// Проверяет файл текстуры.
        /// </summary>
        /// <param name="assetPath">Путь к текстуре.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        public static void Validate(
            string assetPath,
            ICollection<ValidationIssue> issues)
        {
            var problems = new List<string>();
            var fileName = Path.GetFileNameWithoutExtension(assetPath);

            // PNG без потерь: при сборке движок пережимает карты в ASTC/ETC2, и артефакты сжатия сложились бы.
            if (!string.Equals(
                    Path.GetExtension(assetPath),
                    PngExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("формат карт — PNG: артефакты JPG и других форматов с потерями сложатся со сжатием при сборке");
            }

            problems.AddRange(NameGrammar.FindSymbolProblems(fileName));

            var channel = NameGrammar.FindTextureChannel(fileName);

            if (channel == null)
            {
                problems.Add($"в конце имени — назначение карты: {string.Join(", ", NameGrammar.TextureChannels)}");
            }

            if (!NameGrammar.HasVersion(fileName))
            {
                problems.Add("в начале имени — полное имя модели с версией (Box.v1)");
            }

            var lowercase =
                fileName
                    .Split('.')
                    .Where(part => part.Length > 0)
                    .Where(part => char.IsLower(part[0]))
                    .Where(part => !NameGrammar.HasVersion(part))
                    .ToList();

            if (lowercase.Count > 0)
            {
                problems.Add($"слова пишутся с заглавной (PascalCase): {string.Join(", ", lowercase)}");
            }

            if (problems.Count == 0)
            {
                return;
            }

            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    assetPath,
                    string.Empty,
                    $"Текстура «{Path.GetFileName(assetPath)}» не по соглашению (ИмяМодели[.Вариант].Канал.png): {string.Join("; ", problems)}."));
        }
    }
}
