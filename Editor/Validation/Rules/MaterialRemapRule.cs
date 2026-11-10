using System;
using System.Collections.Generic;
using System.Linq;

using PaperFly.RenderKit.Validation.Materials;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит материалы, которые остались внутри FBX или чья привязка указывает на пропавший файл .mat.
    /// Материал внутри FBX нельзя править, а его идентификатор меняется вместе с FBX.
    /// </summary>
    internal sealed class MaterialRemapRule : IModelRule
    {
        private const string WindowHint = "«" + Constants.Menu.Materials + "»";

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

            var missingNames = MaterialBinding.FindMissingRemaps(context.Importer);

            foreach (var name in missingNames)
            {
                issues.Add(
                    CreateIssue(
                        context,
                        name,
                        $"Привязка материала «{name}» пустая: файл .mat удалён или перемещён."));
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
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Except(
                        missingNames,
                        StringComparer.OrdinalIgnoreCase);

            foreach (var name in embeddedNames)
            {
                issues.Add(
                    CreateIssue(
                        context,
                        name,
                        $"Материал «{name}» не привязан к файлу .mat."));
            }
        }

        private static ValidationIssue CreateIssue(
            ModelContext context,
            string materialName,
            string problem)
            => new ValidationIssue(
                ValidationSeverity.Warning,
                context.AssetPath,
                string.Empty,
                $"{problem} {DescribeFix(context.AssetPath, materialName)}");

        private static string DescribeFix(
            string modelPath,
            string materialName)
        {
            var candidates =
                MaterialBinding.FindCandidates(
                    modelPath,
                    materialName);

            return candidates.Count == 0
                ? $"Файла .mat с таким именем в пакете нет: извлеките материал из FBX в окне {WindowHint}."
                : $"В пакете есть {AssetDatabase.GetAssetPath(candidates[0])}: привяжите его в окне {WindowHint}.";
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
