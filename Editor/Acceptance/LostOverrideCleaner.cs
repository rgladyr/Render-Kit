using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Убирает из вариантов и сцен игры правки, которые указывают на объекты, удалённые из новой версии локации.
    /// </summary>
    /// <remarks>
    /// Работает через встроенное в Unity удаление неиспользуемых правок (то же, что Overrides → Unused → Remove)
    /// и только у экземпляров префабов из пакетов локации. Логику на новый объект разработчик переносит до этого.
    /// </remarks>
    internal static class LostOverrideCleaner
    {
        private const string PrefabExtension = ".prefab";

        private const string SceneExtension = ".unity";

        private static bool IsFromLocation(
            GameObject gameObject,
            IReadOnlyCollection<string> locationFolders)
        {
            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

            return !string.IsNullOrEmpty(assetPath)
                && locationFolders.Any(folder => assetPath.StartsWith(
                    folder + "/",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject[] FindLocationInstances(
            IEnumerable<GameObject> roots,
            IReadOnlyCollection<string> locationFolders)
            => roots
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
                .Where(gameObject => IsFromLocation(
                    gameObject,
                    locationFolders))
                .ToArray();

        // Проверки «есть ли неиспользуемые правки» Unity наружу не даёт, поэтому очистка вызывается у всех экземпляров,
        // а был ли файл изменён, определяется по его содержимому или по пометке сцены.
        private static void RemoveFrom(GameObject[] instances)
        {
            if (instances.Length == 0)
            {
                return;
            }

            PrefabUtility.RemoveUnusedOverrides(
                instances,
                InteractionMode.AutomatedAction);
        }

        private static string CleanPrefab(
            string path,
            IReadOnlyCollection<string> locationFolders)
        {
            var before = File.ReadAllText(path);
            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                RemoveFrom(
                    FindLocationInstances(
                        new[] { root },
                        locationFolders));

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    path);

                return string.Equals(
                        before,
                        File.ReadAllText(path),
                        StringComparison.Ordinal)
                    ? null
                    : path;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string CleanScene(
            string path,
            IReadOnlyCollection<string> locationFolders,
            ICollection<string> skipped)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var wasOpen = scene.isLoaded;

            // Несохранённые изменения в открытой сцене не трогаем: иначе сохранение записало бы и их.
            if (wasOpen
             && scene.isDirty)
            {
                skipped.Add($"{path}: сцена открыта и не сохранена. Сохраните её и нажмите кнопку ещё раз.");

                return null;
            }

            if (!wasOpen)
            {
                scene =
                    EditorSceneManager.OpenScene(
                        path,
                        OpenSceneMode.Additive);
            }

            try
            {
                RemoveFrom(
                    FindLocationInstances(
                        scene.GetRootGameObjects(),
                        locationFolders));

                if (!scene.isDirty)
                {
                    return null;
                }

                EditorSceneManager.SaveScene(scene);

                return path;
            }
            finally
            {
                if (!wasOpen)
                {
                    EditorSceneManager.CloseScene(
                        scene,
                        true);
                }
            }
        }

        /// <summary>
        /// Убирает потерянные правки из указанных файлов игры.
        /// </summary>
        /// <param name="files">Файлы игры, в которых проверка приёма нашла потерянные правки.</param>
        /// <param name="locationFolders">Папки пакетов локаций для AssetDatabase: Packages/имя.</param>
        /// <param name="skipped">Сюда добавляются файлы, которые не удалось обработать, с причиной.</param>
        /// <returns>Файлы, в которых правки убраны и которые сохранены.</returns>
        public static List<string> Clean(
            IEnumerable<string> files,
            IReadOnlyCollection<string> locationFolders,
            ICollection<string> skipped)
        {
            var cleaned = new List<string>();

            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string result;

                if (file.EndsWith(
                        PrefabExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result =
                        CleanPrefab(
                            file,
                            locationFolders);
                }
                else if (file.EndsWith(
                             SceneExtension,
                             StringComparison.OrdinalIgnoreCase))
                {
                    result =
                        CleanScene(
                            file,
                            locationFolders,
                            skipped);
                }
                else
                {
                    skipped.Add($"{file}: файлы этого типа очищаются только вручную.");

                    continue;
                }

                if (result != null)
                {
                    cleaned.Add(result);
                }
            }

            AssetDatabase.SaveAssets();

            return cleaned;
        }
    }
}
