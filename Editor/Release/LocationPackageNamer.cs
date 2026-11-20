using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using PaperFly.RenderKit.Validation;
using PaperFly.RenderKit.Versions;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// В новом проекте из шаблона задаёт имя пакета локации по имени репозитория и выдаёт файлам шаблона новые GUID,
    /// чтобы художнику не нужно было делать это вручную.
    /// </summary>
    /// <remarks>
    /// Имя меняется только пока в package.json стоит имя из шаблона: заданное вручную не трогается.
    /// PaperFly.Location.Beach → com.paperfly.beach.
    /// </remarks>
    [InitializeOnLoad]
    internal static class LocationPackageNamer
    {
        private const string LogPrefix = "[Render Kit]";

        /// <summary>
        /// Имя пакета в шаблоне локации.
        /// </summary>
        public const string TemplateName = "com.paperfly.newlocation";

        private const string NamePrefix = "com.paperfly.";

        // Сам репозиторий шаблона: его имя пакета менять нельзя.
        private const string TemplateRepository = "locationtemplate";

        private static readonly string[] RepositoryPrefixes = { "paperfly.ap.location.", "paperfly.location.", "paperfly." };

        private static readonly Regex NamePattern =
            new Regex(
                "\"name\"\\s*:\\s*\"(?<name>[^\"]*)\"",
                RegexOptions.Compiled);

        private static readonly Regex DisplayNamePattern =
            new Regex(
                "\"displayName\"\\s*:\\s*\"[^\"]*\"",
                RegexOptions.Compiled);

        static LocationPackageNamer()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += NameIfTemplate;
        }

        // «https://github.com/org/PaperFly.Location.Beach.git» → «Beach».
        private static string GetRepositoryName(string url)
        {
            var name =
                url
                    .Trim()
                    .TrimEnd('/')
                    .Split(
                        '/',
                        ':')
                    .Last();

            return name.EndsWith(
                    ".git",
                    StringComparison.OrdinalIgnoreCase)
                ? name.Substring(
                    0,
                    name.Length - ".git".Length)
                : name;
        }

        // Unity принимает в имени пакета только строчные латинские буквы, цифры, точки, дефисы и подчёркивания.
        private static string ToPackageSuffix(string repositoryName)
        {
            var name = repositoryName.ToLowerInvariant();

            var prefix =
                RepositoryPrefixes.FirstOrDefault(candidate => name.StartsWith(
                    candidate,
                    StringComparison.Ordinal));

            if (prefix != null)
            {
                name = name.Substring(prefix.Length);
            }

            return Regex
                .Replace(
                    name,
                    "[^a-z0-9._-]",
                    string.Empty)
                .Trim('.');
        }

        private static async void NameIfTemplate()
        {
            if (!ProjectRole.IsLocationProject)
            {
                return;
            }

            var packageFiles = LocationAssets.FindSourcePackageFiles();

            var packageFile =
                packageFiles.FirstOrDefault(path => string.Equals(
                    NamePattern.Match(File.ReadAllText(path)).Groups["name"].Value,
                    TemplateName,
                    StringComparison.Ordinal));

            var templateMetas =
                packageFiles
                    .SelectMany(path => TemplateGuids.Find(Path.GetDirectoryName(path)))
                    .ToList();

            if (packageFile == null
             && templateMetas.Count == 0)
            {
                return;
            }

            var result =
                await GitRunner.RunAsync(
                    Environment.CurrentDirectory,
                    "remote get-url origin");

            if (!result.IsSuccess)
            {
                return;
            }

            var repositoryName = GetRepositoryName(result.Output);
            var suffix = ToPackageSuffix(repositoryName);

            if (suffix.Length == 0
             || string.Equals(
                    suffix,
                    TemplateRepository,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (templateMetas.Count > 0)
            {
                var count = TemplateGuids.Regenerate(templateMetas);

                Debug.Log($"{LogPrefix} Файлы шаблона получили новые GUID ({count}): иначе в игре они конфликтовали бы с файлами других локаций. Закоммитьте изменённые .meta.");
            }

            if (packageFile != null)
            {
                Rename(
                    packageFile,
                    suffix,
                    repositoryName);
            }

            AssetDatabase.Refresh();
        }

        private static void Rename(
            string packageFile,
            string suffix,
            string repositoryName)
        {
            var packageName = NamePrefix + suffix;
            var text = File.ReadAllText(packageFile);

            text =
                NamePattern.Replace(
                    text,
                    $"\"name\": \"{packageName}\"",
                    1);

            text =
                DisplayNamePattern.Replace(
                    text,
                    $"\"displayName\": \"PaperFly {repositoryName}\"",
                    1);

            File.WriteAllText(
                packageFile,
                text);

            Debug.Log($"{LogPrefix} Имя пакета локации задано по имени репозитория: {packageName}. Его можно поменять в {packageFile}. Закоммитьте изменение.");
        }
    }
}
