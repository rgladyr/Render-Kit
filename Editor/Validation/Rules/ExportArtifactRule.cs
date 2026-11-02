using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит следы неправильного экспорта из Blender: масштаб 0.01 или 100 у вложенных мешей (экспорт с Apply Transform)
    /// и масштаб с шумом вроде 0.9999 вместо 1.
    /// </summary>
    internal sealed class ExportArtifactRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Масштаб экспорта";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var target in ModelHierarchy.GetAll(context.Root))
            {
                var scale = target.localScale;

                if (IsExportArtifact(scale))
                {
                    var message =
                        $"Масштаб {scale}: так экспортёр Blender пишет вложенные меши при Apply Transform. "
                      + "Экспортируйте без Apply Transform, с Apply Scalings = FBX Units Scale.";

                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            target,
                            message));

                    continue;
                }

                if (HasNoisyComponent(scale))
                {
                    var formattedScale =
                        scale.ToString(
                            "F6",
                            CultureInfo.InvariantCulture);

                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            target,
                            $"Масштаб {formattedScale} почти равен 1. Примените трансформы в Blender (Ctrl+A → All Transforms)."));
                }
            }
        }

        private static bool IsExportArtifact(Vector3 scale)
            => IsArtifactScale(scale.x)
            && IsArtifactScale(scale.y)
            && IsArtifactScale(scale.z);

        private static bool IsArtifactScale(float component)
            => IsClose(
                   component,
                   Constants.ExportArtifact.SmallScale)
            || IsClose(
                   component,
                   Constants.ExportArtifact.LargeScale);

        private static bool IsClose(
            float component,
            float expected)
            => Mathf.Abs(
                   (component / expected)
                 - 1f)
             < Constants.Tolerances.ExportArtifactScale;

        private static bool HasNoisyComponent(Vector3 scale)
            => IsNoisy(scale.x)
            || IsNoisy(scale.y)
            || IsNoisy(scale.z);

        private static bool IsNoisy(float component)
        {
            var deviation =
                Mathf.Abs(
                    Mathf.Abs(component)
                  - 1f);

            return deviation > Constants.Tolerances.ExactScale
                && deviation < Constants.Tolerances.NoisyScale;
        }
    }
}
