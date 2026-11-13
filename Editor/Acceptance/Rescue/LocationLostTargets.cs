using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using PaperFly.RenderKit.Release;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Потерянные объекты по манифестам подключённых версий локаций и описания по принятым версиям.
    /// </summary>
    internal sealed class LocationLostTargets : ILostTargets
    {
        private const string TransformType = "Transform";

        // Ссылка на префаб или модель целиком (m_SourcePrefab): такой объект есть всегда.
        private const long PrefabAssetFileId = 100100000;

        private readonly HashSet<string> currentObjects = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> assetsWithObjects = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, ReleaseManifest.ObjectEntry> previousObjects =
            new Dictionary<string, ReleaseManifest.ObjectEntry>(StringComparer.Ordinal);

        private readonly Dictionary<string, long> rootTransforms = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>
        /// Собирает сведения по результатам проверки приёма.
        /// </summary>
        /// <param name="results">Результаты проверки подключённых локаций.</param>
        public LocationLostTargets(IEnumerable<AcceptanceResult> results)
        {
            foreach (var result in results.Where(result => !result.IsTrial))
            {
                this.Add(result);
            }
        }

        private void Add(AcceptanceResult result)
        {
            var manifest = result.Location.Manifest;

            foreach (var entry in manifest.objects)
            {
                this.currentObjects.Add(ManifestComparer.GetKey(entry));
                this.assetsWithObjects.Add(entry.guid);
            }

            // Корень файла — объект без «/» в пути иерархии.
            var roots =
                manifest.objects
                    .Where(entry => string.Equals(
                        entry.type,
                        TransformType,
                        StringComparison.Ordinal))
                    .Where(entry => !entry.path.Contains('/'));

            foreach (var root in roots)
            {
                this.rootTransforms[root.guid] = root.fileId;
            }

            foreach (var entry in result.Accepted?.objects ?? Enumerable.Empty<ReleaseManifest.ObjectEntry>())
            {
                this.previousObjects[ManifestComparer.GetKey(entry)] = entry;
            }
        }

        private ReleaseManifest.ObjectEntry FindPrevious(
            string guid,
            long fileId)
        {
            this.previousObjects.TryGetValue(
                ManifestComparer.GetKey(
                    guid,
                    fileId),
                out var entry);

            return entry;
        }

        /// <inheritdoc/>
        public bool IsLost(
            string guid,
            long fileId)
            => fileId != PrefabAssetFileId
            && this.assetsWithObjects.Contains(guid)
            && !this.currentObjects.Contains(ManifestComparer.GetKey(
                guid,
                fileId));

        /// <inheritdoc/>
        public string Describe(
            string guid,
            long fileId)
        {
            var entry =
                this.FindPrevious(
                    guid,
                    fileId);

            return entry == null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "объект {0}",
                    fileId)
                : ManifestComparer.Describe(entry);
        }

        /// <inheritdoc/>
        public string GetName(
            string guid,
            long fileId)
        {
            var entry =
                this.FindPrevious(
                    guid,
                    fileId);

            return entry == null
                ? fileId.ToString(CultureInfo.InvariantCulture)
                : entry.path.Split('/').Last();
        }

        /// <inheritdoc/>
        public bool TryGetRootTransform(
            string guid,
            out long fileId)
            => this.rootTransforms.TryGetValue(
                guid,
                out fileId);
    }
}
