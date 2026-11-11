using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation.Materials
{
    /// <summary>
    /// Находит материалы моделей без привязки к файлу .mat, привязывает их по имени и извлекает из модели.
    /// </summary>
    internal static class MaterialBinding
    {
        private const string AssetsFolder = "Assets";

        private const string ModelsFolder = "Models";

        private const string MaterialsFolder = "Materials";

        private const string MaterialExtension = ".mat";

        private const char PathSeparator = '/';

        private const char InvalidCharReplacement = '_';

        private static AssetImporter.SourceAssetIdentifier ToIdentifier(string materialName)
            => new AssetImporter.SourceAssetIdentifier(
                typeof(Material),
                materialName);

        private static string GetParent(string path)
        {
            var separator = path.LastIndexOf(PathSeparator);

            return separator < 0
                ? string.Empty
                : path.Substring(
                    0,
                    separator);
        }

        // Вне пакета локации материалы ищутся по всему Assets.
        private static string GetPackageFolder(string modelPath)
            => LocationAssets.FindPackageFolder(modelPath) ?? AssetsFolder;

        // Models/Locations/Childhood.v2.fbx → Materials/Locations/Childhood.v2, как уже разложены материалы пакета.
        private static string GetExtractFolder(string modelPath)
        {
            var packageFolder = GetPackageFolder(modelPath);
            var modelFolder = GetParent(modelPath);
            var modelName = Path.GetFileNameWithoutExtension(modelPath);

            var relative =
                modelFolder.Length > packageFolder.Length
                    ? modelFolder.Substring(packageFolder.Length + 1)
                    : string.Empty;

            var segments =
                relative.Split(
                    new[] { PathSeparator },
                    StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length > 0
             && string.Equals(
                    segments[0],
                    ModelsFolder,
                    StringComparison.OrdinalIgnoreCase))
            {
                segments[0] = MaterialsFolder;

                return string.Join(
                    PathSeparator.ToString(),
                    new[] { packageFolder }
                        .Concat(segments)
                        .Append(modelName));
            }

            return $"{modelFolder}/{MaterialsFolder}/{modelName}";
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = GetParent(folder);

            EnsureFolder(parent);

            AssetDatabase.CreateFolder(
                parent,
                folder.Substring(parent.Length + 1));
        }

        private static string ToFileName(string materialName)
        {
            var invalid = Path.GetInvalidFileNameChars();

            return new string(
                materialName
                    .Select(symbol => invalid.Contains(symbol)
                        ? InvalidCharReplacement
                        : symbol)
                    .ToArray());
        }

        private static void Bind(
            ModelImporter importer,
            MaterialSlot slot,
            Material target)
        {
            var identifier = ToIdentifier(slot.Name);

            importer.RemoveRemap(identifier);

            importer.AddRemap(
                identifier,
                target);
        }

        private static void Extract(MaterialSlot slot)
        {
            var folder = GetExtractFolder(slot.ModelPath);

            EnsureFolder(folder);

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{ToFileName(slot.Name)}{MaterialExtension}");

            var error =
                AssetDatabase.ExtractAsset(
                    slot.Embedded,
                    path);

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Не удалось извлечь материал «{slot.Name}» из {slot.ModelPath}: {error}");
            }
        }

        /// <summary>
        /// Проверяет, можно ли менять настройки импорта модели.
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели.</param>
        /// <returns><c>true</c> для файлов в Assets; пакеты, подключённые по git, только для чтения.</returns>
        public static bool IsEditable(string modelPath)
            => modelPath.StartsWith(
                AssetsFolder + PathSeparator,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Находит в пакете модели файлы .mat с тем же именем, что у материала.
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели.</param>
        /// <param name="materialName">Имя материала в модели.</param>
        /// <returns>Найденные материалы.</returns>
        public static List<Material> FindCandidates(
            string modelPath,
            string materialName)
            => AssetDatabase
                .FindAssets(
                    $"{materialName} t:Material",
                    new[] { GetPackageFolder(modelPath) })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => path.EndsWith(
                    MaterialExtension,
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    materialName,
                    StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(material => material != null)
                .ToList();

        /// <summary>
        /// Находит имена материалов, у которых есть привязка, но файл .mat пропал.
        /// </summary>
        /// <param name="importer">Импортёр модели.</param>
        /// <returns>Имена материалов с пустой привязкой.</returns>
        public static List<string> FindMissingRemaps(ModelImporter importer)
            => importer
                .GetExternalObjectMap()
                .Where(pair => pair.Key.type == typeof(Material))
                .Where(pair => pair.Value == null)
                .Select(pair => pair.Key.name)
                .ToList();

        /// <summary>
        /// Находит материалы модели без привязки к файлу .mat.
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели.</param>
        /// <returns>Непривязанные материалы; пустой список, если модель материалы не импортирует.</returns>
        public static List<MaterialSlot> FindUnbound(string modelPath)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
             || importer.materialImportMode == ModelImporterMaterialImportMode.None)
            {
                return new List<MaterialSlot>();
            }

            var embedded =
                AssetDatabase
                    .LoadAllAssetsAtPath(modelPath)
                    .OfType<Material>()
                    .GroupBy(
                        material => material.name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase);

            var missing =
                new HashSet<string>(
                    FindMissingRemaps(importer),
                    StringComparer.OrdinalIgnoreCase);

            return embedded.Keys
                .Union(
                    missing,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    name => name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(name => new MaterialSlot(
                    modelPath,
                    name,
                    missing.Contains(name),
                    embedded.TryGetValue(
                        name,
                        out var material)
                        ? material
                        : null,
                    FindCandidates(
                        modelPath,
                        name)))
                .ToList();
        }

        /// <summary>
        /// Привязывает и извлекает материалы, затем переимпортирует изменённые модели.
        /// </summary>
        /// <param name="choices">Выбранные действия; пропущенные материалы в списке не нужны.</param>
        /// <returns>Пути к переимпортированным моделям.</returns>
        public static List<string> Apply(IReadOnlyCollection<MaterialChoice> choices)
        {
            var models =
                choices
                    .Select(choice => choice.Slot.ModelPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            foreach (var choice in choices.Where(choice => choice.Target != null))
            {
                Bind(
                    (ModelImporter)AssetImporter.GetAtPath(choice.Slot.ModelPath),
                    choice.Slot,
                    choice.Target);
            }

            // Извлечение создаёт файлы .mat и само дописывает привязку в настройки импорта модели.
            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var choice in choices.Where(choice => choice.Target == null))
                {
                    Extract(choice.Slot);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var model in models)
            {
                AssetDatabase.WriteImportSettingsIfDirty(model);

                AssetDatabase.ImportAsset(
                    model,
                    ImportAssetOptions.ForceUpdate);
            }

            return models;
        }
    }
}
