using PaperFly.RenderKit.Acceptance;
using PaperFly.RenderKit.Validation;

namespace PaperFly.RenderKit
{
    /// <summary>
    /// Определяет, чей это проект: художников (локация) или игры. От этого зависят хуки и пункты меню.
    /// </summary>
    /// <remarks>
    /// <para>Проект локации: в Assets лежит пакет локации исходниками — папка с package.json, помеченным
    /// ключевым словом paperfly-location, или с манифестом выпуска.</para>
    /// <para>Проект игры: через Packages/manifest.json подключён пакет локации (с тем же ключевым словом,
    /// манифестом выпуска или папкой Locations).</para>
    /// <para>Если не подходит ни то ни другое, например при работе над самим Render Kit, доступно всё.</para>
    /// Результат запоминается до перезагрузки скриптов: смена пакетов всё равно её вызывает.
    /// </remarks>
    internal static class ProjectRole
    {
        private static bool? isLocationProject;

        private static bool? isGameProject;

        /// <summary>
        /// Проект художников: в Assets есть пакет локации.
        /// </summary>
        public static bool IsLocationProject
            => isLocationProject ??= LocationAssets.FindSourcePackageFiles().Count > 0;

        /// <summary>
        /// Проект игры: подключён пакет локации.
        /// </summary>
        public static bool IsGameProject
            => isGameProject ??= InstalledLocation.FindAll().Count > 0;

        /// <summary>
        /// Нужны ли инструменты художника: проверка, материалы, подготовка выпуска.
        /// </summary>
        public static bool HasLocationTools
            => IsLocationProject
            || !IsGameProject;

        /// <summary>
        /// Нужны ли инструменты игры: приём и версии локаций.
        /// </summary>
        public static bool HasGameTools
            => IsGameProject
            || !IsLocationProject;
    }
}
