using System.Collections.Generic;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверка импортированной модели.
    /// </summary>
    internal interface IModelRule
    {
        /// <summary>
        /// Название проверки в отчёте, по нему фильтруется список проблем.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Проверяет модель и добавляет найденные проблемы.
        /// </summary>
        /// <param name="context">Проверяемая модель.</param>
        /// <param name="issues">Список, в который добавляются проблемы.</param>
        void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues);
    }
}
