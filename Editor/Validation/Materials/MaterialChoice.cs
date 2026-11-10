using UnityEngine;

namespace PaperFly.RenderKit.Validation.Materials
{
    /// <summary>
    /// Что сделать с непривязанным материалом: привязать к файлу .mat или извлечь из модели.
    /// </summary>
    internal sealed class MaterialChoice
    {
        /// <summary>
        /// Создаёт выбранное действие.
        /// </summary>
        /// <param name="slot">Непривязанный материал.</param>
        /// <param name="target">Файл .mat для привязки; <c>null</c> — извлечь материал из модели.</param>
        public MaterialChoice(
            MaterialSlot slot,
            Material target)
        {
            this.Slot = slot;
            this.Target = target;
        }

        /// <summary>
        /// Непривязанный материал.
        /// </summary>
        public MaterialSlot Slot { get; }

        /// <summary>
        /// Файл .mat для привязки; <c>null</c> — извлечь материал из модели.
        /// </summary>
        public Material Target { get; }
    }
}
