using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using UnityEditor;

using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Собирает, читает и записывает манифест выпуска пакета локации.
    /// </summary>
    internal static class ManifestBuilder
    {
        private static string[] GetFiles(string packageFolder)
            => Directory
                .GetFiles(
                    packageFolder,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => ToRelativePath(packageFolder, path))
                .Where(path => !path.StartsWith(ReleaseManifest.FileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToArray();

        // Git может хранить текст с переводами строк CRLF или LF в зависимости от настроек машины.
        // Хеш считается по содержимому с LF, чтобы у художника и в игре он совпадал.
        private static string Hash(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var normalized = new byte[bytes.Length];
            var length = 0;

            for (var index = 0; index < bytes.Length; index++)
            {
                var isCarriageReturn =
                    bytes[index] == '\r'
                 && index + 1 < bytes.Length
                 && bytes[index + 1] == '\n';

                if (!isCarriageReturn)
                {
                    normalized[length++] = bytes[index];
                }
            }

            using (var sha = SHA256.Create())
            {
                var hash =
                    sha.ComputeHash(
                        normalized,
                        0,
                        length);

                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void AddObjects(
            string prefabPath,
            ReleaseManifest manifest)
        {
            foreach (var gameObject in AssetDatabase.LoadAllAssetsAtPath(prefabPath).OfType<GameObject>())
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        gameObject,
                        out _,
                        out long fileId))
                {
                    continue;
                }

                manifest.objects.Add(
                    new ReleaseManifest.ObjectEntry
                    {
                        prefab = prefabPath,
                        path = GetHierarchyPath(gameObject.transform),
                        fileId = fileId,
                    });
            }
        }

        private static string GetHierarchyPath(Transform transform)
            => transform.parent == null
                ? transform.name
                : $"{GetHierarchyPath(transform.parent)}/{transform.name}";

        private static string ToRelativePath(
            string folder,
            string path)
            => path
                .Substring(folder.Length + 1)
                .Replace(
                    '\\',
                    '/');

        private static string GetManifestPath(string packageFolder)
            => $"{packageFolder}/{ReleaseManifest.FileName}";

        /// <summary>
        /// Собирает манифест по текущему состоянию папки пакета.
        /// </summary>
        /// <param name="packageFolder">Папка пакета относительно проекта.</param>
        /// <param name="packageName">Имя пакета из package.json.</param>
        /// <param name="version">Версия пакета из package.json.</param>
        /// <returns>Манифест с файлами и объектами основы.</returns>
        public static ReleaseManifest Build(
            string packageFolder,
            string packageName,
            string version)
        {
            var manifest =
                new ReleaseManifest
                {
                    package = packageName,
                    version = version,
                    renderKitVersion = PackageInfo.FindForAssembly(typeof(ManifestBuilder).Assembly)?.version,
                };

            foreach (var path in GetFiles(packageFolder))
            {
                manifest.files.Add(
                    new ReleaseManifest.FileEntry
                    {
                        path = path,
                        hash = Hash($"{packageFolder}/{path}"),
                    });
            }

            var prefabPaths =
                AssetDatabase
                    .FindAssets(
                        "t:Prefab",
                        new[] { packageFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal);

            foreach (var prefabPath in prefabPaths)
            {
                AddObjects(
                    prefabPath,
                    manifest);
            }

            return manifest;
        }

        /// <summary>
        /// Читает манифест, записанный при прошлой подготовке выпуска.
        /// </summary>
        /// <param name="packageFolder">Папка пакета относительно проекта.</param>
        /// <returns>Манифест или <c>null</c>, если его ещё нет.</returns>
        public static ReleaseManifest Load(string packageFolder)
        {
            var path = GetManifestPath(packageFolder);

            return File.Exists(path)
                ? JsonUtility.FromJson<ReleaseManifest>(File.ReadAllText(path))
                : null;
        }

        /// <summary>
        /// Записывает манифест в папку пакета.
        /// </summary>
        /// <param name="packageFolder">Папка пакета относительно проекта.</param>
        /// <param name="manifest">Манифест.</param>
        public static void Save(
            string packageFolder,
            ReleaseManifest manifest)
        {
            var path = GetManifestPath(packageFolder);

            File.WriteAllText(
                path,
                JsonUtility.ToJson(
                    manifest,
                    true));

            AssetDatabase.ImportAsset(path);
        }
    }
}
