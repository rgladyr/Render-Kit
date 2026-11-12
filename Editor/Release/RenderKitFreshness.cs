using System;
using System.Threading.Tasks;

using PaperFly.RenderKit.Versions;

using UnityEditor;
using UnityEditor.PackageManager;

using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Держит Render Kit у художников на последнем коммите master: обновляет при открытии проекта
    /// и сообщает проверке перед отправкой, если версия устарела.
    /// </summary>
    /// <remarks>
    /// Работает только для Render Kit, подключённого git-пакетом. Сабмодуль в игре и локальный путь
    /// у разработчика Render Kit не трогаются: там версию выбирает сам разработчик.
    /// </remarks>
    [InitializeOnLoad]
    internal static class RenderKitFreshness
    {
        private const string LogPrefix = "[Render Kit]";

        private const string Branch = "refs/heads/master";

        private const int HashLength = 40;

        static RenderKitFreshness()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += UpdateIfStale;
        }

        private static PackageInfo GetGitPackage()
        {
            var package = PackageInfo.FindForAssembly(typeof(RenderKitFreshness).Assembly);

            return package != null
                && package.source == PackageSource.Git
                ? package
                : null;
        }

        // Вид packageId: com.paperfly.renderkit@https://github.com/org/repo.git#хеш
        private static string GetAddress(PackageInfo package)
            => package.packageId
                .Substring(package.name.Length + 1)
                .Split('#')[0];

        private static string GetRepositoryUrl(PackageInfo package)
            => GetAddress(package).Split('?')[0];

        // Продолжение не возвращается в главный поток Unity: проверка перед отправкой ждёт результат синхронно,
        // и возврат в заблокированный главный поток повесил бы редактор.
        private static async Task<string> GetRemoteHeadAsync(PackageInfo package)
        {
            var result =
                await GitRunner
                    .RunAsync(
                        Environment.CurrentDirectory,
                        $"ls-remote {GetRepositoryUrl(package)} {Branch}")
                    .ConfigureAwait(false);

            if (!result.IsSuccess
             || result.Output.Length < HashLength)
            {
                return null;
            }

            return result.Output.Substring(
                0,
                HashLength);
        }

        private static async void UpdateIfStale()
        {
            if (!ProjectRole.IsLocationProject)
            {
                return;
            }

            var package = GetGitPackage();

            if (package == null)
            {
                return;
            }

            var remote = await GetRemoteHeadAsync(package);

            if (remote == null
             || string.Equals(
                    remote,
                    package.git?.hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Debug.Log($"{LogPrefix} Вышла новая версия Render Kit ({remote.Substring(0, 7)}), обновляю. Изменённый Packages/packages-lock.json закоммитьте вместе со следующей работой.");

            // Адрес без хеша: Package Manager берёт последний коммит ветки по умолчанию и обновляет lock-файл.
            Client.Add(GetAddress(package));
        }

        /// <summary>
        /// Проверяет, что Render Kit в проекте совпадает с последним коммитом master на GitHub.
        /// </summary>
        /// <returns>Текст ошибки; <c>null</c>, если версия актуальна, Render Kit подключён не git-пакетом или GitHub недоступен.</returns>
        public static string FindStaleError()
        {
            var package = GetGitPackage();

            if (package == null)
            {
                return null;
            }

            var remote = GetRemoteHeadAsync(package).Result;

            if (remote == null
             || string.Equals(
                    remote,
                    package.git?.hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var installed = package.git?.hash ?? string.Empty;

            return $"Render Kit устарел: в проекте {installed.Substring(0, Math.Min(7, installed.Length))}, на GitHub {remote.Substring(0, 7)}. "
                 + "Откройте проект в Unity — Render Kit обновится сам, — проверьте локацию и повторите push.";
        }
    }
}
