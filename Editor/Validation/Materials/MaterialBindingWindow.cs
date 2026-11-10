using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation.Materials
{
    /// <summary>
    /// Предлагает художнику, что сделать с материалами моделей без привязки к файлу .mat:
    /// привязать найденный по имени, извлечь из FBX или пока оставить как есть.
    /// </summary>
    internal sealed class MaterialBindingWindow : EditorWindow
    {
        private const string WindowTitle = "Материалы моделей";

        private const string SkipOption = "Не привязывать";

        private const string ExtractOption = "Извлечь из FBX";

        private const string BindOptionPrefix = "Привязать: ";

        private const string ModelSearchFilter = "t:Model";

        private const string AssetsFolder = "Assets";

        // Косая черта в пункте выпадающего списка Unity превращает его в подменю.
        private const char MenuSeparator = '/';

        private const char MenuSafeSeparator = '∕';

        private const float NameWidth = 240f;

        private const float StatusWidth = 150f;

        private const int SkipIndex = 0;

        private const int NoIndex = -1;

        private const string Hint =
            "У моделей есть материалы без привязки к файлу .mat. Такой материал нельзя править, "
          + "а при обновлении FBX он пересоздаётся. Выберите, что сделать с каждым: привязать файл .mat "
          + "с тем же именем, извлечь материал из FBX в файл или пока не привязывать.";

        private static readonly HashSet<string> PendingModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Переимпорт после «Применить» снова вызывает обработчик импорта: окно не должно открыться само на себя.
        private static bool isApplying;

        private readonly List<Entry> entries = new List<Entry>();

        [SerializeField]
        private List<string> modelPaths = new List<string>();

        private Vector2 scroll;

        private static bool IsCandidateModel(string path)
            => LocationAssets.IsLocationAsset(path)
            && MaterialBinding.IsEditable(path);

        private static void ShowPending()
        {
            var models = PendingModels.ToList();

            PendingModels.Clear();

            if (!models.Any(model => MaterialBinding.FindUnbound(model).Count > 0))
            {
                return;
            }

            var window =
                GetWindow<MaterialBindingWindow>(
                    WindowTitle,
                    true);

            window.AddModels(models);
        }

        private static string ToMenuLabel(Material material)
            => BindOptionPrefix
             + AssetDatabase
                .GetAssetPath(material)
                .Replace(
                    MenuSeparator,
                    MenuSafeSeparator);

        private void AddModels(IEnumerable<string> models)
        {
            foreach (var model in models.Where(model => !this.modelPaths.Contains(
                         model,
                         StringComparer.OrdinalIgnoreCase)))
            {
                this.modelPaths.Add(model);
            }

            this.Rebuild();
        }

        private void Rebuild()
        {
            this.entries.Clear();

            this.entries.AddRange(
                this.modelPaths
                    .SelectMany(MaterialBinding.FindUnbound)
                    .Select(slot => new Entry(slot)));

            var withSlots =
                new HashSet<string>(
                    this.entries.Select(entry => entry.Slot.ModelPath),
                    StringComparer.OrdinalIgnoreCase);

            this.modelPaths.RemoveAll(model => !withSlots.Contains(model));
        }

        private void Apply()
        {
            var choices =
                this.entries
                    .Where(entry => entry.Selected != SkipIndex)
                    .Select(entry => new MaterialChoice(
                        entry.Slot,
                        entry.GetTarget()))
                    .ToList();

            isApplying = true;

            try
            {
                if (choices.Count > 0)
                {
                    MaterialBinding.Apply(choices);
                }
            }
            finally
            {
                isApplying = false;
            }

            this.Close();
        }

        private void SelectForAll(Func<Entry, int> select)
        {
            foreach (var entry in this.entries)
            {
                var index = select(entry);

                if (index != NoIndex)
                {
                    entry.Selected = index;
                }
            }
        }

        private void OnEnable()
        {
            this.titleContent = new GUIContent(WindowTitle);

            // После перезагрузки скриптов сохраняются только пути к моделям, материалы ищутся заново.
            this.Rebuild();
        }

        private void OnGUI()
        {
            if (this.entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Все материалы моделей привязаны к файлам .mat.",
                    MessageType.Info);

                if (GUILayout.Button("Закрыть"))
                {
                    this.Close();
                }

                return;
            }

            EditorGUILayout.HelpBox(
                Hint,
                MessageType.Warning);

            this.DrawSelectAll();

            using (var scrollView = new EditorGUILayout.ScrollViewScope(this.scroll))
            {
                this.scroll = scrollView.scrollPosition;

                foreach (var model in this.entries.GroupBy(entry => entry.Slot.ModelPath))
                {
                    this.DrawModel(
                        model.Key,
                        model);
                }
            }

            this.DrawButtons();
        }

        private void DrawSelectAll()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    "Для всех:",
                    EditorStyles.miniLabel);

                if (GUILayout.Button(
                    "Привязать найденные",
                    EditorStyles.toolbarButton))
                {
                    this.SelectForAll(entry => entry.SingleCandidateIndex);
                }

                if (GUILayout.Button(
                    ExtractOption,
                    EditorStyles.toolbarButton))
                {
                    this.SelectForAll(entry => entry.ExtractIndex);
                }

                if (GUILayout.Button(
                    SkipOption,
                    EditorStyles.toolbarButton))
                {
                    this.SelectForAll(entry => SkipIndex);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawModel(
            string modelPath,
            IEnumerable<Entry> modelEntries)
        {
            EditorGUILayout.Space();

            if (GUILayout.Button(
                modelPath,
                EditorStyles.boldLabel))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(modelPath));
            }

            foreach (var entry in modelEntries)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        entry.Slot.Name,
                        GUILayout.Width(NameWidth));

                    GUILayout.Label(
                        entry.Slot.IsMissing
                            ? "файл .mat пропал"
                            : "внутри FBX",
                        EditorStyles.miniLabel,
                        GUILayout.Width(StatusWidth));

                    entry.Selected =
                        EditorGUILayout.Popup(
                            entry.Selected,
                            entry.Options);
                }
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Применить"))
                {
                    // Применение переимпортирует модели, поэтому выполняется вне отрисовки окна.
                    EditorApplication.delayCall += this.Apply;
                }

                if (GUILayout.Button("Закрыть"))
                {
                    this.Close();
                }
            }
        }

        /// <summary>
        /// Открывает окно для всех моделей локаций, которые можно править.
        /// </summary>
        [MenuItem(Constants.Menu.Materials)]
        public static void Open()
        {
            var models =
                AssetDatabase
                    .FindAssets(
                        ModelSearchFilter,
                        new[] { AssetsFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsCandidateModel)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var window = GetWindow<MaterialBindingWindow>(WindowTitle);

            window.AddModels(models);
        }

        /// <summary>
        /// Открывает окно после импорта, если у импортированных моделей есть непривязанные материалы.
        /// </summary>
        /// <param name="importedModels">Пути к импортированным моделям.</param>
        public static void OfferFor(IEnumerable<string> importedModels)
        {
            if (Application.isBatchMode
             || isApplying)
            {
                return;
            }

            foreach (var model in importedModels.Where(IsCandidateModel))
            {
                PendingModels.Add(model);
            }

            if (PendingModels.Count == 0)
            {
                return;
            }

            // Материалы внутри модели доступны только после завершения импорта.
            EditorApplication.delayCall -= ShowPending;
            EditorApplication.delayCall += ShowPending;
        }

        /// <summary>
        /// Непривязанный материал и выбранное для него действие.
        /// </summary>
        private sealed class Entry
        {
            private readonly List<Material> targets = new List<Material>();

            public Entry(MaterialSlot slot)
            {
                this.Slot = slot;

                var options = new List<string> { SkipOption };

                this.targets.Add(null);

                this.ExtractIndex = NoIndex;

                if (slot.CanExtract)
                {
                    this.ExtractIndex = options.Count;

                    options.Add(ExtractOption);
                    this.targets.Add(null);
                }

                foreach (var candidate in slot.Candidates)
                {
                    options.Add(ToMenuLabel(candidate));
                    this.targets.Add(candidate);
                }

                this.Options = options.ToArray();

                this.SingleCandidateIndex =
                    slot.Candidates.Count == 1
                        ? options.Count - 1
                        : NoIndex;

                // Привязка выбирается сама, только если файл .mat с таким именем один; иначе решает художник.
                this.Selected =
                    this.SingleCandidateIndex == NoIndex
                        ? SkipIndex
                        : this.SingleCandidateIndex;
            }

            public MaterialSlot Slot { get; }

            public string[] Options { get; }

            public int ExtractIndex { get; }

            public int SingleCandidateIndex { get; }

            public int Selected { get; set; }

            public Material GetTarget()
                => this.targets[this.Selected];
        }
    }
}
