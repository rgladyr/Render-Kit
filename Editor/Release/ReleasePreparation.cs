using System.Collections.Generic;
using System.Text;

using PaperFly.RenderKit.Validation;

using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Готовит выпуск версии локации: проверяет пакет, показывает изменения основы и записывает манифест.
    /// </summary>
    internal static class ReleasePreparation
    {
        /// <summary>
        /// Пункт меню подготовки выпуска.
        /// </summary>
        public const string MenuPath = "PaperFly/Выпуск/Подготовить выпуск";

        private const string LogPrefix = "[Подготовка выпуска]";

        private const string DialogTitle = "Подготовка выпуска";

        private static bool ShowErrors(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return false;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"{LogPrefix} {error}");
            }

            ValidationWindow.ShowWithoutFocus();

            EditorUtility.DisplayDialog(
                DialogTitle,
                $"Найдено ошибок: {errors.Count}. Манифест не записан. Список ошибок — в консоли и окне отчёта.",
                "Понятно");

            return true;
        }

        private static string Prepare(LocationPackage package)
        {
            var previous = ManifestBuilder.Load(package.Folder);
            var current = package.BuildManifest();

            var changes =
                previous == null
                    ? new List<string>()
                    : ManifestComparer.DescribeObjectChanges(
                        previous,
                        current);

            foreach (var change in changes)
            {
                Debug.LogWarning($"{LogPrefix} {package.Folder}: {change}");
            }

            ManifestBuilder.Save(
                package.Folder,
                current);

            var summary = new StringBuilder($"{package.Name} {package.Version}: ");

            summary.Append(
                previous == null
                    ? "первый манифест."
                    : $"изменений, которые затронут логику в игре: {changes.Count}.");

            return summary.ToString();
        }

        [MenuItem(MenuPath)]
        private static void PrepareAll()
        {
            var errors = ReleaseChecks.Run(false);

            if (ShowErrors(errors))
            {
                return;
            }

            var summary = new StringBuilder();

            foreach (var package in ReleaseChecks.FindPackages(errors))
            {
                summary.AppendLine(Prepare(package));
            }

            summary.AppendLine();
            summary.Append("Манифест записан в папку пакета. Удалённые и переименованные объекты — в консоли. Закоммитьте манифест, поставьте тег версии и отправьте.");

            EditorUtility.DisplayDialog(
                DialogTitle,
                summary.ToString(),
                "Понятно");
        }
    }
}
