using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет размер исходной текстуры: степень двойки и не больше максимума из настроек импорта.
    /// </summary>
    internal static class TextureSizeRule
    {
        /// <summary>
        /// Название проверки в отчёте.
        /// </summary>
        public const string Title = "Размер текстуры";

        /// <summary>
        /// Проверяет размер текстуры и добавляет найденные проблемы.
        /// </summary>
        /// <param name="assetPath">Путь к текстуре.</param>
        /// <param name="importer">Импортёр текстуры.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        public static void Validate(
            string assetPath,
            TextureImporter importer,
            ICollection<ValidationIssue> issues)
        {
            importer.GetSourceTextureWidthAndHeight(
                out var width,
                out var height);

            if (!Mathf.IsPowerOfTwo(width)
             || !Mathf.IsPowerOfTwo(height))
            {
                issues.Add(
                    CreateIssue(
                        ValidationSeverity.Warning,
                        assetPath,
                        $"Размер {width}×{height} — не степень двойки. Используйте 256, 512, 1024, 2048: так лучше сжатие и mipmaps."));
            }

            var maxSize = GetSmallestMaxSize(importer);

            var longestSide =
                Mathf.Max(
                    width,
                    height);

            if (longestSide > maxSize)
            {
                issues.Add(
                    CreateIssue(
                        ValidationSeverity.Info,
                        assetPath,
                        $"Исходник {width}×{height}, в игре будет уменьшен до {maxSize}. Можно сохранить файл меньшего размера."));
            }
        }

        // Итоговый размер ограничен самым строгим из общих настроек и переопределений мобильных платформ.
        private static int GetSmallestMaxSize(TextureImporter importer)
        {
            var maxSize = importer.maxTextureSize;

            foreach (var platform in Constants.Limits.TexturePlatforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);

                if (settings.overridden)
                {
                    maxSize =
                        Mathf.Min(
                            maxSize,
                            settings.maxTextureSize);
                }
            }

            return maxSize;
        }

        private static ValidationIssue CreateIssue(
            ValidationSeverity severity,
            string assetPath,
            string message)
            => new ValidationIssue(
                severity,
                assetPath,
                string.Empty,
                message);
    }
}
