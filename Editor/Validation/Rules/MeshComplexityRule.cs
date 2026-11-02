using System.Collections.Generic;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит меши с большим числом материалов или вершин.
    /// </summary>
    internal sealed class MeshComplexityRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Сложность меша";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var renderer in context.Root.GetComponentsInChildren<Renderer>(true))
            {
                var materialCount = renderer.sharedMaterials.Length;

                if (materialCount > Constants.Limits.MaxMaterialsPerRenderer)
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            renderer.transform,
                            $"{materialCount} материалов на одном меше (допустимо до {Constants.Limits.MaxMaterialsPerRenderer}). Каждый материал — отдельный вызов отрисовки."));
                }
            }

            foreach (var (owner, mesh) in ModelHierarchy.GetMeshes(context.Root))
            {
                if (mesh.vertexCount > Constants.Limits.MaxVerticesPerMesh)
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            owner,
                            $"В меше «{mesh.name}» {mesh.vertexCount} вершин (допустимо до {Constants.Limits.MaxVerticesPerMesh}). Разделите меш или упростите его."));
                }
            }
        }
    }
}
