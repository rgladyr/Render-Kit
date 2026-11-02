namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Поле, по которому сортируется список проблем в окне отчёта.
    /// </summary>
    internal enum IssueSortColumn
    {
        /// <summary>
        /// Путь к файлу, затем путь к объекту внутри него.
        /// </summary>
        Object,

        /// <summary>
        /// Уровень проблемы, затем название проверки и текст.
        /// </summary>
        Error,

        /// <summary>
        /// Время проверки.
        /// </summary>
        Date,
    }
}
