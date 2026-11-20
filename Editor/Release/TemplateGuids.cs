using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// GUID файлов шаблона локации. Новая локация из шаблона получает копию его .meta, поэтому без замены
    /// у всех локаций были бы одни GUID, и в игре, где все локации — пакеты одного проекта, они конфликтовали бы.
    /// </summary>
    /// <remarks>
    /// Список нужно обновлять вместе с файлами в Assets/Location шаблона.
    /// </remarks>
    internal static class TemplateGuids
    {
        private const string AssetsFolder = "Assets";

        private const string MetaExtension = ".meta";

        // Текстовые файлы Unity, в которых могут быть ссылки по GUID.
        private static readonly string[] ReferenceExtensions =
        {
            ".meta", ".unity", ".prefab", ".asset", ".mat", ".controller", ".overrideController", ".anim", ".playable", ".lighting",
        };

        private static readonly HashSet<string> Guids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Location-Art.prefab
                "fdee5f6adb23439ab2c6266ee4109b06",

                // Materials
                "1d4a4c8d45ee47d9bda39938ed895e05",

                // Models
                "70b8066a950442cf8c0b4f507dc6ebf4",

                // package.json
                "ee63d0f5d9da416aaabc88912163dbe6",

                // Textures
                "4061be4f91e54885be2ce1eace3dd482",
            };

        private static readonly Regex GuidLine =
            new Regex(
                @"^guid: (?<guid>[0-9a-f]{32})",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static string ReadGuid(string metaPath)
        {
            var match = GuidLine.Match(File.ReadAllText(metaPath));

            return match.Success
                ? match.Groups["guid"].Value
                : string.Empty;
        }

        private static string ToUnityPath(string path)
            => path.Replace(
                '\\',
                '/');

        /// <summary>
        /// Находит в папке пакета файлы, у которых остался GUID шаблона.
        /// </summary>
        /// <param name="packageFolder">Папка пакета локации, например Assets/Location.</param>
        /// <returns>Пути файлов .meta.</returns>
        public static List<string> Find(string packageFolder)
            => Directory.Exists(packageFolder)
                ? Directory
                    .GetFiles(
                        packageFolder,
                        "*" + MetaExtension,
                        SearchOption.AllDirectories)
                    .Where(path => Guids.Contains(ReadGuid(path)))
                    .Select(ToUnityPath)
                    .ToList()
                : new List<string>();

        /// <summary>
        /// Выдаёт файлам с GUID шаблона новые GUID и переводит на них ссылки во всём проекте.
        /// </summary>
        /// <param name="metaPaths">Файлы .meta из <see cref="Find"/>.</param>
        /// <returns>Число файлов, получивших новый GUID.</returns>
        /// <remarks>
        /// Ссылки меняются в тексте файлов, затем Unity нужно обновить базу ассетов (AssetDatabase.Refresh).
        /// </remarks>
        public static int Regenerate(IReadOnlyCollection<string> metaPaths)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var metaPath in metaPaths)
            {
                var guid = ReadGuid(metaPath);

                if (Guids.Contains(guid))
                {
                    replacements[guid] = Guid.NewGuid().ToString("N");
                }
            }

            if (replacements.Count == 0)
            {
                return 0;
            }

            var files =
                Directory
                    .GetFiles(
                        AssetsFolder,
                        "*",
                        SearchOption.AllDirectories)
                    .Where(path => ReferenceExtensions.Any(extension => path.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase)));

            foreach (var path in files)
            {
                var text = File.ReadAllText(path);

                var replaced =
                    replacements.Aggregate(
                        text,
                        (current, pair) => current.Replace(
                            pair.Key,
                            pair.Value));

                if (!string.Equals(
                        text,
                        replaced,
                        StringComparison.Ordinal))
                {
                    File.WriteAllText(
                        path,
                        replaced);
                }
            }

            return replacements.Count;
        }
    }
}
