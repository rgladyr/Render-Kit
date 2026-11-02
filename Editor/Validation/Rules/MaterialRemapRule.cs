using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит материалы, которые остались внутри FBX и не привязаны к файлу .mat.
    /// Такой материал нельзя править, а его идентификатор меняется вместе с FBX.
    /// </summary>
    internal sealed class MaterialRemapRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Материалы";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            if (context.Importer == null
             || context.Importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                return;
            }

            var embeddedNames =
                context.Root
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Where(material => IsEmbedded(
                        material,
                        context.AssetPath))
                    .Select(material => material.name)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in embeddedNames)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        context.AssetPath,
                        string.Empty,
                        $"Материал «{name}» не привязан к файлу .mat. Привяжите его в настройках импорта (Materials → Remapped Materials)."));
            }
        }

        // Во время импорта встроенный материал ещё не сохранён и пути не имеет, после импорта его путь совпадает с путём FBX.
        private static bool IsEmbedded(
            Material material,
            string modelPath)
        {
            var materialPath = AssetDatabase.GetAssetPath(material);

            return string.IsNullOrEmpty(materialPath)
                || string.Equals(
                    materialPath,
                    modelPath,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
