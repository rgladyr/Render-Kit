using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит пустые меши и меши без UV при текстурированном материале.
    /// </summary>
    internal sealed class MeshContentRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Содержимое меша";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var filter in context.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;

                if (mesh == null
                 || IsEmpty(mesh))
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            filter.transform,
                            "Меш без треугольников. Удалите пустой объект в Blender."));

                    continue;
                }

                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0)
                 && HasTexturedMaterial(filter))
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            filter.transform,
                            $"У меша «{mesh.name}» нет UV, а материал использует текстуру. Сделайте развёртку в Blender."));
                }
            }
        }

        private static bool IsEmpty(Mesh mesh)
            => mesh.vertexCount == 0
            || Enumerable
                .Range(
                    0,
                    mesh.subMeshCount)
                .All(subMesh => mesh.GetIndexCount(subMesh) == 0);

        private static bool HasTexturedMaterial(MeshFilter filter)
            => filter.TryGetComponent<Renderer>(out var renderer)
            && renderer.sharedMaterials.Any(material =>
                material != null
                && material.mainTexture != null);
    }
}
