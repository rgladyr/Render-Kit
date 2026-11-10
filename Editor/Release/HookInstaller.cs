using System;
using System.IO;

using UnityEditor;

using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Ставит хук pre-push в репозиторий проекта с пакетом локации при каждом открытии проекта.
    /// </summary>
    /// <remarks>
    /// Git не хранит хуки в репозитории, поэтому без установщика каждому художнику пришлось бы ставить хук вручную.
    /// </remarks>
    [InitializeOnLoad]
    internal static class HookInstaller
    {
        private const string Marker = "# PaperFly Render Kit: проверка перед отправкой";

        private const string ScriptRelativePath = "Tools~/release-gate.ps1";

        // Хук LFS отправляет модели и текстуры на сервер, поэтому его вызов сохраняется после проверки.
        private const string Template =
            "#!/bin/sh\n"
          + Marker + "\n"
          + "# Файл создаётся Render Kit при открытии проекта, правки будут перезаписаны.\n"
          + "\n"
          + "refs=$(cat)\n"
          + "\n"
          + "# Отправляемые ссылки, кроме удалений, в виде \"ссылка:объект\".\n"
          + "# Git передаёт строки \"<локальная ссылка> <объект> <удалённая ссылка> <объект>\".\n"
          + "pushed=$(printf '%s\\n' \"$refs\" | awk '$1 != \"(delete)\" && $2 !~ /^0+$/ { print $1 \":\" $2 }')\n"
          + "\n"
          + "if [ -n \"$pushed\" ]\n"
          + "then\n"
          + "  powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{SCRIPT}\" $pushed || exit 1\n"
          + "fi\n"
          + "\n"
          + "command -v git-lfs >/dev/null 2>&1 || { printf >&2 \"\\nНе найден git-lfs: модели и текстуры не будут отправлены.\\n\\n\"; exit 2; }\n"
          + "printf '%s\\n' \"$refs\" | git lfs pre-push \"$@\"\n";

        static HookInstaller()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += Install;
        }

        private static void Install()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var hooksFolder =
                Path.Combine(
                    projectRoot,
                    ".git",
                    "hooks");

            if (!Directory.Exists(hooksFolder)
             || !HasLocationPackage())
            {
                return;
            }

            var hookPath =
                Path.Combine(
                    hooksFolder,
                    "pre-push");

            var content =
                Template.Replace(
                    "{SCRIPT}",
                    GetScriptPath());

            if (File.Exists(hookPath)
             && string.Equals(
                    File.ReadAllText(hookPath),
                    content,
                    StringComparison.Ordinal))
            {
                return;
            }

            KeepForeignHook(hookPath);

            File.WriteAllText(
                hookPath,
                content);

            Debug.Log($"Render Kit: установлен хук проверки перед отправкой ({hookPath}).");
        }

        private static void KeepForeignHook(string hookPath)
        {
            if (!File.Exists(hookPath)
             || File.ReadAllText(hookPath).Contains(Marker))
            {
                return;
            }

            File.Copy(
                hookPath,
                hookPath + ".backup",
                true);
        }

        private static bool HasLocationPackage()
            => Directory
                .GetFiles(
                    "Assets",
                    "package.json",
                    SearchOption.AllDirectories)
                .Length > 0;

        private static string GetScriptPath()
        {
            var package = PackageInfo.FindForAssembly(typeof(HookInstaller).Assembly);

            return Path
                .Combine(
                    package.resolvedPath,
                    ScriptRelativePath)
                .Replace(
                    '\\',
                    '/');
        }
    }
}
