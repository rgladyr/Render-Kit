using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFly.RenderKit.Acceptance.Rescue
{
    /// <summary>
    /// Файл сцены или префаба в формате Unity YAML, разобранный на документы-объекты.
    /// </summary>
    /// <remarks>
    /// Разбирается только то, что нужно для переноса: заголовки документов и строки верхнего уровня.
    /// Строки, которые не менялись, записываются обратно без изменений.
    /// </remarks>
    internal sealed class UnityYamlFile
    {
        private const string DocumentPrefix = "--- !u!";

        private static readonly Regex HeaderPattern =
            new Regex(
                @"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)(?<stripped> stripped)?",
                RegexOptions.Compiled);

        private readonly List<string> preamble;

        private readonly string newLine;

        private readonly bool endsWithNewLine;

        private UnityYamlFile(
            List<string> preamble,
            List<UnityYamlDocument> documents,
            string newLine,
            bool endsWithNewLine)
        {
            this.preamble = preamble;
            this.Documents = documents;
            this.newLine = newLine;
            this.endsWithNewLine = endsWithNewLine;
        }

        /// <summary>
        /// Документы файла в исходном порядке.
        /// </summary>
        public List<UnityYamlDocument> Documents { get; }

        private static UnityYamlDocument CreateDocument(
            string header,
            List<string> lines)
        {
            var match = HeaderPattern.Match(header);

            return new UnityYamlDocument(
                int.Parse(
                    match.Groups["classId"].Value,
                    CultureInfo.InvariantCulture),
                long.Parse(
                    match.Groups["fileId"].Value,
                    CultureInfo.InvariantCulture),
                match.Groups["stripped"].Success,
                header,
                lines);
        }

        /// <summary>
        /// Разбирает текст файла.
        /// </summary>
        /// <param name="text">Содержимое файла сцены или префаба.</param>
        /// <returns>Разобранный файл.</returns>
        public static UnityYamlFile Parse(string text)
        {
            var newLine =
                text.Contains(
                    "\r\n",
                    StringComparison.Ordinal)
                    ? "\r\n"
                    : "\n";

            var endsWithNewLine =
                text.EndsWith(
                    newLine,
                    StringComparison.Ordinal);

            var body =
                endsWithNewLine
                    ? text.Substring(
                        0,
                        text.Length - newLine.Length)
                    : text;

            var preamble = new List<string>();
            var documents = new List<UnityYamlDocument>();

            string header = null;
            var lines = new List<string>();

            foreach (var line in body.Split(
                         new[] { newLine },
                         StringSplitOptions.None))
            {
                if (!line.StartsWith(
                        DocumentPrefix,
                        StringComparison.Ordinal))
                {
                    (header == null
                        ? preamble
                        : lines).Add(line);

                    continue;
                }

                if (header != null)
                {
                    documents.Add(
                        CreateDocument(
                            header,
                            lines));
                }

                header = line;
                lines = new List<string>();
            }

            if (header != null)
            {
                documents.Add(
                    CreateDocument(
                        header,
                        lines));
            }

            return new UnityYamlFile(
                preamble,
                documents,
                newLine,
                endsWithNewLine);
        }

        /// <summary>
        /// Находит документ по идентификатору объекта в файле.
        /// </summary>
        /// <param name="fileId">Идентификатор объекта.</param>
        /// <returns>Документ; <c>null</c>, если его нет.</returns>
        public UnityYamlDocument Find(long fileId)
            => this.Documents.FirstOrDefault(document => document.FileId == fileId);

        /// <summary>
        /// Создаёт идентификатор, которого ещё нет в файле.
        /// </summary>
        /// <returns>Положительный идентификатор.</returns>
        public long CreateFileId()
        {
            while (true)
            {
                var fileId =
                    BitConverter.ToInt64(
                        Guid.NewGuid().ToByteArray(),
                        0)
                  & long.MaxValue;

                if (fileId != 0
                 && this.Find(fileId) == null)
                {
                    return fileId;
                }
            }
        }

        /// <summary>
        /// Собирает текст файла.
        /// </summary>
        /// <returns>Содержимое файла с исходными переводами строк.</returns>
        public string ToText()
        {
            var lines =
                this.preamble
                    .Concat(this.Documents.SelectMany(document => document.GetAllLines()));

            var text =
                string.Join(
                    this.newLine,
                    lines);

            return this.endsWithNewLine
                ? text + this.newLine
                : text;
        }
    }
}
