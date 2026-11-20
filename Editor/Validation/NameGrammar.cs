using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Validation
{
    /// <summary>
    /// Разбор имён по соглашению о наименованиях в Blender: [Зона.]Наименование[.Вариация].vX[.ZZZ][_LODi].
    /// </summary>
    /// <remarks>
    /// Части разделяются точкой, слова пишутся в PascalCase латиницей. Версия .vX обязательна, индекс экземпляра —
    /// три цифры сразу после версии. Подчёркивание допустимо только в постфиксе уровня детализации _LODi,
    /// который стоит последним: так его опознаёт импортёр Unity.
    /// </remarks>
    internal static class NameGrammar
    {
        private const char Separator = '.';

        private const int IndexLength = 3;

        private static readonly Regex LodSuffix =
            new Regex(
                @"_LOD(?<level>\d+)$",
                RegexOptions.Compiled);

        private static readonly Regex MisplacedLod =
            new Regex(
                @"_LOD\d+",
                RegexOptions.Compiled);

        private static readonly Regex VersionPart =
            new Regex(
                @"^v\d+$",
                RegexOptions.Compiled);

        private static readonly Regex DigitsPart =
            new Regex(
                @"^\d+$",
                RegexOptions.Compiled);

        /// <summary>
        /// Назначение карты в конце имени файла текстуры.
        /// </summary>
        public static readonly string[] TextureChannels = { "BaseColor", "Normal", "Roughness", "Metallic", "Opacity", "AO", "Emissive", "Height" };

        private static bool IsAllowedSymbol(char symbol)
            => (symbol >= 'A' && symbol <= 'Z')
            || (symbol >= 'a' && symbol <= 'z')
            || (symbol >= '0' && symbol <= '9')
            || symbol == Separator;

        private static string DescribeSymbol(char symbol)
            => char.IsWhiteSpace(symbol)
                ? "пробел"
                : $"«{symbol}» — разделитель только точка";

        /// <summary>
        /// Отделяет постфикс уровня детализации.
        /// </summary>
        /// <param name="name">Имя объекта.</param>
        /// <param name="baseName">Имя без постфикса _LODi.</param>
        /// <returns><c>true</c>, если постфикс есть.</returns>
        public static bool TrySplitLod(
            string name,
            out string baseName)
        {
            var match = LodSuffix.Match(name);

            baseName =
                match.Success
                    ? name.Substring(
                        0,
                        match.Index)
                    : name;

            return match.Success;
        }

        /// <summary>
        /// Находит недопустимые символы и дефекты разделителей.
        /// </summary>
        /// <param name="name">Имя без постфикса _LODi.</param>
        /// <returns>Описания проблем.</returns>
        public static List<string> FindSymbolProblems(string name)
        {
            var invalid =
                name
                    .Where(symbol => !IsAllowedSymbol(symbol))
                    .Distinct()
                    .ToList();

            var nonLatin =
                invalid
                    .Where(symbol => symbol > Constants.Names.MaxAsciiCode)
                    .ToList();

            var problems =
                invalid
                    .Where(symbol => symbol <= Constants.Names.MaxAsciiCode)
                    .Select(symbol => symbol == '_'
                        ? MisplacedLod.IsMatch(name)
                            ? "постфикс _LODi всегда стоит последним"
                            : "подчёркивание допустимо только в постфиксе _LODi в конце имени"
                        : DescribeSymbol(symbol))
                    .ToList();

            if (nonLatin.Count > 0)
            {
                problems.Add($"символы не латиницей: {string.Join(" ", nonLatin)} — кириллические «С», «а», «е», «о», «р», «х» неотличимы на глаз от латинских");
            }

            if (name.Contains(".."))
            {
                problems.Add("двойная точка");
            }

            if (name.StartsWith(
                    ".",
                    StringComparison.Ordinal)
             || name.EndsWith(
                    ".",
                    StringComparison.Ordinal))
            {
                problems.Add("точка в начале или в конце");
            }

            return problems;
        }

        /// <summary>
        /// Проверяет устройство имени: PascalCase, версия, индекс.
        /// </summary>
        /// <param name="name">Имя без постфикса _LODi.</param>
        /// <param name="requireIndex"><c>true</c> для экземпляров на локации; <c>false</c> для моделей и материалов.</param>
        /// <returns>Описания проблем.</returns>
        public static List<string> FindStructureProblems(
            string name,
            bool requireIndex)
        {
            var problems = new List<string>();

            var parts =
                name.Split(
                    new[] { Separator },
                    StringSplitOptions.RemoveEmptyEntries);

            var versionIndexes =
                parts
                    .Select((part, index) => (part, index))
                    .Where(pair => VersionPart.IsMatch(pair.part))
                    .Select(pair => pair.index)
                    .ToList();

            if (versionIndexes.Count == 0)
            {
                problems.Add("нет версии .vX (например Bench.v1)");

                return problems;
            }

            if (versionIndexes.Count > 1)
            {
                problems.Add("версия .vX указана несколько раз");
            }

            var version = versionIndexes[0];

            var lowercase =
                parts
                    .Take(version)
                    .Where(part => char.IsLower(part[0]))
                    .ToList();

            if (lowercase.Count > 0)
            {
                problems.Add($"слова пишутся с заглавной (PascalCase): {string.Join(", ", lowercase)}");
            }

            if (parts.Take(version).Any(part => DigitsPart.IsMatch(part)))
            {
                problems.Add("число стоит до версии: индекс пишется после неё (Bench.v1.001)");
            }

            var tail = parts.Skip(version + 1).ToList();

            if (tail.Count == 0)
            {
                if (requireIndex)
                {
                    problems.Add("нет индекса экземпляра после версии (Bench.v1.001)");
                }

                return problems;
            }

            if (!requireIndex)
            {
                problems.Add($"после версии ничего не пишется, а здесь «{string.Join(".", tail)}»");

                return problems;
            }

            if (tail.Count > 1
             || !DigitsPart.IsMatch(tail[0]))
            {
                problems.Add($"после версии может стоять только индекс из трёх цифр, а здесь «{string.Join(".", tail)}»");
            }
            else if (tail[0].Length != IndexLength)
            {
                problems.Add($"индекс «{tail[0]}» пишется тремя цифрами: {tail[0].PadLeft(IndexLength, '0')}");
            }
            else if (int.Parse(
                         tail[0],
                         CultureInfo.InvariantCulture)
                  == 0)
            {
                problems.Add("индекс начинается с 001");
            }

            return problems;
        }

        /// <summary>
        /// Проверяет, есть ли в имени версия .vX.
        /// </summary>
        /// <param name="name">Имя.</param>
        /// <returns><c>true</c>, если одна из частей имени — версия.</returns>
        public static bool HasVersion(string name)
            => name
                .Split(Separator)
                .Any(part => VersionPart.IsMatch(part));

        /// <summary>
        /// Имя модели без индекса экземпляра и постфикса _LODi: Bench.v1.001_LOD0 → Bench.v1.
        /// </summary>
        /// <param name="name">Имя объекта.</param>
        /// <returns>Имя модели; само имя, если версии нет.</returns>
        public static string GetModelName(string name)
        {
            TrySplitLod(
                name,
                out var baseName);

            var parts = baseName.Split(Separator).ToList();
            var version = parts.FindIndex(part => VersionPart.IsMatch(part));

            return version < 0
                ? baseName
                : string.Join(
                    Separator.ToString(),
                    parts.Take(version + 1));
        }

        /// <summary>
        /// Находит назначение карты по имени файла текстуры: Box.v1.A2.Normal → Normal.
        /// </summary>
        /// <param name="fileName">Имя файла без расширения.</param>
        /// <returns>Назначение из <see cref="TextureChannels"/>; <c>null</c>, если не найдено.</returns>
        public static string FindTextureChannel(string fileName)
        {
            var last = fileName.Split(Separator).Last();

            return TextureChannels.FirstOrDefault(channel => string.Equals(
                channel,
                last,
                StringComparison.Ordinal));
        }
    }
}
