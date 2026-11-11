using System;
using System.Collections.Generic;
using System.Linq;

using PaperFly.RenderKit.Validation.Materials;

using UnityEditor;
using UnityEditor.Presets;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Применяет пресеты к моделям и текстурам локации при каждом импорте и проверяет импортированные модели.
    /// </summary>
    internal sealed class LocationImportPostprocessor : AssetPostprocessor
    {
        private const string PresetRuleTitle = "Пресет импорта";

        private static readonly ModelValidator Validator = new ModelValidator();

        private static readonly string[] ArtistOwnedProperties = { "m_LODScreenPercentages" };

        // Unity создаёт новый обработчик на каждый импортируемый файл, поэтому проблемы предобработки
        // доживают до постобработки того же файла.
        private readonly List<ValidationIssue> preprocessIssues = new List<ValidationIssue>();

        /// <inheritdoc/>
        public override uint GetVersion()
            => Constants.PostprocessorVersion;

        // Текстуры при параллельном импорте обрабатываются в отдельных процессах, поэтому их имена
        // проверяются здесь, в основном процессе редактора, после завершения всего импорта.
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in deletedAssets.Concat(movedFromAssetPaths))
            {
                ValidationReport.Remove(path);
            }

            var locationAssets =
                importedAssets
                    .Where(LocationAssets.IsLocationAsset)
                    .ToList();

            foreach (var path in locationAssets.Where(path => AssetImporter.GetAtPath(path) is TextureImporter))
            {
                ValidationReport.Set(
                    path,
                    ModelValidator.ValidateTexture(path));
            }

            if (locationAssets.Any(ValidationReport.HasIssues))
            {
                EditorApplication.delayCall -= ValidationWindow.ShowWithoutFocus;
                EditorApplication.delayCall += ValidationWindow.ShowWithoutFocus;
            }

            MaterialBindingWindow.OfferFor(locationAssets.Where(path => AssetImporter.GetAtPath(path) is ModelImporter));
        }

        private void ApplyPreset(string presetPath)
        {
            var presetGuid =
                AssetDatabase.AssetPathToGUID(
                    presetPath,
                    AssetPathToGUIDOptions.OnlyExistingAssets);

            if (string.IsNullOrEmpty(presetGuid))
            {
                this.AddPreprocessIssue(
                    ValidationSeverity.Warning,
                    $"Пресет {presetPath} не найден, настройки импорта не применены.");

                return;
            }

            // Изменение пресета переимпортирует все файлы, к которым он применялся.
            this.context.DependsOnArtifact(new GUID(presetGuid));

            var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);

            if (preset == null
             || !preset.ApplyTo(
                    this.assetImporter,
                    this.GetAppliedProperties(preset)))
            {
                this.AddPreprocessIssue(
                    ValidationSeverity.Warning,
                    $"Пресет {presetPath} не подходит к этому файлу, настройки импорта не применены.");
            }
        }

        // Настройки, которые художник подбирает под каждую модель (проценты LOD), берутся из пресета только при первом
        // импорте файла. Дальше пресет их не трогает, иначе каждый переимпорт стирал бы ручные значения.
        private string[] GetAppliedProperties(Preset preset)
        {
            var isFirstImport = this.assetImporter.importSettingsMissing;

            return preset.PropertyModifications
                .Select(modification => modification.propertyPath)
                .Where(path => isFirstImport
                            || !ArtistOwnedProperties.Any(property => path.StartsWith(
                                   property,
                                   StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private void AddPreprocessIssue(
            ValidationSeverity severity,
            string message)
            => this.preprocessIssues.Add(
                new ValidationIssue(
                        severity,
                        this.assetPath,
                        string.Empty,
                        message)
                    .WithRule(PresetRuleTitle));

        private void OnPostprocessModel(GameObject root)
        {
            if (!LocationAssets.IsLocationAsset(this.assetPath))
            {
                return;
            }

            var issues =
                Validator.ValidateModel(
                    new ModelContext(
                        this.assetPath,
                        root,
                        this.assetImporter as ModelImporter));

            ValidationReport.Set(
                this.assetPath,
                this.preprocessIssues.Concat(issues));
        }

        private void OnPreprocessModel()
        {
            if (LocationAssets.IsLocationAsset(this.assetPath))
            {
                this.ApplyPreset(Constants.Paths.ModelPreset);
            }
        }

        private void OnPreprocessTexture()
        {
            if (LocationAssets.IsLocationAsset(this.assetPath))
            {
                this.ApplyPreset(Constants.Paths.TexturePreset);
            }
        }
    }
}
