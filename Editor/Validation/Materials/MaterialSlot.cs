using System.Collections.Generic;

using UnityEngine;

namespace PaperFly.RenderKit.Validation.Materials
{
    /// <summary>
    /// Материал модели, который не привязан к файлу .mat.
    /// </summary>
    internal sealed class MaterialSlot
    {
        /// <summary>
        /// Создаёт описание непривязанного материала.
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели.</param>
        /// <param name="name">Имя материала в модели.</param>
        /// <param name="isMissing"><c>true</c>, если привязка есть, но файл .mat пропал.</param>
        /// <param name="embedded">Материал внутри модели; <c>null</c>, если Unity его не создал.</param>
        /// <param name="candidates">Файлы .mat пакета с тем же именем.</param>
        public MaterialSlot(
            string modelPath,
            string name,
            bool isMissing,
            Material embedded,
            IReadOnlyList<Material> candidates)
        {
            this.ModelPath = modelPath;
            this.Name = name;
            this.IsMissing = isMissing;
            this.Embedded = embedded;
            this.Candidates = candidates;
        }

        /// <summary>
        /// Путь к файлу модели.
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// Имя материала в модели.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Привязка есть, но файл .mat удалён или перемещён.
        /// </summary>
        public bool IsMissing { get; }

        /// <summary>
        /// Материал внутри модели, который можно извлечь в файл.
        /// </summary>
        public Material Embedded { get; }

        /// <summary>
        /// Файлы .mat пакета с тем же именем.
        /// </summary>
        public IReadOnlyList<Material> Candidates { get; }

        /// <summary>
        /// Материал можно извлечь из модели в файл .mat.
        /// </summary>
        public bool CanExtract => this.Embedded != null;
    }
}
