using System.Linq;

using PaperFly.RenderKit.Acceptance.Conflicts;

using UnityEditor;
using UnityEditor.PackageManager;

using UnityEngine;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Открывает окно приёма, когда в проекте появляется непринятая версия локации.
    /// </summary>
    /// <remarks>
    /// Отдельный класс, а не статический конструктор окна: Unity восстанавливает открытое окно при загрузке
    /// и вызывает статический конструктор при создании ScriptableObject, где обращаться к Application нельзя.
    /// </remarks>
    [InitializeOnLoad]
    internal static class AcceptanceTrigger
    {
        private const string StartupCheckKey = "PaperFly.RenderKit.Acceptance.StartupChecked";

        static AcceptanceTrigger()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            Events.registeredPackages += OnPackagesRegistered;

            // Версию могли подтянуть вне Unity (git pull), поэтому при открытии проекта непринятые версии тоже показываются.
            if (!SessionState.GetBool(
                    StartupCheckKey,
                    false))
            {
                SessionState.SetBool(
                    StartupCheckKey,
                    true);

                EditorApplication.delayCall += OpenIfNotAccepted;
            }
        }

        private static void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            if (args.added.Concat(args.changedTo).Any())
            {
                EditorApplication.delayCall -= OpenIfNotAccepted;
                EditorApplication.delayCall += OpenIfNotAccepted;
            }
        }

        private static void OpenIfNotAccepted()
        {
            // Копии файлов локации в игре тоже показываются в окне приёма: это ошибка даже у принятой версии.
            if (InstalledLocation.FindAll().Any(location => !AcceptedManifests.IsAccepted(location))
             || GuidConflicts.Find().Count > 0)
            {
                AcceptanceWindow.Open();
            }
        }
    }
}
