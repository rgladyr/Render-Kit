using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит соседние объекты с одинаковыми именами.
    /// Unity различает их по порядку, поэтому после перестановки в Blender логика может привязаться не к тому объекту.
    /// </summary>
    internal sealed class DuplicateSiblingNamesRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Одинаковые имена";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var parent in ModelHierarchy.GetAll(context.Root))
            {
                var duplicates =
                    parent
                        .Cast<Transform>()
                        .GroupBy(
                            child => child.name,
                            StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Count() > 1);

                foreach (var duplicate in duplicates)
                {
                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            parent,
                            $"Несколько дочерних объектов с именем «{duplicate.Key}» ({duplicate.Count()} шт.). Переименуйте их в Blender."));
                }
            }
        }
    }
}
