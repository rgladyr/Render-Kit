using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Заполняет README.md репозитория локации: заголовок и готовую строку подключения пакета в игру.
    /// </summary>
    /// <remarks>
    /// Разработчику не нужно собирать адрес вручную: строку из README достаточно скопировать в Packages/manifest.json игры.
    /// Заменяется только текст между метками paperfly:game-package и заголовок шаблона, остальное не трогается.
    /// </remarks>
    internal static class LocationReadme
    {
        private const string FileName = "README.md";

        private const string TemplateTitle = "# Шаблон локации PaperFly";

        private const string GitSuffix = ".git";

        private const string HttpsPrefix = "https://";

        private static readonly Regex BlockPattern =
            new Regex(
                @"(?<start><!-- paperfly:game-package -->\r?\n)(?<body>.*?)(?<end>\r?\n<!-- /paperfly:game-package -->)",
                RegexOptions.Compiled | RegexOptions.Singleline);

        // git@github.com:org/repo.git, ssh://git@github.com/org/repo.git, https://user@github.com/org/repo.git
        private static readonly Regex RemotePattern =
            new Regex(
                @"^(?:ssh://)?(?:[^@/]+@)?(?<host>[^:/]+)[:/](?<path>.+)$",
                RegexOptions.Compiled);

        /// <summary>
        /// Приводит адрес репозитория к https: так локации подключены в игре, даже если художник клонировал по ssh.
        /// </summary>
        /// <param name="remoteUrl">Адрес из git remote.</param>
        /// <returns>Адрес вида https://github.com/org/repo.git.</returns>
        public static string ToHttpsUrl(string remoteUrl)
        {
            var url = remoteUrl.Trim();

            if (url.StartsWith(
                    HttpsPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(HttpsPrefix.Length);
            }

            var match = RemotePattern.Match(url);

            var https =
                match.Success
                    ? $"{HttpsPrefix}{match.Groups["host"].Value}/{match.Groups["path"].Value.TrimStart('/')}"
                    : HttpsPrefix + url;

            return https.EndsWith(
                    GitSuffix,
                    StringComparison.OrdinalIgnoreCase)
                ? https
                : https + GitSuffix;
        }

        /// <summary>
        /// Записывает в README.md строку подключения и заголовок, если они не совпадают с текущими.
        /// </summary>
        /// <param name="projectFolder">Корень репозитория.</param>
        /// <param name="remoteUrl">Адрес из git remote.</param>
        /// <param name="packageName">Имя пакета из package.json.</param>
        /// <param name="packagePath">Путь к папке пакета в репозитории, например /Assets/Location.</param>
        /// <param name="repositoryName">Имя репозитория для заголовка.</param>
        /// <returns><c>true</c>, если файл изменён.</returns>
        public static bool Update(
            string projectFolder,
            string remoteUrl,
            string packageName,
            string packagePath,
            string repositoryName)
        {
            var path =
                Path.Combine(
                    projectFolder,
                    FileName);

            if (!File.Exists(path))
            {
                return false;
            }

            var text = File.ReadAllText(path);
            var newLine =
                text.Contains("\r\n")
                    ? "\r\n"
                    : "\n";

            var line = $"\"{packageName}\": \"{ToHttpsUrl(remoteUrl)}?path={packagePath}\"";
            var body = $"```json{newLine}{line}{newLine}```";

            var updated =
                BlockPattern.Replace(
                    text,
                    match => match.Groups["start"].Value + body + match.Groups["end"].Value,
                    1);

            if (updated.StartsWith(
                    TemplateTitle,
                    StringComparison.Ordinal))
            {
                updated =
                    $"# Локация {repositoryName}"
                  + updated.Substring(TemplateTitle.Length);
            }

            if (string.Equals(
                    text,
                    updated,
                    StringComparison.Ordinal))
            {
                return false;
            }

            File.WriteAllText(
                path,
                updated);

            return true;
        }
    }
}
