using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Находит неполные наборы LOD среди соседних объектов: есть Tree_LOD1, но нет Tree_LOD0, или пропущен уровень.
    /// Объекты без LOD не проверяются.
    /// </summary>
    internal sealed class LodPairRule : IModelRule
    {
        // Unity компилирует на C# 9 без генераторов регулярных выражений, поэтому GeneratedRegex недоступен.
        private static readonly Regex LodName =
            new Regex(
                Constants.Names.LodPattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <inheritdoc/>
        public string Title => "LOD";

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            foreach (var parent in ModelHierarchy.GetAll(context.Root))
            {
                var lodGroups =
                    parent
                        .Cast<Transform>()
                        .Select(child => LodName.Match(child.name))
                        .Where(match => match.Success)
                        .GroupBy(match => match.Groups["base"].Value);

                foreach (var lodGroup in lodGroups)
                {
                    var levels =
                        lodGroup
                            .Select(match => int.Parse(
                                match.Groups["level"].Value,
                                CultureInfo.InvariantCulture))
                            .Distinct()
                            .OrderBy(level => level)
                            .ToList();

                    if (levels[0] == 0
                     && levels[levels.Count - 1] == levels.Count - 1)
                    {
                        continue;
                    }

                    issues.Add(
                        context.CreateIssue(
                            ValidationSeverity.Warning,
                            parent,
                            $"Неполный набор LOD у «{lodGroup.Key}»: есть только LOD{string.Join(", LOD", levels)}. Уровни должны идти подряд с LOD0."));
                }
            }
        }
    }
}
