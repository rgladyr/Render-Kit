using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Копия истории репозитория без содержимого файлов: коммиты занимают килобайты, а LFS и текстуры не скачиваются.
    /// </summary>
    internal sealed class RepositoryHistory
    {
        private readonly string cacheRoot;

        private readonly string directory;

        private readonly string repositoryUrl;

        /// <summary>
        /// Создаёт историю репозитория.
        /// </summary>
        /// <param name="cacheRoot">Папка, в которой хранятся копии истории.</param>
        /// <param name="packageName">Имя пакета, оно же имя папки копии.</param>
        /// <param name="repositoryUrl">Адрес репозитория.</param>
        public RepositoryHistory(
            string cacheRoot,
            string packageName,
            string repositoryUrl)
        {
            this.cacheRoot = Path.GetFullPath(cacheRoot);
            this.repositoryUrl = repositoryUrl;

            this.directory =
                Path.Combine(
                    this.cacheRoot,
                    $"{packageName}.git");
        }

        /// <summary>
        /// Копия истории уже скачана.
        /// </summary>
        public bool HasCache => Directory.Exists(this.directory);

        /// <summary>
        /// Скачивает новые коммиты, а при первом обращении создаёт копию истории.
        /// </summary>
        /// <returns>Результат git.</returns>
        public async Task<GitResult> UpdateAsync()
        {
            if (!GitInput.IsRepositoryUrl(this.repositoryUrl))
            {
                return new GitResult(
                    false,
                    string.Empty,
                    $"Адрес не поддерживается: {this.repositoryUrl}");
            }

            return this.HasCache
                ? await this.FetchAsync().ConfigureAwait(false)
                : await this.CloneAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Читает все коммиты из копии истории, новые первыми.
        /// </summary>
        /// <returns>Коммиты и текст ошибки, пустой при успехе.</returns>
        public async Task<(List<CommitInfo> Commits, string Error)> GetCommitsAsync()
        {
            if (!this.HasCache)
            {
                return (new List<CommitInfo>(), string.Empty);
            }

            var result =
                await GitRunner
                    .RunAsync(
                        this.directory,
                        $"log --all --format=\"{CommitInfo.LogFormat}\"")
                    .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return (new List<CommitInfo>(), result.Error);
            }

            var commits =
                result.Output
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(CommitInfo.Parse)
                    .ToList();

            return (commits, string.Empty);
        }

        /// <summary>
        /// Превращает короткий хеш, тег или ветку в полный хеш коммита.
        /// </summary>
        /// <param name="revision">Значение, прошедшее <see cref="GitInput.IsRevision"/>.</param>
        /// <returns>Полный хеш или пустая строка, если такого коммита нет.</returns>
        public async Task<string> ResolveAsync(string revision)
        {
            if (!this.HasCache)
            {
                return string.Empty;
            }

            var result =
                await GitRunner
                    .RunAsync(
                        this.directory,
                        $"rev-parse --verify --quiet \"{revision}^{{commit}}\"")
                    .ConfigureAwait(false);

            return result.IsSuccess
                ? result.Output.Trim()
                : string.Empty;
        }

        private Task<GitResult> CloneAsync()
        {
            Directory.CreateDirectory(this.cacheRoot);

            return GitRunner.RunAsync(
                this.cacheRoot,
                $"clone --quiet --bare --filter=blob:none \"{this.repositoryUrl}\" \"{this.directory}\"");
        }

        // В копии --bare у origin нет своих правил получения веток, поэтому они указаны явно.
        private async Task<GitResult> FetchAsync()
        {
            var setUrl =
                await GitRunner
                    .RunAsync(
                        this.directory,
                        $"remote set-url origin \"{this.repositoryUrl}\"")
                    .ConfigureAwait(false);

            if (!setUrl.IsSuccess)
            {
                return setUrl;
            }

            return await GitRunner
                .RunAsync(
                    this.directory,
                    "fetch --quiet --prune origin \"+refs/heads/*:refs/heads/*\" \"+refs/tags/*:refs/tags/*\"")
                .ConfigureAwait(false);
        }
    }
}
