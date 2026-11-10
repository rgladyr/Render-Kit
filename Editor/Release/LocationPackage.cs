using System;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Пакет локации: папка в Assets с package.json.
    /// </summary>
    internal sealed class LocationPackage
    {
        /// <summary>
        /// Создаёт описание пакета.
        /// </summary>
        /// <param name="folder">Папка пакета относительно проекта.</param>
        /// <param name="name">Имя пакета из package.json.</param>
        /// <param name="version">Версия пакета из package.json.</param>
        public LocationPackage(
            string folder,
            string name,
            string version)
        {
            this.Folder = folder;
            this.Name = name;
            this.Version = version;
        }

        /// <summary>
        /// Папка пакета относительно проекта.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// Имя пакета из package.json.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Версия пакета из package.json.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Проверяет, лежит ли файл в папке пакета.
        /// </summary>
        /// <param name="assetPath">Путь к файлу относительно проекта.</param>
        /// <returns><c>true</c>, если файл входит в пакет.</returns>
        public bool Contains(string assetPath)
            => assetPath.StartsWith(
                this.Folder + "/",
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Собирает манифест выпуска по текущему состоянию папки.
        /// </summary>
        /// <returns>Манифест с файлами и объектами основы.</returns>
        public ReleaseManifest BuildManifest()
            => ManifestBuilder.Build(
                this.Folder,
                this.Name,
                this.Version);
    }
}
