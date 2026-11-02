using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Запускает все проверки модели и текстуры и подписывает каждую проблему названием проверки.
    /// </summary>
    internal sealed class ModelValidator
    {
        private readonly IModelRule[] rules =
        {
            new DuplicateSiblingNamesRule(),
            new DefaultNamesRule(),
            new NegativeScaleRule(),
            new ExportArtifactRule(),
            new MeshContentRule(),
            new LodPairRule(),
            new MaterialRemapRule(),
            new CoordinateRangeRule(),
            new MeshComplexityRule(),
        };

        /// <summary>
        /// Проверяет текстуру: имя файла и размер исходника.
        /// </summary>
        /// <param name="assetPath">Путь к текстуре.</param>
        /// <returns>Найденные проблемы.</returns>
        public static List<ValidationIssue> ValidateTexture(string assetPath)
        {
            var issues = new List<ValidationIssue>();

            Run(
                AssetNameRule.Title,
                found => AssetNameRule.Validate(
                    assetPath,
                    found),
                issues);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                Run(
                    TextureSizeRule.Title,
                    found => TextureSizeRule.Validate(
                        assetPath,
                        importer,
                        found),
                    issues);
            }

            return issues;
        }

        private static void Run(
            string title,
            Action<List<ValidationIssue>> validate,
            List<ValidationIssue> issues)
        {
            var found = new List<ValidationIssue>();

            validate(found);

            issues.AddRange(found.Select(issue => issue.WithRule(title)));
        }

        /// <summary>
        /// Проверяет модель: имя файла и все правила иерархии.
        /// </summary>
        /// <param name="context">Проверяемая модель.</param>
        /// <returns>Найденные проблемы.</returns>
        public List<ValidationIssue> ValidateModel(ModelContext context)
        {
            var issues = new List<ValidationIssue>();

            Run(
                AssetNameRule.Title,
                found => AssetNameRule.Validate(
                    context.AssetPath,
                    found),
                issues);

            foreach (var rule in this.rules)
            {
                Run(
                    rule.Title,
                    found => rule.Validate(
                        context,
                        found),
                    issues);
            }

            return issues;
        }
    }
}
