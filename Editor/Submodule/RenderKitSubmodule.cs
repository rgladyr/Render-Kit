using System;
using System.IO;
using System.Threading.Tasks;

using PaperFly.RenderKit.Versions;

using UnityEditor;
using UnityEditor.PackageManager;

using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Submodule
{
    /// <summary>
    /// Обслуживает Render Kit, подключённый к игре сабмодулем: настраивает Smart Merge и предупреждает о detached HEAD.
    /// </summary>
    /// <remarks>
    /// <para>Сабмодуль лежит в Packages, поэтому Unity берёт его как встроенный пакет, и его можно править прямо в игре.
    /// Коммит, pull, push и конфликты делаются в Fork, как в любом репозитории.</para>
    /// <para>После обновления сабмодуль стоит на коммите, а не на ветке. Коммит, сделанный в таком состоянии,
    /// не попадёт ни в одну ветку и потеряется, поэтому об этом предупреждаем сразу.</para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class RenderKitSubmodule
    {
        private const string LogPrefix = "[Render Kit]";

        private const string DriverName = "unityyamlmerge";

        private const string MergeToolRelativePath = "Tools/UnityYAMLMerge.exe";

        private const string GitEntry = ".git";

        static RenderKitSubmodule()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += Check;
        }

        private static string FindSubmoduleFolder()
        {
            var package = PackageInfo.FindForAssembly(typeof(RenderKitSubmodule).Assembly);

            // У сабмодуля .git — файл со ссылкой на репозиторий в .git/modules игры, у обычного клона — папка.
            return package != null
                && package.source == PackageSource.Embedded
                && (File.Exists(Path.Combine(
                        package.resolvedPath,
                        GitEntry))
                 || Directory.Exists(Path.Combine(
                        package.resolvedPath,
                        GitEntry)))
                ? package.resolvedPath
                : null;
        }

        private static async void Check()
        {
            var folder = FindSubmoduleFolder();

            if (folder == null)
            {
                return;
            }

            await ConfigureMergeDriver(folder);
            await WarnIfDetached(folder);
        }

        private static async Task ConfigureMergeDriver(string folder)
        {
            var mergeTool =
                Path
                    .Combine(
                        EditorApplication.applicationContentsPath,
                        MergeToolRelativePath)
                    .Replace(
                        '\\',
                        '/');

            if (!File.Exists(mergeTool))
            {
                Debug.LogWarning($"{LogPrefix} Не найден {mergeTool}: конфликты в YAML Render Kit будут сливаться как обычный текст.");

                return;
            }

            var driver = $"\\\"{mergeTool}\\\" merge -h -p --force %O %B %A %A";

            var current =
                await GitRunner.RunAsync(
                    folder,
                    $"config --get merge.{DriverName}.driver");

            if (current.IsSuccess
             && string.Equals(
                    current.Output.Trim(),
                    driver.Replace(
                        "\\\"",
                        "\""),
                    StringComparison.Ordinal))
            {
                return;
            }

            await GitRunner.RunAsync(
                folder,
                $"config merge.{DriverName}.name \"Unity Smart Merge\"");

            var result =
                await GitRunner.RunAsync(
                    folder,
                    $"config merge.{DriverName}.driver \"{driver}\"");

            if (result.IsSuccess)
            {
                Debug.Log($"{LogPrefix} Для сабмодуля настроен Smart Merge: {mergeTool}.");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} Не удалось настроить Smart Merge: {result.Error}");
            }
        }

        private static async Task WarnIfDetached(string folder)
        {
            var branch =
                await GitRunner.RunAsync(
                    folder,
                    "symbolic-ref -q --short HEAD");

            if (!branch.IsSuccess)
            {
                Debug.LogWarning($"{LogPrefix} Сабмодуль Render Kit стоит на коммите, а не на ветке. Перед правкой переключитесь в нём на master в Fork, иначе ваш коммит не попадёт ни в одну ветку.");
            }
        }
    }
}
