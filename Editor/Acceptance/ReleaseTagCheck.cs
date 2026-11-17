using System;
using System.Collections.Generic;
using System.Linq;

using PaperFly.RenderKit.Versions;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Проверяет на GitHub, что на подключённом коммите локации стоит тег её версии, то есть это выпуск.
    /// </summary>
    /// <remarks>
    /// Манифест выпуска остаётся в репозитории и после выпуска, поэтому по нему одному выпуск не отличить
    /// от любого следующего коммита. Тег художник ставит только на выпуск.
    /// </remarks>
    internal static class ReleaseTagCheck
    {
        private const string TagPrefix = "refs/tags/";

        private const string PeeledSuffix = "^{}";

        // Тег, однажды найденный на коммите, не пропадает, поэтому повторно сеть не трогается.
        private static readonly HashSet<string> TaggedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool IsTagged(
            string output,
            string version,
            string hash)
            => output
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('\t'))
                .Where(parts => parts.Length == 2)
                .Any(parts => string.Equals(
                                  parts[0].Trim(),
                                  hash,
                                  StringComparison.OrdinalIgnoreCase)
                           && string.Equals(
                                  parts[1]
                                      .Trim()
                                      .Replace(
                                          PeeledSuffix,
                                          string.Empty),
                                  TagPrefix + version,
                                  StringComparison.Ordinal));

        /// <summary>
        /// Проверяет, стоит ли на подключённом коммите тег версии из манифеста.
        /// </summary>
        /// <param name="location">Подключённый пакет.</param>
        /// <param name="version">Версия из манифеста выпуска.</param>
        /// <param name="error">Почему проверить не удалось; пустая строка, если удалось.</param>
        /// <returns><c>true</c> — тег есть, <c>false</c> — нет, <c>null</c> — проверить не удалось.</returns>
        public static bool? Check(
            InstalledLocation location,
            string version,
            out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(location.RepositoryUrl)
             || string.IsNullOrEmpty(location.CommitHash))
            {
                error = "пакет подключён не по git";

                return null;
            }

            var key = $"{location.RepositoryUrl}#{location.CommitHash}#{version}";

            if (TaggedCache.Contains(key))
            {
                return true;
            }

            var result =
                GitRunner
                    .RunAsync(
                        Environment.CurrentDirectory,
                        $"ls-remote --tags {location.RepositoryUrl}")
                    .Result;

            if (!result.IsSuccess)
            {
                error = result.Error;

                return null;
            }

            var isTagged =
                IsTagged(
                    result.Output,
                    version,
                    location.CommitHash);

            if (isTagged)
            {
                TaggedCache.Add(key);
            }

            return isTagged;
        }
    }
}
