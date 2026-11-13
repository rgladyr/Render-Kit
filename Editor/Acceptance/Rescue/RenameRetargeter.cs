using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Переводит ссылки файла игры со старых объектов основы на объекты, в которые они переименованы.
    /// </summary>
    /// <remarks>
    /// Меняются все ссылки файла: цели правок, добавленных компонентов и объектов, значения-ссылки и заглушки экземпляров.
    /// Заглушка получает новый идентификатор «экземпляр XOR объект основы», как её назвала бы Unity, и все ссылки на неё
    /// внутри файла переводятся следом.
    /// </remarks>
    internal static class RenameRetargeter
    {
        private const string SourceLinePrefix = "  m_CorrespondingSourceObject:";

        private static readonly Regex ExternalPattern =
            new Regex(
                @"\{fileID: (?<fileId>-?\d+), guid: (?<guid>[0-9a-f]{32}), type: (?<type>\d+)\}",
                RegexOptions.Compiled);

        private static string Retarget(
            string line,
            ILostTargets targets)
            => ExternalPattern.Replace(
                line,
                match =>
                {
                    var guid = match.Groups["guid"].Value;

                    var fileId =
                        long.Parse(
                            match.Groups["fileId"].Value,
                            CultureInfo.InvariantCulture);

                    if (!targets.IsLost(
                            guid,
                            fileId)
                     || !targets.TryGetRenamed(
                            guid,
                            fileId,
                            out var renamedFileId,
                            out _))
                    {
                        return match.Value;
                    }

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{{fileID: {0}, guid: {1}, type: {2}}}",
                        renamedFileId,
                        guid,
                        match.Groups["type"].Value);
                });

        private static long GetExpectedFileId(UnityYamlDocument stripped)
        {
            YamlReference.TryFind(
                stripped.GetValue("m_CorrespondingSourceObject"),
                out var source);

            YamlReference.TryFind(
                stripped.GetValue("m_PrefabInstance"),
                out var instance);

            return (instance.FileId
                  ^ source.FileId)
                 & long.MaxValue;
        }

        private static void ReplaceLocalReferences(
            UnityYamlFile file,
            long oldFileId,
            long newFileId)
        {
            var oldReference = YamlReference.Local(oldFileId);
            var newReference = YamlReference.Local(newFileId);

            foreach (var document in file.Documents)
            {
                for (var index = 0; index < document.Lines.Count; index++)
                {
                    document.Lines[index] =
                        document.Lines[index].Replace(
                            oldReference,
                            newReference);
                }
            }
        }

        private static void Renumber(
            UnityYamlFile file,
            UnityYamlDocument stripped,
            long newFileId)
        {
            ReplaceLocalReferences(
                file,
                stripped.FileId,
                newFileId);

            var index = file.Documents.IndexOf(stripped);

            // Если заглушка нового объекта уже есть, старая просто убирается: ссылки уже переведены на неё.
            if (file.Find(newFileId) != null)
            {
                file.Documents.RemoveAt(index);

                return;
            }

            file.Documents[index] =
                new UnityYamlDocument(
                    stripped.ClassId,
                    newFileId,
                    true,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "--- !u!{0} &{1} stripped",
                        stripped.ClassId,
                        newFileId),
                    stripped.Lines);
        }

        /// <summary>
        /// Переводит ссылки на переименованные объекты.
        /// </summary>
        /// <param name="file">Разобранный файл; меняется на месте.</param>
        /// <param name="targets">Сведения об объектах основы.</param>
        /// <returns><c>true</c>, если что-то изменено.</returns>
        public static bool Apply(
            UnityYamlFile file,
            ILostTargets targets)
        {
            var changed = false;
            var moved = new List<UnityYamlDocument>();

            foreach (var document in file.Documents)
            {
                for (var index = 0; index < document.Lines.Count; index++)
                {
                    var line = document.Lines[index];

                    var retargeted =
                        Retarget(
                            line,
                            targets);

                    if (string.Equals(
                            line,
                            retargeted,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    document.Lines[index] = retargeted;
                    changed = true;

                    if (document.IsStripped
                     && line.StartsWith(
                            SourceLinePrefix,
                            StringComparison.Ordinal))
                    {
                        moved.Add(document);
                    }
                }
            }

            foreach (var document in moved)
            {
                Renumber(
                    file,
                    document,
                    GetExpectedFileId(document));
            }

            return changed;
        }
    }
}
