using System;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Определяет, относится ли файл к локации.
    /// </summary>
    internal static class LocationAssets
    {
        /// <summary>
        /// Проверяет, лежит ли файл в папке локации.
        /// </summary>
        /// <param name="assetPath">Путь к файлу в проекте.</param>
        /// <returns><c>true</c>, если путь содержит папку Locations.</returns>
        public static bool IsLocationAsset(string assetPath)
            => !string.IsNullOrEmpty(assetPath)
            && assetPath
                .Replace('\\', '/')
                .Contains(
                    Constants.Paths.LocationMarker,
                    StringComparison.OrdinalIgnoreCase);
    }
}
