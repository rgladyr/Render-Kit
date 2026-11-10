using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.PackageManager;

using UnityEngine;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Окно версий локации: показывает все коммиты репозитория и подключает выбранный.
    /// </summary>
    internal sealed class LocationVersionsWindow : EditorWindow
    {
        private const float RowHeight = 22f;

        private const float CellPadding = 6f;

        private const float HashWidth = 64f;

        private const float DateWidth = 116f;

        private const float AuthorWidth = 110f;

        private const float LabelsWidth = 130f;

        private const float ButtonWidth = 96f;

        private const float LocationPopupWidth = 280f;

        private const float InputLabelWidth = 110f;

        private const string DateFormat = "dd.MM.yyyy HH:mm";

        private static readonly Color CurrentRowColor =
            new Color(
                0.3f,
                0.6f,
                1f,
                0.18f);

        private List<GitLocation> locations = new List<GitLocation>();

        private List<CommitInfo> commits = new List<CommitInfo>();

        [SerializeField]
        private string selectedName = string.Empty;

        [SerializeField]
        private string revisionInput = string.Empty;

        [SerializeField]
        private string newLocationUrl = string.Empty;

        // Переживает перезагрузку скриптов, поэтому сеть не трогается при каждой компиляции,
        // но новое окно один раз получает свежие коммиты.
        [SerializeField]
        private bool hasFetched;

        private bool isBusy;

        private string statusMessage = string.Empty;

        private MessageType statusType = MessageType.None;

        private Vector2 scrollPosition;

        /// <summary>
        /// Открывает окно.
        /// </summary>
        [MenuItem(Constants.MenuPath)]
        public static void Open()
            => GetWindow<LocationVersionsWindow>(Constants.WindowTitle);

        private static string Shorten(string hash)
            => hash.Length > Constants.ShortHashLength
                ? hash.Substring(
                    0,
                    Constants.ShortHashLength)
                : hash;

        private static Rect TakeLeft(
            ref Rect row,
            float width)
        {
            var cell =
                new Rect(
                    row.x,
                    row.y,
                    width,
                    row.height);

            row.x += width + CellPadding;
            row.width -= width + CellPadding;

            return cell;
        }

        private static RepositoryHistory CreateHistory(GitLocation location)
            => new RepositoryHistory(
                Constants.HistoryCacheDirectory,
                location.Name,
                location.RepositoryUrl);

        private static void DrawCommitCells(
            Rect row,
            CommitInfo commit)
        {
            var date =
                commit.Date.ToLocalTime().ToString(
                    DateFormat,
                    CultureInfo.InvariantCulture);

            var hashRect =
                TakeLeft(
                    ref row,
                    HashWidth);

            GUI.Label(
                hashRect,
                new GUIContent(
                    commit.ShortHash,
                    commit.Hash));

            var dateRect =
                TakeLeft(
                    ref row,
                    DateWidth);

            GUI.Label(
                dateRect,
                date);

            var authorRect =
                TakeLeft(
                    ref row,
                    AuthorWidth);

            GUI.Label(
                authorRect,
                commit.Author);

            var labelsRect =
                TakeLeft(
                    ref row,
                    LabelsWidth);

            var labelsStyle =
                commit.HasTag
                    ? EditorStyles.boldLabel
                    : EditorStyles.miniLabel;

            GUI.Label(
                labelsRect,
                new GUIContent(
                    commit.Labels,
                    commit.Labels),
                labelsStyle);

            GUI.Label(
                row,
                new GUIContent(
                    commit.Subject,
                    commit.Subject));
        }

        private void OnEnable()
        {
            Events.registeredPackages += this.OnPackagesRegistered;

            this.ReloadLocations();
            this.StartLoadingCommits(!this.hasFetched);
        }

        private void OnDisable()
            => Events.registeredPackages -= this.OnPackagesRegistered;

        private void OnPackagesRegistered(PackageRegistrationEventArgs arguments)
        {
            var previousName = this.selectedName;

            this.ReloadLocations();

            // Выбранный пакет удалили: список коммитов относится уже не к нему.
            if (!string.Equals(
                previousName,
                this.selectedName,
                StringComparison.Ordinal))
            {
                this.StartLoadingCommits(true);
            }

            this.Repaint();
        }

        private void ReloadLocations()
        {
            this.locations = GitLocation.GetAll();

            if (this.GetSelectedLocation() == null
             && this.locations.Count > 0)
            {
                this.selectedName = this.locations[0].Name;
            }
        }

        private GitLocation GetSelectedLocation()
            => this.locations.FirstOrDefault(location => string.Equals(
                location.Name,
                this.selectedName,
                StringComparison.Ordinal));

        private void SetStatus(
            string message,
            MessageType type)
        {
            this.statusMessage = message;
            this.statusType = type;
        }

        private void StartLoadingCommits(bool update)
            => _ = this.RunAsync(
                "Загружаю историю…",
                () => this.LoadCommitsAsync(update));

        // Только одна операция за раз: кнопки на это время выключены.
        [SuppressMessage("Design", "CA1031", Justification = "Операция запущена без ожидания: непойманная ошибка потерялась бы, а окно осталось бы занятым.")]
        private async Task RunAsync(
            string message,
            Func<Task> operation)
        {
            if (this.isBusy)
            {
                return;
            }

            this.isBusy = true;

            this.SetStatus(
                message,
                MessageType.Info);

            this.Repaint();

            try
            {
                await operation().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                this.SetStatus(
                    exception.Message,
                    MessageType.Error);
            }
            finally
            {
                this.isBusy = false;

                if (this != null)
                {
                    this.Repaint();
                }
            }
        }

        private async Task LoadCommitsAsync(bool update)
        {
            var location = this.GetSelectedLocation();

            if (location == null)
            {
                this.commits = new List<CommitInfo>();

                this.SetStatus(
                    string.Empty,
                    MessageType.None);

                return;
            }

            var history = CreateHistory(location);
            var updateError = string.Empty;

            if (update
             || !history.HasCache)
            {
                var result = await history.UpdateAsync().ConfigureAwait(true);

                updateError =
                    result.IsSuccess
                        ? string.Empty
                        : result.Error;

                this.hasFetched = true;
            }

            var (loadedCommits, logError) = await history.GetCommitsAsync().ConfigureAwait(true);

            this.commits = loadedCommits;

            var error =
                string.IsNullOrEmpty(updateError)
                    ? logError
                    : updateError;

            var type =
                string.IsNullOrEmpty(error)
                    ? MessageType.None
                    : MessageType.Error;

            this.SetStatus(
                error,
                type);
        }

        private async Task ConnectRevisionAsync(
            GitLocation location,
            string revision)
        {
            if (!GitInput.IsRevision(revision))
            {
                this.SetStatus(
                    "Введите хеш коммита, тег или ветку без пробелов.",
                    MessageType.Error);

                return;
            }

            var hash = await CreateHistory(location).ResolveAsync(revision).ConfigureAwait(true);

            if (string.IsNullOrEmpty(hash))
            {
                this.SetStatus(
                    $"Коммит «{revision}» не найден. Нажмите «Обновить» и проверьте значение.",
                    MessageType.Error);

                return;
            }

            await this
                .ConnectCommitAsync(
                    location,
                    hash)
                .ConfigureAwait(true);
        }

        private async Task ConnectCommitAsync(
            GitLocation location,
            string hash)
        {
            var (_, error) = await PackageInstaller.InstallAsync($"{location.PackageAddress}#{hash}").ConfigureAwait(true);

            if (!string.IsNullOrEmpty(error))
            {
                this.SetStatus(
                    error,
                    MessageType.Error);

                return;
            }

            this.ReloadLocations();

            this.SetStatus(
                $"Подключён {Shorten(hash)}. Закоммитьте Packages/manifest.json и Packages/packages-lock.json.",
                MessageType.Info);
        }

        private async Task AddLocationAsync(string url)
        {
            if (!GitInput.IsPackageAddress(url))
            {
                this.SetStatus(
                    "Введите адрес репозитория: https://… или git@…, для пакета в подпапке добавьте ?path=/папка. Версию через # не указывайте.",
                    MessageType.Error);

                return;
            }

            var (packageName, error) = await PackageInstaller.InstallAsync(url).ConfigureAwait(true);

            if (!string.IsNullOrEmpty(error))
            {
                this.SetStatus(
                    error,
                    MessageType.Error);

                return;
            }

            this.newLocationUrl = string.Empty;
            this.selectedName = packageName;
            this.ReloadLocations();

            await this.LoadCommitsAsync(true).ConfigureAwait(true);
        }

        private void OnGUI()
        {
            var selected = this.GetSelectedLocation();

            using (new EditorGUI.DisabledScope(this.isBusy))
            {
                this.DrawToolbar(selected);

                if (selected != null)
                {
                    this.DrawRevisionInput(selected);
                }

                this.DrawNewLocationInput();
            }

            if (!string.IsNullOrEmpty(this.statusMessage))
            {
                EditorGUILayout.HelpBox(
                    this.statusMessage,
                    this.statusType);
            }

            if (selected == null)
            {
                EditorGUILayout.HelpBox(
                    "Git-пакетов com.paperfly.* в проекте нет. Добавьте локацию по адресу репозитория.",
                    MessageType.Info);

                return;
            }

            this.DrawCommitList(selected);
        }

        private void DrawToolbar(GitLocation selected)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var names =
                    this.locations
                        .Select(location => location.Name)
                        .ToArray();

                var selectedIndex =
                    EditorGUILayout.Popup(
                        Array.IndexOf(
                            names,
                            this.selectedName),
                        names,
                        EditorStyles.toolbarPopup,
                        GUILayout.Width(LocationPopupWidth));

                if (selectedIndex >= 0
                 && !string.Equals(
                        names[selectedIndex],
                        this.selectedName,
                        StringComparison.Ordinal))
                {
                    this.selectedName = names[selectedIndex];
                    this.StartLoadingCommits(true);
                }

                if (GUILayout.Button(
                    "Обновить",
                    EditorStyles.toolbarButton))
                {
                    this.StartLoadingCommits(true);
                }

                GUILayout.FlexibleSpace();

                if (selected != null)
                {
                    GUILayout.Label(
                        $"Сейчас: {Shorten(selected.CurrentHash)}",
                        EditorStyles.miniLabel);
                }
            }
        }

        private void DrawRevisionInput(GitLocation selected)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    "SHA или тег",
                    GUILayout.Width(InputLabelWidth));

                this.revisionInput = EditorGUILayout.TextField(this.revisionInput);

                if (GUILayout.Button(
                    "Подключить",
                    GUILayout.Width(ButtonWidth)))
                {
                    var revision = this.revisionInput.Trim();

                    _ = this.RunAsync(
                        $"Подключаю {revision}…",
                        () => this.ConnectRevisionAsync(
                            selected,
                            revision));
                }
            }
        }

        private void DrawNewLocationInput()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    "Новая локация",
                    GUILayout.Width(InputLabelWidth));

                this.newLocationUrl = EditorGUILayout.TextField(this.newLocationUrl);

                if (GUILayout.Button(
                    "Добавить",
                    GUILayout.Width(ButtonWidth)))
                {
                    var url = this.newLocationUrl.Trim();

                    _ = this.RunAsync(
                        "Добавляю локацию…",
                        () => this.AddLocationAsync(url));
                }
            }
        }

        // Рисует только строки в видимой области: коммитов могут быть тысячи.
        private void DrawCommitList(GitLocation selected)
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

            var viewRect =
                new Rect(
                    0f,
                    0f,
                    rowWidth,
                    this.commits.Count * RowHeight);

            this.scrollPosition =
                GUI.BeginScrollView(
                    listRect,
                    this.scrollPosition,
                    viewRect);

            var firstRow = Mathf.FloorToInt(this.scrollPosition.y / RowHeight);

            var lastRow =
                Mathf.Min(
                    this.commits.Count - 1,
                    Mathf.CeilToInt((this.scrollPosition.y + listRect.height) / RowHeight));

            for (var index = firstRow; index <= lastRow; index++)
            {
                var rowRect =
                    new Rect(
                        0f,
                        index * RowHeight,
                        rowWidth,
                        RowHeight);

                this.DrawCommit(
                    rowRect,
                    this.commits[index],
                    selected);
            }

            GUI.EndScrollView();
        }

        private void DrawCommit(
            Rect row,
            CommitInfo commit,
            GitLocation selected)
        {
            var isCurrent =
                string.Equals(
                    commit.Hash,
                    selected.CurrentHash,
                    StringComparison.OrdinalIgnoreCase);

            if (isCurrent)
            {
                EditorGUI.DrawRect(
                    row,
                    CurrentRowColor);
            }

            var buttonRect =
                new Rect(
                    row.xMax - ButtonWidth - CellPadding,
                    row.y + 1f,
                    ButtonWidth,
                    row.height - 2f);

            row.x += CellPadding;
            row.width -= ButtonWidth + (3f * CellPadding);

            DrawCommitCells(
                row,
                commit);

            if (isCurrent)
            {
                GUI.Label(
                    buttonRect,
                    "текущий",
                    EditorStyles.centeredGreyMiniLabel);

                return;
            }

            using (new EditorGUI.DisabledScope(this.isBusy))
            {
                if (GUI.Button(
                    buttonRect,
                    "Подключить"))
                {
                    _ = this.RunAsync(
                        $"Подключаю {commit.ShortHash}…",
                        () => this.ConnectCommitAsync(
                            selected,
                            commit.Hash));
                }
            }
        }
    }
}
