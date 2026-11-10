using System;
using System.IO;

using UnityEditor;

using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Ставит git-хуки проверки при каждом открытии проекта. Какой хук нужен, определяется по тому, где лежит пакет локации.
    /// </summary>
    /// <remarks>
    /// <para>Пакет лежит в Assets исходниками — это проект художников: хук pre-push запускает проверку перед отправкой.</para>
    /// <para>Пакет подключён через Packages/manifest.json и в нём есть манифест выпуска — это проект игры:
    /// хук pre-commit запускает проверку приёма, когда в коммит попадает новая версия пакета.</para>
    /// <para>Git не хранит хуки в репозитории, поэтому без установщика каждому пришлось бы ставить их вручную.</para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class HookInstaller
    {
        private const string PushMarker = "# PaperFly Render Kit: проверка перед отправкой";

        private const string CommitMarker = "# PaperFly Render Kit: проверка приёма локаций";

        private const string ScriptRelativePath = "Tools~/release-gate.ps1";

        private const string Notice = "# Файл создаётся Render Kit при открытии проекта, правки будут перезаписаны.\n";

        // Хук LFS отправляет модели и текстуры на сервер, поэтому его вызов сохраняется после проверки.
        private const string PushTemplate =
            "#!/bin/sh\n"
          + PushMarker + "\n"
          + Notice
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

        // Версия пакета меняется только через манифест и lock-файл, остальные коммиты проверка не задерживает.
        private const string CommitTemplate =
            "#!/bin/sh\n"
          + CommitMarker + "\n"
          + Notice
          + "\n"
          + "changed=$(git diff --cached --name-only -- Packages/manifest.json Packages/packages-lock.json)\n"
          + "\n"
          + "if [ -n \"$changed\" ]\n"
          + "then\n"
          + "  powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{SCRIPT}\" -Mode accept || exit 1\n"
          + "fi\n";

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

            if (!Directory.Exists(hooksFolder))
            {
                return;
            }

            if (ProjectRole.IsLocationProject)
            {
                InstallHook(
                    hooksFolder,
                    "pre-push",
                    PushTemplate,
                    PushMarker);
            }

            if (ProjectRole.IsGameProject)
            {
                InstallHook(
                    hooksFolder,
                    "pre-commit",
                    CommitTemplate,
                    CommitMarker);
            }
        }

        private static void InstallHook(
            string hooksFolder,
            string hookName,
            string template,
            string marker)
        {
            var hookPath =
                Path.Combine(
                    hooksFolder,
                    hookName);

            var content =
                template.Replace(
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

            KeepForeignHook(
                hookPath,
                marker);

            File.WriteAllText(
                hookPath,
                content);

            Debug.Log($"Render Kit: установлен хук {hookName} ({hookPath}).");
        }

        private static void KeepForeignHook(
            string hookPath,
            string marker)
        {
            if (!File.Exists(hookPath)
             || File.ReadAllText(hookPath).Contains(marker))
            {
                return;
            }

            File.Copy(
                hookPath,
                hookPath + ".backup",
                true);
        }

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
