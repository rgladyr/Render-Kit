using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Acceptance.Rescue;

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
    /// Сначала добавленное на удалённые объекты (компоненты, дочерние объекты) переносится в объекты «_Потерянное …»
    /// <see cref="LostOverrideRescue.HolderPrefix"/>, затем остальное убирает встроенное в Unity удаление неиспользуемых
    /// правок (то же, что Overrides → Unused → Remove) у экземпляров префабов из пакетов локаций.
    /// </remarks>
    internal static class LostOverrideCleaner
    {
        private const string PrefabExtension = ".prefab";

        private const string SceneExtension = ".unity";

        private const string ReportFolder = "Logs/PaperFly";

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

        private static bool IsSupported(string file)
            => file.EndsWith(
                   PrefabExtension,
                   StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(
                   SceneExtension,
                   StringComparison.OrdinalIgnoreCase);

        // Переносит добавленное в «_Потерянное …» прямо в тексте файла, пока Unity его не загрузила и не потеряла.
        private static bool Rescue(
            string path,
            ILostTargets targets)
        {
            var file = UnityYamlFile.Parse(File.ReadAllText(path));

            var items =
                LostOverrideRescue.Find(
                    file,
                    targets);

            // До переноса: перенос перестраивает списки правок, и номера строк ссылок сместились бы.
            var cleared =
                LostReferenceFinder.Clear(
                    LostReferenceFinder.Find(
                        file,
                        targets));

            var rescued =
                LostOverrideRescue.Rescue(
                    file,
                    items,
                    targets);

            if (!cleared
             && !rescued)
            {
                return false;
            }

            File.WriteAllText(
                path,
                file.ToText());

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);

            return true;
        }

        // Открытый на редактирование префаб при сохранении из редактора затёр бы изменения файла, поэтому его нужно закрыть.
        private static bool TryCloseStage(
            string path,
            ICollection<string> skipped)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage == null
             || !string.Equals(
                    stage.assetPath,
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (stage.scene.isDirty)
            {
                skipped.Add($"{path}: префаб открыт на редактирование и в нём есть несохранённые изменения. Сохраните или отмените их и нажмите кнопку ещё раз.");

                return false;
            }

            StageUtility.GoToMainStage();

            return true;
        }

        private static int DestroyHolders(
            IEnumerable<GameObject> roots,
            Func<string, bool> isSelected)
        {
            var holders =
                roots
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(transform => transform.name.StartsWith(
                        LostOverrideRescue.HolderPrefix,
                        StringComparison.Ordinal))
                    .Where(transform => isSelected(transform.name))
                    .Select(transform => transform.gameObject)
                    .ToList();

            // Вложенный «_Потерянное» уничтожается вместе с родительским.
            foreach (var holder in holders.Where(holder => holder != null))
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }

            return holders.Count;
        }

        private static bool RemoveHoldersFromPrefab(
            string path,
            Func<string, bool> isSelected,
            ICollection<string> skipped)
        {
            if (!TryCloseStage(
                    path,
                    skipped))
            {
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var destroyed =
                    DestroyHolders(
                        new[] { root },
                        isSelected);

                if (destroyed == 0)
                {
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    path);

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool RemoveHoldersFromScene(
            string path,
            Func<string, bool> isSelected,
            ICollection<string> skipped)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var wasOpen = scene.isLoaded;

            if (wasOpen
             && HasDirtyScenes())
            {
                skipped.Add($"{path}: сцена открыта, а в открытых сценах есть несохранённые изменения. Сохраните их и нажмите кнопку ещё раз.");

                return false;
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
                var destroyed =
                    DestroyHolders(
                        scene.GetRootGameObjects(),
                        isSelected);

                if (destroyed == 0)
                {
                    return false;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                return true;
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

        private static string CleanPrefab(
            string path,
            ILostTargets targets,
            IReadOnlyCollection<string> locationFolders,
            ICollection<string> skipped)
        {
            if (!TryCloseStage(
                    path,
                    skipped))
            {
                return null;
            }

            var before = File.ReadAllText(path);

            Rescue(
                path,
                targets);

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

        private static bool HasDirtyScenes()
            => Enumerable
                .Range(
                    0,
                    SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.isDirty);

        // Открытая сцена уже загружена со старым текстом: после переноса её нужно перечитать с диска.
        private static bool RescueOpenScene(
            string path,
            ILostTargets targets)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();

            if (!Rescue(
                    path,
                    targets))
            {
                return false;
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            EditorSceneManager.RestoreSceneManagerSetup(setup);

            return true;
        }

        private static string CleanScene(
            string path,
            ILostTargets targets,
            IReadOnlyCollection<string> locationFolders,
            ICollection<string> skipped)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var wasOpen = scene.isLoaded;

            // Несохранённые изменения не трогаем: сохранение записало бы и их, а перечитывание сцены их бы потеряло.
            if (wasOpen
             && HasDirtyScenes())
            {
                skipped.Add($"{path}: сцена открыта, а в открытых сценах есть несохранённые изменения. Сохраните их и нажмите кнопку ещё раз.");

                return null;
            }

            var before = File.ReadAllText(path);

            if (wasOpen)
            {
                RescueOpenScene(
                    path,
                    targets);

                scene = SceneManager.GetSceneByPath(path);
            }
            else
            {
                Rescue(
                    path,
                    targets);

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

                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                }

                return string.Equals(
                        before,
                        File.ReadAllText(path),
                        StringComparison.Ordinal)
                    ? null
                    : path;
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
        /// Описывает, что потеряно в указанных файлах и что будет перенесено в объекты «_Потерянное …».
        /// </summary>
        /// <param name="files">Файлы игры с потерянными правками.</param>
        /// <param name="targets">Сведения об объектах подключённых версий локаций.</param>
        /// <returns>Строки отчёта: файл, затем его правки с отступом.</returns>
        public static List<string> Describe(
            IEnumerable<string> files,
            ILostTargets targets)
        {
            var report = new List<string>();

            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                report.Add(file);

                if (!IsSupported(file))
                {
                    report.Add("    файлы этого типа очищаются только вручную");

                    continue;
                }

                var parsed = UnityYamlFile.Parse(File.ReadAllText(file));

                var items =
                    LostOverrideRescue.Find(
                        parsed,
                        targets);

                var references =
                    LostReferenceFinder.Find(
                        parsed,
                        targets);

                report.AddRange(items.Select(item => "    " + item.Description));
                report.AddRange(references.Select(reference => $"    {reference.Description} → ссылка станет пустой: назначьте её заново"));
            }

            return report;
        }

        /// <summary>
        /// Записывает отчёт в папку Logs проекта.
        /// </summary>
        /// <param name="report">Строки отчёта.</param>
        /// <returns>Путь к файлу отчёта.</returns>
        public static string SaveReport(IEnumerable<string> report)
        {
            Directory.CreateDirectory(ReportFolder);

            var path =
                Path.Combine(
                        ReportFolder,
                        $"Потерянные правки {DateTime.Now:yyyy-MM-dd HH-mm-ss}.txt")
                    .Replace(
                        '\\',
                        '/');

            File.WriteAllLines(
                path,
                report);

            return path;
        }

        /// <summary>
        /// Удаляет объекты «_Потерянное …» вместе со всем, что в них перенесено.
        /// </summary>
        /// <param name="files">Сцены и префабы игры с такими объектами.</param>
        /// <param name="skipped">Сюда добавляются файлы, которые не удалось обработать, с причиной.</param>
        /// <returns>Изменённые файлы.</returns>
        public static List<string> RemoveHolders(
            IEnumerable<string> files,
            ICollection<string> skipped)
        {
            var changed = new List<string>();

            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var isChanged =
                    RemoveHolders(
                        file,
                        _ => true,
                        skipped);

                if (isChanged)
                {
                    changed.Add(file);
                }
            }

            AssetDatabase.SaveAssets();

            return changed;
        }

        /// <summary>
        /// Удаляет из файла выбранные объекты «_Потерянное …».
        /// </summary>
        /// <param name="file">Сцена или префаб игры.</param>
        /// <param name="isSelected">Отбор по имени объекта.</param>
        /// <param name="skipped">Сюда добавляется причина, если файл не удалось обработать.</param>
        /// <returns><c>true</c>, если файл изменён.</returns>
        public static bool RemoveHolders(
            string file,
            Func<string, bool> isSelected,
            ICollection<string> skipped)
            => file.EndsWith(
                   PrefabExtension,
                   StringComparison.OrdinalIgnoreCase)
                ? RemoveHoldersFromPrefab(
                    file,
                    isSelected,
                    skipped)
                : RemoveHoldersFromScene(
                    file,
                    isSelected,
                    skipped);

        /// <summary>
        /// Собирает потерянные правки файлов для окна приёма: по каждой разработчик выбирает, что сделать.
        /// </summary>
        /// <param name="files">Файлы игры с потерянными правками.</param>
        /// <param name="targets">Сведения об объектах подключённых версий локаций.</param>
        /// <param name="packageName">Пакет локации.</param>
        /// <returns>По плану на файл; файлы, где разбирать нечего, пропускаются.</returns>
        public static List<LostPlan> Plan(
            IEnumerable<string> files,
            ILostTargets targets,
            string packageName)
        {
            var plans = new List<LostPlan>();

            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase).Where(IsSupported))
            {
                var parsed = UnityYamlFile.Parse(File.ReadAllText(file));

                var plan =
                    new LostPlan(
                        file,
                        packageName);

                var items =
                    LostOverrideRescue.Find(
                        parsed,
                        targets);

                foreach (var group in items.Where(item => item.HolderName != null).GroupBy(item => item.HolderName))
                {
                    var choice = new LostPlan.HolderChoice(group.Key);

                    choice.Items.AddRange(group.Select(item => item.Description));
                    plan.Holders.Add(choice);
                }

                plan.Recreated.AddRange(
                    items
                        .Where(item => item.IsRecreated)
                        .Select(item => item.Description));

                plan.Lost.AddRange(
                    items
                        .Where(item => item.HolderName == null)
                        .Where(item => !item.IsRecreated)
                        .Select(item => item.Description));

                var references =
                    LostReferenceFinder.Find(
                        parsed,
                        targets);

                plan.References.AddRange(references.Select(reference => new LostPlan.ReferenceChoice(reference)));

                if (!plan.IsEmpty)
                {
                    plans.Add(plan);
                }
            }

            return plans;
        }

        /// <summary>
        /// Выполняет решения разработчика: переносит добавленное, удаляет ненужное, запоминает очищенные ссылки.
        /// </summary>
        /// <param name="plans">Планы из <see cref="Plan"/> с выбранными действиями.</param>
        /// <param name="targets">Сведения об объектах подключённых версий локаций.</param>
        /// <param name="locationFolders">Папки пакетов локаций для AssetDatabase: Packages/имя.</param>
        /// <param name="skipped">Сюда добавляются файлы, которые не удалось обработать, с причиной.</param>
        /// <returns>Изменённые файлы.</returns>
        /// <remarks>
        /// Сначала всё добавленное переносится в «_Потерянное …»: так Unity не теряет его при очистке правок.
        /// Затем объекты, которые разработчик решил удалить, удаляются целиком вместе с дочерними.
        /// </remarks>
        public static List<string> Apply(
            IReadOnlyCollection<LostPlan> plans,
            ILostTargets targets,
            IReadOnlyCollection<string> locationFolders,
            ICollection<string> skipped)
        {
            var cleaned = new List<string>();

            foreach (var plan in plans)
            {
                var skippedBefore = skipped.Count;

                var result =
                    plan.File.EndsWith(
                        PrefabExtension,
                        StringComparison.OrdinalIgnoreCase)
                        ? CleanPrefab(
                            plan.File,
                            targets,
                            locationFolders,
                            skipped)
                        : CleanScene(
                            plan.File,
                            targets,
                            locationFolders,
                            skipped);

                if (skipped.Count > skippedBefore)
                {
                    continue;
                }

                var removed =
                    new HashSet<string>(
                        plan.Holders
                            .Where(holder => !holder.Keep)
                            .Select(holder => holder.Name),
                        StringComparer.Ordinal);

                var isRemoved =
                    removed.Count > 0
                 && RemoveHolders(
                        plan.File,
                        removed.Contains,
                        skipped);

                LostReferenceStore.Add(
                    plan.File,
                    plan.PackageName,
                    plan.References
                        .Where(reference => reference.Track)
                        .Select(reference => reference.Reference));

                if (result != null
                 || isRemoved)
                {
                    cleaned.Add(plan.File);
                }
            }

            AssetDatabase.SaveAssets();

            return cleaned;
        }
    }
}
