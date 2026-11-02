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

            ValidationWindow.Open();
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
