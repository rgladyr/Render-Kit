using System.Collections.Generic;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит объекты с отрицательным масштабом (зеркальные): у них выворачиваются нормали и ломается батчинг.
    /// </summary>
    internal sealed class NegativeScaleRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Отрицательный масштаб";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var target in ModelHierarchy.GetAll(context.Root))
            {
                var scale = target.localScale;

                if (scale.x < 0f
                 || scale.y < 0f
                 || scale.z < 0f)
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            target,
                            $"Отрицательный масштаб {scale}. Примените масштаб в Blender (Ctrl+A → Scale) и проверьте нормали."));
                }
            }
        }
    }
}
