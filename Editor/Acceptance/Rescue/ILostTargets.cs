namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Сведения об объектах основы: каких больше нет и как их назвать в отчёте.
    /// </summary>
    internal interface ILostTargets
    {
        /// <summary>
        /// Объекта основы больше нет: правка на него потеряна.
        /// </summary>
        /// <param name="guid">GUID файла основы.</param>
        /// <param name="fileId">Идентификатор объекта в файле основы.</param>
        /// <returns><c>true</c>, если файл относится к локации, а объекта в нём больше нет.</returns>
        bool IsLost(
            string guid,
            long fileId);

        /// <summary>
        /// Описание объекта для отчёта.
        /// </summary>
        /// <param name="guid">GUID файла основы.</param>
        /// <param name="fileId">Идентификатор объекта.</param>
        /// <returns>Файл, путь в иерархии и тип, если объект известен по принятой версии.</returns>
        string Describe(
            string guid,
            long fileId);

        /// <summary>
        /// Имя для объекта «_Потерянное …».
        /// </summary>
        /// <param name="guid">GUID файла основы.</param>
        /// <param name="fileId">Идентификатор объекта.</param>
        /// <returns>Имя последнего звена пути в иерархии.</returns>
        string GetName(
            string guid,
            long fileId);

        /// <summary>
        /// Находит пересозданный объект: тот же файл, путь в иерархии и тип, но новый идентификатор.
        /// </summary>
        /// <param name="guid">GUID файла основы.</param>
        /// <param name="fileId">Идентификатор потерянного объекта.</param>
        /// <param name="recreatedFileId">Идентификатор нового объекта в том же файле.</param>
        /// <returns><c>false</c>, если объект удалён или переименован.</returns>
        bool TryGetRecreated(
            string guid,
            long fileId,
            out long recreatedFileId);

        /// <summary>
        /// Корневой Transform основы, к которому вешается объект «_Потерянное …».
        /// </summary>
        /// <param name="guid">GUID файла основы.</param>
        /// <param name="fileId">Идентификатор корневого Transform.</param>
        /// <returns><c>false</c>, если корень неизвестен.</returns>
        bool TryGetRootTransform(
            string guid,
            out long fileId);
    }
}
