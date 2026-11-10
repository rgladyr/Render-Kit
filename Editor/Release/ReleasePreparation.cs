using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using PaperFly.RenderKit.Validation;

using UnityEditor;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Готовит выпуск версии локации: проверяет пакет, показывает изменения основы, записывает новую версию
    /// в package.json и манифест. Коммит и тег художник делает сам.
    /// </summary>
    internal sealed class ReleasePreparation : EditorWindow
    {
        /// <summary>
        /// Пункт меню подготовки выпуска.
        /// </summary>
        public const string MenuPath = "PaperFly/Выпуск/Подготовить выпуск";

        private const string LogPrefix = "[Подготовка выпуска]";

        private const string DialogTitle = "Подготовка выпуска";

        private const string PackageFileName = "package.json";

        private const string MajorPart = "первую";

        private const string MinorPart = "среднюю";

        private const string PatchPart = "последнюю";

        private const float VersionWidth = 120f;

        private static readonly Regex VersionFormat =
            new Regex(
                @"^\d+\.\d+\.\d+$",
                RegexOptions.Compiled);

        private static readonly Regex VersionField =
            new Regex(
                "\"version\"\\s*:\\s*\"[^\"]*\"",
                RegexOptions.Compiled);

        private readonly List<Entry> entries = new List<Entry>();

        private HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);

        private bool tagsUnavailable;

        private Vector2 scroll;

        private static bool ShowErrors(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return false;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"{LogPrefix} {error}");
            }

            ValidationWindow.ShowWithoutFocus();

            EditorUtility.DisplayDialog(
                DialogTitle,
                $"Найдено ошибок: {errors.Count}. Выпуск не подготовлен. Список ошибок — в консоли и окне отчёта.",
                "Понятно");

            return true;
        }

        // Теги читаются из git, чтобы не выпустить версию, номер которой уже занят.
        private static HashSet<string> ReadTags(out bool unavailable)
        {
            var startInfo =
                new ProcessStartInfo(
                    "git",
                    "tag --list")
                {
                    WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    var output = process.StandardOutput.ReadToEnd();

                    process.WaitForExit();

                    unavailable = process.ExitCode != 0;

                    return new HashSet<string>(
                        output
                            .Split(
                                new[] { '\r', '\n' },
                                StringSplitOptions.RemoveEmptyEntries)
                            .Select(tag => tag.Trim()),
                        StringComparer.Ordinal);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LogPrefix} Не удалось прочитать теги git: {exception.Message}");

                unavailable = true;

                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private static bool TryParse(
            string text,
            out Version version)
        {
            version = null;

            return VersionFormat.IsMatch(text ?? string.Empty)
                && Version.TryParse(
                    text,
                    out version);
        }

        private static string Bump(
            string current,
            int part)
        {
            if (!TryParse(
                    current,
                    out var version))
            {
                return current;
            }

            switch (part)
            {
                case 0:
                    return $"{version.Major + 1}.0.0";

                case 1:
                    return $"{version.Major}.{version.Minor + 1}.0";

                default:
                    return $"{version.Major}.{version.Minor}.{version.Build + 1}";
            }
        }

        // Подсказка по семантике версий: что сломается у игры, то и определяет цифру.
        private static string Suggest(Entry entry)
        {
            var version = entry.Package.Version;

            if (entry.Previous == null)
            {
                return "Первый выпуск этой локации.";
            }

            if (entry.Previous.format != ReleaseManifest.CurrentFormat)
            {
                return $"Прошлый манифест старого формата, объекты сравнить нельзя. Поднимите хотя бы {PatchPart} цифру: {Bump(version, 2)}.";
            }

            if (entry.ObjectChanges.Count > 0)
            {
                return $"Объекты основы удалены или переименованы, в игре могут потеряться правки. Поднимите {MajorPart} цифру: {Bump(version, 0)}.";
            }

            if (ManifestComparer.HasAddedObjects(
                    entry.Previous,
                    entry.Current))
            {
                return $"Добавлены объекты. Поднимите {MinorPart} цифру: {Bump(version, 1)}.";
            }

            return entry.FileChanges.Count > 0
                ? $"Изменились только файлы. Поднимите {PatchPart} цифру: {Bump(version, 2)}."
                : "С прошлого выпуска ничего не изменилось.";
        }

        private static void WriteVersion(
            LocationPackage package,
            string version)
        {
            var path = $"{package.Folder}/{PackageFileName}";
            var text = File.ReadAllText(path);

            var updated =
                VersionField.Replace(
                    text,
                    $"\"version\": \"{version}\"",
                    1);

            File.WriteAllText(
                path,
                updated);

            AssetDatabase.ImportAsset(path);
        }

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var errors = ReleaseChecks.Run(false);

            if (ShowErrors(errors))
            {
                return;
            }

            var window =
                GetWindow<ReleasePreparation>(
                    true,
                    DialogTitle);

            window.Load(ReleaseChecks.FindPackages(errors));
        }

        private void Load(IEnumerable<LocationPackage> packages)
        {
            this.tags = ReadTags(out this.tagsUnavailable);

            this.entries.Clear();
            this.entries.AddRange(packages.Select(package => new Entry(package)));
        }

        private string Validate(Entry entry)
        {
            if (!TryParse(
                    entry.NewVersion,
                    out var newVersion))
            {
                return "Версия должна быть в виде 1.2.3.";
            }

            if (TryParse(
                    entry.Package.Version,
                    out var currentVersion)
             && newVersion <= currentVersion)
            {
                return $"Новая версия должна быть выше текущей {entry.Package.Version}.";
            }

            return this.tags.Contains(entry.NewVersion)
                ? $"Тег {entry.NewVersion} уже есть в репозитории: выберите другую версию."
                : null;
        }

        private void Write()
        {
            // Пока окно было открыто, файлы могли измениться, поэтому пакет проверяется ещё раз.
            if (ShowErrors(ReleaseChecks.Run(false)))
            {
                return;
            }

            var summary = new StringBuilder();

            foreach (var entry in this.entries)
            {
                // Хеш package.json входит в манифест, поэтому сначала записывается версия, потом манифест.
                WriteVersion(
                    entry.Package,
                    entry.NewVersion);

                var manifest =
                    ManifestBuilder.Build(
                        entry.Package.Folder,
                        entry.Package.Name,
                        entry.NewVersion);

                ManifestBuilder.Save(
                    entry.Package.Folder,
                    manifest);

                foreach (var change in entry.ObjectChanges)
                {
                    Debug.LogWarning($"{LogPrefix} {entry.Package.Folder}: {change}");
                }

                summary.AppendLine($"{entry.Package.Name}: {entry.Package.Version} → {entry.NewVersion}");
            }

            var newVersions =
                string.Join(
                    ", ",
                    this.entries.Select(entry => entry.NewVersion).Distinct());

            summary.AppendLine();
            summary.Append($"Версия записана в package.json и манифест выпуска. Закоммитьте их в Fork, поставьте на этот коммит тег {newVersions} и отправьте вместе с тегом.");

            this.Close();

            EditorUtility.DisplayDialog(
                DialogTitle,
                summary.ToString(),
                "Понятно");
        }

        private void OnGUI()
        {
            if (this.entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Пакеты локаций не найдены.",
                    MessageType.Info);

                return;
            }

            if (this.tagsUnavailable)
            {
                EditorGUILayout.HelpBox(
                    "Не удалось прочитать теги git: занятость номера версии не проверяется.",
                    MessageType.Warning);
            }

            var hasErrors = false;

            using (var scrollView = new EditorGUILayout.ScrollViewScope(this.scroll))
            {
                this.scroll = scrollView.scrollPosition;

                foreach (var entry in this.entries)
                {
                    hasErrors |= !this.DrawEntry(entry);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(hasErrors))
                {
                    if (GUILayout.Button("Записать версию и манифест"))
                    {
                        EditorApplication.delayCall += this.Write;
                    }
                }

                if (GUILayout.Button("Отмена"))
                {
                    this.Close();
                }
            }
        }

        private bool DrawEntry(Entry entry)
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                entry.Package.Name,
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Текущая версия",
                entry.Package.Version);

            entry.NewVersion =
                EditorGUILayout.TextField(
                    "Новая версия",
                    entry.NewVersion,
                    GUILayout.MinWidth(VersionWidth));

            EditorGUILayout.HelpBox(
                entry.Suggestion,
                MessageType.Info);

            entry.IsExpanded =
                EditorGUILayout.Foldout(
                    entry.IsExpanded,
                    $"Изменения с прошлого выпуска: объектов {entry.ObjectChanges.Count}, файлов {entry.FileChanges.Count}",
                    true);

            if (entry.IsExpanded)
            {
                foreach (var change in entry.ObjectChanges.Concat(entry.FileChanges))
                {
                    EditorGUILayout.LabelField(
                        change,
                        EditorStyles.wordWrappedMiniLabel);
                }
            }

            var error = this.Validate(entry);

            if (error == null)
            {
                return true;
            }

            EditorGUILayout.HelpBox(
                error,
                MessageType.Error);

            return false;
        }

        /// <summary>
        /// Пакет, который готовится к выпуску.
        /// </summary>
        private sealed class Entry
        {
            public Entry(LocationPackage package)
            {
                this.Package = package;
                this.Previous = ManifestBuilder.Load(package.Folder);
                this.Current = package.BuildManifest();

                // Сравнивать объекты можно только с манифестом текущего формата.
                var comparable =
                    this.Previous != null
                 && this.Previous.format == ReleaseManifest.CurrentFormat;

                this.ObjectChanges =
                    comparable
                        ? ManifestComparer.DescribeObjectChanges(
                            this.Previous,
                            this.Current)
                        : new List<string>();

                this.FileChanges =
                    this.Previous == null
                        ? new List<string>()
                        : ManifestComparer.DescribeFileChanges(
                            this.Previous,
                            this.Current);

                // Подставляется текущая версия: художник обязан поднять её сам.
                this.NewVersion = package.Version;
                this.Suggestion = Suggest(this);
            }

            public LocationPackage Package { get; }

            public ReleaseManifest Previous { get; }

            public ReleaseManifest Current { get; }

            public List<string> ObjectChanges { get; }

            public List<string> FileChanges { get; }

            public string Suggestion { get; }

            public string NewVersion { get; set; }

            public bool IsExpanded { get; set; }
        }
    }
}
