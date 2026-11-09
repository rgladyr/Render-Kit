using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Validation;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Выполняет проверку перед отправкой по запросу хука pre-push: в открытой Unity или при запуске из командной строки.
    /// </summary>
    [InitializeOnLoad]
    internal static class ReleaseGate
    {
        private const string RootPath = "Library/PaperFly/ReleaseGate";

        private const string RequestPath = RootPath + "/request.txt";

        private const string ResultPath = RootPath + "/result.txt";

        private const string LogPrefix = "[Проверка перед отправкой]";

        private const double PollIntervalSeconds = 0.5;

        private static double nextPollTime;

        static ReleaseGate()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPollTime)
            {
                return;
            }

            nextPollTime =
                EditorApplication.timeSinceStartup
              + PollIntervalSeconds;

            if (File.Exists(RequestPath))
            {
                HandleRequest();
            }
        }

        private static bool HandleRequest()
        {
            var requestId = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);

            var errors = ReleaseChecks.Run();

            WriteResult(
                requestId,
                errors);

            Report(errors);

            return errors.Count == 0;
        }

        private static void WriteResult(
            string requestId,
            IReadOnlyList<string> errors)
        {
            var lines = new List<string> { requestId, GetStatus(errors) };
            lines.AddRange(errors);

            // Хук ждёт файл результата, поэтому он должен появиться целиком: сначала пишется временный файл, потом переименовывается.
            var temporaryPath = ResultPath + ".tmp";

            File.WriteAllLines(
                temporaryPath,
                lines);

            File.Delete(ResultPath);

            File.Move(
                temporaryPath,
                ResultPath);
        }

        private static void Report(IReadOnlyList<string> errors)
        {
            var warnings = ValidationReport.GetAll().FindAll(issue => issue.Severity == ValidationSeverity.Warning);

            if (warnings.Count > 0)
            {
                var byRule =
                    warnings
                        .GroupBy(issue => issue.Rule)
                        .OrderByDescending(group => group.Count())
                        .Select(group => $"{group.Key}: {group.Count()}");

                Debug.LogWarning($"{LogPrefix} Предупреждений: {warnings.Count} ({string.Join("; ", byRule)}), отправку они не останавливают.");
            }

            if (errors.Count == 0)
            {
                Debug.Log($"{LogPrefix} Ошибок нет, отправка разрешена.");

                return;
            }

            Debug.LogError($"{LogPrefix} Отправка отменена, ошибок: {errors.Count}.");

            foreach (var error in errors)
            {
                Debug.LogError($"{LogPrefix} {error}");
            }

            if (!Application.isBatchMode)
            {
                ValidationWindow.ShowWithoutFocus();
            }
        }

        private static string GetStatus(IReadOnlyList<string> errors)
            => errors.Count == 0 ? "OK" : "FAIL";

        /// <summary>
        /// Точка входа для Unity, которую хук запускает из командной строки, когда проект не открыт.
        /// </summary>
        public static void RunFromCommandLine()
        {
            var passed = File.Exists(RequestPath) && HandleRequest();

            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
