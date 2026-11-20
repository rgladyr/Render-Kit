using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Проверяет имена объектов и материалов модели по соглашению о наименованиях в Blender.
    /// </summary>
    /// <remarks>
    /// Пока правило выдаёт только предупреждения: существующие локации ещё не приведены к соглашению.
    /// Корень модели не проверяется: его имя — имя файла, его проверяет правило имён файлов.
    /// </remarks>
    internal sealed class NamingConventionRule : IModelRule
    {
        /// <inheritdoc/>
        public string Title => "Соглашение об именах";

        private static void ValidateObject(
            ModelContext context,
            Transform target,
            ICollection<ValidationIssue> issues)
        {
            var isLod =
                NameGrammar.TrySplitLod(
                    target.name,
                    out var baseName);

            var problems = NameGrammar.FindSymbolProblems(baseName);

            problems.AddRange(
                NameGrammar.FindStructureProblems(
                    baseName,
                    true));

            if (isLod
             && !string.Equals(
                    target.parent.name,
                    baseName,
                    StringComparison.Ordinal))
            {
                problems.Add($"уровень LOD лежит в пустом родителе с тем же именем без постфикса: «{baseName}», а родитель «{target.parent.name}»");
            }

            var hasLodChildren =
                target
                    .Cast<Transform>()
                    .Any(child => NameGrammar.TrySplitLod(
                        child.name,
                        out _));

            if (hasLodChildren
             && target.TryGetComponent<MeshFilter>(out _))
            {
                problems.Add("родитель уровней LOD должен быть пустым объектом без меша");
            }

            if (problems.Count > 0)
            {
                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Warning,
                        target,
                        $"Имя «{target.name}» не по соглашению: {string.Join("; ", problems)}."));
            }
        }

        // Материал одной модели называется как модель (Bench.v1), общий — по назначению, без версии (VertexColor).
        private static void ValidateMaterial(
            ModelContext context,
            string material,
            Transform owner,
            ICollection<string> models,
            ICollection<ValidationIssue> issues)
        {
            NameGrammar.TrySplitLod(
                material,
                out var baseName);

            var problems = NameGrammar.FindSymbolProblems(baseName);
            var modelName = NameGrammar.GetModelName(material);

            if (!NameGrammar.HasVersion(material))
            {
                // Общий материал: имя по назначению, проверить можно только символы и регистр.
                if (char.IsLower(material[0]))
                {
                    problems.Add("слова пишутся с заглавной (PascalCase)");
                }
            }
            else if (!string.Equals(
                         modelName,
                         material,
                         StringComparison.Ordinal))
            {
                problems.Add($"материал модели называется как модель, без индекса и _LODi: «{modelName}»");
            }
            else
            {
                problems.AddRange(
                    NameGrammar.FindStructureProblems(
                        material,
                        false));

                if (!models.Contains(material))
                {
                    problems.Add("материал с версией называется как модель, а модели с таким именем в файле нет; общий материал называется по назначению, без версии");
                }
            }

            if (problems.Count > 0)
            {
                issues.Add(
                    context.CreateIssue(
                        ValidationSeverity.Warning,
                        owner,
                        $"Материал «{material}» не по соглашению: {string.Join("; ", problems)}."));
            }
        }

        /// <inheritdoc/>
        public void Validate(
            ModelContext context,
            ICollection<ValidationIssue> issues)
        {
            var objects =
                ModelHierarchy
                    .GetAll(context.Root)
                    .Where(target => target != context.Root.transform)
                    .ToList();

            foreach (var target in objects)
            {
                ValidateObject(
                    context,
                    target,
                    issues);
            }

            var models =
                new HashSet<string>(
                    objects.Select(target => NameGrammar.GetModelName(target.name)),
                    StringComparer.Ordinal);

            var materials =
                objects
                    .Where(target => target.TryGetComponent<Renderer>(out _))
                    .SelectMany(target => target
                        .GetComponent<Renderer>()
                        .sharedMaterials
                        .Where(material => material != null)
                        .Select(material => (material.name, target)))
                    .GroupBy(
                        pair => pair.name,
                        StringComparer.Ordinal);

            foreach (var material in materials)
            {
                ValidateMaterial(
                    context,
                    material.Key,
                    material.First().target,
                    models,
                    issues);
            }
        }
    }
}
