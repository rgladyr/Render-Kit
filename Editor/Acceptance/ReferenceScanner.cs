using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Acceptance
{
    /// <summary>
    /// Находит в файлах игры ссылки на объекты пакетов: правки сцен и вариантов, ссылки на модели и материалы.
    /// </summary>
    internal static class ReferenceScanner
    {
        private const string AssetsFolder = "Assets";

        private static readonly string[] Extensions = { ".unity", ".prefab", ".asset", ".mat", ".controller", ".overrideController", ".playable" };

        private static readonly Regex ReferencePattern =
            new Regex(
                @"fileID: (?<fileId>-?\d+), guid: (?<guid>[0-9a-f]{32})",
                RegexOptions.Compiled);

        private static bool IsScanned(string path)
            => Extensions.Any(extension => path.EndsWith(
                extension,
                StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<Reference> Scan(
            string path,
            ICollection<string> guids)
        {
            var text = File.ReadAllText(path);

            if (!guids.Any(guid => text.Contains(
                    guid,
                    StringComparison.Ordinal)))
            {
                return Enumerable.Empty<Reference>();
            }

            var assetPath =
                path.Replace(
                    '\\',
                    '/');

            return ReferencePattern
                .Matches(text)
                .Cast<Match>()
                .Where(match => guids.Contains(match.Groups["guid"].Value))
                .Select(match => new Reference(
                    assetPath,
                    match.Groups["guid"].Value,
                    long.Parse(
                        match.Groups["fileId"].Value,
                        CultureInfo.InvariantCulture)))
                .Distinct();
        }

        /// <summary>
        /// Находит ссылки на файлы с указанными GUID во всех сценах, префабах и ассетах игры.
        /// </summary>
        /// <param name="guids">GUID файлов пакетов.</param>
        /// <returns>Ссылки без повторов.</returns>
        public static List<Reference> FindAll(ICollection<string> guids)
            => Directory
                .GetFiles(
                    AssetsFolder,
                    "*",
                    SearchOption.AllDirectories)
                .Where(IsScanned)
                .SelectMany(path => Scan(
                    path,
                    guids))
                .ToList();

        /// <summary>
        /// Ссылка из файла игры на объект внутри файла пакета.
        /// </summary>
        internal readonly struct Reference : IEquatable<Reference>
        {
            public Reference(
                string source,
                string guid,
                long fileId)
            {
                this.Source = source;
                this.Guid = guid;
                this.FileId = fileId;
            }

            /// <summary>
            /// Файл игры, в котором записана ссылка.
            /// </summary>
            public string Source { get; }

            /// <summary>
            /// GUID файла пакета.
            /// </summary>
            public string Guid { get; }

            /// <summary>
            /// Идентификатор объекта внутри файла пакета.
            /// </summary>
            public long FileId { get; }

            /// <inheritdoc/>
            public bool Equals(Reference other)
                => string.Equals(
                       this.Source,
                       other.Source,
                       StringComparison.Ordinal)
                && string.Equals(
                       this.Guid,
                       other.Guid,
                       StringComparison.Ordinal)
                && this.FileId == other.FileId;

            /// <inheritdoc/>
            public override bool Equals(object obj)
                => obj is Reference other
                && this.Equals(other);

            /// <inheritdoc/>
            public override int GetHashCode()
                => HashCode.Combine(
                    this.Source,
                    this.Guid,
                    this.FileId);
        }
    }
}
