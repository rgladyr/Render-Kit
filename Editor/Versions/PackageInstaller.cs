using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.PackageManager;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Подключает git-пакет через Package Manager, который сам обновляет манифест и скачивает файлы.
    /// </summary>
    internal static class PackageInstaller
    {
        /// <summary>
        /// Подключает пакет или переключает уже подключённый на другую версию.
        /// </summary>
        /// <param name="identifier">Адрес репозитория, при необходимости с #хешем.</param>
        /// <returns>Имя пакета при успехе и текст ошибки при неудаче.</returns>
        public static Task<(string PackageName, string Error)> InstallAsync(string identifier)
        {
            var completion = new TaskCompletionSource<(string PackageName, string Error)>();
            var request = Client.Add(identifier);

            void Poll()
            {
                if (!request.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= Poll;

                completion.SetResult(
                    request.Status == StatusCode.Success
                        ? (request.Result.name, string.Empty)
                        : (string.Empty, request.Error.message));
            }

            EditorApplication.update += Poll;

            return completion.Task;
        }
    }
}
