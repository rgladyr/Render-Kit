using System.Globalization;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Ссылка Unity YAML вида {fileID: 123, guid: abc, type: 3}.
    /// </summary>
    internal readonly struct YamlReference
    {
        private static readonly Regex Pattern =
            new Regex(
                @"\{fileID: (?<fileId>-?\d+)(?:, guid: (?<guid>[0-9a-f]{32}), type: \d+)?\}",
                RegexOptions.Compiled);

        private YamlReference(
            long fileId,
            string guid)
        {
            this.FileId = fileId;
            this.Guid = guid;
        }

        /// <summary>
        /// Идентификатор объекта.
        /// </summary>
        public long FileId { get; }

        /// <summary>
        /// GUID файла; пустая строка для ссылки внутри того же файла.
        /// </summary>
        public string Guid { get; }

        /// <summary>
        /// Находит первую ссылку в строке.
        /// </summary>
        /// <param name="text">Строка YAML.</param>
        /// <param name="reference">Найденная ссылка.</param>
        /// <returns><c>false</c>, если ссылки в строке нет.</returns>
        public static bool TryFind(
            string text,
            out YamlReference reference)
        {
            var match = Pattern.Match(text ?? string.Empty);

            reference =
                match.Success
                    ? new YamlReference(
                        long.Parse(
                            match.Groups["fileId"].Value,
                            CultureInfo.InvariantCulture),
                        match.Groups["guid"].Value)
                    : default;

            return match.Success;
        }

        /// <summary>
        /// Ссылка на объект внутри того же файла.
        /// </summary>
        /// <param name="fileId">Идентификатор объекта.</param>
        /// <returns>Текст ссылки.</returns>
        public static string Local(long fileId)
            => string.Format(
                CultureInfo.InvariantCulture,
                "{{fileID: {0}}}",
                fileId);

        /// <summary>
        /// Ссылка на объект другого файла.
        /// </summary>
        /// <param name="fileId">Идентификатор объекта.</param>
        /// <param name="guid">GUID файла.</param>
        /// <returns>Текст ссылки.</returns>
        public static string External(
            long fileId,
            string guid)
            => string.Format(
                CultureInfo.InvariantCulture,
                "{{fileID: {0}, guid: {1}, type: 3}}",
                fileId,
                guid);
    }
}
