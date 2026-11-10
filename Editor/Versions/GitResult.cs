namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Результат запуска git.
    /// </summary>
    internal sealed class GitResult
    {
        /// <summary>
        /// Создаёт результат.
        /// </summary>
        /// <param name="isSuccess">Команда завершилась без ошибки.</param>
        /// <param name="output">Стандартный вывод.</param>
        /// <param name="error">Текст ошибки.</param>
        public GitResult(
            bool isSuccess,
            string output,
            string error)
        {
            this.IsSuccess = isSuccess;
            this.Output = output;
            this.Error = error;
        }

        /// <summary>
        /// Команда завершилась без ошибки.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Стандартный вывод.
        /// </summary>
        public string Output { get; }

        /// <summary>
        /// Текст ошибки.
        /// </summary>
        public string Error { get; }
    }
}
