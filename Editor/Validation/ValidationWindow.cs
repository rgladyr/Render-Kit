using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Validation.Materials;

using UnityEditor;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Окно отчёта: каждая проблема отдельным блоком. Выделение работает как в консоли Unity: клик выделяет
    /// проблему и её объект, Ctrl и Shift добавляют к выделению, Ctrl+C копирует выделенные проблемы.
    /// Список сортируется по объекту, ошибке или дате и фильтруется по уровню и объекту.
    /// </summary>
    internal sealed class ValidationWindow : EditorWindow
    {
        private const float IconWidth = 20f;

        private const float SearchFieldWidth = 220f;

        private const float BlockMargin = 4f;

        private const float BlockPadding = 6f;

        private const float LineSpacing = 2f;

        private const string AscendingMark = " ↑";

        private const string DescendingMark = " ↓";

        private const string ClipboardTimeFormat = "dd.MM.yyyy HH:mm:ss";

        private const string ClipboardSeparator = "\n\n";

        private const string CopyCommand = "Copy";

        private const string SelectAllCommand = "SelectAll";

        private static readonly Color SelectionColor =
            new Color(
                0.24f,
                0.48f,
                0.9f,
                0.35f);

        private static readonly (IssueSortColumn Column, string Title)[] SortColumns =
        {
            (IssueSortColumn.Object, "Объект"),
            (IssueSortColumn.Error, "Ошибка"),
            (IssueSortColumn.Date, "Дата"),
        };

        // Список и высоты блоков пересчитываются только при изменении отчёта, фильтров или ширины окна,
        // а рисуются только блоки в видимой области: иначе прокрутка длинного отчёта тормозит.
        private readonly List<ValidationIssue> rows = new List<ValidationIssue>();

        private readonly List<float> rowHeights = new List<float>();

        private readonly HashSet<ValidationIssue> selectedIssues = new HashSet<ValidationIssue>();

        [SerializeField]
        private bool showErrors = true;

        [SerializeField]
        private bool showWarnings = true;

        [SerializeField]
        private bool showInfos = true;

        [SerializeField]
        private string objectFilter = string.Empty;

        [SerializeField]
        private IssueSortColumn sortColumn = IssueSortColumn.Error;

        [SerializeField]
        private bool isDescending = true;

        private Dictionary<ValidationSeverity, int> severityCounts = new Dictionary<ValidationSeverity, int>();

        private bool hasIssues;

        private bool isDirty = true;

        private float measuredWidth;

        private float contentHeight;

        private Vector2 scrollPosition;

        // Строка, от которой Shift выделяет диапазон.
        private int anchorIndex = -1;

        /// <summary>
        /// Открывает окно и переводит на него фокус.
        /// </summary>
        [MenuItem(Constants.Menu.Report)]
        public static void Open()
            => GetWindow<ValidationWindow>(Constants.WindowTitle);

        /// <summary>
        /// Открывает окно, не забирая фокус у текущего окна.
        /// </summary>
        public static void ShowWithoutFocus()
            => GetWindow<ValidationWindow>(
                Constants.WindowTitle,
                false);

        private static GUIContent GetIcon(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon.sml");

                case ValidationSeverity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon.sml");

                default:
                    return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }

        private static string GetSeverityName(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error:
                    return "Ошибка";

                case ValidationSeverity.Warning:
                    return "Предупреждение";

                default:
                    return "Информация";
            }
        }

        // Сегодняшние проблемы — только время, более старые — с датой.
        private static string FormatTime(DateTime time)
        {
            var format =
                time.Date == DateTime.Today
                    ? "HH:mm:ss"
                    : "dd.MM HH:mm";

            return time.ToString(
                format,
                CultureInfo.InvariantCulture);
        }

        private static string FormatObject(ValidationIssue issue)
            => string.IsNullOrEmpty(issue.ObjectPath)
                ? issue.AssetPath
                : $"{issue.AssetPath} → {issue.ObjectPath}";

        // Формат для вставки в чат или задачу: объект, проблема, время.
        private static string FormatForClipboard(ValidationIssue issue)
        {
            var time =
                issue.DetectedAt.ToString(
                    ClipboardTimeFormat,
                    CultureInfo.InvariantCulture);

            return $"Объект: {FormatObject(issue)}\n"
                 + $"Ошибка: [{GetSeverityName(issue.Severity)} · {issue.Rule}] {issue.Message}\n"
                 + $"Время: {time}";
        }

        private static void SelectAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // Объект проблемы: экземпляры в открытых сценах, если они есть, иначе объект внутри файла,
        // а если путь к объекту не указан или не найден — сам файл.
        private static IEnumerable<UnityEngine.Object> FindTargets(ValidationIssue issue)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);

            if (asset == null)
            {
                return Array.Empty<UnityEngine.Object>();
            }

            var assetObject = FindAssetObject(issue);

            if (assetObject == null)
            {
                return new[] { asset };
            }

            var instances = FindSceneInstances(assetObject);

            return instances.Count > 0
                ? instances
                : new UnityEngine.Object[] { assetObject };
        }

        private static GameObject FindAssetObject(ValidationIssue issue)
        {
            if (string.IsNullOrEmpty(issue.ObjectPath)
             || !(AssetDatabase.LoadMainAssetAtPath(issue.AssetPath) is GameObject root))
            {
                return null;
            }

            var target =
                ModelHierarchy
                    .GetAll(root)
                    .FirstOrDefault(
                        transform => string.Equals(
                            ModelHierarchy.GetPath(
                                transform,
                                root.transform),
                            issue.ObjectPath,
                            StringComparison.Ordinal));

            return target == null
                ? null
                : target.gameObject;
        }

        private static List<UnityEngine.Object> FindSceneInstances(GameObject assetObject)
        {
            var instances = new List<UnityEngine.Object>();

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    instances.AddRange(
                        ModelHierarchy
                            .GetAll(root)
                            .Where(transform => PrefabUtility.GetCorrespondingObjectFromOriginalSource(transform.gameObject) == assetObject)
                            .Select(transform => transform.gameObject));
                }
            }

            return instances;
        }

        private static int Compare(
            IssueSortColumn column,
            ValidationIssue first,
            ValidationIssue second)
        {
            switch (column)
            {
                case IssueSortColumn.Object:
                    return CompareByObject(
                        first,
                        second);

                case IssueSortColumn.Error:
                    return CompareByError(
                        first,
                        second);

                default:
                    return first.DetectedAt.CompareTo(second.DetectedAt);
            }
        }

        private static int CompareByObject(
            ValidationIssue first,
            ValidationIssue second)
        {
            var result =
                string.Compare(
                    first.AssetPath,
                    second.AssetPath,
                    StringComparison.OrdinalIgnoreCase);

            if (result != 0)
            {
                return result;
            }

            return string.Compare(
                first.ObjectPath,
                second.ObjectPath,
                StringComparison.OrdinalIgnoreCase);
        }

        // Ошибки по уровню, внутри уровня — по названию проверки, затем по тексту.
        private static int CompareByError(
            ValidationIssue first,
            ValidationIssue second)
        {
            var result = first.Severity.CompareTo(second.Severity);

            if (result == 0)
            {
                result =
                    string.Compare(
                        first.Rule,
                        second.Rule,
                        StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0)
            {
                return result;
            }

            return string.Compare(
                first.Message,
                second.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        private static float GetInnerWidth(float rowWidth)
            => rowWidth
             - (2f * BlockMargin)
             - (2f * BlockPadding);

        // Должна совпадать с раскладкой в DrawIssue: строка заголовка, путь к объекту, текст.
        private static float GetRowHeight(
            ValidationIssue issue,
            float rowWidth)
        {
            var innerWidth = GetInnerWidth(rowWidth);

            var height =
                EditorGUIUtility.singleLineHeight
              + LineSpacing
              + EditorStyles.wordWrappedLabel.CalcHeight(
                    new GUIContent(issue.Message),
                    innerWidth);

            if (!string.IsNullOrEmpty(issue.ObjectPath))
            {
                height +=
                    LineSpacing
                  + EditorStyles.wordWrappedMiniLabel.CalcHeight(
                        new GUIContent(issue.ObjectPath),
                        innerWidth);
            }

            return height
                 + (2f * BlockPadding)
                 + BlockMargin;
        }

        private static void DrawHeader(
            Rect line,
            ValidationIssue issue)
        {
            var iconRect =
                new Rect(
                    line.x,
                    line.y,
                    IconWidth,
                    line.height);

            GUI.Label(
                iconRect,
                GetIcon(issue.Severity));

            var details = new GUIContent($"{issue.Rule} · {FormatTime(issue.DetectedAt)}");
            var detailsWidth = EditorStyles.miniLabel.CalcSize(details).x;
            var fileName = new GUIContent(Path.GetFileName(issue.AssetPath));

            var fileRect =
                new Rect(
                    line.x + IconWidth,
                    line.y,
                    Mathf.Min(
                        EditorStyles.linkLabel.CalcSize(fileName).x,
                        line.width - IconWidth - detailsWidth),
                    line.height);

            if (GUI.Button(
                fileRect,
                fileName,
                EditorStyles.linkLabel))
            {
                SelectAsset(issue.AssetPath);
            }

            var detailsRect =
                new Rect(
                    line.xMax - detailsWidth,
                    line.y,
                    detailsWidth,
                    line.height);

            GUI.Label(
                detailsRect,
                details,
                EditorStyles.miniLabel);
        }

        private static bool DrawSeverityToggle(
            bool isShown,
            ValidationSeverity severity,
            Dictionary<ValidationSeverity, int> counts)
        {
            counts.TryGetValue(
                severity,
                out var count);

            var content =
                new GUIContent(
                    count.ToString(CultureInfo.InvariantCulture),
                    GetIcon(severity).image,
                    GetSeverityName(severity));

            return GUILayout.Toggle(
                isShown,
                content,
                EditorStyles.toolbarButton);
        }

        private void OnEnable()
        {
            this.isDirty = true;
            ValidationReport.Changed += this.OnReportChanged;
        }

        private void OnDisable()
            => ValidationReport.Changed -= this.OnReportChanged;

        private void OnReportChanged()
        {
            this.isDirty = true;
            this.Repaint();
        }

        private void OnGUI()
        {
            if (this.isDirty)
            {
                this.RebuildRows();
            }

            this.HandleCommands();
            this.DrawToolbar();

            if (!this.hasIssues)
            {
                EditorGUILayout.HelpBox(
                    "Проблем не найдено. Отчёт заполняется при импорте локаций и сбрасывается при перезагрузке скриптов.",
                    MessageType.Info);

                return;
            }

            if (this.rows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Под фильтры ничего не подходит.",
                    MessageType.None);

                return;
            }

            this.DrawList();
        }

        private void RebuildRows()
        {
            var allIssues = ValidationReport.GetAll();

            var searched =
                allIssues
                    .Where(this.MatchesObject)
                    .ToList();

            this.hasIssues = allIssues.Count > 0;

            this.severityCounts =
                searched
                    .GroupBy(issue => issue.Severity)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Count());

            this.rows.Clear();
            this.rows.AddRange(searched.Where(this.MatchesSeverity));
            this.rows.Sort(this.CompareRows);

            // Выделение остаётся только у проблем, которые по-прежнему видны.
            this.selectedIssues.IntersectWith(this.rows);
            this.anchorIndex = -1;

            this.rowHeights.Clear();
            this.measuredWidth = 0f;
            this.isDirty = false;
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                    "Проверить локации заново",
                    EditorStyles.toolbarButton))
                {
                    LocationScanner.ValidateAll();
                }

                if (GUILayout.Button(
                    "Очистить",
                    EditorStyles.toolbarButton))
                {
                    ValidationReport.Clear();
                }

                if (GUILayout.Button(
                    "Материалы моделей",
                    EditorStyles.toolbarButton))
                {
                    MaterialBindingWindow.Open();
                }

                if (GUILayout.Button(
                    "Копировать видимые",
                    EditorStyles.toolbarButton))
                {
                    this.Copy(
                        string.Join(
                            ClipboardSeparator,
                            this.rows.Select(FormatForClipboard)));
                }

                GUILayout.FlexibleSpace();

                foreach (var (column, title) in SortColumns)
                {
                    this.DrawSortToggle(
                        column,
                        title);
                }

                GUILayout.Space(BlockPadding);

                this.DrawFilters();
            }
        }

        // Клик по активной колонке меняет направление, по другой — выбирает её с сортировкой по возрастанию.
        private void DrawSortToggle(
            IssueSortColumn column,
            string title)
        {
            var isActive = this.sortColumn == column;

            var label =
                isActive
                    ? title + (this.isDescending ? DescendingMark : AscendingMark)
                    : title;

            var isPressed =
                GUILayout.Toggle(
                    isActive,
                    label,
                    EditorStyles.toolbarButton);

            if (isPressed == isActive)
            {
                return;
            }

            this.isDescending = isActive && !this.isDescending;
            this.sortColumn = column;
            this.isDirty = true;
        }

        private void DrawFilters()
        {
            EditorGUI.BeginChangeCheck();

            this.objectFilter =
                GUILayout.TextField(
                    this.objectFilter,
                    EditorStyles.toolbarSearchField,
                    GUILayout.Width(SearchFieldWidth));

            this.showErrors =
                DrawSeverityToggle(
                    this.showErrors,
                    ValidationSeverity.Error,
                    this.severityCounts);

            this.showWarnings =
                DrawSeverityToggle(
                    this.showWarnings,
                    ValidationSeverity.Warning,
                    this.severityCounts);

            this.showInfos =
                DrawSeverityToggle(
                    this.showInfos,
                    ValidationSeverity.Info,
                    this.severityCounts);

            if (EditorGUI.EndChangeCheck())
            {
                this.isDirty = true;
            }
        }

        // Рисует только блоки, попавшие в видимую область прокрутки.
        private void DrawList()
        {
            var listRect =
                GUILayoutUtility.GetRect(
                    GUIContent.none,
                    GUIStyle.none,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Layout)
            {
                return;
            }

            var rowWidth =
                listRect.width
              - GUI.skin.verticalScrollbar.fixedWidth;

            this.MeasureRows(rowWidth);

            var viewRect =
                new Rect(
                    0f,
                    0f,
                    rowWidth,
                    this.contentHeight);

            this.scrollPosition =
                GUI.BeginScrollView(
                    listRect,
                    this.scrollPosition,
                    viewRect);

            var visibleBottom =
                this.scrollPosition.y
              + listRect.height;

            var top = 0f;

            for (var index = 0; index < this.rows.Count && top < visibleBottom; index++)
            {
                var height = this.rowHeights[index];

                if (top + height > this.scrollPosition.y)
                {
                    var rowRect =
                        new Rect(
                            0f,
                            top,
                            rowWidth,
                            height);

                    this.DrawIssue(
                        rowRect,
                        index);
                }

                top += height;
            }

            GUI.EndScrollView();
        }

        private void MeasureRows(float rowWidth)
        {
            if (this.rowHeights.Count == this.rows.Count
             && Mathf.Approximately(
                    this.measuredWidth,
                    rowWidth))
            {
                return;
            }

            this.rowHeights.Clear();
            this.contentHeight = 0f;

            foreach (var issue in this.rows)
            {
                var height =
                    GetRowHeight(
                        issue,
                        rowWidth);

                this.rowHeights.Add(height);
                this.contentHeight += height;
            }

            this.measuredWidth = rowWidth;
        }

        private void DrawIssue(
            Rect row,
            int index)
        {
            var issue = this.rows[index];

            var block =
                new Rect(
                    row.x + BlockMargin,
                    row.y,
                    row.width - (2f * BlockMargin),
                    row.height - BlockMargin);

            GUI.Box(
                block,
                GUIContent.none,
                EditorStyles.helpBox);

            if (this.selectedIssues.Contains(issue))
            {
                EditorGUI.DrawRect(
                    block,
                    SelectionColor);
            }

            var line =
                new Rect(
                    block.x + BlockPadding,
                    block.y + BlockPadding,
                    GetInnerWidth(row.width),
                    EditorGUIUtility.singleLineHeight);

            DrawHeader(
                line,
                issue);

            line.y += line.height + LineSpacing;

            if (!string.IsNullOrEmpty(issue.ObjectPath))
            {
                var objectPath = new GUIContent(issue.ObjectPath);

                line.height =
                    EditorStyles.wordWrappedMiniLabel.CalcHeight(
                        objectPath,
                        line.width);

                GUI.Label(
                    line,
                    objectPath,
                    EditorStyles.wordWrappedMiniLabel);

                line.y += line.height + LineSpacing;
            }

            line.height = block.yMax - BlockPadding - line.y;

            GUI.Label(
                line,
                issue.Message,
                EditorStyles.wordWrappedLabel);

            this.HandleBlockClick(
                block,
                index);
        }

        // Как в консоли Unity: клик выделяет строку, Ctrl — добавляет или убирает её, Shift — выделяет диапазон,
        // двойной клик открывает файл. Клик по имени файла забирает кнопка заголовка, он выделяет сам файл.
        private void HandleBlockClick(
            Rect block,
            int index)
        {
            var current = Event.current;

            if (current.type != EventType.MouseDown
             || current.button != 0
             || !block.Contains(current.mousePosition))
            {
                return;
            }

            var issue = this.rows[index];

            if (current.clickCount == 2)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(issue.AssetPath));
            }
            else if (current.shift
                  && this.anchorIndex >= 0)
            {
                this.SelectRange(index);
            }
            else if (EditorGUI.actionKey)
            {
                this.ToggleSelection(index);
            }
            else
            {
                this.selectedIssues.Clear();
                this.selectedIssues.Add(issue);
                this.anchorIndex = index;
            }

            this.SelectTargets();
            current.Use();
            this.Repaint();
        }

        private void SelectRange(int index)
        {
            var from =
                Mathf.Min(
                    this.anchorIndex,
                    index);

            var to =
                Mathf.Max(
                    this.anchorIndex,
                    index);

            this.selectedIssues.Clear();

            for (var rowIndex = from; rowIndex <= to; rowIndex++)
            {
                this.selectedIssues.Add(this.rows[rowIndex]);
            }
        }

        private void ToggleSelection(int index)
        {
            var issue = this.rows[index];

            if (!this.selectedIssues.Remove(issue))
            {
                this.selectedIssues.Add(issue);
            }

            this.anchorIndex = index;
        }

        private void SelectTargets()
        {
            var targets =
                this.GetSelectedRows()
                    .SelectMany(FindTargets)
                    .Distinct()
                    .ToArray();

            Selection.objects = targets;

            if (targets.Length > 0)
            {
                EditorGUIUtility.PingObject(targets[0]);
            }
        }

        // Ctrl+C копирует выделенные проблемы в порядке списка, Ctrl+A выделяет все видимые.
        private void HandleCommands()
        {
            var current = Event.current;

            if (current.type != EventType.ValidateCommand
             && current.type != EventType.ExecuteCommand)
            {
                return;
            }

            var isCopy =
                string.Equals(
                    current.commandName,
                    CopyCommand,
                    StringComparison.Ordinal);

            var isSelectAll =
                string.Equals(
                    current.commandName,
                    SelectAllCommand,
                    StringComparison.Ordinal);

            if (!isCopy
             && !isSelectAll)
            {
                return;
            }

            if (current.type == EventType.ExecuteCommand)
            {
                if (isCopy)
                {
                    this.CopySelected();
                }
                else
                {
                    this.selectedIssues.UnionWith(this.rows);
                    this.Repaint();
                }
            }

            current.Use();
        }

        private void CopySelected()
        {
            var selected = this.GetSelectedRows().ToList();

            if (selected.Count == 0)
            {
                return;
            }

            this.Copy(
                string.Join(
                    ClipboardSeparator,
                    selected.Select(FormatForClipboard)));
        }

        private IEnumerable<ValidationIssue> GetSelectedRows()
            => this.rows.Where(this.selectedIssues.Contains);

        private int CompareRows(
            ValidationIssue first,
            ValidationIssue second)
        {
            var result =
                Compare(
                    this.sortColumn,
                    first,
                    second);

            // При равенстве — по объекту, чтобы порядок не прыгал между перестроениями списка.
            if (result == 0)
            {
                result =
                    CompareByObject(
                        first,
                        second);
            }

            return this.isDescending
                ? -result
                : result;
        }

        private void Copy(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
            this.ShowNotification(new GUIContent("Скопировано"));
        }

        private bool MatchesSeverity(ValidationIssue issue)
        {
            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    return this.showErrors;

                case ValidationSeverity.Warning:
                    return this.showWarnings;

                default:
                    return this.showInfos;
            }
        }

        // Ищет и по пути к файлу, и по пути к объекту внутри модели.
        private bool MatchesObject(ValidationIssue issue)
        {
            var filter = this.objectFilter.Trim();

            if (filter.Length == 0)
            {
                return true;
            }

            return issue.AssetPath.Contains(
                       filter,
                       StringComparison.OrdinalIgnoreCase)
                || issue.ObjectPath.Contains(
                       filter,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
