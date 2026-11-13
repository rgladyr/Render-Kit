using System;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;

using Object = UnityEngine.Object;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Создаёт в игре вариант основы локации, которая пришла пакетом только для чтения.
    /// </summary>
    /// <remarks>
    /// Стандартный пункт Create → Prefab Variant для префаба из пакета неактивен: Unity не может положить файл рядом
    /// с основой. Вариант же — файл игры, который только ссылается на основу, поэтому его сохраняем в Assets.
    /// </remarks>
    internal static class LocationVariantCreator
    {
        private const string MenuPath = "Assets/PaperFly/Создать вариант локации";

        private const string PackagesPrefix = "Packages/";

        private const string VariantSuffix = "_Game";

        private const string PrefabExtension = "prefab";

        private const string DefaultFolder = "Assets";

        private const int MenuPriority = 19;

        private static GameObject GetSelectedBase()
        {
            if (!(Selection.activeObject is GameObject selected)
             || !PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(selected);

            return path.StartsWith(
                    PackagesPrefix,
                    StringComparison.OrdinalIgnoreCase)
                ? selected
                : null;
        }

        [MenuItem(
            MenuPath,
            true)]
        private static bool CanCreate()
            => GetSelectedBase() != null;

        [MenuItem(
            MenuPath,
            false,
            MenuPriority)]
        private static void Create()
        {
            var source = GetSelectedBase();

            var path =
                EditorUtility.SaveFilePanelInProject(
                    "Вариант локации",
                    source.name + VariantSuffix,
                    PrefabExtension,
                    "Где сохранить вариант основы в проекте игры",
                    DefaultFolder);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Экземпляр создаётся во временной сцене, чтобы открытая сцена не пометилась изменённой.
            var scene = EditorSceneManager.NewPreviewScene();

            try
            {
                var instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(
                        source,
                        scene);

                // Сохранение экземпляра чужого префаба создаёт вариант, а не копию.
                var variant =
                    PrefabUtility.SaveAsPrefabAsset(
                        instance,
                        path);

                Selection.activeObject = variant;
                EditorGUIUtility.PingObject(variant);

                Debug.Log($"[Приём локаций] Создан вариант {path} основы {AssetDatabase.GetAssetPath(source)}.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
