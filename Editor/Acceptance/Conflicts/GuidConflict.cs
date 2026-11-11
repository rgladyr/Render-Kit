namespace PaperFly.RenderKit.Acceptance.Conflicts
{
    /// <summary>
    /// Файл игры и файл пакета локации с одинаковым GUID.
    /// </summary>
    /// <remarks>
    /// Так бывает, когда репозиторий локации создан копией файлов игры вместе с .meta, а оригиналы остались в игре.
    /// </remarks>
    internal sealed class GuidConflict
    {
        /// <summary>
        /// Создаёт описание конфликта.
        /// </summary>
        /// <param name="guid">Общий GUID.</param>
        /// <param name="gamePath">Путь к файлу игры относительно проекта.</param>
        /// <param name="packagePath">Путь к файлу пакета для AssetDatabase: Packages/имя/....</param>
        /// <param name="packageName">Имя пакета локации.</param>
        /// <param name="isFolder"><c>true</c>, если конфликтуют папки.</param>
        /// <param name="isIdentical"><c>true</c>, если содержимое файлов совпадает.</param>
        public GuidConflict(
            string guid,
            string gamePath,
            string packagePath,
            string packageName,
            bool isFolder,
            bool isIdentical)
        {
            this.Guid = guid;
            this.GamePath = gamePath;
            this.PackagePath = packagePath;
            this.PackageName = packageName;
            this.IsFolder = isFolder;
            this.IsIdentical = isIdentical;
        }

        /// <summary>
        /// Общий GUID.
        /// </summary>
        public string Guid { get; }

        /// <summary>
        /// Путь к файлу игры относительно проекта.
        /// </summary>
        public string GamePath { get; }

        /// <summary>
        /// Путь к файлу пакета для AssetDatabase.
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// Имя пакета локации.
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// Конфликтуют папки, а не файлы.
        /// </summary>
        public bool IsFolder { get; }

        /// <summary>
        /// Содержимое файлов совпадает: файл игры можно удалить без потерь.
        /// </summary>
        public bool IsIdentical { get; }
    }
}
