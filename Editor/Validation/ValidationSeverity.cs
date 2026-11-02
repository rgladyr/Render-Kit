namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Уровень найденной проблемы.
    /// </summary>
    internal enum ValidationSeverity
    {
        /// <summary>
        /// Информация, действий не требует.
        /// </summary>
        Info,

        /// <summary>
        /// Предупреждение, стоит исправить.
        /// </summary>
        Warning,

        /// <summary>
        /// Ошибка, блокирует выпуск.
        /// </summary>
        Error,
    }
}
