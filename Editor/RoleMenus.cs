using System.Reflection;

using PaperFly.RenderKit.Acceptance;
using PaperFly.RenderKit.Release;

using UnityEditor;

using ValidationConstants = PaperFly.RenderKit.Validation.Constants;
using VersionsConstants = PaperFly.RenderKit.Versions.Constants;

namespace PaperFly.RenderKit
{
    /// <summary>
    /// Оставляет в меню PaperFly только пункты, нужные проекту этой роли: художникам — проверку и выпуск, игре — приём и версии.
    /// </summary>
    /// <remarks>
    /// Публичного способа скрыть пункт меню у Unity нет, поэтому лишние пункты убираются внутренним Menu.RemoveMenuItem.
    /// Если в новой версии Unity его не окажется, пункты останутся, но будут неактивны благодаря проверкам ниже.
    /// </remarks>
    [InitializeOnLoad]
    internal static class RoleMenus
    {
        private const string RemoveMenuItemName = "RemoveMenuItem";

        private static readonly string[] LocationItems =
        {
            ValidationConstants.Menu.ValidateLocations,
            ValidationConstants.Menu.Materials,
            ReleasePreparation.MenuPath,
        };

        private static readonly string[] GameItems =
        {
            AcceptanceChecks.MenuPath,
            VersionsConstants.MenuPath,
        };

        static RoleMenus()
        {
            // Меню собирается после загрузки скриптов, поэтому пункты убираются на следующем кадре.
            EditorApplication.delayCall += RemoveForeignItems;
        }

        private static void RemoveForeignItems()
        {
            var removeMenuItem =
                typeof(Menu).GetMethod(
                    RemoveMenuItemName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

            if (removeMenuItem == null)
            {
                return;
            }

            if (!ProjectRole.HasLocationTools)
            {
                Remove(
                    removeMenuItem,
                    LocationItems);
            }

            if (!ProjectRole.HasGameTools)
            {
                Remove(
                    removeMenuItem,
                    GameItems);
            }
        }

        private static void Remove(
            MethodInfo removeMenuItem,
            string[] items)
        {
            foreach (var item in items)
            {
                removeMenuItem.Invoke(
                    null,
                    new object[] { item });
            }
        }

        [MenuItem(ValidationConstants.Menu.ValidateLocations, true)]
        private static bool CanValidateLocations()
            => ProjectRole.HasLocationTools;

        [MenuItem(ValidationConstants.Menu.Materials, true)]
        private static bool CanBindMaterials()
            => ProjectRole.HasLocationTools;

        [MenuItem(ReleasePreparation.MenuPath, true)]
        private static bool CanPrepareRelease()
            => ProjectRole.HasLocationTools;

        [MenuItem(AcceptanceChecks.MenuPath, true)]
        private static bool CanAccept()
            => ProjectRole.HasGameTools;

        [MenuItem(VersionsConstants.MenuPath, true)]
        private static bool CanSwitchVersions()
            => ProjectRole.HasGameTools;
    }
}
