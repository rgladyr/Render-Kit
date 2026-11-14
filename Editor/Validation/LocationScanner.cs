using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет все модели и текстуры локаций без переимпорта.
    /// </summary>
    internal static class LocationScanner
    {
        private static readonly ModelValidator Validator = new ModelValidator();

        /// <summary>
        /// Заново проверяет все файлы локаций и открывает отчёт.
        /// </summary>
        [MenuItem(Constants.Menu.ValidateLocations)]
        public static void ValidateAll()
        {
            Rescan();

            ValidationWindow.Open();
        }

        /// <summary>
        /// Заново проверяет все файлы локаций и обновляет отчёт, не открывая окно.
        /// </summary>
        /// <returns>Все найденные проблемы.</returns>
        public static List<ValidationIssue> Rescan()
        {
            ValidationReport.Clear();

            foreach (var path in FindLocationAssets("t:Model"))
            {
                ValidateModel(path);
            }

            foreach (var path in FindLocationAssets("t:Texture2D"))
            {
                ValidationReport.Set(
                    path,
                    ModelValidator.ValidateTexture(path));
            }

            return ValidationReport.GetAll();
        }

        /// <summary>
        /// Заново проверяет только указанные модели и текстуры: быстрая проверка при коммите.
        /// </summary>
        /// <param name="paths">Пути файлов в проекте; остальные файлы пропускаются.</param>
        /// <returns>Проблемы указанных файлов.</returns>
        public static List<ValidationIssue> Rescan(IReadOnlyCollection<string> paths)
        {
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path);

                if (importer is ModelImporter)
                {
                    ValidateModel(path);
                }
                else if (importer is TextureImporter)
                {
                    ValidationReport.Set(
                        path,
                        ModelValidator.ValidateTexture(path));
                }
            }

            return ValidationReport
                .GetAll()
                .Where(issue => paths.Contains(issue.AssetPath))
                .ToList();
        }

        private static string[] FindLocationAssets(string filter)
            => AssetDatabase
                .FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(LocationAssets.IsLocationAsset)
                .Distinct()
                .ToArray();

        private static void ValidateModel(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
            {
                return;
            }

            var issues =
                Validator.ValidateModel(
                    new ModelContext(
                        path,
                        root,
                        AssetImporter.GetAtPath(path) as ModelImporter));

            ValidationReport.Set(
                path,
                issues);
        }
    }
}
