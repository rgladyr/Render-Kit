using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;

using UnityEngine;

using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PaperFly.RenderKit.Release
{
    /// <summary>
    /// Собирает, читает и записывает манифест выпуска пакета локации.
    /// </summary>
    internal static class ManifestBuilder
    {
        private const string MetaExtension = ".meta";

        private const string LfsPointerPrefix = "version https://git-lfs.github.com/spec/";

        private static readonly string[] ObjectAssetFilters = { "t:Prefab", "t:Model" };

        private static string[] GetFiles(string packageFolder)
            => Directory
                .GetFiles(
                    packageFolder,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => ToRelativePath(
                    packageFolder,
                    path))
                .Where(path => !path.StartsWith(
                    ReleaseManifest.FileName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToArray();

        private static string GetGuid(
            string packageFolder,
            string relativePath)
            => relativePath.EndsWith(
                    MetaExtension,
                    StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID($"{packageFolder}/{relativePath}");

        private static string Describe(Object asset)
        {
            switch (asset)
            {
                case GameObject gameObject:
                    return GetHierarchyPath(gameObject.transform);

                case Component component:
                    return GetHierarchyPath(component.transform);

                default:
                    return asset.name;
            }
        }

        private static void AddObjects(
            string packageFolder,
            string assetPath,
            ReleaseManifest manifest)
        {
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var relativePath = assetPath.Substring(packageFolder.Length + 1);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(asset => asset != null))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        asset,
                        out _,
                        out long fileId))
                {
                    continue;
                }

                manifest.objects.Add(
                    new ReleaseManifest.ObjectEntry
                    {
                        guid = assetGuid,
                        asset = relativePath,
                        path = Describe(asset),
                        type = asset.GetType().Name,
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
        /// Считает хеш содержимого файла.
        /// </summary>
        /// <param name="path">Путь к файлу на диске.</param>
        /// <returns>SHA-256 в нижнем регистре.</returns>
        /// <remarks>
        /// Git может хранить текст с переводами строк CRLF или LF в зависимости от настроек машины.
        /// Хеш считается по содержимому с LF, чтобы у художника и в игре он совпадал.
        /// </remarks>
        public static string Hash(string path)
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

        /// <summary>
        /// Проверяет, пришёл ли вместо файла указатель LFS.
        /// </summary>
        /// <param name="path">Путь к файлу на диске.</param>
        /// <returns><c>true</c>, если файл — текстовый указатель LFS.</returns>
        public static bool IsLfsPointer(string path)
        {
            var prefix = Encoding.ASCII.GetBytes(LfsPointerPrefix);

            using (var stream = File.OpenRead(path))
            {
                var buffer = new byte[prefix.Length];
                var read =
                    stream.Read(
                        buffer,
                        0,
                        buffer.Length);

                return read == prefix.Length
                    && buffer.SequenceEqual(prefix);
            }
        }

        /// <summary>
        /// Собирает манифест по текущему состоянию папки пакета.
        /// </summary>
        /// <param name="packageFolder">Папка пакета относительно проекта.</param>
        /// <param name="packageName">Имя пакета из package.json.</param>
        /// <param name="version">Версия пакета из package.json.</param>
        /// <returns>Манифест с файлами и объектами префабов и моделей.</returns>
        public static ReleaseManifest Build(
            string packageFolder,
            string packageName,
            string version)
        {
            var manifest =
                new ReleaseManifest
                {
                    format = ReleaseManifest.CurrentFormat,
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
                        guid =
                            GetGuid(
                                packageFolder,
                                path),
                        hash = Hash($"{packageFolder}/{path}"),
                    });
            }

            // Правки в игре ссылаются и на префабы, и прямо на объекты внутри FBX, поэтому в манифест входят оба.
            var assetPaths =
                AssetDatabase
                    .FindAssets(
                        string.Join(
                            " ",
                            ObjectAssetFilters),
                        new[] { packageFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal);

            foreach (var assetPath in assetPaths)
            {
                AddObjects(
                    packageFolder,
                    assetPath,
                    manifest);
            }

            return manifest;
        }

        /// <summary>
        /// Читает манифест из файла.
        /// </summary>
        /// <param name="path">Путь к файлу манифеста на диске.</param>
        /// <returns>Манифест или <c>null</c>, если файла нет.</returns>
        public static ReleaseManifest LoadFile(string path)
            => File.Exists(path)
                ? JsonUtility.FromJson<ReleaseManifest>(File.ReadAllText(path))
                : null;

        /// <summary>
        /// Читает манифест, записанный при прошлой подготовке выпуска.
        /// </summary>
        /// <param name="packageFolder">Папка пакета относительно проекта.</param>
        /// <returns>Манифест или <c>null</c>, если его ещё нет.</returns>
        public static ReleaseManifest Load(string packageFolder)
            => LoadFile(GetManifestPath(packageFolder));

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
