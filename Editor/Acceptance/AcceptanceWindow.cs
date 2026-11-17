using System;
using System.Collections.Generic;
using System.Linq;

using PaperFly.RenderKit.Acceptance.Rescue;
using PaperFly.RenderKit.Release;
using PaperFly.RenderKit.Validation;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Окно приёма версий локаций в игре: результат проверки, изменения с принятой версии и кнопка «Принять».
    /// </summary>
    internal sealed class AcceptanceWindow : EditorWindow
    {
        private const string WindowTitle = "Приём локаций";

        private const float ChoiceWidth = 230f;

        private const float SmallButtonWidth = 90f;

        private static readonly string[] HolderOptions = { $"Сохранить в «{LostOverrideRescue.HolderPrefix}»", "Удалить" };

        private static readonly string[] ReferenceOptions = { "Напоминать, пока не назначу", "Просто очистить" };

        private readonly HashSet<string> expanded = new HashSet<string>();

        private List<AcceptanceResult> results = new List<AcceptanceResult>();

        private List<LostPlan> plans = new List<LostPlan>();

        private List<LostReferenceStore.Entry> cleared = new List<LostReferenceStore.Entry>();

        private Vector2 scroll;

        private static string GetStatus(AcceptanceResult result)
        {
            if (result.IsAccepted)
            {
                return "принята";
            }

            if (result.IsTrial)
            {
                return "проверочная: коммит локации, а не выпуск";
            }

            return result.Accepted == null
                ? "не принята, первый приём"
                : $"не принята, принята версия {result.Accepted.version}";
        }

        private static void DrawLines(
            string title,
            IReadOnlyCollection<string> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(
                title,
                EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var line in lines)
                {
                    EditorGUILayout.LabelField(
                        line,
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void Refresh()
        {
            this.results = AcceptanceChecks.Run();

            var targets = new LocationLostTargets(this.results);

            this.plans =
                this.results
                    .SelectMany(result => LostOverrideCleaner.Plan(
                        result.LostLinkFiles,
                        targets,
                        result.Location.Name))
                    .ToList();

            this.cleared = LostReferenceStore.Refresh();

            if (this.results.Any(result => result.Issues.Count > 0))
            {
                ValidationWindow.ShowWithoutFocus();
            }

            this.Repaint();
        }

        private void Accept(AcceptanceResult result)
        {
            AcceptedManifests.Accept(result.Location);

            Debug.Log($"[Приём локаций] Принята {result.Location.Name} {result.Location.Version}. Закоммитьте {AcceptedManifests.Folder} вместе с Packages/manifest.json и packages-lock.json.");

            this.Refresh();
        }

        private void OnEnable()
        {
            this.titleContent = new GUIContent(WindowTitle);

            // При открытии и после перезагрузки скриптов результатов нет, проверка выполняется заново.
            EditorApplication.delayCall += this.Refresh;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                    "Проверить заново",
                    EditorStyles.toolbarButton))
                {
                    this.Refresh();
                }

                if (GUILayout.Button(
                    "Отчёт",
                    EditorStyles.toolbarButton))
                {
                    ValidationWindow.Open();
                }

                GUILayout.FlexibleSpace();
            }

            if (this.results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "К проекту не подключены пакеты локаций с манифестом выпуска.",
                    MessageType.Info);

                return;
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(this.scroll))
            {
                this.scroll = scrollView.scrollPosition;

                foreach (var result in this.results.ToList())
                {
                    this.DrawResult(result);
                }
            }
        }

        private void DrawResult(AcceptanceResult result)
        {
            var location = result.Location;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                $"{location.Name} {location.Version}",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Статус",
                GetStatus(result));

            EditorGUILayout.LabelField(
                "Проверка",
                $"ошибок: {result.ErrorCount}, предупреждений: {result.WarningCount}");

            var isExpanded =
                EditorGUILayout.Foldout(
                    this.expanded.Contains(location.Name),
                    $"Изменения с принятой версии: {result.Changes.Count}",
                    true);

            if (isExpanded)
            {
                this.expanded.Add(location.Name);

                foreach (var change in result.Changes)
                {
                    EditorGUILayout.LabelField(
                        change,
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                this.expanded.Remove(location.Name);
            }

            if (result.IsTrial)
            {
                EditorGUILayout.HelpBox(
                    "Подключён коммит локации без выпуска: смотреть и проверять можно, принять и закоммитить эту версию пакета нельзя. Для приёма художник выпускает версию через «" + ReleasePreparation.MenuPath + "».",
                    MessageType.Info);
            }
            else if (!result.CanAccept)
            {
                EditorGUILayout.HelpBox(
                    "Есть ошибки: версию нельзя принять. Список — в окне отчёта.",
                    MessageType.Error);
            }

            var locationPlans =
                this.plans
                    .Where(plan => string.Equals(
                        plan.PackageName,
                        location.Name,
                        StringComparison.Ordinal))
                    .ToList();

            if (locationPlans.Count > 0)
            {
                this.DrawPlans(locationPlans);
            }

            this.DrawCleared(location.Name);

            var holderFiles = result.HolderFiles;

            if (holderFiles.Count > 0
             && GUILayout.Button($"Удалить «{LostOverrideRescue.HolderPrefix} …» ({holderFiles.Count} файл.)"))
            {
                EditorApplication.delayCall += () => this.RemoveHolders(holderFiles);
            }

            using (new EditorGUI.DisabledScope(result.IsAccepted || !result.CanAccept))
            {
                if (GUILayout.Button("Принять"))
                {
                    this.Accept(result);
                }
            }
        }

        // По каждому пропавшему объекту и каждой ссылке разработчик сам выбирает, что сделать, прямо в окне.
        private void DrawPlans(List<LostPlan> locationPlans)
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Потерянные правки: объектов из основы больше нет. Выберите, что сделать, и нажмите «Применить».",
                EditorStyles.wordWrappedLabel);

            foreach (var plan in locationPlans)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        plan.File,
                        EditorStyles.boldLabel);

                    foreach (var holder in plan.Holders)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"Добавлено игрой: «{holder.Name}»",
                                EditorStyles.wordWrappedLabel);

                            holder.Keep =
                                EditorGUILayout.Popup(
                                    holder.Keep
                                        ? 0
                                        : 1,
                                    HolderOptions,
                                    GUILayout.Width(ChoiceWidth))
                             == 0;
                        }

                        DrawLines(
                            string.Empty,
                            holder.Items);
                    }

                    foreach (var reference in plan.References)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"Ссылка станет пустой: {reference.Reference.Description}",
                                EditorStyles.wordWrappedLabel);

                            reference.Track =
                                EditorGUILayout.Popup(
                                    reference.Track
                                        ? 0
                                        : 1,
                                    ReferenceOptions,
                                    GUILayout.Width(ChoiceWidth))
                             == 0;
                        }
                    }

                    DrawLines(
                        "Изменённые поля: перенести некуда, попадут в отчёт",
                        plan.Lost);

                    DrawLines(
                        "Объекты пересозданы: правки перейдут на них сами",
                        plan.Recreated);
                }
            }

            if (GUILayout.Button("Применить"))
            {
                // Меняет файлы и может открывать сцены, поэтому выполняется вне отрисовки окна.
                EditorApplication.delayCall += () => this.ApplyPlans(locationPlans);
            }
        }

        private void DrawCleared(string packageName)
        {
            var entries =
                this.cleared
                    .Where(entry => string.Equals(
                        entry.PackageName,
                        packageName,
                        StringComparison.Ordinal))
                    .ToList();

            if (entries.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                "Пустые ссылки: назначьте их заново или пропустите. Пока они есть, версию не принять.",
                EditorStyles.wordWrappedLabel);

            foreach (var entry in entries)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"{entry.FilePath}: {entry.Description}",
                        EditorStyles.wordWrappedLabel);

                    if (GUILayout.Button(
                        "Открыть",
                        GUILayout.Width(SmallButtonWidth)))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(entry.FilePath);

                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }

                    if (GUILayout.Button(
                        "Пропустить",
                        GUILayout.Width(SmallButtonWidth)))
                    {
                        LostReferenceStore.Skip(entry);

                        EditorApplication.delayCall += this.Refresh;
                    }
                }
            }
        }

        private void ApplyPlans(List<LostPlan> locationPlans)
        {
            var targets = new LocationLostTargets(this.results);

            var report =
                LostOverrideCleaner.Describe(
                    locationPlans.Select(plan => plan.File),
                    targets);

            foreach (var plan in locationPlans)
            {
                report.Add($"{plan.File}: решения");
                report.AddRange(plan.Holders.Select(holder => $"    «{holder.Name}» → {(holder.Keep ? "сохранить" : "удалить")}"));
                report.AddRange(plan.References.Select(reference => $"    {reference.Reference.Description} → {(reference.Track ? "напоминать" : "очистить")}"));
            }

            var reportPath = LostOverrideCleaner.SaveReport(report);

            Debug.Log($"[Приём локаций] Потерянные правки (отчёт сохранён в {reportPath}):\n{string.Join("\n", report)}");

            var skipped = new List<string>();

            var changed =
                LostOverrideCleaner.Apply(
                    locationPlans,
                    targets,
                    this.results
                        .Select(result => result.Location.AssetFolder)
                        .ToList(),
                    skipped);

            foreach (var file in changed)
            {
                Debug.Log($"[Приём локаций] Потерянные правки разобраны: {file}");
            }

            this.ReportSkipped(skipped);
        }

        // Пропуск легко не заметить в консоли, а правки в этих файлах остались.
        private void ReportSkipped(IReadOnlyCollection<string> skipped)
        {
            foreach (var reason in skipped)
            {
                Debug.LogWarning($"[Приём локаций] Не обработан {reason}");
            }

            if (skipped.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Не все файлы обработаны",
                    string.Join("\n\n", skipped),
                    "Понятно");
            }

            this.Refresh();
        }

        private void RemoveHolders(List<string> files)
        {
            var message =
                "Объекты «"
              + LostOverrideRescue.HolderPrefix
              + " …» будут удалены вместе со всем, что в них перенесено:\n\n"
              + string.Join("\n", files)
              + "\n\nУдаляйте, только если перенесённое больше не нужно. Изменения можно откатить через git.";

            var confirmed =
                EditorUtility.DisplayDialog(
                    "Удалить «" + LostOverrideRescue.HolderPrefix + " …»",
                    message,
                    "Удалить",
                    "Отмена");

            if (!confirmed)
            {
                return;
            }

            var skipped = new List<string>();

            var changed =
                LostOverrideCleaner.RemoveHolders(
                    files,
                    skipped);

            foreach (var file in changed)
            {
                Debug.Log($"[Приём локаций] Удалены объекты «{LostOverrideRescue.HolderPrefix} …»: {file}");
            }

            this.ReportSkipped(skipped);
        }

        /// <summary>
        /// Открывает окно приёма и проверяет подключённые локации.
        /// </summary>
        [MenuItem(AcceptanceChecks.MenuPath)]
        public static void Open()
        {
            // Новое окно проверяет локации само при открытии, уже открытое нужно проверить заново.
            var wasOpen = HasOpenInstances<AcceptanceWindow>();
            var window = GetWindow<AcceptanceWindow>(WindowTitle);

            if (wasOpen)
            {
                window.Refresh();
            }
        }
    }
}
