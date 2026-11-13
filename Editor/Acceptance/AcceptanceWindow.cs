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

        // Больше строк окно подтверждения не вмещает.
        private const int DialogReportLines = 25;

        private readonly HashSet<string> expanded = new HashSet<string>();

        private List<AcceptanceResult> results = new List<AcceptanceResult>();

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

        private void Refresh()
        {
            this.results = AcceptanceChecks.Run();

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

            var lostFiles = result.LostLinkFiles;

            if (lostFiles.Count > 0
             && GUILayout.Button($"Убрать потерянные правки ({lostFiles.Count} файл.)"))
            {
                // Меняет файлы и может открывать сцены, поэтому выполняется вне отрисовки окна.
                EditorApplication.delayCall += () => this.CleanLostOverrides(lostFiles);
            }

            using (new EditorGUI.DisabledScope(result.IsAccepted || !result.CanAccept))
            {
                if (GUILayout.Button("Принять"))
                {
                    this.Accept(result);
                }
            }
        }

        private void CleanLostOverrides(List<string> files)
        {
            var targets = new LocationLostTargets(this.results);

            var report =
                LostOverrideCleaner.Describe(
                    files,
                    targets);

            var shown = report.Take(DialogReportLines);

            var more =
                report.Count > DialogReportLines
                    ? $"\n… и ещё {report.Count - DialogReportLines} строк: полный список будет в консоли и в файле отчёта."
                    : string.Empty;

            var message =
                "Эти правки указывают на объекты, удалённые в новой версии локации:\n\n"
              + string.Join("\n", shown)
              + more
              + "\n\nДобавленные компоненты и дочерние объекты будут перенесены в «"
              + LostOverrideRescue.HolderPrefix
              + " …» под корнем экземпляра, остальные правки будут удалены. Изменения можно откатить через git.";

            var confirmed =
                EditorUtility.DisplayDialog(
                    "Убрать потерянные правки",
                    message,
                    "Убрать",
                    "Отмена");

            if (!confirmed)
            {
                return;
            }

            var reportPath = LostOverrideCleaner.SaveReport(report);

            Debug.Log($"[Приём локаций] Потерянные правки (отчёт сохранён в {reportPath}):\n{string.Join("\n", report)}");

            var skipped = new List<string>();

            var cleaned =
                LostOverrideCleaner.Clean(
                    files,
                    targets,
                    this.results
                        .Select(result => result.Location.AssetFolder)
                        .ToList(),
                    skipped);

            foreach (var file in cleaned)
            {
                Debug.Log($"[Приём локаций] Убраны потерянные правки: {file}");
            }

            foreach (var reason in skipped)
            {
                Debug.LogWarning($"[Приём локаций] Не обработан {reason}");
            }

            // Пропуск легко не заметить в консоли, а правки в этих файлах остались.
            if (skipped.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Не все файлы обработаны",
                    string.Join("\n\n", skipped),
                    "Понятно");
            }

            this.Refresh();
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
