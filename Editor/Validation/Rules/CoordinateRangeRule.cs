using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит геометрию за пределами ±2048 по любой оси и показывает, на сколько она вышла за предел.
    /// Корень локации стоит в нуле, поэтому координаты модели совпадают с координатами в сцене.
    /// </summary>
    internal sealed class CoordinateRangeRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Координаты";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            var outliers =
                ModelHierarchy
                    .GetMeshes(context.Root)
                    .Select(entry =>
                        ToOutlier(
                            entry.Owner,
                            entry.Mesh))
                    .Where(outlier => MaxComponent(outlier.Excess) > 0f)
                    .OrderByDescending(outlier => MaxComponent(outlier.Excess))
                    .ToList();

            if (outliers.Count == 0)
            {
                return;
            }

            AddSummary(
                context,
                outliers.Select(entry => entry.Bounds).ToList(),
                issues);

            foreach (var outlier in outliers.Take(Constants.Limits.MaxReportedOutliers))
            {
                var message =
                    $"Выходит за ±{Format(Constants.Limits.MaxCoordinate)}: {DescribeExcess(outlier.Excess)}. "
                  + $"Габариты от {Format(outlier.Bounds.min)} до {Format(outlier.Bounds.max)}.";

                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Warning,
                        outlier.Owner,
                        message));
            }
        }

        private static void AddSummary(
            ModelContext context,
            List<Bounds> outlierBounds,
            ICollection<ValidationIssue> issues)
        {
            var total = outlierBounds[0];

            foreach (var bounds in outlierBounds.Skip(1))
            {
                total.Encapsulate(bounds);
            }

            var hiddenCount =
                outlierBounds.Count
              - Constants.Limits.MaxReportedOutliers;

            var message =
                $"Модель выходит за ±{Format(Constants.Limits.MaxCoordinate)}: {DescribeExcess(GetExcess(total))}. "
              + $"Объектов за пределом: {outlierBounds.Count}"
              + (hiddenCount > 0 ? $", ниже перечислены {Constants.Limits.MaxReportedOutliers} самых дальних. " : ". ")
              + "Сдвиньте локацию к началу координат или уменьшите её в Blender.";

            issues.Add(
                context.CreateIssue(
                    ValidationSeverity.Warning,
                    context.Root.transform,
                    message));
        }

        private static (Transform Owner, Bounds Bounds, Vector3 Excess) ToOutlier(
            Transform owner,
            Mesh mesh)
        {
            var bounds =
                ToModelSpace(
                    mesh.bounds,
                    owner.localToWorldMatrix);

            return (owner, bounds, GetExcess(bounds));
        }

        private static Bounds ToModelSpace(
            Bounds local,
            Matrix4x4 localToModel)
        {
            var result =
                new Bounds(
                    localToModel.MultiplyPoint3x4(local.min),
                    Vector3.zero);

            for (var corner = 0; corner < 8; corner++)
            {
                var point =
                    new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);

                result.Encapsulate(localToModel.MultiplyPoint3x4(point));
            }

            return result;
        }

        private static Vector3 GetExcess(Bounds bounds)
            => new Vector3(
                GetAxisExcess(
                    bounds.min.x,
                    bounds.max.x),
                GetAxisExcess(
                    bounds.min.y,
                    bounds.max.y),
                GetAxisExcess(
                    bounds.min.z,
                    bounds.max.z));

        private static float GetAxisExcess(
            float min,
            float max)
            => Mathf.Max(
                Mathf.Abs(min),
                Mathf.Abs(max))
             - Constants.Limits.MaxCoordinate;

        private static string DescribeExcess(Vector3 excess)
        {
            var parts = new List<string>();

            if (excess.x > 0f)
            {
                parts.Add($"по X на {Format(excess.x)}");
            }

            if (excess.y > 0f)
            {
                parts.Add($"по Y на {Format(excess.y)}");
            }

            if (excess.z > 0f)
            {
                parts.Add($"по Z на {Format(excess.z)}");
            }

            return string.Join(
                ", ",
                parts);
        }

        private static float MaxComponent(Vector3 vector)
            => Mathf.Max(
                vector.x,
                vector.y,
                vector.z);

        private static string Format(float value)
            => value.ToString(
                "F0",
                CultureInfo.InvariantCulture);

        private static string Format(Vector3 point)
            => $"({Format(point.x)}, {Format(point.y)}, {Format(point.z)})";
    }
}
