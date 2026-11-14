using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PaperFly.RenderKit.Acceptance;
using PaperFly.RenderKit.Validation;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Выполняет проверку по запросу git-хука в открытой Unity или при запуске из командной строки:
    /// перед отправкой у художников (pre-push) и при коммите новой версии локации в игре (pre-commit).
    /// </summary>
    [InitializeOnLoad]
    internal static class ReleaseGate
    {
        private const string RootPath = "Library/PaperFly/ReleaseGate";

        private const string RequestPath = RootPath + "/request.txt";

        private const string ResultPath = RootPath + "/result.txt";

        private const string PushPrefix = "[Проверка перед отправкой]";

        private const string AcceptancePrefix = "[Проверка приёма локаций]";

        private const string CommitPrefix = "[Проверка коммита]";

        private const string ReleaseMode = "release";

        private const string CommitMode = "commit";

        private const string AcceptMode = "accept";

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
            // Первая строка запроса — его номер, вторая — режим: push, release при отправке тега версии,
            // commit при коммите у художника (дальше — файлы коммита) или accept при коммите новой версии локации в игре.
            var request = File.ReadAllLines(RequestPath);
            File.Delete(RequestPath);

            var requestId = request[0].Trim();

            var mode =
                request.Length > 1
                    ? request[1].Trim()
                    : string.Empty;

            List<string> errors;

            if (string.Equals(
                    mode,
                    AcceptMode,
                    StringComparison.Ordinal))
            {
                errors = AcceptanceChecks.RunForGate();
            }
            else if (string.Equals(
                         mode,
                         CommitMode,
                         StringComparison.Ordinal))
            {
                errors = ReleaseChecks.RunForCommit(request.Skip(2).Where(line => line.Trim().Length > 0));
            }
            else
            {
                errors =
                    ReleaseChecks.Run(string.Equals(
                        mode,
                        ReleaseMode,
                        StringComparison.Ordinal));
            }

            WriteResult(
                requestId,
                errors);

            Report(
                errors,
                mode);

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

        private static void Report(
            IReadOnlyList<string> errors,
            string mode)
        {
            var isAcceptance =
                string.Equals(
                    mode,
                    AcceptMode,
                    StringComparison.Ordinal);

            var isCommit =
                isAcceptance
             || string.Equals(
                    mode,
                    CommitMode,
                    StringComparison.Ordinal);

            var prefix =
                isAcceptance
                    ? AcceptancePrefix
                    : isCommit
                        ? CommitPrefix
                        : PushPrefix;

            var action =
                isCommit
                    ? "коммит"
                    : "отправку";

            var warnings = ValidationReport.GetAll().FindAll(issue => issue.Severity == ValidationSeverity.Warning);

            if (warnings.Count > 0)
            {
                var byRule =
                    warnings
                        .GroupBy(issue => issue.Rule)
                        .OrderByDescending(group => group.Count())
                        .Select(group => $"{group.Key}: {group.Count()}");

                Debug.LogWarning($"{prefix} Предупреждений: {warnings.Count} ({string.Join("; ", byRule)}), {action} они не останавливают.");
            }

            if (errors.Count == 0)
            {
                Debug.Log(
                    isCommit
                        ? $"{prefix} Ошибок нет, коммит разрешён."
                        : $"{prefix} Ошибок нет, отправка разрешена.");

                return;
            }

            Debug.LogError(
                isCommit
                    ? $"{prefix} Коммит отменён, ошибок: {errors.Count}."
                    : $"{prefix} Отправка отменена, ошибок: {errors.Count}.");

            foreach (var error in errors)
            {
                Debug.LogError($"{prefix} {error}");
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
