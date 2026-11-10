using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PaperFly.RenderKit.Versions
{
    /// <summary>
    /// Коммит из истории репозитория локации.
    /// </summary>
    internal sealed class CommitInfo
    {
        /// <summary>
        /// Формат git log, который разбирает <see cref="Parse"/>. Поля разделены символом 0x1F.
        /// </summary>
        public const string LogFormat = "%H%x1f%aI%x1f%an%x1f%D%x1f%s";

        private const char FieldSeparator = '\u001f';

        private const int FieldCount = 5;

        private const string TagPrefix = "tag: ";

        private const string HeadPointerPrefix = "HEAD -> ";

        private const string Head = "HEAD";

        private CommitInfo(
            string hash,
            DateTimeOffset date,
            string author,
            IReadOnlyCollection<string> references,
            string subject)
        {
            this.Hash = hash;
            this.Date = date;
            this.Author = author;
            this.Subject = subject;

            this.HasTag =
                references.Any(reference => reference.StartsWith(
                    TagPrefix,
                    StringComparison.Ordinal));

            this.Labels =
                string.Join(
                    ", ",
                    references.Select(reference => reference.Replace(
                        TagPrefix,
                        string.Empty,
                        StringComparison.Ordinal)));
        }

        /// <summary>
        /// Полный хеш.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// Короткий хеш.
        /// </summary>
        public string ShortHash
            => this.Hash.Substring(
                0,
                Constants.ShortHashLength);

        /// <summary>
        /// Дата создания коммита.
        /// </summary>
        public DateTimeOffset Date { get; }

        /// <summary>
        /// Автор.
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// Первая строка сообщения.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// На коммите есть тег, то есть это выпущенная версия.
        /// </summary>
        public bool HasTag { get; }

        /// <summary>
        /// Теги и ветки, указывающие на коммит.
        /// </summary>
        public string Labels { get; }

        /// <summary>
        /// Разбирает строку вывода git log.
        /// </summary>
        /// <param name="line">Строка в формате <see cref="LogFormat"/>.</param>
        /// <returns>Коммит.</returns>
        public static CommitInfo Parse(string line)
        {
            var fields =
                line.Split(
                    FieldSeparator,
                    FieldCount);

            var date =
                DateTimeOffset.Parse(
                    fields[1],
                    CultureInfo.InvariantCulture);

            return new CommitInfo(
                fields[0],
                date,
                fields[2],
                ParseReferences(fields[3]),
                fields[4].TrimEnd('\r'));
        }

        // Вид поля: "HEAD -> master, tag: 1.0.0, test-branch". HEAD сам по себе версию не описывает.
        private static List<string> ParseReferences(string references)
            => references
                .Split(
                    ", ",
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(reference => reference.Replace(
                    HeadPointerPrefix,
                    string.Empty,
                    StringComparison.Ordinal))
                .Where(reference => !string.Equals(
                    reference,
                    Head,
                    StringComparison.Ordinal))
                .ToList();
    }
}
