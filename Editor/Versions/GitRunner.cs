using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Запускает git в фоне, не блокируя редактор.
    /// </summary>
    internal static class GitRunner
    {
        /// <summary>
        /// Запускает git с указанными аргументами.
        /// </summary>
        /// <param name="workingDirectory">Папка, в которой выполняется команда.</param>
        /// <param name="arguments">Аргументы командной строки.</param>
        /// <returns>Результат команды.</returns>
        public static Task<GitResult> RunAsync(
            string workingDirectory,
            string arguments)
            => Task.Run(() => Run(
                workingDirectory,
                arguments));

        private static GitResult Run(
            string workingDirectory,
            string arguments)
        {
            var startInfo =
                CreateStartInfo(
                    workingDirectory,
                    arguments);

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.StandardInput.Close();

                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();

                    if (!process.WaitForExit(Constants.GitTimeoutMilliseconds))
                    {
                        process.Kill();

                        return new GitResult(
                            false,
                            string.Empty,
                            "Git не ответил вовремя. Проверьте сеть и доступ к репозиторию.");
                    }

                    return new GitResult(
                        process.ExitCode == 0,
                        output.Result,
                        error.Result.Trim());
                }
            }
            catch (Win32Exception exception)
            {
                return new GitResult(
                    false,
                    string.Empty,
                    $"Не удалось запустить git: {exception.Message}");
            }
        }

        // Окно работает без консоли, поэтому запросы пароля отключены: иначе git ждал бы ответа до таймаута.
        private static ProcessStartInfo CreateStartInfo(
            string workingDirectory,
            string arguments)
        {
            var startInfo =
                new ProcessStartInfo(
                    "git",
                    arguments)
                {
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.EnvironmentVariables["GCM_INTERACTIVE"] = "never";

            if (!startInfo.EnvironmentVariables.ContainsKey("GIT_SSH_COMMAND"))
            {
                startInfo.EnvironmentVariables["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";
            }

            return startInfo;
        }
    }
}
