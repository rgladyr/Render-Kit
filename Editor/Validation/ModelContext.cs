using UnityEditor;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Импортированная модель, которую проверяют правила.
    /// </summary>
    internal sealed class ModelContext
    {
        /// <summary>
        /// Создаёт контекст проверки модели.
        /// </summary>
        /// <param name="assetPath">Путь к файлу модели.</param>
        /// <param name="root">Корень иерархии модели.</param>
        /// <param name="importer">Импортёр модели.</param>
        public ModelContext(
            string assetPath,
            GameObject root,
            ModelImporter importer)
        {
            this.AssetPath = assetPath;
            this.Root = root;
            this.Importer = importer;
        }

        /// <summary>
        /// Путь к файлу модели.
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// Корень иерархии модели.
        /// </summary>
        public GameObject Root { get; }

        /// <summary>
        /// Импортёр модели.
        /// </summary>
        public ModelImporter Importer { get; }

        /// <summary>
        /// Создаёт проблему, привязанную к объекту модели.
        /// </summary>
        /// <param name="severity">Уровень проблемы.</param>
        /// <param name="target">Объект модели.</param>
        /// <param name="message">Описание проблемы.</param>
        /// <returns>Проблема с путём к объекту.</returns>
        public ValidationIssue CreateIssue(
            ValidationSeverity severity,
            Transform target,
            string message)
            => new ValidationIssue(
                severity,
                this.AssetPath,
                ModelHierarchy.GetPath(
                    target,
                    this.Root.transform),
                message);
    }
}
