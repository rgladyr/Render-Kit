using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит объекты и материалы с именами Blender по умолчанию (Cube.001, Material.003)
    /// и объекты с суффиксом копии .001.
    /// </summary>
    internal sealed class DefaultNamesRule : IModelRule
    {
        // Unity компилирует на C# 9 без генераторов регулярных выражений, поэтому GeneratedRegex недоступен.
        private static readonly Regex DefaultObjectName =
            new Regex(
                Constants.Names.BlenderDefaultObjectPattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DefaultMaterialName =
            new Regex(
                Constants.Names.BlenderDefaultMaterialPattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DuplicateSuffix =
            new Regex(
                Constants.Names.DuplicateSuffixPattern,
                RegexOptions.Compiled);

        /// <inheritdoc/>
        public string Title => "Имена по умолчанию";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var target in ModelHierarchy.GetAll(context.Root))
            {
                ValidateObjectName(
                    context,
                    target,
                    issues);

                ValidateMaterialNames(
                    context,
                    target,
                    issues);
            }
        }

        private static void ValidateObjectName(
            ModelContext context,
            Transform target,
            ICollection<ValidationIssue> issues)
        {
            if (DefaultObjectName.IsMatch(target.name))
            {
                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Error,
                        target,
                        $"Имя Blender по умолчанию «{target.name}». Дайте объекту осмысленное имя."));

                return;
            }

            if (DuplicateSuffix.IsMatch(target.name))
            {
                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Error,
                        target,
                        $"Суффикс копии в имени «{target.name}». Похоже на случайный дубль."));
            }
        }

        private static void ValidateMaterialNames(
            ModelContext context,
            Transform target,
            ICollection<ValidationIssue> issues)
        {
            if (!target.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            var defaultNames =
                renderer.sharedMaterials
                    .Where(material => material != null)
                    .Select(material => material.name)
                    .Where(name => DefaultMaterialName.IsMatch(name))
                    .Distinct();

            foreach (var name in defaultNames)
            {
                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Error,
                        target,
                        $"Материал с именем Blender по умолчанию «{name}». Дайте материалу осмысленное имя."));
            }
        }
    }
}
