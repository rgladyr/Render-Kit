using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using PaperFly.RenderKit.Release;

namespace PaperFly.RenderKit.Acceptance.Conflicts
{
    /// <summary>
    /// Находит файлы игры с теми же GUID, что у файлов подключённых пакетов локаций.
    /// </summary>
    /// <remarks>
    /// Так бывает, когда файл скопировали из игры в локацию вместе с .meta, а оригинал в игре оставили.
    /// Каждый файл принадлежит либо локации, либо игре, поэтому копию нужно удалить из игры:
    /// ссылки игры по тому же GUID сами перейдут на файл пакета.
    /// </remarks>
    internal static class GuidConflicts
    {
        private const string AssetsFolder = "Assets";

        private const string MetaExtension = ".meta";

        private const string FolderMarker = "folderAsset: yes";

        private const int MetaHeaderLines = 4;

        private static readonly Regex GuidLine =
            new Regex(
                @"^guid: (?<guid>[0-9a-f]{32})\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static string ToUnityPath(string path)
            => path.Replace(
                '\\',
                '/');

        private static (string Guid, bool IsFolder) ReadMeta(string metaPath)
        {
            var header =
                string.Join(
                    "\n",
                    File.ReadLines(metaPath).Take(MetaHeaderLines));

            var match = GuidLine.Match(header);

            return match.Success
                ? (match.Groups["guid"].Value, header.Contains(
                    FolderMarker,
                    StringComparison.Ordinal))
                : (string.Empty, false);
        }

        private static string StripMeta(string metaPath)
            => metaPath.Substring(
                0,
                metaPath.Length - MetaExtension.Length);

        private static Dictionary<string, string> ReadGuids(string folder)
        {
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var metaPath in Directory.GetFiles(
                         folder,
                         "*" + MetaExtension,
                         SearchOption.AllDirectories))
            {
                var (guid, _) = ReadMeta(metaPath);

                if (!string.IsNullOrEmpty(guid))
                {
                    guids[guid] = StripMeta(metaPath);
                }
            }

            return guids;
        }

        private static bool AreIdentical(
            string gamePath,
            string packagePath)
            => File.Exists(gamePath)
            && File.Exists(packagePath)
            && string.Equals(
                ManifestBuilder.Hash(gamePath),
                ManifestBuilder.Hash(packagePath),
                StringComparison.Ordinal);

        /// <summary>
        /// Находит конфликты GUID между Assets игры и подключёнными пакетами локаций.
        /// </summary>
        /// <returns>Конфликты, сначала файлы, потом папки.</returns>
        public static List<GuidConflict> Find()
        {
            var gameGuids = ReadGuids(AssetsFolder);
            var conflicts = new List<GuidConflict>();

            foreach (var location in InstalledLocation.FindAll())
            {
                foreach (var metaPath in Directory.GetFiles(
                             location.DiskFolder,
                             "*" + MetaExtension,
                             SearchOption.AllDirectories))
                {
                    var (guid, isFolder) = ReadMeta(metaPath);

                    if (string.IsNullOrEmpty(guid)
                     || !gameGuids.TryGetValue(
                            guid,
                            out var gamePath))
                    {
                        continue;
                    }

                    var diskPath = StripMeta(metaPath);
                    var relative = ToUnityPath(diskPath.Substring(location.DiskFolder.Length + 1));

                    conflicts.Add(
                        new GuidConflict(
                            guid,
                            ToUnityPath(gamePath),
                            $"{location.AssetFolder}/{relative}",
                            location.Name,
                            isFolder,
                            !isFolder
                         && AreIdentical(
                                gamePath,
                                diskPath)));
                }
            }

            return conflicts
                .OrderBy(conflict => conflict.IsFolder)
                .ThenBy(
                    conflict => conflict.GamePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
